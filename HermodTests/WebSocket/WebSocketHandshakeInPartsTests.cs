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
using System.Diagnostics;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket handshake that arrives in parts is answered once the whole
    /// of it has arrived - and one whose header never ends is not waited for
    /// beyond the limits of a handshake.
    /// </summary>
    /// <remarks>
    /// The server used to parse the handshake from whatever the first read of
    /// the connection returned. A read returns what has arrived, and that need
    /// not be all that was sent: a slow link, a TLS record boundary or a proxy
    /// that passes on what it has can split a request anywhere. Parsed from
    /// its first part, a request was refused for the header fields that were
    /// still on their way - "The 'Connection: Upgrade' header is missing!" -
    /// or it did not parse at all and got the server's own 400 without a word.
    /// It is the empty line that ends an HTTP header, and the server now reads
    /// on until that has arrived.
    ///
    /// Reading on is bounded as reading was before: by HandshakeTimeout, which
    /// runs from the moment the connection was accepted, and by
    /// MaxHandshakeRequestSize, which counts everything kept while waiting and
    /// not only what the last read returned.
    ///
    /// Over a plain TCP client, because the WebSocket client writes its
    /// request in one go - and over a loopback, what is written in one go
    /// arrives in one read.
    /// </remarks>
    [TestFixture]
    public class WebSocketHandshakeInPartsTests
    {

        #region Data

        /// <summary>
        /// A handshake that is answered 101 when it arrives in one piece. Its
        /// key is the sample nonce of RFC 6455 section 1.3.
        /// </summary>
        private const            String    Handshake     = "GET / HTTP/1.1\r\n"                              +
                                                           "Host: 127.0.0.1\r\n"                             +
                                                           "Upgrade: websocket\r\n"                          +
                                                           "Connection: Upgrade\r\n"                         +
                                                           "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                                                           "Sec-WebSocket-Version: 13\r\n"                   +
                                                           "\r\n";

        /// <summary>
        /// The pause between two parts of a handshake: long enough for the
        /// server to have read the one before the next is written, so that
        /// each arrives in a read of its own.
        /// </summary>
        private static readonly  TimeSpan  BetweenParts  = TimeSpan.FromMilliseconds(300);

        private WebSocketMirrorServer?  server;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.Shutdown(Wait: true);

            server = null;

        }

        #endregion


        #region AHandshakeInTwoPartsIsUpgraded()

        /// <summary>
        /// A handshake split between two of its header lines - the upgrade
        /// asked for in the first part, the Connection header, the key and the
        /// version in the second - is upgraded.
        /// </summary>
        /// <remarks>
        /// Answered from its first part, this was a 400 with the body "The
        /// 'Connection: Upgrade' header is missing!": a request refused for a
        /// header it had not yet finished sending.
        /// </remarks>
        [Test]
        public Task AHandshakeInTwoPartsIsUpgraded()

            => UpgradedInParts(CutAt(Handshake.IndexOf("Connection: Upgrade")));

        #endregion

        #region AHandshakeCutInsideALineIsUpgraded()

        /// <summary>
        /// A handshake cut inside a line - its request line, between "GET / HT"
        /// and "TP/1.1" - is upgraded.
        /// </summary>
        /// <remarks>
        /// Answered from its first part, this was a request that did not parse
        /// at all, and it got the server's own 400: no body, and not a word of
        /// why.
        /// </remarks>
        [Test]
        public Task AHandshakeCutInsideALineIsUpgraded()

            => UpgradedInParts(CutAt("GET / HT".Length));

        #endregion

        #region AHandshakeCutInsideItsMethodIsUpgraded()

        /// <summary>
        /// A handshake whose first part is too short to hold its method - "GE",
        /// and everything else after it - is upgraded.
        /// </summary>
        /// <remarks>
        /// The server tells a handshake from anything else by its first four
        /// bytes, "GET ". With two of them it took the connection for something
        /// else and closed it. What can still become a "GET " is now waited
        /// for; what cannot is turned away at once, as before.
        /// </remarks>
        [Test]
        public Task AHandshakeCutInsideItsMethodIsUpgraded()

            => UpgradedInParts(CutAt("GE".Length));

        #endregion

        #region AHandshakeCutInsideItsEmptyLineIsUpgraded()

        /// <summary>
        /// A handshake cut inside the empty line that ends it - everything up
        /// to its last carriage return in the first part, the line feed alone
        /// in the second - is upgraded, and the connection works.
        /// </summary>
        /// <remarks>
        /// Every header field is in the first part here, and the server used to
        /// upgrade from that part alone: it answered 101 to a request it had
        /// not yet received whole, and then read the line feed that followed as
        /// the first byte of a WebSocket frame, so that the first message the
        /// client sent failed the connection. The end of a header has to be
        /// found where it lies across two reads - and nothing of it may be left
        /// over for the frames.
        /// </remarks>
        [Test]
        public Task AHandshakeCutInsideItsEmptyLineIsUpgraded()

            => UpgradedInParts(CutAt(Handshake.Length - "\n".Length));

        #endregion

        #region AHandshakeWithBareLineFeedsIsStillUpgraded()

        /// <summary>
        /// A handshake whose lines end in a bare line feed, written in one
        /// piece, is upgraded, as it was before.
        /// </summary>
        /// <remarks>
        /// RFC 9112 section 2.2 lets a recipient take a bare line feed for the
        /// end of a line, and the parser does. Such a request ends in two line
        /// feeds with no carriage return between them - so waiting for the end
        /// of the header must not mean waiting for "\r\n\r\n", or a request
        /// that used to be answered at once would now be left to time out.
        /// </remarks>
        [Test]
        public Task AHandshakeWithBareLineFeedsIsStillUpgraded()

            => UpgradedInParts(Handshake.Replace("\r\n", "\n"));

        #endregion

        #region ARequestThatCannotBeAHandshakeIsStillTurnedAwayAtOnce()

        /// <summary>
        /// A request that cannot become a handshake - a PUT whose header never
        /// ends - is still turned away at once, and not waited for.
        /// </summary>
        /// <remarks>
        /// Only what can still become a "GET " is read on for. Anything else
        /// was closed as soon as it had been read, and still is: it will not
        /// become a handshake by being waited for, and holding its connection
        /// open until the handshake timeout would only hand it that long. The
        /// timeout is set a minute out, so that a wait would show. How the
        /// server says goodbye is not asked here - only that it does not wait.
        /// </remarks>
        [Test]
        public async Task ARequestThatCannotBeAHandshakeIsStillTurnedAwayAtOnce()
        {

            server = NewServer();
            server.HandshakeTimeout = TimeSpan.FromMinutes(1);

            var (_, closed, after) = await SendUntilClosed(
                                               server.IPPort,
                                               TimeSpan.Zero,
                                               [
                                                   "PUT / HTTP/1.1\r\n" +
                                                   "Host: 127.0.0.1\r\n"
                                               ]
                                           );

            Assert.Multiple(() => {

                Assert.That(closed,  Is.True,
                            "The server kept the connection of a request that cannot become a handshake open, waiting for the rest of it.");

                Assert.That(after,   Is.LessThan(TimeSpan.FromSeconds(5)),
                            "The server waited for the rest of a request that cannot become a handshake.");

            });

        }

        #endregion

        #region AHeaderThatNeverEndsIsClosedAtTheHandshakeTimeout()

        /// <summary>
        /// A header that keeps coming, a line at a time, and never ends is not
        /// answered, and its connection is closed at the handshake timeout.
        /// </summary>
        /// <remarks>
        /// Slowloris: a peer that ties up a connection by sending just enough
        /// to keep it waiting. Reading on for the rest of a request must not
        /// become a way around HandshakeTimeout, and it does not: the deadline
        /// runs from the moment the connection was accepted, and no line that
        /// arrives moves it.
        ///
        /// Answered from its first part, this was refused at once with a 400.
        /// Now the server waits for the rest, so it is the deadline that has to
        /// end the wait - and nothing is answered, because nothing was asked.
        /// </remarks>
        [Test]
        public async Task AHeaderThatNeverEndsIsClosedAtTheHandshakeTimeout()
        {

            server = NewServer();
            server.HandshakeTimeout = TimeSpan.FromSeconds(1);

            // A line every 100 ms for eight seconds: far past the timeout, and
            // nowhere near the size limit.
            var (received, closed, after) = await SendUntilClosed(
                                                      server.IPPort,
                                                      TimeSpan.FromMilliseconds(100),
                                                      [
                                                          "GET / HTTP/1.1\r\n",
                                                          "Host: 127.0.0.1\r\n",
                                                          .. Enumerable.Range(1, 80).Select(line => $"X-Still-Coming-{line}: yes\r\n")
                                                      ]
                                                  );

            Assert.Multiple(() => {

                Assert.That(closed,    Is.True,
                            "The server kept the connection of a header that never ended open past its handshake timeout.");

                Assert.That(received,  Is.Empty,
                            $"The server answered a request whose header had not ended:{Environment.NewLine}{received}");

                Assert.That(after,     Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(0.5)),
                            "The server hung up long before its handshake timeout, rather than waiting for the rest of the header.");

                Assert.That(after,     Is.LessThan(TimeSpan.FromSeconds(4)),
                            "The lines that kept arriving pushed the handshake timeout back.");

            });

        }

        #endregion

        #region AHeaderThatNeverEndsIsClosedAtTheSizeLimit()

        /// <summary>
        /// A header that keeps coming and never ends is not answered either
        /// when it comes in parts that are each far below the size limit of a
        /// handshake, and its connection is closed once they add up to more.
        /// </summary>
        /// <remarks>
        /// What MaxHandshakeRequestSize bounds is everything kept of a request
        /// while the server waits for the rest of it - and not only what the
        /// last read returned, which no part here comes near. The handshake
        /// timeout is set a minute out, so that it cannot be what ends the
        /// connection.
        /// </remarks>
        [Test]
        public async Task AHeaderThatNeverEndsIsClosedAtTheSizeLimit()
        {

            server = NewServer();
            server.HandshakeTimeout         = TimeSpan.FromMinutes(1);
            server.MaxHandshakeRequestSize  = 2048;

            // Lines of 200 bytes, one every 20 ms: 64 of them, more than six
            // times the limit.
            var (received, closed, _) = await SendUntilClosed(
                                                  server.IPPort,
                                                  TimeSpan.FromMilliseconds(20),
                                                  [
                                                      "GET / HTTP/1.1\r\n" +
                                                      "Host: 127.0.0.1\r\n",
                                                      .. Enumerable.Range(1, 64).Select(line => $"X-Filler-{line:D2}: {new String('x', 185)}\r\n")
                                                  ]
                                              );

            Assert.Multiple(() => {

                Assert.That(closed,    Is.True,
                            "The server kept the connection of a header that never ended open past its size limit.");

                Assert.That(received,  Is.Empty,
                            $"The server answered a request whose header had not ended:{Environment.NewLine}{received}");

            });

        }

        #endregion


        #region (private static) NewServer()

        /// <summary>
        /// A mirror server that lets anybody in, on a port of its own.
        /// </summary>
        /// <remarks>
        /// Bound to 127.0.0.1 rather than to the default, the dual-stack
        /// localhost, which a container without IPv6 cannot bind.
        /// </remarks>
        private static WebSocketMirrorServer NewServer()

            => new (IPAddress:              IPv4Address.Localhost,
                    HTTPPort:               IPPort.Zero,
                    RequireAuthentication:  false,
                    AutoStart:              true);

        #endregion

        #region (private) UpgradedInParts(Parts)

        /// <summary>
        /// Ask for an upgrade in the given parts, each written on its own, and
        /// expect it to be answered 101 once the last part is in and not before
        /// - and the connection that opens to work.
        /// </summary>
        /// <remarks>
        /// That it works is asked as well as the 101: a text message is sent
        /// over it, and the mirror server has to send it back, reversed.
        /// Whatever the server kept of a request while it waited for the rest
        /// has to be gone once the handshake is done. Left over, it would stand
        /// in front of the first frame and fail the connection for a protocol
        /// violation the client never committed.
        /// </remarks>
        /// <param name="Parts">The parts of the handshake, in order.</param>
        private async Task UpgradedInParts(params String[] Parts)
        {

            server = NewServer();

            using var tcpClient  = new TcpClient();

            await tcpClient.ConnectAsync("127.0.0.1", server.IPPort.ToInt32());

            // Each part goes out when it is written, and not only once the one
            // before it has been acknowledged.
            tcpClient.NoDelay    = true;

            var stream           = tcpClient.GetStream();
            var answeredEarly    = false;

            try
            {
                for (var part = 0; part < Parts.Length; part++)
                {

                    if (part > 0)
                    {

                        await Task.Delay(BetweenParts);

                        // Answered before the request was whole: that alone is
                        // wrong, whatever the answer. The rest is not sent, and
                        // what came back says what the server made of the part
                        // it had. Sent anyway, to a server that hung up after
                        // its answer, it would draw a reset - and on Windows a
                        // reset throws away what was received and not yet read.
                        if (stream.DataAvailable)
                        {
                            answeredEarly = true;
                            break;
                        }

                    }

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(Parts[part]));

                }
            }

            // Hung up on before the last part: what came back says why.
            catch (IOException)
            { }

            var (answer, _, _)   = await Read(stream, HeaderHasEnded);
            var answerText       = Encoding.ASCII.GetString(answer);

            Assert.That(answeredEarly, Is.False,
                        $"The server answered a handshake before the last of its {Parts.Length} parts had been written:{Environment.NewLine}{answerText}");

            Assert.That(answerText, Does.StartWith("HTTP/1.1 101"),
                        $"A handshake written in {Parts.Length} part(s) was not upgraded. The server answered:{Environment.NewLine}{answerText}");

            await stream.WriteAsync(WebSocketFrame.Text("Hello",
                                                        Mask:        WebSocketFrame.MaskStatus.On,
                                                        MaskingKey:  [ 0x37, 0xfa, 0x21, 0x3d ]).ToByteArray());

            var (echo, _, _)     = await Read(stream, received => WebSocketFrame.TryParse(received, out _, out _, out _));

            var echoed           = WebSocketFrame.TryParse(echo, out var frame, out _, out _)
                                       ? $"{frame.Opcode}: {frame.Payload.ToUTF8String()}"
                                       : $"{echo.Length} byte(s), and no frame";

            Assert.That(echoed, Is.EqualTo("Text: olleH"),
                        $"The connection opened by a handshake written in {Parts.Length} part(s) did not mirror the first message sent over it.");

        }

        #endregion

        #region (private static) CutAt(At)

        /// <summary>
        /// The handshake, cut in two at the given position.
        /// </summary>
        /// <param name="At">Where the second part begins.</param>
        private static String[] CutAt(Int32 At)

            => [ Handshake[..At], Handshake[At..] ];

        #endregion

        #region (private static) SendUntilClosed(Port, Pause, Parts)

        /// <summary>
        /// Send the given parts of a request one at a time, with the given pause
        /// between them, for as long as the server keeps the connection open -
        /// and read whatever it sends meanwhile.
        /// </summary>
        /// <remarks>
        /// Read while writing, and not afterwards, so that a close is seen when
        /// it happens, and not only once the last part has gone out.
        /// </remarks>
        /// <param name="Port">The port the server listens on.</param>
        /// <param name="Pause">The pause between two parts.</param>
        /// <param name="Parts">The parts of the request, in order.</param>
        /// <returns>What came back, whether the server closed the connection, and how long after it was opened.</returns>
        private static async Task<(String Received, Boolean Closed, TimeSpan After)> SendUntilClosed(IPPort               Port,
                                                                                                     TimeSpan             Pause,
                                                                                                     IEnumerable<String>  Parts)
        {

            using var tcpClient  = new TcpClient();

            await tcpClient.ConnectAsync("127.0.0.1", Port.ToInt32());

            tcpClient.NoDelay    = true;

            var stream           = tcpClient.GetStream();
            var reading          = Read(stream, Enough: _ => false);

            foreach (var part in Parts)
            {

                if (reading.IsCompleted)
                    break;

                try
                {
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(part));
                }

                // Hung up on.
                catch (IOException)
                {
                    break;
                }

                await Task.WhenAny(reading, Task.Delay(Pause));

            }

            var (received, closed, after) = await reading;

            return (Encoding.ASCII.GetString(received), closed, after);

        }

        #endregion

        #region (private static) Read(Stream, Enough)

        /// <summary>
        /// Read what the server sends until it is enough, or until the server
        /// closes the connection - for ten seconds at the most.
        /// </summary>
        /// <param name="Stream">The stream to read from.</param>
        /// <param name="Enough">Whether what has been read so far is enough.</param>
        /// <returns>What was read, whether the server closed the connection, and how long the reading took.</returns>
        private static async Task<(Byte[] Received, Boolean Closed, TimeSpan Took)> Read(Stream                 Stream,
                                                                                        Func<Byte[], Boolean>  Enough)
        {

            var took             = Stopwatch.StartNew();
            var received         = new MemoryStream();
            var buffer           = new Byte[4096];
            var closed           = false;

            using var timeout    = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                while (!closed && !Enough(received.ToArray()))
                {

                    var read = await Stream.ReadAsync(buffer, timeout.Token);

                    if (read == 0)
                        closed = true;

                    received.Write(buffer, 0, read);

                }
            }
            catch (OperationCanceledException)
            { }

            // A reset is a close as well.
            catch (IOException)
            {
                closed = true;
            }

            return (received.ToArray(), closed, took.Elapsed);

        }

        #endregion

        #region (private static) HeaderHasEnded(Received)

        /// <summary>
        /// Whether what was received holds the whole header of an HTTP answer.
        /// </summary>
        /// <param name="Received">What was received so far.</param>
        private static Boolean HeaderHasEnded(Byte[] Received)

            => Encoding.ASCII.GetString(Received).Contains("\r\n\r\n");

        #endregion

    }

}
