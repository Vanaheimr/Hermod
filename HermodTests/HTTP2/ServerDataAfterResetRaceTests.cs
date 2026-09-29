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
    /// DATA the server's writer loop has taken off a stream's queue, and an
    /// RST_STREAM of the server's own that gets the connection's write lock
    /// first. The loop takes a chunk, gives back what it reserved of the send
    /// windows beyond it, and only then waits for the write lock. A reset of the
    /// stream made meanwhile on another task, the read loop's for a stream error
    /// of the client's, or a failing handler's, sends its RST_STREAM under that
    /// lock, and when it got the lock first, the DATA followed it onto the closed
    /// stream: RFC 9113, Section 5.1 allows nothing but PRIORITY there, and a
    /// client may take such a frame for a connection error of type STREAM_CLOSED.
    ///
    /// On a real connection the gap is a few instructions wide.
    /// <see cref="PipedH2ServerConnection.HoldTakenDataAsync"/> holds the loop in
    /// it, where it gives back the window, with the write lock free: the
    /// RST_STREAM is on the wire before the loop goes on, on every run. (The
    /// tests in <see cref="ServerDataAfterResetTests"/> hold the taken chunk at
    /// the write lock instead, where it is ahead of the RST_STREAM.)
    /// </summary>
    [TestFixture]
    public class ServerDataAfterResetRaceTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// How long a test waits for a reset that must not come while a chunk is
        /// held. One that does come, comes at once: the handler's task only has to
        /// be scheduled.
        /// </summary>
        private static readonly TimeSpan ResetGrace = TimeSpan.FromMilliseconds(250);

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Refuses every request: every request is streamed here.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> NotStreamed(UInt32                             StreamId,
                                                                                                                  List<(String Name, String Value)>  Headers,
                                                                                                                  Byte[]?                            Body,
                                                                                                                  CancellationToken                  CancellationToken)

            => throw new InvalidOperationException("Every request is streamed here");

        /// <summary>
        /// The handler's two writes of a download, and the token it was given.
        /// </summary>
        private sealed record Writes(Task First, Task Second, CancellationToken HandlerToken);

        /// <summary>
        /// A streaming handler. "/download" sends its response headers, says so
        /// with <paramref name="HeadersSent"/>, and once <paramref name="Write"/>
        /// is completed, writes "first" and "chunk" at once, both queued before it
        /// waits for either, and hands the writes to the test with
        /// <paramref name="Written"/>. Once both have returned, it fails, given
        /// <paramref name="FailAfterLastWrite"/>, or else waits for
        /// <paramref name="Finish"/>. "/idle" waits for its token, and writes
        /// nothing. Any other path is answered "other".
        /// </summary>
        private static HTTP2StreamingHandler Download(TaskCompletionSource          HeadersSent,
                                                      TaskCompletionSource          Write,
                                                      TaskCompletionSource<Writes>  Written,
                                                      TaskCompletionSource          Finish,
                                                      Boolean                       FailAfterLastWrite)

            => async (request, response, cancellationToken) => {

                   var path = request.Headers.First(header => header.Name == ":path").Value;

                   if (path == "/idle")
                   {
                       await Task.Delay(Timeout.Infinite, cancellationToken);
                       return;
                   }

                   await response.WriteHeadersAsync([(":status", "200")]);

                   if (path != "/download")
                   {
                       await response.WriteAsync(ASCII("other"));
                       return;
                   }

                   HeadersSent.TrySetResult();

                   await Write.Task;

                   var first   = response.WriteAsync(ASCII("first"));
                   var second  = response.WriteAsync(ASCII("chunk"));

                   Written.TrySetResult(new Writes(first, second, cancellationToken));

                   await first;
                   await second;

                   if (FailAfterLastWrite)
                       throw new InvalidOperationException("The handler failed on purpose, right after its last write");

                   await Finish.Task;

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


        #region RstStreamOutWhileTakenDataIsHeld_DataNotSentAfterIt()

        /// <summary>
        /// A download's second chunk is taken off the stream's queue, and held
        /// before the write lock, when the client sends a malformed frame on the
        /// stream: a WINDOW_UPDATE with an increment of 0. The read loop resets the
        /// stream for that stream error, and its RST_STREAM PROTOCOL_ERROR goes
        /// out, the write lock being free. Then the writer loop goes on with the
        /// chunk. It does not send it, as the stream is closed: the handler's write
        /// fails with the stream's token, the window taken for the chunk goes back
        /// to the connection, and the connection serves on. The chunk used to go
        /// out after the RST_STREAM.
        /// </summary>
        [Test]
        public async Task RstStreamOutWhileTakenDataIsHeld_DataNotSentAfterIt()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var write        = new TaskCompletionSource(Async);
            var written      = new TaskCompletionSource<Writes>(Async);
            var finish       = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(NotStreamed, StreamingHandler: Download(headersSent, write, written, finish, FailAfterLastWrite: false));

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.RequestAsync(3, "/idle");

                var windowBefore = peer.ConnectionSendWindow();

                Writes?        writes     = null;
                HTTP2ErrorCode resetCode;

                using (var held = await peer.HoldTakenDataAsync(1, ScannedAfter: 3, Write: async () => {
                                                                    write.TrySetResult();
                                                                    writes = await written.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                                                                }))
                {

                    await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, 0));

                    resetCode = await peer.ReadToResetAsync(1);

                    await held.LetGoAsync();

                }

                var handlerWrites     = writes!;
                var (ended, failure)  = await EndOf(handlerWrites.Second);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetCode,                    Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),       "how the server reset the stream");

                    Assert.That(SentOn(peer, 1),              Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                 "DATA \"first\"",
                                                                                 "RST_STREAM PROTOCOL_ERROR" }),
                                                              "what the server sent on the reset stream");

                    Assert.That(ended,                        Is.True,                                         "the handler's write of the chunk returned");
                    Assert.That(failure,                      Is.InstanceOf<OperationCanceledException>(),     "how the handler's write of the unsent chunk ended");
                    Assert.That(TokenOf(failure),             Is.EqualTo(handlerWrites.HandlerToken),          "the token it carries: the handler's, the stream's own");

                    Assert.That(peer.ConnectionSendWindow(),  Is.EqualTo(windowBefore - "first".Length),       "the connection's send window: less the chunk that went out, the other one's share given back");
                    Assert.That(GoAways(peer),                Is.Empty,                                        "how the server ended the connection");

                });

                await peer.RequestAsync(5, "/other");

                var other = await peer.TryResponseAsync(5, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(other?.Status,  Is.EqualTo("200"),    "status of the next response");
                    Assert.That(other?.Body,    Is.EqualTo("other"),  "body of the next response");
                });

            }
            finally
            {
                write. TrySetResult();
                finish.TrySetResult();
            }

        }

        #endregion

        #region HandlerFailsAfterItsLastWrite_TakenChunkHeld_DataOutBeforeTheRstStream()

        /// <summary>
        /// A streaming handler fails right after its last write, once that has
        /// returned, and the server resets the stream on the handler's task. The
        /// chunk is taken off the stream's queue, and held before the write lock,
        /// the lock being free: an RST_STREAM sent meanwhile would go out first.
        /// The handler's write does not return while the chunk is held, and so the
        /// stream is reset only once the chunk is out. The write used to return as
        /// soon as the writer loop took the chunk: the handler failed, its
        /// RST_STREAM INTERNAL_ERROR went out, and the chunk followed it onto the
        /// closed stream.
        /// </summary>
        [Test]
        public async Task HandlerFailsAfterItsLastWrite_TakenChunkHeld_DataOutBeforeTheRstStream()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var write        = new TaskCompletionSource(Async);
            var written      = new TaskCompletionSource<Writes>(Async);
            var finish       = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(NotStreamed, StreamingHandler: Download(headersSent, write, written, finish, FailAfterLastWrite: true));

            try
            {

                await peer.RequestAsync(1, "/download");
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.RequestAsync(3, "/idle");

                var stream        = peer.ServerStream(1);
                var windowBefore  = peer.ConnectionSendWindow();

                Boolean resetWhileHeld;

                using (var held = await peer.HoldTakenDataAsync(1, ScannedAfter: 3, Write: async () => {
                                                                    write.TrySetResult();
                                                                    await written.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                                                                }))
                {

                    resetWhileHeld = await ResetWithinAsync(stream, ResetGrace);

                    // The handler has reset the stream: its RST_STREAM is read, and
                    // so on the wire, before the chunk goes on.
                    if (resetWhileHeld)
                        await peer.ReadToResetAsync(1);

                    await held.LetGoAsync();

                }

                if (!resetWhileHeld)
                    await peer.ReadToResetAsync(1);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(resetWhileHeld,               Is.False,                                    "the stream reset while the handler's last chunk was held after its take");

                    Assert.That(SentOn(peer, 1),              Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                 "DATA \"first\"",
                                                                                 "DATA \"chunk\"",
                                                                                 "RST_STREAM INTERNAL_ERROR" }),
                                                              "what the server sent on the stream");

                    Assert.That(peer.ConnectionSendWindow(),  Is.EqualTo(windowBefore - "firstchunk".Length),  "the connection's send window: less both chunks, and nothing else");
                    Assert.That(GoAways(peer),                Is.Empty,                                    "how the server ended the connection");

                });

            }
            finally
            {
                write. TrySetResult();
                finish.TrySetResult();
            }

        }

        #endregion

    }

}
