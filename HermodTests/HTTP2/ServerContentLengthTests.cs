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
    /// The length a request declares with content-length, compared with the DATA
    /// it sends (RFC 9113, Section 8.1.1) where its body ends: at a DATA frame
    /// with END_STREAM, or at trailers, a header block with END_STREAM after the
    /// DATA (Section 8.1). Every test here runs for each way a body can end.
    ///
    /// Trailers were the odd one out. A buffered upload, one that no streaming
    /// handler takes, that declared its length and ended with trailers was reset
    /// with PROTOCOL_ERROR rather than dispatched: the check that a request ended
    /// by its first header block, and so without a body, declares no length fired
    /// on its trailers as well. And only a DATA frame with END_STREAM compared the
    /// length: trailers let a body of another length through, on the streaming
    /// path as on the buffered one.
    /// </summary>
    [TestFixture]
    public class ServerContentLengthTests
    {

        #region (helpers)

        /// <summary>
        /// Where an upload's body ends: at its last DATA frame, which carries
        /// END_STREAM, or at trailers after it, in one HEADERS frame, or split
        /// into a HEADERS frame and CONTINUATION frames, the last of which
        /// completes the block the END_STREAM is on.
        /// </summary>
        public enum BodyEnd
        {
            Data,
            Trailers,
            TrailersInContinuation
        }

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// The length of the body every upload here sends: "hello " and "world",
        /// in a DATA frame each.
        /// </summary>
        private const String BodyLength = "11";

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// The token a cancellation carries, or null for any other outcome.
        /// </summary>
        private static CancellationToken? TokenOf(Exception? Failure)

            => (Failure as OperationCanceledException)?.CancellationToken;

        /// <summary>
        /// Send an upload to /upload on stream 1 that declares this content-length,
        /// then its body, ended as <paramref name="End"/> says.
        /// </summary>
        private static async Task UploadAsync(PipedH2ServerConnection  Peer,
                                              String                   ContentLength,
                                              BodyEnd                  End)
        {

            await Peer.SendHeadersAsync(1,
                                        [(":method",        "POST"),
                                         (":scheme",        "http"),
                                         (":authority",     "localhost"),
                                         (":path",          "/upload"),
                                         ("content-length", ContentLength)],
                                        EndStream: false);

            await Peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("hello ")));
            await Peer.SendAsync(HTTP2Frame.CreateData(1, ASCII("world"), EndStream: End == BodyEnd.Data));

            if (End != BodyEnd.Data)
                await Peer.SendHeadersAsync(1,
                                            [("x-checksum", "abc123")],
                                            EndStream:     true,
                                            FragmentSize:  End == BodyEnd.TrailersInContinuation ? 4 : null);

        }

        /// <summary>
        /// Request /other on stream 3, with the trailers' field once more. After
        /// trailers, its header block refers to the entry they left in the
        /// client's dynamic table, so it decodes only if the server decoded them
        /// too.
        /// </summary>
        private static async Task<PipedH2ServerConnection.Response?> OtherAsync(PipedH2ServerConnection Peer)
        {

            await Peer.SendHeadersAsync(3,
                                        [(":method",    "GET"),
                                         (":scheme",    "http"),
                                         (":authority", "localhost"),
                                         (":path",      "/other"),
                                         ("x-checksum", "abc123")],
                                        EndStream: true);

            return await Peer.TryResponseAsync(3, PipedH2ServerConnection.StepTimeout);

        }

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
        /// A buffered request handler: it answers an upload with the body it got,
        /// and anything else with "other", and counts the uploads it got.
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

                var upload = RequestHeaders.First(header => header.Name == ":path").Value == "/upload";

                if (upload)
                    Interlocked.Increment(ref uploads);

                return Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(
                           ([(":status", "200")], upload ? RequestBody ?? [] : ASCII("other")));

            }

        }

        /// <summary>
        /// A streaming request handler: it reads an upload to its end, tells the
        /// test how that went, and answers it with the chunks it read, joined,
        /// unless its stream was reset; anything else with "other".
        /// </summary>
        private static HTTP2StreamingHandler StreamingEcho(TaskCompletionSource<(List<String> Chunks, Exception? End, List<(String Name, String Value)> Trailers, CancellationToken HandlerToken)> BodyRead)

            => async (request, response, cancellationToken) => {

                   if (request.Headers.First(header => header.Name == ":path").Value != "/upload")
                   {
                       await response.WriteHeadersAsync([(":status", "200")]);
                       await response.WriteAsync(ASCII("other"));
                       return;
                   }

                   var (chunks, end) = await ReadToEndAsync(request);

                   BodyRead.TrySetResult((chunks, end, [.. request.Trailers], cancellationToken));

                   // No answer on a reset stream.
                   cancellationToken.ThrowIfCancellationRequested();

                   await response.WriteHeadersAsync([(":status", "200")]);
                   await response.WriteAsync(ASCII(String.Concat(chunks)));

               };

        #endregion


        #region BufferedUpload_RightLength_IsDispatched(End)

        /// <summary>
        /// A buffered upload whose declared length is that of its body reaches its
        /// handler with the whole body, and is answered, however the body ends.
        /// Ended by trailers, it was reset with PROTOCOL_ERROR instead: the check
        /// that a request ended by its first header block declares no length fired
        /// on the trailers too.
        /// </summary>
        [Test]
        public async Task BufferedUpload_RightLength_IsDispatched([Values] BodyEnd End)
        {

            var handler = new EchoHandler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Handle);

            await UploadAsync(peer, BodyLength, End);

            var upload  = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);
            var other   = await OtherAsync(peer);

            Assert.Multiple(() =>
            {

                Assert.That(upload?.Status,    Is.EqualTo("200"),          "status of the upload's response");
                Assert.That(upload?.Body,      Is.EqualTo("hello world"),  "body of the upload's response: the body the handler got");
                Assert.That(handler.Uploads,   Is.EqualTo(1),              "uploads the handler got");

                Assert.That(peer.FramesOn(1).Where(frame => frame.Frame.Type == HTTP2FrameType.RST_STREAM).Select(frame => frame.ToString()),
                            Is.Empty,
                            "resets the server sent on the upload's stream");

                Assert.That(other?.Status,     Is.EqualTo("200"),          "status of the next response");
                Assert.That(other?.Body,       Is.EqualTo("other"),        "body of the next response");

            });

        }

        #endregion

        #region BufferedUpload_WrongLength_IsReset(ContentLength, End)

        /// <summary>
        /// A buffered upload whose body is of another length than it declared is
        /// malformed (RFC 9113, Section 8.1.1): the server resets it with
        /// PROTOCOL_ERROR, its handler never gets it, and the connection serves on,
        /// however the body ends. Ended by trailers, one that declared no body at
        /// all was dispatched: nothing compared the length there. Any other length
        /// was reset only because the check for a request without a body fired.
        /// </summary>
        [Test]
        public async Task BufferedUpload_WrongLength_IsReset([Values("0", "5", "20")] String   ContentLength,
                                                             [Values]                 BodyEnd  End)
        {

            var handler = new EchoHandler();

            await using var peer = await PipedH2ServerConnection.StartAsync(handler.Handle);

            await UploadAsync(peer, ContentLength, End);

            var upload  = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);
            var other   = await OtherAsync(peer);

            Assert.Multiple(() =>
            {

                Assert.That(upload,            Is.Null,                    "a response to the upload");
                Assert.That(handler.Uploads,   Is.Zero,                    "uploads the handler got");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),
                            "what the server sent on the upload's stream");

                Assert.That(other?.Status,     Is.EqualTo("200"),          "status of the next response");
                Assert.That(other?.Body,       Is.EqualTo("other"),        "body of the next response");

            });

        }

        #endregion

        #region StreamedUpload_RightLength_ReadsWhole(End)

        /// <summary>
        /// A streamed upload whose declared length is that of its body reads whole:
        /// every chunk, then null, and after that its trailers, if it has any.
        /// </summary>
        [Test]
        public async Task StreamedUpload_RightLength_ReadsWhole([Values] BodyEnd End)
        {

            var bodyRead = new TaskCompletionSource<(List<String> Chunks, Exception? End, List<(String Name, String Value)> Trailers, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(
                                             (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),
                                             StreamingHandler: StreamingEcho(bodyRead));

            await UploadAsync(peer, BodyLength, End);

            var upload  = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);
            var other   = await OtherAsync(peer);

            var ended   = await Task.WhenAny(bodyRead.Task, Task.Delay(PipedH2ServerConnection.StepTimeout)) == bodyRead.Task;

            Assert.That(ended, Is.True, "the handler's reading of the body ended while the connection was open");

            var (chunks, end, trailers, handlerToken) = await bodyRead.Task;

            Assert.Multiple(() =>
            {

                Assert.That(chunks,                                Is.EqualTo(new[] { "hello ", "world" }),  "what the handler read of the body");
                Assert.That(end,                                   Is.Null,                                  "how the body ended: with null, as a whole one");
                Assert.That(handlerToken.IsCancellationRequested,  Is.False,                                 "the handler's token, of a stream nobody reset");

                Assert.That(trailers,
                            Is.EqualTo(End == BodyEnd.Data ? [] : new[] { ("x-checksum", "abc123") }),
                            "the trailers the handler read after the body");

                Assert.That(upload?.Status,                        Is.EqualTo("200"),                        "status of the upload's response");
                Assert.That(upload?.Body,                          Is.EqualTo("hello world"),                "body of the upload's response");

                Assert.That(other?.Status,                         Is.EqualTo("200"),                        "status of the next response");
                Assert.That(other?.Body,                           Is.EqualTo("other"),                      "body of the next response");

            });

        }

        #endregion

        #region StreamedUpload_WrongLength_ReadFails(ContentLength, End)

        /// <summary>
        /// A streamed upload whose body is of another length than it declared is
        /// malformed as well: the server resets it with PROTOCOL_ERROR. Its handler
        /// reads the chunks that arrived, and then its read fails with the stream's
        /// token, as on any stream reset while its body was open; it does not end
        /// with null, which says the body is whole. Ended by trailers, it did:
        /// nothing compared the length there, and the handler answered a malformed
        /// request.
        /// </summary>
        [Test]
        public async Task StreamedUpload_WrongLength_ReadFails([Values("0", "5", "20")] String   ContentLength,
                                                               [Values]                 BodyEnd  End)
        {

            var bodyRead = new TaskCompletionSource<(List<String> Chunks, Exception? End, List<(String Name, String Value)> Trailers, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(
                                             (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),
                                             StreamingHandler: StreamingEcho(bodyRead));

            await UploadAsync(peer, ContentLength, End);

            var upload  = await peer.TryResponseAsync(1, PipedH2ServerConnection.StepTimeout);
            var other   = await OtherAsync(peer);

            var ended   = await Task.WhenAny(bodyRead.Task, Task.Delay(PipedH2ServerConnection.StepTimeout)) == bodyRead.Task;

            Assert.That(ended, Is.True, "the handler's reading of the body ended while the connection was open");

            var (chunks, end, _, handlerToken) = await bodyRead.Task;

            Assert.Multiple(() =>
            {

                Assert.That(chunks,            Is.EqualTo(new[] { "hello ", "world" }),      "what the handler read of the body");
                Assert.That(end,               Is.InstanceOf<OperationCanceledException>(),  "how the body ended: null would say it was whole");
                Assert.That(TokenOf(end),      Is.EqualTo(handlerToken),                    "the token it carries: the handler's, the stream's own");

                Assert.That(upload,            Is.Null,                                     "a response to the upload");

                Assert.That(peer.FramesOn(1).Select(frame => frame.ToString()),
                            Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),
                            "what the server sent on the upload's stream");

                Assert.That(other?.Status,     Is.EqualTo("200"),                           "status of the next response");
                Assert.That(other?.Body,       Is.EqualTo("other"),                         "body of the next response");

            });

        }

        #endregion

    }

}
