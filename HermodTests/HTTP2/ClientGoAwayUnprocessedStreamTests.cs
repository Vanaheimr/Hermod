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
    /// Streams a GOAWAY leaves unprocessed: those above its last-stream-id,
    /// which the server has not acted on, will not act on, and whose frames it
    /// ignores (RFC 9113, Section 6.8). Their exchanges failed with
    /// HTTP2RequestNotProcessedException, as they still do, but the streams
    /// stayed open until the connection ended. A request body still being sent
    /// on one, and the writes of a streamed request there, went on into the
    /// void, ahead of the streams the server still serves. And an accepted
    /// tunnel there — only a server that breaks Section 6.8 leaves one above
    /// its last-stream-id, as answering the CONNECT is acting on it — read
    /// nothing more, and its reads waited until the whole connection ended,
    /// which after a graceful GOAWAY may take long.
    ///
    /// The GOAWAY now closes such a stream at once, as though it had never been
    /// opened: a reset of the client's own, which sends nothing, as for a
    /// stream a GOAWAY catches before its HEADERS go out. A tunnel reads its
    /// end, null, as after the server's RST_STREAM, and a WebSocket over it
    /// receives null; a write fails with an OperationCanceledException, and
    /// nothing more goes out on the stream. The streams the GOAWAY covers go on
    /// as before.
    /// </summary>
    [TestFixture]
    public class ClientGoAwayUnprocessedStreamTests
    {

        #region (helpers)

        /// <summary>
        /// What a GOAWAY leaves unprocessed, for the writes on it.
        /// </summary>
        public enum UnprocessedStream
        {

            /// <summary>
            /// An accepted CONNECT tunnel.
            /// </summary>
            Tunnel,

            /// <summary>
            /// A streamed request still waiting for its answer
            /// (<see cref="HTTP2ClientConnection.StartStreamingRequestAsync"/>).
            /// </summary>
            StreamedRequest

        }

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        private static String? Text(Byte[]? Bytes)

            => Bytes is null
                   ? null
                   : Encoding.ASCII.GetString(Bytes);

        /// <summary>
        /// The stream of the first header block the client sent for
        /// <paramref name="Target"/> — a method and path, or CONNECT and an
        /// authority or path.
        /// </summary>
        private static async Task<UInt32> StreamOfAsync(ClientWire  Wire,
                                                        String      Target)
        {

            var lines = await Wire.UntilAsync(lines => lines.Any(line => line.Contains($" HEADERS {Target}")), $"the HEADERS of {Target}");

            return ClientWire.StreamOf(lines.First(line => line.Contains($" HEADERS {Target}")));

        }

        /// <summary>
        /// Answer a stream with a 200 that keeps it open.
        /// </summary>
        private static Task AcceptAsync(HoldingH2Transport  Transport,
                                        UInt32              StreamId)

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            => Transport.SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                            new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                            EndStream:  false,
                                                            EndHeaders: true));

        /// <summary>
        /// Open a plain CONNECT tunnel, and accept it.
        /// </summary>
        private static async Task<HTTP2ClientTunnel> OpenTunnelAsync(HoldingH2Transport     Transport,
                                                                     ClientWire             Wire,
                                                                     HTTP2ClientConnection  Connection,
                                                                     String                 Authority,
                                                                     HTTP2Priority?         Priority   = null)
        {

            var opening = Connection.OpenTunnelAsync(Authority, Priority: Priority);

            await AcceptAsync(Transport, await StreamOfAsync(Wire, $"CONNECT {Authority}"));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

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


        #region ATunnelAboveTheLastStreamId_ReadsItsEnd_AtTheGoAway()

        /// <summary>
        /// The server accepts two tunnels, then sends a GOAWAY that covers only
        /// the first. The other tunnel reads what had arrived on it before the
        /// GOAWAY, and then its end, null, at once: it used to wait until the
        /// whole connection ended. The covered tunnel goes on reading, the
        /// connection stays open, and only the covered tunnel's stream still
        /// counts as active.
        /// </summary>
        [Test]
        public async Task ATunnelAboveTheLastStreamId_ReadsItsEnd_AtTheGoAway()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection   = await transport.ConnectAsync(new HTTP2ClientOptions());
            var covered      = await OpenTunnelAsync(transport, wire, connection, "covered.internal:443");
            var unprocessed  = await OpenTunnelAsync(transport, wire, connection, "unprocessed.internal:443");

            await transport.SendAsync(HTTP2Frame.CreateData(unprocessed.StreamId, ASCII("before the GOAWAY"), EndStream: false));

            var coveredReading = covered.ReadAsync(CancellationToken.None);

            // Having answered the CONNECT, the server has acted on the second
            // tunnel's stream, and breaks RFC 9113, Section 6.8 with a
            // last-stream-id below it. The client takes it at its word.
            await transport.SendAsync(HTTP2Frame.CreateGoAway(covered.StreamId, HTTP2ErrorCode.NO_ERROR));

            var (firstEnded,  first,  firstFailure)   = await EndOf(unprocessed.ReadAsync(CancellationToken.None));
            var (secondEnded, second, secondFailure)  = await EndOf(unprocessed.ReadAsync(CancellationToken.None));

            var coveredWaited  = !coveredReading.IsCompleted;
            var stillOpen      = !connection.Closed.IsCompleted;
            var activeStreams  = connection.ActiveStreamCount;

            await transport.SendAsync(HTTP2Frame.CreateData(covered.StreamId, ASCII("after the GOAWAY"), EndStream: false));

            var (coveredEnded, coveredRead, coveredFailure) = await EndOf(coveredReading);

            Assert.Multiple(() =>
            {

                Assert.That(firstEnded,         Is.True,                         "the first read of the unprocessed tunnel returned");
                Assert.That(firstFailure,       Is.Null,                         "how the first read failed");
                Assert.That(Text(first),        Is.EqualTo("before the GOAWAY"), "what the first read returned: what had arrived before the GOAWAY");

                Assert.That(secondEnded,        Is.True,                         "the second read returned at the GOAWAY, rather than at the end of the connection");
                Assert.That(secondFailure,      Is.Null,                         "how the second read failed");
                Assert.That(second,             Is.Null,                         "what the second read returned: the tunnel's end");

                Assert.That(coveredWaited,      Is.True,                         "the read of the covered tunnel, still waiting after the GOAWAY");
                Assert.That(stillOpen,          Is.True,                         "the connection, still open after the GOAWAY");
                Assert.That(activeStreams,      Is.EqualTo(1),                   "the streams still counted as active after the GOAWAY: the covered tunnel's");

                Assert.That(coveredEnded,       Is.True,                         "the covered tunnel's read returned");
                Assert.That(coveredFailure,     Is.Null,                         "how the covered tunnel's read failed");
                Assert.That(Text(coveredRead),  Is.EqualTo("after the GOAWAY"),  "what the covered tunnel read: what the server sent after the GOAWAY");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region AWebSocketAboveTheLastStreamId_ReceivesNull_AtTheGoAway()

        /// <summary>
        /// A client WebSocket waits for the server's next message, with no token
        /// of its own, when a GOAWAY leaves its stream unprocessed. ReceiveAsync
        /// returns null, as when its tunnel ends without a close handshake, while
        /// the connection is still open. It used to wait until the connection
        /// ended.
        /// </summary>
        [Test]
        public async Task AWebSocketAboveTheLastStreamId_ReceivesNull_AtTheGoAway()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var opening     = connection.OpenWebSocketAsync("localhost", URIScheme.https, "/ocpp");

            await AcceptAsync(transport, await StreamOfAsync(wire, "CONNECT /ocpp"));

            var webSocket   = await opening.WaitAsync(HoldingH2Transport.StepTimeout);

            var receiving   = webSocket.ReceiveAsync(CancellationToken.None);
            var waited      = !receiving.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateGoAway(0, HTTP2ErrorCode.NO_ERROR));

            var (ended, message, failure) = await EndOf(receiving);

            var stillOpen   = !connection.Closed.IsCompleted;

            Assert.Multiple(() =>
            {

                Assert.That(waited,     Is.True,  "the receive, waiting while the server sends nothing");

                Assert.That(ended,      Is.True,  "the receive returned at the GOAWAY, rather than at the end of the connection");
                Assert.That(failure,    Is.Null,  "how the receive failed");
                Assert.That(message,    Is.Null,  "what the WebSocket received: its end");

                Assert.That(stillOpen,  Is.True,  "the connection, still open after the GOAWAY");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WritesAboveTheLastStreamId_Fail_AndNothingMoreGoesOut(What)

        /// <summary>
        /// A write on a tunnel, or on a streamed request, waits for window when a
        /// GOAWAY leaves its stream unprocessed, and the server then grants the
        /// stream window all the same. The write fails with an
        /// OperationCanceledException, as at the end of the connection, and so
        /// does a write after the GOAWAY; ending our side returns, sending
        /// nothing, as an end that does not go out always has. Nothing goes out
        /// on the stream after its HEADERS: the waiting write, the late one and
        /// the END_STREAM used to go out into the void, to a server that ignores
        /// them. A streamed request's response fails as unprocessed, as ever.
        /// </summary>
        [TestCase(UnprocessedStream.Tunnel)]
        [TestCase(UnprocessedStream.StreamedRequest)]
        public async Task WritesAboveTheLastStreamId_Fail_AndNothingMoreGoesOut(UnprocessedStream What)
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream: a write waits for it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var covered     = await OpenTunnelAsync(transport, wire, connection, "covered.internal:443");

            Func<Byte[], Task>               write;
            Func<Task>                       end;
            Task<HTTP2ResponseHead>?         responseHead  = null;
            UInt32                           streamId;
            String                           headers;

            if (What == UnprocessedStream.Tunnel)
            {

                var tunnel    = await OpenTunnelAsync(transport, wire, connection, "unprocessed.internal:443");

                write         = data => tunnel.WriteAsync(data, CancellationToken.None);
                end           = tunnel.CloseAsync;
                streamId      = tunnel.StreamId;
                headers       = $"{streamId} HEADERS CONNECT unprocessed.internal:443";

            }
            else
            {

                var upload    = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload").
                                                 WaitAsync(HoldingH2Transport.StepTimeout);

                write         = data => upload.WriteAsync(data);
                end           = () => upload.CompleteRequestAsync();
                responseHead  = upload.GetResponseAsync();
                streamId      = upload.StreamId;
                headers       = $"{streamId} HEADERS POST /upload";

            }

            var waiting     = write(ASCII("waits for window"));
            var waited      = !waiting.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateGoAway(covered.StreamId, HTTP2ErrorCode.NO_ERROR));

            // Window for both streams: nothing may use it on the one the GOAWAY
            // leaves unprocessed.
            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(streamId,          100));
            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(covered.StreamId,  100));

            var (waitEnded,  waitFailure)   = await EndOf(waiting);
            var (lateEnded,  lateFailure)   = await EndOf(write(ASCII("after the GOAWAY")));
            var (endEnded,   endFailure)    = await EndOf(end());

            // Written once all of the above has returned, or failed: whatever of
            // it went out on the unprocessed stream went out before this.
            await covered.WriteAsync(ASCII("on the covered tunnel"), CancellationToken.None).WaitAsync(HoldingH2Transport.StepTimeout);

            var lines = await wire.UntilAsync(lines => lines.Contains($"{covered.StreamId} DATA 21"), "the DATA on the covered tunnel");

            var (headEnded, _, headFailure) = responseHead is not null
                                                  ? await EndOf(responseHead)
                                                  : (true, null, null);

            Assert.Multiple(() =>
            {

                Assert.That(waited,                          Is.True,                                      "the write, waiting for window before the GOAWAY");

                Assert.That(waitEnded,                       Is.True,                                      "the waiting write ended");
                Assert.That(waitFailure,                     Is.InstanceOf<OperationCanceledException>(),  "how the waiting write ended");

                Assert.That(lateEnded,                       Is.True,                                      "a write after the GOAWAY ended");
                Assert.That(lateFailure,                     Is.InstanceOf<OperationCanceledException>(),  "how a write after the GOAWAY ended");

                Assert.That(endEnded,                        Is.True,                                      "ending our side after the GOAWAY returned");
                Assert.That(endFailure,                      Is.Null,                                      "how ending our side failed");

                Assert.That(ClientWire.On(lines, streamId),  Is.EqualTo(new[] { headers }),                "what the client sent on the unprocessed stream: its HEADERS, and nothing more");

                if (What == UnprocessedStream.StreamedRequest)
                {
                    Assert.That(headEnded,                   Is.True,                                      "the response ended");
                    Assert.That(headFailure,                 Is.TypeOf<HTTP2RequestNotProcessedException>(),
                                                                                                           "how the response failed: unprocessed, and safe to send again elsewhere");
                }

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ARequestBodyAboveTheLastStreamId_StopsGoingOut_AtTheGoAway()

        /// <summary>
        /// A request's body waits for window when a GOAWAY leaves its stream
        /// unprocessed, and the server then grants the stream window all the
        /// same. The request fails as unprocessed, as ever, and the body stays
        /// unsent: it used to go out to a server that ignores it, ahead of the
        /// DATA of a stream the GOAWAY covers. That stream asks for a lower
        /// urgency, so that it would go out second even when both have window
        /// at once.
        /// </summary>
        [Test]
        public async Task ARequestBodyAboveTheLastStreamId_StopsGoingOut_AtTheGoAway()
        {

            await using var transport  = new HoldingH2Transport(HoldHeadersOf: streamId => false);
            await using var wire       = new ClientWire(transport);

            var connection   = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream: a body waits for it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var covered      = await OpenTunnelAsync(transport, wire, connection, "covered.internal:443", new HTTP2Priority(7, false));

            var responding   = connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload", Body: new Byte[100]);
            var streamId     = await StreamOfAsync(wire, "POST /upload");

            var coveredWrite = covered.WriteAsync(ASCII("on the covered tunnel"), CancellationToken.None);

            await transport.SendAsync(HTTP2Frame.CreateGoAway(covered.StreamId, HTTP2ErrorCode.NO_ERROR));

            var (ended, response, failure) = await EndOf(responding);

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(streamId,          1000));
            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(covered.StreamId,  1000));

            await coveredWrite.WaitAsync(HoldingH2Transport.StepTimeout);

            var lines = await wire.UntilAsync(lines => lines.Contains($"{covered.StreamId} DATA 21"), "the DATA on the covered tunnel");

            Assert.Multiple(() =>
            {

                Assert.That(ended,                           Is.True,                                         "the request ended at the GOAWAY");
                Assert.That(response,                        Is.Null,                                         "the response");
                Assert.That(failure,                         Is.TypeOf<HTTP2RequestNotProcessedException>(),  "how the request failed: unprocessed, and safe to send again elsewhere");

                Assert.That(ClientWire.On(lines, streamId),  Is.EqualTo(new[] { $"{streamId} HEADERS POST /upload" }),
                                                                                                              "what the client sent on the unprocessed stream: its HEADERS, and none of its body");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
