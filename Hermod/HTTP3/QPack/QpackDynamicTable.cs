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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

/// <summary>
/// The dynamic QPACK table (RFC 9204 §3.2): a FIFO of header entries with a byte capacity.
/// Entries are inserted at the back and evicted at the front when the capacity would otherwise be
/// exceeded. Every entry receives a running <b>absolute index</b> (from 0); evicted indices are
/// invalid. Encoder and decoder each hold such a table and synchronise them via the QPACK
/// encoder stream.
/// </summary>
public sealed class QpackDynamicTable
{
    private readonly List<(string Name, string Value)> _entries = [];
    private readonly List<int> _refCounts = []; // per entry: open references from not-yet-acknowledged field sections (encoder side)

    /// <summary>
    /// Currently set capacity in bytes (via Set Dynamic Table Capacity, bounded by SETTINGS).
    /// </summary>
    public ulong Capacity { get; private set; }

    /// <summary>
    /// Current size of all held entries in bytes.
    /// </summary>
    public ulong Size { get; private set; }

    /// <summary>
    /// Total number of entries ever inserted – the next entry would receive this absolute index.
    /// </summary>
    public ulong InsertCount { get; private set; }

    /// <summary>
    /// Number of currently held entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Maximum theoretical entry count (RFC 9204 §3.2.2): <c>floor(capacity / 32)</c>.
    /// </summary>
    public ulong MaxEntries => Capacity / 32;

    private ulong OldestAbsolute => InsertCount - (ulong)_entries.Count;

    /// <summary>
    /// Entry size (RFC 9204 §3.2.1): name length + value length + 32.
    /// </summary>
    public static ulong EntrySize(string name, string value) => (ulong)(name.Length + value.Length + 32);

    /// <summary>
    /// Sets the capacity and evicts entries if needed (RFC 9204 §3.2.3).
    /// </summary>
    public void SetCapacity(ulong capacity)
    {
        Capacity = capacity;
        EvictToFit(0);
    }

    /// <summary>
    /// Inserts an entry; evicts the oldest <b>unreferenced</b> entries if needed.
    /// <c>false</c> when it exceeds the capacity itself or room could only be made by evicting a
    /// still-referenced entry (eviction protection, RFC 9204 §2.1.1).
    /// </summary>
    public bool Insert(string name, string value)
    {
        ulong size = EntrySize(name, value);
        if (size > Capacity || !EvictToFit(size))
            return false;
        _entries.Add((name, value));
        _refCounts.Add(0);
        Size += size;
        InsertCount++;
        return true;
    }

    /// <summary>
    /// Evicts the oldest unreferenced entries; <c>false</c> when the needed room is blocked.
    /// </summary>
    private bool EvictToFit(ulong incoming)
    {
        while (Size + incoming > Capacity)
        {
            if (_entries.Count == 0)
                return incoming <= Capacity; // empty: fits exactly when incoming ≤ capacity
            if (_refCounts[0] > 0)
                return false;                // the oldest entry is referenced ⇒ not evictable
            (string n, string v) = _entries[0];
            _entries.RemoveAt(0);
            _refCounts.RemoveAt(0);
            Size -= EntrySize(n, v);
        }
        return true;
    }

    /// <summary>
    /// Records a reference to an entry (encoder: protects it until the section acknowledgment).
    /// </summary>
    public void AddReference(ulong absoluteIndex)
    {
        if (absoluteIndex < OldestAbsolute || absoluteIndex >= InsertCount)
            return;
        _refCounts[(int)(absoluteIndex - OldestAbsolute)]++;
    }

    /// <summary>
    /// Releases a reference (encoder: after section acknowledgment/stream cancellation).
    /// </summary>
    public void RemoveReference(ulong absoluteIndex)
    {
        if (absoluteIndex < OldestAbsolute || absoluteIndex >= InsertCount)
            return;
        int i = (int)(absoluteIndex - OldestAbsolute);
        if (_refCounts[i] > 0)
            _refCounts[i]--;
    }

    /// <summary>
    /// Returns the entry with the absolute index, if it is (still) present.
    /// </summary>
    public bool TryGetByAbsolute(ulong absoluteIndex, out (string Name, string Value) entry)
    {
        entry = default;
        if (absoluteIndex < OldestAbsolute || absoluteIndex >= InsertCount)
            return false;
        entry = _entries[(int)(absoluteIndex - OldestAbsolute)];
        return true;
    }

    /// <summary>
    /// Highest absolute index of an exact (name, value) pair, if present.
    /// </summary>
    public bool TryFindExact(string name, string value, out ulong absoluteIndex)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].Name == name && _entries[i].Value == value)
            {
                absoluteIndex = OldestAbsolute + (ulong)i;
                return true;
            }
        absoluteIndex = 0;
        return false;
    }

    /// <summary>
    /// Highest absolute index of an entry with a matching name (any value), if present.
    /// </summary>
    public bool TryFindName(string name, out ulong absoluteIndex)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].Name == name)
            {
                absoluteIndex = OldestAbsolute + (ulong)i;
                return true;
            }
        absoluteIndex = 0;
        return false;
    }

    /// <summary>
    /// Whether an entry of this size could still be inserted (possibly after evicting up to the oldest unused one).
    /// </summary>
    public bool CanInsert(string name, string value) => EntrySize(name, value) <= Capacity;
}
