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
/// QPACK encoder without a dynamic table (RFC 9204). Encodes header field sections exclusively with
/// the static table and literals – spec-conformant and interop-capable when
/// <c>SETTINGS_QPACK_MAX_TABLE_CAPACITY = 0</c> is announced (then the peer also forgoes the
/// dynamic table). Produces no encoder-stream instructions.
/// </summary>
public static class QpackEncoder
{
    /// <summary>
    /// Encodes a header list as an encoded field section (including the prefix).
    /// </summary>
    public static byte[] Encode(IReadOnlyList<HeaderField> headers)
    {
        var writer = new BufferWriter(128);
        try
        {
            // Field Section Prefix: Required Insert Count = 0, S = 0, Delta Base = 0.
            writer.WriteByte(0x00);
            writer.WriteByte(0x00);

            foreach (HeaderField header in headers)
                EncodeField(ref writer, header.Name.ToLowerInvariant(), header.Value);

            return writer.WrittenSpan.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void EncodeField(ref BufferWriter writer, string name, string value)
    {
        // 1) Exact (name,value) pair in the static table → indexed field line.
        if (QpackStaticTable.TryGetPairIndex(name, value, out int pairIndex))
        {
            // 1 T=1 Index(6+)  → pattern 0b1100_0000
            QpackPrimitives.EncodeInteger(ref writer, (ulong)pairIndex, 6, 0b1100_0000);
            return;
        }

        // 2) Name in the static table → literal field line with name reference.
        if (QpackStaticTable.TryGetNameIndex(name, out int nameIndex))
        {
            // 0 1 N=0 T=1 NameIndex(4+)  → pattern 0b0101_0000
            QpackPrimitives.EncodeInteger(ref writer, (ulong)nameIndex, 4, 0b0101_0000);
            QpackPrimitives.EncodeString(ref writer, value, 7, 0x00);
            return;
        }

        // 3) Otherwise → literal field line with literal name.
        // 0 0 1 N=0 H NameLen(3+)  → pattern 0b0010_0000, Huffman bit = 0x08
        QpackPrimitives.EncodeString(ref writer, name, 3, 0b0010_0000);
        QpackPrimitives.EncodeString(ref writer, value, 7, 0x00);
    }
}
