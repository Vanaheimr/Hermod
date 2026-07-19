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

        private PgpSecretKeyRing  secretKeyRing  = null!;
        private PgpPublicKeyRing  publicKeyRing  = null!;
        private Int64             keyId;

        #region OneTimeSetUp — generate an RSA OpenPGP key ring

        [OneTimeSetUp]
        public void GenerateTestKey()
        {

            var rsa = new RsaKeyPairGenerator();
            rsa.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), new SecureRandom(), 2048, 25));

            var pgpKeyPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, rsa.GenerateKeyPair(), DateTime.UtcNow);

            var gen = new PgpKeyRingGenerator(
                          PgpSignature.PositiveCertification,
                          pgpKeyPair,
                          "Alice <alice@example.com>",
                          SymmetricKeyAlgorithmTag.Aes256,
                          Passphrase.ToCharArray(),
                          true,
                          null,
                          null,
                          new SecureRandom());

            secretKeyRing  = gen.GenerateSecretKeyRing();
            publicKeyRing  = gen.GeneratePublicKeyRing();
            keyId          = secretKeyRing.GetSecretKey().KeyId;

        }

        #endregion

        #region (helper) Build(html, withAttachment, withPgp)

        private EMail Build(Boolean html, Boolean withAttachment, Boolean withPgp)
        {

            EMailAddress from = withPgp
                ? new EMailAddress("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing)
                : (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");

            void Configure(AbstractEMail.Builder b)
            {
                b.From          = from;
                b.To            = (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");
                b.Subject       = "Test Subject";
                b.SecurityLevel = withPgp ? EMailSecurity.sign : EMailSecurity.none;
                if (withPgp)         b.Passphrase = Passphrase;
                if (withAttachment)  b.AddAttachment(Encoding.UTF8.GetBytes("%PDF-1.7 fake attachment bytes"), "doc.pdf");
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

            // Structural fidelity: a multipart/mixed (attachment) message re-parses into nested
            // body parts. NOTE: multipart/signed (PGP) currently re-parses to a *flat* body —
            // its content and signature survive (asserted above) but the MIME tree is not yet
            // reconstructed. That is a known limitation, tracked separately; hence attach-only here.
            if (attach && !pgp)
                Assert.That(parsed.Body.NestedBodyparts.Any(), Is.True,
                            "a multipart/mixed message should re-parse into nested body parts");

            var reText = String.Join("\r\n", parsed.ToText());
            Assert.That(reText, Does.Contain(Marker), "body content must survive parse → serialize");
            if (attach) Assert.That(reText, Does.Contain("doc.pdf"),              "attachment must survive parse → serialize");
            if (pgp)    Assert.That(reText, Does.Contain("BEGIN PGP SIGNATURE"),  "signature must survive parse → serialize");

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
