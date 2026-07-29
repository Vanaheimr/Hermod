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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic;

/// <summary>
/// QUIC transport parameters (RFC 9000, §18). They are exchanged in the TLS handshake as the opaque
/// extension <c>quic_transport_parameters</c> (type 0x39) and establish limits such as flow-control
/// windows, idle timeout and stream counts. This model covers the parameters needed for a client
/// handshake; unknown peer parameters are ignored during parsing (grease-tolerant).
/// </summary>
public sealed class TransportParameters
{
    // Parameter IDs (RFC 9000 §18.2).
    private const ulong OriginalDestinationConnectionId = 0x00;
    private const ulong MaxIdleTimeout = 0x01;
    private const ulong StatelessResetToken = 0x02;
    private const ulong MaxUdpPayloadSize = 0x03;
    private const ulong InitialMaxData = 0x04;
    private const ulong InitialMaxStreamDataBidiLocal = 0x05;
    private const ulong InitialMaxStreamDataBidiRemote = 0x06;
    private const ulong InitialMaxStreamDataUni = 0x07;
    private const ulong InitialMaxStreamsBidi = 0x08;
    private const ulong InitialMaxStreamsUni = 0x09;
    private const ulong AckDelayExponent = 0x0a;
    private const ulong MaxAckDelay = 0x0b;
    private const ulong ActiveConnectionIdLimit = 0x0e;
    private const ulong InitialSourceConnectionId = 0x0f;
    private const ulong MaxDatagramFrameSize = 0x20; // RFC 9221 §3
    private const ulong RetrySourceConnectionId = 0x10;
    private const ulong PreferredAddress = 0x0d;
    private const ulong ResetStreamAt = 0x1d; // draft-ietf-quic-reliable-stream-reset §3 (provisional)
    private const ulong MinAckDelay = 0xff04de1b; // draft-ietf-quic-ack-frequency §3 (provisional)

    /// <summary>
    /// Idle timeout in milliseconds (0 = disabled).
    /// </summary>
    public ulong MaxIdleTimeoutMs { get; set; } = 30_000;

    public ulong MaxUdpPayloadSizeValue { get; set; } = 65527;
    public ulong InitialMaxDataValue { get; set; } = 1_048_576;      // 1 MiB connection window
    public ulong InitialMaxStreamDataBidiLocalValue { get; set; } = 262_144;
    public ulong InitialMaxStreamDataBidiRemoteValue { get; set; } = 262_144;
    public ulong InitialMaxStreamDataUniValue { get; set; } = 262_144;
    public ulong InitialMaxStreamsBidiValue { get; set; } = 100;
    public ulong InitialMaxStreamsUniValue { get; set; } = 100;
    /// <summary>
    /// Exponent used to decode the ACK Delay field of an ACK frame (RFC 9000 §18.2, parameter 0x0a):
    /// the field carries microseconds divided by 2^exponent. "If this value is absent, a default
    /// value of 3 is assumed (indicating a multiplier of 8). Values above 20 are invalid."
    /// </summary>
    public ulong AckDelayExponentValue { get; set; } = 3;

    /// <summary>
    /// The most this endpoint will ever intentionally delay an acknowledgment, in milliseconds
    /// (RFC 9000 §18.2, parameter 0x0b). §13.2.1 calls it "an explicit contract"; the peer folds it
    /// into its probe timeout, so exceeding it costs the peer spurious retransmissions. Default 25,
    /// values of 2^14 or greater are invalid.
    /// </summary>
    public ulong MaxAckDelayMs { get; set; } = 25;

    public ulong ActiveConnectionIdLimitValue { get; set; } = 2;

    /// <summary>
    /// The source connection ID from our own Initial packet. The client MUST mirror it here so the
    /// server can check the handshake's authenticity (RFC 9001 §8.2).
    /// </summary>
    public ConnectionId InitialSourceConnectionIdValue { get; set; } = ConnectionId.Empty;

    /// <summary>
    /// Set only by the server: the DCID from the very first client Initial.
    /// </summary>
    public ConnectionId? OriginalDestinationConnectionIdValue { get; set; }

    /// <summary>
    /// Set only by the server and only after a Retry: the SCID from the Retry packet (RFC 9000 §7.3).
    /// </summary>
    public ConnectionId? RetrySourceConnectionIdValue { get; set; }

