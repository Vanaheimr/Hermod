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
    /// The connection window of request-body and tunnel bytes that nothing will
    /// read, on a stream that is not reset. A streaming handler reads its body,
    /// and a tunnel handler its tunnel, from a channel that the read loop fills as
    /// DATA arrives, and the window of each chunk is given back only when it is
    /// read. A reset gives back what is unread. But a stream can end without one:
    /// the handler answers early and returns, or fails and is answered with a 500,
    /// or no handler is started at all — a streaming request refused with 421 or
    /// 425, a CONNECT refused. What was left unread, and every chunk the client
    /// sent after, withheld its window for good, and with a whole window's worth,
    /// every later upload on the connection stalled.
    ///
    /// Once nothing reads the stream any more, the window of what is unread is
    /// given back, and later DATA is dropped, its window given back at once,
    /// without growing the channel. Once the response is complete while the
    /// client's side is still open, the server asks the client to stop sending,
    /// with RST_STREAM NO_ERROR (RFC 9113, Section 8.1).
    /// </summary>
    [TestFixture]
    public class ServerUnreadWindowWithoutReaderTests
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
        /// 1 MiB at once here (see <see cref="StartAsync"/>), and each stream's,
        /// its INITIAL_WINDOW_SIZE of 1 MiB.
        /// </summary>
        private const Int64 Window = 1024 * 1024;

        /// <summary>
        /// Start a server connection that grants the client a connection window of
        /// one stream window, <see cref="Window"/>, as it did by default before
        /// that became four: one stream can take all of it then, and window that
        /// is not given back stops every later upload on the connection — which
        /// the tests here look for.
        /// </summary>
        private static Task<PipedH2ServerConnection> StartAsync(HTTP2RequestHandler     RequestHandler,
                                                                HTTP2StreamingHandler?  StreamingHandler   = null,
                                                                HTTP2ConnectHandler?    ConnectHandler     = null,
                                                                Func<String, Boolean>?  IsAuthorityServed  = null)

            => PipedH2ServerConnection.StartAsync(RequestHandler,
                                                  StreamingHandler:      StreamingHandler,
                                                  ConnectHandler:        ConnectHandler,
                                                  IsAuthorityServed:     IsAuthorityServed,
                                                  ConnectionWindowSize:  (Int32) Window);

        /// <summary>
        /// The client's receive windows, RFC 9113's default: the client here never
        /// changes them, and grants the server more only where a test says so.
        /// </summary>
        private const Int32 ClientWindow = 65_535;

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
        /// Answer 200 with the length of the whole request body.
        /// </summary>
        private static async Task EchoLengthAsync(IHTTP2RequestStream Request, IHTTP2ResponseStream Response)
        {

            var (_, bytes, end) = await ReadToEndAsync(Request);

            await Response.WriteHeadersAsync([(":status", end is null ? "200" : "500")]);
            await Response.WriteAsync(ASCII(bytes.ToString()));

        }

        /// <summary>
        /// Answers every request that is not streamed, or is no CONNECT, with the
        /// length of its body.
        /// </summary>
        private static Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Other(UInt32                             StreamId,
                                                                                                            List<(String Name, String Value)>  Headers,
                                                                                                            Byte[]?                            Body,
                                                                                                            CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], ASCII((Body?.Length ?? 0).ToString())));

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
        /// What the server sent on this stream, in order, DATA with its length
        /// rather than its bytes.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.Frame.Type == HTTP2FrameType.DATA
                                                               ? $"DATA {frame.Frame.Payload.Length}{(frame.Frame.EndStream ? " END_STREAM" : "")}"
                                                               : frame.ToString())];

        /// <summary>
        /// The response on this stream: read on until the server has ended its
        /// side, unless an earlier read, a ping's say, has read that far already
        /// — or null, if the server did not end it in time.
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
        /// for frames the writer loop sends, which a single ping may not wait for.
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
        /// How a write the handler did not wait for has ended, once it has:
        /// "completed", "canceled", or the type of what it failed with. Its task
        /// ends a moment after its bytes are taken to be sent, not with them.
        /// </summary>
        private static async Task<String> OutcomeOf(Task Write)
        {

            try
            {
                await Write.WaitAsync(PipedH2ServerConnection.StepTimeout);
                return "completed";
            }
            catch (OperationCanceledException) when (Write.IsCanceled)
            {
                return "canceled";
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }

        }

        /// <summary>
        /// The DATA bytes the server sent on this stream, as read so far.
        /// </summary>
        private static Int64 DataSentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => Peer.FramesOn(StreamId).Where(frame => frame.Frame.Type == HTTP2FrameType.DATA).
                                       Sum  (frame => (Int64) frame.Frame.Payload.Length);

        /// <summary>
        /// The connection window held back, once the end of the reading, on a task
        /// of the server's own, has been handled there: no frame tells the client
        /// when that is, so this waits until nothing is held back, and gives up
        /// after the step timeout with what still is.
        /// </summary>
        private static async Task<Int64> HeldBackOnceSettledAsync(PipedH2ServerConnection Peer)
        {

            var waited = Stopwatch.StartNew();

            while (Peer.ConnectionWindowHeldBack() != 0 && waited.Elapsed < PipedH2ServerConnection.StepTimeout)
                await Task.Delay(1);

            return Peer.ConnectionWindowHeldBack();

        }

        /// <summary>
        /// POST one chunk on a new stream, for <paramref name="Authority"/>, and
        /// return the response: 200 with the length the server read.
        /// </summary>
        private static async Task<PipedH2ServerConnection.Response?> NextUploadAsync(PipedH2ServerConnection  Peer,
                                                                                     UInt32                   StreamId,
                                                                                     String                   Authority   = "localhost")
        {

            await Peer.SendHeadersAsync(StreamId, [(":method",    "POST"),
                                                   (":scheme",    "http"),
                                                   (":authority", Authority),
                                                   (":path",      "/echo")],
                                        EndStream: false);

            await Peer.SendAsync(HTTP2Frame.CreateData(StreamId, new Byte[ChunkSize], EndStream: true));

            return await Peer.TryResponseAsync(StreamId, PipedH2ServerConnection.StepTimeout);

        }

        #endregion


        #region HandlerAnswersEarly_UploadAfterwards_WindowGivenBack()

        /// <summary>
        /// A streaming handler answers 200 "early" and returns, without reading the
        /// body the client has only begun to send. The server asks the client to
        /// stop with RST_STREAM NO_ERROR once the response is complete. The four
        /// chunks and the end of the body that the client had sent before it read
        /// that are discarded, and their connection window given back: they used
        /// to go into the body channel on the half-closed stream, where nothing
        /// read them, and 64 KiB of the window stayed held back.
        /// </summary>
        [Test]
        public async Task HandlerAnswersEarly_UploadAfterwards_WindowGivenBack()
        {

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) != "/upload")
                    {
                        await EchoLengthAsync(request, response);
                        return;
                    }

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.WriteAsync(ASCII("early"));

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);

            var early = await ResponseOn(peer, 1);
            var reset = await ResetOn(peer, 1);

            await UploadAsync(peer, 1, 4);
            await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
            await peer.PingAsync();

            var next = await NextUploadAsync(peer, 3);

            Assert.Multiple(() =>
            {

                Assert.That(early?.Status,                    Is.EqualTo("200"),                    "status of the early response");
                Assert.That(early?.Body,                      Is.EqualTo("early"),                  "body of the early response");
                Assert.That(reset,                            Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the upload once its response was complete");

                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                        "connection window held back for the chunks nobody will read");

                Assert.That(SentOn(peer, 1),
                            Is.EqualTo(new[] { "HEADERS :status 200",
                                               "DATA 5",
                                               "DATA 0 END_STREAM",
                                               "RST_STREAM NO_ERROR" }),
                            "what the server sent on the stream: nothing for the upload that crossed its RST_STREAM");

                Assert.That(next?.Status,                     Is.EqualTo("200"),                    "status of the next upload's response");
                Assert.That(next?.Body,                       Is.EqualTo(ChunkSize.ToString()),     "length of the next upload, as the server read it");

            });

        }

        #endregion

        #region HandlerReturnsWithBodyUnread_WindowGivenBack_UploadStopped(Chunks, CompletesFirst)

        /// <summary>
        /// The client uploads, and the handler answers and returns without reading
        /// any of it. The connection window of the whole upload is given back once
        /// the handler has returned: with a whole window's worth unread (64 chunks,
        /// 1 MiB), in a WINDOW_UPDATE right away, and with less, set aside for the
        /// next one, as any window is. The server stops the upload with RST_STREAM
        /// NO_ERROR, after its END_STREAM, and discards the client's END_STREAM
        /// that crossed it. The next upload on the connection goes through.
        ///
        /// Two orders: the handler returns and leaves its response to be ended
        /// for it, and the writer loop, which ends it, sends the RST_STREAM; or
        /// the handler ends its response and returns only once the END_STREAM is on
        /// the wire, and the end of the reading sends it.
        /// </summary>
        [TestCase(4,  false)]
        [TestCase(64, false)]
        [TestCase(64, true)]
        public async Task HandlerReturnsWithBodyUnread_WindowGivenBack_UploadStopped(Int32 Chunks, Boolean CompletesFirst)
        {

            var answered   = new TaskCompletionSource(Async);
            var returnNow  = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) != "/upload")
                    {
                        await EchoLengthAsync(request, response);
                        return;
                    }

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.WriteAsync(ASCII("early"));

                    if (CompletesFirst)
                        await response.CompleteAsync();

                    answered.TrySetResult();

                    await returnNow.Task;

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await answered.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await UploadAsync(peer, 1, Chunks);
                await peer.PingAsync();

                var stream          = peer.ServerStream(1);
                var heldBackBefore  = peer.ConnectionWindowHeldBack();

                PipedH2ServerConnection.Response? early = null;

                if (CompletesFirst)
                {

                    early = await ResponseOn(peer, 1);

                    // The writer loop's half-close follows its END_STREAM.
                    await UntilAsync(() => stream.State == HTTP2StreamState.HalfClosedLocal, "the server has ended its side of the stream");

                }

                returnNow.TrySetResult();

                early ??= await ResponseOn(peer, 1);

                var reset = await ResetOn(peer, 1);

                await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
                await peer.PingAsync();

                var heldBack      = peer.ConnectionWindowHeldBack();
                var clientWindow  = ClientConnectionWindow(peer, Sent: Chunks * ChunkSize);

                var next          = await NextUploadAsync(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackBefore,  Is.EqualTo(Chunks * ChunkSize),     "connection window held back while the handler ran: the upload, unread");

                    Assert.That(early?.Body,     Is.EqualTo("early"),                "body of the early response");
                    Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR), "how the server stopped the upload once its response was complete");

                    Assert.That(heldBack,        Is.EqualTo(0),                      "connection window held back once the handler has returned");
                    Assert.That(clientWindow,    Chunks * ChunkSize >= Window / 2
                                                     ? Is.EqualTo(Window)
                                                     : Is.GreaterThan(Window / 2),   "the client's connection window: never less than half of it once the handler has returned");

                    Assert.That(SentOn(peer, 1),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA 5",
                                                   "DATA 0 END_STREAM",
                                                   "RST_STREAM NO_ERROR" }),
                                "what the server sent on the stream");

                    Assert.That(WindowUpdatesOn(peer, 1),  Is.Empty,                      "stream-level WINDOW_UPDATEs: nothing was read");

                    Assert.That(next?.Status,    Is.EqualTo("200"),                  "status of the next upload's response");
                    Assert.That(next?.Body,      Is.EqualTo(ChunkSize.ToString()),   "length of the next upload, as the server read it");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

        }

        #endregion

        #region HandlerReturnsBeforeItsResponseEnds_LaterDataDropped(ClientResets)

        /// <summary>
        /// The handler writes a response larger than the client's window, 100 KiB,
        /// and returns: its response cannot end before the client grants more
        /// window. Nothing reads the body from then on, while the stream is still
        /// open. The four chunks the client sends now are dropped, not put into
        /// the body channel, and their connection window is given back at once;
        /// the stream's is not, for nothing will read on it.
        ///
        /// Then either the client grants the window, the response ends, and the
        /// writer loop, which ends it, stops the upload with RST_STREAM NO_ERROR;
        /// or the client resets the stream itself, and its reset gives back no
        /// window a second time.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task HandlerReturnsBeforeItsResponseEnds_LaterDataDropped(Boolean ClientResets)
        {

            const Int32 responseSize = 100 * 1024;

            var written = new TaskCompletionSource<Task>(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    // Not waited for: it waits for the client's window.
                    written.TrySetResult(response.WriteAsync(new Byte[responseSize]));

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);

            // Once the ping is answered, the read loop has opened the stream.
            await peer.PingAsync();

            var stream  = peer.ServerStream(1);
            var body    = stream.RequestBodyChannel!;

            await UntilAsync(() => body.Reader.Completion.IsCompleted, "nothing reads the body any more: its channel is completed");

            await UploadAsync(peer, 1, 4);
            await peer.PingAsync();

            var heldBackAfterUpload  = peer.ConnectionWindowHeldBack();
            var chunksInChannel      = body.Reader.Count;

            await PingUntilAsync(peer, () => DataSentOn(peer, 1) == ClientWindow, "the server has sent as much of its response as the client's window lets it");

            var sentBeforeTheEnd     = SentOn(peer, 1);

            HTTP2ErrorCode? reset = null;

            if (ClientResets)
            {
                await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.CANCEL));
            }
            else
            {

                await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(0, responseSize));
                await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, responseSize));

                await ResponseOn(peer, 1);

                reset = await ResetOn(peer, 1);

            }

            await peer.PingAsync();

            var write = await OutcomeOf(await written.Task.WaitAsync(PipedH2ServerConnection.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(heldBackAfterUpload,                               Is.EqualTo(0),                          "connection window held back for the chunks sent once nothing read the body");
                Assert.That(chunksInChannel,                                   Is.EqualTo(0),                          "chunks put into the body channel once nothing read it");
                Assert.That(stream.ReceivedBodyLength,                         Is.EqualTo(4 * ChunkSize),              "the chunks, counted on the open stream");
                Assert.That(stream.RecvWindow,                                 Is.EqualTo(Window - 4 * ChunkSize),     "the stream's window, charged for them and never given back: nothing reads on it");

                Assert.That(sentBeforeTheEnd,                                  Is.EqualTo(new[] { "HEADERS :status 200",
                                                                                                  "DATA 16384",
                                                                                                  "DATA 16384",
                                                                                                  "DATA 16384",
                                                                                                  "DATA 16383" }),     "what the server sent before the client granted more window: the client's 65 535 bytes");

                Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                          "connection window held back in the end");
                Assert.That(ClientConnectionWindow(peer, 4 * ChunkSize),       Is.EqualTo(Window - 4 * ChunkSize),     "the client's connection window: the chunks' given back once, and set aside for the next WINDOW_UPDATE");
                Assert.That(WindowUpdatesOn(peer, 1),                          Is.Empty,                               "stream-level WINDOW_UPDATEs");

                if (ClientResets)
                {
                    Assert.That(SentOn(peer, 1).Where(frame => frame.StartsWith("RST_STREAM")),  Is.Empty,                              "the server's own RST_STREAM on a stream the client reset");
                    Assert.That(write,                                                           Is.EqualTo("canceled"),                               "the handler's write, once the client reset the stream");
                }
                else
                {
                    Assert.That(reset,                                                           Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the upload once its response was complete");
                    Assert.That(SentOn(peer, 1).TakeLast(2),                                     Is.EqualTo(new[] { "DATA 0 END_STREAM",
                                                                                                                    "RST_STREAM NO_ERROR" }),  "how the server ended the stream");
                    Assert.That(write,                                                           Is.EqualTo("completed"),                               "the handler's write");
                }

            });

        }

        #endregion

        #region ClientEndedItsSide_HandlerReturnsUnread_WindowGivenBack()

        /// <summary>
        /// The client uploads a whole window's worth and ends the body; the handler
        /// answers without reading any of it. The connection window of the body is
        /// given back once the handler has returned, and the next upload goes
        /// through. Both sides have ended the stream: no RST_STREAM.
        /// </summary>
        [Test]
        public async Task ClientEndedItsSide_HandlerReturnsUnread_WindowGivenBack()
        {

            const Int32 chunks = 64;

            var returnNow = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) != "/upload")
                    {
                        await EchoLengthAsync(request, response);
                        return;
                    }

                    await returnNow.Task;

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.WriteAsync(ASCII("ignored"));

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
                await peer.PingAsync();

                var heldBackBefore = peer.ConnectionWindowHeldBack();

                returnNow.TrySetResult();

                var answer = await ResponseOn(peer, 1);

                await peer.PingAsync();

                var heldBack      = peer.ConnectionWindowHeldBack();
                var clientWindow  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

                var next          = await NextUploadAsync(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackBefore,  Is.EqualTo(Window),                "connection window held back while the handler ran: the whole body, unread");
                    Assert.That(answer?.Body,    Is.EqualTo("ignored"),             "body of the response");

                    Assert.That(heldBack,        Is.EqualTo(0),                     "connection window held back once the handler has returned");
                    Assert.That(clientWindow,    Is.EqualTo(Window),                "the client's connection window once the handler has returned");

                    Assert.That(SentOn(peer, 1),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA 7",
                                                   "DATA 0 END_STREAM" }),
                                "what the server sent on the stream: no RST_STREAM, as both sides have ended it");

                    Assert.That(next?.Status,    Is.EqualTo("200"),                 "status of the next upload's response");
                    Assert.That(next?.Body,      Is.EqualTo(ChunkSize.ToString()),  "length of the next upload, as the server read it");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

        }

        #endregion

        #region HandlerFailsBeforeItsHeaders_500_WindowGivenBack()

        /// <summary>
        /// The handler fails before it has sent its response headers, with four
        /// chunks of the body unread. The server answers 500, a complete response,
        /// gives back the window of the four chunks, and stops the upload with
        /// RST_STREAM NO_ERROR. It used to answer the 500 and hold back the window.
        /// </summary>
        [Test]
        public async Task HandlerFailsBeforeItsHeaders_500_WindowGivenBack()
        {

            const Int32 chunks = 4;

            var fail = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await fail.Task;

                    throw new InvalidOperationException("The handler failed on purpose");

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.PingAsync();

                var heldBackBefore = peer.ConnectionWindowHeldBack();

                fail.TrySetResult();

                var answer    = await ResponseOn(peer, 1);
                var reset     = await ResetOn(peer, 1);

                await peer.PingAsync();

                var heldBack  = await HeldBackOnceSettledAsync(peer);

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackBefore,  Is.EqualTo(chunks * ChunkSize),     "connection window held back while the handler ran: the upload, unread");

                    Assert.That(answer?.Status,  Is.EqualTo("500"),                  "status of the response to the failed handler");
                    Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR), "how the server stopped the upload once the 500 was complete");
                    Assert.That(heldBack,        Is.EqualTo(0),                      "connection window held back once the handler has failed");

                    Assert.That(SentOn(peer, 1),
                                Is.EqualTo(new[] { "HEADERS :status 500, content-type text/plain",
                                                   "DATA 21",
                                                   "DATA 0 END_STREAM",
                                                   "RST_STREAM NO_ERROR" }),
                                "what the server sent on the stream");

                });

            }
            finally
            {
                fail.TrySetResult();
            }

        }

        #endregion

        #region TooEarly_NoHandler_UploadWindowGivenBack(Chunks)

        /// <summary>
        /// A POST the client marks as early data (RFC 8470), which the server does
        /// not take the replay risk for: it answers 425 on the streaming path and
        /// starts no handler, so nothing ever reads the upload the client sends
        /// with it. Its connection window is given back — the chunks that came
        /// before the refusal was decided, and after — and the server stops the
        /// upload with RST_STREAM NO_ERROR once the 425 is complete. The next
        /// upload goes through.
        /// </summary>
        [TestCase(4)]
        [TestCase(64)]
        public async Task TooEarly_NoHandler_UploadWindowGivenBack(Int32 Chunks)
        {

            var handlerRan = false;

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) == "/upload")
                        handlerRan = true;

                    await EchoLengthAsync(request, response);

                });

            await peer.SendHeadersAsync(1, [(":method",    "POST"),
                                            (":scheme",    "http"),
                                            (":authority", "localhost"),
                                            (":path",      "/upload"),
                                            ("early-data", "1")],
                                        EndStream: false);

            await UploadAsync(peer, 1, Chunks);

            var answer = await ResponseOn(peer, 1);
            var reset  = await ResetOn(peer, 1);

            await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
            await peer.PingAsync();

            var heldBack  = await HeldBackOnceSettledAsync(peer);
            var next      = await NextUploadAsync(peer, 3);

            Assert.Multiple(() =>
            {

                Assert.That(answer?.Status,  Is.EqualTo("425"),                  "status of the answer to early data");
                Assert.That(handlerRan,      Is.False,                           "whether the handler ran for the request refused");
                Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR), "how the server stopped the upload once the 425 was complete");
                Assert.That(heldBack,        Is.EqualTo(0),                      "connection window held back for the upload nothing reads");

                Assert.That(SentOn(peer, 1),
                            Is.EqualTo(new[] { "HEADERS :status 425, content-type text/plain, cache-control no-store",
                                               "DATA 9 END_STREAM",
                                               "RST_STREAM NO_ERROR" }),
                            "what the server sent on the stream");

                Assert.That(next?.Status,    Is.EqualTo("200"),                  "status of the next upload's response");
                Assert.That(next?.Body,      Is.EqualTo(ChunkSize.ToString()),   "length of the next upload, as the server read it");

            });

        }

        #endregion

        #region Misdirected_NoHandler_UploadWindowGivenBack()

        /// <summary>
        /// A request, with an upload, for an origin the server does not answer for:
        /// it answers 421 on the streaming path and starts no handler, so nothing
        /// ever reads the upload. Its connection window is given back, a whole
        /// window's worth, and the server stops the upload with RST_STREAM NO_ERROR
        /// once the 421 is complete. The next upload, for an origin the server
        /// answers for, goes through.
        /// </summary>
        [Test]
        public async Task Misdirected_NoHandler_UploadWindowGivenBack()
        {

            const Int32 chunks = 64;

            var handlerRan = false;

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (PathOf(request) == "/upload")
                        handlerRan = true;

                    await EchoLengthAsync(request, response);

                },

                IsAuthorityServed: authority => authority == "served.example");

            // For localhost, which the server does not answer for.
            await peer.RequestAsync(1, "/upload", EndStream: false);
            await UploadAsync(peer, 1, chunks);

            var answer = await ResponseOn(peer, 1);
            var reset  = await ResetOn(peer, 1);

            await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
            await peer.PingAsync();

            var heldBack  = await HeldBackOnceSettledAsync(peer);
            var next      = await NextUploadAsync(peer, 3, Authority: "served.example");

            Assert.Multiple(() =>
            {

                Assert.That(answer?.Status,  Is.EqualTo("421"),                  "status of the answer for an origin the server does not answer for");
                Assert.That(handlerRan,      Is.False,                           "whether the handler ran for the request refused");
                Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR), "how the server stopped the upload once the 421 was complete");
                Assert.That(heldBack,        Is.EqualTo(0),                      "connection window held back for the upload nothing reads");

                Assert.That(SentOn(peer, 1),
                            Is.EqualTo(new[] { "HEADERS :status 421, content-type text/plain",
                                               "DATA 19 END_STREAM",
                                               "RST_STREAM NO_ERROR" }),
                            "what the server sent on the stream");

                Assert.That(next?.Status,    Is.EqualTo("200"),                  "status of the next upload's response");
                Assert.That(next?.Body,      Is.EqualTo(ChunkSize.ToString()),   "length of the next upload, as the server read it");

            });

        }

        #endregion

        #region ConnectRefused_TunnelDataWindowGivenBack(Status)

        /// <summary>
        /// The client sends tunnel data right after its CONNECT, as it may, and the
        /// server refuses the tunnel: with 403, the connect handler's answer, given
        /// once the data has come; or with 501, when no connect handler is
        /// registered. No tunnel handler ever reads the data. Its connection window
        /// is given back, and the server stops the client's side of the stream
        /// with RST_STREAM NO_ERROR once its refusal is out.
        /// </summary>
        [TestCase(403)]
        [TestCase(501)]
        public async Task ConnectRefused_TunnelDataWindowGivenBack(Int32 Status)
        {

            const Int32 chunks = 4;

            var decide = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                Other,

                ConnectHandler: Status == 501
                                    ? null
                                    : async (streamId, headers, cancellationToken) => {

                                          await decide.Task;

                                          return new HTTP2ConnectResult { StatusCode = (UInt16) Status };

                                      });

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await UploadAsync(peer, 1, chunks);
                await peer.PingAsync();

                var heldBackBefore = peer.ConnectionWindowHeldBack();

                decide.TrySetResult();

                var answer    = await ResponseOn(peer, 1);
                var reset     = await ResetOn(peer, 1);

                await peer.PingAsync();

                var heldBack  = await HeldBackOnceSettledAsync(peer);

                Assert.Multiple(() =>
                {

                    if (Status == 403)
                        Assert.That(heldBackBefore,  Is.EqualTo(chunks * ChunkSize),  "connection window held back while the connect handler decided: the tunnel data, unread");

                    Assert.That(answer?.Status,  Is.EqualTo(Status.ToString()),       "status of the refusal");
                    Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the client's side once the refusal was out");
                    Assert.That(heldBack,        Is.EqualTo(0),                       "connection window held back for the tunnel data nothing reads");

                    Assert.That(SentOn(peer, 1),
                                Is.EqualTo(new[] { $"HEADERS :status {Status} END_STREAM",
                                                   "RST_STREAM NO_ERROR" }),
                                "what the server sent on the stream");

                });

            }
            finally
            {
                decide.TrySetResult();
            }

        }

        #endregion

        #region TunnelHandlerReturnsWithDataUnread_WindowGivenBack_TunnelStopped()

        /// <summary>
        /// The client sends a whole window's worth, 1 MiB, into a CONNECT tunnel,
        /// and the tunnel's handler returns without reading any of it. The window
        /// is given back once the handler has returned, the server ends its side
        /// of the tunnel and then stops the client's with RST_STREAM NO_ERROR, and
        /// the next upload on the connection goes through.
        /// </summary>
        [Test]
        public async Task TunnelHandlerReturnsWithDataUnread_WindowGivenBack_TunnelStopped()
        {

            const Int32 chunks = 64;

            var tunnelOpen  = new TaskCompletionSource(Async);
            var returnNow   = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                Other,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    RunAsync    = async (tunnel, cancellationToken) => {
                                      tunnelOpen.TrySetResult();
                                      await returnNow.Task;
                                  }

                }));

            try
            {

                await peer.RequestTunnelAsync(1, "tunnel.example:443");
                await tunnelOpen.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await UploadAsync(peer, 1, chunks);
                await peer.PingAsync();

                var heldBackBefore = peer.ConnectionWindowHeldBack();

                returnNow.TrySetResult();

                var answer = await ResponseOn(peer, 1);
                var reset  = await ResetOn(peer, 1);

                await peer.PingAsync();

                var heldBack      = peer.ConnectionWindowHeldBack();
                var clientWindow  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

                await peer.RequestAsync(3, "/other", EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(3, new Byte[ChunkSize], EndStream: true));

                var other = await peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackBefore,  Is.EqualTo(Window),                  "connection window held back while the tunnel's handler ran: all it had not read");

                    Assert.That(answer?.Status,  Is.EqualTo("200"),                   "status of the tunnel");
                    Assert.That(reset,           Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the client's side once its own had ended");

                    Assert.That(heldBack,        Is.EqualTo(0),                       "connection window held back once the tunnel's handler has returned");
                    Assert.That(clientWindow,    Is.EqualTo(Window),                  "the client's connection window once the tunnel's handler has returned");

                    Assert.That(SentOn(peer, 1),
                                Is.EqualTo(new[] { "HEADERS :status 200",
                                                   "DATA 0 END_STREAM",
                                                   "RST_STREAM NO_ERROR" }),
                                "what the server sent on the stream");

                    Assert.That(other?.Status,   Is.EqualTo("200"),                   "status of the next upload's response");
                    Assert.That(other?.Body,     Is.EqualTo(ChunkSize.ToString()),    "length of the next upload, as the server read it");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

        }

        #endregion

        #region TunnelHandlerReturnsBeforeItsLastBytesGoOut_LaterDataDropped()

        /// <summary>
        /// The tunnel's handler writes more than the client's window, 100 KiB, and
        /// returns: the server's END_STREAM waits behind those bytes until the
        /// client grants more window. Nothing reads the tunnel from then on. The
        /// four chunks the client sends meanwhile are dropped, not put into the
        /// tunnel's channel, and their connection window is given back at once.
        /// Once the client grants the window, the server ends its side and stops
        /// the client's with RST_STREAM NO_ERROR.
        /// </summary>
        [Test]
        public async Task TunnelHandlerReturnsBeforeItsLastBytesGoOut_LaterDataDropped()
        {

            const Int32 tunnelBytes = 100 * 1024;

            var written = new TaskCompletionSource<Task>(Async);

            await using var peer = await StartAsync(

                Other,

                ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                    StatusCode  = 200,

                    // The write is not waited for: it waits for the client's window.
                    RunAsync    = (tunnel, cancellationToken) => {
                                      written.TrySetResult(tunnel.WriteAsync(new Byte[tunnelBytes], CancellationToken.None));
                                      return Task.CompletedTask;
                                  }

                }));

            await peer.RequestTunnelAsync(1, "tunnel.example:443");

            // Once the ping is answered, the read loop has opened the stream.
            await peer.PingAsync();

            var stream   = peer.ServerStream(1);
            var inbound  = stream.TunnelInbound!;

            await UntilAsync(() => inbound.Reader.Completion.IsCompleted, "nothing reads the tunnel any more: its channel is completed");

            await UploadAsync(peer, 1, 4);
            await peer.PingAsync();

            var heldBackAfterUpload  = peer.ConnectionWindowHeldBack();
            var chunksInChannel      = inbound.Reader.Count;

            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(0, tunnelBytes));
            await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, tunnelBytes));

            await ResponseOn(peer, 1);

            var reset = await ResetOn(peer, 1);

            await peer.PingAsync();

            var write = await OutcomeOf(await written.Task.WaitAsync(PipedH2ServerConnection.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(heldBackAfterUpload,              Is.EqualTo(0),                          "connection window held back for the chunks sent once nothing read the tunnel");
                Assert.That(chunksInChannel,                  Is.EqualTo(0),                          "chunks put into the tunnel's channel once nothing read it");
                Assert.That(stream.RecvWindow,                Is.EqualTo(Window - 4 * ChunkSize),     "the stream's window, charged for them and never given back: nothing reads on it");

                Assert.That(reset,                            Is.EqualTo(HTTP2ErrorCode.NO_ERROR),    "how the server stopped the client's side once its own had ended");
                Assert.That(SentOn(peer, 1).TakeLast(2),      Is.EqualTo(new[] { "DATA 0 END_STREAM",
                                                                                 "RST_STREAM NO_ERROR" }), "how the server ended the stream");

                Assert.That(write,                            Is.EqualTo("completed"),                "the handler's write");
                Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                          "connection window held back in the end");

            });

        }

        #endregion

        #region LateReadAfterTheHandlerReturned_GivesNothingBack(ClientEnded)

        /// <summary>
        /// The handler hands its request to a task of its own, answers, and
        /// returns, with a whole window's worth unread, 1 MiB. The window is given
        /// back once the handler has returned. The task then reads: it gets the
        /// chunks that had arrived, in order, and gives nothing back a second
        /// time, so the client is never granted more window than the server can
        /// take in. The body ends as it stood when the handler returned: whole,
        /// if the client had ended it; else the read fails, as nothing more is
        /// taken in — never null, which would pass a truncated body off as whole.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task LateReadAfterTheHandlerReturned_GivesNothingBack(Boolean ClientEnded)
        {

            const Int32 chunks = 64;

            var returnNow  = new TaskCompletionSource(Async);
            var readOn     = new TaskCompletionSource(Async);
            var lateRead   = new TaskCompletionSource<(Int32 Chunks, Int64 Bytes, Exception? End)>(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await returnNow.Task;

                    _ = Task.Run(async () => {
                            await readOn.Task;
                            lateRead.TrySetResult(await ReadToEndAsync(request));
                        });

                    await response.WriteHeadersAsync([(":status", "202")]);

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);

                if (ClientEnded)
                    await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));

                await peer.PingAsync();

                returnNow.TrySetResult();

                var answer = await ResponseOn(peer, 1);

                await peer.PingAsync();

                var heldBackAtReturn      = peer.ConnectionWindowHeldBack();
                var clientWindowAtReturn  = ClientConnectionWindow(peer, Sent: chunks * ChunkSize);

                readOn.TrySetResult();

                var (chunksRead, bytesRead, end) = await lateRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(answer?.Status,                                    Is.EqualTo("202"),                "status of the response");

                    Assert.That(heldBackAtReturn,                                  Is.EqualTo(0),                    "connection window held back once the handler has returned, before the late read");
                    Assert.That(clientWindowAtReturn,                              Is.EqualTo(Window),               "the client's connection window once the handler has returned");

                    Assert.That(chunksRead,                                        Is.EqualTo(chunks),               "chunks the late read got: all that had arrived");
                    Assert.That(bytesRead,                                         Is.EqualTo(chunks * ChunkSize),   "bytes the late read got");

                    if (ClientEnded)
                        Assert.That(end,                                           Is.Null,                                     "how the body ended: whole, as the client had ended it");
                    else
                        Assert.That(end,                                           Is.InstanceOf<InvalidOperationException>(),  "how the body ended: not whole, and null would say it was");

                    Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                    "connection window held back once the late read is done: given back once, not twice");
                    Assert.That(ClientConnectionWindow(peer, chunks * ChunkSize),  Is.EqualTo(Window),               "the client's connection window: no more than the server takes in");
                    Assert.That(WindowUpdatesOn(peer, 1),                          Is.Empty,                         "stream-level WINDOW_UPDATEs on the stream");

                });

            }
            finally
            {
                returnNow.TrySetResult();
                readOn.TrySetResult();
            }

        }

        #endregion

        #region LateRead_ClientEndsAfterTheHandlerReturned_BodyNotWhole()

        /// <summary>
        /// The handler hands its request to a task of its own, after two chunks
        /// have come, and returns while its response still waits for the client's
        /// window: the stream stays open, and the server does not reset it. The
        /// client then sends two more chunks, which are dropped, and ends the body.
        /// The task reads the two chunks that had come, and then fails: the body
        /// is not whole, and a null, the end of a whole body, would pass it off as
        /// one. The client's END_STREAM ends the body channel no more, once nothing
        /// reads it.
        /// </summary>
        [Test]
        public async Task LateRead_ClientEndsAfterTheHandlerReturned_BodyNotWhole()
        {

            const Int32 responseSize = 100 * 1024;

            var returnNow  = new TaskCompletionSource(Async);
            var readOn     = new TaskCompletionSource(Async);
            var written    = new TaskCompletionSource<Task>(Async);
            var lateRead   = new TaskCompletionSource<(Int32 Chunks, Int64 Bytes, Exception? End)>(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await returnNow.Task;

                    _ = Task.Run(async () => {
                            await readOn.Task;
                            lateRead.TrySetResult(await ReadToEndAsync(request));
                        });

                    await response.WriteHeadersAsync([(":status", "200")]);

                    // Not waited for: it waits for the client's window.
                    written.TrySetResult(response.WriteAsync(new Byte[responseSize]));

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, 2);
                await peer.PingAsync();

                returnNow.TrySetResult();

                // Given back once the handler has returned: its end is the end of
                // the reading.
                var heldBackAtReturn = await HeldBackOnceSettledAsync(peer);

                await UploadAsync(peer, 1, 2);
                await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
                await peer.PingAsync();

                readOn.TrySetResult();

                var (chunksRead, bytesRead, end) = await lateRead.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(0, responseSize));
                await peer.SendAsync(HTTP2Frame.CreateWindowUpdate(1, responseSize));

                await ResponseOn(peer, 1);
                await peer.PingAsync();

                var write = await OutcomeOf(await written.Task.WaitAsync(PipedH2ServerConnection.StepTimeout));

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackAtReturn,                 Is.EqualTo(0),                                  "connection window held back once the handler has returned");

                    Assert.That(chunksRead,                       Is.EqualTo(2),                                  "chunks the late read got: the two that came before the handler returned");
                    Assert.That(bytesRead,                        Is.EqualTo(2 * ChunkSize),                      "bytes the late read got");
                    Assert.That(end,                              Is.InstanceOf<InvalidOperationException>(),     "how the body ended: not whole, although the client ended it, and null would say it was");

                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                                  "connection window held back: the four chunks', each given back once");
                    Assert.That(write,                            Is.EqualTo("completed"),                        "the handler's write");

                    Assert.That(SentOn(peer, 1).Where(frame => frame.StartsWith("RST_STREAM")),
                                Is.Empty,
                                "the server's RST_STREAM: none, as both sides have ended the stream");

                });

            }
            finally
            {
                returnNow.TrySetResult();
                readOn.TrySetResult();
            }

        }

        #endregion

        #region HandlerReadsPartAndReturns_RestGivenBackOnce()

        /// <summary>
        /// The handler reads half of a whole window's worth and returns. Its reads
        /// give back the window of what they read, as always, and the end of the
        /// reading gives back that of the rest: each chunk's once. The client's
        /// connection window ends at exactly 1 MiB.
        /// </summary>
        [Test]
        public async Task HandlerReadsPartAndReturns_RestGivenBackOnce()
        {

            const Int32 chunks = 64;

            var readNow = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await readNow.Task;

                    for (var i = 0; i < chunks / 2; i++)
                        await request.ReadAsync();

                    await response.WriteHeadersAsync([(":status", "200")]);
                    await response.WriteAsync(ASCII("half"));

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await UploadAsync(peer, 1, chunks);
                await peer.PingAsync();

                readNow.TrySetResult();

                var answer  = await ResponseOn(peer, 1);
                var reset   = await ResetOn(peer, 1);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(answer?.Body,                                      Is.EqualTo("half"),                   "body of the response");
                    Assert.That(reset,                                             Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the upload once its response was complete");

                    Assert.That(peer.ConnectionWindowHeldBack(),                   Is.EqualTo(0),                        "connection window held back once the handler has returned");
                    Assert.That(ClientConnectionWindow(peer, chunks * ChunkSize),  Is.EqualTo(Window),                   "the client's connection window: every chunk's given back once");
                    Assert.That(WindowUpdatesOn(peer, 1),                          Is.EqualTo(new[] { "WINDOW_UPDATE 524288" }), "stream-level WINDOW_UPDATEs: for what the handler read, while the client could still send");

                });

            }
            finally
            {
                readNow.TrySetResult();
            }

        }

        #endregion

        #region HandlerReturnsAsAChunkIsHandedOver_WindowGivenBackOnce()

        /// <summary>
        /// The read loop has counted a third chunk as unread, and is about to put
        /// it into the body channel, when the handler returns, and its task gives
        /// back the window of all that is unread, the third chunk's too. The chunk
        /// then finds the channel completed, and is dropped without its window being
        /// given back a second time; the channel keeps the two chunks that came
        /// before. To hold the read loop there, the test swaps the body channel
        /// for one whose write lets the handler return first, and waits until the
        /// handler's task has given the window back.
        /// </summary>
        [Test]
        public async Task HandlerReturnsAsAChunkIsHandedOver_WindowGivenBackOnce()
        {

            var started    = new TaskCompletionSource(Async);
            var returnNow  = new TaskCompletionSource(Async);

            await using var peer = await StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    started.TrySetResult();

                    await returnNow.Task;

                });

            try
            {

                await peer.RequestAsync(1, "/upload", EndStream: false);
                await started.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                var stream                = peer.ServerStream(1);
                var body                  = stream.RequestBodyChannel!;
                var writes                = 0;
                var heldBackAtWrite       = 0L;
                var givenBackBeforeWrite  = false;

                stream.RequestBodyChannel = new InterceptedChannel(body, BeforeWrite: () => {

                    if (++writes < 3)
                        return;

                    // On the read loop, which has counted the third chunk.
                    heldBackAtWrite = peer.ConnectionWindowHeldBack();

                    returnNow.TrySetResult();

                    givenBackBeforeWrite = SpinWait.SpinUntil(() => peer.ConnectionWindowHeldBack() == 0, PipedH2ServerConnection.StepTimeout);

                });

                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 1")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 2")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("part 3")));

                var answer = await ResponseOn(peer, 1);
                var reset  = await ResetOn(peer, 1);

                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(heldBackAtWrite,                  Is.EqualTo(18),                       "connection window held back as the third chunk was written: all three chunks'");
                    Assert.That(givenBackBeforeWrite,             Is.True,                              "all of it given back by the handler's task, the third chunk's too, before that chunk's write");

                    Assert.That(answer?.Status,                   Is.EqualTo("200"),                    "status of the response the server ended for the handler");
                    Assert.That(reset,                            Is.EqualTo(HTTP2ErrorCode.NO_ERROR),  "how the server stopped the upload once its response was complete");

                    Assert.That(peer.ConnectionWindowHeldBack(),  Is.EqualTo(0),                        "connection window held back in the end: given back once for each chunk, the dropped one too");
                    Assert.That(body.Reader.Count,                Is.EqualTo(2),                        "chunks in the body channel: the two that came before the handler returned");

                });

            }
            finally
            {
                returnNow.TrySetResult();
            }

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

    }

}
