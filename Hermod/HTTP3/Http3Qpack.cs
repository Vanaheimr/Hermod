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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// Encapsulates an HTTP/3 connection's QPACK integration: the dynamic table (encoder + decoder),
/// reading the peer's QPACK encoder and control streams, and (de)coding field sections.
/// With an announced capacity of 0, everything stays purely static (Cloudflare-interop-safe); with
/// &gt; 0 and the peer also announcing a capacity, the dynamic table is used on both sides (RFC 9204).
/// Also enforces the frame/stream state machine of the uni streams (RFC 9114 §6.2, §7.2):
/// peer violations are reported via <c>onConnectionError</c> as HTTP/3 connection errors.
/// </summary>
internal sealed class Http3Qpack
{
    private const ulong DesiredEncoderCapacity = 4096;

    private readonly QpackDynamicEncoder _encoder = new();
    private readonly QpackDynamicDecoder _decoder = new();
    private readonly ulong _localMaxCapacity; // what we announce as decoder
    private readonly bool _weAreClient;
    private readonly Action<ulong, string> _fatal; // HTTP/3 connection error (RFC 9114 §8)

    private QuicStream? _encoderStream; // our outgoing QPACK encoder stream (insert instructions)
    private QuicStream? _decoderStream; // our outgoing QPACK decoder stream (section acks)
    private bool _encoderCapacitySet;
    private ulong _peerMaxCapacity;
    private bool _peerSettingsSeen;

    private ulong? _peerControlId;  // RFC 9114 §6.2.1: exactly ONE control stream per peer
    private ulong? _peerEncoderId;  // RFC 9204 §4.2: exactly ONE encoder/decoder stream per peer
    private ulong? _peerDecoderId;

    private readonly Dictionary<ulong, PeerUniStream> _peerStreams = [];
    private readonly HashSet<ulong> _webTransportUniStreams = []; // uni streams handed over to the WebTransport manager

    public Http3Qpack(ulong localMaxCapacity, bool weAreClient, Action<ulong, string> onConnectionError)
    {
        _localMaxCapacity = localMaxCapacity;
        _weAreClient = weAreClient;
        _fatal = onConnectionError;
    }

    /// <summary>
    /// Reserved HTTP/2 frame types without an HTTP/3 counterpart (RFC 9114 §7.2.8/§11.2.1) — their
    /// receipt MUST be treated as the connection error H3_FRAME_UNEXPECTED.
    /// </summary>
    internal static bool IsReservedHttp2FrameType(ulong type) => type is 0x02 or 0x06 or 0x08 or 0x09;

    /// <summary>
    /// Limit announced by the peer via SETTINGS_MAX_FIELD_SECTION_SIZE (RFC 9114 §4.2.2);
    /// <c>null</c> = unlimited. Field sections above this limit SHOULD NOT be sent.
    /// </summary>
    public ulong? PeerMaxFieldSectionSize { get; private set; }

    /// <summary>
    /// The peer (server) permits Extended CONNECT (SETTINGS_ENABLE_CONNECT_PROTOCOL = 1,
    /// RFC 8441 §3 / RFC 9220 §3). Without this setting the client MUST NOT send :protocol.
    /// </summary>
    public bool PeerEnableConnectProtocol { get; private set; }

    /// <summary>
    /// The peer accepts HTTP/3 datagrams (SETTINGS_H3_DATAGRAM = 1, RFC 9297 §2.1.1).
    /// </summary>
    public bool PeerH3Datagram { get; private set; }

    /// <summary>
    /// The peer's WebTransport settings (draft-webtrans-http3 §9.2): max. sessions and the initial
    /// flow-control limits. <c>WtMaxSessions</c> = 0 ⇒ the peer accepts no WebTransport sessions.
    /// </summary>
    public ulong PeerWtMaxSessions { get; private set; }
    public ulong PeerWtInitialMaxStreamsUni { get; private set; }
    public ulong PeerWtInitialMaxStreamsBidi { get; private set; }
    public ulong PeerWtInitialMaxData { get; private set; }

    /// <summary>
    /// Uncompressed size of a field section per RFC 9114 §4.2.2: per field, the byte lengths of
    /// name and value plus 32 bytes of overhead.
    /// </summary>
    internal static ulong FieldSectionSize(IReadOnlyList<HeaderField> fields)
    {
        ulong size = 0;
        foreach (HeaderField field in fields)
            size += (ulong)System.Text.Encoding.UTF8.GetByteCount(field.Name)
                  + (ulong)System.Text.Encoding.UTF8.GetByteCount(field.Value)
                  + 32;
        return size;
    }

