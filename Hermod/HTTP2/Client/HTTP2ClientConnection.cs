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
    /// The client-side counterpart of <see cref="HTTP2Connection"/> — the same
    /// framing/HPACK/flow-control machinery driven from the *client* role
    /// (RFC 9113):
    ///
    ///   - it *sends* the connection preface (the magic octets + its own SETTINGS)
    ///     first, then reads the server's SETTINGS (the mirror of the server's
    ///     read-then-send order);
    ///   - it *allocates* client-initiated odd stream IDs
    ///     (<see cref="HTTP2StreamManager.CreateLocalStream"/>) instead of
    ///     accepting them;
    ///   - it sends requests (HEADERS [+ DATA]) and assembles responses
    ///     (HEADERS [+ DATA]) — the inverse of the server's request-assembly /
    ///     response-send paths.
    ///
    /// Everything below the role — frames, HPACK, the stream state machine, flow
    /// control — is the shared, direction-neutral code reused unchanged.
    /// </summary>
    public sealed class HTTP2ClientConnection
    {

        private static readonly byte[] ConnectionPreface = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        private readonly Stream             transportStream;
        private readonly HTTP2Settings      localSettings  = new();
        private readonly HTTP2Settings      remoteSettings = new();
        private readonly HTTP2StreamManager streamManager  = new(HTTP2Role.Client);
        private readonly HPACKDecoder        hpackDecoder   = new();
        private readonly HPACKEncoder        hpackEncoder   = new();
        private readonly SemaphoreSlim       writeLock      = new(1, 1);

        /// <summary>
        /// Serializes request *starts* — allocating the next odd stream ID,
        /// HPACK-encoding the header block (the encoder's dynamic table is stateful
        /// and shared), and writing the HEADERS frame — as one atomic step. RFC
        /// 9113, Section 5.1.1 requires new stream IDs to open in increasing order
        /// (a higher ID implicitly closes lower ones), so two concurrent requests
        /// must not interleave their opens.
        /// </summary>
        private readonly SemaphoreSlim  requestStartLock = new(1, 1);

        private readonly CancellationTokenSource connectionCts;
        private readonly CancellationToken       cancellationToken;

        /// <summary>
        /// Whether the transport is an authenticated TLS stream — false for
        /// cleartext h2c. Statements a server makes about its own identity (the
        /// RFC 8336 Origin Set) are only meaningful when it has one.
        /// </summary>
        private readonly bool                    isSecure;

        /// <summary>
        /// Answers 401 challenges (RFC 9110, Section 11). One per connection, since
        /// it carries the Digest nonce counter; created lazily so a connection
        /// without credentials never allocates one.
        /// </summary>
        private readonly Lazy<HTTPClientAuthenticator> authenticator;

        private readonly object flowLock = new();
        private TaskCompletionSource windowChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Connection-level receive window we raise to at startup (above the 65535 default).</summary>
        private const long ConnectionRecvWindowTarget = 1024 * 1024;   // 1 MiB

        /// <summary>Bytes consumed connection-wide since our last connection-level WINDOW_UPDATE (batched replenish).</summary>
        private long connectionPendingRecvUpdate;

        private readonly TaskCompletionSource settingsReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Completes when the connection has finished — the frame read loop exited,
        /// whether via a clean <see cref="CloseAsync"/>, a peer GOAWAY teardown, or a
        /// fatal I/O error. Never faults (it's a lifecycle signal, not a result), so
        /// a consumer like <see cref="HTTP2ClientPool"/> can simply <c>await</c> it to
        /// learn the connection died and replace it.
        /// </summary>
        private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Completes as soon as the connection can no longer take *new* streams —
        /// either it received a GOAWAY (in-flight streams may still be finishing) or
        /// it fully closed. This is the pool's cue to stop routing here and open a
        /// replacement, without waiting for the connection to fully drain.
        /// </summary>
        private readonly TaskCompletionSource unusable = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private UInt32? continuationStreamId;
        private int     continuationFrameCount;
        private bool    goawayReceived;

        private readonly HTTP2ClientOptions options;

        /// <summary>
        /// Pulsed whenever an in-flight stream finishes, so a request waiting for a
        /// free MAX_CONCURRENT_STREAMS slot can proceed (see the gate in
        /// <see cref="IssueOnNewStreamAsync"/>).
        /// </summary>
        private TaskCompletionSource streamSlotFreed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Keepalive / liveness state.
        private long                     lastActivityTimestamp;    // options.TimeProvider.GetTimestamp() at last inbound frame
        private readonly object          pingLock = new();
        private TaskCompletionSource?    pendingPingAck;
        private byte[]                   pendingPingPayload = [];

        /// <summary>In-flight requests keyed by their stream ID (guarded by <see cref="exchangesLock"/>).</summary>
        private readonly Dictionary<UInt32, ClientExchange> exchanges = [];
        private readonly object                             exchangesLock = new();


        /// <param name="TransportStream">
        /// The byte transport: an <see cref="SslStream"/> for HTTP/2-over-TLS, or a
        /// raw <see cref="System.Net.Sockets.NetworkStream"/> for cleartext h2c
        /// (prior-knowledge, RFC 9113 §3.3). Only the <see cref="Stream"/> base API
        /// is used, so the connection is transport-agnostic.
        /// </param>
        public HTTP2ClientConnection(
            Stream              TransportStream,
            CancellationToken   CancellationToken = default,
            HTTP2ClientOptions? Options           = null)
        {
            this.transportStream   = TransportStream;
            this.isSecure          = TransportStream is System.Net.Security.SslStream { IsAuthenticated: true };
            this.options           = Options ?? HTTP2ClientOptions.Default;
            this.connectionCts     = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            this.cancellationToken = connectionCts.Token;
            this.lastActivityTimestamp = this.options.TimeProvider.GetTimestamp();
            this.authenticator     = new Lazy<HTTPClientAuthenticator>(
                                         () => new HTTPClientAuthenticator(this.options.Credentials!),
                                         LazyThreadSafetyMode.ExecutionAndPublication);
        }


        #region Handshake + run loop

        /// <summary>
        /// Send the client connection preface (magic + our SETTINGS), then start
        /// the background frame loop and wait until the server's initial SETTINGS
        /// have been received (so callers don't race the handshake).
        /// </summary>
        public async Task StartAsync()
        {

            await transportStream.WriteAsync(ConnectionPreface, cancellationToken);

            await SendFrameAsync(HTTP2Frame.CreateSettings(
                (HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, localSettings.MaxConcurrentStreams),
                (HTTP2SettingsParameter.INITIAL_WINDOW_SIZE,    localSettings.InitialWindowSize),
                (HTTP2SettingsParameter.MAX_FRAME_SIZE,         localSettings.MaxFrameSize),
                (HTTP2SettingsParameter.ENABLE_PUSH,            0),  // We don't accept server push
                // RFC 9218, Section 3: we use the modern priority scheme (the
                // "priority" header + PRIORITY_UPDATE), not RFC 7540 priority.
                (HTTP2SettingsParameter.NO_RFC7540_PRIORITIES,  1)
            ));

            // New streams' receive windows start at the INITIAL_WINDOW_SIZE we just
            // advertised; keep the manager in sync so the accounting matches.
            streamManager.LocalInitialWindowSize = localSettings.InitialWindowSize;

            // Raise the connection-level receive window above its 65535 default so
            // large responses aren't throttled (INITIAL_WINDOW_SIZE is stream-only).
            var connectionBump = ConnectionRecvWindowTarget - streamManager.ConnectionRecvWindow;
            if (connectionBump > 0)
            {
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, (UInt32) connectionBump));
                streamManager.ConnectionRecvWindow += connectionBump;
            }

            _ = Task.Run(RunAsync);

            if (options.KeepAliveInterval > TimeSpan.Zero)
                _ = Task.Run(KeepAliveLoopAsync);

            await settingsReceived.Task.WaitAsync(cancellationToken);

        }

        private async Task RunAsync()
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync();

                    // Liveness: any inbound frame counts as the connection being alive.
                    Interlocked.Exchange(ref lastActivityTimestamp, options.TimeProvider.GetTimestamp());

                    if (continuationStreamId.HasValue && frame.Type != HTTP2FrameType.CONTINUATION)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "Expected CONTINUATION frame");

                    switch (frame.Type)
                    {
                        case HTTP2FrameType.SETTINGS:       await HandleSettingsAsync(frame); break;
                        case HTTP2FrameType.HEADERS:         HandleHeaders(frame);            break;
                        case HTTP2FrameType.CONTINUATION:    HandleContinuation(frame);       break;
                        case HTTP2FrameType.DATA:            await HandleDataAsync(frame);     break;
                        case HTTP2FrameType.WINDOW_UPDATE:   HandleWindowUpdate(frame);       break;
                        case HTTP2FrameType.PING:            await HandlePingAsync(frame);     break;
                        case HTTP2FrameType.RST_STREAM:      HandleRstStream(frame);          break;
                        case HTTP2FrameType.GOAWAY:          HandleGoAway(frame);             break;
                        case HTTP2FrameType.ORIGIN:          HandleOrigin(frame);             break;
                        case HTTP2FrameType.PUSH_PROMISE:
                            // We advertised ENABLE_PUSH=0; a server that pushes anyway is in error.
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "Server push not enabled");
                        default:
                            // PRIORITY, unknown types — ignore (RFC 9113, Section 4.1).
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                FailAllExchanges(ex);
                settingsReceived.TrySetException(ex);
            }
            finally
            {
                connectionCts.Cancel();
                unusable.TrySetResult();   // no new streams from here on
                closed.TrySetResult();     // wake any pool watcher awaiting this connection's death
            }
        }

        #endregion


        #region Pool support — liveness + load

        /// <summary>
        /// Completes when this connection has finished (see <see cref="closed"/>) —
        /// a clean close, a peer GOAWAY teardown, or a fatal error. Never faults.
        /// </summary>
        public Task Closed => closed.Task;

        /// <summary>
        /// Completes as soon as the connection stops accepting new streams — a GOAWAY
        /// (while in-flight streams finish) or a full close. The pool's cue to prune
        /// and replace it. Never faults.
        /// </summary>
        public Task Unusable => unusable.Task;

        /// <summary>
        /// Whether this connection can still take *new* requests: it hasn't been torn
        /// down and hasn't received a GOAWAY (after which the peer accepts no new
        /// streams). In-flight requests may still be completing; this only gates new
        /// ones — which is exactly what a pool needs to route around a draining or
        /// dead connection.
        /// </summary>
        public bool IsUsable => !goawayReceived && !cancellationToken.IsCancellationRequested;

        /// <summary>Number of requests currently in flight on this connection.</summary>
        public int ActiveStreamCount
        {
            get { lock (exchangesLock) return exchanges.Count; }
        }

        /// <summary>
        /// How many more streams may be opened before hitting the peer's advertised
        /// MAX_CONCURRENT_STREAMS — a pool prefers the connection with the most free
        /// slots (least loaded), and knows to open/await another when this is 0.
        /// </summary>
        public int AvailableStreamSlots
        {
            get { lock (exchangesLock) return Math.Max(0, (int) streamManager.MaxConcurrentStreams - exchanges.Count); }
        }

        #endregion


        #region Sending a request

        /// <summary>
        /// Send an HTTP/2 request and await the assembled response. Thread-safe:
        /// multiple requests may be in flight concurrently on the same connection
        /// (real client-side multiplexing). Pass Priority to signal an RFC 9218
        /// urgency/incremental hint via the <c>priority</c> request header.
        /// </summary>
        public async Task<HTTP2Response> SendRequestAsync(
            string                             Method,
            string                             Scheme,
            string                             Authority,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            byte[]?                            Body         = null,
            HTTP2Priority?                     Priority     = null,
            CancellationToken                  CancellationToken = default)
        {
            var handle   = await StartRequestAsync(Method, Scheme, Authority, Path, ExtraHeaders, Body, Priority, CancellationToken);
            var response = await handle.Response;

            // RFC 9110 Section 11.6.1: a 401 names the schemes the server will
            // accept. If we hold credentials for one of them, answer the challenge
            // and re-issue — exactly once. A second 401 means the credentials are
            // wrong, and repeating the request would only earn a third.
            //
            // Re-issuing the *same* request is also what keeps this safe: the
            // authority cannot change between the challenge and the answer, so
            // credentials can never leak to an origin that did not ask for them.
            if (response.Status == 401 &&
                options.Credentials is not null &&
                (ExtraHeaders is null || !ExtraHeaders.Any(header => header.Name == "authorization")))
            {

                var challenges = response.Headers.
                                     Where (header => header.Name == "www-authenticate").
                                     Select(header => header.Value).
                                     ToList();

                var authorization = challenges.Count > 0
                                        ? authenticator.Value.Answer(challenges, Method, Path)
                                        : null;

                if (authorization is not null)
                {

                    List<(string Name, string Value)> retryHeaders = ExtraHeaders is null ? [] : [.. ExtraHeaders];
                    retryHeaders.Add(("authorization", authorization));

                    var retry = await StartRequestAsync(Method, Scheme, Authority, Path, retryHeaders, Body, Priority, CancellationToken);
                    return await retry.Response;

                }

            }

            return response;

        }

        /// <summary>
        /// Begin a request and return a handle *immediately* once its HEADERS are
        /// on the wire — before the response arrives — exposing the allocated
        /// <see cref="HTTP2RequestHandle.StreamId"/> so the caller can reprioritize
        /// the in-flight request via <see cref="UpdatePriorityAsync"/> (RFC 9218
        /// PRIORITY_UPDATE) while awaiting <see cref="HTTP2RequestHandle.Response"/>.
        /// </summary>
        public async Task<HTTP2RequestHandle> StartRequestAsync(
            string                             Method,
            string                             Scheme,
            string                             Authority,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            byte[]?                            Body         = null,
            HTTP2Priority?                     Priority     = null,
            CancellationToken                  CancellationToken = default)
        {

            var hasBody = Body is not null && Body.Length > 0;

            var headers = new List<(string Name, string Value)>
            {
                (":method",    Method),
                (":scheme",    Scheme),
                (":authority", Authority),
                (":path",      Path)
            };

            // RFC 9218 Section 4: the priority hint, unless the caller already put
            // one in ExtraHeaders explicitly.
            if (Priority is not null && (ExtraHeaders is null || !ExtraHeaders.Any(h => h.Name == "priority")))
                headers.Add(("priority", Priority.Value.ToHeaderValue()));

            // RFC 9110 Section 12.5.3: ask for the codings we can undo — but never
            // over a caller's own accept-encoding, which may be deliberately
            // narrower (or an explicit "identity" to switch compression off for
            // one request).
            if (options.AutomaticDecompression &&
                (ExtraHeaders is null || !ExtraHeaders.Any(h => h.Name == "accept-encoding")))
                headers.Add(("accept-encoding", HTTPContentCoding.AcceptEncoding));

            if (ExtraHeaders is not null)
                headers.AddRange(ExtraHeaders);

            // The exchange carries everything needed to re-issue the request on a
            // fresh stream should the server refuse this one (REFUSED_STREAM).
            var exchange = new ClientExchange
            {
                RequestHeaders = headers,
                RequestBody    = Body,
                HasBody        = hasBody,
                RequestToken   = CancellationToken
            };

            var streamId = await IssueOnNewStreamAsync(exchange);

            return new HTTP2RequestHandle(streamId, AwaitResponseAsync(exchange));

        }

        /// <summary>
        /// Begin a bidirectional streaming request: send the HEADERS (keeping the
        /// request side open) and return an <see cref="HTTP2ClientStream"/> the caller
        /// drives — writing request-body chunks over time and reading the response
        /// incrementally. This is the client counterpart of the server's streaming
        /// seam, and the enabler for client-streaming / bidirectional gRPC (whose
        /// request and response messages interleave over one stream). Never
        /// auto-retried on REFUSED_STREAM (the outbound chunks aren't buffered for
        /// replay); a reset surfaces on the response side instead.
        /// </summary>
        public async Task<HTTP2ClientStream> StartStreamingRequestAsync(
            string                             Method,
            string                             Scheme,
            string                             Authority,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            HTTP2Priority?                     Priority     = null,
            CancellationToken                  CancellationToken = default)
        {

            var headers = new List<(string Name, string Value)>
            {
                (":method",    Method),
                (":scheme",    Scheme),
                (":authority", Authority),
                (":path",      Path)
            };

            if (Priority is not null && (ExtraHeaders is null || !ExtraHeaders.Any(h => h.Name == "priority")))
                headers.Add(("priority", Priority.Value.ToHeaderValue()));

            if (ExtraHeaders is not null)
                headers.AddRange(ExtraHeaders);

            // Vehicles shared between the exchange (connection side) and the returned
            // handle (caller side): created up front so neither needs to look the
            // other up after the exchange is removed on completion.
            var responseHead     = new TaskCompletionSource<HTTP2ResponseHead>(TaskCreationOptions.RunContinuationsAsynchronously);
            var responseChunks   = Channel.CreateUnbounded<byte[]>();
            var responseTrailers = new TaskCompletionSource<List<(string Name, string Value)>>(TaskCreationOptions.RunContinuationsAsynchronously);

            var exchange = new ClientExchange
            {
                RequestHeaders   = headers,
                RequestBody      = null,
                HasBody          = false,
                RequestToken     = CancellationToken,
                IsStreaming      = true,
                ResponseHead     = responseHead,
                ResponseChunks   = responseChunks,
                ResponseTrailers = responseTrailers
            };

            await IssueOnNewStreamAsync(exchange);

            return new HTTP2ClientStream(this, exchange.Stream, responseHead.Task, responseChunks.Reader, responseTrailers.Task);

        }

        /// <summary>
        /// Allocate a fresh outbound stream and put the request on the wire: gate on
        /// the peer's MAX_CONCURRENT_STREAMS, allocate + open the stream, encode and
        /// send HEADERS atomically under the request-start lock, then (if there's a
        /// body) send it concurrently. Reused verbatim by the REFUSED_STREAM retry
        /// path, which is why it lives apart from <see cref="StartRequestAsync"/>.
        /// </summary>
        private async Task<UInt32> IssueOnNewStreamAsync(ClientExchange Exchange)
        {

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Exchange.RequestToken);

            await requestStartLock.WaitAsync(linked.Token);
            try
            {
                // Wait for a free stream slot rather than failing when the server's
                // MAX_CONCURRENT_STREAMS limit is momentarily reached.
                await WaitForStreamSlotAsync(linked.Token);

                var stream = streamManager.CreateLocalStream();
                stream.Open();
                Exchange.Stream = stream;

                lock (exchangesLock)
                    exchanges[stream.StreamId] = Exchange;

                // A streaming exchange keeps the request side open (HEADERS without
                // END_STREAM) so the caller can write DATA chunks over time; the
                // buffered path ends the stream now (no body) or streams the whole
                // body then closes (below).
                var keepOpen = Exchange.IsStreaming;

                var headerBlock = hpackEncoder.EncodeHeaderBlock(Exchange.RequestHeaders);
                await SendHeaderBlockAsync(stream.StreamId, headerBlock, EndStream: !Exchange.HasBody && !keepOpen);

                if (!Exchange.HasBody && !keepOpen)
                    stream.CloseLocal();
            }
            finally
            {
                requestStartLock.Release();
            }

            if (Exchange.HasBody)
                _ = SendBodyThenCloseAsync(Exchange);

            return Exchange.Stream!.StreamId;

        }

        /// <summary>Wait until fewer than MAX_CONCURRENT_STREAMS outbound streams are in flight.</summary>
        private async Task WaitForStreamSlotAsync(CancellationToken Token)
        {
            while (true)
            {
                Task wait;
                lock (exchangesLock)
                {
                    if (exchanges.Count < streamManager.MaxConcurrentStreams)
                        return;
                    wait = streamSlotFreed.Task;
                }
                await wait.WaitAsync(Token);
            }
        }

        /// <summary>Send the request body, then half-close our side. Runs concurrently with other streams.</summary>
        private async Task SendBodyThenCloseAsync(ClientExchange Exchange)
        {
            try
            {
                await SendBodyAsync(Exchange.Stream!, Exchange.RequestBody!);
                CloseLocalIfOpen(Exchange.Stream!);
            }
            catch (Exception ex)
            {
                // A body-send failure that isn't just "stream was reset" (which
                // SendBodyAsync handles by returning) fails the request.
                Exchange.Completion.TrySetException(ex);
            }
        }

        /// <summary>Await the assembled response, honoring both the connection and the per-request cancellation.</summary>
        private async Task<HTTP2Response> AwaitResponseAsync(ClientExchange Exchange)
        {
            using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Exchange.RequestToken);
            return await Exchange.Completion.Task.WaitAsync(reqCts.Token);
        }

        /// <summary>Re-issue a refused request on a fresh stream, reusing its Completion (RFC 9113 §8.1).</summary>
        private async Task RetryExchangeAsync(ClientExchange Exchange)
        {
            try
            {
                // Discard any partial response state from the refused attempt.
                Exchange.HeaderBuffer.SetLength(0);
                Exchange.Body.SetLength(0);
                Exchange.Headers         = null;
                Exchange.HeadersReceived = false;
                Exchange.Trailers        = [];
                Exchange.Interim         = [];

                await IssueOnNewStreamAsync(Exchange);
            }
            catch (Exception ex)
            {
                Exchange.Completion.TrySetException(ex);
            }
        }

        /// <summary>
        /// Reprioritize an in-flight request by sending a PRIORITY_UPDATE frame
        /// (RFC 9218, Section 7.1) — e.g. to promote a stalled download. The stream
        /// ID comes from a <see cref="HTTP2RequestHandle"/>. A no-op-safe hint: the
        /// server may honor or ignore it, and a PRIORITY_UPDATE for an
        /// already-finished stream is silently dropped by the peer.
        /// </summary>
        public Task UpdatePriorityAsync(UInt32 StreamId, HTTP2Priority Priority, CancellationToken CancellationToken = default)
            => SendFrameAsync(HTTP2Frame.CreatePriorityUpdate(StreamId, Priority.ToHeaderValue()));

        #endregion


        #region CONNECT tunnels (RFC 9113 §8.5; extended CONNECT RFC 8441)

        /// <summary>
        /// Open a CONNECT tunnel to <paramref name="Authority"/> and return a raw
        /// bidirectional byte tunnel once the server accepts it (2xx). Plain CONNECT
        /// (RFC 9113 §8.5) omits <paramref name="Protocol"/>/<paramref name="Scheme"/>/<paramref name="Path"/>;
        /// extended CONNECT (RFC 8441 — e.g. to bootstrap a WebSocket) requires all
        /// three. The stream is kept open (HEADERS without END_STREAM) so tunnel
        /// bytes can flow both ways. Throws if the server refuses the tunnel.
        /// </summary>
        public async Task<HTTP2ClientTunnel> OpenTunnelAsync(
            string                             Authority,
            string?                            Protocol     = null,
            string?                            Scheme       = null,
            string?                            Path         = null,
            List<(string Name, string Value)>? ExtraHeaders = null,
            CancellationToken                  CancellationToken = default)
        {

            var headers = new List<(string Name, string Value)>
            {
                (":method",    "CONNECT"),
                (":authority", Authority)
            };

            // Extended CONNECT (RFC 8441 §4): :protocol plus — unlike plain CONNECT
            // — a mandatory :scheme and :path.
            if (Protocol is not null)
            {
                headers.Add((":protocol", Protocol));
                headers.Add((":scheme",   Scheme ?? throw new ArgumentException("Extended CONNECT requires a scheme", nameof(Scheme))));
                headers.Add((":path",     Path   ?? throw new ArgumentException("Extended CONNECT requires a path",   nameof(Path))));
            }

            if (ExtraHeaders is not null)
                headers.AddRange(ExtraHeaders);

            var exchange = new ClientExchange
            {
                RequestHeaders = headers,
                RequestBody    = null,
                HasBody        = false,
                RequestToken   = CancellationToken,
                IsTunnel       = true
            };

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);

            // Open the stream and send CONNECT HEADERS *without* END_STREAM — the
            // request side stays open so we can send tunnel bytes.
            await requestStartLock.WaitAsync(linked.Token);
            try
            {
                await WaitForStreamSlotAsync(linked.Token);

                var stream = streamManager.CreateLocalStream();
                stream.Open();
                exchange.Stream = stream;

                lock (exchangesLock)
                    exchanges[stream.StreamId] = exchange;

                var block = hpackEncoder.EncodeHeaderBlock(headers);
                await SendHeaderBlockAsync(stream.StreamId, block, EndStream: false);
            }
            finally
            {
                requestStartLock.Release();
            }

            var status = await exchange.TunnelStatus.Task.WaitAsync(linked.Token);

            if (status is < 200 or >= 300)
                throw new HTTP2StreamException(HTTP2ErrorCode.REFUSED_STREAM, exchange.Stream.StreamId,
                    $"CONNECT to '{Authority}' rejected with status {status}");

            return new HTTP2ClientTunnel(this, exchange.Stream, exchange.Headers ?? []);

        }

        /// <summary>
        /// Open a WebSocket (RFC 6455) over an extended-CONNECT tunnel (RFC 8441) —
        /// a convenience over <see cref="OpenTunnelAsync"/> with
        /// <c>:protocol = websocket</c>, returning a client-role
        /// <see cref="WebSocketConnection"/> (it masks every frame it sends).
        ///
        /// When <paramref name="PerMessageDeflate"/> is set, the client offers
        /// permessage-deflate (RFC 7692) via a <c>sec-websocket-extensions</c> header
        /// on the CONNECT request and only actually compresses if the server echoes
        /// its acceptance back — so a server that ignores the offer transparently
        /// yields an uncompressed connection.
        /// </summary>
        public async Task<WebSocketConnection> OpenWebSocketAsync(
            string                             Authority,
            string                             Scheme,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders      = null,
            bool                               PerMessageDeflate = false,
            CancellationToken                  CancellationToken = default)
        {

            // Offer permessage-deflate as an ordinary request header on the extended
            // CONNECT (over HTTP/2 the WebSocket handshake headers are just fields).
            if (PerMessageDeflate)
            {
                ExtraHeaders = ExtraHeaders is null ? [] : [.. ExtraHeaders];
                ExtraHeaders.Add(("sec-websocket-extensions", WebSocketDeflate.Offer));
            }

            var tunnel = await OpenTunnelAsync(Authority, "websocket", Scheme, Path, ExtraHeaders, CancellationToken);

            // Only run the extension if the server accepted it (echoed back in its
            // sec-websocket-extensions response header, RFC 7692 §5.1).
            var accepted = PerMessageDeflate &&
                           WebSocketDeflate.WasAccepted(
                               tunnel.ResponseHeaders.FirstOrDefault(h => h.Name == "sec-websocket-extensions").Value);

            return new WebSocketConnection(tunnel, WebSocketRole.Client, PerMessageDeflate: accepted);

        }

        /// <summary>Send tunnel bytes as flow-controlled DATA frames (never END_STREAM).</summary>
        internal async Task SendTunnelDataAsync(HTTP2Stream Stream, byte[] Data, CancellationToken CancellationToken)
        {

            if (Data.Length == 0)
                return;

            var maxPayload = (int) remoteSettings.MaxFrameSize;
            var offset     = 0;

            while (offset < Data.Length)
            {
                var chunkSize = await ReserveSendWindowAsync(Stream, Math.Min(Data.Length - offset, maxPayload));

                if (chunkSize == 0)
                    return;   // stream reset — abandon

                var chunk = Data[offset..(offset + chunkSize)];
                await SendFrameAsync(HTTP2Frame.CreateData(Stream.StreamId, chunk, EndStream: false));
                offset += chunkSize;
            }

        }

        /// <summary>
        /// Send stream body bytes as flow-controlled DATA frames (never END_STREAM) —
        /// the send path shared by CONNECT tunnels and streaming requests; both are
        /// "raw bytes out on an open stream", flow-controlled identically.
        /// </summary>
        internal Task SendStreamDataAsync(HTTP2Stream Stream, byte[] Data, CancellationToken CancellationToken)
            => SendTunnelDataAsync(Stream, Data, CancellationToken);

        /// <summary>End our side of the tunnel with a zero-length END_STREAM DATA frame.</summary>
        internal async Task EndTunnelAsync(HTTP2Stream Stream)
        {
            try { await SendFrameAsync(HTTP2Frame.CreateData(Stream.StreamId, [], EndStream: true)); }
            catch { /* best-effort */ }

            CloseLocalIfOpen(Stream);
        }

        #endregion


        #region Sending helpers (body + header block)

        private async Task SendBodyAsync(HTTP2Stream Stream, byte[] Body)
        {

            var maxPayload = (int) remoteSettings.MaxFrameSize;
            var offset     = 0;

            while (offset < Body.Length)
            {
                var chunkSize = await ReserveSendWindowAsync(Stream, Math.Min(Body.Length - offset, maxPayload));

                if (chunkSize == 0)
                    return;   // Stream reset by peer — abandon

                var isLast = offset + chunkSize >= Body.Length;
                var chunk  = Body[offset..(offset + chunkSize)];

                await SendFrameAsync(HTTP2Frame.CreateData(Stream.StreamId, chunk, EndStream: isLast));

                offset += chunkSize;
            }

        }

        /// <summary>Write a header block as HEADERS (+ CONTINUATION if oversized), atomically under the write lock.</summary>
        private async Task SendHeaderBlockAsync(UInt32 StreamId, byte[] HeaderBlock, bool EndStream)
        {

            var maxPayload = (int) remoteSettings.MaxFrameSize;
            var frames     = new List<HTTP2Frame>();

            if (HeaderBlock.Length <= maxPayload)
            {
                frames.Add(HTTP2Frame.CreateHeaders(StreamId, HeaderBlock, EndStream, EndHeaders: true));
            }
            else
            {
                frames.Add(HTTP2Frame.CreateHeaders(StreamId, HeaderBlock[..maxPayload], EndStream, EndHeaders: false));

                var offset = maxPayload;
                while (offset < HeaderBlock.Length)
                {
                    var size   = Math.Min(HeaderBlock.Length - offset, maxPayload);
                    var isLast = offset + size >= HeaderBlock.Length;

                    frames.Add(new HTTP2Frame {
                        Type     = HTTP2FrameType.CONTINUATION,
                        Flags    = isLast ? HTTP2FrameFlags.END_HEADERS : HTTP2FrameFlags.NONE,
                        StreamId = StreamId,
                        Payload  = HeaderBlock[offset..(offset + size)]
                    });

                    offset += size;
                }
            }

            await SendFramesAsync(frames);

        }

        #endregion


        #region SETTINGS

        private async Task HandleSettingsAsync(HTTP2Frame Frame)
        {

            if (Frame.StreamId != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "SETTINGS must be on stream 0");

            if (Frame.IsAck)
                return;

            if (Frame.Length % 6 != 0)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR, "SETTINGS payload must be a multiple of 6");

            ApplyRemoteSettings(Frame);
            await SendFrameAsync(HTTP2Frame.CreateSettingsAck());

            settingsReceived.TrySetResult();

        }

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
                        // Bound our encoder's dynamic table to what the server's
                        // decoder will keep (RFC 7541, Section 6.3).
                        hpackEncoder.SetMaxDynamicTableSize((int) value);
                        break;

                    case HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS:
                        remoteSettings.MaxConcurrentStreams = value;
                        // The server's advertised limit caps how many streams *we* may open.
                        streamManager.MaxConcurrentStreams = value;
                        break;

                    case HTTP2SettingsParameter.INITIAL_WINDOW_SIZE:
                        if (value > 0x7FFFFFFF)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR, "INITIAL_WINDOW_SIZE too large");
                        lock (flowLock)
                        {
                            var delta = (Int64) value - (Int64) remoteSettings.InitialWindowSize;
                            remoteSettings.InitialWindowSize   = value;
                            streamManager.PeerInitialWindowSize = value;
                            streamManager.AdjustAllStreamWindows(delta);
                        }
                        SignalWindowChange();
                        break;

                    case HTTP2SettingsParameter.MAX_FRAME_SIZE:
                        if (value < 16384 || value > 16777215)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "MAX_FRAME_SIZE out of range");
                        remoteSettings.MaxFrameSize = value;
                        break;

                    case HTTP2SettingsParameter.MAX_HEADER_LIST_SIZE:
                        remoteSettings.MaxHeaderListSize = value;
                        break;

                    default:
                        break;   // Unknown / not-acted-on settings ignored (RFC 9113, Section 6.5.2)
                }
            }

        }

        #endregion


        #region Response HEADERS / CONTINUATION

        private void HandleHeaders(HTTP2Frame Frame)
        {

            var exchange = GetExchange(Frame.StreamId);
            if (exchange is null)
                return;   // Stream we don't know (already completed / reset) — ignore stragglers

            var headerData = StripPadding(Frame, Frame.Payload.AsSpan());

            if (Frame.HasPriority)
            {
                if (headerData.Length < 5)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR, "HEADERS+PRIORITY too short");
                headerData = headerData[5..];   // skip 4-byte dependency + 1-byte weight
            }

            continuationFrameCount = 0;
            exchange.HeaderBuffer.Write(headerData);
            EnforceHeaderBufferLimit(exchange);

            if (Frame.EndHeaders)
                CompleteHeaderBlock(exchange, Frame.EndStream);
            else
                continuationStreamId = Frame.StreamId;

        }

        private void HandleContinuation(HTTP2Frame Frame)
        {

            if (continuationStreamId != Frame.StreamId)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "Unexpected CONTINUATION stream");

            // A server flooding us with CONTINUATION frames that never set
            // END_HEADERS is the CVE-2024-27316 class — bound it (mirror of the
            // server's inbound defense).
            if (++continuationFrameCount > options.MaxContinuationFrames)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Server sent too many CONTINUATION frames ({options.MaxContinuationFrames} max)");

            var exchange = GetExchange(Frame.StreamId);
            if (exchange is null)
                return;

            exchange.HeaderBuffer.Write(Frame.Payload);
            EnforceHeaderBufferLimit(exchange);

            if (Frame.EndHeaders)
            {
                continuationStreamId = null;
                CompleteHeaderBlock(exchange, Frame.EndStream);
            }

        }

        /// <summary>
        /// Bound the response header block we're accumulating against our advertised
        /// MAX_HEADER_LIST_SIZE, so a server can't exhaust client memory by never
        /// setting END_HEADERS (the client-side mirror of the server's
        /// EnforceHeaderBufferLimit).
        /// </summary>
        private void EnforceHeaderBufferLimit(ClientExchange Exchange)
        {
            if (Exchange.HeaderBuffer.Length > localSettings.MaxHeaderListSize)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.ENHANCE_YOUR_CALM,
                    $"Response header block exceeds MAX_HEADER_LIST_SIZE ({localSettings.MaxHeaderListSize} bytes)");
        }

        private void CompleteHeaderBlock(ClientExchange Exchange, bool EndStream)
        {

            var block = Exchange.HeaderBuffer.ToArray();
            Exchange.HeaderBuffer.SetLength(0);

            // The HPACK dynamic table is connection-wide state — always decode.
            var decoded = hpackDecoder.DecodeHeaderBlock(block);

            if (!Exchange.HeadersReceived)
            {

                // An interim (1xx) response (RFC 9110 §15.2 — e.g. 100 Continue, 103
                // Early Hints) precedes the final response: record it and keep
                // waiting, rather than mistaking it for the final response.
                var interimStatusText = decoded.FirstOrDefault(h => h.Name == ":status").Value;
                if (int.TryParse(interimStatusText, out var interimStatus) && interimStatus is >= 100 and < 200)
                {
                    Exchange.Interim.Add((interimStatus, decoded));
                    return;   // do NOT set HeadersReceived — the final HEADERS follow
                }

                Exchange.Headers         = decoded;
                Exchange.HeadersReceived = true;

                // A CONNECT tunnel: the response :status decides accept (2xx keeps
                // the stream open — subsequent DATA is tunnel bytes) vs. reject.
                if (Exchange.IsTunnel)
                {
                    var statusText = decoded.FirstOrDefault(h => h.Name == ":status").Value;
                    _ = int.TryParse(statusText, out var status);

                    if (status is >= 200 and < 300 && !EndStream)
                    {
                        Exchange.Stream.IsConnectTunnel = true;
                        Exchange.Stream.TunnelInbound   = Channel.CreateUnbounded<byte[]>();
                        Exchange.TunnelStatus.TrySetResult(status);
                    }
                    else
                    {
                        // Rejected (or immediately closed) — no tunnel.
                        Exchange.TunnelStatus.TrySetResult(status);
                        RemoveExchange(Exchange.Stream.StreamId);
                    }

                    return;
                }

                // A streaming exchange: surface the response head immediately so the
                // caller can start reading the body (which arrives as DATA), and
                // finalize now if this response has no body/trailers (headers-only).
                if (Exchange.IsStreaming)
                {
                    var statusText = decoded.FirstOrDefault(h => h.Name == ":status").Value;
                    _ = int.TryParse(statusText, out var status);
                    Exchange.ResponseHead!.TrySetResult(new HTTP2ResponseHead(status, decoded));

                    if (EndStream)
                        FinalizeStreamingResponse(Exchange);

                    return;
                }
            }
            else
            {
                // A second header block is trailers (RFC 9113, Section 8.1).
                Exchange.Trailers = decoded;
            }

            if (EndStream)
            {
                if (Exchange.IsStreaming)
                    FinalizeStreamingResponse(Exchange);
                else
                    CompleteResponse(Exchange);
            }

        }

        /// <summary>
        /// Finalize a streaming response at END_STREAM: complete the body channel
        /// (so the reader sees end-of-stream) and hand over the trailers. Mirrors
        /// <see cref="CompleteResponse"/> for the streaming path.
        /// </summary>
        private void FinalizeStreamingResponse(ClientExchange Exchange)
        {
            CloseRemoteIfOpen(Exchange.Stream);
            RemoveExchange(Exchange.Stream.StreamId);

            // In case a body-less response never carried HEADERS through the head
            // path above (defensive — HEADERS always precede END_STREAM), make sure
            // the head is satisfied so a waiting GetResponseAsync can't hang.
            var statusText = Exchange.Headers?.FirstOrDefault(h => h.Name == ":status").Value;
            _ = int.TryParse(statusText, out var status);
            Exchange.ResponseHead!.TrySetResult(new HTTP2ResponseHead(status, Exchange.Headers ?? []));

            Exchange.ResponseChunks!.Writer.TryComplete();
            Exchange.ResponseTrailers!.TrySetResult(Exchange.Trailers);
        }

        #endregion


        #region Response DATA

        private async Task HandleDataAsync(HTTP2Frame Frame)
        {

            var exchange = GetExchange(Frame.StreamId);

            // RFC 9113, Section 6.1: the ENTIRE frame payload counts against flow
            // control — including the Pad Length byte and the padding itself —
            // so the replenish below must return the full length, not just the
            // useful data StripPadding leaves over.
            var flowLength = Frame.Payload.Length;

            var payload = StripPadding(Frame, Frame.Payload.AsSpan());
            var length  = payload.Length;

            if (exchange is not null)
            {
                if (exchange.Stream.IsConnectTunnel)
                {
                    // Tunnel bytes — hand them to the tunnel reader as they arrive.
                    if (length > 0)
                        await exchange.Stream.TunnelInbound!.Writer.WriteAsync(payload.ToArray(), cancellationToken);
                }
                else if (exchange.IsStreaming)
                {
                    // Streaming response body — hand each chunk to the reader as it
                    // arrives, rather than buffering the whole response.
                    if (length > 0)
                        await exchange.ResponseChunks!.Writer.WriteAsync(payload.ToArray(), cancellationToken);
                }
                else
                    exchange.Body.Write(payload);
            }

            // Replenish flow control — batched, not one WINDOW_UPDATE per DATA
            // frame, and for the full payload incl. padding (Section 6.1).
            await ReplenishReceiveWindowsAsync(exchange?.Stream, flowLength);

            if (Frame.EndStream && exchange is not null)
            {
                if (exchange.Stream.IsConnectTunnel)
                {
                    exchange.Stream.TunnelInbound!.Writer.TryComplete();
                    RemoveExchange(Frame.StreamId);
                }
                else if (exchange.IsStreaming)
                    FinalizeStreamingResponse(exchange);   // no trailers — END_STREAM on DATA
                else
                    CompleteResponse(exchange);
            }

        }

        /// <summary>
        /// Return consumed flow-control window to the server in batches — accumulate
        /// per-stream and connection-wide, emitting a WINDOW_UPDATE only once the
        /// accumulated amount crosses half the respective window, instead of one
        /// stream + one connection WINDOW_UPDATE per DATA frame. <paramref name="Stream"/>
        /// is null for DATA on a stream we no longer track (a straggler), where only
        /// the connection window is returned.
        /// </summary>
        private async Task ReplenishReceiveWindowsAsync(HTTP2Stream? Stream, int DataLength)
        {

            if (DataLength <= 0)
                return;

            if (Stream is not null)
            {
                Stream.PendingRecvUpdate += DataLength;
                if (Stream.PendingRecvUpdate >= localSettings.InitialWindowSize / 2)
                {
                    await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(Stream.StreamId, (UInt32) Stream.PendingRecvUpdate));
                    Stream.PendingRecvUpdate = 0;
                }
            }

            connectionPendingRecvUpdate += DataLength;
            if (connectionPendingRecvUpdate >= ConnectionRecvWindowTarget / 2)
            {
                await SendFrameAsync(HTTP2Frame.CreateWindowUpdate(0, (UInt32) connectionPendingRecvUpdate));
                connectionPendingRecvUpdate = 0;
            }

        }

        #endregion


        #region WINDOW_UPDATE / PING / RST_STREAM / GOAWAY

        private void HandleWindowUpdate(HTTP2Frame Frame)
        {

            if (Frame.Length != 4)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR, "WINDOW_UPDATE must be 4 bytes");

            var increment = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload) & 0x7FFFFFFFu;
            if (increment == 0)
                return;

            if (Frame.StreamId == 0)
            {
                lock (flowLock)
                    streamManager.ConnectionSendWindow += increment;
            }
            else
            {
                var exchange = GetExchange(Frame.StreamId);
                if (exchange is not null)
                    lock (flowLock)
                        exchange.Stream.SendWindow += increment;
            }

            SignalWindowChange();

        }

        private async Task HandlePingAsync(HTTP2Frame Frame)
        {
            if (Frame.Length != 8)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR, "PING must be 8 bytes");

            if (Frame.IsAck)
            {
                // A keepalive PING we sent just came back — the connection is alive.
                lock (pingLock)
                {
                    if (pendingPingAck is not null && Frame.Payload.AsSpan().SequenceEqual(pendingPingPayload))
                        pendingPingAck.TrySetResult();
                }
                return;
            }

            await SendFrameAsync(HTTP2Frame.CreatePingAck(Frame.Payload));
        }

        /// <summary>
        /// Optional liveness probe: after each idle interval, send a PING and expect
        /// its ACK back within KeepAliveTimeout. A missing ACK means the connection
        /// (or the network path) is dead — tear it down so waiting requests fail
        /// fast instead of hanging on a silently-broken socket.
        /// </summary>
        private async Task KeepAliveLoopAsync()
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(options.KeepAliveInterval, options.TimeProvider, cancellationToken);

                    // Skip the probe if we've seen inbound traffic recently.
                    if (options.TimeProvider.GetElapsedTime(Interlocked.Read(ref lastActivityTimestamp)) < options.KeepAliveInterval)
                        continue;

                    var payload = new byte[8];
                    System.Security.Cryptography.RandomNumberGenerator.Fill(payload);

                    var ackTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (pingLock) { pendingPingAck = ackTcs; pendingPingPayload = payload; }

                    await SendFrameAsync(HTTP2Frame.CreatePing(payload));

                    try
                    {
                        await ackTcs.Task.WaitAsync(options.KeepAliveTimeout, options.TimeProvider, cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                        FailAllExchanges(new HTTP2ConnectionException(HTTP2ErrorCode.NO_ERROR,
                            "Keepalive PING not acknowledged — connection is unresponsive"));
                        connectionCts.Cancel();
                        return;
                    }
                    finally
                    {
                        lock (pingLock) { pendingPingAck = null; }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Connection ended — nothing to do.
            }
        }

        private void HandleRstStream(HTTP2Frame Frame)
        {
            var exchange = GetExchange(Frame.StreamId);
            if (exchange is null)
                return;

            var code = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload);
            exchange.Stream.Reset();
            RemoveExchange(Frame.StreamId);

            // A tunnel exchange has no buffered-response Completion to fail and is
            // never auto-retried; surface the reset to whoever is opening/using it.
            if (exchange.IsTunnel)
            {
                exchange.TunnelStatus.TrySetException(
                    new HTTP2StreamException(code, Frame.StreamId, $"CONNECT stream reset by server: {code}"));
                SignalWindowChange();
                return;
            }

            // A streaming exchange is likewise never auto-retried (its outbound
            // chunks aren't buffered for replay); surface the reset on the response
            // side (head / body channel / trailers).
            if (exchange.IsStreaming)
            {
                FailStreaming(exchange,
                    new HTTP2StreamException(code, Frame.StreamId, $"Stream reset by server: {code}"));
                SignalWindowChange();
                return;
            }

            if (code == HTTP2ErrorCode.REFUSED_STREAM)
            {
                // RFC 9113 §8.1: REFUSED_STREAM guarantees the request was NOT
                // processed, so it is side-effect-safe to retry verbatim on a fresh
                // stream of this same connection — do so, bounded.
                if (exchange.RetryCount < options.MaxRefusedStreamRetries)
                {
                    exchange.RetryCount++;
                    _ = RetryExchangeAsync(exchange);
                }
                else
                {
                    exchange.Completion.TrySetException(
                        new HTTP2RequestNotProcessedException(code,
                            $"Server refused the stream after {exchange.RetryCount} retries"));
                }
            }
            else
            {
                exchange.Completion.TrySetException(
                    new HTTP2StreamException(code, Frame.StreamId, $"Stream reset by server: {code}"));
            }

            SignalWindowChange();
        }

        private void HandleGoAway(HTTP2Frame Frame)
        {
            var lastStreamId = BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(0, 4)) & 0x7FFFFFFFu;
            var code         = (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(4, 4));

            goawayReceived = true;
            unusable.TrySetResult();   // the peer accepts no new streams — pool should route elsewhere

            // Streams above lastStreamId were definitely not processed (RFC 9113
            // §6.8) — fail them with the retry-safe exception so a caller (or a
            // connection pool) can re-issue them on a new connection.
            List<ClientExchange> abandoned;
            lock (exchangesLock)
                abandoned = exchanges.Where(kv => kv.Key > lastStreamId).Select(kv => kv.Value).ToList();

            foreach (var ex in abandoned)
            {
                RemoveExchange(ex.Stream.StreamId);
                FailExchange(ex,
                    new HTTP2RequestNotProcessedException(code,
                        $"Server sent GOAWAY (lastStreamId={lastStreamId}, {code}) — request not processed"));
            }
        }

        /// <summary>
        /// RFC 8336, Section 2: the server's Origin Set — the origins it claims this
        /// connection is authoritative for. Purely informational here: this client
        /// pools per origin (see <see cref="HTTP2ClientPool"/>) and so never
        /// coalesces requests for a second origin onto an existing connection. It is
        /// surfaced because it is the input such a decision would need.
        ///
        /// Null until (and unless) an ORIGIN frame arrives; the RFC's default is
        /// "the Origin Set is unbounded", which is a different statement from an
        /// announced empty set.
        /// </summary>
        public IReadOnlyList<String>? OriginSet { get; private set; }

        private void HandleOrigin(HTTP2Frame Frame)
        {

            // RFC 8336, Section 2.1: an ORIGIN frame on any stream other than 0 is
            // invalid and MUST be ignored — pointedly not a connection error.
            if (Frame.StreamId != 0)
                return;

            // Section 2.4: the Origin Set only means anything if the connection is
            // authenticated, and h2c authenticates nothing. Learning it over
            // cleartext would let anyone on the path claim any origin.
            if (!isSecure)
                return;

            // Section 2.3: each frame *replaces* nothing — origins accumulate over
            // the life of the connection.
            OriginSet = [.. OriginSet ?? [], .. HTTP2Frame.ParseOrigins(Frame.Payload ?? [])];

        }

        /// <summary>Fail an exchange, routing to the streaming vehicles or the buffered Completion as appropriate.</summary>
        private static void FailExchange(ClientExchange Exchange, Exception Ex)
        {
            if (Exchange.IsStreaming)
                FailStreaming(Exchange, Ex);
            else if (Exchange.IsTunnel)
                Exchange.TunnelStatus.TrySetException(Ex);
            else
                Exchange.Completion.TrySetException(Ex);
        }

        /// <summary>Propagate a failure to a streaming exchange's head, body channel, and trailers.</summary>
        private static void FailStreaming(ClientExchange Exchange, Exception Ex)
        {
            Exchange.ResponseHead?.TrySetException(Ex);
            Exchange.ResponseChunks?.Writer.TryComplete(Ex);
            Exchange.ResponseTrailers?.TrySetException(Ex);
        }

        #endregion


        #region Flow control (send)

        private void SignalWindowChange()
        {
            TaskCompletionSource previous;
            lock (flowLock)
            {
                previous      = windowChanged;
                windowChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            previous.TrySetResult();
        }

        private async Task<int> ReserveSendWindowAsync(HTTP2Stream Stream, int MaxBytes)
        {
            while (true)
            {
                Task waitTask;

                lock (flowLock)
                {
                    if (Stream.State == HTTP2StreamState.Closed)
                        return 0;

                    var available = (int) Math.Min(Math.Min(Stream.SendWindow, streamManager.ConnectionSendWindow), MaxBytes);

                    if (available > 0)
                    {
                        Stream.SendWindow                  -= available;
                        streamManager.ConnectionSendWindow -= available;
                        return available;
                    }

                    waitTask = windowChanged.Task;
                }

                await waitTask.WaitAsync(cancellationToken);
            }
        }

        #endregion


        #region Exchange bookkeeping

        private sealed class ClientExchange
        {
            // The stream this attempt is on — reassigned when a refused request is
            // retried on a fresh stream.
            public HTTP2Stream                          Stream          { get; set; } = null!;

            // Request definition, kept so a REFUSED_STREAM can be re-issued verbatim.
            public required List<(string Name, string Value)> RequestHeaders { get; init; }
            public required byte[]?                     RequestBody     { get; init; }
            public required bool                        HasBody         { get; init; }
            public required CancellationToken           RequestToken    { get; init; }
            public int                                  RetryCount      { get; set; }

            // CONNECT-tunnel exchange: completed with the response :status when the
            // response HEADERS arrive (a 2xx keeps the stream open as a tunnel).
            public bool                                 IsTunnel        { get; init; }
            public TaskCompletionSource<int>            TunnelStatus    { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Streaming exchange (HTTP2ClientStream): the response is delivered
            // incrementally rather than buffered — a head TCS (status + headers), a
            // body-chunk channel, and a trailers TCS, all shared with the handle.
            public bool                                                          IsStreaming      { get; init; }
            public TaskCompletionSource<HTTP2ResponseHead>?                      ResponseHead     { get; init; }
            public Channel<byte[]>?                                             ResponseChunks   { get; init; }
            public TaskCompletionSource<List<(string Name, string Value)>>?      ResponseTrailers { get; init; }

            // Response accumulation (reset between retry attempts).
            public MemoryStream                         HeaderBuffer    { get; } = new();
            public MemoryStream                         Body            { get; } = new();
            public List<(string Name, string Value)>?   Headers         { get; set; }
            public List<(string Name, string Value)>    Trailers        { get; set; } = [];
            public List<(int Status, List<(string Name, string Value)> Headers)> Interim { get; set; } = [];
            public bool                                 HeadersReceived { get; set; }
            public TaskCompletionSource<HTTP2Response>  Completion      { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private ClientExchange? GetExchange(UInt32 StreamId)
        {
            lock (exchangesLock)
                return exchanges.TryGetValue(StreamId, out var ex) ? ex : null;
        }

        private void RemoveExchange(UInt32 StreamId)
        {
            TaskCompletionSource freed;
            lock (exchangesLock)
            {
                exchanges.Remove(StreamId);
                // A stream slot just freed up — wake any request waiting on the
                // MAX_CONCURRENT_STREAMS gate.
                freed           = streamSlotFreed;
                streamSlotFreed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            freed.TrySetResult();
        }

        private void CompleteResponse(ClientExchange Exchange)
        {

            CloseRemoteIfOpen(Exchange.Stream);
            RemoveExchange(Exchange.Stream.StreamId);

            var statusText = Exchange.Headers?.FirstOrDefault(h => h.Name == ":status").Value;

            if (statusText is null || !int.TryParse(statusText, out var status))
            {
                Exchange.Completion.TrySetException(
                    new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, Exchange.Stream.StreamId,
                        "Response missing a valid :status pseudo-header"));
                return;
            }

            var headers = Exchange.Headers!;
            var body    = Exchange.Body.ToArray();
            String? decodedContentEncoding = null;

            // RFC 9110 Section 8.4: undo the content coding we asked for, so the
            // caller sees the identity representation. A body we cannot decode —
            // an unknown coding, or one that blows past MaxDecodedBodySize — must
            // not be passed off as identity, so the failure surfaces on the
            // response task rather than being swallowed.
            if (options.AutomaticDecompression)
            {
                try
                {
                    (body, decodedContentEncoding) = HTTPContentCoding.DecodeBody(headers, body, options.MaxDecodedBodySize);
                }
                catch (Exception ex)
                {
                    Exchange.Completion.TrySetException(ex);
                    return;
                }
            }

            Exchange.Completion.TrySetResult(new HTTP2Response {
                Status                 = status,
                Headers                = headers,
                Body                   = body,
                Trailers               = Exchange.Trailers,
                InformationalResponses = Exchange.Interim,
                DecodedContentEncoding = decodedContentEncoding
            });

        }

        private void FailAllExchanges(Exception Ex)
        {
            List<ClientExchange> all;
            TaskCompletionSource freed;
            lock (exchangesLock)
            {
                all = [.. exchanges.Values];
                exchanges.Clear();
                freed           = streamSlotFreed;
                streamSlotFreed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            freed.TrySetResult();   // release any request blocked on the concurrency gate
            foreach (var ex in all)
                FailExchange(ex, Ex);
        }

        #endregion


        #region Frame I/O + helpers

        private async Task<HTTP2Frame> ReadFrameAsync()
        {
            var header = new byte[HTTP2Frame.HeaderSize];
            await ReadExactAsync(header);

            var frame = HTTP2Frame.ParseHeader(header);

            if (frame.Length > localSettings.MaxFrameSize)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.FRAME_SIZE_ERROR,
                    $"Frame length {frame.Length} exceeds MAX_FRAME_SIZE {localSettings.MaxFrameSize}");

            if (frame.Length > 0)
            {
                frame.Payload = new byte[frame.Length];
                await ReadExactAsync(frame.Payload);
            }

            return frame;
        }

        private async Task ReadExactAsync(byte[] Buffer)
        {
            var offset = 0;
            while (offset < Buffer.Length)
            {
                var read = await transportStream.ReadAsync(Buffer.AsMemory(offset, Buffer.Length - offset), cancellationToken);
                if (read == 0)
                    throw new IOException("Connection closed by peer");
                offset += read;
            }
        }

        private async Task SendFrameAsync(HTTP2Frame Frame)
        {
            var bytes = Frame.Serialize();
            await writeLock.WaitAsync(cancellationToken);
            try
            {
                await transportStream.WriteAsync(bytes, cancellationToken);
                await transportStream.FlushAsync(cancellationToken);
            }
            finally { writeLock.Release(); }
        }

        private async Task SendFramesAsync(IReadOnlyList<HTTP2Frame> Frames)
        {
            await writeLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var frame in Frames)
                    await transportStream.WriteAsync(frame.Serialize(), cancellationToken);
                await transportStream.FlushAsync(cancellationToken);
            }
            finally { writeLock.Release(); }
        }

        private static ReadOnlySpan<byte> StripPadding(HTTP2Frame Frame, ReadOnlySpan<byte> Payload)
        {
            if (!Frame.IsPadded)
                return Payload;
            if (Payload.Length < 1)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "PADDED frame with no pad length");
            var padLength = Payload[0];
            if (padLength >= Payload.Length)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR, "Pad length exceeds payload");
            return Payload.Slice(1, Payload.Length - 1 - padLength);
        }

        private static void CloseLocalIfOpen(HTTP2Stream Stream)
        {
            if (Stream.State is HTTP2StreamState.Open or HTTP2StreamState.HalfClosedRemote)
                Stream.CloseLocal();
        }

        private static void CloseRemoteIfOpen(HTTP2Stream Stream)
        {
            if (Stream.State is HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal)
                Stream.CloseRemote();
        }

        #endregion


        #region Shutdown

        /// <summary>Send a best-effort GOAWAY and tear the connection down.</summary>
        public async Task CloseAsync()
        {
            if (!goawayReceived)
            {
                try
                {
                    await SendFrameAsync(HTTP2Frame.CreateGoAway(streamManager.LastPeerStreamId, HTTP2ErrorCode.NO_ERROR));
                }
                catch { /* best effort */ }
            }

            connectionCts.Cancel();
        }

        #endregion

    }

}
