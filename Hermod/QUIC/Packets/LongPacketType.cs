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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Long-header packet types in QUIC v1 (RFC 9000, table 5).
/// </summary>
public enum LongPacketType : byte
{
    Initial = 0x00,
    ZeroRtt = 0x01,
    Handshake = 0x02,
    Retry = 0x03,
}

/// <summary>
/// Helper functions for the first byte of a packet (RFC 9000, §17.2/§17.3).
/// </summary>
public static class PacketFormat
{
    /// <summary>
    /// Header form (0x80): set = long header.
    /// </summary>
    public const byte HeaderFormBit = 0x80;

    /// <summary>
    /// Fixed bit (0x40): set in valid packets (except version negotiation).
    /// </summary>
    public const byte FixedBit = 0x40;

    /// <summary>
    /// <c>true</c> when the first byte announces a long header.
    /// </summary>
    public static bool IsLongHeader(byte firstByte) => (firstByte & HeaderFormBit) != 0;

    /// <summary>
    /// <c>true</c> when the first byte announces a short header (1-RTT).
    /// </summary>
    public static bool IsShortHeader(byte firstByte) => (firstByte & HeaderFormBit) == 0;

    /// <summary>
    /// Long packet type from bits 0x30 of the first byte (only valid for long headers).
    /// </summary>
    public static LongPacketType GetLongPacketType(byte firstByte) => (LongPacketType)((firstByte & 0x30) >> 4);

    /// <summary>
    /// Version negotiation is recognised by the version field being 0 – not by the first byte
    /// (its lower bits are unspecified for VN). This check assumes a long header.
    /// </summary>
    public static bool IsVersionNegotiation(uint version) => version == 0;

    /// <summary>
    /// Builds the first byte of a long-header packet with a packet number (Initial/Handshake/0-RTT):
    /// header form + fixed bit + type, reserved = 0, packet number length = <paramref name="pnLength"/> − 1.
    /// </summary>
    public static byte BuildLongHeaderFirstByte(LongPacketType type, int pnLength)
    {
        if (pnLength is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(pnLength));
        return (byte)(HeaderFormBit | FixedBit | ((byte)type << 4) | (pnLength - 1));
    }
}
