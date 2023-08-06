/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP.WebSockets
{

    /// <summary>
    /// Hermod HTTP WebSocket server tests endpoints.
    /// </summary>
    public abstract class AWebSocketServerTests
    {

        #region Data

        protected WebSocketServer?      webSocketServer;
        protected IPPort                HTTPPort;
        protected IEnumerable<String>?  SecWebSocketProtocols;

        public AWebSocketServerTests(IPPort                HTTPPort,
                                     IEnumerable<String>?  SecWebSocketProtocols   = null)
        {

            this.HTTPPort               = HTTPPort;
            this.SecWebSocketProtocols  = SecWebSocketProtocols;

        }

        #endregion

        #region Init_WebSocketServer()

        [SetUp]
        public void Init_WebSocketServer()
        {

            webSocketServer = new WebSocketServer2(
                                  HTTPPort:               HTTPPort,
                                  SecWebSocketProtocols:  SecWebSocketProtocols,
                                  AutoStart:              true
                              );

        }

        #endregion

        #region Shutdown_WebSocketServer()

        [TearDown]
        public void Shutdown_WebSocketServer()
        {
            webSocketServer?.Shutdown(Wait: true);
            webSocketServer = null;
        }

        #endregion


    }

}
