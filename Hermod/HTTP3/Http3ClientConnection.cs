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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// An HTTP/3 client (RFC 9114) on top of a <see cref="QuicClientConnection"/>. Opens the control
/// stream (with SETTINGS) and the QPACK encoder/decoder streams, sends requests as a HEADERS frame
/// on a bidirectional stream and reassembles the response (HEADERS + DATA). Transport-agnostic:
/// datagrams flow in/out via <see cref="GetDatagramsToSend"/> / <see cref="ProcessDatagram"/>.
/// </summary>
public sealed class Http3ClientConnection : IDisposable, IWebTransportHost
{
    private readonly QuicClientConnection _quic;
    private readonly Dictionary<ulong, RequestState> _requests = [];
    private readonly Http3Qpack _qpack;
    private bool _http3Initialized;
    private readonly WebTransportManager _webTransport = new(weAreClient: true);
    private readonly ulong _wtMaxSessions; // draft-webtrans-http3 §9.2 (0 = WebTransport off)

    /// <param name="qpackMaxTableCapacity">
    /// Announced maximum QPACK table capacity (RFC 9204). <c>0</c> (default) = purely static
    /// (interop-safe); &gt; 0 activates the dynamic table once the peer announces one too.
    /// </param>
    /// <param name="maxFieldSectionSize">
    /// Optional limit for the size of accepted field sections (RFC 9114 §4.2.2, uncompressed:
    /// name + value + 32 per field). Announced via SETTINGS_MAX_FIELD_SECTION_SIZE; larger
    /// response header sections are discarded (<see cref="IsResponseTooLarge"/>). <c>null</c> = unlimited.
    /// </param>
    /// <param name="enableDatagrams">
    /// Enable HTTP datagrams (RFC 9297/9221): announces max_datagram_frame_size = 65535 (QUIC TP)
    /// and SETTINGS_H3_DATAGRAM = 1. Usable once the peer has announced both as well
    /// (<see cref="DatagramsNegotiated"/>) — datagrams run over Extended-CONNECT tunnels.
    /// </param>
    public Http3ClientConnection(
        string serverName,
        TransportParameters? transportParameters = null,
        CertificateValidationOptions? certificateValidation = null,
        ulong qpackMaxTableCapacity = 0,
        IReadOnlyList<CipherSuite>? cipherSuites = null,
        IReadOnlyList<NamedGroup>? keyExchangeGroups = null,
        ResumptionTicket? resumptionTicket = null,
        ulong? maxFieldSectionSize = null,
        bool enableDatagrams = false,
        ulong webTransportMaxSessions = 0,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        ServerCertificate? clientCertificate = null,
        byte[]? addressValidationToken = null)
    {
        _wtMaxSessions = webTransportMaxSessions;
        if (webTransportMaxSessions > 0) // WebTransport requires HTTP/3 datagrams (draft §3.1)
            enableDatagrams = true;
        _localDatagramsEnabled = enableDatagrams;
        if (enableDatagrams)
        {
            transportParameters ??= new TransportParameters();
            transportParameters.MaxDatagramFrameSizeValue = 65535; // RFC 9221 §3 RECOMMENDED
        }
        _quic = new QuicClientConnection(serverName, transportParameters, certificateValidation: certificateValidation, cipherSuites: cipherSuites, keyExchangeGroups: keyExchangeGroups, resumptionTicket: resumptionTicket, timeProvider: timeProvider, keyLog: keyLog, qlog: qlog, clientCertificate: clientCertificate, addressValidationToken: addressValidationToken);
        _qpack = new Http3Qpack(qpackMaxTableCapacity, weAreClient: true, FatalConnectionError)
        {
            OnWebTransportUniStream = (stream, sessionId, leftover) =>
                _webTransport.ClaimStream(stream, sessionId, leftover, bidirectional: false),
        };
        _localMaxFieldSectionSize = maxFieldSectionSize;
    }

    private readonly ulong? _localMaxFieldSectionSize; // our announced limit (RFC 9114 §4.2.2)
    private readonly bool _localDatagramsEnabled;      // HTTP datagrams enabled locally (RFC 9297)

    /// <summary>
    /// HTTP datagrams are negotiated on both sides (RFC 9297 §2.1.1: SETTINGS_H3_DATAGRAM sent
    /// AND received; RFC 9221 §3: the peer announced max_datagram_frame_size).
    /// </summary>
    public bool DatagramsNegotiated
        => _localDatagramsEnabled && _qpack.PeerH3Datagram && _quic.PeerMaxDatagramFrameSize > 0;

