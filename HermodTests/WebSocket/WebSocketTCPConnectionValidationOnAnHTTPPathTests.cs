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

using System.Net;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket server lent to an HTTP path asks its handlers of
    /// OnValidateTCPConnection about the connection the HTTP server accepted,
    /// and answers a refusal with 403 Forbidden.
    /// </summary>
    /// <remarks>
    /// On a port of its own a WebSocket server accepts the TCP connection
    /// itself, and ATCPServer asks ValidateConnection - and through it every
    /// handler of OnValidateTCPConnection - before a byte of HTTP is read.
    ///
    /// Lent to a path by WebSocketUpgrade.For() it accepts nothing. The HTTP
    /// server accepted the connection and read the request, and hands the
    /// stream over to AcceptUpgradedConnectionAsync, which went straight to the
    /// handshake. ValidateConnection was never called on that path, so a
    /// handler meant as a firewall was never asked, kept nobody out, and
    /// nothing said so.
    ///
    /// Whether the same answers give the same verdict on both paths is
    /// WebSocketTCPConnectionValidationTests' question, which runs every test
    /// for both arrangements. What is asked here is particular to a lent
    /// server: what a refused client is told, which connection the handlers are
    /// shown, and what happens when whoever hands a stream over has no TCP
    /// connection to go with it.
    /// </remarks>
    [TestFixture]
    public class WebSocketTCPConnectionValidationOnAnHTTPPathTests
    {

        #region Data

        private HTTPServer?             httpServer;
        private WebSocketMirrorServer?  server;

        #endregion

        #region SetUp()

        /// <summary>
        /// An HTTP server, and a WebSocket server lent to its path /lent.
        /// </summary>
        /// <remarks>
        /// The WebSocket server is not started, and that is the arrangement under
        /// test: it never listens and never accepts, so whatever it asks about a
        /// connection it has to ask about one it was handed. It asks nobody for
        /// credentials, so that the handlers under test are the only thing that
        /// can turn a client away.
        /// </remarks>
        [SetUp]
        public async Task SetUp()
        {

            httpServer  = await HTTPServer.StartNew();

            server      = new WebSocketMirrorServer(
                              RequireAuthentication:  false,
                              AutoStart:              false
                          );

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/lent"),
                HTTPDelegate: WebSocketUpgrade.For(server)
            );

        }

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.Shutdown(Wait: true);

            if (httpServer is not null)
                await httpServer.Stop();

            server      = null;
            httpServer  = null;

        }

        #endregion


        #region ARefusalIsAnsweredWithForbiddenAndWithoutItsReason()

        /// <summary>
        /// A refused client is answered 403 Forbidden - and is not told why.
        /// </summary>
        /// <remarks>
        /// On a port of its own the server resets a connection it refuses,
        /// before a byte of HTTP is read. Here the handshake has been read and
        /// the client is waiting for an HTTP answer, as it gets for every other
        /// refusal on this path. Why goes only to OnNewTCPConnectionRejected: a
        /// reason can carry the message of a handler that failed, and a
        /// firewall does not explain itself to whoever it keeps out.
        /// </remarks>
        [Test]
        public async Task ARefusalIsAnsweredWithForbiddenAndWithoutItsReason()
        {

            server!.OnValidateTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) =>
                                                   Task.FromResult(ConnectionFilterResponse.Rejected("Not from this network."));

            using var client  = await ConnectToTheLentPath();

            var answer        = await ReadUntilClosed(client.GetStream());

            Assert.Multiple(() => {
                Assert.That(answer, Does.StartWith("HTTP/1.1 403"),                answer);
                Assert.That(answer, Does.Not.Contain("Not from this network."),   "The client was told why it was refused.");
            });

        }

        #endregion

        #region TheHandlersAreShownTheConnectionTheHTTPServerAccepted()

        /// <summary>
        /// The TcpClient a handler is shown is the HTTP server's own, the one
        /// the client is connected to - and not null, and not a stand-in.
        /// </summary>
        /// <remarks>
        /// A firewall that is shown some other connection, or none, can only
        /// guess. The ports are compared, and not the addresses: a listener in
        /// dual mode sees 127.0.0.1 as ::ffff:127.0.0.1, and on the loopback a
        /// port is enough to tell one connection from another.
        /// </remarks>
        [Test]
        public async Task TheHandlersAreShownTheConnectionTheHTTPServerAccepted()
        {

            IPEndPoint? shown = null;

            server!.OnValidateTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                shown = connection?.Client?.RemoteEndPoint as IPEndPoint;
                return Task.FromResult(ConnectionFilterResponse.Rejected("Seen enough."));
            };

            using var client  = await ConnectToTheLentPath();

            await ReadUntilClosed(client.GetStream());

            Assert.That(shown?.Port,
                        Is.EqualTo(((IPEndPoint) client.Client.LocalEndPoint!).Port),
                        "The handler was not shown the connection the client is on.");

        }

        #endregion

        #region AConnectionHandedOverWithoutItsTCPConnection(HandlersAreWaiting)

        /// <summary>
        /// A stream handed over with no TCP connection to go with it: refused
        /// where handlers are waiting to be asked, and let in where nobody is.
        /// </summary>
        /// <remarks>
        /// WebSocketUpgrade always has a connection to hand over along with the
        /// stream, but AcceptUpgradedConnectionAsync can be called by anybody,
        /// and not everybody has one. A handler that is there to look at the
        /// connection cannot look, then - and that is a refusal, for the reason
        /// a handler that fails is one. Even a handler that would have said yes
        /// cannot say it unasked, which is why the one here would have.
        /// </remarks>
        /// <param name="HandlersAreWaiting">Whether OnValidateTCPConnection has a handler.</param>
        [TestCase(true,  TestName = "AConnectionHandedOverWithoutItsTCPConnection(is refused where handlers are waiting to be asked)")]
        [TestCase(false, TestName = "AConnectionHandedOverWithoutItsTCPConnection(is let in where nobody is asking)")]
        public async Task AConnectionHandedOverWithoutItsTCPConnection(Boolean HandlersAreWaiting)
        {

            var lentServer  = server!;
            var asked       = 0;
            var refusal     = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (HandlersAreWaiting)
                lentServer.OnValidateTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(ConnectionFilterResponse.Accepted());
                };

            lentServer.OnNewTCPConnectionRejected += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, reason) => {
                refusal.TrySetResult(reason.FirstText());
                return Task.CompletedTask;
            };

            // A connection of the test's own, handed over the way something
            // other than this library's HTTP server might: a stream and two
            // sockets, and no TCP connection to go with them.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            using var client    = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port);

            using var accepted  = await listener.AcceptTcpClientAsync();
            listener.Stop();

            var handOver        = lentServer.AcceptUpgradedConnectionAsync(
                                      NetworkStream:  accepted.GetStream(),
                                      LocalSocket:    IPSocket.FromIPEndPoint((IPEndPoint) accepted.Client.LocalEndPoint!),
                                      RemoteSocket:   IPSocket.FromIPEndPoint((IPEndPoint) accepted.Client.RemoteEndPoint!),
                                      RequestBytes:   UpgradeRequest("/")
                                  );

            var answer          = await ReadHeader(client.GetStream());

            if (HandlersAreWaiting)
            {

                await handOver.WaitAsync(TimeSpan.FromSeconds(5));

                var reason = await refusal.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(answer,  Does.StartWith("HTTP/1.1 403"),                        answer);
                    Assert.That(asked,   Is.Zero,                                               "A handler was asked about a connection there was nothing to show of.");
                    Assert.That(reason,  Does.Contain(nameof(AWebSocketServer.OnValidateTCPConnection)));
                });

            }

            else
            {

                Assert.Multiple(() => {
                    Assert.That(answer,                  Does.StartWith("HTTP/1.1 101"),  answer);
                    Assert.That(refusal.Task.IsCompleted, Is.False,                       "The connection was refused.");
                });

                // Hanging up ends the WebSocket loop, and with it the hand-over.
                client.Close();

                await handOver.WaitAsync(TimeSpan.FromSeconds(5));

            }

        }

        #endregion


        #region (private) ConnectToTheLentPath()

        /// <summary>
        /// Connect to the HTTP server and ask for an upgrade at /lent.
        /// </summary>
        /// <remarks>
        /// By hand rather than with a WebSocket client, because what is under
        /// test is exactly what arrives on the wire - and a client that meets
        /// a closed connection instead of an answer makes an answer up.
        /// </remarks>
        private async Task<TcpClient> ConnectToTheLentPath()
        {

            var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort.ToInt32());
            await client.GetStream().WriteAsync(UpgradeRequest("/lent"));

            return client;

        }

        #endregion

        #region (private static) UpgradeRequest(Path)

        /// <summary>
        /// The bytes of a request for a WebSocket upgrade at the given path.
        /// </summary>
        /// <param name="Path">The path to ask at.</param>
        private static Byte[] UpgradeRequest(String Path)

            => Encoding.ASCII.GetBytes(
                   $"GET {Path} HTTP/1.1\r\n"                          +
                    "Host: 127.0.0.1\r\n"                             +
                    "Upgrade: websocket\r\n"                          +
                    "Connection: Upgrade\r\n"                         +
                    "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                    "Sec-WebSocket-Version: 13\r\n"                   +
                    "\r\n"
               );

        #endregion

        #region (private static) ReadHeader(Stream)

        /// <summary>
        /// Everything the server sent, up to the end of the header of its answer.
        /// </summary>
        /// <param name="Stream">The stream to read from.</param>
        private static Task<String> ReadHeader(Stream Stream)

            => Read(Stream, UntilClosed: false);

        #endregion

        #region (private static) ReadUntilClosed(Stream)

        /// <summary>
        /// Everything the server sent, until it closed the connection.
        /// </summary>
        /// <param name="Stream">The stream to read from.</param>
        private static Task<String> ReadUntilClosed(Stream Stream)

            => Read(Stream, UntilClosed: true);

        #endregion

        #region (private static) Read(Stream, UntilClosed)

        /// <summary>
        /// Everything the server sent, until it closed the connection - or, where
        /// that is not asked for, until the end of the header of its answer.
        /// </summary>
        /// <param name="Stream">The stream to read from.</param>
        /// <param name="UntilClosed">Whether to read on until the connection is closed.</param>
        private static async Task<String> Read(Stream   Stream,
                                               Boolean  UntilClosed)
        {

            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var answer         = new MemoryStream();
            var buffer         = new Byte[4096];

            while (true)
            {

                var read = await Stream.ReadAsync(buffer, timeout.Token);

                if (read == 0)
                    break;

                answer.Write(buffer, 0, read);

                if (!UntilClosed &&
                    Encoding.ASCII.GetString(answer.ToArray()).Contains("\r\n\r\n"))
                {
                    break;
                }

            }

            return Encoding.UTF8.GetString(answer.ToArray());

        }

        #endregion

    }

}
