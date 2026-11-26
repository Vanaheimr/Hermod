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
    /// The pseudo-header fields of a response (RFC 9113, Sections 8.1.1 and
    /// 8.3.2). A response carries exactly one, :status, before every regular
    /// field, and none in its trailers; anything else makes it malformed, and
    /// "Clients MUST NOT accept a malformed response". The status code is three
    /// digits (RFC 9110, Section 15). One from 600 to 999 is invalid, but not
    /// malformed: a client SHOULD process such a response as if it had a 5xx
    /// status, and so it is handed over with its code, as the HTTP/1.1 and the
    /// HTTP/3 client hand it over. Below 100 there is neither a final nor an
    /// interim response.
    ///
    /// The client used to read :status with Int32.TryParse, at the end of the
    /// response: it took "42", "1000", "+200" and " 200" as status codes, the
    /// first of two :status fields, a :path or a :status after a regular field,
    /// and a :status in the trailers. A response with no :status failed only
    /// once it had ended, and a streamed one handed its reader a head with
    /// status 0. Now a response that is malformed for its pseudo-header fields
    /// fails at the header block that makes it so, an interim one included, and
    /// its stream is reset with PROTOCOL_ERROR, unless both sides have ended it
    /// (Section 5.1). The connection goes on.
    /// </summary>
    [TestFixture]
    public class ClientResponsePseudoHeaderTests
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

        #endregion


        #region ThreeDigitStatus_IsHandedOverWithItsCode(Status)

        /// <summary>
        /// Every three-digit status from 200 to 999 is a final response with that
        /// code: 600 to 999 are invalid (RFC 9110, Section 15), not malformed,
        /// and it is the caller's to read them as a 5xx.
        /// </summary>
        [TestCase("200", 200)]
        [TestCase("404", 404)]
        [TestCase("599", 599)]
        [TestCase("600", 600)]
        [TestCase("999", 999)]
        public async Task ThreeDigitStatus_IsHandedOverWithItsCode(String Value, Int32 Status)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendHeadersAsync(request.StreamId, [(":status", Value), ("content-type", "text/plain")], EndStream: true);

            var (ended, failure) = await EndOf(request.Response);

            Assert.Multiple(() =>
            {
                Assert.That(ended,              Is.True,  "the response ended");
                Assert.That(Describe(failure),  Is.Null,  "how the response failed");
                if (ended && failure is null)
                    Assert.That(request.Response.Result.Status, Is.EqualTo(Status), "its status");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region InvalidStatus_FailsAtTheHeaders(Value)

        /// <summary>
        /// A :status that is not three digits, or lies below 100, makes the
        /// response malformed. Int32.TryParse took all of these but the last
        /// two, and the client handed the response over with the number.
        /// </summary>
        [TestCase("000")]
        [TestCase("099")]
        [TestCase("42")]
        [TestCase("1000")]
        [TestCase("+200")]
        [TestCase(" 200")]
        [TestCase("200 ")]
        [TestCase("2x0")]
        [TestCase("")]
        public async Task InvalidStatus_FailsAtTheHeaders(String Value)

            => AssertMalformed(await AnswerWithAsync([(":status", Value), ("content-length", "5")]),
                               $"invalid :status '{Value}'");

        #endregion

        #region MissingStatus_FailsAtTheHeaders()

        /// <summary>
        /// A response without a :status fails at its HEADERS. It used to fail
        /// only once it had ended, and its stream was not reset.
        /// </summary>
        [Test]
        public async Task MissingStatus_FailsAtTheHeaders()

            => AssertMalformed(await AnswerWithAsync([("content-length", "5")]),
                               "missing :status");

        #endregion

        #region DuplicateStatus_FailsAtTheHeaders()

        /// <summary>
        /// Two :status fields make the response malformed, even two that agree.
        /// The client used to take the first.
        /// </summary>
        [TestCase("200", "204")]
        [TestCase("200", "200")]
        public async Task DuplicateStatus_FailsAtTheHeaders(String First, String Second)

            => AssertMalformed(await AnswerWithAsync([(":status", First), (":status", Second), ("content-length", "5")]),
                               "duplicate pseudo-header field ':status'");

        #endregion

        #region OtherPseudoHeader_FailsAtTheHeaders(Name)

        /// <summary>
        /// :status is the one pseudo-header field a response has: a request's,
        /// or one no specification defines, makes it malformed.
        /// </summary>
        [TestCase(":path")]
        [TestCase(":method")]
        [TestCase(":authority")]
        [TestCase(":scheme")]
        [TestCase(":protocol")]
        [TestCase(":foo")]
        public async Task OtherPseudoHeader_FailsAtTheHeaders(String Name)

            => AssertMalformed(await AnswerWithAsync([(":status", "200"), (Name, "x"), ("content-length", "5")]),
                               $"pseudo-header field '{Name}' in a response");

        #endregion

        #region StatusAfterARegularField_FailsAtTheHeaders()

        /// <summary>
        /// Pseudo-header fields come before every regular field (Section 8.3).
        /// </summary>
        [Test]
        public async Task StatusAfterARegularField_FailsAtTheHeaders()

            => AssertMalformed(await AnswerWithAsync([("content-length", "5"), (":status", "200")]),
                               "pseudo-header field ':status' after a regular field");

        #endregion

        #region MalformedInterimResponse_Fails()

        /// <summary>
        /// An interim response is held to the same rules as a final one: one that
        /// is malformed fails the request at its own HEADERS, before any final
        /// response arrives.
        /// </summary>
        [Test]
        public async Task MalformedInterimResponse_Fails()

            => AssertMalformed(await AnswerWithAsync([(":status", "103"), (":path", "/")]),
                               "pseudo-header field ':path' in a response");

        #endregion

        #region ValidInterimResponse_ThenFinal_Stands()

        /// <summary>
        /// A 103 and then a 200: both pass, the 103 is handed over as an interim
        /// response, and nothing is reset.
        /// </summary>
        [Test]
        public async Task ValidInterimResponse_ThenFinal_Stands()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendHeadersAsync(request.StreamId, [(":status", "103"), ("link", "</style.css>; rel=preload")]);
            await transport.SendHeadersAsync(request.StreamId, [(":status", "200")], EndStream: true);

            var (ended, failure) = await EndOf(request.Response);

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {
                Assert.That(ended,              Is.True,  "the response ended");
                Assert.That(Describe(failure),  Is.Null,  "how the response failed");
                if (ended && failure is null)
                {
                    Assert.That(request.Response.Result.Status,                                         Is.EqualTo(200),               "its status");
                    Assert.That(request.Response.Result.InformationalResponses.Select(r => r.Status),  Is.EqualTo(new[] { 103 }),     "its interim responses");
                }
                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),  "what the client sent on its streams");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region PseudoHeaderInTrailers_FailsAtTheTrailers()

        /// <summary>
        /// Trailers carry no pseudo-header fields (Section 8.1). The trailers end
        /// the stream, and the request's HEADERS had ended our side: the stream
        /// is closed, and takes no RST_STREAM. The client used to hand the
        /// trailers over.
        /// </summary>
        [Test]
        public async Task PseudoHeaderInTrailers_FailsAtTheTrailers()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendHeadersAsync(request.StreamId, [(":status", "200"), ("content-length", "2")]);
            await transport.SendAsync(HTTP2Frame.CreateData(request.StreamId, new Byte[2], EndStream: false));
            await transport.SendHeadersAsync(request.StreamId, [("x-checksum", "abc"), (":status", "500")], EndStream: true);

            var (ended, failure) = await EndOf(request.Response);

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {
                Assert.That(ended,              Is.True,                                                                    "the response ended at its trailers");
                Assert.That(Describe(failure),  Is.EqualTo(Malformed(request.StreamId, "pseudo-header field ':status' in trailers")), "how the response failed");
                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),                "what the client sent on its streams");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region StreamedResponse_InvalidStatus_FailsTheHead()

        /// <summary>
        /// A streamed response hands its head over at its HEADERS: a malformed one
        /// fails the head there, and its trailers, and the stream, still open on
        /// our side, is reset. It used to hand over a head with status 0 for a
        /// :status that was no number.
        /// </summary>
        [Test]
        public async Task StreamedResponse_InvalidStatus_FailsTheHead()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await StartStreamingAsync(connection, "/");

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendHeadersAsync(stream.StreamId, [(":status", "2OO")]);

            var headFailure      = (await EndOf(stream.GetResponseAsync())).Failure;
            var trailersFailure  = (await EndOf(stream.GetTrailersAsync())).Failure;

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {
                Assert.That(Describe(headFailure),      Is.EqualTo(Malformed(stream.StreamId, "invalid :status '2OO'")),  "how the response head failed");
                Assert.That(Describe(trailersFailure),  Is.EqualTo(Malformed(stream.StreamId, "invalid :status '2OO'")),  "how the trailers failed");
                Assert.That(sent,                       Is.EqualTo(new[] { $"{stream.StreamId} RST_STREAM PROTOCOL_ERROR",
                                                                           $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                                          "what the client sent on its streams, up to the next request's HEADERS");
            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