    /// <summary>
    /// The maximum table capacity we announce (as decoder).
    /// </summary>
    public ulong LocalMaxCapacity => _localMaxCapacity;

    /// <summary>
    /// Insert count of the encoder table (diagnostics: &gt; 0 ⇒ the dynamic table was used).
    /// </summary>
    public ulong EncoderInsertCount => _encoder.Table.InsertCount;

    /// <summary>
    /// Insert count of the decoder table (diagnostics).
    /// </summary>
    public ulong DecoderInsertCount => _decoder.Table.InsertCount;

    /// <summary>
    /// Insert count confirmed by the peer via section-ack/insert-count-increment (diagnostics).
    /// </summary>
    public ulong EncoderKnownReceivedCount => _encoder.KnownReceivedCount;

    public void SetEncoderStream(QuicStream stream) => _encoderStream = stream;
    public void SetDecoderStream(QuicStream stream) => _decoderStream = stream;

    /// <summary>
    /// Encodes a header list (for the stream <paramref name="streamId"/>) into a field section. Uses
    /// the dynamic table (with insert instructions on the encoder stream) once we and the peer have
    /// each announced a capacity &gt; 0; otherwise static.
    /// </summary>
    public byte[] EncodeHeaders(ulong streamId, IReadOnlyList<HeaderField> headers)
    {
        if (_localMaxCapacity > 0 && _peerSettingsSeen && _peerMaxCapacity > 0 && _encoderStream is not null)
        {
            if (!_encoderCapacitySet)
            {
                _encoderStream.Write(_encoder.SetCapacity(Math.Min(_peerMaxCapacity, DesiredEncoderCapacity)));
                _encoderCapacitySet = true;
            }
            (byte[] instructions, byte[] section) = _encoder.Encode(streamId, headers);
            if (instructions.Length > 0)
                _encoderStream.Write(instructions);
            return section;
        }
        return QpackEncoder.Encode(headers);
    }

    /// <summary>
    /// Decodes a field section of the stream <paramref name="streamId"/>. Returns <c>null</c> when
    /// the stream is blocked (the referenced dynamic entries have not yet arrived) – try again later.
    /// For a dynamic section, a section acknowledgment is sent (RFC 9204 §4.4.1).
    /// </summary>
    public List<HeaderField>? TryDecodeHeaders(ulong streamId, ReadOnlySpan<byte> section)
    {
        QpackResult result = _decoder.Decode(section, out List<HeaderField> headers, out ulong requiredInsertCount);
        if (result == QpackResult.Blocked)
            return null;
        if (result == QpackResult.Ok && requiredInsertCount > 0 && _decoderStream is not null)
            _decoderStream.Write(QpackDynamicDecoder.EncodeSectionAcknowledgment(streamId));
        return headers; // Ok or error (empty list)
    }

    /// <summary>
    /// Reads the peer's unidirectional streams (control for SETTINGS, the QPACK encoder stream),
    /// enforcing the stream/frame rules from RFC 9114 §6.2/§7.2 in the process.
    /// </summary>
    public void PumpPeerStreams(IReadOnlyDictionary<ulong, QuicStream> streams)
    {
        foreach ((ulong id, QuicStream stream) in streams)
        {
            if (!stream.Id.IsUnidirectional)
                continue;
            bool peerInitiated = _weAreClient ? stream.Id.IsServerInitiated : stream.Id.IsClientInitiated;
            if (!peerInitiated)
                continue;

            if (_webTransportUniStreams.Contains(id))
                continue; // handed to the WebTransport manager – no longer pumped here
            if (!_peerStreams.TryGetValue(id, out PeerUniStream? peer))
                _peerStreams[id] = peer = new PeerUniStream(stream);

            byte[] chunk = stream.Read();
            if (chunk.Length > 0)
                peer.Buffer.Append(chunk);
            RoutePeerStream(peer);

            // RFC 9114 §6.2.1 / RFC 9204 §4.2: control, encoder and decoder streams must NEVER end —
            // neither cleanly (FIN) nor via reset ⇒ H3_CLOSED_CRITICAL_STREAM.
            if ((peer.Type == Http3StreamType.Control || peer.Type == Http3StreamType.QpackEncoder ||
                 peer.Type == Http3StreamType.QpackDecoder) &&
                (stream.Receive.FinReceived || stream.IsResetByPeer))
            {
                _fatal(Http3Error.ClosedCriticalStream, "critical stream closed");
                return;
            }
        }
    }

