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
using System.Net.Security;
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A request's stream can outlive its exchange. A server may answer before
    /// the request body is all sent and go on reading it (RFC 9113, Section 8.1),
    /// and the client removed the exchange at the response's END_STREAM all the
    /// same. Two things followed the exchange where they had to follow the
    /// stream:
    ///
    ///   - The gate for the server's MAX_CONCURRENT_STREAMS counted exchanges,
    ///     while the stream allocator counts the streams that are open or
    ///     half-closed. The upload's stream, half-closed (remote), still counted
    ///     there, so the next request passed the gate and then failed with
    ///     "Maximum concurrent streams (1) exceeded" instead of waiting.
    ///   - A stream's WINDOW_UPDATE reached the stream through its exchange only,
    ///     so the server's credit for the rest of the upload was dropped, and the
    ///     upload stopped for good once its send window ran out.
    ///
    /// CONNECT tunnels kept streams open behind their exchanges as well: one the
    /// server rejected, which nobody ended, and one whose END_STREAM from the
    /// server never reached its stream. Each held its slot for the rest of the
    /// connection.
    ///
    /// After a GOAWAY the server takes no new stream (RFC 9113, Section 6.8). A
    /// request that would open one then, or that waits for a slot when the
    /// GOAWAY comes, fails at once, not processed. It went out, or waited, for
    /// an answer that never came.
    /// </summary>
    [TestFixture]
    public class ClientStreamSlotTests
    {

        #region (helpers)

        /// <summary>
        /// How long any single step may take before the test gives up.
        /// </summary>
        private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The send window a stream and the connection start with, when the
        /// server announces no other: what an upload sends before it needs the
        /// server's WINDOW_UPDATEs.
        /// </summary>
        private const Int32 InitialWindow = 65535;

        /// <summary>
        /// Three initial windows and more.
        /// </summary>
        private const Int32 UploadSize = 200 * 1024;

        /// <summary>
        /// Whether Task ends within the step timeout.
        /// </summary>
        private static async Task<Boolean> EndsInTime(Task Task)

            => await System.Threading.Tasks.Task.WhenAny(Task, System.Threading.Tasks.Task.Delay(StepTimeout)) == Task;

        /// <summary>
        /// Fail at once if a request that has to wait for a stream slot has
        /// already failed rather than wait.
        /// </summary>
        private static void AssertWaiting(Task<HTTP2Response> Request)
        {

            if (Request.IsFaulted)
                Assert.Fail($"The request failed rather than wait for a stream slot: {Request.Exception!.GetBaseException().Message}");

            Assert.That(Request.IsCompleted, Is.False, "the request waits for a stream slot");

        }

        /// <summary>
        /// The server's end of one connection, on a <see cref="MockH2Server"/>. It
        /// answers the HEADERS of each request as Answer says, counts the DATA the
        /// client sends on each stream, and returns window for it: for every DATA
        /// frame from the start, or once <see cref="GrantWindowAsync"/> has been
        /// called. It keeps what the client sent, in order: "HEADERS n",
        /// "END_STREAM n" for DATA that ended a stream, and "RST_STREAM n CODE".
        /// </summary>
        private sealed class Peer : IAsyncDisposable
        {

            private readonly MockH2Server                               mock;
            private readonly Func<Peer, UInt32, Task>                   answer;
            private readonly SemaphoreSlim                              writeLock  = new(1, 1);
            private readonly Lock                                       sync       = new();
            private readonly Dictionary<UInt32, Int64>                  received   = [];
            private readonly List<String>                               events     = [];
            private readonly Dictionary<String, TaskCompletionSource>  awaited    = [];
            private          SslStream?                                 ssl;
            private          HPACKEncoder?                              encoder;
            private          Boolean                                    granting;

            public Peer(Int32                     MaxConcurrentStreams,
                        Boolean                   GrantWindow,
                        Func<Peer, UInt32, Task>  Answer)
            {
                this.answer    = Answer;
                this.granting  = GrantWindow;
                this.mock      = MockH2Server.Start(MaxConcurrentStreams, OnFrameAsync);
            }

            public Task<HTTP2ClientConnection> ConnectAsync()

                => HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);

            /// <summary>
            /// What the client sent so far, in order.
            /// </summary>
            public IReadOnlyList<String> Events
            {
                get
                {
                    lock (sync)
                        return [.. events];
                }
            }

            /// <summary>
            /// The DATA bytes the client sent on a stream so far.
            /// </summary>
            public Int64 Received(UInt32 StreamId)
            {
                lock (sync)
                    return received.GetValueOrDefault(StreamId);
            }

            /// <summary>
            /// Completes once the client has sent what Event names.
            /// </summary>
            public Task SeenAsync(String Event)
            {
                lock (sync)
                {

                    if (events.Contains(Event))
                        return Task.CompletedTask;

                    if (!awaited.TryGetValue(Event, out var seen))
                        awaited[Event] = seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    return seen.Task;

                }
            }

            /// <summary>
            /// Return window for every DATA frame from now on, and at once for the
            /// DATA that came on this stream so far.
            /// </summary>
            public Task GrantWindowAsync(UInt32 StreamId)
            {

                Int64 backlog;

                lock (sync)
                {
                    granting  = true;
                    backlog   = received.GetValueOrDefault(StreamId);
                }

                return backlog > 0
                           ? ReturnWindowAsync(StreamId, backlog)
                           : Task.CompletedTask;

            }

            /// <summary>
            /// Answer a stream with a status. Without a body the HEADERS end the
            /// stream if EndStream says so; with one, a DATA frame after them
            /// carries it and does.
            /// </summary>
            public async Task RespondAsync(UInt32 StreamId, Int32 Status, Boolean EndStream = true, String? Body = null)
            {

                await writeLock.WaitAsync();

                try
                {

                    // Encoded under the write lock, as the peer's decoder takes the
                    // header blocks in the order they are sent.
                    await MockH2Server.WriteFrameAsync(ssl!, HTTP2Frame.CreateHeaders(StreamId,
                                                                                      encoder!.EncodeHeaderBlock([(":status", Status.ToString())]),
                                                                                      EndStream:  EndStream && Body is null,
                                                                                      EndHeaders: true));

                    if (Body is not null)
                        await MockH2Server.WriteFrameAsync(ssl!, HTTP2Frame.CreateData(StreamId, Encoding.ASCII.GetBytes(Body), EndStream));

                }
                finally
                {
                    writeLock.Release();
                }

            }

            /// <summary>
            /// Send frames, in one piece, between the others.
            /// </summary>
            public async Task SendAsync(params HTTP2Frame[] Frames)
            {

                await writeLock.WaitAsync();

                try
                {
                    foreach (var frame in Frames)
                        await MockH2Server.WriteFrameAsync(ssl!, frame);
                }
                finally
                {
                    writeLock.Release();
                }

            }

            private Task ReturnWindowAsync(UInt32 StreamId, Int64 Bytes)

                => SendAsync(HTTP2Frame.CreateWindowUpdate(StreamId, (UInt32) Bytes),
                             HTTP2Frame.CreateWindowUpdate(0,        (UInt32) Bytes));

            private void Record(String Event)
            {

                TaskCompletionSource? seen;

                lock (sync)
                {
                    events.Add(Event);
                    awaited.Remove(Event, out seen);
                }

                seen?.TrySetResult();

            }

            private async Task OnFrameAsync(Int32 Connection, SslStream Ssl, HTTP2Frame Frame, HPACKEncoder Encoder)
            {

                lock (sync)
                {
                    ssl      = Ssl;
                    encoder  = Encoder;
                }

                switch (Frame.Type)
                {

                    case HTTP2FrameType.HEADERS:
                        Record($"HEADERS {Frame.StreamId}");
                        await answer(this, Frame.StreamId);
                        break;

                    case HTTP2FrameType.DATA:

                        Int64 grant;

                        lock (sync)
                        {
                            received[Frame.StreamId] = received.GetValueOrDefault(Frame.StreamId) + Frame.Length;
                            grant                    = granting ? Frame.Length : 0;
                        }

                        if (grant > 0)
                            await ReturnWindowAsync(Frame.StreamId, grant);

                        if (Frame.EndStream)
                            Record($"END_STREAM {Frame.StreamId}");

                        break;

                    case HTTP2FrameType.RST_STREAM:
                        Record($"RST_STREAM {Frame.StreamId} {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload)}");
                        break;

                }

            }

            public ValueTask DisposeAsync()

                => mock.DisposeAsync();

        }

        #endregion


        #region Upload_AnsweredBeforeItsEnd_GetsItsStreamWindow()

        /// <summary>
        /// The server answers at the request's HEADERS, then reads the whole body
        /// and returns window for each DATA frame. Its WINDOW_UPDATEs for the
        /// stream came after the response, when the exchange was gone, and were
        /// dropped: the upload stopped after the stream's initial window.
        /// </summary>
        [Test]
        public async Task Upload_AnsweredBeforeItsEnd_GetsItsStreamWindow()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 0,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => peer.RespondAsync(streamId, 200, Body: "ok"));

            var connection  = await peer.ConnectAsync();
            var response    = await connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload",
                                                                Body: new Byte[UploadSize]).WaitAsync(StepTimeout);

            Assert.That(response.Status, Is.EqualTo(200), "the early answer");

            Assert.That(await EndsInTime(peer.SeenAsync("END_STREAM 1")), Is.True,
                        $"the upload ends, rather than stop after {peer.Received(1)} of its {UploadSize} bytes");

            Assert.That(peer.Received(1), Is.EqualTo(UploadSize), "all of the upload arrived");

            await connection.CloseAsync();

        }

        #endregion

        #region StreamingRequest_WrittenAfterItsResponseEnded_GetsItsStreamWindow()

        /// <summary>
        /// The same for a streamed request: its response has ended, and the caller
        /// goes on writing the request body.
        /// </summary>
        [Test]
        public async Task StreamingRequest_WrittenAfterItsResponseEnded_GetsItsStreamWindow()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 0,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => peer.RespondAsync(streamId, 200));

            var connection  = await peer.ConnectAsync();
            var request     = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/stream").WaitAsync(StepTimeout);
            var head        = await request.GetResponseAsync().WaitAsync(StepTimeout);
            var end         = await request.ReadAsync().WaitAsync(StepTimeout);

            Assert.That(head.Status, Is.EqualTo(200), "the early answer");
            Assert.That(end,         Is.Null,         "the response has ended");

            Assert.That(await EndsInTime(request.WriteAsync(new Byte[UploadSize])), Is.True,
                        $"the write ends, rather than stop after {peer.Received(1)} of its {UploadSize} bytes");

            await request.CompleteRequestAsync().WaitAsync(StepTimeout);

            Assert.That(await EndsInTime(peer.SeenAsync("END_STREAM 1")), Is.True, "the request ends");
            Assert.That(peer.Received(1), Is.EqualTo(UploadSize), "all of the request body arrived");

            await connection.CloseAsync();

        }

        #endregion

        #region Request_WaitsForTheSlotOfAnUploadAnsweredBeforeItsEnd()

        /// <summary>
        /// With room for one stream, the server answers an upload at its HEADERS
        /// and returns no window until the next request is under way. The upload's
        /// stream holds the slot until the upload ends, so the next request waits
        /// for it, and goes out after the upload's END_STREAM. It passed the gate
        /// and failed with "Maximum concurrent streams (1) exceeded".
        /// </summary>
        [Test]
        public async Task Request_WaitsForTheSlotOfAnUploadAnsweredBeforeItsEnd()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          false,
                                            Answer:               (peer, streamId) => peer.RespondAsync(streamId, 200, Body: "ok"));

            var connection  = await peer.ConnectAsync();
            var upload      = await connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload",
                                                                Body: new Byte[UploadSize]).WaitAsync(StepTimeout);

            Assert.That(upload.Status, Is.EqualTo(200), "the early answer");

            Assert.That(await H2.EventuallyAsync(() => peer.Received(1) == InitialWindow), Is.True,
                        $"the upload waits for window, after {peer.Received(1)} bytes");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            AssertWaiting(next);

            Assert.That(connection.AvailableStreamSlots, Is.EqualTo(0), "the upload holds the only stream slot");

            await peer.GrantWindowAsync(1);

            Assert.That(await EndsInTime(peer.SeenAsync("END_STREAM 1")), Is.True,
                        $"the upload ends, rather than stop after {peer.Received(1)} of its {UploadSize} bytes");

            Assert.That(await EndsInTime(next), Is.True, "the next request is answered once the upload has ended");

            var response = await next;

            Assert.Multiple(() => {
                Assert.That(response.Status,   Is.EqualTo(200),                                               "the next request's answer");
                Assert.That(peer.Received(1),  Is.EqualTo(UploadSize),                                        "all of the upload arrived");
                Assert.That(peer.Events,       Is.EqualTo(new[] { "HEADERS 1", "END_STREAM 1", "HEADERS 3" }), "the next request goes out after the upload's END_STREAM");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region Request_WaitsForTheSlotOfAStreamingRequestWhoseResponseEnded()

        /// <summary>
        /// The same for a streamed request whose response ended first: its stream
        /// holds the slot until the caller ends the request.
        /// </summary>
        [Test]
        public async Task Request_WaitsForTheSlotOfAStreamingRequestWhoseResponseEnded()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => streamId == 1
                                                                                          ? peer.RespondAsync(streamId, 200)
                                                                                          : peer.RespondAsync(streamId, 200, Body: "ok"));

            var connection  = await peer.ConnectAsync();
            var request     = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/stream").WaitAsync(StepTimeout);

            await request.GetResponseAsync().WaitAsync(StepTimeout);

            Assert.That(await request.ReadAsync().WaitAsync(StepTimeout), Is.Null, "the response has ended");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            AssertWaiting(next);

            Assert.That(connection.AvailableStreamSlots, Is.EqualTo(0), "the request holds the only stream slot");

            await request.WriteAsync("the rest of the request"u8.ToArray()).WaitAsync(StepTimeout);
            await request.CompleteRequestAsync().WaitAsync(StepTimeout);

            Assert.That(await EndsInTime(next), Is.True, "the next request is answered once the streamed one has ended");

            var response = await next;

            Assert.Multiple(() => {
                Assert.That(response.Status,  Is.EqualTo(200),                                               "the next request's answer");
                Assert.That(peer.Events,      Is.EqualTo(new[] { "HEADERS 1", "END_STREAM 1", "HEADERS 3" }), "the next request goes out after the streamed one's END_STREAM");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region Request_GetsTheSlotOfAnUploadStoppedAfterItsResponse()

        /// <summary>
        /// A server that has answered in full may stop the rest of the upload with
        /// RST_STREAM NO_ERROR (RFC 9113, Section 8.1). That closes the stream and
        /// frees its slot for the request waiting for it, although the stream's
        /// exchange is long gone.
        /// </summary>
        [Test]
        public async Task Request_GetsTheSlotOfAnUploadStoppedAfterItsResponse()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          false,
                                            Answer:               (peer, streamId) => peer.RespondAsync(streamId, 200, Body: "ok"));

            var connection  = await peer.ConnectAsync();
            var upload      = await connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload",
                                                                Body: new Byte[UploadSize]).WaitAsync(StepTimeout);

            Assert.That(upload.Status, Is.EqualTo(200), "the early answer");

            Assert.That(await H2.EventuallyAsync(() => peer.Received(1) == InitialWindow), Is.True,
                        $"the upload waits for window, after {peer.Received(1)} bytes");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            AssertWaiting(next);

            await peer.SendAsync(HTTP2Frame.CreateRstStream(1, HTTP2ErrorCode.NO_ERROR));

            Assert.That(await EndsInTime(next), Is.True, "the next request is answered once the upload has been stopped");

            var response = await next;

            Assert.Multiple(() => {
                Assert.That(response.Status,   Is.EqualTo(200),                                "the next request's answer");
                Assert.That(peer.Received(1),  Is.EqualTo(InitialWindow),                      "nothing more of the upload after the reset");
                Assert.That(peer.Events,       Is.EqualTo(new[] { "HEADERS 1", "HEADERS 3" }), "the next request goes out after the reset");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region RejectedTunnel_GivesBackItsSlot(WithBody)

        /// <summary>
        /// A rejected CONNECT hands no tunnel to anyone, and nobody ended its
        /// stream: it stayed open on both ends and kept its slot. The client now
        /// ends it itself: with END_STREAM when the rejection has ended the
        /// server's side, else with RST_STREAM CANCEL, as it reads nothing of the
        /// rest.
        /// </summary>
        [TestCase(false, "END_STREAM 1")]
        [TestCase(true,  "RST_STREAM 1 CANCEL")]
        public async Task RejectedTunnel_GivesBackItsSlot(Boolean WithBody, String EndOfTunnel)
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => streamId == 1
                                                                                          ? peer.RespondAsync(streamId, 407, Body: WithBody ? "proxy authentication required" : null)
                                                                                          : peer.RespondAsync(streamId, 200, Body: "ok"));

            var connection = await peer.ConnectAsync();

            Assert.That(async () => await connection.OpenTunnelAsync("echo.internal").WaitAsync(StepTimeout),
                        Throws.TypeOf<HTTP2StreamException>(),
                        "the CONNECT is rejected");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            Assert.That(await EndsInTime(next), Is.True, "the next request is answered");

            if (next.IsFaulted)
                Assert.Fail($"The next request failed: {next.Exception!.GetBaseException().Message}");

            Assert.Multiple(() => {
                Assert.That(next.Result.Status,  Is.EqualTo(200),                                              "the next request's answer");
                Assert.That(peer.Events,         Is.EqualTo(new[] { "HEADERS 1", EndOfTunnel, "HEADERS 3" }),  "the rejected CONNECT's stream ends before the next request goes out");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region EndedTunnel_GivesBackItsSlot(ServerEndsFirst)

        /// <summary>
        /// A tunnel both sides have ended is closed. The server's END_STREAM ended
        /// the tunnel's exchange but never reached its stream, which stayed open
        /// on that side and kept its slot, whichever side ended first.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task EndedTunnel_GivesBackItsSlot(Boolean ServerEndsFirst)
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          true,
                                            Answer:               async (peer, streamId) => {

                                                                      if (streamId != 1)
                                                                          await peer.RespondAsync(streamId, 200, Body: "ok");

                                                                      else
                                                                      {

                                                                          await peer.RespondAsync(streamId, 200, EndStream: false);

                                                                          if (ServerEndsFirst)
                                                                              await peer.SendAsync(HTTP2Frame.CreateData(streamId, [], EndStream: true));

                                                                      }

                                                                  });

            var connection  = await peer.ConnectAsync();
            var tunnel      = await connection.OpenTunnelAsync("echo.internal").WaitAsync(StepTimeout);

            if (ServerEndsFirst)
            {
                Assert.That(await tunnel.ReadAsync(CancellationToken.None).WaitAsync(StepTimeout), Is.Null, "the server has ended the tunnel");
                await tunnel.CloseAsync().WaitAsync(StepTimeout);
            }
            else
            {
                await tunnel.CloseAsync().WaitAsync(StepTimeout);
                await peer.SeenAsync("END_STREAM 1").WaitAsync(StepTimeout);
                await peer.SendAsync(HTTP2Frame.CreateData(1, [], EndStream: true));
                Assert.That(await tunnel.ReadAsync(CancellationToken.None).WaitAsync(StepTimeout), Is.Null, "the server has ended the tunnel");
            }

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            Assert.That(await EndsInTime(next), Is.True, "the next request is answered");

            if (next.IsFaulted)
                Assert.Fail($"The next request failed: {next.Exception!.GetBaseException().Message}");

            Assert.Multiple(() => {
                Assert.That(next.Result.Status,  Is.EqualTo(200),                                               "the next request's answer");
                Assert.That(peer.Events,         Is.EqualTo(new[] { "HEADERS 1", "END_STREAM 1", "HEADERS 3" }), "the next request goes out on stream 3");
            });

            await connection.CloseAsync();

        }

        #endregion


        #region Request_AfterGoAway_IsNotSent()

        /// <summary>
        /// After a GOAWAY the server takes no new streams, and the client must open
        /// none (RFC 9113, Section 6.8). A request started then went out on a new
        /// stream all the same, which the server ignores, and waited for an answer
        /// that never came. It fails at once now, as a request the GOAWAY leaves
        /// unprocessed does: with HTTP2RequestNotProcessedException, which says it
        /// may be sent again on another connection.
        /// </summary>
        [Test]
        public async Task Request_AfterGoAway_IsNotSent()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 0,
                                            GrantWindow:          true,
                                            Answer:               async (peer, streamId) => {
                                                                      if (streamId == 1)
                                                                      {
                                                                          await peer.RespondAsync(streamId, 200, Body: "ok");
                                                                          await peer.SendAsync(HTTP2Frame.CreateGoAway(1, HTTP2ErrorCode.NO_ERROR));
                                                                      }
                                                                  });

            var connection  = await peer.ConnectAsync();
            var first       = await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/first").WaitAsync(StepTimeout);

            Assert.That(await H2.EventuallyAsync(() => !connection.IsUsable), Is.True, "the GOAWAY has been handled");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            Assert.That(await EndsInTime(next), Is.True, "the request fails at once, rather than wait for an answer that never comes");

            Assert.Multiple(() => {
                Assert.That(first.Status,                      Is.EqualTo(200),                                    "the stream the GOAWAY covers");
                Assert.That(next.Exception?.GetBaseException(), Is.TypeOf<HTTP2RequestNotProcessedException>(),   "the request was not processed, and may be sent again elsewhere");
                Assert.That(peer.Events,                       Is.EqualTo(new[] { "HEADERS 1" }),                  "no new stream after the GOAWAY");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region WaitingRequest_FailsAtGoAway()

        /// <summary>
        /// A request that waits for a stream slot when the GOAWAY comes can no more
        /// be sent than one started after it. It waited on, and went out on a new
        /// stream once the stream the GOAWAY covers had ended.
        /// </summary>
        [Test]
        public async Task WaitingRequest_FailsAtGoAway()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => Task.CompletedTask);

            var connection  = await peer.ConnectAsync();
            var first       = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/first");

            await peer.SeenAsync("HEADERS 1").WaitAsync(StepTimeout);

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            AssertWaiting(next);

            // The server finishes the stream it has, and takes no new one.
            await peer.SendAsync(HTTP2Frame.CreateGoAway(1, HTTP2ErrorCode.NO_ERROR));

            Assert.That(await EndsInTime(next), Is.True, "the waiting request fails at the GOAWAY");

            await peer.RespondAsync(1, 200, Body: "ok");

            var response = await first.WaitAsync(StepTimeout);

            Assert.Multiple(() => {
                Assert.That(next.Exception?.GetBaseException(), Is.TypeOf<HTTP2RequestNotProcessedException>(),   "the request was not processed, and may be sent again elsewhere");
                Assert.That(response.Status,                   Is.EqualTo(200),                                    "the stream the GOAWAY covers still ends as usual");
                Assert.That(peer.Events,                       Is.EqualTo(new[] { "HEADERS 1" }),                  "no new stream after the GOAWAY");
            });

            await connection.CloseAsync();

        }

        #endregion

        #region Request_AfterGoAwayLeftTheOnlySlotUnprocessed_IsNotSent()

        /// <summary>
        /// A GOAWAY that leaves the only request unprocessed ends its exchange.
        /// Its stream used to stay half-closed, and count, as the server sends
        /// nothing more on it (the GOAWAY closes it now, see
        /// ClientGoAwayUnprocessedStreamTests). Since stream slots follow the
        /// stream, a request started then waited for that slot until the
        /// connection closed; before, it failed at once, but as "Maximum
        /// concurrent streams (1) exceeded". It now fails as any request after
        /// a GOAWAY does, whether its slot is free or not.
        /// </summary>
        [Test]
        public async Task Request_AfterGoAwayLeftTheOnlySlotUnprocessed_IsNotSent()
        {

            await using var peer = new Peer(MaxConcurrentStreams: 1,
                                            GrantWindow:          true,
                                            Answer:               (peer, streamId) => streamId == 1
                                                                                          ? peer.SendAsync(HTTP2Frame.CreateGoAway(0, HTTP2ErrorCode.NO_ERROR))
                                                                                          : Task.CompletedTask);

            var connection = await peer.ConnectAsync();

            Assert.That(async () => await connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/first").WaitAsync(StepTimeout),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "the GOAWAY leaves the first request unprocessed");

            var next = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/next");

            Assert.That(await EndsInTime(next), Is.True, "the request fails at once, rather than wait for a slot until the connection closes");

            Assert.Multiple(() => {
                Assert.That(next.Exception?.GetBaseException(), Is.TypeOf<HTTP2RequestNotProcessedException>(),   "the request was not processed, and may be sent again elsewhere");
                Assert.That(peer.Events,                       Is.EqualTo(new[] { "HEADERS 1" }),                  "no new stream after the GOAWAY");
            });

            await connection.CloseAsync();

        }

        #endregion

    }

}
