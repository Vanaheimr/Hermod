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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// The packet numbers a space has received and not yet pruned, as ascending, disjoint ranges that
/// never touch (RFC 9000 §13.2.4 prunes them from below).
/// </summary>
/// <remarks>
/// This used to be a set of single numbers, and every ACK frame was built by walking all of them.
/// Pruning releases them only once the peer has acknowledged an ACK of ours, a round trip later,
/// so the set held about one flight of the peer's packets. A server streaming to a client receives
/// an acknowledgment for every other packet it sends, and piggybacks an ACK on nearly every packet,
/// so each packet paid for a walk over the peer's whole flight. HTTP3ConformanceTests' demo host
/// spent ~10 % of its thread there at 32 responses in flight. As ranges, an in-order arrival
/// extends the newest range, and an ACK frame costs what it reports: a handful of ranges, at most
/// <c>maxRanges</c>.
/// </remarks>
internal sealed class ReceivedPacketNumbers
{
    private readonly List<PacketNumberRange> _ranges = []; // ascending

    /// <summary>
    /// How many packet numbers are held (not ranges).
    /// </summary>
    public ulong Count { get; private set; }

    /// <summary>
    /// Records <paramref name="packetNumber"/>; <c>false</c> if it was already held.
    /// </summary>
    public bool Add(ulong packetNumber)
    {
        int last = _ranges.Count - 1;

        // In order, the common case: past the newest range, or extending it.
        if (last < 0 || packetNumber > _ranges[last].Largest + 1)
        {
            _ranges.Add(new PacketNumberRange(Largest: packetNumber, Smallest: packetNumber));
            Count++;
            return true;
        }
        if (packetNumber == _ranges[last].Largest + 1)
        {
            _ranges[last] = _ranges[last] with { Largest = packetNumber };
            Count++;
            return true;
        }

        // Reordered: the first range that reaches up to it either holds it already or lies above it.
        int i = FirstReaching(packetNumber);
        if (packetNumber >= _ranges[i].Smallest)
            return false;

        bool joinsAbove = packetNumber + 1 == _ranges[i].Smallest;
        bool joinsBelow = i > 0 && _ranges[i - 1].Largest + 1 == packetNumber;
        if (joinsAbove && joinsBelow)
        {
            _ranges[i - 1] = _ranges[i - 1] with { Largest = _ranges[i].Largest };
            _ranges.RemoveAt(i);
        }
        else if (joinsAbove)
            _ranges[i] = _ranges[i] with { Smallest = packetNumber };
        else if (joinsBelow)
            _ranges[i - 1] = _ranges[i - 1] with { Largest = packetNumber };
        else
            _ranges.Insert(i, new PacketNumberRange(Largest: packetNumber, Smallest: packetNumber));
        Count++;
        return true;
    }

    public bool Contains(ulong packetNumber)
    {
        if (_ranges.Count == 0 || packetNumber > _ranges[^1].Largest)
            return false;
        return packetNumber >= _ranges[FirstReaching(packetNumber)].Smallest;
    }

    /// <summary>
    /// The smallest packet number at or above <paramref name="from"/> that is not held.
    /// </summary>
    public ulong SmallestMissingFrom(ulong from)
    {
        if (_ranges.Count == 0 || from > _ranges[^1].Largest)
            return from;
        PacketNumberRange range = _ranges[FirstReaching(from)];
        return from < range.Smallest ? from : range.Largest + 1; // ranges never touch, so the next one starts higher
    }

    /// <summary>
    /// Drops every packet number up to and including <paramref name="largest"/>; returns how many were held.
    /// </summary>
    public ulong PruneUpTo(ulong largest)
    {
        ulong removed = 0;
        int drop = 0;
        while (drop < _ranges.Count && _ranges[drop].Largest <= largest)
            removed += _ranges[drop++].Count;
        _ranges.RemoveRange(0, drop);

        if (_ranges.Count > 0 && _ranges[0].Smallest <= largest)
        {
            removed += largest - _ranges[0].Smallest + 1;
            _ranges[0] = _ranges[0] with { Smallest = largest + 1 };
        }
        Count -= removed;
        return removed;
    }

    /// <summary>
    /// The newest <paramref name="maxRanges"/> ranges, descending, as an ACK frame carries them
    /// (RFC 9000 §19.3). Dropping the oldest only leaves those packets unacknowledged for now.
    /// </summary>
    public List<PacketNumberRange> NewestDescending(int maxRanges)
    {
        var result = new List<PacketNumberRange>(Math.Min(maxRanges, _ranges.Count));
        for (int i = _ranges.Count - 1; i >= 0 && result.Count < maxRanges; i--)
            result.Add(_ranges[i]);
        return result;
    }

    // Index of the first range whose Largest is at least packetNumber; the caller guarantees one exists.
    private int FirstReaching(ulong packetNumber)
    {
        int lo = 0, hi = _ranges.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_ranges[mid].Largest < packetNumber)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
