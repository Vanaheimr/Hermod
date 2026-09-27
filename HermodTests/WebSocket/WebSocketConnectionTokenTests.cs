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

using System.Text;
using System.Net.WebSockets;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A connection that has ended leaves nothing behind on the token of the
    /// server that accepted it.
    /// </summary>
    /// <remarks>
    /// The connection loop links a token source of its own to the server's
    /// token and to the connection's, and hands its token to every handler it
    /// calls. A linked source stays registered on the tokens it was linked to
    /// until it is disposed, and this one never was: every connection a server
    /// had ever handled stayed on the server's token - the linked source, a
    /// callback node on either side and, behind the node on the connection's
    /// side, the connection's own token source. About 350 bytes a connection,
    /// measured, for as long as the server runs, and a central system that its
    /// charging stations reconnect to around the clock runs for a long time.
    ///
    /// Asked of the connection's own token source, through a weak reference.
    /// Nothing else keeps it once its connection has ended, so whether it can
    /// be collected is whether the server's token still holds on to it.
    /// </remarks>
    [TestFixture]
    public class WebSocketConnectionTokenTests
    {

        #region Data

        /// <summary>
        /// Enough for "every one of them" to be a finding rather than an
        /// accident, and few enough to take well under a second.
        /// </summary>
        private const Int32             Connections = 25;

        private WebSocketMirrorServer?  server;
        private HTTPServer?             httpServer;

        #endregion

        #region TearDown()

        /// <summary>
        /// Whatever a test left running.
        /// </summary>
        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.Shutdown();

            if (httpServer is not null)
                await httpServer.Stop();

            server      = null;
            httpServer  = null;

        }

        #endregion


        #region AnEndedConnectionLeavesNothingOnTheServersToken()

        /// <summary>
        /// Connections to a server listening on a port of its own, each with a
        /// message there and back, each closed by the client: once they have
        /// ended, not one of them is still held by the server.
        /// </summary>
        [Test]
        public async Task AnEndedConnectionLeavesNothingOnTheServersToken()
        {

            server         = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            var sources    = await OpenAndClose(server, $"ws://127.0.0.1:{server.IPPort}/", Connections);
            var reachable  = await StillReachable(sources);

            Assert.Multiple(() => {
                Assert.That(sources,    Has.Length.EqualTo(Connections));
                Assert.That(reachable,  Is.Zero,  $"{reachable} of {Connections} ended connections are still held by the server's token.");
            });

        }

        #endregion

        #region NorDoesOneLentToAnHTTPPath()

        /// <summary>
        /// Nor does a connection that an HTTP server accepted and lent to the
        /// WebSocket server for one path. Its loop is handed the HTTP server's
        /// token instead, and that one lives as long as the HTTP server does.
        /// </summary>
        [Test]
        public async Task NorDoesOneLentToAnHTTPPath()
        {

            httpServer     = await HTTPServer.StartNew();
            server         = new WebSocketMirrorServer(RequireAuthentication: false, AutoStart: false);

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/lent"),
                HTTPDelegate: WebSocketUpgrade.For(server)
            );

            var sources    = await OpenAndClose(server, $"ws://127.0.0.1:{httpServer.TCPPort}/lent", Connections);
            var reachable  = await StillReachable(sources);

            Assert.Multiple(() => {
                Assert.That(sources,    Has.Length.EqualTo(Connections));
                Assert.That(reachable,  Is.Zero,  $"{reachable} of {Connections} ended connections are still held by the HTTP server's token.");
            });

        }

        #endregion

        #region NorDoesOneWhoseLoopFailed()

        /// <summary>
        /// Nor does a connection whose loop ended in an exception - and the
        /// token its handlers were given is cancelled, as it is for every other
        /// connection that is over.
        /// </summary>
        /// <remarks>
        /// A subprotocol selector that throws takes the loop down in the middle
        /// of the handshake, past the way out at its bottom, and leaves the rest
        /// to the loop's finally. The token its handlers were given has to end up
        /// cancelled there all the same: a source disposed without having been
        /// cancelled can never be cancelled any more, and disposing it unlinks it
        /// from the server's token too - so whatever a handler had started on its
        /// token would wait for ever, and not even the server stopping would end
        /// it.
        ///
        /// The selector, because it is one of the few things whose exception
        /// gets that far: what an event handler throws is caught where the
        /// event is raised.
        /// </remarks>
        [Test]
        public async Task NorDoesOneWhoseLoopFailed()
        {

            server                 = new WebSocketMirrorServer(
                                         HTTPPort:               IPPort.Zero,
                                         RequireAuthentication:  false,
                                         SubprotocolSelector:    (webSocketServer, connection, sharedSubprotocols) => throw new InvalidOperationException("A subprotocol selector that fails."),
                                         AutoStart:              true
                                     );

            WeakReference? source  = null;
            var cancelled          = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnNewTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, ct) => {

                source = new WeakReference(connection.CancellationTokenSource);

                // The registration is not kept: it points back at the token
                // source, and holding it here would hold that - and through it
                // the connection's - for as long as this test runs.
                ct.Register(() => cancelled.TrySetResult());

                return Task.CompletedTask;

            };

            using (var client  = new ClientWebSocket())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.IPPort}/"), timeout.Token);
                }
                catch (Exception)
                {
                    // Expected: the loop that should have answered is gone.
                }
            }

            var wasCancelled  = await Task.WhenAny(cancelled.Task, Task.Delay(TimeSpan.FromSeconds(5))) == cancelled.Task;
            var reachable     = source is not null
                                    ? await StillReachable([ source ])
                                    : -1;

            Assert.Multiple(() => {
                Assert.That(source,        Is.Not.Null,  "The connection was never reported.");
                Assert.That(wasCancelled,  Is.True,      "The token the connection's handlers were given was never cancelled.");
                Assert.That(reachable,     Is.Zero,      "The connection whose loop failed is still held by the server's token.");
            });

        }

        #endregion


        #region (private static) OpenAndClose  (Server, URL, Count)

        /// <summary>
        /// Open and close the given number of connections, one after another,
        /// and return a weak reference to each one's own token source once the
        /// server's loops for all of them have ended.
        /// </summary>
        private static async Task<WeakReference[]> OpenAndClose(AWebSocketServer  Server,
                                                                String            URL,
                                                                Int32             Count)
        {

            var sources  = new ConcurrentQueue<WeakReference>();
            var ended    = 0;

            Server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {
                sources.Enqueue(new WeakReference(connection.CancellationTokenSource));
                return Task.CompletedTask;
            };

            Server.OnTCPConnectionClosed    += (timestamp, webSocketServer, connection, eventTrackingId, reason, ct) => {
                Interlocked.Increment(ref ended);
                return Task.CompletedTask;
            };

            for (var i = 0; i < Count; i++)
            {

                using var client   = new ClientWebSocket();
                using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                await client.ConnectAsync(new Uri(URL), timeout.Token);

                // One message there and back, so that the loop has been through a
                // frame and a handler, and not only through the handshake.
                var buffer = new Byte[16];

                await client.SendAsync("hello"u8.ToArray(), WebSocketMessageType.Text, true, timeout.Token);

                var reply  = await client.ReceiveAsync(buffer, timeout.Token);

                Assert.That(Encoding.UTF8.GetString(buffer, 0, reply.Count), Is.EqualTo("olleh"));

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);

            }

            // The client is done once the server has answered its close frame,
            // and the server's loop a moment after that. Asserted rather than
            // merely waited for: a loop that is still running holds on to its
            // connection, and the check for what is left behind would report it
            // as a leak it is not.
            var allEnded = SpinWait.SpinUntil(() => Volatile.Read(ref ended) >= Count, TimeSpan.FromSeconds(10));

            Assert.That(allEnded, Is.True, $"Only {Volatile.Read(ref ended)} of {Count} connection loops had ended after ten seconds.");

            return [.. sources];

        }

        #endregion

        #region (private static) StillReachable(References)

        /// <summary>
        /// How many of the given objects are still reachable once the garbage
        /// collector has had every chance to prove otherwise.
        /// </summary>
        /// <remarks>
        /// Several collections, a moment apart, rather than one: the last of the
        /// connections may still be on its way out - its loop has raised
        /// OnTCPConnectionClosed and has not returned yet.
        /// </remarks>
        private static async Task<Int32> StillReachable(IEnumerable<WeakReference> References)
        {

            var deadline   = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            var reachable  = 0;

            do
            {

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                reachable = References.Count(reference => reference.IsAlive);

                if (reachable == 0)
                    break;

                await Task.Delay(100);

            }
            while (DateTime.UtcNow < deadline);

            return reachable;

        }

        #endregion

    }

}
