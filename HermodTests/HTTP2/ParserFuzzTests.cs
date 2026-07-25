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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Fuzzing the two hand-rolled parsers a peer can reach before any
    /// authentication, authorisation or application code runs: the frame header
    /// (RFC 9113, Section 4.1) and the HPACK decoder (RFC 7541).
    ///
    /// The invariant under test is not "it parses correctly" — most of this input is
    /// garbage and *should* be rejected. It is that garbage is rejected in the
    /// protocol's own vocabulary: a typed <see cref="HTTP2ConnectionException"/> or
    /// <see cref="HTTP2StreamException"/>, never an
    /// <c>IndexOutOfRangeException</c>, <c>ArgumentException</c>,
    /// <c>NullReferenceException</c> or <c>OverflowException</c>. The distinction
    /// matters on the wire: a typed error becomes the right GOAWAY code, an untyped
    /// one becomes INTERNAL_ERROR and a logged surprise.
    ///
    /// Every case is seeded deterministically, and a failure reports the seed and the
    /// exact input as hex so it can be replayed. Iterations default to a budget that
    /// keeps the suite fast; set <c>HERMOD_FUZZ_ITERATIONS</c> to soak for longer:
    ///
    ///   HERMOD_FUZZ_ITERATIONS=200000 dotnet test --filter FullyQualifiedName~ParserFuzz
    /// </summary>
    [TestFixture]
    public class ParserFuzzTests
    {

        #region Data / helpers

        private static Int32 Iterations
            => Int32.TryParse(Environment.GetEnvironmentVariable("HERMOD_FUZZ_ITERATIONS"), out var configured) && configured > 0
                   ? configured
                   : 20_000;

        /// <summary>
        /// Run one fuzz case, converting anything that is not a protocol-level
        /// rejection into a reproducible failure.
        /// </summary>
        private static void Case(Int32 Seed, Byte[] Input, Action Body)
        {
            try
            {
                Body();
            }
            catch (HTTP2ConnectionException)
            {
                // The correct way to reject garbage.
            }
            catch (HTTP2StreamException)
            {
                // Likewise, for the stream-scoped variety.
            }
            catch (Exception e)
            {
                Assert.Fail($"seed {Seed}: {e.GetType().Name}: {e.Message}{Environment.NewLine}" +
                            $"input ({Input.Length} bytes): {Convert.ToHexString(Input)}");
            }
        }

        /// <summary>A valid header block, as our own encoder would produce it.</summary>
        private static Byte[] ValidBlock()
            => new HPACKEncoder().EncodeHeaderBlock([
                   (":method",    "GET"),
                   (":scheme",    "https"),
                   (":authority", "example.com"),
                   (":path",      "/some/path?with=query"),
                   ("user-agent", "fuzz/1.0"),
                   ("accept",     "text/html,application/xhtml+xml"),
                   ("cookie",     "session=0123456789abcdef")
               ]);

        #endregion


        #region FrameHeader_SurvivesArbitraryBytes()

        // Any nine bytes are a syntactically valid frame header, so the parser must
        // never reject them — but it must also never hand back a stream ID with the
        // reserved bit still set, or a length outside the 24-bit field, since every
        // downstream check assumes those hold.
        [Test]
        public void FrameHeader_SurvivesArbitraryBytes()
        {

            for (var seed = 0; seed < Iterations; seed++)
            {

                var random = new Random(seed);
                var header = new Byte[9];
                random.NextBytes(header);

                Case(seed, header, () =>
                {

                    var frame = HTTP2Frame.ParseHeader(header);

                    if (frame.StreamId > 0x7FFFFFFFu)
                        Assert.Fail($"seed {seed}: reserved bit leaked into the stream ID ({frame.StreamId})");

                    if (frame.Length > 0xFFFFFFu)
                        Assert.Fail($"seed {seed}: length {frame.Length} exceeds the 24-bit field");

                });

            }

        }

        #endregion

        #region FrameHeader_RoundTrips()

        // Serialize/parse must be lossless for everything a peer could legitimately
        // send, including the extremes of each field.
        [Test]
        public void FrameHeader_RoundTrips()
        {

            for (var seed = 0; seed < Math.Min(Iterations, 5_000); seed++)
            {

                var random   = new Random(seed);
                var payload  = new Byte[random.Next(0, 64)];
                random.NextBytes(payload);

                var original = new HTTP2Frame {
                    Type     = (HTTP2FrameType) random.Next(0, 256),
                    Flags    = (HTTP2FrameFlags) random.Next(0, 256),
                    StreamId = (UInt32) random.Next() & 0x7FFFFFFFu,
                    Length   = (UInt32) payload.Length,
                    Payload  = payload
                };

                var parsed = HTTP2Frame.ParseHeader(original.Serialize()[..9]);

                Assert.Multiple(() =>
                {
                    Assert.That(parsed.Type,     Is.EqualTo(original.Type),     $"seed {seed}: type");
                    Assert.That(parsed.Flags,    Is.EqualTo(original.Flags),    $"seed {seed}: flags");
                    Assert.That(parsed.StreamId, Is.EqualTo(original.StreamId), $"seed {seed}: stream id");
                    Assert.That(parsed.Length,   Is.EqualTo(original.Length),   $"seed {seed}: length");
                });

            }

        }

        #endregion


        #region HPACK_RandomBlocks_FailInTheProtocolsVocabulary()

        [Test]
        public void HPACK_RandomBlocks_FailInTheProtocolsVocabulary()
        {

            for (var seed = 0; seed < Iterations; seed++)
            {

                var random = new Random(seed);
                var block  = new Byte[random.Next(0, 128)];
                random.NextBytes(block);

                Case(seed, block, () => new HPACKDecoder().DecodeHeaderBlock(block));

            }

        }

        #endregion

        #region HPACK_MutatedValidBlocks_FailInTheProtocolsVocabulary()

        // Purely random bytes rarely get far into the decoder — most are rejected on
        // the first octet. Mutating a *valid* block reaches the deep paths: string
        // literals, Huffman runs, dynamic-table updates, indexed lookups.
        [Test]
        public void HPACK_MutatedValidBlocks_FailInTheProtocolsVocabulary()
        {

            var valid = ValidBlock();

            for (var seed = 0; seed < Iterations; seed++)
            {

                var random  = new Random(seed);
                var mutated = (Byte[]) valid.Clone();

                switch (random.Next(4))
                {

                    case 0:   // flip a few bits
                        for (var i = random.Next(1, 4); i > 0; i--)
                            mutated[random.Next(mutated.Length)] ^= (Byte) (1 << random.Next(8));
                        break;

                    case 1:   // truncate
                        mutated = mutated[..random.Next(0, mutated.Length)];
                        break;

                    case 2:   // replace a run with garbage
                        var start = random.Next(mutated.Length);
                        var run   = random.Next(1, Math.Min(16, mutated.Length - start + 1));
                        for (var i = start; i < start + run && i < mutated.Length; i++)
                            mutated[i] = (Byte) random.Next(256);
                        break;

                    case 3:   // append garbage
                        var extra = new Byte[random.Next(1, 32)];
                        random.NextBytes(extra);
                        mutated = [.. mutated, .. extra];
                        break;

                }

                var input = mutated;
                Case(seed, input, () => new HPACKDecoder().DecodeHeaderBlock(input));

            }

        }

        #endregion


        #region HPACK_IntegerOverflow_IsADecodingError()

        // RFC 7541 §5.1: an encoding whose value exceeds what the implementation can
        // represent MUST be treated as a decoding error. Five continuation octets
        // overflow a 32-bit accumulator — and the dangerous case is the one where the
        // *last* octet clears the continuation bit, since a naive loop then returns
        // the wrapped (negative) value instead of rejecting it.
        [Test]
        public void HPACK_IntegerOverflow_IsADecodingError()
        {

            // A literal header field with an incremental index (0x40), whose name is a
            // string literal whose length is the overflowing integer.
            Byte[] WithLength(params Byte[] LengthOctets)
                => [0x40, .. LengthOctets];

            Assert.Multiple(() =>
            {
                // 0x7F prefix = "127, continued"; then 5 continuation octets.
                Assert.That(() => new HPACKDecoder().DecodeHeaderBlock(WithLength(0x7F, 0x80, 0x80, 0x80, 0x80, 0x7F)),
                            Throws.InstanceOf<HTTP2ConnectionException>(),
                            "five continuation octets, last one terminating -> must be rejected, not wrapped");

                Assert.That(() => new HPACKDecoder().DecodeHeaderBlock(WithLength(0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F)),
                            Throws.InstanceOf<HTTP2ConnectionException>(),
                            "the same, with every value bit set");

                // Continuation bit set at the end of the block: the integer never
                // terminates, which is a truncated encoding, not a value of "so far".
                Assert.That(() => new HPACKDecoder().DecodeHeaderBlock(WithLength(0x7F, 0x80, 0x80)),
                            Throws.InstanceOf<HTTP2ConnectionException>(),
                            "unterminated multi-byte integer");
            });

        }

        #endregion

        #region HPACK_ZeroIndex_IsADecodingError()

        // RFC 7541 §6.1: the index of an indexed header field must not be zero.
        [Test]
        public void HPACK_ZeroIndex_IsADecodingError()
        {
            Assert.That(() => new HPACKDecoder().DecodeHeaderBlock([0x80]),
                        Throws.InstanceOf<HTTP2ConnectionException>());
        }

        #endregion

        #region HPACK_HuffmanViolations_AreDecodingErrors()

        // RFC 7541 §5.2: the EOS symbol must never be encoded explicitly, padding is
        // at most 7 bits, and padding bits must be the prefix of EOS — i.e. all ones.
        [Test]
        public void HPACK_HuffmanViolations_AreDecodingErrors()
        {

            // A literal with an empty name and a Huffman-coded value.
            Byte[] WithHuffmanValue(params Byte[] Encoded)
                => [0x40, 0x00, (Byte) (0x80 | Encoded.Length), .. Encoded];

            Assert.Multiple(() =>
            {
                // EOS is 30 bits of (almost) all ones: 0x3FFFFFFF.
                Assert.That(() => new HPACKDecoder().DecodeHeaderBlock(WithHuffmanValue(0xFF, 0xFF, 0xFF, 0xFF)),
                            Throws.InstanceOf<HTTP2ConnectionException>(),
                            "an explicitly encoded EOS");

                // '0' is 5 bits of zero; a whole zero octet is '0','0' plus 6 zero
                // padding bits — too many, and not ones either.
                Assert.That(() => new HPACKDecoder().DecodeHeaderBlock(WithHuffmanValue(0x00, 0x00)),
                            Throws.InstanceOf<HTTP2ConnectionException>(),
                            "padding that is neither short enough nor all ones");
            });

        }

        #endregion

        #region HPACK_OversizedTableUpdate_IsADecodingError()

        // RFC 7541 §6.3: a dynamic table size update must not exceed the limit the
        // decoder advertised via SETTINGS_HEADER_TABLE_SIZE.
        [Test]
        public void HPACK_OversizedTableUpdate_IsADecodingError()
        {

            var decoder = new HPACKDecoder { HeaderTableSizeLimit = 4096 };

            Assert.Multiple(() =>
            {
                // 0x3F = size update with a continued 5-bit prefix; 0xE1 0x3F then
                // encodes 31 + 97 + 63*128 = 8192, twice the advertised limit.
                Assert.That(() => decoder.DecodeHeaderBlock([0x3F, 0xE1, 0x3F]),
                            Throws.InstanceOf<HTTP2ConnectionException>(), "8192 > 4096");

                // Exactly the limit is legal, and worth pinning: an off-by-one here
                // would reject a perfectly conformant peer.
                Assert.That(() => new HPACKDecoder { HeaderTableSizeLimit = 4096 }.DecodeHeaderBlock([0x3F, 0xE1, 0x1F]),
                            Throws.Nothing, "4096 == 4096 is allowed");
            });

        }

        #endregion

    }

}
