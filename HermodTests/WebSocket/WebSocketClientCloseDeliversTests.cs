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
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// What a client sent before it closed its connection reaches the server.
    /// </summary>
    /// <remarks>
    /// The client's connection sent its close frame and closed the socket in the
    /// same breath. A socket closed with data on it that nobody read, or one that
    /// is sent something after it was closed, answers with a reset - and on
    /// Windows a reset throws away what the other side has received and not yet
    /// read. So a server that was a moment late in reading found a reset where
    /// the client's last message and its close frame had been: a charging station
    /// that answered a request and closed lost the answer one time in three.
    ///
    /// The server here is a socket the test reads by hand, and it reads late:
    /// only after the client has closed. Other systems hand the server what it
    /// had received before they report the reset, and these tests pass there
    /// with or without the client waiting.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientCloseDeliversTests
    {

        #region Data

        private TcpListener?       listener;
        private Socket?            serverSide;
        private WebSocketClient?   client;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void Listen()
        {
            listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
        }

        [TearDown]
        public async Task TearDown()
        {

            try { serverSide?.Close(); } catch { }
            serverSide = null;

            if (client is not null)
                await client.DisposeAsync();

            client = null;

            listener?.Dispose();

        }

        #endregion


        #region WhatWasSentBeforeACloseArrives_WithUnreadDataOnTheClientsSocket()

        /// <summary>
        /// The client closes from within its handler of a message, as a charging
        /// station does that answers and hangs up - and the server has sent it
        /// another message meanwhile, which nobody is going to read.
        /// </summary>
        [Test]
        public async Task WhatWasSentBeforeACloseArrives_WithUnreadDataOnTheClientsSocket()
        {

            var inHandler   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var goOn        = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var hasClosed   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            client = NewClient();

            client.OnTextMessageReceived += async (timestamp, webSocketClient, connection, frame, eventTrackingId, text, cancellationToken) => {

                if (text != "first")
                    return;

                inHandler.TrySetResult();
                await goOn.Task;

                await webSocketClient.SendTextMessage("last words");
                await connection.     Close(WebSocketFrame.ClosingStatusCode.PolicyViolation, "bye");

                hasClosed.TrySetResult();

            };

            var connection = await ConnectAndUpgrade();

            // The client's loop is held in its handler of the first message,
            // and so the second one stays on its socket.
            await serverSide!.SendAsync(WebSocketFrame.Text("first"). ToByteArray());
            await inHandler.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await serverSide!.SendAsync(WebSocketFrame.Text("second").ToByteArray());

            Assert.That(await Until(() => connection.Available > 0, TimeSpan.FromSeconds(10)), Is.True,
                        "The second message never reached the client's socket.");

            goOn.SetResult();

            // The server reads late: a reset, if there is one, is in by then.
            await Task.WhenAny(hasClosed.Task, Task.Delay(TimeSpan.FromMilliseconds(300)));
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            var (frames, readError) = await ReadToTheEnd();

            AssertLastWordsAndClose(frames, readError);

            await hasClosed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        }

        #endregion

        #region WhatWasSentBeforeACloseArrives_WhenTheServerStillSendsAfterIt()

        /// <summary>
        /// The client closes, and the server - which has not read that yet -
        /// sends it one more message.
        /// </summary>
        [Test]
        public async Task WhatWasSentBeforeACloseArrives_WhenTheServerStillSendsAfterIt()
        {

            client = NewClient();

            var connection = await ConnectAndUpgrade();

            Assert.That(await client.SendTextMessage("last words"), Is.EqualTo(SentStatus.Success));

            var closing = connection.Close(WebSocketFrame.ClosingStatusCode.PolicyViolation, "bye");

            await Task.WhenAny(closing, Task.Delay(TimeSpan.FromMilliseconds(300)));

            // To a socket that is closed already this is answered with a reset.
            await serverSide!.SendAsync(WebSocketFrame.Text("one more thing").ToByteArray());
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            var (frames, readError) = await ReadToTheEnd();

            AssertLastWordsAndClose(frames, readError);

            // The server has closed its end by now, and the client does not wait any longer.
            await closing.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(connection.IsClosed, Is.True);

        }

        #endregion

        #region ACloseDoesNotWaitForLongForAServerThatNeverCloses()

        /// <summary>
        /// A server that neither answers the close frame nor closes its end holds
        /// the client for no longer than the connection's close handshake timeout.
        /// </summary>
        [Test]
        public async Task ACloseDoesNotWaitForLongForAServerThatNeverCloses()
        {

            client = NewClient();

            var connection = await ConnectAndUpgrade(CloseHandshakeTimeout: TimeSpan.FromMilliseconds(500));

            var took = Stopwatch.StartNew();
            await connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, "bye").WaitAsync(TimeSpan.FromSeconds(20));
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(connection.IsClosed, Is.True);
                Assert.That(took.Elapsed,        Is.LessThan(TimeSpan.FromSeconds(5)),
                            $"The close took {took.Elapsed.TotalSeconds:F1} s for a server that never closed its end.");
            });

            // And what was sent is there all the same.
            var (frames, readError) = await ReadToTheEnd();

            Assert.That(frames.Any(frame => frame.Opcode == WebSocketFrame.Opcodes.Close), Is.True,
                        $"The server did not get the close frame (read ended with: {readError?.ToString() ?? "end of stream"}).");

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

            client = NewClient();

            var connection = await ConnectAndUpgrade();

            var first   = connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, "first");
            var second  = connection.Close(WebSocketFrame.ClosingStatusCode.GoingAway,     "second");

            var (frames, readError) = await ReadToTheEnd();

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

            var closeFrames = frames.Where(frame => frame.Opcode == WebSocketFrame.Opcodes.Close).ToArray();

            Assert.Multiple(() => {
                Assert.That(closeFrames.Length,                Is.EqualTo(1),
                            $"Close frames received: {closeFrames.Length} (read ended with: {readError?.ToString() ?? "end of stream"}).");
                Assert.That(closeFrames.FirstOrDefault()?.GetClosingReason(), Is.EqualTo("first"));
            });

        }

        #endregion


        #region (private) NewClient()

        private WebSocketClient NewClient()

            => new (URL.Parse($"ws://127.0.0.1:{((IPEndPoint) listener!.LocalEndpoint).Port}/late"),
                    DisableWebSocketPings:  true,
                    RequestTimeout:         TimeSpan.FromSeconds(20));

        #endregion

        #region (private) ConnectAndUpgrade(CloseHandshakeTimeout = null)

        /// <summary>
        /// Connect the client, answer its upgrade by hand, and from then on
        /// read nothing until the test says so.
        /// </summary>
        /// <param name="CloseHandshakeTimeout">How long the client's connection waits for the server to close: long by default, so that a busy machine does not end the wait the tests are about.</param>
        private async Task<WebSocketClientConnection> ConnectAndUpgrade(TimeSpan? CloseHandshakeTimeout = null)
        {

            var accepted = new TaskCompletionSource<WebSocketClientConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            client!.OnWebSocketConnectionAccepted += (timestamp, webSocketClient, connection, response, cancellationToken) => {
                connection.CloseHandshakeTimeout = CloseHandshakeTimeout ?? TimeSpan.FromSeconds(30);
                accepted.TrySetResult(connection);
                return Task.CompletedTask;
            };

            var connecting  = client.Connect();

            serverSide      = await listener!.AcceptSocketAsync().WaitAsync(TimeSpan.FromSeconds(10));

            var request     = new StringBuilder();
            var buffer      = new Byte[4096];

            while (!request.ToString().Contains("\r\n\r\n"))
            {

                var read = await serverSide.ReceiveAsync(buffer).WaitAsync(TimeSpan.FromSeconds(10));

                Assert.That(read, Is.GreaterThan(0), "The client hung up before its upgrade request was whole.");

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));

            }

            var key = request.ToString().
                              Split("\r\n").
                              First (line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).
                              Split (':', 2)[1].
                              Trim  ();

            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            await serverSide.SendAsync(
                      Encoding.ASCII.GetBytes(
                          "HTTP/1.1 101 Switching Protocols\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                         $"Sec-WebSocket-Accept: {accept}\r\n" +
                          "\r\n"
                      )
                  );

            var (_, response) = await connecting.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.SwitchingProtocols));

            return await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        }

        #endregion

        #region (private) ReadToTheEnd()

        /// <summary>
        /// Read what the client sent since its upgrade, up to the end of the
        /// stream or to the error the read ended with, and close the server's end.
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

                    var read = await serverSide!.ReceiveAsync(buffer, SocketFlags.None, giveUp.Token);

                    if (read == 0)
                        break;

                    received.Write(buffer, 0, read);

                    // A client that waits for the server to close gets just that,
                    // once its close frame is in.
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
                // A client that never ends its side: what came is what there is.
                readError = SocketError.TimedOut;
            }

            try { serverSide!.Close(); } catch { }

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

        #region (private static) AssertLastWordsAndClose(Frames, ReadError)

        private static void AssertLastWordsAndClose(List<WebSocketFrame>  Frames,
                                                    SocketError?          ReadError)
        {

            var ended = ReadError?.ToString() ?? "end of stream";

            Assert.Multiple(() => {

                Assert.That(Frames.Any(frame => frame.Opcode == WebSocketFrame.Opcodes.Text &&
                                                frame.Payload.ToUTF8String() == "last words"),
                            Is.True,
                            $"The server did not get what the client sent before it closed: {Frames.Count} frame(s), read ended with: {ended}.");

                Assert.That(Frames.LastOrDefault()?.Opcode,                 Is.EqualTo(WebSocketFrame.Opcodes.Close),
                            $"The server did not get the client's close frame: {Frames.Count} frame(s), read ended with: {ended}.");

                Assert.That(Frames.LastOrDefault()?.GetClosingStatusCode(), Is.EqualTo(WebSocketFrame.ClosingStatusCode.PolicyViolation));

            });

        }

        #endregion

        #region (private static) Until(Condition, Within)

        private static async Task<Boolean> Until(Func<Boolean> Condition, TimeSpan Within)
        {

            var giveUp = Stopwatch.StartNew();

            while (giveUp.Elapsed < Within)
            {

                if (Condition())
                    return true;

                await Task.Delay(10);

            }

            return Condition();

        }

        #endregion

    }

}
