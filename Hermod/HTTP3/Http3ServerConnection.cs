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

#region Usings

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// An HTTP/3 server (RFC 9114) on top of a <see cref="QuicServerConnection"/>. After the handshake
/// it opens the control stream (with SETTINGS) and the QPACK streams, accepts requests on
/// bidirectional streams (HEADERS via QPACK) and answers them with the given handler
/// (HEADERS + DATA). Transport-agnostic: datagrams via <see cref="GetDatagramsToSend"/> /
/// <see cref="ProcessDatagram"/>.
/// </summary>
public sealed class Http3ServerConnection : IDisposable, IWebTransportHost
{
    private readonly QuicServerConnection _quic;
    // Normalised handler: the synchronous overload is wrapped, so the pump has ONE shape to drive.
    private readonly Func<Http3Request, CancellationToken, Task<Http3Response>> _handler;

    // Optional streaming handler: invoked right after the header section, with the body still
    // arriving. When set, it takes precedence over _handler for ordinary requests.
    private readonly Func<Http3Request, Http3RequestBody, CancellationToken, Task<Http3Response>>? _streamingHandler;
    private readonly Dictionary<ulong, RequestState> _requests = [];
    private readonly Http3Qpack _qpack;
    private bool _http3Initialized;
    private QuicStream? _controlStream;      // our control stream (SETTINGS, GOAWAY)
    private ulong? _goAwayId;                // limit announced via GOAWAY (RFC 9114 §5.2)
    private bool _goAwayPending;             // shutdown requested before the control stream existed
    private ulong? _highestRequestStreamId;  // highest request stream ID accepted so far
    private readonly WebTransportManager _webTransport = new(weAreClient: false);
    private readonly ulong _wtMaxSessions;   // draft-webtrans-http3 §9.2 (0 = WebTransport off)
    private readonly Func<Http3Request, Action<WebTransportSession>?>? _webTransportHandler;
    private readonly Func<Http3Request, IReadOnlyList<string>, string?>? _webTransportProtocolSelector;
    private int _wtSessionCount;

    /// <summary>
    /// Streaming handler: invoked right after the header section, with the request body still
    /// arriving. The <see cref="Http3RequestBody"/> is read asynchronously, so a large upload is
    /// processed as it flows instead of being buffered in full. A handler that reads slowly
    /// throttles the peer via QUIC flow control (see <see cref="Http3RequestBody.HighWatermark"/>).
    /// <para>Note the shifted contract compared with the buffered handlers: it runs BEFORE the
    /// request is complete, so <see cref="Http3Request.Body"/> is empty and trailers are not yet
    /// known. The content-length consistency check (RFC 9114 §4.1.2) therefore happens at the end of
    /// the body; a violation aborts the request stream with H3_MESSAGE_ERROR.</para>
    /// </summary>
    public Http3ServerConnection(
        ServerCertificate certificate,
        Func<Http3Request, Http3RequestBody, CancellationToken, Task<Http3Response>> handler,
        TransportParameters? transportParameters = null,
        bool requireRetry = false,
        ulong qpackMaxTableCapacity = 4096,
        IReadOnlyList<Quic.Tls.NamedGroup>? preferredGroups = null,
        Quic.Tls.ServerResumptionCache? resumptionCache = null,
        uint maxEarlyDataSize = 0,
        Quic.Packets.StatelessResetTokenGenerator? statelessResetTokens = null,
        ulong? maxFieldSectionSize = null,
        ulong? maxRequestBodySize = null,
        Func<Http3Request, Http3ConnectResult>? connectHandler = null,
        bool enableDatagrams = false,
        ulong webTransportMaxSessions = 0,
        Func<Http3Request, Action<WebTransportSession>?>? webTransportHandler = null,
        Func<Http3Request, IReadOnlyList<string>, string?>? webTransportProtocolSelector = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        Quic.Connection.QuicServerConnection.ValidatedRetry? validatedRetry = null,
        Quic.Tls.Handshake.ClientCertificateOptions? clientCertificate = null)
        : this(certificate, _ => new Http3Response { Status = 500 }, transportParameters, requireRetry,
               qpackMaxTableCapacity, preferredGroups, resumptionCache, maxEarlyDataSize, statelessResetTokens,
               maxFieldSectionSize, maxRequestBodySize, connectHandler, enableDatagrams, webTransportMaxSessions,
               webTransportHandler, webTransportProtocolSelector, timeProvider, keyLog, qlog, validatedRetry,
               clientCertificate)
    {
        _streamingHandler = handler;
    }

    /// <summary>
    /// Asynchronous handler (RECOMMENDED for real servers): the request is dispatched, the pump
    /// continues immediately and the response is sent once the task completes. A slow handler
    /// therefore no longer blocks the connection — nor, in <see cref="Http3Server"/>, all the others
    /// sharing the loop.
    /// <para>The <see cref="CancellationToken"/> is cancelled when the client aborts the request
    /// (RFC 9114 §4.1.1) or the connection ends, so the handler can stop work that has become
    /// pointless.</para>
    /// </summary>
    /// <param name="qpackMaxTableCapacity">
    /// Announced maximum QPACK table capacity (RFC 9204). The default of 4096 enables the dynamic
    /// table; a purely static client (capacity 0) never triggers it, so it is interop-safe.
    /// </param>
    /// <param name="maxFieldSectionSize">
    /// Optional limit for the size of accepted field sections (RFC 9114 §4.2.2, uncompressed:
    /// name + value + 32 per field). Announced via SETTINGS_MAX_FIELD_SECTION_SIZE; larger
    /// request header sections are answered with **431 Request Header Fields Too Large**
    /// without invoking the handler. <c>null</c> = unlimited.
    /// </param>
    /// <param name="connectHandler">
    /// Optional handler for Extended CONNECT (RFC 8441/9220): when set, the server announces
    /// SETTINGS_ENABLE_CONNECT_PROTOCOL = 1; per request it decides (e.g. :protocol
    /// "websocket") between acceptance (2xx + <see cref="Http3ConnectResult.OnTunnel"/>) and rejection.
    /// Unknown :protocol values SHOULD be answered with 501 (RFC 9220 §3).
    /// </param>
    /// <param name="webTransportProtocolSelector">
    /// Optional ALPN-like protocol selection per WebTransport session (draft-webtrans-http3 §3.3):
    /// receives the request and the protocols offered via <c>WT-Available-Protocols</c> (preference
    /// first) and picks ONE of them (the 2xx response carries it as <c>WT-Protocol</c>) or <c>null</c>
    /// (no header). A choice outside the offered list is discarded (draft MUST).
    /// </param>
    public Http3ServerConnection(
        ServerCertificate certificate,
        Func<Http3Request, CancellationToken, Task<Http3Response>> handler,
        TransportParameters? transportParameters = null,
        bool requireRetry = false,
        ulong qpackMaxTableCapacity = 4096,
        IReadOnlyList<Quic.Tls.NamedGroup>? preferredGroups = null,
        Quic.Tls.ServerResumptionCache? resumptionCache = null,
        uint maxEarlyDataSize = 0,
        Quic.Packets.StatelessResetTokenGenerator? statelessResetTokens = null,
        ulong? maxFieldSectionSize = null,
        ulong? maxRequestBodySize = null,
        Func<Http3Request, Http3ConnectResult>? connectHandler = null,
        bool enableDatagrams = false,
        ulong webTransportMaxSessions = 0,
        Func<Http3Request, Action<WebTransportSession>?>? webTransportHandler = null,
        Func<Http3Request, IReadOnlyList<string>, string?>? webTransportProtocolSelector = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        Quic.Connection.QuicServerConnection.ValidatedRetry? validatedRetry = null,
        Quic.Tls.Handshake.ClientCertificateOptions? clientCertificate = null)
        : this(certificate, _ => new Http3Response { Status = 500 }, transportParameters, requireRetry,
               qpackMaxTableCapacity, preferredGroups, resumptionCache, maxEarlyDataSize, statelessResetTokens,
               maxFieldSectionSize, maxRequestBodySize, connectHandler, enableDatagrams, webTransportMaxSessions,
               webTransportHandler, webTransportProtocolSelector, timeProvider, keyLog, qlog, validatedRetry,
               clientCertificate)
    {
        _handler = handler;
    }

