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
/// Parsing of short-header packets (1-RTT, RFC 9000, §17.3).
/// <para>
/// Unlike the long header, the short header contains <em>no</em> length field for the destination
/// connection ID. The receiver must know its length from connection state (it issued the CID
/// itself, after all). A short-header datagram also always fills the rest of the UDP packet –
/// there is no length field, hence no coalescing after it.
/// </para>
/// </summary>
public static class ShortHeader
{
    /// <summary>
    /// Determines the offset of the packet-number field from the known local DCID length.
    /// The caller passes the result to <see cref="PacketProtection.UnprotectPacket"/>
    /// (with <c>longHeader: false</c>).
    /// </summary>
    /// <returns><c>true</c> when the packet is a valid short header and large enough for DCID + sample.</returns>
    public static bool TryLocatePacketNumber(
        ReadOnlySpan<byte> datagram,
        int localConnectionIdLength,
        out ConnectionId destinationConnectionId,
        out int packetNumberOffset)
    {
        destinationConnectionId = ConnectionId.Empty;
        packetNumberOffset = 0;

        if (datagram.IsEmpty)
            return false;
        byte first = datagram[0];
        if (!PacketFormat.IsShortHeader(first) || (first & PacketFormat.FixedBit) == 0)
            return false;

        int dcidEnd = 1 + localConnectionIdLength;
        if (datagram.Length < dcidEnd)
            return false;

        destinationConnectionId = new ConnectionId(datagram.Slice(1, localConnectionIdLength));
        packetNumberOffset = dcidEnd;
        return true;
    }

    /// <summary>
    /// Builds a protected 1-RTT packet (short header). The first byte carries header form 0, fixed
    /// bit 1, spin 0, reserved 0, key phase <paramref name="keyPhase"/> and the packet-number length.
    /// </summary>
    public static byte[] Build(
        PacketProtection protection,
        ConnectionId destinationConnectionId,
        ulong packetNumber,
        int packetNumberLength,
        ReadOnlySpan<byte> payload,
        bool keyPhase = false)
    {
        if (packetNumberLength is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(packetNumberLength));

        using var header = new BufferWriter();
        byte first = (byte)(PacketFormat.FixedBit | (keyPhase ? 0x04 : 0) | (packetNumberLength - 1));
        header.WriteByte(first);
        header.WriteBytes(destinationConnectionId.Span);

        Span<byte> pn = stackalloc byte[4];
        PacketNumber.Encode(pn, packetNumber, packetNumberLength);
        header.WriteBytes(pn[..packetNumberLength]);

        // Pad a very small payload so the header-protection sample fits (RFC 9001 §5.4.2).
        byte[] padded = PacketPadding.ForSampling(payload, packetNumberLength);
        return protection.ProtectPacket(header.WrittenSpan, packetNumberLength, packetNumber, padded, longHeader: false);
    }
}
