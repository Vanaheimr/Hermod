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

using System.Net.WebSockets;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket server that is shut down says so: every client is sent a
    /// close frame, 1001 going away, before its connection ends.
    /// </summary>
    /// <remarks>
    /// Shutdown() closed each connection with Close(), and Close() sends a close
    /// frame only where it is given a status or a reason - so it sent none, and
    /// every client found out from a connection that broke. .NET's
    /// ClientWebSocket says so in as many words: "The remote party closed the
    /// WebSocket connection without completing the close handshake." Found by a
    /// charging station that wanted to tell the apps on its WebSocket that it
    /// was stopping, and had to send the frames itself.
    ///
    /// The message Shutdown() has always taken, and never used, is the reason in
    /// that frame now - cut where it is longer than a close frame can carry, and
    /// where a character ends.
    /// </remarks>
    [TestFixture]
    public class WebSocketShutdownTests
    {

        #region Data

        private WebSocketMirrorServer?  server;
        private HTTPServer?             httpServer;

        #endregion

        #region TearDown()

        /// <summary>
        /// Whatever a test left running, for a test that failed before it could
        /// shut its own server down.
        /// </summary>
        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
            {
                await server.Shutdown();
                await server.DisposeAsync();
            }

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            server      = null;
            httpServer  = null;

        }

        #endregion


        #region EveryClientIsToldTheServerIsGoingAway()

        /// <summary>
        /// Two clients, and both are told.
        /// </summary>
        [Test]
        public async Task EveryClientIsToldTheServerIsGoingAway()
        {

            server            = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            using var first   = await Connect($"ws://127.0.0.1:{server.IPPort}/");
            using var second  = await Connect($"ws://127.0.0.1:{server.IPPort}/");

            var toldFirst     = ReceiveClose(first);
            var toldSecond    = ReceiveClose(second);

            await ShutDownTheServer();

            foreach (var told in new[] { await toldFirst, await toldSecond })
                Assert.That(told.Status, Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable), told.Problem);

        }

        #endregion

        #region TheMessageIsTheReason()

        /// <summary>
        /// What the server is shut down with is what the clients read.
        /// </summary>
        [Test]
        public async Task TheMessageIsTheReason()
        {

            server          = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            using var client = await Connect($"ws://127.0.0.1:{server.IPPort}/");

            var told        = ReceiveClose(client);

            await ShutDownTheServer("Back after the maintenance window.");

            var (status, reason, problem) = await told;

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable), problem);
                Assert.That(reason, Is.EqualTo("Back after the maintenance window."));
            });

        }

        #endregion

        #region AMessageTooLongForACloseFrameIsCutWhereACharacterEnds()

        /// <summary>
        /// A close frame is a control frame, and a control frame carries at most
        /// 125 bytes: two of status, 123 of reason. A longer message is cut to
        /// that - and not in the middle of a character, which would leave the
        /// client with a reason that is no UTF-8 and a protocol error instead of
        /// a goodbye.
        /// </summary>
        [Test]
        public async Task AMessageTooLongForACloseFrameIsCutWhereACharacterEnds()
        {

            server           = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            using var client = await Connect($"ws://127.0.0.1:{server.IPPort}/");

            var told         = ReceiveClose(client);

            // A hundred characters of two bytes each, and a last one of four:
            // 204 bytes, of which 61 characters - 122 bytes - fit.
            await ShutDownTheServer(new String('ä', 100) + "😀");

            var (status, reason, problem) = await told;

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable), problem);
                Assert.That(reason, Is.EqualTo(new String('ä', 61)));
            });

        }

        #endregion

        #region AServerLentToAnHTTPPathSaysSoToo()

        /// <summary>
        /// And a server that never listened on a port of its own, whose
        /// connections an HTTP server handed it for one path, says goodbye the
        /// same way.
        /// </summary>
        [Test]
        public async Task AServerLentToAnHTTPPathSaysSoToo()
        {

            httpServer       = await HTTPServer.StartNew();
            server           = new WebSocketMirrorServer(RequireAuthentication: false, AutoStart: false);

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/lent"),
                HTTPDelegate: WebSocketUpgrade.For(server)
            );

            using var client = await Connect($"ws://127.0.0.1:{httpServer.TCPPort}/lent");

            var told         = ReceiveClose(client);

            await ShutDownTheServer();

            var (status, _, problem) = await told;

            Assert.That(status, Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable), problem);

        }

        #endregion

        #region AClientWhoseAnswerWasOnItsWayIsToldAsWell()

        /// <summary>
        /// A server shut down while it was still sending a client its 101 tells
        /// that client as well: the close waits for the 101 to have gone out,
        /// and the client - on the WebSocket as soon as it has read it - is
        /// sent the close frame after it.
        /// </summary>
        /// <remarks>
        /// Such a close used to find the connection not yet switched, which it
        /// is only once the send of the 101 has returned, and closed it without
        /// a frame: the client either never got its 101 or read it and then a
        /// connection that broke. Found by a charging station stopped just after
        /// an app had connected to it. The 101 goes out a byte at a time here,
        /// which stretches that moment from a few instructions to some hundred
        /// milliseconds at least.
        /// </remarks>
        [Test]
        public async Task AClientWhoseAnswerWasOnItsWayIsToldAsWell()
        {

            var shuttingDown  = new WebSocketMirrorServer(HTTPPort:                    IPPort.Zero,
                                                          RequireAuthentication:       false,
                                                          SlowNetworkSimulationDelay:  TimeSpan.FromMilliseconds(1),
                                                          AutoStart:                   true);

            server            = shuttingDown;

            Task? shutdown    = null;

            // Accepted, and its 101 about to be sent: the server is shut down
            // once that send has begun - its answer is set just before - and
            // long before it can have ended.
            shuttingDown.OnWebSocketConnectionAccepted += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {

                shutdown = Task.Run(async () => {

                               while (connection.HTTPResponse is null)
                                   await Task.Delay(1);

                               await Task.Delay(30);

                               await shuttingDown.Shutdown("Going.");

                           });

                return Task.CompletedTask;

            };

            ClientWebSocket? client  = null;
            String?          failed  = null;

            try
            {
                client = await Connect($"ws://127.0.0.1:{shuttingDown.IPPort}/");
            }
            catch (Exception e)
            {
                failed = e.Message;
            }

            var (status, reason, problem) = client is not null
                                                ? await ReceiveClose(client)
                                                : (null, null, $"The client never got onto the WebSocket: {failed}");

            if (shutdown is not null)
                await shutdown;

            server = null;
            await shuttingDown.DisposeAsync();
            client?.Dispose();

            Assert.Multiple(() => {
                Assert.That(shutdown,  Is.Not.Null,                                         "the server never accepted the connection");
                Assert.That(status,    Is.EqualTo(WebSocketCloseStatus.EndpointUnavailable), problem);
                Assert.That(reason,    Is.EqualTo("Going."));
            });

        }

        #endregion


        #region (private) ShutDownTheServer(Message = null)

        /// <summary>
        /// Shut the test's WebSocket server down, so that TearDown does not.
        /// </summary>
        private async Task ShutDownTheServer(String? Message = null)
        {

            var shuttingDown = server!;

            server = null;

            await shuttingDown.Shutdown(Message);
            await shuttingDown.DisposeAsync();

        }

        #endregion

        #region (private static) Connect(URL)

        private static async Task<ClientWebSocket> Connect(String URL)
        {

            var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri(URL), CancellationToken.None);

            return client;

        }

        #endregion

        #region (private static) ReceiveClose(Client)

        /// <summary>
        /// What the client was told when its connection ended: the status and
        /// the reason of the close frame - or, where there was none, why not.
        /// </summary>
        private static async Task<(WebSocketCloseStatus? Status, String? Reason, String Problem)> ReceiveClose(ClientWebSocket Client)
        {

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {

                var result = await Client.ReceiveAsync(new Byte[1024], timeout.Token);

                return result.MessageType == WebSocketMessageType.Close
                           ? (result.CloseStatus, result.CloseStatusDescription, "")
                           : (null, null, $"The client was sent a {result.MessageType} frame rather than a close frame.");

            }
            catch (Exception e)
            {
                return (null, null, $"The client was sent no close frame: {e.Message}");
            }

        }

        #endregion

    }

}