    /// <summary>
    /// Synchronous handler — convenience for simple servers and the deterministic in-process tests.
    /// The handler runs INLINE on the pump, so a slow handler blocks this connection (and in
    /// <see cref="Http3Server"/> every other one on the same loop). Real servers should use the
    /// asynchronous overload.
    /// </summary>
    public Http3ServerConnection(
        ServerCertificate certificate,
        Func<Http3Request, Http3Response> handler,
        TransportParameters? transportParameters = null,
        bool requireRetry = false,
        ulong qpackMaxTableCapacity = 4096,
        IReadOnlyList<Quic.Tls.NamedGroup>? preferredGroups = null,
        Quic.Tls.ServerResumptionCache? resumptionCache = null,
        uint maxEarlyDataSize = 0,
        Quic.Packets.StatelessResetTokenGenerator? statelessResetTokens = null,
        ulong? maxFieldSectionSize = null,
        ulong? maxRequestBodySize = null,
        Func<Http3Request, Http3ConnectResult>? connectHandler = null,
        bool enableDatagrams = false,
        ulong webTransportMaxSessions = 0,
        Func<Http3Request, Action<WebTransportSession>?>? webTransportHandler = null,
        Func<Http3Request, IReadOnlyList<string>, string?>? webTransportProtocolSelector = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        Quic.Connection.QuicServerConnection.ValidatedRetry? validatedRetry = null,
        Quic.Tls.Handshake.ClientCertificateOptions? clientCertificate = null)
    {
        _connectHandler = connectHandler;
        _wtMaxSessions = webTransportMaxSessions;
        _webTransportHandler = webTransportHandler;
        _webTransportProtocolSelector = webTransportProtocolSelector;
        // WebTransport requires Extended CONNECT + HTTP/3 datagrams (draft §3.1) ⇒ enable both along with it.
        if (webTransportMaxSessions > 0)
            enableDatagrams = true;
        _localDatagramsEnabled = enableDatagrams;
        if (enableDatagrams)
        {
            transportParameters ??= new TransportParameters();
            transportParameters.MaxDatagramFrameSizeValue = 65535; // RFC 9221 §3 RECOMMENDED
        }
        _quic = new QuicServerConnection(certificate, transportParameters, requireRetry: requireRetry, preferredGroups: preferredGroups, resumptionCache: resumptionCache, maxEarlyDataSize: maxEarlyDataSize, statelessResetTokens: statelessResetTokens, timeProvider: timeProvider, keyLog: keyLog, qlog: qlog, validatedRetry: validatedRetry, clientCertificate: clientCertificate);
        _handler = (request, _) => Task.FromResult(handler(request));
        _qpack = new Http3Qpack(qpackMaxTableCapacity, weAreClient: false, FatalConnectionError)
        {
            OnPriorityUpdate = ApplyPriorityUpdate, // RFC 9218 §7.2
            OnWebTransportUniStream = (stream, sessionId, leftover) =>
                _webTransport.ClaimStream(stream, sessionId, leftover, bidirectional: false),
        };
        _localMaxFieldSectionSize = maxFieldSectionSize;
        _maxRequestBodySize = maxRequestBodySize;
    }

    /// <summary>
    /// Upper bound on buffered PRIORITY_UPDATEs for streams not yet opened (RFC 9218 §7:
    /// "bounded by local implementation policy" — only the latest update per stream counts).
    /// </summary>
    private const int MaxPendingPriorityUpdates = 32;
    private readonly Dictionary<ulong, Http3Priority> _pendingPriorityUpdates = [];

    /// <summary>
    /// Applies a PRIORITY_UPDATE (RFC 9218): if the request stream already exists, the send
    /// scheduler is switched directly (the update overrides the <c>priority</c> header);
    /// otherwise the LATEST update is buffered and applied when the stream opens (§7).
    /// </summary>
    private void ApplyPriorityUpdate(ulong streamId, string priorityFieldValue)
    {
        Http3Priority priority = Http3Priority.Parse(priorityFieldValue);
        if (_quic.Streams.TryGetValue(streamId, out QuicStream? stream))
        {
            stream.SendUrgency = priority.Urgency;
            stream.SendIncremental = priority.Incremental;
            if (_requests.TryGetValue(streamId, out RequestState? state))
                state.PriorityUpdated = true; // §7: the update trumps any header
        }
        else if (_pendingPriorityUpdates.Count < MaxPendingPriorityUpdates || _pendingPriorityUpdates.ContainsKey(streamId))
            _pendingPriorityUpdates[streamId] = priority;
    }

    private readonly ulong? _maxRequestBodySize; // RFC 9114 has no limit of its own — ours (413)
    private readonly ulong? _localMaxFieldSectionSize; // our announced limit (RFC 9114 §4.2.2)
    private readonly Func<Http3Request, Http3ConnectResult>? _connectHandler; // Extended CONNECT (RFC 8441/9220)
    private readonly bool _localDatagramsEnabled;      // HTTP datagrams enabled locally (RFC 9297)

    /// <summary>
    /// HTTP datagrams are negotiated on both sides (RFC 9297 §2.1.1 + RFC 9221 §3).
    /// </summary>
    public bool DatagramsNegotiated
        => _localDatagramsEnabled && _qpack.PeerH3Datagram && _quic.PeerMaxDatagramFrameSize > 0;

