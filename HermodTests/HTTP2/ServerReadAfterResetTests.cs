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
    /// Reads of a request body on a stream that is reset. A streaming handler
    /// pulls the body from a channel that the connection's read loop fills as
    /// DATA arrives, and completes at END_STREAM. A reset cancelled the handler's
    /// token, but did not complete the channel: a read made without that token
    /// waited for a chunk that would never come. So when a client cancelled an
    /// upload with RST_STREAM CANCEL, a handler that read on without passing its
    /// token hung there, with its buffers.
    ///
    /// Such a read now fails, with an OperationCanceledException that carries the
    /// stream's own token, the one its handler was given, as a write on a reset
    /// stream does. It never returns null: that says the body is whole, and a
    /// truncated upload must not look like one.
    /// </summary>
    [TestFixture]
    public class ServerReadAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// How a read ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task<Byte[]?> Read)
        {

            if (await Task.WhenAny(Read, Task.Delay(PipedH2ServerConnection.StepTimeout)) != Read)
                return (false, null);

            try
            {
                await Read;
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
        /// Read a request body to its end, as a handler does that passes no token:
        /// the chunks it read, and how the body ended — null for a whole body,
        /// else the exception the last read failed with.
        /// </summary>
        private static async Task<(List<String> Chunks, Exception? End)> ReadToEndAsync(IHTTP2RequestStream Request)
        {

            var chunks = new List<String>();

            try
            {

                while (await Request.ReadAsync() is { } chunk)
                    chunks.Add(Encoding.ASCII.GetString(chunk));

                return (chunks, null);

            }
            catch (Exception e)
            {
                return (chunks, e);
            }

        }

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

        #endregion


        #region ReadAfterReset_FailsAtOnce_ConnectionServesOn()

        /// <summary>
        /// The client cancels an upload with RST_STREAM CANCEL, after a first chunk
        /// the handler has read, and the handler reads on without its token. That
        /// read fails as it is made, with the stream's token: it neither waits for
        /// a chunk that will never come, nor returns null as if the body had ended.
        /// Nothing goes out on the reset stream, and the connection serves the
        /// next request.
        /// </summary>
        [Test]
        public async Task ReadAfterReset_FailsAtOnce_ConnectionServesOn()
        {

            var firstChunkRead  = new TaskCompletionSource<String>(Async);
            var readOn          = new TaskCompletionSource(Async);
            var nextChunkRead   = new TaskCompletionSource<(Task<Byte[]?> Read, Boolean DoneAtOnce, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value != "/upload")
                    {
                        await response.WriteHeadersAsync([(":status", "200")]);
                        await response.WriteAsync(ASCII("other"));
                        return;
                    }

                    firstChunkRead.TrySetResult(Encoding.ASCII.GetString(await request.ReadAsync() ?? []));

                    await readOn.Task;

                    var read = request.ReadAsync().AsTask();

                    nextChunkRead.TrySetResult((read, read.IsCompleted, cancellationToken));

                    await read;

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));

                var firstChunk = await firstChunkRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                // The read loop handles one frame at a time: once the ping is
                // answered, stream 1 is reset.
                await peer.PingAsync();

                readOn.TrySetResult();

                var (read, doneAtOnce, handlerToken)  = await nextChunkRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                var (ended, failure)                  = await EndOf(read);

                // Only the end of the test ends the connection.
                Assert.That(ended, Is.True, "the read on the reset stream returned while the connection was open");

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(firstChunk,                            Is.EqualTo("part 1"),                        "the chunk read before the reset");
                    Assert.That(doneAtOnce,                            Is.True,                                     "the read after the reset, done as it was made");
                    Assert.That(failure,                               Is.InstanceOf<OperationCanceledException>(), "how the read on the reset stream ended");
                    Assert.That(TokenOf(failure),                      Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                    Assert.That(handlerToken.IsCancellationRequested,  Is.True,                                    "the handler's token, cancelled by the reset");

                    Assert.That(other?.Status,                         Is.EqualTo("200"),                          "status of the next response");
                    Assert.That(other?.Body,                           Is.EqualTo("other"),                        "body of the next response");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.Empty,
                                "what the server sent on the reset stream");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region ReadWaitingAtReset_FailsWithTheStreamsToken()

        /// <summary>
        /// A read already waiting when the reset comes, here for a chunk the client
        /// never sends, ends as one made after it does: with the stream's token,
        /// once the reset is handled.
        /// </summary>
        [Test]
        public async Task ReadWaitingAtReset_FailsWithTheStreamsToken()
        {

            var readWaiting = new TaskCompletionSource<(Task<Byte[]?> Read, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    var read = request.ReadAsync().AsTask();

                    readWaiting.TrySetResult((read, cancellationToken));

                    await read;

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);

            var (read, handlerToken) = await readWaiting.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            Assert.That(read.IsCompleted, Is.False, "the read, while the client sends nothing");

            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            var (ended, failure) = await EndOf(read);

            await peer.PingAsync();

            Assert.Multiple(() =>
            {

                Assert.That(ended,             Is.True,                                     "the read returned once the stream was reset");
                Assert.That(failure,           Is.InstanceOf<OperationCanceledException>(), "how the read ended");
                Assert.That(TokenOf(failure),  Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.Empty,
                            "what the server sent on the reset stream");

            });

        }

        #endregion

        #region BodyCutByReset_ReadsWhatArrived_ThenFails()

        /// <summary>
        /// The reset cuts an upload short while two chunks still wait to be read.
        /// A handler that reads on without its token gets what arrived before the
        /// reset, in order, and then fails with the stream's token. It never gets
        /// null, which says the body is whole: a truncated upload must not look
        /// like one that ended.
        /// </summary>
        [Test]
        public async Task BodyCutByReset_ReadsWhatArrived_ThenFails()
        {

            var readOn    = new TaskCompletionSource(Async);
            var bodyRead  = new TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await readOn.Task;

                    var (chunks, end) = await ReadToEndAsync(request);

                    bodyRead.TrySetResult((chunks, end, cancellationToken));

                    // No answer on a reset stream.
                    cancellationToken.ThrowIfCancellationRequested();

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));
                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                readOn.TrySetResult();

                var ended = await Task.WhenAny(bodyRead.Task, Task.Delay(PipedH2ServerConnection.StepTimeout)) == bodyRead.Task;

                Assert.That(ended, Is.True, "the handler's reading of the body ended while the connection was open");

                var (chunks, end, handlerToken) = await bodyRead.Task;

                Assert.Multiple(() =>
                {

                    Assert.That(chunks,        Is.EqualTo(new[] { "part 1", "part 2" }),    "what the handler read of the body");
                    Assert.That(end,           Is.InstanceOf<OperationCanceledException>(), "how the body ended: null would say it was whole");
                    Assert.That(TokenOf(end),  Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region BodyEndedBeforeReset_ReadsAsWhole()

        /// <summary>
        /// A reset that comes after the client has ended the body, as when it gives
        /// up waiting for the response, cuts nothing short: the handler reads the
        /// whole body and then null, as before. Its token is cancelled, and that is
        /// how it learns of the reset.
        /// </summary>
        [Test]
        public async Task BodyEndedBeforeReset_ReadsAsWhole()
        {

            var readOn    = new TaskCompletionSource(Async);
            var bodyRead  = new TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await readOn.Task;

                    var (chunks, end) = await ReadToEndAsync(request);

                    bodyRead.TrySetResult((chunks, end, cancellationToken));

                    // No answer on a reset stream.
                    cancellationToken.ThrowIfCancellationRequested();

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("whole"), EndStream: true));
                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                readOn.TrySetResult();

                var ended = await Task.WhenAny(bodyRead.Task, Task.Delay(PipedH2ServerConnection.StepTimeout)) == bodyRead.Task;

                Assert.That(ended, Is.True, "the handler's reading of the body ended while the connection was open");

                var (chunks, end, handlerToken) = await bodyRead.Task;

                Assert.Multiple(() =>
                {

                    Assert.That(chunks,                                Is.EqualTo(new[] { "whole" }), "what the handler read of the body");
                    Assert.That(end,                                   Is.Null,                       "how the body ended: with null, as a whole one");
                    Assert.That(handlerToken.IsCancellationRequested,  Is.True,                       "the handler's token, cancelled by the reset");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region HandlerFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()

        /// <summary>
        /// A streaming handler fails after it has sent its response headers, while
        /// the client is still uploading, and the server resets the stream on the
        /// handler's task. The read loop may be taking the client's next DATA frame
        /// on that stream just then: past its check of the stream's state, but
        /// before it hands the chunk to the body channel that the reset has just
        /// completed. The chunk is dropped, and its connection window given back,
        /// as for DATA on a closed stream; the connection serves on. Written into
        /// the completed channel instead, it would throw the reset's
        /// OperationCanceledException into the read loop, and that ends the
        /// connection, without even a GOAWAY.
        /// </summary>
        [Test]
        public async Task HandlerFailsAsAChunkArrives_ChunkDropped_ConnectionServesOn()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    if (request.Headers.First(header => header.Name == ":path").Value != "/upload")
                    {
                        await response.WriteAsync(ASCII("other"));
                        return;
                    }

                    headersSent.TrySetResult();

                    await fail.Task;

                    throw new InvalidOperationException("The handler failed on purpose");

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(1);

                await peer.HoldDataAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("part 1")), async () => {

                    fail.TrySetResult();

                    // The server's RST_STREAM goes out once the handler's task has
                    // reset the stream.
                    await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                });

                var answers = await Answers(peer);

                Assert.That(answers, Is.True, "the connection, once the chunk that came with the reset was handled");

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(stream.ReceivedBodyLength,        Is.EqualTo(6),       "the chunk, counted as one on an open stream: the reset came after the read loop's check");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),       "connection window held back for the chunk, which nobody will read");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the reset stream");

                    Assert.That(other?.Status,                    Is.EqualTo("200"),   "status of the next response");
                    Assert.That(other?.Body,                      Is.EqualTo("other"), "body of the next response");

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