    private void RoutePeerStream(PeerUniStream peer)
    {
        // Read the stream type (first varint) once.
        if (peer.Type is null)
        {
            var reader = new BufferReader(peer.Buffer.Span);
            if (!reader.TryReadVarInt(out ulong type))
                return; // type varint still incomplete
            peer.Type = type;
            peer.Buffer.Consume(reader.Position);

            // Stream creation rules (checked once when reading the type):
            switch (type)
            {
                case Http3StreamType.Control when _peerControlId is not null:       // §6.2.1: only ONE control stream
                case Http3StreamType.QpackEncoder when _peerEncoderId is not null:  // RFC 9204 §4.2: only ONE each …
                case Http3StreamType.QpackDecoder when _peerDecoderId is not null:  // … encoder/decoder stream
                    _fatal(Http3Error.StreamCreationError, "duplicate critical stream");
                    return;
                case Http3StreamType.Control:
                    _peerControlId = peer.Stream.Id.Value;
                    break;
                case Http3StreamType.QpackEncoder:
                    _peerEncoderId = peer.Stream.Id.Value;
                    break;
                case Http3StreamType.QpackDecoder:
                    _peerDecoderId = peer.Stream.Id.Value;
                    break;
                case Http3StreamType.Push when !_weAreClient:
                    // §6.2.2: only servers push; a client-initiated push stream is a connection error.
                    _fatal(Http3Error.StreamCreationError, "client-initiated push stream");
                    return;
                case Http3StreamType.Push:
                    // §4.6: we (the client) never sent a MAX_PUSH_ID ⇒ every push stream is illegal.
                    _fatal(Http3Error.IdError, "push stream without MAX_PUSH_ID");
                    return;
            }
        }

        switch (peer.Type)
        {
            case Http3StreamType.QpackEncoder:
                if (_decoder.ProcessEncoderInstructions(peer.Buffer.Span, out int consumed))
                    peer.Buffer.Consume(consumed);
                break;

            case Http3StreamType.QpackDecoder: // the peer's section acks / insert count increment.
                int ackConsumed = _encoder.ProcessDecoderInstructions(peer.Buffer.Span);
                peer.Buffer.Consume(ackConsumed);
                break;

            case Http3StreamType.Control:
                if (peer.Buffer.Count > 0 &&
                    Http3Frames.TryReadAll(peer.Buffer.Memory, out List<Http3Frame> frames, out int used))
                {
                    foreach (Http3Frame frame in frames)
                        if (!HandleControlFrame(frame))
                            return; // connection error reported
                    peer.Buffer.Consume(used);
                }
                break;

            case WebTransport.WebTransportConstants.UniStreamType when OnWebTransportUniStream is not null:
                // WebTransport uni stream (draft §4.1): 0x54 ‖ session ID ‖ payload. Read the session
                // ID, then hand the stream to the WebTransport manager (which reads directly from then on).
                var wtReader = new BufferReader(peer.Buffer.Span);
                if (!wtReader.TryReadVarInt(out ulong wtSessionId))
                    break; // session ID still incomplete
                byte[] wtLeftover = peer.Buffer.Span[wtReader.Position..].ToArray();
                _webTransportUniStreams.Add(peer.Stream.Id.Value); // the WebTransport manager from now on
                _peerStreams.Remove(peer.Stream.Id.Value);
                OnWebTransportUniStream(peer.Stream, wtSessionId, wtLeftover);
                break;

            default: // Unknown/reserved stream types: discard the data, NO connection error (§6.2).
                peer.Buffer.Clear();
                break;
        }
    }

    /// <summary>
    /// Callback for a recognised WebTransport uni stream (draft §4.1): (stream, session ID, payload
    /// already read along). When set, the HTTP/3 layer announces WebTransport.
    /// </summary>
    public Action<QuicStream, ulong, byte[]>? OnWebTransportUniStream { get; set; }

