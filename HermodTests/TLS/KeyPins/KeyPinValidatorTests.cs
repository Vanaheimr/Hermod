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

using System.Formats.Asn1;
using System.Net.Security;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// The key pin validator: what it accepts, learns and refuses, and that it
    /// looks at the key and at nothing else.
    /// </summary>
    [TestFixture]
    public class KeyPinValidatorTests
    {

        #region (helpers)

        private static X509Certificate2 Certificate(ECDsa           Key,
                                                    String          Subject     = "CN=localhost",
                                                    DateTimeOffset? NotBefore   = null,
                                                    DateTimeOffset? NotAfter    = null)

            => new CertificateRequest(Subject, Key, HashAlgorithmName.SHA256).
                   CreateSelfSigned(NotBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
                                    NotAfter  ?? DateTimeOffset.UtcNow.AddDays(1));

        private static Byte[] SPKI(String Base64)
            => Convert.FromBase64String(Base64);

        private static Byte[] SPKI(String AlgorithmOID, String? ParameterOID, Byte[] Key, Boolean Null = false)
        {

            var writer = new AsnWriter(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {

                using (writer.PushSequence())
                {

                    writer.WriteObjectIdentifier(AlgorithmOID);

                    if (ParameterOID is not null)
                        writer.WriteObjectIdentifier(ParameterOID);

                    if (Null)
                        writer.WriteNull();

                }

                writer.WriteBitString(Key);

            }

            return writer.Encode();

        }

        private static readonly KeyPinPolicy AnyOtherKey = KeyPinPolicy.PinnedTo(KeyPin.Parse(KeyPinTests.Ed25519_Pin));

        #endregion


        #region Accepted, learned, refused

        [Test]
        public void PinnedKey_IsAccepted()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);

            var validation = KeyPinValidator.Validate(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)), certificate);

            Assert.Multiple(() => {
                Assert.That(validation.Outcome,          Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(validation.IsAccepted,       Is.True);
                Assert.That(validation.Pin,              Is.EqualTo(KeyPin.FromKey(key)));
                Assert.That(validation.CertificateType,  Is.EqualTo(TLSCertificateType.X509));
            });

        }

        [Test]
        public void AnotherKey_IsRefused_AndTheReasonNamesItsPin()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);

            var validation = KeyPinValidator.Validate(AnyOtherKey, certificate);

            Assert.Multiple(() => {
                Assert.That(validation.Outcome,     Is.EqualTo(KeyPinOutcome.Refused));
                Assert.That(validation.IsAccepted,  Is.False);
                Assert.That(validation.Pin,         Is.EqualTo(KeyPin.FromKey(key)));
                Assert.That(validation.Reason,      Does.Contain(KeyPin.FromKey(key).ToHex()));
            });

        }

        [Test]
        public void OneOfFourPins_IsEnough()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);

            var policy = KeyPinPolicy.PinnedTo(KeyPin.Parse(KeyPinTests.Ed25519_Pin),
                                               KeyPin.Parse(KeyPinTests.P256_Pin),
                                               KeyPin.FromKey(key),
                                               KeyPin.FromSHA256(new Byte[32]));

            Assert.That(KeyPinValidator.Validate(policy, certificate).Outcome, Is.EqualTo(KeyPinOutcome.Accepted));

        }

        [Test]
        public void NoCertificate_IsRefused()
        {
            Assert.That(KeyPinValidator.Validate(AnyOtherKey,                    null).Outcome, Is.EqualTo(KeyPinOutcome.Refused));
            Assert.That(KeyPinValidator.Validate(KeyPinPolicy.LearnOnFirstUse(), null).Outcome, Is.EqualTo(KeyPinOutcome.Refused), "nothing to learn");
        }

        [Test]
        public void WithoutPins_TrustOnFirstUse_LearnsTheKey()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);

            var validation = KeyPinValidator.Validate(KeyPinPolicy.LearnOnFirstUse(), certificate);

            Assert.Multiple(() => {
                Assert.That(validation.Outcome,     Is.EqualTo(KeyPinOutcome.Learned));
                Assert.That(validation.IsAccepted,  Is.True);
                Assert.That(validation.Pin,         Is.EqualTo(KeyPin.FromKey(key)));
            });

            // ...and holds the next connection to it.
            using var other   = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var another = Certificate(other);

            var learned = KeyPinPolicy.LearnOnFirstUse().Learned(validation.Pin!);

            Assert.Multiple(() => {
                Assert.That(KeyPinValidator.Validate(learned, certificate).Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(learned, another).    Outcome, Is.EqualTo(KeyPinOutcome.Refused));
            });

        }

        #endregion

        #region The key, and nothing else

        [Test]
        public void NameAndValidity_AreNotLookedAt()
        {

            using var key     = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var expired = Certificate(key, "CN=somebody-else.example", DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));
            using var future  = Certificate(key, "CN=nobody",                DateTimeOffset.UtcNow.AddYears(1),  DateTimeOffset.UtcNow.AddYears(2));

            var policy = KeyPinPolicy.PinnedTo(KeyPin.FromKey(key));

            Assert.Multiple(() => {
                Assert.That(KeyPinValidator.Validate(policy, expired).Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(policy, future). Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
            });

        }

        [Test]
        public void PolicyErrorsOfSslStream_AreIgnored_BothWays()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);
            using var other       = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var stranger    = Certificate(other);

            var handler  = KeyPinValidator.ForServerCertificates<Object>(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)));
            var errors   = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

            Assert.Multiple(() => {

                // The pinned key with every policy error: accepted.
                Assert.That(handler(this, certificate, null, this, errors).               IsValid, Is.True);

                // Another key that the operating system would accept: refused. No way back to the CA.
                Assert.That(handler(this, stranger,    null, this, SslPolicyErrors.None). IsValid, Is.False);

                // The same for SslStream's own callback and for client certificates.
                Assert.That(KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)))(this, certificate, null, errors),                Is.True);
                Assert.That(KeyPinValidator.ForSslStream(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)))(this, stranger,    null, SslPolicyErrors.None),  Is.False);
                Assert.That(KeyPinValidator.ForClientCertificates<Object>(KeyPinPolicy.PinnedTo(KeyPin.FromKey(key)))(this, stranger, null, this, SslPolicyErrors.None).IsValid, Is.False);

            });

        }

        [Test]
        public void Handlers_TellEveryResult_AndAskThePolicyEachTime()
        {

            using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var certificate = Certificate(key);

            var policy   = KeyPinPolicy.LearnOnFirstUse();
            var told     = new List<KeyPinValidation>();
            var handler  = KeyPinValidator.ForServerCertificates<Object>(() => policy,
                                                                         validation => {
                                                                             told.Add(validation);
                                                                             if (validation.Outcome == KeyPinOutcome.Learned)
                                                                                 policy = policy.Learned(validation.Pin!);
                                                                         });

            var first  = handler(this, certificate, null, this, SslPolicyErrors.RemoteCertificateChainErrors);
            var second = handler(this, certificate, null, this, SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.Multiple(() => {
                Assert.That(first. IsValid,              Is.True);
                Assert.That(first. Warnings.Count(),     Is.EqualTo(1), "a learned key is said so");
                Assert.That(second.IsValid,              Is.True);
                Assert.That(told.Select(t => t.Outcome), Is.EqualTo(new[] { KeyPinOutcome.Learned, KeyPinOutcome.Accepted }));
            });

        }

        #endregion

        #region Raw public keys

        [Test]
        public void RawPublicKey_OnlyWhereOffered()
        {

            var spki    = SPKI(KeyPinTests.P256_SPKI);
            var pin     = KeyPin.Parse(KeyPinTests.P256_Pin);

            Assert.Multiple(() => {
                Assert.That(KeyPinValidator.Validate(new KeyPinPolicy([pin], RawPublicKeys: RawPublicKeyMode.Off),     spki, TLSCertificateType.RawPublicKey).Outcome, Is.EqualTo(KeyPinOutcome.Refused),  "not offered");
                Assert.That(KeyPinValidator.Validate(new KeyPinPolicy([pin], RawPublicKeys: RawPublicKeyMode.Prefer),  spki, TLSCertificateType.RawPublicKey).Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(new KeyPinPolicy([pin], RawPublicKeys: RawPublicKeyMode.Require), spki, TLSCertificateType.RawPublicKey).Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(new KeyPinPolicy([pin], RawPublicKeys: RawPublicKeyMode.Prefer),  spki, TLSCertificateType.X509).        Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(new KeyPinPolicy([pin], RawPublicKeys: RawPublicKeyMode.Require), spki, TLSCertificateType.X509).        Outcome, Is.EqualTo(KeyPinOutcome.Refused),  "only raw keys required");
            });

        }

        #endregion

        #region The SubjectPublicKeyInfo is checked strictly

        private static KeyPinValidation ValidateRaw(Byte[] SubjectPublicKeyInfo)

            => KeyPinValidator.Validate(new KeyPinPolicy([], RawPublicKeys: RawPublicKeyMode.Prefer, TrustOnFirstUse: true),
                                        SubjectPublicKeyInfo,
                                        TLSCertificateType.RawPublicKey);

        [Test]
        public void KnownKeys_AreAccepted()
        {

            using var p384  = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            using var p521  = ECDsa.Create(ECCurve.NamedCurves.nistP521);

            Assert.Multiple(() => {
                Assert.That(ValidateRaw(SPKI(KeyPinTests.P256_SPKI)).      Outcome, Is.EqualTo(KeyPinOutcome.Learned), "P-256");
                Assert.That(ValidateRaw(p384.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned), "P-384");
                Assert.That(ValidateRaw(p521.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned), "P-521");
                Assert.That(ValidateRaw(SPKI(KeyPinTests.Ed25519_SPKI)).   Outcome, Is.EqualTo(KeyPinOutcome.Learned), "Ed25519");
            });

        }

        [Test]
        public void MLDsaKeys_AreAccepted()
        {

            Assume.That(MLDsa.IsSupported, "ML-DSA is not supported by this platform's crypto library.");

            foreach (var scheme in new[] { Quic.Tls.SignatureScheme.MLDsa44, Quic.Tls.SignatureScheme.MLDsa65, Quic.Tls.SignatureScheme.MLDsa87 })
            {
                using var mlDsa = Quic.Tls.ServerCertificate.CreateSelfSignedMLDsa(scheme: scheme);
                Assert.That(ValidateRaw(mlDsa.Certificate.PublicKey.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned), scheme.ToString());
            }

        }

        [Test]
        public void ManyGeneratedKeys_AreOnTheirCurves()
        {

            // A wrong constant of a curve would refuse real keys, and only sometimes.
            for (var i = 0; i < 50; i++)
            {

                using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
                using var p521 = ECDsa.Create(ECCurve.NamedCurves.nistP521);

                Assert.That(ValidateRaw(p256.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned));
                Assert.That(ValidateRaw(p384.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned));
                Assert.That(ValidateRaw(p521.ExportSubjectPublicKeyInfo()).Outcome, Is.EqualTo(KeyPinOutcome.Learned));

                var ed25519 = new Ed25519PrivateKeyParameters(new Org.BouncyCastle.Security.SecureRandom()).GeneratePublicKey().GetEncoded();
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, ed25519)).Outcome, Is.EqualTo(KeyPinOutcome.Learned));

            }

        }

        [Test]
        public void TrailingData_IsRefused()
        {

            var spki = SPKI(KeyPinTests.P256_SPKI);

            var validation = ValidateRaw([.. spki, 0x00]);

            Assert.Multiple(() => {
                Assert.That(validation.Outcome,  Is.EqualTo(KeyPinOutcome.Refused));
                Assert.That(validation.Pin,      Is.Null, "nothing is hashed before the check");
                Assert.That(validation.Reason,   Does.Contain("trailing data"));
            });

        }

        [Test]
        public void UnknownAlgorithmOrCurve_IsRefused()
        {

            var point = ReadKey(SPKI(KeyPinTests.P256_SPKI));

            using var ed448 = Quic.Tls.ServerCertificate.CreateSelfSignedEd448();

            Assert.Multiple(() => {
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.3.132.0.10",        point)).Reason, Does.Contain("unknown curve"),     "secp256k1");
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", null,                  point)).Reason, Does.Contain("without its curve"));
                Assert.That(ValidateRaw(SPKI("1.2.3.4",           null,                  point)).Reason, Does.Contain("unknown algorithm"));
                Assert.That(ValidateRaw(ed448.Certificate.PublicKey.ExportSubjectPublicKeyInfo()).Reason, Does.Contain("unknown algorithm"), "Ed448 is not one of the key types");
            });

        }

        [Test]
        public void ECPoints_NotOnTheCurve_AreRefused()
        {

            var point     = ReadKey(SPKI(KeyPinTests.P256_SPKI));

            var offCurve  = point.ToArray();
            offCurve[^1] ^= 0x01;

            var compressed = new Byte[33];
            compressed[0] = (Byte) (0x02 | (point[^1] & 1));
            point.AsSpan(1, 32).CopyTo(compressed.AsSpan(1));

            // x = p: outside the field
            var outside   = point.ToArray();
            Convert.FromHexString("ffffffff00000001000000000000000000000000ffffffffffffffffffffffff").CopyTo(outside, 1);

            Assert.Multiple(() => {
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.2.840.10045.3.1.7", point)).     Outcome, Is.EqualTo(KeyPinOutcome.Learned), "the unchanged point");
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.2.840.10045.3.1.7", offCurve)).  Reason,  Does.Contain("not on the curve"));
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.2.840.10045.3.1.7", compressed)).Reason,  Does.Contain("not an uncompressed point"));
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.2.840.10045.3.1.7", outside)).   Reason,  Does.Contain("outside the field"));
                Assert.That(ValidateRaw(SPKI("1.2.840.10045.2.1", "1.3.132.0.34",        point)).     Reason,  Does.Contain("not an uncompressed point"), "a P-256 point said to be on P-384");
            });

        }

        [Test]
        public void Ed25519_AgreesWithBouncyCastle_OnWhichEncodingsArePoints()
        {

            // Every y from 0 to 199 with both signs of x: the check here and BouncyCastle's
            // decoding have to say the same, and some of them are no points at all.
            var points    = 0;
            var noPoints  = 0;

            for (var y = 0; y < 200; y++)
            {
                foreach (var sign in new[] { false, true })
                {

                    var encoded = new BigInteger(y).ToByteArray(isUnsigned: true, isBigEndian: false);
                    var key     = new Byte[32];
                    encoded.CopyTo(key, 0);
                    if (sign)
                        key[31] |= 0x80;

                    Boolean bouncyCastle;
                    try
                    {
                        var parameters = new Ed25519PublicKeyParameters(key);
                        bouncyCastle   = true;
                    }
                    catch (ArgumentException)
                    {
                        bouncyCastle   = false;
                    }

                    var here = ValidateRaw(SPKI("1.3.101.112", null, key)).IsAccepted;

                    Assert.That(here, Is.EqualTo(bouncyCastle), $"y = {y}, sign = {sign}");

                    if (here) points++; else noPoints++;

                }
            }

            Assert.Multiple(() => {
                Assert.That(points,   Is.GreaterThan(0));
                Assert.That(noPoints, Is.GreaterThan(0));
            });

        }

        [Test]
        public void Ed25519_OfSmallOrder_IsRefused()
        {

            // y = 1: the neutral point. y = 0: a point of order 4. y = p - 1: a point of order 2.
            var neutral = new Byte[32]; neutral[0] = 1;
            var order4  = new Byte[32];
            var order2  = Convert.FromHexString("ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f");

            Assert.Multiple(() => {
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, neutral)).Reason, Does.Contain("small order"));
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, order4)). Reason, Does.Contain("small order"));
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, order2)). Reason, Does.Contain("small order"));
            });

        }

        [Test]
        public void Ed25519_OutsideTheFieldOrOfTheWrongLength_IsRefused()
        {

            // y = p = 2^255 - 19, little endian
            var p = Convert.FromHexString("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f");

            Assert.Multiple(() => {
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, p)).          Reason, Does.Contain("outside the field"));
                Assert.That(ValidateRaw(SPKI("1.3.101.112", null, new Byte[31])).Reason, Does.Contain("31 bytes"));
                Assert.That(ValidateRaw(SPKI("1.3.101.112", "1.2.3", new Byte[32])).Reason, Does.Contain("with parameters"));
            });

        }

        [Test]
        public void RSA_InACertificate_ButNotRaw()
        {

            using var rsa = RSA.Create(2048);
            var spki      = rsa.ExportSubjectPublicKeyInfo();

            var policy    = new KeyPinPolicy([KeyPin.FromKey(rsa)], RawPublicKeys: RawPublicKeyMode.Prefer);

            Assert.Multiple(() => {
                Assert.That(KeyPinValidator.Validate(policy, spki, TLSCertificateType.X509).        Outcome, Is.EqualTo(KeyPinOutcome.Accepted));
                Assert.That(KeyPinValidator.Validate(policy, spki, TLSCertificateType.RawPublicKey).Reason,  Does.Contain("raw RSA key"));
            });

        }

        [Test]
        public void MLDsa_OfTheWrongLength_IsRefused()
        {
            Assert.That(ValidateRaw(SPKI("2.16.840.1.101.3.4.3.17", null, new Byte[1311])).Reason, Does.Contain("1311 bytes instead of 1312"));
        }

        [Test]
        public void NotDER_IsRefused()
        {
            Assert.Multiple(() => {
                Assert.That(ValidateRaw([0x30, 0x80, 0x00, 0x00]).Outcome, Is.EqualTo(KeyPinOutcome.Refused), "indefinite length");
                Assert.That(ValidateRaw([0x04, 0x01, 0x00]).      Outcome, Is.EqualTo(KeyPinOutcome.Refused), "not a SEQUENCE");
                Assert.That(ValidateRaw([0x30]).                  Outcome, Is.EqualTo(KeyPinOutcome.Refused), "truncated");
            });
        }

        private static Byte[] ReadKey(Byte[] SubjectPublicKeyInfo)
        {
            var spki = new AsnReader(SubjectPublicKeyInfo, AsnEncodingRules.DER).ReadSequence();
            spki.ReadSequence();
            return spki.ReadBitString(out _);
        }

        #endregion

    }

}
