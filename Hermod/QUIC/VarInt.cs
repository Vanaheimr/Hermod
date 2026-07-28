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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Core;

/// <summary>
/// QUIC variable-length integer (RFC 9000, §16).
/// <para>
/// The two most significant bits of the first byte encode the length of the integer
/// (2^bits bytes: 1, 2, 4 or 8). The remaining bits, together with the following
/// bytes, form the value (big-endian). Usable value range: 0 .. 2^62-1.
/// </para>
/// <code>
///   2Bit | Length | Usable bits | Maximum value
///   -----+--------+-------------+---------------------------
///    00  |   1    |      6      | 63
///    01  |   2    |     14      | 16383
///    10  |   4    |     30      | 1073741823
///    11  |   8    |     62      | 4611686018427387903
/// </code>
/// </summary>
public static class VarInt
{
    /// <summary>
    /// Largest encodable value (2^62 - 1).
    /// </summary>
    public const ulong MaxValue = (1UL << 62) - 1;

    private const ulong Max1Byte = (1UL << 6) - 1;   // 63
    private const ulong Max2Byte = (1UL << 14) - 1;  // 16383
    private const ulong Max4Byte = (1UL << 30) - 1;  // 1073741823

    /// <summary>
    /// Returns the number of bytes that <paramref name="value"/> occupies in the wire format (1, 2, 4 or 8).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="value"/> &gt; <see cref="MaxValue"/>.</exception>
    public static int GetLength(ulong value)
    {
        if (value <= Max1Byte) return 1;
        if (value <= Max2Byte) return 2;
        if (value <= Max4Byte) return 4;
        if (value <= MaxValue) return 8;
        throw new ArgumentOutOfRangeException(nameof(value), value,
            $"Value exceeds the largest encodable VarInt ({MaxValue}).");
    }

    /// <summary>
    /// Returns the total length (in bytes) of a VarInt based on its first byte,
    /// without reading the rest. Useful for checking in advance whether enough bytes are present.
    /// </summary>
    public static int GetLengthFromFirstByte(byte first) => 1 << (first >> 6);

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="destination"/> and returns the
    /// number of bytes written.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If the value is too large.</exception>
    /// <exception cref="ArgumentException">If <paramref name="destination"/> is too small.</exception>
    public static int Write(Span<byte> destination, ulong value)
    {
        int length = GetLength(value);
        if (destination.Length < length)
            throw new ArgumentException(
                $"Destination buffer too small: needs {length}, available {destination.Length}.",
                nameof(destination));

        switch (length)
        {
            case 1:
                // Prefix 00 — the upper 2 bits are 0 anyway for values <= 63.
                destination[0] = (byte)value;
                break;
            case 2:
                // Prefix 01
                BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)(value | (0b01UL << 14)));
                break;
            case 4:
                // Prefix 10
                BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)(value | (0b10UL << 30)));
                break;
            default: // 8
                // Prefix 11
                BinaryPrimitives.WriteUInt64BigEndian(destination, value | (0b11UL << 62));
                break;
        }

        return length;
    }

    /// <summary>
    /// Reads a VarInt from <paramref name="source"/>. On success returns <c>true</c> and
    /// sets <paramref name="value"/> and <paramref name="bytesRead"/>. With too few bytes
    /// (incomplete packet) returns <c>false</c> without throwing.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;
        if (source.IsEmpty)
            return false;

        int length = GetLengthFromFirstByte(source[0]);
        if (source.Length < length)
            return false;

        switch (length)
        {
            case 1:
                value = (ulong)(source[0] & 0x3F);
                break;
            case 2:
                value = BinaryPrimitives.ReadUInt16BigEndian(source) & 0x3FFFUL;
                break;
            case 4:
                value = BinaryPrimitives.ReadUInt32BigEndian(source) & 0x3FFF_FFFFUL;
                break;
            default: // 8
                value = BinaryPrimitives.ReadUInt64BigEndian(source) & 0x3FFF_FFFF_FFFF_FFFFUL;
                break;
        }

        bytesRead = length;
        return true;
    }
}
