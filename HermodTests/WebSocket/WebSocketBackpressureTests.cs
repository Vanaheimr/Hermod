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
    /// Tests for the send backpressure limit (uWebSockets "maxBackpressure" model).
    /// Exercised on the client send path, which shares its implementation with the
    /// server connection. A message whose frame exceeds the configured backpressure
    /// limit is either dropped or fails the connection, depending on the behaviour.
    /// </summary>
    [TestFixture]
    public class WebSocketBackpressureTests
    {

        #region Data

        private WebSocketMirrorServer? server;

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


        #region Backpressure_MessageExceedsLimit_DropsMessage()

        [Test]
        public async Task Backpressure_MessageExceedsLimit_DropsMessage()
        {

            var port    = IPPort.Parse(1147);
            server      = new WebSocketMirrorServer(HTTPPort: port, AutoStart: true);

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}")) {
                              MaxBackpressure        = 4,   // any real frame is larger
                              BackpressureBehaviour  = WebSocketBackpressureBehaviour.DropMessage
                          };

            await client.Connect();

            var sentStatus = await client.SendTextMessage("hello world");

            Assert.That(sentStatus, Is.EqualTo(SentStatus.Dropped));

        }

        #endregion

        #region Backpressure_MessageExceedsLimit_ClosesConnection()

        [Test]
        public async Task Backpressure_MessageExceedsLimit_ClosesConnection()
        {

            var port    = IPPort.Parse(1148);
            server      = new WebSocketMirrorServer(HTTPPort: port, AutoStart: true);

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}")) {
                              MaxBackpressure        = 4,
                              BackpressureBehaviour  = WebSocketBackpressureBehaviour.CloseConnection
                          };

            await client.Connect();

            var sentStatus = await client.SendTextMessage("hello world");

            Assert.That(sentStatus, Is.EqualTo(SentStatus.FatalError));

        }

        #endregion

        #region Backpressure_UnderLimit_Succeeds()

        [Test]
        public async Task Backpressure_UnderLimit_Succeeds()
        {

            var port    = IPPort.Parse(1149);
            server      = new WebSocketMirrorServer(HTTPPort: port, AutoStart: true);

            var client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}")) {
                              MaxBackpressure        = 1024 * 1024,   // generous
                              BackpressureBehaviour  = WebSocketBackpressureBehaviour.CloseConnection
                          };

            await client.Connect();

            var sentStatus = await client.SendTextMessage("hello world");

            Assert.That(sentStatus, Is.EqualTo(SentStatus.Success));

        }

        #endregion

    }

}
