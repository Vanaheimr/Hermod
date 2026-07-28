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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

/// <summary>
/// A contiguous, acknowledged packet-number range [Smallest, Largest] (inclusive).
/// </summary>
public readonly record struct PacketNumberRange(ulong Largest, ulong Smallest)
{
    public ulong Count => Largest - Smallest + 1;
}

/// <summary>
/// The three ECN counters of a type-0x03 ACK frame (RFC 9000 §19.3.2).
/// </summary>
public readonly record struct EcnCounts(ulong Ect0, ulong Ect1, ulong CongestionExperienced);

/// <summary>
/// ACK frame (type 0x02/0x03, RFC 9000 §19.3). Acknowledges received packets as descending-sorted,
/// non-overlapping ranges. The wire encoding uses relative gap/length values; this model instead
/// keeps the absolute ranges and converts when writing/reading.
/// </summary>
public sealed record AckFrame(
    IReadOnlyList<PacketNumberRange> Ranges,
    ulong AckDelay,
    EcnCounts? Ecn = null) : Frame
{
    /// <summary>
    /// Largest acknowledged packet number (from the first, highest range).
    /// </summary>
    public ulong LargestAcknowledged => Ranges[0].Largest;

    /// <summary>
    /// Whether <paramref name="packetNumber"/> falls into one of the acknowledged ranges.
    /// </summary>
    public bool Covers(ulong packetNumber)
    {
        foreach (PacketNumberRange range in Ranges)
            if (packetNumber >= range.Smallest && packetNumber <= range.Largest)
                return true;
        return false;
    }

    /// <summary>
    /// Builds an ACK frame from a set of received packet numbers by merging consecutive numbers
    /// into ranges (sorted descending).
    /// </summary>
    public static AckFrame FromPacketNumbers(IEnumerable<ulong> packetNumbers, ulong ackDelay = 0)
        => FromAscendingPacketNumbers([.. packetNumbers.Distinct().Order()], ackDelay);

    /// <summary>
    /// Like <see cref="FromPacketNumbers"/>, but for an already ascending, duplicate-free sequence
    /// (e.g. a <see cref="SortedSet{T}"/>): walks it exactly once and therefore avoids the
    /// sort plus intermediate copy of the whole set on every ACK.
    /// </summary>
    /// <param name="maxRanges">
    /// Optional upper bound on the number of ranges (RFC 9000 §13.2.4), so the frame stays within a
    /// packet. Since the ranges are descending, the NEWEST are kept and the oldest dropped.
    /// </param>
    public static AckFrame FromAscendingPacketNumbers(IReadOnlyCollection<ulong> packetNumbers, ulong ackDelay = 0,
                                                      int maxRanges = int.MaxValue)
    {
        if (packetNumbers.Count == 0)
            throw new ArgumentException("At least one packet number is required.", nameof(packetNumbers));

        var ranges = new List<PacketNumberRange>();
        ulong smallest = 0, largest = 0;
        bool open = false;

        foreach (ulong pn in packetNumbers)
        {
            if (open && pn == largest + 1)
            {
                largest = pn; // extend the current run upwards
                continue;
            }
            if (open)
                ranges.Add(new PacketNumberRange(largest, smallest));
            smallest = largest = pn;
            open = true;
        }
        ranges.Add(new PacketNumberRange(largest, smallest));

        ranges.Reverse(); // the wire format expects descending ranges (RFC 9000 §19.3)
        if (ranges.Count > maxRanges)
            ranges.RemoveRange(maxRanges, ranges.Count - maxRanges); // keep the newest
        return new AckFrame(ranges, ackDelay);
    }

    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(Ecn is null ? FrameType.AckNoEcn : FrameType.AckEcn);
        writer.WriteVarInt(Ranges[0].Largest);
        writer.WriteVarInt(AckDelay);
        writer.WriteVarInt((ulong)(Ranges.Count - 1));                 // ACK Range Count
        writer.WriteVarInt(Ranges[0].Largest - Ranges[0].Smallest);    // First ACK Range

        for (int i = 1; i < Ranges.Count; i++)
        {
            // largest_i = previous_smallest - gap - 2  =>  gap = previous_smallest - largest_i - 2
            ulong gap = Ranges[i - 1].Smallest - Ranges[i].Largest - 2;
            writer.WriteVarInt(gap);
            writer.WriteVarInt(Ranges[i].Largest - Ranges[i].Smallest); // ACK Range Length
        }

        if (Ecn is { } ecn)
        {
            writer.WriteVarInt(ecn.Ect0);
            writer.WriteVarInt(ecn.Ect1);
            writer.WriteVarInt(ecn.CongestionExperienced);
        }
    }

    /// <summary>
    /// Parses the frame body. <paramref name="hasEcn"/> follows from the type (0x03).
    /// </summary>
    public static bool TryReadBody(ref BufferReader reader, bool hasEcn, out AckFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong largest) ||
            !reader.TryReadVarInt(out ulong ackDelay) ||
            !reader.TryReadVarInt(out ulong rangeCount) ||
            !reader.TryReadVarInt(out ulong firstAckRange) ||
            firstAckRange > largest)
            return false;

        var ranges = new List<PacketNumberRange>((int)Math.Min(rangeCount + 1, 1024));
        ulong smallest = largest - firstAckRange;
        ranges.Add(new PacketNumberRange(largest, smallest));

        for (ulong i = 0; i < rangeCount; i++)
        {
            if (!reader.TryReadVarInt(out ulong gap) || !reader.TryReadVarInt(out ulong ackRangeLength))
                return false;

            // largest_next = previous_smallest - gap - 2; every computed number must stay >= 0.
            if (smallest < gap + 2)
                return false; // FRAME_ENCODING_ERROR: negative packet-number value
            ulong nextLargest = smallest - gap - 2;
            if (ackRangeLength > nextLargest)
                return false;
            ulong nextSmallest = nextLargest - ackRangeLength;

            ranges.Add(new PacketNumberRange(nextLargest, nextSmallest));
            smallest = nextSmallest;
        }

        EcnCounts? ecn = null;
        if (hasEcn)
        {
            if (!reader.TryReadVarInt(out ulong ect0) ||
                !reader.TryReadVarInt(out ulong ect1) ||
                !reader.TryReadVarInt(out ulong ce))
                return false;
            ecn = new EcnCounts(ect0, ect1, ce);
        }

        frame = new AckFrame(ranges, ackDelay, ecn);
        return true;
    }
}
