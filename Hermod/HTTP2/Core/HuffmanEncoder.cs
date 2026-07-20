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
    /// Huffman encoder for HPACK string literals, the mirror of
    /// <see cref="HuffmanDecoder"/>. Emits each octet's canonical codeword MSB-first
    /// and pads the final partial byte with 1-bits (a prefix of the EOS codeword),
    /// exactly as RFC 7541 Section 5.2 requires. Encodes from the same
    /// <see cref="HuffmanDecoder.HuffmanTable"/>, so a round trip is exact by
    /// construction.
    /// </summary>
    public static class HuffmanEncoder
    {

        /// <summary>
        /// The number of bytes <paramref name="Data"/> would occupy Huffman-encoded
        /// (rounded up to a whole byte). Lets the caller pick the shorter of the
        /// Huffman and raw representations without encoding twice.
        /// </summary>
        public static int EncodedByteLength(ReadOnlySpan<byte> Data)
        {
            var bits = 0L;
            foreach (var octet in Data)
                bits += HuffmanDecoder.HuffmanTable[octet].Bits;
            return (int) ((bits + 7) / 8);
        }

        /// <summary>
        /// Huffman-encode <paramref name="Data"/> into a freshly allocated byte
        /// array, padding the last byte's unused low bits with 1s.
        /// </summary>
        public static byte[] Encode(ReadOnlySpan<byte> Data)
        {

            var output = new byte[EncodedByteLength(Data)];

            var bitPos = 0;   // absolute bit offset into output, MSB-first

            foreach (var octet in Data)
            {

                var (code, bits) = HuffmanDecoder.HuffmanTable[octet];

                // Emit the codeword's bits from most- to least-significant.
                for (var bitIndex = bits - 1; bitIndex >= 0; bitIndex--)
                {
                    if (((code >> bitIndex) & 1) != 0)
                        output[bitPos >> 3] |= (byte) (0x80 >> (bitPos & 7));
                    bitPos++;
                }

            }

            // Pad the remainder of the final byte with 1-bits (EOS prefix).
            while ((bitPos & 7) != 0)
            {
                output[bitPos >> 3] |= (byte) (0x80 >> (bitPos & 7));
                bitPos++;
            }

            return output;

        }

    }

}
