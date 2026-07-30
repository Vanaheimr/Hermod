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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

/// <summary>
/// Huffman encoding/decoding per the static HPACK/QPACK table (RFC 7541, appendix B).
/// Codes are prefix-free; the encoding pads at the end with 1 bits (the EOS prefix) to the byte boundary.
/// </summary>
internal static partial class Huffman
{
    // (bit length << 32) | code  ->  symbol. Prefix-freeness guarantees at most one match per step.
    // Lazy, since the field initialisation order across partial-class files (Table) is not guaranteed.
    private static Dictionary<long, int>? _decodeMap;
    private static Dictionary<long, int> DecodeMap => _decodeMap ??= BuildDecodeMap();

    private static Dictionary<long, int> BuildDecodeMap()
    {
        var map = new Dictionary<long, int>(Table.Length);
        for (int sym = 0; sym < Table.Length; sym++)
            map[Key(Table[sym].Bits, Table[sym].Code)] = sym;
        return map;
    }

    private static long Key(int bits, uint code) => ((long)bits << 32) | code;

    /// <summary>
    /// Length of the Huffman-encoded form of <paramref name="data"/> in bytes.
    /// </summary>
    public static int EncodedLength(ReadOnlySpan<byte> data)
    {
        long bits = 0;
        foreach (byte b in data)
            bits += Table[b].Bits;
        return (int)((bits + 7) / 8);
    }

    /// <summary>
    /// Huffman-encodes <paramref name="data"/> and writes the result into <paramref name="writer"/>.
    /// </summary>
    public static void Encode(ref BufferWriter writer, ReadOnlySpan<byte> data)
    {
        ulong buffer = 0;
        int bitCount = 0;
        foreach (byte b in data)
        {
            (uint code, byte bits) = Table[b];
            buffer = (buffer << bits) | code;
            bitCount += bits;
            while (bitCount >= 8)
            {
                bitCount -= 8;
                writer.WriteByte((byte)(buffer >> bitCount));
            }
        }
        if (bitCount > 0)
        {
            // Pad with 1 bits (the EOS prefix).
            int pad = 8 - bitCount;
            byte last = (byte)((buffer << pad) | ((1u << pad) - 1));
            writer.WriteByte(last);
        }
    }

    /// <summary>
    /// Decodes a Huffman-encoded byte sequence. Returns <c>false</c> for an invalid encoding.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> data, out byte[] result)
    {
        result = [];
        var output = new List<byte>(data.Length * 2);
        uint code = 0;
        int len = 0;

        foreach (byte b in data)
        {
            for (int i = 7; i >= 0; i--)
            {
                code = (code << 1) | (uint)((b >> i) & 1);
                len++;
                if (len > 30)
                    return false; // no code longer than 30 bits
                if (DecodeMap.TryGetValue(Key(len, code), out int sym))
                {
                    if (sym == 256)
                        return false; // EOS must not occur inside a string
                    output.Add((byte)sym);
                    code = 0;
                    len = 0;
                }
            }
        }

        // The remainder must be valid padding: fewer than 8 bits, all 1 (a prefix of EOS).
        if (len >= 8)
            return false;
        if (len > 0 && code != (1u << len) - 1)
            return false;

        result = [.. output];
        return true;
    }
}
