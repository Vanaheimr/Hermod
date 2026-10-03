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
    /// A client told to use STARTTLS sends over TLS or not at all (RFC 3207 §6).
    ///
    /// Each server here stands in for an on-path attacker who rewrote one reply: the
    /// STARTTLS line removed from the EHLO answer, STARTTLS answered with 454, the
    /// handshake answered with something that is not TLS, or plaintext written right
    /// behind the 220, where the handshake belongs. In every case the message
    /// must not leave in cleartext, and the outcome must say why.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientStartTlsTests
    {

        public enum Attack { StartTlsNotOffered, StartTlsRefused, HandshakeBroken, PlaintextBehindReady }

        private sealed class DowngradingServer : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Attack                   attack;

            public readonly ConcurrentQueue<String>   Commands = new();

            // Whether the client sent anything (its ClientHello) after the server's 220 to STARTTLS.
            public volatile Boolean                   HandshakeStarted;

            public Int32 Port
                => ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public DowngradingServer(Attack Attack)
            {
                attack = Attack;
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

                    await writer.WriteLineAsync("220 downgrade.test ESMTP");

                    String? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        Commands.Enqueue(line);
                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-downgrade.test");
                            if (attack != Attack.StartTlsNotOffered)
                                await writer.WriteLineAsync("250-STARTTLS");
                            await writer.WriteLineAsync("250 8BITMIME");
                        }
                        else if (upper == "STARTTLS")
                        {
                            if (attack == Attack.StartTlsRefused)
                                await writer.WriteLineAsync("454 4.7.0 TLS not available due to temporary reason");

                            else if (attack == Attack.PlaintextBehindReady)
                            {
                                // The 220 and plaintext behind it in one write, so they arrive together
                                // on every OS. Then note whether a ClientHello still comes.
                                await stream.WriteAsync(Encoding.ASCII.GetBytes("220 2.0.0 Ready to start TLS\r\nthis is not a TLS ServerHello\r\n"));
                                HandshakeStarted = await stream.ReadAsync(new Byte[4096]) > 0;
                                return;
                            }

                            else
                            {
                                // Agree, wait for the ClientHello, then answer it with plaintext. Written
                                // back to back, the 220 and the plaintext arrive in one read on Linux and
                                // the handshake is never reached - that is PlaintextBehindReady.
                                await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                                HandshakeStarted = await stream.ReadAsync(new Byte[4096]) > 0;
                                await writer.WriteLineAsync("this is not a TLS ServerHello");
                                return;
                            }
                        }
                        else if (upper == "DATA")  await writer.WriteLineAsync("354 go ahead");
                        else if (line == ".")      await writer.WriteLineAsync("250 2.0.0 queued");
                        else if (upper == "QUIT") { await writer.WriteLineAsync("221 2.0.0 bye"); return; }
                        else                       await writer.WriteLineAsync("250 2.0.0 ok");

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


        // With credentials the old client stopped anyway, one step later: it refused to send
        // AUTH in cleartext and reported InvalidLogin. Without them nothing stopped it, and
        // the message went out in the clear - which is why both variants are here.
        [Test]
        public async Task Nothing_is_sent_in_cleartext_when_STARTTLS_does_not_happen([Values] Attack  Attack,
                                                                                     [Values] Boolean WithCredentials)
        {

            using var server = new DowngradingServer(Attack);
            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        IPPort.Parse((UInt16) server.Port),
                                                        Login:                       WithCredentials ? "app"    : null,
                                                        Password:                    WithCredentials ? "secret" : null,
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.STARTTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(
                                   new EMailEnvelop(EMail.Parse([
                                       "From: app@client.example",
                                       "To: you@downgrade.test",
                                       "Subject: confidential",
                                       "",
                                       "for TLS only"
                                   ])),
                                   NumberOfRetries: 2
                               );

            await Task.Delay(200);
            var commands = server.Commands.ToArray();

            Assert.That(result.Status,    Is.EqualTo(MailSentStatus.TLSUnavailable));
            Assert.That(result.TLSActive, Is.False);
            Assert.That(result.Attempts,  Is.EqualTo(1), "a downgrade is not a transient failure: no retry");
            Assert.That(server.HandshakeStarted, Is.EqualTo(Attack == Attack.HandshakeBroken),
                        "a handshake is attempted after a lone 220, never on top of plaintext behind it");
            Assert.That(commands,         Has.None.StartsWith("MAIL").And.None.StartsWith("AUTH").And.None.EqualTo("for TLS only"),
                        "nothing past EHLO/STARTTLS may be sent in cleartext: " + String.Join(" | ", commands));

        }

    }

}
