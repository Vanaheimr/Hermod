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

using System.Buffers.Binary;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// The services a <see cref="WebTransportSession"/> needs from its HTTP/3 connection: opening
/// WebTransport streams, sending WT datagrams, writing capsules on the CONNECT stream and ending
/// its send direction.
/// </summary>
internal interface IWebTransportHost
{
    QuicStream OpenWebTransportUniStream(ulong sessionId);
    QuicStream OpenWebTransportBidiStream(ulong sessionId);
    bool SendWebTransportDatagram(ulong sessionId, byte[] payload);
    void WriteConnectStreamData(ulong sessionId, byte[] data);
    void FinishConnectStream(ulong sessionId);
    bool FlowControlEnabled { get; }
    ulong LocalInitialMaxStreamsUni { get; }
    ulong LocalInitialMaxStreamsBidi { get; }
    ulong LocalInitialMaxData { get; }
    ulong PeerInitialMaxStreamsUni { get; }
    ulong PeerInitialMaxStreamsBidi { get; }
    ulong PeerInitialMaxData { get; }

    /// <summary>
    /// TLS keying-material exporter of the underlying QUIC connection (RFC 8446 §7.5).
    /// </summary>
    byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length);
}

/// <summary>
/// A WebTransport session over HTTP/3 (draft-ietf-webtrans-http3-13): runs over an
/// Extended-CONNECT stream (<c>:protocol = webtransport</c>) and multiplexes over it uni-/
/// bidirectional WebTransport streams (§4.1/§4.2), unreliable datagrams (§4.4) and session
/// control/flow control via capsules (§5.6, §6). The session ID is the stream ID of the
/// CONNECT stream.
/// </summary>
public sealed class WebTransportSession
{
    private readonly IWebTransportHost _host;

    private readonly Queue<WebTransportStream> _incomingUni = new();
    private readonly Queue<WebTransportStream> _incomingBidi = new();
    private readonly Queue<byte[]> _datagrams = new();
    private readonly List<WebTransportStream> _allStreams = [];

    // Flow control (only active when both sides announced WT_MAX_SESSIONS > 1, §5.1).
    private ulong _peerMaxStreamsUni, _peerMaxStreamsBidi, _peerMaxData; // granted by the peer
    private ulong _localMaxStreamsUni, _localMaxStreamsBidi, _localMaxData; // granted by us
    private ulong _openedUni, _openedBidi;   // streams opened by us (cumulative)
    private ulong _acceptedUni, _acceptedBidi; // opened by the peer (cumulative)
    private ulong _dataSent, _dataReceived;

    internal WebTransportSession(ulong sessionId, IWebTransportHost host)
    {
        SessionId = sessionId;
        _host = host;
        _peerMaxStreamsUni = host.PeerInitialMaxStreamsUni;
        _peerMaxStreamsBidi = host.PeerInitialMaxStreamsBidi;
        _peerMaxData = host.PeerInitialMaxData;
        _localMaxStreamsUni = host.LocalInitialMaxStreamsUni;
        _localMaxStreamsBidi = host.LocalInitialMaxStreamsBidi;
        _localMaxData = host.LocalInitialMaxData;
    }

    /// <summary>
    /// The session ID = stream ID of the CONNECT stream (§4).
    /// </summary>
    public ulong SessionId { get; }

    /// <summary>
    /// The session has ended (§6: CONNECT stream closed or WT_CLOSE_SESSION).
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// Error code/message of a received (or implicit) WT_CLOSE_SESSION.
    /// </summary>
    public uint? CloseErrorCode { get; private set; }
    public string? CloseReason { get; private set; }

    /// <summary>
    /// Flow control is active (both sides WT_MAX_SESSIONS &gt; 1, §5.1).
    /// </summary>
    public bool FlowControlEnabled => _host.FlowControlEnabled;

    /// <summary>
    /// The application protocol negotiated via WT-Available-Protocols/WT-Protocol (draft §3.3,
    /// ALPN-like); <c>null</c> when none was offered or the server picked none.
    /// </summary>
    public string? NegotiatedProtocol { get; internal set; }

