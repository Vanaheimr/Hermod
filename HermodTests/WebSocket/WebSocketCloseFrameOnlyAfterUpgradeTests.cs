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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket close frame goes out only on a connection whose 101
    /// Switching Protocols went out before it.
    /// </summary>
    /// <remarks>
    /// Until its 101 a client reads HTTP, and a close frame is four octets of
    /// garbage in it: 88 02 03 EA, a close with 1002, protocol error. The
    /// server sent one on nearly every way a connection could end before it
    /// was upgraded - after a first request that was not a GET, after its loop
    /// however that had ended, to every connection still waiting for its
    /// answer when the server was shut down. And a refusal that did not say
    /// Connection: close was taken for an upgrade outright: the connection
    /// was announced as a new WebSocket connection, and the client's next
    /// request, read as a frame, drew a close frame of its own.
    ///
    /// Every test here reads what the server sends over a plain TCP
    /// connection, octet for octet, until the server closes it, and asks that
    /// nothing arrives but the HTTP message it expects - or nothing at all. A
    /// WebSocket client would not do: it reads a close frame after a refusal
    /// as noise, or not at all, and an HTTP client takes it for the next
    /// response or throws it away.
    ///
    /// The servers listen on 127.0.0.1 alone. The default, the dual-stack
    /// localhost, cannot be bound in a container without IPv6.
    /// </remarks>
    [TestFixture]
    public class WebSocketCloseFrameOnlyAfterUpgradeTests
    {

        #region Data

        /// <summary>
        /// How long a test waits for the server to answer, or to close.
        /// </summary>
        private static readonly TimeSpan  WaitAtMost               = TimeSpan.FromSeconds(5);

        /// <summary>
        /// What a close frame with 1002, protocol error, and no reason is on
        /// the wire - the frame these tests are about.
        /// </summary>
        private static readonly Byte[]    ProtocolErrorCloseFrame  = [ 0x88, 0x02, 0x03, 0xEA ];

        private WebSocketMirrorServer?    server;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

            server = null;

        }

        #endregion


        #region AFirstRequestThatIsNotAGETIsClosedWithoutACloseFrame()

        /// <summary>
        /// A first request that does not begin with "GET " is no WebSocket
        /// handshake, and the connection it came on is closed without a word
        /// in WebSocket.
        /// </summary>
        /// <remarks>
        /// A web crawler, a health check, a TLS handshake on the plain port:
        /// whatever the client speaks, it is not WebSocket, and a close frame is
        /// the one thing it certainly cannot read.
        /// </remarks>
        [Test]
        public async Task AFirstRequestThatIsNotAGETIsClosedWithoutACloseFrame()
        {

            server = StartServer();

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            await Send(stream,
                       "HEAD / HTTP/1.1\r\n" +
                       "Host: 127.0.0.1\r\n" +
                       "\r\n");

            var received  = new List<Byte>();
            var closed    = await ReadUntilClosed(stream, received);

            Assert.Multiple(() => {

                Assert.That(closed,    Is.True,
                            "The server neither answered nor closed the connection a HEAD request came on.");

                Assert.That(received,  Is.Empty,
                            $"A HEAD request was answered with {Octets(received)}");

            });

        }

        #endregion

        #region AClientThatHangsUpBeforeAskingIsNotSentACloseFrame()

        /// <summary>
        /// A client that connects, says nothing and shuts its side of the
        /// connection down is not sent a close frame on the way out.
        /// </summary>
        /// <remarks>
        /// The loop ends at the read of zero octets, as it ends at a read that
        /// fails, and closes the connection after itself. That close sent 1002
        /// however the loop had ended - and a client that has only shut down
        /// its sending side still reads.
        /// </remarks>
        [Test]
        public async Task AClientThatHangsUpBeforeAskingIsNotSentACloseFrame()
        {

            server = StartServer();

            var accepted = Accepted(server);

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            // Not before the server's loop has the connection: otherwise this
            // could be over before there is a loop to end.
            await accepted.WaitAsync(WaitAtMost);

            // Only the sending side: the stream taken above still reads, where
            // TcpClient.GetStream() would refuse from now on.
            client.Client.Shutdown(SocketShutdown.Send);

            var received  = new List<Byte>();
            var closed    = await ReadUntilClosed(stream, received);

            Assert.Multiple(() => {

                Assert.That(closed,    Is.True,
                            "The server did not close the connection of a client that had hung up.");

                Assert.That(received,  Is.Empty,
                            $"A client that hung up before it asked was sent {Octets(received)}");

            });

        }

        #endregion

        #region AClientStillWaitingForItsAnswerIsNotToldTheServerIsGoingAway()

        /// <summary>
        /// A server that is shut down tells every client it has upgraded that
        /// it is going away - and closes a connection it has not answered yet
        /// without a close frame.
        /// </summary>
        /// <remarks>
        /// Shutdown() closes every connection the server lists with 1001,
        /// going away, and the server lists a connection from the moment it
        /// accepts it, not from its 101. A client still in its handshake was
        /// sent 88 02 03 E9 where it waited for HTTP. That upgraded clients
        /// are still told is WebSocketShutdownTests' to show.
        /// </remarks>
        [Test]
        public async Task AClientStillWaitingForItsAnswerIsNotToldTheServerIsGoingAway()
        {

            server = StartServer();

            var accepted = Accepted(server);

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            // Listed by the server by now: it lists a connection before it
            // raises OnNewTCPConnection.
            await accepted.WaitAsync(WaitAtMost);

            await server.Shutdown(Wait: true);
            await server.DisposeAsync();
            server = null;

            var received  = new List<Byte>();
            var closed    = await ReadUntilClosed(stream, received);

            Assert.Multiple(() => {

                Assert.That(closed,    Is.True,
                            "Shutting the server down did not close a connection that had not been answered yet.");

                Assert.That(received,  Is.Empty,
                            $"Shutting the server down sent a connection that had not been answered yet {Octets(received)}");

            });

        }

        #endregion

        #region ARefusalThatDoesNotSayCloseIsSentSayingCloseAndNothingFollowsIt()

        /// <summary>
        /// A validator that refuses the upgrade without saying Connection:
        /// close is answered as it said - but saying close, and the connection
        /// is closed after it rather than taken for an upgrade.
        /// </summary>
        /// <remarks>
        /// Only a 101 opens the connection for frames, but everything that
        /// did not say close was taken for one: the refused connection was
        /// announced as a new WebSocket connection, and the client's next
        /// request - asked again with credentials, as a client does after a
        /// 401 - was read as a frame and drew 88 02 03 EA.
        ///
        /// The validator says that its answer's body is empty, rather than
        /// leave that to the response builder: an answer without a length has a
        /// body that ends with the connection, and would take whatever came
        /// after it for its own.
        /// </remarks>
        [Test]
        public async Task ARefusalThatDoesNotSayCloseIsSentSayingCloseAndNothingFollowsIt()
        {

            server = StartServer();

            var announced = 0;

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken)

                => Task.FromResult<HTTPResponse?>(
                       new HTTPResponse.Builder(connection.HTTPRequest!) {
                           HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                           WWWAuthenticate  = WWWAuthenticate.Basic("WebSocket"),
                           ContentLength    = 0
                       }.AsImmutable
                   );

            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {
                Interlocked.Increment(ref announced);
                return Task.CompletedTask;
            };

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            await Send(stream, UpgradeRequest(server.IPPort));

            var received  = new List<Byte>();
            var closed    = await ReadOneMessage(stream, received);

            // Whatever the server says after its answer is read before the
            // client asks again. A write to a connection the server has closed
            // draws a reset, and on Windows a reset throws away what has come
            // and not been read yet - a close frame after the answer with it.
            if (!closed)
                closed = await ReadUntilClosedOrQuiet(stream, received);

            // Asked again, with credentials, as a client that was challenged
            // does - on a connection the server has kept open, as it used to.
            if (!closed)
            {

                try
                {
                    await Send(stream, UpgradeRequest(server.IPPort, "Authorization: Basic dXNlcjpwYXNzd29yZA==\r\n"));
                }
                catch (IOException)
                { }

                closed = await ReadUntilClosed(stream, received);

            }

            var (message, rest) = SplitOffOneMessage(received);

            Assert.Multiple(() => {

                Assert.That(message,                     Is.Not.Null,
                            $"No complete HTTP answer arrived, only {Octets(received)}");

                Assert.That(message,                     Does.StartWith("HTTP/1.1 401 "),
                            "The validator's refusal was not the answer.");

                Assert.That(ConnectionOptions(message),  Does.Contain("close"),
                            $"The refusal did not say that the connection closes:{Environment.NewLine}{message}");

                Assert.That(rest,                        Is.Empty,
                            $"After the refusal came {Octets(rest)}");

                Assert.That(closed,                      Is.True,
                            "The server did not close the connection after the refusal.");

                Assert.That(announced,                   Is.Zero,
                            "The refused connection was announced as a new WebSocket connection.");

            });

        }

        #endregion

        #region ARefusalThatSaysKeepAliveSaysCloseInstead()

        /// <summary>
        /// A refusal that says keep-alive says close instead, and loses the
        /// Keep-Alive field that went with it.
        /// </summary>
        /// <remarks>
        /// An answer that said keep-alive and close at once would contradict
        /// itself, and one that said keep-alive and was closed after all would
        /// break its word. Whatever else it says stays.
        /// </remarks>
        [Test]
        public async Task ARefusalThatSaysKeepAliveSaysCloseInstead()
        {

            server = StartServer();

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken)

                => Task.FromResult<HTTPResponse?>(
                       new HTTPResponse.Builder(connection.HTTPRequest!) {
                           HTTPStatusCode  = HTTPStatusCode.Forbidden,
                           Connection      = ConnectionType.KeepAlive,
                           ContentLength   = 0
                       }.Set("Keep-Alive", "timeout=5, max=100").
                         Set("X-Refused-By", "a validator").
                         AsImmutable
                   );

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            await Send(stream, UpgradeRequest(server.IPPort));

            var received  = new List<Byte>();
            var closed    = await ReadUntilClosed(stream, received);

            var (message, rest) = SplitOffOneMessage(received);

            Assert.Multiple(() => {

                Assert.That(message,                     Does.StartWith("HTTP/1.1 403 "),
                            $"The validator's refusal was not the answer, {Octets(received)} was.");

                Assert.That(ConnectionOptions(message),  Is.EqualTo(new[] { "close" }),
                            $"The refusal does not say close, and close alone:{Environment.NewLine}{message}");

                Assert.That(message,                     Does.Not.Contain("\r\nKeep-Alive:"),
                            $"The refusal still says how long the connection is kept alive:{Environment.NewLine}{message}");

                Assert.That(message,                     Does.Contain("\r\nX-Refused-By: a validator\r\n"),
                            $"The refusal lost a field of its own on the way:{Environment.NewLine}{message}");

                Assert.That(rest,                        Is.Empty,
                            $"After the refusal came {Octets(rest)}");

                Assert.That(closed,                      Is.True,
                            "The server did not close the connection after the refusal.");

            });

        }

        #endregion

        #region ARefusalThatOffersAnUpgradeKeepsOfferingItAndSaysCloseToo()

        /// <summary>
        /// A refusal that names the protocol to upgrade to - a 426 Upgrade
        /// Required, with an Upgrade field and "Connection: Upgrade" - keeps
        /// both and says close as well. And for all the Upgrade in it, it is no
        /// upgrade: the connection is closed after it, and not announced.
        /// </summary>
        /// <remarks>
        /// RFC 9110, section 7.8: a sender of Upgrade also sends the "Upgrade"
        /// connection option. So close is added to the options, rather than
        /// put in their place. And the connection was upgraded wherever the
        /// Connection field was not exactly "close" - which "Upgrade, close"
        /// is not.
        /// </remarks>
        [Test]
        public async Task ARefusalThatOffersAnUpgradeKeepsOfferingItAndSaysCloseToo()
        {

            server = StartServer();

            var announced = 0;

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken)

                => Task.FromResult<HTTPResponse?>(
                       new HTTPResponse.Builder(connection.HTTPRequest!) {
                           HTTPStatusCode       = HTTPStatusCode.UpgradeRequired,
                           Upgrade              = "websocket",
                           Connection           = ConnectionType.Upgrade,
                           SecWebSocketVersion  = "13",
                           ContentLength        = 0
                       }.AsImmutable
                   );

            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {
                Interlocked.Increment(ref announced);
                return Task.CompletedTask;
            };

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            await Send(stream, UpgradeRequest(server.IPPort));

            var received  = new List<Byte>();
            var closed    = await ReadUntilClosed(stream, received);

            var (message, rest) = SplitOffOneMessage(received);

            Assert.Multiple(() => {

                Assert.That(message,                     Does.StartWith("HTTP/1.1 426 "),
                            $"The validator's refusal was not the answer, {Octets(received)} was.");

                Assert.That(ConnectionOptions(message),  Is.EquivalentTo(new[] { "upgrade", "close" }),
                            $"The refusal does not say both upgrade and close:{Environment.NewLine}{message}");

                Assert.That(message,                     Does.Contain("\r\nUpgrade: websocket\r\n"),
                            $"The refusal no longer says what to upgrade to:{Environment.NewLine}{message}");

                Assert.That(rest,                        Is.Empty,
                            $"After the refusal came {Octets(rest)}");

                Assert.That(closed,                      Is.True,
                            "The server did not close the connection after the refusal.");

                Assert.That(announced,                   Is.Zero,
                            "The refused connection was announced as a new WebSocket connection.");

            });

        }

        #endregion

        #region AnUpgradedConnectionIsStillFailedWithACloseFrame()

        /// <summary>
        /// After a 101, the close frame is where it belongs: a client that
        /// sends an unmasked frame is failed with 1002, protocol error.
        /// </summary>
        /// <remarks>
        /// RFC 6455, section 5.1: a server closes the connection upon a frame
        /// from the client that is not masked, and may say 1002 as it does.
        /// This is also the proof that the reading in the other tests would
        /// have seen a close frame, had one come.
        /// </remarks>
        [Test]
        public async Task AnUpgradedConnectionIsStillFailedWithACloseFrame()
        {

            server = StartServer();

            using var client = await ConnectTo(server);
            var       stream = client.GetStream();

            await Send(stream, UpgradeRequest(server.IPPort));

            var received = new List<Byte>();

            await ReadOneMessage(stream, received);

            // "hi" as a final text frame, and not masked.
            await stream.WriteAsync(new Byte[] { 0x81, 0x02, 0x68, 0x69 });

            var closed = await ReadUntilClosed(stream, received);

            var (message, rest) = SplitOffOneMessage(received);

            Assert.Multiple(() => {

                Assert.That(message,  Does.StartWith("HTTP/1.1 101 "),
                            $"The upgrade was not answered with a 101, but with {Octets(received)}");

                Assert.That(rest,     Is.EqualTo(ProtocolErrorCloseFrame),
                            $"After the 101 and an unmasked frame came {Octets(rest)}");

                Assert.That(closed,   Is.True,
                            "The server did not close the connection after an unmasked frame.");

            });

        }

        #endregion


        #region (private static) StartServer()

        /// <summary>
        /// A mirror server on 127.0.0.1 and a port of its own choosing, which
        /// lets everybody in that asks properly.
        /// </summary>
        private static WebSocketMirrorServer StartServer()

            => new (
                   IPAddress:              IPv4Address.Localhost,
                   HTTPPort:               IPPort.Zero,
                   RequireAuthentication:  false,
                   AutoStart:              true
               );

        #endregion

        #region (private static) Accepted(Server)

        /// <summary>
        /// Completed once the given server has taken on a connection.
        /// </summary>
        private static Task Accepted(AWebSocketServer Server)
        {

            var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Server.OnNewTCPConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                accepted.TrySetResult();
                return Task.CompletedTask;
            };

            return accepted.Task;

        }

        #endregion

        #region (private static) ConnectTo(Server) / Send(Stream, Text)

        /// <summary>
        /// A plain TCP connection to the given server.
        /// </summary>
        private static async Task<TcpClient> ConnectTo(AWebSocketServer Server)
        {

            var client = new TcpClient();

            await client.ConnectAsync("127.0.0.1", Server.IPPort.ToInt32());

            return client;

        }

        /// <summary>
        /// Send the given text, as ASCII.
        /// </summary>
        private static async Task Send(NetworkStream  Stream,
                                       String         Text)
        {
            await Stream.WriteAsync(Encoding.ASCII.GetBytes(Text));
        }

        #endregion

        #region (private static) UpgradeRequest(Port, MoreHeaderLines = "")

        /// <summary>
        /// A request for an upgrade that the server has nothing to object to,
        /// with the given header lines in addition.
        /// </summary>
        private static String UpgradeRequest(IPPort  Port,
                                             String  MoreHeaderLines   = "")

            => "GET / HTTP/1.1\r\n" +
              $"Host: 127.0.0.1:{Port}\r\n" +
               "Upgrade: websocket\r\n" +
               "Connection: Upgrade\r\n" +
               "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
               "Sec-WebSocket-Version: 13\r\n" +
               MoreHeaderLines +
               "\r\n";

        #endregion

        #region (private static) ReadUntilClosed / ReadOneMessage / ReadUntilClosedOrQuiet(Stream, Received)

        /// <summary>
        /// Read whatever comes until the server closes the connection, or for
        /// as long as a test waits at most.
        /// </summary>
        /// <param name="Stream">The connection to read from.</param>
        /// <param name="Received">Where what is read is added.</param>
        /// <returns>Whether the server closed the connection.</returns>
        private static Task<Boolean> ReadUntilClosed(NetworkStream  Stream,
                                                     List<Byte>     Received)

            => Read(Stream, Received, received => false);

        /// <summary>
        /// Read until one whole HTTP message has come, or the server closes
        /// the connection, or for as long as a test waits at most - and not an
        /// octet further than the read that completed the message.
        /// </summary>
        /// <param name="Stream">The connection to read from.</param>
        /// <param name="Received">Where what is read is added.</param>
        /// <returns>Whether the server closed the connection.</returns>
        private static Task<Boolean> ReadOneMessage(NetworkStream  Stream,
                                                    List<Byte>     Received)

            => Read(Stream, Received, received => MessageLength(received).HasValue);

        /// <summary>
        /// Read whatever comes until the server closes the connection, or until
        /// nothing has come for half a second.
        /// </summary>
        /// <param name="Stream">The connection to read from.</param>
        /// <param name="Received">Where what is read is added.</param>
        /// <returns>Whether the server closed the connection.</returns>
        private static Task<Boolean> ReadUntilClosedOrQuiet(NetworkStream  Stream,
                                                            List<Byte>     Received)

            => Read(Stream, Received, received => false, Quiet: TimeSpan.FromMilliseconds(500));

        private static async Task<Boolean> Read(NetworkStream              Stream,
                                                List<Byte>                 Received,
                                                Func<List<Byte>, Boolean>  Enough,
                                                TimeSpan?                  Quiet   = null)
        {

            var buffer = new Byte[4096];

            using var timeout = new CancellationTokenSource(WaitAtMost);

            try
            {
                while (!Enough(Received))
                {

                    using var quiet = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);

                    if (Quiet.HasValue)
                        quiet.CancelAfter(Quiet.Value);

                    var read = await Stream.ReadAsync(buffer, quiet.Token);

                    if (read == 0)
                        return true;

                    Received.AddRange(buffer.Take(read));

                }
            }
            catch (OperationCanceledException)
            { }

            // A reset is a close as well.
            catch (IOException)
            {
                return true;
            }

            return false;

        }

        #endregion

        #region (private static) MessageLength(Received) / SplitOffOneMessage(Received)

        /// <summary>
        /// How many of the given octets the HTTP message at their beginning
        /// takes - its header, the empty line and a body as long as its
        /// Content-Length says - or null, where the message is not complete.
        /// </summary>
        /// <remarks>
        /// A message without a Content-Length is taken to have no body: every
        /// answer these tests expect is either a 101, which has none, or says
        /// how long its body is.
        /// </remarks>
        private static Int32? MessageLength(IReadOnlyList<Byte> Received)
        {

            var headerEnd = -1;

            for (var i = 0; i + 3 < Received.Count; i++)
            {
                if (Received[i] == '\r' && Received[i + 1] == '\n' && Received[i + 2] == '\r' && Received[i + 3] == '\n')
                {
                    headerEnd = i;
                    break;
                }
            }

            if (headerEnd < 0)
                return null;

            var header         = Encoding.ASCII.GetString(Received.Take(headerEnd).ToArray());
            var contentLength  = 0;

            foreach (var line in header.Split("\r\n").Skip(1))
            {

                var colon = line.IndexOf(':');

                if (colon > 0 &&
                    line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = Int32.Parse(line[(colon + 1)..].Trim());
                }

            }

            var length = headerEnd + 4 + contentLength;

            return Received.Count >= length
                       ? length
                       : null;

        }

        /// <summary>
        /// The HTTP message at the beginning of the given octets, as text, and
        /// the octets after it - or null and nothing, where no whole message
        /// has come.
        /// </summary>
        private static (String? Message, Byte[] After) SplitOffOneMessage(List<Byte> Received)
        {

            var length = MessageLength(Received);

            if (!length.HasValue)
                return (null, []);

            return (Encoding.ASCII.GetString(Received.Take(length.Value).ToArray()),
                    Received.Skip(length.Value).ToArray());

        }

        #endregion

        #region (private static) ConnectionOptions(Message)

        /// <summary>
        /// The connection options of the given HTTP message, from all of its
        /// Connection header lines, in lower case.
        /// </summary>
        private static IEnumerable<String> ConnectionOptions(String? Message)
        {

            if (Message is null)
                return [];

            var header = Message.Split("\r\n\r\n")[0];

            return header.Split("\r\n").
                          Skip(1).
                          Select(line => line.Split(':', 2)).
                          Where (field => field.Length == 2 &&
                                          field[0].Trim().Equals("Connection", StringComparison.OrdinalIgnoreCase)).
                          SelectMany(field => field[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).
                          Select(option => option.ToLowerInvariant()).
                          ToArray();

        }

        #endregion

        #region (private static) Octets(Received)

        /// <summary>
        /// The given octets for an assertion message: how many there are, and
        /// what they are - printable ASCII as itself, line ends as \r and \n,
        /// and every other octet in hex, so that a close frame is plain to see.
        /// </summary>
        private static String Octets(IEnumerable<Byte> Received)
        {

            var octets = Received.ToArray();

            if (octets.Length == 0)
                return "nothing";

            var text = new StringBuilder();

            foreach (var octet in octets)
            {
                text.Append(octet switch {
                                (Byte) '\r'           => "\\r",
                                (Byte) '\n'           => "\\n",
                                >= 0x20 and < 0x7F    => ((Char) octet).ToString(),
                                _                     => $"<{octet:X2}>"
                            });
            }

            return $"{octets.Length} octets: {text}";

        }

        #endregion

    }

}
