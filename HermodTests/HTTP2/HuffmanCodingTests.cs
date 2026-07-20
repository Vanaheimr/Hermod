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
using System.Reflection;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Unit tests for the trie-based RFC 7541 Huffman decoder: a known test
    /// vector, differential fuzzing against a from-scratch oracle encoder and
    /// against the old linear-scan algorithm, and the padding / EOS edge cases.
    /// Pure in-memory, no network.
    /// </summary>
    [TestFixture]
    public class HuffmanCodingTests
    {

        #region Data

        // The production Huffman table, pulled via reflection so the oracle
        // encoder and the oracle (old-algorithm) decoder both work off the EXACT
        // same table data the production trie is built from — differential
        // testing then isolates "did I re-implement the algorithm correctly",
        // not "did I mistype the table".
        private static readonly (UInt32 Code, Int32 Bits)[] HuffmanTable =
            ((UInt32 Code, Int32 Bits)[]) typeof(HuffmanDecoder)
                .GetField("HuffmanTable", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

        #endregion

        #region (helper) OracleEncode(String)

        /// <summary>
        /// A known-correct, from-scratch Huffman encoder (bit-by-bit, all-1s
        /// padding to the byte boundary) used purely as a test oracle.
        /// </summary>
        private static Byte[] OracleEncode(String s)
        {

            var bits = new List<Int32>();
            foreach (var ch in s)
            {
                var (code, len) = HuffmanTable[(Byte) ch];
                for (var i = len - 1; i >= 0; i--)
                    bits.Add((Int32) ((code >> i) & 1));
            }

            // Pad with 1s to a byte boundary (valid EOS-prefix padding).
            while (bits.Count % 8 != 0)
                bits.Add(1);

            var bytes = new Byte[bits.Count / 8];
            for (var i = 0; i < bits.Count; i++)
                if (bits[i] == 1)
                    bytes[i / 8] |= (Byte) (1 << (7 - (i % 8)));

            return bytes;

        }

        #endregion

        #region (helper) OracleDecodeOld(ReadOnlySpan<Byte>)

        /// <summary>
        /// Re-implementation of the OLD algorithm being replaced (O(n*257) linear
        /// scan), used purely as a differential oracle — not the code under test.
        /// </summary>
        private static String OracleDecodeOld(ReadOnlySpan<Byte> data)
        {

            var    result = new StringBuilder(data.Length * 2);
            UInt32 buffer = 0;
            Int32  bits   = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits  += 8;

                while (bits >= 5)
                {
                    var matched = false;
                    for (var sym = 0; sym < 256; sym++)
                    {
                        var (code, codeLen) = HuffmanTable[sym];
                        if (bits >= codeLen)
                        {
                            var candidate = buffer >> (bits - codeLen);
                            if (candidate == code)
                            {
                                result.Append((Char) sym);
                                bits   -= codeLen;
                                buffer &= (UInt32) ((1L << bits) - 1);
                                matched = true;
                                break;
                            }
                        }
                    }
                    if (!matched) break;
                }
            }

            if (bits > 7)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.COMPRESSION_ERROR, "too many padding bits");

            return result.ToString();

        }

        #endregion


        #region Decode_RFC7541KnownVector()

        /// <summary>
        /// The canonical RFC 7541 Appendix C example: "www.example.com".
        /// </summary>
        [Test]
        public void Decode_RFC7541KnownVector()
        {

            var encoded = new Byte[] { 0xf1, 0xe3, 0xc2, 0xe5, 0xf2, 0x3a, 0x6b, 0xa0, 0xab, 0x90, 0xf4, 0xff };

            Assert.That(HuffmanDecoder.Decode(encoded),
                        Is.EqualTo("www.example.com"));

        }

        #endregion

        #region Decode_RoundTripFuzz()

        /// <summary>
        /// 5000 random strings (full byte range, up to 60 chars): encode with the
        /// oracle encoder, decode with the production trie decoder, assert we get
        /// the original string back exactly.
        /// </summary>
        [Test]
        public void Decode_RoundTripFuzz()
        {

            var rnd = new Random(12345);

            for (var i = 0; i < 5000; i++)
            {

                var len = rnd.Next(0, 60);
                var sb  = new StringBuilder(len);
                for (var j = 0; j < len; j++)
                    sb.Append((Char) rnd.Next(0, 256));   // full byte range, incl. rare long-code symbols

                var original = sb.ToString();

                Assert.That(HuffmanDecoder.Decode(OracleEncode(original)),
                            Is.EqualTo(original),
                            $"round #{i}: original=\"{original}\"");

            }

        }

        #endregion

        #region Decode_DifferentialCrossCheckVsOldAlgorithm()

        /// <summary>
        /// 2000 printable-ASCII strings (all &lt;=15-bit codes): the old linear-scan
        /// decoder and the new trie decoder must agree, and both must match the
        /// original. Restricted to symbols &lt;128 so the old algorithm's own
        /// 32-bit-buffer limitation does not trip (it is only a secondary oracle).
        /// </summary>
        [Test]
        public void Decode_DifferentialCrossCheckVsOldAlgorithm()
        {

            var rnd = new Random(54321);

            for (var i = 0; i < 2000; i++)
            {

                var len = rnd.Next(0, 60);
                var sb  = new StringBuilder(len);
                for (var j = 0; j < len; j++)
                    sb.Append((Char) rnd.Next(32, 127));   // printable ASCII

                var original = sb.ToString();
                var encoded  = OracleEncode(original);

                var oldResult = OracleDecodeOld(encoded);
                var newResult = HuffmanDecoder.Decode(encoded);

                Assert.Multiple(() =>
                {
                    Assert.That(newResult, Is.EqualTo(original),  $"round #{i}: new decoder vs. original \"{original}\"");
                    Assert.That(oldResult, Is.EqualTo(newResult), $"round #{i}: old vs. new decoder");
                });

            }

        }

        #endregion

        #region Decode_PaddingEdgeCases()

        /// <summary>
        /// Padding / EOS handling per RFC 7541, Section 5.2: valid all-1s padding
        /// decodes; non-1 padding, over-length padding and an explicit EOS
        /// codeword are each rejected with COMPRESSION_ERROR; empty input decodes
        /// to the empty string.
        /// </summary>
        [Test]
        public void Decode_PaddingEdgeCases()
        {

            // Valid: 'a' (5-bit code 00011) + 3 padding bits, all 1s => 0x1F.
            Assert.That(HuffmanDecoder.Decode(new Byte[] { 0x1F }),
                        Is.EqualTo("a"),
                        "valid all-1s padding after 'a'");

            // Corrupted: same but padding bits are 000 => 0x18.
            var badPad = Assert.Throws<HTTP2ConnectionException>(
                             () => HuffmanDecoder.Decode(new Byte[] { 0x18 }));
            Assert.That(badPad!.ErrorCode, Is.EqualTo(HTTP2ErrorCode.COMPRESSION_ERROR),
                        "padding bits that are not all 1s");

            // Over-length padding: 'a' then 11 leftover 1-bits (>7) => 0x1F 0xFF.
            var overLong = Assert.Throws<HTTP2ConnectionException>(
                               () => HuffmanDecoder.Decode(new Byte[] { 0x1F, 0xFF }));
            Assert.That(overLong!.ErrorCode, Is.EqualTo(HTTP2ErrorCode.COMPRESSION_ERROR),
                        "more than 7 leftover padding bits");

            // Explicit EOS codeword (>=30 bits of 1) encoded mid-stream => reject.
            var eos = Assert.Throws<HTTP2ConnectionException>(
                          () => HuffmanDecoder.Decode(new Byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
            Assert.That(eos!.ErrorCode, Is.EqualTo(HTTP2ErrorCode.COMPRESSION_ERROR),
                        "explicit EOS codeword must never be encoded");

            // Empty input decodes to the empty string without error.
            Assert.That(HuffmanDecoder.Decode(ReadOnlySpan<Byte>.Empty),
                        Is.EqualTo(""),
                        "empty input");

        }

        #endregion

    }

}
