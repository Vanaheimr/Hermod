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
    /// The DSN parameters (RFC 3461) the submission client gives: ENVID as xtext (§4, §4.4), and
    /// an identifier that cannot be one refused before MAIL - a line break in it would end the
    /// command and make one of its own; RET without NOTIFY (§4.3); NOTIFY=NEVER (§4.1).
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientDsnTests
    {

        private sealed class Server : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);

            public readonly ConcurrentQueue<String>   Commands = new();

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            public Server()
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
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    await writer.WriteLineAsync("220 dsn.test ESMTP");

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 queued"); inData = false; }
                            continue;
                        }

                        Commands.Enqueue(line);
                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-dsn.test");
                            await writer.WriteLineAsync("250-8BITMIME");
                            await writer.WriteLineAsync("250 DSN");
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


        private static async Task<(SMTPSendResult Result, String[] Commands)> Send(DsnParameters Dsn)
        {

            using var server = new Server();
            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        server.Port,
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                         "From: app@client.example",
                                                         "To: you@dsn.test",
                                                         "Subject: DSN",
                                                         "",
                                                         "body"
                                                     ])) { Dsn = Dsn },
                                                     NumberOfRetries: 2);

            await Task.Delay(100);

            return (result, server.Commands.ToArray());

        }


        [TestCase("order 42",   "order+2042")]
        [TestCase("batch=7",    "batch+3D7")]
        [TestCase("QQ+314159",  "QQ+2B314159")]
        public async Task ENVID_goes_as_xtext(String EnvId, String XText)
        {

            var (result, commands) = await Send(new DsnParameters(DsnNotify.Failure, EnvId: EnvId));

            Assert.That(result.Status,                                     Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.EndWith(" ENVID=" + XText));

        }


        [TestCase("x\r\nRCPT TO:<intruder@evil.test>")]
        [TestCase("Grüße")]
        public async Task An_ENVID_that_cannot_be_one_is_not_sent(String EnvId)
        {

            var (result, commands) = await Send(new DsnParameters(DsnNotify.Failure, EnvId: EnvId));

            Assert.That(result.Status,            Is.EqualTo(MailSentStatus.InvalidEnvelopeId));
            Assert.That(result.Attempts,          Is.EqualTo(1), "a permanent failure is not retried");
            Assert.That(commands,                 Has.None.StartsWith("MAIL").And.None.StartsWith("RCPT"), String.Join(" | ", commands));
            Assert.That(commands.LastOrDefault(), Is.EqualTo("QUIT"));

        }


        [Test]
        public async Task An_ENVID_longer_than_100_characters_is_not_sent()
        {

            var (result, commands) = await Send(new DsnParameters(DsnNotify.Failure, EnvId: new String('e', 101)));

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.InvalidEnvelopeId));
            Assert.That(commands,      Has.None.StartsWith("MAIL"));

        }


        [Test]
        public async Task RET_goes_without_NOTIFY_and_ENVID()
        {

            var (result, commands) = await Send(new DsnParameters(Ret: DsnRet.Hdrs));

            Assert.That(result.Status,                                          Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.EndWith(" RET=HDRS"));
            Assert.That(commands.Single(command => command.StartsWith("RCPT")), Does.Not.Contain("NOTIFY="));

        }


        [Test]
        public async Task NOTIFY_NEVER_goes_on_RCPT()
        {

            var (result, commands) = await Send(new DsnParameters(DsnNotify.Never));

            Assert.That(result.Status,                                          Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands.Single(command => command.StartsWith("RCPT")), Is.EqualTo("RCPT TO:<you@dsn.test> NOTIFY=NEVER ORCPT=rfc822;you@dsn.test"));

        }


        [Test]
        public async Task A_message_that_asks_for_nothing_goes_without_DSN_parameters()
        {

            var (result, commands) = await Send(DsnParameters.None);

            Assert.That(result.Status,                                          Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands.Single(command => command.StartsWith("MAIL")), Does.Not.Contain("RET=").And.Not.Contain("ENVID="));
            Assert.That(commands.Single(command => command.StartsWith("RCPT")), Is.EqualTo("RCPT TO:<you@dsn.test>"));

        }

    }

}
