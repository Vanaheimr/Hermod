/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{
    using System.Buffers.Binary;
    using System.Net.Security;
    using System.Text;
    using System.Threading.Channels;

    /// <summary>
    /// Handles a single HTTP/2 connection over an SslStream.
    ///
    /// Lifecycle:
    ///   1. Read and validate the client connection preface (magic + SETTINGS)
    ///   2. Send our own SETTINGS and ACK the client's
    ///   3. Enter the main frame loop: read frames, dispatch by type
    ///   4. For complete requests, invoke the request handler and send the response
    ///   5. Handle errors with RST_STREAM (stream) or GOAWAY (connection)
    /// </summary>
    public sealed class HTTP2Connection
    {

        #region Client Connection Preface (RFC 9113, Section 3.4)

        /// <summary>
        /// "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"  (24 bytes)
        /// The client must send this before any HTTP/2 frames.
        /// </summary>
        private static readonly byte[] ConnectionPreface = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        #endregion


        private readonly Stream               transportStream;
        private readonly HTTP2RequestHandler  requestHandler;
        private readonly HTTP2ConnectHandler? connectHandler;
        private readonly HTTP2StreamingHandler? streamingHandler;
        private readonly HTTP2Settings        localSettings  = new();
        private readonly HTTP2Settings        remoteSettings = new();
        private readonly HTTP2StreamManager   streamManager  = new();
        private readonly HPACKDecoder         hpackDecoder   = new();
        private readonly HPACKEncoder         hpackEncoder   = new();
        private readonly SemaphoreSlim        writeLock      = new(1, 1);

        /// <summary>
        /// Linked to the external token; cancelled when the connection ends so that
        /// in-flight request handler / response tasks are aborted.
        /// </summary>
        private readonly CancellationTokenSource  connectionCts;
        private readonly CancellationToken        cancellationToken;

        private bool         goawaySent;

        /// <summary>
        /// Connection-level receive window we raise to at startup (above RFC 9113's
        /// 65535 default) via an initial WINDOW_UPDATE, so large multiplexed
        /// transfers aren't throttled by the small default connection window.
        /// </summary>
        private const long   ConnectionRecvWindowTarget = 1024 * 1024;   // 1 MiB

        /// <summary>
        /// Bytes consumed connection-wide since our last connection-level
        /// WINDOW_UPDATE — accumulated so we replenish in batches (see
        /// <see cref="ReplenishReceiveWindowsAsync"/>).
        /// </summary>
        private long         connectionPendingRecvUpdate;

        /// <summary>
        /// Guards the send-side flow control windows (stream + connection), since
        /// the writer loop decrements them while the read loop increments them.
        /// </summary>
        private readonly object  flowLock  = new();

        /// <summary>
        /// Guards the RECEIVE-side flow control windows (stream + connection) and
        /// their pending-replenish accumulators. Previously these were touched only
        /// from the single frame read loop and needed no lock; consumption-driven
        /// backpressure now also replenishes them from streaming/tunnel handler
        /// tasks (when the application actually reads a body chunk), so the read
        /// loop's decrement and the handlers' increments can race. Never held
        /// across an <c>await</c> — the WINDOW_UPDATE send happens outside it.
        /// </summary>
        private readonly object  recvLock  = new();

        /// <summary>
        /// Upper bound on a BUFFERED request body (the default handler seam, which
        /// hands the whole body to the app at END_STREAM). Unlike the streaming and
        /// tunnel paths — where the receive window itself bounds memory because the
        /// window is only replenished as the handler consumes chunks — the buffered
        /// path has no incremental consumer to drive backpressure, so an unbounded
        /// body would grow unbounded in memory. A body exceeding this cap resets the
        /// stream (RST_STREAM/ENHANCE_YOUR_CALM); the connection stays usable.
        /// </summary>
        private readonly long    maxRequestBodySize;

        /// <summary>Default <see cref="maxRequestBodySize"/>: 16 MiB.</summary>
        private const long   DefaultMaxRequestBodySize = 16 * 1024 * 1024;

        /// <summary>
        /// Completed (and replaced) whenever the writer loop should re-scan for
        /// something to send: a send window grew, a stream was reset, new data
        /// was enqueued, or a stream's priority changed. See SignalWriterWakeup
        /// and DataWriterLoopAsync.
        /// </summary>
        private TaskCompletionSource  windowChanged  = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Tracks which stream is currently receiving CONTINUATION frames.
        /// Only one stream at a time can be in the "headers pending" state.
        /// </summary>
        private UInt32?      continuationStreamId;

        /// <summary>
        /// Number of CONTINUATION frames received for the header block currently
        /// being accumulated. Reset when a new HEADERS frame starts a block. A
        /// flood of (even empty) CONTINUATION frames that never sets END_HEADERS
        /// is the CVE-2024-27316 attack class.
        /// </summary>
        private int         continuationFrameCount;

        /// <summary>
        /// Control frames that cost us work but make no request progress (non-ACK
        /// PING and SETTINGS). Reset whenever a HEADERS/DATA frame arrives. A
        /// sustained flood with no real requests is answered with ENHANCE_YOUR_CALM.
        /// </summary>
        private int         unproductiveFrames;

        /// <summary>Upper bound on CONTINUATION frames per single header block.</summary>
        private const int   MaxContinuationFrames  = 64;

        /// <summary>Upper bound on consecutive control frames without request progress.</summary>
        private const int   MaxUnproductiveFrames  = 1000;

        /// <summary>
        /// Streams the peer has opened (a HEADERS frame starting a new,
        /// non-trailers stream) over this connection's lifetime.
        /// </summary>
        private int         streamsOpenedByPeer;

        /// <summary>
        /// Streams the peer has torn down via RST_STREAM. RFC 9113 doesn't forbid
        /// cancelling requests, but a peer that opens streams only to immediately
        /// reset them, over and over, is the "HTTP/2 Rapid Reset" attack
        /// (CVE-2023-44487, disclosed October 2023): each cycle still costs a
        /// stream slot, HPACK decode work, and a dispatched handler task before
        /// the reset lands — and never counts against MAX_CONCURRENT_STREAMS
        /// (a Closed stream doesn't count in GetOrCreateStream's openCount).
        /// Unlike the CONTINUATION/PING/SETTINGS floods above, the existing
        /// "unproductive frames" counter can't catch this, since HEADERS *is*
        /// real request progress each time — the abuse signal is specifically the
        /// ratio of streams opened to streams reset by the peer.
        /// </summary>
        private int         peerResetStreams;

        /// <summary>
        /// Start checking the reset ratio only once this many streams have been
        /// opened — too small a sample makes the ratio meaningless (a client that
        /// opens 2 streams and cancels 1 isn't an attack).
        /// </summary>
        private const int    MinStreamsForResetRatioCheck = 20;

        /// <summary>
        /// Fraction of opened streams that may be peer-reset before it's treated
        /// as abusive. Deliberately not time-windowed — this is a ratio over the
        /// connection's whole lifetime, which is simple and effective for this
        /// server's threat model, but a very long-lived connection with a
        /// naturally high organic cancellation rate could in principle still trip
        /// it eventually; a production server would want a sliding time window.
        /// </summary>
        private const double MaxPeerResetRatio       = 0.5;

        /// <summary>
        /// True once we've proactively told the peer (via GOAWAY) that this
        /// connection won't accept further new streams because it's nearing the
        /// 31-bit stream-ID space (RFC 9113, Section 5.1.1). Set once so we only
        /// send that GOAWAY a single time.
        /// </summary>
        private bool         streamIdExhaustionGoAwaySent;


        /// <summary>
        /// The client's validated mTLS certificate, if the server required one and
        /// the peer presented it — surfaced to request handlers as a synthetic
        /// <c>x-client-cert-subject</c> header. Null for ordinary (non-mTLS)
        /// connections.
        /// </summary>
        private readonly System.Security.Cryptography.X509Certificates.X509Certificate2? clientCertificate;

        /// <summary>Slowloris/idle timeouts for this connection.</summary>
        private readonly HTTP2Timeouts  timeouts;

        /// <summary>
        /// Completed when the peer ACKs our SETTINGS. Enforces RFC 9113 §6.5.3
        /// (SETTINGS_TIMEOUT) via <see cref="EnforceSettingsAckTimeoutAsync"/>.
        /// </summary>
        private readonly TaskCompletionSource  settingsAckReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <param name="TransportStream">
        /// The byte transport for this connection: an <see cref="SslStream"/> for
        /// HTTP/2-over-TLS ("h2"), or a raw <see cref="System.Net.Sockets.NetworkStream"/>
        /// for cleartext HTTP/2 ("h2c", prior-knowledge — RFC 9113 §3.3). The
        /// connection only ever uses the <see cref="Stream"/> base API, so it is
        /// oblivious to which transport it runs on.
        /// </param>
        public HTTP2Connection(
            Stream               TransportStream,
            HTTP2RequestHandler  RequestHandler,
            HTTP2ConnectHandler? ConnectHandler     = null,
            CancellationToken    CancellationToken  = default,
            System.Security.Cryptography.X509Certificates.X509Certificate2? ClientCertificate = null,
            HTTP2Timeouts?       Timeouts           = null,
            HTTP2StreamingHandler? StreamingHandler = null,
            long                 MaxRequestBodySize = DefaultMaxRequestBodySize)
        {
            this.transportStream    = TransportStream;
            this.requestHandler     = RequestHandler;
            this.connectHandler     = ConnectHandler;
            this.streamingHandler   = StreamingHandler;
            this.clientCertificate  = ClientCertificate;
            this.maxRequestBodySize = MaxRequestBodySize;
            this.timeouts          = Timeouts ?? HTTP2Timeouts.Default;
            this.connectionCts     = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            this.cancellationToken = connectionCts.Token;
        }


        #region Main Connection Loop

        /// <summary>
        /// Run the HTTP/2 connection. This is the entry point after TLS+ALPN negotiation.
        /// </summary>
        public async Task RunAsync()
        {

            Task? writerTask = null;

            try
            {

                // 1. Read and validate the client connection preface,
                //    send our server preface (SETTINGS) and ACK the client's SETTINGS
                await ReadConnectionPrefaceAsync();

                // 2. Start the priority-aware DATA writer loop — runs concurrently
                //    with the frame read loop for the rest of the connection's
                //    lifetime (see DataWriterLoopAsync).
                writerTask = DataWriterLoopAsync();

                // 3. Enter the frame read loop
                await FrameLoopAsync();

            }
            catch (HTTP2ConnectionException ex)
            {
                Console.Error.WriteLine($"[HTTP/2] Connection error: {ex.ErrorCode} — {ex.Message}");
                await SendGoAwayAsync(ex.ErrorCode, ex.Message);
            }
            catch (IOException)
            {
                // Peer disconnected — normal for connection close
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HTTP/2] Unexpected error: {ex}");
                await SendGoAwayAsync(HTTP2ErrorCode.INTERNAL_ERROR, "Internal server error");
            }
            finally
            {
                // Abort any in-flight request handler / response tasks
                connectionCts.Cancel();

                if (writerTask is not null)
                {
                    // DataWriterLoopAsync only ever throws OperationCanceledException
                    // by design (its own try/catch swallows that internally) — but if
                    // something unexpected ever slips through, log it instead of
                    // silently discarding it, consistent with every other error path
                    // in this class.
                    try { await writerTask; }
                    catch (Exception ex) { Console.Error.WriteLine($"[HTTP/2] Writer loop error: {ex}"); }
                }
            }

        }

        #endregion


        #region Connection Preface

        /// <summary>
        /// Read the 24-byte magic string, then read the client's initial SETTINGS frame.
        /// </summary>
        private async Task ReadConnectionPrefaceAsync()
        {

            var prefaceBuffer = new byte[ConnectionPreface.Length];
            await ReadExactAsync(prefaceBuffer, timeouts.Preface, HTTP2ErrorCode.ENHANCE_YOUR_CALM, "reading the connection preface");

            if (!prefaceBuffer.AsSpan().SequenceEqual(ConnectionPreface))
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "Invalid client connection preface");

            // The server preface MUST be a (non-ACK) SETTINGS frame and MUST be
            // the first frame we send (RFC 9113, Section 3.4) — strict clients
            // (e.g. .NET's HttpClient) reject a connection whose first server
            // frame is the SETTINGS ACK.
            //
            // ENABLE_CONNECT_PROTOCOL (RFC 8441, Section 3) is only advertised
            // when a connect handler is actually registered — telling a peer
            // extended CONNECT is supported and then rejecting every attempt
            // would be a pointless round trip.
            var settings = new List<(HTTP2SettingsParameter Id, UInt32 Value)>
            {
                (HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS,   localSettings.MaxConcurrentStreams),
                (HTTP2SettingsParameter.INITIAL_WINDOW_SIZE,      localSettings.InitialWindowSize),
                (HTTP2SettingsParameter.MAX_FRAME_SIZE,           localSettings.MaxFrameSize),
                (HTTP2SettingsParameter.ENABLE_PUSH,              0),   // We don't do server push

                // RFC 9218, Section 3: unconditional, since we already ignore
                // RFC 7540's stream-dependency/weight priority signaling
                // entirely (the PRIORITY frame, and HEADERS' PRIORITY flag) —
                // this tells the peer to rely on the "priority" header field and
                // PRIORITY_UPDATE instead.
                (HTTP2SettingsParameter.NO_RFC7540_PRIORITIES,    1)
            };

            if (connectHandler is not null)
                settings.Add((HTTP2SettingsParameter.ENABLE_CONNECT_PROTOCOL, 1));

            await SendFrameAsync(
                HTTP2Frame.CreateSettings(settings.ToArray())
            );

            // New streams' receive windows start at the INITIAL_WINDOW_SIZE we just
            // advertised (keep the stream manager in sync so the accounting matches
            // what the peer thinks it may send).
            streamManager.LocalInitialWindowSize = localSettings.InitialWindowSize;

            // SETTINGS_INITIAL_WINDOW_SIZE only governs stream windows; the
            // connection window starts at the fixed 65535 default. Raise it with an
            // initial WINDOW_UPDATE so large multiplexed transfers aren't throttled.
            var connectionBump = ConnectionRecvWindowTarget - streamManager.ConnectionRecvWindow;
            if (connectionBump > 0)
            {
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, (UInt32) connectionBump));
                streamManager.ConnectionRecvWindow += connectionBump;
            }

            // We've sent our SETTINGS — start the clock on the peer ACKing them
            // (RFC 9113 §6.5.3). Runs concurrently with the frame loop.
            _ = EnforceSettingsAckTimeoutAsync();

            // The first frame from the client MUST be a SETTINGS frame
            var frame = await ReadFrameAsync(timeouts.Preface, HTTP2ErrorCode.ENHANCE_YOUR_CALM);

            if (frame.Type != HTTP2FrameType.SETTINGS || frame.IsAck)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "First frame must be a non-ACK SETTINGS frame");

            ApplyRemoteSettings(frame);
            await SendFrameAsync(HTTP2Frame.CreateSettingsAck());

        }

        /// <summary>
        /// RFC 9113 §6.5.3: if the peer never acknowledges our SETTINGS within the
        /// timeout, close the connection with SETTINGS_TIMEOUT. Deliberately a
        /// write-only GOAWAY (no inbound drain): the frame read loop is still the
        /// sole reader of the SslStream, and SslStream forbids two concurrent reads
        /// — draining here would race it. Cancelling unblocks the read loop.
        /// </summary>
        private async Task EnforceSettingsAckTimeoutAsync()
        {

            try
            {
                await settingsAckReceived.Task.WaitAsync(timeouts.SettingsAck, timeouts.TimeProvider, cancellationToken);
            }
            catch (TimeoutException)
            {

                if (!goawaySent)
                {
                    goawaySent = true;
                    try
                    {
                        await SendFrameAsync(HTTP2Frame.CreateGoAway(
                            streamManager.LastPeerStreamId, HTTP2ErrorCode.SETTINGS_TIMEOUT,
                            "SETTINGS ACK not received in time"));
                    }
                    catch { /* best-effort — connection may already be gone */ }
                }

                connectionCts.Cancel();

            }
            catch (OperationCanceledException)
            {
                // Connection ended before the deadline — nothing to do.
            }

        }

        #endregion


        #region Frame I/O

        /// <summary>
        /// Read a complete HTTP/2 frame (9-byte header + payload) from the stream.
        /// </summary>
        private async Task<HTTP2Frame> ReadFrameAsync(TimeSpan HeaderTimeout, HTTP2ErrorCode HeaderTimeoutCode)
        {

            var headerBuf = new byte[HTTP2Frame.HeaderSize];

            // The header read is where we wait for the *next* frame to begin, so its
            // timeout is the caller's choice (generous idle vs. tight in-progress).
            await ReadExactAsync(headerBuf, HeaderTimeout, HeaderTimeoutCode, "waiting for the next frame");

            var frame = HTTP2Frame.ParseHeader(headerBuf);

            // Validate frame size
            if (frame.Length > localSettings.MaxFrameSize)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   $"Frame payload length {frame.Length} exceeds MAX_FRAME_SIZE {localSettings.MaxFrameSize}");

            if (frame.Length > 0)
            {
                // Once a frame header is on the wire, its payload must follow
                // promptly — a trickled payload is a Slowloris vector.
                frame.Payload = new byte[frame.Length];
                await ReadExactAsync(frame.Payload, timeouts.InProgress, HTTP2ErrorCode.ENHANCE_YOUR_CALM, "reading a frame payload");
            }

            return frame;

        }

        /// <summary>
        /// Read exactly N bytes from the SslStream (handles partial reads), bounded
        /// by a single whole-operation deadline. A trickle that never completes the
        /// buffer within <paramref name="Timeout"/> aborts the connection with
        /// <paramref name="TimeoutCode"/> — the deadline spans the whole read, so it
        /// can't be reset by dribbling one byte at a time.
        /// </summary>
        private async Task ReadExactAsync(byte[] Buffer, TimeSpan Timeout, HTTP2ErrorCode TimeoutCode, string What)
        {

            using var timeoutCts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var timeoutTimer = timeouts.TimeProvider.CreateTimer(
                                         static state => ((CancellationTokenSource) state!).Cancel(),
                                         timeoutCts,
                                         Timeout,
                                         System.Threading.Timeout.InfiniteTimeSpan);

            var offset = 0;

            while (offset < Buffer.Length)
            {

                int read;

                try
                {
                    read = await transportStream.ReadAsync(
                               Buffer.AsMemory(offset, Buffer.Length - offset),
                               timeoutCts.Token
                           );
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The per-operation deadline fired (not a connection-wide cancel).
                    throw new HTTP2ConnectionException(TimeoutCode,
                        $"Timed out after {Timeout.TotalSeconds:0.#}s {What}");
                }

                if (read == 0)
                    throw new IOException("Connection closed by peer");

                offset += read;

            }

        }

        /// <summary>
        /// Send a frame to the peer. Thread-safe via write lock.
        /// </summary>
        private async Task SendFrameAsync(HTTP2Frame Frame)
        {

            var bytes = Frame.Serialize();

            await writeLock.WaitAsync(cancellationToken);

            try
            {
                await transportStream.WriteAsync(bytes, cancellationToken);
                await transportStream.FlushAsync(cancellationToken);
            }
            finally
            {
                writeLock.Release();
            }

        }

        /// <summary>
        /// Send a sequence of frames while holding the write lock for the whole
        /// sequence. Required for HEADERS + CONTINUATION: a header block must be
        /// contiguous on the connection (RFC 9113, Section 4.3) — frames of other
        /// streams must not be interleaved.
        /// </summary>
        private async Task SendFramesAsync(IReadOnlyList<HTTP2Frame> Frames)
        {

            await writeLock.WaitAsync(cancellationToken);

            try
            {
                foreach (var frame in Frames)
                    await transportStream.WriteAsync(frame.Serialize(), cancellationToken);

                await transportStream.FlushAsync(cancellationToken);
            }
            finally
            {
                writeLock.Release();
            }

        }

        #endregion


        #region Frame Dispatch Loop

        /// <summary>
        /// The main loop that reads and dispatches frames until the connection closes.
        /// </summary>
        private async Task FrameLoopAsync()
        {

            while (!cancellationToken.IsCancellationRequested && !goawaySent)
            {

                // Between frames we wait with the generous idle timeout — unless a
                // header block is mid-flight (CONTINUATION pending), in which case
                // the peer must finish it promptly (a HEADERS without END_HEADERS
                // then silence is a Slowloris vector).
                var (headerTimeout, headerTimeoutCode) = continuationStreamId.HasValue
                    ? (timeouts.InProgress, HTTP2ErrorCode.ENHANCE_YOUR_CALM)
                    : (timeouts.Idle,       HTTP2ErrorCode.NO_ERROR);

                var frame = await ReadFrameAsync(headerTimeout, headerTimeoutCode);

                //Console.WriteLine($"[HTTP/2] Received: {frame}");

                // If we're in the middle of a CONTINUATION sequence, only CONTINUATION is allowed
                if (continuationStreamId.HasValue && frame.Type != HTTP2FrameType.CONTINUATION)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                        "Expected CONTINUATION frame");

                try
                {

                    switch (frame.Type)
                    {
                        case HTTP2FrameType.SETTINGS:       await HandleSettingsAsync(frame);      break;
                        case HTTP2FrameType.HEADERS:         await HandleHeaders(frame);            break;
                        case HTTP2FrameType.CONTINUATION:    await HandleContinuation(frame);       break;
                        case HTTP2FrameType.DATA:            await HandleDataAsync(frame);          break;
                        case HTTP2FrameType.WINDOW_UPDATE:   HandleWindowUpdate(frame);             break;
                        case HTTP2FrameType.PING:            await HandlePingAsync(frame);          break;
                        case HTTP2FrameType.RST_STREAM:      HandleRstStream(frame);                break;
                        case HTTP2FrameType.GOAWAY:          HandleGoAway(frame);                   break;
                        case HTTP2FrameType.PRIORITY:        HandlePriority(frame);                 break;
                        case HTTP2FrameType.PRIORITY_UPDATE: HandlePriorityUpdate(frame);           break;
                        case HTTP2FrameType.PUSH_PROMISE:
                            // RFC 9113, Section 8.4 / 6.6: only a server sends
                            // PUSH_PROMISE. A client (our peer) sending one to us is
                            // always a connection error — and we advertise
                            // ENABLE_PUSH=0 besides.
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                "PUSH_PROMISE received by server (clients must not push)");
                        default:
                            // Unknown frame types MUST be ignored (RFC 9113, Section 4.1)
                            break;
                    }

                }
                catch (HTTP2StreamException ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Stream {ex.StreamId} error: {ex.ErrorCode} — {ex.Message}");
                    await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode));

                    var stream = streamManager.TryGetStream(ex.StreamId);
                    stream?.Reset();
                }

            }

        }

        #endregion


        #region SETTINGS (Section 6.5)

        private async Task HandleSettingsAsync(HTTP2Frame Frame)
        {

            if (Frame.StreamId != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "SETTINGS frame must be on stream 0");

            if (Frame.IsAck)
            {
                // Acknowledgement of our SETTINGS.
                if (Frame.Length != 0)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                        "SETTINGS ACK must have empty payload");

                // Satisfies the SETTINGS_TIMEOUT deadline (RFC 9113 §6.5.3).
                settingsAckReceived.TrySetResult();
                return;
            }

            if (Frame.Length % 6 != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   "SETTINGS payload must be a multiple of 6 bytes");

            // A SETTINGS change forces us to ACK; a flood of them makes no request
            // progress (empty-frame flood, RFC 9113 §10.5).
            CountUnproductiveFrame();

            ApplyRemoteSettings(Frame);

            await SendFrameAsync(HTTP2Frame.CreateSettingsAck());

        }

        /// <summary>
        /// Register a control frame that makes us do work but yields no request
        /// progress. A sustained flood of these (empty PING/SETTINGS floods) is
        /// abusive; once the threshold is crossed we tear the connection down with
        /// ENHANCE_YOUR_CALM (RFC 9113 §10.5).
        /// </summary>
        private void CountUnproductiveFrame()
        {
            if (++unproductiveFrames > MaxUnproductiveFrames)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    "Too many control frames without request progress");
        }

        /// <summary>
        /// Parse and apply the peer's SETTINGS parameters.
        /// </summary>
        private void ApplyRemoteSettings(HTTP2Frame Frame)
        {

            var payload = Frame.Payload.AsSpan();

            for (var i = 0; i < payload.Length; i += 6)
            {

                var id    = (HTTP2SettingsParameter) BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(i, 2));
                var value = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(i + 2, 4));

                switch (id)
                {

                    case HTTP2SettingsParameter.HEADER_TABLE_SIZE:
                        remoteSettings.HeaderTableSize = value;
                        // The peer's decoder will keep a dynamic table of at most
                        // this size — bound our encoder's table to match (RFC 7541,
                        // Section 6.3).
                        hpackEncoder.SetMaxDynamicTableSize((int) value);
                        break;

                    case HTTP2SettingsParameter.ENABLE_PUSH:
                        if (value > 1)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                "ENABLE_PUSH must be 0 or 1");
                        remoteSettings.EnablePush = value == 1;
                        break;

                    case HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS:
                        remoteSettings.MaxConcurrentStreams = value;
                        break;

                    case HTTP2SettingsParameter.INITIAL_WINDOW_SIZE:
                        if (value > 0x7FFFFFFF)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR,
                                "INITIAL_WINDOW_SIZE must not exceed 2^31-1");

                        lock (flowLock)
                        {
                            var delta = (Int64) value - (Int64) remoteSettings.InitialWindowSize;
                            remoteSettings.InitialWindowSize = value;

                            // Adjust existing streams (RFC 9113, Section 6.9.2)
                            streamManager.PeerInitialWindowSize = value;
                            streamManager.AdjustAllStreamWindows(delta);
                        }

                        SignalWriterWakeup();
                        break;

                    case HTTP2SettingsParameter.MAX_FRAME_SIZE:
                        if (value < 16384 || value > 16777215)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                "MAX_FRAME_SIZE must be between 2^14 and 2^24-1");
                        remoteSettings.MaxFrameSize = value;
                        break;

                    case HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE:
                        remoteSettings.MaxHeaderListSize = value;
                        break;

                    case HTTP2SettingsParameter.NO_RFC7540_PRIORITIES:
                        // Recognized (RFC 9218, Section 3), but nothing to act on:
                        // we never emit RFC 7540 priority signals ourselves in
                        // either direction, so whether the peer honors them is
                        // moot either way.
                        break;

                    default:
                        // Unknown settings MUST be ignored (RFC 9113, Section 6.5.2)
                        break;

                }

            }

        }

        #endregion


        #region HEADERS (Section 6.2)

        private async Task HandleHeaders(HTTP2Frame Frame)
        {

            if (Frame.StreamId == 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "HEADERS frame must not be on stream 0");

            // Real request traffic — reset the control-frame flood counter.
            unproductiveFrames = 0;

            var existingStream = streamManager.TryGetStream(Frame.StreamId);
            var isTrailers     = existingStream is not null;

            HTTP2Stream stream;

            if (isTrailers)
            {

                stream = existingStream!;

                // Trailers (RFC 9113, Section 8.1) are only legal while we're
                // still expecting body/trailer data on this stream, i.e. the
                // original HEADERS did not set END_STREAM.
                if (stream.State is not (HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal))
                {
                    // RFC 9113, Section 5.1: a HEADERS frame arriving after the
                    // peer sent END_STREAM (clean close) is a *connection* error of
                    // type STREAM_CLOSED; after an RST_STREAM close it's only a
                    // *stream* error (a peer racing frames past a reset it hasn't
                    // seen yet).
                    if (stream.WasReset)
                        throw new HTTP2StreamException(HTTP2ErrorCode.STREAM_CLOSED, Frame.StreamId,
                            $"HEADERS received after RST_STREAM on stream {Frame.StreamId}");

                    throw new HTTP2ConnectionException(HTTP2ErrorCode.STREAM_CLOSED,
                        $"HEADERS received on closed stream {Frame.StreamId}");
                }

                // Trailers are the last thing on the stream — they MUST end it.
                if (!Frame.EndStream)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                        "Trailing HEADERS frame must set END_STREAM");

            }
            else
            {

                // RFC 9113, Section 5.1.1: streams initiated by a client MUST use
                // odd-numbered stream identifiers (this server never pushes, so no
                // even-numbered stream is ever legitimately opened by the peer).
                if (Frame.StreamId % 2 == 0)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                        $"Client used an even (server-only) stream ID {Frame.StreamId}");

                // RFC 9113, Section 5.1.1: stream identifiers are a 31-bit field
                // and can never be reused. Once we're close to the top of that
                // space, proactively tell the peer (once) to stop opening new
                // streams here and migrate to a fresh connection, instead of
                // running this one all the way to the hard wall — where the next
                // stream ID would fail the "must be greater than the last one"
                // check below anyway, but as an abrupt connection-level
                // PROTOCOL_ERROR instead of a clean GOAWAY.
                if (streamManager.IsNearStreamIdExhaustion)
                {
                    if (!streamIdExhaustionGoAwaySent)
                    {
                        streamIdExhaustionGoAwaySent = true;
                        _ = InitiateGracefulShutdownAsync();
                    }

                    throw new HTTP2StreamException(HTTP2ErrorCode.REFUSED_STREAM, Frame.StreamId,
                        "Connection is near its stream ID limit; open a new connection");
                }

                // Sweep out streams that finished since the last new request, so
                // the dictionary doesn't grow unboundedly over a long-lived
                // connection. Safe here specifically: the streams dictionary is
                // only ever touched from this read loop — response tasks run in
                // the background (StartRequestHandler) and only ever mutate a
                // stream's own State/window fields, never the dictionary itself.
                streamManager.PruneClosedStreams();

                stream = streamManager.GetOrCreateStream(Frame.StreamId);
                stream.Open();
                streamsOpenedByPeer++;

            }

            // Handle padding
            var payload    = Frame.Payload.AsSpan();
            var headerData = StripPadding(Frame, payload);

            // Handle priority fields (deprecated, but must still parse for compatibility)
            if (Frame.HasPriority)
            {
                if (headerData.Length < 5)
                    throw new HTTP2StreamException(HTTP2ErrorCode.FRAME_SIZE_ERROR, Frame.StreamId,
                        "HEADERS with PRIORITY flag has insufficient data");

                // 4 bytes stream dependency (top bit = exclusive flag) + 1 byte weight.
                // RFC 9113/7540, Section 5.3.1: a stream cannot depend on itself.
                var streamDependency = BinaryPrimitives.ReadUInt32BigEndian(headerData) & 0x7FFFFFFFu;
                if (streamDependency == Frame.StreamId)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                        "HEADERS priority: a stream cannot depend on itself");

                headerData = headerData[5..];
            }

            // Start a fresh header block
            stream.HeaderBuffer      = new MemoryStream();
            stream.EndStreamPending  = Frame.EndStream;
            continuationFrameCount   = 0;
            stream.HeaderBuffer.Write(headerData);
            EnforceHeaderBufferLimit(stream);

            if (Frame.EndHeaders)
            {
                await CompleteHeaders(stream, Frame.EndStream);
            }
            else
            {
                // More CONTINUATION frames are expected
                continuationStreamId = Frame.StreamId;
            }

        }

        #endregion


        #region CONTINUATION (Section 6.10)

        private async Task HandleContinuation(HTTP2Frame Frame)
        {

            if (Frame.StreamId == 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "CONTINUATION frame must not be on stream 0");

            if (continuationStreamId != Frame.StreamId)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                    $"CONTINUATION stream ID {Frame.StreamId} doesn't match expected {continuationStreamId}");

            var stream = streamManager.TryGetStream(Frame.StreamId)
                ?? throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                       $"CONTINUATION for unknown stream {Frame.StreamId}");

            // Bound the number of fragments per header block: a peer that keeps
            // sending CONTINUATION frames (even empty ones) without END_HEADERS
            // would otherwise pin the connection and grow the buffer unbounded
            // (CVE-2024-27316 class).
            if (++continuationFrameCount > MaxContinuationFrames)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Too many CONTINUATION frames ({MaxContinuationFrames} max) for one header block");

            stream.HeaderBuffer!.Write(Frame.Payload);
            EnforceHeaderBufferLimit(stream);

            if (Frame.EndHeaders)
            {
                continuationStreamId = null;
                await CompleteHeaders(stream, stream.EndStreamPending);
            }

        }

        /// <summary>
        /// Enforce the advertised MAX_HEADER_LIST_SIZE against the header block we
        /// are still accumulating, so a peer cannot exhaust memory by never sending
        /// END_HEADERS (CONTINUATION flood, CVE-2024-27316). We compare against the
        /// compressed buffered size; for a peer that respects the advertised limit
        /// on the uncompressed list, the compressed form never exceeds it.
        /// </summary>
        private void EnforceHeaderBufferLimit(HTTP2Stream Stream)
        {
            if (Stream.HeaderBuffer!.Length > localSettings.MaxHeaderListSize)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Header block exceeds MAX_HEADER_LIST_SIZE ({localSettings.MaxHeaderListSize} bytes)");
        }

        /// <summary>
        /// Decode the complete header block and, if END_STREAM was set, dispatch the request.
        /// </summary>
        private async Task CompleteHeaders(HTTP2Stream Stream, bool EndStream)
        {

            var headerBlock    = Stream.HeaderBuffer!.ToArray();
            Stream.HeaderBuffer.Dispose();
            Stream.HeaderBuffer = null;

            // The HPACK dynamic table is shared connection-wide state, so the block
            // must always be fully decoded — even if the request turns out to be
            // malformed — or the peer's encoder and our decoder fall out of sync
            // for every subsequent header block on this connection.
            var decoded = hpackDecoder.DecodeHeaderBlock(headerBlock);

            // A second header block on the same stream is trailers, not a fresh
            // request (RFC 9113, Section 8.1) — recognized by RequestHeaders
            // already being populated from the first block. A CONNECT tunnel has
            // no defined trailers concept (there's no "body" to trail, just an
            // open-ended byte stream); if a peer sends a second header block on
            // one anyway, it's still just validated and stored, not re-dispatched
            // — the branch below only ever fires for the FIRST header block.
            var isInitialHeaders = Stream.RequestHeaders is null;

            if (isInitialHeaders)
            {

                ValidateRequestHeaders(Stream.StreamId, decoded);

                // RFC 9113, Section 8.2.3: clients may split the cookie header into
                // multiple field lines for better HPACK compression ("crumbling");
                // before handing the request to a generic HTTP application they
                // MUST be reassembled into a single field, joined with "; ".
                CombineCookieFields(decoded);

                Stream.RequestHeaders = decoded;

                // RFC 9218, Section 4: an ordinary (non-pseudo) header field, so
                // it already passed the regular field-level checks above —
                // parsed leniently, same as PRIORITY_UPDATE (see ParsePriority).
                var priorityEntry = decoded.FirstOrDefault(h => h.Name == "priority");
                if (priorityEntry.Name is not null)
                    Stream.Priority = ParsePriority(priorityEntry.Value);

                if (decoded.First(h => h.Name == ":method").Value == "CONNECT")
                    Stream.IsConnectTunnel = true;

                // RFC 9113, Section 8.1.2.6: a declared content-length must later
                // equal the summed DATA payload length. Parse it now; a
                // syntactically invalid or self-conflicting value is itself a
                // malformed request (a CONNECT tunnel has no such body semantics).
                if (!Stream.IsConnectTunnel)
                    Stream.ExpectedContentLength = ParseContentLength(Stream.StreamId, decoded);

            }
            else
            {
                ValidateTrailerHeaders(Stream.StreamId, decoded);
                Stream.Trailers = decoded;
            }

            if (Stream.IsConnectTunnel)
            {

                if (isInitialHeaders)
                {
                    // A CONNECT tunnel has no "buffer a complete body, then
                    // produce a single response" cycle — DATA flows both ways for
                    // as long as the tunnel stays open, so it's dispatched
                    // immediately rather than waiting for END_STREAM
                    // (HandleDataAsync routes further inbound DATA into this
                    // channel instead of RequestBody).
                    Stream.TunnelInbound = Channel.CreateUnbounded<byte[]>();
                    StartConnectHandler(Stream);
                }

                // Reached either on the initial HEADERS (with END_STREAM already
                // true, an immediately half-closed tunnel) or on a later
                // "trailers" block — HandleHeaders already enforced END_STREAM
                // for that case, so this is always the end of the peer's side.
                if (EndStream)
                {
                    Stream.CloseRemote();
                    Stream.TunnelInbound?.Writer.TryComplete();
                }

                return;

            }

            // RFC 9110 Section 10.1.1 (Expect: 100-continue): a client that sends a
            // body but wants to be told to proceed first waits for an interim 100
            // before sending DATA. We always accept, so send the 100 as soon as the
            // (initial) headers of a body-bearing request arrive; the final response
            // follows normally after the body. (An unsupported expectation is a
            // "MAY 417" — we just ignore it and process the request, per §10.1.1.)
            if (isInitialHeaders && !EndStream)
            {
                var expect = Stream.RequestHeaders!.FirstOrDefault(h => h.Name == "expect").Value;
                if (expect is not null && expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                    await SendHeaderListAsync(Stream.StreamId, [(":status", "100")], EndStream: false);
            }

            // Streaming request path (a streaming handler is registered): dispatch
            // the handler now — at HEADERS-complete — and feed it the body through a
            // channel as DATA arrives, rather than buffering the whole body first.
            // This is what lets a handler read the request and write the response
            // concurrently (bidirectional streaming, e.g. gRPC).
            if (streamingHandler is not null)
            {

                if (isInitialHeaders)
                {
                    // A declared content-length with an immediate END_STREAM (no
                    // body) is malformed — reject before dispatching (Section 8.1.2.6).
                    if (EndStream && Stream.ExpectedContentLength is > 0)
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Stream.StreamId,
                            $"content-length {Stream.ExpectedContentLength} declared but no request body sent");

                    Stream.IsStreamingRequest = true;
                    Stream.RequestBodyChannel = Channel.CreateUnbounded<byte[]>();
                    StartStreamingHandler(Stream);
                }

                // Reached on the initial HEADERS (if END_STREAM: a bodyless request)
                // or on a later trailers block (which HandleHeaders requires to set
                // END_STREAM) — either way the peer's side is done, so end the body.
                if (EndStream)
                {
                    Stream.CloseRemote();
                    Stream.RequestBodyChannel!.Writer.TryComplete();
                }

                return;

            }

            if (EndStream)
            {
                // No DATA frames will follow, so the body length is 0 — a non-zero
                // declared content-length is a malformed request (Section 8.1.2.6).
                if (Stream.ExpectedContentLength is > 0)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Stream.StreamId,
                        $"content-length {Stream.ExpectedContentLength} declared but no request body sent");

                Stream.CloseRemote();
                StartRequestHandler(Stream);
            }
            else
            {
                // We expect DATA frames next (request body). If the peer already
                // declared a content-length past the buffered-body cap, refuse now
                // rather than buffering most of it first (the per-DATA check in
                // HandleDataAsync is the backstop for an undeclared/lying length).
                if (Stream.ExpectedContentLength > maxRequestBodySize)
                    throw new HTTP2StreamException(HTTP2ErrorCode.ENHANCE_YOUR_CALM, Stream.StreamId,
                        $"Declared content-length {Stream.ExpectedContentLength} exceeds the {maxRequestBodySize}-byte limit");

                Stream.RequestBody = new MemoryStream();
            }

        }

        /// <summary>
        /// Parse the request's <c>content-length</c> (RFC 9113, Section 8.1.2.6).
        /// Returns the declared length, or null when absent. A syntactically
        /// invalid value, a negative value, or multiple content-length fields with
        /// differing values make the request malformed — a stream error.
        /// </summary>
        private static long? ParseContentLength(UInt32 StreamId, List<(string Name, string Value)> Headers)
        {

            long? result = null;

            foreach (var (name, value) in Headers)
            {

                if (name != "content-length")
                    continue;

                if (!long.TryParse(value, System.Globalization.NumberStyles.None,
                                   System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        $"malformed content-length value '{value}'");

                // Multiple content-length fields are allowed only if identical.
                if (result is not null && result != parsed)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "conflicting content-length header fields");

                result = parsed;

            }

            return result;

        }

        /// <summary>
        /// Pseudo-header fields defined for HTTP/2 requests (RFC 9113, Section
        /// 8.3.1) plus ":protocol" (RFC 8441, Section 4) for extended CONNECT.
        /// ":protocol"'s legality is context-dependent (CONNECT only, and only if
        /// we advertised ENABLE_CONNECT_PROTOCOL) — checked separately below,
        /// same as it being CONNECT-only isn't a "which pseudo-headers exist at
        /// all" concern.
        /// </summary>
        private static readonly HashSet<string> RequestPseudoHeaders = new(StringComparer.Ordinal)
            { ":method", ":scheme", ":authority", ":path", ":protocol" };

        /// <summary>
        /// Header fields that carry connection-specific semantics from HTTP/1.1 and
        /// are prohibited in HTTP/2 (RFC 9113, Section 8.2.2) because framing and
        /// connection management are handled by the HTTP/2 layer itself.
        /// </summary>
        private static readonly HashSet<string> ConnectionSpecificHeaders = new(StringComparer.Ordinal)
            { "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade" };

        /// <summary>
        /// Validate a decoded request header block against RFC 9113, Section 8,
        /// plus the CONNECT (Section 8.5) and extended-CONNECT (RFC 8441 Section
        /// 4) variants. Malformed requests are treated as a stream error (Section
        /// 8.1.1), not a connection error, so a single bad request doesn't take
        /// down the connection for other streams.
        ///
        /// Three shapes, distinguished by :method and the presence of :protocol:
        ///   - Ordinary request: :method, :scheme, :path all mandatory.
        ///   - Plain CONNECT (Section 8.5): :scheme and :path MUST be absent;
        ///     :authority (the tunnel target) is mandatory instead.
        ///   - Extended CONNECT (RFC 8441 — :protocol present): unlike plain
        ///     CONNECT, :scheme and :path ARE mandatory here (this is the one
        ///     place RFC 8441 explicitly differs from RFC 9113 §8.5), on top of
        ///     :protocol and :authority. Only accepted if a connect handler is
        ///     registered (see ENABLE_CONNECT_PROTOCOL in ReadConnectionPrefaceAsync).
        /// </summary>
        private void ValidateRequestHeaders(UInt32 StreamId, List<(string Name, string Value)> Headers)
        {

            var seenPseudoHeaders   = new HashSet<string>(StringComparer.Ordinal);
            var seenRegularHeader   = false;

            foreach (var (name, value) in Headers)
            {

                if (name.Length == 0)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "Empty header field name");

                if (name[0] == ':')
                {

                    // Section 8.1.1: pseudo-header fields MUST appear before regular fields.
                    if (seenRegularHeader)
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                            $"Pseudo-header field '{name}' appears after a regular header field");

                    if (!RequestPseudoHeaders.Contains(name))
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                            $"Unknown or response-only pseudo-header field '{name}' in request");

                    // Section 8.1.1: duplicate pseudo-header fields make the request malformed.
                    if (!seenPseudoHeaders.Add(name))
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                            $"Duplicate pseudo-header field '{name}'");

                    continue;

                }

                seenRegularHeader = true;
                ValidateRegularHeaderField(StreamId, name, value);

            }

            if (!seenPseudoHeaders.Contains(":method"))
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Missing mandatory pseudo-header field ':method'");

            var method      = Headers.First(h => h.Name == ":method").Value;
            var isConnect   = method == "CONNECT";
            var hasProtocol = seenPseudoHeaders.Contains(":protocol");

            // RFC 8441, Section 4: ":protocol" is meaningless outside CONNECT.
            if (hasProtocol && !isConnect)
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Pseudo-header field ':protocol' is only valid on a CONNECT request");

            if (hasProtocol && connectHandler is null)
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Extended CONNECT (':protocol') is not enabled on this connection");

            if (isConnect && !hasProtocol)
            {

                // RFC 9113, Section 8.5: plain CONNECT MUST NOT include :scheme or
                // :path, and MUST include :authority (the tunnel target).
                if (seenPseudoHeaders.Contains(":scheme"))
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "CONNECT request must not include pseudo-header field ':scheme'");

                if (seenPseudoHeaders.Contains(":path"))
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "CONNECT request must not include pseudo-header field ':path'");

                if (!seenPseudoHeaders.Contains(":authority"))
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "CONNECT request must include pseudo-header field ':authority'");

                // Deliberately NOT rejected here even if no connect handler is
                // registered — that's a "we don't support this method" business
                // decision, answered with a proper 501 by DispatchConnectAsync,
                // not a framing-level PROTOCOL_ERROR/RST_STREAM.
                return;

            }

            // Ordinary requests, and extended CONNECT (which — unlike plain
            // CONNECT — still needs the usual triad; RFC 8441 Section 4).
            if (!seenPseudoHeaders.Contains(":scheme"))
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Missing mandatory pseudo-header field ':scheme'");

            if (!seenPseudoHeaders.Contains(":path"))
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Missing mandatory pseudo-header field ':path'");

            var path = Headers.First(h => h.Name == ":path").Value;

            if (path.Length == 0)
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Pseudo-header field ':path' must not be empty");

            if (isConnect && !seenPseudoHeaders.Contains(":authority"))
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "Extended CONNECT request must include pseudo-header field ':authority'");

        }

        /// <summary>
        /// Validate a decoded trailer header block (RFC 9113, Section 8.1). Unlike
        /// the initial request headers, trailers MUST NOT contain any pseudo-header
        /// fields; the remaining field-level rules (Section 8.2) still apply.
        /// </summary>
        private static void ValidateTrailerHeaders(UInt32 StreamId, List<(string Name, string Value)> Headers)
        {

            foreach (var (name, value) in Headers)
            {

                if (name.Length == 0)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "Empty header field name");

                if (name[0] == ':')
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        $"Trailing header block must not contain pseudo-header field '{name}'");

                ValidateRegularHeaderField(StreamId, name, value);

            }

        }

        /// <summary>
        /// Field-level rules that apply to every regular (non-pseudo) header field,
        /// whether in the initial request headers or in trailers (RFC 9113, Section 8.2).
        /// </summary>
        private static void ValidateRegularHeaderField(UInt32 StreamId, string Name, string Value)
        {

            // Section 8.2.1: field names MUST be lowercase.
            foreach (var c in Name)
            {
                if (c is >= 'A' and <= 'Z')
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        $"Header field name '{Name}' is not lowercase");
            }

            // Section 8.2.2: connection-specific header fields are prohibited.
            if (ConnectionSpecificHeaders.Contains(Name))
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    $"Connection-specific header field '{Name}' is not allowed in HTTP/2");

            // Section 8.2.2: TE is the one exception, but only with value "trailers".
            if (Name == "te" && Value != "trailers")
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                    "TE header field must not contain any value other than \"trailers\"");

        }

        #endregion


        #region PRIORITY_UPDATE (RFC 9218, Section 7.1)

        /// <summary>
        /// A connection-level frame (its own Stream Identifier MUST be 0 — this
        /// frame is not sent "on" the stream it reprioritizes) whose payload
        /// names a "Prioritized Stream ID" plus a new Priority Field Value for
        /// it. Silently ignored (not a protocol error) if that stream doesn't
        /// exist yet or has already closed — reordering between this frame and
        /// the target stream's own HEADERS, or a PRIORITY_UPDATE arriving after
        /// its target already finished, are both expected outcomes of ordinary
        /// network reordering per the RFC, not violations.
        /// </summary>
        /// <summary>
        /// RFC 7540 PRIORITY frame (Section 6.3). RFC 9113 deprecated stream
        /// dependencies/weights and we advertise SETTINGS_NO_RFC7540_PRIORITIES=1,
        /// so we do not act on the payload — but the frame envelope still MUST be
        /// validated (Section 6.3), which is what h2spec checks here.
        /// </summary>
        private void HandlePriority(HTTP2Frame Frame)
        {

            // A PRIORITY frame is associated with a stream; stream 0 is a
            // connection error (Section 6.3).
            if (Frame.StreamId == 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "PRIORITY frame must not be on stream 0");

            // A PRIORITY frame with a length other than 5 octets is a stream error
            // of type FRAME_SIZE_ERROR (Section 6.3).
            if (Frame.Length != 5)
                throw new HTTP2StreamException(HTTP2ErrorCode.FRAME_SIZE_ERROR, Frame.StreamId,
                                               "PRIORITY frame payload must be 5 bytes");

            // Section 5.3.1: a stream cannot depend on itself (the low 31 bits of
            // the first 4 payload octets are the stream dependency).
            var streamDependency = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload) & 0x7FFFFFFFu;
            if (streamDependency == Frame.StreamId)
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                                               "PRIORITY frame: a stream cannot depend on itself");

            // A well-formed PRIORITY frame carries no request progress — treat a
            // flood of them like the PING/SETTINGS/PRIORITY_UPDATE flood class.
            CountUnproductiveFrame();

            // Payload (deprecated RFC 7540 priority) is deliberately ignored.

        }

        private void HandlePriorityUpdate(HTTP2Frame Frame)
        {

            if (Frame.StreamId != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "PRIORITY_UPDATE frame must be on stream 0");

            if (Frame.Length < 4)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   "PRIORITY_UPDATE payload must be at least 4 bytes");

            // A PRIORITY_UPDATE flood with no accompanying request progress is
            // the same class of abuse as an empty PING/SETTINGS flood — cheap
            // for the peer to send, but each one still costs us a lookup + parse.
            CountUnproductiveFrame();

            var prioritizedStreamId = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload) & 0x7FFFFFFFu;
            var priorityFieldValue  = Encoding.ASCII.GetString(Frame.Payload.AsSpan(4));

            var stream = streamManager.TryGetStream(prioritizedStreamId);

            if (stream is null || stream.State == HTTP2StreamState.Closed)
                return;

            stream.Priority = ParsePriority(priorityFieldValue);

            // The writer loop may already be idle-waiting with this stream's
            // (now stale) priority baked into its last pick — wake it so the
            // new priority takes effect immediately rather than on the next
            // unrelated window change.
            SignalWriterWakeup();

        }

        #endregion


        #region RFC 9218 Priority (Extensible Prioritization Scheme for HTTP)

        /// <summary>
        /// Parse an RFC 9218 Priority Field Value — a Structured Fields
        /// Dictionary (RFC 8941) with two recognized keys, "u" (urgency, integer
        /// 0-7, default 3) and "i" (incremental, boolean, default false). Used
        /// both for the request's own "priority" header field (Section 4) and
        /// for a PRIORITY_UPDATE frame's payload (Section 7.1), which share the
        /// identical value grammar.
        ///
        /// Deliberately lenient (Section 4): a parse failure, an unknown key, or
        /// an out-of-range urgency just falls back to that parameter's default
        /// rather than raising a stream/connection error — a malformed priority
        /// hint is a hint gone wrong, not a protocol violation. Per RFC 8941,
        /// Section 3.3.6, a bare key with no "=value" is shorthand for a true
        /// Boolean, which is how a bare "i" (e.g. "u=1, i") means "i=?1".
        /// </summary>
        private static HTTP2Priority ParsePriority(string Value)
        {

            var urgency     = HTTP2Priority.DefaultUrgency;
            var incremental = false;

            foreach (var rawMember in Value.Split(','))
            {

                var member = rawMember.Trim();
                if (member.Length == 0)
                    continue;

                var eq    = member.IndexOf('=');
                var key   = (eq < 0 ? member : member[..eq]).Trim();
                var value =  eq < 0 ? "?1"   : member[(eq + 1)..].Trim();

                switch (key)
                {

                    case "u" when byte.TryParse(value, out var u) && u <= 7:
                        urgency = u;
                        break;

                    case "i":
                        incremental = value == "?1";
                        break;

                    // Unknown key, or a recognized key with an out-of-range /
                    // malformed value — ignored, that parameter keeps its default.

                }

            }

            return new HTTP2Priority(urgency, incremental);

        }

        #endregion


        #region DATA (Section 6.1)

        private async Task HandleDataAsync(HTTP2Frame Frame)
        {

            if (Frame.StreamId == 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "DATA frame must not be on stream 0");

            // RFC 9113, Section 6.1: the ENTIRE frame payload counts against flow
            // control — including the Pad Length byte and the padding itself, not
            // just the useful data. StripPadding (below) only decides what reaches
            // the request body, never what is accounted.
            var flowLength = Frame.Payload.Length;

            var stream = streamManager.TryGetStream(Frame.StreamId);

            if (stream is null || stream.State is not (HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal))
            {

                // RFC 9113, Section 5.1: a genuinely idle stream (never opened, not
                // implicitly closed by a later stream) only accepts HEADERS/PRIORITY —
                // anything else is a connection error, not merely a stream error.
                // (A connection error needs no window accounting — Section 6.9
                // exempts exactly that case.)
                if (stream is null && streamManager.IsIdle(Frame.StreamId))
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                        $"DATA frame for idle stream {Frame.StreamId}");

                // RFC 9113, Section 6.9: DATA on an (implicitly) closed stream —
                // answered with a mere stream error while the connection lives on —
                // MUST still be counted against, and returned to, the CONNECTION
                // flow-control window: the peer charged its connection send window
                // for these bytes, and never crediting them back would leak that
                // window shut. (The stream window is moot — the stream is gone.)
                lock (recvLock)
                {
                    streamManager.ConnectionRecvWindow -= flowLength;
                    if (streamManager.ConnectionRecvWindow < 0)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR,
                            "Flow control window exceeded");
                }

                await ReplenishReceiveWindowsAsync(null, flowLength);

                // Closed-stream DATA advances no request, so it counts against the
                // flood budget instead of resetting it — the necessary companion to
                // the accounting above: now that window is dutifully handed back,
                // an endless closed-stream DATA spray would otherwise be free.
                CountUnproductiveFrame();

                throw new HTTP2StreamException(HTTP2ErrorCode.STREAM_CLOSED, Frame.StreamId,
                    stream is null
                        ? "DATA for unknown or closed stream"
                        : $"DATA received in invalid stream state {stream.State}");

            }

            // Real request traffic — reset the control-frame flood counter.
            unproductiveFrames = 0;

            // Flow control accounting (full payload incl. padding, Section 6.1)
            lock (recvLock)
            {
                stream.RecvWindow                  -= flowLength;
                streamManager.ConnectionRecvWindow -= flowLength;

                if (stream.RecvWindow < 0 || streamManager.ConnectionRecvWindow < 0)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR,
                        "Flow control window exceeded");
            }

            var payload = StripPadding(Frame, Frame.Payload.AsSpan());
            var dataLength = payload.Length;

            // Padding (the Pad Length byte + the padding octets) counts against flow
            // control (Section 6.1) but is discarded here — no consumer ever reads
            // it — so its window is returned immediately. The DATA bytes proper are
            // returned differently per path (below).
            var paddingOverhead = flowLength - dataLength;

            // A CONNECT tunnel and a streaming request have an incremental consumer
            // (the handler's tunnel/body ReadAsync), so their flow-control window is
            // returned as the handler CONSUMES each chunk (ReplenishConsumedAsync),
            // NOT on receipt. This is real backpressure: a slow consumer leaves the
            // window depleted, so the peer is forced to stop sending — and because
            // the window is thus never replenished ahead of consumption, the inbound
            // channel can hold at most a window's worth (per-stream + connection)
            // regardless of how fast the peer sends. A buffered request has no such
            // consumer (the whole body is handed over at END_STREAM), so it is
            // replenished on receipt and bounded instead by maxRequestBodySize.
            if (stream.IsConnectTunnel)
            {
                if (dataLength > 0)
                    await stream.TunnelInbound!.Writer.WriteAsync(payload.ToArray(), cancellationToken);

                // Only the discarded padding is returned now; the data waits for the
                // tunnel consumer (Section 6.1 padding still owed regardless).
                await ReplenishReceiveWindowsAsync(stream, paddingOverhead);
            }
            else if (stream.IsStreamingRequest)
            {
                // Hand the body chunk to the streaming handler as it arrives (no
                // buffering); track the running length for the content-length check.
                stream.ReceivedBodyLength += dataLength;
                if (dataLength > 0)
                    await stream.RequestBodyChannel!.Writer.WriteAsync(payload.ToArray(), cancellationToken);

                await ReplenishReceiveWindowsAsync(stream, paddingOverhead);
            }
            else
            {
                stream.RequestBody?.Write(payload);

                // Bound the buffered body: with no incremental consumer, an
                // unbounded upload would grow RequestBody without limit. Over the
                // cap, reset the stream — but first credit the CONNECTION window for
                // this frame (Section 6.9: a stream error while the connection lives
                // must still return the connection-level window, exactly as the
                // closed-stream path above does), or an over-cap upload would leak
                // the connection window shut on the way out.
                if ((stream.RequestBody?.Length ?? 0) > maxRequestBodySize)
                {
                    await ReplenishReceiveWindowsAsync(null, flowLength);
                    throw new HTTP2StreamException(HTTP2ErrorCode.ENHANCE_YOUR_CALM, Frame.StreamId,
                        $"Request body exceeds the {maxRequestBodySize}-byte limit");
                }

                // Buffered: replenish the full payload incl. padding on receipt.
                await ReplenishReceiveWindowsAsync(stream, flowLength);
            }

            if (Frame.EndStream)
            {

                // RFC 9113, Section 8.1.2.6: a declared content-length MUST equal
                // the summed DATA payload length, else the request is malformed.
                // (A CONNECT tunnel is exempt — it has no body semantics.)
                if (!stream.IsConnectTunnel && stream.ExpectedContentLength is { } expected)
                {
                    var received = stream.IsStreamingRequest
                                       ? stream.ReceivedBodyLength
                                       : stream.RequestBody?.Length ?? 0;

                    if (received != expected)
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                            $"content-length {expected} does not match body length {received}");
                }

                stream.CloseRemote();

                if (stream.IsConnectTunnel)
                    stream.TunnelInbound!.Writer.TryComplete();
                else if (stream.IsStreamingRequest)
                    stream.RequestBodyChannel!.Writer.TryComplete();
                else
                    StartRequestHandler(stream);
            }

        }

        /// <summary>
        /// Return consumed flow-control window to the peer in batches: accumulate
        /// per-stream and connection-wide, and only emit a WINDOW_UPDATE once the
        /// accumulated amount crosses half the respective window. This replaces the
        /// old "one stream + one connection WINDOW_UPDATE per DATA frame" strategy,
        /// roughly halving flow-control frames on a small window and eliminating
        /// them almost entirely on the large one (a transfer smaller than half the
        /// window sends none at all). <paramref name="Stream"/> is null for DATA on
        /// a closed/unknown stream (RFC 9113, Section 6.9 still requires connection-
        /// window accounting there), in which case only the connection window is
        /// returned.
        /// </summary>
        private async Task ReplenishReceiveWindowsAsync(HTTP2Stream? Stream, int DataLength)
        {

            if (DataLength <= 0)
                return;

            // Decide what (if anything) to emit and apply the local window bookkeeping
            // under recvLock — but do NOT send while holding it (SendFrameAsync is
            // async and takes writeLock). The local RecvWindow/ConnectionRecvWindow
            // are updated here to reflect what we're about to grant; the frames go
            // out below, after the lock is released.
            UInt32 streamInc = 0, connInc = 0;
            var    streamId  = Stream?.StreamId ?? 0;

            lock (recvLock)
            {

                if (Stream is not null)
                {
                    Stream.PendingRecvUpdate += DataLength;
                    if (Stream.PendingRecvUpdate >= localSettings.InitialWindowSize / 2)
                    {
                        streamInc                 = (UInt32) Stream.PendingRecvUpdate;
                        Stream.RecvWindow        += streamInc;
                        Stream.PendingRecvUpdate  = 0;
                    }
                }

                connectionPendingRecvUpdate += DataLength;
                if (connectionPendingRecvUpdate >= ConnectionRecvWindowTarget / 2)
                {
                    connInc                             = (UInt32) connectionPendingRecvUpdate;
                    streamManager.ConnectionRecvWindow += connInc;
                    connectionPendingRecvUpdate         = 0;
                }

            }

            if (streamInc > 0)
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(streamId, streamInc));
            if (connInc > 0)
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, connInc));

        }

        /// <summary>
        /// Return flow-control window for body/tunnel bytes a streaming or CONNECT
        /// handler has just CONSUMED (read off its inbound channel) — the demand
        /// signal that drives consumption-based backpressure. Called from handler
        /// tasks (via <see cref="HTTP2RequestStream"/> / <see cref="HTTP2Tunnel"/>),
        /// so it runs concurrently with the read loop's decrement; both go through
        /// the recvLock-guarded <see cref="ReplenishReceiveWindowsAsync"/>.
        /// </summary>
        internal Task ReplenishConsumedAsync(HTTP2Stream Stream, int Count)
            => ReplenishReceiveWindowsAsync(Stream, Count);

        #endregion


        #region WINDOW_UPDATE (Section 6.9)

        private void HandleWindowUpdate(HTTP2Frame Frame)
        {

            if (Frame.Length != 4)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   "WINDOW_UPDATE payload must be 4 bytes");

            var increment = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload) & 0x7FFFFFFFu;

            if (increment == 0)
            {
                if (Frame.StreamId == 0)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                        "WINDOW_UPDATE increment must not be 0");
                else
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                        "WINDOW_UPDATE increment must not be 0");
            }

            if (Frame.StreamId == 0)
            {
                lock (flowLock)
                {
                    streamManager.ConnectionSendWindow += increment;

                    if (streamManager.ConnectionSendWindow > Int32.MaxValue)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR,
                            "Connection flow control window overflow");
                }

                SignalWriterWakeup();
            }
            else
            {
                var stream = streamManager.TryGetStream(Frame.StreamId);

                if (stream is null)
                {
                    // A genuinely idle stream only accepts HEADERS/PRIORITY (RFC 9113,
                    // Section 5.1); an implicitly-closed or already-closed stream MAY
                    // still receive a straggling WINDOW_UPDATE — ignore it (Section 6.9).
                    if (streamManager.IsIdle(Frame.StreamId))
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                            $"WINDOW_UPDATE frame for idle stream {Frame.StreamId}");
                }
                else if (stream.State != HTTP2StreamState.Closed)
                {
                    lock (flowLock)
                    {
                        stream.SendWindow += increment;

                        if (stream.SendWindow > Int32.MaxValue)
                            throw new HTTP2StreamException(HTTP2ErrorCode.FLOW_CONTROL_ERROR, Frame.StreamId,
                                "Stream flow control window overflow");
                    }

                    SignalWriterWakeup();
                }
            }

        }

        #endregion


        #region Priority-Aware DATA Writer (RFC 9218)

        /// <summary>
        /// Wake anything waiting in <see cref="DataWriterLoopAsync"/> — a send
        /// window grew, a stream was reset, new data was enqueued, or a
        /// stream's priority changed — then arm a fresh signal for the next
        /// wait. Named for its broadest original purpose (window changes); it
        /// now also doubles as the writer loop's general "something worth
        /// re-scanning for" wakeup.
        /// </summary>
        private void SignalWriterWakeup()
        {

            TaskCompletionSource previous;

            lock (flowLock)
            {
                previous      = windowChanged;
                windowChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            previous.TrySetResult();

        }

        /// <summary>
        /// Monotonic counter handed out to a stream's <see cref="HTTP2Stream.LastServedSequence"/>
        /// each time the writer loop sends from it — see <see cref="ComparePriority"/>.
        /// </summary>
        private long writerSequence;

        /// <summary>
        /// Queue Data (and, if EndStream, a pending End-Stream marker) on
        /// Stream's outbound queue for <see cref="DataWriterLoopAsync"/> to send,
        /// and wake the loop so it notices without waiting for an unrelated
        /// window/priority change. Returns once the data has actually been sent,
        /// the stream was reset (<see cref="HTTP2Stream.Reset"/> drains the
        /// queue), or the connection is tearing down (observes this connection's
        /// own cancellation token) — mirroring the old direct-send loop this
        /// replaced, which likewise only returned once bytes were actually on
        /// the wire or the send was abandoned.
        /// </summary>
        internal Task EnqueueOutboundAsync(HTTP2Stream Stream, byte[] Data, bool EndStream, List<(string Name, string Value)>? Trailers = null)
        {
            var completion = Stream.OutboundQueue.EnqueueAsync(Data, EndStream, Trailers);
            SignalWriterWakeup();
            return completion.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// The single task that actually writes every response/tunnel DATA frame
        /// for this connection — the "multiplexed-writer rework" the RFC 9218
        /// roadmap note called for. Producers (SendResponseAsync,
        /// SendTunnelDataAsync) no longer race each other for send-window space;
        /// they just enqueue onto their stream's <see cref="HTTP2OutboundQueue"/>
        /// and this loop is the sole arbiter of whose bytes go out next,
        /// applying RFC 9218 urgency/incremental ordering (<see cref="ComparePriority"/>)
        /// instead of first-come-first-served.
        ///
        /// Runs for the connection's whole lifetime, started in RunAsync
        /// alongside FrameLoopAsync (a slow/blocked writer must never stop the
        /// read loop from servicing other frames, and vice versa).
        /// </summary>
        private async Task DataWriterLoopAsync()
        {

            try
            {

                while (!cancellationToken.IsCancellationRequested)
                {

                    var candidates = streamManager.GetSendableStreams();

                    HTTP2Stream? stream    = null;
                    var          reserved  = 0;
                    Task?        waitTask  = null;

                    lock (flowLock)
                    {

                        stream = PickNextStreamToSend(candidates, streamManager.ConnectionSendWindow, out var needsWindow);

                        if (stream is not null && needsWindow)
                        {

                            // PickNextStreamToSend already confirmed both windows
                            // are positive for a window-needing pick, so this is
                            // always > 0 — nothing left to do here but take it.
                            reserved = (int) Math.Min(
                                Math.Min(stream.SendWindow, streamManager.ConnectionSendWindow),
                                (int) remoteSettings.MaxFrameSize);

                            stream.SendWindow                  -= reserved;
                            streamManager.ConnectionSendWindow -= reserved;

                        }

                        if (stream is null)
                            waitTask = windowChanged.Task;

                    }

                    if (stream is null)
                    {
                        await waitTask!.WaitAsync(cancellationToken);
                        continue;
                    }

                    var taken = stream.OutboundQueue.TakeChunk(reserved);

                    if (taken is null)
                    {
                        // Rare race: the item vanished between the pick and the
                        // take (e.g. the stream was reset in between). Give back
                        // whatever window we reserved and try again.
                        if (reserved > 0)
                            lock (flowLock)
                            {
                                stream.SendWindow                  += reserved;
                                streamManager.ConnectionSendWindow += reserved;
                            }

                        continue;
                    }

                    var (chunk, endStream, trailers) = taken.Value;

                    if (chunk.Length < reserved)
                        lock (flowLock)
                        {
                            var giveBack = reserved - chunk.Length;
                            stream.SendWindow                  += giveBack;
                            streamManager.ConnectionSendWindow += giveBack;
                        }

                    stream.LastServedSequence = Interlocked.Increment(ref writerSequence);

                    if (trailers is not null)
                    {
                        // A response with trailers: the last DATA (if any) must NOT
                        // carry END_STREAM — the trailing HEADERS block does (RFC
                        // 9113, Section 8.1). Both go out here, in order, with the
                        // trailers HPACK-encoded under the write lock.
                        if (chunk.Length > 0)
                            await SendFrameAsync(HTTP2Frame.CreateData(stream.StreamId, chunk, EndStream: false));

                        await SendHeaderListAsync(stream.StreamId, trailers, EndStream: true);
                        CloseLocalIfNotReset(stream);
                    }
                    else
                    {
                        if (chunk.Length > 0 || endStream)
                            await SendFrameAsync(HTTP2Frame.CreateData(stream.StreamId, chunk, EndStream: endStream));

                        if (endStream)
                            CloseLocalIfNotReset(stream);
                    }

                }

            }
            catch (OperationCanceledException)
            {
                // Connection shutting down — normal.
            }

        }

        /// <summary>
        /// Pick the best of Candidates to send from next, per <see cref="ComparePriority"/>.
        /// Skips streams with nothing queued, and streams whose only queued
        /// bytes need flow-control window that isn't currently available — either
        /// the stream's own send window or, since it's shared, the connection's
        /// (an end-of-stream-only marker needs no window at all, so such a
        /// stream is still a candidate regardless of either window).
        ///
        /// Filtering window-blocked streams out of candidacy here — rather than
        /// picking the single best candidate first and discarding the whole turn
        /// if only *it* turns out window-blocked — matters for correctness, not
        /// just efficiency: without it, a lower-priority stream that needs no
        /// window (e.g. a tunnel's closing marker) could starve indefinitely
        /// behind a higher-priority stream that's permanently connection-window-
        /// blocked, even though the lower-priority one is otherwise immediately
        /// sendable.
        /// </summary>
        private static HTTP2Stream? PickNextStreamToSend(IReadOnlyList<HTTP2Stream> Candidates, long ConnectionSendWindow, out bool NeedsWindow)
        {

            HTTP2Stream? best            = null;
            var          bestNeedsWindow = false;

            foreach (var stream in Candidates)
            {

                if (!stream.OutboundQueue.HasPending)
                    continue;

                var needsWindow = stream.OutboundQueue.HeadNeedsWindow;

                if (needsWindow && (stream.SendWindow <= 0 || ConnectionSendWindow <= 0))
                    continue;   // Flow control blocked; retry once WINDOW_UPDATE arrives

                if (best is null || ComparePriority(stream, best) < 0)
                {
                    best            = stream;
                    bestNeedsWindow = needsWindow;
                }

            }

            NeedsWindow = bestNeedsWindow;
            return best;

        }

        /// <summary>
        /// RFC 9218 send ordering: lower urgency number sends first; within the
        /// same urgency, a non-incremental stream ("send as a single unit") is
        /// preferred over an incremental one ("fine to interleave"); ties beyond
        /// that are broken round-robin-fairly by recency of last service. This
        /// is a deliberate simplification of the RFC's non-incremental guidance
        /// — a strict reading favors draining one non-incremental stream to
        /// completion before starting the next at the same urgency, whereas this
        /// still round-robins fairly among several concurrent non-incremental
        /// streams at that urgency, rather than head-of-line-blocking one behind
        /// another. Reasonable for a learning implementation, and arguably a
        /// fairer outcome for concurrent equal-urgency responses either way.
        /// </summary>
        private static int ComparePriority(HTTP2Stream A, HTTP2Stream B)
        {

            if (A.Priority.Urgency != B.Priority.Urgency)
                return A.Priority.Urgency.CompareTo(B.Priority.Urgency);

            if (A.Priority.Incremental != B.Priority.Incremental)
                return A.Priority.Incremental ? 1 : -1;   // Non-incremental drained preferentially

            return A.LastServedSequence.CompareTo(B.LastServedSequence);   // Least-recently-served first

        }

        #endregion


        #region PING (Section 6.7)

        private async Task HandlePingAsync(HTTP2Frame Frame)
        {

            if (Frame.StreamId != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "PING must be on stream 0");

            if (Frame.Length != 8)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   "PING payload must be 8 bytes");

            if (!Frame.IsAck)
            {
                // Each non-ACK PING forces a PING ACK; a flood makes no request
                // progress (empty-frame flood, RFC 9113 §10.5).
                CountUnproductiveFrame();
                await SendFrameAsync(HTTP2Frame.CreatePingAck(Frame.Payload));
            }

        }

        #endregion


        #region RST_STREAM (Section 6.4)

        private void HandleRstStream(HTTP2Frame Frame)
        {

            if (Frame.StreamId == 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "RST_STREAM must not be on stream 0");

            if (Frame.Length != 4)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                                                   "RST_STREAM payload must be 4 bytes");

            var errorCode = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload);
            var stream    = streamManager.TryGetStream(Frame.StreamId);

            if (stream is not null)
            {
                Console.Error.WriteLine($"[HTTP/2] Stream {Frame.StreamId} reset by peer: {errorCode}");
                stream.Reset();

                // Wake a response task possibly waiting for window space on this
                // stream, so it can notice the reset and abort.
                SignalWriterWakeup();

                CheckRapidReset();
            }
            else if (streamManager.IsIdle(Frame.StreamId))
            {
                // A genuinely idle stream only accepts HEADERS/PRIORITY (RFC 9113,
                // Section 5.1); an implicitly-closed or already-closed stream MAY
                // still receive a straggling RST_STREAM — ignore it in that case.
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                    $"RST_STREAM frame for idle stream {Frame.StreamId}");
            }

        }

        /// <summary>
        /// "HTTP/2 Rapid Reset" mitigation (CVE-2023-44487): once enough streams
        /// have been opened to make the ratio meaningful, a peer that resets most
        /// of what it opens — before we ever get a chance to complete a response
        /// — is treated as abusive and the connection is torn down. RFC 9113
        /// doesn't define a specific counter-measure; this ratio-based check is
        /// the same general shape multiple implementations (nginx, Go, nghttp2)
        /// shipped in their October 2023 patches.
        /// </summary>
        private void CheckRapidReset()
        {

            peerResetStreams++;

            if (streamsOpenedByPeer < MinStreamsForResetRatioCheck)
                return;

            if ((double) peerResetStreams / streamsOpenedByPeer > MaxPeerResetRatio)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Too many streams reset by peer ({peerResetStreams}/{streamsOpenedByPeer})");

        }

        #endregion


        #region GOAWAY (Section 6.8)

        private void HandleGoAway(HTTP2Frame Frame)
        {

            if (Frame.StreamId != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                                                   "GOAWAY must be on stream 0");

            var lastStreamId = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(0, 4)) & 0x7FFFFFFFu;
            var errorCode    = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(4, 4));
            var debugData    = Frame.Length > 8 ? Encoding.UTF8.GetString(Frame.Payload.AsSpan(8)) : "";

            Console.Error.WriteLine($"[HTTP/2] GOAWAY received: lastStream={lastStreamId} error={errorCode} debug=\"{debugData}\"");

            goawaySent = true;  // Stop processing new streams

        }

        #endregion


        #region Request Dispatch & Response Sending

        /// <summary>
        /// Run the request handler + response sending on its own task, so the
        /// frame read loop keeps processing frames (WINDOW_UPDATE, other streams)
        /// meanwhile. This is what makes multiplexing real: a slow handler on one
        /// stream no longer blocks the whole connection.
        /// </summary>
        private void StartRequestHandler(HTTP2Stream Stream)
        {

            _ = Task.Run(async () =>
            {

                try
                {
                    await DispatchRequestAsync(Stream);
                }
                catch (OperationCanceledException)
                {
                    // Either the peer RST_STREAM'd this specific stream (see
                    // HTTP2Stream.CancellationToken) or the whole connection is
                    // shutting down — either way, no response is expected.
                    Console.WriteLine($"[HTTP/2] Stream {Stream.StreamId} handler cancelled");
                }
                catch (HTTP2StreamException ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Stream {ex.StreamId} error: {ex.ErrorCode} — {ex.Message}");
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Response error on stream {Stream.StreamId}: {ex.Message}");
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR)); } catch { }
                }

            }, CancellationToken.None);

        }

        /// <summary>
        /// Run a streaming request handler on its own task (mirroring
        /// <see cref="StartRequestHandler"/>): it reads the request body from a
        /// channel and writes the response incrementally, both concurrently. A
        /// handler that returns without ending the response auto-completes; one that
        /// throws before sending headers falls back to a 500, or otherwise resets
        /// the stream (a partial response can't be turned into an error status).
        /// </summary>
        private void StartStreamingHandler(HTTP2Stream Stream)
        {

            _ = Task.Run(async () =>
            {

                // mTLS: surface the validated client-certificate subject as a
                // synthetic request header, same as the buffered path.
                var requestHeaders = clientCertificate is null
                                         ? Stream.RequestHeaders!
                                         : [.. Stream.RequestHeaders!, ("x-client-cert-subject", clientCertificate.Subject)];

                var request  = new HTTP2RequestStream(this, Stream, requestHeaders);
                var response = new HTTP2ResponseStream(this, Stream);

                try
                {
                    await streamingHandler!(request, response, Stream.CancellationToken);
                    await response.EnsureCompletedAsync();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[HTTP/2] Streaming handler on stream {Stream.StreamId} cancelled");
                }
                catch (HTTP2StreamException ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Stream {ex.StreamId} error: {ex.ErrorCode} — {ex.Message}");
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Streaming handler error on stream {Stream.StreamId}: {ex.Message}");

                    // Only send a 500 if nothing has gone out yet — once headers (or
                    // body) are on the wire, the only honest signal left is RST_STREAM.
                    if (!response.HeadersSent)
                    {
                        try
                        {
                            await response.WriteHeadersAsync([(":status", "500"), ("content-type", "text/plain")]);
                            await response.WriteAsync(Encoding.UTF8.GetBytes("Internal Server Error"));
                            await response.CompleteAsync();
                            return;
                        }
                        catch { /* fall through to reset */ }
                    }

                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR)); } catch { }
                }

            }, CancellationToken.None);

        }

        /// <summary>
        /// Invoke the application-level request handler and send back the HTTP/2 response.
        /// </summary>
        private async Task DispatchRequestAsync(HTTP2Stream Stream)
        {

            if (Stream.RequestHeaders is null)
                return;

            var body = Stream.RequestBody is not null
                           ? Stream.RequestBody.ToArray()
                           : null;

            Stream.RequestBody?.Dispose();
            Stream.RequestBody = null;

            // mTLS: surface the validated client-certificate subject to the handler
            // as a synthetic request header. (A synthetic, server-injected header,
            // like a reverse proxy's X-Forwarded-*, not something the peer sent.)
            var requestHeaders = clientCertificate is null
                                     ? Stream.RequestHeaders
                                     : [.. Stream.RequestHeaders, ("x-client-cert-subject", clientCertificate.Subject)];

            try
            {

                var (responseHeaders, responseBody) = await requestHandler(
                    Stream.StreamId,
                    requestHeaders,
                    body,
                    Stream.CancellationToken
                );

                await SendResponseAsync(Stream, responseHeaders, responseBody);

            }
            catch (OperationCanceledException)
            {
                // The stream was reset (or the connection is shutting down) while
                // the handler was running. The peer no longer wants a response —
                // let StartRequestHandler's outer catch swallow this; sending a
                // 500 (or anything else) for an already-RST_STREAM'd stream would
                // be both pointless and wrong.
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HTTP/2] Handler error on stream {Stream.StreamId}: {ex.Message}");

                // Send a 500 Internal Server Error
                var errorHeaders = new List<(string, string)>
                {
                    (":status", "500"),
                    ("content-type", "text/plain")
                };

                var errorBody = Encoding.UTF8.GetBytes("Internal Server Error");
                await SendResponseAsync(Stream, errorHeaders, errorBody);
            }

        }

        /// <summary>
        /// Send an HTTP/2 response: HEADERS frame(s) + optional DATA frame(s).
        /// The DATA is handed to the connection's shared priority-aware writer
        /// loop (<see cref="DataWriterLoopAsync"/>) rather than sent directly —
        /// it decides send order relative to other concurrent streams'
        /// responses; this method just waits for its own body to be fully sent
        /// (or abandoned, on reset/connection teardown).
        /// </summary>
        private async Task SendResponseAsync(
            HTTP2Stream                      Stream,
            List<(string Name, string Value)> ResponseHeaders,
            byte[]?                          ResponseBody)
        {

            EnforceOutboundHeaderListSize(Stream.StreamId, ResponseHeaders);
            ApplyResponsePriorityOverride(Stream, ResponseHeaders);

            var hasBody      = ResponseBody is not null && ResponseBody.Length > 0;
            var endStream    = !hasBody;

            await SendHeaderListAsync(Stream.StreamId, ResponseHeaders, endStream);

            if (endStream)
            {
                CloseLocalIfNotReset(Stream);
                return;
            }

            await EnqueueOutboundAsync(Stream, ResponseBody!, EndStream: true);

        }

        /// <summary>
        /// RFC 9218, Section 5: a server may reprioritize a response relative to
        /// what the client originally requested by including its own "priority"
        /// header field on the response. Applied here — in addition to being
        /// sent to the client like any other header, unchanged — so the writer
        /// loop picks up the new priority for this response's own body.
        /// </summary>
        internal static void ApplyResponsePriorityOverride(HTTP2Stream Stream, List<(string Name, string Value)> ResponseHeaders)
        {

            var priorityEntry = ResponseHeaders.FirstOrDefault(h => h.Name == "priority");

            if (priorityEntry.Name is not null)
                Stream.Priority = ParsePriority(priorityEntry.Value);

        }

        /// <summary>
        /// Send an already-HPACK-encoded header block as HEADERS (+ CONTINUATION
        /// if it doesn't fit in one frame), atomically under the write lock so
        /// frames of other streams can't interleave into the header block
        /// (RFC 9113, Section 4.3). Shared by ordinary responses and CONNECT's
        /// own (bodyless) response headers.
        /// </summary>
        /// <summary>
        /// HPACK-encode a header list and write it as HEADERS(+CONTINUATION) while
        /// holding the write lock for the whole operation. The encode is done under
        /// the lock deliberately: the HPACK encoder's dynamic table is stateful, so
        /// the order blocks are encoded in MUST equal the order they hit the wire —
        /// concurrent response tasks would otherwise encode in one order and write
        /// in another, desynchronizing the peer's decoder.
        /// </summary>
        internal async Task SendHeaderListAsync(UInt32 StreamId, List<(string Name, string Value)> Headers, bool EndStream)
        {

            await writeLock.WaitAsync(cancellationToken);

            try
            {
                var headerBlock  = hpackEncoder.EncodeHeaderBlock(Headers);
                var headerFrames = BuildHeaderFrames(StreamId, headerBlock, EndStream);

                foreach (var frame in headerFrames)
                    await transportStream.WriteAsync(frame.Serialize(), cancellationToken);

                await transportStream.FlushAsync(cancellationToken);
            }
            finally
            {
                writeLock.Release();
            }

        }

        /// <summary>
        /// Split an encoded header block into a HEADERS frame plus as many
        /// CONTINUATION frames as the peer's MAX_FRAME_SIZE requires (RFC 9113,
        /// Section 4.3 — a header block is a single logical unit).
        /// </summary>
        private List<HTTP2Frame> BuildHeaderFrames(UInt32 StreamId, byte[] HeaderBlock, bool EndStream)
        {

            var maxPayload   = (int) remoteSettings.MaxFrameSize;
            var headerFrames = new List<HTTP2Frame>();

            if (HeaderBlock.Length <= maxPayload)
            {
                headerFrames.Add(
                    HTTP2Frame.CreateHeaders(StreamId, HeaderBlock, EndStream, EndHeaders: true)
                );
            }
            else
            {
                headerFrames.Add(
                    HTTP2Frame.CreateHeaders(StreamId, HeaderBlock[..maxPayload], EndStream, EndHeaders: false)
                );

                var offset = maxPayload;
                while (offset < HeaderBlock.Length)
                {
                    var remaining = HeaderBlock.Length - offset;
                    var chunkSize = Math.Min(remaining, maxPayload);
                    var chunk     = HeaderBlock[offset..(offset + chunkSize)];
                    var isLast    = offset + chunkSize >= HeaderBlock.Length;

                    headerFrames.Add(new HTTP2Frame {
                        Type     = HTTP2FrameType.CONTINUATION,
                        Flags    = isLast ? HTTP2FrameFlags.END_HEADERS : HTTP2FrameFlags.NONE,
                        StreamId = StreamId,
                        Payload  = chunk
                    });

                    offset += chunkSize;
                }
            }

            return headerFrames;

        }

        /// <summary>
        /// CloseLocal, unless the stream was already reset (RST_STREAM) in the
        /// meantime — the response task and the read loop run concurrently.
        /// </summary>
        private static void CloseLocalIfNotReset(HTTP2Stream Stream)
        {
            if (Stream.State != HTTP2StreamState.Closed)
                Stream.CloseLocal();
        }

        /// <summary>
        /// RFC 9113, Section 6.5.2: SETTINGS_MAX_HEADER_LIST_SIZE is the peer's
        /// advisory limit on the UNCOMPRESSED header list size — the sum of each
        /// field's name + value length plus a fixed 32-byte overhead per field
        /// (the same accounting HPACK's own dynamic table uses internally). We
        /// already enforce this on the way IN (EnforceHeaderBufferLimit, checked
        /// against the compressed buffer as a cheap floor); this is the missing
        /// outbound half — proactively refuse to send a response the peer already
        /// told us it won't accept, rather than spend a round trip on headers
        /// it's likely to just reject anyway. Throwing here is caught by
        /// DispatchRequestAsync's catch-all, which falls back to a small (and
        /// thus safely under any sane limit) 500 response.
        /// </summary>
        internal void EnforceOutboundHeaderListSize(UInt32 StreamId, List<(string Name, string Value)> Headers)
        {

            long uncompressedSize = 0;

            foreach (var (name, value) in Headers)
                uncompressedSize += name.Length + value.Length + 32;

            if (uncompressedSize > remoteSettings.MaxHeaderListSize)
                throw new HTTP2StreamException(HTTP2ErrorCode.INTERNAL_ERROR, StreamId,
                    $"Response header list ({uncompressedSize} bytes) exceeds the peer's advertised " +
                    $"MAX_HEADER_LIST_SIZE ({remoteSettings.MaxHeaderListSize} bytes)");

        }

        /// <summary>
        /// Validate outbound trailer fields (RFC 9113, Section 8.1): no
        /// pseudo-header fields, and field names must be lowercase.
        /// </summary>
        internal static void ValidateOutboundTrailers(UInt32 StreamId, List<(string Name, string Value)> Trailers)
        {
            foreach (var (name, _) in Trailers)
            {
                if (name.Length == 0 || name[0] == ':')
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "Trailers must not contain pseudo-header fields");

                foreach (var c in name)
                    if (c is >= 'A' and <= 'Z')
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                            $"Trailer field name '{name}' must be lowercase");
            }
        }

        #endregion


        #region CONNECT / Extended CONNECT Tunneling (RFC 9113 Section 8.5; RFC 8441)

        /// <summary>
        /// Run the connect handler on its own task, mirroring StartRequestHandler
        /// — a tunnel (especially a WebSocket one) can be open indefinitely, so it
        /// must never block the frame read loop from servicing other streams.
        /// </summary>
        private void StartConnectHandler(HTTP2Stream Stream)
        {

            _ = Task.Run(async () =>
            {

                try
                {
                    await DispatchConnectAsync(Stream);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[HTTP/2] Stream {Stream.StreamId} CONNECT tunnel cancelled");
                }
                catch (HTTP2StreamException ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] Stream {ex.StreamId} error: {ex.ErrorCode} — {ex.Message}");
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[HTTP/2] CONNECT tunnel error on stream {Stream.StreamId}: {ex.Message}");
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR)); } catch { }
                }

            }, CancellationToken.None);

        }

        /// <summary>
        /// Ask the registered connect handler whether to accept this CONNECT /
        /// extended-CONNECT stream, send the corresponding response, and — if
        /// accepted — hand it a <see cref="HTTP2Tunnel"/> and run it to
        /// completion. Unlike DispatchRequestAsync there is no generic "500 on any
        /// exception" fallback: by the time an exception could occur, we may have
        /// already sent a 2xx and started streaming, so a thrown exception here is
        /// simply treated like an ordinary handler failure (RST_STREAM via
        /// StartConnectHandler's catch), not something worth retrying as a
        /// different status code.
        /// </summary>
        private async Task DispatchConnectAsync(HTTP2Stream Stream)
        {

            if (Stream.RequestHeaders is null)
                return;

            if (connectHandler is null)
            {
                // No connect handler registered — a well-formed CONNECT is still
                // a request we simply don't implement (RFC 9113 §8.5 doesn't
                // require servers to support it).
                await SendConnectResponseAsync(Stream, 501, null);
                return;
            }

            var result = await connectHandler(Stream.StreamId, Stream.RequestHeaders, Stream.CancellationToken);
            var accepted = result.StatusCode is >= 200 and < 300 && result.RunAsync is not null;

            await SendConnectResponseAsync(Stream, result.StatusCode, result.ExtraHeaders, EndStream: !accepted);

            if (!accepted)
                return;

            var tunnel = new HTTP2Tunnel(this, Stream);

            try
            {
                await result.RunAsync!(tunnel, Stream.CancellationToken);
            }
            finally
            {
                await CompleteTunnelAsync(Stream);
            }

        }

        /// <summary>
        /// Send the HEADERS response to a CONNECT request: just a status (plus
        /// any extra headers the handler wants), never a body in the ordinary
        /// sense — an accepted tunnel's "body" is raw DATA frames sent via
        /// SendTunnelDataAsync as the handler produces them, not a single
        /// pre-computed buffer.
        /// </summary>
        private async Task SendConnectResponseAsync(
            HTTP2Stream                       Stream,
            UInt16                             StatusCode,
            List<(string Name, string Value)>? ExtraHeaders,
            bool                               EndStream = true)
        {

            var headers = new List<(string Name, string Value)> { (":status", StatusCode.ToString()) };

            if (ExtraHeaders is not null)
                headers.AddRange(ExtraHeaders);

            EnforceOutboundHeaderListSize(Stream.StreamId, headers);

            await SendHeaderListAsync(Stream.StreamId, headers, EndStream);

            if (EndStream)
                CloseLocalIfNotReset(Stream);

        }

        /// <summary>
        /// Queue a chunk of tunnel data for the writer loop to send as DATA
        /// frame(s) on Stream — same queue, same priority-aware send order as an
        /// ordinary response body (<see cref="SendResponseAsync"/>); a tunnel
        /// write is not privileged over a normal response's bytes.
        /// </summary>
        internal Task SendTunnelDataAsync(HTTP2Stream Stream, byte[] Data, CancellationToken CancellationToken)
        {

            if (Data.Length == 0)
                return Task.CompletedTask;

            return EnqueueOutboundAsync(Stream, Data, EndStream: false);

        }

        /// <summary>
        /// End our side of an accepted tunnel once the connect handler's RunAsync
        /// returns — a queued empty DATA frame carrying END_STREAM, mirroring how
        /// an ordinary response's last DATA chunk ends the stream. The writer
        /// loop closes the stream locally once it actually sends that frame
        /// (same as it does for any other End-Stream marker); this method's own
        /// job is just to queue it and swallow the (best-effort) failure if the
        /// peer or the connection is already gone.
        /// </summary>
        private async Task CompleteTunnelAsync(HTTP2Stream Stream)
        {
            try
            {
                await EnqueueOutboundAsync(Stream, [], EndStream: true);
            }
            catch
            {
                // Best-effort — the peer may already be gone.
            }
        }

        #endregion


        #region Helpers

        /// <summary>
        /// Strip padding from a frame payload if the PADDED flag is set.
        /// </summary>
        /// <summary>
        /// RFC 9113, Section 8.2.3: reassemble a cookie header that the peer split
        /// into multiple field lines (for HPACK efficiency) back into a single
        /// field, concatenated with the two-octet delimiter "; " (0x3B 0x20), in
        /// original order at the position of the first crumb. Field names are
        /// already validated lowercase, so a plain comparison suffices.
        /// </summary>
        private static void CombineCookieFields(List<(string Name, string Value)> Headers)
        {

            var first = Headers.FindIndex(h => h.Name == "cookie");
            if (first < 0 || Headers.FindIndex(first + 1, h => h.Name == "cookie") < 0)
                return;   // zero or one cookie line — nothing to reassemble

            var combined = String.Join("; ", Headers.Where(h => h.Name == "cookie")
                                                    .Select(h => h.Value));

            Headers.RemoveAll(h => h.Name == "cookie");
            Headers.Insert(first, ("cookie", combined));

        }

        private static ReadOnlySpan<byte> StripPadding(HTTP2Frame Frame, ReadOnlySpan<byte> Payload)
        {

            if (!Frame.IsPadded)
                return Payload;

            if (Payload.Length < 1)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                    "PADDED frame with no pad length byte");

            var padLength = Payload[0];

            if (padLength >= Payload.Length)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                    "Pad length exceeds payload size");

            return Payload.Slice(1, Payload.Length - 1 - padLength);

        }

        /// <summary>
        /// Best-effort graceful-shutdown notice for use by <c>HTTP2Server.StopAsync</c>:
        /// send a GOAWAY (NO_ERROR) telling the peer we won't accept new streams
        /// beyond what we've already seen. Deliberately does NOT set
        /// <c>goawaySent</c> or drain the socket the way the internal error-teardown
        /// path (<see cref="SendGoAwayAsync"/>) does — this is called from outside
        /// the connection's own read loop while that loop may still be blocked on
        /// its own pending read, so draining here would race it on the same
        /// SslStream. The frame read loop keeps running so in-flight streams can
        /// still complete; the caller is expected to cancel shortly after (which
        /// is what actually ends the connection — this method only notifies).
        ///
        /// This sends a single GOAWAY rather than RFC 9113 Section 6.8's full
        /// two-phase sequence (an initial "stop opening streams" notice, one RTT
        /// wait, then a final GOAWAY) — good enough to replace a silent abrupt
        /// disconnect, but a client that opens a new stream in the brief window
        /// before the caller's cancellation takes effect can still race it.
        /// </summary>
        public async Task InitiateGracefulShutdownAsync()
        {

            try
            {
                await SendFrameAsync(
                    HTTP2Frame.CreateGoAway(
                        streamManager.LastPeerStreamId,
                        HTTP2ErrorCode.NO_ERROR,
                        "Server shutting down"
                    )
                );
            }
            catch
            {
                // Best-effort — connection may already be dead
            }

        }

        private async Task SendGoAwayAsync(HTTP2ErrorCode ErrorCode, string? DebugMessage = null)
        {

            if (goawaySent)
                return;

            goawaySent = true;

            try
            {
                await SendFrameAsync(
                    HTTP2Frame.CreateGoAway(
                        streamManager.LastPeerStreamId,
                        ErrorCode,
                        DebugMessage
                    )
                );

                await DrainForCloseAsync();
            }
            catch
            {
                // Best-effort — connection may already be dead
            }

        }

        /// <summary>
        /// After sending GOAWAY, briefly read and discard inbound data before the
        /// socket closes. Without this, a peer with in-flight frames (e.g. the tail
        /// of a flood we stopped reading) causes TCP to close with unread data and
        /// send a RST, which discards the GOAWAY we just wrote — the peer then sees
        /// only a broken connection, not the reason (RFC 9113, Section 6.8).
        /// Strictly bounded in time and volume so it cannot itself be abused.
        /// </summary>
        private async Task DrainForCloseAsync()
        {

            try
            {
                using var drainCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

                var buffer  = new byte[8192];
                var drained = 0;

                while (drained < 256 * 1024)
                {
                    var n = await transportStream.ReadAsync(buffer, drainCts.Token);
                    if (n == 0)
                        break;
                    drained += n;
                }
            }
            catch
            {
                // Timeout, cancellation, or closed socket — best effort
            }

        }

        #endregion

    }

}
