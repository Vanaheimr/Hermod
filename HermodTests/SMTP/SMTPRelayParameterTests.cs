/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What a relayed message carries from the inbound session to the next hop:
    /// REQUIRETLS (RFC 8689 §5) and the DSN parameters ENVID, RET, NOTIFY and ORCPT
    /// (RFC 3461 §5.2.1). Both halves are tested - the session filling the relay queue,
    /// and the outbound client putting the parameters on the wire - because a value
    /// that survives one half and not the other is just as lost.
    /// </summary>
    [TestFixture]
    public class SMTPRelayParameterTests
    {

        #region Test doubles

        private sealed class NoDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private static Task<DNSInfo> Empty()
                => Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                               [], [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Empty();

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Empty();

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            public override String ToString()
                => "no DNS";

        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private sealed class NullStorage : IMailStorage
        {
            public Task<String> StoreAsync(EMailMessage message, String envelopeFrom, IEnumerable<String> envelopeTo, CancellationToken ct = default)
                => Task.FromResult("discarded");
        }

        private sealed class RecordingQueue : IMailQueue
        {

            public readonly ConcurrentQueue<QueuedMail> Queued = new();

            private readonly Channel<QueuedMail> newMail = Channel.CreateUnbounded<QueuedMail>();
            private readonly Channel<Boolean>    retry   = Channel.CreateUnbounded<Boolean>();

            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
            {
                Queued.Enqueue(mail);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(Int32 maxItems = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public Task<QueuedMail?> GetByIdAsync(String id, CancellationToken ct = default)                          => Task.FromResult<QueuedMail?>(null);
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default)                                   => Task.CompletedTask;
            public Task RemoveAsync(String id, CancellationToken ct = default)                                         => Task.CompletedTask;
            public Task<Int32> GetQueueLengthAsync(CancellationToken ct = default)                                     => Task.FromResult(Queued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(Int32 maxItems = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader    => newMail.Reader;
            public void SignalRetryCheck()                    => retry.Writer.TryWrite(true);
            public ChannelReader<Boolean>    RetryCheckReader => retry.Reader;

        }

        private static UInt16 FreePort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Any, 0);
            listener.Start();
            var port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion

        #region A raw SMTP client that can do STARTTLS

        private sealed class Client(TcpClient tcp) : IDisposable
        {

            private Stream        stream = tcp.GetStream();
            private StreamReader  reader = new (tcp.GetStream(), Encoding.ASCII);

            public async Task<Int32> ReplyAsync()
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))
                                   ?? throw new IOException("connection closed");
                    if (line.Length < 4 || line[3] != '-')
                        return Int32.Parse(line[..3]);
                }
            }

            public async Task<Int32> CommandAsync(String line)
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\r\n"));
                return await ReplyAsync();
            }

            public async Task StartTlsAsync()
            {
                Assert.That(await CommandAsync("STARTTLS"), Is.EqualTo(220));
                var tls = new SslStream(stream, false, (_, _, _, _) => true);
                await tls.AuthenticateAsClientAsync("localhost");
                stream = tls;
                reader = new StreamReader(tls, Encoding.ASCII);
            }

            public void Dispose()
                => tcp.Dispose();

        }

        #endregion


        #region Inbound: the session fills the relay queue

        [Test]
        public async Task A_relayed_message_keeps_REQUIRETLS_and_its_DSN_parameters()
        {

            var directory = Directory.CreateTempSubdirectory("hermod-relay-").FullName;
            var pfx       = Path.Combine(directory, "server.pfx");

            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pfx, "test"));
            }

            var queue  = new RecordingQueue();
            var port   = FreePort();
            var server = new SMTPServer(new SMTPServerConfig {
                                            Hostname             = "mx.hermod.test",
                                            Port                 = port,
                                            SubmissionPort       = FreePort(),
                                            EnableImplicitTls    = false,
                                            MailStoragePath      = directory,
                                            CertificatePath      = pfx,
                                            CertificatePassword  = "test",
                                            LocalDomains         = [ "hermod.test" ],
                                            RequireAuthForRelay  = false        // relay without the AUTH ceremony
                                        },
                                        new NoDNS(),
                                        new QuietLogger(),
                                        mailQueue:   queue,
                                        mailStorage: new NullStorage());

            var running = server.Start();

            try
            {

                TcpClient? tcp = null;
                for (var attempt = 0; tcp is null; attempt++)
                {
                    try                                          { tcp = new TcpClient("127.0.0.1", port); }
                    catch (SocketException) when (attempt < 50) { await Task.Delay(20); }
                }

                using var client = new Client(tcp);

                Assert.That(await client.ReplyAsync(),                     Is.EqualTo(220));
                Assert.That(await client.CommandAsync("EHLO client.example"), Is.EqualTo(250));
                await client.StartTlsAsync();
                Assert.That(await client.CommandAsync("EHLO client.example"), Is.EqualTo(250));

                Assert.That(await client.CommandAsync("MAIL FROM:<sender@client.example> REQUIRETLS RET=HDRS ENVID=QQ314159"),            Is.EqualTo(250));
                Assert.That(await client.CommandAsync("RCPT TO:<bob@elsewhere.example> NOTIFY=SUCCESS,FAILURE ORCPT=rfc822;bob@original.example"), Is.EqualTo(250));
                Assert.That(await client.CommandAsync("RCPT TO:<carol@elsewhere.example> NOTIFY=NEVER"),                                     Is.EqualTo(250));
                Assert.That(await client.CommandAsync("RCPT TO:<dave@other.example>"),                                                       Is.EqualTo(250));
                Assert.That(await client.CommandAsync("DATA"),                                                                               Is.EqualTo(354));
                Assert.That(await client.CommandAsync("Subject: relay\r\n\r\nbody\r\n."),                                                   Is.EqualTo(250));

                // One queue entry per target domain, each with only its own recipients.
                var elsewhere = queue.Queued.Single(q => q.TargetDomain == "elsewhere.example");
                var other     = queue.Queued.Single(q => q.TargetDomain == "other.example");

                Assert.Multiple(() => {

                    Assert.That(elsewhere.RequireTls, Is.True,                     "REQUIRETLS (RFC 8689 §5)");
                    Assert.That(elsewhere.EnvId,      Is.EqualTo("QQ314159"),      "ENVID (RFC 3461 §5.2.1)");
                    Assert.That(elsewhere.Ret,        Is.EqualTo(DsnRet.Hdrs),     "RET (RFC 3461 §5.2.1)");

                    Assert.That(elsewhere.RecipientDsns.Select(r => (r.Recipient, r.Notify, r.OriginalRecipient)),
                                Is.EqualTo(new[] {
                                    ("bob@elsewhere.example",   DsnNotify.Success | DsnNotify.Failure, (String?) "rfc822;bob@original.example"),
                                    ("carol@elsewhere.example", DsnNotify.Never,                       (String?) null)
                                }),
                                "NOTIFY and ORCPT per recipient (RFC 3461 §5.2.1)");

                    Assert.That(other.RequireTls,                                   Is.True);
                    Assert.That(other.RecipientDsns.Select(r => r.Recipient),       Is.EqualTo(new[] { "dave@other.example" }));

                });

            }
            finally
            {
                try { await server.DisposeAsync(); }                    catch { }
                try { await running.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                try { Directory.Delete(directory, recursive: true); }   catch { }
            }

        }

        #endregion

        #region Outbound: the relay client puts them on the wire

        /// <summary>
        /// A next hop that advertises DSN and records MAIL and RCPT as received.
        /// </summary>
        private sealed class NextHop : IDisposable
        {

            private readonly TcpListener            listener = new (System.Net.IPAddress.Loopback, 0);

            public readonly ConcurrentQueue<String> Commands = new();

            public UInt16 Port
                => (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;

            public NextHop()
            {
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    await writer.WriteLineAsync("220 next-hop.test ESMTP");

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 2.0.0 queued"); inData = false; }
                            continue;
                        }

                        Commands.Enqueue(line);
                        var upper = line.ToUpperInvariant();

                        if      (upper.StartsWith("EHLO")) { await writer.WriteLineAsync("250-next-hop.test"); await writer.WriteLineAsync("250 DSN"); }
                        else if (upper == "DATA")          { await writer.WriteLineAsync("354 go ahead"); inData = true; }
                        else if (upper == "QUIT")          { await writer.WriteLineAsync("221 2.0.0 bye"); return; }
                        else                                 await writer.WriteLineAsync("250 2.0.0 ok");

                    }

                }
                catch
                {
                    // the client hung up
                }
            }

            public void Dispose()
                => listener.Stop();

        }


        [Test]
        public async Task The_relay_client_sends_each_recipient_its_own_NOTIFY_and_ORCPT()
        {

            using var nextHop = new NextHop();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname   = "mx.hermod.test",
                                                    SmartHost       = "127.0.0.1",
                                                    SmartHostPort   = nextHop.Port,
                                                    PreferStartTls  = false,
                                                    ConnectTimeoutMs = 3000,
                                                    ReadTimeoutMs    = 3000
                                                },
                                                null,
                                                new NoDNS(),
                                                new QuietLogger());

            var result = await client.SendAsync(
                                   "elsewhere.example",
                                   "sender@client.example",
                                   [ "bob@elsewhere.example", "carol@elsewhere.example" ],
                                   "Subject: relay\r\n\r\nbody\r\n",
                                   requireTls:    false,
                                   dsn:           new DsnParameters(DsnNotify.Success | DsnNotify.Failure, DsnRet.Hdrs, "QQ314159"),
                                   recipientDsns: [
                                       new RecipientDsn { Recipient = "bob@elsewhere.example",   Notify = DsnNotify.Success | DsnNotify.Failure, OriginalRecipient = "rfc822;bob@original.example" },
                                       new RecipientDsn { Recipient = "carol@elsewhere.example", Notify = DsnNotify.Never }
                                   ]
                               );

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

            var commands = nextHop.Commands.ToArray();

            Assert.That(commands, Does.Contain("MAIL FROM:<sender@client.example> RET=HDRS ENVID=QQ314159"));
            Assert.That(commands, Does.Contain("RCPT TO:<bob@elsewhere.example> NOTIFY=SUCCESS,FAILURE ORCPT=rfc822;bob@original.example"),
                        "the received ORCPT is passed on unchanged");
            Assert.That(commands, Does.Contain("RCPT TO:<carol@elsewhere.example> NOTIFY=NEVER ORCPT=rfc822;carol@elsewhere.example"),
                        "NOTIFY=NEVER was the client's choice and is passed on");

        }

        #endregion

    }

}
