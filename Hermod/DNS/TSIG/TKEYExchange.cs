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

using System.Buffers.Binary;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The TKEY modes of RFC 2930 §2.5.
    /// </summary>
    public static class TKEYModes
    {

        /// <summary>The server picks the key and sends it back (RFC 2930 §4.3).</summary>
        public const UInt16 ServerAssignment    = 1;

        /// <summary>Both sides derive a shared secret by Diffie-Hellman (RFC 2930 §4.1).</summary>
        public const UInt16 DiffieHellman       = 2;

        /// <summary>GSS-API negotiation (RFC 2930 §4.2, elaborated by RFC 3645).</summary>
        public const UInt16 GssApi              = 3;

        /// <summary>The resolver picks the key (RFC 2930 §4.4).</summary>
        public const UInt16 ResolverAssignment  = 4;

        /// <summary>Delete a previously established key (RFC 2930 §4.5).</summary>
        public const UInt16 KeyDeletion         = 5;

    }


    /// <summary>
    /// TKEY key establishment, RFC 2930 §4.1 — deriving a TSIG secret by
    /// Diffie-Hellman instead of configuring one out of band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three properties of this mechanism are worth knowing before reaching for
    /// it, because none of them are obvious from the fact that it is on the
    /// standards track.
    /// </para>
    /// <para>
    /// The exchange is <em>unauthenticated</em>. Diffie-Hellman alone gives two
    /// parties a shared secret without telling either who the other is, so a
    /// machine-in-the-middle can run it with both sides. RFC 2930 §5 is explicit
    /// that the KEY records must themselves be authenticated — by DNSSEC, or by
    /// prior knowledge — for the result to mean anything.
    /// </para>
    /// <para>
    /// The derivation is <em>fixed to MD5</em>. §4.1 gives exactly one formula
    /// and it is built on MD5, so there is no conforming way to avoid it. On a
    /// host where the platform refuses MD5 — a FIPS-restricted machine — this
    /// code path cannot run at all, and says so rather than substituting a
    /// different hash and producing a key no peer would agree with.
    /// </para>
    /// <para>
    /// It is also, in practice, <em>not deployed</em>. What is deployed under
    /// the TKEY name is GSS-TSIG (mode 3), which needs a Kerberos stack. TSIG
    /// keys are otherwise configured out of band.
    /// </para>
    /// </remarks>
    public static class TKEYExchange
    {

        #region (static) DeriveKeyingMaterial(SharedSecret, QueryData, ServerData)

        /// <summary>
        /// Derive the keying material from a completed Diffie-Hellman exchange,
        /// per RFC 2930 §4.1.
        /// </summary>
        /// <param name="SharedSecret">The Diffie-Hellman shared value, as both sides computed it.</param>
        /// <param name="QueryData">The nonce the client sent in its TKEY Key Data.</param>
        /// <param name="ServerData">The nonce the server returned in its TKEY Key Data.</param>
        /// <remarks>
        /// <para>The formula, verbatim from §4.1:</para>
        /// <code>
        ///   keying material = XOR ( DH value,
        ///                           MD5 ( query data | DH value ) |
        ///                           MD5 ( server data | DH value ) )
        /// </code>
        /// <para>
        /// "|" is concatenation throughout: each nonce is suffixed with the DH
        /// value and digested, the two digests are joined into 32 octets, and
        /// that is XORed against the DH value. Where the two operands differ in
        /// length the shorter is left-justified and zero-padded, so the result is
        /// always as long as the longer of the two.
        /// </para>
        /// </remarks>
        public static Byte[] DeriveKeyingMaterial(Byte[]  SharedSecret,
                                                  Byte[]  QueryData,
                                                  Byte[]  ServerData)
        {

            var digests = new Byte[32];

            Buffer.BlockCopy(MD5Digest([.. QueryData,  .. SharedSecret]), 0, digests,  0, 16);
            Buffer.BlockCopy(MD5Digest([.. ServerData, .. SharedSecret]), 0, digests, 16, 16);

            return XorLeftJustified(SharedSecret, digests);

        }

        #endregion

        #region (static) EncodeDiffieHellmanKey(Prime, Generator, PublicValue)

        /// <summary>
        /// Encode Diffie-Hellman parameters and a public value as KEY RDATA,
        /// per RFC 2539 §2.
        /// </summary>
        /// <param name="Prime">The modulus p.</param>
        /// <param name="Generator">The generator g.</param>
        /// <param name="PublicValue">This party's public value, g^x mod p.</param>
        /// <remarks>
        /// Each of the three values is length-prefixed with two octets. A prime
        /// length of 1 or 2 does not mean a one- or two-octet prime: §2 reserves
        /// those to mean "the prime field is an index into a table of well-known
        /// prime/generator pairs". This encoder always writes the prime out in
        /// full, so it never produces that form — but the decoder has to
        /// recognise it, or it would read a table index as a modulus.
        /// </remarks>
        public static Byte[] EncodeDiffieHellmanKey(Byte[]  Prime,
                                                    Byte[]  Generator,
                                                    Byte[]  PublicValue)
        {

            if (Prime.Length is 1 or 2)
                throw new ArgumentException(
                          "RFC 2539 §2 reserves prime lengths 1 and 2 for well-known-group indices, " +
                          "so a prime of that size cannot be encoded literally.",
                          nameof(Prime));

            using var rdata = new MemoryStream();

            WriteLengthPrefixed(rdata, Prime);
            WriteLengthPrefixed(rdata, Generator);
            WriteLengthPrefixed(rdata, PublicValue);

            return rdata.ToArray();

        }

        #endregion

        #region (static) TryDecodeDiffieHellmanKey(RData, out Prime, out Generator, out PublicValue)

        /// <summary>
        /// Decode KEY RDATA holding Diffie-Hellman parameters (RFC 2539 §2).
        /// </summary>
        /// <param name="RData">The public-key octets of a KEY record with algorithm 2.</param>
        /// <param name="Prime">The modulus p.</param>
        /// <param name="Generator">The generator g.</param>
        /// <param name="PublicValue">The peer's public value.</param>
        /// <returns>False when the RDATA is malformed, or names a well-known group this implementation does not carry a table for.</returns>
        public static Boolean TryDecodeDiffieHellmanKey(Byte[]        RData,
                                                        out Byte[]?   Prime,
                                                        out Byte[]?   Generator,
                                                        out Byte[]?   PublicValue)
        {

            Prime        = null;
            Generator    = null;
            PublicValue  = null;

            var offset   = 0;

            if (!TryReadLengthPrefixed(RData, ref offset, out var primeLength, out var prime))
                return false;

            // §2: 1 and 2 are indices into a table of well-known groups rather
            // than literal primes. Nothing here carries that table, and guessing
            // would produce a key the peer never agreed to.
            if (primeLength is 1 or 2)
                return false;

            if (!TryReadLengthPrefixed(RData, ref offset, out _, out var generator) ||
                !TryReadLengthPrefixed(RData, ref offset, out _, out var publicValue))
                return false;

            if (offset != RData.Length)
                return false;

            Prime        = prime;
            Generator    = generator;
            PublicValue  = publicValue;

            return true;

        }

        #endregion


        #region (private static) MD5Digest(Data)

        /// <summary>
        /// The MD5 digest RFC 2930 §4.1 requires.
        /// </summary>
        /// <remarks>
        /// Isolated here so the one unavoidable use of MD5 in this library has a
        /// single place to point at, and so a platform that refuses it fails with
        /// an explanation rather than a bare cryptographic exception.
        /// </remarks>
        private static Byte[] MD5Digest(Byte[] Data)
        {
            try
            {
                return MD5.HashData(Data);
            }
            catch (Exception e)
            {
                throw new PlatformNotSupportedException(
                          "RFC 2930 §4.1 derives TKEY keying material with MD5 and defines no alternative, " +
                          "and this platform does not provide MD5 — a FIPS-restricted host, most likely. " +
                          "Diffie-Hellman TKEY cannot be used here; configure the TSIG key out of band instead.",
                          e);
            }
        }

        #endregion

        #region (private static) XorLeftJustified(Left, Right)

        /// <summary>
        /// XOR two octet strings, left-justifying the shorter and treating its
        /// missing tail as zero — so the result keeps the length of the longer.
        /// </summary>
        private static Byte[] XorLeftJustified(Byte[] Left, Byte[] Right)
        {

            var result = new Byte[Math.Max(Left.Length, Right.Length)];

            for (var i = 0; i < result.Length; i++)
                result[i] = (Byte) ((i < Left. Length ? Left [i] : 0) ^
                                    (i < Right.Length ? Right[i] : 0));

            return result;

        }

        #endregion

        #region (private static) WriteLengthPrefixed / TryReadLengthPrefixed

        private static void WriteLengthPrefixed(Stream Stream, Byte[] Value)
        {
            Stream.WriteUInt16BE((UInt16) Value.Length);
            Stream.Write(Value, 0, Value.Length);
        }

        private static Boolean TryReadLengthPrefixed(Byte[]       Data,
                                                     ref Int32    Offset,
                                                     out UInt16   Length,
                                                     out Byte[]?  Value)
        {

            Length  = 0;
            Value   = null;

            if (Offset + 2 > Data.Length)
                return false;

            Length   = BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(Offset, 2));
            Offset  += 2;

            if (Offset + Length > Data.Length)
                return false;

            Value    = Data[Offset..(Offset + Length)];
            Offset  += Length;

            return true;

        }

        #endregion

    }

}