    /// <summary>
    /// Frame state machine of the control stream (RFC 9114 §6.2.1, §7.2). Returns <c>false</c>
    /// when a connection error was reported.
    /// </summary>
    private bool HandleControlFrame(Http3Frame frame)
    {
        // §6.2.1: the FIRST frame MUST be SETTINGS.
        if (!_peerSettingsSeen && frame.Type != Http3FrameType.Settings)
        {
            _fatal(Http3Error.MissingSettings, "first control frame is not SETTINGS");
            return false;
        }

        switch (frame.Type)
        {
            case Http3FrameType.Settings when _peerSettingsSeen:
                _fatal(Http3Error.FrameUnexpected, "second SETTINGS frame"); // §7.2.4
                return false;
            case Http3FrameType.Settings:
                return ParseSettings(frame.Payload.Span);

            case Http3FrameType.Data:    // §7.2.1: DATA only on request/push streams
            case Http3FrameType.Headers: // §7.2.2: HEADERS only on request/push streams
                _fatal(Http3Error.FrameUnexpected, "DATA/HEADERS on control stream");
                return false;

            case Http3FrameType.PushPromise:
                // §7.2.5: always illegal on the control stream (and clients never send PUSH_PROMISE).
                _fatal(Http3Error.FrameUnexpected, "PUSH_PROMISE on control stream");
                return false;

            case Http3FrameType.MaxPushId when _weAreClient:
                _fatal(Http3Error.FrameUnexpected, "MAX_PUSH_ID sent to client"); // §7.2.7
                return false;
            case Http3FrameType.MaxPushId:
                return RequireSingleVarInt(frame, "MAX_PUSH_ID"); // we do not push ⇒ the value is irrelevant

            case Http3FrameType.CancelPush when _weAreClient:
                // §7.2.3: references a push ID beyond what is permitted — we NEVER permitted one.
                _fatal(Http3Error.IdError, "CANCEL_PUSH without MAX_PUSH_ID");
                return false;
            case Http3FrameType.CancelPush:
                return RequireSingleVarInt(frame, "CANCEL_PUSH");

            case Http3FrameType.GoAway:
                return HandleGoAway(frame);

            case Http3FrameType.PriorityUpdateRequest when _weAreClient:
            case Http3FrameType.PriorityUpdatePush when _weAreClient:
                // RFC 9218 §7.2: servers MUST NEVER send PRIORITY_UPDATE.
                _fatal(Http3Error.FrameUnexpected, "PRIORITY_UPDATE sent to client");
                return false;
            case Http3FrameType.PriorityUpdatePush:
                // RFC 9218 §7.2: push ID greater than the maximum or never promised — we never
                // permit push, so EVERY referenced push ID is illegal.
                _fatal(Http3Error.IdError, "PRIORITY_UPDATE for unpromised push");
                return false;
            case Http3FrameType.PriorityUpdateRequest:
                return HandlePriorityUpdate(frame);

            default:
                if (IsReservedHttp2FrameType(frame.Type))
                {
                    _fatal(Http3Error.FrameUnexpected, "reserved HTTP/2 frame type"); // §7.2.8
                    return false;
                }
                return true; // ignore unknown types (incl. grease 0x1f·N+0x21) (§9)
        }
    }

    /// <summary>
    /// Called on the server for every valid PRIORITY_UPDATE (RFC 9218 §7.2):
    /// (request stream ID, priority field value as ASCII text).
    /// </summary>
    public Action<ulong, string>? OnPriorityUpdate { get; set; }

    /// <summary>
    /// PRIORITY_UPDATE for request streams (RFC 9218 §7.2): payload = prioritized element ID (varint)
    /// + priority field value (ASCII). The ID MUST be a request stream ID (client-initiated
    /// bidirectional, bits 0b00), otherwise H3_ID_ERROR.
    /// </summary>
    private bool HandlePriorityUpdate(Http3Frame frame)
    {
        var reader = new BufferReader(frame.Payload.Span);
        if (!reader.TryReadVarInt(out ulong id))
        {
            _fatal(Http3Error.FrameError, "malformed PRIORITY_UPDATE payload"); // §7.1
            return false;
        }
        if ((id & 0x03) != 0)
        {
            _fatal(Http3Error.IdError, "PRIORITY_UPDATE for non-request stream"); // RFC 9218 §7.2 MUST
            return false;
        }
        string fieldValue = System.Text.Encoding.ASCII.GetString(frame.Payload.Span[reader.Position..]);
        OnPriorityUpdate?.Invoke(id, fieldValue);
        return true;
    }

