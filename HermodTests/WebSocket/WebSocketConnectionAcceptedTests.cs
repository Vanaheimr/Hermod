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
using System.Net.Sockets;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// OnWebSocketConnectionAccepted is raised before the 101 is sent, and nothing
    /// sent on the connection goes out ahead of the 101.
    /// </summary>
    /// <remarks>
    /// A server that learns of a connection only after answering the upgrade
    /// cannot be asked for it the moment the peer has its answer: the peer is
    /// connected, and the server does not know it yet. An application server
    /// built on this one - an OCPP central system sending requests to its
    /// charging stations - answered "unknown client" to exactly that. So this
    /// event comes first, and a connection can be known before it is open.
    ///
    /// Which leaves one thing to hold: a frame sent in that moment must not go
    /// out ahead of the 101, or the peer reads it as part of the HTTP response.
    ///
    /// Both are asked of the socket itself, through a plain TCP client, rather
    /// than of a WebSocket client that would hide the order of the bytes.
    /// </remarks>
    [TestFixture]
    public class WebSocketConnectionAcceptedTests
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


        #region (private) SendUpgrade  (Client, Port)

        private static Task SendUpgrade(TcpClient  Client,
                                        IPPort     Port)

            => Client.GetStream().WriteAsync(
                   Encoding.ASCII.GetBytes(
                       "GET / HTTP/1.1\r\n" +
                      $"Host: 127.0.0.1:{Port}\r\n" +
                       "Upgrade: websocket\r\n" +
                       "Connection: Upgrade\r\n" +
                       "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                       "Sec-WebSocket-Version: 13\r\n" +
                       "\r\n"
                   )
               ).AsTask();

        #endregion

        #region (private) ReadUntil    (Client, Enough)

        /// <summary>
        /// Read until the bytes received so far are enough, or five seconds
        /// have passed, or the server has closed the connection.
        /// </summary>
        private static async Task<Byte[]> ReadUntil(TcpClient             Client,
                                                    Func<Byte[], Boolean>  Enough)
        {

            var received  = new List<Byte>();
            var buffer    = new Byte[4096];

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (!Enough([.. received]))
                {

                    var read = await Client.GetStream().ReadAsync(buffer, timeout.Token);

                    if (read == 0)
                        break;

                    received.AddRange(buffer.Take(read));

                }
            }
            catch (OperationCanceledException)
            { }

            return [.. received];

        }

        #endregion

        #region (private) HeaderEnd    (Bytes)

        /// <summary>
        /// Where the HTTP response ends and whatever follows it begins, or -1.
        /// </summary>
        private static Int32 HeaderEnd(Byte[] Bytes)
        {

            var at = Encoding.ASCII.GetString(Bytes).IndexOf("\r\n\r\n", StringComparison.Ordinal);

            return at < 0 ? -1 : at + 4;

        }

        #endregion


        #region TheEventComesBeforeThePeerHasItsAnswer()

        /// <summary>
        /// While the connection is being accepted, the peer has not received a
        /// single byte - and OnNewWebSocketConnection comes after, as it did.
        /// </summary>
        [Test]
        public async Task TheEventComesBeforeThePeerHasItsAnswer()
        {

            server            = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            var port          = server.IPPort;

            using var client  = new TcpClient();
            var order         = new ConcurrentQueue<String>();
            var receivedThen  = -1;
            var looked        = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnWebSocketConnectionAccepted += async (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {
                order.Enqueue("accepted");
                // Long enough for an answer that had already been sent to arrive.
                await Task.Delay(300, ct);
                receivedThen = client.Available;
                looked.TrySetResult();
            };

            server.OnNewWebSocketConnection      += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {
                order.Enqueue("new");
                return Task.CompletedTask;
            };

            await client.ConnectAsync("127.0.0.1", port.ToInt32());
            await SendUpgrade(client, port);

            // Nothing is read before the event has looked: bytes this test had
            // already taken off the socket would not be Available any more.
            await looked.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var response = Encoding.ASCII.GetString(await ReadUntil(client, bytes => HeaderEnd(bytes) >= 0));

            // OnNewWebSocketConnection is raised once the 101 is out, which the
            // peer may notice first.
            SpinWait.SpinUntil(() => order.Count >= 2, TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response,      Does.StartWith("HTTP/1.1 101"));
                Assert.That(receivedThen,  Is.Zero,  "The peer had its answer while the connection was still being accepted.");
                Assert.That(order,         Is.EqualTo(new[] { "accepted", "new" }));
            });

        }

        #endregion

        #region AFrameSentBeforeTheAnswerFollowsIt()

        /// <summary>
        /// A text message sent while the connection is being accepted, and not
        /// waited for: it arrives, and it arrives after the 101.
        /// </summary>
        [Test]
        public async Task AFrameSentBeforeTheAnswerFollowsIt()
        {

            server            = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            var port          = server.IPPort;

            Task<SentStatus>? early = null;

            server.OnWebSocketConnectionAccepted += async (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {
                // Sent and not waited for: the connection is known now, and not open yet.
                early = webSocketServer.SendTextMessage(connection, "early");
                // Long enough for a send that did not wait to reach the socket.
                await Task.Delay(300, ct);
            };

            using var client  = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port.ToInt32());
            await SendUpgrade(client, port);

            // The response, and the seven bytes of an unmasked text frame "early".
            var received      = await ReadUntil(client, bytes => HeaderEnd(bytes) >= 0 && bytes.Length >= HeaderEnd(bytes) + 7);
            var headerEnd     = HeaderEnd(received);

            Assert.That(Encoding.ASCII.GetString(received),  Does.StartWith("HTTP/1.1 101"),
                        "Something went out ahead of the 101.");

            Assert.That(received.Skip(headerEnd).Take(7).ToArray(),
                        Is.EqualTo(new Byte[] { 0x81, 0x05, (Byte) 'e', (Byte) 'a', (Byte) 'r', (Byte) 'l', (Byte) 'y' }));

            Assert.That(early,                               Is.Not.Null);
            Assert.That(await early!,                        Is.EqualTo(SentStatus.Success));

        }

        #endregion

        #region TheClientHearsOfItsConnectionBeforeItsFirstFrame()

        /// <summary>
        /// The client's side of the same moment. A server may send the instant
        /// its 101 is out, so whatever a client needs to handle what it receives
        /// - who the server is, the way back to it - has to be in place before
        /// it reads its first frame, not once Connect has returned.
        /// </summary>
        [Test]
        public async Task TheClientHearsOfItsConnectionBeforeItsFirstFrame()
        {

            server            = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            var port          = server.IPPort;

            // A server that speaks first, as soon as it is allowed to.
            server.OnWebSocketConnectionAccepted += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, ct) => {
                _ = webSocketServer.SendTextMessage(connection, "first");
                return Task.CompletedTask;
            };

            var order         = new ConcurrentQueue<String>();
            var client        = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"));

            client.OnWebSocketConnectionAccepted += async (timestamp, webSocketClient, connection, httpResponse, ct) => {
                // Long enough for a frame that was read regardless to be handled.
                await Task.Delay(300, ct);
                order.Enqueue("accepted");
            };

            client.OnTextMessageReceived          += (timestamp, webSocketClient, connection, frame, eventTrackingId, textMessage, ct) => {
                order.Enqueue(textMessage);
                return Task.CompletedTask;
            };

            var (_, httpResponse) = await client.Connect();

            SpinWait.SpinUntil(() => order.Count >= 2, TimeSpan.FromSeconds(5));

            await client.Close();

            Assert.Multiple(() => {
                Assert.That(httpResponse.HTTPStatusCode.Code,  Is.EqualTo(101), httpResponse.EntirePDU);
                Assert.That(order,                             Is.EqualTo(new[] { "accepted", "first" }));
            });

        }

        #endregion

    }

}
