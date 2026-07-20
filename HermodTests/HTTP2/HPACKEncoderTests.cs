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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Unit tests for the HPACK encoder (RFC 7541): full static table +
    /// per-connection dynamic table + Huffman coding. The strongest correctness
    /// check is a round trip through our OWN decoder — an encoder+decoder pair
    /// that stay in lockstep prove the dynamic-table accounting matches. Interop
    /// with real peers (Kestrel/HttpClient decoding our encoder output) is
    /// covered separately by the integration harnesses.
    /// </summary>
    [TestFixture]
    public class HPACKEncoderTests
    {

        #region (helper) SameHeaders(a, b)

        private static Boolean SameHeaders(List<(String Name, String Value)> a,
                                           List<(String Name, String Value)> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
                if (a[i].Name != b[i].Name || a[i].Value != b[i].Value) return false;
            return true;
        }

        #endregion


        #region HuffmanEncoder_RoundTrip()

        /// <summary>
        /// 5000-round HuffmanEncoder.Encode -> HuffmanDecoder.Decode round trip,
        /// plus an EncodedByteLength spot check ('a' is a 5-bit code, so ten of
        /// them are 50 bits = 7 bytes).
        /// </summary>
        [Test]
        public void HuffmanEncoder_RoundTrip()
        {

            var rng = new Random(1234);

            for (var iter = 0; iter < 5000; iter++)
            {
                var len   = rng.Next(0, 40);
                var bytes = new Byte[len];
                rng.NextBytes(bytes);

                var decoded = HuffmanDecoder.Decode(HuffmanEncoder.Encode(bytes));

                // The decoder yields one char per source octet.
                var back = new Byte[decoded.Length];
                for (var i = 0; i < decoded.Length; i++)
                    back[i] = (Byte) decoded[i];

                Assert.That(back.AsSpan().SequenceEqual(bytes), Is.True,
                            $"round #{iter}: {len}-byte Huffman round trip");
            }

            Assert.That(HuffmanEncoder.EncodedByteLength(Encoding.ASCII.GetBytes("aaaaaaaaaa")),
                        Is.EqualTo(7),
                        "EncodedByteLength('aaaaaaaaaa')");

        }

        #endregion

        #region StaticTable_ExactAndNameIndex()

        /// <summary>
        /// ":method GET" is static index 2 and must encode to the single indexed
        /// byte 0x82; "content-type" is a static NAME (index 31) with a literal
        /// value, which must still compress below the raw name+value length.
        /// </summary>
        [Test]
        public void StaticTable_ExactAndNameIndex()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();

            var block = enc.EncodeHeaderBlock([(":method", "GET")]);

            Assert.Multiple(() =>
            {
                Assert.That(block.Length == 1 && block[0] == 0x82, Is.True,
                            $":method GET encodes to one byte 0x82 ({BitConverter.ToString(block)})");
                Assert.That(SameHeaders(dec.DecodeHeaderBlock(block), [(":method", "GET")]), Is.True,
                            ":method GET round-trips");
            });

            var enc2   = new HPACKEncoder();
            var dec2   = new HPACKDecoder();
            var block2 = enc2.EncodeHeaderBlock([("content-type", "text/plain")]);

            Assert.Multiple(() =>
            {
                Assert.That(SameHeaders(dec2.DecodeHeaderBlock(block2), [("content-type", "text/plain")]), Is.True,
                            "content-type: text/plain round-trips");
                Assert.That(block2.Length < ("content-type".Length + "text/plain".Length), Is.True,
                            $"content-type value compressed ({block2.Length} bytes)");
            });

        }

        #endregion

        #region DynamicTable_RepeatedFieldCollapses()

        /// <summary>
        /// A repeated custom field must collapse to a single indexed byte the
        /// second time it is encoded on the same connection.
        /// </summary>
        [Test]
        public void DynamicTable_RepeatedFieldCollapses()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();

            var hdr = new List<(String, String)> { ("x-custom-header", "some-repeated-value") };

            var first  = enc.EncodeHeaderBlock(hdr);
            var d1     = dec.DecodeHeaderBlock(first);
            var second = enc.EncodeHeaderBlock(hdr);
            var d2     = dec.DecodeHeaderBlock(second);

            Assert.Multiple(() =>
            {
                Assert.That(SameHeaders(d1, hdr), Is.True, "first block round-trips");
                Assert.That(SameHeaders(d2, hdr), Is.True, "second block round-trips");
                Assert.That(second.Length == 1 && (second[0] & 0x80) != 0, Is.True,
                            $"repeated field is a single index byte (first={first.Length}B second={second.Length}B)");
                Assert.That(second.Length < first.Length, Is.True,
                            "second is dramatically smaller than first");
            });

        }

        #endregion

        #region MultiBlock_StaysInSync()

        /// <summary>
        /// Three sequential mixed header blocks all round-trip, and a value
        /// repeated across blocks ("server: demo") is indexed after the first.
        /// </summary>
        [Test]
        public void MultiBlock_StaysInSync()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();

            var blocks = new List<(String, String)>[]
            {
                [(":status", "200"), ("content-type", "text/plain"),       ("server", "demo")],
                [(":status", "200"), ("content-type", "application/json"), ("server", "demo")],
                [(":status", "404"), ("content-type", "text/plain"),       ("server", "demo")],
            };

            foreach (var b in blocks)
            {
                var back = dec.DecodeHeaderBlock(enc.EncodeHeaderBlock(new List<(String, String)>(b)));
                Assert.That(SameHeaders(back, new List<(String, String)>(b)), Is.True,
                            "sequential mixed block round-trips");
            }

            var enc2 = new HPACKEncoder();
            var dec2 = new HPACKDecoder();
            var s1   = enc2.EncodeHeaderBlock([("server", "demo")]);
            _        = dec2.DecodeHeaderBlock(s1);
            var s2   = enc2.EncodeHeaderBlock([("server", "demo")]);

            Assert.That(s2.Length < s1.Length && SameHeaders(dec2.DecodeHeaderBlock(s2), [("server", "demo")]), Is.True,
                        "repeated 'server: demo' indexed on reuse");

        }

        #endregion

        #region NeverIndex_SensitiveAndVolatileFields()

        /// <summary>
        /// "authorization" (never-indexed) and "content-length" (no-index /
        /// volatile) must both round-trip but must NOT collapse to a one-byte
        /// index when repeated.
        /// </summary>
        [Test]
        public void NeverIndex_SensitiveAndVolatileFields()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();
            var auth = new List<(String, String)> { ("authorization", "Bearer secret-token-value") };
            var a1  = enc.EncodeHeaderBlock(auth);
            var da1 = dec.DecodeHeaderBlock(a1);
            var a2  = enc.EncodeHeaderBlock(auth);
            var da2 = dec.DecodeHeaderBlock(a2);

            Assert.Multiple(() =>
            {
                Assert.That(SameHeaders(da1, auth) && SameHeaders(da2, auth), Is.True, "authorization round-trips");
                Assert.That(a2.Length > 1, Is.True, $"authorization NOT collapsed to an index on repeat (a1={a1.Length}B a2={a2.Length}B)");
            });

            var enc2 = new HPACKEncoder();
            var dec2 = new HPACKDecoder();
            var cl  = new List<(String, String)> { ("content-length", "12345") };
            var c1  = enc2.EncodeHeaderBlock(cl);
            var dc1 = dec2.DecodeHeaderBlock(c1);
            var c2  = enc2.EncodeHeaderBlock(cl);
            var dc2 = dec2.DecodeHeaderBlock(c2);

            Assert.That(SameHeaders(dc1, cl) && SameHeaders(dc2, cl) && c2.Length > 1, Is.True,
                        "content-length round-trips and stays literal");

        }

        #endregion

        #region DynamicTableSizeUpdate_Signaling()

        /// <summary>
        /// Shrinking the dynamic table to 0 must emit a size-update prefix (0x20)
        /// and disable dynamic indexing; growing it back must re-enable indexing.
        /// </summary>
        [Test]
        public void DynamicTableSizeUpdate_Signaling()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();

            enc.SetMaxDynamicTableSize(0);
            var hdr = new List<(String, String)> { ("x-thing", "value1") };
            var b1  = enc.EncodeHeaderBlock(hdr);

            Assert.Multiple(() =>
            {
                Assert.That((b1[0] & 0xE0) == 0x20, Is.True, $"block after shrink starts with a size update (0x{b1[0]:X2})");
                Assert.That(SameHeaders(dec.DecodeHeaderBlock(b1), hdr), Is.True, "round-trips after size update");
            });

            var b2 = enc.EncodeHeaderBlock(hdr);
            Assert.That(b2.Length > 1 && SameHeaders(dec.DecodeHeaderBlock(b2), hdr), Is.True,
                        "with table size 0, repeat is NOT indexed");

            enc.SetMaxDynamicTableSize(4096);
            var g1  = enc.EncodeHeaderBlock(hdr);
            var dg1 = dec.DecodeHeaderBlock(g1);
            var g2  = enc.EncodeHeaderBlock(hdr);
            var dg2 = dec.DecodeHeaderBlock(g2);

            Assert.That(SameHeaders(dg1, hdr) && SameHeaders(dg2, hdr) && g2.Length < g1.Length, Is.True,
                        "after growing back, round-trips and re-indexes");

        }

        #endregion

        #region RealisticRequestSet_RoundTrips()

        /// <summary>
        /// A realistic request header set round-trips and compresses below its
        /// raw name+value byte count.
        /// </summary>
        [Test]
        public void RealisticRequestSet_RoundTrips()
        {

            var enc = new HPACKEncoder();
            var dec = new HPACKDecoder();

            var req = new List<(String, String)>
            {
                (":method",         "GET"),
                (":scheme",         "https"),
                (":authority",      "example.com"),
                (":path",           "/index.html"),
                ("user-agent",      "HTTP2FromScratch/1.0"),
                ("accept",          "text/html,application/xhtml+xml"),
                ("accept-encoding", "gzip, deflate"),
                ("cookie",          "session=abc123"),
            };

            var block   = enc.EncodeHeaderBlock(req);
            var back    = dec.DecodeHeaderBlock(block);
            var rawSize = req.Sum(h => h.Item1.Length + h.Item2.Length);

            Assert.Multiple(() =>
            {
                Assert.That(SameHeaders(back, req), Is.True, "realistic request round-trips");
                Assert.That(block.Length < rawSize, Is.True, $"compressed smaller than raw ({block.Length}B vs {rawSize}B)");
            });

        }

        #endregion

    }

}
