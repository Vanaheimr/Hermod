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

using Org.BouncyCastle.Bcpg.OpenPgp;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.BouncyCastle
{

    /// <summary>
    /// Hermod's OpenPGP with keys as GnuPG 2.4 and OpenPGP.js make them (H4 of
    /// EMailWebApp's ARBEITSPLAN.md): version 4, Ed25519 to sign, Curve25519 to
    /// encrypt, the secret key armored and under a passphrase - read, the key
    /// that can sign chosen, signed and checked, encrypted and opened.
    /// </summary>
    /// <remarks>
    /// The keys were made with GnuPG 2.4 for these tests and nothing else; the
    /// passphrase is hermod-test-passphrase. "Hermod Certify" has a primary key
    /// that only certifies and signs with a subkey, as keys made for a
    /// smartcard often do.
    /// </remarks>
    [TestFixture]
    public class OpenPGPTests
    {

        private const String Passphrase = "hermod-test-passphrase";

        private const String SecretKey = """
            -----BEGIN PGP PRIVATE KEY BLOCK-----
            
            lIYEatEqlxYJKwYBBAHaRw8BAQdARLYdEIPLH3c1LH6f8B25bm110r4+PAq7OAKq
            WHsZBt/+BwMCTmd0rcC5bAfyH9SPqCu+egp9MKNn8ZQLNv4jixpY7BgdQOBYSVCf
            3tGhachs8PGacxPOkSsOgzSCicL6qG7uGAyzHaNBtuJ9zsOIVFOrVLQmSGVybW9k
            IFRlc3QgPGhlcm1vZC10ZXN0QGV4YW1wbGUudGVzdD6IkAQTFgoAOBYhBBbEgaem
            6hxXf4zV9uw61rtyp+FLBQJq0SqXAhsDBQsJCAcCBhUKCQgLAgQWAgMBAh4BAheA
            AAoJEOw61rtyp+FLrEQBAJCD1MdA8mb9mcMdn2IsLaTEhid+0ku+8F/RU9+DLW22
            AQDFtcptl0tjwX9RyQZjT/5Fg1Cv9IN/NgmypwvP1s6UBpyLBGrRKpgSCisGAQQB
            l1UBBQEBB0BaHnAko+OB/OCJdWGDqZebI4w4Q2TMxniAiKPeuI5HLwMBCAf+BwMC
            H77iAQLhkAXyVsiBuIGUQCE2egfUf5DUv5nousvSDxQL3K0x8fVz7bXfgooi8xFY
            r6CvmO8wm+BA7eZygpqcagiHLOBdRzhMRgFTe+/iRYh4BBgWCgAgFiEEFsSBp6bq
            HFd/jNX27DrWu3Kn4UsFAmrRKpgCGwwACgkQ7DrWu3Kn4UsCqAEA82/HbsVFuONA
            PfX2uTb68mt/BKrcnpqYDVWFBSI1nbAA/0AUBadguxfq4lsSSfGIQ3rRyup5pWGb
            eP5CqausR4sO
            =ciRa
            -----END PGP PRIVATE KEY BLOCK-----
            """;

        private const String PublicKey = """
            -----BEGIN PGP PUBLIC KEY BLOCK-----
            
            mDMEatEqlxYJKwYBBAHaRw8BAQdARLYdEIPLH3c1LH6f8B25bm110r4+PAq7OAKq
            WHsZBt+0Jkhlcm1vZCBUZXN0IDxoZXJtb2QtdGVzdEBleGFtcGxlLnRlc3Q+iJAE
            ExYKADgWIQQWxIGnpuocV3+M1fbsOta7cqfhSwUCatEqlwIbAwULCQgHAgYVCgkI
            CwIEFgIDAQIeAQIXgAAKCRDsOta7cqfhS6xEAQCQg9THQPJm/ZnDHZ9iLC2kxIYn
            ftJLvvBf0VPfgy1ttgEAxbXKbZdLY8F/UckGY0/+RYNQr/SDfzYJsqcLz9bOlAa4
            OARq0SqYEgorBgEEAZdVAQUBAQdAWh5wJKPjgfzgiXVhg6mXmyOMOENkzMZ4gIij
            3riORy8DAQgHiHgEGBYKACAWIQQWxIGnpuocV3+M1fbsOta7cqfhSwUCatEqmAIb
            DAAKCRDsOta7cqfhSwKoAQDzb8duxUW440A99fa5Nvrya38EqtyempgNVYUFIjWd
            sAD/QBQFp2C7F+riWxJJ8YhDetHK6nmlYZt4/kKpq6xHiw4=
            =xxAv
            -----END PGP PUBLIC KEY BLOCK-----
            """;

        private const String CertifyOnlySecretKey = """
            -----BEGIN PGP PRIVATE KEY BLOCK-----
            
            lIYEatEqmBYJKwYBBAHaRw8BAQdAQaLYklFl4WLls5MTOIvBlj9arKGusrteQvE2
            fc21uab+BwMCMegVYT7ZKOHyfHTySBLQWaUNKz+Zfn+IXDm5DJ2DYZWeXzNVmVam
            2lP91bJ426xHrc1XiYlgfZTpcAZHPAawTUPXs8fo4GmswEEWGBklhbQsSGVybW9k
            IENlcnRpZnkgPGhlcm1vZC1jZXJ0aWZ5QGV4YW1wbGUudGVzdD6IkAQTFgoAOBYh
            BE+/ol1X2PRT9/SlW/1QXpKDs915BQJq0SqYAhsBBQsJCAcCBhUKCQgLAgQWAgMB
            Ah4BAheAAAoJEP1QXpKDs915Y/oBANOzfuuaxEfsMwMG8ZYlM9Tk+S6X/GTrRIgN
            PP+O+tBZAQCZdaIQ79UmmkknchOb9Wc6Z/tdzpGQuuDIrf81U5bdAZyGBGrRKpkW
            CSsGAQQB2kcPAQEHQKAI6sqJe+oGi1J/sF/7JpZxqtCQDbmOwJvKlXL5ceE0/gcD
            AoNntrfmL95H8mJ2Q0dACfUnpTAytlTGRo92yxuhn1xFHo267zlWoBAfVq0BQTcX
            17gTPJI8ULU9Fose5SzIYb29xhsTjg5GvBgDpItpXzGI7wQYFgoAIBYhBE+/ol1X
            2PRT9/SlW/1QXpKDs915BQJq0SqZAhsCAIEJEP1QXpKDs915diAEGRYKAB0WIQTt
            2Frzk7ls2RCa9hC48clxN+Oo2wUCatEqmQAKCRC48clxN+Oo2/kSAQCEAjvmCe3t
            OyQNCe1Uy3zobhxTpw/PYToGMnvC+t7TewD/aBtMxIwPRZ8URvBR8vAokBo7ICLV
            5tGayulqdLSqgQzJhgEA2q/jiu7LBY6PgnwSafgjwzGxgNrGV8nGRXqz07LPqEYB
            AIxBLkGU/x7/Rx8F20Veva4Bzo7b0/+3Tej5kqQMXXADnIsEatEqmRIKKwYBBAGX
            VQEFAQEHQJvADjBJzyYexU39Zgiz6cuHpmlzSVA5S65xHgtk6qcTAwEIB/4HAwK/
            0x2xX5uQd/KeMdABya43VodrO2NoYAF/c045UqQ7uftg8H1Ln8oksdw9eww0BDRX
            fySM/QIq0ZeSCq+Dxen8UcVYzKCpgPhSeDZ8QwIDiHgEGBYKACAWIQRPv6JdV9j0
            U/f0pVv9UF6Sg7PdeQUCatEqmQIbDAAKCRD9UF6Sg7PdeZt7AP9SBD4iYsuJg97X
            XOWQ1J2tcj28RNiVCDZJ09Nz5nSzNQEAinPaK9wDMGW6lE2AJpciVueKfT9q3luj
            rmN3vJ3JFwU=
            =384K
            -----END PGP PRIVATE KEY BLOCK-----
            """;


        [Test]
        public void An_armored_secret_key_is_read()
        {

            var ring = OpenPGP.ReadSecretKeyRing(SecretKey);

            Assert.Multiple(() => {
                Assert.That(ring.GetSecretKeys().Cast<PgpSecretKey>().Count(),     Is.EqualTo(2), "the primary key and its encryption subkey");
                Assert.That(ring.GetPublicKey().GetUserIds().Cast<String>().First(), Is.EqualTo("Hermod Test <hermod-test@example.test>"));
            });

        }

        [Test]
        public void The_key_that_signs_is_chosen()
        {

            var primary  = OpenPGP.ReadSecretKey(SecretKey);
            var subkey   = OpenPGP.ReadSecretKey(CertifyOnlySecretKey);

            Assert.Multiple(() => {
                Assert.That(primary?.IsSigningKey,   Is.True);
                Assert.That(primary?.IsMasterKey,    Is.True,  "a primary key that signs");
                Assert.That(subkey?.IsSigningKey,    Is.True);
                Assert.That(subkey?.IsMasterKey,     Is.False, "the signing subkey of a primary key that only certifies");
            });

        }

        [Test]
        public void An_Ed25519_signature_is_made_and_checked()
        {

            var data       = Encoding.UTF8.GetBytes("Content-Type: text/plain\r\n\r\nSigniert mit Ed25519.\r\n");
            var signature  = OpenPGP.CreateSignature(new MemoryStream(data), OpenPGP.ReadSecretKey(SecretKey)!, Passphrase, Org.BouncyCastle.Bcpg.HashAlgorithmTag.Sha256);

            using var encoded = new MemoryStream();
            signature.Encode(encoded);

            var publicKeys = OpenPGP.ReadPublicKeyRingBundle(PublicKey);
            var changed    = data.ToArray();
            changed[^3]    = (Byte) '?';

            Assert.Multiple(() => {
                Assert.That(OpenPGP.VerifyDetachedSignature(data,    new MemoryStream(encoded.ToArray()), publicKeys).Status, Is.EqualTo(PgpVerificationStatus.Valid));
                Assert.That(OpenPGP.VerifyDetachedSignature(changed, new MemoryStream(encoded.ToArray()), publicKeys).Status, Is.EqualTo(PgpVerificationStatus.Invalid));
            });

        }

        [Test]
        public void A_message_is_encrypted_to_Curve25519_signed_and_opened()
        {

            var plaintext  = Encoding.UTF8.GetBytes("Verschlüsselt an Curve25519, signiert mit Ed25519.");
            var encryption = OpenPGP.ReadPublicKeyRing(PublicKey).GetPublicKeys().Cast<PgpPublicKey>().First(key => key.IsEncryptionKey);

            using var ciphertext = new MemoryStream();
            OpenPGP.EncryptSignAndZip(new MemoryStream(plaintext), (UInt64) plaintext.Length, OpenPGP.ReadSecretKey(SecretKey)!, Passphrase, encryption, ciphertext);

            var (opened, signature) = OpenPGP.DecryptAndVerify(new MemoryStream(ciphertext.ToArray()),
                                                               OpenPGP.ReadSecretKeyRingBundle(SecretKey),
                                                               Passphrase,
                                                               OpenPGP.ReadPublicKeyRingBundle(PublicKey));

            Assert.Multiple(() => {
                Assert.That(opened,            Is.EqualTo(plaintext));
                Assert.That(signature.Status,  Is.EqualTo(PgpVerificationStatus.Valid));
            });

        }


        #region Integrity protection (RFC 9580 §5.7, §5.13.1)

        /// <summary>
        /// A plaintext long enough that the octet in the middle of its ciphertext lies inside
        /// the literal data, far from the signature and the MDC at the end.
        /// </summary>
        private static Byte[] LongPlaintext
            => Encoding.ASCII.GetBytes(String.Join("\r\n", Enumerable.Range(1, 40).Select(i => $"Line {i:00}: pay the invoice to account 4711 as agreed.")));

        /// <summary>
        /// The plaintext encrypted to the Curve25519 key without compression - so that a
        /// changed octet changes the plaintext and nothing else complains - in a SEIPD
        /// packet with its MDC or in the obsolete SED packet without one, and signed
        /// with the Ed25519 key (one-pass) if asked to.
        /// </summary>
        private static Byte[] Encrypt(Byte[] Plaintext, Boolean IntegrityProtected, Boolean Signed)
        {

            using var output = new MemoryStream();

            var encryption = new PgpEncryptedDataGenerator(Org.BouncyCastle.Bcpg.SymmetricKeyAlgorithmTag.Aes256, IntegrityProtected, new Org.BouncyCastle.Security.SecureRandom());
            encryption.AddMethod(OpenPGP.ReadPublicKeyRing(PublicKey).GetPublicKeys().Cast<PgpPublicKey>().First(key => key.IsEncryptionKey));

            using (var encrypted = encryption.Open(output, new Byte[1 << 12]))
            {

                PgpSignatureGenerator? signer = null;

                if (Signed)
                {
                    var key = OpenPGP.ReadSecretKey(SecretKey)!;
                    signer  = new PgpSignatureGenerator(key.PublicKey.Algorithm, Org.BouncyCastle.Bcpg.HashAlgorithmTag.Sha256);
                    signer.InitSign(PgpSignature.BinaryDocument, key.ExtractPrivateKey(Passphrase.ToCharArray()));
                    signer.GenerateOnePassVersion(false).Encode(encrypted);
                }

                using (var literal = new PgpLiteralDataGenerator().Open(encrypted, PgpLiteralData.Binary, "", Plaintext.Length, DateTime.UtcNow))
                    literal.Write(Plaintext);

                if (signer is not null)
                {
                    signer.Update(Plaintext);
                    signer.Generate().Encode(encrypted);
                }

            }

            return output.ToArray();

        }

        private static (Byte[] Plaintext, PgpSignatureVerification Signature) Open(Byte[] Ciphertext)

            => OpenPGP.DecryptAndVerify(new MemoryStream(Ciphertext),
                                        OpenPGP.ReadSecretKeyRingBundle(SecretKey),
                                        Passphrase,
                                        OpenPGP.ReadPublicKeyRingBundle(PublicKey));


        [TestCase(false, Description = "not signed")]
        [TestCase(true,  Description = "signed")]
        [Test(Description = "RFC 9580 §5.13.1: \"A mismatch of the hash indicates that the message has been modified and MUST be treated as a security problem\"; §13.7: \"it SHOULD NOT attempt to parse nor release decrypted data to the user\"")]
        public void A_ciphertext_changed_in_transit_is_not_decrypted(Boolean Signed)
        {

            var ciphertext = Encrypt(LongPlaintext, IntegrityProtected: true, Signed);
            ciphertext[ciphertext.Length / 2] ^= 0x01;

            Assert.Multiple(() => {
                Assert.That(() => Open(ciphertext),                                                                                     Throws.InstanceOf<PgpException>(), "DecryptAndVerify");
                Assert.That(() => OpenPGP.Decrypt(new MemoryStream(ciphertext), OpenPGP.ReadSecretKeyRingBundle(SecretKey), Passphrase), Throws.InstanceOf<PgpException>(), "Decrypt");
            });

        }

        [TestCase(false, Description = "not signed")]
        [TestCase(true,  Description = "signed")]
        [Test(Description = "RFC 9580 §5.7: the Symmetrically Encrypted Data packet: \"An implementation SHOULD reject such a packet and stop processing the message\"")]
        public void A_ciphertext_without_integrity_protection_is_not_decrypted(Boolean Signed)
        {

            var ciphertext = Encrypt(LongPlaintext, IntegrityProtected: false, Signed);

            Assert.That(() => Open(ciphertext), Throws.InstanceOf<PgpException>());

        }

        [TestCase(false, Description = "not signed")]
        [TestCase(true,  Description = "signed")]
        [Test(Description = "RFC 9580 §5.13.1: a SEIPD packet whose MDC holds decrypts - with the signature it carries")]
        public void An_unchanged_ciphertext_with_its_MDC_is_decrypted(Boolean Signed)
        {

            var (plaintext, signature) = Open(Encrypt(LongPlaintext, IntegrityProtected: true, Signed));

            Assert.Multiple(() => {
                Assert.That(plaintext,         Is.EqualTo(LongPlaintext));
                Assert.That(signature.Status,  Is.EqualTo(Signed ? PgpVerificationStatus.Valid : PgpVerificationStatus.NoSignature));
            });

        }

        #endregion

    }

}
