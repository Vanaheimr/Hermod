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

using System;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

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
    /// Build → serialize → re-parse round-trip tests for the e-mail builders, covering
    /// HTML and plain-text bodies, each with and without a file attachment and with and
    /// without an OpenPGP signature (RFC 3156). The PGP key pair is generated in-test.
    /// </summary>
    [TestFixture]
    public class EMailBuilderTests
    {

        private const String Passphrase = "s3cr3t-test-passphrase";
        private const String Marker     = "ROUNDTRIPMARKER42";     // ASCII, survives MIME encodings verbatim

        private PgpSecretKeyRing  secretKeyRing        = null!;   // Alice (sender)
        private PgpPublicKeyRing  publicKeyRing        = null!;
        private Int64             keyId;

        private PgpSecretKeyRing  recipientSecretRing  = null!;   // Bob (recipient — needs a public key to encrypt to)
        private PgpPublicKeyRing  recipientPublicRing  = null!;

        private PgpSecretKeyRing  carolSecretRing      = null!;   // Carol (second recipient, for multi-recipient encryption)
        private PgpPublicKeyRing  carolPublicRing      = null!;

        #region OneTimeSetUp — generate the RSA OpenPGP key rings

        [OneTimeSetUp]
        public void GenerateTestKey()
        {

            (secretKeyRing,       publicKeyRing)        = GenerateKeyRing("Alice <alice@example.com>");
            (recipientSecretRing, recipientPublicRing)  = GenerateKeyRing("Bob <bob@example.org>");
            (carolSecretRing,     carolPublicRing)      = GenerateKeyRing("Carol <carol@example.net>");

            keyId = secretKeyRing.GetSecretKey().KeyId;

        }

        private static (PgpSecretKeyRing, PgpPublicKeyRing) GenerateKeyRing(String Identity)
        {

            var rsa = new RsaKeyPairGenerator();
            rsa.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), new SecureRandom(), 2048, 25));

            var pgpKeyPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, rsa.GenerateKeyPair(), DateTime.UtcNow);

            var gen = new PgpKeyRingGenerator(
                          PgpSignature.PositiveCertification,
                          pgpKeyPair,
                          Identity,
                          SymmetricKeyAlgorithmTag.Aes256,
                          Passphrase.ToCharArray(),
                          true,
                          null,
                          null,
                          new SecureRandom());

            return (gen.GenerateSecretKeyRing(), gen.GeneratePublicKeyRing());

        }

        #endregion

        #region (helper) Build(html, withAttachment, withPgp)

        private EMail Build(Boolean html, Boolean withAttachment, Boolean withPgp, Boolean withEncryption = false)
        {

            // Signing (and, a fortiori, encrypting) needs Alice's secret+public key ring on the sender.
            EMailAddress from = (withPgp || withEncryption)
                ? new EMailAddress("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing)
                : (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");

            // Encrypting additionally needs the recipient's public key ring.
            EMailAddress to = withEncryption
                ? new EMailAddress("Bob", SimpleEMailAddress.Parse("bob@example.org"), null, recipientPublicRing)
                : (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");

            void Configure(AbstractEMail.Builder b)
            {
                b.From          = from;
                b.To            = to;
                b.Subject       = "Test Subject";
                b.SecurityLevel = withEncryption ? EMailSecurity.encrypt
                                : withPgp         ? EMailSecurity.sign
                                :                   EMailSecurity.none;
                if (withPgp || withEncryption)  b.Passphrase = Passphrase;
                if (withAttachment)             b.AddAttachment(Encoding.UTF8.GetBytes("%PDF-1.7 fake attachment bytes"), "doc.pdf");
            }

            if (html)
            {
                var b = new HTMLEMailBuilder { HTMLText = $"<h1>Hallo</h1><p>{Marker}</p>", PlainText = $"Hallo {Marker}" };
                Configure(b);
                return b;   // implicit HTMLEMailBuilder -> EMail (builds + signs)
            }
            else
            {
                var b = new TextEMailBuilder { Text = $"Hallo {Marker}" };
                Configure(b);
                return b;   // implicit TextEMailBuilder -> EMail
            }

        }

        #endregion

        #region (helper) AssertSignatureIsOurs(text)

        // Extract the armored PGP signature and confirm it is a well-formed signature made by our key.
        private void AssertSignatureIsOurs(String text)
        {

            const String beginMarker = "-----BEGIN PGP SIGNATURE-----";
            const String endMarker   = "-----END PGP SIGNATURE-----";

            var start = text.IndexOf(beginMarker, StringComparison.Ordinal);
            var end   = text.IndexOf(endMarker,   StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "PGP signature block must be present");
            Assert.That(end,   Is.GreaterThan(start));

            var armored = text.Substring(start, end - start + endMarker.Length);

            using var ms      = new MemoryStream(Encoding.ASCII.GetBytes(armored));
            using var armor   = new ArmoredInputStream(ms);
            var       factory = new PgpObjectFactory(armor);
            var       sigList = factory.NextPgpObject() as PgpSignatureList;

            Assert.That(sigList,        Is.Not.Null, "armored block must decode to a PGP signature list");
            Assert.That(sigList!.Count, Is.GreaterThan(0));
            Assert.That(sigList[0].KeyId, Is.EqualTo(keyId), "signature must be made by our in-test key");

        }

        #endregion

        #region (helper) ExtractPgpMessage(text)

        // Return the armored "-----BEGIN PGP MESSAGE----- … -----END PGP MESSAGE-----" block verbatim.
        private static String ExtractPgpMessage(String text)
        {

            const String beginMarker = "-----BEGIN PGP MESSAGE-----";
            const String endMarker   = "-----END PGP MESSAGE-----";

            var start = text.IndexOf(beginMarker, StringComparison.Ordinal);
            var end   = text.IndexOf(endMarker,   StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "PGP MESSAGE block must be present");
            Assert.That(end,   Is.GreaterThan(start));

            return text.Substring(start, end - start + endMarker.Length);

        }

        #endregion


        #region The 2×2×2 matrix — build, serialize, re-parse

        [TestCase(true,  false, false, TestName = "HTML, no attachment, no PGP")]
        [TestCase(true,  true,  false, TestName = "HTML, with attachment, no PGP")]
        [TestCase(true,  false, true,  TestName = "HTML, no attachment, PGP-signed")]
        [TestCase(true,  true,  true,  TestName = "HTML, with attachment, PGP-signed")]
        [TestCase(false, false, false, TestName = "Text, no attachment, no PGP")]
        [TestCase(false, true,  false, TestName = "Text, with attachment, no PGP")]
        [TestCase(false, false, true,  TestName = "Text, no attachment, PGP-signed")]
        [TestCase(false, true,  true,  TestName = "Text, with attachment, PGP-signed")]
        public void Build_serialize_and_reparse(Boolean html, Boolean attach, Boolean pgp)
        {

            var mail = Build(html, attach, pgp);
            var text = String.Join("\r\n", mail.ToText());

            // --- headers + body content present in the serialized wire form ---
            Assert.That(text, Does.Contain("From:"));
            Assert.That(text, Does.Contain("To:"));
            Assert.That(text, Does.Contain("Subject:"));
            Assert.That(text, Does.Contain("Test Subject"));
            Assert.That(text, Does.Contain(Marker), "the body content must be on the wire");

            // --- MIME structure per case ---
            if (pgp)
            {
                Assert.That(text, Does.Contain("multipart/signed"));
                Assert.That(text, Does.Contain("pgp-signature"));
                AssertSignatureIsOurs(text);
            }

            if (attach)
            {
                Assert.That(text, Does.Contain("multipart/mixed"));
                Assert.That(text, Does.Contain("doc.pdf"), "the attachment filename must be present");
            }

            if (!pgp && !attach)
                Assert.That(text, Does.Contain(html ? "text/html" : "text/plain"));

            // --- round-trip: parse the serialized message back and check it re-serializes intact ---
            var parsed = EMail.Parse(mail.ToText());

            Assert.That(parsed.Subject,                 Is.EqualTo("Test Subject"));
            Assert.That(parsed.From?.Address.ToString(), Is.EqualTo("alice@example.com"));
            Assert.That(parsed.To.Any(),                Is.True);

            // Structural fidelity: a multipart message — multipart/mixed (attachment) or
            // multipart/signed (PGP) — must re-parse into nested body parts, not a flat body.
            if (attach || pgp)
                Assert.That(parsed.Body.NestedBodyparts.Any(), Is.True,
                            "a multipart message should re-parse into nested body parts");

            var reText = String.Join("\r\n", parsed.ToText());
            Assert.That(reText, Does.Contain(Marker), "body content must survive parse → serialize");
            if (attach) Assert.That(reText, Does.Contain("doc.pdf"),              "attachment must survive parse → serialize");
            if (pgp)    Assert.That(reText, Does.Contain("BEGIN PGP SIGNATURE"),  "signature must survive parse → serialize");

        }

        #endregion

        #region The encrypted matrix — build, serialize, re-parse (multipart/encrypted, RFC 3156)

        [TestCase(true,  false, TestName = "HTML, no attachment, PGP-encrypted")]
        [TestCase(true,  true,  TestName = "HTML, with attachment, PGP-encrypted")]
        [TestCase(false, false, TestName = "Text, no attachment, PGP-encrypted")]
        [TestCase(false, true,  TestName = "Text, with attachment, PGP-encrypted")]
        public void Encrypt_serialize_and_reparse(Boolean html, Boolean attach)
        {

            var mail = Build(html, attach, withPgp: false, withEncryption: true);
            var text = String.Join("\r\n", mail.ToText());

            // --- headers present; the plaintext body/marker must NOT be on the wire (it is encrypted) ---
            Assert.That(text, Does.Contain("From:"));
            Assert.That(text, Does.Contain("To:"));
            Assert.That(text, Does.Contain("Subject:"));
            Assert.That(text, Does.Contain("Test Subject"));
            Assert.That(text, Does.Not.Contain(Marker), "the body must be encrypted, so the plaintext marker must not appear");

            // --- MIME structure: multipart/encrypted with the two RFC 3156 control/data parts ---
            Assert.That(text, Does.Contain("multipart/encrypted"));
            Assert.That(text, Does.Contain("application/pgp-encrypted"));
            Assert.That(text, Does.Contain("Version: 1"));
            Assert.That(text, Does.Contain("BEGIN PGP MESSAGE"));

            // --- round-trip: parse back and confirm it re-parses into the two nested parts ---
            var parsed = EMail.Parse(mail.ToText());

            Assert.That(parsed.Subject,                  Is.EqualTo("Test Subject"));
            Assert.That(parsed.From?.Address.ToString(), Is.EqualTo("alice@example.com"));
            Assert.That(parsed.To.Any(),                 Is.True);

            Assert.That(parsed.Body.NestedBodyparts.Any(), Is.True,
                        "a multipart/encrypted message must re-parse into nested body parts, not a flat body");
            Assert.That(parsed.Body.NestedBodyparts.Count(), Is.EqualTo(2),
                        "multipart/encrypted has exactly two parts: the pgp-encrypted control part and the octet-stream ciphertext");

            var reText = String.Join("\r\n", parsed.ToText());
            Assert.That(reText, Does.Contain("BEGIN PGP MESSAGE"), "the ciphertext must survive parse → serialize");
            Assert.That(reText, Does.Contain("END PGP MESSAGE"),   "the ciphertext must survive parse → serialize");

            // The armored ciphertext block must survive parse → serialize byte-for-byte; any corruption
            // (dropped/added lines, altered whitespace) would make it undecryptable at the far end.
            Assert.That(ExtractPgpMessage(reText), Is.EqualTo(ExtractPgpMessage(text)),
                        "the OpenPGP ciphertext block must round-trip unchanged");

            // ...and, definitively, the re-parsed message must still decrypt back to the original body.
            Assert.That(DecryptWith(ExtractPgpMessage(reText), recipientSecretRing), Does.Contain(Marker),
                        "the re-parsed ciphertext must still decrypt to the plaintext");

        }

        #endregion

        #region Security-mode selection — autosign / auto (graceful degradation) & encrypt failure paths

        // Address fixtures with different key material available.
        private EMailAddress AliceWithKey => new ("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing);
        private EMailAddress AliceNoKey   => (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");
        private EMailAddress BobWithKey   => new ("Bob",   SimpleEMailAddress.Parse("bob@example.org"),   null, recipientPublicRing);
        private EMailAddress BobNoKey     => (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");
        private EMailAddress CarolWithKey => new ("Carol", SimpleEMailAddress.Parse("carol@example.net"), null, carolPublicRing);

        // A minimal text builder wired with the given sender/recipient/mode (passphrase always supplied).
        private TextEMailBuilder MakeBuilder(EMailAddress From, EMailAddress To, EMailSecurity Security)
            => new ()
               {
                   Text          = $"Hallo {Marker}",
                   From          = From,
                   To            = To,
                   Subject       = "Test Subject",
                   SecurityLevel = Security,
                   Passphrase    = Passphrase
               };

        // Assertions on the serialized wire form for each of the three possible outcomes.

        private void AssertSigned(String text)
        {
            Assert.That(text, Does.Contain("multipart/signed"),        "expected a signed (multipart/signed) message");
            Assert.That(text, Does.Contain("BEGIN PGP SIGNATURE"),     "expected an armored signature");
            Assert.That(text, Does.Not.Contain("multipart/encrypted"), "must not be encrypted");
            Assert.That(text, Does.Contain(Marker),                    "a signed (unencrypted) body keeps the plaintext on the wire");
            AssertSignatureIsOurs(text);
        }

        private void AssertEncrypted(String text)
        {
            Assert.That(text, Does.Contain("multipart/encrypted"),     "expected an encrypted (multipart/encrypted) message");
            Assert.That(text, Does.Contain("BEGIN PGP MESSAGE"),       "expected an armored ciphertext");
            Assert.That(text, Does.Not.Contain(Marker),                "an encrypted body must not leak the plaintext marker");
        }

        private void AssertPlaintext(String text)
        {
            Assert.That(text, Does.Not.Contain("multipart/signed"),    "must not be signed");
            Assert.That(text, Does.Not.Contain("multipart/encrypted"), "must not be encrypted");
            Assert.That(text, Does.Not.Contain("BEGIN PGP"),           "must carry no OpenPGP block");
            Assert.That(text, Does.Contain(Marker),                    "plaintext body must be on the wire");
        }


        [Test]
        public void Autosign_with_a_key_signs()
        {
            // autosign + key available  →  sign
            EMail mail = MakeBuilder(AliceWithKey, BobNoKey, EMailSecurity.autosign);
            AssertSigned(String.Join("\r\n", mail.ToText()));
        }

        [Test]
        public void Autosign_without_a_key_degrades_to_plaintext_and_does_not_throw()
        {
            // autosign is best-effort: no key must NOT throw, and must fall back to plaintext.
            var b = MakeBuilder(AliceNoKey, BobNoKey, EMailSecurity.autosign);
            EMail mail = null!;
            Assert.That(() => { mail = b; }, Throws.Nothing, "autosign must never fail");
            AssertPlaintext(String.Join("\r\n", mail.ToText()));
        }

        [Test]
        public void Auto_with_sender_and_recipient_keys_encrypts()
        {
            // auto + can sign + recipient has a public key  →  sign+encrypt
            EMail mail = MakeBuilder(AliceWithKey, BobWithKey, EMailSecurity.auto);
            AssertEncrypted(String.Join("\r\n", mail.ToText()));
        }

        [Test]
        public void Auto_with_sender_key_but_recipient_without_key_degrades_to_sign_only()
        {
            // auto + can sign + recipient has NO public key  →  degrade to sign-only (no throw)
            var b = MakeBuilder(AliceWithKey, BobNoKey, EMailSecurity.auto);
            EMail mail = null!;
            Assert.That(() => { mail = b; }, Throws.Nothing, "auto must never fail");
            AssertSigned(String.Join("\r\n", mail.ToText()));
        }

        [Test]
        public void Auto_without_a_key_degrades_to_plaintext_and_does_not_throw()
        {
            // auto + cannot even sign  →  plaintext (no throw)
            var b = MakeBuilder(AliceNoKey, BobWithKey, EMailSecurity.auto);
            EMail mail = null!;
            Assert.That(() => { mail = b; }, Throws.Nothing, "auto must never fail");
            AssertPlaintext(String.Join("\r\n", mail.ToText()));
        }

        [Test]
        public void Encrypt_without_a_recipient_key_throws()
        {
            // encrypt is mandatory: a recipient without a public key must throw, never silently downgrade.
            var b = MakeBuilder(AliceWithKey, BobNoKey, EMailSecurity.encrypt);
            Assert.That(() => { EMail _ = b; }, Throws.TypeOf<ApplicationException>());
        }

        [Test]
        public void Encrypt_without_a_sender_key_throws()
        {
            // encrypt always signs too, so a missing sender secret key must throw.
            var b = MakeBuilder(AliceNoKey, BobWithKey, EMailSecurity.encrypt);
            Assert.That(() => { EMail _ = b; }, Throws.TypeOf<ApplicationException>());
        }

        [Test]
        public void Encrypt_addresses_every_recipient_not_just_the_first()
        {

            // Two recipients, both with a public key: the message must be encrypted to BOTH, so each
            // can decrypt with their own private key (regression against "encrypt to To.First() only").
            var b = new TextEMailBuilder { Text = $"Hallo {Marker}" };
            b.From          = AliceWithKey;
            b.To            = EMailAddressListBuilder.Create(BobWithKey, CarolWithKey);
            b.Subject       = "Test Subject";
            b.SecurityLevel = EMailSecurity.encrypt;
            b.Passphrase    = Passphrase;

            EMail mail = b;
            var   text = String.Join("\r\n", mail.ToText());

            AssertEncrypted(text);

            // The armored ciphertext must carry one public-key encrypted session-key (PKESK) packet
            // per recipient, keyed to each recipient's encryption key.
            var recipientKeyIds = SessionKeyRecipients(ExtractPgpMessage(text));

            Assert.That(recipientKeyIds.Length, Is.EqualTo(2), "one session-key packet per recipient");
            Assert.That(recipientKeyIds, Does.Contain(EncryptionKeyId(recipientPublicRing)), "Bob must be a recipient");
            Assert.That(recipientKeyIds, Does.Contain(EncryptionKeyId(carolPublicRing)),     "Carol must be a recipient");

            // ...and it must survive our own parser: the multipart/encrypted shape is independent of the
            // recipient count (still exactly two MIME parts; the extra PKESK packets live inside the
            // opaque ciphertext), so re-parsing must preserve both recipients' session-key packets.
            var parsed = EMail.Parse(mail.ToText());
            Assert.That(parsed.Body.NestedBodyparts.Count(), Is.EqualTo(2), "multipart/encrypted must re-parse into two parts");

            var reText          = String.Join("\r\n", parsed.ToText());
            var reRecipientKeys = SessionKeyRecipients(ExtractPgpMessage(reText));
            Assert.That(reRecipientKeys, Is.EquivalentTo(recipientKeyIds), "both recipients' session-key packets must survive parse → serialize");

        }

        [Test]
        public void Encrypted_mail_decrypts_back_to_the_plaintext_for_every_recipient()
        {

            // The whole point of encryption: each intended recipient must actually be able to recover
            // the original plaintext with their OWN private key (end-to-end, not just structurally).
            var b = new TextEMailBuilder { Text = $"Hallo {Marker}" };
            b.From          = AliceWithKey;
            b.To            = EMailAddressListBuilder.Create(BobWithKey, CarolWithKey);
            b.Subject       = "Test Subject";
            b.SecurityLevel = EMailSecurity.encrypt;
            b.Passphrase    = Passphrase;

            EMail mail = b;
            var   pgp  = ExtractPgpMessage(String.Join("\r\n", mail.ToText()));

            Assert.That(DecryptWith(pgp, recipientSecretRing), Does.Contain(Marker), "Bob must recover the plaintext with his own key");
            Assert.That(DecryptWith(pgp, carolSecretRing),     Does.Contain(Marker), "Carol must recover the plaintext with her own key");

        }

        // The KeyId the builder encrypts to for a given ring: prefer the encryption (sub)key, else the primary.
        private static Int64 EncryptionKeyId(PgpPublicKeyRing Ring)
            => (Ring.GetPublicKeys().Cast<PgpPublicKey>().FirstOrDefault(key => key.IsEncryptionKey)
                    ?? Ring.GetPublicKeys().Cast<PgpPublicKey>().First()).KeyId;

        // The recipient KeyIds a given armored PGP MESSAGE is encrypted to (one per PKESK packet).
        private static Int64[] SessionKeyRecipients(String ArmoredPgpMessage)
        {

            using var ms      = new MemoryStream(Encoding.ASCII.GetBytes(ArmoredPgpMessage));
            using var armor   = new ArmoredInputStream(ms);
            var       factory = new PgpObjectFactory(armor);
            var       encList = factory.NextPgpObject() as PgpEncryptedDataList;

            Assert.That(encList, Is.Not.Null, "a PGP MESSAGE must begin with an encrypted-data list");

            return encList!.GetEncryptedDataObjects().
                            Cast<PgpPublicKeyEncryptedData>().
                            Select(data => data.KeyId).
                            ToArray();

        }

        // Fully decrypt an armored PGP MESSAGE with the given secret key ring and return the recovered
        // plaintext. Reverses EncryptSignAndZip: PKESK -> session key -> decompress -> (one-pass sig) ->
        // literal data. Throws / asserts if this ring is not one of the recipients.
        private String DecryptWith(String ArmoredPgpMessage, PgpSecretKeyRing SecretRing)
        {

            using var ms      = new MemoryStream(Encoding.ASCII.GetBytes(ArmoredPgpMessage));
            using var decoder = PgpUtilities.GetDecoderStream(ms);

            var factory = new PgpObjectFactory(decoder);
            var encList = (PgpEncryptedDataList) factory.NextPgpObject();

            // Pick the PKESK packet this ring holds the private key for.
            PgpPublicKeyEncryptedData?  encrypted   = null;
            PgpPrivateKey?              privateKey  = null;

            foreach (PgpPublicKeyEncryptedData candidate in encList.GetEncryptedDataObjects())
            {
                var secretKey = SecretRing.GetSecretKey(candidate.KeyId);
                if (secretKey is not null)
                {
                    privateKey  = secretKey.ExtractPrivateKey(Passphrase.ToCharArray());
                    encrypted   = candidate;
                    break;
                }
            }

            Assert.That(encrypted, Is.Not.Null, "no session-key packet matched this recipient's secret key");

            var       clearFactory = new PgpObjectFactory(encrypted!.GetDataStream(privateKey));
            PgpObject message       = clearFactory.NextPgpObject();

            if (message is PgpCompressedData compressed)
            {
                clearFactory = new PgpObjectFactory(compressed.GetDataStream());
                message      = clearFactory.NextPgpObject();
            }

            // EncryptSignAndZip writes a one-pass signature ahead of the literal data.
            if (message is PgpOnePassSignatureList)
                message = clearFactory.NextPgpObject();

            var literal = (PgpLiteralData) message;

            using var literalStream = literal.GetInputStream();
            using var output        = new MemoryStream();
            literalStream.CopyTo(output);

            return Encoding.UTF8.GetString(output.ToArray());

        }

        #endregion

        #region Inbound: structure flags, signature verification & decryption

        [Test]
        public void Parsed_signed_mail_is_flagged_and_verifies_against_the_sender_key()
        {

            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: true).ToText());

            Assert.That(parsed.IsPgpSigned,    Is.True,  "a multipart/signed mail must be flagged as signed");
            Assert.That(parsed.IsPgpEncrypted, Is.False);

            var result = parsed.VerifyPgpSignature(publicKeyRing);

            Assert.That(result.IsValid,     Is.True, "the signature must verify against Alice's public key");
            Assert.That(result.Status,      Is.EqualTo(PgpVerificationStatus.Valid));
            Assert.That(result.SignerKeyId, Is.EqualTo(keyId), "the signer key id must be Alice's");

        }

        [Test]
        public void Tampered_signed_body_fails_verification()
        {

            // Corrupt the signed body after signing → the signature must no longer validate.
            var lines = Build(html: false, withAttachment: false, withPgp: true).ToText().ToList();
            var idx   = lines.FindIndex(line => line.Contains(Marker));
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            lines[idx] = lines[idx].Replace(Marker, "TAMPERED-CONTENT");

            var result = EMail.Parse(lines).VerifyPgpSignature(publicKeyRing);

            Assert.That(result.IsValid, Is.False, "a tampered body must fail signature verification");
            Assert.That(result.Status,  Is.EqualTo(PgpVerificationStatus.Invalid));

        }

        [Test]
        public void Signature_verification_without_the_signer_key_reports_no_matching_key()
        {

            // Verify with Bob's key ring, which does not contain Alice's signing key.
            var result = EMail.Parse(Build(html: false, withAttachment: false, withPgp: true).ToText()).
                             VerifyPgpSignature(recipientPublicRing);

            Assert.That(result.Status,  Is.EqualTo(PgpVerificationStatus.NoMatchingKey));
            Assert.That(result.IsValid, Is.False);

        }

        [Test]
        public void Unsigned_mail_reports_no_signature()
        {

            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: false).ToText());

            Assert.That(parsed.IsPgpSigned, Is.False);
            Assert.That(parsed.VerifyPgpSignature(publicKeyRing).Status, Is.EqualTo(PgpVerificationStatus.NoSignature));

        }


        [Test]
        public void Signature_verifies_against_raw_bytes_of_a_foreign_serialization()
        {

            // A signed part whose exact wire form our own serializer would NOT reproduce: a body line
            // with trailing whitespace. RFC 1847 signs the octets as they appear, so a real signer signs
            // WITH the trailing spaces — but our old reconstruction path right-trimmed each line and would
            // therefore have computed different bytes and reported Invalid. Verifying against the preserved
            // raw bytes must accept it.
            var signedPartLines = new[]
            {
                "Content-Type: text/plain; charset=utf-8",
                "",
                $"Hallo {Marker}   "                       // <- trailing whitespace, part of the signed octets
            };
            var signedBytes = String.Join("\r\n", signedPartLines);

            // Alice signs the exact bytes (BinaryDocument, as our sign path does).
            using var sigStream = new MemoryStream();
            OpenPGP.CreateSignature(new MemoryStream(Encoding.UTF8.GetBytes(signedBytes)),
                                    secretKeyRing.GetSecretKey(),
                                    Passphrase).
                    WriteTo(sigStream, ArmoredOutput: true, CloseOutputStream: false);

            var armoredSigLines = Encoding.ASCII.GetString(sigStream.ToArray()).
                                      Split(["\r\n", "\n"], StringSplitOptions.None).
                                      ToList();
            if (armoredSigLines.Count > 0 && armoredSigLines[^1].Length == 0)
                armoredSigLines.RemoveAt(armoredSigLines.Count - 1);

            // Assemble the multipart/signed message by hand.
            const String boundary = "=-=-=raw-signed-boundary=-=-=";
            var message = new List<String>
            {
                "From: Alice <alice@example.com>",
                "To: Bob <bob@example.org>",
                "Subject: Test Subject",
                $"Content-Type: multipart/signed; micalg=\"pgp-sha512\"; protocol=\"application/pgp-signature\"; boundary=\"{boundary}\"",
                "",
                "--" + boundary
            };
            message.AddRange(signedPartLines);
            message.Add("--" + boundary);
            message.Add("Content-Type: application/pgp-signature");
            message.Add("");
            message.AddRange(armoredSigLines);
            message.Add("--" + boundary + "--");

            var mail = EMail.Parse(message);

            Assert.That(mail.IsPgpSigned, Is.True);

            // Sanity: our re-serialization normalizes the trailing whitespace away, so a reconstruction-based
            // verify would have used different bytes than were signed.
            var reconstructed = String.Join("\r\n", mail.Body.NestedBodyparts.First().ToText().Select(l => l.TrimEnd()));
            Assert.That(reconstructed, Does.Not.Contain($"Hallo {Marker}   "), "reconstruction drops the trailing whitespace");

            // ...but verifying against the preserved raw bytes succeeds.
            var result = mail.VerifyPgpSignature(publicKeyRing);
            Assert.That(result.IsValid, Is.True, "raw-preserved verification must accept the foreign serialization");

        }

        [Test]
        public void Parsed_encrypted_mail_is_flagged_and_decrypts_with_the_recipient_key()
        {

            // The inbound flow: parse shows it as encrypted; supplying the key decrypts it.
            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: false, withEncryption: true).ToText());

            Assert.That(parsed.IsPgpEncrypted, Is.True, "a multipart/encrypted mail must be flagged as encrypted");
            Assert.That(parsed.IsPgpSigned,    Is.False);

            var decrypted = parsed.DecryptPgp(recipientSecretRing, Passphrase);

            Assert.That(String.Join("\r\n", decrypted.ToText()), Does.Contain(Marker),
                        "decrypting with the recipient's key must recover the plaintext body");

        }

        [Test]
        public void Parsed_multi_recipient_encrypted_mail_decrypts_for_each_recipient()
        {

            var b = new TextEMailBuilder { Text = $"Hallo {Marker}" };
            b.From          = AliceWithKey;
            b.To            = EMailAddressListBuilder.Create(BobWithKey, CarolWithKey);
            b.Subject       = "Test Subject";
            b.SecurityLevel = EMailSecurity.encrypt;
            b.Passphrase    = Passphrase;

            EMail mail   = b;
            var   parsed = EMail.Parse(mail.ToText());

            Assert.That(String.Join("\r\n", parsed.DecryptPgp(recipientSecretRing, Passphrase).ToText()), Does.Contain(Marker), "Bob decrypts");
            Assert.That(String.Join("\r\n", parsed.DecryptPgp(carolSecretRing,     Passphrase).ToText()), Does.Contain(Marker), "Carol decrypts");

        }

        [Test]
        public void Encrypted_signed_mail_decrypts_and_verifies_the_inner_signature()
        {

            // Hermod always signs when encrypting, so a decrypted inbound mail carries an inner signature
            // over the plaintext. DecryptAndVerify recovers the content AND its authenticity in one pass.
            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: false, withEncryption: true).ToText());

            var result = parsed.DecryptAndVerifyPgp(recipientSecretRing, Passphrase, publicKeyRing);

            Assert.That(String.Join("\r\n", result.Body.ToText()), Does.Contain(Marker), "the plaintext body must be recovered");
            Assert.That(result.IsSignatureValid,      Is.True, "the embedded signature must verify against Alice's key");
            Assert.That(result.Signature.Status,      Is.EqualTo(PgpVerificationStatus.Valid));
            Assert.That(result.Signature.SignerKeyId, Is.EqualTo(keyId), "the signer must be Alice");

        }

        [Test]
        public void DecryptAndVerify_with_the_wrong_sender_key_still_decrypts_but_reports_no_matching_key()
        {

            // Decryption depends only on the recipient key; verifying against a ring that lacks the
            // signer's key must still return the plaintext, but flag the signature as unverifiable.
            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: false, withEncryption: true).ToText());

            // recipientPublicRing is Bob's ring; the inner signature is Alice's.
            var result = parsed.DecryptAndVerifyPgp(recipientSecretRing, Passphrase, recipientPublicRing);

            Assert.That(String.Join("\r\n", result.Body.ToText()), Does.Contain(Marker), "the plaintext must still be recovered");
            Assert.That(result.Signature.Status,   Is.EqualTo(PgpVerificationStatus.NoMatchingKey));
            Assert.That(result.IsSignatureValid,   Is.False);

        }

        [Test]
        public void Decrypting_with_a_non_recipient_key_throws()
        {

            // Alice (the sender) is not a recipient, so her key cannot decrypt the message.
            var parsed = EMail.Parse(Build(html: false, withAttachment: false, withPgp: false, withEncryption: true).ToText());

            Assert.That(() => parsed.DecryptPgp(secretKeyRing, Passphrase),
                        Throws.TypeOf<Org.BouncyCastle.Bcpg.OpenPgp.PgpException>());

        }

        #endregion

        #region Edge cases

        [Test]
        public void Signing_without_a_key_throws()
        {

            var b = new TextEMailBuilder { Text = "hello" };
            b.From          = (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");   // no key ring
            b.To            = (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");
            b.Subject       = "no key";
            b.SecurityLevel = EMailSecurity.sign;
            b.Passphrase    = Passphrase;

            Assert.That(() => { EMail _ = b; }, Throws.TypeOf<ApplicationException>());

        }

        [Test]
        public void Serialized_message_uses_CRLF_and_a_header_body_separator()
        {

            var mail = Build(html: true, withAttachment: false, withPgp: false);
            var text = String.Join("\r\n", mail.ToText());

            Assert.That(text, Does.Contain("\r\n"));
            Assert.That(text, Does.Contain("\r\n\r\n"), "there must be a blank line between headers and body");

        }

        #endregion

    }

}
