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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Tunnels still open when their client connection ends: the server goes
    /// away, the client closes the connection, or the connection's DATA writer
    /// loop fails. The end fails every request still waiting for its answer,
    /// but an accepted CONNECT tunnel has nothing left to fail — its CONNECT
    /// has its answer — and nothing ended what the tunnel reads. A tunnel's
    /// ReadAsync, unless a token of its caller's ended it, waited for good on a
    /// connection that was gone, and so did a WebSocket's ReceiveAsync over it:
    /// a charging station that keeps its OCPP WebSockets over one HTTP/2
    /// connection never learned that its backend was gone.
    ///
    /// The end of a client connection now resets every stream still open on
    /// it, as the end of a server connection does since 37598d96: a tunnel
    /// reads its end, null, as after the server's RST_STREAM, and a WebSocket
    /// receives null, as when its tunnel ends without a close handshake. A
    /// write waiting in the tunnel's queue fails as it did before the reset,
    /// with an OperationCanceledException.
    /// </summary>
    [TestFixture]
    public class ClientConnectionEndTests
    {

        #region (helpers)

        /// <summary>
        /// How the connection ends.
        /// </summary>
        public enum ConnectionEnd
        {

            /// <summary>
            /// The server goes away: the read loop finds the end of the stream.
            /// </summary>
            ServerGoesAway,

            /// <summary>
            /// The client closes the connection (<see cref="HTTP2ClientConnection.CloseAsync"/>).
            /// </summary>
            ClientCloses,

            /// <summary>
            /// The DATA writer loop fails: the transport fails the write of an
            /// upload's DATA on another stream, and the loop ends the connection at
            /// once (see ClientWriterLoopFailureTests).
            /// </summary>
            WriterLoopFails

        }

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Open a plain CONNECT tunnel, and answer it with a 200.
        /// </summary>
        private static async Task<HTTP2ClientTunnel> OpenTunnelAsync(HoldingH2Transport     Transport,
                                                                     ClientWire             Wire,
                                                                     HTTP2ClientConnection  Connection)
        {

            var opening = Connection.OpenTunnelAsync("echo.internal:443");

            await Wire.UntilAsync(lines => lines.Contains("1 HEADERS CONNECT echo.internal:443"), "the CONNECT");

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            await Transport.SendAsync(HTTP2Frame.CreateHeaders(1,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        /// <summary>
        /// Open a WebSocket (RFC 8441), and answer its CONNECT with a 200.
        /// </summary>
        private static async Task<WebSocketConnection> OpenWebSocketAsync(HoldingH2Transport     Transport,
                                                                          ClientWire             Wire,
                                                                          HTTP2ClientConnection  Connection)
        {

            var opening = Connection.OpenWebSocketAsync("localhost", URIScheme.https, "/ocpp");

            await Wire.UntilAsync(lines => lines.Contains("1 HEADERS CONNECT /ocpp"), "the CONNECT");

            await Transport.SendAsync(HTTP2Frame.CreateHeaders(1,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        /// <summary>
        /// End the connection as <paramref name="How"/> says, and return whether
        /// it has ended within the step timeout.
        /// </summary>
        private static async Task<Boolean> EndAsync(HoldingH2Transport     Transport,
                                                    HTTP2ClientConnection  Connection,
                                                    ConnectionEnd          How)
        {

            switch (How)
            {

                case ConnectionEnd.ServerGoesAway:
                    await Transport.EndServerSideAsync();
                    break;

                case ConnectionEnd.ClientCloses:
                    await Connection.CloseAsync();
                    break;

                case ConnectionEnd.WriterLoopFails:

                    var upload = await Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload").
                                                  WaitAsync(HoldingH2Transport.StepTimeout);

                    // Window for the upload, should the test have given new streams none.
                    await Transport.SendAsync(HTTP2Frame.CreateWindowUpdate(upload.StreamId, 100));

                    Transport.FailNextDataWrite(upload.StreamId, new IOException("The write failed on purpose"));

                    _ = upload.WriteAsync(ASCII("on a broken transport"));

                    break;

            }

            return await Task.WhenAny(Connection.Closed, Task.Delay(HoldingH2Transport.StepTimeout)) == Connection.Closed;

        }

        /// <summary>
        /// How a call ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Call)
        {

            if (await Task.WhenAny(Call, Task.Delay(HoldingH2Transport.StepTimeout)) != Call)
                return (false, null);

            try
            {
                await Call;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        /// <summary>
        /// How a call ended: whether it did within the step timeout, with which
        /// result, and with which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, T? Result, Exception? Failure)> EndOf<T>(Task<T> Call)
        {

            if (await Task.WhenAny(Call, Task.Delay(HoldingH2Transport.StepTimeout)) != Call)
                return (false, default, null);

            try
            {
                return (true, await Call, null);
            }
            catch (Exception e)
            {
                return (true, default, e);
            }

        }

        #endregion


        #region ATunnelReadWaiting_ReadsItsEnd_WhenTheConnectionEnds(How)

        /// <summary>
        /// A tunnel waits for the server's next bytes, with no token of its own,
        /// when the connection ends. The read returns null, the tunnel's end, as
        /// after the server's RST_STREAM, once the connection has ended, and no
        /// stream of the connection counts as active any more. The read used to
        /// wait for good.
        /// </summary>
        [TestCase(ConnectionEnd.ServerGoesAway)]
        [TestCase(ConnectionEnd.ClientCloses)]
        [TestCase(ConnectionEnd.WriterLoopFails)]
        public async Task ATunnelReadWaiting_ReadsItsEnd_WhenTheConnectionEnds(ConnectionEnd How)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var tunnel      = await OpenTunnelAsync(transport, wire, connection);

            var reading     = tunnel.ReadAsync(CancellationToken.None);
            var waited      = !reading.IsCompleted;

            var closed      = await EndAsync(transport, connection, How);

            var (ended, read, failure) = await EndOf(reading);

            Assert.Multiple(() =>
            {

                Assert.That(waited,                        Is.True,  "the read, waiting while the server sends nothing");
                Assert.That(closed,                        Is.True,  "the connection ended");

                Assert.That(ended,                         Is.True,  "the read returned once the connection had ended");
                Assert.That(failure,                       Is.Null,  "how the read failed");
                Assert.That(read,                          Is.Null,  "what the tunnel read: its end");

                Assert.That(connection.ActiveStreamCount,  Is.Zero,  "the streams still counted as active");

            });

        }

        #endregion

        #region AWebSocketReceiveWaiting_ReturnsNull_WhenTheConnectionEnds(How)

        /// <summary>
        /// A client WebSocket waits for the server's next message, with no token
        /// of its own, when the connection ends, as a charging station waits for
        /// its backend's next OCPP call. ReceiveAsync returns null, as when its
        /// tunnel ends without a close handshake, once the connection has ended.
        /// It used to wait for good, and the station never learned that its
        /// backend was gone.
        /// </summary>
        [TestCase(ConnectionEnd.ServerGoesAway)]
        [TestCase(ConnectionEnd.ClientCloses)]
        [TestCase(ConnectionEnd.WriterLoopFails)]
        public async Task AWebSocketReceiveWaiting_ReturnsNull_WhenTheConnectionEnds(ConnectionEnd How)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var webSocket   = await OpenWebSocketAsync(transport, wire, connection);

            var receiving   = webSocket.ReceiveAsync(CancellationToken.None);
            var waited      = !receiving.IsCompleted;

            var closed      = await EndAsync(transport, connection, How);

            var (ended, message, failure) = await EndOf(receiving);

            Assert.Multiple(() =>
            {

                Assert.That(waited,   Is.True,  "the receive, waiting while the server sends nothing");
                Assert.That(closed,   Is.True,  "the connection ended");

                Assert.That(ended,    Is.True,  "the receive returned once the connection had ended");
                Assert.That(failure,  Is.Null,  "how the receive failed");
                Assert.That(message,  Is.Null,  "what the WebSocket received: its end");

            });

        }

        #endregion

        #region AWriteWaitingWhenTheConnectionEnds_IsCanceled(How)

        /// <summary>
        /// A tunnel write waits for window, which the server never grants, when
        /// the connection ends. It fails with an OperationCanceledException, as it
        /// did before the end of the connection reset the stream: that reset has
        /// no error code, and a write reports a reset as an HTTP2StreamException
        /// only with one — the server's RST_STREAM's, or the writer loop's own. A
        /// write after the end fails at once, the same way, and a close returns
        /// at once, sending nothing, as an end that does not go out always has.
        /// </summary>
        [TestCase(ConnectionEnd.ServerGoesAway)]
        [TestCase(ConnectionEnd.ClientCloses)]
        [TestCase(ConnectionEnd.WriterLoopFails)]
        public async Task AWriteWaitingWhenTheConnectionEnds_IsCanceled(ConnectionEnd How)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream: a write waits for it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var tunnel      = await OpenTunnelAsync(transport, wire, connection);

            var writing     = tunnel.WriteAsync(ASCII("waits for window"), CancellationToken.None);
            var waited      = !writing.IsCompleted;

            var closed      = await EndAsync(transport, connection, How);

            var (writeEnded, writeFailure)  = await EndOf(writing);

            var late          = tunnel.WriteAsync(ASCII("after the end"), CancellationToken.None);
            var lateAtOnce    = late.IsCompleted;
            var lateFailure   = (await EndOf(late)).Failure;

            var closing       = tunnel.CloseAsync();
            var closeAtOnce   = closing.IsCompleted;
            var closeFailure  = (await EndOf(closing)).Failure;

            Assert.Multiple(() =>
            {

                Assert.That(waited,        Is.True,                                      "the write, waiting for window");
                Assert.That(closed,        Is.True,                                      "the connection ended");

                Assert.That(writeEnded,    Is.True,                                      "the write ended once the connection had ended");
                Assert.That(writeFailure,  Is.InstanceOf<OperationCanceledException>(),  "how the write ended");

                Assert.That(lateAtOnce,    Is.True,                                      "a write after the end, ended at once");
                Assert.That(lateFailure,   Is.InstanceOf<OperationCanceledException>(),  "how the write after the end ended");

                Assert.That(closeAtOnce,   Is.True,                                      "a close after the end, returned at once");
                Assert.That(closeFailure,  Is.Null,                                      "how the close failed");

            });

        }

        #endregion

    }

}
