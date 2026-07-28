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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// The cleartext-readable fields of a long-header packet with a packet number
/// (Initial / Handshake / 0-RTT), up to just before the header-protected bytes.
/// <para>
/// All fields except the lower bits of the first byte and the packet number itself are
/// unencrypted. From this prefix follows <see cref="PacketNumberOffset"/>, which
/// <see cref="PacketProtection"/> needs to remove the header protection.
/// </para>
/// </summary>
public sealed class LongHeaderPrefix
{
    public required LongPacketType Type { get; init; }
    public required uint Version { get; init; }
    public required ConnectionId DestinationConnectionId { get; init; }
    public required ConnectionId SourceConnectionId { get; init; }

    /// <summary>
    /// Retry/NEW_TOKEN token; only present in Initial packets (otherwise empty).
    /// </summary>
    public required byte[] Token { get; init; }

    /// <summary>
    /// Length of packet number + payload (incl. AEAD tag) per the length field.
    /// </summary>
    public required long Length { get; init; }

    /// <summary>
    /// Offset of the packet-number field within the datagram.
    /// </summary>
    public required int PacketNumberOffset { get; init; }

    /// <summary>
    /// Offset of the first byte after this packet (for coalesced packets): <c>PacketNumberOffset + Length</c>.
    /// </summary>
    public int PacketEndOffset => PacketNumberOffset + (int)Length;
}

/// <summary>
/// Parsing and serialising of long-header packets (RFC 9000, §17.2).
/// </summary>
public static class LongHeader
{
    /// <summary>
    /// Parses the cleartext fields of a long-header packet with a packet number (Initial/Handshake/0-RTT).
    /// Header and packet protection are <em>not</em> removed here – call
    /// <see cref="PacketProtection.UnprotectPacket"/> with <see cref="LongHeaderPrefix.PacketNumberOffset"/> afterwards.
    /// </summary>
    /// <returns><c>true</c> for a well-formed prefix; <c>false</c> for too-short/invalid data (drop the packet).</returns>
    public static bool TryParse(ReadOnlySpan<byte> datagram, out LongHeaderPrefix? prefix)
    {
        prefix = null;
        var reader = new BufferReader(datagram);

        if (!reader.TryReadByte(out byte first))
            return false;
        if (!PacketFormat.IsLongHeader(first) || (first & PacketFormat.FixedBit) == 0)
            return false;

        if (!reader.TryReadUInt32(out uint version))
            return false;
        // Version 0 = version negotiation: different format, not handled here.
        if (version == 0)
            return false;

        LongPacketType type = PacketFormat.GetLongPacketType(first);
        // Retry carries no length/packet number – handled separately.
        if (type == LongPacketType.Retry)
            return false;

        if (!TryReadConnectionId(ref reader, out ConnectionId dcid) ||
            !TryReadConnectionId(ref reader, out ConnectionId scid))
            return false;

        byte[] token = [];
        if (type == LongPacketType.Initial)
        {
            if (!reader.TryReadVarInt(out ulong tokenLength) || tokenLength > (ulong)reader.Remaining)
                return false;
            if (!reader.TryReadBytes((int)tokenLength, out ReadOnlySpan<byte> tokenSpan))
                return false;
            token = tokenSpan.ToArray();
        }

        if (!reader.TryReadVarInt(out ulong length))
            return false;

        int pnOffset = reader.Position;
        // The length field must lie fully within the datagram.
        if (length > (ulong)reader.Remaining)
            return false;

        prefix = new LongHeaderPrefix
        {
            Type = type,
            Version = version,
            DestinationConnectionId = dcid,
            SourceConnectionId = scid,
            Token = token,
            Length = (long)length,
            PacketNumberOffset = pnOffset,
        };
        return true;
    }

    /// <summary>
    /// Reads the <em>version-independent</em> fields of a long header (RFC 8999): first byte,
    /// version, destination and source connection ID. Also works for unknown versions and
    /// version-negotiation/Retry packets, since this prefix is laid out identically in all QUIC versions.
    /// </summary>
    public static bool TryParseInvariant(ReadOnlySpan<byte> datagram, out uint version, out ConnectionId dcid, out ConnectionId scid)
    {
        version = 0;
        dcid = ConnectionId.Empty;
        scid = ConnectionId.Empty;

        var reader = new BufferReader(datagram);
        if (!reader.TryReadByte(out byte first) || !PacketFormat.IsLongHeader(first))
            return false;
        if (!reader.TryReadUInt32(out version))
            return false;
        return TryReadConnectionId(ref reader, out dcid) && TryReadConnectionId(ref reader, out scid);
    }

    private static bool TryReadConnectionId(ref BufferReader reader, out ConnectionId cid)
    {
        cid = ConnectionId.Empty;
        if (!reader.TryReadByte(out byte len))
            return false;
        if (len > ConnectionId.MaxLength)
            return false; // RFC 9000 §17.2: > 20 bytes -> drop the packet.
        if (!reader.TryReadBytes(len, out ReadOnlySpan<byte> bytes))
            return false;
        cid = new ConnectionId(bytes);
        return true;
    }

    /// <summary>
    /// Builds a complete, protected Initial, Handshake or 0-RTT packet from structured fields.
    /// </summary>
    /// <param name="protection">Packet/header protection of the sending side/level.</param>
    /// <param name="type"><see cref="LongPacketType.Initial"/>, <see cref="LongPacketType.Handshake"/> or
    /// <see cref="LongPacketType.ZeroRtt"/> (0-RTT, like Handshake, has no token field).</param>
    /// <param name="token">Initial token (empty if none); ignored for Handshake/0-RTT.</param>
    /// <param name="packetNumberLength">Length of the packet number on the wire (1–4).</param>
    public static byte[] Build(
        PacketProtection protection,
        LongPacketType type,
        uint version,
        ConnectionId destinationConnectionId,
        ConnectionId sourceConnectionId,
        ReadOnlySpan<byte> token,
        ulong packetNumber,
        int packetNumberLength,
        ReadOnlySpan<byte> payload)
    {
        if (type is not (LongPacketType.Initial or LongPacketType.Handshake or LongPacketType.ZeroRtt))
            throw new ArgumentException("Build only supports Initial, Handshake and 0-RTT.", nameof(type));
        if (packetNumberLength is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(packetNumberLength));

        // Pad a small payload (e.g. only an ACK) with PADDING so the sample fits (RFC 9001 §5.4.2).
        payload = PacketPadding.ForSampling(payload, packetNumberLength);

        using var header = new BufferWriter();
        header.WriteByte(PacketFormat.BuildLongHeaderFirstByte(type, packetNumberLength));
        header.WriteUInt32(version);
        header.WriteByte((byte)destinationConnectionId.Length);
        header.WriteBytes(destinationConnectionId.Span);
        header.WriteByte((byte)sourceConnectionId.Length);
        header.WriteBytes(sourceConnectionId.Span);
        if (type == LongPacketType.Initial)
        {
            header.WriteVarInt((ulong)token.Length);
            header.WriteBytes(token);
        }

        // Length = packet number + payload + 16-byte AEAD tag.
        long lengthField = packetNumberLength + payload.Length + 16;
        header.WriteVarInt((ulong)lengthField);

        Span<byte> pn = stackalloc byte[4];
        PacketNumber.Encode(pn, packetNumber, packetNumberLength);
        header.WriteBytes(pn[..packetNumberLength]);

        return protection.ProtectPacket(header.WrittenSpan, packetNumberLength, packetNumber, payload, longHeader: true);
    }
}
