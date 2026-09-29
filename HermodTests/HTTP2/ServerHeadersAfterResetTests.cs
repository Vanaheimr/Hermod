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
using System.Diagnostics.Tracing;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Header blocks on a stream that is reset. RFC 9113, Section 5.1: an
    /// endpoint must send nothing but PRIORITY on a closed stream. The server
    /// wrote a response's HEADERS without a look at the stream's state, so when
    /// a client cancelled a request with RST_STREAM CANCEL, a streaming handler
    /// that went on to write its headers put them on the wire, and so did one
    /// that simply returned, whose response the server ended with a 200 of its
    /// own. So did a buffered handler's answer, a 421 refusal, a CONNECT answer
    /// and a response's trailers.
    ///
    /// Such a header write now sends nothing, and fails as a body write on a
    /// reset stream does: with an OperationCanceledException that carries the
    /// stream's own token, the one its handler was given. Whether to send is
    /// decided under the connection's write lock, before the header list is
    /// HPACK-encoded: the encoder adds fields to its dynamic table as it encodes,
    /// and a block encoded but never sent would leave the client's decoder a
    /// step behind for every later header block on the connection. So the tests
    /// end with a next request on the same connection, whose response repeats
    /// a field of the block that was not sent: the encoder would send that field
    /// as an index into an entry the client does not have. (The 200 the server
    /// writes for a handler that returns carries no such field.)
    /// </summary>
    [TestFixture]
    public class ServerHeadersAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The field every header block written after a reset here carries, and
        /// the next response repeats. The encoder puts it into its dynamic table.
        /// </summary>
        private static readonly (String Name, String Value) Late = ("x-late", "after the reset");

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// The next response, streamed: it repeats <see cref="Late"/>.
        /// </summary>
        private static async Task AnswerOther(IHTTP2ResponseStream Response)
        {
            await Response.WriteHeadersAsync([(":status", "200"), Late]);
            await Response.WriteAsync(ASCII("other"));
        }

        /// <summary>
        /// The next response, buffered: it repeats <see cref="Late"/>.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Other(UInt32                             StreamId,
                                                                                                            List<(String Name, String Value)>  Headers,
                                                                                                            Byte[]?                            Body,
                                                                                                            CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200"), Late], ASCII("other")));

        /// <summary>
        /// How a write ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Write)
        {

            if (await Task.WhenAny(Write, Task.Delay(PipedH2ServerConnection.StepTimeout)) != Write)
                return (false, null);

            try
            {
                await Write;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        /// <summary>
        /// The token a cancellation carries, or null for any other outcome.
        /// </summary>
        private static CancellationToken? TokenOf(Exception? Failure)

            => (Failure as OperationCanceledException)?.CancellationToken;

        /// <summary>
        /// What the server sent on a stream, one line per frame.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        /// <summary>
        /// The GOAWAY frames the server sent.
        /// </summary>
        private static List<String> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => frame.ToString())];

        /// <summary>
        /// Wait until the server has reset the stream, as its read loop does for
        /// the client's RST_STREAM. A ping would tell as much, but it is not
        /// answered while writes are held.
        /// </summary>
        private static async Task WaitUntilResetAsync(HTTP2Stream Stream)
        {

            var waited = Stopwatch.StartNew();

            while (!Stream.WasReset)
            {

                if (waited.Elapsed > PipedH2ServerConnection.StepTimeout)
                    throw new TimeoutException($"Stream {Stream.StreamId} was never reset");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// Wait until the writer loop has taken what was queued on the stream, to
        /// write it once the write lock is its own.
        /// </summary>
        private static async Task WaitUntilTakenAsync(HTTP2Stream Stream)
        {

            var waited = Stopwatch.StartNew();

            while (Stream.OutboundQueue.HasPending)
            {

                if (waited.Elapsed > PipedH2ServerConnection.StepTimeout)
                    throw new TimeoutException($"What was queued on stream {Stream.StreamId} was never taken");

                await Task.Delay(1);

            }

        }

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
            /// The first event not looked at yet that matches, or null if none
            /// comes within the step timeout.
            /// </summary>
            public async Task<(String Name, List<Object?> Payload)?> FirstAsync(Func<(String Name, List<Object?> Payload), Boolean> Matches)
            {

                using var timeout = new CancellationTokenSource(PipedH2ServerConnection.StepTimeout);

                try
                {
                    while (true)
                    {
                        var e = await events.Reader.ReadAsync(timeout.Token);
                        if (Matches(e))
                            return e;
                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    return null;
                }

            }

            /// <summary>
            /// How the server reported the handler of this stream: the first
            /// HandlerCancelled, HandlerFailed or RequestHandled event for it,
            /// with its kind or status, or null if none came.
            /// </summary>
            public async Task<String?> OutcomeAsync(UInt32 StreamId)
            {

                var outcome = await FirstAsync(e => (e.Name is "HandlerCancelled" or "HandlerFailed" or "RequestHandled") &&
                                                    Equals(e.Payload[0], (Int32) StreamId));

                return outcome is { } reported
                           ? $"{reported.Name} {(reported.Name == "RequestHandled" ? reported.Payload[3] : reported.Payload[1])}"
                           : null;

            }

        }

        #endregion


        #region HeadersAfterReset_FailAtOnce_NothingSent(Interim)

        /// <summary>
        /// The client cancels a request with RST_STREAM CANCEL, and the streaming
        /// handler goes on to write its response headers, or an interim 103,
        /// without a look at its token. The write fails at once, with the stream's
        /// token; nothing goes out on the reset stream; the handler is reported as
        /// cancelled; and the connection serves the next request, whose header
        /// block decodes.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task HeadersAfterReset_FailAtOnce_NothingSent(Boolean Interim)
        {

            // IDs no other test uses: the handler events name nothing but the stream.
            var streamId        = Interim ? 5005u : 5001u;

            var requestArrived  = new TaskCompletionSource(Async);
            var writeHeaders    = new TaskCompletionSource(Async);
            var headersWritten  = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (id, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value != "/late")
                    {
                        await AnswerOther(response);
                        return;
                    }

                    requestArrived.TrySetResult();
                    await writeHeaders.Task;

                    var write = Interim
                                    ? response.WriteInterimResponseAsync(103, [Late])
                                    : response.WriteHeadersAsync([(":status", "200"), Late]);

                    headersWritten.TrySetResult((write, cancellationToken));

                    await write;

                });

            try
            {

                await peer.RequestAsync(streamId, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(streamId, HTTP2ErrorCode.CANCEL));

                // The read loop handles one frame at a time: once the ping is
                // answered, the stream is reset.
                await peer.PingAsync();

                writeHeaders.TrySetResult();

                var (write, handlerToken)  = await headersWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                var (ended, failure)       = await EndOf(write);

                // Only the end of the test ends the connection.
                Assert.That(ended, Is.True, "the header write on the reset stream returned while the connection was open");

                var outcome = await events.OutcomeAsync(streamId);

                await peer.RequestAsync(streamId + 2, "/other");

                var other = await peer.TryResponseAsync(streamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(failure,                  Is.InstanceOf<OperationCanceledException>(), "how the header write on the reset stream ended");
                    Assert.That(TokenOf(failure),         Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled streaming"),   "how the server reported the handler");

                    Assert.That(SentOn(peer, streamId),   Is.Empty,                                   "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                          "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                         "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                        "body of the next response");

                });

            }
            finally
            {
                writeHeaders.TrySetResult();
            }

        }

        #endregion

        #region HeadersWaitingForTheWriteLock_ResetMeanwhile_NotSent()

        /// <summary>
        /// The handler's header write waits for the connection's write lock —
        /// another frame is being written — while the read loop handles the
        /// client's RST_STREAM. Once the write gets the lock, it finds the stream
        /// reset: it sends nothing, and fails with the stream's token. A check
        /// made only before the wait would have let these headers through.
        /// </summary>
        [Test]
        public async Task HeadersWaitingForTheWriteLock_ResetMeanwhile_NotSent()
        {

            const UInt32 StreamId = 5009;

            var requestArrived  = new TaskCompletionSource(Async);
            var writeHeaders    = new TaskCompletionSource(Async);
            var headersWritten  = new TaskCompletionSource<(Task Write, Boolean DoneAtOnce, CancellationToken HandlerToken)>(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value != "/late")
                    {
                        await AnswerOther(response);
                        return;
                    }

                    requestArrived.TrySetResult();
                    await writeHeaders.Task;

                    var write = response.WriteHeadersAsync([(":status", "200"), Late]);

                    headersWritten.TrySetResult((write, write.IsCompleted, cancellationToken));

                    await write;

                });

            try
            {

                await peer.RequestAsync(StreamId, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(StreamId);

                Task               write;
                Boolean            doneAtOnce;
                CancellationToken  handlerToken;

                using (await peer.HoldWritesAsync())
                {

                    writeHeaders.TrySetResult();

                    (write, doneAtOnce, handlerToken) = await headersWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                    await WaitUntilResetAsync(stream);

                }

                var (ended, failure) = await EndOf(write);

                Assert.That(ended, Is.True, "the header write, once it got the lock, returned while the connection was open");

                var outcome = await events.OutcomeAsync(StreamId);

                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(doneAtOnce,               Is.False,                                    "the header write, while another frame held the write lock");
                    Assert.That(failure,                  Is.InstanceOf<OperationCanceledException>(), "how the header write ended");
                    Assert.That(TokenOf(failure),         Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled streaming"),   "how the server reported the handler");

                    Assert.That(SentOn(peer, StreamId),   Is.Empty,                                   "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                          "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                         "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                        "body of the next response");

                });

            }
            finally
            {
                writeHeaders.TrySetResult();
            }

        }

        #endregion

        #region HandlerReturnsAfterReset_NothingSent_ReportedCancelled()

        /// <summary>
        /// A streaming handler that returns after its stream was reset, without
        /// having written anything. The server ends a response the handler left
        /// open itself, and for one without headers it wrote a 200 of its own:
        /// on the reset stream. Now nothing goes out there, and the handler is
        /// reported as cancelled.
        /// </summary>
        [Test]
        public async Task HandlerReturnsAfterReset_NothingSent_ReportedCancelled()
        {

            const UInt32 StreamId = 5013;

            var requestArrived  = new TaskCompletionSource(Async);
            var returnNow       = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value != "/late")
                    {
                        await AnswerOther(response);
                        return;
                    }

                    requestArrived.TrySetResult();
                    await returnNow.Task;

                });

            try
            {

                await peer.RequestAsync(StreamId, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                returnNow.TrySetResult();

                var outcome = await events.OutcomeAsync(StreamId);

                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled streaming"),  "how the server reported the handler");
                    Assert.That(SentOn(peer, StreamId),   Is.Empty,                                  "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                         "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                        "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                       "body of the next response");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

        }

        #endregion

        #region BufferedAnswerAfterReset_NothingSent_ReportedCancelled(WithBody)

        /// <summary>
        /// A buffered handler that answers after its stream was reset. Its
        /// response's HEADERS went out on the reset stream, and, when it had no
        /// body, even ended it there, and the request was logged as handled. Now
        /// nothing goes out, and the handler is reported as cancelled, body or not.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task BufferedAnswerAfterReset_NothingSent_ReportedCancelled(Boolean WithBody)
        {

            var streamId        = WithBody ? 5017u : 5021u;

            var requestArrived  = new TaskCompletionSource(Async);
            var answer          = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (id, headers, body, cancellationToken) => {

                    if (id != streamId)
                        return await Other(id, headers, body, cancellationToken);

                    requestArrived.TrySetResult();
                    await answer.Task;

                    return ([(":status", "200"), Late], WithBody ? ASCII("late") : null);

                });

            try
            {

                await peer.RequestAsync(streamId, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(streamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                answer.TrySetResult();

                var outcome = await events.OutcomeAsync(streamId);

                await peer.RequestAsync(streamId + 2, "/other");

                var other = await peer.TryResponseAsync(streamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled request"),  "how the server reported the request");
                    Assert.That(SentOn(peer, streamId),   Is.Empty,                                "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                       "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                      "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                     "body of the next response");

                });

            }
            finally
            {
                answer.TrySetResult();
            }

        }

        #endregion

        #region MisdirectedRefusalAfterReset_NothingSent()

        /// <summary>
        /// The server decides to refuse a request with 421, for an origin it does
        /// not serve, and the client resets the stream before the refusal goes
        /// out. The refusal's HEADERS went out on the reset stream; now nothing
        /// does, and the request is reported as cancelled.
        /// </summary>
        [Test]
        public async Task MisdirectedRefusalAfterReset_NothingSent()
        {

            const UInt32 StreamId = 5025;

            var askedFirst      = 0;
            var originAsked     = new TaskCompletionSource(Async);

            using var refuse = new ManualResetEventSlim();
            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                // It repeats both fields of the refusal: its status and content type.
                (streamId, headers, body, cancellationToken) => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(
                                                                    ([(":status", "200"), ("content-type", "text/plain"), Late], ASCII("other"))),

                // Asked on the request's own task, just before the refusal: the
                // first request is refused, once the test lets it.
                IsAuthorityServed: authority => {

                    if (Interlocked.Exchange(ref askedFirst, 1) == 1)
                        return true;

                    originAsked.TrySetResult();
                    refuse.Wait(PipedH2ServerConnection.StepTimeout);

                    return false;

                });

            try
            {

                await peer.RequestAsync(StreamId, "/misdirected");
                await originAsked.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                refuse.Set();

                var outcome = await events.OutcomeAsync(StreamId);

                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled request"),  "how the server reported the request");
                    Assert.That(SentOn(peer, StreamId),   Is.Empty,                                "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                       "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(("content-type", "text/plain")),
                                                                                                   "the refusal's field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                     "body of the next response");

                });

            }
            finally
            {
                refuse.Set();
            }

        }

        #endregion

        #region ConnectAnsweredAfterReset_NothingSent(StatusCode)

        /// <summary>
        /// The connect handler decides on a CONNECT after the client has reset its
        /// stream: it accepts the tunnel, or refuses it. The answer's HEADERS
        /// went out on the reset stream, and an accepted tunnel was run there.
        /// Now nothing goes out, the tunnel is not run, and the handler is
        /// reported as cancelled.
        /// </summary>
        [TestCase((UInt16) 200)]
        [TestCase((UInt16) 403)]
        public async Task ConnectAnsweredAfterReset_NothingSent(UInt16 StatusCode)
        {

            var streamId        = StatusCode == 200 ? 5029u : 5033u;

            var connectArrived  = new TaskCompletionSource(Async);
            var answer          = new TaskCompletionSource(Async);
            var tunnelRun       = false;

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: async (id, headers, cancellationToken) => {

                    connectArrived.TrySetResult();
                    await answer.Task;

                    return new HTTP2ConnectResult {
                        StatusCode    = StatusCode,
                        ExtraHeaders  = [Late],
                        RunAsync      = StatusCode == 200
                                            ? (tunnel, tunnelToken) => { tunnelRun = true; return Task.CompletedTask; }
                                            : null
                    };

                });

            try
            {

                await peer.RequestTunnelAsync(streamId, "tunnel.example:443");
                await connectArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(streamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                answer.TrySetResult();

                var outcome = await events.OutcomeAsync(streamId);

                await peer.RequestAsync(streamId + 2, "/other");

                var other = await peer.TryResponseAsync(streamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(outcome,                  Is.EqualTo("HandlerCancelled CONNECT"),  "how the server reported the connect handler");
                    Assert.That(tunnelRun,                Is.False,                                "the tunnel, run on the reset stream");
                    Assert.That(SentOn(peer, streamId),   Is.Empty,                                "what the server sent on the reset stream");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                       "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                      "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                     "body of the next response");

                });

            }
            finally
            {
                answer.TrySetResult();
            }

        }

        #endregion

        #region TrailersTakenAtReset_NotSent_ConnectionServesOn()

        /// <summary>
        /// A streaming handler ends its response with trailers, and the writer
        /// loop takes them off the stream's queue, to send them as the response's
        /// last HEADERS block, just as the client resets the stream. The trailers
        /// went out on the reset stream. Now they stay unsent, the handler's
        /// CompleteAsync fails with the stream's token, and the writer loop, which
        /// every stream's body goes through, serves on: the failed header write
        /// is no failure of the connection's.
        /// </summary>
        [Test]
        public async Task TrailersTakenAtReset_NotSent_ConnectionServesOn()
        {

            const UInt32 StreamId = 5037;

            var headersSent  = new TaskCompletionSource(Async);
            var complete     = new TaskCompletionSource(Async);
            var completing   = new TaskCompletionSource<(Task Completion, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value != "/late")
                    {
                        await AnswerOther(response);
                        return;
                    }

                    await response.WriteHeadersAsync([(":status", "200")]);

                    headersSent.TrySetResult();
                    await complete.Task;

                    // Queued at once. It completes once the trailers go out, and
                    // fails once they never will.
                    var completion = response.CompleteAsync([Late]);

                    completing.TrySetResult((completion, cancellationToken));

                    await completion;

                });

            try
            {

                await peer.RequestAsync(StreamId, "/late");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(StreamId);

                (Task Completion, CancellationToken HandlerToken) completion;

                using (await peer.HoldWritesAsync())
                {

                    complete.TrySetResult();

                    completion = await completing.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await WaitUntilTakenAsync(stream);

                    await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                    await WaitUntilResetAsync(stream);

                }

                var (ended, failure) = await EndOf(completion.Completion);

                // The writer loop goes on from the trailers to the next response's
                // body, so by the end of that response, the trailers would have
                // been read.
                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(GoAways(peer),            Is.Empty,                                    "how the server ended the connection");

                    Assert.That(SentOn(peer, StreamId),   Is.EqualTo(new[] { "HEADERS :status 200" }), "what the server sent on the reset stream");

                    Assert.That(ended,                    Is.True,                                     "the handler's CompleteAsync returned");
                    Assert.That(failure,                  Is.InstanceOf<OperationCanceledException>(), "how the handler's CompleteAsync ended, its trailers unsent");
                    Assert.That(TokenOf(failure),         Is.EqualTo(completion.HandlerToken),         "the token it carries: the handler's, the stream's own");

                    Assert.That(other?.Status,            Is.EqualTo("200"),                           "status of the next response");
                    Assert.That(other?.Headers,           Does.Contain(Late),                          "the field the next response repeats");
                    Assert.That(other?.Body,              Is.EqualTo("other"),                         "body of the next response");

                });

            }
            finally
            {
                complete.TrySetResult();
            }

        }

        #endregion

        #region HeadersAfterConnectionEnd_FailWithTheStreamsToken(ClientGoesAway)

        /// <summary>
        /// A streaming handler writes its headers after its connection has ended:
        /// the client went away, or the server stopped. The end of a connection
        /// resets every stream still open on it, so the write fails as on any
        /// reset stream, with the stream's token, the handler's. It failed with
        /// the connection's own token, which the handler was never given.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task HeadersAfterConnectionEnd_FailWithTheStreamsToken(Boolean ClientGoesAway)
        {

            var requestArrived  = new TaskCompletionSource(Async);
            var writeHeaders    = new TaskCompletionSource(Async);
            var headersWritten  = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    requestArrived.TrySetResult();
                    await writeHeaders.Task;

                    var write = response.WriteHeadersAsync([(":status", "200"), Late]);

                    headersWritten.TrySetResult((write, cancellationToken));

                    await write;

                });

            try
            {

                await peer.RequestAsync(1, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                if (ClientGoesAway)
                    await peer.DisconnectAsync();
                else
                    await peer.DisposeAsync();

                await peer.Ended.WaitAsync(PipedH2ServerConnection.StepTimeout);

                writeHeaders.TrySetResult();

                var (write, handlerToken)  = await headersWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                var (ended, failure)       = await EndOf(write);

                Assert.Multiple(() =>
                {

                    Assert.That(ended,                                 Is.True,                                     "the header write returned");
                    Assert.That(failure,                               Is.InstanceOf<OperationCanceledException>(), "how the header write ended");
                    Assert.That(TokenOf(failure),                      Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                    Assert.That(handlerToken.IsCancellationRequested,  Is.True,                                    "the handler's token, cancelled when the connection ended");

                });

            }
            finally
            {
                writeHeaders.TrySetResult();
            }

        }

        #endregion

    }

}
