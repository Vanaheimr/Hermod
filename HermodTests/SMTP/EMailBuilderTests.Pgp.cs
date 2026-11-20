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

        #region (helpers) Signed(...), SignedPart(...)

        private EMail Signed(EMailBodypart? Attachment = null, HashAlgorithmTag Hash = HashAlgorithmTag.Sha256)
        {

            var builder = new TextEMailBuilder {
                              From           = new EMailAddress("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing),
                              Subject        = "Signiert",
                              Text           = $"Notes attached. {Marker}",
                              SecurityLevel  = EMailSecurity.sign,
                              HashAlgorithm  = Hash,
                              Passphrase     = Passphrase
                          };

            builder.To.Add(Address("bob@example.org"));

            if (Attachment is not null)
                builder.AddAttachment<TextEMailBuilder>(Attachment);

            return builder;

        }

        /// <summary>
        /// The first part of the multipart/signed, as it was sent.
        /// </summary>
        private static String[] SignedPart(String Text)
            => EMail.Parse(Text.Split("\r\n")).Body.NestedBodyparts.First().RawContent!.ToArray();

        #endregion


        #region Signing: what is signed is what is sent (RFC 3156 §3, §5)

        [Test(Description = "RFC 3156 §3: \"implementations MUST make sure that no trailing whitespace is present after the MIME encoding has been applied\"; §5: \"(signed message and transmitted message MUST be identical)\"")]
        public void An_attached_text_part_with_trailing_white_space_is_signed_as_sent()
        {

            var attachment = new EMailBodypart(ContentTypeBuilder:       _ => new MailContentType(MailContentTypes.text_plain) { CharSet = "us-ascii" },
                                               ContentTransferEncoding:  "7bit",
                                               ContentDisposition:       "attachment; filename=\"notes.txt\"",
                                               Content:                  [ "Item 1   ", "Item 2\t" ]);

            var text     = Text(Signed(attachment));
            var parsed   = EMail.Parse(text.Split("\r\n"));
            var sent     = parsed.Body.NestedBodyparts.First().NestedBodyparts.Last();
            var verified = parsed.VerifyPgpSignature(publicKeyRing);

            Assert.Multiple(() => {
                Assert.That(verified.Status,                                                                                    Is.EqualTo(PgpVerificationStatus.Valid), "the signature over the part as sent");
                Assert.That(SignedPart(text).Where(line => line.EndsWith(' ') || line.EndsWith('\t')),                         Is.Empty, "no trailing white space");
                Assert.That(sent.ContentTransferEncoding,                                                                       Is.EqualTo("quoted-printable"));
                Assert.That(Encoding.ASCII.GetString(TransferEncodings.Decode(Encoding.ASCII.GetBytes(String.Join("\r\n", sent.MailBody)), sent.ContentTransferEncoding)),
                            Is.EqualTo("Item 1   \r\nItem 2\t"), "the text as it was");
                Assert.That(sent.ContentDisposition,                                                                            Is.EqualTo("attachment; filename=\"notes.txt\""));
            });

        }

        [Test(Description = "RFC 3156 §3: \"all data signed according to this protocol MUST be constrained to 7 bits (8-bit data MUST be encoded using either Quoted-Printable or Base64)\"")]
        public void An_attached_8bit_text_part_is_signed_in_7bit()
        {

            var attachment = new EMailBodypart(ContentTypeBuilder:       _ => new MailContentType(MailContentTypes.text_plain) { CharSet = "utf-8" },
                                               ContentTransferEncoding:  "8bit",
                                               ContentDisposition:       "attachment; filename=\"notes.txt\"",
                                               Content:                  [ "Grüße aus Jena", "Item 2" ]);

            var text     = Text(Signed(attachment));
            var parsed   = EMail.Parse(text.Split("\r\n"));
            var sent     = parsed.Body.NestedBodyparts.First().NestedBodyparts.Last();

            Assert.Multiple(() => {
                Assert.That(SignedPart(text).SelectMany(line => line).All(c => c < 128),     Is.True, "7 bit");
                Assert.That(parsed.VerifyPgpSignature(publicKeyRing).Status,                 Is.EqualTo(PgpVerificationStatus.Valid));
                Assert.That(Encoding.UTF8.GetString(TransferEncodings.Decode(Encoding.ASCII.GetBytes(String.Join("\r\n", sent.MailBody)), sent.ContentTransferEncoding)),
                            Is.EqualTo("Grüße aus Jena\r\nItem 2"), "the text as it was");
            });

        }

        [Test(Description = "RFC 3156 §3: the encoding is applied to what is signed and sent - the part the application gave stays as it is, and a part that needs no encoding is sent as it is")]
        public void The_attachment_the_application_gave_is_not_changed_by_signing()
        {

            var attachment = new EMailBodypart(ContentTypeBuilder:       _ => new MailContentType(MailContentTypes.text_plain) { CharSet = "us-ascii" },
                                               ContentTransferEncoding:  "7bit",
                                               ContentDisposition:       "attachment; filename=\"notes.txt\"",
                                               Content:                  [ "Item 1   ", "Item 2" ]);

            var plain      = new EMailBodypart(ContentTypeBuilder:       _ => new MailContentType(MailContentTypes.text_plain) { CharSet = "us-ascii" },
                                               ContentTransferEncoding:  "7bit",
                                               ContentDisposition:       "attachment; filename=\"plain.txt\"",
                                               Content:                  [ "Item 1", "Item 2" ]);

            var text       = Text(Signed(attachment));
            var plainText  = Text(Signed(plain));

            Assert.Multiple(() => {
                Assert.That(attachment.ContentTransferEncoding,                                       Is.EqualTo("7bit"));
                Assert.That(attachment.MailBody,                                                      Is.EqualTo(new[] { "Item 1   ", "Item 2" }));
                Assert.That(EMail.Parse(plainText.Split("\r\n")).Body.NestedBodyparts.First().NestedBodyparts.Last().ContentTransferEncoding,
                            Is.EqualTo("7bit"), "7-bit text without trailing white space stays as it is");
                Assert.That(EMail.Parse(plainText.Split("\r\n")).VerifyPgpSignature(publicKeyRing).Status, Is.EqualTo(PgpVerificationStatus.Valid));
            });

        }

        [TestCase(HashAlgorithmTag.Sha3_256, "pgp-sha3-256")]
        [TestCase(HashAlgorithmTag.Sha3_512, "pgp-sha3-512")]
        [TestCase(HashAlgorithmTag.Sha256,   "pgp-sha256")]
        [Test(Description = "RFC 3156 §5: \"Hash-symbols are constructed from the text names registered in [1] ... by converting the text name to lower case and prefixing it with the four characters \"pgp-\"\"; RFC 9580 §9.5 registers \"SHA3-256\" and \"SHA3-512\"")]
        public void The_micalg_of_a_SHA3_signature_is_its_registered_text_name(HashAlgorithmTag Hash, String MicAlg)
        {

            var text     = Text(Signed(Hash: Hash));
            var verified = EMail.Parse(text.Split("\r\n")).VerifyPgpSignature(publicKeyRing);

            Assert.Multiple(() => {
                Assert.That(Header(text, "Content-Type"), Does.Contain($"micalg=\"{MicAlg}\""));
                Assert.That(verified.Status,              Is.EqualTo(PgpVerificationStatus.Valid));
                Assert.That(verified.HashAlgorithm,       Is.EqualTo(Hash));
            });

        }

        #endregion

        #region Verifying: two parts, and a signature that is none (RFC 3156 §5)

        [Test(Description = "RFC 3156 §5: \"The multipart/signed body MUST consist of exactly two parts\"; RFC 1847 §2.1: \"The multipart/signed content type contains exactly two body parts\"")]
        public void A_multipart_signed_with_a_third_part_is_not_valid()
        {

            var text     = Text(Signed());
            var boundary = EMail.Parse(text.Split("\r\n")).Body.ContentType!.MIMEBoundary;
            var tampered = text.Replace($"--{boundary}--",
                                        $"--{boundary}\r\nContent-Type: text/plain\r\n\r\nP.S.: No, pay the invoice to account 666 instead.\r\n--{boundary}--");

            var parsed   = EMail.Parse(tampered.Split("\r\n"));

            Assert.Multiple(() => {
                Assert.That(EMail.Parse(text.Split("\r\n")).VerifyPgpSignature(publicKeyRing).Status, Is.EqualTo(PgpVerificationStatus.Valid), "the two parts as signed");
                Assert.That(parsed.Body.NestedBodyparts.Count(),                                       Is.EqualTo(3));
                Assert.That(parsed.VerifyPgpSignature(publicKeyRing).IsValid,                          Is.False, "with a third part");
            });

        }

        [TestCase("-----BEGIN PGP SIGNATURE-----\r\n\r\nAAAA\r\n-----END PGP SIGNATURE-----",  Description = "armor around a broken packet")]
        [TestCase("-----BEGIN PGP SIGNATURE-----\r\n\r\n%%%%\r\n-----END PGP SIGNATURE-----",  Description = "armor around no base64")]
        [TestCase("-----BEGIN PGP SIGNATURE-----\r\n\r\n-----END PGP SIGNATURE-----",          Description = "armor around nothing")]
        [TestCase("not a signature at all",                                                    Description = "no armor")]
        [Test(Description = "RFC 3156 §5: \"The second body MUST contain the OpenPGP digital signature\" - a part that holds none is a failed verification, not an exception")]
        [CancelAfter(10_000)]
        public void A_signature_part_without_a_signature_is_not_valid(String Signature)
        {

            var text     = Text(Signed());
            var parsed   = EMail.Parse(text.Split("\r\n"));
            var boundary = parsed.Body.ContentType!.MIMEBoundary;
            var start    = text.IndexOf("-----BEGIN PGP SIGNATURE-----", StringComparison.Ordinal);
            var end      = text.IndexOf($"\r\n--{boundary}--", StringComparison.Ordinal);
            var broken   = EMail.Parse((text[..start] + Signature + text[end..]).Split("\r\n"));

            PgpSignatureVerification? result = null;

            Assert.That(() => result = broken.VerifyPgpSignature(publicKeyRing), Throws.Nothing);
            Assert.That(result?.IsValid, Is.False);

        }

        #endregion

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
