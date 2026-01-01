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

using NUnit.Framework;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.PKI;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// Generate rootCA certificates for a Public Key Infrastructure tests.
    /// </summary>
    [TestFixture]
    public class Generate_RootCA_Tests
    {

        #region (private static) ValidateRootCACertificate(RootCACertificate)

        /// <summary>
        /// Validate the given rootCA certificate.
        /// </summary>
        /// <param name="RootCACertificate">A X.509 rootCA certificate.</param>
        private static void ValidateRootCACertificate(X509Certificate RootCACertificate)
        {

            var publicKey = RootCACertificate.GetPublicKey();
            Assert.That(publicKey,                                                          Is.Not.Null);

            Assert.That(RootCACertificate,                                                  Is.Not.Null);
            Assert.That(RootCACertificate.Version,                                          Is.EqualTo(3),                                                         "Must be X.509v3 because of extensions!");
            Assert.That(RootCACertificate.SubjectDN.ToString(),                             Is.EqualTo    ("CN=Hermod RootCA"),                                    "Unexpected rootCA certificate subject DN!");
            Assert.That(RootCACertificate.IssuerDN. ToString(),                             Is.EqualTo    (RootCACertificate.SubjectDN.ToString()),                "RootCA must be self-issued!");

            var serialUnsigned          = RootCACertificate.SerialNumber.ToByteArrayUnsigned();
            Assert.That(serialUnsigned.Length,                                              Is.LessThanOrEqualTo(20),                                              "Serial must be ≤ 20 octets.");
            Assert.That(serialUnsigned.Length,                                              Is.GreaterThan(0),                                                     "Serial must be positive!");

            Assert.That(RootCACertificate.NotAfter,                                         Is.GreaterThan(RootCACertificate.NotBefore),                           "Validity range must be sane!");
            RootCACertificate.CheckValidity(Timestamp.Now.DateTime.ToUniversalTime());

            // Verify with the subject/issuer (self-signed) public key
            RootCACertificate.Verify(publicKey);


            // Extensions: Criticality Sets
            var criticalOids    = new HashSet<String>(RootCACertificate.GetCriticalExtensionOids());
            var nonCriticalOids = new HashSet<String>(RootCACertificate.GetNonCriticalExtensionOids());


            // Extensions: BasicConstraints (CA = true, optional pathLen)
            var basicConstraints        = PKIFactory.ParseBasicConstraints(RootCACertificate);
            Assert.That(basicConstraints,                                                   Is.Not.Null,                                                           "BasicConstraints must be present!");
            Assert.That(basicConstraints!.IsCA(),                                           Is.True,                                                               "RootCA must have CA=true!");
            if (basicConstraints.PathLenConstraint is not null)
                Assert.That(basicConstraints.PathLenConstraint.IntValue,                    Is.GreaterThanOrEqualTo(0),                                            "PathLength, when present must be >= 0!");
            Assert.That(criticalOids.Contains(X509Extensions.BasicConstraints.Id),          Is.True, "BasicConstraints must be critical!");

            // Extensions: KeyUsage (keyCertSign + cRLSign required for rootCA)
            var keyUsage                = PKIFactory.ParseKeyUsage(RootCACertificate);
            Assert.That(keyUsage,                                                           Is.Not.Null,                                                           "KeyUsage must be present!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.DigitalSignature),                       Is.False,                                                              "RootCA must not allow digital signatures!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.NonRepudiation),                         Is.False,                                                              "RootCA must not allow non-repudiation!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.KeyEncipherment),                        Is.False,                                                              "RootCA must not allow key encipherment!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.DataEncipherment),                       Is.False,                                                              "RootCA must not allow data encipherment!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.KeyAgreement),                           Is.False,                                                              "RootCA must not allow key agreement!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.KeyCertSign),                            Is.True,                                                               "RootCA must allow certificate signing!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.CrlSign),                                Is.True,                                                               "RootCA should allow CRL signing!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.EncipherOnly),                           Is.False,                                                              "RootCA must not allow encipher only!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.DecipherOnly),                           Is.False,                                                              "RootCA must not allow decipher only!");
            Assert.That(criticalOids.Contains(X509Extensions.KeyUsage.Id),                  Is.True, "KeyUsage must be critical!");

            // Extensions: Subject Key Identifier (SKI) present (20 bytes) & non-critical
            var subjectKeyIdentifier    = PKIFactory.ParseSubjectKeyIdentifier  (RootCACertificate);
            Assert.That(subjectKeyIdentifier,                                               Is.Not.Null,                                                           "SubjectKeyIdentifier should be present!");
            Assert.That(nonCriticalOids.Contains(X509Extensions.SubjectKeyIdentifier.Id),   Is.True,                                                               "SubjectKeyIdentifier should be non-critical!");
            Assert.That(subjectKeyIdentifier!.GetKeyIdentifier().Length,                    Is.EqualTo(20),                                                        "SubjectKeyIdentifier should be 20 bytes (SHA-1 keyIdentifier)!");

            // Extensions: Authority Key Identifier
            var authorityKeyIdentifier  = PKIFactory.ParseAuthorityKeyIdentifier(RootCACertificate);
            Assert.That(authorityKeyIdentifier,                              Is.Not.Null,                                                                          "AuthorityKeyIdentifier should be present!");
            Assert.That(nonCriticalOids.Contains(X509Extensions.AuthorityKeyIdentifier.Id), Is.True,                                                               "AuthorityKeyIdentifier should be non-critical.");

            // Extensions: SKI == AKI?
            var akiKid                  = authorityKeyIdentifier?.GetKeyIdentifier();
            Assert.That(akiKid,                                                             Is.Not.Null,                                                           "AuthorityKeyIdentifier.keyIdentifier must not be null for a rootCA!");
            Assert.That(subjectKeyIdentifier.GetKeyIdentifier().ToHexString(),              Is.EqualTo(akiKid!.ToHexString()),                                     "Root SKI.keyIdentifier and AKI.keyIdentifier must match!");


            // No EKU / SAN on root (policy choice). If present: fail!
            Assert.That(RootCACertificate.GetExtensionValue(X509Extensions.ExtendedKeyUsage),         Is.Null, "ExtendedKeyUsage should be absent on a rootCA!");
            Assert.That(RootCACertificate.GetExtensionValue(X509Extensions.SubjectAlternativeName),   Is.Null, "SubjectAlternativeNames should be absent on a rootCA!");




            // AIA/CRLDP: if present, must be non-critical and URI-based
            var crldpExt = RootCACertificate.GetExtensionValue(X509Extensions.CrlDistributionPoints);
            if (crldpExt is not null)
            {

                Assert.That(nonCriticalOids.Contains(X509Extensions.CrlDistributionPoints.Id), Is.True, "CRL DistributionPoints should be non-critical!");

                var dp = CrlDistPoint.GetInstance(X509ExtensionUtilities.FromExtensionValue(crldpExt));
                foreach (var distributionPoint in dp.GetDistributionPoints())
                {

                    var name = distributionPoint.DistributionPointName;
                    Assert.That(name?.Type, Is.EqualTo(DistributionPointName.FullName));

                    foreach (var gn in GeneralNames.GetInstance(name.Name).GetNames())
                        Assert.That(gn.TagNo, Is.EqualTo(GeneralName.UniformResourceIdentifier), "CRL DistributionPoints must be an Uniform Resource Identifier!");

                }

            }

            var aiaExt = RootCACertificate.GetExtensionValue(X509Extensions.AuthorityInfoAccess);
            if (aiaExt is not null)
            {

                Assert.That(nonCriticalOids.Contains(X509Extensions.AuthorityInfoAccess.Id), Is.True, "AuthorityInfoAccess should be non-critical!");

                var aia = AuthorityInformationAccess.GetInstance(X509ExtensionUtilities.FromExtensionValue(aiaExt));
                foreach (var accessDescription in aia.GetAccessDescriptions())
                {

                    var oid = accessDescription.AccessMethod;

                    Assert.That(oid.Equals(AccessDescription.IdADCAIssuers) ||
                                oid.Equals(AccessDescription.IdADOcsp),                             Is.True, "AIA must contain only caIssuers/OCSP!");

                    Assert.That(accessDescription.AccessLocation.TagNo,   Is.EqualTo(GeneralName.UniformResourceIdentifier), "AuthorityInfoAccess locations must be an Uniform Resource Identifier!");

                }
            }




            // --- DER RoundTrip
            var derBytes                = RootCACertificate.GetEncoded();
            var parsedCertificate       = new X509CertificateParser().ReadCertificate(derBytes);
            Assert.That(parsedCertificate,                                   Is.Not.Null);
            Assert.That(parsedCertificate.SubjectDN.ToString(),              Is.EqualTo(RootCACertificate.SubjectDN.ToString()));
            Assert.That(parsedCertificate.SerialNumber,                      Is.EqualTo(RootCACertificate.SerialNumber));
            parsedCertificate.Verify(publicKey);

        }

        #endregion


        #region Generate_ECC_RootCA_Test()

        /// <summary>
        /// Create a rootCA ECC key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_ECC_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateECCKeyPair();

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var ecPublicKey        = rootCAKeyPair.Public as ECPublicKeyParameters;
            Assert.That(ecPublicKey,                                       Is.Not.Null,                         "Public key must be ECC!");
            Assert.That(ecPublicKey!.Parameters,                           Is.Not.Null);
            Assert.That(ecPublicKey!.Parameters.Curve.FieldSize,           Is.GreaterThanOrEqualTo(256),        "EC field size must be >= 256 bits.");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(ecPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.2.840.10045.2.1"),     "SPKI must advertise 'ecPublicKey'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // ECDSA-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ECDSA"),               "Expect ECDSA signature!");
            Assert.That(rootCACertificate.SigAlgOid,                       Is.EqualTo("1.2.840.10045.4.3.2"),   "ecdsa-with-SHA256 expected!");

            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateECCKeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),             "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion

        #region Generate_ECC_secp521r1_RootCA_Test()

        /// <summary>
        /// Create a rootCA ECC secp521r1 key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_ECC_secp521r1_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateECCKeyPair("secp521r1");

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var ecPublicKey        = rootCAKeyPair.Public as ECPublicKeyParameters;
            Assert.That(ecPublicKey,                                       Is.Not.Null,                         "Public key must be ECC!");
            Assert.That(ecPublicKey!.Parameters,                           Is.Not.Null);
            Assert.That(ecPublicKey!.Parameters.Curve.FieldSize,           Is.GreaterThanOrEqualTo(512),        "EC field size must be >= 512 bits.");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(ecPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.2.840.10045.2.1"),     "SPKI must advertise 'ecPublicKey'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // ECDSA-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ECDSA"),               "Expect ECDSA signature!");
            Assert.That(rootCACertificate.SigAlgOid,                       Is.EqualTo("1.2.840.10045.4.3.4"),   "ecdsa-with-SHA512 expected!");

            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateECCKeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),             "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion


        #region Generate_Ed25519_RootCA_Test()

        /// <summary>
        /// Create a rootCA Ed25519 key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_Ed25519_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateEd25519KeyPair();

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var ed25519PublicKey     = rootCAKeyPair.Public as Ed25519PublicKeyParameters;
            Assert.That(ed25519PublicKey,                                  Is.Not.Null,                 "Public key must be ECC!");
            Assert.That(ed25519PublicKey!.GetEncoded().Length,             Is.EqualTo(32),              "Ed25519 public key must be 32 bytes (256 bits)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(ed25519PublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.3.101.112"),   "SPKI must advertise 'id-Ed25519'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // ED25519-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ED25519"),     "Expect ED25519 signature!");
            Assert.That(rootCACertificate.SigAlgOid,                       Is.EqualTo("1.3.101.112"),   "id-Ed25519 expected!");

            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateEd25519KeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),     "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion

        #region Generate_Ed448_RootCA_Test()

        /// <summary>
        /// Create a rootCA Ed448 key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_Ed448_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateEd448KeyPair();

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var ed448PublicKey     = rootCAKeyPair.Public as Ed448PublicKeyParameters;
            Assert.That(ed448PublicKey,                                    Is.Not.Null,                 "Public key must be ECC!");
            Assert.That(ed448PublicKey!.GetEncoded().Length,               Is.EqualTo(57),              "Ed448 public key must be 57 bytes (448 bits)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(ed448PublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.3.101.113"),   "SPKI must advertise 'id-Ed448'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // ED448-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ED448"),       "Expect ED448 signature!");
            Assert.That(rootCACertificate.SigAlgOid,                       Is.EqualTo("1.3.101.113"),   "id-Ed448 expected!");

            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateEd448KeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),     "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion


        #region Generate_MLDSA_RootCA_Test()

        /// <summary>
        /// Create a rootCA ML-DSA key pair and certificate.
        /// 
        /// Module-Lattice-based Digital Signature Algorithm (ML-DSA/"Dilithium")
        /// is a post-quantum digital signature algorithm replacing ECDSA/EdDSA.
        /// </summary>
        [Test]
        public void Generate_MLDSA_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateMLDSAKeyPair();

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var mlDSAPublicKey     = rootCAKeyPair.Public as MLDsaPublicKeyParameters;
            Assert.That(mlDSAPublicKey,                                    Is.Not.Null,                             "Public key must be ML-DSA!");
            Assert.That(mlDSAPublicKey!.GetEncoded().Length,               Is.EqualTo(1952),                        "ML-DSA public key must be 1952 bytes (15616 bits)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(mlDSAPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("2.16.840.1.101.3.4.3.18"),   "SPKI must advertise 'id-ml-dsa-65'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // ML-DSA-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ML-DSA-65"),               "Expect ML-DSA-65 signature!");
            Assert.That(rootCACertificate.SigAlgOid,                       Is.EqualTo("2.16.840.1.101.3.4.3.18"),   "id-ml-dsa-65 expected!");

            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateMLDSAKeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),                 "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion

        #region Generate_MLKEM_RootCA_Test()

        /// <summary>
        /// Create a rootCA ML-KEM key pair and certificate,
        /// but it will fail because ML-KEM is not supported for signing!
        /// 
        /// Module-Lattice-based Key Encapsulation Mechanism (ML-KEM/"Kyber")
        /// is a post-quantum key encapsulation mechanism replacing
        /// the traditional Diffie-Hellman key exchange.
        /// </summary>
        [Test]
        public void Generate_MLKEM_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateMLKEMKeyPair();

            Assert.That(rootCAKeyPair,                         Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                 Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                  Is.Not.Null);

            var mlKEMPublicKey     = rootCAKeyPair.Public as MLKemPublicKeyParameters;
            Assert.That(mlKEMPublicKey,                        Is.Not.Null,                            "Public key must be ML-KEM!");
            Assert.That(mlKEMPublicKey!.GetEncoded().Length,   Is.EqualTo(1184),                       "ML-KEM public key must be 1184 bytes (9472 bits)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(mlKEMPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,           Is.EqualTo("2.16.840.1.101.3.4.4.2"),   "SPKI must advertise 'kems'!");


            Assert.Throws<NotSupportedException>(() => PKIFactory.CreateRootCACertificate(
                                                           RootKeyPair:              rootCAKeyPair,
                                                           SubjectName:             "Hermod RootCA",
                                                           CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                                           AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                                           AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                                           CertificatePolicies:      [
                                                                                         new CertificatePolicy(
                                                                                             "1.3.6.1.4.1.99999.2.10",
                                                                                             URL.Parse("https://pki.example.com/cps.html")
                                                                                         )
                                                                                     ]
                                                       ));

        }

        #endregion


        #region Generate_RSA_RootCA_Test()

        /// <summary>
        /// Create a rootCA RSA key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_RSA_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateRSAKeyPair();

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var rsaPublicKey       = rootCAKeyPair.Public as RsaKeyParameters;
            Assert.That(rsaPublicKey,                                      Is.Not.Null,                          "Public key must be RSA!");
            Assert.That(rsaPublicKey!.IsPrivate,                           Is.False);
            Assert.That(rsaPublicKey.Modulus,                              Is.Not.Null);
            Assert.That(rsaPublicKey.Modulus.BitLength,                    Is.EqualTo(4096),                     "RSA modulus must be 4096 bits!");
            Assert.That(rsaPublicKey.Exponent,                             Is.Not.Null);
            Assert.That(rsaPublicKey.Exponent.IntValue,                    Is.EqualTo(65537),                    "Unexpected public exponent (expected 65537)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(rsaPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.2.840.113549.1.1.1"),   "SPKI must advertise 'rsaEncryption'!");

            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // RSA-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("RSA"),                      "Root should be signed with an RSA algorithm (PKCS#1 v1.5 or RSASSA-PSS)!");

            // Check for weak signature algorithms
            var sigOid             = rootCACertificate.SigAlgOid;
            Assert.That(sigOid,                                            Is.Not.EqualTo("1.2.840.113549.1.1.4"),   "Weak signature algorithm 'md5-with-rsa-signature' is not allowed!");
            Assert.That(sigOid,                                            Is.Not.EqualTo("1.2.840.113549.1.1.5"),   "Weak signature algorithm 'sha1-with-rsa-signature' is not allowed!");

            var allowedRsa         = new HashSet<String> {
                                         "1.2.840.113549.1.1.10", // RSASSA-PSS
                                         "1.2.840.113549.1.1.11", // sha256WithRSAEncryption
                                         "1.2.840.113549.1.1.12", // sha384WithRSAEncryption
                                         "1.2.840.113549.1.1.13"  // sha512WithRSAEncryption
                                     };
            Assert.That(allowedRsa.Contains(sigOid),                       Is.True,                                 $"Unexpected signature algorithm OID: {sigOid}!");


            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateRSAKeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),                 "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion

        #region Generate_RSA2048_RootCA_Test()

        /// <summary>
        /// Create a rootCA RSA 2048 key pair and certificate.
        /// </summary>
        [Test]
        public void Generate_RSA2048_RootCA_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateRSAKeyPair(2048);

            Assert.That(rootCAKeyPair,                                     Is.Not.Null);
            Assert.That(rootCAKeyPair.Private,                             Is.Not.Null);
            Assert.That(rootCAKeyPair.Public,                              Is.Not.Null);

            var rsaPublicKey       = rootCAKeyPair.Public as RsaKeyParameters;
            Assert.That(rsaPublicKey,                                      Is.Not.Null,                          "Public key must be RSA!");
            Assert.That(rsaPublicKey!.IsPrivate,                           Is.False);
            Assert.That(rsaPublicKey.Modulus,                              Is.Not.Null);
            Assert.That(rsaPublicKey.Modulus.BitLength,                    Is.EqualTo(2048),                     "RSA modulus must be 2048 bits!");
            Assert.That(rsaPublicKey.Exponent,                             Is.Not.Null);
            Assert.That(rsaPublicKey.Exponent.IntValue,                    Is.EqualTo(65537),                    "Unexpected public exponent (expected 65537)!");

            var spki               = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(rsaPublicKey);
            Assert.That(spki.Algorithm.Algorithm.Id,                       Is.EqualTo("1.2.840.113549.1.1.1"),   "SPKI must advertise 'rsaEncryption'!");


            var rootCACertificate  = PKIFactory.CreateRootCACertificate(
                                         RootKeyPair:              rootCAKeyPair,
                                         SubjectName:             "Hermod RootCA",
                                         CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/root.crl") ],
                                         AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                         AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/root.cer") ],
                                         CertificatePolicies:      [
                                                                       new CertificatePolicy(
                                                                           "1.3.6.1.4.1.99999.2.10",
                                                                           URL.Parse("https://pki.example.com/cps.html")
                                                                       )
                                                                   ]
                                     );

            ValidateRootCACertificate(rootCACertificate);


            // RSA-specific certificate checks
            Assert.That(rootCACertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("RSA"),                      "Root should be signed with an RSA algorithm (PKCS#1 v1.5 or RSASSA-PSS)!");

            // Check for weak signature algorithms
            var sigOid             = rootCACertificate.SigAlgOid;
            Assert.That(sigOid,                                            Is.Not.EqualTo("1.2.840.113549.1.1.4"),   "Weak signature algorithm 'md5-with-rsa-signature' is not allowed!");
            Assert.That(sigOid,                                            Is.Not.EqualTo("1.2.840.113549.1.1.5"),   "Weak signature algorithm 'sha1-with-rsa-signature' is not allowed!");

            var allowedRsa         = new HashSet<String> {
                                         "1.2.840.113549.1.1.10", // RSASSA-PSS
                                         "1.2.840.113549.1.1.11", // sha256WithRSAEncryption
                                         "1.2.840.113549.1.1.12", // sha384WithRSAEncryption
                                         "1.2.840.113549.1.1.13"  // sha512WithRSAEncryption
                                     };
            Assert.That(allowedRsa.Contains(sigOid),                       Is.True,                                 $"Unexpected signature algorithm OID: {sigOid}!");


            // Verification of the rootCA signature with a different key must fail
            var wrongKeyPair       = PKIFactory.GenerateRSAKeyPair();
            Assert.Throws<InvalidKeyException>(() => rootCACertificate.Verify(wrongKeyPair.Public),                 "Verification of the rootCA signature with a different key must fail!");

        }

        #endregion

    }

}
