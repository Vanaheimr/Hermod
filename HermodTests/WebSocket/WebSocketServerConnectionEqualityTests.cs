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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Two connections to one WebSocket server are two connections: not equal,
    /// and two keys of a dictionary.
    /// </summary>
    /// <remarks>
    /// Equality, the hash code and the order used to look at the local socket
    /// alone. On the server that is the server's own address and port, the
    /// same for every connection it accepted, so all of them were equal: a
    /// dictionary keyed by connections kept one entry for all clients, and ==
    /// between two different clients was true.
    /// </remarks>
    [TestFixture]
    public class WebSocketServerConnectionEqualityTests
    {

        #region Data

        private WebSocketMirrorServer?  server;
        private WebSocketClient?        client1;
        private WebSocketClient?        client2;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client1 is not null)
                await client1.DisposeAsync();

            if (client2 is not null)
                await client2.DisposeAsync();

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

            client1  = null;
            client2  = null;
            server   = null;

        }

        #endregion


        #region (private) StartServer()

        private WebSocketMirrorServer StartServer()

            => server = new WebSocketMirrorServer(HTTPPort:               IPPort.Zero,
                                                  RequireAuthentication:  false,
                                                  AutoStart:              true);

        #endregion


        #region TwoClientsAreTwoConnections()

        /// <summary>
        /// Two clients connected at the same time: their connections share the
        /// local socket, and are still neither equal nor one dictionary key.
        /// </summary>
        [Test]
        public async Task TwoClientsAreTwoConnections()
        {

            var server       = StartServer();
            var connections  = new ConcurrentQueue<WebSocketServerConnection>();
            var bothThere    = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {
                connections.Enqueue(connection);
                if (connections.Count == 2)
                    bothThere.TrySetResult();
                return Task.CompletedTask;
            };

            client1 = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));
            client2 = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            await Task.WhenAll(client1.Connect(), client2.Connect()).WaitAsync(TimeSpan.FromSeconds(10));

            // Raised once the 101 is out, which the clients may notice first.
            await bothThere.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var a = connections.ElementAt(0);
            var b = connections.ElementAt(1);

            var dictionary = new Dictionary<WebSocketServerConnection, String> {
                                 { a, "a" },
                                 { b, "b" }
                             };

            Assert.Multiple(() => {

                // What made them one before.
                Assert.That(a.LocalSocket,       Is.EqualTo(b.LocalSocket));
                Assert.That(a.RemoteSocket,      Is.Not.EqualTo(b.RemoteSocket));

                Assert.That(a.Equals(b),         Is.False);
                Assert.That(a.Equals((Object) b), Is.False);
                Assert.That(a == b,              Is.False);
                Assert.That(a != b,              Is.True);
                Assert.That(a.CompareTo(b),      Is.Not.Zero);
                Assert.That(a <  b,              Is.Not.EqualTo(b < a));

                Assert.That(dictionary,          Has.Count.EqualTo(2));
                Assert.That(dictionary[a],       Is.EqualTo("a"));
                Assert.That(dictionary[b],       Is.EqualTo("b"));
                Assert.That(new HashSet<WebSocketServerConnection> { a, b },  Has.Count.EqualTo(2));

                // And each still equals itself.
                Assert.That(a.Equals(a),         Is.True);
                Assert.That(a.CompareTo(a),      Is.Zero);

            });

        }

        #endregion

        #region TwoConnectionsWithTheSameSocketsAreStillTwo()

        /// <summary>
        /// Two connections with the very same sockets, as a client coming back
        /// from the same port would have: still not equal, and ordered by which
        /// was made first.
        /// </summary>
        [Test]
        public void TwoConnectionsWithTheSameSocketsAreStillTwo()
        {

            var server  = StartServer();
            var local   = IPSocket.Parse("127.0.0.1:8000");
            var remote  = IPSocket.Parse("127.0.0.1:50000");

            var first   = new WebSocketServerConnection(server, new MemoryStream(), local, remote);
            var second  = new WebSocketServerConnection(server, new MemoryStream(), local, remote);

            Assert.Multiple(() => {

                Assert.That(first.Equals(second),       Is.False);
                Assert.That(first == second,            Is.False);
                Assert.That(first.CompareTo(second),    Is.Negative);
                Assert.That(second.CompareTo(first),    Is.Positive);

                Assert.That(new HashSet<WebSocketServerConnection> { first, second },  Has.Count.EqualTo(2));

            });

        }

        #endregion


    }

}
