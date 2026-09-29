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
using System.Buffers.Binary;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The connection window of request-body and tunnel bytes a reset leaves
    /// unread. A streaming handler reads its body, and a tunnel handler its
    /// tunnel, from a channel that the read loop fills as DATA arrives, and the
    /// window of each chunk is given back only when the handler reads it: that is
    /// how the server pushes back on a fast client. A reset ends the reading. A
    /// handler that honours its token stops, one that failed has stopped, and
    /// what was still in the channel was never read, so its window was never
    /// given back. The connection lost that much of its window for good, and with
    /// a whole window's worth unread, every later upload on it stalled.
    ///
    /// Once a reset is handled, the connection window of every byte still unread
    /// is given back now. The bytes stay readable, as before: the chunks that
    /// arrived, in order, and then a body's read fails and a tunnel's returns
    /// null. But reading them gives nothing back a second time. And a
    /// stream-level WINDOW_UPDATE goes out only while the client may still send
    /// DATA on the stream, never once it is closed.
    /// </summary>
    [TestFixture]
    public class ServerUnreadWindowAfterResetTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The size of the DATA frames the client uploads with: the smallest
        /// SETTINGS_MAX_FRAME_SIZE, 16 KiB.
        /// </summary>
        private const Int32 ChunkSize = 16 * 1024;

        /// <summary>
        /// The server's receive windows: the connection's, which it raises to
        /// 1 MiB at once, and each stream's, its INITIAL_WINDOW_SIZE of 1 MiB.
        /// </summary>
        private const Int64 Window = 1024 * 1024;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        private static String PathOf(IHTTP2RequestStream Request)

            => Request.Headers.First(header => header.Name == ":path").Value;

        /// <summary>
        /// Upload <paramref name="Count"/> chunks of <see cref="ChunkSize"/> bytes
        /// on the stream, without ending it.
        /// </summary>
        private static async Task UploadAsync(PipedH2ServerConnection Peer, UInt32 StreamId, Int32 Count)
        {
            for (var i = 0; i < Count; i++)
                await Peer.SendAsync(HTTP2Frame.CreateData(StreamId, new Byte[ChunkSize]));
        }

        /// <summary>
        /// Read a request body to its end, as a handler does that passes no token:
        /// how many chunks and bytes it read, and how the body ended — null for a
        /// whole body, else the exception the last read failed with.
        /// </summary>
        private static async Task<(Int32 Chunks, Int64 Bytes, Exception? End)> ReadToEndAsync(IHTTP2RequestStream Request)
        {

            var chunks  = 0;
            var bytes   = 0L;

            try
            {

                while (await Request.ReadAsync() is { } chunk)
                {
                    chunks++;
                    bytes += chunk.Length;
                }

                return (chunks, bytes, null);

            }
            catch (Exception e)
            {
                return (chunks, bytes, e);
            }

        }

        /// <summary>
        /// Answers every request that is no CONNECT, whatever its body.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Other(UInt32                             StreamId,
                                                                                                            List<(String Name, String Value)>  Headers,
                                                                                                            Byte[]?                            Body,
                                                                                                            CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], ASCII((Body?.Length ?? 0).ToString())));

        /// <summary>
        /// A connect handler that opens every tunnel and runs it with
        /// <paramref name="RunAsync"/>.
        /// </summary>
        private static HTTP2ConnectHandler Tunnel(Func<HTTP2Tunnel, CancellationToken, Task> RunAsync)

            => (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {
                                                                             StatusCode  = 200,
                                                                             RunAsync    = RunAsync
                                                                         });

        /// <summary>
        /// Read a tunnel to its end, as a handler does that passes no token: how
        /// many chunks and bytes it read, and how the tunnel ended — null when a
        /// read returned null, else the exception it failed with.
        /// </summary>
        private static async Task<(Int32 Chunks, Int64 Bytes, Exception? End)> ReadToEndAsync(HTTP2Tunnel Tunnel)
        {

            var chunks  = 0;
            var bytes   = 0L;

            try
            {

                while (await Tunnel.ReadAsync(CancellationToken.None) is { } chunk)
                {
                    chunks++;
                    bytes += chunk.Length;
                }

                return (chunks, bytes, null);

            }
            catch (Exception e)
            {
                return (chunks, bytes, e);
            }

        }

        /// <summary>
        /// Answer 200 with the length of the whole request body.
        /// </summary>
        private static async Task EchoLengthAsync(IHTTP2RequestStream Request, IHTTP2ResponseStream Response)
        {

            var (_, bytes, end) = await ReadToEndAsync(Request);

            await Response.WriteHeadersAsync([(":status", end is null ? "200" : "500")]);
            await Response.WriteAsync(ASCII(bytes.ToString()));

        }

        /// <summary>
        /// The token a cancellation carries, or null for any other outcome.
        /// </summary>
        private static CancellationToken? TokenOf(Exception? Failure)

            => (Failure as OperationCanceledException)?.CancellationToken;

        /// <summary>
        /// The client's connection send window as the frames tell it: RFC 9113's
        /// initial 65 535 bytes, plus every connection-level WINDOW_UPDATE the
        /// server sent, less the DATA the client sent. Read after a ping.
        /// </summary>
        private static Int64 ClientConnectionWindow(PipedH2ServerConnection Peer, Int64 Sent)

            => 65_535 + Peer.FramesOn(0).
                             Where (frame => frame.Frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                             Sum   (frame => (Int64) (BinaryPrimitives.ReadUInt32BigEndian(frame.Frame.Payload) & 0x7FFFFFFF))
                      - Sent;

        /// <summary>
        /// The stream-level WINDOW_UPDATEs the server sent on this stream, with
        /// their increments.
        /// </summary>
        private static List<String> WindowUpdatesOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).
                        Where (frame => frame.Frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                        Select(frame => $"WINDOW_UPDATE {BinaryPrimitives.ReadUInt32BigEndian(frame.Frame.Payload) & 0x7FFFFFFF}")];

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
        /// The connection window held back, once a reset made on a task of the
        /// server's own, a handler's or the writer loop's, has been handled there:
        /// no frame tells the client when that is, so this waits until nothing is
        /// held back, and gives up after the step timeout with what still is.
        /// </summary>
        private static async Task<Int64> HeldBackOnceSettledAsync(PipedH2ServerConnection Peer)
        {

            var waited = Stopwatch.StartNew();

            while (Peer.ConnectionWindowHeldBack() != 0 && waited.Elapsed < PipedH2ServerConnection.StepTimeout)
                await Task.Delay(1);

            return Peer.ConnectionWindowHeldBack();

        }

        #endregion


        #region ResetWithBodyUnread_WindowGivenBack_UploadsGoOn(Chunks)

        /// <summary>
        /// The client uploads, then cancels with RST_STREAM CANCEL, while the
        /// handler, which honours its token, has read nothing. The connection
        /// window of the whole upload is given back once the reset is handled:
        /// with a whole window's worth unread (64 chunks, 1 MiB), in a
        /// WINDOW_UPDATE right away, and with less, set aside for the next one,
        /// as any window is. Nothing goes out on the reset stream, and the next
        /// upload on the connection goes through.
        /// </summary>
        [TestCase(4)]
        [TestCase(64)]
        public async Task ResetWithBodyUnread_WindowGivenBack_UploadsGoOn(Int32 Chunks)
        {

            var handlerEnded = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) != "/upload")
                    {
                        await EchoLengthAsync(request, response);
                        return;
                    }

                    try
                    {
                        // Reads nothing, and stops when its token says so.
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    finally
                    {
                        handlerEnded.TrySetResult();
                    }

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);
            await UploadAsync(peer, 1, Chunks);
            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            await handlerEnded.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            // The read loop handles one frame at a time: once the ping is
            // answered, the reset is handled, and all the server sent before is read.
            await peer.PingAsync();

            var heldBack      = peer.ConnectionWindowHeldBack();
            var clientWindow  = ClientConnectionWindow(peer, Sent: Chunks * ChunkSize);

            Assert.Multiple(() =>
            {

                Assert.That(heldBack,      Is.EqualTo(0),           "connection window held back for the chunks nobody will read, once the reset is handled");
                Assert.That(clientWindow,  Is.GreaterThan(Window / 2), "the client's connection window: never less than half of it once a reset is handled");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.Empty,
                            "what the server sent on the reset stream");

            });

            await peer.RequestAsync(3, "/echo", EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateData(3, new Byte[ChunkSize], EndStream: true));

            var echo = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(echo?.Status,  Is.EqualTo("200"),                "status of the next upload's response");
                Assert.That(echo?.Body,    Is.EqualTo(ChunkSize.ToString()), "length of the next upload, as the server read it");
            });

        }

        #endregion

        #region BodyReadAfterReset_WindowGivenBackOnce()

        /// <summary>
        /// A reset cuts short an upload of a whole window's worth, 1 MiB, before
        /// the handler has read any of it. Its connection window is given back
        /// once the reset is handled. The handler then reads on without its token,
        /// and gets what arrived, in order, and then the failure: reading it gives
        /// nothing back a second time, so the client is never granted more window
        /// than the server can take in, and no stream-level WINDOW_UPDATE goes out
        /// on the reset stream.
        /// </summary>
        [Test]
        public async Task BodyReadAfterReset_WindowGivenBackOnce()
        {

            const Int32 chunks = 64;

            var readOn    = new TaskCompletionSource(Async);
            var bodyRead  = new TaskCompletionSource<(Int32 Chunks, Int64 Bytes, Exception? End, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await readOn.Task;

                    var (chunks, bytes, end) = await ReadToEndAsync(request);

                    bodyRead.TrySetResult((chunks, bytes, end, cancellationToken));

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                var heldBackAtReset      = peer.ConnectionWindowHeldBack();
                var clientWindowAtReset  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

                readOn.TrySetResult();

                var (chunksRead, bytesRead, end, handlerToken) = await bodyRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                // Whatever the reads made the server send is read after this.
                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackAtReset,                                   Is.EqualTo(0),                               "connection window held back once the reset is handled, before the handler has read anything");
                    Assert.That(clientWindowAtReset,                               Is.EqualTo(Window),                          "the client's connection window once the reset is handled");

                    Assert.That(chunksRead,                                        Is.EqualTo(chunks),                          "chunks the handler read after the reset: all that arrived");
                    Assert.That(bytesRead,                                         Is.EqualTo(chunks * ChunkSize),              "bytes the handler read after the reset");
                    Assert.That(end,                                               Is.InstanceOf<OperationCanceledException>(), "how the body ended: null would say it was whole");
                    Assert.That(TokenOf(end),                                      Is.EqualTo(handlerToken),                    "the token it carries: the handler's, the stream's own");

                    Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                               "connection window held back once the handler has read it all: given back once, not twice");
                    Assert.That(ClientConnectionWindow(peer, chunks * ChunkSize),  Is.EqualTo(Window),                          "the client's connection window: no more than the server takes in");
                    Assert.That(WindowUpdatesOn(peer, 1),                          Is.Empty,                                    "stream-level WINDOW_UPDATEs on the reset stream");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region StreamWindowUpdateDueAsResetComes_NotSent()

        /// <summary>
        /// The handler reads half its stream's window, which makes a stream-level
        /// WINDOW_UPDATE due, and the client resets the stream before that frame
        /// is out: it waits for the connection's write lock, which the test holds.
        /// Once the lock is free, the stream is closed, and the frame stays unsent.
        /// Sent, it would be a frame other than PRIORITY on a closed stream, which
        /// RFC 9113, Section 5.1 forbids.
        /// </summary>
        [Test]
        public async Task StreamWindowUpdateDueAsResetComes_NotSent()
        {

            const Int32 chunks = 32;

            var readOn        = new TaskCompletionSource(Async);
            var handlerEnded  = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    try
                    {

                        await readOn.Task;

                        for (var i = 0; i < chunks; i++)
                            await request.ReadAsync();

                        await Task.Delay(Timeout.Infinite, cancellationToken);

                    }
                    finally
                    {
                        handlerEnded.TrySetResult();
                    }

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.PingAsync();

                var stream = peer.ServerStream(1);

                Assert.That(stream.RecvWindow, Is.EqualTo(Window - chunks * ChunkSize), "the stream's receive window, with half of it taken up");

                using (await peer.HoldWritesAsync())
                {

                    readOn.TrySetResult();

                    // Counted up again by the handler's last read: a stream-level
                    // WINDOW_UPDATE is due, and waits for the write lock.
                    await UntilAsync(() => stream.RecvWindow == Window, "the handler's reads have made a stream-level WINDOW_UPDATE due");

                    await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

                    await UntilAsync(() => stream.WasReset, "the reset is handled");

                }

                await handlerEnded.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                await peer.PingAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(WindowUpdatesOn(peer, 1),         Is.Empty,      "stream-level WINDOW_UPDATEs on the reset stream, one of them due as the reset came");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0), "connection window held back");
                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region BodyEndedBeforeRead_NoStreamWindowUpdate()

        /// <summary>
        /// The client ends its upload before the handler reads it. Once the client
        /// has ended its side, it can send no more DATA on the stream, and a
        /// stream-level WINDOW_UPDATE would tell it nothing: none goes out, while
        /// the connection's window is given back as the handler reads. RFC 9113,
        /// Section 5.1 frees a stream that is half-closed (remote) from keeping up
        /// its receive window, and a WINDOW_UPDATE then could even follow the
        /// server's own END_STREAM onto the closed stream.
        /// </summary>
        [Test]
        public async Task BodyEndedBeforeRead_NoStreamWindowUpdate()
        {

            const Int32 chunks = 32;

            var readOn = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await readOn.Task;
                    await EchoLengthAsync(request, response);

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
                await peer.PingAsync();

                readOn.TrySetResult();

                var echo = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(echo?.Status,                                      Is.EqualTo("200"),                           "status of the response");
                    Assert.That(echo?.Body,                                        Is.EqualTo((chunks * ChunkSize).ToString()), "length of the upload, as the server read it");

                    Assert.That(WindowUpdatesOn(peer, 1),                          Is.Empty,                                    "stream-level WINDOW_UPDATEs once the client had ended its side");
                    Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                               "connection window held back");
                    Assert.That(ClientConnectionWindow(peer, chunks * ChunkSize),  Is.EqualTo(Window),                          "the client's connection window, given back as the handler read");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region StreamErrorWithBodyUnread_WindowGivenBack()

        /// <summary>
        /// The client sends a malformed frame on a stream whose handler has not
        /// read its body yet, a WINDOW_UPDATE with an increment of 0. The read loop
        /// answers with a stream error, RST_STREAM PROTOCOL_ERROR, and resets the
        /// stream itself. The connection window of the unread chunks is given
        /// back.
        /// </summary>
        [Test]
        public async Task StreamErrorWithBodyUnread_WindowGivenBack()
        {

            const Int32 chunks = 4;

            var handlerEnded = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    finally
                    {
                        handlerEnded.TrySetResult();
                    }

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);
            await UploadAsync(peer, 1, chunks);
            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, 0));

            var answered = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

            await handlerEnded.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
            await peer.PingAsync();

            Assert.Multiple(() =>
            {

                Assert.That(answered,                         Is.Null,       "a response on the stream, rather than a reset");
                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0), "connection window held back for the chunks nobody will read");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),
                            "what the server sent on the stream");

            });

        }

        #endregion

        #region WriterLoopResetsWithBodyUnread_WindowGivenBack()

        /// <summary>
        /// The server's DATA writer loop fails to send the handler's response body
        /// with a stream error, and resets the stream on its own task while two
        /// chunks of the request body are still unread. Their connection window is
        /// given back.
        /// </summary>
        [Test]
        public async Task WriterLoopResetsWithBodyUnread_WindowGivenBack()
        {

            var writeOn       = new TaskCompletionSource(Async);
            var handlerEnded  = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    try
                    {
                        await response.WriteHeadersAsync([(":status", "200")]);
                        await writeOn.Task;
                        await response.WriteAsync(ASCII("chunk 1"));
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    finally
                    {
                        handlerEnded.TrySetResult();
                    }

                });

            try
            {

                peer.FailNextDataWrite(1, new HTTP2StreamException(HTTP2ErrorCode.INTERNAL_ERROR, 1, "The write failed on purpose"));

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));

                // Both chunks wait in the body channel before the reset comes.
                await peer.PingAsync();

                writeOn.TrySetResult();

                var answered = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                await handlerEnded.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
                await peer.PingAsync();

                var heldBack = await HeldBackOnceSettledAsync(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(answered,  Is.Null,       "a response on the stream, rather than a reset");
                    Assert.That(heldBack,  Is.EqualTo(0), "connection window held back for the chunks nobody will read");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the stream");

                });

            }
            finally
            {
                writeOn.TrySetResult();
            }

        }

        #endregion

        #region HandlerFailsWithBodyUnread_AsAChunkArrives_WindowGivenBack()

        /// <summary>
        /// A streaming handler fails after it has sent its response headers, with
        /// two chunks of the body unread, and the server resets the stream on the
        /// handler's task. The read loop is taking a third chunk just then: past
        /// its check of the stream's state, but before it hands the chunk to the
        /// body channel. The window of all three is given back, each once: the two
        /// unread ones, and the third, which is dropped. The two tests below hold
        /// each order of the reset's return and the read loop's count of that
        /// third chunk in place.
        /// </summary>
        [Test]
        public async Task HandlerFailsWithBodyUnread_AsAChunkArrives_WindowGivenBack()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    if (PathOf(request) != "/upload")
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
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream = peer.ServerStream(1);

                await peer.HoldDataAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("part 3")), async () => {

                    fail.TrySetResult();

                    // The server's RST_STREAM goes out once the handler's task has
                    // reset the stream.
                    await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                });

                await peer.PingAsync();

                var heldBack = await HeldBackOnceSettledAsync(peer);

                await peer.RequestAsync(3, "/other");

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(stream.ReceivedBodyLength,  Is.EqualTo(18),      "the three chunks, counted on an open stream: the reset came after the read loop's check of the third");
                    Assert.That(heldBack,                   Is.EqualTo(0),       "connection window held back for the chunks nobody will read");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the reset stream");

                    Assert.That(other?.Status,              Is.EqualTo("200"),   "status of the next response");
                    Assert.That(other?.Body,                Is.EqualTo("other"), "body of the next response");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region HandlerFailsAsAChunkIsHandedOver_WindowGivenBackOnce()

        /// <summary>
        /// The tightest spot of the race above. The read loop has counted a third
        /// chunk as unread, and is about to put it into the body channel, when the
        /// handler fails, and its task resets the stream and gives back the window
        /// of all that is unread: the third chunk's too. The chunk then finds the
        /// channel completed, and is dropped without its window being given back a
        /// second time. To hold the read loop there, the test swaps the body
        /// channel for one whose write lets the handler fail first, and waits until
        /// the handler's task has given the window back.
        /// </summary>
        [Test]
        public async Task HandlerFailsAsAChunkIsHandedOver_WindowGivenBackOnce()
        {

            var headersSent  = new TaskCompletionSource(Async);
            var fail         = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    headersSent.TrySetResult();

                    await fail.Task;

                    throw new InvalidOperationException("The handler failed on purpose");

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await headersSent.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream                = peer.ServerStream(1);
                var writes                = 0;
                var heldBackAtWrite       = 0L;
                var givenBackBeforeWrite  = false;

                stream.RequestBodyChannel = new InterceptedChannel(stream.RequestBodyChannel!, BeforeWrite: () => {

                    if (++writes < 3)
                        return;

                    // On the read loop, which has taken in the third chunk.
                    heldBackAtWrite = peer.ConnectionWindowHeldBack();

                    fail.TrySetResult();

                    givenBackBeforeWrite = SpinWait.SpinUntil(() => peer.ConnectionWindowHeldBack() == 0, PipedH2ServerConnection.StepTimeout);

                });

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 3")));

                var answered = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackAtWrite,                  Is.EqualTo(18),  "connection window held back as the third chunk was written: all three chunks'");
                    Assert.That(givenBackBeforeWrite,             Is.True,         "all of it given back by the handler's task, the third chunk's window too, before that chunk's write");
                    Assert.That(answered,                         Is.Null,         "a response on the stream, rather than a reset");
                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),   "connection window held back in the end: given back once for each chunk, the dropped one too");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
                                "what the server sent on the reset stream");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region ChunkPastTheCheckWhenResetGivesBack_WindowGivenBackAtOnce()

        /// <summary>
        /// The other side of the race: the read loop holds a third chunk past its
        /// check of the stream's state, and before it has counted the chunk as
        /// unread, the stream is reset and the window of the two unread chunks
        /// given back. The third comes after that, so it is not withheld at all:
        /// it is dropped, and its window given back at once. Counted as unread, it
        /// would be held back for good, with the reset's return already done.
        /// </summary>
        [Test]
        public async Task ChunkPastTheCheckWhenResetGivesBack_WindowGivenBackAtOnce()
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: (request, response, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken));

            await peer.RequestAsync(1, "/upload", EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));

            await peer.PingAsync();

            var stream = peer.ServerStream(1);

            await peer.ResetAfterStateCheckAsync(HTTP2Frame.CreateData(1, ASCII("part 3")));

            await peer.PingAsync();

            Assert.Multiple(() =>
            {

                Assert.That(stream.WasReset,                  Is.True,        "the stream, reset while the read loop held the third chunk");
                Assert.That(stream.ReceivedBodyLength,        Is.EqualTo(18), "the three chunks, counted on an open stream: the third passed the read loop's check before the reset");
                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),  "connection window held back: given back once for each chunk, the third too");

            });

        }

        #endregion

        #region (InterceptedChannel)

        /// <summary>
        /// A channel that runs <paramref name="BeforeWrite"/> in every write, on the
        /// writer's thread, before it passes the write on to <paramref name="Inner"/>.
        /// </summary>
        private sealed class InterceptedChannel : Channel<Byte[]>
        {

            public InterceptedChannel(Channel<Byte[]> Inner, Action BeforeWrite)
            {
                Reader = Inner.Reader;
                Writer = new InterceptedWriter(Inner.Writer, BeforeWrite);
            }

            private sealed class InterceptedWriter(ChannelWriter<Byte[]> Inner, Action BeforeWrite) : ChannelWriter<Byte[]>
            {

                public override Boolean TryWrite(Byte[] Item)
                {
                    BeforeWrite();
                    return Inner.TryWrite(Item);
                }

                public override ValueTask WriteAsync(Byte[] Item, CancellationToken CancellationToken = default)
                {
                    BeforeWrite();
                    return Inner.WriteAsync(Item, CancellationToken);
                }

                public override ValueTask<Boolean> WaitToWriteAsync(CancellationToken CancellationToken = default)
                    => Inner.WaitToWriteAsync(CancellationToken);

                public override Boolean TryComplete(Exception? Error = null)
                    => Inner.TryComplete(Error);

            }

        }

        #endregion


        #region ResetWithTunnelDataUnread_WindowGivenBack_UploadsGoOn()

        /// <summary>
        /// The client sends a whole window's worth, 1 MiB, into a CONNECT tunnel,
        /// then resets it with RST_STREAM CANCEL, while the tunnel's handler, which
        /// honours its token, has read nothing. The window is given back once the
        /// reset is handled, and the next upload on the connection goes through.
        /// </summary>
        [Test]
        public async Task ResetWithTunnelDataUnread_WindowGivenBack_UploadsGoOn()
        {

            const Int32 chunks = 64;

            var tunnelOpen    = new TaskCompletionSource(Async);
            var handlerEnded  = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: Tunnel(async (tunnel, cancellationToken) => {

                    try
                    {
                        tunnelOpen.TrySetResult();
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    finally
                    {
                        handlerEnded.TrySetResult();
                    }

                }));

            await peer.RequestTunnelAsync(1, "tunnel.example:443");
            await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await UploadAsync(peer, 1, chunks);
            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));

            await handlerEnded.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);
            await peer.PingAsync();

            var heldBack      = peer.ConnectionWindowHeldBack();
            var clientWindow  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

            Assert.Multiple(() =>
            {

                Assert.That(heldBack,      Is.EqualTo(0),        "connection window held back for the tunnel's unread data, once the reset is handled");
                Assert.That(clientWindow,  Is.EqualTo(Window),   "the client's connection window once the reset is handled");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.EqualTo(new[] { "HEADERS :status 200" }),
                            "what the server sent on the reset stream");

            });

            await peer.RequestAsync(3, "/other", EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateData(3, new Byte[ChunkSize], EndStream: true));

            var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(other?.Status,  Is.EqualTo("200"),                "status of the next upload's response");
                Assert.That(other?.Body,    Is.EqualTo(ChunkSize.ToString()), "length of the next upload, as the server read it");
            });

        }

        #endregion

        #region TunnelReadAfterReset_WindowGivenBackOnce()

        /// <summary>
        /// A reset cuts a tunnel short while it holds a whole window's worth that
        /// its handler has not read. The window is given back once the reset is
        /// handled. The handler then reads on without its token: it gets what
        /// arrived, in order, and then null, the end of the tunnel, as before. The
        /// reads give nothing back a second time, and no stream-level
        /// WINDOW_UPDATE goes out on the reset stream.
        /// </summary>
        [Test]
        public async Task TunnelReadAfterReset_WindowGivenBackOnce()
        {

            const Int32 chunks = 64;

            var tunnelOpen  = new TaskCompletionSource(Async);
            var readOn      = new TaskCompletionSource(Async);
            var tunnelRead  = new TaskCompletionSource<(Int32 Chunks, Int64 Bytes, Exception? End)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: Tunnel(async (tunnel, cancellationToken) => {

                    tunnelOpen.TrySetResult();

                    await readOn.Task;

                    tunnelRead.TrySetResult(await ReadToEndAsync(tunnel));

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await UploadAsync(peer, 1, chunks);
                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));
                await peer.PingAsync();

                var heldBackAtReset      = peer.ConnectionWindowHeldBack();
                var clientWindowAtReset  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

                readOn.TrySetResult();

                var (chunksRead, bytesRead, end) = await tunnelRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackAtReset,                                   Is.EqualTo(0),                   "connection window held back once the reset is handled, before the handler has read anything");
                    Assert.That(clientWindowAtReset,                               Is.EqualTo(Window),              "the client's connection window once the reset is handled");

                    Assert.That(chunksRead,                                        Is.EqualTo(chunks),              "chunks the handler read after the reset: all that arrived");
                    Assert.That(bytesRead,                                         Is.EqualTo(chunks * ChunkSize),  "bytes the handler read after the reset");
                    Assert.That(end,                                               Is.Null,                         "how the tunnel ended: with null, as a reset tunnel does");

                    Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                   "connection window held back once the handler has read it all: given back once, not twice");
                    Assert.That(ClientConnectionWindow(peer, chunks * ChunkSize),  Is.EqualTo(Window),              "the client's connection window: no more than the server takes in");
                    Assert.That(WindowUpdatesOn(peer, 1),                          Is.Empty,                        "stream-level WINDOW_UPDATEs on the reset stream");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region TunnelHandlerFailsWithDataUnread_WindowGivenBack()

        /// <summary>
        /// A tunnel's handler fails with two chunks unread, and the server resets
        /// the stream on the handler's task, with no END_STREAM before it. The
        /// window of the two chunks is given back.
        /// </summary>
        [Test]
        public async Task TunnelHandlerFailsWithDataUnread_WindowGivenBack()
        {

            var tunnelOpen  = new TaskCompletionSource(Async);
            var fail        = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                Other,

                ConnectHandler: Tunnel(async (tunnel, cancellationToken) => {

                    tunnelOpen.TrySetResult();

                    await fail.Task;

                    throw new InvalidOperationException("The tunnel's handler failed on purpose");

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));

                // Both chunks wait in the tunnel's channel before the reset comes.
                await peer.PingAsync();

                fail.TrySetResult();

                var reset = await peer.ReadToResetAsync(1).WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.PingAsync();

                var heldBack = await HeldBackOnceSettledAsync(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(reset,     Is.EqualTo(HTTP2ErrorCode.INTERNAL_ERROR), "how the server reset the stream");
                    Assert.That(heldBack,  Is.EqualTo(0),                             "connection window held back for the chunks nobody will read");

                    Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "RST_STREAM INTERNAL_ERROR" }),
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