    /// <summary>
    /// Stateless-reset token (16 bytes) for the handshake connection ID (RFC 9000 §10.3, §18.2). The
    /// receiver uses it to recognise a stateless-reset packet. <c>null</c> = not set.
    /// </summary>
    public byte[]? StatelessResetTokenValue { get; set; }

    /// <summary>
    /// max_datagram_frame_size (RFC 9221 §3): maximum size of a DATAGRAM frame (incl. type/length)
    /// we are willing to RECEIVE. 0 (default) = DATAGRAM frames are not supported;
    /// 65535 ("anything that fits into a QUIC packet") is recommended.
    /// </summary>
    public ulong MaxDatagramFrameSizeValue { get; set; }

    /// <summary>
    /// reset_stream_at (draft-ietf-quic-reliable-stream-reset §3): announces that we are willing to
    /// RECEIVE RESET_STREAM_AT frames (empty value). Default <c>true</c> — the extension is harmless
    /// and enables peers to do reliable partial delivery (e.g. the WebTransport stream prefix).
    /// </summary>
    public bool ResetStreamAtSupported { get; set; } = true;

    /// <summary>
    /// The peer announced reset_stream_at — we MAY send it RESET_STREAM_AT frames. Set while
    /// parsing the peer parameters.
    /// </summary>
    public bool PeerSupportsResetStreamAt { get; private set; }

    /// <summary>
    /// min_ack_delay (draft-ietf-quic-ack-frequency §3), in MICROSECONDS: the least time we are ever
    /// willing to delay an acknowledgment. Sending it is the unilateral opt-in that lets the peer send
    /// us ACK_FREQUENCY/IMMEDIATE_ACK frames. <c>null</c> disables the extension. §3: it MUST NOT be
    /// greater than max_ack_delay (which is in milliseconds). Default 1000 µs (= 1 ms, our timer
    /// granularity, RFC 9002 kGranularity).
    /// </summary>
    public ulong? MinAckDelayUs { get; set; } = 1000;

    /// <summary>
    /// The peer sent min_ack_delay (in microseconds) ⇒ we MAY send it ACK_FREQUENCY/IMMEDIATE_ACK
    /// frames, and any Requested Max Ack Delay we send must not be smaller than this. <c>null</c> =
    /// the peer did not advertise the extension. Set while parsing the peer parameters.
    /// </summary>
    public ulong? PeerMinAckDelayUs { get; private set; }

    /// <summary>
    /// Set during parsing: the peer sent initial_source_connection_id. Its ABSENCE is a connection
    /// error (RFC 9000 §7.3) — and since the empty CID is also a valid value, this flag is needed
    /// in addition to the value.
    /// </summary>
    public bool SawInitialSourceConnectionId { get; private set; }

    /// <summary>
    /// Set during parsing: the peer sent preferred_address (0x0d). We do not evaluate the content,
    /// but a SERVER receiving this server-only parameter from a client MUST close with
    /// TRANSPORT_PARAMETER_ERROR (RFC 9000 §18.2).
    /// </summary>
    public bool SawPreferredAddress { get; private set; }

    /// <summary>
    /// The server's preferred address (RFC 9000 §9.6), when one was sent or is to be sent. Only a
    /// server may set this — §18.2: "This transport parameter is only sent by a server."
    /// </summary>
    public PreferredAddress? PreferredAddressValue { get; set; }

