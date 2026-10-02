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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The client and <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> (RFC 9113, Section
    /// 6.5.2) on the way in. The client did not advertise its limit, and held a
    /// server to it all the same: a response header block whose compressed bytes
    /// passed 8 KiB ended the connection with ENHANCE_YOUR_CALM, and every request
    /// on it failed with the one that drew big headers. The client states its
    /// limit, 32 KiB (see HeaderListSizeTests), and now holds the decoded header
    /// list of each block to it, as the RFC counts it. A response over it is
    /// discarded (Section 10.5.1): its request fails, and its stream is reset
    /// with PROTOCOL_ERROR, as for a malformed response (Section 8.1.1). The
    /// connection goes on, and decodes what the server had sent on the stream
    /// before it read the reset (see ClientHeadersAfterResetTests). Only a block
    /// that passes twice the limit in its compressed bytes ends the connection.
    ///
    /// The tests play the server with blocks of literals without indexing, which
    /// leave the client's dynamic table as it is, unless a test means to fill it.
    /// </summary>
    [TestFixture]
    public class ClientHeaderListSizeTests
    {

        #region (helpers)

        /// <summary>
        /// What the client advertises, and holds a header list to.
        /// </summary>
        private const Int32 Limit = 32 * 1024;

        private static readonly TimeSpan StepTimeout = HoldingH2Transport.StepTimeout;

        /// <summary>
        /// The field the blocks the server sent before it read the reset put into
        /// the client's dynamic table, and the next response refers to by its
        /// index alone.
        /// </summary>
        private static readonly (String Name, String Value) Late = ("x-late", "in flight");

        /// <summary>
        /// These fields, the value of the last padded out so that the header list
        /// is exactly <paramref name="Size"/> bytes as the RFC counts it.
        /// </summary>
        private static List<(String Name, String Value)> OfSize(List<(String Name, String Value)> Fields, Int64 Size)
        {

            List<(String Name, String Value)> fields = [.. Fields];

            fields[^1] = (fields[^1].Name, new String('v', (Int32) (Size - HTTP2HeaderList.UncompressedSize(fields))));

            return fields;

        }

        /// <summary>
        /// The header block of a response, each field a literal without indexing,
        /// that is exactly <paramref name="Length"/> bytes long.
        /// </summary>
        private static Byte[] ResponseBlockOfLength(Int32 Length)
        {

            List<(String Name, String Value)> fields = [(":status", "200"), ("x-pad", new String('p', Length))];

            var overhead = RawHeaderBlock.Literals(fields).Length - Length;

            fields[^1] = ("x-pad", new String('p', Length - overhead));

            var block = RawHeaderBlock.Literals(fields);

            // The length of the padding's length prefix must not change in between.
            if (block.Length != Length)
                throw new InvalidOperationException($"A block of {block.Length} bytes, not {Length}");

            return block;

        }

        /// <summary>
        /// Send a header block in as many frames as it takes.
        /// </summary>
        private static async Task SendBlockAsync(HoldingH2Transport  Transport,
                                                 UInt32              StreamId,
                                                 Byte[]              Block,
                                                 Boolean             EndStream,
                                                 Int32               FragmentSize   = 16384)
        {
            foreach (var frame in RawHeaderBlock.Frames(StreamId, Block, EndStream, FragmentSize))
                await Transport.SendAsync(frame);
        }

        /// <summary>
        /// The exception the task ended with, or null if it succeeded.
        /// </summary>
        private static async Task<Exception?> FailureOf(Task Task)
        {
            try
            {
                await Task.WaitAsync(StepTimeout);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        /// <summary>
        /// An exception as type, error code and whether it names the limit.
        /// </summary>
        private static String Describe(Exception? Failure)

            => Failure switch {
                   HTTP2StreamException      stream      => $"{nameof(HTTP2StreamException)} {stream.ErrorCode}{NamesTheLimit(stream)}",
                   HTTP2ConnectionException  connection  => $"{nameof(HTTP2ConnectionException)} {connection.ErrorCode}{NamesTheLimit(connection)}",
                   null                                  => "none",
                   _                                     => Failure.GetType().Name
               };

        private static String NamesTheLimit(Exception Failure)

            => Failure.Message.Contains("MAX_HEADER_LIST_SIZE") ? ", MAX_HEADER_LIST_SIZE" : "";

        /// <summary>
        /// The RST_STREAM frames among these, as stream and error code.
        /// </summary>
        private static List<String> Resets(IEnumerable<HTTP2Frame> Frames)

            => [.. Frames.Where (frame => frame.Type == HTTP2FrameType.RST_STREAM).
                          Select(frame => $"{frame.StreamId} {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Payload)}")];

        /// <summary>
        /// Read a streamed request's response: its head, its body and its trailers.
        /// </summary>
        private static async Task ReadAllAsync(HTTP2ClientStream Upload)
        {

            await Upload.GetResponseAsync();

            while (await Upload.ReadAsync() is not null)
            { }

            await Upload.GetTrailersAsync();

        }

        #endregion


        #region ResponseUpToTheLimit_Taken(InTrailers)

        /// <summary>
        /// A response whose header list, or whose trailers' list, is exactly the
        /// limit, as the RFC counts it, is taken, its block in a HEADERS frame and
        /// a CONTINUATION frame.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task ResponseUpToTheLimit_Taken(Boolean InTrailers)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var response    = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");
            var streamId    = (await transport.NextHeadersAsync()).StreamId;
            var head        = InTrailers ? [(":status", "200")]                : OfSize([(":status", "200"), ("x-field", "")], Limit);
            var trailers    = InTrailers ? OfSize([("x-trailer", "")], Limit)  : null;

            await SendBlockAsync(transport, streamId, RawHeaderBlock.Literals(head), EndStream: !InTrailers);

            if (trailers is not null)
            {
                await transport.SendAsync(HTTP2Frame.CreateData(streamId, "body"u8.ToArray()));
                await SendBlockAsync(transport, streamId, RawHeaderBlock.Literals(trailers), EndStream: true);
            }

            var answer = await response.WaitAsync(StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(answer.Status,    Is.EqualTo(200),             "status of the response");
                Assert.That(answer.Headers,   Is.EqualTo(head),            "the response's header fields");
                Assert.That(answer.Trailers,  Is.EqualTo(trailers ?? []),  "the response's trailer fields");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResponseOverTheLimit_FailsOnlyItsRequest(Streaming, InTrailers)

        /// <summary>
        /// A response whose header block, a body still to come, or whose trailers
        /// decode to a list one byte past the limit — in fewer compressed bytes
        /// than the limit: its request fails, with an HTTP2StreamException of
        /// PROTOCOL_ERROR that names the limit, and the client resets its stream
        /// with that code. Not so where the trailers end the stream, and a
        /// buffered request has ended its side already: the stream is closed, and
        /// takes no RST_STREAM (RFC 9113, Section 5.1). The next request on the
        /// connection is answered.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true,  false)]
        [TestCase(true,  true)]
        public async Task ResponseOverTheLimit_FailsOnlyItsRequest(Boolean Streaming, Boolean InTrailers)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection = await transport.ConnectAsync(new HTTP2ClientOptions());

            UInt32  streamId;
            Task    response;

            if (Streaming)
            {
                var upload  = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/first").WaitAsync(StepTimeout);
                streamId    = (await transport.NextHeadersAsync()).StreamId;
                response    = ReadAllAsync(upload);
            }
            else
            {
                response    = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/first");
                streamId    = (await transport.NextHeadersAsync()).StreamId;
            }

            var block = RawHeaderBlock.Literals(InTrailers ? OfSize([("x-trailer", "")],                   Limit + 1)
                                                           : OfSize([(":status", "200"), ("x-field", "")], Limit + 1));

            Assert.That(block.Length, Is.LessThan(Limit), "the block is small enough for its compressed bytes to have let it through");

            if (InTrailers)
            {
                await SendBlockAsync(transport, streamId, RawHeaderBlock.Literals([(":status", "200")]), EndStream: false);
                await transport.SendAsync(HTTP2Frame.CreateData(streamId, "body"u8.ToArray()));
            }

            await SendBlockAsync(transport, streamId, block, EndStream: InTrailers);

            var failure  = await FailureOf(response);

            var next     = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/next");
            var skipped  = new List<HTTP2Frame>();
            var request  = await transport.NextHeadersAsync(skipped);

            await transport.RespondAsync(request.StreamId, "next");

            var answer   = await next.WaitAsync(StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo("HTTP2StreamException PROTOCOL_ERROR, MAX_HEADER_LIST_SIZE"),
                                                                                                    "how the request over the limit failed");
                Assert.That(Resets(skipped),    Is.EqualTo(InTrailers && !Streaming ? []
                                                                                    : new[] { $"{streamId} PROTOCOL_ERROR" }),
                                                                                                    "the client's resets before the next request");

                Assert.That(answer.Status,      Is.EqualTo(200),                                    "status of the next response");
                Assert.That(Encoding.ASCII.GetString(answer.Body),
                                                Is.EqualTo("next"),                                 "body of the next response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResponseOverTheLimit_TheRequestSendsNoMore(Ending)

        /// <summary>
        /// A streamed request whose response the client has discarded for its
        /// size, and whose caller goes on with it: a write, the end of the request
        /// and its end with trailers fail, as on a stream the server has reset,
        /// with an HTTP2StreamException that carries the code of the client's own
        /// RST_STREAM, PROTOCOL_ERROR. Nothing more goes out on the stream after
        /// that RST_STREAM (RFC 9113, Section 5.1). The trailers went out, a
        /// HEADERS frame on a closed stream, and a write and an end returned as if
        /// they had been sent.
        /// </summary>
        [TestCase("write")]
        [TestCase("end")]
        [TestCase("trailers")]
        public async Task ResponseOverTheLimit_TheRequestSendsNoMore(String Ending)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/first").WaitAsync(StepTimeout);
            var streamId    = (await transport.NextHeadersAsync()).StreamId;

            await SendBlockAsync(transport, streamId, RawHeaderBlock.Literals(OfSize([(":status", "200"), ("x-field", "")], Limit + 1)), EndStream: false);

            var discarded   = await FailureOf(upload.GetResponseAsync());

            var failure     = await FailureOf(Ending switch {
                                                  "write"  => upload.WriteAsync("more"u8.ToArray()),
                                                  "end"    => upload.CompleteRequestAsync(),
                                                  _        => upload.CompleteRequestAsync([("x-trailer", "late")])
                                              });

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/next");
            var sent        = new List<HTTP2Frame>();

            // Up to the HEADERS of the next request: trailers on the first stream
            // would be HEADERS too.
            HTTP2Frame request;

            while ((request = await transport.NextHeadersAsync(sent)).StreamId == streamId)
                sent.Add(request);

            await transport.RespondAsync(request.StreamId, "next");
            await next.WaitAsync(StepTimeout);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(discarded),  Is.EqualTo("HTTP2StreamException PROTOCOL_ERROR, MAX_HEADER_LIST_SIZE"),
                                                                                    "how the response was discarded");
                Assert.That(Describe(failure),    Is.EqualTo("HTTP2StreamException PROTOCOL_ERROR"),
                                                                                    $"how the {Ending} failed");
                Assert.That(sent.Where (frame => frame.StreamId == streamId).
                                 Select(frame => frame.Type),
                                                  Is.EqualTo(new[] { HTTP2FrameType.RST_STREAM }),
                                                                                    "what the client sent on the stream after its HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResponseBlockPastTwiceTheLimit_EndsTheConnection(Over)

        /// <summary>
        /// A response header block of twice the limit in its compressed bytes is
        /// still decoded, and its response discarded on its own. One byte more,
        /// and the client ends the connection with ENHANCE_YOUR_CALM: that is a
        /// server far past a limit it was told, and what a CONTINUATION flood
        /// (CVE-2024-27316) asks the client to hold. A streamed request, whose
        /// response head fails with what ended the connection: a buffered one may
        /// see the cancellation of the connection's token instead.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        public async Task ResponseBlockPastTwiceTheLimit_EndsTheConnection(Int32 Over)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/").WaitAsync(StepTimeout);
            var response    = upload.GetResponseAsync();
            var streamId    = (await transport.NextHeadersAsync()).StreamId;
            var frames      = RawHeaderBlock.Frames(streamId, ResponseBlockOfLength(2 * Limit + Over), EndStream: true);

            foreach (var frame in frames[..^1])
                await transport.SendAsync(frame);

            // The client ends the connection at the last frame, if at all, and then
            // reads no more: a wait for its read loop to take the frame in would
            // last the step timeout. Its failure is observed, and nothing else.
            var last = transport.SendAsync(frames[^1]);

            var failure = await FailureOf(response);

            if (Over == 0)
                await last;

            else
            {

                _ = last.ContinueWith(sent => sent.Exception, TaskContinuationOptions.OnlyOnFaulted);

                // The read loop fails the request before it is done with the end.
                await connection.Closed.WaitAsync(StepTimeout);

            }

            Assert.Multiple(() =>
            {
                Assert.That(Describe(failure),     Is.EqualTo(Over == 0 ? "HTTP2StreamException PROTOCOL_ERROR, MAX_HEADER_LIST_SIZE"
                                                                        : "HTTP2ConnectionException ENHANCE_YOUR_CALM, MAX_HEADER_LIST_SIZE"),
                                                                                                   "how the request failed");
                Assert.That(connection.IsUsable,   Is.EqualTo(Over == 0),                          "whether the connection takes new requests");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region BlocksSentBeforeTheServerReadTheReset_Decoded(FragmentSize)

        /// <summary>
        /// The server sends a response over the limit, and goes on to send its
        /// body and trailers that put a field into the dynamic table, in one frame
        /// or across CONTINUATION frames, before it reads the client's reset. The
        /// client decodes those trailers, and drops them: the next response refers
        /// to that field by its index alone, and decodes.
        /// </summary>
        [TestCase(16384)]
        [TestCase(4)]
        public async Task BlocksSentBeforeTheServerReadTheReset_Decoded(Int32 FragmentSize)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var first       = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/first");
            var streamId    = (await transport.NextHeadersAsync()).StreamId;

            await SendBlockAsync(transport, streamId, RawHeaderBlock.Literals(OfSize([(":status", "200"), ("x-field", "")], Limit + 1)), EndStream: false);

            var failure     = await FailureOf(first);

            await transport.SendAsync(HTTP2Frame.CreateData(streamId, "body"u8.ToArray()));
            await SendBlockAsync(transport, streamId, RawHeaderBlock.LiteralWithIndexing(Late.Name, Late.Value), EndStream: true, FragmentSize);

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/next");
            var skipped     = new List<HTTP2Frame>();
            var request     = await transport.NextHeadersAsync(skipped);

            // :status 200 from the static table, and the field from the dynamic one,
            // where only the trailers on the reset stream put it.
            await SendBlockAsync(transport, request.StreamId, [.. RawHeaderBlock.Indexed(8), .. RawHeaderBlock.Indexed(62)], EndStream: true);

            var nextFailure = await FailureOf(next);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo("HTTP2StreamException PROTOCOL_ERROR, MAX_HEADER_LIST_SIZE"),
                                                                                                    "how the request over the limit failed");
                Assert.That(Resets(skipped),    Is.EqualTo(new[] { $"{streamId} PROTOCOL_ERROR" }), "the client's resets before the next request");

                Assert.That(nextFailure,        Is.Null,                                            "how the next request ended");
                Assert.That(next.IsCompletedSuccessfully ? next.Result.HeaderValue(Late.Name) : null,
                                                Is.EqualTo(Late.Value),                             "the field the next response refers to");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
