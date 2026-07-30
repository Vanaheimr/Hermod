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
/// QPACK encoder with a dynamic table (RFC 9204). Stateful: holds the dynamic table and, when
/// encoding a header list, produces both the <b>encoder-stream instructions</b> (inserts) and the
/// <b>field section</b>. The instructions must reach the decoder before the field section.
/// <para>Simplifications: base = required insert count (all references pre-base, no post-base);
/// insert instructions use static name references or literal names (no dynamic name refs,
/// no duplicates). The decoder nevertheless masters the complete encoding (incl. RFC vectors).</para>
/// </summary>
public sealed class QpackDynamicEncoder
{
    private enum RepKind { StaticIndexed, DynamicIndexed, LiteralStaticName, LiteralLiteralName }
    private readonly record struct Rep(RepKind Kind, ulong Index, string Name, string Value);

    private readonly QpackDynamicTable _table = new();
    private readonly Dictionary<ulong, List<ulong>> _outstandingSections = []; // stream ID → referenced absolute indices
    private ulong _knownReceivedCount; // insert count confirmed by the decoder (RFC 9204 §2.1.4)

    /// <summary>
    /// The encoder's dynamic table (diagnostics/test).
    /// </summary>
    public QpackDynamicTable Table => _table;

    /// <summary>
    /// Insert count confirmed by the decoder (known received count). Diagnostics.
    /// </summary>
    public ulong KnownReceivedCount => _knownReceivedCount;

