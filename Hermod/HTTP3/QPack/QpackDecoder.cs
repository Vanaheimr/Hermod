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
/// Result of a QPACK decode.
/// </summary>
public enum QpackResult
{
    Ok,
    /// <summary>
    /// Malformed/truncated encoding ⇒ QPACK_DECOMPRESSION_FAILED.
    /// </summary>
    DecompressionFailed,
    /// <summary>
    /// A reference to the dynamic table, which we do not maintain with capacity 0.
    /// </summary>
    DynamicTableReference,

    /// <summary>
    /// The field section requires more inserts than the decoder has received so far (stream blocked).
    /// </summary>
    Blocked,
}

/// <summary>
/// QPACK decoder without a dynamic table (RFC 9204). Decodes encoded field sections that use only
/// the static table and literals. References to the dynamic table (indexed/literal with post-base
/// or a dynamic table reference) are rejected, since we announce capacity 0.
/// </summary>
public static class QpackDecoder
{
    public static QpackResult Decode(ReadOnlySpan<byte> encoded, out List<HeaderField> headers)
    {
        headers = [];
        var reader = new BufferReader(encoded);

        // Field Section Prefix: Required Insert Count + (S, Delta Base).
        if (!reader.TryReadByte(out byte ricByte) ||
            !QpackPrimitives.TryDecodeInteger(ref reader, ricByte, 8, out ulong requiredInsertCount) ||
            !reader.TryReadByte(out byte baseByte) ||
            !QpackPrimitives.TryDecodeInteger(ref reader, baseByte, 7, out _))
            return QpackResult.DecompressionFailed;

        if (requiredInsertCount != 0)
            return QpackResult.DynamicTableReference; // we maintain no dynamic table

        while (!reader.IsEmpty)
        {
            if (!reader.TryReadByte(out byte first))
                return QpackResult.DecompressionFailed;

            QpackResult result = (first & 0x80) != 0
                ? DecodeIndexed(ref reader, first, headers)
                : (first & 0x40) != 0
                    ? DecodeLiteralWithNameRef(ref reader, first, headers)
                    : (first & 0x20) != 0
                        ? DecodeLiteralWithLiteralName(ref reader, first, headers)
                        : QpackResult.DynamicTableReference; // post-base forms -> dynamic

            if (result != QpackResult.Ok)
                return result;
        }

        return QpackResult.Ok;
    }

    // 1 T Index(6+)
    private static QpackResult DecodeIndexed(ref BufferReader reader, byte first, List<HeaderField> headers)
    {
        bool isStatic = (first & 0x40) != 0;
        if (!isStatic)
            return QpackResult.DynamicTableReference;
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 6, out ulong index) || index >= (ulong)QpackStaticTable.Count)
            return QpackResult.DecompressionFailed;

        (string name, string value) = QpackStaticTable.Get((int)index);
        headers.Add(new HeaderField(name, value));
        return QpackResult.Ok;
    }

    // 0 1 N T NameIndex(4+) + Value
    private static QpackResult DecodeLiteralWithNameRef(ref BufferReader reader, byte first, List<HeaderField> headers)
    {
        bool isStatic = (first & 0x10) != 0;
        if (!isStatic)
            return QpackResult.DynamicTableReference;
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 4, out ulong index) || index >= (ulong)QpackStaticTable.Count)
            return QpackResult.DecompressionFailed;

        string name = QpackStaticTable.Get((int)index).Name;
        if (!reader.TryReadByte(out byte valueFirst) ||
            !QpackPrimitives.TryDecodeString(ref reader, valueFirst, 7, out string value))
            return QpackResult.DecompressionFailed;

        headers.Add(new HeaderField(name, value));
        return QpackResult.Ok;
    }

    // 0 0 1 N H NameLen(3+) + Name + Value
    private static QpackResult DecodeLiteralWithLiteralName(ref BufferReader reader, byte first, List<HeaderField> headers)
    {
        if (!QpackPrimitives.TryDecodeString(ref reader, first, 3, out string name))
            return QpackResult.DecompressionFailed;
        if (!reader.TryReadByte(out byte valueFirst) ||
            !QpackPrimitives.TryDecodeString(ref reader, valueFirst, 7, out string value))
            return QpackResult.DecompressionFailed;

        headers.Add(new HeaderField(name, value));
        return QpackResult.Ok;
    }
}
