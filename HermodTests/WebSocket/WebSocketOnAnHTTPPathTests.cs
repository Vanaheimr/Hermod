/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket and ordinary HTTP on one port.
    /// </summary>
    /// <remarks>
    /// <b>The arrangement this was written for is XMPP's.</b> A client speaks
    /// RFC 7395 over a WebSocket and fetches files over HTTPS (XEP-0363), and
    /// there is no reason those should be two ports with two copies of the same
    /// certificate. Until the upgrade worker existed there was no choice: a
    /// WebSocket needed a TCP server of its own.
    ///
    /// The rounds below are the questions that decide whether the seam holds -
    /// and the last two are the ones a test that only checked the happy path
    /// would miss: whether the *other* paths still work once a WebSocket is
    /// living on one of them, and whether a plain GET at the WebSocket's own
    /// path is answered rather than hung up on.
    /// </remarks>
    [TestFixture]
    public class WebSocketOnAnHTTPPathTests
    {

        #region Data

        private HTTPServer?            httpServer;
        private WebSocketMirrorServer?  webSocketServer;
        private DismissingServer?       dismissingServer;

        #endregion

        #region (class) DismissingServer

        /// <summary>
        /// A server that answers once and then throws the caller off.
        /// </summary>
        /// <remarks>
        /// <b>The shape of what a real protocol does, and not the shape a test
        /// reaches for on its own.</b> XMPP ends a stream by writing the error,
        /// writing the stream close and then dropping the connection - all three
        /// from inside the handler for the message that provoked it. Closing an
        /// idle connection from the outside, which is what the round above does,
        /// goes through a different path and passes even when this one does not:
        /// measured, with the socket close mutated away.
        /// </remarks>
        private sealed class DismissingServer : AWebSocketServer
        {

            public DismissingServer()
                : base(TCPPort: IPPort.Parse(0), AutoStart: false)
            { }

            public override async Task ProcessTextMessage(DateTimeOffset             Timestamp,
                                                          AWebSocketServer           Server,
                                                          WebSocketServerConnection  Connection,
                                                          EventTracking_Id           EventTrackingId,
                                                          WebSocketFrame             TextFrame,
                                                          String                     TextMessage,
                                                          CancellationToken          CancellationToken)
            {

                await SendTextMessage(Connection, "go away then");

                // Blocking, and not awaited, because that is what the caller
                // this was written for does: XMPP's Kill() is a synchronous
                // method on a session and ends with
                // Close().GetAwaiter().GetResult(). Awaiting it here instead
                // takes a different path through the close and the round goes
                // green on a server that never closes the socket - measured.
                Connection.Close().GetAwaiter().GetResult();

            }

        }

        #endregion

        #region Setting up / tearing down

        [SetUp]
        public async Task Init()
        {

            httpServer       = await HTTPServer.StartNew();

            // Not started, and that is the whole idea: it never accepts
            // anything. What it is here for is its protocol, which the HTTP
            // server borrows for one path.
            webSocketServer  = new WebSocketMirrorServer(
                                   AutoStart: false
                               );

            var api = httpServer.AddHTTPAPI();

            api.AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/xmpp"),
                HTTPDelegate: WebSocketUpgrade.For(webSocketServer)
            );

            dismissingServer = new DismissingServer();

            api.AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/dismiss"),
                HTTPDelegate: WebSocketUpgrade.For(dismissingServer)
            );

            api.AddHandler(
                HTTPMethod.PUT,
                HTTPPath.Parse("/upload"),
                HTTPDelegate: async request => new HTTPResponse.Builder(request) {
                                                   HTTPStatusCode  = HTTPStatusCode.Created,
                                                   ContentType     = HTTPContentType.Text.PLAIN,
                                                   Content         = $"{request.HTTPBody?.Length ?? 0} bytes".ToUTF8Bytes(),
                                                   Connection      = ConnectionType.KeepAlive
                                               }.AsImmutable
            );

        }

        [TearDown]
        public async Task Shutdown()
        {

            if (httpServer is not null)
                await httpServer.Stop();

            httpServer       = null;
            webSocketServer  = null;

        }

        #endregion

        #region Helper functions

        /// <summary>
        /// Sends text through the mounted WebSocket and gives back the answer.
        /// </summary>
        /// <remarks>
        /// WebSocketMirrorServer sends the text back <b>reversed</b>, which is
        /// better here than an echo would be: an echo cannot tell a frame that
        /// travelled both ways from one that was never sent and somehow read
        /// back out of the client's own buffer.
        /// </remarks>
        private async Task<String> MirrorAsync(String Text)
        {

            var client = new ClientWebSocket();

            try
            {

                await client.ConnectAsync(new Uri($"ws://127.0.0.1:{httpServer!.TCPPort}/xmpp"),
                                          CancellationToken.None);

                await client.SendAsync(Encoding.UTF8.GetBytes(Text),
                                       WebSocketMessageType.Text,
                                       true,
                                       CancellationToken.None);

                var buffer  = new Byte[1024];
                var result  = await client.ReceiveAsync(buffer, CancellationToken.None);

                return Encoding.UTF8.GetString(buffer, 0, result.Count);

            }
            finally
            {
                try { client.Dispose(); } catch { }
            }

        }

        #endregion


        #region 1. A WebSocket answers on its path

        /// <summary>
        /// The upgrade goes through and frames travel.
        /// </summary>
        /// <remarks>
        /// A .NET <c>ClientWebSocket</c> on purpose, rather than Hermod's own:
        /// both halves being the same code is the finding of D62 to D65, and a
        /// handshake is exactly the place where two halves written together
        /// agree on something neither of them should.
        /// </remarks>
        [Test]
        public async Task AWebSocketAnswersOnItsPath()
        {

            Assert.That(await MirrorAsync("Guten Morgen"), Is.EqualTo("negroM netuG"));

        }

        #endregion

        #region 2. And ordinary HTTP still answers beside it

        /// <summary>
        /// The other paths are untouched.
        /// </summary>
        /// <remarks>
        /// <b>This is the round the whole change exists for.</b> One listener,
        /// one certificate, one port: <c>/xmpp</c> speaks frames and
        /// <c>/upload</c> takes a PUT, and a client is told one number rather
        /// than two.
        /// </remarks>
        [Test]
        public async Task OrdinaryHttpStillAnswersBesideIt()
        {

            Assert.That(await MirrorAsync("first"), Is.EqualTo("tsrif"));

            var (client, _) = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer!.TCPPort);

            var request  = client!.CreateRequest(HTTPMethod.PUT, HTTPPath.Parse("/upload"));
            request.Content = "0123456789".ToUTF8Bytes();

            var response = await client.SendRequest(request);

            Assert.Multiple(() =>
            {
                Assert.That(response.HTTPStatusCode,          Is.EqualTo(HTTPStatusCode.Created));
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo("10 bytes"));
            });

        }

        #endregion

        #region 3. A WebSocket path does not swallow the connection it refuses

        /// <summary>
        /// A plain GET at the WebSocket's path.
        /// </summary>
        /// <remarks>
        /// RFC 9110, section 15.5.23. Somebody opening the address in a browser
        /// has to be told what it is, not left holding a connection that never
        /// answers - and the handler must not hand a stream to the WebSocket
        /// server for a request that was never an upgrade, because nothing would
        /// then ever close it.
        /// </remarks>
        [Test]
        public async Task APlainRequestAtTheWebSocketPathIsAnswered()
        {

            var (client, _) = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer!.TCPPort);

            var response = await client!.SendRequest(
                                     client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/xmpp"))
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.UpgradeRequired));

        }

        #endregion

        #region 4. Two WebSockets on the same port at the same time

        /// <summary>
        /// The upgrade worker owns one connection and not the server.
        /// </summary>
        /// <remarks>
        /// <b>Written because the worker runs inside the HTTP server's own
        /// connection loop.</b> If it were awaited anywhere that serialised
        /// connections, one WebSocket would hold the whole server for as long as
        /// it stayed open - and the symptom would be a second client that simply
        /// hangs, which reads like a network problem and is not one.
        /// </remarks>
        [Test]
        public async Task TwoWebSocketsAtOnce()
        {

            var first   = MirrorAsync("one");
            var second  = MirrorAsync("two");

            await Task.WhenAll(first, second);

            Assert.Multiple(() =>
            {
                Assert.That(first.Result,  Is.EqualTo("eno"));
                Assert.That(second.Result, Is.EqualTo("owt"));
            });

        }

        #endregion

        #region 5. A connection closed from this end really closes

        /// <summary>
        /// The server drops the connection and the client finds out.
        /// </summary>
        /// <remarks>
        /// <b>The half of a hand-over that is easy to lose.</b> A connection
        /// that was handed a stream does not own the socket, so closing it means
        /// closing the stream - and if that did not reach the socket, everything
        /// would still look right: frames would travel, the tests above would
        /// pass, and only a server trying to throw somebody off would find that
        /// it could not. Which is exactly the shape of the two XMPP tests that
        /// went red when this was first wired up.
        /// </remarks>
        [Test]
        public async Task AConnectionClosedByTheServerReallyCloses()
        {

            var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{httpServer!.TCPPort}/xmpp"),
                                      CancellationToken.None);

            // One round trip, so there is certainly a connection on the server's
            // side to go and close.
            await client.SendAsync(Encoding.UTF8.GetBytes("ping"),
                                   WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new Byte[1024];
            await client.ReceiveAsync(buffer, CancellationToken.None);

            var connection = webSocketServer!.WebSocketConnections.FirstOrDefault();

            Assert.That(connection, Is.Not.Null,
                        "The WebSocket server does not know about the connection it is speaking on.");

            await connection!.Close();

            // The client notices by its next read failing or reporting a close -
            // either is the connection ending, and which of the two it is
            // depends on how far the close got before the socket went.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            try
            {
                var result = await client.ReceiveAsync(buffer, deadline.Token);
                Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("The connection was closed on the server and the client noticed nothing.");
            }
            catch (WebSocketException)
            {
                // The socket went without a close frame, which is what Close()
                // without a status code is for.
            }

            client.Dispose();

        }

        #endregion

        #region 6. A server that throws somebody off really does

        /// <summary>
        /// Written, sent and dropped - all from inside the handler.
        /// </summary>
        /// <remarks>
        /// <b>This is the round that caught it.</b> Handing a connection over
        /// means giving away a stream, and a stream is not a socket:
        /// <c>TcpClient.GetStream()</c> hands out a <c>NetworkStream</c> that
        /// does not own the socket, so closing it leaves the connection
        /// standing. Everything still looked right - frames travelled, an idle
        /// connection closed on request - and only a server trying to end a
        /// session found that the far side noticed nothing. Thirteen seconds,
        /// measured, in a client that had already been told why it was being
        /// thrown off and simply never saw the line go.
        ///
        /// A second is generous. The whole exchange is on the loopback, and the
        /// close needs no round trip: what is being waited for is a socket that
        /// should already be gone.
        /// </remarks>
        [Test]
        public async Task AServerThatThrowsSomebodyOffReallyDoes()
        {

            var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{httpServer!.TCPPort}/dismiss"),
                                      CancellationToken.None);

            await client.SendAsync(Encoding.UTF8.GetBytes("are you there"),
                                   WebSocketMessageType.Text, true, CancellationToken.None);

            var buffer = new Byte[1024];

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            try
            {

                // The answer, which the server writes immediately before
                // dropping the connection - and which must not be lost by the
                // dropping.
                var answer = await client.ReceiveAsync(buffer, deadline.Token);

                Assert.That(Encoding.UTF8.GetString(buffer, 0, answer.Count),
                            Is.EqualTo("go away then"),
                            "What the server said on its way out did not arrive.");

                // And then the connection has to be gone.
                var next = await client.ReceiveAsync(buffer, deadline.Token);

                Assert.That(next.MessageType, Is.EqualTo(WebSocketMessageType.Close));

            }
            catch (OperationCanceledException)
            {
                Assert.Fail("The server dropped the connection and the client was left holding it open.");
            }
            catch (WebSocketException)
            {
                // The socket went without a close frame, which is what a drop is.
            }

            client.Dispose();

        }

        #endregion

    }

}
