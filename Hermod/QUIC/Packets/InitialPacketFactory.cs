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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Assembles client Initial packets (CRYPTO frame + PADDING + packet/header protection).
/// </summary>
public static class InitialPacketFactory
{
    /// <summary>
    /// A client Initial datagram MUST be at least 1200 bytes (RFC 9000 §14.1) so the server has
    /// sufficient amplification budget. If the ClientHello carries less, PADDING fills the rest.
    /// </summary>
    public const int MinimumClientInitialSize = 1200;

    /// <summary>
    /// Creates a protected client Initial that carries <paramref name="cryptoData"/> (typically the
    /// ClientHello) in a CRYPTO frame starting at offset 0, padded to ≥ 1200 bytes.
    /// </summary>
    public static byte[] BuildClientInitial(
        PacketProtection clientProtection,
        uint version,
        ConnectionId destinationConnectionId,
        ConnectionId sourceConnectionId,
        ReadOnlySpan<byte> token,
        ulong packetNumber,
        int packetNumberLength,
        ReadOnlySpan<byte> cryptoData)
    {
        byte[] payload = FrameParser.Serialize([new CryptoFrame(0, cryptoData.ToArray())]);
        return BuildPadded(clientProtection, version, destinationConnectionId, sourceConnectionId,
            token, packetNumber, packetNumberLength, payload);
    }

    /// <summary>
    /// Builds an Initial packet from arbitrary already-serialised frames and pads it with PADDING to
    /// ≥ 1200 bytes. Usable for the first Initial (CRYPTO) as well as later Initials (e.g. just an
    /// ACK) – every datagram containing a client Initial must satisfy the minimum size.
    /// </summary>
    public static byte[] BuildPadded(
        PacketProtection clientProtection,
        uint version,
        ConnectionId destinationConnectionId,
        ConnectionId sourceConnectionId,
        ReadOnlySpan<byte> token,
        ulong packetNumber,
        int packetNumberLength,
        ReadOnlySpan<byte> frames)
    {
        // Pre-compute the header overhead to determine the needed amount of PADDING.
        // The length field is 2 bytes for ~1200-byte packets (value < 16384).
        int headerOverhead =
            1                                     // first byte
            + 4                                   // version
            + 1 + destinationConnectionId.Length  // DCID length + DCID
            + 1 + sourceConnectionId.Length       // SCID length + SCID
            + VarInt.GetLength((ulong)token.Length) + token.Length
            + 2                                   // length varint (assumption: 2 bytes)
            + packetNumberLength;
        const int authTag = 16;

        int payloadForMinimum = MinimumClientInitialSize - headerOverhead - authTag;
        int payloadLength = Math.Max(frames.Length, payloadForMinimum);

        byte[] payload = new byte[payloadLength];
        frames.CopyTo(payload);
        // The remainder stays 0 => PADDING frames.

        return LongHeader.Build(
            clientProtection, LongPacketType.Initial, version,
            destinationConnectionId, sourceConnectionId, token,
            packetNumber, packetNumberLength, payload);
    }
}
