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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Request trailers on a stream the server has reset. RFC 9113, Section 5.1:
    /// an endpoint must send nothing but PRIORITY on a closed stream. The client
    /// wrote a streamed request's trailers without a look at the stream's state,
    /// so an upload the server had reset still ended with a HEADERS frame on the
    /// wire: after a reset before the response, and after one that came with a
    /// complete response, which the client did not even take note of. RFC 9113,
    /// Section 8.1 lets a server that has answered in full stop the rest of an
    /// upload that way, with RST_STREAM NO_ERROR.
    ///
    /// Such trailers now stay unsent, and ending the upload with them fails as
    /// the response side of a reset stream fails: with an HTTP2StreamException
    /// that carries the reset's error code. Whether to send is decided under the
    /// lock that orders the client's header blocks, before the trailers are
    /// HPACK-encoded: the encoder adds fields to its dynamic table as it encodes,
    /// and a block encoded but never sent would leave the server's decoder a step
    /// behind for every later header block on the connection. So the tests end
    /// with a next request on the same connection, which repeats a field of the
    /// trailers that were not sent: the encoder would send that field as an
    /// index into an entry the server does not have.
    ///
    /// A reset after a complete response now closes the stream as any other
    /// reset does. So the rest of the upload stays unsent as well, a write
    /// waiting for window returns, and the stream no longer counts against the
    /// server's MAX_CONCURRENT_STREAMS.
    /// </summary>
    [TestFixture]
    public class ClientTrailersAfterResetTests
    {

        #region (helpers)

        /// <summary>
        /// The trailer field written after the reset, which the next request
        /// repeats. The encoder puts it into its dynamic table.
        /// </summary>
        private static readonly (String Name, String Value) Late = ("x-late", "after the reset");

        /// <summary>
        /// The HEADERS of the next request, as the server decodes them: they
        /// repeat <see cref="Late"/>.
        /// </summary>
        private static String NextRequest(UInt32 StreamId)

            => $"{StreamId} HEADERS :method=POST, :scheme=http, :authority=localhost, :path=/next, x-late=after the reset";

        private static Byte[] ASCII(String Text)

            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Start a streamed POST, whose request side stays open.
        /// </summary>
        private static Task<HTTP2ClientStream> StartUploadAsync(HTTP2ClientConnection              Connection,
                                                                String                             Path,
                                                                List<(String Name, String Value)>? ExtraHeaders   = null)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", Path, ExtraHeaders);

        /// <summary>
        /// What the client sends on its streams from here on, one line per frame,
        /// up to and including the first HEADERS on <paramref name="StreamId"/>.
        /// A header block is shown with its fields as the server reads them, or
        /// with what the decoder said instead. One decoder per connection, given
        /// every block in order, as the server's is. Frames on stream 0 are left
        /// out.
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
                             (frame.Type == HTTP2FrameType.DATA ? $" \"{Encoding.ASCII.GetString(frame.Payload ?? [])}\"" : "") +
                             (frame.Type == HTTP2FrameType.DATA && frame.EndStream ? " END_STREAM" : ""));

                String fields;

                try
                {
                    fields = String.Join(", ", Decoder.DecodeHeaderBlock(headers.Payload).
                                                   Select(field => $"{field.Name}={field.Value}"));
                }
                catch (Exception e)
                {
                    fields = e.Message;
                }

                sent.Add($"{headers.StreamId} HEADERS {fields}" + (headers.EndStream ? " END_STREAM" : ""));

                if (headers.StreamId == StreamId)
                    return sent;

            }

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
        /// stream and the error code, and its message.
        /// </summary>
        private static String? Describe(Exception? Failure)

            => Failure switch {
                   null                     => null,
                   HTTP2StreamException e   => $"{nameof(HTTP2StreamException)} on stream {e.StreamId}, {e.ErrorCode}: {e.Message}",
                   _                        => $"{Failure.GetType().Name}: {Failure.Message}"
               };

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

        #endregion


        #region TrailersAfterReset_Fail_NothingSent(Code)

        /// <summary>
        /// The server resets an upload before it answers, and the client goes on
        /// to end the upload with trailers. The call fails as the response side
        /// does, with an HTTP2StreamException that carries the reset's error
        /// code; nothing goes out on the reset stream; and the HEADERS of the
        /// next request decode.
        /// </summary>
        [TestCase(HTTP2ErrorCode.CANCEL)]
        [TestCase(HTTP2ErrorCode.INTERNAL_ERROR)]
        public async Task TrailersAfterReset_Fail_NothingSent(HTTP2ErrorCode Code)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, Code));

            var (ended, failure)  = await EndOf(upload.CompleteRequestAsync([Late]));
            var responseFailure   = (await EndOf(upload.GetResponseAsync())).Failure;

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                 "ending the upload with trailers returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2StreamException)} on stream {upload.StreamId}, {Code}: Stream reset by server: {Code}"),
                                                                                         "how ending the upload with trailers failed");
                Assert.That(Describe(failure),  Is.EqualTo(Describe(responseFailure)),   "as the response side of the stream failed");

                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(upload.StreamId + 2) }),
                                                                                         "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region TrailersAfterACompleteResponseAndItsReset_NotSent(Code)

        /// <summary>
        /// The server answers an upload in full and then resets the stream, as
        /// RFC 9113, Section 8.1 lets it ask the client to stop sending the rest
        /// of a request it no longer needs: with RST_STREAM NO_ERROR. Ending the
        /// upload with trailers afterwards fails with that code, and sends
        /// nothing. The response stands: the client must not discard it over
        /// such a reset.
        /// </summary>
        [TestCase(HTTP2ErrorCode.NO_ERROR)]
        [TestCase(HTTP2ErrorCode.CANCEL)]
        public async Task TrailersAfterACompleteResponseAndItsReset_NotSent(HTTP2ErrorCode Code)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.RespondAsync(upload.StreamId, "answered early");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, Code));

            var (ended, failure)  = await EndOf(upload.CompleteRequestAsync([Late]));

            var head              = await upload.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var body              = await ReadToEndAsync(upload);
            var trailers          = await upload.GetTrailersAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(ended,              Is.True,                                 "ending the upload with trailers returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2StreamException)} on stream {upload.StreamId}, {Code}: Stream reset by server: {Code}"),
                                                                                         "how ending the upload with trailers failed");

                Assert.That(head.Status,        Is.EqualTo(200),                         "status of the response, which stands");
                Assert.That(body,               Is.EqualTo("answered early"),            "body of the response");
                Assert.That(trailers,           Is.Empty,                                "trailers of the response");

                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(upload.StreamId + 2) }),
                                                                                         "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region TrailersWaitingForTheLock_ResetMeanwhile_NotSent()

        /// <summary>
        /// Ending the upload with trailers waits for the lock that orders the
        /// client's header blocks — another request is starting, held by the test
        /// in the write of its HEADERS — while the read loop handles the server's
        /// RST_STREAM. Once the trailers get the lock, they find the stream reset:
        /// nothing is sent, and the call fails with the reset's code. A check made
        /// only before the wait would have let them through.
        /// </summary>
        [Test]
        public async Task TrailersWaitingForTheLock_ResetMeanwhile_NotSent()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            var completing  = upload.CompleteRequestAsync([Late]);
            var waited      = !completing.IsCompleted;

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            transport.Release(3);

            var (ended, failure)  = await EndOf(completing);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(waited,             Is.True,                                 "ending the upload waited for the other request's start");
                Assert.That(otherStarted,       Is.True,                                 "the other request started");

                Assert.That(ended,              Is.True,                                 "ending the upload with trailers returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2StreamException)} on stream {upload.StreamId}, CANCEL: Stream reset by server: CANCEL"),
                                                                                         "how ending the upload with trailers failed");

                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(5) }),    "what the client sent after the other request's HEADERS, up to the next request's");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region TrailersAfterReset_WhileAnotherRequestStarts_FailAtOnce()

        /// <summary>
        /// The server has reset the upload, and another request is starting,
        /// held by the test in the write of its HEADERS, when the client ends the
        /// upload with trailers. The call fails at once, rather than after a wait
        /// for that request's start: there is nothing to wait for.
        /// </summary>
        [Test]
        public async Task TrailersAfterReset_WhileAnotherRequestStarts_FailAtOnce()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 3);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.CANCEL));

            var other       = StartUploadAsync(connection, "/other");

            await SentUntilHeadersOfAsync(transport, decoder, 3);

            var completing  = upload.CompleteRequestAsync([Late]);
            var atOnce      = completing.IsCompleted;

            transport.Release(3);

            var (ended, failure)  = await EndOf(completing);
            var otherStarted      = (await EndOf(other)).Ended;

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, 5);

            Assert.Multiple(() =>
            {

                Assert.That(atOnce,             Is.True,                                 "ending the upload ended before the other request's start did");
                Assert.That(otherStarted,       Is.True,                                 "the other request started");

                Assert.That(ended,              Is.True,                                 "ending the upload with trailers returned while the connection was open");
                Assert.That(Describe(failure),  Is.EqualTo($"{nameof(HTTP2StreamException)} on stream {upload.StreamId}, CANCEL: Stream reset by server: CANCEL"),
                                                                                         "how ending the upload with trailers failed");

                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(5) }),    "what the client sent after the other request's HEADERS, up to the next request's");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResetAfterACompleteResponse_StopsTheUpload()

        /// <summary>
        /// The server answers an upload in full and then resets the stream with
        /// NO_ERROR, and the client goes on writing the upload. Nothing more of it
        /// goes out on the reset stream.
        /// </summary>
        [Test]
        public async Task ResetAfterACompleteResponse_StopsTheUpload()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.RespondAsync(upload.StreamId, "answered early");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.NO_ERROR));

            var written = (await EndOf(upload.WriteAsync(ASCII("the rest")))).Ended;

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(written,            Is.True,                                 "writing the rest of the upload returned");
                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(upload.StreamId + 2) }),
                                                                                         "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResetAfterACompleteResponse_EndsAWriteWaitingForWindow()

        /// <summary>
        /// The usual shape of an early answer: the server stops reading the
        /// upload, so the client's write runs out of stream window and waits for
        /// more; then the server answers in full and resets the stream with
        /// NO_ERROR. The waiting write returns, and nothing more of it goes out.
        /// </summary>
        [Test]
        public async Task ResetAfterACompleteResponse_EndsAWriteWaitingForWindow()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            // Ten bytes of window for every new stream.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 10)));

            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            var writing     = upload.WriteAsync(ASCII("more than ten bytes"));

            await transport.RespondAsync(upload.StreamId, "answered early");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.NO_ERROR));

            var written     = (await EndOf(writing)).Ended;

            await StartUploadAsync(connection, "/next", [Late]).WaitAsync(HoldingH2Transport.StepTimeout);

            var sent = await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2);

            Assert.Multiple(() =>
            {

                Assert.That(written,            Is.True,                                 "the write waiting for window returned");
                Assert.That(sent,               Is.EqualTo(new[] { $"{upload.StreamId} DATA \"more than \"", NextRequest(upload.StreamId + 2) }),
                                                                                         "what the client sent from the write on, up to the next request's HEADERS: the ten bytes it had window for");

            });

            await connection.CloseAsync();

        }

        #endregion

        #region ResetAfterACompleteResponse_FreesTheStreamSlot()

        /// <summary>
        /// On a connection that allows one stream at a time, the server answers
        /// an upload in full and then resets the stream with NO_ERROR, while the
        /// caller has not ended the upload. The reset closes the stream, so the
        /// next request starts: a closed stream does not count against
        /// MAX_CONCURRENT_STREAMS (RFC 9113, Section 5.1.2).
        /// </summary>
        [Test]
        public async Task ResetAfterACompleteResponse_FreesTheStreamSlot()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var decoder     = new HPACKDecoder();
            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, 1)));

            var upload      = await StartUploadAsync(connection, "/upload").WaitAsync(HoldingH2Transport.StepTimeout);

            await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId);

            await transport.RespondAsync(upload.StreamId, "answered early");
            await transport.SendAsync(HTTP2Frame.CreateRstStream(upload.StreamId, HTTP2ErrorCode.NO_ERROR));

            var (started, failure) = await EndOf(StartUploadAsync(connection, "/next", [Late]));

            var sent = started && failure is null
                           ? await SentUntilHeadersOfAsync(transport, decoder, upload.StreamId + 2)
                           : [];

            Assert.Multiple(() =>
            {

                Assert.That(started,            Is.True,                                 "starting the next request returned");
                Assert.That(Describe(failure),  Is.Null,                                 "how starting the next request failed");
                Assert.That(sent,               Is.EqualTo(new[] { NextRequest(upload.StreamId + 2) }),
                                                                                         "what the client sent after the reset, up to the next request's HEADERS");

            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
