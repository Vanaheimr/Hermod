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
    /// The regular fields of a response (RFC 9113, Section 8.2): a field name is
    /// lowercase (Section 8.2.1), a connection-specific field is not carried at
    /// all, and te carries "trailers" and nothing else (Section 8.2.2). A
    /// response that breaks one of these, in its header block, an interim
    /// one's, or its trailers, is malformed, and "Clients MUST NOT accept a
    /// malformed response". These are the rules the server holds a request's
    /// fields to, and nghttp2 a response's; te's "trailers" is read in any case,
    /// as nghttp2 reads it.
    ///
    /// The client used to hand all of these over. Now the response fails at
    /// the header block that carries the field, and its stream is reset with
    /// PROTOCOL_ERROR, unless both sides have ended it (Section 5.1). The
    /// connection goes on.
    /// </summary>
    [TestFixture]
    public class ClientResponseFieldTests
    {

        #region (helpers)

        private static Task<HTTP2ClientConnection> ConnectAsync(HoldingH2Transport Transport)

            => Transport.ConnectAsync(new HTTP2ClientOptions());

        /// <summary>
        /// Start a buffered GET, and return once its HEADERS are on the wire. Its
        /// side of the stream has ended with them.
        /// </summary>
        private static Task<HTTP2RequestHandle> StartAsync(HTTP2ClientConnection Connection, String Path)

            => Connection.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", Path).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// Start a streamed request, and return once its HEADERS are on the wire.
        /// Its side of the stream stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartStreamingAsync(HTTP2ClientConnection Connection, String Path)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path).
                          WaitAsync(HoldingH2Transport.StepTimeout);

        /// <summary>
        /// What the client sends from here on, up to and including the HEADERS
        /// that open <paramref name="StreamId"/>.
        /// </summary>
        private static async Task<List<HTTP2Frame>> SentUntilHeadersOfAsync(HoldingH2Transport Transport, UInt32 StreamId)
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
        /// the connection, stream 0: the stream, the type, the error code of an
        /// RST_STREAM, and END_STREAM on HEADERS.
        /// </summary>
        private static List<String> OnStreams(IEnumerable<HTTP2Frame> Frames)

            => [.. Frames.Where (frame => frame.StreamId != 0 && frame.Type is HTTP2FrameType.HEADERS or HTTP2FrameType.RST_STREAM).
                          Select(frame => $"{frame.StreamId} {frame.Type}" +
                                          (frame.Type == HTTP2FrameType.RST_STREAM
                                               ? $" {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Payload)}"
                                               : frame.EndStream ? " END_STREAM" : ""))];

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
        /// How a response fails that is malformed for <paramref name="Reason"/>.
        /// </summary>
        private static String Malformed(UInt32 StreamId, String Reason)

            => $"HTTP2StreamException on stream {StreamId}, PROTOCOL_ERROR: Malformed response: {Reason}";

        /// <summary>
        /// Send a malformed header block in answer to a buffered GET, which does
        /// not end the stream, then — as a server would that had not yet read the
        /// RST_STREAM — the rest of a response. Return how the request ended,
        /// what the client sent on its streams up to the next request's HEADERS,
        /// and the next request's answer.
        /// </summary>
        private static async Task<(Boolean Ended, String? Failure, List<String> Sent, String Next, UInt32 StreamId)>

            AnswerWithAsync(params (String Name, String Value)[][] HeaderBlocks)

        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            foreach (var block in HeaderBlocks)
                await transport.SendHeadersAsync(request.StreamId, block);

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(HTTP2Frame.CreateData(request.StreamId, new Byte[5], EndStream: true));

            var next    = await StartAsync(connection, "/next");
            var sent    = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer  = await next.Response.WaitAsync(HoldingH2Transport.StepTimeout);

            await connection.CloseAsync();

            return (ended, Describe(failure), sent, $"{answer.Status} {Encoding.ASCII.GetString(answer.Body)}", request.StreamId);

        }

        /// <summary>
        /// Assert that a response failed at the header block that made it
        /// malformed, for <paramref name="Reason"/>, that its stream was reset,
        /// and that the next request on the connection was answered.
        /// </summary>
        private static void AssertMalformed((Boolean Ended, String? Failure, List<String> Sent, String Next, UInt32 StreamId) Outcome,
                                            String Reason)
        {

            Assert.Multiple(() =>
            {

                Assert.That(Outcome.Ended,    Is.True,                                               "the response ended at the header block");
                Assert.That(Outcome.Failure,  Is.EqualTo(Malformed(Outcome.StreamId, Reason)),       "how the response failed");

                Assert.That(Outcome.Sent,     Is.EqualTo(new[] { $"{Outcome.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                 $"{Outcome.StreamId + 2} HEADERS END_STREAM" }),
                                                                                                     "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(Outcome.Next,     Is.EqualTo("200 next"),                                "the next request's response");

            });

        }

        /// <summary>
        /// Send trailers that end the stream after a valid head and its body, in
        /// answer to a buffered GET. Return how the request ended, and what the
        /// client sent on its streams up to the next request's HEADERS.
        /// </summary>
        private static async Task<(Boolean Ended, String? Failure, List<String> Sent, UInt32 StreamId)>

            AnswerWithTrailersAsync(params (String Name, String Value)[] Trailers)

        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendHeadersAsync(request.StreamId, [(":status", "200"), ("content-length", "2")]);
            await transport.SendAsync(HTTP2Frame.CreateData(request.StreamId, new Byte[2], EndStream: false));
            await transport.SendHeadersAsync(request.StreamId, Trailers, EndStream: true);

            var (ended, failure) = await EndOf(request.Response);

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await connection.CloseAsync();

            return (ended, Describe(failure), sent, request.StreamId);

        }

        #endregion


        #region UppercaseFieldName_FailsAtTheHeaders(Name)

        /// <summary>
        /// A field name is lowercase (Section 8.2.1): one uppercase letter makes
        /// the response malformed, wherever it is, and whatever field it would
        /// name. "Content-Length" is checked before it is read.
        /// </summary>
        [TestCase("Content-Type")]
        [TestCase("content-Type")]
        [TestCase("X-REQUEST-ID")]
        [TestCase("x-requesT-id")]
        [TestCase("Content-Length")]
        public async Task UppercaseFieldName_FailsAtTheHeaders(String Name)

            => AssertMalformed(await AnswerWithAsync([(":status", "200"), (Name, "5"), ("content-length", "5")]),
                               $"field name '{Name}' is not lowercase");

        #endregion

        #region EmptyFieldName_FailsAtTheHeaders()

        /// <summary>
        /// A field name is a token, which has at least one character.
        /// </summary>
        [Test]
        public async Task EmptyFieldName_FailsAtTheHeaders()

            => AssertMalformed(await AnswerWithAsync([(":status", "200"), ("", "x"), ("content-length", "5")]),
                               "empty field name");

        #endregion

        #region ConnectionSpecificField_FailsAtTheHeaders(Name, Value)

        /// <summary>
        /// A connection-specific field makes the response malformed, whatever its
        /// value (Section 8.2.2): HTTP/2 frames the message and manages the
        /// connection itself.
        /// </summary>
        [TestCase("connection",         "keep-alive")]
        [TestCase("keep-alive",         "timeout=5")]
        [TestCase("proxy-connection",   "close")]
        [TestCase("transfer-encoding",  "chunked")]
        [TestCase("upgrade",            "h2c")]
        public async Task ConnectionSpecificField_FailsAtTheHeaders(String Name, String Value)

            => AssertMalformed(await AnswerWithAsync([(":status", "200"), (Name, Value), ("content-length", "5")]),
                               $"connection-specific field '{Name}'");

        #endregion

        #region TeOtherThanTrailers_FailsAtTheHeaders(Value)

        /// <summary>
        /// te is the one exception to the connection-specific fields, and carries
        /// "trailers" and nothing else (Section 8.2.2).
        /// </summary>
        [TestCase("gzip")]
        [TestCase("trailers, deflate")]
        [TestCase("trailers;q=1")]
        [TestCase(" trailers")]
        [TestCase("")]
        public async Task TeOtherThanTrailers_FailsAtTheHeaders(String Value)

            => AssertMalformed(await AnswerWithAsync([(":status", "200"), ("te", Value), ("content-length", "5")]),
                               $"te '{Value}' other than \"trailers\"");

        #endregion

        #region TeTrailers_AndLowercaseFields_Stand(Value)

        /// <summary>
        /// te with "trailers", in any case, as nghttp2 reads it and RFC 9110's
        /// ABNF, and fields whose names are lowercase, with digits, hyphens and
        /// the other token characters in them, and whose values carry uppercase
        /// letters: the response is handed over with all of them, and nothing is
        /// reset.
        /// </summary>
        [TestCase("trailers")]
        [TestCase("Trailers")]
        [TestCase("TRAILERS")]
        public async Task TeTrailers_AndLowercaseFields_Stand(String Value)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            (String, String)[] fields = [("te", Value), ("x-request-id-2", "ABC"), ("x_a.b~c!#$%&'*+^`|", "Keep-Alive"), ("content-type", "Text/Plain")];

            await transport.SendHeadersAsync(request.StreamId, [(":status", "200"), .. fields], EndStream: true);

            var (ended, failure) = await EndOf(request.Response);

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {
                Assert.That(ended,              Is.True,  "the response ended");
                Assert.That(Describe(failure),  Is.Null,  "how the response failed");
                if (ended && failure is null)
                {
                    Assert.That(request.Response.Result.Status,                     Is.EqualTo(200),     "its status");
                    Assert.That(request.Response.Result.Headers.Where(h => h.Name[0] != ':'),  Is.EqualTo(fields),  "its fields");
                }
                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),  "what the client sent on its streams");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region MalformedInterimResponse_Fails(Name, Value, Reason)

        /// <summary>
        /// An interim response is held to the same rules as a final one: one that
        /// is malformed fails the request at its own HEADERS, before any final
        /// response arrives.
        /// </summary>
        [TestCase("Link",        "</style.css>; rel=preload",  "field name 'Link' is not lowercase")]
        [TestCase("connection",  "keep-alive",                 "connection-specific field 'connection'")]
        public async Task MalformedInterimResponse_Fails(String Name, String Value, String Reason)

            => AssertMalformed(await AnswerWithAsync([(":status", "103"), (Name, Value)]),
                               Reason);

        #endregion

        #region MalformedTrailers_FailAtTheTrailers(Name, Value, Reason)

        /// <summary>
        /// The rules hold for trailers' fields as well. The trailers end the
        /// stream, and the request's HEADERS had ended our side: the stream is
        /// closed, and takes no RST_STREAM. The client used to hand the trailers
        /// over.
        /// </summary>
        [TestCase("X-Checksum",         "abc",       "field name 'X-Checksum' is not lowercase in trailers")]
        [TestCase("transfer-encoding",  "chunked",   "connection-specific field 'transfer-encoding' in trailers")]
        [TestCase("te",                 "deflate",   "te 'deflate' other than \"trailers\" in trailers")]
        public async Task MalformedTrailers_FailAtTheTrailers(String Name, String Value, String Reason)
        {

            var (ended, failure, sent, streamId) = await AnswerWithTrailersAsync(("x-ok", "1"), (Name, Value));

            Assert.Multiple(() =>
            {
                Assert.That(ended,    Is.True,                                                  "the response ended at its trailers");
                Assert.That(failure,  Is.EqualTo(Malformed(streamId, Reason)),                  "how the response failed");
                Assert.That(sent,     Is.EqualTo(new[] { $"{streamId + 2} HEADERS END_STREAM" }), "what the client sent on its streams");
            });

        }

        #endregion

        #region ValidTrailers_Stand()

        /// <summary>
        /// Trailers with lowercase fields and te: trailers are handed over.
        /// </summary>
        [Test]
        public async Task ValidTrailers_Stand()
        {

            var (ended, failure, sent, streamId) = await AnswerWithTrailersAsync(("x-checksum", "ABC"), ("te", "trailers"));

            Assert.Multiple(() =>
            {
                Assert.That(ended,    Is.True,                                                  "the response ended at its trailers");
                Assert.That(failure,  Is.Null,                                                  "how the response failed");
                Assert.That(sent,     Is.EqualTo(new[] { $"{streamId + 2} HEADERS END_STREAM" }), "what the client sent on its streams");
            });

        }

        #endregion

        #region StreamedResponse_ConnectionSpecificField_FailsTheHead()

        /// <summary>
        /// A streamed response hands its head over at its HEADERS: a malformed one
        /// fails the head there, and its trailers, and the stream, still open on
        /// our side, is reset.
        /// </summary>
        [Test]
        public async Task StreamedResponse_ConnectionSpecificField_FailsTheHead()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendHeadersAsync(stream.StreamId, [(":status", "200"), ("transfer-encoding", "chunked")]);

            var headFailure      = (await EndOf(stream.GetResponseAsync())).Failure;
            var trailersFailure  = (await EndOf(stream.GetTrailersAsync())).Failure;

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {
                Assert.That(Describe(headFailure),      Is.EqualTo(Malformed(stream.StreamId, "connection-specific field 'transfer-encoding'")),  "how the response head failed");
                Assert.That(Describe(trailersFailure),  Is.EqualTo(Malformed(stream.StreamId, "connection-specific field 'transfer-encoding'")),  "how the trailers failed");
                Assert.That(sent,                       Is.EqualTo(new[] { $"{stream.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                           $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                                                                  "what the client sent on its streams, up to the next request's HEADERS");
            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