    /// <summary>
    /// Session-bound keying-material exporter (draft §4.7): the QUIC connection's TLS exporter
    /// (RFC 8446 §7.5) with the fixed label <c>EXPORTER-WebTransport</c> and the "WebTransport
    /// Exporter Context" struct (session ID ‖ label ‖ context) — thereby different sessions of the
    /// same connection obtain separate material, but both ends of the same session identical
    /// material. The application-supplied label must be 1–255 UTF-8 bytes long, the context 0–255 bytes.
    /// </summary>
    public byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length)
    {
        byte[] labelBytes = Encoding.UTF8.GetBytes(label);
        if (labelBytes.Length is < 1 or > 255)
            throw new ArgumentException("The exporter label must be 1–255 UTF-8 bytes long (draft §4.7).", nameof(label));
        if (context.Length > 255)
            throw new ArgumentException("The exporter context may be at most 255 bytes long (draft §4.7).", nameof(context));

        // WebTransport Exporter Context { Session ID (64) ‖ LabelLen (8) ‖ Label ‖ ContextLen (8) ‖ Context }
        byte[] exporterContext = new byte[8 + 1 + labelBytes.Length + 1 + context.Length];
        BinaryPrimitives.WriteUInt64BigEndian(exporterContext, SessionId);
        exporterContext[8] = (byte)labelBytes.Length;
        labelBytes.CopyTo(exporterContext, 9);
        exporterContext[9 + labelBytes.Length] = (byte)context.Length;
        context.CopyTo(exporterContext.AsSpan(10 + labelBytes.Length));

        return _host.ExportKeyingMaterial("EXPORTER-WebTransport", exporterContext, length);
    }

    // ---- Streams (§4.1/§4.2) --------------------------------------------------------------

    /// <summary>
    /// Opens a unidirectional WebTransport stream (header 0x54 ‖ session ID is written).
    /// <c>null</c> when the session is closed or the peer's stream limit is reached (§5.3) —
    /// a WT_STREAMS_BLOCKED capsule is then sent.
    /// </summary>
    public WebTransportStream? OpenUnidirectionalStream()
    {
        if (IsClosed)
            return null;
        if (FlowControlEnabled && _openedUni >= _peerMaxStreamsUni)
        {
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleStreamsBlockedUni, _peerMaxStreamsUni));
            return null;
        }
        _openedUni++;
        // Locally opened uni stream: send-only.
        var stream = new WebTransportStream(_host.OpenWebTransportUniStream(SessionId), bidirectional: false, canSend: true, canReceive: false);
        _allStreams.Add(stream);
        return stream;
    }

    /// <summary>
    /// Opens a bidirectional WebTransport stream (header WT_STREAM 0x41 ‖ session ID); limit/state
    /// rules as in <see cref="OpenUnidirectionalStream"/>.
    /// </summary>
    public WebTransportStream? OpenBidirectionalStream()
    {
        if (IsClosed)
            return null;
        if (FlowControlEnabled && _openedBidi >= _peerMaxStreamsBidi)
        {
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleStreamsBlockedBidi, _peerMaxStreamsBidi));
            return null;
        }
        _openedBidi++;
        var stream = new WebTransportStream(_host.OpenWebTransportBidiStream(SessionId), bidirectional: true, canSend: true, canReceive: true);
        _allStreams.Add(stream);
        return stream;
    }

    /// <summary>
    /// Accepts the next peer-opened unidirectional stream, if any.
    /// </summary>
    public WebTransportStream? AcceptUnidirectionalStream() => _incomingUni.Count > 0 ? _incomingUni.Dequeue() : null;

    /// <summary>
    /// Accepts the next peer-opened bidirectional stream, if any.
    /// </summary>
    public WebTransportStream? AcceptBidirectionalStream() => _incomingBidi.Count > 0 ? _incomingBidi.Dequeue() : null;

    // ---- Datagrams (§4.4) -----------------------------------------------------------------

    /// <summary>
    /// Sends a WebTransport datagram (unreliable); the payload directly follows the quarter stream
    /// ID of the CONNECT stream (§4.4). <c>false</c> when the session is closed or the datagram
    /// cannot be sent.
    /// </summary>
    public bool SendDatagram(byte[] payload) => !IsClosed && _host.SendWebTransportDatagram(SessionId, payload);

    /// <summary>
    /// Accepts the next received datagram, if any.
    /// </summary>
    public bool TryReceiveDatagram(out byte[]? payload)
    {
        if (_datagrams.Count > 0) { payload = _datagrams.Dequeue(); return true; }
        payload = null;
        return false;
    }

    // ---- Session end (§6) -----------------------------------------------------------------

    /// <summary>
    /// Ends the session (§6): sends a WT_CLOSE_SESSION capsule and then a FIN on the CONNECT
    /// stream; all associated streams are aborted with WT_SESSION_GONE.
    /// </summary>
    public void Close(uint errorCode = 0, string reason = "")
    {
        if (IsClosed)
            return;
        if (errorCode != 0 || reason.Length > 0)
            _host.WriteConnectStreamData(SessionId, WebTransportCapsule.BuildCloseSession(errorCode, reason));
        _host.FinishConnectStream(SessionId);
        MarkClosed(errorCode, reason);
    }

    // ---- Called by the manager ------------------------------------------------------------

    internal void OnIncomingUniStream(QuicStream stream, byte[] leftover)
    {
        _acceptedUni++;
        // Incoming uni stream: receive-only.
        var wt = new WebTransportStream(stream, bidirectional: false, canSend: false, canReceive: true, leftover);
        _allStreams.Add(wt);
        _incomingUni.Enqueue(wt);
        MaybeGrantStreams(uni: true);
    }

    internal void OnIncomingBidiStream(QuicStream stream, byte[] leftover)
    {
        _acceptedBidi++;
        var wt = new WebTransportStream(stream, bidirectional: true, canSend: true, canReceive: true, leftover);
        _allStreams.Add(wt);
        _incomingBidi.Enqueue(wt);
        MaybeGrantStreams(uni: false);
    }

    internal void OnDatagram(byte[] payload) => _datagrams.Enqueue(payload);

    /// <summary>
    /// Processes a capsule received on the CONNECT stream (§5.6/§6). Unknown types are skipped
    /// silently (RFC 9297 §3.2).
    /// </summary>
    internal void HandleCapsule(WebTransportCapsule capsule)
    {
        switch (capsule.Type)
        {
            case WebTransportConstants.CapsuleCloseSession:
                var span = capsule.Value.Span;
                uint code = span.Length >= 4 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(span) : 0;
                string reason = span.Length > 4 ? System.Text.Encoding.UTF8.GetString(span[4..]) : "";
                MarkClosed(code, reason);
                break;

            // Flow-control capsules are ignored while flow control is not negotiated (§5.1).
            case WebTransportConstants.CapsuleMaxStreamsUni when FlowControlEnabled:
                if (ReadVarInt(capsule.Value, out ulong maxUni)) _peerMaxStreamsUni = Math.Max(_peerMaxStreamsUni, maxUni);
                break;
            case WebTransportConstants.CapsuleMaxStreamsBidi when FlowControlEnabled:
                if (ReadVarInt(capsule.Value, out ulong maxBidi)) _peerMaxStreamsBidi = Math.Max(_peerMaxStreamsBidi, maxBidi);
                break;
            case WebTransportConstants.CapsuleMaxData when FlowControlEnabled:
                if (ReadVarInt(capsule.Value, out ulong maxData)) _peerMaxData = Math.Max(_peerMaxData, maxData);
                break;
            case WebTransportConstants.CapsuleStreamsBlockedUni when FlowControlEnabled:
            case WebTransportConstants.CapsuleStreamsBlockedBidi when FlowControlEnabled:
            case WebTransportConstants.CapsuleDataBlocked when FlowControlEnabled:
                break; // purely informational – we grant streams/data proactively anyway (MaybeGrant*)
            // Unknown capsules: skip silently (RFC 9297 §3.2).
        }
    }

    /// <summary>
    /// Session end via a closed CONNECT stream (§6, without WT_CLOSE_SESSION = code 0).
    /// </summary>
    internal void OnConnectStreamClosed() => MarkClosed(CloseErrorCode ?? 0, CloseReason ?? "");

    internal bool TryRecordSentData(int bytes)
    {
        if (!FlowControlEnabled)
            return true;
        if (_dataSent + (ulong)bytes > _peerMaxData)
        {
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleDataBlocked, _peerMaxData));
            return false;
        }
        _dataSent += (ulong)bytes;
        return true;
    }

    internal void RecordReceivedData(int bytes)
    {
        if (!FlowControlEnabled)
            return;
        _dataReceived += (ulong)bytes;
        // Replenish the window (§5.4): grant more credit once half the window is consumed.
        if (_localMaxData - _dataReceived < _localMaxData / 2)
        {
            _localMaxData += Math.Max(_host.LocalInitialMaxData, 65536);
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleMaxData, _localMaxData));
        }
    }

    private void MaybeGrantStreams(bool uni)
    {
        if (!FlowControlEnabled)
            return;
        // Replenish the cumulative limit once the peer approaches its current limit (§5.3).
        if (uni && _acceptedUni + 1 >= _localMaxStreamsUni)
        {
            _localMaxStreamsUni += Math.Max(_host.LocalInitialMaxStreamsUni, 1);
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleMaxStreamsUni, _localMaxStreamsUni));
        }
        else if (!uni && _acceptedBidi + 1 >= _localMaxStreamsBidi)
        {
            _localMaxStreamsBidi += Math.Max(_host.LocalInitialMaxStreamsBidi, 1);
            _host.WriteConnectStreamData(SessionId,
                WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleMaxStreamsBidi, _localMaxStreamsBidi));
        }
    }

    private void MarkClosed(uint code, string reason)
    {
        if (IsClosed)
            return;
        IsClosed = true;
        CloseErrorCode = code;
        CloseReason = reason;
        foreach (WebTransportStream stream in _allStreams) // §6: abort all associated streams
            stream.AbortForSessionGone();
    }

    private static bool ReadVarInt(ReadOnlyMemory<byte> value, out ulong result)
    {
        var reader = new Quic.Core.Buffers.BufferReader(value.Span);
        return reader.TryReadVarInt(out result);
    }
}
