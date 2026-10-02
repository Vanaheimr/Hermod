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
    /// The body of a buffered response — SendRequestAsync and StartRequestAsync
    /// collect it whole before they hand it over — is bounded by
    /// HTTP2ClientOptions.MaxResponseBodySize. It was bounded by nothing: the
    /// client gave back the window of every DATA frame as it took it in, so a
    /// server could make it buffer as much as the server cared to send.
    /// MaxDecodedBodySize did not bound it either: it bounds what decoding makes
    /// of a body, and only once the whole of it has been taken in.
    ///
    /// Now the DATA frame that would take the body past the limit is refused: the
    /// response fails with an HTTP2ResponseTooLargeException, and the stream is
    /// reset with RST_STREAM CANCEL. A response that declares a content-length
    /// above the limit fails the same way at its HEADERS, unless it can have no
    /// body. The refused frame, and what the server sent on the stream before it
    /// read the RST_STREAM, go back to the connection's window; nothing of the
    /// request follows the RST_STREAM; the stream's slot is free once it is on
    /// the wire, and the connection goes on.
    /// </summary>
    [TestFixture]
    public class ClientResponseBodyLimitTests
    {

        #region (helpers)

        /// <summary>
        /// The MaxResponseBodySize of most of these tests: small, so that a few
        /// short DATA frames go past it.
        /// </summary>
        private const Int64 Limit = 1000;

        /// <summary>
        /// The client's MAX_FRAME_SIZE: the largest DATA frame it takes.
        /// </summary>
        private const Int32 FrameSize = 16384;

        /// <summary>
        /// A connection whose buffered responses may hold <paramref name="MaxResponseBodySize"/> bytes.
        /// </summary>
        private static Task<HTTP2ClientConnection> ConnectAsync(HoldingH2Transport  Transport,
                                                                Int64               MaxResponseBodySize = Limit)

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
        /// The HEADERS of a response, which leave the stream open for a body
        /// unless told otherwise: a 200, unless told otherwise, with a
        /// content-length, if given one. Every field is in HPACK's static table,
        /// or one the encoder never indexes (content-length), so no such block
        /// changes the client's dynamic table: one encoder per block decodes
        /// right, next to the blocks of HoldingH2Transport.RespondAsync, which has
        /// an encoder of its own.
        /// </summary>
        private static HTTP2Frame ResponseHeaders(UInt32   StreamId,
                                                  Int64?   ContentLength   = null,
                                                  Int32    Status          = 200,
                                                  Boolean  EndStream       = false)
        {

            List<(String Name, String Value)> fields = [(":status", Status.ToString())];

            if (ContentLength is not null)
                fields.Add(("content-length", ContentLength.Value.ToString()));

            return HTTP2Frame.CreateHeaders(StreamId,
                                            new HPACKEncoder().EncodeHeaderBlock(fields),
                                            EndStream,
                                            EndHeaders: true);

        }

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
        /// A failure as the caller reads it: its type, for a stream error the
        /// stream and the error code, for a response too large what it says
        /// about its size, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                              => null,
                   HTTP2ResponseTooLargeException e  => $"{nameof(HTTP2ResponseTooLargeException)} on stream {e.StreamId}, {e.ErrorCode}, " +
                                                        $"limit {e.Limit}, content-length {e.ContentLength?.ToString() ?? "-"}, received {e.Received}: {e.Message}",
                   HTTP2StreamException e            => $"{nameof(HTTP2StreamException)} on stream {e.StreamId}, {e.ErrorCode}: {e.Message}",
                   _                                 => $"{Failure.GetType().Name}: {Failure.Message}"
               };

        /// <summary>
        /// How a response fails whose body went past <paramref name="Limit"/>
        /// with the DATA frame that brought it to <paramref name="Received"/>
        /// octets, as <see cref="Describe"/> shows it.
        /// </summary>
        private static String BodyTooLarge(UInt32 StreamId, Int64 Limit, Int64 Received)

            => $"{nameof(HTTP2ResponseTooLargeException)} on stream {StreamId}, CANCEL, limit {Limit}, content-length -, received {Received}: " +
               $"Response body exceeds the {Limit}-byte limit of a buffered response (MaxResponseBodySize): {Received} bytes received. " +
               $"Stream reset by client: CANCEL";

        /// <summary>
        /// How a response fails that declared a content-length above
        /// <paramref name="Limit"/>, as <see cref="Describe"/> shows it.
        /// </summary>
        private static String DeclaredTooLarge(UInt32 StreamId, Int64 Limit, Int64 ContentLength)

            => $"{nameof(HTTP2ResponseTooLargeException)} on stream {StreamId}, CANCEL, limit {Limit}, content-length {ContentLength}, received 0: " +
               $"Declared content-length {ContentLength} exceeds the {Limit}-byte limit of a buffered response (MaxResponseBodySize). " +
               $"Stream reset by client: CANCEL";

        /// <summary>
        /// A response's status and its body, read as text.
        /// </summary>
        private static String Answer(HTTP2Response Response)

            => $"{Response.Status} {Encoding.ASCII.GetString(Response.Body)}";

        #endregion


        #region BodyPastTheLimit_FailsTheResponse_ResetsTheStream()

        /// <summary>
        /// A response without a content-length, whose body goes past the limit.
        /// Up to the limit the client takes it in, and the response waits for
        /// more. The DATA frame that would take it past is refused: the response
        /// fails, and the stream is reset with CANCEL. What the server sent on the
        /// stream before it read the RST_STREAM is taken in quietly, and the next
        /// request on the connection is answered. The client used to take in the
        /// whole body, however large.
        /// </summary>
        [Test]
        public async Task BodyPastTheLimit_FailsTheResponse_ResetsTheStream()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId));
            await transport.SendAsync(Data(request.StreamId, 600));
            await transport.SendAsync(Data(request.StreamId, 400));

            var waitingAtTheLimit = !request.Response.IsCompleted;

            await transport.SendAsync(Data(request.StreamId, 1));

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, 500, EndStream: true));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer = Answer(await next.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(waitingAtTheLimit,  Is.True,                                                    "the response waited for more once its body had reached the limit");
                Assert.That(ended,              Is.True,                                                    "the response ended once its body went past the limit");
                Assert.That(Describe(failure),  Is.EqualTo(BodyTooLarge(request.StreamId, Limit, 1001)),    "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }), "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                                     "the next request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyUpToTheLimit_Stands(Declared)

        /// <summary>
        /// A response whose body reaches the limit and goes no further, with or
        /// without a content-length that says so. Its last DATA frame is padded,
        /// past the limit: padding counts against flow control, not against the
        /// body. The response stands as it did, and nothing is reset.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task BodyUpToTheLimit_Stands(Boolean Declared)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: Declared ? Limit : null));
            await transport.SendAsync(Data(request.StreamId, 600));
            await transport.SendAsync(PaddedData(request.StreamId, 400, Padding: 255, EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                               "the response ended");
                Assert.That(Describe(failure),  Is.Null,                                               "how the response failed");

                if (ended && failure is null)
                {
                    Assert.That(request.Response.Result.Status,       Is.EqualTo(200),                "status of the response");
                    Assert.That(request.Response.Result.Body.Length,  Is.EqualTo(Limit),              "length of its body");
                }

                Assert.That(sent,               Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                       "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region DeclaredLengthPastTheLimit_FailsAtTheHeaders()

        /// <summary>
        /// A response that declares a content-length above the limit fails at its
        /// HEADERS, before any of its body arrives, and the stream is reset with
        /// CANCEL. The body the server sent before it read the RST_STREAM is taken
        /// in quietly, and the connection goes on. The client used to wait for the
        /// body, and take it in, however large.
        /// </summary>
        [Test]
        public async Task DeclaredLengthPastTheLimit_FailsAtTheHeaders()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: Limit + 1));

            var (ended, failure) = await EndOf(request.Response);

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, Limit + 1, EndStream: true));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            await transport.RespondAsync(next.StreamId, "next");

            var answer = Answer(await next.Response.WaitAsync(HoldingH2Transport.StepTimeout));

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                                        "the response ended at its HEADERS");
                Assert.That(Describe(failure),  Is.EqualTo(DeclaredTooLarge(request.StreamId, Limit, Limit + 1)),  "how the response failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),     "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                                         "the next request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region DeclaredLengthPastTheLimit_NoBodyToCome_Stands(Method, Status)

        /// <summary>
        /// A response that can have no body may declare the content-length of one
        /// all the same (RFC 9113, Section 8.1.1; RFC 9110, Section 6.4.1): the
        /// answer to a HEAD request, a 304 and a 204. A content-length above the
        /// limit fails none of them, even when their HEADERS leave the stream
        /// open for an empty DATA frame to end it.
        /// </summary>
        [TestCase("HEAD", 200)]
        [TestCase("GET",  304)]
        [TestCase("GET",  204)]
        public async Task DeclaredLengthPastTheLimit_NoBodyToCome_Stands(String Method, Int32 Status)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big", Method == "HEAD" ? HTTPMethod.HEAD : HTTPMethod.GET);

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: Limit + 1, Status: Status));
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

        #region DeclaredLengthPastTheLimit_HeadersEndTheStream_NoReset()

        /// <summary>
        /// A response whose HEADERS end the stream brings no body to bound,
        /// whatever content-length it declares, and its stream is closed on both
        /// sides then: the limit refuses nothing, and no RST_STREAM goes out on
        /// the closed stream (RFC 9113, Section 5.1). That a response which
        /// declares a body it does not send is malformed (Section 8.1.1) is a
        /// matter of the content-length, not of the limit.
        /// </summary>
        [Test]
        public async Task DeclaredLengthPastTheLimit_HeadersEndTheStream_NoReset()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var request     = await StartAsync(connection, "/big");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: Limit + 1, EndStream: true));

            var (ended, failure)  = await EndOf(request.Response);

            var next              = await StartAsync(connection, "/next");
            var sent              = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(ended,    Is.True,                                                  "the response ended with its HEADERS");
                Assert.That(failure,  Is.Not.InstanceOf<HTTP2ResponseTooLargeException>(),     "how the response failed, if it did");

                Assert.That(sent,     Is.EqualTo(new[] { $"{next.StreamId} HEADERS END_STREAM" }),
                                                                                                "what the client sent on its streams, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RefusedFrameAndWhatFollowsIt_GoBackToTheConnectionWindow()

        /// <summary>
        /// The DATA frame the client refuses counts against the connection's
        /// window like every other, its padding included (RFC 9113, Section 6.1),
        /// and is given back to it like every other, as is the DATA the server
        /// sent on the stream before it read the RST_STREAM (Section 6.9). Not to
        /// the stream's window: no WINDOW_UPDATE goes out on the stream, before
        /// its RST_STREAM or after it. The refused frame here is the one that
        /// fills half the stream's window, at which a stream WINDOW_UPDATE would
        /// be due.
        /// </summary>
        [Test]
        public async Task RefusedFrameAndWhatFollowsIt_GoBackToTheConnectionWindow()
        {

            const Int64 limit = 520_000;

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport, limit);
            var request     = await StartAsync(connection, "/big");

            // Up to the request's HEADERS: the connection's opening frames, the
            // WINDOW_UPDATE that raises its window among them.
            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId));

            // 31 × 16 384 = 507 904 octets, short of the limit.
            for (var i = 0; i < 31; i++)
                await transport.SendAsync(Data(request.StreamId, FrameSize));

            // 16 128 octets of data take the body past the limit, to 524 032; with
            // the Pad Length octet and 255 of padding, the frame is 16 384 octets
            // to flow control, and brings the stream to half its window.
            await transport.SendAsync(PaddedData(request.StreamId, 16_128, Padding: 255));

            var failure = (await EndOf(request.Response)).Failure;

            // Sent before the server read the RST_STREAM.
            await transport.SendAsync(Data(request.StreamId, FrameSize));
            await transport.SendAsync(Data(request.StreamId, FrameSize, EndStream: true));

            var owed   = ConnectionWindowOwed(connection);

            var next   = await StartAsync(connection, "/next");
            var sent   = await SentUntilHeadersOfAsync(transport, next.StreamId);

            var given  = sent.Where(frame => frame.StreamId == 0 && frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                              Sum  (Increment);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo(BodyTooLarge(request.StreamId, limit, 524_032)),  "how the response failed");

                Assert.That(OnStreams(sent),    Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),  "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(given + owed,       Is.EqualTo(34 * FrameSize),                                  "the connection's window given back, and owed, for the 34 DATA frames the server sent");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region DataAfterARefusalAtTheHeaders_GoesBackToTheConnectionOnly()

        /// <summary>
        /// A response refused at its HEADERS leaves no exchange behind. The DATA
        /// the server sent before it read the RST_STREAM — half the stream's
        /// window here, at which a stream WINDOW_UPDATE would be due, and well
        /// within the limit — is not taken in, and goes back to the connection's
        /// window only: no WINDOW_UPDATE goes out on the reset stream.
        /// </summary>
        [Test]
        public async Task DataAfterARefusalAtTheHeaders_GoesBackToTheConnectionOnly()
        {

            const Int64 limit = 600_000;

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport, limit);
            var request     = await StartAsync(connection, "/big");

            // Up to the request's HEADERS: the connection's opening frames, the
            // WINDOW_UPDATE that raises its window among them.
            await SentUntilHeadersOfAsync(transport, request.StreamId);

            await transport.SendAsync(ResponseHeaders(request.StreamId, ContentLength: limit + 1));

            var failure = (await EndOf(request.Response)).Failure;

            // Sent before the server read the RST_STREAM: 32 × 16 384 = 524 288
            // octets, half the stream's window.
            for (var i = 0; i < 32; i++)
                await transport.SendAsync(Data(request.StreamId, FrameSize));

            var owed       = ConnectionWindowOwed(connection);
            var exchanges  = Exchanges(connection);

            var next   = await StartAsync(connection, "/next");
            var sent   = await SentUntilHeadersOfAsync(transport, next.StreamId);

            var given  = sent.Where(frame => frame.StreamId == 0 && frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                              Sum  (Increment);

            Assert.Multiple(() =>
            {

                Assert.That(Describe(failure),  Is.EqualTo(DeclaredTooLarge(request.StreamId, limit, limit + 1)),  "how the response failed");

                Assert.That(OnStreams(sent),    Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),        "what the client sent on its streams, up to the next request's HEADERS");

                Assert.That(given + owed,       Is.EqualTo(32 * FrameSize),                                        "the connection's window given back, and owed, for the 32 DATA frames the server sent");

                Assert.That(exchanges,          Is.Zero,                                                           "exchanges left: none takes the DATA in after the refusal");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RequestWaitingForAStreamSlot_GetsTheRefusedResponsesSlot()

        /// <summary>
        /// The server takes one stream at a time, and a second request waits for
        /// the first's. When the first response goes past the limit, its stream
        /// is reset, and the second request gets its slot — after the RST_STREAM,
        /// so the server never counts the two streams at once.
        /// </summary>
        [Test]
        public async Task RequestWaitingForAStreamSlot_GetsTheRefusedResponsesSlot()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);

            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, 1)));

            var request     = await StartAsync(connection, "/big");

            await SentUntilHeadersOfAsync(transport, request.StreamId);

            var waiting     = connection.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/next");

            await transport.SendAsync(ResponseHeaders(request.StreamId));
            await transport.SendAsync(Data(request.StreamId, Limit + 1));

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

                Assert.That(Describe(failure),  Is.EqualTo(BodyTooLarge(request.StreamId, Limit, Limit + 1)),  "how the response failed");
                Assert.That(started,            Is.True,                                                        "the waiting request got a stream");

                Assert.That(sent,               Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{request.StreamId + 2} HEADERS END_STREAM" }),
                                                                                                                "what the client sent on its streams, up to the waiting request's HEADERS");

                Assert.That(answer,             Is.EqualTo("200 next"),                                         "the waiting request's response");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region RequestBodyBeingSent_GoesNoFurtherThanTheReset()

        /// <summary>
        /// A request body larger than its stream's send window is waiting for
        /// more of it when its response goes past the limit. The stream is reset,
        /// and the body goes no further, even once the server grants the window
        /// for more of it: nothing but PRIORITY goes out on a stream after its
        /// RST_STREAM (RFC 9113, Section 5.1).
        /// </summary>
        [Test]
        public async Task RequestBodyBeingSent_GoesNoFurtherThanTheReset()
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

            await transport.SendAsync(ResponseHeaders(request.StreamId));
            await transport.SendAsync(Data(request.StreamId, Limit + 1));

            var failure = (await EndOf(request.Response)).Failure;

            // Window for another frame of the body, which the connection has as well.
            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(request.StreamId, FrameSize));

            var next  = await StartAsync(connection, "/next");
            var sent  = OnStreams(await SentUntilHeadersOfAsync(transport, next.StreamId));

            Assert.Multiple(() =>
            {

                Assert.That(waiting,            Is.True,                                                        "the body waited for window, with all of its stream's taken");
                Assert.That(Describe(failure),  Is.EqualTo(BodyTooLarge(request.StreamId, Limit, Limit + 1)),  "how the response failed");

                Assert.That(sent.SkipWhile(line => line != $"{request.StreamId} RST_STREAM CANCEL").ToList(),
                                                Is.EqualTo(new[] { $"{request.StreamId} RST_STREAM CANCEL",
                                                                   $"{next.StreamId} HEADERS END_STREAM" }),     "what the client sent on its streams from the RST_STREAM on, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region StreamedResponse_NotBoundByTheLimit()

        /// <summary>
        /// The limit is a buffered response's. A streamed response is read chunk
        /// by chunk, and takes a body past it — the way to read one that large.
        /// </summary>
        [Test]
        public async Task StreamedResponse_NotBoundByTheLimit()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await ConnectAsync(transport);
            var stream      = await connection.StartStreamingRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/big").
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, stream.StreamId);

            await transport.SendAsync(ResponseHeaders(stream.StreamId, ContentLength: Limit + 1));
            await transport.SendAsync(Data(stream.StreamId, 600));
            await transport.SendAsync(Data(stream.StreamId, 401, EndStream: true));

            var head  = await stream.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var read  = 0L;

            Byte[]? chunk;

            while ((chunk = await stream.ReadAsync().WaitAsync(HoldingH2Transport.StepTimeout)) is not null)
                read += chunk.Length;

            Assert.Multiple(() =>
            {
                Assert.That(head.Status,  Is.EqualTo(200),        "status of the response");
                Assert.That(read,         Is.EqualTo(Limit + 1),  "length of the body read");
            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
