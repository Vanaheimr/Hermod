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
    /// RFC 5321 §4.2.1: "a sender-SMTP MUST be prepared to handle codes not specified in this
    /// document and MUST do so by interpreting the first digit only." A 252 to MAIL or RCPT is a
    /// 250, a 350 to DATA a 354, a 251 to the end of data a 250.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientReplyCodeTests
    {

        /// <summary>
        /// A server answering each command with the given reply, or with the usual one.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener                  listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly String                       greeting;
            private readonly Boolean                      pipelining;
            private readonly Dictionary<String, String>   replies;

            public readonly ConcurrentQueue<String>       Commands  = new();
            public readonly ConcurrentQueue<String>       DataLines = new();

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            /// <param name="Replies">The reply to a command ("MAIL", "RCPT", "DATA", or "." for the end of data).</param>
            public Server(Dictionary<String, String>? Replies = null, String Greeting = "220 server.test ESMTP", Boolean Pipelining = true)
            {
                replies     = Replies ?? [];
                greeting    = Greeting;
                pipelining  = Pipelining;
                listener.Start();
                _ = Task.Run(AcceptAsync);
            }

            private async Task AcceptAsync()
            {
                try
                {
                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => ServeAsync(client));
                    }
                }
                catch
                {
                    // listener stopped
                }
            }

            private String Reply(String Command, String Usual)
                => replies.TryGetValue(Command, out var reply) ? reply : Usual;

            private async Task ServeAsync(TcpClient client)
            {
                try
                {

                    using (client)
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true })
                    {

                        await writer.WriteLineAsync(greeting);

                        var inData = false;
                        String? line;

                        while ((line = await reader.ReadLineAsync()) is not null)
                        {

                            if (inData)
                            {
                                if (line == ".") { await writer.WriteLineAsync(Reply(".", "250 2.0.0 queued")); inData = false; }
                                else               DataLines.Enqueue(line);
                                continue;
                            }

                            Commands.Enqueue(line);
                            var verb = line.Split(' ')[0].ToUpperInvariant();

                            switch (verb)
                            {

                                case "EHLO":
                                    await writer.WriteLineAsync("250-server.test");
                                    if (pipelining)
                                        await writer.WriteLineAsync("250-PIPELINING");
                                    await writer.WriteLineAsync("250 8BITMIME");
                                    break;

                                case "DATA":
                                    var dataReply = Reply("DATA", "354 go ahead");
                                    await writer.WriteLineAsync(dataReply);
                                    inData = dataReply.StartsWith('3');
                                    break;

                                case "QUIT":
                                    await writer.WriteLineAsync("221 bye");
                                    return;

                                default:
                                    await writer.WriteLineAsync(Reply(verb, "250 ok"));
                                    break;

                            }

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


        private static SMTPSubmissionClient ClientFor(Server Server)
            => new (DomainName.Parse("127.0.0.1"),
                    Server.Port,
                    LocalDomain:                 "client.example",
                    UseTLS:                      TLSUsage.NoTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(3),
                    CommandTimeout:              TimeSpan.FromSeconds(3));

        private static EMailEnvelop Message()
            => new (EMail.Parse([
                        "From: app@client.example",
                        "To: you@server.test",
                        "Subject: reply codes",
                        "",
                        "hello"
                    ]));

        private static async Task<(MailSentStatus Result, Server Server)> Send(Dictionary<String, String>? Replies = null, String Greeting = "220 server.test ESMTP", Boolean Pipelining = true)
        {

            var server = new Server(Replies, Greeting, Pipelining);
            using var client = ClientFor(server);

            var result = await client.Send(Message(), NumberOfRetries: 0);
            await Task.Delay(100);

            return (result, server);

        }


        [TestCase("MAIL", "252 2.1.0 Sender accepted", true)]
        [TestCase("MAIL", "252 2.1.0 Sender accepted", false)]
        [TestCase("RCPT", "252 2.1.5 Cannot verify, will attempt delivery", true)]
        [TestCase("RCPT", "252 2.1.5 Cannot verify, will attempt delivery", false)]
        [TestCase("DATA", "350 Send it", true)]
        [TestCase(".",    "251 2.0.0 Taken, will forward", true)]
        public async Task Another_positive_code_is_read_by_its_first_digit(String Command, String Reply, Boolean Pipelining)
        {

            var (result, server) = await Send(new () { [Command] = Reply }, Pipelining: Pipelining);
            using var _ = server;

            Assert.That(result,            Is.EqualTo(MailSentStatus.ok), String.Join(" | ", server.Commands));
            Assert.That(server.DataLines,  Does.Contain("hello"));

        }


        [TestCase("220")]
        [TestCase("221 2.0.0 Other positive")]
        public async Task Another_2yz_greeting_opens_the_session(String Greeting)
        {

            var (result, server) = await Send(Greeting: Greeting);
            using var _ = server;

            Assert.That(result, Is.EqualTo(MailSentStatus.ok));

        }


        [TestCase("MAIL", "250")]
        [TestCase("RCPT", "250")]
        [TestCase("DATA", "354")]
        [TestCase(".",    "250")]
        public async Task A_code_without_text_is_read(String Command, String Reply)
        {

            var (result, server) = await Send(new () { [Command] = Reply });
            using var _ = server;

            Assert.That(result, Is.EqualTo(MailSentStatus.ok));

        }


        [TestCase("MAIL", "553 5.1.8 Sender refused")]
        [TestCase("RCPT", "550 5.1.1 No such user")]
        [TestCase("DATA", "554 5.5.1 No valid recipients")]
        [TestCase(".",    "554 5.6.0 Refused")]
        [TestCase("DATA", "250 2.0.0 ok")]
        public async Task Anything_else_is_no_delivery(String Command, String Reply)
        {

            var (result, server) = await Send(new () { [Command] = Reply });
            using var _ = server;

            Assert.That(result, Is.Not.EqualTo(MailSentStatus.ok).And.Not.EqualTo(MailSentStatus.PartiallySent));

        }


        [TestCase(SMTPStatusCodes.Ok,                                    true,  false)]
        [TestCase(SMTPStatusCodes.CannotVerifyUserWillAttemptDelivery,   true,  false)]
        [TestCase((SMTPStatusCodes) 299,                                 true,  false)]
        [TestCase(SMTPStatusCodes.StartMailInput,                        false, true)]
        [TestCase((SMTPStatusCodes) 350,                                 false, true)]
        [TestCase(SMTPStatusCodes.MailboxBusy,                           false, false)]
        [TestCase(SMTPStatusCodes.MailboxUnavailable,                    false, false)]
        public void A_code_is_read_by_its_first_digit(SMTPStatusCodes Code, Boolean PositiveCompletion, Boolean PositiveIntermediate)
        {
            Assert.That(Code.IsPositiveCompletion(),    Is.EqualTo(PositiveCompletion));
            Assert.That(Code.IsPositiveIntermediate(),  Is.EqualTo(PositiveIntermediate));
        }

    }

}
