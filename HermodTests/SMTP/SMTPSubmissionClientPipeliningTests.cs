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
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// PIPELINING (RFC 2920) and CHUNKING (RFC 3030), used when offered: MAIL and the RCPTs as
    /// one group, the message as one BDAT ... LAST.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientPipeliningTests
    {

        /// <summary>
        /// A byte-exact server. Before it answers MAIL it waits briefly and notes how many RCPTs
        /// arrived in the meantime - a client that pipelines has sent them all.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener               listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly String[]                  extensions;
            private readonly Func<String, String>      reply;

            public readonly ConcurrentQueue<String>    Commands   = new();
            public          Int32                      RcptsBeforeMailReply;
            public          Byte[]?                    BdatContent;

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            /// <param name="Reply">The reply to a command line, or null for the default.</param>
            /// <param name="Extensions">The EHLO keywords.</param>
            public Server(Func<String, String?>? Reply = null, params String[] Extensions)
            {
                extensions = Extensions;
                reply      = line => Reply?.Invoke(line) ?? Default(line);
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private static String Default(String Line)
                => Line.Split(' ')[0].ToUpperInvariant() switch {
                       "MAIL"  => "250 2.1.0 ok",
                       "RCPT"  => "250 2.1.5 ok",
                       "DATA"  => "354 go ahead",
                       "BDAT"  => "250 2.0.0 queued",
                       "."     => "250 2.0.0 queued",
                       "QUIT"  => "221 2.0.0 bye",
                       _       => "250 2.0.0 ok"
                   };

            private async Task ServeAsync()
            {
                try
                {

                    using var client  = await listener.AcceptTcpClientAsync();
                    using var stream  = client.GetStream();
                    var       reader  = new LineReader(stream);

                    Task Write(String Text)
                        => stream.WriteAsync(Encoding.ASCII.GetBytes(Text + "\r\n")).AsTask();

                    await Write("220 pipe.test ESMTP");

                    var pending = new Queue<String>();

                    while (true)
                    {

                        var line = pending.Count > 0 ? pending.Dequeue() : await reader.ReadLineAsync(TimeSpan.FromSeconds(5));
                        if (line is null)
                            return;

                        Commands.Enqueue(line);
                        var verb = line.Split(' ')[0].ToUpperInvariant();

                        switch (verb)
                        {

                            case "EHLO":
                                await Write(extensions.Length == 0 ? "250 pipe.test" : "250-pipe.test");
                                for (var i = 0; i < extensions.Length; i++)
                                    await Write((i == extensions.Length - 1 ? "250 " : "250-") + extensions[i]);
                                break;

                            case "MAIL":
                                // Whatever comes within the next moment was sent without waiting for this reply.
                                while (await reader.ReadLineAsync(TimeSpan.FromMilliseconds(400)) is { } early)
                                    pending.Enqueue(early);
                                RcptsBeforeMailReply = pending.Count(command => command.StartsWith("RCPT", StringComparison.OrdinalIgnoreCase));
                                await Write(reply(line));
                                break;

                            case "DATA":
                                var dataReply = reply(line);
                                await Write(dataReply);
                                if (dataReply.StartsWith('3'))
                                {
                                    while (await reader.ReadLineAsync(TimeSpan.FromSeconds(5)) is { } dataLine && dataLine != ".")
                                    { }
                                    await Write(reply("."));
                                }
                                break;

                            case "BDAT":
                                var size     = Int32.Parse(line.Split(' ')[1]);
                                BdatContent  = await reader.ReadExactlyAsync(size, TimeSpan.FromSeconds(5));
                                await Write(reply(line));
                                break;

                            case "QUIT":
                                await Write(reply(line));
                                return;

                            default:
                                await Write(reply(line));
                                break;

                        }

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

        /// <summary>
        /// Lines end at CR LF; a timed-out read returns null and keeps what it had.
        /// </summary>
        private sealed class LineReader(Stream Stream)
        {

            private readonly List<Byte>  buffer = [];
            private          Task<Int32>? pendingRead;
            private readonly Byte[]      chunk  = new Byte[4096];

            private async Task<Boolean> FillAsync(TimeSpan Timeout)
            {
                pendingRead ??= Stream.ReadAsync(chunk).AsTask();
                if (await Task.WhenAny(pendingRead, Task.Delay(Timeout)) != pendingRead)
                    return false;
                var read     = await pendingRead;
                pendingRead  = null;
                if (read == 0)
                    throw new EndOfStreamException();
                buffer.AddRange(chunk[..read]);
                return true;
            }

            public async Task<String?> ReadLineAsync(TimeSpan Timeout)
            {
                while (true)
                {
                    for (var i = 1; i < buffer.Count; i++)
                        if (buffer[i - 1] == '\r' && buffer[i] == '\n')
                        {
                            var line = Encoding.UTF8.GetString([.. buffer[..(i - 1)]]);
                            buffer.RemoveRange(0, i + 1);
                            return line;
                        }
                    if (!await FillAsync(Timeout))
                        return null;
                }
            }

            public async Task<Byte[]> ReadExactlyAsync(Int32 Count, TimeSpan Timeout)
            {
                while (buffer.Count < Count)
                    if (!await FillAsync(Timeout))
                        throw new TimeoutException();
                var octets = buffer[..Count].ToArray();
                buffer.RemoveRange(0, Count);
                return octets;
            }

        }


        private static async Task<SMTPSendResult> Send(Server Server, params String[] Recipients)
        {

            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        IPPort.Parse(Server.Port),
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(5));

            var mail = EMail.Parse([
                           "From: app@client.example",
                           "To: " + String.Join(", ", Recipients.DefaultIfEmpty("you@pipe.test")),
                           "Subject: pipelining",
                           "Content-Type: text/plain; charset=utf-8",
                           "",
                           "line one",
                           ".stuffed",
                           "Grüße"
                       ]);

            var envelope = new EMailEnvelop(mail);

            return await client.SendWithResult(envelope, NumberOfRetries: 0);

        }


        [Test]
        public async Task With_PIPELINING_MAIL_and_every_RCPT_go_out_together()
        {

            using var server = new Server(null, "PIPELINING", "8BITMIME", "SIZE 1000000");

            var result = await Send(server, "a@pipe.test", "b@pipe.test", "c@pipe.test");

            Assert.That(result.Status,                 Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.RcptsBeforeMailReply,   Is.EqualTo(3), "all three RCPTs follow MAIL without waiting for its reply");
            Assert.That(result.Recipients,             Has.Count.EqualTo(3));

        }


        [Test]
        public async Task Without_PIPELINING_the_client_waits_for_each_reply()
        {

            using var server = new Server(null, "8BITMIME", "SIZE 1000000");

            var result = await Send(server, "a@pipe.test", "b@pipe.test");

            Assert.That(result.Status,                Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.RcptsBeforeMailReply,  Is.Zero);

        }


        /// <summary>
        /// Each pipelined reply belongs to its command: a refused RCPT in the middle is reported
        /// for that recipient, and the transaction is not continued.
        /// </summary>
        [Test]
        public async Task A_refused_RCPT_in_a_pipelined_group_is_its_own_and_ends_the_transaction()
        {

            using var server = new Server(line => line.Contains("<b@pipe.test>") ? "550 5.1.1 no such user" : null,
                                          "PIPELINING", "8BITMIME", "SIZE 1000000");

            var result = await Send(server, "a@pipe.test", "b@pipe.test", "c@pipe.test");

            Assert.That(result.Status, Is.Not.EqualTo(MailSentStatus.ok));
            Assert.That(result.Recipients.Select(recipient => (UInt16) recipient.StatusCode),
                        Is.EqualTo(new UInt16[] { 250, 550, 250 }));
            Assert.That(server.Commands, Has.None.EqualTo("DATA").And.None.StartsWith("BDAT"));

        }


        /// <summary>
        /// A refused MAIL in a pipelined group: the RCPT replies behind it (503 here) are read too,
        /// so that none is taken for the reply to a later command.
        /// </summary>
        [Test]
        public async Task A_refused_MAIL_in_a_pipelined_group_leaves_no_reply_behind()
        {

            using var server = new Server(line => line.StartsWith("MAIL") ? "550 5.7.1 sender rejected"
                                                : line.StartsWith("RCPT") ? "503 5.5.1 need MAIL"
                                                : null,
                                          "PIPELINING", "8BITMIME", "SIZE 1000000");

            var result = await Send(server, "a@pipe.test", "b@pipe.test");

            Assert.That(result.Status,   Is.Not.EqualTo(MailSentStatus.ok));
            Assert.That(server.Commands, Has.None.EqualTo("DATA").And.None.StartsWith("BDAT"));

        }


        [Test]
        public async Task With_CHUNKING_the_message_goes_as_one_BDAT_LAST()
        {

            using var server = new Server(null, "PIPELINING", "8BITMIME", "SIZE 1000000", "CHUNKING");

            var result = await Send(server);

            var bdat    = server.Commands.Single(command => command.StartsWith("BDAT"));
            var content = Encoding.UTF8.GetString(server.BdatContent!);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.Commands, Has.None.EqualTo("DATA"));
            Assert.That(bdat,            Is.EqualTo($"BDAT {server.BdatContent!.Length} LAST"));
            Assert.That(content,         Does.Contain("\r\n.stuffed\r\n").And.EndWith("Grüße\r\n"),
                        "no dot-stuffing, every line with its CR LF");

            var mail     = server.Commands.Single(command => command.StartsWith("MAIL"));
            Assert.That(mail, Does.Contain($" SIZE={server.BdatContent!.Length}"), "the size declared on MAIL is the size sent");

        }


        [Test]
        public async Task A_refused_BDAT_is_a_failed_delivery()
        {

            using var server = new Server(line => line.StartsWith("BDAT") ? "554 5.7.1 rejected" : null,
                                          "8BITMIME", "SIZE 1000000", "CHUNKING");

            var result = await Send(server);

            Assert.That(result.Status, Is.Not.EqualTo(MailSentStatus.ok));

        }


        /// <summary>
        /// RFC 3030 §3: BODY=BINARYMIME only with BDAT - so only when CHUNKING is there too.
        /// </summary>
        [TestCase(true,  true)]
        [TestCase(false, false)]
        public async Task BODY_BINARYMIME_only_with_CHUNKING(Boolean Chunking, Boolean BinaryMimeOnMail)
        {

            using var server = Chunking
                                   ? new Server(null, "BINARYMIME", "CHUNKING", "SIZE 1000000")
                                   : new Server(null, "BINARYMIME", "SIZE 1000000");

            await Send(server);

            Assert.That(server.Commands.FirstOrDefault(command => command.StartsWith("MAIL")) ?? "",
                        BinaryMimeOnMail ? Does.Contain("BODY=BINARYMIME") : Does.Not.Contain("BODY=BINARYMIME"));

        }

    }

}
