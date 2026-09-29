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
using System.Diagnostics.Tracing;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Writes on a stream that is reset, or closed. The server queues every body
    /// it sends, streamed or not, every end of a stream and every tunnel write on
    /// the stream's outbound queue, for the connection's DATA writer loop to send,
    /// and the writer waits until its bytes go out. The loop sends nothing on
    /// a closed stream, and a reset released only what was queued at that moment:
    /// a write that came after it waited until the connection ended. So when a
    /// client cancelled a download with RST_STREAM CANCEL, a handler that wrote
    /// its next chunk without checking its token first hung there, with its
    /// buffers, for as long as the client kept the connection open.
    ///
    /// Such a write now fails at once, and so does one still queued when the
    /// reset comes: with an OperationCanceledException that carries the stream's
    /// own token, the one its handler was given, so the write ends the handler as
    /// the handler's own check of that token would.
    /// </summary>
    [TestFixture]
    public class ServerWriteAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The client's SETTINGS that give every new stream a send window of 0: a
        /// body written there stays in the stream's outbound queue until the
        /// client grants it room with a WINDOW_UPDATE.
        /// </summary>
        private static HTTP2Frame NoSendWindow()

            => HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0));

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
        /// The token a task was canceled with, or null for a task that is not
        /// canceled (yet). Never waits.
        /// </summary>
        private static CancellationToken? CanceledWith(Task Task)
        {

            if (!Task.IsCanceled)
                return null;

            try
            {
                Task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException e)
            {
                return e.CancellationToken;
            }

            return null;

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

        }

        #endregion


        #region WriteAfterReset_FailsAtOnce_ConnectionServesOn()

        /// <summary>
        /// The client cancels a download with RST_STREAM CANCEL, and the handler
        /// writes its next chunk without a look at its token. That write fails at
        /// once, with the stream's token; nothing more goes out on the reset
        /// stream; and the connection serves the next request.
        /// </summary>
        [Test]
        public async Task WriteAfterReset_FailsAtOnce_ConnectionServesOn()
        {

            var firstChunkWritten  = new TaskCompletionSource(Async);
            var writeNextChunk     = new TaskCompletionSource(Async);
            var nextChunkWritten   = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    if (request.Headers.First(header => header.Name == ":path").Value != "/download")
                    {
                        await response.WriteAsync(Encoding.ASCII.GetBytes("other"));
                        return;
                    }

                    await response.WriteAsync(Encoding.ASCII.GetBytes("chunk 1"));

                    firstChunkWritten.TrySetResult();
                    await writeNextChunk.Task;

                    var write = response.WriteAsync(Encoding.ASCII.GetBytes("chunk 2"));

                    nextChunkWritten.TrySetResult((write, cancellationToken));

                    await write;

                });

            try
            {

                await peer.RequestAsync(1, "/download");
                await firstChunkWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                // The read loop handles one frame at a time: once the ping is
                // answered, stream 1 is reset.
                await peer.PingAsync();

                writeNextChunk.TrySetResult();

                var (write, handlerToken)  = await nextChunkWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                var (ended, failure)       = await EndOf(write);

                // Only the end of the test ends the connection.
                Assert.That(ended, Is.True, "the write on the reset stream returned while the connection was open");

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(failure,                               Is.InstanceOf<OperationCanceledException>(), "how the write on the reset stream ended");
                    Assert.That(TokenOf(failure),                      Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                    Assert.That(handlerToken.IsCancellationRequested,  Is.True,                                    "the handler's token, cancelled by the reset");

                    Assert.That(other?.Status,                         Is.EqualTo("200"),                          "status of the next response");
                    Assert.That(other?.Body,                           Is.EqualTo("other"),                        "body of the next response");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA \"chunk 1\"" }),
                                "what the server sent on the reset stream");

                });

            }
            finally
            {
                writeNextChunk.TrySetResult();
            }

        }

        #endregion

        #region WriteQueuedAtReset_FailsWithTheStreamsToken()

        /// <summary>
        /// A write still queued when the reset comes, here for want of a send
        /// window, ends as one that comes after it: with the stream's token. It
        /// used to succeed, as if its chunk had been sent.
        /// </summary>
        [Test]
        public async Task WriteQueuedAtReset_FailsWithTheStreamsToken()
        {

            var chunkQueued = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    var write = response.WriteAsync(Encoding.ASCII.GetBytes("chunk"));

                    chunkQueued.TrySetResult((write, cancellationToken));

                    await write;

                });

            await peer.SendAsync(NoSendWindow());
            await peer.RequestAsync(1, "/download");

            var (write, handlerToken) = await chunkQueued.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            Assert.That(write.IsCompleted, Is.False, "the write, while the client grants no room");

            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            var (ended, failure) = await EndOf(write);

            await peer.PingAsync();

            Assert.Multiple(() =>
            {

                Assert.That(ended,             Is.True,                                     "the write returned once the stream was reset");
                Assert.That(failure,           Is.InstanceOf<OperationCanceledException>(), "how the write ended");
                Assert.That(TokenOf(failure),  Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.EqualTo(new[] { "HEADERS :status 200" }),
                            "what the server sent on the reset stream");

            });

        }

        #endregion

        #region WriteCancelledByItsToken_EndsTheWait_ChunkStillSent()

        /// <summary>
        /// The token a write is given ends the wait for its chunk to go out — here
        /// for a send window the client does not grant. It does not take the chunk
        /// back: the stream stays open, and the chunk stays queued, in order, and
        /// goes out once there is room. The token used to be ignored, and the
        /// write waited for the window regardless.
        /// </summary>
        [Test]
        public async Task WriteCancelledByItsToken_EndsTheWait_ChunkStillSent()
        {

            using var writeCancellation = new CancellationTokenSource();

            var chunkQueued = new TaskCompletionSource<Task>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    var write = response.WriteAsync(Encoding.ASCII.GetBytes("chunk"), writeCancellation.Token);

                    chunkQueued.TrySetResult(write);

                    try
                    {
                        await write;
                    }
                    catch (OperationCanceledException)
                    {
                        // The handler gave up waiting, not the response: it
                        // returns, and the server completes the response.
                    }

                });

            await peer.SendAsync(NoSendWindow());
            await peer.RequestAsync(1, "/download");

            var write = await chunkQueued.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            writeCancellation.Cancel();

            var (ended, failure) = await EndOf(write);

            Assert.That(ended, Is.True, "the write returned once its token was cancelled, with no room granted");

            var resetMeanwhile = peer.ServerStream(1).WasReset;

            // Room for the chunk: it goes out, and the end of the response after it.
            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, 5));

            var response = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(failure,           Is.InstanceOf<OperationCanceledException>(), "how the write ended");
                Assert.That(TokenOf(failure),  Is.EqualTo(writeCancellation.Token),         "the token it carries: its own");
                Assert.That(resetMeanwhile,    Is.False,                                    "the stream reset when the write was cancelled");

                Assert.That(response?.Status,  Is.EqualTo("200"),                           "status of the response");
                Assert.That(response?.Body,    Is.EqualTo("chunk"),                         "body of the response: the chunk whose write was cancelled");

            });

        }

        #endregion

        #region BodyAfterReset_ReleasesTheHandlersTask()

        /// <summary>
        /// A buffered handler that answers after its stream was reset. Its
        /// response body, queued on the reset stream, waited for the connection to
        /// end, and so did the task that sent it. The task now ends at once, and
        /// is reported as cancelled.
        /// </summary>
        [Test]
        public async Task BodyAfterReset_ReleasesTheHandlersTask()
        {

            // An ID no other test uses: the handler events name nothing but the stream.
            const UInt32 StreamId = 4097;

            var requestArrived  = new TaskCompletionSource(Async);
            var answer          = new TaskCompletionSource(Async);

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    if (streamId == StreamId)
                    {
                        requestArrived.TrySetResult();
                        await answer.Task;
                    }

                    return ([(":status", "200")], Encoding.ASCII.GetBytes(streamId == StreamId ? "late" : "other"));

                });

            try
            {

                await peer.RequestAsync(StreamId, "/late");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                answer.TrySetResult();

                // RequestHandled once the body has gone out, HandlerCancelled once
                // it never will: nothing, while the task waits.
                var outcome = await events.FirstAsync(e => (e.Name is "RequestHandled" or "HandlerCancelled") &&
                                                           Equals(e.Payload[0], (Int32) StreamId));

                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(outcome?.Name,        Is.EqualTo("HandlerCancelled"), "how the server reported the request");
                    Assert.That(outcome?.Payload[1],  Is.EqualTo("request"),          "the kind of handler reported");

                    Assert.That(other?.Status,        Is.EqualTo("200"),              "status of the next response");
                    Assert.That(other?.Body,          Is.EqualTo("other"),            "body of the next response");

                });

            }
            finally
            {
                answer.TrySetResult();
            }

        }

        #endregion

        #region FailureAfterReset_NotAnswered(Streaming)

        /// <summary>
        /// A handler that fails after its stream was reset. The failure is
        /// reported, but not answered: nobody reads a 500 on a reset stream, and a
        /// stream reset of the server's own after it would be a second reset of
        /// the stream. The buffered fallback sent the 500's HEADERS there; the
        /// streamed one sent them too, then waited for the 500's body on the reset
        /// stream, and with that write failing at once it would have gone on to an
        /// RST_STREAM INTERNAL_ERROR.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task FailureAfterReset_NotAnswered(Boolean Streaming)
        {

            // An ID no other test uses, as the failure's message is: the handler
            // events name nothing else.
            const UInt32 StreamId = 4101;

            var failure         = new InvalidOperationException($"The handler failed on purpose ({Guid.NewGuid()})");
            var requestArrived  = new TaskCompletionSource(Async);
            var fail            = new TaskCompletionSource(Async);

            async Task FailAfterTheReset()
            {
                requestArrived.TrySetResult();
                await fail.Task;
                throw failure;
            }

            HTTP2StreamingHandler streamed = async (request, response, cancellationToken) => {

                if (request.Headers.First(header => header.Name == ":path").Value == "/fail")
                    await FailAfterTheReset();

                await response.WriteHeadersAsync([(":status", "200")]);
                await response.WriteAsync(Encoding.ASCII.GetBytes("other"));

            };

            using var events = new HTTP2Events();

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    if (streamId == StreamId)
                        await FailAfterTheReset();

                    return ([(":status", "200")], Encoding.ASCII.GetBytes("other"));

                },

                StreamingHandler: Streaming ? streamed : null);

            try
            {

                await peer.RequestAsync(StreamId, "/fail");
                await requestArrived.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                fail.TrySetResult();

                var reported = await events.FirstAsync(e => e.Name == "HandlerFailed" && Equals(e.Payload[2], failure.Message));

                // An answer would follow the report at once, on the handler's own
                // task: long before a whole later exchange has ended.
                await peer.RequestAsync(StreamId + 2, "/other");

                var other = await peer.TryResponseAsync(StreamId + 2, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(reported?.Payload[1],                                  Is.EqualTo(Streaming ? "streaming" : "request"), "the handler reported as failed");
                    Assert.That(peer.FramesOn(StreamId).Select(frame => frame.ToString()), Is.Empty,                                "what the server sent on the reset stream");
                    Assert.That(other?.Body,                                           Is.EqualTo("other"),                             "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion


        #region Queue_AbandonedBeforeOrAfterAWrite_WriteCanceled(AbandonFirst)

        /// <summary>
        /// AbandonAll and EnqueueAsync both take the queue's gate, so one of the
        /// two comes first. A write queued before is canceled with the rest; one
        /// that comes after finds the queue abandoned and is canceled at once.
        /// Both with the token AbandonAll was given, and nothing is left for the
        /// writer loop. A write after the abandonment used to stay queued for good.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Queue_AbandonedBeforeOrAfterAWrite_WriteCanceled(Boolean AbandonFirst)
        {

            var queue = new HTTP2OutboundQueue();

            using var reset = new CancellationTokenSource();
            reset.Cancel();

            Task write;

            if (AbandonFirst)
            {
                queue.AbandonAll(reset.Token);
                write = queue.EnqueueAsync([1, 2, 3], EndStream: false);
            }
            else
            {
                write = queue.EnqueueAsync([1, 2, 3], EndStream: false);
                queue.AbandonAll(reset.Token);
            }

            Assert.Multiple(() =>
            {
                Assert.That(CanceledWith(write),   Is.EqualTo(reset.Token), "the token the write was canceled with");
                Assert.That(queue.HasPending,      Is.False,                "anything left for the writer loop");
                Assert.That(queue.TakeChunk(1024), Is.Null,                 "what the writer loop could still take");
            });

        }

        #endregion

        #region Stream_ClosedByBothSides_WritesCanceled(PeerEndsLast)

        /// <summary>
        /// A stream both sides have ended is closed, and the writer loop sends
        /// nothing on it any more. A write there, which only one after the end of
        /// the server's own side can be, is canceled, whether it was queued before
        /// the stream closed or comes later, and whichever side ended it last. No
        /// reset: the stream's token stays as it is.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Stream_ClosedByBothSides_WritesCanceled(Boolean PeerEndsLast)
        {

            var stream = new HTTP2Stream(1, 65535, 65535);
            stream.Open();

            if (PeerEndsLast)
                stream.TryCloseLocal();
            else
                stream.CloseRemote();

            var queued = stream.OutboundQueue.EnqueueAsync([1], EndStream: false);

            if (PeerEndsLast)
                stream.CloseRemote();
            else
                stream.TryCloseLocal();

            var later = stream.OutboundQueue.EnqueueAsync([2], EndStream: false);

            Assert.Multiple(() =>
            {

                Assert.That(stream.State,                                   Is.EqualTo(HTTP2StreamState.Closed), "the stream");
                Assert.That(queued.IsCanceled,                              Is.True,                            "a write queued before the stream closed, canceled");
                Assert.That(later. IsCanceled,                              Is.True,                            "a write after it closed, canceled");
                Assert.That(stream.OutboundQueue.HasPending,                Is.False,                           "anything left for the writer loop");

                Assert.That(stream.WasReset,                                Is.False,                           "the stream reset");
                Assert.That(stream.CancellationToken.IsCancellationRequested, Is.False,                         "the stream's token, which only a reset cancels");

            });

        }

        #endregion

    }

}
