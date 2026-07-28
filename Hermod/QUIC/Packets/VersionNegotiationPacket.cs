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

using System.Security.Cryptography;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Version-negotiation packet (RFC 9000 §17.2.1). A server sends it when it does not support the
/// version chosen by the client; it lists the versions the server supports. Recognisable by version
/// field 0. Carries no packet number, no encryption. When answering, the server swaps DCID/SCID:
/// the client's SCID becomes the VN packet's DCID and vice versa.
/// </summary>
public static class VersionNegotiationPacket
{
    /// <summary>
    /// Builds a version-negotiation packet with the list of supported versions.
    /// </summary>
    public static byte[] Build(ConnectionId destinationConnectionId, ConnectionId sourceConnectionId, IReadOnlyList<uint> supportedVersions)
    {
        using var w = new BufferWriter(16 + supportedVersions.Count * 4);
        // First byte: only the long-header form matters; the remaining 7 bits are set randomly
        // (RFC 9000 §17.2.1 – hampers ossification). The receiver recognises VN by version field 0.
        w.WriteByte((byte)(0x80 | (RandomNumberGenerator.GetBytes(1)[0] & 0x7f)));
        w.WriteUInt32(0); // version = 0 marks version negotiation
        w.WriteByte((byte)destinationConnectionId.Length);
        w.WriteBytes(destinationConnectionId.Span);
        w.WriteByte((byte)sourceConnectionId.Length);
        w.WriteBytes(sourceConnectionId.Span);
        foreach (uint v in supportedVersions)
            w.WriteUInt32(v);
        return w.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Parses a version-negotiation packet. Sets <paramref name="supportedVersions"/> to the list.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> datagram, out ConnectionId dcid, out ConnectionId scid, out List<uint> supportedVersions)
    {
        dcid = ConnectionId.Empty;
        scid = ConnectionId.Empty;
        supportedVersions = [];

        var r = new BufferReader(datagram);
        if (!r.TryReadByte(out byte first) || !PacketFormat.IsLongHeader(first))
            return false;
        if (!r.TryReadUInt32(out uint version) || version != 0)
            return false;
        if (!TryReadCid(ref r, out dcid) || !TryReadCid(ref r, out scid))
            return false;

        // The rest is a sequence of 4-byte versions (at least one).
        while (r.Remaining >= 4)
        {
            r.TryReadUInt32(out uint v);
            supportedVersions.Add(v);
        }
        return supportedVersions.Count > 0;
    }

    private static bool TryReadCid(ref BufferReader r, out ConnectionId cid)
    {
        cid = ConnectionId.Empty;
        if (!r.TryReadByte(out byte len) || len > ConnectionId.MaxLength || !r.TryReadBytes(len, out ReadOnlySpan<byte> bytes))
            return false;
        cid = new ConnectionId(bytes);
        return true;
    }
}
