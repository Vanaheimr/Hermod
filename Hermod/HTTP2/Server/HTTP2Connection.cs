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
    using org.GraphDefined.Vanaheimr.Hermod.HTTP;
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

        /// <summary>
        /// Whether this server is authoritative for a given request
        /// <c>:authority</c> — null means "answer for anything", which is the only
        /// possible answer without a certificate to derive the origin set from
        /// (see <see cref="HTTPAuthority.ServedByCertificate"/> and RFC 9110,
        /// Section 15.5.20).
        /// </summary>
        private readonly Func<string, bool>?  isAuthorityServed;

        /// <summary>
        /// The origins announced to the client in an ORIGIN frame (RFC 8336), or
        /// null to announce nothing and leave the client to infer authority from
        /// the certificate as it always has.
        /// </summary>
        private readonly string[]?           originSet;

        /// <summary>
        /// Alternative services announced to the client in an ALTSVC frame (RFC
        /// 7838) — (origin, Alt-Svc field value) pairs — or null to announce none.
        /// </summary>
        private readonly (string Origin, string FieldValue)[]? alternativeServices;

        /// <summary>
        /// Whether a request an intermediary flagged <c>Early-Data: 1</c> (RFC 8470)
        /// may be processed anyway, or must be declined with <c>425</c>. Null uses
        /// <see cref="HTTP2EarlyData.IsSafeToProcess"/> — safe methods only — which
        /// is the whole point of the field; pass <c>_ =&gt; true</c> to accept the
        /// replay risk deliberately.
        /// </summary>
        private readonly Func<List<(string Name, string Value)>, bool> acceptEarlyData;
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
        /// transfers aren't throttled by the small default connection window. A
        /// multiple of the stream window, so that one stream whose reader has
        /// stalled cannot take all of it (see <see cref="HTTP2FlowControl"/>).
        /// </summary>
        private readonly Int32  connectionWindowSize;

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

        /// <summary>
        /// Default <see cref="maxRequestBodySize"/>: 16 MiB.
        /// </summary>
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
        /// The header block of a HEADERS frame on a stream that was reset, while
        /// CONTINUATION frames of it are still to come: kept only to be decoded
        /// once complete, and then dropped, or answered with STREAM_CLOSED if
        /// <see cref="discardedHeaderBlockAnswered"/>. Null otherwise.
        /// </summary>
        private MemoryStream?  discardedHeaderBlock;
        private bool           discardedHeaderBlockAnswered;

        /// <summary>
        /// Control frames that cost us work but make no request progress (non-ACK
        /// PING and SETTINGS). Reset whenever a HEADERS/DATA frame arrives. A
        /// sustained flood with no real requests is answered with ENHANCE_YOUR_CALM.
        /// </summary>
        private int         unproductiveFrames;

        /// <summary>
        /// Upper bound on CONTINUATION frames per single header block.
        /// </summary>
        private const int   MaxContinuationFrames  = 64;

        /// <summary>
        /// Upper bound on consecutive control frames without request progress.
        /// </summary>
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

        /// <summary>
        /// Slowloris/idle timeouts for this connection.
        /// </summary>
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
        /// <param name="ConnectionWindowSize">
        /// The connection-level receive window granted to the client: at least
        /// RFC 9113's 65 535 octets, four stream windows (4 MiB) by default. It
        /// bounds what the connection holds for handlers that have not read yet,
        /// and above a stream window it keeps one stalled stream from stopping
        /// all others (see <see cref="HTTP2FlowControl"/>).
        /// </param>
        public HTTP2Connection(
            Stream               TransportStream,
            HTTP2RequestHandler  RequestHandler,
            HTTP2ConnectHandler? ConnectHandler     = null,
            CancellationToken    CancellationToken  = default,
            System.Security.Cryptography.X509Certificates.X509Certificate2? ClientCertificate = null,
            HTTP2Timeouts?       Timeouts           = null,
            HTTP2StreamingHandler? StreamingHandler = null,
            long                 MaxRequestBodySize = DefaultMaxRequestBodySize,
            Func<string, bool>?  IsAuthorityServed  = null,
            string[]?            OriginSet          = null,
            (string Origin, string FieldValue)[]? AlternativeServices = null,
            Func<List<(string Name, string Value)>, bool>? AcceptEarlyData = null,
            Int32                ConnectionWindowSize = HTTP2FlowControl.DefaultConnectionWindowSize)
        {
            this.connectionWindowSize = HTTP2FlowControl.CheckConnectionWindowSize(ConnectionWindowSize, nameof(ConnectionWindowSize));
            this.isAuthorityServed   = IsAuthorityServed;
            this.originSet           = OriginSet;
            this.alternativeServices = AlternativeServices;
            this.acceptEarlyData     = AcceptEarlyData ?? HTTP2EarlyData.IsSafeToProcess;
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

            // The outer span: every request span below nests inside it, which is what
            // makes "this one slow request was on a connection that had 40 others"
            // visible in a trace. Null unless something is listening.
            using var connectionActivity = HTTP2Diagnostics.StartConnection(
                                               (transportStream as System.Net.Sockets.NetworkStream)?.Socket.RemoteEndPoint?.ToString(),
                                               "server"
                                           );

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
                HTTP2EventSource.Log.ConnectionError(ex.ErrorCode.ToString(), ex.Message);
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
                HTTP2EventSource.Log.ConnectionError(HTTP2ErrorCode.INTERNAL_ERROR.ToString(), ex.ToString());
                await SendGoAwayAsync(HTTP2ErrorCode.INTERNAL_ERROR, "Internal server error");
            }
            finally
            {
                // Abort any in-flight request handler / response tasks
                connectionCts.Cancel();

                if (writerTask is not null)
                {
                    // DataWriterLoopAsync handles its own failures, at once, and
                    // ends without throwing (see there) — but if its teardown
                    // itself ever throws, log that instead of silently discarding
                    // it, consistent with every other error path in this class.
                    try { await writerTask; }
                    catch (Exception ex) { HTTP2EventSource.Log.ConnectionError("WRITER_LOOP", ex.ToString()); }
                }

                // The connection is gone, and with it every stream still open on
                // it: reset them all (every stream not closed yet), as the peer's
                // RST_STREAM would. Their handlers were given the stream's token,
                // not this connection's, so the cancellation above does not reach
                // them. Without this, a handler waiting for request-body data, even
                // with its own token, or for a tunnel's next chunk, waited for good,
                // and one that checks its token ran on. The reset cancels that token,
                // fails body reads with it, ends tunnel reads and releases writers,
                // and it sends nothing.
                foreach (var stream in streamManager.GetSendableStreams())
                {
                    try
                    {
                        stream.Reset();
                    }
                    catch (Exception ex)
                    {
                        // A callback on the handler's token threw out of Cancel().
                        // The other streams still have to be released.
                        HTTP2EventSource.Log.HandlerFailed((int) stream.StreamId, "cancellation", ex.Message);
                    }
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
            var connectionBump = connectionWindowSize - streamManager.ConnectionRecvWindow;
            if (connectionBump > 0)
            {
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, (UInt32) connectionBump));
                streamManager.ConnectionRecvWindow += connectionBump;
            }

            // RFC 8336, Section 2.3: state the Origin Set as early as possible, so
            // the client knows what this connection is authoritative for before it
            // decides what to send over it. Only when the application actually
            // configured one — an ORIGIN frame listing nothing would assert that we
            // serve *no* origin, which is the opposite of saying nothing.
            if (originSet is not null && originSet.Length > 0)
                await SendFrameAsync(HTTP2Frame.CreateOrigin(originSet));

            // RFC 7838, Section 4: alternatives are advertised on stream 0 with the
            // origin named explicitly. Sent right after the preface for the same
            // reason as ORIGIN — a client that is going to act on this should learn
            // it before it decides what to do with the connection.
            if (alternativeServices is not null)
                foreach (var (origin, fieldValue) in alternativeServices)
                    await SendFrameAsync(HTTP2Frame.CreateAltSvc(origin, fieldValue));

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

        /// <summary>
        /// Send a DATA frame on the stream, unless the stream was reset: checked
        /// once the write lock is ours, as a header block checks its stream
        /// (<see cref="SendHeaderListAsync"/>). A reset of ours happens
        /// (<see cref="HTTP2Stream.Reset"/>) before its RST_STREAM takes the write
        /// lock, so the DATA goes out before the RST_STREAM, or not at all — never
        /// on a closed stream, which gets nothing but PRIORITY (RFC 9113, Section
        /// 5.1); nor after the peer's own RST_STREAM, once the read loop has
        /// handled it. Not at all means an OperationCanceledException that carries
        /// the stream's token.
        ///
        /// <paramref name="Completion"/>, the producer's task, is completed once
        /// the frame is sure to go out next: with the lock, before the write. So
        /// whatever the producer sends then, an RST_STREAM of a handler that fails
        /// right after its last write, waits for this frame to be out. Completed
        /// when the writer loop took the chunk, it let that RST_STREAM go first,
        /// and the DATA followed it onto the closed stream. And not after the
        /// write: a peer that closes the connection once it has the frame could
        /// then end the producer's wait, with the connection's token, first. It
        /// must run its continuations asynchronously, as the outbound queue's do,
        /// or they would run under the write lock.
        /// </summary>
        private async Task SendDataAsync(HTTP2Stream Stream, byte[] Chunk, bool EndStream, TaskCompletionSource? Completion = null)
        {

            var bytes = HTTP2Frame.CreateData(Stream.StreamId, Chunk, EndStream).Serialize();

            await writeLock.WaitAsync(cancellationToken);

            try
            {

                ThrowIfReset(Stream);

                Completion?.TrySetResult();

                await transportStream.WriteAsync(bytes, cancellationToken);
                await transportStream.FlushAsync(cancellationToken);

            }
            finally
            {
                writeLock.Release();
            }

        }

        /// <summary>
        /// Send a stream-level WINDOW_UPDATE, if the peer may still send DATA on the
        /// stream once the write lock is ours — see <see cref="MayStillSendData"/>.
        /// The window may have been counted up while it could, and the stream reset
        /// since, with its RST_STREAM on the wire already: a reset of ours happens
        /// (<see cref="HTTP2Stream.Reset"/>) before its RST_STREAM takes the write
        /// lock. So the WINDOW_UPDATE goes out before the RST_STREAM, or not at all
        /// — never on a closed stream, which gets nothing but PRIORITY (RFC 9113,
        /// Section 5.1).
        /// </summary>
        private async Task SendStreamWindowUpdateAsync(HTTP2Stream Stream, UInt32 Increment)
        {

            var bytes = HTTP2Frame.CreateWindowUpdate(Stream.StreamId, Increment).Serialize();

            await writeLock.WaitAsync(cancellationToken);

            try
            {

                if (!MayStillSendData(Stream))
                    return;

                await transportStream.WriteAsync(bytes, cancellationToken);
                await transportStream.FlushAsync(cancellationToken);

            }
            finally
            {
                writeLock.Release();
            }

        }

        /// <summary>
        /// Whether the peer may still send DATA on the stream: while it is open,
        /// or closed on our side only. Once the peer has ended its side, or the
        /// stream is closed or reset, its receive window matters no more (RFC 9113,
        /// Section 5.1): a stream-level WINDOW_UPDATE would tell the peer nothing,
        /// and once the stream is closed, it must not be sent at all.
        /// </summary>
        private static bool MayStillSendData(HTTP2Stream Stream)

            => Stream.State is HTTP2StreamState.Open
                            or HTTP2StreamState.HalfClosedLocal;

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
                        case HTTP2FrameType.RST_STREAM:      await HandleRstStreamAsync(frame);     break;
                        case HTTP2FrameType.GOAWAY:          HandleGoAway(frame);                   break;
                        case HTTP2FrameType.PRIORITY:        HandlePriority(frame);                 break;
                        case HTTP2FrameType.PRIORITY_UPDATE: HandlePriorityUpdate(frame);           break;
                        case HTTP2FrameType.ORIGIN:
                            // RFC 8336, Section 2.1: ORIGIN is server-to-client
                            // only, and servers MUST ignore it — not a protocol
                            // error, just nothing to do. Listed explicitly because
                            // we do know this frame type; falling into the
                            // unknown-type default below would read as an oversight.
                            break;

                        case HTTP2FrameType.ALTSVC:
                            // RFC 7838, Section 4: likewise server-to-client only —
                            // "an ALTSVC frame ... received by a server MUST be
                            // ignored". A client advertising alternatives to us is
                            // meaningless, not hostile.
                            break;

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
                catch (HTTP2StreamException ex) when (streamManager.DiscardsPeerFrames(ex.StreamId))
                {
                    // A stream error in a frame on a stream we reset while the peer
                    // could still send on it: the peer sent the frame before it read
                    // our RST_STREAM, and RFC 9113, Section 5.1 has it discarded (see
                    // HandleDataAsync). That RST_STREAM is the stream's answer, and it
                    // gets no other. Most such frames throw nothing; this is for one
                    // that passed its handler's checks while the stream was still
                    // open, and then met a reset of ours made on another task, a
                    // handler's or the writer loop's: the END_STREAM of a DATA frame,
                    // or of trailers, found the stream closed.
                }
                catch (HTTP2StreamException ex)
                {

                    HTTP2EventSource.Log.StreamError((int) ex.StreamId, ex.ErrorCode.ToString(), ex.Message);

                    // Reset first, then the RST_STREAM, as every other reset here
                    // does: a WINDOW_UPDATE for the stream, due on another task,
                    // must find it closed once the RST_STREAM is out (see
                    // SendStreamWindowUpdateAsync).
                    var stream = streamManager.TryGetStream(ex.StreamId);
                    stream?.Reset();

                    await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode));

                    if (stream is not null)
                        await ReturnUnreadWindowAsync(stream);

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
                            // The difference from the value the open streams were
                            // given: the RFC's 65535 until the peer first states
                            // this setting (RFC 9113, Section 6.5.2), not
                            // remoteSettings', which starts out with what we
                            // advertise ourselves.
                            var delta = (Int64) value - streamManager.PeerInitialWindowSize;
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

            var existingStream  = streamManager.TryGetStream(Frame.StreamId);

            // Read once, and every decision below is taken on this one read: a
            // reset of ours, made on another task, may land at any moment. One
            // that lands after it finds the block taken as trailers and decoded;
            // the end of the peer's side that trailers carry then finds the
            // stream reset, and FrameLoopAsync discards what that throws.
            var existingState   = existingStream?.State;

            // A header block on a stream that was reset is decoded all the same,
            // and only then dropped, or answered with a stream error of type
            // STREAM_CLOSED: the HPACK dynamic table is connection-wide state
            // (RFC 9113, Section 4.3, and see CompleteHeaders). The peer's encoder
            // has put the block's fields into its table already, and every later
            // block refers to the table past them.
            //
            // Dropped if we reset the stream while the peer could still send on
            // it: the peer sent the block before it read our RST_STREAM, and
            // Section 5.1 has such frames "minimally processed and then
            // discarded". So also once the stream is pruned, as long as its ID is
            // kept (see HTTP2StreamManager.DiscardsPeerFrames). Answered after the
            // peer's own reset, and after ours of a stream whose peer had ended its
            // side: the peer had nothing left to send there.
            if (streamManager.DiscardsPeerFrames(Frame.StreamId))
            {
                StartDiscardedHeaderBlock(Frame, AnswerWithStreamClosed: false);
                return;
            }

            if (existingStream is { WasReset: true } &&
                existingState  is not (HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal))
            {
                StartDiscardedHeaderBlock(Frame, AnswerWithStreamClosed: true);
                return;
            }

            // Real request traffic — reset the control-frame flood counter.
            unproductiveFrames = 0;

            var isTrailers     = existingStream is not null;

            HTTP2Stream stream;

            if (isTrailers)
            {

                stream = existingStream!;

                // Trailers (RFC 9113, Section 8.1) are only legal while we're
                // still expecting body/trailer data on this stream, i.e. the
                // original HEADERS did not set END_STREAM. RFC 9113, Section 5.1:
                // a HEADERS frame arriving after the peer sent END_STREAM (clean
                // close) is a *connection* error of type STREAM_CLOSED; after an
                // RST_STREAM close, see above.
                if (existingState is not (HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal))
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.STREAM_CLOSED,
                        $"HEADERS received on closed stream {Frame.StreamId}");

                // "Trailers MUST end the stream" is deliberately NOT checked here —
                // see CompleteHeaders. Rejecting the frame at this point would skip
                // the HPACK decode of a block the peer has already folded into its
                // encoder's dynamic table, and that table is connection-wide state.

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
            EnforceHeaderBufferLimit(stream.HeaderBuffer);

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

        /// <summary>
        /// Take in the header block of a HEADERS frame on a stream that was reset
        /// (see HandleHeaders), to be decoded once it is complete, here or with its
        /// last CONTINUATION frame, and then dropped — or answered with a stream
        /// error of type STREAM_CLOSED. The block keeps to the limits of any other,
        /// and is no request progress.
        /// </summary>
        private void StartDiscardedHeaderBlock(HTTP2Frame Frame, bool AnswerWithStreamClosed)
        {

            CountUnproductiveFrame();

            // The frame's layout is checked as for any HEADERS frame, but not the
            // priority it carries: there is no stream left to give it to.
            var headerData = StripPadding(Frame, Frame.Payload.AsSpan());

            if (Frame.HasPriority)
            {
                if (headerData.Length < 5)
                    throw new HTTP2StreamException(HTTP2ErrorCode.FRAME_SIZE_ERROR, Frame.StreamId,
                        "HEADERS with PRIORITY flag has insufficient data");

                headerData = headerData[5..];
            }

            discardedHeaderBlock          = new MemoryStream();
            discardedHeaderBlockAnswered  = AnswerWithStreamClosed;
            continuationFrameCount        = 0;
            discardedHeaderBlock.Write(headerData);
            EnforceHeaderBufferLimit(discardedHeaderBlock);

            if (Frame.EndHeaders)
                EndDiscardedHeaderBlock(Frame.StreamId);
            else
                continuationStreamId = Frame.StreamId;

        }

        /// <summary>
        /// Decode the discarded header block, now complete, and drop it — or answer
        /// it, as it was taken in to be.
        /// </summary>
        private void EndDiscardedHeaderBlock(UInt32 StreamId)
        {

            var headerBlock  = discardedHeaderBlock!.ToArray();
            var answered     = discardedHeaderBlockAnswered;

            discardedHeaderBlock.Dispose();
            discardedHeaderBlock          = null;
            discardedHeaderBlockAnswered  = false;

            hpackDecoder.DecodeHeaderBlock(headerBlock);

            if (answered)
                throw new HTTP2StreamException(HTTP2ErrorCode.STREAM_CLOSED, StreamId,
                    $"HEADERS received after RST_STREAM on stream {StreamId}");

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

            // Bound the number of fragments per header block: a peer that keeps
            // sending CONTINUATION frames (even empty ones) without END_HEADERS
            // would otherwise pin the connection and grow the buffer unbounded
            // (CVE-2024-27316 class).
            if (++continuationFrameCount > MaxContinuationFrames)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Too many CONTINUATION frames ({MaxContinuationFrames} max) for one header block");

            // The rest of a header block on a stream that was reset — see
            // StartDiscardedHeaderBlock. A pruned stream has no buffer of its own.
            if (discardedHeaderBlock is not null)
            {

                discardedHeaderBlock.Write(Frame.Payload);
                EnforceHeaderBufferLimit(discardedHeaderBlock);

                if (Frame.EndHeaders)
                {
                    continuationStreamId = null;
                    EndDiscardedHeaderBlock(Frame.StreamId);
                }

                return;

            }

            var stream = streamManager.TryGetStream(Frame.StreamId)
                ?? throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                       $"CONTINUATION for unknown stream {Frame.StreamId}");

            stream.HeaderBuffer!.Write(Frame.Payload);
            EnforceHeaderBufferLimit(stream.HeaderBuffer);

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
        private void EnforceHeaderBufferLimit(MemoryStream HeaderBuffer)
        {
            if (HeaderBuffer.Length > localSettings.MaxHeaderListSize)
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

                // RFC 9113, Section 8.1.1: a declared content-length must later
                // equal the summed DATA payload length. Parse it now; a
                // syntactically invalid or self-conflicting value is itself a
                // malformed request (a CONNECT tunnel has no such body semantics).
                if (!Stream.IsConnectTunnel)
                    Stream.ExpectedContentLength = ParseContentLength(Stream.StreamId, decoded);

            }
            else
            {

                // RFC 9113, Section 8.1: trailers are the last thing on the stream,
                // so they MUST end it. Checked here rather than on the frame itself,
                // and that placement is the whole point: the decode above has to
                // happen first, whatever then becomes of the stream.
                //
                // It used to be rejected earlier, in HandleHeaders, which meant this
                // block never reached the decoder while the peer HAD already added it
                // to its encoder's dynamic table. The two tables then differed by one
                // entry, and it was not this stream that paid for it but the NEXT
                // request on the connection, which died with
                //   COMPRESSION_ERROR: HPACK dynamic table index 63 out of range
                // — a connection error, for a stream error two requests earlier.
                if (!EndStream)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Stream.StreamId,
                        "Trailing HEADERS frame must set END_STREAM");

                ValidateTrailerHeaders(Stream.StreamId, decoded);

                // Trailers end the body, so it is here, and not at a DATA frame
                // with END_STREAM, that a declared content-length is compared
                // with it: on the streaming path as on the buffered one. Before
                // the trailers are stored, so that a malformed request passes
                // none to its handler.
                CheckContentLength(Stream);

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
                // "trailers" block — the check above already enforced END_STREAM
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
                    await SendHeaderListAsync(Stream, [(":status", "100")], EndStream: false);
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
                    // body) is malformed — reject before dispatching (Section 8.1.1).
                    if (EndStream && Stream.ExpectedContentLength is > 0)
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Stream.StreamId,
                            $"content-length {Stream.ExpectedContentLength} declared but no request body sent");

                    Stream.IsStreamingRequest = true;
                    Stream.RequestBodyChannel = Channel.CreateUnbounded<byte[]>();

                    // The 421 check (see DispatchRequestAsync) has to happen on this
                    // path too — a streaming handler is dispatched here, at
                    // HEADERS-complete, and would otherwise never pass through it.
                    // The stream bookkeeping above stays exactly as it is either way,
                    // so inbound DATA keeps being flow-control-accounted normally;
                    // only the task we start differs. The peer's own send window
                    // bounds what it can push into a body nobody reads.
                    // Both pre-dispatch refusals (421 and RFC 8470's 425) have to be
                    // repeated here for the same reason: a streaming handler is
                    // dispatched at HEADERS-complete and never passes through
                    // DispatchRequestAsync, where the buffered path checks them.
                    if (isAuthorityServed is not null && !IsAuthoritativeFor(decoded))
                        StartMisdirectedRequestResponse(Stream);
                    else if (IsTooEarly(decoded))
                        StartTooEarlyResponse(Stream);
                    else
                        StartStreamingHandler(Stream);
                }

                // Reached on the initial HEADERS (if END_STREAM: a bodyless request)
                // or on a later trailers block (which the check above requires to
                // set END_STREAM) — either way the peer's side is done, so end the body.
                if (EndStream)
                {
                    Stream.CloseRemote();
                    Stream.RequestBodyChannel!.Writer.TryComplete();
                }

                return;

            }

            if (EndStream)
            {
                // Ended by its initial header block, a request has no body: no DATA
                // frames will follow, so a non-zero declared content-length makes
                // it malformed (Section 8.1.1). Ended by trailers, it may have
                // one, and the trailers' branch above has compared its length.
                if (isInitialHeaders && Stream.ExpectedContentLength is > 0)
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
        /// Parse the request's <c>content-length</c> (RFC 9113, Section 8.1.1).
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
        /// RFC 9113, Section 8.1.1: a declared content-length MUST equal the
        /// summed DATA payload length, else the request is malformed. Checked
        /// where the body ends: at a DATA frame with END_STREAM, or at trailers.
        /// A body longer than declared does not get there: HandleDataAsync resets
        /// its stream at the DATA frame that takes it past. (A CONNECT tunnel is
        /// exempt — it has no body semantics.)
        /// </summary>
        private static void CheckContentLength(HTTP2Stream Stream)
        {

            if (Stream.IsConnectTunnel || Stream.ExpectedContentLength is not { } expected)
                return;

            var received = BodyLengthSoFar(Stream);

            if (received != expected)
                throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Stream.StreamId,
                    $"content-length {expected} does not match body length {received}");

        }

        /// <summary>
        /// The DATA payload a request's body has taken in so far: handed to its
        /// streaming handler, or buffered. What a declared content-length is
        /// compared with (RFC 9113, Section 8.1.1).
        /// </summary>
        private static long BodyLengthSoFar(HTTP2Stream Stream)

            => Stream.IsStreamingRequest
                   ? Stream.ReceivedBodyLength
                   : Stream.RequestBody?.Length ?? 0;

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

                // RFC 9113, Section 5.1: on a stream we reset while the peer could
                // still send on it, what the peer sent before it read our RST_STREAM
                // is "minimally processed and then discarded" — for DATA, the
                // accounting above. Not answered: each such frame drew a second
                // RST_STREAM, and an upload with a window's worth in flight when a
                // handler failed drew one for every frame of it. So also once the
                // stream is pruned, as long as its ID is kept (see
                // HTTP2StreamManager.DiscardsPeerFrames).
                if (streamManager.DiscardsPeerFrames(Frame.StreamId))
                    return;

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

            // RFC 9113, Section 8.1.1: DATA that add up to more than a declared
            // content-length make the request malformed, and that is certain at
            // the frame that takes them past it: no later frame brings the sum
            // back. So the stream is reset here, before any of this frame reaches
            // a streaming handler or the buffered body — a streaming handler used
            // to read every byte past the declared length, and a buffered body was
            // taken in up to maxRequestBodySize, only to be refused where it ended.
            // CheckContentLength, where the body ends, is left with one cut short.
            // The frame is counted against both windows above, and its connection
            // window given back now, as for the other stream errors here (Section
            // 6.9); what the handler left unread of the DATA before it, the reset
            // gives back (see FrameLoopAsync).
            if (!stream.IsConnectTunnel && stream.ExpectedContentLength is { } declared)
            {

                var received = BodyLengthSoFar(stream) + dataLength;

                if (received > declared)
                {
                    await ReplenishReceiveWindowsAsync(null, flowLength);
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Frame.StreamId,
                        $"content-length {declared} exceeded: {received} bytes of DATA received");
                }

            }

            // A CONNECT tunnel and a streaming request have an incremental consumer
            // (the handler's tunnel/body ReadAsync), so their flow-control window is
            // returned as the handler CONSUMES each chunk (ReplenishConsumedAsync),
            // NOT on receipt. This is real backpressure: a slow consumer leaves the
            // window depleted, so the peer is forced to stop sending — and because
            // the window is thus never replenished ahead of consumption, the inbound
            // channel can hold at most a window's worth (per-stream + connection)
            // regardless of how fast the peer sends. Once the stream is reset, what
            // is still unread has its connection window returned all at once
            // (ReturnUnreadWindowAsync). A buffered request has no such consumer (the
            // whole body is handed over at END_STREAM), so it is replenished on
            // receipt and bounded instead by maxRequestBodySize.
            if (stream.IsConnectTunnel)
            {
                if (dataLength > 0)
                    await HandToReaderAsync(stream, stream.TunnelInbound!, payload.ToArray());

                // The discarded padding is returned now; the data the channel took
                // waits for the tunnel consumer (Section 6.1 padding still owed
                // regardless). Without padding too: the connection window owed for
                // what was read goes back, should this DATA have left the client
                // no more than that (see ReplenishReceiveWindowsAsync).
                await ReplenishReceiveWindowsAsync(stream, paddingOverhead);
            }
            else if (stream.IsStreamingRequest)
            {
                // Hand the body chunk to the streaming handler as it arrives (no
                // buffering); track the running length for the content-length check.
                stream.ReceivedBodyLength += dataLength;

                if (dataLength > 0)
                    await HandToReaderAsync(stream, stream.RequestBodyChannel!, payload.ToArray());

                // As for a tunnel: the padding, and what is owed, if due.
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

                // The body ends with this frame, so its length is compared with a
                // declared content-length now, as it is where trailers end a body
                // (see CompleteHeaders).
                CheckContentLength(stream);

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
        /// returned — as it is for a stream the peer can send no more DATA on
        /// (<see cref="MayStillSendData"/>).
        ///
        /// The connection's window is given back as well once the peer has no
        /// more of it left than is owed. All streams draw on it, and one whose
        /// handler does not read keeps the window of what it was sent: while
        /// such streams held more than half of it, half of it never came to be
        /// owed, so the window of what the others' handlers read was not given
        /// back — the peer could send them nothing more, and they waited for good.
        /// DATA that nobody reads can leave the peer that short as well as DATA
        /// that is read, so this is called for every DATA frame, with nothing
        /// owed anew (<paramref name="DataLength"/> 0) for one whose bytes went to
        /// a reader.
        /// </summary>
        private async Task ReplenishReceiveWindowsAsync(HTTP2Stream? Stream, int DataLength)
        {

            // Decide what (if anything) to emit and apply the local window bookkeeping
            // under recvLock — but do NOT send while holding it (SendFrameAsync is
            // async and takes writeLock). The local RecvWindow/ConnectionRecvWindow
            // are updated here to reflect what we're about to grant; the frames go
            // out below, after the lock is released.
            UInt32 streamInc = 0, connInc = 0;

            lock (recvLock)
            {

                // The stream's own window only while the peer may still send DATA
                // on it — and SendStreamWindowUpdateAsync asks again once the write
                // lock is its own: the stream may have been reset meanwhile. And
                // only for what is owed anew: unread DATA on a stream holds up no
                // reader but the stream's own, which gives its window back by
                // reading it, so there is nothing to check for it here.
                if (DataLength > 0 && Stream is not null && MayStillSendData(Stream))
                {
                    Stream.PendingRecvUpdate += DataLength;
                    if (Stream.PendingRecvUpdate >= localSettings.InitialWindowSize / 2)
                    {
                        streamInc                 = (UInt32) Stream.PendingRecvUpdate;
                        Stream.RecvWindow        += streamInc;
                        Stream.PendingRecvUpdate  = 0;
                    }
                }

                // The connection's once half of it is owed, or once the peer has no
                // more of it left than that: what it has left is the window that is
                // neither owed nor taken by DATA still unread.
                connectionPendingRecvUpdate += DataLength;
                if (connectionPendingRecvUpdate > 0 &&
                   (connectionPendingRecvUpdate >= connectionWindowSize / 2 ||
                    connectionPendingRecvUpdate >= streamManager.ConnectionRecvWindow))
                {
                    connInc                             = (UInt32) connectionPendingRecvUpdate;
                    streamManager.ConnectionRecvWindow += connInc;
                    connectionPendingRecvUpdate         = 0;
                }

            }

            if (streamInc > 0)
                await SendStreamWindowUpdateAsync(Stream!, streamInc);
            if (connInc > 0)
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, connInc));

        }

        /// <summary>
        /// Return flow-control window for body/tunnel bytes a streaming or CONNECT
        /// handler has just CONSUMED (read off its inbound channel) — the demand
        /// signal that drives consumption-based backpressure. Called from handler
        /// tasks (via <see cref="HTTP2RequestStream"/> / <see cref="HTTP2Tunnel"/>),
        /// so it runs concurrently with the read loop's decrement; both go through
        /// the recvLock-guarded <see cref="ReplenishReceiveWindowsAsync"/>. Bytes
        /// read once nothing was to read them any more return nothing: after a
        /// reset, or by a task a handler left behind when it ended, their window
        /// was returned then already (<see cref="ReturnUnreadWindowAsync"/>).
        /// </summary>
        internal Task ReplenishConsumedAsync(HTTP2Stream Stream, int Count)

            => TryReleaseWithheldWindow(Stream, Count)
                   ? ReplenishReceiveWindowsAsync(Stream, Count)
                   : Task.CompletedTask;

        /// <summary>
        /// Hand a chunk of DATA to the handler that reads the stream, a streaming
        /// request's body or a tunnel, and withhold its window until the handler
        /// reads it (<see cref="ReplenishConsumedAsync"/>).
        ///
        /// TryWrite: the channel is unbounded, so a write fails only on a completed
        /// channel, and past HandleDataAsync's state check that means a reset, or
        /// the end of the stream's handler (<see cref="EndReadingAsync"/>). Both
        /// complete the channel, on another task, a handler's or the writer loop's,
        /// and can land between that check and this write. WriteAsync would then
        /// throw into the read loop — the reset's OperationCanceledException for a
        /// body, ChannelClosedException for a tunnel — and end the connection.
        /// Nobody will read the chunk now: it is dropped, and its window given back
        /// to the connection at once, as for DATA on a closed stream (Section 6.9)
        /// — unless it was given back already, with the rest of what was unread
        /// (<see cref="ReturnUnreadWindowAsync"/>). A chunk that comes once that is
        /// done is dropped without being withheld at all, and never written: the
        /// channel would grow with every chunk, as their windows are given back.
        /// </summary>
        private async Task HandToReaderAsync(HTTP2Stream Stream, Channel<byte[]> Channel, byte[] Chunk)
        {

            if (TryWithholdWindow(Stream, Chunk.Length))
            {

                if (Channel.Writer.TryWrite(Chunk))
                    return;

                if (!TryReleaseWithheldWindow(Stream, Chunk.Length))
                    return;

            }

            await ReplenishReceiveWindowsAsync(null, Chunk.Length);

        }

        /// <summary>
        /// Count bytes about to be handed to the stream's reader as unread, so that
        /// their window is withheld until they are read — or return false once the
        /// window of what was unread has been returned, after a reset or at the end
        /// of the stream's handler: from then on, nothing is withheld on the stream.
        /// </summary>
        private bool TryWithholdWindow(HTTP2Stream Stream, int Count)
        {

            lock (recvLock)
            {

                if (Stream.UnreadWindowReturned)
                    return false;

                Stream.UnreadRecvBytes += Count;

                return true;

            }

        }

        /// <summary>
        /// Take bytes that were read, or dropped, off the stream's unread count, and
        /// return true when their window is still to be given back — false when it
        /// was given back already, with the rest of what was unread, after a reset
        /// or at the end of the stream's handler. Under the same lock as that
        /// return's count, so that whichever comes first, the window of each byte
        /// is given back once.
        /// </summary>
        private bool TryReleaseWithheldWindow(HTTP2Stream Stream, int Count)
        {

            lock (recvLock)
            {

                if (Stream.UnreadWindowReturned)
                    return false;

                Stream.UnreadRecvBytes -= Count;

                return true;

            }

        }

        /// <summary>
        /// Give back the connection window of every DATA byte still unread on a
        /// stream that nothing will read any more: one that was reset, or whose
        /// handler has ended, or none was started (<see cref="EndReadingAsync"/>).
        /// That window is withheld until the handler reads the bytes, and after a
        /// reset a handler that honours its token, or has failed, never does, nor
        /// does one that has ended: the connection lost that much of its window
        /// for good, and with a whole window's worth unread, every later upload on
        /// it stalled. The bytes stay readable, in order, as before, but a read of
        /// them gives nothing back a second time (<see cref="ReplenishConsumedAsync"/>),
        /// and a chunk that arrives afterwards is dropped, its window given back at
        /// once (<see cref="HandToReaderAsync"/>). Only the connection's window: the
        /// stream's matters no more.
        ///
        /// Called once a reset is made, after its RST_STREAM, sent or received, and
        /// at the end of the reading — not from HTTP2Stream.Reset, which the client
        /// shares, which cannot await the WINDOW_UPDATE this may send, and which the
        /// connection's teardown calls where nothing can be sent any more. Called
        /// again, it gives back nothing.
        /// </summary>
        private Task ReturnUnreadWindowAsync(HTTP2Stream Stream)
        {

            Int64 unread;

            lock (recvLock)
            {

                if (Stream.UnreadWindowReturned)
                    return Task.CompletedTask;

                Stream.UnreadWindowReturned  = true;
                unread                       = Stream.UnreadRecvBytes;
                Stream.UnreadRecvBytes       = 0;

            }

            // No more than the stream's receive window: at most 2^31-1 bytes
            // (RFC 9113, Section 6.9.1).
            return ReplenishReceiveWindowsAsync(null, (int) unread);

        }

        /// <summary>
        /// Nothing reads what the client sends on this stream any more: its handler
        /// has ended — returned, failed or cancelled — or none was started, for a
        /// streaming request refused with 421 or 425, or a CONNECT that was refused.
        /// A reset gives back the window of what was left unread
        /// (<see cref="ReturnUnreadWindowAsync"/>), but a stream can end without one:
        /// the handler answers before the upload is over, and returns. What it left
        /// unread, and every chunk that came after, withheld its window for good,
        /// and with a whole window's worth, every later upload on the connection
        /// stalled. So the same is done here: the window of what is unread is given
        /// back now, once, and nothing is withheld on the stream any more.
        ///
        /// The channel is completed first: nothing is added to it any more, and a
        /// read that comes after what is in it ends. A body's with a failure, as a
        /// reset completes it: completed without one, once the client ends its
        /// side, it would pass the body off as whole, although chunks were dropped
        /// since — to a task the handler left behind to read it, say. A body the
        /// client has ended already stays whole: TryComplete leaves a completed
        /// channel as it is. Not with an OperationCanceledException: the stream's
        /// token has not been cancelled, unless a reset follows. A tunnel's channel
        /// ends, as a reset ends it.
        ///
        /// Then the client is asked to stop sending, once the response is complete
        /// (<see cref="StopUnreadUploadAsync"/>). Called only once whatever else is
        /// to become of the stream has become of it: a handler that fails is
        /// answered with a 500, or a reset of its own, first.
        ///
        /// Best-effort, as a reset's return is: only the end of the connection can
        /// fail what this sends, and nothing needs its window after that. Called
        /// again, it does nothing more.
        /// </summary>
        private async Task EndReadingAsync(HTTP2Stream Stream)
        {

            Stream.RequestBodyChannel?.Writer.TryComplete(new InvalidOperationException($"Nothing reads the request body of stream {Stream.StreamId} any more"));
            Stream.TunnelInbound?.     Writer.TryComplete();

            try
            {
                await ReturnUnreadWindowAsync(Stream);
                await StopUnreadUploadAsync(Stream);
            }
            catch
            {
                // The connection is ending — nothing left to give back.
            }

        }

        /// <summary>
        /// Ask the client to stop sending what nothing reads, with RST_STREAM
        /// NO_ERROR: once the response is complete, its END_STREAM on the wire,
        /// while the client's side of the stream is still open, and once nothing
        /// reads what it sends there any more (<see cref="EndReadingAsync"/>). RFC
        /// 9113, Section 8.1 allows a server exactly that, and has the client keep
        /// the response. Without it, the client goes on sending into chunks that
        /// are dropped: their connection window is given back, but not the stream's,
        /// which drains until the upload hangs, and the stream counts as open
        /// until the connection ends.
        ///
        /// The two come on two tasks, in either order: the end of the reading, and
        /// the writer loop's END_STREAM, after which it calls this for the stream.
        /// Each marks its own, and then looks for the other's — the end of the
        /// reading under recvLock, the END_STREAM in the stream's state, under the
        /// stream's lock — so whichever comes second finds both. The stream is
        /// reset only from half-closed (local), tested and changed under the
        /// stream's lock (<see cref="HTTP2Stream.TryResetHalfClosedLocal"/>). So one
        /// RST_STREAM goes out at most; none once the client has ended its side as
        /// well, or the stream was reset otherwise; and none before the END_STREAM,
        /// which half-closed (local) comes after. Reset first, then the RST_STREAM,
        /// as every other reset here (see SendStreamWindowUpdateAsync).
        /// </summary>
        private async Task StopUnreadUploadAsync(HTTP2Stream Stream)
        {

            bool nothingReads;

            // Set after a reset as well, but then the stream is closed, and is not
            // reset again below.
            lock (recvLock)
                nothingReads = Stream.UnreadWindowReturned;

            if (!nothingReads || !Stream.TryResetHalfClosedLocal())
                return;

            await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.NO_ERROR));

        }

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
        /// window/priority change. Returns once the data has actually been sent
        /// — mirroring the old direct-send loop this replaced, which likewise
        /// only returned once bytes were actually on the wire or the send was
        /// abandoned. Sent means sure to go out next, with the write lock the
        /// writer loop's for its last frame (<see cref="SendDataAsync"/>), not
        /// merely taken off the queue: a caller that goes on to reset the stream
        /// — a handler that fails right after its last write — resets it after
        /// that DATA, and its RST_STREAM cannot overtake it.
        ///
        /// It is abandoned once the stream is closed, whether before this call
        /// or while the data waits (<see cref="HTTP2OutboundQueue.AbandonAll"/>),
        /// or once a reset keeps a chunk the writer loop has taken from going out
        /// (<see cref="SendDataAsync"/>), and the returned task is canceled then,
        /// at once: for a reset, with the stream's own
        /// <see cref="HTTP2Stream.CancellationToken"/>, the token its handler was
        /// given — as the rest of the connection treats a reset, the write ends
        /// the handler as the handler's own check of that token would.
        /// The task is canceled as well once the connection is tearing down (this
        /// connection's own token), and once <paramref name="CancellationToken"/>,
        /// the caller's, is. That one ends only the wait: the data stays queued,
        /// and still goes out in order.
        /// </summary>
        internal Task EnqueueOutboundAsync(HTTP2Stream Stream, byte[] Data, bool EndStream, List<(string Name, string Value)>? Trailers = null, CancellationToken CancellationToken = default)
        {

            var completion = Stream.OutboundQueue.EnqueueAsync(Data, EndStream, Trailers);

            SignalWriterWakeup();

            // One wait per token rather than one on a linked token: the
            // cancellation then carries the token that fired, and the caller can
            // tell its own from the connection's.
            var sent = completion.WaitAsync(cancellationToken);

            return CancellationToken.CanBeCanceled
                       ? sent.WaitAsync(CancellationToken)
                       : sent;

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
        ///
        /// Nothing but the connection's cancellation may end it early, since
        /// every body on the connection goes out through it. Its failures are
        /// handled the way the read loop's are: a stream error resets that one
        /// stream and the loop goes on; anything else ends the connection at
        /// once, with GOAWAY INTERNAL_ERROR.
        ///
        /// A chunk it has taken goes out only if its stream has not been reset
        /// by the time the write lock is the loop's (<see cref="SendDataAsync"/>),
        /// and its producer learns how its write went only then.
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

                    var (chunk, endStream, trailers, completion) = taken.Value;

                    if (chunk.Length < reserved)
                        lock (flowLock)
                        {
                            var giveBack = reserved - chunk.Length;
                            stream.SendWindow                  += giveBack;
                            streamManager.ConnectionSendWindow += giveBack;
                        }

                    stream.LastServedSequence = Interlocked.Increment(ref writerSequence);

                    // The window taken for the chunk is spent only once its DATA
                    // frame goes out.
                    var unsent = chunk.Length;

                    try
                    {

                        // The producer's task goes to the write of the chunk's last
                        // frame, which completes it once that frame is sure to go out
                        // next (see SendDataAsync).
                        if (trailers is not null)
                        {
                            // A response with trailers: the last DATA (if any) must NOT
                            // carry END_STREAM — the trailing HEADERS block does (RFC
                            // 9113, Section 8.1). Both go out here, in order, with the
                            // trailers HPACK-encoded under the write lock.
                            if (chunk.Length > 0)
                                await SendDataAsync(stream, chunk, EndStream: false);

                            unsent = 0;

                            await SendHeaderListAsync(stream, trailers, EndStream: true, completion);
                            CloseLocalIfNotReset(stream);
                        }
                        else
                        {
                            if (chunk.Length > 0 || endStream)
                                await SendDataAsync(stream, chunk, EndStream: endStream, completion);
                            else
                                completion?.TrySetResult();

                            unsent = 0;

                            if (endStream)
                                CloseLocalIfNotReset(stream);
                        }

                        // Our side has ended, its END_STREAM on the wire. If nothing
                        // reads what the client may still send, a body or a tunnel,
                        // ask it to stop.
                        if (endStream && (stream.IsStreamingRequest || stream.IsConnectTunnel))
                            await StopUnreadUploadAsync(stream);

                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == stream.CancellationToken)
                    {
                        // The stream was reset once the chunk had been taken off its
                        // queue, before its DATA or trailers got the write lock: they
                        // stay unsent, as nothing more goes out on a reset stream (see
                        // SendDataAsync, SendHeaderListAsync). No failure of this
                        // loop's — the other streams' bodies go on.
                    }
                    catch (HTTP2StreamException ex)
                    {

                        // A stream error is confined to its stream (RFC 9113, Section
                        // 5.4.2), and must not end the loop that sends every other
                        // stream's body. This loop reads nothing from the peer, so
                        // the error is ours — an END_STREAM sent twice, say — and the
                        // stream is reset with INTERNAL_ERROR, not with the code the
                        // exception carries; the log keeps both code and message.
                        // Reset also releases the stream's producers and frees its
                        // slot under MAX_CONCURRENT_STREAMS, and what its handler
                        // left unread gives its window back.
                        HTTP2EventSource.Log.StreamError((int) ex.StreamId, ex.ErrorCode.ToString(), ex.Message);

                        stream.Reset();
                        await SendFrameAsync(HTTP2Frame.CreateRstStream(stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR));
                        await ReturnUnreadWindowAsync(stream);

                    }
                    finally
                    {

                        // DATA that did not go out spent nothing of the peer's
                        // windows: the connection's is ours to use again. The
                        // loop's next pick sees it, before any wait, as it sees the
                        // window given back above: no wakeup needed.
                        if (unsent > 0)
                            lock (flowLock)
                            {
                                stream.SendWindow                  += unsent;
                                streamManager.ConnectionSendWindow += unsent;
                            }

                        // A producer whose last frame did not go out learns it here;
                        // one whose frame did was told so as it went. Canceled with
                        // the stream's token if the stream was reset, as every write
                        // on a reset stream is, or else with the connection's, which
                        // is ending.
                        completion?.TrySetCanceled(stream.WasReset ? stream.CancellationToken : cancellationToken);

                    }

                }

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Connection shutting down — normal.
            }
            catch (Exception ex)
            {

                // Anything else leaves the connection without its writer, while the
                // read loop would go on accepting requests: their HEADERS would
                // still go out, sent by their own tasks, but no body ever would,
                // and their streams would stay open until the connection ended. So
                // end it now: GOAWAY for the peer, then the cancellation that stops
                // the read loop. Write-only, as in EnforceSettingsAckTimeoutAsync —
                // the read loop is still the transport's reader, and a drain here
                // would race it.
                HTTP2EventSource.Log.ConnectionError("WRITER_LOOP", ex.ToString());

                if (!goawaySent)
                {
                    goawaySent = true;
                    try
                    {
                        await SendFrameAsync(HTTP2Frame.CreateGoAway(
                            streamManager.LastPeerStreamId, HTTP2ErrorCode.INTERNAL_ERROR,
                            "Internal server error"));
                    }
                    catch { /* best-effort — the transport may be what failed */ }
                }

                connectionCts.Cancel();

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

        private async Task HandleRstStreamAsync(HTTP2Frame Frame)
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
                HTTP2EventSource.Log.StreamResetByPeer((int) Frame.StreamId, errorCode.ToString());
                stream.ResetByPeer();

                // Wake a response task possibly waiting for window space on this
                // stream, so it can notice the reset and abort.
                SignalWriterWakeup();

                // An upload the peer cancels leaves unread what arrived: give its
                // window back, or every later upload on the connection stalls.
                await ReturnUnreadWindowAsync(stream);

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

            HTTP2EventSource.Log.GoAwayReceived((int) lastStreamId, errorCode.ToString(), debugData);

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
                    HTTP2EventSource.Log.HandlerCancelled((int) Stream.StreamId, "request");
                }
                catch (HTTP2StreamException ex)
                {
                    HTTP2EventSource.Log.StreamError((int) ex.StreamId, ex.ErrorCode.ToString(), ex.Message);
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                }
                catch (Exception ex)
                {
                    HTTP2EventSource.Log.HandlerFailed((int) Stream.StreamId, "response", ex.Message);
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR)); } catch { }
                }

            }, CancellationToken.None);

        }

        /// <summary>
        /// Whether an OperationCanceledException out of a handler's task is the
        /// handler's cancellation: the stream's reset — the client's RST_STREAM,
        /// or one of ours — or the connection's end, whose token is cancelled
        /// before the teardown resets the streams still open (see
        /// <see cref="RunAsync"/>). Nobody waits for an answer then. Any other is
        /// a failure like any other — a timeout of the handler's own, say — and
        /// is answered as one. Taken for a cancellation, it was only reported:
        /// the stream got neither a response nor an RST_STREAM, and stayed open,
        /// counted against MAX_CONCURRENT_STREAMS, until the connection ended.
        /// </summary>
        private bool IsCancelled(HTTP2Stream Stream)

            => Stream.WasReset ||
               cancellationToken.IsCancellationRequested;

        /// <summary>
        /// Run a streaming request handler on its own task (mirroring
        /// <see cref="StartRequestHandler"/>): it reads the request body from a
        /// channel and writes the response incrementally, both concurrently. A
        /// handler that returns without ending the response auto-completes; one that
        /// throws before sending headers falls back to a 500, or otherwise resets
        /// the stream (a partial response can't be turned into an error status) —
        /// unless it had completed its response, which stands. A cancellation of
        /// the handler's own is such a failure too (<see cref="IsCancelled"/>).
        /// Once the handler has ended, nothing reads the request body any more
        /// (<see cref="EndReadingAsync"/>).
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

                    // Returned: nothing reads the body any more. Before the response
                    // is ended, which may wait for the client's window as long as the
                    // client likes; the reading is over all the same.
                    await EndReadingAsync(Stream);

                    await response.EnsureCompletedAsync();

                }
                catch (OperationCanceledException) when (IsCancelled(Stream))
                {
                    HTTP2EventSource.Log.HandlerCancelled((int) Stream.StreamId, "streaming");
                }
                catch (HTTP2StreamException ex)
                {
                    HTTP2EventSource.Log.StreamError((int) ex.StreamId, ex.ErrorCode.ToString(), ex.Message);
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                    try { await ReturnUnreadWindowAsync(Stream); } catch { }
                }
                catch (Exception ex)
                {
                    // Any other failure, a cancellation of the handler's own included.
                    HTTP2EventSource.Log.HandlerFailed((int) Stream.StreamId, "streaming", ex.Message);

                    // A reset stream is answered no more: nobody reads a 500 there,
                    // its body could not be written, and an RST_STREAM of ours
                    // would reset the stream a second time.
                    if (Stream.WasReset)
                        return;

                    // Nor is a response the handler completed: it stands. Its
                    // END_STREAM is queued, to go out after all that was written
                    // before it, and an RST_STREAM of ours would follow it — on a
                    // stream both sides had ended, where RFC 9113, Section 5.1
                    // allows nothing but PRIORITY, or, while the client still
                    // sends, with a code for which a client may discard a complete
                    // response (Section 8.1). The end of the reading below stops
                    // the upload with NO_ERROR instead, as for a handler that
                    // returns.
                    if (response.Completed)
                        return;

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
                    try { await ReturnUnreadWindowAsync(Stream); } catch { }
                }
                finally
                {
                    // Failed or cancelled, the handler reads nothing more either. Only
                    // now, once a failure is answered: with a 500, a response like any
                    // other, or with a reset, which asks the client to stop sending in
                    // its own way. A complete response — the 500, or the handler's own,
                    // which stands although the handler failed after it — asks with
                    // RST_STREAM NO_ERROR, once its END_STREAM is out, if the client
                    // is still sending then.
                    await EndReadingAsync(Stream);
                }

            }, CancellationToken.None);

        }

        /// <summary>
        /// Whether <see cref="isAuthorityServed"/> accepts the origin this request
        /// names. The origin is <c>:authority</c>, falling back to <c>host</c> for a
        /// request translated from HTTP/1.1 (RFC 9113, Section 8.3.1). A request
        /// that names no origin at all cannot be misdirected, so it passes.
        /// </summary>
        /// <summary>
        /// Answer 421 (Misdirected Request): we are not authoritative for the origin
        /// this stream named, and the client should try a connection of its own.
        /// </summary>
        private Task SendMisdirectedRequestAsync(HTTP2Stream Stream)

            => SendResponseAsync(
                   Stream,
                   [(":status", "421"), ("content-type", "text/plain")],
                   Encoding.UTF8.GetBytes("Misdirected Request")
               );

        /// <summary>
        /// The fire-and-forget form, for the streaming path where the decision is
        /// made inside the frame read loop: the read loop must not block on a
        /// response's flow control. No handler is started, so nothing reads the
        /// request body, from the start (<see cref="EndReadingAsync"/>).
        /// </summary>
        private void StartMisdirectedRequestResponse(HTTP2Stream Stream)

            => _ = Task.Run(async () => {
                       await EndReadingAsync(Stream);
                       try
                       {
                           await SendMisdirectedRequestAsync(Stream);
                       }
                       catch
                       {
                           // The stream may have been reset, or the connection torn
                           // down, while we were declining it — nothing left to say.
                       }
                   }, CancellationToken.None);

        /// <summary>
        /// Whether this request must be declined with <c>425</c> (RFC 8470): it
        /// carries <c>Early-Data: 1</c> and the policy will not take the replay risk.
        /// A request without the field is not early data as far as we can tell, and
        /// is never refused on these grounds — we terminate no early data ourselves
        /// (<see cref="HTTP2EarlyData"/>), so the field is the only evidence there is.
        /// </summary>
        private bool IsTooEarly(List<(string Name, string Value)> RequestHeaders)

            => HTTP2EarlyData.IsFlagged(RequestHeaders) &&
               !acceptEarlyData(RequestHeaders);

        /// <summary>
        /// Answer 425 (Too Early): we will not risk processing a request that might
        /// be a replay. RFC 8470, Section 5.2 — the client may repeat it once the
        /// handshake has completed, and the response is not cacheable, so a stale
        /// refusal cannot outlive the reason for it.
        /// </summary>
        private Task SendTooEarlyAsync(HTTP2Stream Stream)

            => SendResponseAsync(
                   Stream,
                   [(":status", "425"), ("content-type", "text/plain"), ("cache-control", "no-store")],
                   Encoding.UTF8.GetBytes("Too Early")
               );

        /// <summary>
        /// The fire-and-forget form, for the streaming path — the frame read loop
        /// must not block on a response's flow control. No handler is started, so
        /// nothing reads the request body, from the start
        /// (<see cref="EndReadingAsync"/>).
        /// </summary>
        private void StartTooEarlyResponse(HTTP2Stream Stream)

            => _ = Task.Run(async () => {
                       await EndReadingAsync(Stream);
                       try
                       {
                           await SendTooEarlyAsync(Stream);
                       }
                       catch
                       {
                           // The stream may have been reset, or the connection torn
                           // down, while we were declining it — nothing left to say.
                       }
                   }, CancellationToken.None);

        private bool IsAuthoritativeFor(List<(string Name, string Value)> RequestHeaders)
        {

            var authority = RequestHeaders.FirstOrDefault(header => header.Name == ":authority").Value ??
                            RequestHeaders.FirstOrDefault(header => header.Name == "host").      Value;

            return string.IsNullOrEmpty(authority) ||
                   isAuthorityServed!(authority);

        }

        /// <summary>
        /// Invoke the application-level request handler and send back the HTTP/2 response.
        /// </summary>
        private async Task DispatchRequestAsync(HTTP2Stream Stream)
        {

            if (Stream.RequestHeaders is null)
                return;

            // RFC 9113, Section 9.1.1 / RFC 9110, Section 15.5.20: the origin this
            // request names need not be the one the client dialed — a client may
            // reuse ("coalesce") this connection for any origin our certificate
            // covers. If we are not authoritative for it, decline with 421 so the
            // client retries on a connection to the right server, rather than
            // receiving an answer from the wrong one.
            //
            // A stream-level answer, not a connection error: other streams on this
            // connection may well name an origin we do serve.
            if (isAuthorityServed is not null && !IsAuthoritativeFor(Stream.RequestHeaders))
            {
                await SendMisdirectedRequestAsync(Stream);
                return;
            }

            // RFC 8470, Section 5.2: an intermediary told us this request came out of
            // its early data, so it may be a replay of one we have already run. Unless
            // the policy says it is harmless to repeat, decline it — the client can
            // send it again once the handshake in front of us has completed.
            if (IsTooEarly(Stream.RequestHeaders))
            {
                await SendTooEarlyAsync(Stream);
                return;
            }

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

            // One span per request, nested inside the connection's. Null unless
            // something is listening, and disposed on every path out of here.
            var method = HTTPMethod.TryParse(requestHeaders.FirstOrDefault(header => header.Name == ":method").Value);
            var path   = requestHeaders.FirstOrDefault(header => header.Name == ":path").  Value;

            using var activity = HTTP2Diagnostics.StartRequest(
                                     method,
                                     URIScheme.TryParse(requestHeaders.FirstOrDefault(header => header.Name == ":scheme").Value),
                                     requestHeaders.FirstOrDefault(header => header.Name == ":authority").Value,
                                     path,
                                     Stream.StreamId,
                                     "server"
                                 );

            try
            {

                var (responseHeaders, responseBody) = await requestHandler(
                    Stream.StreamId,
                    requestHeaders,
                    body,
                    Stream.CancellationToken
                );

                await SendResponseAsync(Stream, responseHeaders, responseBody);

                var status = Int32.TryParse(responseHeaders.FirstOrDefault(header => header.Name == ":status").Value, out var parsed)
                                 ? parsed
                                 : 0;

                HTTP2Diagnostics.Complete(activity, status);
                HTTP2EventSource.Log.RequestHandled((int) Stream.StreamId, method?.ToString() ?? "?", path ?? "?", status);

            }
            catch (OperationCanceledException) when (IsCancelled(Stream))
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
                // Any other failure, a cancellation of the handler's own included.
                HTTP2EventSource.Log.HandlerFailed((int) Stream.StreamId, "request", ex.Message);

                // Not on a reset stream either, for the reason above: a handler
                // that failed after the reset is reported, and that is all.
                if (Stream.WasReset)
                    return;

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

            await SendHeaderListAsync(Stream, ResponseHeaders, endStream);

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
        ///
        /// Nothing is sent on a stream that was reset (RFC 9113, Section 5.1: a
        /// closed stream carries no frame but PRIORITY). The write fails instead,
        /// as a DATA write there does: with an OperationCanceledException that
        /// carries the stream's own token, the one its handler was given. That is
        /// decided before the list is encoded, never after: the encoder adds fields
        /// to its dynamic table as it encodes, and a block encoded but not sent
        /// would leave the peer's decoder a step behind, for every later header
        /// block on the connection.
        ///
        /// <paramref name="Completion"/>, for trailers the DATA writer loop sends,
        /// is completed as <see cref="SendDataAsync"/> completes it: once the
        /// block is encoded, right before it is written, under the write lock —
        /// so it must run its continuations asynchronously.
        /// </summary>
        internal async Task SendHeaderListAsync(HTTP2Stream Stream, List<(string Name, string Value)> Headers, bool EndStream, TaskCompletionSource? Completion = null)
        {

            // At once, as a DATA write on a reset stream fails, rather than after a
            // wait for the lock; and with the stream's token even once the
            // connection has ended, which cancels the connection's own token before
            // it resets every stream still open on it.
            ThrowIfReset(Stream);

            await writeLock.WaitAsync(cancellationToken);

            try
            {

                // Again under the lock, right before the encoding, for a reset that
                // came while this write waited for the lock. A reset the read loop
                // has handled is seen here. One the peer has sent and the read loop
                // has not handled yet cannot be, but the peer must be prepared for
                // frames that crossed its RST_STREAM, and still decodes their
                // header blocks (RFC 9113, Sections 5.1 and 6.4). A reset of our
                // own is made before its RST_STREAM takes this lock (see
                // SendStreamWindowUpdateAsync), so no HEADERS follows that.
                ThrowIfReset(Stream);

                var headerBlock  = hpackEncoder.EncodeHeaderBlock(Headers);
                var headerFrames = BuildHeaderFrames(Stream.StreamId, headerBlock, EndStream);

                Completion?.TrySetResult();

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
        /// Fail a write on a reset stream as a read of its request body fails (see
        /// <see cref="HTTP2Stream.Reset"/>): with an OperationCanceledException that
        /// carries the stream's own token.
        /// </summary>
        private static void ThrowIfReset(HTTP2Stream Stream)
        {
            if (Stream.WasReset)
                throw new OperationCanceledException($"Stream {Stream.StreamId} was reset", Stream.CancellationToken);
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
        /// meantime — the response task and the writer loop run concurrently with
        /// the read loop, which may handle a reset between any test of the state
        /// and the transition. So TryCloseLocal tests and transitions in one step,
        /// under the stream's lock, and only when that fails is the stream asked
        /// why: a reset stream is left alone; in any other state our END_STREAM
        /// went out twice, which is a bug, and CloseLocal still throws for it.
        /// </summary>
        private static void CloseLocalIfNotReset(HTTP2Stream Stream)
        {
            if (!Stream.TryCloseLocal() && !Stream.WasReset)
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

            var uncompressedSize = HTTP2HeaderList.UncompressedSize(Headers);

            if (uncompressedSize > remoteSettings.MaxHeaderListSize)
                throw new HTTP2StreamException(HTTP2ErrorCode.INTERNAL_ERROR, StreamId,
                    $"Response header list ({uncompressedSize} bytes) exceeds the peer's advertised " +
                    $"MAX_HEADER_LIST_SIZE ({remoteSettings.MaxHeaderListSize} bytes)");

        }

        /// <summary>
        /// Validate outbound trailer fields (RFC 9113, Section 8.1). The rules are
        /// direction-neutral — the client sends request trailers under exactly the
        /// same constraints — so they live in <see cref="HTTP2Trailers"/>; this stays
        /// as the server-side name its call sites already use.
        /// </summary>
        internal static void ValidateOutboundTrailers(UInt32 StreamId, List<(string Name, string Value)> Trailers)
            => HTTP2Trailers.Validate(StreamId, Trailers);

        #endregion


        #region CONNECT / Extended CONNECT Tunneling (RFC 9113 Section 8.5; RFC 8441)

        /// <summary>
        /// Run the connect handler on its own task, mirroring StartRequestHandler
        /// — a tunnel (especially a WebSocket one) can be open indefinitely, so it
        /// must never block the frame read loop from servicing other streams. Once
        /// the tunnel is refused, or its handler has ended, nothing reads what the
        /// client sends on it any more (<see cref="EndReadingAsync"/>). A handler
        /// that fails, deciding on the tunnel or running it, has the stream reset
        /// with INTERNAL_ERROR, and a cancellation of its own is such a failure too
        /// (<see cref="IsCancelled"/>).
        /// </summary>
        private void StartConnectHandler(HTTP2Stream Stream)
        {

            _ = Task.Run(async () =>
            {

                try
                {
                    await DispatchConnectAsync(Stream);
                }
                catch (OperationCanceledException) when (IsCancelled(Stream))
                {
                    HTTP2EventSource.Log.HandlerCancelled((int) Stream.StreamId, "CONNECT");
                }
                catch (HTTP2StreamException ex)
                {
                    HTTP2EventSource.Log.StreamError((int) ex.StreamId, ex.ErrorCode.ToString(), ex.Message);
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(ex.StreamId, ex.ErrorCode)); } catch { }
                    try { await ReturnUnreadWindowAsync(Stream); } catch { }
                }
                catch (Exception ex)
                {
                    // Any other failure, a cancellation of the handler's own included.
                    HTTP2EventSource.Log.HandlerFailed((int) Stream.StreamId, "CONNECT", ex.Message);

                    // A reset stream is reset no more, as in StartStreamingHandler:
                    // after the client's RST_STREAM, one of ours would answer it,
                    // which RFC 9113, Section 5.4.2 forbids.
                    if (Stream.WasReset)
                        return;

                    // Our side of a tunnel that failed has not been ended (see
                    // DispatchConnectAsync), so the stream is not closed, and the
                    // reset may go out.
                    Stream.Reset();
                    try { await SendFrameAsync(HTTP2Frame.CreateRstStream(Stream.StreamId, HTTP2ErrorCode.INTERNAL_ERROR)); } catch { }
                    try { await ReturnUnreadWindowAsync(Stream); } catch { }
                }
                finally
                {
                    // Refused, run to its end, or failed: nothing reads the tunnel any
                    // more. After a failure's reset, which asks the client to stop
                    // sending in its own way; a tunnel that ran to its end has come
                    // here already.
                    await EndReadingAsync(Stream);
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

            // The 421 check applies to extended CONNECT (RFC 8441) but not to plain
            // CONNECT: they spell :authority the same way and mean opposite things.
            // On an extended CONNECT it names the origin being addressed, exactly as
            // in an ordinary request; on a plain CONNECT it names the *tunnel
            // target*, a host somewhere out there that we are being asked to reach —
            // being "authoritative" for it is not the question.
            if (isAuthorityServed is not null &&
                Stream.RequestHeaders.Any(header => header.Name == ":protocol") &&
                !IsAuthoritativeFor(Stream.RequestHeaders))
            {
                await SendConnectResponseAsync(Stream, 421, null);
                return;
            }

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

            // On a stream reset meanwhile this fails with the stream's token, and
            // an accepted tunnel is not run: its answer never reached the client.
            await SendConnectResponseAsync(Stream, result.StatusCode, result.ExtraHeaders, EndStream: !accepted);

            if (!accepted)
                return;

            var tunnel = new HTTP2Tunnel(this, Stream);

            await result.RunAsync!(tunnel, Stream.CancellationToken);

            // Run to its end: nothing reads the tunnel any more. Before our side
            // is ended below, which waits behind the tunnel's last bytes for the
            // client's window.
            await EndReadingAsync(Stream);

            // Only a tunnel that ran to its end has our side ended, with END_STREAM,
            // a tunnel's TCP FIN (RFC 9113, Section 8.5). One whose handler failed
            // is reset by StartConnectHandler, with no END_STREAM before it: an
            // error in a tunnel is an RST_STREAM, as in TCP it is an RST. Ended
            // first, the tunnel looked to the client as if it had ended cleanly,
            // and once the client had ended its side too, the RST_STREAM went out
            // on a closed stream, where Section 5.1 allows nothing but PRIORITY.
            await CompleteTunnelAsync(Stream);

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

            await SendHeaderListAsync(Stream, headers, EndStream);

            if (EndStream)
                CloseLocalIfNotReset(Stream);

        }

        /// <summary>
        /// Queue a chunk of tunnel data for the writer loop to send as DATA
        /// frame(s) on Stream — same queue, same priority-aware send order as an
        /// ordinary response body (<see cref="SendResponseAsync"/>); a tunnel
        /// write is not privileged over a normal response's bytes. So it fails
        /// alike on a reset or closed stream, and CancellationToken, once
        /// cancelled, keeps a chunk from being queued, or ends the wait for one
        /// already queued (see <see cref="EnqueueOutboundAsync"/>).
        /// </summary>
        internal Task SendTunnelDataAsync(HTTP2Stream Stream, byte[] Data, CancellationToken CancellationToken)
        {

            if (CancellationToken.IsCancellationRequested)
                return Task.FromCanceled(CancellationToken);

            if (Data.Length == 0)
                return Task.CompletedTask;

            return EnqueueOutboundAsync(Stream, Data, EndStream: false, CancellationToken: CancellationToken);

        }

        /// <summary>
        /// End our side of an accepted tunnel once the connect handler's RunAsync
        /// returns — a queued empty DATA frame carrying END_STREAM, mirroring how
        /// an ordinary response's last DATA chunk ends the stream. The writer
        /// loop closes the stream locally once it actually sends that frame
        /// (same as it does for any other End-Stream marker); this method's own
        /// job is just to queue it, wait until it is out, and swallow the
        /// (best-effort) failure if the peer or the connection is already gone —
        /// on a reset tunnel, at once. Not once RunAsync has failed: that tunnel
        /// is reset instead (see DispatchConnectAsync).
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
