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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Whatever a handler throws during the WebSocket handshake refuses the
    /// upgrade and leaves nothing of the connection behind - and what a
    /// validator throws is answered, with a 500.
    /// </summary>
    /// <remarks>
    /// The OnValidateWebSocketConnection handlers are awaited rather than fired
    /// and forgotten, so that one that fails refuses the upgrade instead of
    /// admitting it. What it threw then went on up, past the rest of the
    /// handshake and past the code that takes a connection off the server's
    /// books again. The client was hung up on without an answer, and the server
    /// kept the connection: listed among its WebSocketConnections, and counted
    /// against its address by MaxConnectionsPerIP for as long as the server ran.
    /// </remarks>
    [TestFixture]
    public class WebSocketHandshakeExceptionTests
    {

        #region Data

        private WebSocketMirrorServer?  server;
        private WebSocketClient?        client;
        private WebSocketClient?        secondClient;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.Close();

            if (secondClient is not null)
                await secondClient.Close();

            if (server is not null)
                await server.Shutdown(Wait: true);

            client        = null;
            secondClient  = null;
            server        = null;

        }

        #endregion


        #region AValidatorThatThrowsIsAnsweredWith500AndLeavesNoConnectionBehind()

        /// <summary>
        /// A validator that throws right where it is called.
        /// </summary>
        [Test]
        public Task AValidatorThatThrowsIsAnsweredWith500AndLeavesNoConnectionBehind()

            => RefusedByABrokenValidator(() => throw new InvalidOperationException("This validator is broken."));

        #endregion

        #region AValidatorWhoseTaskFailsIsAnsweredWith500AndLeavesNoConnectionBehind()

        /// <summary>
        /// A validator that fails later, in the task it has returned.
        /// </summary>
        [Test]
        public Task AValidatorWhoseTaskFailsIsAnsweredWith500AndLeavesNoConnectionBehind()

            => RefusedByABrokenValidator(async () => {
                   await Task.Yield();
                   throw new InvalidOperationException("This validator is broken.");
               });

        #endregion

        #region AValidatorThatThrowsCostsTheAddressNoPlace()

        /// <summary>
        /// Where one connection per address is allowed, the next client from an
        /// address whose last attempt a validator refused by throwing is let in.
        /// </summary>
        /// <remarks>
        /// The limit counts what the server has on its books, not what
        /// WebSocketConnections lists - that leaves out a connection that is
        /// closed or that the garbage collector has taken. So this is where a
        /// connection left on the books shows even when nothing holds on to it:
        /// its address has one place fewer, for as long as the server runs.
        /// </remarks>
        [Test]
        public async Task AValidatorThatThrowsCostsTheAddressNoPlace()
        {

            server = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            server.MaxConnectionsPerIP = 1;

            var asked   = 0;
            var closed  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // The first attempt is refused by throwing, the second one let in.
            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken)

                => Interlocked.Increment(ref asked) == 1
                       ? throw new InvalidOperationException("This validator is broken.")
                       : Task.FromResult<HTTPResponse?>(null);

            server.OnTCPConnectionClosed += (timestamp, webSocketServer, connection, eventTrackingId, reason, cancellationToken) => {
                closed.TrySetResult();
                return Task.CompletedTask;
            };

            client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            await client.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            // A connection that is taken off the books is taken off them before
            // this event. One that is not does not get the event either, and
            // then it is the second client below that shows what that costs.
            await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            secondClient = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            var (_, httpResponse) = await secondClient.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {

                Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(101),
                            "The second client was not let in: the first one, refused by a validator that threw, still counted against MaxConnectionsPerIP." +
                            $"{Environment.NewLine}{httpResponse.EntirePDU}");

                Assert.That(asked, Is.EqualTo(2),
                            "The validator was not asked about the second client.");

            });

        }

        #endregion

        #region TheFirstRefusalIsStillTheAnswer()

        /// <summary>
        /// Of two validators, one refusing with 403 and one throwing, the one
        /// registered first gives the answer - whichever of the two it is.
        /// </summary>
        /// <remarks>
        /// What a validator throws is its refusal, in its place among the
        /// others. That is why each one is asked through AskValidator rather
        /// than all of them inside one try: an exception out of Task.WhenAll
        /// would be answered 500 however the others had answered, and a client
        /// that one of them had turned away for good would be told to try
        /// again.
        /// </remarks>
        [Test]
        public async Task TheFirstRefusalIsStillTheAnswer()
        {

            var refusedFirst  = await AnswerToTwoValidators(Refuses, Throws);
            var thrownFirst   = await AnswerToTwoValidators(Throws,  Refuses);

            Assert.Multiple(() => {

                Assert.That(refusedFirst,  Is.EqualTo(403),
                            "A validator that refused came before one that threw, and was not the answer.");

                Assert.That(thrownFirst,   Is.EqualTo(500),
                            "A validator that threw came before one that refused, and was not the answer.");

            });

        }

        #endregion

        #region ASubprotocolSelectorThatThrowsLeavesNoConnectionBehind()

        /// <summary>
        /// Whatever else gets out of the handshake ends the connection, and takes
        /// it off the books: the next client from its address is let in.
        /// </summary>
        /// <remarks>
        /// The subprotocol selector stands in for anything that throws past the
        /// handshake. It is somebody else's code as well, and unlike a validator
        /// its exception is not answered - nothing catches it before the
        /// connection loop itself does. What is asked here is only that the
        /// client is not left waiting, and that the server keeps nothing: the
        /// connection is closed, however the server says so, and gone from its
        /// books.
        /// </remarks>
        [Test]
        public async Task ASubprotocolSelectorThatThrowsLeavesNoConnectionBehind()
        {

            server = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            server.MaxConnectionsPerIP = 1;

            var selected = 0;

            // Held on to, for the same reason as in RefusedByABrokenValidator.
            WebSocketServerConnection? first = null;

            // The first attempt fails in the selector, the second one is let in.
            server.SubprotocolSelector = (webSocketServer, connection, subprotocols) => {

                if (Interlocked.Increment(ref selected) == 1)
                {
                    first = connection;
                    throw new InvalidOperationException("This subprotocol selector is broken.");
                }

                return null;

            };

            var (received, closed) = await AskForAnUpgrade(server.IPPort);

            var noneLeft = await NoneLeft(TimeSpan.FromSeconds(5));

            secondClient = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            var (_, httpResponse) = await secondClient.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {

                Assert.That(first, Is.Not.Null,
                            "The subprotocol selector was never asked, so this test tested nothing.");

                Assert.That(closed, Is.True,
                            "The server neither answered nor closed the connection whose subprotocol selector threw.");

                Assert.That(received, Does.Not.StartWith("HTTP/1.1 101"),
                            "The connection whose subprotocol selector threw was upgraded.");

                Assert.That(noneLeft, Is.True,
                            "The server still lists the connection whose subprotocol selector threw.");

                Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(101),
                            "The second client was not let in: the first one, whose subprotocol selector threw, still counted against MaxConnectionsPerIP." +
                            $"{Environment.NewLine}{httpResponse.EntirePDU}");

            });

        }

        #endregion


        #region (private) RefusedByABrokenValidator(Validator)

        /// <summary>
        /// A client asks a server whose one validator fails as the given one
        /// does. It is answered 500, and not left without an answer, and the
        /// server does not list its connection afterwards.
        /// </summary>
        private async Task RefusedByABrokenValidator(Func<Task<HTTPResponse?>> Validator)
        {

            server = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            // Held on to here, and not only for the assertion below: the server
            // keeps its connections by weak reference, and a connection left
            // behind that the garbage collector has taken in the meantime could
            // not be seen to have been left behind.
            WebSocketServerConnection? validated = null;

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                validated = connection;
                return Validator();
            };

            client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            // Bounded, so that a client left waiting fails this test rather than
            // holding it for as long as the client itself is willing to wait.
            var (_, httpResponse) = await client.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            var noneLeft = await NoneLeft(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {

                Assert.That(validated, Is.Not.Null,
                            "The validator was never asked, so this test tested nothing.");

                Assert.That(httpResponse.HTTPStatusCode.Code, Is.EqualTo(500),
                            $"A validator that threw was answered with:{Environment.NewLine}{httpResponse.EntirePDU}");

                Assert.That(noneLeft, Is.True,
                            "The server still lists the connection whose validator threw.");

            });

        }

        #endregion

        #region (private) NoneLeft(Within)

        /// <summary>
        /// Whether the server lists no connection any more, within the given time.
        /// </summary>
        private async Task<Boolean> NoneLeft(TimeSpan Within)
        {

            var giveUp = DateTimeOffset.UtcNow + Within;

            while (server!.WebSocketConnections.Any() &&
                   DateTimeOffset.UtcNow < giveUp)
            {
                await Task.Delay(50);
            }

            return !server.WebSocketConnections.Any();

        }

        #endregion

        #region (private static) AnswerToTwoValidators(First, Second)

        /// <summary>
        /// The status a client is answered with by a server that has these two
        /// validators, registered in this order.
        /// </summary>
        private static async Task<UInt32> AnswerToTwoValidators(OnValidateWebSocketConnectionDelegate  First,
                                                                OnValidateWebSocketConnectionDelegate  Second)
        {

            var twoValidators  = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
            var asking         = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{twoValidators.IPPort}"));

            try
            {

                twoValidators.OnValidateWebSocketConnection += First;
                twoValidators.OnValidateWebSocketConnection += Second;

                var (_, httpResponse) = await asking.Connect().WaitAsync(TimeSpan.FromSeconds(10));

                return httpResponse.HTTPStatusCode.Code;

            }
            finally
            {
                await asking.Close();
                await twoValidators.Shutdown(Wait: true);
            }

        }

        #endregion

        #region (private static) Refuses(...) / Throws(...)

        /// <summary>
        /// A validator that refuses the upgrade with 403.
        /// </summary>
        private static Task<HTTPResponse?> Refuses(DateTimeOffset             Timestamp,
                                                   AWebSocketServer           Server,
                                                   WebSocketServerConnection  Connection,
                                                   EventTracking_Id           EventTrackingId,
                                                   CancellationToken          CancellationToken)

            => Task.FromResult<HTTPResponse?>(
                   new HTTPResponse.Builder(Connection.HTTPRequest!) {
                       HTTPStatusCode  = HTTPStatusCode.Forbidden,
                       Connection      = ConnectionType.Close
                   }.AsImmutable
               );

        /// <summary>
        /// A validator that throws right where it is called.
        /// </summary>
        private static Task<HTTPResponse?> Throws(DateTimeOffset             Timestamp,
                                                  AWebSocketServer           Server,
                                                  WebSocketServerConnection  Connection,
                                                  EventTracking_Id           EventTrackingId,
                                                  CancellationToken          CancellationToken)

            => throw new InvalidOperationException("This validator is broken.");

        #endregion

        #region (private static) AskForAnUpgrade(Port)

        /// <summary>
        /// Ask for an upgrade over a plain TCP connection, and read whatever
        /// comes back until the server closes the connection, or for five
        /// seconds.
        /// </summary>
        /// <remarks>
        /// Over a plain TCP client rather than a WebSocket client, because a
        /// server that closes without an answer is exactly what is looked for
        /// here, and the WebSocket client reads a closed connection as silence
        /// and waits out its timeout before it says so.
        /// </remarks>
        /// <param name="Port">The port the server listens on.</param>
        /// <returns>What came back, and whether the server closed the connection.</returns>
        private static async Task<(String Received, Boolean Closed)> AskForAnUpgrade(IPPort Port)
        {

            using var tcpClient  = new TcpClient();

            await tcpClient.ConnectAsync("127.0.0.1", Port.ToInt32());

            var stream           = tcpClient.GetStream();

            await stream.WriteAsync(
                      Encoding.ASCII.GetBytes(
                          "GET / HTTP/1.1\r\n" +
                         $"Host: 127.0.0.1:{Port}\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                          "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                          "Sec-WebSocket-Version: 13\r\n" +
                          "\r\n"
                      )
                  );

            var received         = new List<Byte>();
            var buffer           = new Byte[4096];
            var closed           = false;

            using var timeout    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (!closed)
                {

                    var read = await stream.ReadAsync(buffer, timeout.Token);

                    if (read == 0)
                        closed = true;

                    received.AddRange(buffer.Take(read));

                }
            }
            catch (OperationCanceledException)
            { }

            // A reset is a close as well.
            catch (IOException)
            {
                closed = true;
            }

            return (Encoding.ASCII.GetString([.. received]), closed);

        }

        #endregion

    }

}