    /// <summary>
    /// Sends an HTTP datagram for the request stream <paramref name="streamId"/> (RFC 9297 §2.1).
    /// </summary>
    public bool TrySendHttpDatagram(ulong streamId, byte[] payload)
    {
        if (!DatagramsNegotiated ||
            !_requests.TryGetValue(streamId, out RequestState? state) || state.Stream.Send.IsReset)
            return false; // §2.1: only while the send side is open

        var writer = new BufferWriter(payload.Length + 8);
        try
        {
            writer.WriteVarInt(streamId / 4); // Quarter Stream ID
            writer.WriteBytes(payload);
            return _quic.TrySendDatagram(writer.WrittenSpan);
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// Maps received QUIC DATAGRAMs to their request streams (RFC 9297 §2.1).
    /// </summary>
    private void DispatchReceivedDatagrams()
    {
        foreach (byte[] datagram in _quic.TakeReceivedDatagrams())
        {
            var reader = new BufferReader(datagram);
            if (!reader.TryReadVarInt(out ulong quarter))
            {
                FatalConnectionError(Http3Error.DatagramError, "malformed HTTP/3 datagram"); // §2.1
                return;
            }
            if (quarter > (1UL << 60) - 1)
            {
                FatalConnectionError(Http3Error.DatagramError, "quarter stream ID too large"); // §2.1
                return;
            }

            // WebTransport datagram (draft §4.4): the quarter stream ID addresses the CONNECT stream = session.
            if (_webTransport.TryDeliverDatagram(quarter * 4, datagram[reader.Position..]))
                continue;

            if (!_requests.TryGetValue(quarter * 4, out RequestState? state))
                continue; // stream (still) unknown ⇒ drop silently (§2.1 SHALL drop or buffer)

            if (state.Tunnel is { } tunnel)
            {
                if (!state.Stream.IsResetByPeer) // receive side closed ⇒ drop silently (§2.1)
                    tunnel.DeliverDatagram(datagram[reader.Position..]);
            }
            else if (!state.Responded)
            {
                // §2: a datagram for a request without datagram semantics ⇒ terminate the request
                // (STREAM error H3_DATAGRAM_ERROR, no connection error).
                state.Stream.Reset(Http3Error.DatagramError);
                state.Stream.AbortRead(Http3Error.DatagramError);
                state.Responded = true;
            }
        }
    }

    /// <summary>
    /// Reports an HTTP/3 connection error (RFC 9114 §8): CONNECTION_CLOSE type 0x1d with an H3 error code.
    /// </summary>
    private void FatalConnectionError(ulong errorCode, string reason) => _quic.CloseApplication(errorCode, reason);

    // ---- IWebTransportHost (draft-webtrans-http3) -----------------------------------------

    /// <summary>
    /// Initial flow-control limits we grant per session (draft §5.5).
    /// </summary>
    internal ulong LocalInitialMaxStreamsUni { get; init; } = 16;
    internal ulong LocalInitialMaxStreamsBidi { get; init; } = 16;
    internal ulong LocalInitialMaxData { get; init; } = 1_048_576;

    bool IWebTransportHost.FlowControlEnabled => _wtMaxSessions > 1 && _qpack.PeerWtMaxSessions > 1; // §5.1
    ulong IWebTransportHost.LocalInitialMaxStreamsUni => LocalInitialMaxStreamsUni;
    ulong IWebTransportHost.LocalInitialMaxStreamsBidi => LocalInitialMaxStreamsBidi;
    ulong IWebTransportHost.LocalInitialMaxData => LocalInitialMaxData;
    ulong IWebTransportHost.PeerInitialMaxStreamsUni => _qpack.PeerWtInitialMaxStreamsUni;
    ulong IWebTransportHost.PeerInitialMaxStreamsBidi => _qpack.PeerWtInitialMaxStreamsBidi;
    ulong IWebTransportHost.PeerInitialMaxData => _qpack.PeerWtInitialMaxData;

    byte[] IWebTransportHost.ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length)
        => _quic.ExportKeyingMaterial(label, context, length); // RFC 8446 §7.5 / draft §4.7

    QuicStream IWebTransportHost.OpenWebTransportUniStream(ulong sessionId)
    {
        QuicStream stream = _quic.OpenUnidirectionalStream();
        stream.Write(WebTransportStreamHeader(WebTransportConstants.UniStreamType, sessionId)); // 0x54 ‖ session ID
        return stream;
    }

    QuicStream IWebTransportHost.OpenWebTransportBidiStream(ulong sessionId)
    {
        QuicStream stream = _quic.OpenBidirectionalStream();
        stream.Write(WebTransportStreamHeader(WebTransportConstants.BidiStreamSignal, sessionId)); // 0x41 ‖ session ID
        return stream;
    }

    bool IWebTransportHost.SendWebTransportDatagram(ulong sessionId, byte[] payload)
        => TrySendHttpDatagram(sessionId, payload); // §4.4: quarter stream ID = CONNECT stream

    void IWebTransportHost.WriteConnectStreamData(ulong sessionId, byte[] data)
    {
        if (_requests.TryGetValue(sessionId, out RequestState? state))
            state.Stream.Write(Http3Frames.Build(Http3FrameType.Data, data)); // capsules in DATA frames
    }

    void IWebTransportHost.FinishConnectStream(ulong sessionId)
    {
        if (_requests.TryGetValue(sessionId, out RequestState? state))
            state.Stream.Finish();
    }

    private static byte[] WebTransportStreamHeader(ulong signal, ulong sessionId)
    {
        var writer = new BufferWriter(16);
        try
        {
            writer.WriteVarInt(signal);
            writer.WriteVarInt(sessionId);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// <c>true</c> when the handshake was performed via session resumption (PSK).
    /// </summary>
    public bool ResumptionAccepted => _quic.ResumptionAccepted;

    /// <summary>
    /// <c>true</c> when 0-RTT (early_data) was accepted.
    /// </summary>
    public bool EarlyDataAccepted => _quic.EarlyDataAccepted;

    /// <summary>
    /// Insert count of the QPACK encoder table (diagnostics: &gt; 0 ⇒ dynamic table used).
    /// </summary>
    public ulong QpackEncoderInsertCount => _qpack.EncoderInsertCount;

    /// <summary>
    /// Insert count of the QPACK decoder table (diagnostics).
    /// </summary>
    public ulong QpackDecoderInsertCount => _qpack.DecoderInsertCount;

    /// <summary>
    /// Insert count of our encoder table acknowledged by the client via section acks (diagnostics).
    /// </summary>
    public ulong QpackEncoderKnownReceivedCount => _qpack.EncoderKnownReceivedCount;

    public bool HandshakeComplete => _quic.HandshakeComplete;

    /// <summary>
    /// The underlying QUIC server connection (symmetric to <see cref="Http3ClientConnection.Quic"/>).
    /// </summary>
    public QuicServerConnection Quic => _quic;

    /// <summary>
    /// The outcome of client authentication (mutual TLS, RFC 8446 §4.3.2) for this connection.
    /// Under <see cref="ClientCertificateMode.Require"/> a handler only ever runs when this reports
    /// success; under <see cref="ClientCertificateMode.Request"/> the handler decides for itself what
    /// an unauthenticated client may do.
    /// </summary>
    public Quic.Tls.Handshake.ClientAuthenticationResult ClientAuthentication => _quic.ClientAuthentication;

    /// <summary>
    /// <c>true</c> once the server has sent a Retry for address validation.
    /// </summary>
    public bool SentRetry => _quic.SentRetry;

    /// <summary>
    /// <c>true</c> while the connection is closing after our own CONNECTION_CLOSE (RFC 9000 §10.2).
    /// </summary>
    public bool IsClosing => _quic.IsClosing;

    /// <summary>
    /// <c>true</c> after a CONNECTION_CLOSE from the peer was received (draining state).
    /// </summary>
    public bool IsDraining => _quic.IsDraining;

    /// <summary>
    /// <c>true</c> once the connection is finally closed (closing/draining expired after 3·PTO).
    /// </summary>
    public bool IsClosed => _quic.IsClosed;

    /// <summary>
    /// The CONNECTION_CLOSE received from the peer, if any.
    /// </summary>
    public Quic.Frames.ConnectionCloseFrame? PeerCloseFrame => _quic.PeerCloseFrame;

    /// <summary>
    /// Closes the connection immediately with a CONNECTION_CLOSE (RFC 9000 §10.2; default: NO_ERROR).
    /// </summary>
    public void Close(TransportError error = TransportError.NoError, string reason = "") => _quic.Close(error, reason);

    /// <summary>
    /// Initiates the graceful connection shutdown (RFC 9114 §5.2): sends a GOAWAY with the first
    /// request stream ID that is NO longer accepted. In-flight requests are still answered to
    /// completion (<see cref="HasPendingRequests"/>); request streams ≥ the ID arriving later are
    /// reset with H3_REQUEST_REJECTED (safely repeatable on a new connection). Idempotent.
    /// </summary>
    public void InitiateGracefulShutdown()
    {
        if (_goAwayId is not null)
            return; // already announced — the ID must never grow (§5.2)
        if (_controlStream is null)
        {
            _goAwayPending = true; // the control stream only exists after the handshake — catch up then
            return;
        }
        // First no-longer-accepted client-initiated bidi stream ID (…, +4 = next; 0 = none).
        _goAwayId = _highestRequestStreamId is { } highest ? highest + 4 : 0;
        _controlStream.Write(Http3Frames.Build(Http3FrameType.GoAway, BuildVarInt(_goAwayId.Value)));
    }

    /// <summary>
    /// The limit announced in the GOAWAY, if the shutdown was initiated.
    /// </summary>
    public ulong? GoAwaySent => _goAwayId;

    /// <summary>
    /// There are still accepted but unanswered requests (serve to completion after the GOAWAY, §5.2).
    /// </summary>
    public bool HasPendingRequests => _requests.Values.Any(s => !s.ResponseComplete);

    /// <summary>
    /// Number of requests already handed to the handler (and answered).
    /// </summary>
    public int RequestsHandled { get; private set; }

    /// <summary>
    /// Closes the connection after a completed graceful shutdown (RFC 9114 §5.2 SHOULD:
    /// CONNECTION_CLOSE type 0x1d with H3_NO_ERROR).
    /// </summary>
    public void CloseGracefully() => _quic.CloseApplication(Http3Error.NoError, "graceful shutdown");

    private static byte[] BuildVarInt(ulong value)
    {
        var writer = new BufferWriter(8);
        try
        {
            writer.WriteVarInt(value);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// Keep-alive interval (RFC 9000 §10.1.2): sends PINGs against the idle timeout. <c>null</c> = off.
    /// </summary>
    public TimeSpan? KeepAliveInterval
    {
        get => _quic.KeepAliveInterval;
        set => _quic.KeepAliveInterval = value;
    }

    /// <summary>
    /// <c>true</c> when <paramref name="cid"/> is an active local connection ID (CID-based demuxing for migration).
    /// </summary>
    public bool OwnsConnectionId(Quic.Packets.ConnectionId cid) => _quic.OwnsConnectionId(cid);

    /// <summary>
    /// Starts a path validation (RFC 9000 §8.2), e.g. after the client changed its address.
    /// </summary>
    public void InitiatePathValidation() => _quic.InitiatePathValidation();

    /// <summary>
    /// <c>true</c> once the (new) path was confirmed via PATH_CHALLENGE/PATH_RESPONSE.
    /// </summary>
    public bool PathValidated => _quic.PathValidated;

    /// <summary>
    /// <c>true</c> once the connection was silently closed due to the idle timeout (RFC 9000 §10.1).
    /// </summary>
    public bool IsIdleTimedOut => _quic.IsIdleTimedOut;

    /// <summary>
    /// Checks loss-detection/PTO and idle timeouts (call periodically).
    /// </summary>
    public void CheckTimeouts()
    {
        _quic.CheckLossDetectionTimeout();
        _quic.CheckIdleTimeout();
        Pump(); // also drives completed handler tasks and streaming bodies (no traffic needed)
    }

    public IReadOnlyList<byte[]> GetDatagramsToSend() => _quic.GetDatagramsToSend();

    public void ProcessDatagram(ReadOnlySpan<byte> datagram)
    {
        _quic.ProcessDatagram(datagram);
        Pump();
    }

    private void Pump()
    {
        if (_quic.IsClosing || _quic.IsDraining || _quic.IsClosed)
            return; // process nothing further after a connection error

        InitializeHttp3IfReady();

        // Process the client's uni streams (SETTINGS + QPACK encoder instructions).
        _qpack.PumpPeerStreams(_quic.Streams);

        foreach (ulong id in _quic.TakeNewRequestStreams())
        {
            // After the GOAWAY (§5.2): request streams ≥ the announced ID are NOT processed but
            // explicitly aborted (SHOULD) — H3_REQUEST_REJECTED ⇒ safely repeatable.
            if (_goAwayId is { } goAway && id >= goAway)
            {
                QuicStream rejected = _quic.Streams[id];
                rejected.Reset(Http3Error.RequestRejected);
                rejected.AbortRead(Http3Error.RequestRejected);
                continue;
            }
            var newState = new RequestState(_quic.Streams[id]);
            _requests[id] = newState;
            _highestRequestStreamId = _highestRequestStreamId is { } h ? Math.Max(h, id) : id;

            // Apply a PRIORITY_UPDATE received BEFORE the stream was opened (RFC 9218 §7).
            if (_pendingPriorityUpdates.Remove(id, out Http3Priority pending))
            {
                newState.Stream.SendUrgency = pending.Urgency;
                newState.Stream.SendIncremental = pending.Incremental;
                newState.PriorityUpdated = true;
            }
        }

        foreach (RequestState state in _requests.Values)
        {
            if (_quic.IsClosing)
                return; // a connection error was reported
            if (state.Responded)
                continue;

            // Client cancellation (RFC 9114 §4.1.1): RESET_STREAM on the request ⇒ abort the response
            // side as well — H3_REQUEST_REJECTED when nothing was processed yet (the request counts as
            // never sent), otherwise H3_REQUEST_CANCELLED. A STOP_SENDING from the client has already
            // reset our send side automatically (RFC 9000 §3.5).
            if (state.Stream.IsResetByPeer)
            {
                state.Tunnel?.End(); // abrupt tunnel teardown (≙ TCP RST, RFC 9220 §3)
                state.Stream.Reset(state.HeadersReceived ? Http3Error.RequestCancelled : Http3Error.RequestRejected);
                state.Responded = true; // done — there will be no response anymore
                continue;
            }

            // Backpressure for a streaming request body: while the handler has not consumed what it
            // was given, leave the data ON the QUIC stream. Its receive window then stays shut and
            // the peer stops sending — instead of us buffering the upload in memory.
            if (state.RequestBody is { IsSaturated: true })
                continue;

            byte[] chunk = state.Stream.Read();
            if (chunk.Length > 0)
                state.Buffer.Append(chunk);

            // WebTransport CONNECT stream (draft §6/§5.6): after the 2xx response the DATA frames carry
            // capsules (session control/flow control). Do not process them further as HTTP frames.
            if (state.WebTransportSession is { } wtSession)
            {
                ProcessWebTransportConnectStream(state, wtSession);
                continue;
            }

            // WebTransport bidi data stream (draft §4.2): starts with WT_STREAM (0x41) ‖ session ID —
            // NOT an HTTP request. Hand it to the WebTransport manager.
            if (_wtMaxSessions > 0 && !state.HeadersReceived)
            {
                WtBidiResult wt = ClassifyWebTransportBidi(state);
                if (wt == WtBidiResult.Claimed)
                {
                    _webTransportClaimed.Add(state.Stream.Id.Value); // remove from _requests after the loop
                    continue;
                }
                if (wt == WtBidiResult.NeedMore)
                    continue; // header still incomplete — next pump
            }

            // Streaming body: consume DATA frames INCREMENTALLY. A client may pack the whole upload
            // into a single DATA frame — waiting for it to be complete (as TryReadAll does) would
            // buffer exactly what streaming is supposed to avoid.
            if (state.RequestBody is not null)
                ConsumeStreamingData(state);

            // While a DATA frame is open, the buffer starts in the MIDDLE of its payload — the frame
            // parser must not look at it, or it would read payload bytes as a frame header.
            if (state.DataRemaining == 0 && state.Buffer.Count > 0 &&
                Http3Frames.TryReadAll(state.Buffer.Memory, out List<Http3Frame> frames, out int consumed))
            {
                foreach (Http3Frame frame in frames)
                    state.Pending.Enqueue(frame);
                state.Buffer.Consume(consumed);
            }

            // Frame state machine of the request stream (RFC 9114 §4.1, §7.2): HEADERS, then content
            // as a series of DATA frames, optionally a trailer section; a blocked QPACK section
            // stalls the sequence (waiting for the encoder stream).
            while (state.Pending.Count > 0 && !state.Responded)
            {
                Http3Frame frame = state.Pending.Peek();
                // Streaming body: while the handler has not consumed what it already has, the DATA
                // frame stays queued — same mechanism as a blocked QPACK section. Together with the
                // read stop above, the QUIC receive window closes and the peer throttles.
                if (frame.Type == Http3FrameType.Data && state.RequestBody is { IsSaturated: true })
                    break;
                if (!ProcessRequestFrame(state, frame, out bool blocked))
                    return; // connection error reported
                if (blocked)
                    break;
                state.Pending.Dequeue();
            }

            // Tunnel streams (Extended CONNECT): a FIN from the client is the orderly tunnel end
            // (≙ TCP close, RFC 9220 §3) — not a normal end of message.
            if (state.Tunnel is not null)
            {
                if (state.Stream.IsReceiveComplete && state.Pending.Count == 0)
                    state.Tunnel.End();
                continue;
            }

            // Respond as soon as the message is COMPLETE (FIN received, all frames processed) —
            // only then is the request body final (RFC 9114 §4.1).
            // Streaming request: the FIN ends the body, the response comes from the handler task.
            if (state.RequestBody is not null && state.Stream.IsReceiveComplete &&
                state.Pending.Count == 0 && state.DataRemaining == 0 && state.Buffer.Count == 0)
                CompleteStreamingBody(state);

            if (!state.Responded && state.RequestBody is null &&
                state.Stream.IsReceiveComplete && state.Pending.Count == 0)
            {
                // §7.1: a stream ending cleanly in the middle of a frame is an H3_FRAME_ERROR.
                if (state.Buffer.Count > 0)
                {
                    FatalConnectionError(Http3Error.FrameError, "truncated frame at end of stream");
                    return;
                }
                if (state.HeadersReceived && state.Request is not null)
                {
                    // §4.1.2: a present content-length MUST match the sum of the DATA lengths.
                    if (Http3MessageValidator.ValidateContentLength(state.Request.AdditionalHeaders,
                            (ulong)state.Body.Count, contentNeverPresent: false) is { } lengthProblem)
                    {
                        RejectMalformedRequest(state, lengthProblem);
                        continue;
                    }
                    Http3Request request = state.Request;
                    if (state.Body.Count > 0)
                        request = request with { Body = [.. state.Body] };
                    if (state.Trailers.Count > 0)
                        request = request with { Trailers = state.Trailers };
                    // Dispatch: the handler may run asynchronously — the pump does not wait for it,
                    // it polls the task below in PumpResponses.
                    state.Cancellation = new CancellationTokenSource();
                    state.Responded = true; // no further frames of this stream are processed
                    try
                    {
                        state.HandlerTask = _handler(request, state.Cancellation.Token);
                    }
                    catch (Exception)
                    {
                        // A handler that throws synchronously must not kill the connection.
                        state.HandlerTask = Task.FromResult(new Http3Response { Status = 500 });
                    }
                    RequestsHandled++;
                }
            }
        }

        // Remove "requests" recognized as WebTransport bidi streams from request management
        // (the WebTransport manager reads them from now on).
        foreach (ulong claimed in _webTransportClaimed)
            _requests.Remove(claimed);
        _webTransportClaimed.Clear();

        PumpResponses();

        // Tunnel writes: queued by the consumer — possibly on a thread-pool thread — and put on the
        // stream here. Deliberately AFTER the stream processing above, so an answer written by a
        // continuation running inline on a completed read still leaves on this same pass.
        foreach (RequestState state in _requests.Values)
            state.Tunnel?.PumpOutbound();

        // Dispatch HTTP datagrams LAST (RFC 9297 §2.1): this way request streams/tunnels from the
        // same flight are already set up instead of discarding the datagrams as "unknown".
        DispatchReceivedDatagrams();
    }

    private readonly List<ulong> _webTransportClaimed = [];

    private enum WtBidiResult { NotWebTransport, NeedMore, Claimed }

    /// <summary>
    /// Classifies a client-initiated bidi stream: WT_STREAM (0x41) ‖ session ID ⇒ WebTransport
    /// (draft §4.2), handed to the manager; otherwise a normal HTTP request.
    /// </summary>
    private WtBidiResult ClassifyWebTransportBidi(RequestState state)
    {
        var reader = new BufferReader(state.Buffer.Span);
        if (!reader.TryReadVarInt(out ulong signal))
            return WtBidiResult.NeedMore; // first VarInt still incomplete
        if (signal != WebTransportConstants.BidiStreamSignal)
            return WtBidiResult.NotWebTransport;
        if (!reader.TryReadVarInt(out ulong sessionId))
            return WtBidiResult.NeedMore; // session ID still incomplete

        byte[] leftover = state.Buffer.Span[reader.Position..].ToArray();
        _webTransport.ClaimStream(state.Stream, sessionId, leftover, bidirectional: true);
        return WtBidiResult.Claimed;
    }

    /// <summary>
    /// Processes the WebTransport CONNECT stream (draft §5.6/§6): DATA frames carry capsules; their
    /// value bytes are accumulated and passed to the session as capsules. A FIN ends the session.
    /// </summary>
    private void ProcessWebTransportConnectStream(RequestState state, WebTransportSession session)
    {
        if (state.Buffer.Count > 0 &&
            Http3Frames.TryReadAll(state.Buffer.Memory, out List<Http3Frame> frames, out int consumed))
        {
            foreach (Http3Frame frame in frames)
                if (frame.Type == Http3FrameType.Data)
                    state.CapsuleBuffer.Append(frame.Payload.Span);
            state.Buffer.Consume(consumed);
        }
        if (state.CapsuleBuffer.Count > 0)
        {
            List<WebTransportCapsule> capsules = WebTransportCapsule.ReadAll(state.CapsuleBuffer.Memory, out int used);
            foreach (WebTransportCapsule capsule in capsules)
                session.HandleCapsule(capsule);
            state.CapsuleBuffer.Consume(used);
        }
        if ((state.Stream.IsReceiveComplete || state.Stream.IsResetByPeer) && !session.IsClosed)
            session.OnConnectStreamClosed(); // §6: CONNECT stream closed ⇒ session terminated
    }

    private void InitializeHttp3IfReady()
    {
        // 1-RTT write keys are enough — waiting for the client's Finished would delay SETTINGS by a
        // round trip and, worse, put them behind HANDSHAKE_DONE in the same flight. Chrome decides
        // whether a server supports WebTransport the moment the handshake completes, and a client that
        // has not seen SETTINGS by then concludes it does not (net::ERR_METHOD_NOT_SUPPORTED).
        if (_http3Initialized || !_quic.ApplicationKeysInstalled)
            return;

        QuicStream control = _quic.OpenUnidirectionalStream();
        control.SendUrgency = 0; // never starve critical streams behind bulk responses (RFC 9218 §10)
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, BuildSettings()));
        _controlStream = control;

        QuicStream encoderStream = _quic.OpenUnidirectionalStream();
        encoderStream.SendUrgency = 0;
        encoderStream.Write([(byte)Http3StreamType.QpackEncoder]);
        _qpack.SetEncoderStream(encoderStream);
        QuicStream decoderStream = _quic.OpenUnidirectionalStream();
        decoderStream.SendUrgency = 0;
        decoderStream.Write([(byte)Http3StreamType.QpackDecoder]);
        _qpack.SetDecoderStream(decoderStream);

        // Offer the client a spare connection ID (RFC 9000 §5.1), as far as its limit allows.
        _quic.IssueConnectionId();

        _http3Initialized = true;

        // A graceful shutdown requested before the handshake is caught up now (§5.2).
        if (_goAwayPending)
        {
            _goAwayPending = false;
            InitiateGracefulShutdown();
        }
    }

    /// <summary>
    /// Processes ONE frame of the request stream per RFC 9114 §4.1/§7.2. Returns <c>false</c> when
    /// a connection error was reported; <paramref name="blocked"/> indicates a blocked QPACK section
    /// (do not consume the frame yet).
    /// </summary>
    private bool ProcessRequestFrame(RequestState state, Http3Frame frame, out bool blocked)
    {
        blocked = false;

        // WebTransport CONNECT stream (draft §5.6): from now on DATA frames carry capsules — collect
        // those arriving in the same flight as the CONNECT HEADERS section instead of interpreting them as HTTP.
        if (state.WebTransportSession is not null)
        {
            if (frame.Type == Http3FrameType.Data)
                state.CapsuleBuffer.Append(frame.Payload.Span);
            return true;
        }

        // Tunnel mode (Extended CONNECT, RFC 9114 §4.4): after the CONNECT acceptance only DATA
        // frames are allowed on the stream — they carry the tunneled bytes.
        if (state.Tunnel is not null)
        {
            if (frame.Type == Http3FrameType.Data)
            {
                state.Tunnel.Deliver(frame.Payload.ToArray());
                return true;
            }
            if (frame.Type == Http3FrameType.Headers || Http3FrameType.CancelPush == frame.Type ||
                frame.Type is Http3FrameType.Settings or Http3FrameType.PushPromise or Http3FrameType.GoAway
                           or Http3FrameType.MaxPushId or Http3FrameType.PriorityUpdateRequest
                           or Http3FrameType.PriorityUpdatePush ||
                Http3Qpack.IsReservedHttp2FrameType(frame.Type))
            {
                FatalConnectionError(Http3Error.FrameUnexpected, "non-DATA frame on CONNECT stream"); // §4.4
                return false;
            }
            return true; // ignore unknown types (grease/extensions) (§9)
        }

        switch (frame.Type)
        {
            case Http3FrameType.Headers when state.TrailersSeen:
                FatalConnectionError(Http3Error.FrameUnexpected, "HEADERS after trailers"); // §4.1
                return false;
            case Http3FrameType.Headers:
                if (_qpack.TryDecodeHeaders(state.Stream.Id.Value, frame.Payload.Span) is not { } headers)
                {
                    blocked = true;
                    return true;
                }
                // §4.2.2: request header section above our announced limit ⇒ 431 (RFC 6585)
                // WITHOUT invoking the handler; reading the rest is aborted with H3_NO_ERROR (§4.1).
                if (_localMaxFieldSectionSize is { } limit && Http3Qpack.FieldSectionSize(headers) > limit)
                {
                    state.Stream.AbortRead(Http3Error.NoError);
                    SendResponse(state.Stream.Id.Value, state.Stream,
                        new Http3Response { Status = 431 }); // Request Header Fields Too Large
                    state.Responded = true; // ends frame processing for this stream
                    return true;
                }
                if (!state.HeadersReceived)
                {
                    // Malformed check (§4.1.2/§4.3.1): pseudo-header obligations/prohibitions, field rules.
                    if (Http3MessageValidator.ValidateRequestHeaders(headers) is { } problem)
                    {
                        RejectMalformedRequest(state, problem);
                        return true;
                    }
                    state.Request = BuildRequest(headers);
                    state.HeadersReceived = true;

                    // Apply the `priority` header (RFC 9218 §5) to the send scheduler — unless a
                    // PRIORITY_UPDATE has already overridden the priority (§7: the update wins).
                    if (!state.PriorityUpdated &&
                        state.Request.AdditionalHeaders.FirstOrDefault(h => h.Name == "priority") is { Name: "priority" } prio)
                    {
                        Http3Priority parsed = Http3Priority.Parse(prio.Value);
                        state.Stream.SendUrgency = parsed.Urgency;
                        state.Stream.SendIncremental = parsed.Incremental;
                    }

                    // CONNECT is handled IMMEDIATELY (§4.4/RFC 8441): the stream stays open —
                    // waiting for a FIN would be pointless for a tunnel.
                    if (state.Request.Method == HTTPMethod.CONNECT)
                        return HandleConnect(state);

                    // Streaming handler: start right here, with the body still arriving.
                    if (_streamingHandler is not null)
                        StartStreamingHandler(state);
                }
                else
                {
                    // Trailer section (§4.1 item 3); §4.3: pseudo-headers are forbidden in trailers.
                    if (Http3MessageValidator.ValidateTrailers(headers) is { } trailerProblem)
                    {
                        RejectMalformedRequest(state, trailerProblem);
                        return true;
                    }
                    state.TrailersSeen = true;
                    state.Trailers.AddRange(headers);
                }
                return true;

            case Http3FrameType.Data when !state.HeadersReceived || state.TrailersSeen:
                FatalConnectionError(Http3Error.FrameUnexpected, "DATA outside message content"); // §4.1
                return false;
            case Http3FrameType.Data:
                if (state.RequestBody is { } streamingBody)
                {
                    streamingBody.Deliver(frame.Payload.Span); // straight to the handler
                    return true;
                }
                state.Body.AddRange(frame.Payload.ToArray());
                // Body limit (our own — RFC 9114 defines none): a buffered body grows in memory, so
                // an oversized upload is refused with 413 instead of being collected to the end.
                // Streaming handlers are exempt: they decide themselves how much they consume.
                if (_maxRequestBodySize is { } bodyLimit && (ulong)state.Body.Count > bodyLimit)
                {
                    state.Stream.AbortRead(Http3Error.NoError); // §4.1 early-response pattern
                    SendResponse(state.Stream.Id.Value, state.Stream,
                                 new Http3Response { Status = 413 }, state); // Content Too Large
                    state.Responded = true;
                    return true;
                }
                return true;

            case Http3FrameType.Settings:
                FatalConnectionError(Http3Error.FrameUnexpected, "SETTINGS on request stream"); // §7.2.4
                return false;
            case Http3FrameType.GoAway:
                FatalConnectionError(Http3Error.FrameUnexpected, "GOAWAY on request stream");   // §7.2.6
                return false;
            case Http3FrameType.MaxPushId:
                FatalConnectionError(Http3Error.FrameUnexpected, "MAX_PUSH_ID on request stream"); // §7.2.7
                return false;
            case Http3FrameType.CancelPush:
                FatalConnectionError(Http3Error.FrameUnexpected, "CANCEL_PUSH on request stream"); // §7.2.3
                return false;
            case Http3FrameType.PushPromise:
                // §7.2.5: clients NEVER send PUSH_PROMISE; the server MUST close with H3_FRAME_UNEXPECTED.
                FatalConnectionError(Http3Error.FrameUnexpected, "PUSH_PROMISE from client");
                return false;
            case Http3FrameType.PriorityUpdateRequest:
            case Http3FrameType.PriorityUpdatePush:
                // RFC 9218 §7.2: PRIORITY_UPDATE belongs exclusively on the client control stream.
                FatalConnectionError(Http3Error.FrameUnexpected, "PRIORITY_UPDATE on request stream");
                return false;

            default:
                if (Http3Qpack.IsReservedHttp2FrameType(frame.Type))
                {
                    FatalConnectionError(Http3Error.FrameUnexpected, "reserved HTTP/2 frame type"); // §7.2.8
                    return false;
                }
                return true; // ignore unknown types (incl. grease) (§9)
        }
    }

    /// <summary>
    /// Handles a CONNECT request right after the HEADERS (RFC 9114 §4.4, RFC 8441/9220):
    /// classic CONNECT (no :protocol) ⇒ 501 (not supported); Extended CONNECT without the
    /// announced setting ⇒ malformed (the client MUST NOT send it then, RFC 8441 §3);
    /// otherwise the handler decides — 2xx sets up the tunnel, anything else rejects.
    /// </summary>
    private bool HandleConnect(RequestState state)
    {
        Http3Request request = state.Request!;

        // WebTransport (draft-webtrans-http3 §3.2): :protocol = webtransport.
        if (request.Protocol == "webtransport" && _wtMaxSessions > 0)
            return HandleWebTransportConnect(state);

        if (request.Protocol is null || _connectHandler is null)
        {
            if (request.Protocol is not null)
            {
                // Extended CONNECT without SETTINGS_ENABLE_CONNECT_PROTOCOL = 1 ⇒ malformed (RFC 8441 §3).
                RejectMalformedRequest(state, "extended CONNECT without ENABLE_CONNECT_PROTOCOL");
                return true;
            }
            // This server does not support classic CONNECT (proxy tunnel).
            state.Stream.AbortRead(Http3Error.NoError);
            SendResponse(state.Stream.Id.Value, state.Stream, new Http3Response { Status = 501 }, state);
            state.Responded = true;
            return true;
        }

        Http3ConnectResult result = _connectHandler(request);
        var fields = new List<HeaderField> { new(":status", result.Status.ToString()) };
        fields.AddRange(result.Headers);
        state.Stream.Write(Http3Frames.Build(Http3FrameType.Headers,
            _qpack.EncodeHeaders(state.Stream.Id.Value, fields)));

        if (result.Status is >= 200 and < 300 && result.OnTunnel is not null)
        {
            // Accepted: NO FIN — the stream is now the tunnel (bytes in DATA frames, §4.4).
            ulong tunnelStreamId = state.Stream.Id.Value;
            state.Tunnel = new Http3Tunnel(state.Stream)
            {
                DatagramSender = payload => TrySendHttpDatagram(tunnelStreamId, payload), // RFC 9297
            };
            RequestsHandled++;
            result.OnTunnel(state.Tunnel);
        }
        else
        {
            state.Stream.Finish(); // rejected: complete the response
            state.Responded = true;
        }
        return true;
    }

    /// <summary>
    /// Accepts a WebTransport session (draft-webtrans-http3 §3.2): the handler decides between
    /// acceptance (2xx + session callback) and rejection (404). When WT_MAX_SESSIONS is exceeded,
    /// the CONNECT stream is reset with H3_REQUEST_REJECTED (§5.2). WebTransport strictly requires
    /// HTTP/3 and QUIC datagrams — if they are missing, the request is malformed (§3.1).
    /// </summary>
    private bool HandleWebTransportConnect(RequestState state)
    {
        // §3.1: without QUIC/HTTP datagrams a WebTransport request is malformed.
        if (!_localDatagramsEnabled || !_qpack.PeerH3Datagram || _quic.PeerMaxDatagramFrameSize == 0)
        {
            RejectMalformedRequest(state, "WebTransport without datagram support");
            return true;
        }
        // §5.2: more sessions than announced ⇒ reset the CONNECT stream with H3_REQUEST_REJECTED.
        if ((ulong)_wtSessionCount >= _wtMaxSessions)
        {
            state.Stream.Reset(Http3Error.RequestRejected);
            state.Stream.AbortRead(Http3Error.RequestRejected);
            state.Responded = true;
            return true;
        }

        Action<WebTransportSession>? onSession = _webTransportHandler?.Invoke(state.Request!);
        if (onSession is null)
        {
            state.Stream.AbortRead(Http3Error.NoError); // §3.2: no matching resource ⇒ 404
            SendResponse(state.Stream.Id.Value, state.Stream, new Http3Response { Status = 404 }, state);
            state.Responded = true;
            return true;
        }

        // Protocol negotiation (draft §3.3): parse the offered protocols and possibly pick one.
        string? negotiated = NegotiateWebTransportProtocol(state.Request!);

        // Send the 2xx (NO FIN — the CONNECT stream carries capsules from now on); create the session.
        SendHeadersOnly(state.Stream, 200,
            negotiated is null ? null : new HeaderField(WebTransportProtocols.ProtocolHeader,
                                                        WebTransportProtocols.SerializeProtocol(negotiated)));
        var session = new WebTransportSession(state.Stream.Id.Value, this) { NegotiatedProtocol = negotiated };
        state.WebTransportSession = session;
        _webTransport.RegisterSession(session);
        _wtSessionCount++;
        RequestsHandled++;
        onSession(session);
        return true;
    }

    /// <summary>
    /// Evaluates the CONNECT request's <c>WT-Available-Protocols</c> and lets the selector choose
    /// (draft §3.3). Multiple header instances are joined with commas (SF lists may be spread across
    /// several field lines); an invalid field is ignored ENTIRELY (MUST), a selector choice outside
    /// the offered list is discarded (MUST include a single choice from the list).
    /// </summary>
    private string? NegotiateWebTransportProtocol(Http3Request request)
    {
        if (_webTransportProtocolSelector is null)
            return null;
        string? fieldValue = null;
        foreach (HeaderField h in request.AdditionalHeaders)
            if (h.Name == WebTransportProtocols.AvailableProtocolsHeader)
                fieldValue = fieldValue is null ? h.Value : fieldValue + "," + h.Value;
        if (fieldValue is null || !WebTransportProtocols.TryParseProtocolList(fieldValue, out List<string> offered))
            return null;
        string? chosen = _webTransportProtocolSelector(request, offered);
        return chosen is not null && offered.Contains(chosen) ? chosen : null;
    }

    private void SendHeadersOnly(QuicStream stream, int status, HeaderField? extra = null)
    {
        var fields = new List<HeaderField> { new(":status", status.ToString()) };
        if (extra is { } field)
            fields.Add(field);
        stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(stream.Id.Value, fields)));
    }

    /// <summary>
    /// Handles a malformed request (RFC 9114 §4.1.2): stream error H3_MESSAGE_ERROR — the server
    /// MAY send an error response first (we do: 400 Bad Request, without invoking the handler).
    /// </summary>
    private void RejectMalformedRequest(RequestState state, string reason)
    {
        _ = reason;
        state.Stream.AbortRead(Http3Error.MessageError);
        SendResponse(state.Stream.Id.Value, state.Stream, new Http3Response { Status = 400 }, state);
        state.Responded = true; // ends frame processing for this stream
    }

    private static Http3Request BuildRequest(List<HeaderField> headers)
    {

        var     method     = HTTPMethod.GET;
        URIScheme  scheme  = URIScheme.https;
        String  authority  = "";
        String  path       = "/";
        String? protocol   = null;

        var extra = new List<HeaderField>();
        foreach (HeaderField h in headers)
        {
            switch (h.Name)
            {

                case ":method":    method     = HTTPMethod.TryParse(h.Value) ?? HTTPMethod.GET; break;
                case ":scheme":    scheme     = URIScheme.TryParse(h.Value) ?? scheme;          break;
                case ":authority": authority  = h.Value;                                        break;
                case ":path":      path       = h.Value;                                        break;
                case ":protocol":  protocol   = h.Value;                                        break; // Extended CONNECT (RFC 8441 §4)

                default:
                    extra.Add(h);
                    break;

            }
        }

        return new Http3Request(
                   method,
                   scheme,
                   authority,
                   path
               ) {
                     AdditionalHeaders = extra,
                     Protocol          = protocol
                 };

    }

    /// <summary>
    /// Chunk size pulled from a streaming body per read.
    /// </summary>
    private const int BodyChunkSize = 16 * 1024;

    /// <summary>
    /// Backpressure watermark: no new chunk is read while this many bytes are still waiting to go
    /// out on the stream. Without it a fast producer (e.g. a MemoryStream) would be drained into
    /// memory at full speed and streaming would buy nothing.
    /// </summary>
    private const int BodyHighWatermark = 64 * 1024;

    /// <summary>
    /// Starts a streaming handler right after the header section: it receives the request (whose
    /// <see cref="Http3Request.Body"/> stays empty) plus the body reader that the pump feeds from
    /// the DATA frames as they arrive.
    /// </summary>
    private void StartStreamingHandler(RequestState state)
    {
        state.RequestBody = new Http3RequestBody();
        state.Cancellation = new CancellationTokenSource();
        // NOTE: deliberately NOT Responded = true. That flag stops frame processing for the stream —
        // but a streaming request needs exactly that to continue, since its body arrives as DATA
        // frames. The buffered "respond at FIN" path is excluded via RequestBody instead.
        RequestsHandled++;
        try
        {
            state.HandlerTask = _streamingHandler!(state.Request!, state.RequestBody, state.Cancellation.Token);
        }
        catch (Exception)
        {
            state.HandlerTask = Task.FromResult(new Http3Response { Status = 500 });
        }
    }

    /// <summary>
    /// Consumes DATA frames of a streaming request piece by piece: an open frame's payload is handed
    /// to the reader as it arrives, and a following frame header is only opened once the reader has
    /// capacity again. Frames other than DATA are left to the normal parser.
    /// </summary>
    private void ConsumeStreamingData(RequestState state)
    {
        Http3RequestBody body = state.RequestBody!;
        while (state.Buffer.Count > 0)
        {
            if (state.DataRemaining > 0)
            {
                if (body.IsSaturated)
                    return; // reader full ⇒ leave the rest in the buffer/on the stream
                int take = (int)Math.Min(state.DataRemaining, (ulong)state.Buffer.Count);
                body.Deliver(state.Buffer.Memory.Span[..take]);
                state.Buffer.Consume(take);
                state.DataRemaining -= (ulong)take;
                continue;
            }

            // Only open a NEW DATA frame here; everything else belongs to the normal parser.
            if (!Http3Frames.TryReadFrameHeader(state.Buffer.Memory.Span, out ulong type, out ulong length,
                                                out int headerLength) ||
                type != Http3FrameType.Data)
                return;
            if (!state.HeadersReceived || state.TrailersSeen)
                return; // §4.1 violation — let the normal parser report it
            if (body.IsSaturated)
                return;

            state.Buffer.Consume(headerLength);
            state.DataRemaining = length;
        }
    }

    /// <summary>
    /// Ends the body of a streaming request: checks the content-length consistency
    /// (RFC 9114 §4.1.2) and signals end of stream to the reader.
    /// </summary>
    private void CompleteStreamingBody(RequestState state)
    {
        if (state.RequestBody is not { } body || state.BodyCompleted)
            return;
        state.BodyCompleted = true;

        if (Http3MessageValidator.ValidateContentLength(state.Request!.AdditionalHeaders,
                body.TotalReceived, contentNeverPresent: false) is { } problem)
        {
            // The handler is already running — we cannot answer with a 400 any more, so the request
            // stream is aborted with H3_MESSAGE_ERROR (§4.1.2) and the reader sees the error.
            body.Fail(new InvalidOperationException($"malformed request: {problem}"));
            state.Stream.Reset(Http3Error.MessageError);
            state.Stream.AbortRead(Http3Error.MessageError);
            state.Cancellation?.Cancel();
            return;
        }
        body.Complete();
    }

    /// <summary>
    /// Drives everything that cannot be finished synchronously: handler tasks that have completed,
    /// and streaming response bodies. Runs on every pump — i.e. on incoming datagrams AND on the
    /// timer tick, so a response gets out even without any traffic.
    /// </summary>
    private void PumpResponses()
    {
        foreach (RequestState state in _requests.Values.ToList())
        {
            // The client aborted the request (RFC 9114 §4.1.1) ⇒ cancel the handler and stop.
            if (state.Cancellation is { IsCancellationRequested: false } cts &&
                (state.Stream.IsResetByPeer || state.Stream.Send.IsReset))
            {
                // A reader waiting on the body must not hang forever.
                state.RequestBody?.Fail(new OperationCanceledException("the request was aborted"));
                state.BodyCompleted = true;
                cts.Cancel();
                state.DisposeResponseResources();
                state.ResponseComplete = true;
                continue;
            }

            if (state.HandlerTask is { IsCompleted: true } task)
            {
                state.HandlerTask = null;
                Http3Response response = task.Status switch
                {
                    TaskStatus.RanToCompletion => task.Result,
                    // A failed or cancelled handler becomes a 500 — never a connection error.
                    _ => new Http3Response { Status = 500 },
                };
                BeginResponse(state, response);
            }

            if (state.BodyStream is not null)
                PumpResponseBody(state);
        }
    }

    /// <summary>
    /// Streams the response body: one chunk per completed read, and a new read only once the send
    /// buffer has drained below <see cref="BodyHighWatermark"/>. On end of stream the trailers and
    /// the FIN follow.
    /// </summary>
    private void PumpResponseBody(RequestState state)
    {
        // A completed read becomes a DATA frame.
        if (state.BodyRead is { IsCompleted: true } read)
        {
            state.BodyRead = null;
            int count;
            try
            {
                count = read.Status == TaskStatus.RanToCompletion ? read.Result : 0;
            }
            catch (Exception)
            {
                count = 0; // a broken body source ends the body — the headers are already out
            }

            if (count <= 0)
            {
                FinishResponse(state, state.PendingTrailers);
                return;
            }
            state.Stream.Write(Http3Frames.Build(Http3FrameType.Data, state.ReadBuffer.AsSpan(0, count)));
        }

        // Fetch the next chunk only while the peer has room — that is the backpressure.
        if (state.BodyRead is null && state.BodyStream is { } body &&
            state.Stream.Send.PendingBytes < BodyHighWatermark)
        {
            state.ReadBuffer ??= new byte[BodyChunkSize];
            try
            {
                state.BodyRead = body.ReadAsync(state.ReadBuffer.AsMemory()).AsTask();
            }
            catch (Exception)
            {
                FinishResponse(state, state.PendingTrailers);
            }
        }
    }

    /// <summary>
    /// Writes the trailer section (if any) and closes the stream.
    /// </summary>
    private void FinishResponse(RequestState state, IReadOnlyList<HeaderField> trailers)
    {
        ulong? peerLimit = _qpack.PeerMaxFieldSectionSize;
        if (trailers.Count > 0 && // trailer section (§4.1 item 3); oversized trailers are dropped
            (peerLimit is not { } tl || Http3Qpack.FieldSectionSize(trailers) <= tl))
            state.Stream.Write(Http3Frames.Build(Http3FrameType.Headers,
                _qpack.EncodeHeaders(state.Stream.Id.Value, [.. trailers])));
        state.Stream.Finish();
        state.DisposeResponseResources();
        state.ReadBuffer = null;
        state.ResponseComplete = true;
    }

    /// <summary>
    /// Sends the response header section and either the buffered body (immediately) or arms the
    /// streaming body (<see cref="Http3Response.BodyStream"/>).
    /// </summary>
    private void BeginResponse(RequestState state, Http3Response response)
    {
        SendResponse(state.Stream.Id.Value, state.Stream, response, state);
    }

    private void SendResponse(ulong streamId, QuicStream stream, Http3Response response,
                              RequestState? state = null)
    {
        // §4.2.2 SHOULD NOT: do not send a field section above the limit announced by the client.
        ulong? peerLimit = _qpack.PeerMaxFieldSectionSize;

        var fields = new List<HeaderField> { new(":status", response.Status.ToString()) };
        fields.AddRange(response.Headers);
        if (peerLimit is { } lim && Http3Qpack.FieldSectionSize(fields) > lim)
        {
            // The final response would be discarded by the client — send a minimal 500 instead.
            response = new Http3Response { Status = 500 };
            fields = [new(":status", "500")];
        }

        // Interim responses (1xx, §4.1): one separate HEADERS section each BEFORE the final response
        // (oversized interim sections are simply omitted — they are purely advisory).
        foreach (Http3InterimResponse interim in response.InterimResponses)
        {
            var interimFields = new List<HeaderField> { new(":status", interim.Status.ToString()) };
            interimFields.AddRange(interim.Headers);
            if (peerLimit is { } il && Http3Qpack.FieldSectionSize(interimFields) > il)
                continue;
            stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(streamId, interimFields)));
        }

        stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(streamId, fields)));

        // Streaming body: headers are out, the rest is pulled chunk by chunk in PumpResponseBody.
        if (state is not null && response.BodyStream is { } bodyStream)
        {
            state.BodyStream = bodyStream;
            state.PendingTrailers = response.Trailers;
            return;
        }

        if (response.Body.Length > 0)
            stream.Write(Http3Frames.Build(Http3FrameType.Data, response.Body));
        if (response.Trailers.Count > 0 && // trailer section (§4.1 item 3); oversized trailers are dropped
            (peerLimit is not { } tl || Http3Qpack.FieldSectionSize(response.Trailers) <= tl))
            stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(streamId, [.. response.Trailers])));
        stream.Finish();
        if (state is not null)
        {
            state.DisposeResponseResources();
            state.ResponseComplete = true;
        }
    }

    private byte[] BuildSettings()
    {
        var writer = new BufferWriter(16);
        try
        {
            writer.WriteVarInt(Http3Setting.QpackMaxTableCapacity);
            writer.WriteVarInt(_qpack.LocalMaxCapacity);
            writer.WriteVarInt(Http3Setting.QpackBlockedStreams);
            writer.WriteVarInt(_qpack.LocalMaxCapacity > 0 ? 16u : 0u);
            if (_localMaxFieldSectionSize is { } maxFieldSection)
            {
                writer.WriteVarInt(Http3Setting.MaxFieldSectionSize); // RFC 9114 §4.2.2
                writer.WriteVarInt(maxFieldSection);
            }
            if (_connectHandler is not null || _wtMaxSessions > 0)
            {
                writer.WriteVarInt(Http3Setting.EnableConnectProtocol); // RFC 8441 §3 / RFC 9220 §3
                writer.WriteVarInt(1);
            }
            if (_localDatagramsEnabled)
            {
                writer.WriteVarInt(Http3Setting.H3Datagram); // RFC 9297 §2.1.1
                writer.WriteVarInt(1);
            }
            if (_wtMaxSessions > 0) // draft-webtrans-http3 §3.1/§9.2
            {
                writer.WriteVarInt(WebTransportConstants.SettingMaxSessions);
                writer.WriteVarInt(_wtMaxSessions);
                writer.WriteVarInt(WebTransportConstants.SettingMaxSessionsDraft07); // browsers still use this codepoint
                writer.WriteVarInt(_wtMaxSessions);
                writer.WriteVarInt(WebTransportConstants.SettingInitialMaxStreamsUni);
                writer.WriteVarInt(LocalInitialMaxStreamsUni);
                writer.WriteVarInt(WebTransportConstants.SettingInitialMaxStreamsBidi);
                writer.WriteVarInt(LocalInitialMaxStreamsBidi);
                writer.WriteVarInt(WebTransportConstants.SettingInitialMaxData);
                writer.WriteVarInt(LocalInitialMaxData);
            }
            // Grease setting (RFC 9114 §7.2.4.1 SHOULD): 0x1f·N + 0x21 — receivers MUST ignore it.
            writer.WriteVarInt(0x1f * 4 + 0x21);
            writer.WriteVarInt(0);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    public void Dispose() => _quic.Dispose();

    private sealed class RequestState(QuicStream stream)
    {
        public QuicStream Stream { get; } = stream;
        public ByteQueue Buffer { get; } = new();
        public Queue<Http3Frame> Pending { get; } = new();
        public Http3Request? Request { get; set; }
        public List<byte> Body { get; } = [];             // collected DATA frame payloads (request body)
        public List<HeaderField> Trailers { get; } = [];  // trailer section of the request (§4.1 item 3)
        public bool HeadersReceived;
        public bool TrailersSeen { get; set; }     // trailer section seen ⇒ frames after it are illegal (§4.1)
        public bool PriorityUpdated { get; set; }  // PRIORITY_UPDATE received ⇒ trumps the header (RFC 9218 §7)
        public Http3Tunnel? Tunnel { get; set; }   // Extended-CONNECT tunnel (RFC 8441/9220), otherwise null
        public WebTransportSession? WebTransportSession { get; set; } // WebTransport session (draft-webtrans-http3)
        public ByteQueue CapsuleBuffer { get; } = new(); // capsule-protocol bytes of the WT CONNECT stream

        /// <summary>
        /// The response has been produced (or the request rejected) ⇒ stop processing further frames
        /// of this stream. With an async handler this is already true while the handler still runs.
        /// </summary>
        public bool Responded { get; set; }

        /// <summary>
        /// The response is completely on the wire (incl. body/trailers/FIN). Only then is the
        /// request really finished — which is what a graceful shutdown has to wait for.
        /// </summary>
        public bool ResponseComplete { get; set; }

        // --- async handler ---
        public Task<Http3Response>? HandlerTask { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }

        /// <summary>
        /// Body reader of a streaming request (set only with a streaming handler).
        /// </summary>
        public Http3RequestBody? RequestBody { get; set; }

        /// <summary>
        /// End of body already signalled to the reader (FIN or abort).
        /// </summary>
        public bool BodyCompleted { get; set; }

        /// <summary>
        /// Bytes still outstanding of the DATA frame currently being consumed incrementally
        /// (streaming body only). &gt; 0 means: everything arriving next is payload.
        /// </summary>
        public ulong DataRemaining { get; set; }

        // --- streaming response body ---
        public Stream? BodyStream { get; set; }
        public byte[]? ReadBuffer { get; set; }
        public Task<int>? BodyRead { get; set; }
        public IReadOnlyList<HeaderField> PendingTrailers { get; set; } = [];

        public void DisposeResponseResources()
        {
            Cancellation?.Dispose();
            Cancellation = null;
            BodyStream?.Dispose();
            BodyStream = null;
        }
    }
}
