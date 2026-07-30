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
/// QPACK decoder with a dynamic table (RFC 9204). Stateful: processes the encoder-stream
/// instructions (Set Capacity, Insert With Name Reference, Insert With Literal Name, Duplicate)
/// into its dynamic table and then decodes field sections that reference static as well as dynamic
/// entries (incl. post-base indexing). Order: first the encoder instructions, then the field
/// section; if the latter requires more inserts than present, the stream is <see cref="QpackResult.Blocked"/>.
/// </summary>
public sealed class QpackDynamicDecoder
{
    private readonly QpackDynamicTable _table = new();

    /// <summary>
    /// The decoder's dynamic table (diagnostics/test).
    /// </summary>
    public QpackDynamicTable Table => _table;

    /// <summary>
    /// Encodes a section acknowledgment (RFC 9204 §4.4.1): <c>1 StreamID(7+)</c>.
    /// </summary>
    public static byte[] EncodeSectionAcknowledgment(ulong streamId)
    {
        var w = new BufferWriter(4);
        try { QpackPrimitives.EncodeInteger(ref w, streamId, 7, 0b1000_0000); return w.WrittenSpan.ToArray(); }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Encodes an insert count increment (RFC 9204 §4.4.3): <c>0 0 Increment(6+)</c>.
    /// </summary>
    public static byte[] EncodeInsertCountIncrement(ulong increment)
    {
        var w = new BufferWriter(4);
        try { QpackPrimitives.EncodeInteger(ref w, increment, 6, 0b0000_0000); return w.WrittenSpan.ToArray(); }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Processes a section of the QPACK encoder stream into the dynamic table and returns whether
    /// all bytes formed complete instructions (<see cref="QpackResult.Ok"/>).
    /// </summary>
    public QpackResult ProcessEncoderInstructions(ReadOnlySpan<byte> data)
        => ProcessEncoderInstructions(data, out int consumed) && consumed == data.Length
            ? QpackResult.Ok
            : QpackResult.DecompressionFailed;

    /// <summary>
    /// Streaming variant: processes as many <b>complete</b> instructions as possible and sets
    /// <paramref name="consumed"/> to the number of consumed bytes (a truncated instruction at the
    /// end is left in place). Returns <c>false</c> only for a structurally invalid instruction.
    /// </summary>
    public bool ProcessEncoderInstructions(ReadOnlySpan<byte> data, out int consumed)
    {
        consumed = 0;
        var reader = new BufferReader(data);
        while (!reader.IsEmpty)
        {
            if (!TryProcessOneInstruction(ref reader, out bool incomplete))
            {
                // Truncated instruction at the end of the buffer: wait for more data (no error).
                return incomplete;
            }
            consumed = reader.Position;
        }
        return true;
    }

    private bool TryProcessOneInstruction(ref BufferReader reader, out bool incomplete)
    {
        incomplete = true;
        if (!reader.TryReadByte(out byte first))
            return false;

        if ((first & 0x80) != 0) // Insert with Name Reference: 1 T NameIndex(6+) + Value
        {
            bool isStatic = (first & 0x40) != 0;
            if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 6, out ulong nameIndex) ||
                !TryReadString(ref reader, 7, out string value))
                return false;
            if (!TryResolveName(isStatic, nameIndex, out string name) || !_table.Insert(name, value))
            { incomplete = false; return false; } // structural error
        }
        else if ((first & 0x40) != 0) // Insert with Literal Name: 0 1 H NameLen(5+) + Name + Value
        {
            if (!QpackPrimitives.TryDecodeString(ref reader, first, 5, out string name) ||
                !TryReadString(ref reader, 7, out string value))
                return false;
            if (!_table.Insert(name, value))
            { incomplete = false; return false; }
        }
        else if ((first & 0x20) != 0) // Set Dynamic Table Capacity: 0 0 1 Capacity(5+)
        {
            if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 5, out ulong capacity))
                return false;
            _table.SetCapacity(capacity);
        }
        else // Duplicate: 0 0 0 Index(5+)
        {
            if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 5, out ulong relIndex))
                return false;
            if (!TryRelativeToAbsolute(relIndex, out ulong abs) ||
                !_table.TryGetByAbsolute(abs, out (string Name, string Value) entry) ||
                !_table.Insert(entry.Name, entry.Value))
            { incomplete = false; return false; }
        }

        incomplete = false;
        return true;
    }

    /// <summary>
    /// Decodes a field section (with prefix). Encoder instructions must be processed beforehand.
    /// </summary>
    public QpackResult Decode(ReadOnlySpan<byte> encoded, out List<HeaderField> headers)
        => Decode(encoded, out headers, out _);

    /// <summary>
    /// Like <see cref="Decode(ReadOnlySpan{byte}, out List{HeaderField})"/>, but additionally reports
    /// the section's required insert count – if it is &gt; 0, the caller should send a section acknowledgment.
    /// </summary>
    public QpackResult Decode(ReadOnlySpan<byte> encoded, out List<HeaderField> headers, out ulong requiredInsertCount)
    {
        headers = [];
        requiredInsertCount = 0;
        var reader = new BufferReader(encoded);

        // Field section prefix: required insert count (8+) and sign+delta base (7+).
        if (!reader.TryReadByte(out byte ricByte) ||
            !QpackPrimitives.TryDecodeInteger(ref reader, ricByte, 8, out ulong encodedInsertCount) ||
            !TryReconstructRequiredInsertCount(encodedInsertCount, out requiredInsertCount) ||
            !reader.TryReadByte(out byte baseByte) ||
            !QpackPrimitives.TryDecodeInteger(ref reader, baseByte, 7, out ulong deltaBase))
            return QpackResult.DecompressionFailed;

        bool sign = (baseByte & 0x80) != 0;
        ulong baseValue;
        if (sign)
        {
            if (requiredInsertCount < deltaBase + 1)
                return QpackResult.DecompressionFailed;
            baseValue = requiredInsertCount - deltaBase - 1;
        }
        else
        {
            baseValue = requiredInsertCount + deltaBase;
        }

        if (requiredInsertCount > _table.InsertCount)
            return QpackResult.Blocked; // the referenced entries have not yet arrived

        while (!reader.IsEmpty)
        {
            if (!reader.TryReadByte(out byte first))
                return QpackResult.DecompressionFailed;

            QpackResult result;
            if ((first & 0x80) != 0)          // Indexed Field Line: 1 T Index(6+)
                result = DecodeIndexed(ref reader, first, baseValue, headers);
            else if ((first & 0x40) != 0)     // Literal With Name Reference: 0 1 N T NameIndex(4+)
                result = DecodeLiteralNameRef(ref reader, first, baseValue, headers);
            else if ((first & 0x20) != 0)     // Literal With Literal Name: 0 0 1 N H NameLen(3+)
                result = DecodeLiteralLiteralName(ref reader, first, headers);
            else if ((first & 0x10) != 0)     // Indexed With Post-Base Index: 0 0 0 1 Index(4+)
                result = DecodePostBaseIndexed(ref reader, first, baseValue, headers);
            else                              // Literal With Post-Base Name Reference: 0 0 0 0 N NameIndex(3+)
                result = DecodePostBaseNameRef(ref reader, first, baseValue, headers);

            if (result != QpackResult.Ok)
                return result;
        }
        return QpackResult.Ok;
    }

    // ---- Field-line representations --------------------------------------------------------

    private QpackResult DecodeIndexed(ref BufferReader reader, byte first, ulong baseValue, List<HeaderField> headers)
    {
        bool isStatic = (first & 0x40) != 0;
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 6, out ulong index))
            return QpackResult.DecompressionFailed;

        if (isStatic)
        {
            if (index >= (ulong)QpackStaticTable.Count)
                return QpackResult.DecompressionFailed;
            (string sn, string sv) = QpackStaticTable.Get((int)index);
            headers.Add(new HeaderField(sn, sv));
            return QpackResult.Ok;
        }

        // Dynamic (pre-base): abs = base - 1 - relIndex.
        if (baseValue < 1 + index || !_table.TryGetByAbsolute(baseValue - 1 - index, out (string Name, string Value) e))
            return QpackResult.DecompressionFailed;
        headers.Add(new HeaderField(e.Name, e.Value));
        return QpackResult.Ok;
    }

    private QpackResult DecodePostBaseIndexed(ref BufferReader reader, byte first, ulong baseValue, List<HeaderField> headers)
    {
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 4, out ulong index) ||
            !_table.TryGetByAbsolute(baseValue + index, out (string Name, string Value) e))
            return QpackResult.DecompressionFailed;
        headers.Add(new HeaderField(e.Name, e.Value));
        return QpackResult.Ok;
    }

    private QpackResult DecodeLiteralNameRef(ref BufferReader reader, byte first, ulong baseValue, List<HeaderField> headers)
    {
        bool isStatic = (first & 0x10) != 0;
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 4, out ulong index))
            return QpackResult.DecompressionFailed;

        string name;
        if (isStatic)
        {
            if (index >= (ulong)QpackStaticTable.Count)
                return QpackResult.DecompressionFailed;
            name = QpackStaticTable.Get((int)index).Name;
        }
        else if (baseValue >= 1 + index && _table.TryGetByAbsolute(baseValue - 1 - index, out (string Name, string Value) e))
        {
            name = e.Name;
        }
        else
        {
            return QpackResult.DecompressionFailed;
        }

        if (!TryReadString(ref reader, 7, out string value))
            return QpackResult.DecompressionFailed;
        headers.Add(new HeaderField(name, value));
        return QpackResult.Ok;
    }

    private QpackResult DecodePostBaseNameRef(ref BufferReader reader, byte first, ulong baseValue, List<HeaderField> headers)
    {
        if (!QpackPrimitives.TryDecodeInteger(ref reader, first, 3, out ulong index) ||
            !_table.TryGetByAbsolute(baseValue + index, out (string Name, string Value) e) ||
            !TryReadString(ref reader, 7, out string value))
            return QpackResult.DecompressionFailed;
        headers.Add(new HeaderField(e.Name, value));
        return QpackResult.Ok;
    }

    private static QpackResult DecodeLiteralLiteralName(ref BufferReader reader, byte first, List<HeaderField> headers)
    {
        if (!QpackPrimitives.TryDecodeString(ref reader, first, 3, out string name) ||
            !TryReadString(ref reader, 7, out string value))
            return QpackResult.DecompressionFailed;
        headers.Add(new HeaderField(name, value));
        return QpackResult.Ok;
    }

    // ---- Helpers ---------------------------------------------------------------------------

    private bool TryResolveName(bool isStatic, ulong nameIndex, out string name)
    {
        name = string.Empty;
        if (isStatic)
        {
            if (nameIndex >= (ulong)QpackStaticTable.Count)
                return false;
            name = QpackStaticTable.Get((int)nameIndex).Name;
            return true;
        }
        if (!TryRelativeToAbsolute(nameIndex, out ulong abs) || !_table.TryGetByAbsolute(abs, out (string Name, string Value) e))
            return false;
        name = e.Name;
        return true;
    }

    // Encoder stream: the relative index refers to the most recently inserted entry.
    private bool TryRelativeToAbsolute(ulong relativeIndex, out ulong absolute)
    {
        absolute = 0;
        if (_table.InsertCount < 1 + relativeIndex)
            return false;
        absolute = _table.InsertCount - 1 - relativeIndex;
        return true;
    }

    // Reconstruct the required insert count from the encoded value (RFC 9204 §4.5.1).
    private bool TryReconstructRequiredInsertCount(ulong encoded, out ulong requiredInsertCount)
    {
        requiredInsertCount = 0;
        if (encoded == 0)
            return true;

        ulong maxEntries = _table.MaxEntries;
        ulong fullRange = 2 * maxEntries;
        if (fullRange == 0 || encoded > fullRange)
            return false;

        ulong maxValue = _table.InsertCount + maxEntries;
        ulong maxWrapped = maxValue / fullRange * fullRange;
        requiredInsertCount = maxWrapped + encoded - 1;
        if (requiredInsertCount > maxValue)
        {
            if (requiredInsertCount <= fullRange)
                return false;
            requiredInsertCount -= fullRange;
        }
        return requiredInsertCount != 0;
    }

    private static bool TryReadString(ref BufferReader reader, int prefixBits, out string value)
    {
        value = string.Empty;
        return reader.TryReadByte(out byte first) && QpackPrimitives.TryDecodeString(ref reader, first, prefixBits, out value);
    }
}