    /// <summary>
    /// Sends an HTTP datagram for the request stream <paramref name="streamId"/> (RFC 9297 §2.1:
    /// quarter stream ID + payload in a QUIC DATAGRAM frame; unreliable).
    /// </summary>
    public bool TrySendHttpDatagram(ulong streamId, byte[] payload)
    {
        if (!DatagramsNegotiated ||
            !_requests.TryGetValue(streamId, out RequestState? state) || state.Stream.Send.IsReset)
            return false; // §2.1: only with an open send side

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
    /// Matches received QUIC DATAGRAMs to their request streams (RFC 9297 §2.1).
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
                continue; // unknown stream ⇒ drop silently (§2.1 SHALL drop or buffer)

            if (state.Tunnel is { } tunnel)
            {
                if (!state.Stream.IsResetByPeer) // receive side closed ⇒ drop silently (§2.1)
                    tunnel.DeliverDatagram(datagram[reader.Position..]);
            }
            else if (!state.Cancelled)
            {
                // §2: a datagram for a request without datagram semantics ⇒ end the request
                // (STREAM error H3_DATAGRAM_ERROR, no connection error).
                state.Cancelled = true;
                state.Stream.Reset(Http3Error.DatagramError);
                state.Stream.AbortRead(Http3Error.DatagramError);
            }
        }
    }

    /// <summary>
    /// Reports an HTTP/3 connection error (RFC 9114 §8): CONNECTION_CLOSE type 0x1d with an H3 error code.
    /// </summary>
    private void FatalConnectionError(ulong errorCode, string reason) => _quic.CloseApplication(errorCode, reason);

    /// <summary>
    /// The session tickets issued by the server (RFC 8446 §4.6.1) for later resumption.
    /// </summary>
    public IReadOnlyList<Quic.Tls.ResumptionTicket> NewSessionTickets => _quic.NewSessionTickets;

    /// <summary>
    /// <c>true</c> when this connection was established via session resumption (PSK).
    /// </summary>
    public bool ResumptionAccepted => _quic.ResumptionAccepted;

    /// <summary>
    /// <c>true</c> when 0-RTT (early_data) was accepted by the server.
    /// </summary>
    public bool EarlyDataAccepted => _quic.EarlyDataAccepted;

    /// <summary>
    /// The limit announced by the server via SETTINGS_MAX_FIELD_SECTION_SIZE (RFC 9114 §4.2.2);
    /// <c>null</c> = not announced (unlimited). We do not send larger field sections.
    /// </summary>
    public ulong? ServerMaxFieldSectionSize => _qpack.PeerMaxFieldSectionSize;

    /// <summary>
    /// Insert count of the QPACK encoder table (diagnostics: &gt; 0 ⇒ the dynamic table was used).
    /// </summary>
    public ulong QpackEncoderInsertCount => _qpack.EncoderInsertCount;

    /// <summary>
    /// Insert count of the QPACK decoder table (diagnostics).
    /// </summary>
    public ulong QpackDecoderInsertCount => _qpack.DecoderInsertCount;

    public bool HandshakeConfirmed => _quic.HandshakeConfirmed;

    /// <summary>
    /// Number of packet numbers currently held in the application space for ACK generation
    /// (RFC 9000 §13.2.4). Diagnostics/test.
    /// </summary>
    public int ApplicationTrackedReceivedCount => _quic.ApplicationTrackedReceivedCount;

    /// <summary>
    /// <c>true</c> once the connection was closed silently due to the idle timeout (RFC 9000 §10.1).
    /// </summary>
    public bool IsIdleTimedOut => _quic.IsIdleTimedOut;

    /// <summary>
    /// <c>true</c> when a server Retry was processed and the ClientHello was resent.
    /// </summary>
    public bool RetryHandled => _quic.RetryHandled;

    /// <summary>
    /// <c>true</c> while the connection is closing after our own CONNECTION_CLOSE (RFC 9000 §10.2).
    /// </summary>
    public bool IsClosing => _quic.IsClosing;

    /// <summary>
    /// <c>true</c> after a peer CONNECTION_CLOSE was received (draining state).
    /// </summary>
    public bool IsDraining => _quic.IsDraining;

    /// <summary>
    /// <c>true</c> once the connection is finally closed (closing/draining expired after 3·PTO).
    /// </summary>
    public bool IsClosed => _quic.IsClosed;

    /// <summary>
    /// The CONNECTION_CLOSE received from the peer (error code + reason), if any.
    /// </summary>
    public Quic.Frames.ConnectionCloseFrame? PeerCloseFrame => _quic.PeerCloseFrame;

    /// <summary>
    /// Closes the connection immediately with a CONNECTION_CLOSE (RFC 9000 §10.2; default: NO_ERROR).
    /// </summary>
    public void Close(TransportError error = TransportError.NoError, string reason = "") => _quic.Close(error, reason);

    /// <summary>
    /// Closes the connection HTTP/3-conformantly without an error (RFC 9114 §5.2 SHOULD:
    /// CONNECTION_CLOSE type 0x1d with H3_NO_ERROR) — e.g. after the server initiated the teardown via GOAWAY.
    /// </summary>
    public void CloseGracefully() => _quic.CloseApplication(Http3Error.NoError, "graceful shutdown");

    /// <summary>
    /// Keep-alive interval (RFC 9000 §10.1.2): sends PINGs against the idle timeout. <c>null</c> = off.
    /// </summary>
    public TimeSpan? KeepAliveInterval
    {
        get => _quic.KeepAliveInterval;
        set => _quic.KeepAliveInterval = value;
    }

    /// <summary>
    /// Starts a path validation (RFC 9000 §8.2), the basis of connection migration.
    /// </summary>
    public void InitiatePathValidation() => _quic.InitiatePathValidation();

    /// <summary>
    /// <c>true</c> once the path was confirmed via PATH_CHALLENGE/PATH_RESPONSE.
    /// </summary>
    public bool PathValidated => _quic.PathValidated;

    /// <summary>
    /// A path validation is in progress (answer outstanding).
    /// </summary>
    public bool PathValidationPending => _quic.PathValidationPending;

    /// <summary>
    /// Access to the underlying QUIC connection (diagnostics).
    /// </summary>
    public QuicClientConnection Quic => _quic;

    public void Start() => _quic.Start();

    /// <summary>
    /// Checks loss-detection/PTO and idle timeouts (call periodically).
    /// </summary>
    public void CheckTimeouts()
    {
        _quic.CheckLossDetectionTimeout();
        _quic.CheckIdleTimeout();
        // Like the server: work that is waiting to go OUT must not depend on something coming IN.
        // Tunnel writes are queued and drained by the pump (they are marshalled off the caller's
        // thread), and Pump used to run only on a received datagram — so an application that wrote
        // into a tunnel while the peer happened to be silent had its data sit in the queue until the
        // peer sent something of its own. Acknowledgments used to hide that by keeping traffic
        // flowing in both directions at all times.
        Pump();
    }

    public IReadOnlyList<byte[]> GetDatagramsToSend() => _quic.GetDatagramsToSend();

    /// <summary>
    /// Processes a datagram and then pumps the HTTP/3 stream data along.
    /// </summary>
    public void ProcessDatagram(ReadOnlySpan<byte> datagram)
    {
        _quic.ProcessDatagram(datagram);
        Pump();
    }

    /// <summary>
    /// Opens control + QPACK streams and sends the SETTINGS (once, after the handshake).
    /// </summary>
    public void InitializeHttp3()
    {
        if (_http3Initialized)
            return;

        // Control stream: type 0x00, then a SETTINGS frame (announcing our QPACK capacity).
        QuicStream control = _quic.OpenUnidirectionalStream();
        control.SendUrgency = 0; // never let critical streams starve behind bulk data (RFC 9218 §10)
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, BuildSettings()));
        _controlStream = control;

        // QPACK encoder stream (for insert instructions) + decoder stream (type prefix suffices).
        QuicStream encoderStream = _quic.OpenUnidirectionalStream();
        encoderStream.SendUrgency = 0;
        encoderStream.Write([(byte)Http3StreamType.QpackEncoder]);
        _qpack.SetEncoderStream(encoderStream);
        QuicStream decoderStream = _quic.OpenUnidirectionalStream();
        decoderStream.SendUrgency = 0;
        decoderStream.Write([(byte)Http3StreamType.QpackDecoder]);
        _qpack.SetDecoderStream(decoderStream);

        _http3Initialized = true;
    }

    private QuicStream? _controlStream; // our control stream (SETTINGS, PRIORITY_UPDATE)

    /// <summary>
    /// The server permits Extended CONNECT (SETTINGS_ENABLE_CONNECT_PROTOCOL = 1, RFC 8441/9220).
    /// </summary>
    public bool ServerEnablesConnectProtocol => _qpack.PeerEnableConnectProtocol;

    /// <summary>
    /// Sends an Extended CONNECT (RFC 8441 §4 / RFC 9220), e.g. with <paramref name="protocol"/> =
    /// "websocket". The request stream stays open — after a 2xx response it becomes the tunnel
    /// (<see cref="TryGetConnectResponse"/>). Without the server setting no Extended CONNECT may be
    /// sent (RFC 8441 §3 MUST NOT) — an <see cref="InvalidOperationException"/> is thrown then.
    /// </summary>
    public ulong SendExtendedConnect(string authority, string path, string protocol,
                                     IReadOnlyList<HeaderField>? headers = null, string scheme = "https")
    {
        if (!_qpack.PeerEnableConnectProtocol)
            throw new InvalidOperationException(
                "The server has not announced SETTINGS_ENABLE_CONNECT_PROTOCOL (RFC 8441 §3) — Extended CONNECT is not permitted.");
        if (_qpack.GoAwayId is not null)
            throw new InvalidOperationException("GOAWAY received (RFC 9114 §5.2): no new requests on this connection.");

        var fields = new List<HeaderField>
        {
            new(":method", "CONNECT"),
            new(":scheme", scheme),
            new(":authority", authority),
            new(":path", path),
            new(":protocol", protocol),
        };
        if (headers is not null)
            fields.AddRange(headers);
        if (Http3MessageValidator.ValidateRequestHeaders(fields) is { } malformed)
            throw new ArgumentException($"Malformed Extended CONNECT (RFC 8441 §4): {malformed}");

        QuicStream stream = _quic.OpenBidirectionalStream();
        stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(stream.Id.Value, fields)));
        // NO Finish: the stream subsequently carries the tunnel bytes (RFC 9114 §4.4).

        _requests[stream.Id.Value] = new RequestState(stream) { Method = HTTPMethod.CONNECT, IsConnect = true };
        return stream.Id.Value;
    }

    // ---- WebTransport (draft-ietf-webtrans-http3) -----------------------------------------

    /// <summary>
    /// The server supports WebTransport (SETTINGS_WT_MAX_SESSIONS &gt; 0 + Extended CONNECT + datagrams).
    /// </summary>
    public bool ServerSupportsWebTransport
        => _qpack.PeerWtMaxSessions > 0 && _qpack.PeerEnableConnectProtocol && DatagramsNegotiated;

    /// <summary>
    /// Opens a WebTransport session (draft-webtrans-http3 §3.2): sends an Extended CONNECT with
    /// <c>:protocol = webtransport</c>. Returns the CONNECT stream ID (= session ID); the session
    /// itself is available after the 2xx response via <see cref="TryGetWebTransportSession"/>.
    /// With <paramref name="availableProtocols"/> (preference first) an ALPN-like application
    /// protocol is negotiated (draft §3.3, header <c>WT-Available-Protocols</c>); the server pick is
    /// afterwards in <see cref="WebTransportSession.NegotiatedProtocol"/>.
    /// </summary>
    public ulong ConnectWebTransport(string authority, string path, IReadOnlyList<HeaderField>? headers = null,
                                     IReadOnlyList<string>? availableProtocols = null)
    {
        if (!ServerSupportsWebTransport)
            throw new InvalidOperationException("The server does not support WebTransport (draft §3.1: WT_MAX_SESSIONS/datagrams missing).");
        if (availableProtocols is { Count: > 0 })
        {
            var combined = new List<HeaderField>(headers ?? [])
            {
                new(WebTransportProtocols.AvailableProtocolsHeader,
                    WebTransportProtocols.SerializeProtocolList(availableProtocols)),
            };
            headers = combined;
        }
        ulong streamId = SendExtendedConnect(authority, path, "webtransport", headers);
        _requests[streamId].IsWebTransport = true;
        _requests[streamId].OfferedWtProtocols = availableProtocols is { Count: > 0 } ? [.. availableProtocols] : null;
        return streamId;
    }

    /// <summary>
    /// Returns the WebTransport session once the server accepted the CONNECT with 2xx; otherwise
    /// (not yet there or rejected) <c>false</c>.
    /// </summary>
    public bool TryGetWebTransportSession(ulong streamId, out WebTransportSession? session)
    {
        session = null;
        if (_requests.TryGetValue(streamId, out RequestState? state) && state.WebTransportSession is { } s)
        {
            session = s;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The status of the WebTransport CONNECT response (e.g. 404 when rejected), once received.
    /// </summary>
    public int? WebTransportConnectStatus(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) ? state.ConnectStatus : null;

    /// <summary>
    /// Evaluates the <c>WT-Protocol</c> header of the 2xx response (draft §3.3). The field MUST be
    /// ignored when we offered nothing, when it occurs more than once (then it would no longer be a
    /// single SF item), when it is not an SF string, or when the server pick is not from our offer list.
    /// </summary>
    private static string? SelectNegotiatedProtocol(RequestState state, List<HeaderField> headers)
    {
        if (state.OfferedWtProtocols is not { Count: > 0 } offered)
            return null;
        string? value = null;
        foreach (HeaderField h in headers)
        {
            if (h.Name != WebTransportProtocols.ProtocolHeader)
                continue;
            if (value is not null)
                return null; // multiple ⇒ ignore
            value = h.Value;
        }
        if (value is null || !WebTransportProtocols.TryParseProtocol(value, out string chosen))
            return null;
        return offered.Contains(chosen) ? chosen : null; // MUST come from the offer list
    }

    /// <summary>
    /// Returns status (and headers) of the Extended-CONNECT response once it is there; on 2xx
    /// additionally the ready-to-use <see cref="Http3Tunnel"/> (otherwise <c>null</c> — the CONNECT
    /// was rejected).
    /// </summary>
    public bool TryGetConnectResponse(ulong streamId, out int status, out IReadOnlyList<HeaderField> headers, out Http3Tunnel? tunnel)
    {
        status = 0;
        headers = [];
        tunnel = null;
        if (!_requests.TryGetValue(streamId, out RequestState? state) || state.ConnectStatus is not { } connectStatus)
            return false;
        status = connectStatus;
        headers = state.Headers;
        tunnel = state.Tunnel;
        return true;
    }

    /// <summary>
    /// Sends a PRIORITY_UPDATE (RFC 9218 §7.2) for a running request — e.g. to make a prefetch
    /// (u=7) urgent afterwards (u=0). The signal overrides the <c>priority</c> header on the server
    /// side; the most recently received update wins.
    /// </summary>
    public void SendPriorityUpdate(ulong streamId, Http3Priority priority)
    {
        if (_controlStream is null)
            throw new InvalidOperationException("Call InitializeHttp3() first — PRIORITY_UPDATE runs over the control stream.");

        var writer = new BufferWriter(16);
        try
        {
            writer.WriteVarInt(streamId); // Prioritized Element ID
            byte[] fieldValue = System.Text.Encoding.ASCII.GetBytes(priority.ToHeaderValue());
            _controlStream.Write(Http3Frames.Build(Http3FrameType.PriorityUpdateRequest,
                [.. writer.WrittenSpan.ToArray(), .. fieldValue]));
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// The stream ID announced by the server via GOAWAY (RFC 9114 §5.2): requests with this ID or
    /// greater are not processed; new requests are no longer allowed on this connection.
    /// </summary>
    public ulong? GoAwayStreamId => _qpack.GoAwayId;

    /// <summary>
    /// <c>true</c> when the request was rejected by the server via GOAWAY (stream ID ≥ GOAWAY ID,
    /// RFC 9114 §5.2) — it was guaranteed NOT to have been processed and may be repeated safely on
    /// a new connection.
    /// </summary>
    public bool IsRequestRejected(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) && state.Rejected;

    /// <summary>
    /// Sends a request on a new bidirectional stream. Returns its stream ID.
    /// A non-empty <see cref="Http3Request.Body"/> follows the HEADERS frame as a DATA frame
    /// (RFC 9114 §4.1: header section, then content as a series of DATA frames), then FIN.
    /// After a received GOAWAY, new requests are forbidden (RFC 9114 §5.2 MUST NOT) —
    /// an <see cref="InvalidOperationException"/> is thrown then; repeat on a NEW connection.
    /// </summary>
    public ulong SendRequest(Http3Request request)
    {
        if (_qpack.GoAwayId is not null)
            throw new InvalidOperationException(
                "GOAWAY received (RFC 9114 §5.2): no new requests on this connection — establish a new connection.");

        // §4.1.2 MUST NOT generate: reject our own malformed requests locally already.
        if (Http3MessageValidator.ValidateRequestHeaders(request.ToHeaderFields()) is { } malformed)
            throw new ArgumentException($"Malformed request (RFC 9114 §4.1.2): {malformed}");
        if (request.Trailers.Count > 0 && Http3MessageValidator.ValidateTrailers(request.Trailers) is { } badTrailer)
            throw new ArgumentException($"Malformed request trailers (RFC 9114 §4.3): {badTrailer}");

        // §4.2.2 SHOULD NOT: send no field section above the limit announced by the server.
        if (_qpack.PeerMaxFieldSectionSize is { } peerLimit)
        {
            if (Http3Qpack.FieldSectionSize(request.ToHeaderFields()) > peerLimit)
                throw new ArgumentException(
                    $"The request headers exceed the server's SETTINGS_MAX_FIELD_SECTION_SIZE ({peerLimit} bytes, RFC 9114 §4.2.2).");
            if (request.Trailers.Count > 0 && Http3Qpack.FieldSectionSize(request.Trailers) > peerLimit)
                throw new ArgumentException(
                    $"The request trailers exceed the server's SETTINGS_MAX_FIELD_SECTION_SIZE ({peerLimit} bytes, RFC 9114 §4.2.2).");
        }

        QuicStream stream = _quic.OpenBidirectionalStream();
        if (request.Priority is { } priority) // RFC 9218 §9: also use locally for sending the request
        {
            stream.SendUrgency = priority.Urgency;
            stream.SendIncremental = priority.Incremental;
        }
        byte[] headerBlock = _qpack.EncodeHeaders(stream.Id.Value, request.ToHeaderFields());
        stream.Write(Http3Frames.Build(Http3FrameType.Headers, headerBlock));
        if (request.Body.Length > 0)
            stream.Write(Http3Frames.Build(Http3FrameType.Data, request.Body));
        if (request.Trailers.Count > 0) // trailer section: final HEADERS frame (§4.1 item 3)
            stream.Write(Http3Frames.Build(Http3FrameType.Headers, _qpack.EncodeHeaders(stream.Id.Value, [.. request.Trailers])));
        stream.Finish(); // end of the message ⇒ FIN (the QUIC layer packetises the stream itself)

        _requests[stream.Id.Value] = new RequestState(stream) { Method = request.Method };
        return stream.Id.Value;
    }

    /// <summary>
    /// Cancels a running request (RFC 9114 §4.1.1): resets our own send side and aborts reading the
    /// response (STOP_SENDING) — both with <c>H3_REQUEST_CANCELLED</c> (clients SHOULD use this
    /// code). An already-complete response stays usable.
    /// </summary>
    public void CancelRequest(ulong streamId)
    {
        if (!_requests.TryGetValue(streamId, out RequestState? state) || state.Complete || state.Cancelled)
            return;
        state.Cancelled = true;
        state.Stream.Reset(Http3Error.RequestCancelled);     // end the send side abruptly (§4.1.1)
        state.Stream.AbortRead(Http3Error.RequestCancelled); // abort reading the response (§4.1.1)
    }

    /// <summary>
    /// <c>true</c> when the request was cancelled — by us (<see cref="CancelRequest"/>) or by the
    /// server (RESET_STREAM on the response side, e.g. <c>H3_REQUEST_REJECTED</c>). A partial response
    /// SHOULD not be used then (RFC 9114 §4.1.1); <see cref="TryGetResponse"/> returns nothing.
    /// </summary>
    public bool IsRequestCancelled(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) &&
           (state.Cancelled || (!state.Complete && state.Stream.IsResetByPeer));

    /// <summary>
    /// The error code with which the server reset the response side (e.g. 0x010b =
    /// H3_REQUEST_REJECTED ⇒ the request counts as never sent and may be repeated), otherwise <c>null</c>.
    /// </summary>
    public ulong? RequestResetErrorCode(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) ? state.Stream.PeerResetErrorCode : null;

    /// <summary>
    /// Returns the finished response of a request stream once it has been received completely.
    /// </summary>
    public bool TryGetResponse(ulong streamId, out Http3Response? response)
    {
        response = null;
        if (!_requests.TryGetValue(streamId, out RequestState? state) || !state.Complete)
            return false;

        response = new Http3Response
        {
            Status = ParseStatus(state.Headers),
            Headers = state.Headers,
            Body = [.. state.Body],
            InterimResponses = state.Interim,
            Trailers = state.Trailers,
        };
        return true;
    }

    // ---- HTTP/3 receiving -----------------------------------------------------------------

    private void Pump()
    {

        if (_quic.IsClosing || _quic.IsDraining || _quic.IsClosed)
            return; // process nothing more after a connection error

        // First process the server's uni streams (SETTINGS + QPACK encoder instructions).
        _qpack.PumpPeerStreams(_quic.Streams);

        foreach (RequestState state in _requests.Values)
        {

            if (_quic.IsClosing)
                return; // a connection error was reported

            // GOAWAY (§5.2): requests with a stream ID ≥ the announced ID are not processed —
            // mark them "rejected" (safely repeatable) and clean up the transport state.
            if (!state.Complete && !state.Rejected &&
                _qpack.GoAwayId is { } goAway && state.Stream.Id.Value >= goAway)
            {
                state.Rejected = true;
                state.Stream.Reset(Http3Error.RequestCancelled);
                state.Stream.AbortRead(Http3Error.RequestCancelled);
                continue;
            }

            if (state.Tunnel is not null && state.Stream.IsResetByPeer)
            {
                state.Tunnel.End(); // abrupt tunnel abort (≙ TCP RST, RFC 9220 §3)
                continue;
            }
            if (state.Cancelled || state.Rejected || state.Malformed || state.TooLarge || state.Stream.IsResetByPeer)
                continue; // cancelled (§4.1.1), malformed (§4.1.2) or too large (§4.2.2) – do not process further

            byte[] chunk = state.Stream.Read();
            if (chunk.Length > 0)
                state.Buffer.Append(chunk);

            // WebTransport CONNECT stream (draft §5.6/§6): after the 2xx response, DATA frames carry capsules.
            if (state.WebTransportSession is { } wtSession)
            {
                ProcessWebTransportConnectStream(state, wtSession);
                continue;
            }

            if (state.Buffer.Count > 0 &&
                Http3Frames.TryReadAll(state.Buffer.Memory, out List<Http3Frame> frames, out int consumed))
            {
                foreach (Http3Frame frame in frames)
                    state.Pending.Enqueue(frame);
                state.Buffer.Consume(consumed);
            }

            // Frame state machine of the request stream (RFC 9114 §4.1, §7.2): interim sections (1xx),
            // final HEADERS, then DATA, optionally a trailer section; blocked sections pause.
            while (state.Pending.Count > 0 && !state.Malformed && !state.TooLarge)
            {
                Http3Frame frame = state.Pending.Peek();
                if (!ProcessResponseFrame(state, frame, out bool blocked))
                    return; // connection error reported
                if (blocked)
                    break;  // wait for more QPACK encoder-stream data
                state.Pending.Dequeue();
            }

            // Tunnel streams: a server FIN is the orderly tunnel end (RFC 9220 §3).
            if (state.Tunnel is not null)
            {
                if (state.Stream.IsReceiveComplete && state.Pending.Count == 0)
                    state.Tunnel.End();
                continue;
            }

            if (!state.Malformed && !state.TooLarge && state.Stream.IsReceiveComplete && state.Pending.Count == 0)
            {
                // §7.1: if the stream ends cleanly mid-frame, that is an H3_FRAME_ERROR.
                if (state.Buffer.Count > 0)
                {
                    FatalConnectionError(Http3Error.FrameError, "truncated frame at end of stream");
                    return;
                }
                // §4.1.2: content-length MUST match the DATA sum — unless the response is body-less
                // by definition (HEAD, 204, 304) and indeed no content arrived.
                int finalStatus = ParseStatus(state.Headers);
                bool contentNeverPresent = state.Method == HTTPMethod.HEAD || finalStatus is 204 or 304;
                if (Http3MessageValidator.ValidateContentLength(state.Headers, (ulong)state.Body.Count, contentNeverPresent) is { } lengthProblem)
                {
                    MarkMalformed(state, lengthProblem);
                    continue;
                }
                state.Complete = true;
            }
        }

        // Tunnel writes: queued by the consumer — possibly on a thread-pool thread — and put on the
        // stream here. Deliberately AFTER the loop above, so an answer written by a continuation
        // running inline on a completed read still leaves on this same pass.
        foreach (RequestState state in _requests.Values)
            state.Tunnel?.PumpOutbound();

        // Server-initiated bidi streams (draft §4.2): WT_STREAM (0x41) ‖ session ID ⇒ WebTransport.
        if (_wtMaxSessions > 0)
            RouteServerInitiatedWebTransportBidi();

        // Match HTTP datagrams LAST (RFC 9297 §2.1): tunnels from the same flight are then already
        // created, instead of dropping the datagrams as "unknown".
        DispatchReceivedDatagrams();

    }

    private readonly Dictionary<ulong, List<byte>> _serverBidiHeaders = []; // header buffers of server-initiated bidi streams

    /// <summary>
    /// Routes server-initiated bidirectional streams (draft §4.2): their header is WT_STREAM (0x41) ‖
    /// session ID; the WebTransport manager takes over afterwards.
    /// </summary>
    private void RouteServerInitiatedWebTransportBidi()
    {
        foreach ((ulong id, QuicStream stream) in _quic.Streams)
        {
            if (!stream.Id.IsServerInitiated || !stream.Id.IsBidirectional || _routedServerBidi.Contains(id))
                continue;
            if (!_serverBidiHeaders.TryGetValue(id, out List<byte>? buffer))
                _serverBidiHeaders[id] = buffer = [];
            buffer.AddRange(stream.Read());

            var reader = new BufferReader(buffer.ToArray());
            if (!reader.TryReadVarInt(out ulong signal))
                continue; // header still incomplete
            if (signal != WebTransportConstants.BidiStreamSignal || !reader.TryReadVarInt(out ulong sessionId))
            {
                if (signal != WebTransportConstants.BidiStreamSignal)
                {
                    _routedServerBidi.Add(id); // not a WT stream ⇒ no longer consider it
                    _serverBidiHeaders.Remove(id);
                }
                continue;
            }
            _routedServerBidi.Add(id);
            _serverBidiHeaders.Remove(id);
            _webTransport.ClaimStream(stream, sessionId, buffer.Skip(reader.Position).ToArray(), bidirectional: true);
        }
    }

    private readonly HashSet<ulong> _routedServerBidi = [];

    /// <summary>
    /// Processes the WebTransport CONNECT stream (draft §5.6/§6): DATA frames carry capsules; their
    /// value bytes are accumulated and handed to the session as capsules. FIN ends the session.
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
            session.OnConnectStreamClosed(); // §6: CONNECT stream closed ⇒ session ended
    }

    // ---- IWebTransportHost (draft-webtrans-http3) -----------------------------------------

    /// <summary>Initial flow-control limits we grant per session (draft §5.5).</summary>
    internal ulong LocalInitialMaxStreamsUni    { get; init; } = 16;
    internal ulong LocalInitialMaxStreamsBidi   { get; init; } = 16;
    internal ulong LocalInitialMaxData          { get; init; } = 1_048_576;

    bool  IWebTransportHost.FlowControlEnabled          => _wtMaxSessions > 1 && _qpack.PeerWtMaxSessions > 1; // §5.1
    ulong IWebTransportHost.LocalInitialMaxStreamsUni   => LocalInitialMaxStreamsUni;
    ulong IWebTransportHost.LocalInitialMaxStreamsBidi  => LocalInitialMaxStreamsBidi;
    ulong IWebTransportHost.LocalInitialMaxData         => LocalInitialMaxData;
    ulong IWebTransportHost.PeerInitialMaxStreamsUni    => _qpack.PeerWtInitialMaxStreamsUni;
    ulong IWebTransportHost.PeerInitialMaxStreamsBidi   => _qpack.PeerWtInitialMaxStreamsBidi;
    ulong IWebTransportHost.PeerInitialMaxData          => _qpack.PeerWtInitialMaxData;

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
    /// Processes ONE frame of the response stream per RFC 9114 §4.1/§7.2. Returns <c>false</c> when
    /// a connection error was reported; <paramref name="blocked"/> indicates a blocked QPACK section
    /// (do not consume the frame yet).
    /// </summary>
    private bool ProcessResponseFrame(RequestState state, Http3Frame frame, out bool blocked)
    {
        blocked = false;

        // WebTransport CONNECT stream (draft §5.6): DATA frames carry capsules — collect those
        // arriving in the same flight as the 2xx section instead of interpreting them as HTTP.
        if (state.WebTransportSession is not null)
        {
            if (frame.Type == Http3FrameType.Data)
                state.CapsuleBuffer.Append(frame.Payload.Span);
            return true;
        }

        // Tunnel mode (Extended CONNECT accepted, RFC 9114 §4.4): only DATA frames allowed from now on.
        if (state.Tunnel is not null)
        {
            if (frame.Type == Http3FrameType.Data)
            {
                state.Tunnel.Deliver(frame.Payload.ToArray());
                return true;
            }
            if (frame.Type is Http3FrameType.Headers or Http3FrameType.Settings or Http3FrameType.GoAway
                           or Http3FrameType.MaxPushId or Http3FrameType.CancelPush or Http3FrameType.PushPromise
                           or Http3FrameType.PriorityUpdateRequest or Http3FrameType.PriorityUpdatePush ||
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
                List<HeaderField>? headers = _qpack.TryDecodeHeaders(state.Stream.Id.Value, frame.Payload.Span);
                if (headers is null)
                {
                    blocked = true;
                    return true;
                }
                // §4.2.2: we cannot process a field section above our announced limit — the
                // response is discarded ("A client can discard responses …").
                if (_localMaxFieldSectionSize is { } limit && Http3Qpack.FieldSectionSize(headers) > limit)
                {
                    state.TooLarge = true;
                    state.Stream.Reset(Http3Error.RequestCancelled);     // no longer interested (§4.1.1)
                    state.Stream.AbortRead(Http3Error.RequestCancelled);
                    return true;
                }
                if (state.FinalHeadersSeen)
                {
                    // Trailer section (§4.1 item 3) — after content OR directly after the final
                    // section. §4.3: pseudo-headers are forbidden in trailers.
                    if (Http3MessageValidator.ValidateTrailers(headers) is not null)
                    {
                        MarkMalformed(state, "malformed trailer section");
                        return true;
                    }
                    state.TrailersSeen = true;
                    state.Trailers.AddRange(headers);
                }
                else if (Http3MessageValidator.ValidateResponseHeaders(headers, out int status) is { } problem)
                {
                    // §4.1.2: clients MUST NOT accept malformed responses.
                    MarkMalformed(state, problem);
                    return true;
                }
                else if (status is >= 100 and <= 199)
                {
                    // Interim response (1xx, §4.1): precedes the final response, NOT a part of it.
                    state.Interim.Add(new Http3InterimResponse(status, headers));
                }
                else if (state.IsConnect)
                {
                    // Extended-CONNECT response (RFC 8441/9220/webtrans): 2xx ⇒ the stream becomes the
                    // tunnel or the WebTransport session; otherwise a normal (rejected) response until the FIN.
                    state.Headers.AddRange(headers);
                    state.ConnectStatus = status;
                    if (status is >= 200 and < 300 && state.IsWebTransport)
                    {
                        // WebTransport session established (draft §3.2): the CONNECT stream carries capsules from now on.
                        var session = new WebTransportSession(state.Stream.Id.Value, this)
                        {
                            NegotiatedProtocol = SelectNegotiatedProtocol(state, headers), // draft §3.3
                        };
                        state.WebTransportSession = session;
                        _webTransport.RegisterSession(session);
                    }
                    else if (status is >= 200 and < 300)
                    {
                        ulong tunnelStreamId = state.Stream.Id.Value;
                        state.Tunnel = new Http3Tunnel(state.Stream)
                        {
                            DatagramSender = payload => TrySendHttpDatagram(tunnelStreamId, payload), // RFC 9297
                        };
                    }
                    else
                        state.FinalHeadersSeen = true;
                }
                else
                {
                    state.Headers.AddRange(headers); // the final header section
                    state.FinalHeadersSeen = true;
                }
                return true;

            case Http3FrameType.Data when state.TrailersSeen:
                FatalConnectionError(Http3Error.FrameUnexpected, "DATA after trailers"); // §4.1
                return false;
            case Http3FrameType.Data when !state.FinalHeadersSeen && state.Interim.Count > 0:
                // The framing would be valid (HEADERS→DATA), but interim responses carry NO content
                // (§4.1) ⇒ malformed ⇒ STREAM error H3_MESSAGE_ERROR (§4.1.2), no connection error.
                MarkMalformed(state, "DATA after interim response");
                return true;
            case Http3FrameType.Data when !state.FinalHeadersSeen:
                FatalConnectionError(Http3Error.FrameUnexpected, "DATA before HEADERS"); // §4.1
                return false;
            case Http3FrameType.Data:
                state.DataSeen = true;
                state.Body.AddRange(frame.Payload.ToArray());
                return true;

            case Http3FrameType.Settings:
                FatalConnectionError(Http3Error.FrameUnexpected, "SETTINGS on request stream"); // §7.2.4
                return false;
            case Http3FrameType.GoAway:
                FatalConnectionError(Http3Error.FrameUnexpected, "GOAWAY on request stream");   // §7.2.6
                return false;
            case Http3FrameType.MaxPushId:
                FatalConnectionError(Http3Error.FrameUnexpected, "MAX_PUSH_ID sent to client"); // §7.2.7
                return false;
            case Http3FrameType.CancelPush:
                FatalConnectionError(Http3Error.FrameUnexpected, "CANCEL_PUSH on request stream"); // §7.2.3
                return false;
            case Http3FrameType.PushPromise:
                // §4.6/§7.2.5: we never sent a MAX_PUSH_ID ⇒ every push ID is too large.
                FatalConnectionError(Http3Error.IdError, "PUSH_PROMISE without MAX_PUSH_ID");
                return false;
            case Http3FrameType.PriorityUpdateRequest:
            case Http3FrameType.PriorityUpdatePush:
                // RFC 9218 §7.2: servers MUST NEVER send PRIORITY_UPDATE (and certainly not here).
                FatalConnectionError(Http3Error.FrameUnexpected, "PRIORITY_UPDATE sent to client");
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
    /// Treats a malformed response as the STREAM error H3_MESSAGE_ERROR (RFC 9114 §4.1.2):
    /// the response MUST NOT be accepted; the stream is aborted, the connection lives on.
    /// </summary>
    private static void MarkMalformed(RequestState state, string reason)
    {
        _ = reason;
        state.Malformed = true;
        state.Stream.Reset(Http3Error.MessageError);
        state.Stream.AbortRead(Http3Error.MessageError);
    }

    /// <summary>
    /// Reads the <c>:status</c> pseudo-header of a header section (0 when absent/invalid).
    /// </summary>
    private static int ParseStatus(List<HeaderField> headers)
    {
        foreach (HeaderField h in headers)
            if (h.Name == ":status")
                return int.TryParse(h.Value, out int status) ? status : 0;
        return 0;
    }

    /// <summary>
    /// <c>true</c> when the response was discarded as malformed (RFC 9114 §4.1.2 —
    /// stream error H3_MESSAGE_ERROR; clients MUST reject malformed responses).
    /// </summary>
    public bool IsResponseMalformed(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) && state.Malformed;

    /// <summary>
    /// <c>true</c> when a response field section exceeded our announced
    /// SETTINGS_MAX_FIELD_SECTION_SIZE and the response was discarded (RFC 9114 §4.2.2).
    /// </summary>
    public bool IsResponseTooLarge(ulong streamId)
        => _requests.TryGetValue(streamId, out RequestState? state) && state.TooLarge;

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
            if (_localDatagramsEnabled)
            {
                writer.WriteVarInt(Http3Setting.H3Datagram); // RFC 9297 §2.1.1
                writer.WriteVarInt(1);
            }
            if (_wtMaxSessions > 0) // draft-webtrans-http3 §3.1: the client announces WT_MAX_SESSIONS > 0
            {
                writer.WriteVarInt(WebTransportConstants.SettingMaxSessions);
                writer.WriteVarInt(_wtMaxSessions);
                writer.WriteVarInt(WebTransportConstants.SettingMaxSessionsDraft07); // servers built against draft-07
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
        public HTTPMethod Method { get; init; } = HTTPMethod.GET; // for the content-length exception (HEAD, §4.1.2)
        public bool IsConnect { get; init; }         // Extended CONNECT (RFC 8441/9220)
        public bool IsWebTransport { get; set; }     // :protocol = webtransport (draft-webtrans-http3)
        public List<string>? OfferedWtProtocols { get; set; } // offered via WT-Available-Protocols (draft §3.3)
        public int? ConnectStatus { get; set; }      // status of the CONNECT response, once received
        public Http3Tunnel? Tunnel { get; set; }     // tunnel after 2xx acceptance, otherwise null
        public WebTransportSession? WebTransportSession { get; set; } // WebTransport session (draft-webtrans-http3)
        public ByteQueue CapsuleBuffer { get; } = new(); // capsule-protocol bytes of the WT CONNECT stream
        public ByteQueue Buffer { get; } = new();
        public Queue<Http3Frame> Pending { get; } = new(); // parsed frames still to be processed
        public List<HeaderField> Headers { get; } = [];
        public List<byte> Body { get; } = [];
        public List<Http3InterimResponse> Interim { get; } = []; // 1xx sections before the final response (§4.1)
        public List<HeaderField> Trailers { get; } = [];         // trailer section (§4.1 item 3)
        public bool Complete { get; set; }
        public bool Cancelled { get; set; }        // cancelled by us (RFC 9114 §4.1.1)
        public bool Rejected { get; set; }         // rejected via GOAWAY (§5.2) ⇒ safely repeatable
        public bool Malformed { get; set; }        // malformed response discarded (§4.1.2, H3_MESSAGE_ERROR)
        public bool TooLarge { get; set; }         // field section above our limit discarded (§4.2.2)
        public bool FinalHeadersSeen { get; set; } // final (non-1xx) header section decoded (§4.1)
        public bool DataSeen { get; set; }         // content started
        public bool TrailersSeen { get; set; }     // trailer section seen ⇒ frames after it are illegal
    }
}
