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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Giving up a stream the application will not read to its end. The client
    /// gives back the stream window of a tunnel or a streamed response only as
    /// the application reads it, so a stream nobody reads any more keeps the
    /// server waiting, and its stream slot, until the connection ends — and the
    /// client had no way to end it but to end the connection.
    ///
    /// HTTP2ClientTunnel and HTTP2ClientStream are IAsyncDisposable now: unless
    /// both sides have ended the stream, or it was reset, DisposeAsync resets it
    /// with RST_STREAM CANCEL, which frees its slot, and every call on it after
    /// that, or waiting on it then, fails with an ObjectDisposedException. A
    /// tunnel whose opening was cancelled before the server answered is reset as
    /// well, and DownloadAsync gives up a response it does not read to its end.
    /// </summary>
    [TestFixture]
    public class ClientStreamDisposeTests
    {

        #region (helpers)

        /// <summary>
        /// The two kinds of stream the application reads chunk by chunk and can
        /// give up: a tunnel, or a streamed request.
        /// </summary>
        public enum Kind
        {
            Tunnel,
            StreamedRequest
        }

        /// <summary>
        /// A stream the application reads chunk by chunk, whichever kind it is.
        /// </summary>
        private sealed record Reader(UInt32                 StreamId,
                                     HTTP2ClientTunnel?     Tunnel,
                                     HTTP2ClientStream?     Request)
        {

            public Task<Byte[]?> ReadAsync()

                => Tunnel is not null
                       ? Tunnel. ReadAsync(CancellationToken.None)
                       : Request!.ReadAsync();

            public Task WriteAsync(Byte[] Data)

                => Tunnel is not null
                       ? Tunnel. WriteAsync(Data, CancellationToken.None)
                       : Request!.WriteAsync(Data);

            /// <summary>
            /// End the client's side: a tunnel's CloseAsync, a request's CompleteRequestAsync.
            /// </summary>
            public Task EndAsync()

                => Tunnel is not null
                       ? Tunnel. CloseAsync()
                       : Request!.CompleteRequestAsync();

            public ValueTask DisposeAsync()

                => Tunnel is not null
                       ? Tunnel. DisposeAsync()
                       : Request!.DisposeAsync();

        }

        /// <summary>
        /// Open a stream of this kind on the next stream ID, and answer it with a
        /// 200 whose DATA is to follow: a CONNECT tunnel, or a streamed POST. The
        /// client's side stays open.
        /// </summary>
        private static async Task<Reader> OpenAsync(Kind                   Kind,
                                                    HoldingH2Transport     Transport,
                                                    HTTP2ClientConnection  Connection)
        {

            if (Kind == Kind.Tunnel)
            {

                var opening  = Connection.OpenTunnelAsync("localhost:443");
                var headers  = await Transport.NextHeadersAsync();

                await Transport.SendHeadersAsync(headers.StreamId, [(":status", "200")]);

                var tunnel   = await opening.WaitAsync(HoldingH2Transport.StepTimeout);

                return new Reader(tunnel.StreamId, tunnel, null);

            }

            var request = await Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload").
                                            WaitAsync(HoldingH2Transport.StepTimeout);

            await Transport.NextHeadersAsync();

            await Transport.SendHeadersAsync(request.StreamId, [(":status", "200")]);
            await request.GetResponseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

            return new Reader(request.StreamId, null, request);

        }

        /// <summary>
        /// Allow the client one stream at a time (SETTINGS_MAX_CONCURRENT_STREAMS),
        /// so that the next request waits for the slot of the one before.
        /// </summary>
        private static Task OneStreamAtATimeAsync(HoldingH2Transport Transport)

            => Transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, 1)));

        /// <summary>
        /// What the client sent on its streams, one line per frame: "HEADERS n",
        /// "DATA n" with " END_STREAM" if it ended the stream, "RST_STREAM n CODE".
        /// WINDOW_UPDATEs are left out, and so is everything on stream 0.
        /// </summary>
        private static List<String> Describe(IEnumerable<HTTP2Frame> Frames)

            => [.. Frames.Where (frame => frame.StreamId != 0 && frame.Type != HTTP2FrameType.WINDOW_UPDATE).
                          Select(frame => frame.Type switch {
                                              HTTP2FrameType.RST_STREAM  => $"RST_STREAM {frame.StreamId} {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Payload)}",
                                              _                          => $"{frame.Type} {frame.StreamId}" + (frame.EndStream ? " END_STREAM" : "")
                                          })];

        /// <summary>
        /// What the client sent on its streams up to and including the next HEADERS, as <see cref="Describe"/> lists it.
        /// </summary>
        private static async Task<List<String>> SentUntilHeadersAsync(HoldingH2Transport Transport)
        {

            var skipped = new List<HTTP2Frame>();
            var headers = await Transport.NextHeadersAsync(skipped);

            return Describe([.. skipped, headers]);

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
        /// The type of the exception a call failed with, or "none".
        /// </summary>
        private static async Task<String> FailureOf(Func<Task> Call)
        {

            Task call;

            try
            {
                call = Call();
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }

            var (ended, failure) = await EndOf(call);

            return ended
                       ? failure?.GetType().Name ?? "none"
                       : "still waiting";

        }

        #endregion


        #region Dispose_ResetsWithCancel_SlotGivenBack(Kind)

        /// <summary>
        /// The server has sent part of what it has for a stream, and the
        /// application gives the stream up: DisposeAsync resets it with
        /// RST_STREAM CANCEL. That frees its slot: the next request, which waited
        /// for it, one stream at a time, goes out after the RST_STREAM, and is
        /// answered. A read that waited on the stream fails with an
        /// ObjectDisposedException, and so does every call after DisposeAsync.
        /// DisposeAsync again sends nothing more.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedRequest)]
        public async Task Dispose_ResetsWithCancel_SlotGivenBack(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection   = await transport.ConnectAsync(new HTTP2ClientOptions());

            await OneStreamAtATimeAsync(transport);

            var reader       = await OpenAsync(Kind, transport, connection);

            await transport.SendAsync(HTTP2Frame.CreateData(reader.StreamId, new Byte[1000]));

            var firstChunk   = await reader.ReadAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            var waitingRead  = reader.ReadAsync();

            var next         = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");
            var nextWaited   = !next.IsCompleted;

            await reader.DisposeAsync();
            await reader.DisposeAsync();

            var sent         = await SentUntilHeadersAsync(transport);

            await transport.RespondAsync(reader.StreamId + 2, "next");

            var answered     = await EndOf(next);

            var readFailure  = await FailureOf(() => waitingRead);
            var readAfter    = await FailureOf(reader.ReadAsync);
            var writeAfter   = await FailureOf(() => reader.WriteAsync([0x42]));
            var endAfter     = await FailureOf(reader.EndAsync);

            Assert.Multiple(() =>
            {

                Assert.That(firstChunk?.Length,  Is.EqualTo(1000),                                                       "the chunk read before");
                Assert.That(nextWaited,          Is.True,                                                                "the next request waited for the slot");

                Assert.That(sent,                Is.EqualTo(new[] { $"RST_STREAM {reader.StreamId} CANCEL", $"HEADERS {reader.StreamId + 2} END_STREAM" }),
                                                                                                                         "what the client sent once the stream was given up");

                Assert.That(answered.Ended,      Is.True,                                                                "the next request was answered");
                Assert.That(answered.Failure,    Is.Null,                                                                "and did not fail");

                Assert.That(readFailure,         Is.EqualTo(nameof(ObjectDisposedException)),                            "the read that waited on the stream");
                Assert.That(readAfter,           Is.EqualTo(nameof(ObjectDisposedException)),                            "a read afterwards");
                Assert.That(writeAfter,          Is.EqualTo(nameof(ObjectDisposedException)),                            "a write afterwards");
                Assert.That(endAfter,            Is.EqualTo(nameof(ObjectDisposedException)),                            "ending the client's side afterwards");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_ResponseSideAsItArrived(Answered)

        /// <summary>
        /// A streamed request given up keeps what of its response had arrived:
        /// the response head, when it had, and the trailers, when the response
        /// had ended. Whichever had not, fails with an ObjectDisposedException,
        /// rather than wait for what will never come.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task Dispose_ResponseSideAsItArrived(Boolean Answered)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var request     = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload").
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            await transport.NextHeadersAsync();

            if (Answered)
                await transport.SendHeadersAsync(request.StreamId, [(":status", "202")]);

            await request.DisposeAsync();

            var head        = await EndOf(request.GetResponseAsync());
            var trailers    = await EndOf(request.GetTrailersAsync());

            var sent        = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(head.Ended,                                Is.True,                                               "the response head is there, or failed");
                Assert.That(head.Failure?.GetType().Name,              Is.EqualTo(Answered ? null : nameof(ObjectDisposedException)),
                                                                                                                              "how the response head ended");

                if (Answered && head.Failure is null)
                    Assert.That(request.GetResponseAsync().Result.Status,  Is.EqualTo(202),                                   "the response head that had arrived");

                Assert.That(trailers.Ended,                            Is.True,                                               "the trailers failed rather than wait");
                Assert.That(trailers.Failure?.GetType().Name,          Is.EqualTo(nameof(ObjectDisposedException)),          "how the trailers ended");

                Assert.That(Describe(sent),                            Is.EqualTo(new[] { $"RST_STREAM {request.StreamId} CANCEL" }),
                                                                                                                              "what the client sent");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_AfterBothSidesEnded_SendsNothing(Kind)

        /// <summary>
        /// A stream both sides have ended is closed, and nothing but PRIORITY may
        /// go out on it (RFC 9113, Section 5.1): giving it up sends nothing. A
        /// read afterwards fails all the same, though the byte the server sent is
        /// still unread, and so does a write: the stream is disposed.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedRequest)]
        public async Task Dispose_AfterBothSidesEnded_SendsNothing(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind, transport, connection);

            await reader.EndAsync().WaitAsync(HoldingH2Transport.StepTimeout);
            await transport.SendAsync(HTTP2Frame.CreateData(reader.StreamId, [0x42], EndStream: true));

            var before      = await transport.SentUntilPingAckAsync();

            await reader.DisposeAsync();

            var sent        = await transport.SentUntilPingAckAsync();

            var readAfter        = await FailureOf(reader.ReadAsync);
            var writeAfter       = await FailureOf(() => reader.WriteAsync([0x42]));
            var emptyWriteAfter  = await FailureOf(() => reader.WriteAsync([]));

            Assert.Multiple(() =>
            {

                Assert.That(Describe(before),  Is.EqualTo(new[] { $"DATA {reader.StreamId} END_STREAM" }),  "what the client sent before: the end of its side");
                Assert.That(Describe(sent),    Is.Empty,                                                     "what the client sent as the stream was given up");

                Assert.That(readAfter,         Is.EqualTo(nameof(ObjectDisposedException)),                  "a read afterwards");
                Assert.That(writeAfter,        Is.EqualTo(nameof(ObjectDisposedException)),                  "a write afterwards");
                Assert.That(emptyWriteAfter,   Is.EqualTo(nameof(ObjectDisposedException)),                  "an empty write afterwards, which sends nothing in any case");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_WriteWaitingForWindow_FailsAsDisposed(Kind)

        /// <summary>
        /// A write has sent all the window the server granted, 65 535 octets, and
        /// waits for more, to send its last 100. The application gives the stream
        /// up meanwhile: the write wakes, and fails with an ObjectDisposedException,
        /// rather than wait for window that never comes, or return as if its
        /// octets had gone out. Nothing of it follows the RST_STREAM.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedRequest)]
        public async Task Dispose_WriteWaitingForWindow_FailsAsDisposed(Kind Kind)
        {

            const Int32 InitialWindow = 65535;

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind, transport, connection);

            var writing     = reader.WriteAsync(new Byte[InitialWindow + 100]);

            var written     = 0L;

            while (written < InitialWindow)
            {
                var frame = await transport.NextFrameOnAsync(reader.StreamId);
                if (frame.Type == HTTP2FrameType.DATA)
                    written += frame.Payload.Length;
            }

            var waited      = !writing.IsCompleted;

            await reader.DisposeAsync();

            var (ended, failure) = await EndOf(writing);

            var sent        = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(written,                    Is.EqualTo(InitialWindow),                         "octets written before the window ran out");
                Assert.That(waited,                     Is.True,                                           "the write waited for window");

                Assert.That(ended,                      Is.True,                                           "the write ended once the stream was given up");
                Assert.That(failure?.GetType().Name,    Is.EqualTo(nameof(ObjectDisposedException)),      "how it failed");

                Assert.That(Describe(sent),             Is.EqualTo(new[] { $"RST_STREAM {reader.StreamId} CANCEL" }),
                                                                                                           "what the client sent then");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_AfterTheServersReset_SendsNothing(Kind)

        /// <summary>
        /// A stream the server has reset is closed: giving it up sends nothing, no
        /// RST_STREAM in answer to the server's.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedRequest)]
        public async Task Dispose_AfterTheServersReset_SendsNothing(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var reader      = await OpenAsync(Kind, transport, connection);

            await transport.SendAsync(HTTP2Frame.CreateRstStream(reader.StreamId, HTTP2ErrorCode.INTERNAL_ERROR));
            await transport.SentUntilPingAckAsync();

            await reader.DisposeAsync();

            var sent        = await transport.SentUntilPingAckAsync();

            Assert.That(Describe(sent), Is.Empty, "what the client sent as the stream was given up");

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_AfterTheServerEnded_ResetsTheClientsSide(Kind)

        /// <summary>
        /// The server has ended its side, the client's is still open: giving the
        /// stream up resets it with RST_STREAM CANCEL — the client's side is not
        /// ended as if all of it had been sent — and frees its slot. What the
        /// server sent is not read: it is dropped.
        /// </summary>
        [TestCase(Kind.Tunnel)]
        [TestCase(Kind.StreamedRequest)]
        public async Task Dispose_AfterTheServerEnded_ResetsTheClientsSide(Kind Kind)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            await OneStreamAtATimeAsync(transport);

            var reader      = await OpenAsync(Kind, transport, connection);

            await transport.SendAsync(HTTP2Frame.CreateData(reader.StreamId, new Byte[1000], EndStream: true));

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");
            var nextWaited  = !next.IsCompleted;

            await reader.DisposeAsync();

            var sent        = await SentUntilHeadersAsync(transport);

            await transport.RespondAsync(reader.StreamId + 2, "next");

            var answered    = await EndOf(next);

            Assert.Multiple(() =>
            {

                Assert.That(nextWaited,        Is.True,   "the next request waited for the slot");
                Assert.That(sent,              Is.EqualTo(new[] { $"RST_STREAM {reader.StreamId} CANCEL", $"HEADERS {reader.StreamId + 2} END_STREAM" }),
                                                          "what the client sent once the stream was given up");
                Assert.That(answered.Ended,    Is.True,   "the next request was answered");
                Assert.That(answered.Failure,  Is.Null,   "and did not fail");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Dispose_BetweenHeadersAndContinuation_BlockStillDecoded()

        /// <summary>
        /// The application gives a streamed request up while the head of its
        /// response is half-way in: the HEADERS frame has arrived, the
        /// CONTINUATION frame that ends the block not yet. The block is decoded
        /// once it is complete, all the same, and dropped (RFC 9113, Section 5.1):
        /// the next response, whose head refers to the field the dropped one
        /// added to the HPACK dynamic table, arrives as the server sent it.
        /// </summary>
        [Test]
        public async Task Dispose_BetweenHeadersAndContinuation_BlockStillDecoded()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());
            var request     = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload").
                                               WaitAsync(HoldingH2Transport.StepTimeout);

            await transport.NextHeadersAsync();

            var head        = transport.EncodeHeaderBlock([(":status", "200"), ("x-session", "4711")]);

            await transport.SendAsync(HTTP2Frame.CreateHeaders(request.StreamId, head[..1], EndStream: false, EndHeaders: false));

            await request.DisposeAsync();

            await transport.SendAsync(new HTTP2Frame {
                                          Type      = HTTP2FrameType.CONTINUATION,
                                          Flags     = HTTP2FrameFlags.END_HEADERS,
                                          StreamId  = request.StreamId,
                                          Payload   = head[1..]
                                      });

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");
            var nextHeaders = await transport.NextHeadersAsync();

            await transport.SendHeadersAsync(nextHeaders.StreamId, [(":status", "200"), ("x-session", "4711")], EndStream: true);

            var answered    = await EndOf(next);

            Assert.Multiple(() =>
            {

                Assert.That(answered.Ended,    Is.True,  "the next request was answered");
                Assert.That(answered.Failure,  Is.Null,  "and did not fail");

                if (answered.Ended && answered.Failure is null)
                    Assert.That(next.Result.HeaderValue("x-session"),  Is.EqualTo("4711"),  "the field the next response refers to in the dynamic table");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region OpenTunnel_CancelledBeforeTheAnswer_StreamReset()

        /// <summary>
        /// The caller gives up opening a tunnel before the server has answered.
        /// Nobody would hold the tunnel the server goes on to accept: the stream
        /// is reset with RST_STREAM CANCEL, and its slot is free for the next
        /// request, which waited for it, one stream at a time. The server's
        /// answer, which crossed the RST_STREAM, changes nothing. The stream used
        /// to stay open, holding its slot: the next request waited for good.
        /// </summary>
        [Test]
        public async Task OpenTunnel_CancelledBeforeTheAnswer_StreamReset()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            await OneStreamAtATimeAsync(transport);

            using var giveUp = new CancellationTokenSource();

            var opening     = connection.OpenTunnelAsync("localhost:443", CancellationToken: giveUp.Token);
            var connect     = await transport.NextHeadersAsync();

            var next        = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            giveUp.Cancel();

            var (ended, failure) = await EndOf(opening);

            // The server's answer, crossing the client's RST_STREAM.
            await transport.SendHeadersAsync(connect.StreamId, [(":status", "200")]);

            var sent        = await SentUntilHeadersAsync(transport);

            await transport.RespondAsync(connect.StreamId + 2, "next");

            var answered    = await EndOf(next);

            Assert.Multiple(() =>
            {

                Assert.That(ended,                           Is.True,                                     "opening the tunnel ended");
                Assert.That(failure,                         Is.InstanceOf<OperationCanceledException>(), "as cancelled");

                Assert.That(sent,                            Is.EqualTo(new[] { $"RST_STREAM {connect.StreamId} CANCEL", $"HEADERS {connect.StreamId + 2} END_STREAM" }),
                                                                                                          "what the client sent once the opening was cancelled");

                Assert.That(answered.Ended,                  Is.True,                                     "the next request was answered");
                Assert.That(answered.Failure,                Is.Null,                                     "and did not fail");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

        #region Download_ErrorStatus_ResponseGivenUp()

        /// <summary>
        /// DownloadAsync writes no error page into the destination: it returns
        /// the status, and reads nothing of the body. Larger than the stream
        /// window, as a body may be, the server could send no more of it, and the
        /// stream stayed open, holding its slot. DownloadAsync gives the response
        /// up now, with RST_STREAM CANCEL.
        /// </summary>
        [Test]
        public async Task Download_ErrorStatus_ResponseGivenUp()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(new HTTP2ClientOptions());

            var destination = new MemoryStream();
            var download    = connection.DownloadAsync(URIScheme.https, "localhost", "/file", destination);

            var request     = await transport.NextHeadersAsync();

            await transport.SendHeadersAsync(request.StreamId, [(":status", "404")]);
            await transport.SendAsync(HTTP2Frame.CreateData(request.StreamId, new Byte[16 * 1024]));

            var result      = await download.WaitAsync(HoldingH2Transport.StepTimeout);
            var sent        = await transport.SentUntilPingAckAsync();

            Assert.Multiple(() =>
            {

                Assert.That(result.Status,       Is.EqualTo(404),  "the status DownloadAsync returned");
                Assert.That(destination.Length,  Is.Zero,          "bytes written to the destination");

                Assert.That(Describe(sent),      Does.Contain($"RST_STREAM {request.StreamId} CANCEL"),
                                                                   "what the client sent: the response given up");

            });

            await connection.CloseAsync().WaitAsync(HoldingH2Transport.StepTimeout);

        }

        #endregion

    }

}
