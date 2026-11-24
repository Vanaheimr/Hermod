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
    /// SMTPUTF8 (RFC 6531) in the submission client: an internationalized message - UTF-8 in an
    /// envelope address or in the header - goes only to a server that offers SMTPUTF8 (§3.2,
    /// §3.5); to any other it is a permanent failure, decided before MAIL. The parameter goes
    /// only with a message that needs it (§3.4): an 8-bit body alone is BODY=8BITMIME's.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientSmtpUtf8Tests
    {

        private sealed class Server : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Boolean                  smtpUtf8;

            public readonly ConcurrentQueue<String>   Commands  = new();
            public readonly ConcurrentQueue<Byte[]>   DataLines = new();

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            public Server(Boolean SmtpUtf8)
            {
                smtpUtf8 = SmtpUtf8;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    await writer.WriteLineAsync("220 utf8.test ESMTP");

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 queued"); inData = false; }
                            else               DataLines.Enqueue(Encoding.UTF8.GetBytes(line));
                            continue;
                        }

                        Commands.Enqueue(line);
                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-utf8.test");
                            await writer.WriteLineAsync("250-8BITMIME");
                            if (smtpUtf8)
                                await writer.WriteLineAsync("250-SMTPUTF8");
                            await writer.WriteLineAsync("250 SIZE 1000000");
                        }
                        else if (upper == "DATA")  { await writer.WriteLineAsync("354 go ahead"); inData = true; }
                        else if (upper == "QUIT")  { await writer.WriteLineAsync("221 bye"); return; }
                        else                         await writer.WriteLineAsync("250 ok");

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


        private static async Task<(SMTPSendResult Result, String[] Commands, Byte[][] Data)> Send(Boolean  SmtpUtf8,
                                                                                                 String   From,
                                                                                                 String   To,
                                                                                                 String   Subject,
                                                                                                 String   Body = "hello")
        {

            using var server = new Server(SmtpUtf8);
            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        server.Port,
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                         $"From: {From}",
                                                         $"To: {To}",
                                                         $"Subject: {Subject}",
                                                         "Content-Type: text/plain; charset=utf-8",
                                                         "Content-Transfer-Encoding: 8bit",
                                                         "",
                                                         Body
                                                     ])),
                                                     NumberOfRetries: 2);

            await Task.Delay(100);

            return (result, server.Commands.ToArray(), server.DataLines.ToArray());

        }


        public static IEnumerable<TestCaseData> InternationalizedMessages()
        {

            TestCaseData Case(String Name, String From, String To, String Subject)
                => new TestCaseData(From, To, Subject).SetName($"An internationalized message is not sent without SMTPUTF8: {Name}");

            yield return Case("a recipient with a UTF-8 local-part",  "app@client.example",   "jörg@utf8.test",     "hello");
            yield return Case("a sender with a UTF-8 local-part",     "jörg@client.example",  "you@utf8.test",      "hello");
            yield return Case("a recipient in U-labels",              "app@client.example",   "you@bücher.test",    "hello");
            yield return Case("a Subject in UTF-8",                   "app@client.example",   "you@utf8.test",      "Grüße aus Köln");

        }


        /// <summary>
        /// RFC 6531 §3.2: "If the SMTPUTF8 SMTP extension is not offered by the SMTP server, the
        /// SMTPUTF8-aware SMTP client MUST NOT transmit an internationalized email address and
        /// MUST NOT transmit a mail message containing internationalized mail headers".
        /// </summary>
        [TestCaseSource(nameof(InternationalizedMessages))]
        public async Task An_internationalized_message_is_not_sent_without_SMTPUTF8(String From, String To, String Subject)
        {

            var (result, commands, data) = await Send(SmtpUtf8: false, From, To, Subject);

            Assert.That(result.Status,            Is.EqualTo(MailSentStatus.SmtpUtf8NotSupported));
            Assert.That(result.Attempts,          Is.EqualTo(1), "a permanent failure is not retried");
            Assert.That(commands,                 Has.None.StartsWith("MAIL").And.None.StartsWith("RCPT"), String.Join(" | ", commands));
            Assert.That(commands.LastOrDefault(), Is.EqualTo("QUIT"));
            Assert.That(data,                     Is.Empty);

        }


        /// <summary>
        /// RFC 6531 §3.4: "If the envelope or message being sent requires the capabilities of the
        /// SMTPUTF8 extension, the SMTPUTF8-aware SMTP client MUST supply the SMTPUTF8 parameter
        /// with the MAIL command".
        /// </summary>
        [TestCaseSource(nameof(InternationalizedMessages))]
        public async Task An_internationalized_message_goes_with_SMTPUTF8(String From, String To, String Subject)
        {

            var (result, commands, _) = await Send(SmtpUtf8: true, From, To, Subject);

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands,      Has.Some.StartsWith("MAIL").And.Contains(" SMTPUTF8"), String.Join(" | ", commands));

        }


        /// <summary>
        /// RFC 6531 §3.4: "If the SMTPUTF8-aware SMTP client is aware that neither the envelope
        /// nor the message being sent requires any of the SMTPUTF8 extension capabilities, it
        /// SHOULD NOT supply the SMTPUTF8 parameter with the MAIL command" - an 8-bit body is
        /// BODY=8BITMIME's (RFC 6152).
        /// </summary>
        [Test]
        public async Task An_8bit_body_alone_goes_without_SMTPUTF8()
        {

            var (result, commands, data) = await Send(SmtpUtf8: true, "app@client.example", "you@utf8.test", "plain", "Grüße aus Köln");

            var mail = commands.FirstOrDefault(command => command.StartsWith("MAIL"));

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(mail,          Does.Contain(" BODY=8BITMIME").And.Not.Contains("SMTPUTF8"));
            Assert.That(data.SelectMany(line => line).Any(octet => octet > 0x7F), Is.True, "the body goes out as UTF-8, unchanged");

        }


        /// <summary>
        /// An 8-bit body with an ASCII envelope and header is no internationalized message: it goes
        /// to a server with 8BITMIME and without SMTPUTF8.
        /// </summary>
        [Test]
        public async Task An_8bit_body_alone_goes_to_a_server_without_SMTPUTF8()
        {

            var (result, commands, _) = await Send(SmtpUtf8: false, "app@client.example", "you@utf8.test", "plain", "Grüße aus Köln");

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands,      Has.Some.StartsWith("MAIL").And.None.Contains("SMTPUTF8"));

        }


        /// <summary>
        /// The header section ends with the first empty line: UTF-8 after it is the body's, and
        /// whatever looks like a header field there is not one.
        /// </summary>
        [Test]
        public void Only_the_header_and_the_envelope_need_SMTPUTF8()
        {

            static EMailEnvelop Envelop(String From, String To)
                => new (EMail.Parse([ $"From: {From}", $"To: {To}", "Subject: x", "", "body" ]));

            Assert.That(SMTPSubmissionClient.NeedsSmtpUtf8(Envelop("app@client.example", "you@utf8.test"),  [ "Subject: x", "", "Grüße: aus Köln" ]), Is.False);
            Assert.That(SMTPSubmissionClient.NeedsSmtpUtf8(Envelop("app@client.example", "you@utf8.test"),  [ "Subject: Grüße", "", "body" ]),        Is.True);
            Assert.That(SMTPSubmissionClient.NeedsSmtpUtf8(Envelop("app@client.example", "you@xn--bcher-kva.test"), [ "Subject: x", "", "body" ]),   Is.False, "A-labels are ASCII");
            Assert.That(SMTPSubmissionClient.NeedsSmtpUtf8(Envelop("app@client.example", "jörg@utf8.test"), [ "Subject: x", "", "body" ]),           Is.True);

        }

    }

}
