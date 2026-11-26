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

using System.Text;
using System.Buffers.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The "aes128gcm" content coding (RFC 8188): the two examples of its
    /// Section 3 to the octet, both ways, and everything Section 2 makes a
    /// decrypter fail on.
    ///
    /// The bodies a test needs that no encrypter would write — a record with no
    /// delimiter, a last record with the delimiter 1 — are sealed by this
    /// fixture's own <see cref="Seal"/>, which derives key and nonces from
    /// Sections 2.2 and 2.3 with HMAC-SHA-256 itself, not with HKDF as the
    /// coding does, and which is held to the RFC's examples and intermediate
    /// values first.
    /// </summary>
    [TestFixture]
    public class EncryptedContentCodingTests
    {

        #region Data

        private static Byte[] B64(String Text)
            => Base64Url.DecodeFromChars(Text.AsSpan());

        private static readonly Byte[] Walrus      = "I am the walrus"u8.ToArray();

        // Section 3.1: one record of 4096, no keyid.
        private static readonly Byte[] Key31       = B64("yqdlZ-tYemfogSmv7Ws5PQ");
        private static readonly Byte[] Salt31      = B64("I1BsxtFttlv3u_Oo94xnmw");
        private static readonly Byte[] Body31      = B64("I1BsxtFttlv3u_Oo94xnmwAAEAAA-NAVub2qFgBEuQKRapoZu-IxkIva3MEB1PD-ly8Thjg");
        private static readonly Byte[] PRK31       = B64("zyeH5phsIsgUyd4oiSEIy35x-gIi4aM7y0hCF8mwn9g");
        private static readonly Byte[] CEK31       = B64("_wniytB-ofscZDh4tbSjHw");
        private static readonly Byte[] Nonce31     = B64("Bcs8gkIRKLI8GeI8");

        // Section 3.2: records of 25, the first with one octet of padding, keyid "a1".
        private static readonly Byte[] Key32       = B64("BO3ZVPxUlnLORbVGMpbT1Q");
        private static readonly Byte[] Body32      = B64("uNCkWiNYzKTnBN9ji3-qWAAAABkCYTHOG8chz_gnvgOqdGYovxyjuqRyJFjEDyoF1Fvkj6hQPdPHI51OEUKEpgz3SsLWIqS_uA");

        #endregion

        #region (private) Seal(Key, Salt, RecordSize, KeyId, Records)

        // Sections 2.2 and 2.3: the info of HKDF, a zero octet, and the 0x01 of
        // its one round of Expand.
        private static readonly Byte[] KeyInfo    = [.. "Content-Encoding: aes128gcm"u8, 0x00, 0x01];
        private static readonly Byte[] NonceInfo  = [.. "Content-Encoding: nonce"u8,    0x00, 0x01];

        private static (Byte[] PRK, Byte[] CEK, Byte[] Nonce) Derive(Byte[] Key, Byte[] Salt)
        {

            var prk    = HMACSHA256.HashData(Salt, Key);
            var cek    = HMACSHA256.HashData(prk, KeyInfo)[..16];
            var nonce  = HMACSHA256.HashData(prk, NonceInfo)[..12];

            return (prk, cek, nonce);

        }

        /// <summary>
        /// A body of these records, each one's plaintext exactly as given —
        /// content, delimiter and padding, or whatever else a test needs.
        /// </summary>
        private static Byte[] Seal(Byte[]            Key,
                                   Byte[]            Salt,
                                   UInt32            RecordSize,
                                   Byte[]            KeyId,
                                   params Byte[][]   Records)
        {

            var (_, cek, nonceBase) = Derive(Key, Salt);

            var header = new Byte[21 + KeyId.Length];
            Salt.CopyTo(header, 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
            header[20] = (Byte) KeyId.Length;
            KeyId.CopyTo(header, 21);

            var body = new List<Byte>(header);

            using var aes = new AesGcm(cek, 16);

            for (var sequence = 0; sequence < Records.Length; sequence++)
            {

                var nonce = (Byte[]) nonceBase.Clone();
                nonce[11] ^= (Byte) sequence;

                var ciphertext = new Byte[Records[sequence].Length];
                var tag        = new Byte[16];

                aes.Encrypt(nonce, Records[sequence], ciphertext, tag);

                body.AddRange(ciphertext);
                body.AddRange(tag);

            }

            return [.. body];

        }

        private static Byte[] Bytes(String Text, params Byte[] Tail)
            => [.. Encoding.ASCII.GetBytes(Text), .. Tail];

        private static void AssertInvalid(Action Decrypt, String Because)
        {
            // Throws is exact: a subtype of InvalidDataException, or an IOException, fails it.
            Assert.Throws<InvalidDataException>(() => Decrypt(), Because);
        }

        #endregion


        #region TheFixturesOwnSeal_ReproducesBothExamples()

        /// <summary>
        /// The fixture's own derivation and sealing, held to Section 3: the
        /// intermediate values of 3.1, and both bodies to the octet — 3.2 as
        /// "7 octets of message in the first record and 8 in the second", the
        /// first with one 0x00 of padding after its delimiter.
        /// </summary>
        [Test]
        public void TheFixturesOwnSeal_ReproducesBothExamples()
        {

            var (prk, cek, nonce) = Derive(Key31, Salt31);

            Assert.Multiple(() => {

                Assert.That(prk,    Is.EqualTo(PRK31),    "PRK of 3.1");
                Assert.That(cek,    Is.EqualTo(CEK31),    "CEK of 3.1");
                Assert.That(nonce,  Is.EqualTo(Nonce31),  "NONCE of 3.1");

                Assert.That(Seal(Key31, Salt31, 4096, [], Bytes("I am the walrus", 0x02)),
                            Is.EqualTo(Body31),
                            "the body of 3.1");

                Assert.That(Seal(Key32, Body32[..16], 25, "a1"u8.ToArray(), Bytes("I am th", 0x01, 0x00), Bytes("e walrus", 0x02)),
                            Is.EqualTo(Body32),
                            "the body of 3.2");

            });

        }

        #endregion

        #region Example31_IsEncryptedToTheOctet()

        [Test]
        public void Example31_IsEncryptedToTheOctet()

            => Assert.That(HTTPEncryptedContentCoding.Encrypt(Walrus, Key31, RecordSize: 4096, Salt: Salt31),
                           Is.EqualTo(Body31));

        #endregion

        #region BothExamples_Decrypt(Example)

        [TestCase("3.1")]
        [TestCase("3.2")]
        public void BothExamples_Decrypt(String Example)

            => Assert.That(Example == "3.1" ? HTTPEncryptedContentCoding.Decrypt(Body31, Key31, 1024)
                                            : HTTPEncryptedContentCoding.Decrypt(Body32, Key32, 1024),
                           Is.EqualTo(Walrus));

        #endregion

        #region TheKeyIsTheOneTheKeyIdNames()

        /// <summary>
        /// The keyid is read from the header and handed to whoever holds the
        /// keys; one it has no key for is a KeyNotFoundException, not a body
        /// that fails to decrypt.
        /// </summary>
        [Test]
        public void TheKeyIsTheOneTheKeyIdNames()
        {

            var asked = new List<String>();

            Byte[]? KeyFor(Byte[] KeyId)
            {
                asked.Add(Encoding.UTF8.GetString(KeyId));
                return KeyId.AsSpan().SequenceEqual("a1"u8) ? Key32 : null;
            }

            Assert.Multiple(() => {
                Assert.That(HTTPEncryptedContentCoding.ReadKeyId(Body32),              Is.EqualTo("a1"u8.ToArray()),  "the keyid of 3.2");
                Assert.That(HTTPEncryptedContentCoding.ReadKeyId(Body31),              Is.Empty,                      "the keyid of 3.1");
                Assert.That(HTTPEncryptedContentCoding.Decrypt(Body32, KeyFor, 1024),  Is.EqualTo(Walrus),            "3.2, with the key for a1");
                Assert.Throws<KeyNotFoundException>(() => HTTPEncryptedContentCoding.Decrypt(Body31, KeyFor, 1024),  "3.1, for whose keyid there is no key");
                Assert.That(asked,                                                     Is.EqualTo(new[] { "a1", "" }), "the keyids asked for");
            });

        }

        #endregion

        #region RoundTrips(Length, RecordSize)

        /// <summary>
        /// Any length, in records of any size: each record but the last carries
        /// rs - 17 octets, the last what is left, an empty body one record of
        /// just its delimiter. The body is exactly as long as that.
        /// </summary>
        [TestCase(     0,    18)]
        [TestCase(     1,    18)]
        [TestCase(     5,    18)]
        [TestCase(     0,    25)]
        [TestCase(     7,    25)]
        [TestCase(     8,    25)]
        [TestCase(     9,    25)]
        [TestCase(    16,    25)]
        [TestCase(    17,    25)]
        [TestCase(  1000,    25)]
        [TestCase(     0,  4096)]
        [TestCase(  4079,  4096)]
        [TestCase(  4080,  4096)]
        [TestCase(100000,  4096)]
        public void RoundTrips(Int32 Length, Int32 RecordSize)
        {

            var plaintext  = new Byte[Length];
            new Random(Length).NextBytes(plaintext);

            var key        = RandomNumberGenerator.GetBytes(16);
            var body       = HTTPEncryptedContentCoding.Encrypt(plaintext, key, "k"u8, (UInt32) RecordSize);

            var perRecord  = RecordSize - 17;
            var records    = Math.Max(1, (Length + perRecord - 1) / perRecord);

            Assert.Multiple(() => {
                Assert.That(body.Length,                                                       Is.EqualTo(21 + 1 + Length + records * 17),  "the length of the body");
                Assert.That(HTTPEncryptedContentCoding.Decrypt(body, key, Length),             Is.EqualTo(plaintext),                       "it decrypts");
            });

        }

        #endregion

        #region EveryEncryptionHasItsOwnSalt()

        /// <summary>
        /// Section 2.1: a salt MUST NOT be used twice with the same key, and a
        /// random one each time makes that "highly unlikely".
        /// </summary>
        [Test]
        public void EveryEncryptionHasItsOwnSalt()
        {

            var first   = HTTPEncryptedContentCoding.Encrypt(Walrus, Key31);
            var second  = HTTPEncryptedContentCoding.Encrypt(Walrus, Key31);

            Assert.Multiple(() => {
                Assert.That(first[..16],                                                Is.Not.EqualTo(second[..16]),  "the salts");
                Assert.That(HTTPEncryptedContentCoding.Decrypt(first,  Key31, 1024),    Is.EqualTo(Walrus));
                Assert.That(HTTPEncryptedContentCoding.Decrypt(second, Key31, 1024),    Is.EqualTo(Walrus));
            });

        }

        #endregion

        #region PaddingAfterTheDelimiterIsDropped()

        /// <summary>
        /// The delimiter is the last octet that is not zero: any zeros after it
        /// are padding, in every record.
        /// </summary>
        [Test]
        public void PaddingAfterTheDelimiterIsDropped()
        {

            var body = Seal(Key31, Salt31, 20, [], Bytes("ab", 0x01, 0x00), Bytes("c", 0x02, 0x00, 0x00));

            Assert.That(HTTPEncryptedContentCoding.Decrypt(body, Key31, 1024), Is.EqualTo("abc"u8.ToArray()));

        }

        #endregion

        #region AnInvalidBody_IsInvalidData(Case)

        /// <summary>
        /// Everything Section 2 makes a decrypter fail on, and every body that is
        /// shorter than it says, fails with an InvalidDataException.
        /// </summary>
        [TestCase("cut off after a record")]
        [TestCase("cut off within a record")]
        [TestCase("a bit flipped")]
        [TestCase("records swapped")]
        [TestCase("under another key")]
        [TestCase("a header and no record")]
        [TestCase("shorter than its header")]
        [TestCase("a keyid beyond the body")]
        [TestCase("a record size of 17")]
        [TestCase("a last record of just its tag")]
        [TestCase("a last record shorter than a tag")]
        [TestCase("a record of zeros")]
        [TestCase("a last record with the delimiter 1")]
        [TestCase("an earlier record with the delimiter 2")]
        [TestCase("a last record with the delimiter 3")]
        public void AnInvalidBody_IsInvalidData(String Case)
        {

            var header32 = 21 + 2;

            (Byte[] Body, Byte[] Key) input = Case switch {

                "cut off after a record"                  => (Body32[..(header32 + 25)],                                       Key32),
                "cut off within a record"                 => (Body32[..^1],                                                    Key32),
                "a bit flipped"                           => (Flip(Body32, header32 + 3),                                      Key32),
                "records swapped"                         => ([.. Body32[..header32], .. Body32[(header32 + 25)..], .. Body32[header32..(header32 + 25)]], Key32),
                "under another key"                       => (Body31,                                                          Key32),
                "a header and no record"                  => (Body31[..21],                                                    Key31),
                "shorter than its header"                 => (Body31[..20],                                                    Key31),
                "a keyid beyond the body"                 => ([.. Body31[..20], 200, .. Body31[21..]],                         Key31),
                "a record size of 17"                     => (Seal(Key31, Salt31, 17,   [], [ 0x01 ], [ 0x02 ]),             Key31),
                "a last record of just its tag"           => (Seal(Key31, Salt31, 4096, [], Array.Empty<Byte>()),              Key31),
                "a last record shorter than a tag"        => (Body32[..(header32 + 25 + 10)],                                  Key32),
                "a record of zeros"                       => (Seal(Key31, Salt31, 4096, [], [ 0x00, 0x00, 0x00 ]),             Key31),
                "a last record with the delimiter 1"      => (Seal(Key31, Salt31, 4096, [], Bytes("abc", 0x01)),               Key31),
                "an earlier record with the delimiter 2"  => (Seal(Key31, Salt31, 20,   [], Bytes("abc", 0x02), Bytes("d", 0x02)), Key31),
                "a last record with the delimiter 3"      => (Seal(Key31, Salt31, 4096, [], Bytes("abc", 0x03)),               Key31),
                _                                         => throw new ArgumentException(Case)

            };

            AssertInvalid(() => HTTPEncryptedContentCoding.Decrypt(input.Body, input.Key, 1024), Case);

        }

        private static Byte[] Flip(Byte[] Body, Int32 At)
        {
            var flipped = (Byte[]) Body.Clone();
            flipped[At] ^= 0x01;
            return flipped;
        }

        #endregion

        #region ADecryptionOverTheLimit_IsRefused()

        [Test]
        public void ADecryptionOverTheLimit_IsRefused()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPEncryptedContentCoding.Decrypt(Body31, Key31, Walrus.Length), Is.EqualTo(Walrus), "at the limit");
                AssertInvalid(() => HTTPEncryptedContentCoding.Decrypt(Body31, Key31, Walrus.Length - 1),         "one octet over it");
            });

        }

        #endregion

        #region Encrypt_RefusesWhatTheRFCDoesNotAllow()

        [Test]
        public void Encrypt_RefusesWhatTheRFCDoesNotAllow()
        {

            Assert.Multiple(() => {
                Assert.Throws<ArgumentOutOfRangeException>(() => HTTPEncryptedContentCoding.Encrypt(Walrus, Key31, RecordSize: 17),          "a record size of 17");
                Assert.Throws<ArgumentOutOfRangeException>(() => HTTPEncryptedContentCoding.Encrypt(Walrus, Key31, KeyId: new Byte[256]),    "a keyid of 256 octets");
                Assert.Throws<ArgumentException>          (() => HTTPEncryptedContentCoding.Encrypt(Walrus, Key31, Salt:  new Byte[15]),     "a salt of 15 octets");
            });

        }

        #endregion

        #region AReceivedBody_IsLeftAsItCame()

        /// <summary>
        /// The key is not the recipient's to know (Section 2), so the automatic
        /// decoding leaves a body of this coding alone, as it does any coding it
        /// cannot undo, rather than handing over ciphertext as the representation.
        /// </summary>
        [Test]
        public void AReceivedBody_IsLeftAsItCame()
        {

            var response = HTTPResponse.Parse("HTTP/1.1 200 OK\r\n" +
                                              "Content-Encoding: aes128gcm\r\n" +
                                             $"Content-Length: {Body31.Length}\r\n\r\n",
                                              Body31);

            Assert.Multiple(() => {
                Assert.That(HTTPContentCoding.IsSupported("aes128gcm"),                   Is.False);
                Assert.That(response.TryDecodeBody(out var body, out var error),          Is.False);
                Assert.That(error,                                                        Does.Contain("aes128gcm"));
                Assert.That(response.HTTPBody,                                            Is.EqualTo(Body31));
            });

        }

        #endregion

    }

}
