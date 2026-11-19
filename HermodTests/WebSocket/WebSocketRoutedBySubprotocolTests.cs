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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// One port for two WebSocket servers: the listener of the one hands each
    /// WebSocket to the server that speaks the subprotocol its client offers
    /// first - which then does the handshake, the authentication and the
    /// messages, as if it had accepted the connection itself.
    /// </summary>
    [TestFixture]
    public class WebSocketRoutedBySubprotocolTests
    {

        #region Data

        private WebSocketMirrorServer?  listening;
        private WebSocketMirrorServer?  routed;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void StartTheServers()
        {

            listening  = new WebSocketMirrorServer(
                             HTTPPort:               IPPort.Zero,
                             RequireAuthentication:  false,
                             SecWebSocketProtocols:  [ "ocpp2.1", "ocpp2.0.1" ],
                             AutoStart:              true
                         );

            // Never started: it has no port of its own.
            routed     = new WebSocketMirrorServer(
                             HTTPPort:               IPPort.Zero,
                             RequireAuthentication:  false,
                             SecWebSocketProtocols:  [ "ocpp1.6" ],
                             AutoStart:              false
                         );

            listening.RouteBySubprotocol(routed);

        }

        [TearDown]
        public async Task StopTheServers()
        {

            if (listening is not null)
            {
                await listening.Shutdown(Wait: true);
                await listening.DisposeAsync();
            }

            if (routed is not null)
                await routed.DisposeAsync();

            listening  = null;
            routed     = null;

        }

        #endregion


        #region (helper) Client(params Subprotocols)

        private WebSocketClient Client(params String[] Subprotocols)

            => new (
                   URL.Parse($"ws://127.0.0.1:{listening!.IPPort}"),
                   SecWebSocketProtocols: Subprotocols
               );

        #endregion


        #region EachWebSocketGoesToTheServerOfItsSubprotocol()

        /// <summary>
        /// A client of OCPP 1.6 is the routed server's, one of OCPP 2.1 the
        /// listening server's: each answered with its own subprotocol, and each
        /// kept by its own server alone.
        /// </summary>
        [Test]
        public async Task EachWebSocketGoesToTheServerOfItsSubprotocol()
        {

            await using var client16 = Client("ocpp1.6");
            await using var client21 = Client("ocpp2.1");

            var (_, answer16) = await client16.Connect();
            var (_, answer21) = await client21.Connect();

            Assert.Multiple(() => {

                Assert.That(answer16.HTTPStatusCode.Code,          Is.EqualTo(101), answer16.EntirePDU);
                Assert.That(answer21.HTTPStatusCode.Code,          Is.EqualTo(101), answer21.EntirePDU);

                Assert.That(answer16.SecWebSocketProtocol,         Is.EqualTo("ocpp1.6"));
                Assert.That(answer21.SecWebSocketProtocol,         Is.EqualTo("ocpp2.1"));

                Assert.That(routed!.   WebSocketConnections.Select(connection => connection.Subprotocol),
                            Is.EqualTo(new[] { "ocpp1.6" }),
                            "The WebSocket of OCPP 1.6 is not the routed server's.");

                Assert.That(listening!.WebSocketConnections.Select(connection => connection.Subprotocol),
                            Is.EqualTo(new[] { "ocpp2.1" }),
                            "The WebSocket of OCPP 2.1 is not the listening server's.");

                Assert.That(routed.RoutedBy,                        Is.SameAs(listening));

            });

        }

        #endregion

        #region TheRoutedServerSpeaksTheMessages()

        /// <summary>
        /// What comes in over a WebSocket handed on is the routed server's to
        /// answer: a mirror sends it back, reversed.
        /// </summary>
        [Test]
        public async Task TheRoutedServerSpeaksTheMessages()
        {

            await using var client = Client("ocpp1.6");

            var received = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.OnTextMessageReceived += (timestamp, webSocketClient, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                received.TrySetResult(textMessage);
                return Task.CompletedTask;
            };

            var (_, answer) = await client.Connect();

            Assert.That(answer.HTTPStatusCode.Code, Is.EqualTo(101), answer.EntirePDU);

            await client.SendTextMessage("hello sixteen");

            // A mirror sends it back the other way round.
            Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo("neetxis olleh"));

        }

        #endregion

        #region TheFirstSubprotocolTheClientOffersChooses()

        /// <summary>
        /// A client that speaks both goes where its first choice is spoken.
        /// </summary>
        [Test]
        public async Task TheFirstSubprotocolTheClientOffersChooses()
        {

            await using var client = Client("ocpp1.6", "ocpp2.1");

            var (_, answer) = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(answer.HTTPStatusCode.Code,                  Is.EqualTo(101), answer.EntirePDU);
                Assert.That(answer.SecWebSocketProtocol,                 Is.EqualTo("ocpp1.6"));
                Assert.That(routed!.WebSocketConnections.Count(),        Is.EqualTo(1));
                Assert.That(listening!.WebSocketConnections.Count(),     Is.Zero);
            });

        }

        #endregion

        #region TheRoutedServerAuthenticatesItsOwnClients()

        /// <summary>
        /// The routed server asks for its own credentials: a client of its
        /// subprotocol without them is turned away by it, although the
        /// listening server would let anybody in.
        /// </summary>
        [Test]
        public async Task TheRoutedServerAuthenticatesItsOwnClients()
        {

            routed!.RequireAuthentication = true;
            routed.AddOrUpdateHTTPBasicAuth("station", "a-password-of-some-length");

            await using var anonymous = Client("ocpp1.6");

            var (_, refused) = await anonymous.Connect();

            await using var signedIn = new WebSocketClient(
                                           URL.Parse($"ws://127.0.0.1:{listening!.IPPort}"),
                                           SecWebSocketProtocols:  [ "ocpp1.6" ],
                                           HTTPAuthentication:     HTTPBasicAuthentication.Create("station", "a-password-of-some-length")
                                       );

            var (_, accepted) = await signedIn.Connect();

            Assert.Multiple(() => {
                Assert.That(refused. HTTPStatusCode.Code, Is.EqualTo(401), refused. EntirePDU);
                Assert.That(accepted.HTTPStatusCode.Code, Is.EqualTo(101), accepted.EntirePDU);
            });

        }

        #endregion

        #region WhatNobodySpeaksStaysWithTheListeningServer()

        /// <summary>
        /// A subprotocol no server speaks is the listening server's to answer,
        /// as it would have been without any routing.
        /// </summary>
        [Test]
        public async Task WhatNobodySpeaksStaysWithTheListeningServer()
        {

            listening!.RequireMatchingSubprotocol = true;

            await using var client = Client("mqtt");

            var (_, answer) = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(answer.HTTPStatusCode.Code,          Is.EqualTo(400), answer.EntirePDU);
                Assert.That(routed!.WebSocketConnections,        Is.Empty);
            });

        }

        #endregion

        #region AServerIsRoutedToByOneListenerOnly()

        [Test]
        public void AServerIsRoutedToByOneListenerOnly()
        {

            using var another = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, AutoStart: false);

            Assert.Multiple(() => {

                Assert.That(() => another.RouteBySubprotocol(routed!),    Throws.ArgumentException);
                Assert.That(() => routed!.RouteBySubprotocol(another),    Throws.InvalidOperationException,
                            "A server handed WebSockets hands them on itself.");

                Assert.That(listening!.StopRouting(routed!),              Is.True);
                Assert.That(routed!.RoutedBy,                             Is.Null);

            });

        }

        #endregion

    }

}
