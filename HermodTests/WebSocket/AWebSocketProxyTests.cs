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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Hermod HTTP WebSocket proxy tests.
    /// </summary>
    public abstract class AWebSocketProxyTests
    {

        #region Data

        protected WebSocketProxy?       webSocketProxy;
        protected IPPort                HTTPPortProxy;

        protected AWebSocketServer?     webSocketServer;
        protected IPPort                HTTPPortServer;
        protected IEnumerable<String>?  SecWebSocketProtocols;

        public AWebSocketProxyTests(IEnumerable<String>?  SecWebSocketProtocols   = null)
        {

            this.SecWebSocketProtocols  = SecWebSocketProtocols;

        }

        #endregion

        #region Init_WebSocketProxyAndServer()

        [SetUp]
        public void Init_WebSocketProxyAndServer()
        {

            webSocketServer  = new WebSocketMirrorServer(
                                   HTTPPort:                IPPort.Zero,
                                   SecWebSocketProtocols:   SecWebSocketProtocols,
                                   AutoStart:               true
                               );

            // The ports the system picked: the proxy reaches the server on the
            // one, the tests reach the proxy on the other.
            HTTPPortServer   = webSocketServer.IPPort;

            webSocketProxy   = new WebSocketProxy(
                                   UpstreamServerURL:       URL.Parse($"ws://127.0.0.1:{HTTPPortServer}"),
                                   AutoConnect:             true,

                                   HTTPPort:                IPPort.Zero,
                                   SecWebSocketProtocols:   SecWebSocketProtocols,
                                   AutoStart:               true
                               );

            HTTPPortProxy    = webSocketProxy.IPPort;

        }

        #endregion

        #region Shutdown_WebSocketProxyAndServer()

        [TearDown]
        public async Task Shutdown_WebSocketProxyAndServer()
        {

            if (webSocketServer is not null)
                await webSocketServer.Shutdown(Wait: true);

            webSocketServer = null;

            if (webSocketProxy is not null)
                await webSocketProxy.Shutdown(Wait: true);

            webSocketProxy  = null;

        }

        #endregion

    }

}
