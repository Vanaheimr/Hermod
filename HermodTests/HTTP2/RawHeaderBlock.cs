/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// HPACK header blocks built byte by byte (RFC 7541), for tests that need a
    /// block of an exact size or shape: an <see cref="HPACKEncoder"/> picks
    /// Huffman coding where it is shorter, and indexes what it has seen before.
    ///
    /// A literal without indexing leaves the decoder's dynamic table as it is,
    /// so blocks of those can go out between the blocks of an encoder on the
    /// same connection without putting the two ends out of step. A literal with
    /// incremental indexing does not: it is for tests that send no block of an
    /// encoder's after it, or that mean the decoder to take the entry in.
    /// </summary>
    internal static class RawHeaderBlock
    {

        /// <summary>
        /// A literal header field without indexing, new name, both strings raw
        /// (Section 6.2.2): one byte of type, then each string's length and octets.
        /// </summary>
        public static Byte[] Literal(String Name, String Value)

            => Field(0x00, Name, Value);

        /// <summary>
        /// A literal header field with incremental indexing, new name, both
        /// strings raw (Section 6.2.1): the decoder adds it to its dynamic table.
        /// </summary>
        public static Byte[] LiteralWithIndexing(String Name, String Value)

            => Field(0x40, Name, Value);

        /// <summary>
        /// An indexed header field (Section 6.1): 1 to 61 the static table, from
        /// 62 on the dynamic table, newest entry first.
        /// </summary>
        public static Byte[] Indexed(Int32 Index)
        {
            var block = new List<Byte>();
            WriteInteger(block, Index, 7, 0x80);
            return [.. block];
        }

        /// <summary>
        /// These fields, each as a literal without indexing.
        /// </summary>
        public static Byte[] Literals(IEnumerable<(String Name, String Value)> Fields)

            => [.. Fields.SelectMany(field => Literal(field.Name, field.Value))];

        /// <summary>
        /// The header block in a HEADERS frame and, past
        /// <paramref name="FragmentSize"/> bytes — the default MAX_FRAME_SIZE —
        /// in CONTINUATION frames after it, END_HEADERS on the last.
        /// </summary>
        public static List<HTTP2Frame> Frames(UInt32   StreamId,
                                              Byte[]   Block,
                                              Boolean  EndStream,
                                              Int32    FragmentSize   = 16384)
        {

            var first   = Block[..Math.Min(FragmentSize, Block.Length)];

            var frames  = new List<HTTP2Frame> {
                              HTTP2Frame.CreateHeaders(StreamId,
                                                       first,
                                                       EndStream:  EndStream,
                                                       EndHeaders: first.Length == Block.Length)
                          };

            for (var offset = first.Length; offset < Block.Length; offset += FragmentSize)
            {

                var fragment = Block[offset..Math.Min(offset + FragmentSize, Block.Length)];

                frames.Add(new HTTP2Frame {
                               Type      = HTTP2FrameType.CONTINUATION,
                               Flags     = offset + fragment.Length == Block.Length
                                               ? HTTP2FrameFlags.END_HEADERS
                                               : HTTP2FrameFlags.NONE,
                               StreamId  = StreamId,
                               Length    = (UInt32) fragment.Length,
                               Payload   = fragment
                           });

            }

            return frames;

        }

        /// <summary>
        /// The value of a SETTINGS frame's parameter, or null if it does not carry it.
        /// </summary>
        public static UInt32? SettingOf(HTTP2Frame Settings, HTTP2SettingsParameter Parameter)
        {

            for (var offset = 0; offset + 6 <= Settings.Payload.Length; offset += 6)
            {

                var id = (HTTP2SettingsParameter) (Settings.Payload[offset] << 8 | Settings.Payload[offset + 1]);

                if (id == Parameter)
                    return (UInt32) (Settings.Payload[offset + 2] << 24 |
                                     Settings.Payload[offset + 3] << 16 |
                                     Settings.Payload[offset + 4] <<  8 |
                                     Settings.Payload[offset + 5]);

            }

            return null;

        }


        private static Byte[] Field(Byte Type, String Name, String Value)
        {

            var block = new List<Byte> { Type };

            WriteString(block, Name);
            WriteString(block, Value);

            return [.. block];

        }

        /// <summary>
        /// A string literal without Huffman coding (Section 5.2).
        /// </summary>
        private static void WriteString(List<Byte> Block, String Text)
        {
            var octets = Encoding.ASCII.GetBytes(Text);
            WriteInteger(Block, octets.Length, 7, 0x00);
            Block.AddRange(octets);
        }

        /// <summary>
        /// An integer on a prefix of <paramref name="PrefixBits"/> bits, the bits
        /// above it taken from <paramref name="Prefix"/> (Section 5.1).
        /// </summary>
        private static void WriteInteger(List<Byte> Block, Int32 Value, Int32 PrefixBits, Byte Prefix)
        {

            var limit = (1 << PrefixBits) - 1;

            if (Value < limit)
            {
                Block.Add((Byte) (Prefix | Value));
                return;
            }

            Block.Add((Byte) (Prefix | limit));

            for (Value -= limit; Value >= 128; Value >>= 7)
                Block.Add((Byte) (0x80 | (Value & 0x7F)));

            Block.Add((Byte) Value);

        }

    }

}
