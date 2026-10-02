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

using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The server and <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> (RFC 9113, Section
    /// 6.5.2) on the way in. The server did not advertise its limit, and held
    /// every client to it all the same, harshly: a header block whose compressed
    /// bytes passed 8 KiB ended the connection with ENHANCE_YOUR_CALM, and every
    /// stream on it went down with the one that carried big cookies. Measured on
    /// the compressed bytes, it let through what they decode to, however large: a
    /// block of a few kilobytes of one-byte references to a cookie in the dynamic
    /// table decodes into thousands of cookie fields, which the server validated
    /// and joined into one string of megabytes, and handed to the handler.
    ///
    /// The server states its limit, 32 KiB, in its connection preface (see
    /// HeaderListSizeTests), and now holds the decoded header list to it, as the
    /// RFC counts it: name, value and 32 bytes per field. A request over it is
    /// answered with 431 (Section 10.5.1), and reaches no handler; oversized
    /// trailers reset their stream. The block is still decoded either way, as
    /// HPACK has to see every block, and only one that passes twice the limit in
    /// its compressed bytes, which the server has to hold to decode it, ends the
    /// connection.
    /// </summary>
    [TestFixture]
    public class ServerHeaderListSizeTests
    {

        #region (helpers)

        /// <summary>
        /// What the server advertises, and holds a header list to.
        /// </summary>
        private const Int32 Limit = 32 * 1024;

        private static readonly TimeSpan StepTimeout = PipedH2ServerConnection.StepTimeout;

        /// <summary>
        /// The body of the server's 431.
        /// </summary>
        private const String TooLarge = "Request Header Fields Too Large";

        /// <summary>
        /// Counts the requests that reach a handler, buffered or streaming, and
        /// answers each with 200 "ok" once the request has ended.
        /// </summary>
        private sealed class Handlers
        {

            private Int32 calls;

            public Int32 Calls
                => Volatile.Read(ref calls);

            public Task<(List<(String Name, String Value)> ResponseHeaders, Byte[]? ResponseBody)> Buffered(UInt32                             StreamId,
                                                                                                           List<(String Name, String Value)>  Headers,
                                                                                                           Byte[]?                            Body,
                                                                                                           CancellationToken                  CancellationToken)
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "200")], "ok"u8.ToArray()));
            }

            public async Task Streaming(IHTTP2RequestStream   Request,
                                        IHTTP2ResponseStream  Response,
                                        CancellationToken     CancellationToken)
            {

                Interlocked.Increment(ref calls);

                while (await Request.ReadAsync(CancellationToken) is not null)
                { }

                await Response.WriteHeadersAsync([(":status", "200")], CancellationToken);
                await Response.WriteAsync("ok"u8.ToArray(), CancellationToken);
                await Response.CompleteAsync(null, CancellationToken);

            }

        }

        /// <summary>
        /// Start a server connection whose requests go to the buffered handler,
        /// or to the streaming one.
        /// </summary>
        private static Task<PipedH2ServerConnection> StartAsync(Handlers Handlers, Boolean Streaming)

            => PipedH2ServerConnection.StartAsync(Handlers.Buffered,
                                                  StreamingHandler: Streaming ? Handlers.Streaming : null);

        /// <summary>
        /// The pseudo-header fields of a GET for this path, as RequestAsync sends them.
        /// </summary>
        private static List<(String Name, String Value)> Get(String Path)

            => [(":method",    "GET"),
                (":scheme",    "http"),
                (":authority", "localhost"),
                (":path",      Path)];

        /// <summary>
        /// A GET for this path whose header list is exactly <paramref name="Size"/>
        /// bytes as the RFC counts it, padded out with an <c>x-pad</c> field.
        /// </summary>
        private static List<(String Name, String Value)> GetOfSize(String Path, Int64 Size)
        {

            List<(String Name, String Value)> fields = [.. Get(Path), ("x-pad", "")];

            fields[^1] = ("x-pad", new String('p', (Int32) (Size - HTTP2HeaderList.UncompressedSize(fields))));

            return fields;

        }

        /// <summary>
        /// The header block of a GET for this path, each field a literal without
        /// indexing, that is exactly <paramref name="Length"/> bytes long.
        /// </summary>
        private static Byte[] GetBlockOfLength(String Path, Int32 Length)
        {

            List<(String Name, String Value)> fields = [.. Get(Path), ("x-pad", new String('p', Length))];

            var overhead = RawHeaderBlock.Literals(fields).Length - Length;

            fields[^1] = ("x-pad", new String('p', Length - overhead));

            var block = RawHeaderBlock.Literals(fields);

            // The length of the padding's length prefix must not change in between.
            if (block.Length != Length)
                throw new InvalidOperationException($"A block of {block.Length} bytes, not {Length}");

            return block;

        }

        /// <summary>
        /// Send a header block in as many frames as it takes. Each send is bounded:
        /// a server that has ended the connection reads no more, and a send into a
        /// full pipe would wait for good.
        /// </summary>
        private static async Task SendBlockAsync(PipedH2ServerConnection  Peer,
                                                 UInt32                   StreamId,
                                                 Byte[]                   Block,
                                                 Boolean                  EndStream)
        {
            foreach (var frame in RawHeaderBlock.Frames(StreamId, Block, EndStream))
                await Peer.SendAsync(frame).WaitAsync(StepTimeout);
        }

        /// <summary>
        /// Wait until the condition holds, or fail once the step timeout is over.
        /// </summary>
        private static async Task WaitUntilAsync(Func<Boolean> Condition, String What)
        {

            var waited = System.Diagnostics.Stopwatch.StartNew();

            while (!Condition())
            {

                if (waited.Elapsed > StepTimeout)
                    throw new TimeoutException($"Timed out waiting until {What}");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// The state of the server's stream, or null while the server has not
        /// opened it yet.
        /// </summary>
        private static HTTP2StreamState? StateOf(PipedH2ServerConnection Peer, UInt32 StreamId)
        {
            try
            {
                return Peer.ServerStream(StreamId).State;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// What the server sent on a stream, one line per frame.
        /// </summary>
        private static List<String> SentOn(PipedH2ServerConnection Peer, UInt32 StreamId)

            => [.. Peer.FramesOn(StreamId).Select(frame => frame.ToString())];

        /// <summary>
        /// The error codes of the GOAWAY frames the server sent.
        /// </summary>
        private static List<HTTP2ErrorCode> GoAways(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).Where (frame => frame.Frame.Type == HTTP2FrameType.GOAWAY).
                                    Select(frame => (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Frame.Payload.AsSpan(4)))];

        /// <summary>
        /// The 431, as it is read off the wire.
        /// </summary>
        private static readonly String[] Answered431 = [
            "HEADERS :status 431, content-type text/plain",
            $"DATA \"{TooLarge}\" END_STREAM"
        ];

        #endregion


        #region RequestUpToTheLimit_Served(Streaming)

        /// <summary>
        /// A request whose header list is exactly the limit, as the RFC counts it,
        /// is served: its compressed bytes, past what used to end the connection,
        /// are no measure of it.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task RequestUpToTheLimit_Served(Boolean Streaming)
        {

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming);

            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals(GetOfSize("/limit", Limit)), EndStream: true);

            var response = await peer.TryResponseAsync(1, StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(response?.Status,   Is.EqualTo("200"),  "status of the request at the limit");
                Assert.That(handlers.Calls,     Is.EqualTo(1),      "requests that reached a handler");
                Assert.That(GoAways(peer),      Is.Empty,           "GOAWAY frames the server sent");
            });

        }

        #endregion

        #region RequestOverTheLimit_Answered431_ConnectionGoesOn(Streaming, WithBody)

        /// <summary>
        /// A request whose header list is one byte over the limit is answered with
        /// 431, whichever handler would have taken it, and reaches none. A body
        /// the client goes on to send is read by nothing: once the 431 is complete,
        /// the client is asked to stop with RST_STREAM NO_ERROR (RFC 9113, Section
        /// 8.1), and what it sent before it read that is dropped, its window given
        /// back. The next request on the connection is served.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true,  false)]
        [TestCase(true,  true)]
        public async Task RequestOverTheLimit_Answered431_ConnectionGoesOn(Boolean Streaming, Boolean WithBody)
        {

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming);

            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals(GetOfSize("/big", Limit + 1)), EndStream: !WithBody);

            if (WithBody)
                await peer.SendAsync(HTTP2Frame.CreateData(1, "first part"u8.ToArray())).WaitAsync(StepTimeout);

            var refused  = await peer.TryResponseAsync(1, StepTimeout);

            HTTP2ErrorCode? reset = null;

            if (WithBody)
            {

                reset = await peer.ReadToResetAsync(1).WaitAsync(StepTimeout);

                // The rest of the upload, sent before the client read the reset.
                await peer.SendAsync(HTTP2Frame.CreateData(1, "second part"u8.ToArray(), EndStream: true)).WaitAsync(StepTimeout);

            }

            await peer.PingAsync();

            var heldBack = peer.ConnectionWindowHeldBack();

            await peer.RequestAsync(3, "/next");

            var next = await peer.TryResponseAsync(3, StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(refused?.Status,   Is.EqualTo("431"),                                         "status of the request over the limit");
                Assert.That(SentOn(peer, 1),   Is.EqualTo(WithBody ? [.. Answered431, "RST_STREAM NO_ERROR"]
                                                                   : Answered431),                       "what the server sent on its stream");
                Assert.That(heldBack,          Is.EqualTo(0),                                             "connection window held back for a body nothing reads");

                Assert.That(next?.Status,      Is.EqualTo("200"),                                         "status of the next request");
                Assert.That(handlers.Calls,    Is.EqualTo(1),                                             "requests that reached a handler: only the next");
                Assert.That(GoAways(peer),     Is.Empty,                                                  "GOAWAY frames the server sent");

            });

        }

        #endregion

        #region RequestOverTheLimit_ItsTrailersReachNoHandler(Streaming)

        /// <summary>
        /// A request over the limit whose body and trailers the server has taken
        /// in before any of its 431 went out: the trailers end the body that
        /// nothing reads, and start no handler — on the buffered path, where the
        /// end of a request is where its handler starts, as on the streaming one.
        /// The client had ended its side, so no RST_STREAM follows the 431.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task RequestOverTheLimit_ItsTrailersReachNoHandler(Boolean Streaming)
        {

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming);

            using (await peer.HoldWritesAsync())
            {

                await SendBlockAsync(peer, 1, RawHeaderBlock.Literals(GetOfSize("/big", Limit + 1)), EndStream: false);
                await peer.SendAsync(HTTP2Frame.CreateData(1, "body"u8.ToArray())).WaitAsync(StepTimeout);
                await SendBlockAsync(peer, 1, RawHeaderBlock.Literals([("x-trailer", "small")]), EndStream: true);

                // No ping while the writes are held: its answer could not go out.
                // The end of the client's side is the last the read loop does with
                // the trailers; let go before that, and the 431's END_STREAM would
                // find that side still open, and ask the client to stop sending.
                await WaitUntilAsync(() => StateOf(peer, 1) == HTTP2StreamState.HalfClosedRemote,
                                     "the server has taken the trailers in, and ended the client's side");

            }

            var refused = await peer.TryResponseAsync(1, StepTimeout);

            await peer.RequestAsync(3, "/next");

            var next = await peer.TryResponseAsync(3, StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(refused?.Status,   Is.EqualTo("431"),     "status of the request over the limit");
                Assert.That(SentOn(peer, 1),   Is.EqualTo(Answered431), "what the server sent on its stream");

                Assert.That(next?.Status,      Is.EqualTo("200"),     "status of the next request");
                Assert.That(handlers.Calls,    Is.EqualTo(1),         "requests that reached a handler: only the next");

            });

        }

        #endregion

        #region SmallBlockOfAHugeList_Answered431(Streaming)

        /// <summary>
        /// A header block of some 6 KB that decodes into a header list of some
        /// 8 MB: a cookie of 4000 bytes taken into the dynamic table, and 1999
        /// one-byte references to it. Measured by its compressed bytes, the
        /// request passed, and the server validated 2000 cookie fields and joined
        /// them into one cookie of 8 MB for the handler (RFC 9113, Section 8.2.3).
        /// It is answered with 431 now, and reaches no handler.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task SmallBlockOfAHugeList_Answered431(Boolean Streaming)
        {

            const Int32 Cookies = 2000;

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming);

            // The dynamic table is empty: the cookie becomes its entry 62.
            Byte[] block = [.. RawHeaderBlock.Literals(Get("/cookies")),
                            .. RawHeaderBlock.LiteralWithIndexing("cookie", new String('c', 4000)),
                            .. Enumerable.Repeat(RawHeaderBlock.Indexed(62), Cookies - 1).SelectMany(reference => reference)];

            Assert.That(block.Length, Is.LessThan(8192), "the block is small enough for its compressed bytes to have let it through");

            await SendBlockAsync(peer, 1, block, EndStream: true);

            var refused = await peer.TryResponseAsync(1, StepTimeout);

            // Another request, from the client's encoder: the cookie is still in the
            // server's table, its oldest entry, and the encoder refers to newer ones only.
            await peer.RequestAsync(3, "/next");

            var next = await peer.TryResponseAsync(3, StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(refused?.Status,  Is.EqualTo("431"),  "status of the request of 2000 cookies");
                Assert.That(next?.Status,     Is.EqualTo("200"),  "status of the next request");
                Assert.That(handlers.Calls,   Is.EqualTo(1),      "requests that reached a handler: only the next");
            });

        }

        #endregion

        #region TrailersOverTheLimit_ResetTheStream_ConnectionGoesOn(Streaming)

        /// <summary>
        /// Trailers over the limit reset their stream with PROTOCOL_ERROR, as any
        /// malformed trailers do (RFC 9113, Section 8.1.1): there may be a
        /// response under way already. A buffered request does not reach its
        /// handler; a streaming handler's read of the body fails with the reset,
        /// and it writes nothing. The next request on the connection is served.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task TrailersOverTheLimit_ResetTheStream_ConnectionGoesOn(Boolean Streaming)
        {

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming);

            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals([.. Get("/upload"), ("content-type", "text/plain")]), EndStream: false);
            await peer.SendAsync(HTTP2Frame.CreateData(1, "body"u8.ToArray())).WaitAsync(StepTimeout);
            await SendBlockAsync(peer, 1, RawHeaderBlock.Literals([("x-trailer", new String('t', Limit))]), EndStream: true);

            var reset = await peer.ReadToResetAsync(1).WaitAsync(StepTimeout);

            await peer.RequestAsync(3, "/next");

            var next = await peer.TryResponseAsync(3, StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(reset,            Is.EqualTo(HTTP2ErrorCode.PROTOCOL_ERROR),  "the reset of the stream with the trailers");
                Assert.That(SentOn(peer, 1),  Is.EqualTo(new[] { "RST_STREAM PROTOCOL_ERROR" }),
                                                                                          "what the server sent on that stream");

                Assert.That(next?.Status,     Is.EqualTo("200"),                          "status of the next request");
                Assert.That(handlers.Calls,   Is.EqualTo(Streaming ? 2 : 1),              "requests that reached a handler: a streaming one has its own at once");
                Assert.That(GoAways(peer),    Is.Empty,                                   "GOAWAY frames the server sent");

            });

        }

        #endregion

        #region BlockPastTwiceTheLimit_EndsTheConnection(Over)

        /// <summary>
        /// The server holds a header block whole until it can decode it, and a
        /// block of twice the limit in its compressed bytes it still decodes, and
        /// answers. One byte more, and it ends the connection with
        /// ENHANCE_YOUR_CALM: that is a client far past a limit it was told,
        /// and what a CONTINUATION flood (CVE-2024-27316) asks the server to hold.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        public async Task BlockPastTwiceTheLimit_EndsTheConnection(Int32 Over)
        {

            var handlers = new Handlers();

            await using var peer = await StartAsync(handlers, Streaming: false);

            await SendBlockAsync(peer, 1, GetBlockOfLength("/huge", 2 * Limit + Over), EndStream: true);

            if (Over == 0)
            {

                var refused = await peer.TryResponseAsync(1, StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(refused?.Status,  Is.EqualTo("431"),  "status of the request of a block twice the limit");
                    Assert.That(GoAways(peer),    Is.Empty,           "GOAWAY frames the server sent");
                });

            }
            else
            {

                await peer.ReadToEndAsync().WaitAsync(StepTimeout);

                Assert.Multiple(() =>
                {
                    Assert.That(GoAways(peer),    Is.EqualTo(new[] { HTTP2ErrorCode.ENHANCE_YOUR_CALM }),  "GOAWAY frames the server sent");
                    Assert.That(SentOn(peer, 1),  Is.Empty,                                                "what the server sent on the stream");
                });

            }

            Assert.That(handlers.Calls, Is.Zero, "requests that reached a handler");

        }

        #endregion

    }

}