    /// <summary>
    /// Sets the dynamic table capacity and returns the corresponding encoder-stream instruction.
    /// </summary>
    public byte[] SetCapacity(ulong capacity)
    {
        _table.SetCapacity(capacity);
        var w = new BufferWriter(4);
        try
        {
            // Set Dynamic Table Capacity: 0 0 1 Capacity(5+)
            QpackPrimitives.EncodeInteger(ref w, capacity, 5, 0b0010_0000);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Encodes a header list without section tracking (stream ID 0). For simple round-trips/tests.
    /// </summary>
    public (byte[] Instructions, byte[] FieldSection) Encode(IReadOnlyList<HeaderField> headers)
        => Encode(0, headers);

    /// <summary>
    /// Encodes a header list for the stream <paramref name="streamId"/>. Returns (encoder-stream
    /// instructions, field section). Referenced dynamic entries are pinned (against eviction) until
    /// a section acknowledgment for this stream arrives.
    /// </summary>
    public (byte[] Instructions, byte[] FieldSection) Encode(ulong streamId, IReadOnlyList<HeaderField> headers)
    {
        ReleaseSection(streamId); // release any old references of this stream

        var instructions = new BufferWriter(64);
        var reps = new List<Rep>(headers.Count);
        var referenced = new List<ulong>();
        long maxReferencedAbsolute = -1;

        try
        {
            foreach (HeaderField header in headers)
            {
                string name = header.Name.ToLowerInvariant();
                string value = header.Value;

                if (QpackStaticTable.TryGetPairIndex(name, value, out int staticPair))
                {
                    reps.Add(new Rep(RepKind.StaticIndexed, (ulong)staticPair, name, value));
                }
                else if (_table.TryFindExact(name, value, out ulong exactAbs))
                {
                    reps.Add(new Rep(RepKind.DynamicIndexed, exactAbs, name, value));
                    referenced.Add(exactAbs);
                    maxReferencedAbsolute = Math.Max(maxReferencedAbsolute, (long)exactAbs);
                }
                else if (_table.Capacity > 0 && _table.CanInsert(name, value) && _table.Insert(name, value))
                {
                    EmitInsert(ref instructions, name, value);
                    ulong abs = _table.InsertCount - 1;
                    reps.Add(new Rep(RepKind.DynamicIndexed, abs, name, value));
                    referenced.Add(abs);
                    maxReferencedAbsolute = Math.Max(maxReferencedAbsolute, (long)abs);
                }
                else if (QpackStaticTable.TryGetNameIndex(name, out int staticName))
                {
                    reps.Add(new Rep(RepKind.LiteralStaticName, (ulong)staticName, name, value));
                }
                else
                {
                    reps.Add(new Rep(RepKind.LiteralLiteralName, 0, name, value));
                }
            }

            ulong requiredInsertCount = maxReferencedAbsolute >= 0 ? (ulong)maxReferencedAbsolute + 1 : 0;
            ulong baseValue = requiredInsertCount; // base = RIC ⇒ all dynamic references are pre-base

            // Pin the referenced entries until the section acknowledgment for this stream arrives.
            if (referenced.Count > 0)
            {
                foreach (ulong abs in referenced)
                    _table.AddReference(abs);
                _outstandingSections[streamId] = referenced;
            }

            var section = new BufferWriter(128);
            try
            {
                WritePrefix(ref section, requiredInsertCount);
                foreach (Rep rep in reps)
                    WriteRepresentation(ref section, rep, baseValue);
                return (instructions.WrittenSpan.ToArray(), section.WrittenSpan.ToArray());
            }
            finally { section.Dispose(); }
        }
        finally { instructions.Dispose(); }
    }

    /// <summary>
    /// Processes the peer's decoder-stream instructions (RFC 9204 §4.4) and returns the number of
    /// consumed bytes (a truncated instruction at the end is left in place).
    /// </summary>
    public int ProcessDecoderInstructions(ReadOnlySpan<byte> data)
    {
        var reader = new BufferReader(data);
        int consumed = 0;
        while (!reader.IsEmpty)
        {
            if (!reader.TryReadByte(out byte first))
                break;

            bool ok;
            if ((first & 0x80) != 0) // Section Acknowledgment: 1 StreamID(7+)
            {
                ok = QpackPrimitives.TryDecodeInteger(ref reader, first, 7, out ulong streamId);
                if (ok)
                {
                    ReleaseSection(streamId);
                    _knownReceivedCount = _table.InsertCount; // acknowledged section ⇒ all previous inserts received
                }
            }
            else if ((first & 0x40) != 0) // Stream Cancellation: 0 1 StreamID(6+)
            {
                ok = QpackPrimitives.TryDecodeInteger(ref reader, first, 6, out ulong streamId);
                if (ok)
                    ReleaseSection(streamId);
            }
            else // Insert Count Increment: 0 0 Increment(6+)
            {
                ok = QpackPrimitives.TryDecodeInteger(ref reader, first, 6, out ulong increment);
                if (ok)
                    _knownReceivedCount += increment;
            }

            if (!ok)
                break; // truncated – wait for more data
            consumed = reader.Position;
        }
        return consumed;
    }

    private void ReleaseSection(ulong streamId)
    {
        if (!_outstandingSections.Remove(streamId, out List<ulong>? refs))
            return;
        foreach (ulong abs in refs)
            _table.RemoveReference(abs);
    }

    private void EmitInsert(ref BufferWriter w, string name, string value)
    {
        if (QpackStaticTable.TryGetNameIndex(name, out int staticName))
        {
            // Insert with Name Reference: 1 T=1 NameIndex(6+) + Value
            QpackPrimitives.EncodeInteger(ref w, (ulong)staticName, 6, 0b1100_0000);
            QpackPrimitives.EncodeString(ref w, value, 7, 0x00);
        }
        else
        {
            // Insert with Literal Name: 0 1 H NameLen(5+) + Name + Value
            QpackPrimitives.EncodeString(ref w, name, 5, 0b0100_0000);
            QpackPrimitives.EncodeString(ref w, value, 7, 0x00);
        }
    }

    private void WritePrefix(ref BufferWriter w, ulong requiredInsertCount)
    {
        ulong encodedInsertCount = 0;
        if (requiredInsertCount != 0)
        {
            ulong fullRange = 2 * _table.MaxEntries; // MaxEntries ≥ 1 when dynamic entries exist
            encodedInsertCount = requiredInsertCount % fullRange + 1;
        }
        QpackPrimitives.EncodeInteger(ref w, encodedInsertCount, 8, 0x00);
        // Base = RIC ⇒ sign = 0, delta base = 0.
        QpackPrimitives.EncodeInteger(ref w, 0, 7, 0x00);
    }

    private void WriteRepresentation(ref BufferWriter w, Rep rep, ulong baseValue)
    {
        switch (rep.Kind)
        {
            case RepKind.StaticIndexed: // 1 T=1 Index(6+)
                QpackPrimitives.EncodeInteger(ref w, rep.Index, 6, 0b1100_0000);
                break;

            case RepKind.DynamicIndexed: // 1 T=0 RelIndex(6+), RelIndex = Base - 1 - Abs
                QpackPrimitives.EncodeInteger(ref w, baseValue - 1 - rep.Index, 6, 0b1000_0000);
                break;

            case RepKind.LiteralStaticName: // 0 1 N=0 T=1 NameIndex(4+) + Value
                QpackPrimitives.EncodeInteger(ref w, rep.Index, 4, 0b0101_0000);
                QpackPrimitives.EncodeString(ref w, rep.Value, 7, 0x00);
                break;

            default: // LiteralLiteralName: 0 0 1 N=0 H NameLen(3+) + Name + Value
                QpackPrimitives.EncodeString(ref w, rep.Name, 3, 0b0010_0000);
                QpackPrimitives.EncodeString(ref w, rep.Value, 7, 0x00);
                break;
        }
    }
}
