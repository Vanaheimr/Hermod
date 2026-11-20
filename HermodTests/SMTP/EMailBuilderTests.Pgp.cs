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

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// PGP/MIME (RFC 3156) as it went wrong: a part the application attaches signed
    /// as it was not sent, micalg for SHA3 in BouncyCastle's spelling, a third part
    /// of a multipart/signed riding along as signed, a broken signature part thrown
    /// at the application, and a ciphertext decrypted whether its MDC held or not.
    /// </summary>
    public partial class EMailBuilderTests
    {


        #region Decrypting: integrity protection (RFC 9580 §5.7, §5.13.1)

        /// <summary>
        /// A multipart/encrypted message to Bob around a ciphertext without compression -
        /// so that a changed octet changes the plaintext and nothing else complains.
        /// </summary>
        private String EncryptedToBob(Boolean IntegrityProtected, Boolean ChangeAnOctet)
        {

            var plaintext = Encoding.ASCII.GetBytes("Content-Type: text/plain; charset=us-ascii\r\n\r\n" +
                                                    String.Join("\r\n", Enumerable.Range(1, 40).Select(i => $"Line {i:00}: pay the invoice to account 4711 as agreed.")));

            using var binary = new MemoryStream();

            var encryption = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Aes256, IntegrityProtected, new SecureRandom());
            encryption.AddMethod(recipientPublicRing.GetPublicKeys().Cast<PgpPublicKey>().FirstOrDefault(key => key.IsEncryptionKey) ?? recipientPublicRing.GetPublicKey());

            using (var encrypted = encryption.Open(binary, new Byte[1 << 12]))
            using (var literal   = new PgpLiteralDataGenerator().Open(encrypted, PgpLiteralData.Binary, "", plaintext.Length, DateTime.UtcNow))
                literal.Write(plaintext);

            var ciphertext = binary.ToArray();

            if (ChangeAnOctet)
                ciphertext[ciphertext.Length / 2] ^= 0x01;

            using var armored = new MemoryStream();
            using (var armor = new ArmoredOutputStream(armored))
                armor.Write(ciphertext);

            return "From: Alice <alice@example.com>\r\n" +
                   "To: Bob <bob@example.org>\r\n" +
                   "Subject: Encrypted\r\n" +
                   "MIME-Version: 1.0\r\n" +
                   "Content-Type: multipart/encrypted; protocol=\"application/pgp-encrypted\"; boundary=\"enc-boundary\"\r\n" +
                   "\r\n" +
                   "--enc-boundary\r\n" +
                   "Content-Type: application/pgp-encrypted\r\n" +
                   "\r\n" +
                   "Version: 1\r\n" +
                   "\r\n" +
                   "--enc-boundary\r\n" +
                   "Content-Type: application/octet-stream\r\n" +
                   "\r\n" +
                   Encoding.ASCII.GetString(armored.ToArray()).Replace("\r\n", "\n").Replace("\n", "\r\n").TrimEnd() + "\r\n" +
                   "--enc-boundary--";

        }

        [Test(Description = "RFC 9580 §5.13.1: \"A mismatch of the hash indicates that the message has been modified and MUST be treated as a security problem\"; §13.7: \"it SHOULD NOT attempt to parse nor release decrypted data to the user\"")]
        public void A_ciphertext_changed_in_transit_is_refused()
        {

            var mail = EMail.Parse(EncryptedToBob(IntegrityProtected: true, ChangeAnOctet: true).Split("\r\n"));

            Assert.Multiple(() => {
                Assert.That(() => mail.DecryptPgp         (recipientSecretRing, Passphrase),                Throws.InstanceOf<PgpException>(), "DecryptPgp");
                Assert.That(() => mail.DecryptAndVerifyPgp(recipientSecretRing, Passphrase, publicKeyRing), Throws.InstanceOf<PgpException>(), "DecryptAndVerifyPgp");
            });

        }

        [Test(Description = "RFC 9580 §5.7: the Symmetrically Encrypted Data packet: \"An implementation SHOULD reject such a packet and stop processing the message\"")]
        public void A_ciphertext_without_integrity_protection_is_refused()
        {

            var mail = EMail.Parse(EncryptedToBob(IntegrityProtected: false, ChangeAnOctet: false).Split("\r\n"));

            Assert.That(() => mail.DecryptPgp(recipientSecretRing, Passphrase), Throws.InstanceOf<PgpException>());

        }

        [Test(Description = "RFC 9580 §5.13.1: a ciphertext whose MDC holds is decrypted")]
        public void A_ciphertext_whose_MDC_holds_is_decrypted()
        {

            var mail = EMail.Parse(EncryptedToBob(IntegrityProtected: true, ChangeAnOctet: false).Split("\r\n"));

            Assert.That(String.Join("\r\n", mail.DecryptPgp(recipientSecretRing, Passphrase).ToText()), Does.Contain("Line 40: pay the invoice to account 4711 as agreed."));

        }

        #endregion

    }

}
