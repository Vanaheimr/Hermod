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
    /// RFC 5321 §4.1.1.10: "The sender MUST NOT intentionally close the transmission channel
    /// until it sends a QUIT command, and it SHOULD wait until it receives the reply." Every
    /// attempt ends with QUIT and a closed connection - delivered or not.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientQuitTests
    {

        /// <summary>
        /// A server with scriptable replies that does not close after QUIT: it waits to see
        /// whether the client closes the connection.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener                          listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly String[]                             extensions;
            private readonly IReadOnlyDictionary<String, String>  replies;

            public readonly ConcurrentQueue<String>               Commands        = new();
            public readonly TaskCompletionSource<Boolean>         ClosedByClient  = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            /// <param name="Replies">Replies by verb ("MAIL", "RCPT", "DATA", "STARTTLS", "QUIT", and "." for the end of data), instead of the defaults.</param>
            /// <param name="Extensions">The EHLO keywords.</param>
            public Server(IReadOnlyDictionary<String, String>? Replies = null, params String[] Extensions)
            {
                replies    = Replies ?? new Dictionary<String, String>();
                extensions = Extensions.Length > 0 ? Extensions : [ "8BITMIME", "SIZE 1000000" ];
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private String Reply(String Verb, String Default)
                => replies.TryGetValue(Verb, out var reply) ? reply : Default;

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    Task Write(String Text)
                        => stream.WriteAsync(Encoding.ASCII.GetBytes(Text + "\r\n")).AsTask();

                    await Write("220 quit.test ESMTP");

                    var inData = false;

                    while (true)
                    {

                        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        if (line is null)
                        {
                            ClosedByClient.TrySetResult(true);
                            return;
                        }

                        if (inData)
                        {
                            if (line == ".")
                            {
                                inData = false;
                                await Write(Reply(".", "250 2.0.0 queued"));
                            }
                            continue;
                        }

                        Commands.Enqueue(line);

                        switch (line.Split(' ')[0].ToUpperInvariant())
                        {

                            case "EHLO":
                                await Write("250-quit.test");
                                for (var i = 0; i < extensions.Length; i++)
                                    await Write((i == extensions.Length - 1 ? "250 " : "250-") + extensions[i]);
                                break;

                            case "DATA":
                                var data = Reply("DATA", "354 go ahead");
                                inData   = data.StartsWith('3');
                                await Write(data);
                                break;

                            case "MAIL":      await Write(Reply("MAIL",     "250 2.1.0 ok"));                 break;
                            case "RCPT":      await Write(Reply("RCPT",     "250 2.1.5 ok"));                 break;
                            case "STARTTLS":  await Write(Reply("STARTTLS", "454 4.7.0 TLS not available"));  break;
                            case "QUIT":      await Write(Reply("QUIT",     "221 2.0.0 bye"));                break;   // and stay
                            default:          await Write("250 2.0.0 ok");                                   break;

                        }

                    }

                }
                catch
                {
                    ClosedByClient.TrySetResult(false);
                }
            }

            public void Dispose()
                => listener.Stop();

        }


        private static async Task<(SMTPSendResult Result, String[] Commands, Boolean ClosedByClient)> Send(Server    Server,
                                                                                                          TLSUsage  UseTLS  = TLSUsage.NoTLS,
                                                                                                          String    Body    = "hello")
        {

            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        IPPort.Parse(Server.Port),
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      UseTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                         "From: app@client.example",
                                                         "To: you@quit.test",
                                                         "Subject: quit",
                                                         "Content-Type: text/plain; charset=utf-8",
                                                         "",
                                                         Body
                                                     ])),
                                                     NumberOfRetries: 0);

            // The client is still alive here: whether the connection is closed is up to the send.
            Boolean closed;
            try   { closed = await Server.ClosedByClient.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch { closed = false; }

            return (result, [.. Server.Commands], closed);

        }


        public static IEnumerable<TestCaseData> FailedAttempts()
        {

            TestCaseData Case(String Name, Dictionary<String, String>? Replies, MailSentStatus Status,
                              TLSUsage UseTLS = TLSUsage.NoTLS, String Body = "hello", String[]? Extensions = null)
                => new TestCaseData(Replies, Extensions ?? [], UseTLS, Body, Status).SetName(Name);

            yield return Case("A refused RCPT ends with QUIT",                 new() { ["RCPT"] = "550 5.1.1 No such user" },  MailSentStatus.ExceptionOccurred);
            yield return Case("A refused MAIL ends with QUIT",                 new() { ["MAIL"] = "550 5.7.1 Rejected" },      MailSentStatus.ExceptionOccurred);
            yield return Case("A refused DATA ends with QUIT",                 new() { ["DATA"] = "554 5.5.1 No recipients" }, MailSentStatus.ExceptionOccurred);
            yield return Case("A refused message ends with QUIT",              new() { ["."]    = "554 5.7.1 Spam" },          MailSentStatus.ExceptionOccurred);
            yield return Case("STARTTLS refused ends with QUIT",               null, MailSentStatus.TLSUnavailable,       TLSUsage.STARTTLS,              Extensions: [ "STARTTLS", "8BITMIME" ]);
            yield return Case("A message above SIZE ends with QUIT",           null, MailSentStatus.MessageSizeExceeded,  Body: new String('x', 500),     Extensions: [ "SIZE 100" ]);
            yield return Case("8-bit content without 8BITMIME ends with QUIT", null, MailSentStatus.EightBitNotSupported, Body: "Grüße",                  Extensions: [ "SIZE 1000000" ]);

        }


        [TestCaseSource(nameof(FailedAttempts))]
        public async Task A_failed_attempt_ends_with_QUIT_and_a_closed_connection(Dictionary<String, String>?  Replies,
                                                                                  String[]                     Extensions,
                                                                                  TLSUsage                     UseTLS,
                                                                                  String                       Body,
                                                                                  MailSentStatus               Status)
        {

            using var server = new Server(Replies, Extensions);

            var (result, commands, closed) = await Send(server, UseTLS, Body);

            Assert.That(result.Status,            Is.EqualTo(Status));
            Assert.That(commands.LastOrDefault(), Is.EqualTo("QUIT"), String.Join(" | ", commands));
            Assert.That(closed,                   Is.True, "the client closes the connection after QUIT");

        }


        [Test]
        public async Task A_delivered_message_ends_with_QUIT_and_a_closed_connection()
        {

            using var server = new Server();

            var (result, commands, closed) = await Send(server);

            Assert.That(result.Status,            Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands.LastOrDefault(), Is.EqualTo("QUIT"));
            Assert.That(closed,                   Is.True, "the client closes the connection after QUIT");

        }


        /// <summary>
        /// Once the end of data is acknowledged, the message is the server's (RFC 5321 §6.1). An
        /// odd answer to QUIT does not make it undelivered.
        /// </summary>
        [Test]
        public async Task An_odd_reply_to_QUIT_does_not_undo_a_delivery()
        {

            using var server = new Server(new Dictionary<String, String> { ["QUIT"] = "500 5.5.1 What?" });

            var (result, _, _) = await Send(server);

            Assert.That(result.Status,     Is.EqualTo(MailSentStatus.ok));
            Assert.That(result.StatusCode, Is.EqualTo(SMTPStatusCodes.Ok));

        }

    }

}
