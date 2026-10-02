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
using System.Reflection;
using System.Diagnostics;
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 9113, Section 8.1.1: a response is malformed if its content-length
    /// does not equal the sum of the DATA payload lengths of its body, and
    /// "Clients MUST NOT accept a malformed response". The client accepted every
    /// such response: a body longer than declared, or shorter, and a
    /// content-length that is no number, or two that disagree. The server held
    /// requests to their content-length; the client held responses to nothing.
    ///
    /// Now a malformed response fails with an HTTP2StreamException carrying
    /// PROTOCOL_ERROR, and its stream is reset with RST_STREAM PROTOCOL_ERROR (a
    /// stream error, Section 5.4.2): at its HEADERS, for a content-length that is
    /// malformed itself; at the DATA frame that takes the body past the declared
    /// length, which is not taken in; and where the body ends, at END_STREAM or
    /// at trailers, for one shorter than declared. A stream that both sides have
    /// ended by then takes no RST_STREAM (Section 5.1). Buffered and streamed
    /// responses alike; a streamed one hands its reader what came before the
    /// failure. A response that can have no content — to a HEAD request, a 204,
    /// a 304 — may declare a length it does not send, but no malformed one. The
    /// connection goes on, and its window stays right.
    /// </summary>
    [TestFixture]
    public class ClientContentLengthTests
    {

        #region (helpers)

        /// <summary>
        /// The client's MAX_FRAME_SIZE: the largest DATA frame it takes.
        /// </summary>
        private const Int32 FrameSize = 16384;

        /// <summary>
        /// A connection whose buffered responses may hold <paramref name="MaxResponseBodySize"/> bytes.
        /// </summary>
        private static Task<HTTP2ClientConnection> ConnectAsync(HoldingH2Transport  Transport,
                                                                Int64               MaxResponseBodySize = 16 * 1024 * 1024)

            => Transport.ConnectAsync(new HTTP2ClientOptions { MaxResponseBodySize = MaxResponseBodySize });

        /// <summary>
        /// Start a buffered request, and return once its HEADERS are on the wire.
        /// </summary>
        private static Task<HTTP2RequestHandle> StartAsync(HTTP2ClientConnection  Connection,
                                                           String                 Path,
                                                           HTTPMethod?            Method   = null,
                                                           Byte[]?                Body     = null)

            => Connection.StartRequestAsync(Method ?? HTTPMethod.GET, URIScheme.http, "localhost", Path, Body: Body).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// Start a streamed request, and return once its HEADERS are on the wire.
        /// Its side of the stream stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartStreamingAsync(HTTP2ClientConnection  Connection,
                                                                   String                 Path)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// The HEADERS of a response, which leave the stream open for a body
        /// unless told otherwise: a 200, unless told otherwise, with a
        /// content-length field for each of <paramref name="ContentLength"/>, as
        /// given. Every field is in HPACK's static table, or one the encoder never
        /// indexes (content-length), so no such block changes the client's dynamic
        /// table: one encoder per block decodes right, next to the blocks of the
        /// transport's own encoder.
        /// </summary>
        private static HTTP2Frame ResponseHeaders(UInt32     StreamId,
                                                  String[]?  ContentLength   = null,
                                                  Int32      Status          = 200,
                                                  Boolean    EndStream       = false)
        {

            List<(String Name, String Value)> fields = [(":status", Status.ToString())];

            foreach (var value in ContentLength ?? [])
                fields.Add(("content-length", value));

            return HTTP2Frame.CreateHeaders(StreamId,
                                            new HPACKEncoder().EncodeHeaderBlock(fields),
                                            EndStream,
                                            EndHeaders: true);

        }

        /// <summary>
        /// Trailers that end the stream, encoded by the transport's encoder: each
        /// block so encoded must be sent, and in this order.
        /// </summary>
        private static HTTP2Frame Trailers(HoldingH2Transport Transport, UInt32 StreamId)

            => HTTP2Frame.CreateHeaders(StreamId,
                                        Transport.EncodeHeaderBlock([("x-checksum", "abc")]),
                                        EndStream:  true,
                                        EndHeaders: true);

        /// <summary>
        /// A DATA frame of <paramref name="Length"/> octets.
        /// </summary>
        private static HTTP2Frame Data(UInt32 StreamId, Int64 Length, Boolean EndStream = false)

            => HTTP2Frame.CreateData(StreamId, new Byte[Length], EndStream);

        /// <summary>
        /// A padded DATA frame (RFC 9113, Section 6.1): the Pad Length octet,
        /// <paramref name="Length"/> octets of data, and <paramref name="Padding"/>
        /// octets of padding. All of them count against flow control, and only
        /// the data is the body's.
        /// </summary>
        private static HTTP2Frame PaddedData(UInt32 StreamId, Int32 Length, Byte Padding, Boolean EndStream = false)

            => new() {
                   Type      = HTTP2FrameType.DATA,
                   Flags     = HTTP2FrameFlags.PADDED | (EndStream ? HTTP2FrameFlags.END_STREAM : HTTP2FrameFlags.NONE),
                   StreamId  = StreamId,
                   Payload   = [Padding, .. new Byte[Length], .. new Byte[Padding]]
               };

        /// <summary>
        /// What the client sends from here on, up to and including the HEADERS
        /// that open <paramref name="StreamId"/>.
        /// </summary>
        private static async Task<List<HTTP2Frame>> SentUntilHeadersOfAsync(HoldingH2Transport  Transport,
                                                                            UInt32              StreamId)
        {

            var sent = new List<HTTP2Frame>();

            while (true)
            {

                var headers = await Transport.NextHeadersAsync(sent);

                sent.Add(headers);

                if (headers.StreamId == StreamId)
                    return sent;

            }

        }

        /// <summary>
        /// The frames on the client's streams, one line each, leaving out those on
        /// the connection, stream 0: the stream, the type, and the error code of
        /// an RST_STREAM, the increment of a WINDOW_UPDATE, the length of DATA,
        /// and END_STREAM where it is set.
        /// </summary>
        private static List<String> OnStreams(IEnumerable<HTTP2Frame> Frames)

            => [.. Frames.Where (frame => frame.StreamId != 0).
                          Select(frame => $"{frame.StreamId} {frame.Type}" +
                                          frame.Type switch {
                                              HTTP2FrameType.RST_STREAM     => $" {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Payload)}",
                                              HTTP2FrameType.WINDOW_UPDATE  => $" {Increment(frame)}",
                                              HTTP2FrameType.DATA           => $" ({frame.Payload.Length} bytes)",
                                              _                             => ""
                                          } +
                                          (frame.Type is HTTP2FrameType.DATA or HTTP2FrameType.HEADERS && frame.EndStream
                                               ? " END_STREAM"
                                               : ""))];

        /// <summary>
        /// The window a WINDOW_UPDATE gives (RFC 9113, Section 6.9).
        /// </summary>
        private static Int64 Increment(HTTP2Frame WindowUpdate)

            => BinaryPrimitives.ReadUInt32BigEndian(WindowUpdate.Payload) & 0x7FFFFFFFu;

        /// <summary>
        /// The window the client owes the server on the connection: given back in
        /// batches, half a connection window at a time, it is counted here until
        /// then (see ReplenishReceiveWindowsAsync).
        /// </summary>
        private static Int64 ConnectionWindowOwed(HTTP2ClientConnection Connection)

            => (Int64) typeof(HTTP2ClientConnection).
                           GetField("connectionPendingRecvUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.
                           GetValue(Connection)!;

        /// <summary>
        /// How many exchanges the connection holds: requests whose response it
        /// still takes DATA into.
        /// </summary>
        private static Int32 Exchanges(HTTP2ClientConnection Connection)

            => ((System.Collections.ICollection) typeof(HTTP2ClientConnection).
                                                     GetField("exchanges", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                     GetValue(Connection)!).Count;

        /// <summary>
        /// The bytes of DATA the connection may still send before the server grants
        /// more, as the connection counts them: less what it has sent, and less
        /// what a write has taken for a frame it has not sent yet.
        /// </summary>
        private static Int64 ConnectionSendWindow(HTTP2ClientConnection Connection)

            => ((HTTP2StreamManager) typeof(HTTP2ClientConnection).
                                         GetField("streamManager", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                         GetValue(Connection)!).ConnectionSendWindow;

        /// <summary>
        /// Wait until the connection's send window is <paramref name="Window"/>. A
        /// request body takes its window on a task of its own, and says so
        /// nowhere else.
        /// </summary>
        private static async Task<Boolean> SendWindowReachesAsync(HTTP2ClientConnection Connection, Int64 Window)
        {

            var watch = Stopwatch.StartNew();

            while (ConnectionSendWindow(Connection) != Window)
            {

                if (watch.Elapsed > HoldingH2Transport.StepTimeout)
                    return false;

                await Task.Delay(1);

            }

            return true;

        }

        /// <summary>
        /// How a call ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Call)
        {

            if (await Task.WhenAny(Call, Task.Delay(HoldingH2Transport.StepTimeout)) != Call)
                return (false, null);

            try
            {
                await Call;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        /// <summary>
        /// Read a streamed response's body to its end, or to the failure of a
        /// read: how many octets it took, and how it ended.
        /// </summary>
        private static async Task<(Int64 Read, Boolean Ended, Exception? Failure)> ReadToTheEndAsync(HTTP2ClientStream Stream)
        {

            var read = 0L;

            while (true)
            {

                var next = Stream.ReadAsync();
                var (ended, failure) = await EndOf(next);

                if (!ended || failure is not null)
                    return (read, ended, failure);

                if (next.Result is null)
                    return (read, true, null);

                read += next.Result.Length;

            }

        }

        /// <summary>
        /// A failure as the caller reads it: its type — the exact one — for a
        /// stream error the stream and the error code, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                    => null,
                   HTTP2StreamException e  => $"{e.GetType().Name} on stream {e.StreamId}, {e.ErrorCode}: {e.Message}",
                   _                       => $"{Failure.GetType().Name}: {Failure.Message}"
               };

        /// <summary>
        /// How a response fails whose body went past its declared length with the
        /// DATA frame that brought it to <paramref name="Received"/> octets.
        /// </summary>
        private static String Exceeded(UInt32 StreamId, Int64 Declared, Int64 Received)

            => $"HTTP2StreamException on stream {StreamId}, PROTOCOL_ERROR: " +
               $"Malformed response: content-length {Declared} exceeded: {Received} bytes of DATA received";

        /// <summary>
        /// How a response fails whose body ended with <paramref name="Received"/>
        /// octets, other than it declared.
        /// </summary>
        private static String Mismatch(UInt32 StreamId, Int64 Declared, Int64 Received)

            => $"HTTP2StreamException on stream {StreamId}, PROTOCOL_ERROR: " +
               $"Malformed response: content-length {Declared} does not match the {Received} bytes of DATA received";

        /// <summary>
        /// How a response fails whose content-length is no number of octets.
        /// </summary>
        private static String Invalid(UInt32 StreamId, String Value)

            => $"HTTP2StreamException on stream {StreamId}, PROTOCOL_ERROR: " +
               $"Malformed response: invalid content-length '{Value}'";

        /// <summary>
        /// How a response fails that declares two content-lengths that differ.
        /// </summary>
        private static String Conflicting(UInt32 StreamId, String First, String Second)

            => $"HTTP2StreamException on stream {StreamId}, PROTOCOL_ERROR: " +
               $"Malformed response: conflicting content-length values '{First}' and '{Second}'";

        /// <summary>
        /// A response's status and its body, read as text.
        /// </summary>
        private static String Answer(HTTP2Response Response)

            => $"{Response.Status} {Encoding.ASCII.GetString(Response.Body)}";

        #endregion


        #region BodyPastTheDeclaredLength_FailsAtTheFrameThatCrossesIt()

        /// <summary>
        /// A response that declares 10 octets and sends 11. Up to 10 the client
        /// takes the body in and waits for its end; the DATA frame that takes it
        /// past is malformed already, as no later frame brings the sum back: the
        /// response fails, and the stream is reset with PROTOCOL_ERROR. What the
        /// server sent on the stream before it read the RST_STREAM is taken in
        /// quietly, and the next request on the connection is answered. The
        /// client used to hand over the 16 octets as the response's body.
        /// </summary>
        [Test]
        public async Task BodyPastTheDeclaredLength_FailsAtTheFrameThatCrossesIt()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["10"]));
            await transport.SendAsync(Data(request.StreamId, 6));
            await transport.SendAsync(Data(request.StreamId, 4));

            var waitingAtTheDeclaredLength = !request.Response.IsCompleted;

            await transport.SendAsync(Data(request.StreamId, 1));

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, 5, EndStream: true));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer = Answer(await next.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(waitingAtTheDeclaredLength,  Is.True,                                             "the response waited for its end once its body had reached the declared length");
                Assert.That(ended,                       Is.True,                                             "the response ended once its body went past the declared length");
                Assert.That(Describe(failure),           Is.EqualTo(Exceeded(request.StreamId, 10, 11)),      "how the response failed");

                Assert.That(sent,                        Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                            $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                              "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(answer,                      Is.EqualTo("200 next"),                              "the next request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyPastADeclaredLengthAtTheLimit_IsMalformed_NotTooLarge()

        /// <summary>
        /// A response that declares as much as MaxResponseBodySize allows, and
        /// sends one octet more: both its content-length and the limit refuse
        /// the frame that brings it. The content-length goes first — the server
        /// broke the protocol, and the response is malformed, not merely too
        /// large for the client.
        /// </summary>
        [Test]
        public async Task BodyPastADeclaredLengthAtTheLimit_IsMalformed_NotTooLarge()
        {

            const Int64 limit = 1000;

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport, limit);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: [limit.ToString()]));
            await transport.SendAsync(Data(request.StreamId, limit + 1));

            var (ended, failure) = await EndOf(request.Response);

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                       "the response ended");
                Assert.That(Describe(failure),  Is.EqualTo(Exceeded(request.StreamId, limit, limit + 1)),      "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),     "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyShortOfTheDeclaredLength_FailsWhereItEnds(EndedByTrailers)

        /// <summary>
        /// A response that declares 10 octets and ends its body after 6: with
        /// END_STREAM on DATA, or with trailers, which end the body as well. The
        /// response fails where its body ends, and hands over neither body nor
        /// trailers. The server has ended its side of the stream, and the request
        /// had ended its own: the stream is closed, and takes no RST_STREAM (RFC
        /// 9113, Section 5.1). The connection goes on. The client used to accept
        /// the 6 octets as the whole body.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task BodyShortOfTheDeclaredLength_FailsWhereItEnds(Boolean EndedByTrailers)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["10"]));

            if (EndedByTrailers)
            {
                await transport.SendAsync(Data(request.StreamId, 6));
                await transport.SendAsync(Trailers(transport, request.StreamId));
            }
            else
                await transport.SendAsync(Data(request.StreamId, 6, EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var exchanges         = Exchanges(connection);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer            = Answer(await next.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                             "the response ended where its body did");
                Assert.That(Describe(failure),  Is.EqualTo(Mismatch(request.StreamId, 10, 6)),       "how the response failed");

                Assert.That(exchanges,          Is.Zero,                                             "exchanges left");

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                     "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                              "the next request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region DeclaredLength_HeadersEndTheStream_Fails()

        /// <summary>
        /// A response that declares 5 octets, and whose HEADERS end the stream:
        /// its body is empty, short of what it declares. It fails, and the stream,
        /// closed on both sides, takes no RST_STREAM. The client used to accept
        /// it as a response without a body.
        /// </summary>
        [Test]
        public async Task DeclaredLength_HeadersEndTheStream_Fails()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["5"], EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                             "the response ended with its HEADERS");
                Assert.That(Describe(failure),  Is.EqualTo(Mismatch(request.StreamId, 5, 0)),        "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                     "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region InvalidContentLength_FailsAtTheHeaders(Value)

        /// <summary>
        /// A content-length that is no number of octets (RFC 9110, Section 8.6:
        /// 1*DIGIT) makes the response malformed: it fails at its HEADERS, before
        /// any of its body arrives, and the stream is reset with PROTOCOL_ERROR.
        /// The body the server sent before it read the RST_STREAM goes back to
        /// the connection, and the next request is answered. A list of equal
        /// values is no number either: the client rejects it, as RFC 9110 lets it,
        /// and as the server does. The client used to accept them all.
        /// </summary>
        [TestCase("abc")]
        [TestCase("-1")]
        [TestCase("+5")]
        [TestCase(" 5")]
        [TestCase("5 ")]
        [TestCase("5, 5")]
        [TestCase("0x5")]
        [TestCase("")]
        [TestCase("99999999999999999999")]
        public async Task InvalidContentLength_FailsAtTheHeaders(String Value)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: [Value]));

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, 5, EndStream: true));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer = Answer(await next.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                             "the response ended at its HEADERS");
                Assert.That(Describe(failure),  Is.EqualTo(Invalid(request.StreamId, Value)),        "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                     "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                              "the next request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ConflictingContentLengths_FailAtTheHeaders()

        /// <summary>
        /// Two content-length fields that differ make the response malformed: it
        /// fails at its HEADERS, whichever the body would match, and the stream is
        /// reset with PROTOCOL_ERROR. The client used to take the first.
        /// </summary>
        [Test]
        public async Task ConflictingContentLengths_FailAtTheHeaders()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["5", "6"]));

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, 5, EndStream: true));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                 "the response ended at its HEADERS");
                Assert.That(Describe(failure),  Is.EqualTo(Conflicting(request.StreamId, "5", "6")),     "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                         "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyOfTheDeclaredLength_Stands(ContentLength, EndedByTrailers)

        /// <summary>
        /// A response whose body is as long as it declares, over two DATA frames,
        /// the last one padded: padding counts against flow control, not against
        /// the body. Ended by END_STREAM on DATA, or by trailers, which it hands
        /// over. With one content-length, or two that are equal. It stands as it
        /// did, and nothing is reset.
        /// </summary>
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public async Task BodyOfTheDeclaredLength_Stands(Int32 ContentLengths, Boolean EndedByTrailers)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: [.. Enumerable.Repeat("10", ContentLengths)]));
            await transport.SendAsync(Data(request.StreamId, 6));
            await transport.SendAsync(PaddedData(request.StreamId, 4, Padding: 200, EndStream: !EndedByTrailers));

            if (EndedByTrailers)
                await transport.SendAsync(Trailers(transport, request.StreamId));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                               "the response ended");
                Assert.That(Describe(failure),  Is.Null,                                               "how the response failed");

                if (ended && failure is null)
                {
                    Assert.That(request.Response.Result.Body.Length,       Is.EqualTo(10),            "length of its body");
                    Assert.That(request.Response.Result.Trailers.Count,    Is.EqualTo(EndedByTrailers ? 1 : 0),
                                                                                                       "trailers handed over");
                }

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                       "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ZeroDeclared_NoBody_Stands(HeadersEndTheStream)

        /// <summary>
        /// A response that declares no body, and sends none: its HEADERS end the
        /// stream, or an empty DATA frame does. It stands.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task ZeroDeclared_NoBody_Stands(Boolean HeadersEndTheStream)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["0"], EndStream: HeadersEndTheStream));

            if (!HeadersEndTheStream)
                await transport.SendAsync(Data(request.StreamId, 0, EndStream: true));

            var (ended, failure) = await EndOf(request.Response);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                               "the response ended");
                Assert.That(Describe(failure),  Is.Null,                                               "how the response failed");

                if (ended && failure is null)
                    Assert.That(Answer(request.Response.Result),  Is.EqualTo("200 "),                 "the response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region NoContentResponse_DeclaredLengthWithoutABody_Stands(Method, Status, HeadersEndTheStream)

        /// <summary>
        /// A response that can have no content may declare the content-length of
        /// the representation it leaves out (RFC 9113, Section 8.1.1; RFC 9110,
        /// Section 8.6): the answer to a HEAD request, a 304 and a 204. Sent
        /// without a body — its HEADERS end the stream, or an empty DATA frame
        /// does — it stands.
        /// </summary>
        [TestCase("HEAD", 200, true)]
        [TestCase("HEAD", 200, false)]
        [TestCase("GET",  304, true)]
        [TestCase("GET",  304, false)]
        [TestCase("GET",  204, true)]
        [TestCase("GET",  204, false)]
        public async Task NoContentResponse_DeclaredLengthWithoutABody_Stands(String Method, Int32 Status, Boolean HeadersEndTheStream)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/", Method == "HEAD" ? HTTPMethod.HEAD : HTTPMethod.GET);

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["10"], Status: Status, EndStream: HeadersEndTheStream));

            if (!HeadersEndTheStream)
                await transport.SendAsync(Data(request.StreamId, 0, EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                               "the response ended");
                Assert.That(Describe(failure),  Is.Null,                                               "how the response failed");

                if (ended && failure is null)
                    Assert.That(Answer(request.Response.Result),  Is.EqualTo($"{Status} "),          "the response");

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                       "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region NoContentResponse_InvalidContentLength_Fails(Method, Status)

        /// <summary>
        /// A response that can have no content need not send the length it
        /// declares, but what it declares must still be a length: a
        /// content-length that is no number, or two that differ, fail it at its
        /// HEADERS like any other — with no RST_STREAM where the HEADERS end the
        /// stream, closed on both sides.
        /// </summary>
        [TestCase("HEAD", 200, "abc")]
        [TestCase("GET",  304, "abc")]
        [TestCase("GET",  204, "abc")]
        [TestCase("HEAD", 200, "5|6")]
        public async Task NoContentResponse_InvalidContentLength_Fails(String Method, Int32 Status, String ContentLength)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/", Method == "HEAD" ? HTTPMethod.HEAD : HTTPMethod.GET);

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            var values = ContentLength.Split('|');

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: values, Status: Status, EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                               "the response ended with its HEADERS");
                Assert.That(Describe(failure),  Is.EqualTo(values.Length == 1
                                                               ? Invalid    (request.StreamId, values[0])
                                                               : Conflicting(request.StreamId, values[0], values[1])),
                                                                                                       "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                       "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RefusedFrameAndWhatFollowsIt_GoBackToTheConnectionWindow()

        /// <summary>
        /// The DATA frame that takes a body past its declared length counts
        /// against the connection's window like every other, its padding included
        /// (RFC 9113, Section 6.1), and is given back to it like every other, as
        /// is the DATA the server sent on the stream before it read the
        /// RST_STREAM (Section 6.9). Not to the stream's window: no WINDOW_UPDATE
        /// goes out on the stream, before its RST_STREAM or after it. The refused
        /// frame here is the one that fills half the stream's window, at which a
        /// stream WINDOW_UPDATE would be due.
        /// </summary>
        [Test]
        public async Task RefusedFrameAndWhatFollowsIt_GoBackToTheConnectionWindow()
        {

            const Int64 declared = 520_000;

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big");

            // Up to the request's HEADERS: the connection's opening frames, the
            // WINDOW_UPDATE that raises its window among them.
            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: [declared.ToString()]));

            // 31 × 16 384 = 507 904 octets, short of the declared length.
            for (var i = 0; i < 31; i++)
                await transport.SendAsync(Data(request.StreamId, FrameSize));

            // 16 128 octets of data take the body past it, to 524 032; with the
            // Pad Length octet and 255 of padding, the frame is 16 384 octets to
            // flow control, and brings the stream to half its window.
            await transport.SendAsync(PaddedData(request.StreamId, 16_128, Padding: 255));

            var failure = (await EndOf(request.Response)).Failure;

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, FrameSize));
            await transport.SendAsync(Data(request.StreamId, FrameSize, EndStream: true));

            var owed       = ConnectionWindowOwed(connection);
            var exchanges  = Exchanges(connection);

            var next   = await StartAsync(connection, "/next");
            var sent   = await SentUntilHeadersOfAsync(transport, next.StreamId);

            var given  = sent.Where(frame => frame.StreamId == 0 && frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                              Sum  (Increment);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo(Exceeded(request.StreamId, declared, 524_032)),  "how the response failed");

                Assert.That(OnStreams(sent),    Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),  "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(given + owed,       Is.EqualTo(34 * FrameSize),                                  "the connection's window given back, and owed, for the 34 DATA frames the server sent");

                Assert.That(exchanges,          Is.Zero,                                                     "exchanges left: none takes the DATA in after the refusal");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RequestWaitingForAStreamSlot_GetsTheMalformedResponsesSlot(Crossing)

        /// <summary>
        /// The server takes one stream at a time, and a second request waits for
        /// the first's. The first response turns out malformed: past its declared
        /// length, where its stream is reset, or short of it at END_STREAM, where
        /// the stream is closed on both sides and takes no reset. Either way the
        /// second request gets the slot — after the RST_STREAM, if one goes out,
        /// so the server never counts the two streams at once.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task RequestWaitingForAStreamSlot_GetsTheMalformedResponsesSlot(Boolean Crossing)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);

            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, 1)));

            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            var waiting     = connection.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/next");

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["10"]));
            await transport.SendAsync(Data(request.StreamId, Crossing ? 11 : 6, EndStream: !Crossing));

            var failure     = (await EndOf(request.Response)).Failure;

            // The waiting request returns once its HEADERS are on the wire.
            var started     = (await EndOf(waiting)).Ended;
            var sent        = new List<String>();
            var answer      = "";

            if (started)
            {

                sent        = OnStreams(await SentUntilHeadersOfAsync(transport, waiting.Result.StreamId));

                await transport.RespondAsync(waiting.Result.StreamId, "next");

                answer      = Answer(await waiting.Result.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            }

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo(Crossing
                                                               ? Exceeded(request.StreamId, 10, 11)
                                                               : Mismatch(request.StreamId, 10,  6)),          "how the response failed");
                Assert.That(started,            Is.True,                                                        "the waiting request got a stream");

                Assert.That(sent,               Is.EqualTo(Crossing
                                                               ? new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                         $"{request.StreamId + 2} HEADERS END_STREAM" }
                                                               : new[] { $"{request.StreamId + 2} HEADERS END_STREAM" }),
                                                                                                                "what the client sent on its streams, up to the waiting request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                                         "the waiting request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RequestBodyBeingSent_ShortResponse_GoesNoFurtherThanTheReset()

        /// <summary>
        /// A request body larger than its stream's send window is waiting for
        /// more of it when its response ends short of its declared length. The
        /// server has ended its side of the stream, the client has not: the
        /// stream is half-closed (remote), and a stream error there still goes
        /// out as RST_STREAM PROTOCOL_ERROR (RFC 9113, Section 5.4.2). The body
        /// goes no further, even once the server grants the window for more of
        /// it. The client used to accept the response, and send the rest of the
        /// body to the server.
        /// </summary>
        [Test]
        public async Task RequestBodyBeingSent_ShortResponse_GoesNoFurtherThanTheReset()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);

            // One frame's worth of window per stream: the body DATA the test leaves
            // unread stays well short of what the transport's pipe holds before it
            // stops the client's writes.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, FrameSize)));

            var request     = await StartAsync(connection, "/upload", HTTPMethod.POST, new Byte[100_000]);

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            // The body takes all its stream's window, and waits for more.
            var waiting = await SendWindowReachesAsync(connection, 65_535 - FrameSize);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: ["10"]));
            await transport.SendAsync(Data(request.StreamId, 6, EndStream: true));

            var failure = (await EndOf(request.Response)).Failure;

            // Window for another frame of the body, which the connection has as well.
            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(request.StreamId, FrameSize));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(waiting,            Is.True,                                             "the body waited for window, with all of its stream's taken");
                Assert.That(Describe(failure),  Is.EqualTo(Mismatch(request.StreamId, 10, 6)),       "how the response failed");

                Assert.That(sent.SkipWhile(line => line != $"{request.StreamId} RST_STREAM PROTOCOL_ERROR").ToList(),
                                                Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                     "what the client sent on its streams from the RST_STREAM on, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion


        #region StreamedBodyPastTheDeclaredLength_ReaderGetsWhatCameBefore_ThenTheFailure()

        /// <summary>
        /// A streamed response that declares 10 octets and sends 11. The reader
        /// gets the 6 octets of the frame before the one that crosses the
        /// declared length, which is not handed over; the next read fails with
        /// the stream error, as do the trailers. The stream is reset with
        /// PROTOCOL_ERROR, and a write on it fails with that code. The client
        /// used to hand the reader all 16 octets as the body.
        /// </summary>
        [Test]
        public async Task StreamedBodyPastTheDeclaredLength_ReaderGetsWhatCameBefore_ThenTheFailure()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendAsync(ResponseHeaders(stream.StreamId, ContentLength: ["10"]));
            await transport.SendAsync(Data(stream.StreamId, 6));
            await transport.SendAsync(Data(stream.StreamId, 5));

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(stream.StreamId, 5, EndStream: true));

            var head                         = await stream.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var (read, ended, readFailure)   = await ReadToTheEndAsync(stream);
            var trailersFailure              = (await EndOf(stream.GetTrailersAsync())).Failure;
            var writeFailure                 = (await EndOf(stream.WriteAsync([1, 2, 3]))).Failure;

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(head.Status,                Is.EqualTo(200),                                    "status of the response");
                Assert.That(read,                       Is.EqualTo(6),                                      "octets read before the failure");
                Assert.That(ended,                      Is.True,                                            "the reads ended");
                Assert.That(Describe(readFailure),      Is.EqualTo(Exceeded(stream.StreamId, 10, 11)),      "how the read after them failed");
                Assert.That(Describe(trailersFailure),  Is.EqualTo(Exceeded(stream.StreamId, 10, 11)),      "how the trailers failed");

                Assert.That(Describe(writeFailure),     Is.EqualTo($"HTTP2StreamException on stream {stream.StreamId}, PROTOCOL_ERROR: Stream reset by client: PROTOCOL_ERROR"),
                                                                                                            "how a write on the stream failed");

                Assert.That(sent,                       Is.EqualTo(new[] { $"{stream.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                           $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                            "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region StreamedBodyShortOfTheDeclaredLength_FailsWhereItEnds(EndedByTrailers)

        /// <summary>
        /// A streamed response that declares 10 octets and ends its body after 6,
        /// with END_STREAM on DATA or with trailers. The reader gets the 6 octets;
        /// where it used to read the end of the body, its read fails with the
        /// stream error, as do the trailers. The request's side of the stream is
        /// still open, so the stream is reset with PROTOCOL_ERROR, and a write on
        /// it fails with that code.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task StreamedBodyShortOfTheDeclaredLength_FailsWhereItEnds(Boolean EndedByTrailers)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendAsync(ResponseHeaders(stream.StreamId, ContentLength: ["10"]));

            if (EndedByTrailers)
            {
                await transport.SendAsync(Data(stream.StreamId, 6));
                await transport.SendAsync(Trailers(transport, stream.StreamId));
            }
            else
                await transport.SendAsync(Data(stream.StreamId, 6, EndStream: true));

            var (read, ended, readFailure)   = await ReadToTheEndAsync(stream);
            var trailersFailure              = (await EndOf(stream.GetTrailersAsync())).Failure;
            var writeFailure                 = (await EndOf(stream.WriteAsync([1, 2, 3]))).Failure;

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(read,                       Is.EqualTo(6),                                      "octets read before the failure");
                Assert.That(ended,                      Is.True,                                            "the reads ended");
                Assert.That(Describe(readFailure),      Is.EqualTo(Mismatch(stream.StreamId, 10, 6)),       "how the read at the end of the body failed");
                Assert.That(Describe(trailersFailure),  Is.EqualTo(Mismatch(stream.StreamId, 10, 6)),       "how the trailers failed");

                Assert.That(Describe(writeFailure),     Is.EqualTo($"HTTP2StreamException on stream {stream.StreamId}, PROTOCOL_ERROR: Stream reset by client: PROTOCOL_ERROR"),
                                                                                                            "how a write on the stream failed");

                Assert.That(sent,                       Is.EqualTo(new[] { $"{stream.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                           $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                            "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region StreamedResponse_InvalidContentLength_FailsTheHead()

        /// <summary>
        /// A streamed response whose content-length is no number fails at its
        /// HEADERS: its head is not handed over, and its body and trailers fail
        /// alike. The stream is reset with PROTOCOL_ERROR.
        /// </summary>
        [Test]
        public async Task StreamedResponse_InvalidContentLength_FailsTheHead()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendAsync(ResponseHeaders(stream.StreamId, ContentLength: ["ten"]));

            var headFailure                  = (await EndOf(stream.GetResponseAsync())).Failure;
            var (read, ended, readFailure)   = await ReadToTheEndAsync(stream);
            var trailersFailure              = (await EndOf(stream.GetTrailersAsync())).Failure;

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(Describe(headFailure),      Is.EqualTo(Invalid(stream.StreamId, "ten")),        "how the response head failed");
                Assert.That(read,                       Is.Zero,                                            "octets read");
                Assert.That(Describe(readFailure),      Is.EqualTo(Invalid(stream.StreamId, "ten")),        "how the read failed");
                Assert.That(Describe(trailersFailure),  Is.EqualTo(Invalid(stream.StreamId, "ten")),        "how the trailers failed");

                Assert.That(sent,                       Is.EqualTo(new[] { $"{stream.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                           $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                            "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region StreamedBodyOfTheDeclaredLength_Stands()

        /// <summary>
        /// A streamed response whose body is as long as it declares, ended by
        /// trailers: the reader gets all of it, then the end of it, and the
        /// trailers are handed over.
        /// </summary>
        [Test]
        public async Task StreamedBodyOfTheDeclaredLength_Stands()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendAsync(ResponseHeaders(stream.StreamId, ContentLength: ["10"]));
            await transport.SendAsync(Data(stream.StreamId, 6));
            await transport.SendAsync(PaddedData(stream.StreamId, 4, Padding: 100));
            await transport.SendAsync(Trailers(transport, stream.StreamId));

            var (read, ended, readFailure)   = await ReadToTheEndAsync(stream);
            var trailers                     = await stream.GetTrailersAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(read,                   Is.EqualTo(10),     "octets read");
                Assert.That(ended,                  Is.True,            "the reads ended");
                Assert.That(Describe(readFailure),  Is.Null,            "how the reads failed");
                Assert.That(trailers,               Is.EqualTo(new[] { ("x-checksum", "abc") }),
                                                                        "the trailers");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region Tunnel_ContentLengthOfItsAnswer_BoundsNothing()

        /// <summary>
        /// A CONNECT tunnel carries bytes, not a body: a content-length on its 2xx
        /// answer, which a server must not send and a client must ignore (RFC
        /// 9110, Section 9.3.6), bounds nothing. The tunnel reads past it.
        /// </summary>
        [Test]
        public async Task Tunnel_ContentLengthOfItsAnswer_BoundsNothing()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var opening     = connection.OpenTunnelAsync("echo.internal:443");
            var connect     = await transport.NextHeadersAsync();

            await transport.SendAsync(ResponseHeaders(connect.StreamId, ContentLength: ["5"]));

            var tunnel      = await opening.WaitAsync(HoldingH2Transport.StepTimeout);

            await transport.SendAsync(Data(connect.StreamId, 20));

            var bytes       = await tunnel.ReadAsync(CancellationToken.None).WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.That(bytes?.Length, Is.EqualTo(20), "octets the tunnel read");

            await connection.CloseAsync();

        }

        #endregion

    }

}
