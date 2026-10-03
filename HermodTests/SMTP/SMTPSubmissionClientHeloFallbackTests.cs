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
    /// RFC 5321 §3.2: a server that does not do EHLO answers it with 500, 501, 502, 504 or
    /// 550, and the client "should then fall back to HELO".
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientHeloFallbackTests
    {

        /// <summary>
        /// One behaviour per connection, in order: an ESMTP server or one that refuses EHLO.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Queue<String?>           ehloRefusals;

            public readonly ConcurrentQueue<String>   Commands = new();

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            /// <param name="EhloRefusals">Per connection: the reply refusing EHLO, or null for an ESMTP server.</param>
            public Server(params String?[] EhloRefusals)
            {
                ehloRefusals = new (EhloRefusals);
                listener.Start();
                _ = Task.Run(AcceptAsync);
            }

            private async Task AcceptAsync()
            {
                try
                {
                    while (ehloRefusals.Count > 0)
                    {
                        var client  = await listener.AcceptTcpClientAsync();
                        var refusal = ehloRefusals.Dequeue();
                        _ = Task.Run(() => ServeAsync(client, refusal));
                    }
                }
                catch
                {
                    // listener stopped
                }
            }

            private async Task ServeAsync(TcpClient client, String? ehloRefusal)
            {
                try
                {

                    using (client)
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true })
                    {

                        await writer.WriteLineAsync("220 old.test SMTP");

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
                                if (ehloRefusal is not null)
                                    await writer.WriteLineAsync(ehloRefusal);
                                else
                                {
                                    await writer.WriteLineAsync("250-old.test");
                                    await writer.WriteLineAsync("250-SIZE 1000000");
                                    await writer.WriteLineAsync("250 8BITMIME");
                                }
                            }
                            else if (upper.StartsWith("HELO")) await writer.WriteLineAsync("250 old.test");
                            else if (upper == "DATA")          { await writer.WriteLineAsync("354 go ahead"); inData = true; }
                            else if (upper == "QUIT")          { await writer.WriteLineAsync("221 bye"); return; }
                            else                                 await writer.WriteLineAsync("250 ok");

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


        private static SMTPSubmissionClient ClientFor(Server Server, TLSUsage UseTLS = TLSUsage.NoTLS)
            => new (DomainName.Parse("127.0.0.1"),
                    IPPort.Parse(Server.Port),
                    LocalDomain:                 "client.example",
                    UseTLS:                      UseTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(3),
                    CommandTimeout:              TimeSpan.FromSeconds(3));

        private static EMailEnvelop Message()
            => new (EMail.Parse([
                        "From: app@client.example",
                        "To: you@old.test",
                        "Subject: plain SMTP",
                        "",
                        "hello"
                    ]));


        [TestCase("500 5.5.1 Command unrecognized")]
        [TestCase("501 5.5.4 Syntax error")]
        [TestCase("502 5.5.1 Command not implemented")]
        [TestCase("504 5.5.4 Command parameter not implemented")]
        [TestCase("550 5.7.0 Not here")]
        public async Task A_refused_EHLO_falls_back_to_HELO(String Refusal)
        {

            using var server = new Server(Refusal);
            using var client = ClientFor(server);

            var result   = await client.Send(Message(), NumberOfRetries: 0);
            await Task.Delay(100);
            var commands = server.Commands.ToArray();

            Assert.That(result,   Is.EqualTo(MailSentStatus.ok), String.Join(" | ", commands));
            Assert.That(commands, Does.Contain("HELO client.example"));

            // No extension was negotiated, so MAIL carries no parameter.
            Assert.That(commands, Does.Contain("MAIL FROM:<app@client.example>"));

        }


        [Test]
        public async Task Capabilities_of_an_earlier_connection_do_not_carry_over()
        {

            // First connection: ESMTP with SIZE and 8BITMIME. Second: EHLO refused.
            using var server = new Server(null, "502 5.5.1 Command not implemented");
            using var client = ClientFor(server);

            Assert.That(await client.Send(Message(), NumberOfRetries: 0), Is.EqualTo(MailSentStatus.ok));
            Assert.That(await client.Send(Message(), NumberOfRetries: 0), Is.EqualTo(MailSentStatus.ok));

            await Task.Delay(100);
            var mailCommands = server.Commands.Where(c => c.StartsWith("MAIL")).ToArray();

            Assert.That(mailCommands, Has.Length.EqualTo(2));
            Assert.That(mailCommands[0], Does.Contain("SIZE=").And.Contain("BODY=8BITMIME"), "the ESMTP server's extensions are used");
            Assert.That(mailCommands[1], Is.EqualTo("MAIL FROM:<app@client.example>"),       "none of them after HELO");

        }


        [Test]
        public async Task With_STARTTLS_required_a_HELO_only_server_is_TLSUnavailable()
        {

            using var server = new Server("502 5.5.1 Command not implemented");
            using var client = ClientFor(server, TLSUsage.STARTTLS);

            var result = await client.Send(Message(), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result,                 Is.EqualTo(MailSentStatus.TLSUnavailable));
            Assert.That(server.Commands,        Has.None.StartsWith("MAIL"));

        }

    }

}
