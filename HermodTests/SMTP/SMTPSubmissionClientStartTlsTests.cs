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


        #region A handshake that never ends

        /// <summary>
        /// Agrees to TLS - with a 220 to STARTTLS, or by accepting the connection on an
        /// implicit TLS port - reads the ClientHello and then says nothing, until the
        /// client hangs up.
        /// </summary>
        private sealed class SilentHandshakeServer : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource  stop     = new ();
            private readonly Boolean                  startTls;

            public readonly ConcurrentQueue<String>   Commands = new();

            public readonly TaskCompletionSource      ClientHelloReceived = new (TaskCreationOptions.RunContinuationsAsynchronously);

            public Int32 Port
                => ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public SilentHandshakeServer(Boolean StartTls)
            {
                startTls = StartTls;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    using var stream = client.GetStream();

                    if (startTls)
                    {

                        // Nothing is read through the reader once the 220 is out: the ClientHello
                        // only comes after it, so it is still in the socket, not in this buffer.
                        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

                        await writer.WriteLineAsync("220 silent.test ESMTP");

                        String? line;
                        while ((line = await reader.ReadLineAsync(stop.Token)) is not null)
                        {

                            Commands.Enqueue(line);
                            var upper = line.ToUpperInvariant();

                            if (upper.StartsWith("EHLO"))
                            {
                                await writer.WriteLineAsync("250-silent.test");
                                await writer.WriteLineAsync("250-STARTTLS");
                                await writer.WriteLineAsync("250 8BITMIME");
                            }
                            else if (upper == "STARTTLS")
                            {
                                await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                                break;
                            }
                            else
                                await writer.WriteLineAsync("250 2.0.0 ok");

                        }

                    }

                    var buffer = new Byte[16 * 1024];

                    if (await stream.ReadAsync(buffer, stop.Token) > 0)
                        ClientHelloReceived.TrySetResult();

                    // ...and never a ServerHello.
                    while (await stream.ReadAsync(buffer, stop.Token) > 0) { }

                }
                catch
                {
                    // the client hung up, or the test is over
                }
            }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
            }

        }

        private static SMTPSubmissionClient ClientFor(SilentHandshakeServer  Server,
                                                      TLSUsage               UseTLS,
                                                      TimeSpan               CommandTimeout,
                                                      Boolean                WithCredentials)

            => new (DomainName.Parse("127.0.0.1"),
                    IPPort.Parse((UInt16) Server.Port),
                    Login:                       WithCredentials ? "app"    : null,
                    Password:                    WithCredentials ? "secret" : null,
                    LocalDomain:                 "client.example",
                    UseTLS:                      UseTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(3),
                    CommandTimeout:              CommandTimeout);

        private static EMailEnvelop Confidential()

            => new (EMail.Parse([
                       "From: app@client.example",
                       "To: you@silent.test",
                       "Subject: confidential",
                       "",
                       "for TLS only"
                   ]));

        /// <summary>
        /// Waits for the send, but not for ever: a send that is not bounded fails the test
        /// instead of hanging the suite.
        /// </summary>
        private static async Task<SMTPSendResult> Bounded(Task<SMTPSendResult> Send)
        {

            var guard = TimeSpan.FromSeconds(30);

            if (await Task.WhenAny(Send, Task.Delay(guard)) != Send)
                Assert.Fail($"the send was still running after {guard.TotalSeconds} s: the TLS handshake is not bounded");

            return await Send;

        }


        // Every SMTP step waits at most the command timeout for the server - except, until
        // now, the handshake after STARTTLS, which only the caller's token could end. With
        // no RequestTimeout that was never: one 220 and then silence, from the server or
        // from someone on the path, held SendWithResult for good.
        [Test]
        public async Task A_server_that_never_answers_the_ClientHello_cannot_hold_the_send([Values] Boolean WithCredentials)
        {

            using var server = new SilentHandshakeServer(StartTls: true);
            using var client = ClientFor(server, TLSUsage.STARTTLS, TimeSpan.FromSeconds(3), WithCredentials);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result    = await Bounded(client.SendWithResult(Confidential(), NumberOfRetries: 2));
            stopwatch.Stop();

            var commands  = server.Commands.ToArray();

            Assert.That(server.ClientHelloReceived.Task.IsCompleted, Is.True, "the client started the handshake");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                        "the handshake gets the 3 s command timeout, like every other step");
            Assert.That(result.Status,     Is.EqualTo(MailSentStatus.TLSUnavailable));
            Assert.That(result.TLSActive,  Is.False);
            Assert.That(result.Attempts,   Is.EqualTo(1), "a stalled handshake is not retried, like every other way STARTTLS can fail");
            Assert.That(commands,          Has.None.StartsWith("MAIL").And.None.StartsWith("AUTH").And.None.EqualTo("for TLS only"),
                        "nothing past EHLO/STARTTLS may be sent in cleartext: " + String.Join(" | ", commands));

        }

        // The deadline is added to the caller's token, not put in its place.
        [Test]
        public async Task The_caller_can_still_cancel_a_stalled_handshake_at_once()
        {

            using var server = new SilentHandshakeServer(StartTls: true);
            using var client = ClientFor(server, TLSUsage.STARTTLS, TimeSpan.FromSeconds(30), WithCredentials: true);
            using var cts    = new CancellationTokenSource();

            var send = client.SendWithResult(Confidential(), NumberOfRetries: 2, CancellationToken: cts.Token);

            await server.ClientHelloReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            cts.Cancel();
            var result    = await Bounded(send);
            stopwatch.Stop();

            var commands  = server.Commands.ToArray();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                        "the cancellation ends the handshake, not the 30 s command timeout");
            Assert.That(result.Status,     Is.EqualTo(MailSentStatus.TLSUnavailable));
            Assert.That(result.Attempts,   Is.EqualTo(1));
            Assert.That(commands,          Has.None.StartsWith("MAIL").And.None.StartsWith("AUTH"),
                        "nothing past EHLO/STARTTLS may be sent in cleartext: " + String.Join(" | ", commands));

        }

        // The same handshake, right after connecting to an implicit TLS port (465): it ran
        // inside ReconnectAsync, with the same token and the same missing deadline.
        [Test]
        public async Task A_silent_server_on_an_implicit_TLS_port_cannot_hold_the_send()
        {

            using var server = new SilentHandshakeServer(StartTls: false);
            using var client = ClientFor(server, TLSUsage.TLSSocket, TimeSpan.FromSeconds(3), WithCredentials: true);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result    = await Bounded(client.SendWithResult(Confidential(), NumberOfRetries: 0));
            stopwatch.Stop();

            Assert.That(server.ClientHelloReceived.Task.IsCompleted, Is.True, "the client started the handshake");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                        "the handshake gets the 3 s command timeout");
            Assert.That(result.Status,     Is.Not.EqualTo(MailSentStatus.ok));
            Assert.That(result.TLSActive,  Is.False);

        }

        #endregion

    }

}
