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
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// DATA past the length a request declares with content-length. Once its DATA
    /// add up to more than that, no later frame can bring them back to it: the
    /// request is malformed (RFC 9113, Section 8.1.1) at the DATA frame that takes
    /// it past, whatever follows, and the server resets the stream there, with
    /// PROTOCOL_ERROR. It compared the length only where the body ends, at a DATA
    /// frame with END_STREAM or at trailers, and does so still for a body cut
    /// short (see <see cref="ServerContentLengthTests"/>). Until then, a streaming
    /// handler read every byte past the declared length, and a buffered upload was
    /// taken in, up to the server's cap on a request body, only to be refused
    /// where it ended.
    ///
    /// That frame counts against the flow-control windows, as any DATA frame does,
    /// and its connection window is given back, as for every stream error while
    /// the connection lives on (Section 6.9). The connection serves on.
    /// </summary>
    [TestFixture]
    public class ServerContentLengthExceededTests
    {

        #region (helpers)

        /// <summary>
        /// How the server takes in an upload: buffered, to hand it whole to its
        /// request handler, or streamed, to a streaming handler as it arrives.
        /// </summary>
        public enum UploadPath
        {
            Buffered,
            Streamed
        }

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The length every upload here declares.
        /// </summary>
        private const String DeclaredLength = "5";

        /// <summary>
        /// The size of the DATA frames of a large upload: the smallest
        /// SETTINGS_MAX_FRAME_SIZE, 16 KiB.
        /// </summary>
        private const Int32 ChunkSize = 16 * 1024;

        /// <summary>
        /// The server's connection receive window, which it raises to 1 MiB at
        /// once here, one stream window (see <see cref="StartAsync"/>). What it
        /// has taken in it gives back in a WINDOW_UPDATE once that is half of
        /// it, and sets aside for the next one before.
        /// </summary>
        private const Int64 Window = 1024 * 1024;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        private static String PathOf(IHTTP2RequestStream Request)

            => Request.Headers.First(header => header.Name == ":path").Value;

        /// <summary>
        /// The token a cancellation carries, or null for any other outcome.
        /// </summary>
        private static CancellationToken? TokenOf(Exception? Failure)

            => (Failure as OperationCanceledException)?.CancellationToken;

        /// <summary>
        /// Every frame the server sent on the stream so far, as text.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        /// <summary>
        /// Open an upload to <paramref name="Path"/> on the stream that declares
        /// <see cref="DeclaredLength"/>, and leave the client's side open for its
        /// body.
        /// </summary>
        private static Task OpenUploadAsync(PipedH2ServerConnection  Peer,
                                            UInt32                   StreamId,
                                            String                   Path   = "/upload")

            => Peer.SendHeadersAsync(StreamId,
                                     [(":method",        "POST"),
                                      (":scheme",        "http"),
                                      (":authority",     "localhost"),
                                      (":path",          Path),
                                      ("content-length", DeclaredLength)],
                                     EndStream: false);

        /// <summary>
        /// Upload "hello", the declared 5 bytes, to /next on the stream, and read
        /// the response: its body is the body the server's handler got.
        /// </summary>
        private static async Task<PipedH2ServerConnection.Response?> NextUploadAsync(PipedH2ServerConnection Peer, UInt32 StreamId)
        {

            await OpenUploadAsync(Peer, StreamId, "/next");
            await Peer.SendAsync(HTTP2Frame.CreateData(StreamId, ASCII("hello"), EndStream: true));

            return await Peer.TryResponseAsync(StreamId, PipedH2ServerConnection.StepTimeout);

        }

        /// <summary>
        /// A DATA frame of this data and this much padding: [Pad Length][data]
        /// [padding], with PADDED set. The whole of it counts against flow
        /// control (RFC 9113, Section 6.1), padding and all.
        /// </summary>
        private static HTTP2Frame PaddedData(UInt32 StreamId, Byte[] Data, Byte PadLength)
        {

            var payload = new Byte[1 + Data.Length + PadLength];

            payload[0] = PadLength;
            Data.CopyTo(payload, 1);

            var frame = HTTP2Frame.CreateData(StreamId, payload);
            frame.Flags |= HTTP2FrameFlags.PADDED;

            return frame;

        }

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
        /// A buffered request handler: it answers every request with the body it
        /// got, and counts the uploads to /upload it got.
        /// </summary>
        private sealed class EchoHandler
        {

            private Int32 uploads;

            public Int32 Uploads
                => Volatile.Read(ref uploads);

            public Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Handle(UInt32                             StreamId,
                                                                                                         List<(String Name, String Value)>  RequestHeaders,
                                                                                                         Byte[]?                            RequestBody,
                                                                                                         CancellationToken                  CancellationToken)
            {

                if (RequestHeaders.First(header => header.Name == ":path").Value == "/upload")
                    Interlocked.Increment(ref uploads);

                return Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(
                           ([(":status", "200")], RequestBody ?? []));

            }

        }

        /// <summary>
        /// A streaming request handler: it reads a body to its end, and answers it
        /// with the chunks it read, joined; on a reset stream, its answer fails.
        /// The upload to /upload it reads only once <paramref name="ReadOn"/> lets
        /// it, if given, and tells the test how that went.
        /// </summary>
        private static HTTP2StreamingHandler StreamingEcho(TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>  UploadRead,
                                                           Task?                                                                                       ReadOn   = null)

            => async (request, response, cancellationToken) => {

                   var upload = PathOf(request) == "/upload";

                   if (upload && ReadOn is not null)
                       await ReadOn;

                   var (chunks, end) = await ReadToEndAsync(request);

                   if (upload)
                       UploadRead.TrySetResult((chunks, end, cancellationToken));

                   await response.WriteHeadersAsync([(":status", "200")]);
                   await response.WriteAsync(ASCII(String.Concat(chunks)));

               };

        /// <summary>
        /// A server connection that takes in every upload as <paramref name="Path"/>
        /// says: with <paramref name="Buffered"/>, or with <see cref="StreamingEcho"/>,
        /// which tells <paramref name="UploadRead"/> how its reading of the upload
        /// to /upload went. It grants the client a connection window of one stream
        /// window, <see cref="Window"/>, as it did by default before that became
        /// four: half of it is then owed for an upload of 512 KiB, and given back
        /// at once, which the tests here count on.
        /// </summary>
        private static Task<PipedH2ServerConnection> StartAsync(UploadPath                                                                                 Path,
                                                                EchoHandler                                                                                Buffered,
                                                                TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>  UploadRead)

            => Path == UploadPath.Buffered
                   ? PipedH2ServerConnection.StartAsync(Buffered.Handle,
                                                        ConnectionWindowSize:  (Int32) Window)
                   : PipedH2ServerConnection.StartAsync((streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),
                                                        StreamingHandler:      StreamingEcho(UploadRead),
                                                        ConnectionWindowSize:  (Int32) Window);

        #endregion


        #region StreamedUpload_DataPastLength_IsReset_NeverRead(EndsBody)

        /// <summary>
        /// A streamed upload declares 5 bytes and sends them, as "hel" and "lo".
        /// Then it sends "!", which takes it past them, in a DATA frame that ends
        /// the body, or not. The server resets the stream with PROTOCOL_ERROR at
        /// that frame, and hands none of it to the handler: the handler, which
        /// reads only once the reset is handled, reads the declared 5 bytes, and
        /// then its read fails with the stream's token, as on any stream reset
        /// while its body was open. The server handed it "!" as well, and reset
        /// the stream only where the body ended.
        ///
        /// Once the reset is handled, the connection window of all of it is given
        /// back: of "!", and of the 5 bytes that were still unread then. What the
        /// client sends on the stream before it has read the reset is discarded,
        /// and the next upload is answered.
        /// </summary>
        [Test]
        public async Task StreamedUpload_DataPastLength_IsReset_NeverRead([Values] Boolean EndsBody)
        {

            var readOn      = new TaskCompletionSource(Async);
            var uploadRead  = new TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(
                                             (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),
                                             StreamingHandler: StreamingEcho(uploadRead, readOn.Task));

            try
            {

                await OpenUploadAsync(peer, 1);
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("hel")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("lo")));
                await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("!"), EndStream: EndsBody));

                // The read loop handles one frame at a time: once the ping is
                // answered, "!" is handled, and what the server sent for it is read.
                await peer.PingAsync();

                var sentForIt  = SentOn(peer, 1);
                var heldBack   = peer.ConnectionWindowHeldBack();

                readOn.TrySetResult();

                // The client has not read the reset yet, and ends the body.
                if (!EndsBody)
                    await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));

                var readEnded = await Task.WhenAny(uploadRead.Task, Task.Delay(PipedH2ServerConnection.StepTimeout)) == uploadRead.Task;

                Assert.That(readEnded, Is.True, "the handler's reading of the upload ended while the connection was open");

                var (chunks, end, handlerToken) = await uploadRead.Task;

                var next = await NextUploadAsync(peer, 3);

                Assert.Multiple(() =>
                {

                    Assert.That(sentForIt,        Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),  "what the server sent on the upload's stream for the DATA past the declared length");
                    Assert.That(heldBack,         Is.Zero,                                            "connection window held back once the reset is handled: for the DATA past the declared length, or for the 5 bytes nobody had read");

                    Assert.That(chunks,           Is.EqualTo(new[] { "hel", "lo" }),                 "what the handler read of the upload: the declared 5 bytes, and not a byte past them");
                    Assert.That(end,              Is.InstanceOf<OperationCanceledException>(),       "how the upload ended for the handler: null would say it was whole");
                    Assert.That(TokenOf(end),     Is.EqualTo(handlerToken),                          "the token it carries: the handler's, the stream's own");

                    Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),  "what the server sent on the upload's stream in all: nothing for what the client sent after");

                    Assert.That(next?.Status,     Is.EqualTo("200"),                                 "status of the next upload's response");
                    Assert.That(next?.Body,       Is.EqualTo("hello"),                               "body of the next upload's response: the body the handler read");

                });

            }
            finally
            {
                readOn.TrySetResult();
            }

        }

        #endregion

        #region BufferedUpload_DataPastLength_IsReset()

        /// <summary>
        /// A buffered upload declares 5 bytes and sends them, as "hel" and "lo".
        /// Then it sends "!", which takes it past them, and does not end the body.
        /// The server resets the stream with PROTOCOL_ERROR at that frame, rather
        /// than take in more of a body it will not dispatch: it took it in, up to
        /// its cap on a request body, and reset the stream only where the body
        /// ended. The handler never gets the upload, what the client sends on the
        /// stream before it has read the reset is discarded, and the next upload
        /// is answered.
        /// </summary>
        [Test]
        public async Task BufferedUpload_DataPastLength_IsReset()
        {

            var handler = new EchoHandler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Handle);

            await OpenUploadAsync(peer, 1);
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("hel")));
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("lo")));
            await peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("!")));

            // The read loop handles one frame at a time: once the ping is
            // answered, "!" is handled, and what the server sent for it is read.
            await peer.PingAsync();

            var sentForIt = SentOn(peer, 1);

            // The client has not read the reset yet, and ends the body.
            await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));

            var next = await NextUploadAsync(peer, 3);

            Assert.Multiple(() =>
            {

                Assert.That(sentForIt,        Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),  "what the server sent on the upload's stream for the DATA past the declared length");
                Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),  "what the server sent on the upload's stream in all: nothing for what the client sent after");
                Assert.That(handler.Uploads,  Is.Zero,                                            "uploads to /upload the handler got");

                Assert.That(next?.Status,     Is.EqualTo("200"),                                 "status of the next upload's response");
                Assert.That(next?.Body,       Is.EqualTo("hello"),                               "body of the next upload's response: the body the handler got");

            });

        }

        #endregion

        #region DataPastLength_WindowCountedAndGivenBack(Path)

        /// <summary>
        /// An upload declares 5 bytes and sends half the connection window, 512 KiB,
        /// in DATA frames of 16 KiB, and pads the first of them with 255 bytes. That
        /// frame takes it past the declared length, and the server resets the
        /// stream there, buffered or streamed. The frame counts against the stream's
        /// window and the connection's, padding and all, and its connection window
        /// is given back, as that of each frame after it, which the server
        /// discards. So once the client has sent half the connection window, the
        /// server gives it back in a WINDOW_UPDATE, and the client's connection
        /// window is whole again: had the first frame not been counted, or not
        /// given back, or only its data, it would be short of the half. The
        /// stream's window is not given back: the stream is reset. Nothing is held
        /// back, and the next upload is answered.
        /// </summary>
        [Test]
        public async Task DataPastLength_WindowCountedAndGivenBack([Values] UploadPath Path)
        {

            const Byte   padLength  = 255;
            const Int32  frames     = (Int32) (Window / 2 / ChunkSize);

            var handler     = new EchoHandler();
            var uploadRead  = new TaskCompletionSource<(List<String> Chunks, Exception? End, CancellationToken HandlerToken)>(Async);

            await using var peer = await StartAsync(Path, handler, uploadRead);

            await OpenUploadAsync(peer, 1);

            // Once the ping is answered, the stream is open, with its whole window,
            // and all the server has sent on the connection so far is read.
            await peer.PingAsync();

            var stream              = peer.ServerStream(1);
            var streamWindowBefore  = stream.RecvWindow;
            var clientWindowBefore  = ClientConnectionWindow(peer, Sent: 0);

            // 1 + 16 128 + 255 bytes: 16 KiB, as every frame after it.
            await peer.SendAsync(PaddedData(1, new Byte[ChunkSize - 1 - padLength], padLength));

            for (var i = 1; i < frames; i++)
                await peer.SendAsync(HTTP2Frame.CreateData(1, new Byte[ChunkSize]));

            await peer.PingAsync();

            var sent                = SentOn(peer, 1);
            var streamWindowAfter   = stream.RecvWindow;
            var clientWindowAfter   = ClientConnectionWindow(peer, Sent: frames * ChunkSize);
            var heldBack            = peer.ConnectionWindowHeldBack();

            var next = await NextUploadAsync(peer, 3);

            Assert.Multiple(() =>
            {

                Assert.That(clientWindowBefore,  Is.EqualTo(Window),                                 "the client's connection window before the upload");

                Assert.That(sent,                Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),  "what the server sent on the upload's stream: its reset, and no WINDOW_UPDATE");
                Assert.That(streamWindowAfter,   Is.EqualTo(streamWindowBefore - ChunkSize),        "the upload's stream window: the first frame counted against it, padding and all, and not given back");
                Assert.That(clientWindowAfter,   Is.EqualTo(clientWindowBefore),                    "the client's connection window after the upload: all of it counted, and given back, the first frame's padding too");
                Assert.That(heldBack,            Is.Zero,                                           "connection window held back");

                Assert.That(next?.Status,        Is.EqualTo("200"),                                 "status of the next upload's response");
                Assert.That(next?.Body,          Is.EqualTo("hello"),                               "body of the next upload's response: the body the handler got");

            });

        }

        #endregion

    }

}
