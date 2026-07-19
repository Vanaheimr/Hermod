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
    /// Tests for the server-side WebSocket handshake hardening: the optional
    /// 'Origin' allow-list (Cross-Site WebSocket Hijacking protection). The
    /// timeout / max-header-size / per-IP-limit guards are exercised by their
    /// defaults not breaking the regular handshake (see the other WebSocket tests).
    /// </summary>
    [TestFixture]
    public class WebSocketHandshakeHardeningTests
    {

        #region Data

        private WebSocketMirrorServer? server;

        #endregion

        #region (helper) StartServer(Port, AllowedOrigins)

        private WebSocketMirrorServer StartServer(IPPort Port, params String[] AllowedOrigins)
        {

            server = new WebSocketMirrorServer(
                         HTTPPort:   Port,
                         AutoStart:  true
                     );

            foreach (var origin in AllowedOrigins)
                server.AllowedOrigins.Add(origin);

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


        #region Origin_NotInAllowList_Rejects403()

        [Test]
        public async Task Origin_NotInAllowList_Rejects403()
        {

            var port    = IPPort.Parse(1144);
            StartServer(port, "https://good.example");

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"));

            var (_, httpResponse) = await client.Connect(
                                        HTTPRequestBuilder: builder => builder.SetHeaderField("Origin", "https://evil.example")
                                    );

            Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(403), httpResponse.EntirePDU);

        }

        #endregion

        #region Origin_InAllowList_Upgrades101()

        [Test]
        public async Task Origin_InAllowList_Upgrades101()
        {

            var port    = IPPort.Parse(1145);
            StartServer(port, "https://good.example");

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"));

            var (_, httpResponse) = await client.Connect(
                                        HTTPRequestBuilder: builder => builder.SetHeaderField("Origin", "https://good.example")
                                    );

            Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(101), httpResponse.EntirePDU);

        }

        #endregion

        #region NoOrigin_WithAllowList_Upgrades101()

        [Test]
        public async Task NoOrigin_WithAllowList_Upgrades101()
        {

            // Non-browser clients (e.g. OCPP charging stations) send no Origin header
            // and must still be accepted even when an allow-list is configured.
            var port    = IPPort.Parse(1146);
            StartServer(port, "https://good.example");

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"));

            var (_, httpResponse) = await client.Connect();

            Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(101), httpResponse.EntirePDU);

        }

        #endregion

    }

}
