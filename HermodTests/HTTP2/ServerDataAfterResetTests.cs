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

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// DATA the server's writer loop has taken off a stream's outbound queue, and
    /// a reset of that stream before the frame is written. The writer loop told
    /// the chunk's writer that its write was done as soon as it took the chunk,
    /// then waited for the connection's write lock, and wrote the frame without
    /// another look at the stream. A reset made meanwhile on another task, the
    /// read loop's for a stream error, a failing handler's, or the client's own,
    /// sends its RST_STREAM under the same lock. When that got the lock first, the
    /// DATA followed it onto the closed stream, where RFC 9113, Section 5.1 allows
    /// nothing but PRIORITY. A handler that failed right after its last write did
    /// this itself: told the write was done, it reset the stream, and its
    /// RST_STREAM could go out before that DATA, a tunnel's END_STREAM among them.
    ///
    /// The writer loop now looks at the stream once the write lock is its own, as
    /// a header block does: a stream reset by then gets no DATA, and the send
    /// window taken for it goes back to the connection. The DATA goes out before
    /// the RST_STREAM, or not at all. And a write returns only once its DATA is
    /// the next to go out, so a handler that fails after its last write resets
    /// the stream only once that DATA is on its way, ahead of the RST_STREAM.
    /// </summary>
    [TestFixture]
    public class ServerDataAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// How long a test that holds the server's writes waits for a reset that
        /// must not come while they are held. One that does come, comes at once:
        /// the handler's task only has to be scheduled.
        /// </summary>
        private static readonly TimeSpan ResetGrace = TimeSpan.FromMilliseconds(250);

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Refuses every request: every request is streamed here, or tunnels.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> NotStreamed(UInt32                             StreamId,
                                                                                                                  List<(String Name, String Value)>  Headers,
                                                                                                                  Byte[]?                            Body,
                                                                                                                  CancellationToken                  CancellationToken)

            => throw new InvalidOperationException("Every request is streamed here");

        /// <summary>
        /// A streaming handler whose "/download" sends its response headers, says
        /// so with <paramref name="HeadersSent"/>, and once
        /// <paramref name="WriteChunk"/> is completed, writes a chunk. It hands the
        /// write to the test with <paramref name="ChunkWritten"/> before it waits
        /// for it, and then waits for its token. Any other path is answered
        /// "other".
        /// </summary>
        private static HTTP2StreamingHandler Download(TaskCompletionSource                                             HeadersSent,
                                                      TaskCompletionSource                                             WriteChunk,
                                                      TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>  ChunkWritten)

            => async (request, response, cancellationToken) => {

                   await response.WriteHeadersAsync([(":status", "200")]);

                   if (request.Headers.First(header => header.Name == ":path").Value != "/download")
                   {
                       await response.WriteAsync(ASCII("other"));
                       return;
                   }

                   HeadersSent.TrySetResult();

                   await WriteChunk.Task;

                   var write = response.WriteAsync(ASCII("chunk"));

                   ChunkWritten.TrySetResult((write, cancellationToken));

                   await write;
                   await Task.Delay(Timeout.Infinite, cancellationToken);

               };

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
        /// Whether the server resets the stream within <paramref name="Grace"/>:
        /// true as soon as it does.
        /// </summary>
        private static async Task<Boolean> ResetWithinAsync(HTTP2Stream Stream, TimeSpan Grace)
        {

            try
            {
                await Task.Delay(Grace, Stream.CancellationToken);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }

        }

        /// <summary>
        /// What the server sent on this stream, in order.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        /// <summary>
        /// The GOAWAY frames the server sent.
        /// </summary>
        private static List<String> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => frame.ToString())];

        #endregion


        #region DataTakenAsTheServerResetsTheStream_NotSent_ConnectionServesOn()

        /// <summary>
        /// A chunk of a download waits for the write lock, taken off the stream's
        /// queue, when the client sends a malformed frame on the stream: a
        /// WINDOW_UPDATE with an increment of 0. The read loop resets the stream
        /// for that stream error, and its RST_STREAM PROTOCOL_ERROR waits for the
        /// write lock as well, behind the chunk. The chunk is not sent: the
        /// handler's write fails with the stream's token, the window taken for the
        /// chunk goes back to the connection, and the connection serves on. The
        /// chunk used to go out; it got the lock first here, and would have
        /// followed the RST_STREAM had that got it first.
        /// </summary>
        [Test]
        public async Task DataTakenAsTheServerResetsTheStream_NotSent_ConnectionServesOn()
        {

            var headersSent   = new TaskCompletionSource(Async);
            var writeChunk    = new TaskCompletionSource(Async);
            var chunkWritten  = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(NotStreamed, StreamingHandler: Download(headersSent, writeChunk, chunkWritten));

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream        = peer.ServerStream(1);
                var windowBefore  = peer.ConnectionSendWindow();

                (Task Write, CancellationToken HandlerToken) chunk;

                using (await peer.HoldWritesAsync())
                {

                    writeChunk.TrySetResult();

                    chunk = await chunkWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await UntilAsync(() => !stream.OutboundQueue.HasPending, "the writer loop has taken the chunk");

                    await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, 0));

                    await UntilAsync(() => stream.WasReset, "the read loop has reset the stream");

                }

                var resetCode         = await peer.ReadToResetAsync(1);
                var (ended, failure)  = await EndOf(chunk.Write);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetCode,                    Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),    "how the server reset the stream");

                    Assert.That(SentOn(peer, 1),              Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                 "RST_STREAM PROTOCOL_ERROR" }),
                                                              "what the server sent on the reset stream");

                    Assert.That(ended,                        Is.True,                                      "the handler's write of the chunk returned");
                    Assert.That(failure,                      Is.InstanceOf<OperationCanceledException>(),  "how the handler's write of the unsent chunk ended");
                    Assert.That(TokenOf(failure),             Is.EqualTo(chunk.HandlerToken),               "the token it carries: the handler's, the stream's own");

                    Assert.That(peer.ConnectionSendWindow(),  Is.EqualTo(windowBefore),                     "the connection's send window, the unsent chunk's share given back");
                    Assert.That(GoAways(peer),                Is.Empty,                                     "how the server ended the connection");

                });

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                writeChunk.TrySetResult();
            }

        }

        #endregion

        #region DataTakenAsTheClientResetsTheStream_NotSent()

        /// <summary>
        /// The client cancels a download with RST_STREAM CANCEL while a chunk of
        /// it waits for the write lock, taken off the stream's queue. The chunk is
        /// not sent: once the read loop has handled the client's RST_STREAM, the
        /// stream is closed, and nothing but PRIORITY may go out on it. The chunk
        /// used to follow the client's reset. The handler's write fails with the
        /// stream's token, and the window taken for the chunk goes back to the
        /// connection.
        /// </summary>
        [Test]
        public async Task DataTakenAsTheClientResetsTheStream_NotSent()
        {

            var headersSent   = new TaskCompletionSource(Async);
            var writeChunk    = new TaskCompletionSource(Async);
            var chunkWritten  = new TaskCompletionSource<(Task Write, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(NotStreamed, StreamingHandler: Download(headersSent, writeChunk, chunkWritten));

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream        = peer.ServerStream(1);
                var windowBefore  = peer.ConnectionSendWindow();

                (Task Write, CancellationToken HandlerToken) chunk;

                using (await peer.HoldWritesAsync())
                {

                    writeChunk.TrySetResult();

                    chunk = await chunkWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await UntilAsync(() => !stream.OutboundQueue.HasPending, "the writer loop has taken the chunk");

                    await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                    await UntilAsync(() => stream.WasReset, "the read loop has handled the client's reset");

                }

                var (ended, failure) = await EndOf(chunk.Write);

                // The writer loop has the write lock before the answer to the
                // ping: once that is read, so is anything it wrote.
                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(SentOn(peer, 1),              Is.EqualTo(new[] { "HEADERS :status 200" }),  "what the server sent on the stream the client reset");

                    Assert.That(ended,                        Is.True,                                      "the handler's write of the chunk returned");
                    Assert.That(failure,                      Is.InstanceOf<OperationCanceledException>(),  "how the handler's write of the unsent chunk ended");
                    Assert.That(TokenOf(failure),             Is.EqualTo(chunk.HandlerToken),               "the token it carries: the handler's, the stream's own");

                    Assert.That(peer.ConnectionSendWindow(),  Is.EqualTo(windowBefore),                     "the connection's send window, the unsent chunk's share given back");

                });

            }
            finally
            {
                writeChunk.TrySetResult();
            }

        }

        #endregion

        #region EndOfStreamTakenAsTheServerResetsTheStream_NotSent()

        /// <summary>
        /// A streaming handler ends its response with nothing left to send, and
        /// the END_STREAM, an empty DATA frame, waits for the write lock, taken
        /// off the stream's queue, when the read loop resets the stream for a
        /// stream error of the client's, a WINDOW_UPDATE of 0. The END_STREAM is
        /// not sent, and the handler's CompleteAsync fails with the stream's
        /// token, rather than report a response that was never ended.
        /// </summary>
        [Test]
        public async Task EndOfStreamTakenAsTheServerResetsTheStream_NotSent()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var complete     = new TaskCompletionSource(Async);
            var completing   = new TaskCompletionSource<(Task Completion, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    headersSent.TrySetResult();

                    await complete.Task;

                    var completion = response.CompleteAsync();

                    completing.TrySetResult((completion, cancellationToken));

                    await completion;

                });

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(1);

                (Task Completion, CancellationToken HandlerToken) completion;

                using (await peer.HoldWritesAsync())
                {

                    complete.TrySetResult();

                    completion = await completing.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await UntilAsync(() => !stream.OutboundQueue.HasPending, "the writer loop has taken the END_STREAM");

                    await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, 0));

                    await UntilAsync(() => stream.WasReset, "the read loop has reset the stream");

                }

                var resetCode         = await peer.ReadToResetAsync(1);
                var (ended, failure)  = await EndOf(completion.Completion);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetCode,          Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),    "how the server reset the stream");

                    Assert.That(SentOn(peer, 1),    Is.EqualTo(new[] { "HEADERS :status 200",
                                                                       "RST_STREAM PROTOCOL_ERROR" }),
                                                    "what the server sent on the reset stream");

                    Assert.That(ended,              Is.True,                                      "the handler's CompleteAsync returned");
                    Assert.That(failure,            Is.InstanceOf<OperationCanceledException>(),  "how the handler's CompleteAsync ended, its END_STREAM unsent");
                    Assert.That(TokenOf(failure),   Is.EqualTo(completion.HandlerToken),          "the token it carries: the handler's, the stream's own");

                });

            }
            finally
            {
                complete.TrySetResult();
            }

        }

        #endregion

        #region DataAheadOfTrailersTakenAsTheClientResetsTheStream_NotSent()

        /// <summary>
        /// A response's last chunk and its trailers, queued as one, wait for the
        /// write lock, taken off the stream's queue, when the client resets the
        /// stream. Neither goes out: not the DATA, which leaves the stream open
        /// for the trailers, and not the trailers' HEADERS. The window taken for
        /// the chunk goes back to the connection, and the write fails with the
        /// stream's token. No producer of the server's queues a chunk and trailers
        /// as one today, but the writer loop sends such an item, so the test
        /// queues it itself.
        /// </summary>
        [Test]
        public async Task DataAheadOfTrailersTakenAsTheClientResetsTheStream_NotSent()
        {

            var headersSent = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    headersSent.TrySetResult();

                    await Task.Delay(Timeout.Infinite, cancellationToken);

                });

            await peer.RequestAsync(1, "/download");
            await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            var stream        = peer.ServerStream(1);
            var windowBefore  = peer.ConnectionSendWindow();

            Task write;

            using (await peer.HoldWritesAsync())
            {

                write = peer.Connection.EnqueueOutboundAsync(stream, ASCII("last chunk"), EndStream: true, [("x-checksum", "sha-256=:bGFzdCBjaHVuaw==:")]);

                await UntilAsync(() => !stream.OutboundQueue.HasPending, "the writer loop has taken the chunk and its trailers");

                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                await UntilAsync(() => stream.WasReset, "the read loop has handled the client's reset");

            }

            var (ended, failure) = await EndOf(write);

            await peer.PingAsync();

            Assert.Multiple(() =>
            {

                Assert.That(SentOn(peer, 1),              Is.EqualTo(new[] { "HEADERS :status 200" }),  "what the server sent on the stream the client reset");

                Assert.That(ended,                        Is.True,                                      "the write of the chunk and its trailers returned");
                Assert.That(failure,                      Is.InstanceOf<OperationCanceledException>(),  "how the write of the unsent chunk and trailers ended");
                Assert.That(TokenOf(failure),             Is.EqualTo(stream.CancellationToken),         "the token it carries: the stream's own");

                Assert.That(peer.ConnectionSendWindow(),  Is.EqualTo(windowBefore),                     "the connection's send window, the unsent chunk's share given back");

            });

        }

        #endregion

        #region TunnelHandlerFails_EndOfStreamOutBeforeTheReset()

        /// <summary>
        /// A tunnel's handler fails, and on the handler's task the server ends its
        /// side of the tunnel with an END_STREAM and then resets the stream. The
        /// END_STREAM waits for the write lock, taken by the writer loop, and the
        /// handler's task waits for it: it resets the stream only once the
        /// END_STREAM is out. It used to be told the END_STREAM was out as soon as
        /// the writer loop took it, and its RST_STREAM could go out first, the
        /// END_STREAM after it, on the closed stream.
        /// </summary>
        [Test]
        public async Task TunnelHandlerFails_EndOfStreamOutBeforeTheReset()
        {

            var tunnelOpen  = new TaskCompletionSource(Async);
            var fail        = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

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

                var stream = peer.ServerStream(1);

                Boolean resetWhileHeld;

                using (await peer.HoldWritesAsync())
                {

                    fail.TrySetResult();

                    resetWhileHeld = await ResetWithinAsync(stream, ResetGrace);

                }

                var resetCode = await peer.ReadToResetAsync(1);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetWhileHeld,    Is.False,                                   "the stream reset while the END_STREAM that ends the server's side waited for the write lock");
                    Assert.That(resetCode,         Is.EqualTo(HTTP2ErrorCode.INTERNAL_ERROR),  "how the server reset the tunnel");

                    Assert.That(SentOn(peer, 1),   Is.EqualTo(new[] { "HEADERS :status 200",
                                                                      "DATA \"\" END_STREAM",
                                                                      "RST_STREAM INTERNAL_ERROR" }),
                                                   "what the server sent on the tunnel");

                    Assert.That(GoAways(peer),     Is.Empty,                                   "how the server ended the connection");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region StreamingHandlerFailsAfterItsLastWrite_DataOutBeforeTheReset()

        /// <summary>
        /// A streaming handler fails right after its last write, once that has
        /// returned, and the server resets the stream on the handler's task. The
        /// chunk waits for the write lock, taken by the writer loop, and the
        /// handler's write waits for it: the stream is reset only once the chunk
        /// is out. The write used to return as soon as the writer loop took the
        /// chunk, and the handler's RST_STREAM could go out first, the chunk after
        /// it, on the closed stream.
        /// </summary>
        [Test]
        public async Task StreamingHandlerFailsAfterItsLastWrite_DataOutBeforeTheReset()
        {

            var headersSent   = new TaskCompletionSource(Async);
            var writeLast     = new TaskCompletionSource(Async);
            var lastWritten   = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                NotStreamed,

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    headersSent.TrySetResult();

                    await writeLast.Task;

                    var write = response.WriteAsync(ASCII("last words"));

                    lastWritten.TrySetResult();

                    await write;

                    throw new InvalidOperationException("The handler failed on purpose, right after its last write");

                });

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(1);

                Boolean resetWhileHeld;

                using (await peer.HoldWritesAsync())
                {

                    writeLast.TrySetResult();

                    await lastWritten.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                    await UntilAsync(() => !stream.OutboundQueue.HasPending, "the writer loop has taken the chunk");

                    resetWhileHeld = await ResetWithinAsync(stream, ResetGrace);

                }

                var resetCode = await peer.ReadToResetAsync(1);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetWhileHeld,    Is.False,                                   "the stream reset while the handler's last chunk waited for the write lock");
                    Assert.That(resetCode,         Is.EqualTo(HTTP2ErrorCode.INTERNAL_ERROR),  "how the server reset the stream");

                    Assert.That(SentOn(peer, 1),   Is.EqualTo(new[] { "HEADERS :status 200",
                                                                      "DATA \"last words\"",
                                                                      "RST_STREAM INTERNAL_ERROR" }),
                                                   "what the server sent on the stream");

                    Assert.That(GoAways(peer),     Is.Empty,                                   "how the server ended the connection");

                });

            }
            finally
            {
                writeLast.TrySetResult();
            }

        }

        #endregion

    }

}
