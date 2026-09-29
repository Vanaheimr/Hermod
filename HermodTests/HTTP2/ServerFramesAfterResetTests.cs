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

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Frames the client sent on a stream before it read the server's RST_STREAM
    /// for it. The server resets a stream while the client may still be sending
    /// on it: a handler, streaming or tunnel, fails during an upload, or the read
    /// loop finds a stream error in a request. What the client had already sent
    /// arrives afterwards. RFC 9113, Section 5.1: an endpoint that sends RST_STREAM
    /// on a stream that is "open" or "half-closed (local)" "MUST minimally process
    /// and then discard any frames it receives in this state" — a header block
    /// still updates the HPACK state, DATA still counts against the connection's
    /// window.
    ///
    /// The server answered each such frame with a further RST_STREAM STREAM_CLOSED,
    /// as it answers frames after the client's own reset or after an END_STREAM.
    /// A header block there it did not even decode, so the next request on the
    /// connection failed with COMPRESSION_ERROR; one split into CONTINUATION frames
    /// ended the connection at once, and so did a header block on such a stream
    /// once a later stream had opened and the server had pruned it.
    ///
    /// Now such frames are discarded. A stream the client reset, or one it had
    /// already ended when the server reset it, is answered as before.
    /// </summary>
    [TestFixture]
    public class ServerFramesAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Trailers the client sends after its upload. A field the client's
        /// encoder adds to its dynamic table, as it does every field it may
        /// index: the server has to decode the block, or its table and the
        /// client's differ from then on.
        /// </summary>
        private static readonly List<(String Name, String Value)> Trailers = [("x-checksum", "sha-256=:dGhlIHVwbG9hZCBvZiB0aGUgY2xpZW50Cg==:")];

        /// <summary>
        /// Answers every request that is not streamed.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Other(UInt32                             StreamId,
                                                                                                            List<(String Name, String Value)>  Headers,
                                                                                                            Byte[]?                            Body,
                                                                                                            CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], ASCII("other")));

        /// <summary>
        /// A streaming handler whose "/upload" sends its response headers, says so
        /// with <paramref name="HeadersSent"/>, and then fails, once
        /// <paramref name="Fail"/> is completed: the server resets the stream with
        /// INTERNAL_ERROR, while the client's side is still open unless the client
        /// ended it. Any other path is answered "other".
        /// </summary>
        private static HTTP2StreamingHandler FailingUpload(TaskCompletionSource HeadersSent, TaskCompletionSource Fail)

            => async (request, response, cancellationToken) => {

                   if (request.Headers.First(header => header.Name == ":path").Value != "/upload")
                   {
                       await response.WriteHeadersAsync([(":status", "200")]);
                       await response.WriteAsync(ASCII("other"));
                       return;
                   }

                   await response.WriteHeadersAsync([(":status", "200")]);

                   HeadersSent.TrySetResult();

                   await Fail.Task;

                   throw new InvalidOperationException("The handler failed on purpose");

               };

        /// <summary>
        /// Whether the server still answers a ping, rather than having ended the
        /// connection.
        /// </summary>
        private static async Task<Boolean> Answers(PipedH2ServerConnection Peer)
        {

            try
            {
                await Peer.PingAsync();
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }

        }

        /// <summary>
        /// The response on this stream, or null if the server reset the stream,
        /// did not end it in time, or closed the connection.
        /// </summary>
        private static async Task<PipedH2ServerConnection.Response?> ResponseOn(PipedH2ServerConnection Peer, UInt32 StreamId)
        {

            try
            {
                return await Peer.TryResponseAsync(StreamId, PipedH2ServerConnection.StepTimeout);
            }
            catch (EndOfStreamException)
            {
                return null;
            }

        }

        /// <summary>
        /// The GOAWAY frames the server sent.
        /// </summary>
        private static List<String> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => frame.ToString())];

        /// <summary>
        /// What the server sent on this stream, in order.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        #endregion


        #region DataAfterAHandlerResetTheUpload_Discarded_ConnectionServesOn()

        /// <summary>
        /// A streaming handler fails while the client is still uploading, and the
        /// server resets the stream with INTERNAL_ERROR. The client's DATA, sent
        /// before it read that, the last with END_STREAM, is discarded: no second
        /// RST_STREAM, while its window is given back to the connection all the
        /// same. The connection serves the next request.
        /// </summary>
        [Test]
        public async Task DataAfterAHandlerResetTheUpload_Discarded_ConnectionServesOn()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                fail.TrySetResult();

                var resetCode = await peer.ReadToResetAsync(1);

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2"), EndStream: true));

                var answers = await Answers(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(resetCode,                        Is.EqualTo(HTTP2ErrorCode.INTERNAL_ERROR),  "how the server reset the upload");
                    Assert.That(answers,                          Is.True,                                    "the connection, once the client's DATA on the reset stream came in");
                    Assert.That(GoAways(peer),                    Is.Empty,                                   "how the server ended the connection");

                    Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                                  "what the server sent on the reset stream");

                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                              "connection window held back for the discarded DATA");

                });

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region DataAfterATunnelHandlerResetTheTunnel_Discarded_ConnectionServesOn()

        /// <summary>
        /// A tunnel's handler fails: the server ends its side of the tunnel and
        /// resets the stream, "half-closed (local)" by then, while the client is
        /// still sending into the tunnel. What the client sent before it read the
        /// reset is discarded.
        /// </summary>
        [Test]
        public async Task DataAfterATunnelHandlerResetTheTunnel_Discarded_ConnectionServesOn()
        {

            var tunnelOpen  = new TaskCompletionSource(Async);
            var fail        = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    RunAsync    = async (tunnel, cancellationToken) => {

                        tunnelOpen.TrySetResult();

                        await fail.Task;

                        throw new InvalidOperationException("The tunnel's handler failed on purpose");

                    }

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                fail.TrySetResult();

                var resetCode = await peer.ReadToResetAsync(1);

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("to the tunnel 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("to the tunnel 2")));

                var answers = await Answers(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(resetCode,                        Is.EqualTo(HTTP2ErrorCode.INTERNAL_ERROR),  "how the server reset the tunnel");
                    Assert.That(answers,                          Is.True,                                    "the connection, once the client's DATA on the reset stream came in");
                    Assert.That(GoAways(peer),                    Is.Empty,                                   "how the server ended the connection");

                    // In this order: the handler's task resets the stream only once
                    // the END_STREAM that ends the server's side is written (see
                    // ServerDataAfterResetTests).
                    Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                     "DATA \"\" END_STREAM",
                                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                                  "what the server sent on the reset stream");

                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                              "connection window held back for the discarded DATA");

                });

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region DataAfterTheReadLoopResetTheRequest_Discarded()

        /// <summary>
        /// The read loop resets a request itself, for a stream error in its
        /// header block, here a content-length that is no number. The body the
        /// client sends on is discarded.
        /// </summary>
        [Test]
        public async Task DataAfterTheReadLoopResetTheRequest_Discarded()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(Other);

            await peer.SendHeadersAsync(1, [(":method",        "POST"),
                                            (":scheme",        "http"),
                                            (":authority",     "localhost"),
                                            (":path",          "/upload"),
                                            ("content-length", "many")],
                                        EndStream: false);

            var resetCode = await peer.ReadToResetAsync(1);

            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2"), EndStream: true));

            var answers = await Answers(peer);

            Assert.Multiple(() =>
            {

                Assert.That(resetCode,                        Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),  "how the server reset the malformed request");
                Assert.That(answers,                          Is.True,                                    "the connection, once the client's DATA on the reset stream came in");

                Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),
                                                              "what the server sent on the reset stream");

                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                              "connection window held back for the discarded DATA");

            });

            await peer.RequestAsync(3, "/other");

            var other = await ResponseOn(peer, 3);

            Assert.That(other?.Body, Is.EqualTo("other"), "body of the next response");

        }

        #endregion

        #region EndStreamThatMeetsTheReset_Discarded()

        /// <summary>
        /// The client's last DATA frame, with END_STREAM, passes the read loop's
        /// check of the stream's state while the stream is open; then, before the
        /// read loop ends the client's side, the handler fails on its own task and
        /// the server resets the stream. The end of the client's side finds the
        /// stream reset: the frame was sent before the client read the reset, and
        /// is discarded, not answered with STREAM_CLOSED.
        /// </summary>
        [Test]
        public async Task EndStreamThatMeetsTheReset_Discarded()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream        = peer.ServerStream(1);
                var windowBefore  = stream.RecvWindow;

                await peer.HoldDataAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("last"), EndStream: true), async () => {

                    fail.TrySetResult();

                    // The handler's task resets the stream and sends RST_STREAM.
                    await peer.ReadToResetAsync(1);

                });

                var answers = await Answers(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(answers,                          Is.True,                       "the connection, once the frame that met the reset was handled");
                    Assert.That(GoAways(peer),                    Is.Empty,                      "how the server ended the connection");

                    Assert.That(stream.RecvWindow,                Is.EqualTo(windowBefore - 4),  "the stream's window, charged for the frame as on an open stream: the reset came after the read loop's check");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                 "connection window held back for the frame, which nobody will read");

                    Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                                  "what the server sent on the reset stream");

                });

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.That(other?.Body, Is.EqualTo("other"), "body of the next response");

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region TrailersAfterAHandlerResetTheUpload_DecodedAndDiscarded()

        /// <summary>
        /// The client's trailers, sent before it read the reset, are discarded,
        /// but only once they are decoded: the client's encoder has put their
        /// field into its dynamic table, and the next request refers to the
        /// table past it. Undecoded, that request failed with COMPRESSION_ERROR,
        /// and the connection with it.
        /// </summary>
        [Test]
        public async Task TrailersAfterAHandlerResetTheUpload_DecodedAndDiscarded()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                fail.TrySetResult();

                await peer.ReadToResetAsync(1);

                await peer.SendHeadersAsync(1, Trailers, EndStream: true);

                var answers = await Answers(peer);

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(answers,          Is.True,            "the connection, once the trailers on the reset stream came in");
                    Assert.That(GoAways(peer),    Is.Empty,           "how the server ended the connection");

                    Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                  "what the server sent on the reset stream");

                    Assert.That(other?.Status,    Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,      Is.EqualTo("other"),  "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region TrailersInContinuationFramesAfterAReset_DecodedAndDiscarded()

        /// <summary>
        /// Trailers split into a HEADERS frame and CONTINUATION frames. The
        /// CONTINUATION frames are part of the discarded block: they ended the
        /// connection with PROTOCOL_ERROR, since the HEADERS frame had been
        /// answered with RST_STREAM and not taken as the start of a block.
        /// </summary>
        [Test]
        public async Task TrailersInContinuationFramesAfterAReset_DecodedAndDiscarded()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                fail.TrySetResult();

                await peer.ReadToResetAsync(1);

                await peer.SendHeadersAsync(1, Trailers, EndStream: true, FragmentSize: 8);

                var answers = await Answers(peer);

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(answers,          Is.True,            "the connection, once the trailers on the reset stream came in");
                    Assert.That(GoAways(peer),    Is.Empty,           "how the server ended the connection");

                    Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                  "what the server sent on the reset stream");

                    Assert.That(other?.Status,    Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,      Is.EqualTo("other"),  "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region TrailersThatMeetTheReset_DecodedAndDiscarded()

        /// <summary>
        /// The trailers pass the read loop's check of the stream's state while the
        /// stream is open, and are decoded as trailers; then, before the read loop
        /// ends the client's side, the server resets the stream on another task.
        /// The end of the client's side finds the stream reset, and the trailers
        /// are discarded, not answered with STREAM_CLOSED.
        /// </summary>
        [Test]
        public async Task TrailersThatMeetTheReset_DecodedAndDiscarded()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(1);

                await peer.ResetBeforeRemoteCloseAsync(1, () => peer.SendHeadersAsync(1, Trailers, EndStream: true));

                var answers = await Answers(peer);

                await peer.RequestAsync(3, "/other");

                var other = await ResponseOn(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(stream.Trailers,  Is.EqualTo(Trailers),  "the trailers, decoded as on an open stream: the reset came after the read loop's check");

                    Assert.That(answers,          Is.True,               "the connection, once the trailers that met the reset were handled");
                    Assert.That(GoAways(peer),    Is.Empty,              "how the server ended the connection");

                    // The reset was made by the test, which sends no RST_STREAM.
                    Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "HEADERS :status 200" }),
                                                  "what the server sent on the reset stream");

                    Assert.That(other?.Status,    Is.EqualTo("200"),     "status of the next response");
                    Assert.That(other?.Body,      Is.EqualTo("other"),   "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region FramesOnAResetStreamPrunedSince_Discarded()

        /// <summary>
        /// Once a later stream opens, the server prunes the closed streams, and the
        /// reset one with them. The client's DATA and trailers for it, sent before
        /// it read the reset, are still discarded. The DATA drew STREAM_CLOSED, and
        /// the trailers, taken for a new stream with too low an ID, ended the
        /// connection with PROTOCOL_ERROR.
        /// </summary>
        [Test]
        public async Task FramesOnAResetStreamPrunedSince_Discarded()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                fail.TrySetResult();

                await peer.ReadToResetAsync(1);

                await peer.RequestAsync(3, "/other");

                var third = await ResponseOn(peer, 3);

                Assert.That(() => peer.ServerStream(1), Throws.InvalidOperationException, "stream 1, pruned once stream 3 opened");

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendHeadersAsync(1, Trailers, EndStream: true);

                var answers = await Answers(peer);

                await peer.RequestAsync(5, "/other");

                var fifth = await ResponseOn(peer, 5);

                Assert.Multiple(() =>
                {

                    Assert.That(third?.Body,                      Is.EqualTo("other"),  "body of the response that pruned the reset stream");

                    Assert.That(answers,                          Is.True,              "the connection, once the frames on the pruned stream came in");
                    Assert.That(GoAways(peer),                    Is.Empty,             "how the server ended the connection");

                    Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                     "RST_STREAM INTERNAL_ERROR" }),
                                                                  "what the server sent on the reset stream");

                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),        "connection window held back for the discarded DATA");

                    Assert.That(fifth?.Status,                    Is.EqualTo("200"),    "status of the next response");
                    Assert.That(fifth?.Body,                      Is.EqualTo("other"),  "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion


        #region DataAfterTheClientsReset_StillAnsweredWithStreamClosed()

        /// <summary>
        /// Unchanged: after the client's own RST_STREAM, it has nothing more to
        /// send on the stream, and each frame it sends there all the same is a
        /// stream error of type STREAM_CLOSED. Its DATA still gives its window
        /// back to the connection.
        /// </summary>
        [Test]
        public async Task DataAfterTheClientsReset_StillAnsweredWithStreamClosed()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(Other);

            await peer.RequestAsync(1, "/upload", EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));

            var answers = await Answers(peer);

            Assert.Multiple(() =>
            {

                Assert.That(answers,                          Is.True,  "the connection, once the client's DATA on the stream it reset came in");

                Assert.That(SentOn(peer, 1),                  Is.EqualTo(new[] { "RST_STREAM STREAM_CLOSED",
                                                                                 "RST_STREAM STREAM_CLOSED" }),
                                                              "what the server sent on the stream the client reset");

                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),  "connection window held back for the DATA");

            });

        }

        #endregion

        #region TrailersAfterTheClientsReset_DecodedAndAnsweredWithStreamClosed()

        /// <summary>
        /// Trailers after the client's own RST_STREAM are still answered with
        /// STREAM_CLOSED, but they are decoded first, as every header block must
        /// be (RFC 9113, Section 4.3): undecoded, they put the server's HPACK
        /// table out of step with the client's, and the next request failed with
        /// COMPRESSION_ERROR, and the connection with it.
        /// </summary>
        [Test]
        public async Task TrailersAfterTheClientsReset_DecodedAndAnsweredWithStreamClosed()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(Other);

            await peer.RequestAsync(1, "/upload", EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            await peer.SendHeadersAsync(1, Trailers, EndStream: true);

            var answers = await Answers(peer);

            await peer.RequestAsync(3, "/other");

            var other = await ResponseOn(peer, 3);

            Assert.Multiple(() =>
            {

                Assert.That(answers,          Is.True,            "the connection, once the trailers on the stream the client reset came in");
                Assert.That(GoAways(peer),    Is.Empty,           "how the server ended the connection");

                Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "RST_STREAM STREAM_CLOSED" }),
                                              "what the server sent on the stream the client reset");

                Assert.That(other?.Status,    Is.EqualTo("200"),    "status of the next response");
                Assert.That(other?.Body,      Is.EqualTo("other"),  "body of the next response");

            });

        }

        #endregion

        #region DataAfterAResetOfAnEndedUpload_StillAnsweredWithStreamClosed()

        /// <summary>
        /// Unchanged: the server resets a stream whose client had already ended
        /// its side with END_STREAM. The client had nothing left to send there,
        /// so DATA it sends all the same is a stream error of type STREAM_CLOSED,
        /// as on a stream closed by END_STREAM both ways.
        /// </summary>
        [Test]
        public async Task DataAfterAResetOfAnEndedUpload_StillAnsweredWithStreamClosed()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(Other, FailingUpload(headersSent, fail));

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: true);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                // The read loop handles one frame at a time: once the ping is
                // answered, it has ended the client's side of stream 1.
                await peer.PingAsync();

                fail.TrySetResult();

                await peer.ReadToResetAsync(1);

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));

                var answers = await Answers(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(answers,          Is.True,  "the connection, once the client's DATA after its END_STREAM came in");

                    Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                     "RST_STREAM INTERNAL_ERROR",
                                                                     "RST_STREAM STREAM_CLOSED" }),
                                                  "what the server sent on the reset stream");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

    }

}