    /// <summary>
    /// Serialises the parameters to the opaque extension bytes.
    /// </summary>
    public byte[] Encode()
    {
        var writer = new BufferWriter(256);
        try
        {
            WriteInteger(ref writer, MaxIdleTimeout, MaxIdleTimeoutMs);
            if (StatelessResetTokenValue is { Length: 16 } token)
                WriteBytes(ref writer, StatelessResetToken, token);
            WriteInteger(ref writer, MaxUdpPayloadSize, MaxUdpPayloadSizeValue);
            WriteInteger(ref writer, InitialMaxData, InitialMaxDataValue);
            WriteInteger(ref writer, InitialMaxStreamDataBidiLocal, InitialMaxStreamDataBidiLocalValue);
            WriteInteger(ref writer, InitialMaxStreamDataBidiRemote, InitialMaxStreamDataBidiRemoteValue);
            WriteInteger(ref writer, InitialMaxStreamDataUni, InitialMaxStreamDataUniValue);
            WriteInteger(ref writer, InitialMaxStreamsBidi, InitialMaxStreamsBidiValue);
            WriteInteger(ref writer, InitialMaxStreamsUni, InitialMaxStreamsUniValue);
            WriteInteger(ref writer, AckDelayExponent, AckDelayExponentValue);
            WriteInteger(ref writer, MaxAckDelay, MaxAckDelayMs);
            WriteInteger(ref writer, ActiveConnectionIdLimit, ActiveConnectionIdLimitValue);
            if (MaxDatagramFrameSizeValue > 0)
                WriteInteger(ref writer, MaxDatagramFrameSize, MaxDatagramFrameSizeValue); // RFC 9221 §3
            if (ResetStreamAtSupported)
                WriteBytes(ref writer, ResetStreamAt, []); // draft §3: an empty value signals receive readiness
            if (MinAckDelayUs is { } minAck)
                WriteInteger(ref writer, MinAckDelay, minAck); // ack-frequency draft §3, microseconds
            WriteBytes(ref writer, InitialSourceConnectionId, InitialSourceConnectionIdValue.Span);
            if (OriginalDestinationConnectionIdValue is { } odcid)
                WriteBytes(ref writer, OriginalDestinationConnectionId, odcid.Span);
            if (RetrySourceConnectionIdValue is { } rscid)
                WriteBytes(ref writer, RetrySourceConnectionId, rscid.Span);
            if (PreferredAddressValue is { } preferred)
                WriteBytes(ref writer, PreferredAddress, preferred.Encode()); // §9.6, server only

            return writer.WrittenSpan.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Parses the opaque extension bytes. Unknown parameter IDs are skipped (grease-tolerant).
    /// <c>false</c> for syntactically or semantically invalid parameters — the caller MUST treat
    /// that as the connection error TRANSPORT_PARAMETER_ERROR (RFC 9000 §7.4): duplicate IDs
    /// (§7.4 MUST NOT), max_udp_payload_size &lt; 1200, active_connection_id_limit &lt; 2, stream
    /// limits &gt; 2^60 (§4.6), stateless_reset_token ≠ 16 bytes, connection IDs &gt; 20 bytes (§17.2).
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> data, out TransportParameters? parameters)
    {
        parameters = null;
        var result = new TransportParameters
        {
            // Defaults that are only set when the peer sends them; preset neutrally here.
            MaxIdleTimeoutMs = 0,
        };
        var reader = new BufferReader(data);
        var seen = new HashSet<ulong>();

        while (!reader.IsEmpty)
        {
            if (!reader.TryReadVarInt(out ulong id) ||
                !reader.TryReadVarInt(out ulong length) ||
                length > (ulong)reader.Remaining ||
                !reader.TryReadBytes((int)length, out ReadOnlySpan<byte> value))
                return false;

            // §7.4: "An endpoint MUST NOT send a parameter more than once" — applies to ALL IDs,
            // including unknown ones; duplicates SHOULD be treated as TRANSPORT_PARAMETER_ERROR.
            if (!seen.Add(id))
                return false;

            // Connection-ID parameters may carry at most 20 bytes (RFC 9000 §17.2) — guard BEFORE
            // the ConnectionId constructor so hostile input never triggers an exception.
            if (id is InitialSourceConnectionId or OriginalDestinationConnectionId or RetrySourceConnectionId &&
                value.Length > ConnectionId.MaxLength)
                return false;

            switch (id)
            {
                case MaxIdleTimeout: result.MaxIdleTimeoutMs = ReadVarIntValue(value); break;
                case StatelessResetToken:
                    if (value.Length != 16)
                        return false; // §18.2: exactly 16 bytes
                    result.StatelessResetTokenValue = value.ToArray();
                    break;
                case MaxUdpPayloadSize:
                    result.MaxUdpPayloadSizeValue = ReadVarIntValue(value);
                    if (result.MaxUdpPayloadSizeValue < 1200)
                        return false; // §18.2: values below 1200 are invalid
                    break;
                case InitialMaxData: result.InitialMaxDataValue = ReadVarIntValue(value); break;
                case InitialMaxStreamDataBidiLocal: result.InitialMaxStreamDataBidiLocalValue = ReadVarIntValue(value); break;
                case InitialMaxStreamDataBidiRemote: result.InitialMaxStreamDataBidiRemoteValue = ReadVarIntValue(value); break;
                case InitialMaxStreamDataUni: result.InitialMaxStreamDataUniValue = ReadVarIntValue(value); break;
                case InitialMaxStreamsBidi:
                    result.InitialMaxStreamsBidiValue = ReadVarIntValue(value);
                    if (result.InitialMaxStreamsBidiValue > 1UL << 60)
                        return false; // §4.6: stream limits above 2^60 are not permitted
                    break;
                case InitialMaxStreamsUni:
                    result.InitialMaxStreamsUniValue = ReadVarIntValue(value);
                    if (result.InitialMaxStreamsUniValue > 1UL << 60)
                        return false;
                    break;
                case AckDelayExponent:
                    result.AckDelayExponentValue = ReadVarIntValue(value);
                    if (result.AckDelayExponentValue > 20)
                        return false; // §18.2: "Values above 20 are invalid."
                    break;
                case MaxAckDelay:
                    result.MaxAckDelayMs = ReadVarIntValue(value);
                    if (result.MaxAckDelayMs >= 1 << 14)
                        return false; // §18.2: "Values of 2^14 or greater are invalid."
                    break;
                case ActiveConnectionIdLimit:
                    result.ActiveConnectionIdLimitValue = ReadVarIntValue(value);
                    if (result.ActiveConnectionIdLimitValue < 2)
                        return false; // §18.2: MUST be at least 2
                    break;
                case MaxDatagramFrameSize: result.MaxDatagramFrameSizeValue = ReadVarIntValue(value); break;
                case ResetStreamAt:
                    if (value.Length != 0)
                        return false; // draft §3: non-empty value ⇒ TRANSPORT_PARAMETER_ERROR
                    result.PeerSupportsResetStreamAt = true;
                    break;
                case MinAckDelay:
                    result.PeerMinAckDelayUs = ReadVarIntValue(value); // ack-frequency draft §3, microseconds
                    break;
                case InitialSourceConnectionId:
                    result.InitialSourceConnectionIdValue = new ConnectionId(value);
                    result.SawInitialSourceConnectionId = true;
                    break;
                case OriginalDestinationConnectionId: result.OriginalDestinationConnectionIdValue = new ConnectionId(value); break;
                case RetrySourceConnectionId: result.RetrySourceConnectionIdValue = new ConnectionId(value); break;
                case PreferredAddress:
                    result.SawPreferredAddress = true; // role check (server-only, §18.2) in the endpoint
                    if (!Quic.PreferredAddress.TryParse(value, out Quic.PreferredAddress? preferred))
                        return false; // §18.2: a malformed one is TRANSPORT_PARAMETER_ERROR
                    result.PreferredAddressValue = preferred;
                    break;
                default: break; // unknown/grease -> ignore
            }
        }

        // ack-frequency draft §3: "An endpoint's min_ack_delay MUST NOT be greater than its
        // max_ack_delay." min_ack_delay is in microseconds, max_ack_delay in milliseconds. Checked
        // here rather than in the case above because the two parameters may arrive in either order.
        if (result.PeerMinAckDelayUs is { } minAckUs && minAckUs > result.MaxAckDelayMs * 1000)
            return false; // ⇒ TRANSPORT_PARAMETER_ERROR

        parameters = result;
        return true;
    }

    private static void WriteInteger(ref BufferWriter writer, ulong id, ulong value)
    {
        writer.WriteVarInt(id);
        writer.WriteVarInt((ulong)VarInt.GetLength(value));
        writer.WriteVarInt(value);
    }

    private static void WriteBytes(ref BufferWriter writer, ulong id, ReadOnlySpan<byte> value)
    {
        writer.WriteVarInt(id);
        writer.WriteVarInt((ulong)value.Length);
        writer.WriteBytes(value);
    }

    private static ulong ReadVarIntValue(ReadOnlySpan<byte> value)
        => VarInt.TryRead(value, out ulong v, out _) ? v : 0;
}
