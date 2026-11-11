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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// Key pins: computed, written and read. The vectors were made with
    /// OpenSSL 3.5, independently of this code:
    /// "openssl pkey -pubout -outform DER | openssl dgst -sha256".
    /// </summary>
    [TestFixture]
    public class KeyPinTests
    {

        #region Test vectors

        /// <summary>
        /// The SubjectPublicKeyInfo of a P-256 key made with "openssl genpkey".
        /// </summary>
        internal const String P256_SPKI        = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEMF0upo3A63JmhumocRbMEg77EH13AEA0NayUW9gMCPMGaS78jKpS6M9dl2E1RG09MtZcU7L5roXa/CUXMhpRAg==";
        internal const String P256_Pin         = "0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7e";

        /// <summary>
        /// A self-signed certificate of that key for "somewhere-else.example",
        /// valid for one day from 2026-11-11 on.
        /// </summary>
        internal const String P256_Certificate = "MIIBmDCCAT2gAwIBAgIUY+Zag9FJeTHGnTKxtvWJbVQqz5IwCgYIKoZIzj0EAwIwITEfMB0GA1UEAwwWc29tZXdoZXJlLWVsc2UuZXhhbXBsZTAeFw0yNjExMTEyMDEzNDRaFw0yNjExMTIyMDEzNDRaMCExHzAdBgNVBAMMFnNvbWV3aGVyZS1lbHNlLmV4YW1wbGUwWTATBgcqhkjOPQIBBggqhkjOPQMBBwNCAAQwXS6mjcDrcmaG6ahxFswSDvsQfXcAQDQ1rJRb2AwI8wZpLvyMqlLoz12XYTVEbT0y1lxTsvmuhdr8JRcyGlECo1MwUTAdBgNVHQ4EFgQUACJHvh/lzAogr5Bau4XAJPkBRmswHwYDVR0jBBgwFoAUACJHvh/lzAogr5Bau4XAJPkBRmswDwYDVR0TAQH/BAUwAwEB/zAKBggqhkjOPQQDAgNJADBGAiEArozWamDG4bcQr0X0Dv4emKDu90XPA1ajM4lwfOuVqMUCIQD3mEV/rWH5e8o2sTQnTdmbzZgxiTvPGivZ7qNMkdMrRQ==";

        /// <summary>
        /// The Ed25519 public key of RFC 8410, section 10.1.
        /// </summary>
        internal const String Ed25519_SPKI     = "MCowBQYDK2VwAyEAGb9ECWmEzf6FQbrBZ9w7lshQhqowtrbLDFw4rXAxZuE=";
        internal const String Ed25519_Pin      = "a1e9156054e04fac899ae9f275132cdc07a5dbc4ea2c2ad3a1ffc6e0d253681f";

        #endregion


        #region The pin is SHA-256 over the SubjectPublicKeyInfo

        [Test]
        public void P256_Key_HasThePinOpenSSLComputes()
        {
            Assert.That(KeyPin.FromSubjectPublicKeyInfo(Convert.FromBase64String(P256_SPKI)).ToHex(),
                        Is.EqualTo(P256_Pin));
        }

        [Test]
        public void Ed25519_Key_HasThePinOpenSSLComputes()
        {
            Assert.That(KeyPin.FromSubjectPublicKeyInfo(Convert.FromBase64String(Ed25519_SPKI)).ToHex(),
                        Is.EqualTo(Ed25519_Pin));
        }

        [Test]
        public void Certificate_HasThePinOfItsKey_NotOfItself()
        {

            using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(P256_Certificate));

            Assert.Multiple(() => {
                Assert.That(KeyPin.FromCertificate(certificate).ToHex(), Is.EqualTo(P256_Pin));
                Assert.That(Convert.ToHexStringLower(SHA256.HashData(certificate.RawData)),
                            Is.Not.EqualTo(P256_Pin),
                            "a certificate pin hashes the whole certificate, a key pin only the key");
            });

        }

        [Test]
        public void TwoCertificatesOfOneKey_HaveOneKeyPin()
        {

            using var key     = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var first   = new CertificateRequest("CN=first",  key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            using var renewed = new CertificateRequest("CN=second", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow,            DateTimeOffset.UtcNow.AddYears(1));

            Assert.Multiple(() => {
                Assert.That(KeyPin.FromCertificate(renewed), Is.EqualTo(KeyPin.FromCertificate(first)));
                Assert.That(KeyPin.FromCertificate(first),   Is.EqualTo(KeyPin.FromKey(key)));
            });

        }

        [Test]
        public void ServerCertificate_KeyPin_IsItsSubjectPublicKeyInfoPin()
        {

            using var certificate = Quic.Tls.ServerCertificate.CreateSelfSigned("localhost");

            Assert.That(certificate.KeyPin.ToBase64(), Is.EqualTo(certificate.SubjectPublicKeyInfoPin));

        }

        #endregion

        #region Written

        [Test]
        public void Written_InTheFourNotations()
        {

            var pin = KeyPin.Parse(P256_Pin);

            Assert.Multiple(() => {
                Assert.That(pin.ToString(),        Is.EqualTo(P256_Pin));
                Assert.That(pin.ToHex(),           Is.EqualTo(P256_Pin));
                Assert.That(pin.ToHexWithColons(), Is.EqualTo("0D:B1:41:0B:06:33:6E:54:18:C6:04:B9:7D:02:7A:26:0B:AF:35:66:7E:1F:6C:41:80:29:DB:F7:BF:C3:EF:7E"));
                Assert.That(pin.ToBase64(),        Is.EqualTo(Convert.ToBase64String(Convert.FromHexString(P256_Pin))));
                Assert.That(pin.ToCurl(),          Is.EqualTo("sha256//" + Convert.ToBase64String(Convert.FromHexString(P256_Pin))));
            });

        }

        #endregion

        #region Read

        [TestCase("0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7e")]
        [TestCase("0DB1410B06336E5418C604B97D027A260BAF35667E1F6C418029DBF7BFC3EF7E")]
        [TestCase("0D:B1:41:0B:06:33:6E:54:18:C6:04:B9:7D:02:7A:26:0B:AF:35:66:7E:1F:6C:41:80:29:DB:F7:BF:C3:EF:7E")]
        [TestCase("0d:b1:41:0b:06:33:6e:54:18:c6:04:b9:7d:02:7a:26:0b:af:35:66:7e:1f:6c:41:80:29:db:f7:bf:c3:ef:7e")]
        [TestCase("sha256//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=")]
        [TestCase("SHA256//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=")]
        [TestCase("  0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7e  ")]
        public void Read_InEveryNotation(String Text)
        {
            Assert.That(KeyPin.TryParse(Text, out var pin), Is.True, Text);
            Assert.That(pin!.ToHex(), Is.EqualTo(P256_Pin));
        }

        [Test]
        public void Read_InCurlsNotation()
        {

            var curl = "sha256//" + Convert.ToBase64String(Convert.FromHexString(P256_Pin));

            Assert.Multiple(() => {
                Assert.That(curl,                                       Is.EqualTo("sha256//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734="));
                Assert.That(KeyPin.Parse(curl).ToHex(),                 Is.EqualTo(P256_Pin));
                Assert.That(KeyPin.Parse(KeyPin.Parse(curl).ToCurl()),  Is.EqualTo(KeyPin.Parse(curl)));
            });

        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7")]       // 63 digits
        [TestCase("0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7e0")]     // 65 digits
        [TestCase("0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7g")]      // not hexadecimal
        [TestCase("0D:B1:41:0B:06:33:6E:54:18:C6:04:B9:7D:02:7A:26:0B:AF:35:66:7E:1F:6C:41:80:29:DB:F7:BF:C3:EF7E:")]  // a colon misplaced
        [TestCase("0D-B1-41-0B-06-33-6E-54-18-C6-04-B9-7D-02-7A-26-0B-AF-35-66-7E-1F-6C-41-80-29-DB-F7-BF-C3-EF-7E")]  // dashes
        [TestCase("DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=")]                          // base64 without curl's prefix
        [TestCase("sha256//DbFBCwYzblQYxgS5fQJ6Jguv")]                                      // base64 of 18 bytes
        [TestCase("sha256//DbFBCwYzblQYxgS5fQJ6Jguv NWZ+H2xBgCnb97/D734=")]                 // a blank inside the base64
        [TestCase("sha256//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=;sha256//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=")]  // two pins
        [TestCase("sha1//DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734=")]
        [TestCase("SHA256:DbFBCwYzblQYxgS5fQJ6JguvNWZ+H2xBgCnb97/D734")]                    // an OpenSSH fingerprint
        public void NotRead(String? Text)
        {
            Assert.Multiple(() => {
                Assert.That(KeyPin.TryParse(Text, out var pin), Is.False, Text);
                Assert.That(pin,                                 Is.Null);
            });
        }

        [Test]
        public void Parse_SaysWhatItExpects()
        {
            var e = Assert.Throws<FormatException>(() => KeyPin.Parse("0db1"));
            Assert.That(e!.Message, Does.Contain("64 hexadecimal digits"));
        }

        [Test]
        public void FromSHA256_TakesExactly32Bytes()
        {
            Assert.Multiple(() => {
                Assert.That(KeyPin.FromSHA256(Convert.FromHexString(P256_Pin)).ToHex(), Is.EqualTo(P256_Pin));
                Assert.Throws<ArgumentException>(() => KeyPin.FromSHA256(new Byte[31]));
                Assert.Throws<ArgumentException>(() => KeyPin.FromSHA256(new Byte[33]));
                Assert.Throws<ArgumentException>(() => KeyPin.FromSubjectPublicKeyInfo([]));
            });
        }

        #endregion

        #region Equality

        [Test]
        public void Equality_IsByValue()
        {

            var a = KeyPin.Parse(P256_Pin);
            var b = KeyPin.Parse(P256_Pin.ToUpperInvariant());
            var c = KeyPin.Parse(Ed25519_Pin);

            Assert.Multiple(() => {
                Assert.That(a == b,                       Is.True);
                Assert.That(a != c,                       Is.True);
                Assert.That(a.Equals((Object) b),         Is.True);
                Assert.That(a.GetHashCode(),              Is.EqualTo(b.GetHashCode()));
                Assert.That(new HashSet<KeyPin> { a, b }, Has.Count.EqualTo(1));
                Assert.That(a == null,                    Is.False);
            });

        }

        #endregion


        #region KeyPinPolicy

        [Test]
        public void Policy_HoldsAtMostFourPins()
        {

            var pins = Enumerable.Range(0, 5).Select(i => KeyPin.FromSHA256(Enumerable.Repeat((Byte) i, 32).ToArray())).ToArray();

            Assert.Multiple(() => {
                Assert.That(KeyPinPolicy.PinnedTo(pins[..4]).Pins, Has.Count.EqualTo(4));
                Assert.Throws<ArgumentException>(() => KeyPinPolicy.PinnedTo(pins));
                Assert.That(KeyPinPolicy.PinnedTo(pins[0], pins[0]).Pins, Has.Count.EqualTo(1), "the same pin twice is one pin");
            });

        }

        [Test]
        public void Policy_WithoutPins_NeedsTrustOnFirstUse()
        {

            // Without both the endpoint is CA-checked and has no key pin policy: there is no
            // policy that trusts nothing, and none that trusts everything.
            Assert.Multiple(() => {
                Assert.Throws<ArgumentException>(() => KeyPinPolicy.PinnedTo());
                Assert.That(KeyPinPolicy.LearnOnFirstUse().Pins, Is.Empty);
            });

        }

        [Test]
        public void Policy_Learned_HasTheOnePinItLearned()
        {

            var pin     = KeyPin.Parse(P256_Pin);
            var learned = KeyPinPolicy.LearnOnFirstUse().Learned(pin);

            Assert.Multiple(() => {
                Assert.That(learned.Pins,  Is.EqualTo(new[] { pin }));
                Assert.That(learned.State, Is.EqualTo(KeyPinState.Learned));
                Assert.Throws<InvalidOperationException>(() => learned.Learned(pin),                          "a policy with a pin learns no other");
                Assert.Throws<InvalidOperationException>(() => KeyPinPolicy.PinnedTo(pin).Learned(pin),       "a policy without trust on first use learns nothing");
                Assert.Throws<ArgumentException>        (() => new KeyPinPolicy([pin, KeyPin.Parse(Ed25519_Pin)], KeyPinState.Learned));
            });

        }

        #endregion

    }

}
