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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Request DATA on a stream the server has reset. RFC 9113, Section 5.1: an
    /// endpoint must send nothing but PRIORITY on a closed stream. The client's
    /// DATA did not keep to that:
    /// - Ending a request without trailers, CompleteRequestAsync() and a
    ///   tunnel's CloseAsync, sent its empty END_STREAM DATA frame without a look
    ///   at the stream, and reported success.
    /// - A write, a streamed request's or a tunnel's WriteAsync and a buffered
    ///   request's body, looked at the stream only while it took its send
    ///   window, and then waited for the connection's write lock. A reset the
    ///   read loop handled meanwhile did not keep the frame off the wire, and the
    ///   window it had taken was spent either way.
    /// - A write that met the reset before it took its window sent nothing, but
    ///   returned as if its bytes had gone out.
    ///
    /// Now nothing of the request goes out on a reset stream. Every call that was
    /// to send something fails as the response side of the stream does, and as
    /// the request's trailers do since 63b05c6d: with an HTTP2StreamException that
    /// carries the reset's error code. It fails at once, or, if it was waiting for
    /// the write lock, once it gets the lock. A frame that stays unsent gives back
    /// the send window it took. Only the body of a buffered request stops quietly:
    /// the reset has decided that exchange already, as a failure, as a retry on a
    /// new stream, or as a complete response that stands.
    /// </summary>
    [TestFixture]
    public class ClientDataAfterResetTests
    {

        #region (helpers)

        /// <summary>
        /// The connection-level send window of a new connection, which the server
        /// in these tests never raises (RFC 9113, Section 6.9.2).
        /// </summary>
        private const Int64 InitialConnectionWindow = 65535;

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Start a streamed POST, whose request side stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartUploadAsync(HTTP2ClientConnection  Connection,
                                                                String                 Path)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path);

        /// <summary>
        /// A request's HEADERS, as <see cref="SentUntilHeadersOfAsync"/> lists them.
        /// </summary>
        private static String Request(UInt32 StreamId, String Method, String Path)

            => $"{StreamId} HEADERS {Method} {Path}";

        /// <summary>
        /// What the client sends on its streams from here on, one line per frame,
        /// up to and including the first HEADERS on <paramref name="StreamId"/>:
        /// DATA with its payload, or only its length if that is long, HEADERS with
        /// the method and the path the server decodes, or the authority for a
        /// CONNECT. One decoder per connection, given every header block in order,
        /// as the server's is. Frames on stream 0 are left out.
        /// </summary>
        private static async Task<List<String>> SentUntilHeadersOfAsync(HoldingH2Transport  Transport,
                                                                        HPACKDecoder        Decoder,
                                                                        UInt32              StreamId)
        {

            var sent = new List<String>();

            while (true)
            {

                var skipped = new List<HTTP2Frame>();
                var headers = await Transport.NextHeadersAsync(skipped);

                foreach (var frame in skipped.Where(frame => frame.StreamId != 0))
                    sent.Add($"{frame.StreamId} {frame.Type}" +
                             (frame.Type == HTTP2FrameType.DATA
                                  ? (frame.Payload?.Length ?? 0) <= 64
                                        ? $" \"{Encoding.ASCII.GetString(frame.Payload ?? [])}\""
                                        : $" ({frame.Payload!.Length} bytes)"
                                  : "") +
                             (frame.Type == HTTP2FrameType.DATA && frame.EndStream ? " END_STREAM" : ""));

                var fields = Decoder.DecodeHeaderBlock(headers.Payload);

                sent.Add(Request(headers.StreamId,
                                 Field(fields, ":method") ?? "?",
                                 Field(fields, ":path")   ?? Field(fields, ":authority") ?? "?") +
                         (headers.EndStream ? " END_STREAM" : ""));

                if (headers.StreamId == StreamId)
                    return sent;

            }

        }

        private static String? Field(List<(String Name, String Value)> Fields, String Name)

            => Fields.FirstOrDefault(field => field.Name == Name).Value;

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
        /// stream and the error code, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                     => null,
                   HTTP2StreamException e   => $"{nameof(HTTP2StreamException)} on stream {e.StreamId}, {e.ErrorCode}: {e.Message}",
                   _                        => $"{Failure.GetType().Name}: {Failure.Message}"
               };

        /// <summary>
        /// How a call on a stream the server has reset with <paramref name="Code"/>
        /// fails, as <see cref="Describe"/> shows it.
        /// </summary>
        private static String ResetFailure(UInt32 StreamId, HTTP2ErrorCode Code)

            => $"{nameof(HTTP2StreamException)} on stream {StreamId}, {Code}: Stream reset by server: {Code}";

        /// <summary>
        /// The response body, read to its end.
        /// </summary>
        private static async Task<String> ReadToEndAsync(HTTP2ClientStream Stream)
        {

            var     body = new MemoryStream();
            Byte[]? chunk;

            while ((chunk = await Stream.ReadAsync().WaitAsync(HoldingH2Transport.StepTimeout)) is not null)
                body.Write(chunk);

            return Encoding.ASCII.GetString(body.ToArray());

        }

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
        /// write the read loop wakes takes its window on a thread of its own, and
        /// says so nowhere else.
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
        /// Open a CONNECT tunnel, and accept it with a 200.
        /// </summary>
        private static async Task<HTTP2ClientTunnel> OpenTunnelAsync(HoldingH2Transport     Transport,
                                                                     HTTP2ClientConnection  Connection,
                                                                     HPACKDecoder           Decoder)
        {

            var opening = Connection.OpenTunnelAsync("localhost:443");

            await SentUntilHeadersOfAsync(Transport, Decoder, 1);

            // ":status: 200" is in HPACK's static table, so this block leaves the
            // client's dynamic table as it is.
            await Transport.SendAsync(HTTP2Frame.CreateHeaders(1,
                                                               new HPACKEncoder().EncodeHeaderBlock([(":status", "200")]),
                                                               EndStream:  false,
                                                               EndHeaders: true));

            return await opening.WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion


        #region EndAfterReset_Fails_NothingSent(Code)

        /// <summary>
        /// The server resets an upload before it answers, and the client goes on
        /// to end the upload, without trailers. The call fails as the response
        /// side does, with an HTTP2StreamException that carries the reset's error
        /// code, and no END_STREAM goes out on the reset stream. It used to send
        /// its empty DATA frame and report success.
        /// </summary>
        [TestCase(HTTP2ErrorCode.CANCEL)]
        [TestCase(HTTP2ErrorCode.INTERNAL_ERROR)]
        public async Task EndAfterReset_Fails_NothingSent(HTTP2ErrorCode Code)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, Code));

            var (ended, failure)  = await EndOf(upload.CompleteRequestAsync());
            var responseFailure   = (await EndOf(upload.GetResponseAsync())).Failure;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                          "ending the upload returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(upload.StreamId, Code)),  "how ending the upload failed");
                Assert.That(Describe(failure),  Is.EqualTo(Describe(responseFailure)),            "as the response side of the stream failed");

                Assert.That(sent,               Is.EqualTo(new[] { Request(upload.StreamId + 2, "POST", "/next") }),
                                                                                                  "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region EndAfterACompleteResponseAndItsReset_Fails_ResponseStands(Code)

        /// <summary>
        /// The server answers an upload in full and then resets the stream, as
        /// RFC 9113, Section 8.1 lets it ask the client to stop sending the rest
        /// of a request it no longer needs: with RST_STREAM NO_ERROR. Ending the
        /// upload afterwards fails with that code and sends nothing, as ending it
        /// with trailers does. The response stands: the client must not discard it
        /// over such a reset.
        /// </summary>
        [TestCase(HTTP2ErrorCode.NO_ERROR)]
        [TestCase(HTTP2ErrorCode.CANCEL)]
        public async Task EndAfterACompleteResponseAndItsReset_Fails_ResponseStands(HTTP2ErrorCode Code)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.RespondAsync(upload.StreamId, "answered early");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, Code));

            var (ended, failure)  = await EndOf(upload.CompleteRequestAsync());

            var head              = await upload.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var body              = await ReadToEndAsync(upload);
            var trailers          = await upload.GetTrailersAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                          "ending the upload returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(upload.StreamId, Code)),  "how ending the upload failed");

                Assert.That(head.Status,        Is.EqualTo(200),                                  "status of the response, which stands");
                Assert.That(body,               Is.EqualTo("answered early"),                     "body of the response");
                Assert.That(trailers,           Is.Empty,                                         "trailers of the response");

                Assert.That(sent,               Is.EqualTo(new[] { Request(upload.StreamId + 2, "POST", "/next") }),
                                                                                                  "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WriteAfterReset_Fails_NothingSent(Code, AnsweredFirst)

        /// <summary>
        /// The server resets an upload, and the client goes on writing it: before
        /// a response, or after a complete one and its NO_ERROR (RFC 9113, Section
        /// 8.1). Nothing goes out, and the write fails with the reset's error code.
        /// It used to return as if its bytes had gone out, and a caller that went
        /// on writing learned of the reset only from the response side, if at all:
        /// after a complete response, that side has nothing to say.
        /// </summary>
        [TestCase(HTTP2ErrorCode.CANCEL,    false)]
        [TestCase(HTTP2ErrorCode.NO_ERROR,  true)]
        public async Task WriteAfterReset_Fails_NothingSent(HTTP2ErrorCode Code, Boolean AnsweredFirst)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            if (AnsweredFirst)
                await transport.RespondAsync(upload.StreamId, "answered early");

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, Code));

            var (ended, failure)  = await EndOf(upload.WriteAsync(ASCII("after the reset")));
            var response          = await EndOf(upload.GetResponseAsync());

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(ended,                       Is.True,                                          "the write returned while the connection was open");
                Assert.That(Describe(failure),           Is.EqualTo(ResetFailure(upload.StreamId, Code)),  "how the write failed");

                Assert.That(Describe(response.Failure),  Is.EqualTo(AnsweredFirst ? null : Describe(failure)),  "how the response side ended: the answer stands, or it failed as the write did");

                Assert.That(sent,                        Is.EqualTo(new[] { Request(upload.StreamId + 2, "POST", "/next") }),
                                                                                                           "what the client sent after the reset, up to the next request's HEADERS");

                Assert.That(ConnectionSendWindow(connection),
                                                         Is.EqualTo(InitialConnectionWindow),              "the connection's send window");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region TunnelWriteAndCloseAfterReset_Fail_NothingSent(Code)

        /// <summary>
        /// The server resets an accepted CONNECT tunnel. Reading the tunnel then
        /// returns its end, as ever. Writing to it and closing it fail with the
        /// reset's error code, and send nothing: the close used to send its empty
        /// END_STREAM DATA frame all the same, and both used to report success.
        /// Without them the caller could not tell the reset from an orderly end.
        /// </summary>
        [TestCase(HTTP2ErrorCode.CANCEL)]
        [TestCase(HTTP2ErrorCode.CONNECT_ERROR)]
        public async Task TunnelWriteAndCloseAfterReset_Fail_NothingSent(HTTP2ErrorCode Code)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var tunnel      = await OpenTunnelAsync(transport, connection, decoder);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(tunnel.StreamId, Code));

            var read                        = await tunnel.ReadAsync(CancellationToken.None).WaitAsync(HoldingH2Transport.StepTimeout);
            var (writeEnded, writeFailure)  = await EndOf(tunnel.WriteAsync(ASCII("after the reset"), CancellationToken.None));
            var (closeEnded, closeFailure)  = await EndOf(tunnel.CloseAsync());

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, tunnel.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(read,                    Is.Null,                                          "what reading the tunnel returned");

                Assert.That(writeEnded,              Is.True,                                          "writing to the tunnel returned while the connection was open");
                Assert.That(Describe(writeFailure),  Is.EqualTo(ResetFailure(tunnel.StreamId, Code)),  "how writing to the tunnel failed");

                Assert.That(closeEnded,              Is.True,                                          "closing the tunnel returned while the connection was open");
                Assert.That(Describe(closeFailure),  Is.EqualTo(ResetFailure(tunnel.StreamId, Code)),  "how closing the tunnel failed");

                Assert.That(sent,                    Is.EqualTo(new[] { Request(tunnel.StreamId + 2, "POST", "/next") }),
                                                                                                       "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WriteAndEndAfterReset_WhileAnotherRequestStarts_FailAtOnce()

        /// <summary>
        /// The server has reset the upload, and another request is starting, held
        /// by the test in the write of its HEADERS, with the connection's write
        /// lock, when the client writes more of the upload and then ends it. Both
        /// calls fail at once, rather than after a wait for that write: there is
        /// nothing to wait for. Ending the upload used to wait, and then send its
        /// END_STREAM on the reset stream.
        /// </summary>
        [Test]
        public async Task WriteAndEndAfterReset_WhileAnotherRequestStarts_FailAtOnce()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            var writing     = upload.WriteAsync(ASCII("after the reset"));
            var writeAtOnce = writing.IsCompleted;

            var completing  = upload.CompleteRequestAsync();
            var endAtOnce   = completing.IsCompleted;

            transport.Release(3);

            var writeFailure  = (await EndOf(writing)).   Failure;
            var endFailure    = (await EndOf(completing)).Failure;
            var otherStarted  = (await EndOf(other)).     Ended;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(writeAtOnce,             Is.True,                                                            "the write ended before the other request's start did");
                Assert.That(endAtOnce,               Is.True,                                                            "ending the upload ended before the other request's start did");
                Assert.That(otherStarted,            Is.True,                                                            "the other request started");

                Assert.That(Describe(writeFailure),  Is.EqualTo(ResetFailure(upload.StreamId, HTTP2ErrorCode.CANCEL)),  "how the write failed");
                Assert.That(Describe(endFailure),    Is.EqualTo(ResetFailure(upload.StreamId, HTTP2ErrorCode.CANCEL)),  "how ending the upload failed");

                Assert.That(sent,                    Is.EqualTo(new[] { Request(5, "POST", "/next") }),                 "what the client sent after the other request's HEADERS, up to the next request's");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WriteWaitingForWindow_ResetMeanwhile_Fails()

        /// <summary>
        /// A write runs out of stream window and waits for more, and then the
        /// server resets the upload. What the write had window for is out; the
        /// rest stays unsent, and the write fails with the reset's error code. It
        /// used to return as if the rest had gone out as well.
        /// </summary>
        [Test]
        public async Task WriteWaitingForWindow_ResetMeanwhile_Fails()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // Ten bytes of window for every new stream.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 10)));

            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            var writing     = upload.WriteAsync(ASCII("more than ten bytes"));
            var waited      = !writing.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            var (ended, failure) = await EndOf(writing);

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(waited,             Is.True,                                                            "the write waited for window");

                Assert.That(ended,              Is.True,                                                            "the write returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(upload.StreamId, HTTP2ErrorCode.CANCEL)),  "how the write failed");

                Assert.That(sent,               Is.EqualTo(new[] { $"{upload.StreamId} DATA \"more than \"", Request(upload.StreamId + 2, "POST", "/next") }),
                                                                                                                    "what the client sent from the write on, up to the next request's HEADERS: the ten bytes it had window for");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WriteWaitingForTheWriteLock_ResetMeanwhile_NotSent()

        /// <summary>
        /// A write has taken its window and waits for the connection's write lock,
        /// which another request's start holds: the test holds it in the write of
        /// its HEADERS. Meanwhile the read loop handles the server's RST_STREAM.
        /// Once the write gets the lock, it finds the stream reset: its DATA stays
        /// unsent, the write fails with the reset's error code, and the window it
        /// took is the connection's again. A check made only while it took the
        /// window let the DATA through onto the reset stream, and the write
        /// returned as if all was well.
        /// </summary>
        [Test]
        public async Task WriteWaitingForTheWriteLock_ResetMeanwhile_NotSent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            var writing     = upload.WriteAsync(ASCII("after the reset"));
            var waited      = !writing.IsCompleted;
            var taken       = ConnectionSendWindow(connection);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            transport.Release(3);

            var (ended, failure)  = await EndOf(writing);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(waited,             Is.True,                                                            "the write waited for the other request's start");
                Assert.That(taken,              Is.EqualTo(InitialConnectionWindow - 15),                           "the connection's send window while the write waited: less its 15 bytes");
                Assert.That(otherStarted,       Is.True,                                                            "the other request started");

                Assert.That(ended,              Is.True,                                                            "the write returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(upload.StreamId, HTTP2ErrorCode.CANCEL)),  "how the write failed");

                Assert.That(sent,               Is.EqualTo(new[] { Request(5, "POST", "/next") }),                 "what the client sent after the other request's HEADERS, up to the next request's");

                Assert.That(ConnectionSendWindow(connection),
                                                Is.EqualTo(InitialConnectionWindow),                                "the connection's send window, once the write failed");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region WindowOfAFrameKeptOffTheWire_GoesToAWriteWaitingForIt()

        /// <summary>
        /// One upload has taken all of the connection's send window but 15 bytes.
        /// A second upload takes those, and waits for the write lock, which a third
        /// request's start holds; the server resets the second upload meanwhile.
        /// Then the first upload writes again, and waits for window. Once the
        /// second upload's frame gets the lock, its 15 bytes stay unsent, and
        /// their window goes back to the connection. The first upload's write gets
        /// it, and its 10 bytes go out. Had the window not come back, or come back
        /// without a word to the write that waits for it, that write would have
        /// waited for good: the server grants window for DATA it gets, and it
        /// never got those 15 bytes. The reset is handled before that write
        /// waits, so that its own wake-up cannot stand in for that word.
        /// </summary>
        [Test]
        public async Task WindowOfAFrameKeptOffTheWire_GoesToAWriteWaitingForIt()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 5);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var first       = await StartUploadAsync(connection, "/first"). WaitAsync(HoldingH2Transport.StepTimeout);
            var second      = await StartUploadAsync(connection, "/second").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, second.StreamId);

            // Read while it is written: the transport's pipe takes less than the
            // whole connection window before the other end reads.
            var reading     = SentUntilHeadersOfAsync(transport, decoder, 5);

            await first.WriteAsync(new Byte[InitialConnectionWindow - 15]).WaitAsync(HoldingH2Transport.StepTimeout);

            var other       = StartUploadAsync(connection, "/other");
            var firstSent   = await reading;

            var dropped     = second.WriteAsync(ASCII("after the reset"));
            var taken       = ConnectionSendWindow(connection);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(second.StreamId, HTTP2ErrorCode.CANCEL));

            var waiting     = first.WriteAsync(ASCII("ten bytes!"));
            var waited      = !waiting.IsCompleted;

            transport.Release(5);

            var droppedFailure    = (await EndOf(dropped)).Failure;
            var (ended, failure)  = await EndOf(waiting);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 7);

            Assert.Multiple(() =>
            {

                Assert.That(firstSent,                Is.EqualTo(new[] { $"{first.StreamId} DATA (16384 bytes)",
                                                                         $"{first.StreamId} DATA (16384 bytes)",
                                                                         $"{first.StreamId} DATA (16384 bytes)",
                                                                         $"{first.StreamId} DATA (16368 bytes)",
                                                                         Request(5, "POST", "/other") }),                  "what the client sent of the first upload, up to the other request's HEADERS");

                Assert.That(taken,                    Is.EqualTo(0),                                                        "the connection's send window, once the second upload took its 15 bytes");
                Assert.That(waited,                   Is.True,                                                              "the first upload's write waited for window");
                Assert.That(otherStarted,             Is.True,                                                              "the other request started");

                Assert.That(Describe(droppedFailure), Is.EqualTo(ResetFailure(second.StreamId, HTTP2ErrorCode.CANCEL)),    "how the second upload's write failed");

                Assert.That(ended,                    Is.True,                                                              "the first upload's write ended while the connection was open");
                Assert.That(Describe(failure),        Is.Null,                                                              "how the first upload's write failed");

                Assert.That(sent,                     Is.EqualTo(new[] { $"{first.StreamId} DATA \"ten bytes!\"", Request(7, "POST", "/next") }),
                                                                                                                            "what the client sent after the other request's HEADERS, up to the next request's");

                Assert.That(ConnectionSendWindow(connection),
                                                      Is.EqualTo(5),                                                        "the connection's send window: the 15 bytes back, less the 10 that went out");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region EndWaitingForTheWriteLock_ResetMeanwhile_NotSent()

        /// <summary>
        /// Ending the upload waits for the connection's write lock, which another
        /// request's start holds, while the read loop handles the server's
        /// RST_STREAM. Once it gets the lock, it finds the stream reset: its
        /// END_STREAM stays unsent, and the call fails with the reset's error
        /// code. It used to send it onto the reset stream, and report success.
        /// </summary>
        [Test]
        public async Task EndWaitingForTheWriteLock_ResetMeanwhile_NotSent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            var completing  = upload.CompleteRequestAsync();
            var waited      = !completing.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            transport.Release(3);

            var (ended, failure)  = await EndOf(completing);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(waited,             Is.True,                                                            "ending the upload waited for the other request's start");
                Assert.That(otherStarted,       Is.True,                                                            "the other request started");

                Assert.That(ended,              Is.True,                                                            "ending the upload returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(upload.StreamId, HTTP2ErrorCode.CANCEL)),  "how ending the upload failed");

                Assert.That(sent,               Is.EqualTo(new[] { Request(5, "POST", "/next") }),                 "what the client sent after the other request's HEADERS, up to the next request's");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyWaitingForTheWriteLock_ResetMeanwhile_NotSent()

        /// <summary>
        /// A buffered request's body waits for stream window, gets it, and then
        /// waits for the connection's write lock, which another request's start
        /// holds, while the read loop handles the server's RST_STREAM CANCEL. Once
        /// the body gets the lock, it finds the stream reset: it stays unsent, and
        /// the window it took is the connection's again. The request fails as the
        /// reset has decided. The body used to go out onto the reset stream.
        /// </summary>
        [Test]
        public async Task BodyWaitingForTheWriteLock_ResetMeanwhile_NotSent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream, until the test gives it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var request     = await connection.StartRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload", Body: ASCII("the body")).
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, request.StreamId);

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(request.StreamId, 8));

            var taken       = await SendWindowReachesAsync(connection, InitialConnectionWindow - 8);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(request.StreamId, HTTP2ErrorCode.CANCEL));

            transport.Release(3);

            var (ended, failure)  = await EndOf(request.Response);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(taken,              Is.True,                                                             "the body took its window: the connection's send window went down by its 8 bytes");
                Assert.That(otherStarted,       Is.True,                                                             "the other request started");

                Assert.That(ended,              Is.True,                                                             "the request ended while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo(ResetFailure(request.StreamId, HTTP2ErrorCode.CANCEL)),  "how the request failed");

                Assert.That(sent,               Is.EqualTo(new[] { Request(5, "POST", "/next") }),                  "what the client sent after the other request's HEADERS, up to the next request's");

                Assert.That(ConnectionSendWindow(connection),
                                                Is.EqualTo(InitialConnectionWindow),                                 "the connection's send window, once the request failed");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region BodyWaitingForTheWriteLock_Refused_RetryUnharmed()

        /// <summary>
        /// As above, but the server refuses the stream (REFUSED_STREAM), and the
        /// client retries the request on a new stream (RFC 9113, Section 8.7). The
        /// first stream's body stays unsent, and it stops quietly: its request
        /// goes on on the new stream, and gets the answer given there. A body that
        /// failed there would fail the retry with it.
        /// </summary>
        [Test]
        public async Task BodyWaitingForTheWriteLock_Refused_RetryUnharmed()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream, until the test gives it: the retry's
            // body stays where it is, and leaves the connection's window alone.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var request     = await connection.StartRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload", Body: ASCII("the body")).
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, request.StreamId);

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            await transport.SendAsync(HTTP2Frame.CreateWindowUpdate(request.StreamId, 8));

            var taken       = await SendWindowReachesAsync(connection, InitialConnectionWindow - 8);

            await transport.RefuseAsync(request.StreamId);

            transport.Release(3);

            var otherStarted  = (await EndOf(other)).Ended;
            var retried       = await SentUntilHeadersOfAsync(transport, decoder, 5);

            await transport.RespondAsync(5, "answered the retry");

            var (ended, failure)  = await EndOf(request.Response);

            Assert.Multiple(() =>
            {

                Assert.That(taken,              Is.True,                                               "the body took its window: the connection's send window went down by its 8 bytes");
                Assert.That(otherStarted,       Is.True,                                               "the other request started");

                Assert.That(retried,            Is.EqualTo(new[] { Request(5, "POST", "/upload") }),  "what the client sent after the other request's HEADERS, up to the retry's");

                Assert.That(ended,              Is.True,                                               "the request ended while the connection was open");
                Assert.That(Describe(failure),  Is.Null,                                               "how the request failed");

                if (failure is null)
                {
                    Assert.That(request.Response.Result.Status,                           Is.EqualTo(200),                   "status of the retry's response");
                    Assert.That(Encoding.ASCII.GetString(request.Response.Result.Body),   Is.EqualTo("answered the retry"),  "body of the retry's response");
                }

                Assert.That(ConnectionSendWindow(connection),
                                                Is.EqualTo(InitialConnectionWindow),                   "the connection's send window, the retry's body still waiting for stream window");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region Download_ResetAfterACompleteResponse_BeforeTheRequestEnds_Succeeds()

        /// <summary>
        /// DownloadAsync starts a streamed GET and ends its request right after the
        /// HEADERS. The test holds the client in the write of those HEADERS while
        /// the server answers in full and resets the stream with NO_ERROR (RFC
        /// 9113, Section 8.1). Ending the request then finds the stream reset, and
        /// sends nothing. The download goes on to read the response, which stands:
        /// the client must not discard it over such a reset. Ending the request
        /// used to put its END_STREAM on the reset stream.
        /// </summary>
        [Test]
        public async Task Download_ResetAfterACompleteResponse_BeforeTheRequestEnds_Succeeds()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 1);

            var decoder      = new HPACKDecoder();
            var connection   = await transport.ConnectAsync(new HTTP2ClientOptions());
            var destination  = new MemoryStream();

            var downloading  = connection.DownloadAsync(URIScheme.http, "localhost", "/file", destination);

            var requested    = await SentUntilHeadersOfAsync(transport, decoder, 1);

            await transport.RespondAsync(1, "the file");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.NO_ERROR));

            transport.Release(1);

            var (ended, failure) = await EndOf(downloading);

            await StartUploadAsync(connection, "/next").WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 3);

            Assert.Multiple(() =>
            {

                Assert.That(requested,          Is.EqualTo(new[] { Request(1, "GET", "/file") }),     "the download's request");

                Assert.That(ended,              Is.True,                                              "the download ended while the connection was open");
                Assert.That(Describe(failure),  Is.Null,                                              "how the download failed");

                if (failure is null)
                {
                    Assert.That(downloading.Result.Status,        Is.EqualTo(200),                   "status of the download");
                    Assert.That(downloading.Result.BytesWritten,  Is.EqualTo(8),                     "bytes the download wrote");
                }

                Assert.That(Encoding.ASCII.GetString(destination.ToArray()),
                                                Is.EqualTo("the file"),                               "what the download wrote");

                Assert.That(sent,               Is.EqualTo(new[] { Request(3, "POST", "/next") }),    "what the client sent after the download's HEADERS, up to the next request's");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
