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
    /// Removing a connection removes that connection, and not another one
    /// that came from the same remote socket.
    /// </summary>
    /// <remarks>
    /// The server keeps its connections by remote socket. A client that comes
    /// back from the same port takes the entry of the connection before it,
    /// and that one is cleaned up whenever its loop ends - which can be after
    /// the new one came in. Removing by remote socket alone then took the new
    /// connection off the books while it went on running.
    ///
    /// A client cannot be made to reuse its port on demand, so the earlier
    /// connection here is one made over a stream with the live connection's
    /// sockets, as a late cleanup of it would see them.
    /// </remarks>
    [TestFixture]
    public class WebSocketRemoveConnectionTests
    {

        #region Data

        private WebSocketMirrorServer?  server;
        private WebSocketClient?        client;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.DisposeAsync();

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

            client  = null;
            server  = null;

        }

        #endregion


        #region (private) Connect()

        /// <summary>
        /// Start a server, connect a client to it, and return the connection
        /// as the server sees it.
        /// </summary>
        private async Task<WebSocketServerConnection> Connect()
        {

            server         = new WebSocketMirrorServer(HTTPPort:               IPPort.Zero,
                                                       RequireAuthentication:  false,
                                                       AutoStart:              true);

            var connected  = new TaskCompletionSource<WebSocketServerConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {
                connected.TrySetResult(connection);
                return Task.CompletedTask;
            };

            client         = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            await client.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            // Raised once the 101 is out, which the client may notice first.
            return await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        }

        #endregion


        #region AnEarlierConnectionFromTheSameSocketLeavesTheLiveOneListed()

        /// <summary>
        /// An earlier connection from the same remote socket, removed after the
        /// live one came in: the live one stays listed, and is removed when it
        /// is its own turn.
        /// </summary>
        [Test]
        public async Task AnEarlierConnectionFromTheSameSocketLeavesTheLiveOneListed()
        {

            var live     = await Connect();
            var earlier  = new WebSocketServerConnection(server!, new MemoryStream(), live.LocalSocket, live.RemoteSocket);

            Assert.That(server!.WebSocketConnections, Does.Contain(live));

            var earlierRemoved = server.RemoveConnection(earlier);

            Assert.Multiple(() => {
                Assert.That(earlierRemoved,               Is.False);
                Assert.That(server.WebSocketConnections,  Does.Contain(live),
                            "Removing the earlier connection took the live one off the books.");
            });

            var liveRemoved = server.RemoveConnection(live);

            Assert.Multiple(() => {
                Assert.That(liveRemoved,                  Is.True);
                Assert.That(server.WebSocketConnections,  Does.Not.Contain(live));
            });

        }

        #endregion


    }

}
