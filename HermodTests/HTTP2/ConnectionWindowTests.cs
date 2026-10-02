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
using System.Diagnostics;
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Several channels over one HTTP/2 connection — a charging station's OCPP
    /// WebSocket next to the WebSocket it uploads its log files on (RFC 8441),
    /// say — share the connection's flow-control window (RFC 9113, Section 6.9).
    /// The server gives back the window of a streamed request body, or of tunnel
    /// bytes, only once the application has read them, so a channel whose reader
    /// is slow, or has stalled, keeps every byte of window the client sent it.
    /// With a connection window no larger than a stream's, 1 MiB each, one such
    /// channel took all of it: the client could send DATA on no stream of the
    /// connection any more, and a log file written away slowly held up the
    /// real-time channel.
    ///
    /// The connection window is now four stream windows by default, and
    /// configurable on both roles. And the server gives back the window it owes
    /// for what it has read once the client has no more left than that: it used
    /// to wait until half the connection window was owed, which never came while
    /// unread streams held more than half of it.
    /// </summary>
    [TestFixture]
    public class ConnectionWindowTests
    {

        #region (helpers)

        /// <summary>
        /// How long any single step may take: on loopback each takes
        /// milliseconds, so a step that runs into this has stalled.
        /// </summary>
        private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// A stream's receive window, the SETTINGS_INITIAL_WINDOW_SIZE both roles
        /// advertise: 1 MiB.
        /// </summary>
        private const Int32 StreamWindow = 1024 * 1024;

        /// <summary>
        /// The size of the chunks uploaded here, and of the WebSocket frames.
        /// </summary>
        private const Int32 Chunk = 64 * 1024;

        /// <summary>
        /// An OCPP message, as the real-time channel carries it.
        /// </summary>
        private const String Heartbeat = "[2,\"19223201\",\"Heartbeat\",{}]";

        /// <summary>
        /// Whether the task completes within the step timeout.
        /// </summary>
        private static async Task<Boolean> Within(Task Task)
        {

            try
            {
                await Task.WaitAsync(StepTimeout);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }

        }

        private static String PathOf(IEnumerable<(String Name, String Value)> Headers)

            => Headers.FirstOrDefault(header => header.Name == ":path").Value;

        /// <summary>
        /// Requests that are neither streamed nor a CONNECT: there are none here.
        /// </summary>
        private static Task<(List<(String Name, String Value)>, Byte[]?)> NoRequests(UInt32                             StreamId,
                                                                                     List<(String Name, String Value)>  Headers,
                                                                                     Byte[]?                            Body,
                                                                                     CancellationToken                  CancellationToken)

            => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(([(":status", "404")], null));

        /// <summary>
        /// WebSockets (RFC 8441) on two paths: "/log" is accepted and then not read
        /// at all — a log writer that has stalled — until <paramref name="Release"/>;
        /// any other path echoes every message.
        /// </summary>
        private static HTTP2ConnectHandler WebSockets(Task Release)

            => (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                   StatusCode  = 200,

                   RunAsync    = PathOf(headers) == "/log"

                                     ? (tunnel, token) => Release.WaitAsync(token)

                                     : async (tunnel, token) => {

                                           var webSocket = new WebSocketConnection(tunnel, WebSocketRole.Server);

                                           while (await webSocket.ReceiveAsync(token) is { } message)
                                               await webSocket.SendTextAsync(Encoding.UTF8.GetString(message.Payload), token);

                                       }

               });

        /// <summary>
        /// Uploads on two paths: "/log" reads nothing — a log writer that has
        /// stalled — until <paramref name="Release"/>, and then answers 204,
        /// still without reading; any other path reads its body to the end,
        /// telling <paramref name="Read"/> how much it has read after each chunk,
        /// and answers 200 with the body's length.
        /// </summary>
        private static HTTP2StreamingHandler Uploads(Task Release, Action<Int64>? Read = null)

            => async (request, response, cancellationToken) => {

                   if (PathOf(request.Headers) == "/log")
                   {
                       await Release.WaitAsync(cancellationToken);
                       await response.WriteHeadersAsync([(":status", "204")], cancellationToken);
                       return;
                   }

                   var length = 0L;

                   while (await request.ReadAsync(cancellationToken) is { } chunk)
                   {
                       length += chunk.Length;
                       Read?.Invoke(length);
                   }

                   await response.WriteHeadersAsync([(":status", "200")], cancellationToken);
                   await response.WriteAsync(Encoding.ASCII.GetBytes(length.ToString()), cancellationToken);

               };

        /// <summary>
        /// Start an upload to this path.
        /// </summary>
        private static Task<HTTP2ClientStream> StartUploadAsync(HTTP2ClientConnection Connection, Int32 Port, String Path)

            => Connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, $"localhost:{Port}", Path);

        /// <summary>
        /// Upload this many bytes of request body, in chunks of 64 KiB, and return
        /// whether all of them went out: false as soon as a chunk did not within
        /// the step timeout, for want of window to send it in.
        /// </summary>
        private static async Task<Boolean> UploadAsync(HTTP2ClientStream Request, Int32 Bytes)
        {

            for (var sent = 0; sent < Bytes; sent += Chunk)
                if (!await Within(Request.WriteAsync(new Byte[Math.Min(Chunk, Bytes - sent)])))
                    return false;

            return true;

        }

        /// <summary>
        /// The status and the body of the response to an upload.
        /// </summary>
        private static async Task<(Int32 Status, String Body)> ResponseOfAsync(HTTP2ClientStream Request)
        {

            var head = await Request.GetResponseAsync();
            var body = new MemoryStream();

            while (await Request.ReadAsync() is { } chunk)
                body.Write(chunk);

            return (head.Status, Encoding.ASCII.GetString(body.ToArray()));

        }

        /// <summary>
        /// Open a stream with a POST to this path on a server connection whose
        /// client the test plays, and upload this many bytes on it, in DATA frames
        /// of 16 KiB, without ending it. Each frame within the step timeout: the
        /// test sends as much as the window it expects allows, and a server that
        /// granted less ends the connection with FLOW_CONTROL_ERROR and reads no
        /// more, so that a write into the full pipe would wait for good.
        /// </summary>
        private static async Task SendUploadAsync(PipedH2ServerConnection Peer, UInt32 StreamId, String Path, Int32 Bytes)
        {

            await Peer.SendHeadersAsync(StreamId,
                                        [(":method",    "POST"),
                                         (":scheme",    "http"),
                                         (":authority", "localhost"),
                                         (":path",      Path)],
                                        EndStream: false).WaitAsync(StepTimeout);

            for (var sent = 0; sent < Bytes; sent += 16 * 1024)
                await Peer.SendAsync(HTTP2Frame.CreateData(StreamId, new Byte[Math.Min(16 * 1024, Bytes - sent)])).WaitAsync(StepTimeout);

        }

        /// <summary>
        /// The increments of the connection-level WINDOW_UPDATEs the server sent,
        /// as read so far.
        /// </summary>
        private static List<Int64> ConnectionWindowUpdates(PipedH2ServerConnection Peer)

            => [.. Peer.FramesOn(0).
                        Where (frame => frame.Frame.Type == HTTP2FrameType.WINDOW_UPDATE).
                        Select(frame => (Int64) (BinaryPrimitives.ReadUInt32BigEndian(frame.Frame.Payload) & 0x7FFFFFFF))];

        /// <summary>
        /// The client's connection window as the frames tell it: RFC 9113's initial
        /// 65 535 bytes, plus every connection-level WINDOW_UPDATE the server sent,
        /// less the DATA the client sent. Read after a ping.
        /// </summary>
        private static Int64 ClientConnectionWindow(PipedH2ServerConnection Peer, Int64 Sent)

            => HTTP2FlowControl.InitialWindowSize + ConnectionWindowUpdates(Peer).Sum() - Sent;

        /// <summary>
        /// Ping until the condition holds, or fail once the step timeout is over:
        /// for frames a handler's task sends, which a single ping may not wait for.
        /// </summary>
        private static async Task PingUntilAsync(PipedH2ServerConnection Peer, Func<Boolean> Condition, String What)
        {

            var waited = Stopwatch.StartNew();

            while (!Condition())
            {

                if (waited.Elapsed > StepTimeout)
                    throw new TimeoutException($"Timed out waiting until {What}");

                await Peer.PingAsync();

            }

        }

        #endregion


        #region UnreadLogWebSocket_OCPPWebSocketStillFlows()

        /// <summary>
        /// A charging station's two WebSockets on one connection: the OCPP channel,
        /// and the channel it uploads its log files on, whose reader on the server
        /// has stalled. The log channel takes a whole stream window — 16 messages
        /// of 65 528 bytes, each one frame of 64 KiB with the 8 bytes of a masked
        /// frame's header (RFC 6455, Section 5.2) — and nothing reads it. An OCPP
        /// message sent after it comes back: it used to wait for good, the log
        /// channel holding the whole of the connection's window.
        /// </summary>
        [Test]
        public async Task UnreadLogWebSocket_OCPPWebSocketStillFlows()
        {

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests, ConnectHandler: WebSockets(release.Task));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var ocpp     = await connection.OpenWebSocketAsync($"localhost:{server.Port}", URIScheme.https, "/ocpp");
                var log      = await connection.OpenWebSocketAsync($"localhost:{server.Port}", URIScheme.https, "/log");

                var logSent  = true;

                for (var i = 0; i < StreamWindow / Chunk && logSent; i++)
                    logSent = await Within(log.SendBinaryAsync(new Byte[Chunk - 8], CancellationToken.None));

                var sent     = logSent && await Within(ocpp.SendTextAsync(Heartbeat, CancellationToken.None));
                var echo     = ocpp.ReceiveAsync(CancellationToken.None);
                var echoed   = sent    && await Within(echo);

                Assert.Multiple(() =>
                {

                    Assert.That(logSent,  Is.True,  "the log channel took a whole stream window, unread");
                    Assert.That(sent,     Is.True,  "the OCPP message went out after it");

                    Assert.That(echoed ? Encoding.UTF8.GetString(echo.Result!.Payload) : null,
                                Is.EqualTo(Heartbeat),
                                "the OCPP message came back");

                });

            }
            finally
            {
                release.TrySetResult();
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region UnreadUpload_NextUploadStillServed()

        /// <summary>
        /// The same with uploads streamed to a handler: one fills its stream window,
        /// and its handler reads none of it. The next upload on the connection is
        /// read and answered: it used to wait for good.
        /// </summary>
        [Test]
        public async Task UnreadUpload_NextUploadStillServed()
        {

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests, StreamingHandler: Uploads(release.Task));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var logSent   = await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow);

                var next      = connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, $"localhost:{server.Port}", "/ocpp",
                                                            Body: Encoding.ASCII.GetBytes(Heartbeat));

                var answered  = logSent && await Within(next);

                Assert.Multiple(() =>
                {

                    Assert.That(logSent,  Is.True,  "the log upload took a whole stream window, unread");

                    Assert.That(answered ? (Int32?) next.Result.Status                : null,  Is.EqualTo(200),                          "status of the next upload's response");
                    Assert.That(answered ? Encoding.ASCII.GetString(next.Result.Body) : null,  Is.EqualTo(Heartbeat.Length.ToString()),  "length of the next upload, as the server read it");

                });

            }
            finally
            {
                release.TrySetResult();
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region ThreeUnreadUploads_FourthRunsToItsEnd()

        /// <summary>
        /// Three uploads fill their stream windows, unread, and hold three of the
        /// connection window's four MiB between them. A fourth, read as it comes,
        /// runs to its end through the one MiB left, though it is twice as long:
        /// the server gives back the window of what it has read once the client
        /// has no more left than that. It used to give it back only once half the
        /// connection window was owed — and with three quarters of it held by
        /// unread uploads, no more than a quarter ever could be.
        /// </summary>
        [Test]
        public async Task ThreeUnreadUploads_FourthRunsToItsEnd()
        {

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests, StreamingHandler: Uploads(release.Task));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var logsSent  = true;

                for (var i = 0; i < 3 && logsSent; i++)
                    logsSent = await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow);

                var upload    = await StartUploadAsync(connection, server.Port, "/upload");
                var sent      = logsSent && await UploadAsync(upload, 2 * StreamWindow);
                var ended     = sent     && await Within(upload.CompleteRequestAsync());
                var response  = ResponseOfAsync(upload);
                var answered  = ended    && await Within(response);

                Assert.Multiple(() =>
                {

                    Assert.That(logsSent,                                 Is.True,                                  "three log uploads took a whole stream window each, unread");
                    Assert.That(sent,                                     Is.True,                                  "the fourth upload went out, all 2 MiB of it");

                    Assert.That(answered ? (Int32?) response.Result.Status : null,  Is.EqualTo(200),                           "status of the fourth upload's response");
                    Assert.That(answered ? response.Result.Body            : null,  Is.EqualTo((2 * StreamWindow).ToString()),  "length of the fourth upload, as the server read it");

                });

            }
            finally
            {
                release.TrySetResult();
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region UnreadDataAfterTheLastRead_OwedWindowGivenBack()

        /// <summary>
        /// The window the server owes for what it has read is given back as soon
        /// as the client has no more left than that, also when it is DATA nobody
        /// reads that leaves the client with so little. Two unread uploads fill
        /// their stream windows. Another upload sends 512 KiB, which its handler
        /// reads: the server owes their window now, but less than half the
        /// connection window, and less than the 1.5 MiB the client has left. Then
        /// a third unread upload takes 1 MiB of those, and a fourth the 512 KiB
        /// left. The upload that is read goes on: the server gave back what it
        /// owed when the client was left with no more than that. Nothing being
        /// read any more, it never would have: the client could send nothing, so
        /// nothing new was read, and nothing was given back.
        /// </summary>
        [Test]
        public async Task UnreadDataAfterTheLastRead_OwedWindowGivenBack()
        {

            var release   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var halfRead  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests,
                                                                   StreamingHandler: Uploads(release.Task,
                                                                                             Read: length => {
                                                                                                       if (length >= StreamWindow / 2)
                                                                                                           halfRead.TrySetResult();
                                                                                                   }));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var logsSent  = await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow) &&
                                await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow);

                var upload    = await StartUploadAsync(connection, server.Port, "/upload");
                var halfSent  = logsSent && await UploadAsync(upload, StreamWindow / 2);
                var wasRead   = halfSent && await Within(halfRead.Task);

                var moreLogs  = wasRead  && await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow) &&
                                            await UploadAsync(await StartUploadAsync(connection, server.Port, "/log"), StreamWindow / 2);

                var goesOn    = moreLogs && await UploadAsync(upload, Chunk);

                Assert.Multiple(() =>
                {

                    Assert.That(logsSent,  Is.True,  "two log uploads took a whole stream window each, unread");
                    Assert.That(halfSent,  Is.True,  "the upload that is read sent its first 512 KiB");
                    Assert.That(wasRead,   Is.True,  "and its handler read them");
                    Assert.That(moreLogs,  Is.True,  "two more log uploads took 1.5 MiB, unread, all the window the client had left");
                    Assert.That(goesOn,    Is.True,  "the upload that is read went on: the server gave back the window it owed for it");

                });

            }
            finally
            {
                release.TrySetResult();
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region OwedWindowGivenBack_OnceReadingLeavesTheClientNoMore()

        /// <summary>
        /// The rule, frame by frame, where the read that makes the window owed is
        /// what leaves the client no more than that: three unread uploads take
        /// 3 MiB of the connection window, and a fourth sends 512 KiB, which its
        /// handler reads. The server gives back those 512 KiB with one
        /// WINDOW_UPDATE, once all are read, and no more: the client then has a
        /// stream window again, the window of the unread 3 MiB still withheld.
        /// </summary>
        [Test]
        public async Task OwedWindowGivenBack_OnceReadingLeavesTheClientNoMore()
        {

            var release   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var halfRead  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var peer = await PipedH2ServerConnection.StartAsync(NoRequests,
                                                                            StreamingHandler: Uploads(release.Task,
                                                                                                      Read: length => {
                                                                                                                if (length >= StreamWindow / 2)
                                                                                                                    halfRead.TrySetResult();
                                                                                                            }));

            try
            {

                await SendUploadAsync(peer, 1, "/log",    StreamWindow);
                await SendUploadAsync(peer, 3, "/log",    StreamWindow);
                await SendUploadAsync(peer, 5, "/log",    StreamWindow);
                await SendUploadAsync(peer, 7, "/upload", StreamWindow / 2);

                await halfRead.Task.WaitAsync(StepTimeout);

                await PingUntilAsync(peer, () => ConnectionWindowUpdates(peer).Count > 1, "the server has given back the window it owes");

                Assert.Multiple(() =>
                {

                    Assert.That(ConnectionWindowUpdates(peer),
                                Is.EqualTo(new Int64[] { 4 * StreamWindow - HTTP2FlowControl.InitialWindowSize, StreamWindow / 2 }),
                                "connection-level WINDOW_UPDATEs: the raise to 4 MiB, then the 512 KiB read");

                    Assert.That(ClientConnectionWindow(peer, Sent: 3 * StreamWindow + StreamWindow / 2),
                                Is.EqualTo(StreamWindow),
                                "the client's connection window: all but the 3 MiB nobody has read");

                });

            }
            finally
            {
                release.TrySetResult();
            }

        }

        #endregion

        #region OwedWindowGivenBack_OnceUnreadDataLeavesTheClientNoMore()

        /// <summary>
        /// The rule, frame by frame, where unread DATA is what leaves the client no
        /// more than is owed: two unread uploads take 2 MiB, and another sends
        /// 512 KiB, which its handler reads. Owed now, they are not given back:
        /// the client still has 1.5 MiB. Then a third unread upload takes 1 MiB of
        /// those, and with the frame that leaves the client with the 512 KiB owed,
        /// the server gives them back.
        /// </summary>
        [Test]
        public async Task OwedWindowGivenBack_OnceUnreadDataLeavesTheClientNoMore()
        {

            var release   = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var halfRead  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var peer = await PipedH2ServerConnection.StartAsync(NoRequests,
                                                                            StreamingHandler: Uploads(release.Task,
                                                                                                      Read: length => {
                                                                                                                if (length >= StreamWindow / 2)
                                                                                                                    halfRead.TrySetResult();
                                                                                                            }));

            try
            {

                await SendUploadAsync(peer, 1, "/log",    StreamWindow);
                await SendUploadAsync(peer, 3, "/log",    StreamWindow);
                await SendUploadAsync(peer, 5, "/upload", StreamWindow / 2);

                await halfRead.Task.WaitAsync(StepTimeout);
                await peer.PingAsync();

                var whileTheClientHadMore = ConnectionWindowUpdates(peer);

                await SendUploadAsync(peer, 7, "/log",    StreamWindow);
                await peer.PingAsync();

                Assert.Multiple(() =>
                {

                    Assert.That(whileTheClientHadMore,
                                Is.EqualTo(new Int64[] { 4 * StreamWindow - HTTP2FlowControl.InitialWindowSize }),
                                "connection-level WINDOW_UPDATEs while the client had 1.5 MiB left: the raise to 4 MiB only");

                    Assert.That(ConnectionWindowUpdates(peer),
                                Is.EqualTo(new Int64[] { 4 * StreamWindow - HTTP2FlowControl.InitialWindowSize, StreamWindow / 2 }),
                                "connection-level WINDOW_UPDATEs once unread DATA has left it no more: the 512 KiB read as well");

                    Assert.That(ClientConnectionWindow(peer, Sent: 3 * StreamWindow + StreamWindow / 2),
                                Is.EqualTo(StreamWindow),
                                "the client's connection window: all but the 3 MiB nobody has read");

                });

            }
            finally
            {
                release.TrySetResult();
            }

        }

        #endregion


        #region Client_UnreadWebSocket_OCPPWebSocketStillFlows()

        /// <summary>
        /// The client, as the receiver: the server sends a whole stream window on
        /// the log channel — 16 messages of 65 532 bytes, each one unmasked frame
        /// of 64 KiB — which the application does not read. The OCPP channel next
        /// to it still echoes.
        ///
        /// The client gives back a stream's window only as the application reads
        /// it, so the log channel takes its whole stream window, and can take no
        /// more; the connection's it gives back on receipt, read or not, so an
        /// unread channel keeps no window from the others. Should it ever give
        /// back the connection's only as it is read, too, as the server does, its
        /// connection window has to be larger than a stream's for this to hold.
        /// </summary>
        [Test]
        public async Task Client_UnreadWebSocket_OCPPWebSocketStillFlows()
        {

            var logSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests,

                                         ConnectHandler: (streamId, headers, cancellationToken) => Task.FromResult(new HTTP2ConnectResult {

                                             StatusCode  = 200,

                                             RunAsync    = PathOf(headers) == "/log"

                                                               ? async (tunnel, token) => {

                                                                     var webSocket = new WebSocketConnection(tunnel, WebSocketRole.Server);

                                                                     for (var i = 0; i < StreamWindow / Chunk; i++)
                                                                         await webSocket.SendBinaryAsync(new Byte[Chunk - 4], token);

                                                                     logSent.TrySetResult();

                                                                     await Task.Delay(Timeout.Infinite, token);

                                                                 }

                                                               : async (tunnel, token) => {

                                                                     var webSocket = new WebSocketConnection(tunnel, WebSocketRole.Server);

                                                                     while (await webSocket.ReceiveAsync(token) is { } message)
                                                                         await webSocket.SendTextAsync(Encoding.UTF8.GetString(message.Payload), token);

                                                                 }

                                         }));

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var ocpp     = await connection.OpenWebSocketAsync($"localhost:{server.Port}", URIScheme.https, "/ocpp");

                // The log channel, which the application never reads.
                _            = await connection.OpenWebSocketAsync($"localhost:{server.Port}", URIScheme.https, "/log");

                var logDone  = await Within(logSent.Task);

                var sent     = logDone && await Within(ocpp.SendTextAsync(Heartbeat, CancellationToken.None));
                var echo     = ocpp.ReceiveAsync(CancellationToken.None);
                var echoed   = sent    && await Within(echo);

                Assert.Multiple(() =>
                {

                    Assert.That(logDone,  Is.True,  "the server sent a whole stream window on the log channel, which the client does not read");
                    Assert.That(sent,     Is.True,  "the OCPP message went out after it");

                    Assert.That(echoed ? Encoding.UTF8.GetString(echo.Result!.Payload) : null,
                                Is.EqualTo(Heartbeat),
                                "the OCPP message came back");

                });

            }
            finally
            {
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region Client_UnreadStreamingResponse_NextResponseStillArrives()

        /// <summary>
        /// The same with a streamed response: the server sends a whole stream
        /// window of response body, which the application does not read, and the
        /// response to the next request on the connection still arrives.
        /// </summary>
        [Test]
        public async Task Client_UnreadStreamingResponse_NextResponseStillArrives()
        {

            var logSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var server = await TestH2Server.StartAsync(NoRequests,

                                         StreamingHandler: async (request, response, cancellationToken) => {

                                             while (await request.ReadAsync(cancellationToken) is not null)
                                             { }

                                             await response.WriteHeadersAsync([(":status", "200")], cancellationToken);

                                             if (PathOf(request.Headers) == "/log")
                                             {

                                                 for (var i = 0; i < StreamWindow / Chunk; i++)
                                                     await response.WriteAsync(new Byte[Chunk], cancellationToken);

                                                 logSent.TrySetResult();

                                                 await Task.Delay(Timeout.Infinite, cancellationToken);

                                             }

                                             await response.WriteAsync(Encoding.ASCII.GetBytes(Heartbeat), cancellationToken);

                                         });

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert);

            try
            {

                var log       = await connection.StartStreamingRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/log");
                await log.CompleteRequestAsync();

                var logDone   = await Within(logSent.Task);

                var next      = connection.SendRequestAsync(HTTPMethod.GET, URIScheme.https, $"localhost:{server.Port}", "/ocpp");
                var answered  = logDone && await Within(next);

                Assert.Multiple(() =>
                {

                    Assert.That(logDone,  Is.True,  "the server sent a whole stream window of response body, which the client does not read");

                    Assert.That(answered ? (Int32?) next.Result.Status                : null,  Is.EqualTo(200),        "status of the next response");
                    Assert.That(answered ? Encoding.ASCII.GetString(next.Result.Body) : null,  Is.EqualTo(Heartbeat),  "body of the next response");

                });

            }
            finally
            {
                await Within(connection.CloseAsync());
            }

        }

        #endregion


        #region ServerGrantsItsConnectionWindow(ConnectionWindowSize)

        /// <summary>
        /// Right after its SETTINGS, the server raises the connection window from
        /// RFC 9113's 65 535 octets to the size it was given — by default to four
        /// stream windows, 4 MiB — with one WINDOW_UPDATE on stream 0, and sends
        /// none where there is nothing to raise.
        /// </summary>
        [TestCase(null)]
        [TestCase(HTTP2FlowControl.InitialWindowSize)]
        [TestCase(StreamWindow)]
        [TestCase(16 * 1024 * 1024)]
        [TestCase(Int32.MaxValue)]
        public async Task ServerGrantsItsConnectionWindow(Int32? ConnectionWindowSize)
        {

            await using var peer = await PipedH2ServerConnection.StartAsync(NoRequests, ConnectionWindowSize: ConnectionWindowSize);

            await peer.PingAsync();

            var granted = ConnectionWindowSize ?? 4 * StreamWindow;

            Assert.Multiple(() =>
            {

                Assert.That(ClientConnectionWindow(peer, Sent: 0),       Is.EqualTo(granted),                                           "the client's connection window");
                Assert.That(ConnectionWindowUpdates(peer),               Has.Count.EqualTo(granted > HTTP2FlowControl.InitialWindowSize ? 1 : 0),
                                                                                                                                         "connection-level WINDOW_UPDATEs");

            });

        }

        #endregion

        #region ClientGrantsItsConnectionWindow(ConnectionWindowSize)

        /// <summary>
        /// Before its first request, the client raises the connection window it
        /// grants the server from RFC 9113's 65 535 octets to
        /// <see cref="HTTP2ClientOptions.ConnectionWindowSize"/> — by default to
        /// four stream windows, 4 MiB — with one WINDOW_UPDATE on stream 0, and
        /// sends none where there is nothing to raise.
        /// </summary>
        [TestCase(null)]
        [TestCase(HTTP2FlowControl.InitialWindowSize)]
        [TestCase(StreamWindow)]
        [TestCase(Int32.MaxValue)]
        public async Task ClientGrantsItsConnectionWindow(Int32? ConnectionWindowSize)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection  = await transport.ConnectAsync(ConnectionWindowSize is { } size
                                                               ? new HTTP2ClientOptions { ConnectionWindowSize = size }
                                                               : new HTTP2ClientOptions());

            var request     = connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.https, "localhost", "/upload");

            var sent        = new List<HTTP2Frame>();
            await transport.NextHeadersAsync(sent);

            await request.WaitAsync(StepTimeout);
            await connection.CloseAsync();

            var increments  = sent.Where (frame => frame.Type == HTTP2FrameType.WINDOW_UPDATE && frame.StreamId == 0).
                                   Select(frame => (Int64) (BinaryPrimitives.ReadUInt32BigEndian(frame.Payload) & 0x7FFFFFFF)).
                                   ToList();

            var granted     = ConnectionWindowSize ?? 4 * StreamWindow;

            Assert.Multiple(() =>
            {

                Assert.That(HTTP2FlowControl.InitialWindowSize + increments.Sum(),  Is.EqualTo(granted),                                           "the server's connection window");
                Assert.That(increments,                                             Has.Count.EqualTo(granted > HTTP2FlowControl.InitialWindowSize ? 1 : 0),
                                                                                                                                                    "connection-level WINDOW_UPDATEs before the first request");

            });

        }

        #endregion

        #region SmallestConnectionWindow_LargeTransfersStillFlow()

        /// <summary>
        /// With RFC 9113's 65 535 octets, the smallest connection window there is,
        /// granted by both ends, 1 MiB still flows both ways: a download to the
        /// client, and an upload to the server. Each end gives back what it has
        /// taken in once half of the window it granted is owed — not half of the
        /// default's, which would never be owed, the peer being allowed no more
        /// than 65 535 octets.
        /// </summary>
        [Test]
        public async Task SmallestConnectionWindow_LargeTransfersStillFlow()
        {

            await using var server = await TestH2Server.StartAsync(

                                         (streamId, headers, body, cancellationToken)

                                             => Task.FromResult<(List<(String Name, String Value)>, Byte[]?)>(
                                                    ([(":status", "200")],
                                                     PathOf(headers) == "/download"
                                                         ? new Byte[StreamWindow]
                                                         : Encoding.ASCII.GetBytes((body?.Length ?? 0).ToString()))),

                                         ConnectionWindowSize: HTTP2FlowControl.InitialWindowSize);

            var connection = await HTTP2Client.ConnectAsync("localhost", server.Port, H2.AcceptAnyServerCert,
                                                            Options: new HTTP2ClientOptions { ConnectionWindowSize = HTTP2FlowControl.InitialWindowSize });

            try
            {

                var download    = connection.SendRequestAsync(HTTPMethod.GET,  URIScheme.https, $"localhost:{server.Port}", "/download");
                var downloaded  = await Within(download);

                var upload      = connection.SendRequestAsync(HTTPMethod.POST, URIScheme.https, $"localhost:{server.Port}", "/upload",
                                                              Body: new Byte[StreamWindow]);
                var uploaded    = await Within(upload);

                Assert.Multiple(() =>
                {

                    Assert.That(downloaded ? (Int32?) download.Result.Body.Length         : null,  Is.EqualTo(StreamWindow),             "length of the download, as the client received it");
                    Assert.That(uploaded   ? Encoding.ASCII.GetString(upload.Result.Body) : null,  Is.EqualTo(StreamWindow.ToString()),  "length of the upload, as the server received it");

                });

            }
            finally
            {
                await Within(connection.CloseAsync());
            }

        }

        #endregion

        #region ConnectionWindowBelowRFC9113sInitialWindow_Refused(ConnectionWindowSize)

        /// <summary>
        /// A connection window starts at RFC 9113's 65 535 octets and can be
        /// raised with a WINDOW_UPDATE, but not lowered: a smaller size is refused
        /// where it is given, by the server, a server connection and the client's
        /// options alike.
        /// </summary>
        [TestCase(HTTP2FlowControl.InitialWindowSize - 1)]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(Int32.MinValue)]
        public void ConnectionWindowBelowRFC9113sInitialWindow_Refused(Int32 ConnectionWindowSize)
        {

            Assert.Multiple(() =>
            {

                Assert.That(() => new HTTP2Server(System.Net.IPAddress.Loopback, 0, null, NoRequests, Cleartext: true, ConnectionWindowSize: ConnectionWindowSize),
                            Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("ConnectionWindowSize"),
                            "the server");

                Assert.That(() => new HTTP2Connection(Stream.Null, NoRequests, ConnectionWindowSize: ConnectionWindowSize),
                            Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("ConnectionWindowSize"),
                            "a server connection");

                Assert.That(() => new HTTP2ClientOptions { ConnectionWindowSize = ConnectionWindowSize },
                            Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("ConnectionWindowSize"),
                            "the client's options");

            });

        }

        #endregion

    }

}
