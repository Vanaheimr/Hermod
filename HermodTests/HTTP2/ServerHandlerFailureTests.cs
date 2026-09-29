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
using System.Reflection;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// How the server answers a handler that fails: a buffered request handler,
    /// a streaming handler, a connect handler and the tunnel it runs.
    ///
    /// A handler that threw an OperationCanceledException of its own — a
    /// timeout of its own, say — was taken to be cancelled with its stream, and
    /// only reported: no response and no RST_STREAM went out, and the stream
    /// stayed open, counted against MAX_CONCURRENT_STREAMS, until the connection
    /// ended. Only the stream's reset and the connection's end are cancellations
    /// now; any other failure is answered, with a 500 before a response has
    /// begun, else with RST_STREAM INTERNAL_ERROR, and a tunnel always so.
    ///
    /// A streaming handler that failed once it had completed its response was
    /// answered with RST_STREAM INTERNAL_ERROR: on a stream both sides had ended,
    /// where RFC 9113, Section 5.1 allows nothing but PRIORITY, and on one the
    /// client still sent on, where a client may discard a complete response for
    /// any code but NO_ERROR (Section 8.1). A complete response now stands: the
    /// failure is reported, and a client still sending is asked to stop with
    /// RST_STREAM NO_ERROR, as after a handler that returns.
    ///
    /// A tunnel whose handler failed was ended with END_STREAM, as if it had
    /// ended cleanly, and then reset — on a closed stream, once the client had
    /// ended its side as well. It is reset now with no END_STREAM before it: an
    /// error in a tunnel is an RST_STREAM (Section 8.5). And one whose handler
    /// failed after the stream's reset was reset a second time, in answer to the
    /// client's RST_STREAM (Section 5.4.2).
    /// </summary>
    [TestFixture]
    public class ServerHandlerFailureTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The client's receive windows, RFC 9113's default: the client here never
        /// changes them.
        /// </summary>
        private const Int32 ClientWindow = 65_535;

        /// <summary>
        /// Refuses every request that is not streamed, or no CONNECT.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> NotStreamed(UInt32                             StreamId,
                                                                                                                  List<(String Name, String Value)>  Headers,
                                                                                                                  Byte[]?                            Body,
                                                                                                                  CancellationToken                  CancellationToken)

            => throw new InvalidOperationException("Every request is streamed here, or tunnels");

        /// <summary>
        /// What the server sent on this stream, in order.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        /// <summary>
        /// The response on this stream: read on until the server has ended its
        /// side, unless an earlier read, a ping's say, has read that far already
        /// — or null, if the server did not end it in time, or reset the stream
        /// first.
        /// </summary>
        private static async Task<PipedH2ServerConnection.Response?> ResponseOn(PipedH2ServerConnection Peer, UInt32 StreamId)
        {

            var frames = Peer.FramesOn(StreamId);

            if (!frames.Any(frame => frame.Frame.Type is HTTP2FrameType.DATA or HTTP2FrameType.HEADERS && frame.Frame.EndStream))
                return await Peer.TryResponseAsync(StreamId, PipedH2ServerConnection.StepTimeout);

            var blocks = frames.Where(frame => frame.Headers is not null).ToList();

            return new PipedH2ServerConnection.Response(blocks[0].Headers!,
                                                        String.Concat(frames.Where (frame => frame.Frame.Type == HTTP2FrameType.DATA).
                                                                             Select(frame => Encoding.ASCII.GetString(frame.Frame.Payload))),
                                                        blocks.Count > 1 ? blocks[^1].Headers : null);

        }

        /// <summary>
        /// The error code of the server's RST_STREAM on this stream, read on until
        /// it comes — or null, if none came within the step timeout.
        /// </summary>
        private static async Task<HTTP2ErrorCode?> ResetOn(PipedH2ServerConnection Peer, UInt32 StreamId)
        {

            var read = Peer.FramesOn(StreamId).FirstOrDefault(frame => frame.Frame.Type == HTTP2FrameType.RST_STREAM);

            if (read is not null)
                return (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(read.Frame.Payload);

            try
            {
                return await Peer.ReadToResetAsync(StreamId);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

        }

        /// <summary>
        /// Wait until the condition holds, or fail once the step timeout is over.
        /// </summary>
        private static async Task UntilAsync(Func<Boolean> Condition, String What)
        {

            var waited = Stopwatch.StartNew();

            while (!Condition())
            {

                if (waited.Elapsed > PipedH2ServerConnection.StepTimeout)
                    throw new TimeoutException($"Timed out waiting until {What}");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// Ping until the condition holds, or fail once the step timeout is over:
        /// for frames the server sends from a task of its own, which a single ping
        /// may not wait for.
        /// </summary>
        private static async Task PingUntilAsync(PipedH2ServerConnection Peer, Func<Boolean> Condition, String What)
        {

            var waited = Stopwatch.StartNew();

            while (!Condition())
            {

                if (waited.Elapsed > PipedH2ServerConnection.StepTimeout)
                    throw new TimeoutException($"Timed out waiting until {What}");

                await Peer.PingAsync();

            }

        }

        /// <summary>
        /// Wait until the task of the stream's handler has ended the reading of
        /// the stream, and then ping, so that everything that task sent before is
        /// read. The end of the reading gives back the window of what is unread,
        /// and marks the stream (<see cref="HTTP2Stream.UnreadWindowReturned"/>).
        /// The task does that last, in a finally, once a failure is answered — or
        /// right after the RST_STREAM, when it answers the failure with a reset.
        /// </summary>
        private static async Task ReadingEndedAsync(PipedH2ServerConnection Peer, HTTP2Stream Stream)
        {

            await UntilAsync(() => Stream.UnreadWindowReturned, $"the server has ended the reading of stream {Stream.StreamId}");

            await Peer.PingAsync();

        }

        /// <summary>
        /// The monitor a stream's state transitions, and its reset, are made under.
        /// </summary>
        private static Object StateLockOf(HTTP2Stream Stream)

            => typeof(HTTP2Stream).GetField("stateLock", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                   GetValue(Stream)!;

        /// <summary>
        /// Events of the stack's EventSource while this listener lives.
        /// </summary>
        private sealed class HTTP2Events : EventListener
        {

            // Initialised with the declaration: the base constructor may already
            // call OnEventSourceCreated, and events may follow at once.
            private readonly Channel<(String Name, List<Object?> Payload)> events = Channel.CreateUnbounded<(String, List<Object?>)>();

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName != "EventCounters")
                    events.Writer.TryWrite((Event.EventName ?? "?", [.. Event.Payload ?? []]));
            }

            /// <summary>
            /// How the server reported the handler of this stream: the first
            /// HandlerCancelled or HandlerFailed event for it, with the kind of
            /// handler, or null if none came within the step timeout.
            /// </summary>
            public async Task<String?> OutcomeAsync(UInt32 StreamId)
            {

                using var timeout = new CancellationTokenSource(PipedH2ServerConnection.StepTimeout);

                try
                {
                    while (true)
                    {

                        var (name, payload) = await events.Reader.ReadAsync(timeout.Token);

                        if (name is "HandlerCancelled" or "HandlerFailed" && Equals(payload[0], (Int32) StreamId))
                            return $"{name} {payload[1]}";

                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    return null;
                }

            }

        }

        #endregion


        #region Buffered_OwnCancellation_Answered500()

        /// <summary>
        /// A buffered request handler throws an OperationCanceledException of its
        /// own. It is no cancellation of the stream's, and is answered with a 500,
        /// as any other failure of a buffered handler is. It used to be taken for a
        /// cancellation and only reported: nothing was sent, and the stream stayed
        /// half-closed until the connection ended.
        /// </summary>
        [Test]
        public async Task Buffered_OwnCancellation_Answered500()
        {

            // An ID no other test uses: the handler events name nothing but the stream.
            const UInt32 StreamId = 6101;

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    await Task.Yield();

                    throw new OperationCanceledException("A timeout of the handler's own");

                });

            await peer.RequestAsync(StreamId, "/own-timeout");

            // Nothing followed the report before: fail at once, rather than wait
            // for a response that does not come.
            var outcome = await events.OutcomeAsync(StreamId);

            Assert.That(outcome, Is.EqualTo("HandlerFailed request"), "how the server reported the handler");

            var response = await ResponseOn(peer, StreamId);

            Assert.Multiple(() =>
            {

                Assert.That(response?.Status,       Is.EqualTo("500"),                    "status of the response");
                Assert.That(response?.Body,         Is.EqualTo("Internal Server Error"),  "body of the response");

                Assert.That(SentOn(peer, StreamId),
                            Is.EqualTo(new[] { "HEADERS :status 500, content-type text/plain",
                                               "DATA \"Internal Server Error\" END_STREAM" }),
                            "what the server sent on the stream");

            });

        }

        #endregion


        #region OwnCancellation_BeforeTheHeaders_Answered500(OwnToken)

        /// <summary>
        /// A streaming handler throws an OperationCanceledException of its own
        /// before it has written anything: one it makes itself, or one a token of
        /// its own throws, a timeout's, say. It is neither the stream's reset nor
        /// the connection's end, and it is answered as any other failure is, with
        /// a 500. It used to be taken for a cancellation and only reported: nothing
        /// was sent, and the stream stayed half-closed until the connection ended.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task OwnCancellation_BeforeTheHeaders_Answered500(Boolean OwnToken)
        {

            const UInt32 StreamId = 6105;

            using var events  = new HTTP2Events();
            using var own     = new CancellationTokenSource();

            own.Cancel();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (OwnToken)
                        await Task.Delay(Timeout.Infinite, own.Token);

                    await Task.Yield();

                    throw new OperationCanceledException("A timeout of the handler's own");

                });

            await peer.RequestAsync(StreamId, "/own-timeout");
            await peer.PingAsync();

            var stream = peer.ServerStream(StreamId);

            await ReadingEndedAsync(peer, stream);

            // The 500's HEADERS go out on the handler's task, before the reading
            // ends: without them, fail at once, rather than wait for the rest.
            Assert.That(SentOn(peer, StreamId), Is.Not.Empty, "what the server sent on the stream: nothing, and the client waits for good");

            var response  = await ResponseOn(peer, StreamId);
            var outcome   = await events.OutcomeAsync(StreamId);

            Assert.Multiple(() =>
            {

                Assert.That(response?.Status,       Is.EqualTo("500"),                     "status of the response");
                Assert.That(response?.Body,         Is.EqualTo("Internal Server Error"),   "body of the response");

                Assert.That(SentOn(peer, StreamId),
                            Is.EqualTo(new[] { "HEADERS :status 500, content-type text/plain",
                                               "DATA \"Internal Server Error\"",
                                               "DATA \"\" END_STREAM" }),
                            "what the server sent on the stream");

                Assert.That(stream.State,           Is.EqualTo(HTTP2StreamState.Closed),   "state of the stream");
                Assert.That(stream.WasReset,        Is.False,                              "stream reset");

                Assert.That(outcome,                Is.EqualTo("HandlerFailed streaming"), "how the server reported the handler");

            });

        }

        #endregion

        #region OwnCancellation_AfterTheHeaders_StreamReset()

        /// <summary>
        /// A streaming handler has written its response headers when it throws an
        /// OperationCanceledException of its own. A response begun cannot be
        /// turned into an error status: the stream is reset with INTERNAL_ERROR,
        /// as for any other failure there. It used to be left open, a response
        /// begun and never ended.
        /// </summary>
        [Test]
        public async Task OwnCancellation_AfterTheHeaders_StreamReset()
        {

            const UInt32 StreamId = 6109;

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    throw new OperationCanceledException("A timeout of the handler's own");

                });

            await peer.RequestAsync(StreamId, "/own-timeout");
            await peer.PingAsync();

            var stream = peer.ServerStream(StreamId);

            await ReadingEndedAsync(peer, stream);

            var outcome = await events.OutcomeAsync(StreamId);

            Assert.Multiple(() =>
            {

                Assert.That(SentOn(peer, StreamId),
                            Is.EqualTo(new[] { "HEADERS :status 200",
                                               "RST_STREAM INTERNAL_ERROR" }),
                            "what the server sent on the stream");

                Assert.That(stream.WasReset,  Is.True,                              "stream reset, and so no longer counted as open");
                Assert.That(outcome,          Is.EqualTo("HandlerFailed streaming"), "how the server reported the handler");

            });

        }

        #endregion

        #region StreamReset_Cancellation_NotAnswered()

        /// <summary>
        /// The client resets the stream while a streaming handler waits on its
        /// token. The handler's OperationCanceledException is the stream's reset:
        /// it is reported as a cancellation, and not answered, as it was before —
        /// nobody reads an answer on a reset stream.
        /// </summary>
        [Test]
        public async Task StreamReset_Cancellation_NotAnswered()
        {

            const UInt32 StreamId = 6113;

            var waiting = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    waiting.TrySetResult();

                    await Task.Delay(Timeout.Infinite, cancellationToken);

                });

            await peer.RequestAsync(StreamId, "/wait");
            await waiting.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));

            var outcome = await events.OutcomeAsync(StreamId);

            await peer.PingAsync();

            Assert.Multiple(() =>
            {
                Assert.That(outcome,                 Is.EqualTo("HandlerCancelled streaming"), "how the server reported the handler");
                Assert.That(SentOn(peer, StreamId),  Is.Empty,                                 "what the server sent on the reset stream");
            });

        }

        #endregion

        #region ConnectionEnd_Cancellation_NotReportedAsAFailure()

        /// <summary>
        /// The connection ends while a streaming handler waits for a write to go
        /// out, for more than the client's window. The write fails with the
        /// connection's token, as soon as the connection is cancelled, before its
        /// teardown resets the stream. That is the connection's end: the handler
        /// is reported as cancelled, as it was before, and not answered as a
        /// failure. The stream's state lock is held meanwhile, so that the
        /// teardown cannot reset the stream before the handler's failure has been
        /// looked at.
        /// </summary>
        [Test]
        public async Task ConnectionEnd_Cancellation_NotReportedAsAFailure()
        {

            const UInt32 StreamId = 6117;

            var writing = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    var write = response.WriteAsync(new Byte[ClientWindow + 1]);

                    writing.TrySetResult();

                    await write;

                });

            await peer.RequestAsync(StreamId, "/download");
            await writing.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            var stream = peer.ServerStream(StreamId);

            String? outcome;

            using (var holder = MonitorHolder.Hold(StateLockOf(stream)))
            {

                await peer.DisconnectAsync();

                outcome = await events.OutcomeAsync(StreamId);

                holder.LetGo();

            }

            Assert.That(outcome, Is.EqualTo("HandlerCancelled streaming"), "how the server reported the handler");

        }

        #endregion

        #region CompleteResponse_ThenFailure_StreamClosed_NothingMoreSent(AfterTheClose)

        /// <summary>
        /// A streaming handler completes its response to a request the client has
        /// ended, and then fails: right away, or once both sides have ended the
        /// stream and it is closed. The response stands, and the failure is
        /// reported, not answered. It used to be answered with RST_STREAM
        /// INTERNAL_ERROR, and on the closed stream RFC 9113, Section 5.1 allows
        /// nothing but PRIORITY.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task CompleteResponse_ThenFailure_StreamClosed_NothingMoreSent(Boolean AfterTheClose)
        {

            const UInt32 StreamId = 6121;

            var fail = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.CompleteAsync();

                    if (AfterTheClose)
                        await fail.Task;

                    throw new InvalidOperationException("The handler failed on purpose, after its response");

                });

            try
            {

                await peer.RequestAsync(StreamId, "/complete");
                await peer.PingAsync();

                var stream    = peer.ServerStream(StreamId);
                var response  = await ResponseOn(peer, StreamId);

                if (AfterTheClose)
                {
                    await UntilAsync(() => stream.State == HTTP2StreamState.Closed, "both sides have ended the stream");
                    fail.TrySetResult();
                }

                await ReadingEndedAsync(peer, stream);

                var outcome = await events.OutcomeAsync(StreamId);

                Assert.Multiple(() =>
                {

                    Assert.That(response?.Status,       Is.EqualTo("200"),                     "status of the response");

                    Assert.That(SentOn(peer, StreamId),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA \"\" END_STREAM" }),
                                "what the server sent on the stream");

                    Assert.That(stream.State,           Is.EqualTo(HTTP2StreamState.Closed),   "state of the stream");
                    Assert.That(stream.WasReset,        Is.False,                              "stream reset");
                    Assert.That(outcome,                Is.EqualTo("HandlerFailed streaming"), "how the server reported the handler");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region CompleteResponse_ThenFailure_ClientStillSending_UploadStoppedWithNoError(AfterTheHalfClose)

        /// <summary>
        /// A streaming handler completes its response while the client's side of
        /// the stream is still open, and then fails: right away, or once its
        /// END_STREAM is out and the stream half-closed (local). The response
        /// stands: the server asks the client to stop sending with RST_STREAM
        /// NO_ERROR, as after a handler that returns, and the client keeps the
        /// response (RFC 9113, Section 8.1). It used to be RST_STREAM
        /// INTERNAL_ERROR, on which a client may discard a complete response.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task CompleteResponse_ThenFailure_ClientStillSending_UploadStoppedWithNoError(Boolean AfterTheHalfClose)
        {

            const UInt32 StreamId = 6125;

            var fail = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.CompleteAsync();

                    if (AfterTheHalfClose)
                        await fail.Task;

                    throw new InvalidOperationException("The handler failed on purpose, after its response");

                });

            try
            {

                await peer.RequestAsync(StreamId, "/complete", EndStream: false);
                await peer.PingAsync();

                var stream    = peer.ServerStream(StreamId);
                var response  = await ResponseOn(peer, StreamId);

                if (AfterTheHalfClose)
                {
                    await UntilAsync(() => stream.State == HTTP2StreamState.HalfClosedLocal, "the server has ended its side of the stream");
                    fail.TrySetResult();
                }

                var reset = await ResetOn(peer, StreamId);

                await peer.PingAsync();

                var outcome = await events.OutcomeAsync(StreamId);

                Assert.Multiple(() =>
                {

                    Assert.That(response?.Status,       Is.EqualTo("200"),                     "status of the response");
                    Assert.That(reset,                  Is.EqualTo(HTTP2ErrorCode.NO_ERROR),   "how the server stopped the upload");

                    Assert.That(SentOn(peer, StreamId),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA \"\" END_STREAM",
                                                   "RST_STREAM NO_ERROR" }),
                                "what the server sent on the stream");

                    Assert.That(outcome,                Is.EqualTo("HandlerFailed streaming"), "how the server reported the handler");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region InvalidTrailersCaught_ResponseNotComplete(ThenFails)

        /// <summary>
        /// A streaming handler's CompleteAsync fails on a trailer field that may
        /// not be sent, and the handler catches that. Nothing has ended its
        /// response, so the response is not complete: a handler that fails then
        /// has the stream reset with INTERNAL_ERROR, as for any failure after the
        /// headers, and one that returns has its response ended for it, as any
        /// response the handler did not end. The response used to count as
        /// complete once CompleteAsync was called, and was never ended: the
        /// handler that returned left the stream open for good, and one that
        /// failed would have too, once a complete response stood.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task InvalidTrailersCaught_ResponseNotComplete(Boolean ThenFails)
        {

            const UInt32 StreamId = 6129;

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    try
                    {
                        await response.CompleteAsync([("X-Checksum", "abc")]);
                    }
                    catch (HTTP2StreamException)
                    {
                        // Field names are lowercase in HTTP/2 (RFC 9113, Section 8.2.1).
                    }

                    if (ThenFails)
                        throw new InvalidOperationException("The handler failed on purpose, after its trailers were refused");

                });

            await peer.RequestAsync(StreamId, "/trailers");
            await peer.PingAsync();

            var stream = peer.ServerStream(StreamId);

            if (ThenFails)
            {

                await ReadingEndedAsync(peer, stream);

                var outcome = await events.OutcomeAsync(StreamId);

                Assert.Multiple(() =>
                {

                    Assert.That(SentOn(peer, StreamId),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the stream");

                    Assert.That(stream.WasReset,  Is.True,                              "stream reset, and so no longer counted as open");
                    Assert.That(outcome,          Is.EqualTo("HandlerFailed streaming"), "how the server reported the handler");

                });

            }

            else
            {

                var response = await ResponseOn(peer, StreamId);

                Assert.Multiple(() =>
                {

                    Assert.That(response?.Status,  Is.EqualTo("200"),  "status of the response, ended for the handler");
                    Assert.That(response?.Trailers, Is.Null,           "trailers of the response");

                    Assert.That(SentOn(peer, StreamId),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA \"\" END_STREAM" }),
                                "what the server sent on the stream");

                });

            }

        }

        #endregion


        #region Connect_OwnCancellation_StreamReset()

        /// <summary>
        /// A connect handler throws an OperationCanceledException of its own while
        /// it decides on the tunnel. It is answered as any other failure of a
        /// connect handler is: the stream is reset with INTERNAL_ERROR. It used to
        /// be taken for a cancellation and only reported: the client got no answer
        /// to its CONNECT, and the stream stayed open until the connection ended.
        /// </summary>
        [Test]
        public async Task Connect_OwnCancellation_StreamReset()
        {

            const UInt32 StreamId = 6133;

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                ConnectHandler: async (streamId, headers, cancellationToken) => {

                    await Task.Yield();

                    throw new OperationCanceledException("A timeout of the connect handler's own");

                });

            await peer.RequestTunnelAsync(StreamId, "tunnel.example:443");
            await peer.PingAsync();

            var stream = peer.ServerStream(StreamId);

            await ReadingEndedAsync(peer, stream);

            var outcome = await events.OutcomeAsync(StreamId);

            Assert.Multiple(() =>
            {

                Assert.That(SentOn(peer, StreamId),  Is.EqualTo(new[] { "RST_STREAM INTERNAL_ERROR" }), "what the server sent on the stream");

                Assert.That(stream.WasReset,         Is.True,                                           "stream reset, and so no longer counted as open");
                Assert.That(outcome,                 Is.EqualTo("HandlerFailed CONNECT"),               "how the server reported the handler");

            });

        }

        #endregion

        #region Tunnel_Fails_ResetWithoutEndStream(ClientEnded, OwnCancellation)

        /// <summary>
        /// The handler of an accepted tunnel fails: once the client has ended its
        /// side, or while it may still send; with an exception, or with an
        /// OperationCanceledException of its own. The stream is reset with
        /// INTERNAL_ERROR, with no END_STREAM before it: an error in a tunnel is an
        /// RST_STREAM (RFC 9113, Section 8.5). The server used to end its side
        /// first, as for a tunnel that ended cleanly, and then reset the stream —
        /// once the client had ended its side as well, on a closed stream (Section
        /// 5.1). A cancellation of its own it took for the stream's, and ended the
        /// tunnel as if the handler had returned, with RST_STREAM NO_ERROR after.
        /// </summary>
        [TestCase(true,  false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public async Task Tunnel_Fails_ResetWithoutEndStream(Boolean ClientEnded, Boolean OwnCancellation)
        {

            const UInt32 StreamId = 6137;

            var fail = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    RunAsync    = async (tunnel, cancellationToken) => {

                        if (ClientEnded)
                            while (await tunnel.ReadAsync(cancellationToken) is not null)
                            { }
                        else
                            await fail.Task;

                        throw OwnCancellation
                                  ? new OperationCanceledException("A timeout of the tunnel's own")
                                  : new InvalidOperationException("The tunnel's handler failed on purpose");

                    }

                }));

            try
            {

                await peer.RequestTunnelAsync(StreamId, "tunnel.example:443");

                await PingUntilAsync(peer, () => peer.FramesOn(StreamId).Any(frame => frame.Headers is not null), "the server has accepted the tunnel");

                var stream = peer.ServerStream(StreamId);

                if (ClientEnded)
                    await peer.SendAsync(HTTP2Frame.CreateData(StreamId, [], EndStream: true));
                else
                    fail.TrySetResult();

                await ReadingEndedAsync(peer, stream);

                var outcome = await events.OutcomeAsync(StreamId);

                Assert.Multiple(() =>
                {

                    Assert.That(SentOn(peer, StreamId),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the tunnel");

                    Assert.That(stream.WasReset,  Is.True,                            "stream reset");
                    Assert.That(outcome,          Is.EqualTo("HandlerFailed CONNECT"), "how the server reported the handler");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region Tunnel_FailsAfterTheClientsReset_NotResetAgain()

        /// <summary>
        /// The client resets an accepted tunnel, and its handler, told so by its
        /// token, fails with an exception of its own. The failure is reported, but
        /// not answered: an RST_STREAM of the server's would answer the client's,
        /// which RFC 9113, Section 5.4.2 forbids. It used to go out, as a streaming
        /// handler's did before it learnt to leave a reset stream alone.
        /// </summary>
        [Test]
        public async Task Tunnel_FailsAfterTheClientsReset_NotResetAgain()
        {

            const UInt32 StreamId = 6141;

            var tunnelOpen = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(

                    streamId == StreamId

                        ? new HTTP2ConnectResult {

                              StatusCode  = 200,

                              RunAsync    = async (tunnel, cancellationToken) => {

                                  tunnelOpen.TrySetResult();

                                  try
                                  {
                                      await Task.Delay(Timeout.Infinite, cancellationToken);
                                  }
                                  catch (OperationCanceledException)
                                  {
                                      throw new InvalidOperationException("The tunnel's handler failed on purpose, once its stream was reset");
                                  }

                              }

                          }

                        : new HTTP2ConnectResult { StatusCode = 404 }));

            await peer.RequestTunnelAsync(StreamId, "tunnel.example:443");
            await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));

            var outcome = await events.OutcomeAsync(StreamId);

            // An answer would follow the report at once, on the handler's own
            // task: long before a whole later exchange has ended.
            await peer.RequestTunnelAsync(StreamId + 2, "tunnel.example:443");

            var refused = await ResponseOn(peer, StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(outcome,                 Is.EqualTo("HandlerFailed CONNECT"),          "how the server reported the handler");
                Assert.That(SentOn(peer, StreamId),  Is.EqualTo(new[] { "HEADERS :status 200" }),  "what the server sent on the tunnel");

                Assert.That(refused?.Status,         Is.EqualTo("404"),                            "status of the next CONNECT's answer");

            });

        }

        #endregion

    }

}
