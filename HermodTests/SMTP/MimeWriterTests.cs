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
using System.Text.RegularExpressions;

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The MIME writer (MimeMessageBuilder, MimeEncoding, MimeEntity): what it
    /// writes is 7-bit, folded and encoded as the RFCs ask - and what MimeMessage
    /// reads back is what was given. Then the answers (MailReplies) and PGP/MIME
    /// around a signature or ciphertext made elsewhere (PgpMime).
    /// </summary>
    [TestFixture]
    public class MimeWriterTests
    {

        #region Data

        private const String Passphrase = "mime-writer-test";

        private static readonly Byte[] Pdf = [ .. Enumerable.Range(0, 3000).Select(i => (Byte) (i * 7 % 256)) ];

        private static readonly String Text = "Grüße aus Köln!\r\n" +
                                              "From here on a line that a mailbox file would munge.\r\n" +
                                              "A line with white space at its end   \r\n" +
                                              "=Gleich= und ein sehr langer Satz, der weit über sechsundsiebzig Zeichen hinaus geht und umbrochen werden muss, ohne dass sich etwas ändert.\r\n" +
                                              "\r\n" +
                                              "Ende.\r\n";

        #endregion

        #region (private) Mail()

        private static MimeMessageBuilder Mail()
        {

            var mail = new MimeMessageBuilder {
                           From     = new MailboxAddress("Jörg Müller", "joerg@example.org"),
                           Subject  = "Grüße aus Köln – und ein Betreff, der so lang ist, dass er auf mehrere Zeilen gefaltet werden muss",
                           Date     = new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(2)),
                           Body     = new MimeMultipart("mixed", [
                                          new MimeTextPart(Text),
                                          new MimeAttachment(Pdf, "application/pdf", "Übersicht 2026.pdf")
                                      ])
                       };

            mail.To.Add (new MailboxAddress("Müller, Jörg",  "joerg.mueller@example.net"));
            mail.To.Add (new MailboxAddress("Doe, John",     "john@example.com"));
            mail.Cc.Add (new MailboxAddress(null,            "carol@example.com"));
            mail.Bcc.Add(new MailboxAddress("Geheim",        "secret@example.com"));

            return mail;

        }

        #endregion


        #region A_mail_is_written_7_bit_and_read_back_as_it_was()

        [Test]
        public void A_mail_is_written_7_bit_and_read_back_as_it_was()
        {

            var builder = Mail();
            var octets  = builder.ToBytes();
            var text    = Encoding.ASCII.GetString(octets);
            var header  = text[..text.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
            var mail    = MimeMessage.Parse(octets);

            Assert.Multiple(() => {

                Assert.That(octets.All(octet => octet < 128),                                 Is.True,  "7-bit");
                Assert.That(text.Replace("\r\n", "").Contains('\n') || text.Replace("\r\n", "").Contains('\r'), Is.False, "CRLF only");
                Assert.That(header.Split("\r\n").Max(line => line.Length),                    Is.LessThanOrEqualTo(78), "header lines folded");
                Assert.That(text.Split("\r\n").Max(line => line.Length),                      Is.LessThanOrEqualTo(78));
                Assert.That(header,                                                           Does.Not.Contain("Bcc"),  "blind copies are blind");
                Assert.That(header,                                                           Does.Contain("Date: Fri, 16 Oct 2026 09:30:00 +0200"));
                Assert.That(Regex.IsMatch(header, @"Message-ID: <[0-9a-f]{32}@example\.org>"), Is.True,  "made, in angle brackets, at the domain of From");
                Assert.That(octets[^2..],                                                     Is.EqualTo("\r\n"u8.ToArray()));

                Assert.That(mail.Subject,                                                     Is.EqualTo(builder.Subject));
                Assert.That(mail.From.Single(),                                               Is.EqualTo(new MailboxAddress("Jörg Müller", "joerg@example.org")));
                Assert.That(mail.To,                                                          Is.EqualTo(builder.To));
                Assert.That(mail.Cc.Single().Address,                                         Is.EqualTo("carol@example.com"));
                Assert.That(mail.Bcc,                                                         Is.Empty);
                Assert.That(mail.MessageId,                                                   Is.EqualTo(builder.MessageId));

                Assert.That(mail.TextBody!.GetText(),                                         Is.EqualTo(Text), "the text, white space and all");
                Assert.That(mail.Attachments.Single().FileName,                               Is.EqualTo("Übersicht 2026.pdf"));
                Assert.That(mail.Attachments.Single().ContentType.MimeType,                   Is.EqualTo("application/pdf"));
                Assert.That(mail.Attachments.Single().GetContent(),                           Is.EqualTo(Pdf));

                Assert.That(builder.Recipients,                                               Is.EqualTo(new[] { "joerg.mueller@example.net", "john@example.com", "carol@example.com", "secret@example.com" }),
                            "the envelope has them all");

            });

        }

        #endregion

        #region Quoted_printable_keeps_what_relays_would_change()

        [Test]
        public void Quoted_printable_keeps_what_relays_would_change()
        {

            String QP(String Text)
                => Encoding.ASCII.GetString(MimeEncoding.QuotedPrintable(Encoding.UTF8.GetBytes(Text)));

            var longLine = new String('x', 200);

            Assert.Multiple(() => {

                Assert.That(QP("a=b"),            Is.EqualTo("a=3Db"));
                Assert.That(QP("ü"),              Is.EqualTo("=C3=BC"));
                Assert.That(QP("trailing  \nx"),  Is.EqualTo("trailing =20\r\nx"),    "white space at the end of a line");
                Assert.That(QP("From me\n"),      Is.EqualTo("=46rom me\r\n"),         "From at the start of a line");
                Assert.That(QP("tab\t"),          Is.EqualTo("tab=09"));

                Assert.That(QP(longLine).Split("\r\n").Max(line => line.Length),       Is.LessThanOrEqualTo(76));
                Assert.That(TransferEncodings.DecodeQuotedPrintable(Encoding.ASCII.GetBytes(QP(longLine))), Is.EqualTo(Encoding.ASCII.GetBytes(longLine)));

                // An escape is never split by a soft line break.
                var umlauts = new String('ü', 60);
                Assert.That(QP(umlauts).Split("\r\n").All(line => !Regex.IsMatch(line, "=[0-9A-F]?=$")), Is.True);
                Assert.That(Encoding.UTF8.GetString(TransferEncodings.DecodeQuotedPrintable(Encoding.ASCII.GetBytes(QP(umlauts)))), Is.EqualTo(umlauts));

                Assert.That(Encoding.ASCII.GetString(MimeEncoding.Base64(Pdf)).Split("\r\n").Max(line => line.Length), Is.EqualTo(76));

            });

        }

        #endregion

        #region Header_values_and_addresses_are_written_safely()

        [Test]
        public void Header_values_and_addresses_are_written_safely()
        {

            static Byte[] With(Action<MimeMessageBuilder> Change)
            {
                var mail = Mail();
                Change(mail);
                return mail.ToBytes();
            }

            Assert.Multiple(() => {

                Assert.Throws<ArgumentException>(() => With(mail => mail.Subject = "Hallo\r\nBcc: victim@example.com"),          "a line break in the Subject");
                Assert.Throws<ArgumentException>(() => With(mail => mail.To.Add(new MailboxAddress("Eve\n", "eve@example.com"))), "in a display name");
                Assert.Throws<ArgumentException>(() => With(mail => mail.To.Add(new MailboxAddress(null, "eve@example.com>\r\nBcc: x@y"))), "in an address");
                Assert.Throws<ArgumentException>(() => With(mail => mail.To.Add(new MailboxAddress(null, "no address"))));
                Assert.Throws<ArgumentException>(() => With(mail => mail.Headers.Add(("X-Test", "a\rb"))));
                Assert.Throws<ArgumentException>(() => With(mail => mail.Headers.Add(("Bad: Name", "x"))));
                Assert.Throws<ArgumentException>(() => With(mail => mail.InReplyTo.Add("<no-id>")));

                var header = Encoding.ASCII.GetString(With(mail => mail.Headers.Add(("X-Mailer", "EMailWebApp"))));
                Assert.That(header, Does.Contain("\"Doe, John\" <john@example.com>"), "a name with a comma is quoted");
                Assert.That(header, Does.Contain("X-Mailer: EMailWebApp"));

                var bccOnly = new MimeMessageBuilder { From = new MailboxAddress(null, "a@example.org"), Body = new MimeTextPart("x") };
                bccOnly.Bcc.Add(new MailboxAddress(null, "b@example.org"));
                Assert.That(Encoding.ASCII.GetString(bccOnly.ToBytes()), Does.Contain("To: undisclosed-recipients:;"));

            });

        }

        #endregion

        #region A_forwarded_message_is_attached_as_it_arrived()

        [Test]
        public void A_forwarded_message_is_attached_as_it_arrived()
        {

            var original = Mail().ToBytes();
            var forward  = new MimeMessageBuilder {
                               From     = new MailboxAddress(null, "anna@example.test"),
                               Subject  = MailReplies.ForwardSubject("AW: Grüße"),
                               Body     = new MimeMultipart("mixed", [ new MimeTextPart("Siehe unten."), new MimeAttachedMessage(original, "Grüße.eml") ])
                           };

            forward.To.Add(new MailboxAddress(null, "ben@example.test"));

            var mail     = MimeMessage.Parse(forward.ToBytes());
            var attached = mail.Root.Children[1];

            Assert.Multiple(() => {
                Assert.That(mail.Subject,                       Is.EqualTo("Fwd: Grüße"));
                Assert.That(attached.ContentType.MimeType,      Is.EqualTo("message/rfc822"));
                Assert.That(attached.GetContent(),              Is.EqualTo(original), "octet for octet");
            });

        }

        #endregion


        #region An_answer_finds_its_subject_its_thread_and_its_recipients()

        [Test]
        public void An_answer_finds_its_subject_its_thread_and_its_recipients()
        {

            var original = MimeMessage.Parse(Encoding.ASCII.GetBytes(
                               "From: Alice <alice@example.org>\r\n" +
                               "Reply-To: Alice Privat <alice@private.example>\r\n" +
                               "To: Anna <anna@example.test>, Ben <ben@example.test>, info@example.test\r\n" +
                               "Cc: Carol <carol@example.com>, anna@example.test\r\n" +
                               "Subject: AW: Re: Fwd: Termin\r\n" +
                               "Message-ID: <c@example.org>\r\n" +
                               "References: <a@example.org> <b@example.org>\r\n" +
                               "List-Post: <mailto:dev@lists.example.org>\r\n" +
                               "\r\n" +
                               "Hallo\r\n"));

            var own      = new[] { "anna@example.test", "info@example.test" };
            var sender   = MailReplies.Addressing(original, ReplyKind.Sender, own);
            var all      = MailReplies.Addressing(original, ReplyKind.All,    own);
            var list     = MailReplies.Addressing(original, ReplyKind.List,   own);

            Assert.Multiple(() => {

                Assert.That(MailReplies.ReplySubject(original.Subject),     Is.EqualTo("Re: Termin"));
                Assert.That(MailReplies.ForwardSubject("Re[2]: Termin"),    Is.EqualTo("Fwd: Termin"));
                Assert.That(MailReplies.InReplyTo(original),                Is.EqualTo(new[] { "c@example.org" }));
                Assert.That(MailReplies.References(original),               Is.EqualTo(new[] { "a@example.org", "b@example.org", "c@example.org" }));

                Assert.That(sender.To.Select(mailbox => mailbox.Address),   Is.EqualTo(new[] { "alice@private.example" }), "Reply-To before From");
                Assert.That(sender.Cc,                                      Is.Empty);

                Assert.That(all.To.Select(mailbox => mailbox.Address),      Is.EqualTo(new[] { "alice@private.example", "ben@example.test" }), "not one's own addresses");
                Assert.That(all.Cc.Select(mailbox => mailbox.Address),      Is.EqualTo(new[] { "carol@example.com" }));

                Assert.That(list.To.Select(mailbox => mailbox.Address),     Is.EqualTo(new[] { "dev@lists.example.org" }));

            });

            var mine = MimeMessage.Parse(Encoding.ASCII.GetBytes(
                           "From: Anna <anna@example.test>\r\nTo: Ben <ben@example.test>\r\nSubject: x\r\n\r\nx\r\n"));

            Assert.That(MailReplies.Addressing(mine, ReplyKind.Sender, own).To.Select(mailbox => mailbox.Address),
                        Is.EqualTo(new[] { "ben@example.test" }), "an answer to one's own message goes where it went");

            Assert.That(MailReplies.Quote("Hallo\r\n> zitiert\r\n\r\n"), Is.EqualTo("> Hallo\n>> zitiert\n"));

        }

        #endregion


        #region PGP/MIME

        private static (PgpSecretKey Secret, PgpPublicKeyRing Public) Key()
        {

            var rsa = new RsaKeyPairGenerator();
            rsa.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), new SecureRandom(), 2048, 25));

            var generator = new PgpKeyRingGenerator(PgpSignature.PositiveCertification,
                                                    new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, rsa.GenerateKeyPair(), DateTime.UtcNow),
                                                    "Anna <anna@example.test>",
                                                    SymmetricKeyAlgorithmTag.Aes256,
                                                    Passphrase.ToCharArray(),
                                                    true, null, null, new SecureRandom());

            return (generator.GenerateSecretKeyRing().GetSecretKey(), generator.GeneratePublicKeyRing());

        }

        private static String Armored(PgpSignature Signature)
        {
            using var output = new MemoryStream();
            using (var armor = new ArmoredOutputStream(output))
                Signature.Encode(armor);
            return Encoding.ASCII.GetString(output.ToArray());
        }

        [Test]
        public void A_signature_made_elsewhere_is_sent_as_multipart_signed_and_holds()
        {

            var (secret, publicRing) = Key();

            var inner      = new MimeMultipart("mixed", [ new MimeTextPart(Text), new MimeAttachment(Pdf, "application/pdf", "Übersicht.pdf") ]).ToBytes();
            var sha256     = Armored(OpenPGP.CreateSignature(new MemoryStream(inner), secret, Passphrase, HashAlgorithmTag.Sha256));
            var sha512     = Armored(OpenPGP.CreateSignature(new MemoryStream(inner), secret, Passphrase, HashAlgorithmTag.Sha512));

            var mail       = new MimeMessageBuilder { From = new MailboxAddress("Anna", "anna@example.test"), Subject = "Signiert", Body = PgpMime.Signed(inner, sha256) };
            mail.To.Add(new MailboxAddress(null, "ben@example.test"));

            var octets     = mail.ToBytes();
            var parsed     = MimeMessage.Parse(octets);
            var signedPart = parsed.Root.Children[0];
            var signature  = parsed.Root.Children[1].GetContent();

            var result     = OpenPGP.VerifyDetachedSignature(signedPart.Raw.ToArray(), new MemoryStream(signature), new PgpPublicKeyRingBundle([ publicRing ]));

            Assert.Multiple(() => {

                Assert.That(parsed.Pgp,                                                 Is.EqualTo("signed"));
                Assert.That(parsed.Root.ContentType.Parameters.GetValueOrDefault("micalg"), Is.EqualTo("pgp-sha256"));
                Assert.That(signedPart.Raw.ToArray(),                                   Is.EqualTo(inner), "the signed part, octet for octet");
                Assert.That(result.Status,                                              Is.EqualTo(PgpVerificationStatus.Valid));
                Assert.That(octets.All(octet => octet < 128),                           Is.True);

                Assert.That(PgpMime.MicAlg(sha512),                                     Is.EqualTo("pgp-sha512"), "micalg is what the signature says");

                Assert.Throws<ArgumentException>(() => PgpMime.Signed(Encoding.UTF8.GetBytes("Content-Type: text/plain\r\n\r\nGrüße\r\n"), sha256), "8-bit");
                Assert.Throws<ArgumentException>(() => PgpMime.Signed(Encoding.ASCII.GetBytes("Content-Type: text/plain\n\nx\n"), sha256),           "bare LF");
                Assert.Throws<ArgumentException>(() => PgpMime.Signed(inner, "-----BEGIN PGP SIGNATURE-----\r\n\r\nAAAA\r\n-----END PGP SIGNATURE-----"), "no signature");

            });

        }

        [Test]
        public void A_ciphertext_made_elsewhere_is_sent_as_multipart_encrypted()
        {

            var (secret, publicRing) = Key();

            var inner      = new MimeTextPart("Geheim.").ToBytes();
            var encryption = publicRing.GetPublicKeys().Cast<PgpPublicKey>().First(key => key.IsEncryptionKey);

            using var ciphertext = new MemoryStream();
            OpenPGP.EncryptSignAndZip(new MemoryStream(inner), (UInt64) inner.Length, secret, Passphrase, encryption, ciphertext);

            var mail    = new MimeMessageBuilder { From = new MailboxAddress(null, "anna@example.test"), Subject = "...", Body = PgpMime.Encrypted(Encoding.ASCII.GetString(ciphertext.ToArray())) };
            mail.To.Add(new MailboxAddress(null, "ben@example.test"));

            var parsed  = MimeMessage.Parse(mail.ToBytes());

            Assert.Multiple(() => {

                Assert.That(parsed.Pgp,                                         Is.EqualTo("encrypted"));
                Assert.That(parsed.Root.Children[0].ContentType.MimeType,       Is.EqualTo("application/pgp-encrypted"));
                Assert.That(parsed.Root.Children[0].GetText().Trim(),           Is.EqualTo("Version: 1"));
                Assert.That(Encoding.ASCII.GetString(parsed.Root.Children[1].GetContent()), Does.StartWith("-----BEGIN PGP MESSAGE-----"));

                Assert.Throws<ArgumentException>(() => PgpMime.Encrypted("Hallo"));
                Assert.Throws<ArgumentException>(() => PgpMime.Encrypted("-----BEGIN PGP MESSAGE-----\r\n\r\nAAAA\r\n-----END PGP MESSAGE-----"));

            });

        }

        #endregion

    }

}
