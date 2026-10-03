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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A wss:// client gives the TLS handshake its connect timeout.
    /// </summary>
    /// <remarks>
    /// The connection attempt handed the handshake no token at all. A server that
    /// took the connection and the ClientHello and then said nothing held the attempt
    /// for good: Connect() returned only when its request timeout ran out, ten minutes
    /// by default, and a reconnect policy never tried again, because the attempt it
    /// would have retried never ended.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientHandshakeTimeoutTests
    {

        private static readonly RemoteTLSServerCertificateValidationHandler<IWebSocketClient> AnyCertificate

            = (sender, certificate, certificateChain, client, policyErrors) => TLSValidationResult.Success();

        /// <summary>
        /// A wait that is not bounded fails the test instead of hanging the suite.
        /// </summary>
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);


        [Test]
        public async Task Connect_returns_when_the_server_never_answers_the_ClientHello()
        {

            using var server        = new SilentTLSServer();
            await using var client  = new WebSocketClient(
                                          URL.Parse($"wss://127.0.0.1:{server.Port}"),
                                          RemoteCertificateValidator:  AnyCertificate,
                                          ConnectTimeout:              TimeSpan.FromSeconds(2)
                                      );

            var stopwatch = Stopwatch.StartNew();
            var connect   = client.Connect();

            if (await Task.WhenAny(connect, Task.Delay(Guard)) != connect)
                Assert.Fail($"Connect() was still running after {Guard.TotalSeconds} s: the TLS handshake is not bounded");

            var (_, response) = await connect;
            stopwatch.Stop();

            var reason    = response.HTTPBodyAsUTF8String ?? "";

            Assert.Multiple(() => {
                Assert.That(server.ClientHellos,     Is.EqualTo(1),                         "the client started the handshake");
                Assert.That(stopwatch.Elapsed,       Is.LessThan(TimeSpan.FromSeconds(10)), "the handshake gets the 2 s connect timeout");
                Assert.That(response.HTTPStatusCode, Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                Assert.That(reason,                  Does.Contain("did not complete within"), reason);
            });

        }

        [Test]
        public async Task A_reconnect_policy_tries_again_after_a_stalled_handshake()
        {

            using var server        = new SilentTLSServer();
            await using var client  = new WebSocketClient(
                                          URL.Parse($"wss://127.0.0.1:{server.Port}"),
                                          RemoteCertificateValidator:  AnyCertificate,
                                          ConnectTimeout:              TimeSpan.FromSeconds(2)
                                      ) {
                                          ReconnectPolicy = new (InitialDelay: TimeSpan.FromMilliseconds(200),
                                                                 MaxDelay:     TimeSpan.FromMilliseconds(500))
                                      };

            _ = client.Connect();

            var deadline = Stopwatch.StartNew();

            while (server.ClientHellos < 2 && deadline.Elapsed < Guard)
                await Task.Delay(50);

            Assert.That(server.ClientHellos, Is.GreaterThanOrEqualTo(2),
                        $"after {deadline.Elapsed.TotalSeconds:F1} s: the stalled attempt has to end before the policy can try again");

        }

    }

}
