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

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// What a server sent before it closed a connection reaches the client.
    /// </summary>
    /// <remarks>
    /// The server's connection sent its close frame and closed the socket in the
    /// same breath, as the client's did - see WebSocketClientCloseDeliversTests.
    /// A client that sent one more message to that socket was answered with a
    /// reset, and on Windows the reset threw away what the client had received
    /// and not yet read: the server's last message and its close frame.
    ///
    /// The client here is a socket the test reads by hand, and it reads late.
    /// Other systems hand the client what it had received before they report
    /// the reset, and the first of these tests passes there with or without
    /// the server waiting.
    /// </remarks>
    [TestFixture]
    public class WebSocketServerCloseDeliversTests
    {

        #region Data

        private WebSocketMirrorServer?  server;
        private Socket?                 clientSide;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void StartServer()
        {
            server = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);
        }

        [TearDown]
        public async Task TearDown()
        {

            try { clientSide?.Close(); } catch { }
            clientSide = null;

            if (server is not null)
            {
                await server.Shutdown();
                await server.DisposeAsync();
            }

            server = null;

        }

        #endregion


        #region WhatWasSentBeforeACloseArrives_WhenTheClientStillSendsAfterIt()

        /// <summary>
        /// The server closes, and the client - which has not read that yet -
        /// sends it one more message.
        /// </summary>
        [Test]
        public async Task WhatWasSentBeforeACloseArrives_WhenTheClientStillSendsAfterIt()
        {

            var connection = await ConnectAndUpgrade();

            Assert.That(await server!.SendTextMessage(connection, "last words"), Is.EqualTo(SentStatus.Success));

            var closing = connection.Close(WebSocketFrame.ClosingStatusCode.PolicyViolation, "bye");

            await Task.WhenAny(closing, Task.Delay(TimeSpan.FromMilliseconds(300)));

            // To a socket that is closed already this is answered with a reset.
            await clientSide!.SendAsync(Masked(WebSocketFrame.Opcodes.Text, "one more thing"));
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            var (frames, readError) = await ReadToTheEnd();

            var ended = readError?.ToString() ?? "end of stream";

            Assert.Multiple(() => {

                Assert.That(frames.Any(frame => frame.Opcode == WebSocketFrame.Opcodes.Text &&
                                                frame.Payload.ToUTF8String() == "last words"),
                            Is.True,
                            $"The client did not get what the server sent before it closed: {frames.Count} frame(s), read ended with: {ended}.");

                Assert.That(frames.LastOrDefault()?.Opcode,                 Is.EqualTo(WebSocketFrame.Opcodes.Close),
                            $"The client did not get the server's close frame: {frames.Count} frame(s), read ended with: {ended}.");

                Assert.That(frames.LastOrDefault()?.GetClosingStatusCode(), Is.EqualTo(WebSocketFrame.ClosingStatusCode.PolicyViolation));

            });

            // The client has closed its end by now, and the server does not wait any longer.
            await closing.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(connection.IsClosed, Is.True);

        }

        #endregion

        #region AClientsCloseFrameEndsTheWait()

        /// <summary>
        /// A client answers the server's close frame with its own and then waits
        /// for the server to close the connection, as RFC 6455 has it: the
        /// server does not wait for the client to hang up first.
        /// </summary>
        [Test]
        public async Task AClientsCloseFrameEndsTheWait()
        {

            var connection = await ConnectAndUpgrade(CloseHandshakeTimeout: TimeSpan.FromSeconds(30));

            var took     = Stopwatch.StartNew();
            var closing  = connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, "bye");

            await clientSide!.SendAsync(Masked(WebSocketFrame.Opcodes.Close));

            await closing.WaitAsync(TimeSpan.FromSeconds(20));
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(connection.IsClosed, Is.True);
                Assert.That(took.Elapsed,        Is.LessThan(TimeSpan.FromSeconds(10)),
                            $"The close took {took.Elapsed.TotalSeconds:F1} s although the client had answered the close frame.");
            });

        }

        #endregion

        #region ACloseDoesNotWaitForLongForAClientThatNeverCloses()

        /// <summary>
        /// A client that neither answers the close frame nor closes its end holds
        /// the server for no longer than the connection's close handshake timeout.
        /// </summary>
        [Test]
        public async Task ACloseDoesNotWaitForLongForAClientThatNeverCloses()
        {

            var connection = await ConnectAndUpgrade(CloseHandshakeTimeout: TimeSpan.FromMilliseconds(500));

            var took = Stopwatch.StartNew();
            await connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, "bye").WaitAsync(TimeSpan.FromSeconds(20));
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(connection.IsClosed, Is.True);
                Assert.That(took.Elapsed,        Is.LessThan(TimeSpan.FromSeconds(5)),
                            $"The close took {took.Elapsed.TotalSeconds:F1} s for a client that never closed its end.");
            });

        }

        #endregion

        #region ASecondCloseSendsNoSecondCloseFrame()

        /// <summary>
        /// Whoever closes a connection that is being closed already neither sends
        /// a second close frame nor waits for the first to be answered.
        /// </summary>
        [Test]
        public async Task ASecondCloseSendsNoSecondCloseFrame()
        {

            var connection = await ConnectAndUpgrade();

            var first   = connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, "first");
            var second  = connection.Close(WebSocketFrame.ClosingStatusCode.GoingAway,     "second");

            var (frames, readError) = await ReadToTheEnd();

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

            var closeFrames = frames.Where(frame => frame.Opcode == WebSocketFrame.Opcodes.Close).ToArray();

            Assert.Multiple(() => {
                Assert.That(closeFrames.Length,                               Is.EqualTo(1),
                            $"Close frames received: {closeFrames.Length} (read ended with: {readError?.ToString() ?? "end of stream"}).");
                Assert.That(closeFrames.FirstOrDefault()?.GetClosingReason(), Is.EqualTo("first"));
            });

        }

        #endregion


        #region (private) ConnectAndUpgrade(CloseHandshakeTimeout = null)

        /// <summary>
        /// Connect a socket, upgrade it by hand, and from then on read nothing
        /// until the test says so.
        /// </summary>
        /// <param name="CloseHandshakeTimeout">How long the server's connection waits for the client to close: long by default, so that a busy machine does not end the wait the tests are about.</param>
        private async Task<WebSocketServerConnection> ConnectAndUpgrade(TimeSpan? CloseHandshakeTimeout = null)
        {

            clientSide = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await clientSide.ConnectAsync(System.Net.IPAddress.Loopback, server!.IPPort.ToUInt16());

            await clientSide.SendAsync(
                      Encoding.ASCII.GetBytes(
                          "GET / HTTP/1.1\r\n" +
                         $"Host: 127.0.0.1:{server.IPPort}\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                          "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                          "Sec-WebSocket-Version: 13\r\n" +
                          "\r\n"
                      )
                  );

            var buffer    = new Byte[4096];
            var response  = new StringBuilder();

            while (!response.ToString().Contains("\r\n\r\n"))
            {

                var read = await clientSide.ReceiveAsync(buffer).WaitAsync(TimeSpan.FromSeconds(10));

                Assert.That(read, Is.GreaterThan(0), "The server hung up before its answer to the upgrade was whole.");

                response.Append(Encoding.ASCII.GetString(buffer, 0, read));

            }

            Assert.That(response.ToString(), Does.StartWith("HTTP/1.1 101"));

            WebSocketServerConnection? connection = null;

            var giveUp = Stopwatch.StartNew();

            while (connection is null && giveUp.Elapsed < TimeSpan.FromSeconds(10))
            {

                connection = server.WebSocketConnections.FirstOrDefault();

                if (connection is null)
                    await Task.Delay(10);

            }

            Assert.That(connection, Is.Not.Null, "The server does not know of the connection.");

            connection!.CloseHandshakeTimeout = CloseHandshakeTimeout ?? TimeSpan.FromSeconds(30);

            return connection;

        }

        #endregion

        #region (private static) Masked(Opcode, Text = "")

        /// <summary>
        /// A text or a close frame as a client sends it.
        /// </summary>
        private static Byte[] Masked(WebSocketFrame.Opcodes  Opcode,
                                     String                  Text   = "")

            => (Opcode == WebSocketFrame.Opcodes.Close
                    ? WebSocketFrame.Close(WebSocketFrame.ClosingStatusCode.NormalClosure,
                                           null,
                                           WebSocketFrame.Fin.Final,
                                           WebSocketFrame.MaskStatus.On,
                                           [ 1, 2, 3, 4 ])
                    : WebSocketFrame.Text (Text,
                                           WebSocketFrame.Fin.Final,
                                           WebSocketFrame.MaskStatus.On,
                                           [ 1, 2, 3, 4 ])).ToByteArray();

        #endregion

        #region (private) ReadToTheEnd()

        /// <summary>
        /// Read what the server sent since its 101, up to its close frame, the
        /// end of the stream or the error the read ended with, and close the
        /// client's end.
        /// </summary>
        private async Task<(List<WebSocketFrame> Frames, SocketError? ReadError)> ReadToTheEnd()
        {

            var          received   = new MemoryStream();
            var          buffer     = new Byte[4096];
            SocketError? readError  = null;

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                while (true)
                {

                    var read = await clientSide!.ReceiveAsync(buffer, SocketFlags.None, giveUp.Token);

                    if (read == 0)
                        break;

                    received.Write(buffer, 0, read);

                    if (Frames(received.ToArray()).Any(frame => frame.Opcode == WebSocketFrame.Opcodes.Close))
                        break;

                }
            }
            catch (SocketException e)
            {
                readError = e.SocketErrorCode;
            }
            catch (OperationCanceledException)
            {
                readError = SocketError.TimedOut;
            }

            try { clientSide!.Close(); } catch { }

            return (Frames(received.ToArray()), readError);

        }

        #endregion

        #region (private static) Frames(Bytes)

        private static List<WebSocketFrame> Frames(Byte[] Bytes)
        {

            var frames  = new List<WebSocketFrame>();
            var offset  = 0;

            while (offset < Bytes.Length &&
                   WebSocketFrame.Parse(Bytes.AsSpan(offset), out var frame, out var length, out _, out _) == WebSocketFrame.ParseResult.Success &&
                   frame is not null)
            {
                frames.Add(frame);
                offset += (Int32) length;
            }

            return frames;

        }

        #endregion

    }

}
