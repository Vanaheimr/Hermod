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

using System.Text;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

/// <summary>
/// The encoding primitives of QPACK/HPACK: integers with an N-bit prefix (RFC 7541 §5.1) and
/// length-prefixed strings with optional Huffman encoding (§5.2).
/// </summary>
internal static class QpackPrimitives
{
    /// <summary>
    /// Encodes an integer with an <paramref name="prefixBits"/>-bit prefix. <paramref name="prefixPattern"/>
    /// contains the higher-order type bits that sit above the prefix in the first byte.
    /// </summary>
    public static void EncodeInteger(ref BufferWriter writer, ulong value, int prefixBits, byte prefixPattern)
    {
        uint max = (1u << prefixBits) - 1;
        if (value < max)
        {
            writer.WriteByte((byte)(prefixPattern | (byte)value));
            return;
        }

        writer.WriteByte((byte)(prefixPattern | (byte)max));
        value -= max;
        while (value >= 128)
        {
            writer.WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }
        writer.WriteByte((byte)value);
    }

    /// <summary>
    /// Decodes an integer with an N-bit prefix. <paramref name="firstByte"/> is the already-read
    /// first byte; the low-order <paramref name="prefixBits"/> bits form the starting value.
    /// </summary>
    public static bool TryDecodeInteger(ref BufferReader reader, byte firstByte, int prefixBits, out ulong value)
    {
        uint max = (1u << prefixBits) - 1;
        value = (uint)(firstByte & max);
        if (value < max)
            return true;

        int shift = 0;
        while (true)
        {
            if (!reader.TryReadByte(out byte b))
                return false;
            value += (ulong)(b & 0x7f) << shift;
            if ((b & 0x80) == 0)
                return true;
            shift += 7;
            if (shift > 62)
                return false; // protection against overlong encoding
        }
    }

    /// <summary>
    /// Writes a string (name or value): 1 Huffman flag bit + an <paramref name="prefixBits"/>-bit
    /// length prefix, then the (possibly Huffman-encoded) bytes. Huffman is chosen when it is shorter.
    /// </summary>
    public static void EncodeString(ref BufferWriter writer, string value, int prefixBits, byte prefixPattern)
    {
        byte[] raw = Encoding.ASCII.GetBytes(value);
        int huffmanLength = Huffman.EncodedLength(raw);

        byte huffmanBit = (byte)(1 << prefixBits); // the bit directly above the length prefix
        if (huffmanLength < raw.Length)
        {
            EncodeInteger(ref writer, (ulong)huffmanLength, prefixBits, (byte)(prefixPattern | huffmanBit));
            Huffman.Encode(ref writer, raw);
        }
        else
        {
            EncodeInteger(ref writer, (ulong)raw.Length, prefixBits, prefixPattern);
            writer.WriteBytes(raw);
        }
    }

    /// <summary>
    /// Reads a string. <paramref name="firstByte"/> is already read (contains the Huffman bit + length).
    /// </summary>
    public static bool TryDecodeString(ref BufferReader reader, byte firstByte, int prefixBits, out string value)
    {
        value = string.Empty;
        bool huffman = (firstByte & (1 << prefixBits)) != 0;
        if (!TryDecodeInteger(ref reader, firstByte, prefixBits, out ulong length) || length > (ulong)reader.Remaining)
            return false;

        if (!reader.TryReadBytes((int)length, out ReadOnlySpan<byte> raw))
            return false;

        if (huffman)
        {
            if (!Huffman.TryDecode(raw, out byte[] decoded))
                return false;
            value = Encoding.ASCII.GetString(decoded);
        }
        else
        {
            value = Encoding.ASCII.GetString(raw);
        }
        return true;
    }
}