    /// <summary>
    /// GOAWAY (RFC 9114 §5.2/§7.2.6): the payload is exactly ONE varint. At the client, the ID MUST
    /// be a client-initiated bidirectional stream ID (bits 0b00), otherwise H3_ID_ERROR. Multiple
    /// GOAWAYs are allowed, but the ID must NEVER increase (§5.2) — otherwise likewise H3_ID_ERROR.
    /// </summary>
    private bool HandleGoAway(Http3Frame frame)
    {
        var reader = new BufferReader(frame.Payload.Span);
        if (!reader.TryReadVarInt(out ulong id) || reader.Remaining > 0)
        {
            _fatal(Http3Error.FrameError, "malformed GOAWAY payload"); // §7.1
            return false;
        }
        if (_weAreClient && (id & 0x03) != 0)
        {
            _fatal(Http3Error.IdError, "GOAWAY with non-request stream ID"); // §7.2.6
            return false;
        }
        if (GoAwayId is { } previous && id > previous)
        {
            _fatal(Http3Error.IdError, "GOAWAY identifier increased"); // §5.2
            return false;
        }
        GoAwayId = id;
        return true;
    }

    /// <summary>
    /// The stream/push ID most recently received via GOAWAY (RFC 9114 §5.2), if any.
    /// </summary>
    public ulong? GoAwayId { get; private set; }

    /// <summary>
    /// §7.1: the payload must contain EXACTLY the defined fields — here a single varint.
    /// </summary>
    private bool RequireSingleVarInt(Http3Frame frame, string name)
    {
        var reader = new BufferReader(frame.Payload.Span);
        if (!reader.TryReadVarInt(out _) || reader.Remaining > 0)
        {
            _fatal(Http3Error.FrameError, $"malformed {name} payload");
            return false;
        }
        return true;
    }

    /// <summary>
    /// SETTINGS payload (RFC 9114 §7.2.4): pairs of varint ID and value. Reserved HTTP/2 IDs
    /// (0x00, 0x02–0x05) and duplicate IDs ⇒ H3_SETTINGS_ERROR; layout errors ⇒ H3_FRAME_ERROR.
    /// </summary>
    private bool ParseSettings(ReadOnlySpan<byte> payload)
    {
        var reader = new BufferReader(payload);
        var seen = new HashSet<ulong>();
        while (reader.Remaining > 0)
        {
            if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong value))
            {
                _fatal(Http3Error.FrameError, "malformed SETTINGS payload"); // §7.1
                return false;
            }
            if (id is 0x00 or 0x02 or 0x03 or 0x04 or 0x05)
            {
                _fatal(Http3Error.SettingsError, "reserved HTTP/2 setting"); // §7.2.4.1/§11.2.2
                return false;
            }
            if (!seen.Add(id))
            {
                _fatal(Http3Error.SettingsError, "duplicate setting identifier"); // §7.2.4
                return false;
            }
            if (id == Http3Setting.QpackMaxTableCapacity)
                _peerMaxCapacity = value;
            else if (id == Http3Setting.MaxFieldSectionSize)
                PeerMaxFieldSectionSize = value; // RFC 9114 §4.2.2
            else if (id == Http3Setting.EnableConnectProtocol)
            {
                if (value > 1)
                {
                    _fatal(Http3Error.SettingsError, "ENABLE_CONNECT_PROTOCOL must be 0 or 1"); // RFC 8441 §3
                    return false;
                }
                PeerEnableConnectProtocol = value == 1;
            }
            else if (id == Http3Setting.H3Datagram)
            {
                if (value > 1)
                {
                    _fatal(Http3Error.SettingsError, "H3_DATAGRAM must be 0 or 1"); // RFC 9297 §2.1.1
                    return false;
                }
                PeerH3Datagram = value == 1;
            }
            // Either codepoint counts (draft-webtrans-http3 §9.2 and the draft-07 value browsers still
            // send). A peer that sends both sends the same number twice, so last-wins is harmless;
            // one that sends only the old one is understood all the same.
            else if (id == WebTransport.WebTransportConstants.SettingMaxSessions ||
                     id == WebTransport.WebTransportConstants.SettingMaxSessionsDraft07)
                PeerWtMaxSessions = value;
            else if (id == WebTransport.WebTransportConstants.SettingInitialMaxStreamsUni)
                PeerWtInitialMaxStreamsUni = value;
            else if (id == WebTransport.WebTransportConstants.SettingInitialMaxStreamsBidi)
                PeerWtInitialMaxStreamsBidi = value;
            else if (id == WebTransport.WebTransportConstants.SettingInitialMaxData)
                PeerWtInitialMaxData = value;
        }
        _peerSettingsSeen = true;
        return true;
    }

    private sealed class PeerUniStream(QuicStream stream)
    {
        public QuicStream Stream { get; } = stream;
        public ulong? Type { get; set; }
        public ByteQueue Buffer { get; } = new();
    }
}
