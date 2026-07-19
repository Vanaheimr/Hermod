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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Tests for the optional server-side WebSocket subprotocol strictness (N6):
    /// when RequireMatchingSubprotocol is set, a client that offers only
    /// unsupported subprotocols is rejected with '400 Bad Request' instead of
    /// being upgraded without a 'Sec-WebSocket-Protocol' header.
    /// </summary>
    [TestFixture]
    public class WebSocketSubprotocolStrictnessTests
    {

        #region Data

        private WebSocketMirrorServer? server;

        #endregion

        #region (helper) StartServer(Port, Strict)

        private WebSocketMirrorServer StartServer(IPPort Port, Boolean Strict)
        {

            server = new WebSocketMirrorServer(
                         HTTPPort:               Port,
                         SecWebSocketProtocols:  [ "ocpp2.1", "ocpp2.0.1" ],
                         AutoStart:              true
                     );

            server.RequireMatchingSubprotocol = Strict;

            return server;

        }

        #endregion

        #region Shutdown()

        [TearDown]
        public async Task Shutdown()
        {
            if (server is not null)
                await server.Shutdown(Wait: true);
            server = null;
        }

        #endregion


        #region Strict_NoMatchingSubprotocol_Rejects400()

        [Test]
        public async Task Strict_NoMatchingSubprotocol_Rejects400()
        {

            var port    = IPPort.Parse(1141);
            StartServer(port, Strict: true);

            var client  = new WebSocketClient(
                              URL.Parse($"ws://127.0.0.1:{port}"),
                              SecWebSocketProtocols: [ "ocpp1.6" ]
                          );

            var (_, httpResponse) = await client.Connect();

            Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(400), httpResponse.EntirePDU);

        }

        #endregion

        #region Strict_MatchingSubprotocol_Upgrades101()

        [Test]
        public async Task Strict_MatchingSubprotocol_Upgrades101()
        {

            var port    = IPPort.Parse(1142);
            StartServer(port, Strict: true);

            var client  = new WebSocketClient(
                              URL.Parse($"ws://127.0.0.1:{port}"),
                              SecWebSocketProtocols: [ "ocpp2.1" ]
                          );

            var (_, httpResponse) = await client.Connect();

            Assert.Multiple(() => {
                Assert.That(httpResponse.HTTPStatusCode.Code,     Is.EqualTo(101),          httpResponse.EntirePDU);
                Assert.That(httpResponse.EntirePDU.Contains("ocpp2.1"), Is.True,             httpResponse.EntirePDU);
            });

        }

        #endregion

        #region NonStrict_NoMatchingSubprotocol_Upgrades101()

        [Test]
        public async Task NonStrict_NoMatchingSubprotocol_Upgrades101()
        {

            // Default behaviour (RFC 6455 Section 4.1): complete the handshake
            // without a 'Sec-WebSocket-Protocol' header even if nothing matched.
            var port    = IPPort.Parse(1143);
            StartServer(port, Strict: false);

            var client  = new WebSocketClient(
                              URL.Parse($"ws://127.0.0.1:{port}"),
                              SecWebSocketProtocols: [ "ocpp1.6" ]
                          );

            var (_, httpResponse) = await client.Connect();

            Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(101), httpResponse.EntirePDU);

        }

        #endregion

    }

}
