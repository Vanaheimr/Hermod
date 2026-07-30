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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.QPack;

[TestFixture]
public class QpackTests
{
    // --- Huffman (RFC 7541 Appendix B/C) -------------------------------------------------------

        [TestCase("www.example.com", "f1e3c2e5f23a6ba0ab90f4ff")]
    [TestCase("/index.html", "60d5485f2bce9a68")] // only used by the encoder when shorter
    [TestCase("no-cache", "a8eb10649cbf")]
    public void Huffman_Encode_MatchesKnownVectors(string text, string expectedHex)
    {
        byte[] encoded = HuffmanEncode(text);
        Assert.That(Hex.ToHex(encoded), Is.EqualTo(expectedHex));

        Assert.That(Huffman.TryDecode(encoded, out byte[] decoded), Is.True);
        Assert.That(System.Text.Encoding.ASCII.GetString(decoded), Is.EqualTo(text));
    }

    [Test]
    public void Huffman_RejectsInvalidPadding()
    {
        // A 0x00 byte is not a valid Huffman string (no symbol, invalid padding).
        Assert.That(Huffman.TryDecode([0x00], out _), Is.False);
    }

    private static byte[] HuffmanEncode(string text)
    {
        var w = new Quic.Core.Buffers.BufferWriter(32);
        try
        {
            Huffman.Encode(ref w, System.Text.Encoding.ASCII.GetBytes(text));
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    // --- Encoder/Decoder (RFC 9204) ------------------------------------------------------------

    [Test]
    public void Encode_LiteralWithNameReference_UsesHuffmanWhenShorter()
    {
        // RFC 9204 B.1 encodes :path=/index.html as a name reference (static index 1) + value.
        // The RFC example uses the raw form (0b + 11 bytes); our encoder prefers Huffman
        // (8 bytes, 0x88) — both spec-compliant. Interop with the raw form is checked by the decode test below.
        byte[] encoded = QpackEncoder.Encode([new HeaderField(":path", "/index.html")]);
        Assert.That(Hex.ToHex(encoded), Is.EqualTo("0000518860d5485f2bce9a68"));
    }

    [Test]
    public void Encode_ExactPair_ProducesIndexedFieldLine()
    {
        // :method=GET is static-table index 17 -> indexed: 0b1100_0000 | 17 = 0xd1.
        byte[] encoded = QpackEncoder.Encode([new HeaderField(":method", "GET")]);
        Assert.That(Hex.ToHex(encoded), Is.EqualTo("0000d1"));
    }

    [Test]
    public void Decode_Rfc9204ExampleB1_YieldsPath()
    {
        QpackResult result = QpackDecoder.Decode(Hex.Parse("0000510b2f696e6465782e68746d6c"), out var headers);
        Assert.That(result, Is.EqualTo(QpackResult.Ok));
        Assert.That(Expect.Single(headers), Is.EqualTo(new HeaderField(":path", "/index.html")));
    }

    [Test]
    public void RoundTrip_TypicalRequestHeaders()
    {
        List<HeaderField> headers =
        [
            new(":method", "GET"),
            new(":scheme", "https"),
            new(":authority", "cloudflare-quic.com"),
            new(":path", "/"),
            new("user-agent", "http3-from-scratch/0.1"),
            new("accept", "*/*"),
        ];

        byte[] encoded = QpackEncoder.Encode(headers);
        Assert.That(QpackDecoder.Decode(encoded, out var decoded), Is.EqualTo(QpackResult.Ok));
        Assert.That(decoded, Is.EqualTo(headers));
    }

    [Test]
    public void RoundTrip_LiteralNameAndValue_WithHuffman()
    {
        // A header whose name is not in the static table -> literal name + literal value.
        var headers = new List<HeaderField> { new("x-custom-header", "some-longer-value-that-huffman-compresses") };

        byte[] encoded = QpackEncoder.Encode(headers);
        Assert.That(QpackDecoder.Decode(encoded, out var decoded), Is.EqualTo(QpackResult.Ok));
        Assert.That(decoded, Is.EqualTo(headers));
    }

    [Test]
    public void Decode_DynamicTableReference_IsRejected()
    {
        // Required Insert Count != 0 signals a dynamic table that we do not maintain.
        QpackResult result = QpackDecoder.Decode(Hex.Parse("0200d1"), out _);
        Assert.That(result, Is.EqualTo(QpackResult.DynamicTableReference));
    }

    [Test]
    public void StaticTable_HasExpectedAnchors()
    {
        // Spot checks against RFC 9204 Appendix A.
        Assert.That(GetStatic(0), Is.EqualTo((":authority", "")));
        Assert.That(GetStatic(1), Is.EqualTo((":path", "/")));
        Assert.That(GetStatic(17), Is.EqualTo((":method", "GET")));
        Assert.That(GetStatic(98), Is.EqualTo(("x-frame-options", "sameorigin")));
    }

    private static (string, string) GetStatic(int index)
    {
        // Access via a round trip of an indexed field line.
        var w = new Quic.Core.Buffers.BufferWriter(8);
        try
        {
            QpackDecoder.Decode(Encode(index), out var headers);
            return (headers[0].Name, headers[0].Value);
        }
        finally { w.Dispose(); }

        static byte[] Encode(int idx)
        {
            // 0000 (prefix) + indexed field line static (1 T=1 index 6+).
            var w = new Quic.Core.Buffers.BufferWriter(8);
            try
            {
                w.WriteByte(0x00); w.WriteByte(0x00);
                if (idx < 63) { w.WriteByte((byte)(0xC0 | idx)); }
                else { w.WriteByte(0xFF); w.WriteByte((byte)(idx - 63)); }
                return w.WrittenSpan.ToArray();
            }
            finally { w.Dispose(); }
        }
    }
}
