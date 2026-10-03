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
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The inbound SMTP session over raw sockets: line endings (RFC 5321 §2.3.8, SMTP
    /// smuggling) and BDAT chunk consumption (RFC 3030 §2).
    ///
    /// Raw sockets because what is under test is which bytes end a line and which bytes
    /// are data; any SMTP client library would send only the well-formed versions.
    /// </summary>
    [TestFixture]
    public partial class SMTPSessionWireTests
    {

        #region Test doubles and the server

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

        private sealed class MemoryStorage : IMailStorage
        {

            public readonly ConcurrentQueue<String>                                      Messages  = new();
            public readonly ConcurrentQueue<(String From, IReadOnlyList<String> To)>     Envelopes = new();

            public Task<String> StoreAsync(EMailMessage message, String envelopeFrom, IEnumerable<String> envelopeTo, CancellationToken ct = default)
            {
                Messages.Enqueue(message.RawMessage);
                Envelopes.Enqueue((envelopeFrom, [.. envelopeTo]));
                return Task.FromResult("memory");
            }

        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private sealed class Server : IAsyncDisposable
        {

            public readonly MemoryStorage  Storage = new();
            public readonly UInt16         Port;

            private readonly SMTPServer    server;
            private readonly Task          running;
            private readonly String        directory;

            public Server(Boolean RejectBareLineEndings = true, Int32 MaxMessageSize = 64 * 1024)
            {

                directory  = Directory.CreateTempSubdirectory("hermod-smtp-").FullName;

                var ports  = new[] { FreePort(), FreePort() };
                Port       = ports[0];

                server     = new SMTPServer(new SMTPServerConfig {
                                                Hostname               = "mx.hermod.test",
                                                Port                   = ports[0],
                                                SubmissionPort         = ports[1],
                                                EnableImplicitTls      = false,
                                                MailStoragePath        = directory,
                                                LocalDomains           = [ "hermod.test" ],
                                                MaxMessageSize         = MaxMessageSize,
                                                RejectBareLineEndings  = RejectBareLineEndings,
                                                SessionTimeout         = TimeSpan.FromSeconds(10)
                                            },
                                            new NoDNS(),
                                            new QuietLogger(),
                                            mailStorage: Storage);

                // Start() runs the accept loops for the server's lifetime.
                running    = server.Start();

            }

            private static UInt16 FreePort()
            {
                var listener = new TcpListener(System.Net.IPAddress.Any, 0);
                listener.Start();
                var port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }

            public async Task<Wire> ConnectAsync()
            {

                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        var wire = new Wire(new TcpClient("127.0.0.1", Port));
                        Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(220));
                        return wire;
                    }
                    catch (SocketException) when (attempt < 50)
                    {
                        await Task.Delay(20);
                    }
                }

            }

            public async ValueTask DisposeAsync()
            {
                try { await server.DisposeAsync(); } catch { }
                try { await running.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                try { Directory.Delete(directory, recursive: true); } catch { }
            }

        }

        /// <summary>
        /// A raw client: writes exactly the given bytes, reads complete replies.
        /// </summary>
        private sealed class Wire(TcpClient client) : IDisposable
        {

            private readonly NetworkStream  stream  = client.GetStream();
            private readonly StringBuilder  pending = new();

            public Task SendAsync(String text)
                => stream.WriteAsync(Encoding.ASCII.GetBytes(text)).AsTask();

            public Task SendAsync(Byte[] bytes)
                => stream.WriteAsync(bytes).AsTask();

            /// <summary>
            /// The next complete reply, or null when none arrives within the timeout.
            /// </summary>
            public async Task<(Int32 Code, String Text)> ReplyAsync(TimeSpan? Timeout = null)
                => await TryReplyAsync(Timeout) ?? throw new TimeoutException("No reply.");

            public async Task<(Int32 Code, String Text)?> TryReplyAsync(TimeSpan? Timeout = null)
            {

                using var cts = new CancellationTokenSource(Timeout ?? TimeSpan.FromSeconds(5));
                var buffer    = new Byte[4096];

                while (true)
                {

                    var text = pending.ToString();
                    var end  = text.IndexOf("\r\n", StringComparison.Ordinal);

                    while (end >= 0)
                    {
                        var line = text[..end];
                        if (line.Length < 4 || line[3] != '-')
                        {
                            pending.Remove(0, text.IndexOf("\r\n", StringComparison.Ordinal) + 2);
                            return (Int32.Parse(line[..3]), line.Length > 4 ? line[4..] : "");
                        }
                        pending.Remove(0, end + 2);
                        text = pending.ToString();
                        end  = text.IndexOf("\r\n", StringComparison.Ordinal);
                    }

                    Int32 read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }

                    if (read == 0)
                        return null;

                    pending.Append(Encoding.ASCII.GetString(buffer, 0, read));

                }

            }

            /// <summary>
            /// Every reply that arrives within the window.
            /// </summary>
            public async Task<List<Int32>> DrainAsync(TimeSpan Window)
            {
                var codes = new List<Int32>();
                while (await TryReplyAsync(Window) is { } reply)
                    codes.Add(reply.Code);
                return codes;
            }

            public async Task<Int32> CommandAsync(String line)
            {
                await SendAsync(line + "\r\n");
                return (await ReplyAsync()).Code;
            }

            public void Dispose()
                => client.Dispose();

        }

        private static async Task OpenTransaction(Wire wire)
        {
            Assert.That(await wire.CommandAsync("EHLO client.example"),              Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),       Is.EqualTo(250));
        }

        #endregion


        #region Line endings (RFC 5321 §2.3.8)

        // The ambiguous end-of-data forms SMTP smuggling uses.
        private static readonly String[] AmbiguousTerminators = [ "\n.\n", "\n.\r\n", "\r\n.\n", "\r.\r", "\r.\r\n", "\r\n.\r" ];


        [TestCaseSource(nameof(AmbiguousTerminators))]
        public async Task An_ambiguous_end_of_data_does_not_end_the_message(String Terminator)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));

            await wire.SendAsync("Subject: outer\r\n\r\nfirst" + Terminator +
                                 "MAIL FROM:<smuggled@victim.example>\r\nRCPT TO:<alice@hermod.test>\r\nDATA\r\n" +
                                 "Subject: smuggled\r\n\r\nsecond\r\n.\r\n");

            // One end of data, one reply: the bare CR/LF makes the message unacceptable by
            // default, and the commands after it were content, so none of them is answered.
            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 550 }));
            Assert.That(server.Storage.Messages,                        Is.Empty);

        }


        [TestCaseSource(nameof(AmbiguousTerminators))]
        public async Task Normalized_bare_line_endings_still_leave_one_message(String Terminator)
        {

            await using var server = new Server(RejectBareLineEndings: false);
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));

            await wire.SendAsync("Subject: outer\r\n\r\nfirst" + Terminator +
                                 "MAIL FROM:<smuggled@victim.example>\r\nRCPT TO:<alice@hermod.test>\r\nDATA\r\n" +
                                 "Subject: smuggled\r\n\r\nsecond\r\n.\r\n");

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 250 }));
            Assert.That(server.Storage.Messages, Has.Count.EqualTo(1));

            var stored = server.Storage.Messages.Single();

            // The would-be second transaction is content of the first, and every line of it
            // ends in CR LF now.
            Assert.That(stored, Does.Contain("MAIL FROM:<smuggled@victim.example>\r\n"));
            Assert.That(stored.Replace("\r\n", ""), Does.Not.Contain('\r').And.Not.Contain('\n'));

        }


        [TestCase("NOOP\nNOOP\r\n")]
        [TestCase("NOOP\rNOOP\r\n")]
        public async Task A_bare_CR_or_LF_does_not_end_a_command(String Bytes)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await wire.SendAsync(Bytes);

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 500 }),
                        "one line, so one reply — and that line is not a valid command");

        }


        [Test]
        public async Task A_dot_line_inside_DATA_still_ends_the_message()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));

            await wire.SendAsync("Subject: plain\r\n\r\n..stuffed\r\nbody\r\n.\r\nNOOP\r\n");

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 250, 250 }));
            Assert.That(server.Storage.Messages.Single(), Does.EndWith("\r\n\r\n.stuffed\r\nbody\r\n"));

        }


        [Test]
        public async Task A_connection_lost_during_DATA_delivers_nothing()
        {

            await using var server = new Server();

            using (var wire = await server.ConnectAsync())
            {
                await OpenTransaction(wire);
                Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
                await wire.SendAsync("Subject: cut off\r\n\r\nhalf a message\r\n");
            }

            await Task.Delay(500);

            Assert.That(server.Storage.Messages, Is.Empty, "a transaction without its end of data must not be delivered");

        }

        #endregion

        #region BDAT chunks (RFC 3030 §2)

        private static Byte[] Bdat(String Payload, Boolean Last, Int32? AnnouncedSize = null)
        {
            var bytes = Encoding.ASCII.GetBytes(Payload);
            return [ .. Encoding.ASCII.GetBytes($"BDAT {AnnouncedSize ?? bytes.Length}{(Last ? " LAST" : "")}\r\n"), .. bytes ];
        }


        [Test]
        public async Task A_BDAT_refused_for_its_sequence_still_consumes_its_chunk()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            // No MAIL, no RCPT: 503 — and the chunk, two valid commands, is data to discard.
            await wire.SendAsync(Bdat("NOOP\r\nNOOP\r\n", Last: true));

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 503 }));
            Assert.That(await wire.CommandAsync("NOOP"),               Is.EqualTo(250), "the session is still in step");

        }


        [Test]
        public async Task A_BDAT_refused_for_its_size_still_consumes_its_chunk()
        {

            await using var server = new Server(MaxMessageSize: 1024);
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            var payload = "NOOP\r\nNOOP\r\n" + new String('x', 2000);
            await wire.SendAsync(Bdat(payload, Last: true));

            Assert.That(await wire.DrainAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new[] { 552 }));
            Assert.That(await wire.CommandAsync("NOOP"),               Is.EqualTo(250));

        }


        [Test]
        public async Task BDAT_chunks_are_delivered_verbatim()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            await wire.SendAsync(Bdat("Subject: chunked\r\n\r\n.\r\n", Last: false));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            await wire.SendAsync(Bdat("bare\nLF is binary-safe here\r\n", Last: true));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages.Single(), Does.EndWith("\r\n\r\n.\r\nbare\nLF is binary-safe here\r\n"));

        }

        #endregion

    }

}
