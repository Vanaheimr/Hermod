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
    /// HPACK header compression as defined in RFC 7541.
    /// Implements the static table, dynamic table, integer encoding/decoding,
    /// and the indexed/literal header field representations.
    /// 
    /// Note: Huffman coding is supported for decoding (required for interop)
    /// but we use raw string encoding for outgoing headers (simpler, always valid).
    /// </summary>
    public sealed class HPACKDecoder
    {

        #region Static Table (RFC 7541, Appendix A)

        // Internal so the HPACKEncoder can build its reverse index from the exact
        // same 61-entry table (RFC 7541, Appendix A) rather than duplicating it.
        internal static readonly (string Name, string? Value)[] StaticTable =
        [
            ("",                          null),              //  0 — unused (1-indexed)
            (":authority",                null),              //  1
            (":method",                   "GET"),             //  2
            (":method",                   "POST"),            //  3
            (":path",                     "/"),               //  4
            (":path",                     "/index.html"),     //  5
            (":scheme",                   "http"),            //  6
            (":scheme",                   "https"),           //  7
            (":status",                   "200"),             //  8
            (":status",                   "204"),             //  9
            (":status",                   "206"),             // 10
            (":status",                   "304"),             // 11
            (":status",                   "400"),             // 12
            (":status",                   "404"),             // 13
            (":status",                   "500"),             // 14
            ("accept-charset",            null),              // 15
            ("accept-encoding",           "gzip, deflate"),   // 16
            ("accept-language",           null),              // 17
            ("accept-ranges",             null),              // 18
            ("accept",                    null),              // 19
            ("access-control-allow-origin", null),            // 20
            ("age",                       null),              // 21
            ("allow",                     null),              // 22
            ("authorization",             null),              // 23
            ("cache-control",             null),              // 24
            ("content-disposition",       null),              // 25
            ("content-encoding",          null),              // 26
            ("content-language",          null),              // 27
            ("content-length",            null),              // 28
            ("content-location",          null),              // 29
            ("content-range",             null),              // 30
            ("content-type",              null),              // 31
            ("cookie",                    null),              // 32
            ("date",                      null),              // 33
            ("etag",                      null),              // 34
            ("expect",                    null),              // 35
            ("expires",                   null),              // 36
            ("from",                      null),              // 37
            ("host",                      null),              // 38
            ("if-match",                  null),              // 39
            ("if-modified-since",         null),              // 40
            ("if-none-match",             null),              // 41
            ("if-range",                  null),              // 42
            ("if-unmodified-since",       null),              // 43
            ("last-modified",             null),              // 44
            ("link",                      null),              // 45
            ("location",                  null),              // 46
            ("max-forwards",             null),               // 47
            ("proxy-authenticate",        null),              // 48
            ("proxy-authorization",       null),              // 49
            ("range",                     null),              // 50
            ("referer",                   null),              // 51
            ("refresh",                   null),              // 52
            ("retry-after",               null),              // 53
            ("server",                    null),              // 54
            ("set-cookie",                null),              // 55
            ("strict-transport-security", null),              // 56
            ("transfer-encoding",         null),              // 57
            ("user-agent",                null),              // 58
            ("vary",                      null),              // 59
            ("via",                       null),              // 60
            ("www-authenticate",          null)               // 61
        ];

        #endregion


        #region Dynamic Table

        /// <summary>
        /// The dynamic table is a FIFO list of (name, value) pairs.
        /// New entries are prepended; eviction happens from the end.
        /// Per RFC 7541 Section 4.1, each entry occupies name.Length + value.Length + 32 bytes.
        /// </summary>
        private readonly List<(string Name, string Value)> dynamicTable = [];
        private int                                        dynamicTableSize;
        private int                                        maxDynamicTableSize = 4096;

        /// <summary>
        /// The upper bound a dynamic table size update may set — the
        /// SETTINGS_HEADER_TABLE_SIZE value we advertise to the peer (RFC 7541,
        /// Section 6.3 / RFC 9113, Section 6.5.2). A size update exceeding this is a
        /// decoding error. Defaults to the HPACK default of 4096; a connection that
        /// advertises a different value should set this to match.
        /// </summary>
        public int HeaderTableSizeLimit { get; set; } = 4096;

        private void AddToDynamicTable(string Name, string Value)
        {

            var entrySize = Name.Length + Value.Length + 32;

            // Evict from the end until we have room
            while (dynamicTableSize + entrySize > maxDynamicTableSize && dynamicTable.Count > 0)
            {
                var last = dynamicTable[^1];
                dynamicTableSize -= (last.Name.Length + last.Value.Length + 32);
                dynamicTable.RemoveAt(dynamicTable.Count - 1);
            }

            // If the entry itself is larger than the max table size, just clear everything
            if (entrySize <= maxDynamicTableSize)
            {
                dynamicTable.Insert(0, (Name, Value));
                dynamicTableSize += entrySize;
            }

        }

        /// <summary>
        /// Lookup by combined index: 1..61 = static table, 62+ = dynamic table.
        /// </summary>
        private (string Name, string? Value) LookupIndex(int Index)
        {

            if (Index < 1)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                   $"HPACK index 0 is invalid");

            if (Index < StaticTable.Length)
                return StaticTable[Index];

            var dynamicIndex = Index - StaticTable.Length;

            if (dynamicIndex >= dynamicTable.Count)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                   $"HPACK dynamic table index {Index} out of range (dynamic table has {dynamicTable.Count} entries)");

            var entry = dynamicTable[dynamicIndex];

            return (entry.Name, entry.Value);

        }

        #endregion


        #region Integer Decoding (RFC 7541, Section 5.1)

        /// <summary>
        /// Decode an HPACK integer with the given prefix size (in bits).
        /// Returns the decoded value and advances the offset.
        /// </summary>
        private static int DecodeInteger(ReadOnlySpan<byte> Data, ref int Offset, int PrefixBits)
        {

            // A truncated header block (the prefix byte is missing entirely) is a
            // compression error, not an unhandled IndexOutOfRangeException.
            if (Offset >= Data.Length)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                   "HPACK truncated integer");

            var maxPrefix  = (1 << PrefixBits) - 1;
            var value      = Data[Offset] & maxPrefix;
            Offset++;

            if (value < maxPrefix)
                return value;

            // Multi-byte encoding
            int m = 0;

            while (Offset < Data.Length)
            {

                var b = Data[Offset++];
                value += (b & 0x7F) << m;
                m     += 7;

                if ((b & 0x80) == 0)
                    break;

                if (m > 28)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                       "HPACK integer overflow");

            }

            return value;

        }

        #endregion


        #region String Decoding (RFC 7541, Section 5.2)

        /// <summary>
        /// Decode an HPACK string literal. The high bit of the first byte indicates
        /// whether Huffman coding is used (1) or raw octets (0).
        /// </summary>
        private static string DecodeString(ReadOnlySpan<byte> Data, ref int Offset)
        {

            // The length prefix byte carries the Huffman flag; if the block ends
            // exactly here (a header field whose value literal is truncated away),
            // that's a compression error rather than an IndexOutOfRangeException.
            if (Offset >= Data.Length)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                   "HPACK truncated string literal");

            var huffman = (Data[Offset] & 0x80) != 0;
            var length  = DecodeInteger(Data, ref Offset, 7);

            if (Offset + length > Data.Length)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                   "HPACK string length exceeds block size");

            var raw = Data.Slice(Offset, length);
            Offset += length;

            if (huffman)
                return HuffmanDecoder.Decode(raw);

            return Encoding.ASCII.GetString(raw);

        }

        #endregion


        #region Header Block Decoding (RFC 7541, Section 6)

        /// <summary>
        /// Decode a complete HPACK header block into a list of (name, value) header pairs.
        /// </summary>
        public List<(string Name, string Value)> DecodeHeaderBlock(ReadOnlySpan<byte> Block)
        {

            var headers = new List<(string Name, string Value)>();
            var offset  = 0;

            // RFC 7541, Section 4.2: a dynamic table size update MUST occur at the
            // beginning of a header block, before any header field representation.
            // Once we've emitted a field, a later size update is a decoding error.
            var sawHeaderField = false;

            while (offset < Block.Length)
            {

                var b = Block[offset];

                if ((b & 0x80) != 0)
                {
                    // 6.1 Indexed Header Field Representation
                    var index = DecodeInteger(Block, ref offset, 7);
                    var (name, value) = LookupIndex(index);
                    headers.Add((name, value ?? ""));
                    sawHeaderField = true;
                }
                else if ((b & 0xC0) == 0x40)
                {
                    // 6.2.1 Literal Header Field with Incremental Indexing
                    var index = DecodeInteger(Block, ref offset, 6);
                    string name;

                    if (index > 0)
                    {
                        (name, _) = LookupIndex(index);
                    }
                    else
                    {
                        name = DecodeString(Block, ref offset);
                    }

                    var value = DecodeString(Block, ref offset);
                    AddToDynamicTable(name, value);
                    headers.Add((name, value));
                    sawHeaderField = true;
                }
                else if ((b & 0xF0) == 0x00)
                {
                    // 6.2.2 Literal Header Field without Indexing
                    var index = DecodeInteger(Block, ref offset, 4);
                    string name;

                    if (index > 0)
                    {
                        (name, _) = LookupIndex(index);
                    }
                    else
                    {
                        name = DecodeString(Block, ref offset);
                    }

                    var value = DecodeString(Block, ref offset);
                    headers.Add((name, value));
                    sawHeaderField = true;
                }
                else if ((b & 0xF0) == 0x10)
                {
                    // 6.2.3 Literal Header Field Never Indexed
                    var index = DecodeInteger(Block, ref offset, 4);
                    string name;

                    if (index > 0)
                    {
                        (name, _) = LookupIndex(index);
                    }
                    else
                    {
                        name = DecodeString(Block, ref offset);
                    }

                    var value = DecodeString(Block, ref offset);
                    headers.Add((name, value));
                    sawHeaderField = true;
                }
                else if ((b & 0xE0) == 0x20)
                {
                    // 6.3 Dynamic Table Size Update

                    // Section 4.2: it MUST appear before any header field.
                    if (sawHeaderField)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                            "Dynamic table size update must occur at the start of a header block");

                    var newSize = DecodeInteger(Block, ref offset, 5);

                    // Section 6.3: the new size MUST NOT exceed the limit we
                    // advertised (SETTINGS_HEADER_TABLE_SIZE).
                    if (newSize > HeaderTableSizeLimit)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                            $"Dynamic table size update {newSize} exceeds advertised limit {HeaderTableSizeLimit}");

                    maxDynamicTableSize = newSize;

                    // Evict if necessary
                    while (dynamicTableSize > maxDynamicTableSize && dynamicTable.Count > 0)
                    {
                        var last = dynamicTable[^1];
                        dynamicTableSize -= (last.Name.Length + last.Value.Length + 32);
                        dynamicTable.RemoveAt(dynamicTable.Count - 1);
                    }
                }
                else
                {
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR,
                                                       $"Unknown HPACK representation byte: 0x{b:X2}");
                }

            }

            return headers;

        }

        #endregion

    }

}
