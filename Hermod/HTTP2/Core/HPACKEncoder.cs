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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Text;


    /// <summary>
    /// HPACK encoder (RFC 7541). Now a full-featured mirror of
    /// <see cref="HPACKDecoder"/>:
    /// - the complete 61-entry static table (shared with the decoder) for exact
    ///   and name-only indexed references;
    /// - a per-connection dynamic table, kept byte-for-byte in step with the peer
    ///   decoder's, so a repeated header field is sent as a one-byte index;
    /// - Huffman coding of string literals whenever it is shorter than raw.
    ///
    /// Because the dynamic table is stateful, one encoder instance belongs to one
    /// connection, and <see cref="EncodeHeaderBlock"/> calls MUST happen in the
    /// same order the resulting blocks are written to the wire (the decoder
    /// replays that order) — callers serialize encode+write accordingly.
    /// </summary>
    public sealed class HPACKEncoder
    {

        // Reverse lookups over the shared static table (RFC 7541, Appendix A).
        private static readonly Dictionary<(string Name, string Value), int> StaticExact;
        private static readonly Dictionary<string, int>                      StaticName;

        static HPACKEncoder()
        {

            var table = HPACKDecoder.StaticTable;
            StaticExact = new(table.Length);
            StaticName  = new(table.Length);

            for (var i = 1; i < table.Length; i++)
            {
                var (name, value) = table[i];
                if (value is not null)
                    StaticExact.TryAdd((name, value), i);
                StaticName.TryAdd(name, i);   // keep the lowest index for a name
            }

        }


        #region Dynamic table (encoder side — mirrors the peer decoder)

        private readonly List<(string Name, string Value)> dynamicTable = [];
        private int  dynamicTableSize;
        private int  maxDynamicTableSize = 4096;
        private int? pendingSizeUpdate;                 // signal at the next block start

        /// <summary>
        /// Field names we deliberately never insert into the dynamic table:
        /// values that change nearly every message (indexing them just churns the
        /// table and evicts useful entries). They are sent as "literal without
        /// indexing".
        /// </summary>
        private static readonly HashSet<string> NoIndexNames = new(StringComparer.Ordinal)
        {
            ":path", "age", "content-length", "content-range", "date", "etag",
            "expires", "last-modified", "if-modified-since", "if-none-match",
            "if-range", "if-unmodified-since", "location", "retry-after"
        };

        /// <summary>
        /// Sensitive field names sent as "literal never indexed" (RFC 7541,
        /// Section 7.1.3) — kept out of the dynamic table so their values are never
        /// exposed to a compression-based side channel.
        /// </summary>
        private static readonly HashSet<string> NeverIndexNames = new(StringComparer.Ordinal)
        {
            "authorization", "proxy-authorization", "cookie", "set-cookie"
        };

        /// <summary>
        /// Adjust the encoder's dynamic table size limit to the value the peer
        /// advertised in SETTINGS_HEADER_TABLE_SIZE. A reduction evicts entries and
        /// queues a dynamic table size update to be emitted at the start of the next
        /// header block (RFC 7541, Section 6.3), so our table never exceeds what the
        /// peer's decoder will keep.
        /// </summary>
        public void SetMaxDynamicTableSize(int NewMax)
        {
            NewMax = Math.Max(0, NewMax);
            if (NewMax == maxDynamicTableSize)
                return;

            maxDynamicTableSize = NewMax;

            while (dynamicTableSize > maxDynamicTableSize && dynamicTable.Count > 0)
                EvictOldest();

            pendingSizeUpdate = NewMax;
        }

        private void EvictOldest()
        {
            var last = dynamicTable[^1];
            dynamicTableSize -= (last.Name.Length + last.Value.Length + 32);
            dynamicTable.RemoveAt(dynamicTable.Count - 1);
        }

        // Mirrors HPACKDecoder.AddToDynamicTable exactly (same 32-byte per-entry
        // overhead, same eviction) so both tables stay in lockstep.
        private void AddToDynamicTable(string Name, string Value)
        {

            var entrySize = Name.Length + Value.Length + 32;

            while (dynamicTableSize + entrySize > maxDynamicTableSize && dynamicTable.Count > 0)
                EvictOldest();

            if (entrySize <= maxDynamicTableSize)
            {
                dynamicTable.Insert(0, (Name, Value));
                dynamicTableSize += entrySize;
            }

        }

        // Position (0 = newest) of an exact (name,value) entry, or -1.
        private int FindDynamicExact(string Name, string Value)
        {
            for (var p = 0; p < dynamicTable.Count; p++)
                if (dynamicTable[p].Name == Name && dynamicTable[p].Value == Value)
                    return p;
            return -1;
        }

        // Position (0 = newest) of the newest entry with this name, or -1.
        private int FindDynamicName(string Name)
        {
            for (var p = 0; p < dynamicTable.Count; p++)
                if (dynamicTable[p].Name == Name)
                    return p;
            return -1;
        }

        #endregion


        /// <summary>
        /// Encode a list of headers into an HPACK header block.
        /// </summary>
        public byte[] EncodeHeaderBlock(IEnumerable<(string Name, string Value)> Headers)
        {

            using var ms = new MemoryStream(256);

            // A pending dynamic table size update MUST come first (RFC 7541,
            // Section 4.2): 001 prefix, 5-bit integer.
            if (pendingSizeUpdate is { } newSize)
            {
                EncodeInteger(ms, newSize, 5, 0x20);
                pendingSizeUpdate = null;
            }

            foreach (var (name, value) in Headers)
            {

                // 1. Exact match in the static table -> Indexed Header Field (6.1).
                if (StaticExact.TryGetValue((name, value), out var staticIdx))
                {
                    EncodeInteger(ms, staticIdx, 7, 0x80);
                    continue;
                }

                // 2. Exact match in the dynamic table -> Indexed Header Field (6.1).
                var dynExact = FindDynamicExact(name, value);
                if (dynExact >= 0)
                {
                    EncodeInteger(ms, HPACKDecoder.StaticTable.Length + dynExact, 7, 0x80);
                    continue;
                }

                // Otherwise a literal — resolve a name index (static preferred, as it
                // never shifts; else the newest dynamic entry with the same name).
                int nameIndex;
                if (StaticName.TryGetValue(name, out var sn))
                    nameIndex = sn;
                else
                {
                    var dn = FindDynamicName(name);
                    nameIndex = dn >= 0 ? HPACKDecoder.StaticTable.Length + dn : 0;
                }

                if (NeverIndexNames.Contains(name))
                {
                    // Literal Never Indexed (6.2.3): 0001 prefix, 4-bit name index.
                    EncodeInteger(ms, nameIndex, 4, 0x10);
                    if (nameIndex == 0) EncodeString(ms, name);
                    EncodeString(ms, value);
                }
                else if (NoIndexNames.Contains(name))
                {
                    // Literal without Indexing (6.2.2): 0000 prefix, 4-bit name index.
                    EncodeInteger(ms, nameIndex, 4, 0x00);
                    if (nameIndex == 0) EncodeString(ms, name);
                    EncodeString(ms, value);
                }
                else
                {
                    // Literal with Incremental Indexing (6.2.1): 01 prefix, 6-bit.
                    EncodeInteger(ms, nameIndex, 6, 0x40);
                    if (nameIndex == 0) EncodeString(ms, name);
                    EncodeString(ms, value);
                    AddToDynamicTable(name, value);
                }

            }

            return ms.ToArray();

        }


        #region Integer Encoding (RFC 7541, Section 5.1)

        private static void EncodeInteger(Stream Output, int Value, int PrefixBits, byte Prefix)
        {

            var maxPrefix = (1 << PrefixBits) - 1;

            if (Value < maxPrefix)
            {
                Output.WriteByte((byte) (Prefix | Value));
                return;
            }

            Output.WriteByte((byte) (Prefix | maxPrefix));
            Value -= maxPrefix;

            while (Value >= 128)
            {
                Output.WriteByte((byte) (0x80 | (Value & 0x7F)));
                Value >>= 7;
            }

            Output.WriteByte((byte) Value);

        }

        #endregion


        #region String Encoding (RFC 7541, Section 5.2)

        /// <summary>
        /// Encode a string literal, choosing Huffman coding when it is strictly
        /// shorter than the raw octets (RFC 7541, Section 5.2 — the length octet's
        /// high bit flags which form was used).
        /// </summary>
        private static void EncodeString(Stream Output, string Value)
        {

            var raw     = Encoding.ASCII.GetBytes(Value);
            var huffLen = HuffmanEncoder.EncodedByteLength(raw);

            if (huffLen < raw.Length)
            {
                var huffman = HuffmanEncoder.Encode(raw);
                EncodeInteger(Output, huffman.Length, 7, 0x80);   // H bit set
                Output.Write(huffman, 0, huffman.Length);
            }
            else
            {
                EncodeInteger(Output, raw.Length, 7, 0x00);       // raw octets
                Output.Write(raw, 0, raw.Length);
            }

        }

        #endregion

    }

}
