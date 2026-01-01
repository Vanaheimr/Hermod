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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Pqc.Crypto.Falcon;
using Org.BouncyCastle.Asn1.Pkcs;

using BCx509 = Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    /// <summary>
    /// Generating a RSA and/or ECC Public Key Infrastructure for testing.
    /// </summary>
    public static class PKIFactory
    {

        #region (static) RandomSerial             (Length = 16)

        public static BigInteger RandomSerial(Byte Length = 16)
        {

            if (Length < 8)
                throw new ArgumentOutOfRangeException(nameof(Length), "Minimum 8 bytes for security reasons!");

            // For production CAs, track issued serials per issuer to avoid collisions (RFC 5280).

            var rnd    = new SecureRandom();
            var bytes  = new Byte[Length];

            do
            {

                rnd.NextBytes(bytes);

                // Ensure positive: set high bit to 0
                bytes[0] &= 0x7F;

                var serial = new BigInteger(1, bytes);

                // Avoid all-zero.
                if (!serial.Equals(BigInteger.Zero))
                    return serial;

            } while (true);

        }

        #endregion

        #region (static) SelectSignatureAlgorithm (SigningKey)

        private static String SelectSignatureAlgorithm(//AsymmetricKeyParameter  SigningKey,
                                                       AsymmetricKeyParameter  SigningKey)
        {

            // --- EdDSA (RFC 8032 / 8410 names) ---
            if (SigningKey is Ed25519PublicKeyParameters)
                return "Ed25519";   // pure Ed25519

            if (SigningKey is Ed448PublicKeyParameters)
                return "Ed448";     // pure Ed448


            if (SigningKey is RsaKeyParameters rsaKey)
            {

                // Prefer RSASSA-PSS for security reasons!
                var bits = rsaKey.Modulus.BitLength;
                if      (bits >= 4096)  return "SHA512WITHRSAANDMGF1"; // BSI TR-02102 v2025-01
                else if (bits >= 3072)  return "SHA384WITHRSAANDMGF1"; // NIST SP 800-57 Part 1 Rev. 5
                else                    return "SHA256WITHRSAANDMGF1";

                // If you still must use PKCS#1 v1.5, use "SHA256WITHRSA".

            }

            if (SigningKey is ECPublicKeyParameters ecPrivateKey)
            {

                // Pick hash by elliptic curve strength
                var fieldSize = ecPrivateKey.Parameters.Curve.FieldSize;

                if      (fieldSize >= 521)  return "SHA512WITHECDSA";
                else if (fieldSize >= 384)  return "SHA384WITHECDSA";
                else                        return "SHA256WITHECDSA";

            }

            if (SigningKey is MLDsaPublicKeyParameters mlDSAPrivateKey)
            {

                var parameters = mlDSAPrivateKey.Parameters;

                if (parameters == MLDsaParameters.ml_dsa_44)
                    return "ML-DSA-44";   // ~128-bit security (NIST Level 1)

                if (parameters == MLDsaParameters.ml_dsa_65)
                    return "ML-DSA-65";   // ~192-bit security (NIST Level 3)

                if (parameters == MLDsaParameters.ml_dsa_87)
                    return "ML-DSA-87";   // ~256-bit security (NIST Level 5)

            }

            if (SigningKey is MLKemPublicKeyParameters mlKEMPrivateKey)
                throw new NotSupportedException("ML-KEM keys cannot sign X.509 certificates. Use a signature-capable issuer key (RSA/ECDSA/Ed25519/Ed448/ML-DSA)!");

            throw new ArgumentException("Unknown signing key type!");

        }

        #endregion


        #region (static) BuildCertificatePolicies (CertificatePolicies)

        public static Asn1Encodable BuildCertificatePolicies(IEnumerable<CertificatePolicy> CertificatePolicies)
        {

            var policyInfos = new List<PolicyInformation>();

            foreach (var p in CertificatePolicies)
            {

                var qualifiersVec = new Asn1EncodableVector();

                if (p.CpsUri is not null)
                {
                    // id-qt-cps = 1.3.6.1.5.5.7.2.1
                    qualifiersVec.Add(new PolicyQualifierInfo(p.CpsUri.ToString()));
                }

                if (!String.IsNullOrWhiteSpace(p.UserNoticeText))
                {

                    // id-qt-unotice = 1.3.6.1.5.5.7.2.2
                    var userNotice = new UserNotice(null, new DisplayText(p.UserNoticeText));

                    qualifiersVec.Add(
                        new PolicyQualifierInfo(
                            new DerObjectIdentifier("1.3.6.1.5.5.7.2.2"),
                            userNotice
                        )
                    );

                }

                var qualifiers = qualifiersVec.Count > 0
                    ? new DerSequence(qualifiersVec)
                    : null;

                var pi = qualifiers is null
                    ? new PolicyInformation(new DerObjectIdentifier(p.PolicyOID))
                    : new PolicyInformation(new DerObjectIdentifier(p.PolicyOID), qualifiers);

                policyInfos.Add(pi);

            }

            return new DerSequence(
                       policyInfos.ToArray()
                   );

        }

        #endregion

        #region (static) BuildDnsSubtrees         (DomainNames)

        public static IEnumerable<GeneralSubtree> BuildDnsSubtrees(IEnumerable<DomainName> DomainNames)

            => DomainNames.Select(domainName => new GeneralSubtree(
                                                    new GeneralName(
                                                        GeneralName.DnsName,
                                                        domainName.ToString()
                                                    )
                                                ));

        #endregion

        #region (static) BuildEmailSubtrees       (EmailsOrDomains)

        public static GeneralSubtree[] BuildEmailSubtrees(IEnumerable<String> EmailsOrDomains)

            // rfc822Name accepts either mailbox or domain;
            // for domain-only constraints, just pass the domain string.
            => [.. EmailsOrDomains.Select(e => new GeneralSubtree(
                                                   new GeneralName(
                                                       GeneralName.Rfc822Name,
                                                       e
                                                   )
                                               ))];

        #endregion

        #region (static) BuildIPSubtrees          (CIDRs)

        public static GeneralSubtree[] BuildIPSubtrees(IEnumerable<IPAddressCidr> CIDRs)

            => [.. CIDRs.Select(c => {

                   // RFC 5280: iPAddress in name constraints is an OCTET STRING: address followed by mask, both of equal length
                   var addrBytes = c.Address.GetAddressBytes();
                   var maskBytes = new byte[addrBytes.Length];

                   // Build mask from prefix length
                   int fullOctets = c.PrefixLength / 8;
                   int remBits    = c.PrefixLength % 8;

                   for (int i = 0; i < fullOctets; i++) maskBytes[i] = 0xFF;
                   if (remBits > 0) maskBytes[fullOctets] = (byte)(0xFF << (8 - remBits));

                   var outBytes = new byte[addrBytes.Length + maskBytes.Length];
                   Buffer.BlockCopy(addrBytes, 0, outBytes, 0, addrBytes.Length);
                   Buffer.BlockCopy(maskBytes, 0, outBytes, addrBytes.Length, maskBytes.Length);

                   return new GeneralSubtree(
                              new GeneralName(
                                  GeneralName.IPAddress,
                                  new DerOctetString(outBytes)
                              )
                          );

               })];

        #endregion

        #region (static) BuildNameConstraints     (Input)

        public static NameConstraints BuildNameConstraints(NameConstraintsInput Input)
        {

            var permitted = new List<GeneralSubtree>();
            var excluded  = new List<GeneralSubtree>();

            if (Input.PermittedDNS?.  Any() == true) permitted.AddRange(BuildDnsSubtrees  (Input.PermittedDNS));
            if (Input.ExcludedDNS?.   Any() == true) excluded .AddRange(BuildDnsSubtrees  (Input.ExcludedDNS));
            if (Input.PermittedEmail?.Any() == true) permitted.AddRange(BuildEmailSubtrees(Input.PermittedEmail));
            if (Input.ExcludedEmail?. Any() == true) excluded .AddRange(BuildEmailSubtrees(Input.ExcludedEmail));
            if (Input.PermittedIP?.   Any() == true) permitted.AddRange(BuildIPSubtrees   (Input.PermittedIP));
            if (Input.ExcludedIP?.    Any() == true) excluded .AddRange(BuildIPSubtrees   (Input.ExcludedIP));

            return new NameConstraints(
                       permitted as IList<GeneralSubtree>,
                       excluded  as IList<GeneralSubtree>
                   );

        }

        #endregion


        #region (static) GenerateRSAKeyPair     (NumberOfBits = 4096)

        /// <summary>
        /// Generate a new RSA key pair.
        /// </summary>
        /// <param name="NumberOfBits">The optional number of RSA bits to use.</param>
        public static AsymmetricCipherKeyPair GenerateRSAKeyPair(UInt16 NumberOfBits = 4096)
        {

            var keyPairGenerator = new RsaKeyPairGenerator();

            keyPairGenerator.Init(
                new KeyGenerationParameters(
                    new SecureRandom(),
                    NumberOfBits
                )
            );

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateECCKeyPair     (ECCName      = "secp256r1")

        /// <summary>
        /// Generate a new ECC key pair.
        /// </summary>
        /// <param name="ECCName">The optional ECC curve to use.</param>
        public static AsymmetricCipherKeyPair GenerateECCKeyPair(String ECCName = "secp256r1")
        {

            var keyPairGenerator = new ECKeyPairGenerator();

            keyPairGenerator.Init(
                new ECKeyGenerationParameters(
                    Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetOid(ECCName),
                    new SecureRandom()
                )
            );

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateEd25519KeyPair ()

        /// <summary>
        /// Generate a new Ed25519 key pair.
        /// </summary>
        public static AsymmetricCipherKeyPair GenerateEd25519KeyPair()
        {

            var keyPairGenerator = new Ed25519KeyPairGenerator();

            keyPairGenerator.Init(
                new KeyGenerationParameters(
                    new SecureRandom(),
                    255  // Strength: 255 bits for Ed25519
                )
            );

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateEd448KeyPair   ()

        /// <summary>
        /// Generate a new Ed448 key pair.
        /// </summary>
        public static AsymmetricCipherKeyPair GenerateEd448KeyPair()
        {

            var keyPairGenerator = new Ed448KeyPairGenerator();

            keyPairGenerator.Init(
                new KeyGenerationParameters(
                    new SecureRandom(),
                    448  // Strength: 448 bits for Ed448
                )
            );

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateFalconKeyPair  (FalconName   = "falcon_512")

        /// <summary>
        /// Generate a new Falcon key pair.
        /// </summary>
        /// <param name="Parameters">The Falcon parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateFalconKeyPair(FalconParameters Parameters)
        {

            var falconKeyPairGenerator = new FalconKeyPairGenerator();

            falconKeyPairGenerator.Init(
                new FalconKeyGenerationParameters(
                    new SecureRandom(),
                    Parameters
                )
            );

            return falconKeyPairGenerator.GenerateKeyPair();

        }


        /// <summary>
        /// Generate a new Falcon key pair.
        /// </summary>
        /// <param name="KEMName">The optional Falcon parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateFalconKeyPair(String FalconName = "falcon_512")
        {

            var falconKeyPairGenerator = new FalconKeyPairGenerator();

            falconKeyPairGenerator.Init(
                new FalconKeyGenerationParameters(
                    new SecureRandom(),
                    FalconName switch {
                        "falcon_512"   => FalconParameters.falcon_512,   // NIST Level 1 (~128-bit security)
                        "falcon_1024"  => FalconParameters.falcon_1024,  // NIST Level 5 (~256-bit security)
                        _              => throw new ArgumentException("Invalid Falcon parameters!")
                    }
                )
            );

            return falconKeyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateMLKEMKeyPair   (KEMName      = "ml_kem_768")

        /// <summary>
        /// Generate a new ML-KEM key pair.
        /// </summary>
        /// <param name="Parameters">The ML-KEM parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateMLKEMKeyPair(MLKemParameters Parameters)
        {

            var kemKeyPairGenerator = new MLKemKeyPairGenerator();

            kemKeyPairGenerator.Init(
                new MLKemKeyGenerationParameters(
                    new SecureRandom(),
                    Parameters
                )
            );

            return kemKeyPairGenerator.GenerateKeyPair();

        }


        /// <summary>
        /// Generate a new ML-KEM key pair.
        /// </summary>
        /// <param name="KEMName">The optional ML-KEM parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateMLKEMKeyPair(String KEMName = "ml_kem_768")
        {

            var kemKeyPairGenerator = new MLKemKeyPairGenerator();

            kemKeyPairGenerator.Init(
                new MLKemKeyGenerationParameters(
                    new SecureRandom(),
                    KEMName switch {
                        "ml_kem_512"   => MLKemParameters.ml_kem_512,   // AES-128-Security (NIST Level 1)
                        "ml_kem_768"   => MLKemParameters.ml_kem_768,   // AES-192-Security (NIST Level 3)
                        "ml_kem_1024"  => MLKemParameters.ml_kem_1024,  // AES-256-Security (NIST Level 5)
                        _              => throw new ArgumentException("Invalid ML-KEM parameters!")
                    }
                )
            );

            return kemKeyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateMLDSAKeyPair   (MLDSAName    = "ml_dsa_65")

        /// <summary>
        /// Generate a new ML-DSA key pair.
        /// </summary>
        /// <param name="Parameters">The ML-DSA parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateMLDSAKeyPair(MLDsaParameters Parameters)
        {

            var dsaKeyPairGenerator = new MLDsaKeyPairGenerator();

            dsaKeyPairGenerator.Init(
                new MLDsaKeyGenerationParameters(
                    new SecureRandom(),
                    Parameters
                )
            );

            return dsaKeyPairGenerator.GenerateKeyPair();

        }


        /// <summary>
        /// Generate a new ML-DSA key pair.
        /// </summary>
        /// <param name="MLDSAName">The optional ML-DSA parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateMLDSAKeyPair(String MLDSAName = "ml_dsa_65")
        {

            var dsaKeyPairGenerator = new MLDsaKeyPairGenerator();

            dsaKeyPairGenerator.Init(
                new MLDsaKeyGenerationParameters(
                    new SecureRandom(),
                    MLDSAName switch {
                        "ml_dsa_44"  => MLDsaParameters.ml_dsa_44,  // AES-128-Security (NIST Level 1)
                        "ml_dsa_65"  => MLDsaParameters.ml_dsa_65,  // AES-192-Security (NIST Level 3)
                        "ml_dsa_87"  => MLDsaParameters.ml_dsa_87,  // AES-256-Security (NIST Level 5)
                        _            => throw new ArgumentException("Invalid ML-DSA parameters!")
                    }
                )
            );

            return dsaKeyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region (static) GenerateSLHDSAKeyPair  (SLHDSAName   = "slh_dsa_sha2_128s")

        /// <summary>
        /// Generate a new SLH-DSA key pair.
        /// </summary>
        /// <param name="Parameters">The SLH-DSA parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateSLHDSAKeyPair(SlhDsaParameters Parameters)
        {

            var slhDSAKeyPairGenerator = new SlhDsaKeyPairGenerator();

            slhDSAKeyPairGenerator.Init(
                new SlhDsaKeyGenerationParameters(
                    new SecureRandom(),
                    Parameters
                )
            );

            return slhDSAKeyPairGenerator.GenerateKeyPair();

        }


        /// <summary>
        /// Generate a new SLH-DSA key pair.
        /// </summary>
        /// <param name="DSAName">The optional SLH-DSA parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateSLHDSAKeyPair(String SLHDSAName = "slh_dsa_sha2_128s")
        {

            var slhDSAKeyPairGenerator = new SlhDsaKeyPairGenerator();

            slhDSAKeyPairGenerator.Init(
                new SlhDsaKeyGenerationParameters(
                    new SecureRandom(),
                    SLHDSAName switch {

                        // --- SHA2-based parameter sets ---
                        "slh_dsa_sha2_128s"   => SlhDsaParameters.slh_dsa_sha2_128s,   // ~128-bit security
                        "slh_dsa_sha2_128f"   => SlhDsaParameters.slh_dsa_sha2_128f,   // ~128-bit, full (smaller sig)
                        "slh_dsa_sha2_192s"   => SlhDsaParameters.slh_dsa_sha2_192s,   // ~192-bit
                        "slh_dsa_sha2_192f"   => SlhDsaParameters.slh_dsa_sha2_192f,
                        "slh_dsa_sha2_256s"   => SlhDsaParameters.slh_dsa_sha2_256s,   // ~256-bit
                        "slh_dsa_sha2_256f"   => SlhDsaParameters.slh_dsa_sha2_256f,

                        // --- SHAKE-based parameter sets ---
                        "slh_dsa_shake_128s"  => SlhDsaParameters.slh_dsa_shake_128s,
                        "slh_dsa_shake_128f"  => SlhDsaParameters.slh_dsa_shake_128f,
                        "slh_dsa_shake_192s"  => SlhDsaParameters.slh_dsa_shake_192s,
                        "slh_dsa_shake_192f"  => SlhDsaParameters.slh_dsa_shake_192f,
                        "slh_dsa_shake_256s"  => SlhDsaParameters.slh_dsa_shake_256s,
                        "slh_dsa_shake_256f"  => SlhDsaParameters.slh_dsa_shake_256f,

                        _                     => throw new ArgumentException("Invalid SLH-DSA parameters!")

                    }
                )
            );

            return slhDSAKeyPairGenerator.GenerateKeyPair();

        }

        #endregion

        // ToDo: HybridKeyPair, PqcHybridKeyEncapsulation


        //ToDo: Allow hybrid certs!
        // var generator = new CmsSignedDataGenerator();
        // generator.AddSigner(eccKeyPair1.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // 1st ECC signature
        // generator.AddSigner(eccKeyPair2.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // 2nd ECC signature
        // var signedData = generator.Generate(certBytes, true);                                    // certBytes = serialized X.509 certificate
        // signedData.Verify();



        #region (static) CreateRootCACertificate   (SubjectName,                  RootKeyPair, ...)

        /// <summary>
        /// Generate a self-signed root certificate for the root certification authority.
        /// </summary>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="RootKeyPair">A crypto key pair.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateRootCACertificate(String                           SubjectName,
                                    AsymmetricCipherKeyPair          RootKeyPair,
                                    TimeSpan?                        LifeTime                 = null,
                                    IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                    IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                    IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                    IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.RootCA,
                       SubjectName,
                       RootKeyPair.Public,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           RootKeyPair.Private,  // self-signed!
                           null
                       ),
                       null,
                       LifeTime,
                       null,
                       null,
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       null,
                       null,
                       CertificatePolicies
                   );

        #endregion

        #region (static) CreateIntermediateCA      (SubjectName,                  IntermediateKeyPair, IssuerPrivateKey, IssuerCertificate, ...)

        /// <summary>
        /// Generate an intermediate server certification authority signed by the root CA.
        /// </summary>
        /// <param name="IntermediateKeyPair">A crypto key pair. Does not need to be of the same type as the root CA key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="IssuerPrivateKey">The private key for signing the new certificate.</param>
        /// <param name="IssuerCertificate">The certificate for signing the new certificate.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateIntermediateCA(String                           SubjectName,
                                 AsymmetricKeyParameter           IntermediatePublicKey,
                                 AsymmetricKeyParameter           IssuerPrivateKey,
                                 BCx509.X509Certificate           IssuerCertificate,
                                 TimeSpan?                        LifeTime                 = null,
                                 Byte?                            PathLenConstraint        = null,
                                 NameConstraintsInput?            NameConstraints          = null,
                                 IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                 IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                 IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                 Boolean?                         TLSMustStaple            = false,  // true -> id-pe-tlsfeature: status_request(5)
                                 Boolean?                         TLSMustStapleV2          = false,  // when true and TLSMustStaple==true -> {5,17})
                                 IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.IntermediateCA,
                       SubjectName,
                       IntermediatePublicKey,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           IssuerPrivateKey,
                           IssuerCertificate
                       ),
                       null, // SubjectAltNames
                       LifeTime,
                       PathLenConstraint,
                       NameConstraints,
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       TLSMustStaple,
                       TLSMustStapleV2,
                       CertificatePolicies
                   );

        #endregion


        #region (static) SignServerCertificate     (ServerName,  SubjectAltNames, ServerPublicKey,     IssuerPrivateKey, IssuerCertificate = null, ...)

        /// <summary>
        /// Sign a new X.509 server certificate.
        /// </summary>
        /// <param name="ServerName">A friendly name for the server certificate.</param>
        /// <param name="SubjectAltNames">The subject alternative names for the server certificate (e.g. DNS/IP).</param>
        /// <param name="ServerPublicKey">The public key of the server.</param>
        /// <param name="IssuerPrivateKey">The private key for signing the new server certificate.</param>
        /// <param name="IssuerCertificate">The certificate for signing the new server certificate.</param>
        /// <param name="LifeTime">The optional life time of the server certificate.</param>
        public static BCx509.X509Certificate

            SignServerCertificate(String                           ServerName,
                                  IEnumerable<GeneralName>         SubjectAltNames,                   // e.g. DNS/IP for server certs
                                  AsymmetricKeyParameter           ServerPublicKey,
                                  AsymmetricKeyParameter           IssuerPrivateKey,
                                  BCx509.X509Certificate?          IssuerCertificate        = null,
                                  TimeSpan?                        LifeTime                 = null,
                                  IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                  IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                  IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                  Boolean?                         TLSMustStaple            = false,  // true -> id-pe-tlsfeature: status_request(5)
                                  Boolean?                         TLSMustStapleV2          = false,  // when true and TLSMustStaple==true -> {5,17})
                                  IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.Server,
                       ServerName,
                       ServerPublicKey,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           IssuerPrivateKey,
                           IssuerCertificate
                       ),
                       SubjectAltNames,
                       LifeTime,
                       null, // PathLenConstraint
                       null, // NameConstraints
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       TLSMustStaple,
                       TLSMustStapleV2,
                       CertificatePolicies
                   );

        #endregion

        #region (static) SelfSignServerCertificate (ServerName,  SubjectAltNames, ServerKeyPair, ...)

        /// <summary>
        /// Sign a new X.509 server certificate.
        /// </summary>
        /// <param name="ServerName">A friendly name for the server certificate.</param>
        /// <param name="SubjectAltNames">The subject alternative names for the server certificate (e.g. DNS/IP).</param>
        /// <param name="ServerKeyPair">The crypto keys of the server.</param>
        /// <param name="LifeTime">The optional life time of the server certificate.</param>
        public static BCx509.X509Certificate

            SelfSignServerCertificate(String                           ServerName,
                                      IEnumerable<GeneralName>         SubjectAltNames,                   // e.g. DNS/IP for server certs
                                      AsymmetricCipherKeyPair          ServerKeyPair,
                                      TimeSpan?                        LifeTime                 = null,
                                      IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                      IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                      IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                      Boolean?                         TLSMustStaple            = false,  // true -> id-pe-tlsfeature: status_request(5)
                                      Boolean?                         TLSMustStapleV2          = false,  // when true and TLSMustStaple==true -> {5,17})
                                      IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.Server,
                       ServerName,
                       ServerKeyPair.Public,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           ServerKeyPair.Private,
                           null
                       ),
                       SubjectAltNames,
                       LifeTime,
                       null, // PathLenConstraint
                       null, // NameConstraints
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       TLSMustStaple,
                       TLSMustStapleV2,
                       CertificatePolicies
                   );

        #endregion

        #region (static) SignServerCertificate     (CertificateSigningRequest,                         IssuerPrivateKey, IssuerCertificate, ...)
        public static BCx509.X509Certificate SignServerCertificate(Pkcs10CertificationRequest      CertificateSigningRequest,
                                                                   AsymmetricKeyParameter          issuerPrivateKey,
                                                                   BCx509.X509Certificate          issuerCertificate,
                                                                   TimeSpan?                       lifeTime                 = null,
                                                                   IEnumerable<URL>?               crlDistributionPoints    = null,
                                                                   IEnumerable<URL>?               aiaOcspUrls              = null,
                                                                   IEnumerable<URL>?               aiaCaIssuersUrls         = null,
                                                                   Boolean?                        tlsMustStaple            = false,  // TLS Feature: 5
                                                                   Boolean?                        tlsMustStapleV2          = false,  // zusätzlich 17
                                                                   IEnumerable<CertificatePolicy>? certificatePolicies      = null)
        {

            if (!CertificateSigningRequest.Verify())
                throw new InvalidOperationException("CSR signature verification failed!");

            var certificationRequestInfo  = CertificateSigningRequest.GetCertificationRequestInfo();
            var subjectName               = certificationRequestInfo.Subject;
            var subjectCN                 = GetCommonName(subjectName);
            var subjectPublicKey          = CertificateSigningRequest.GetPublicKey();

            if (subjectPublicKey is MLKemPublicKeyParameters)
                throw new NotSupportedException("ML-KEM public keys cannot be used for TLS server certificates!");

            GeneralNames? subjectAltNames = null;

            var attrSet = certificationRequestInfo.Attributes;
            if (attrSet is not null)
            {
                for (var i = 0; i < attrSet.Count; i++)
                {

                    var attr = AttributePkcs.GetInstance(attrSet[i]);

                    if (attr.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
                    {
                        var exts   = X509Extensions.GetInstance(attr.AttrValues[0]);
                        var sanExt = exts.GetExtension(X509Extensions.SubjectAlternativeName);
                        if (sanExt is not null)
                        {
                            subjectAltNames = GeneralNames.GetInstance(
                                                  X509ExtensionUtilities.FromExtensionValue(sanExt.Value)
                                              );
                        }
                        break;
                    }

                }
            }

            return SignCertificate(
                       CertificateTypes.Server,
                       subjectCN,
                       subjectPublicKey,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           issuerPrivateKey,
                           issuerCertificate
                       ),
                       subjectAltNames?.GetNames(),  // when null: CN→SAN
                       lifeTime,
                       null,                         // PathLenConstraint
                       null,                         // NameConstraints
                       crlDistributionPoints,
                       aiaOcspUrls,
                       aiaCaIssuersUrls,
                       tlsMustStaple,
                       tlsMustStapleV2,
                       certificatePolicies
                   );

        }

        #endregion


        #region (static) SignClientCertificate     (ClientName,                   ClientKeyPair,       IssuerPrivateKey, IssuerCertificate = null, ...)

        /// <summary>
        /// Sign a new X.509 client certificate.
        /// </summary>
        /// <param name="ClientName">A friendly name for the client certificate.</param>
        /// <param name="ClientPublicKey">The public key of the client.</param>
        /// <param name="IssuerPrivateKey">The private key for signing the new client certificate.</param>
        /// <param name="IssuerCertificate">The certificate for signing the new client certificate.</param>
        /// <param name="LifeTime">The optional life time of the client certificate.</param>
        public static BCx509.X509Certificate

            SignClientCertificate(String                           ClientName,
                                  AsymmetricKeyParameter           ClientPublicKey,
                                  AsymmetricKeyParameter           IssuerPrivateKey,
                                  BCx509.X509Certificate?          IssuerCertificate        = null,
                                  TimeSpan?                        LifeTime                 = null,
                                  IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                  IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                  IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                  IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.Client,
                       ClientName,
                       ClientPublicKey,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           IssuerPrivateKey,
                           IssuerCertificate
                       ),
                       null, // ClientAltNames
                       LifeTime,
                       null, // PathLenConstraint
                       null, // NameConstraints
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       null, // TLSMustStaple
                       null, // TLSMustStapleV2
                       CertificatePolicies
                   );

        #endregion

        #region (static) SelfSignClientCertificate (ClientName,                   ClientKeyPair, ...)

        /// <summary>
        /// Sign a new X.509 client certificate.
        /// </summary>
        /// <param name="ClientName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="ClientKeyPair">The crypto keys of the client.</param>
        /// <param name="LifeTime">The optional life time of the client certificate.</param>
        public static BCx509.X509Certificate

            SelfSignClientCertificate(String                           ClientName,
                                      AsymmetricCipherKeyPair          ClientKeyPair,
                                      TimeSpan?                        LifeTime                 = null,
                                      IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                      IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                      IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                      IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.Client,
                       ClientName,
                       ClientKeyPair.Public,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           ClientKeyPair.Private,
                           null
                       ),
                       null, // ClientAltNames
                       LifeTime,
                       null, // PathLenConstraint
                       null, // NameConstraints
                       CRL_DistributionPoints,
                       AIA_OCSPURLs,
                       AIA_CAIssuersURLs,
                       null, // TLSMustStaple
                       null, // TLSMustStapleV2
                       CertificatePolicies
                   );

        #endregion

        #region (static) SignClientCertificate     (CertificateSigningRequest,                         IssuerPrivateKey, IssuerCertificate, ...)
        public static BCx509.X509Certificate SignClientCertificate(Pkcs10CertificationRequest      CertificateSigningRequest,
                                                                   AsymmetricKeyParameter          issuerPrivateKey,
                                                                   BCx509.X509Certificate          issuerCertificate,
                                                                   TimeSpan?                       lifeTime                 = null,
                                                                   IEnumerable<URL>?               crlDistributionPoints    = null,
                                                                   IEnumerable<URL>?               aiaOcspUrls              = null,
                                                                   IEnumerable<URL>?               aiaCaIssuersUrls         = null,
                                                                   IEnumerable<CertificatePolicy>? certificatePolicies      = null)
        {

            if (!CertificateSigningRequest.Verify())
                throw new InvalidOperationException("CSR signature verification failed!");

            var certificationRequestInfo  = CertificateSigningRequest.GetCertificationRequestInfo();
            var subjectName               = certificationRequestInfo.Subject;
            var subjectCN                 = GetCommonName(subjectName);
            var subjectPublicKey          = CertificateSigningRequest.GetPublicKey();

            if (subjectPublicKey is MLKemPublicKeyParameters)
                throw new NotSupportedException("ML-KEM public keys cannot be used for TLS server certificates!");

            return SignCertificate(
                       CertificateTypes.Client,
                       subjectCN,
                       subjectPublicKey,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>(
                           issuerPrivateKey,
                           issuerCertificate
                       ),
                       null,  // SubjectAltNames
                       lifeTime,
                       null,  // PathLenConstraint
                       null,  // NameConstraints
                       crlDistributionPoints,
                       aiaOcspUrls,
                       aiaCaIssuersUrls,
                       null,  // TLSMustStaple,
                       null,  // TLSMustStapleV2,
                       certificatePolicies
                   );

        }

        #endregion


        #region (static) SignCertificate           (CertificateType, SubjectName, SubjectPublicKey, Issuer, LifeTime = null)

        /// <summary>
        /// Sign a new X.509 certificate.
        /// </summary>
        /// <param name="CertificateType">The type of the certificate.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectPublicKey">The public key of the subject.</param>
        /// <param name="Issuer">The (optional) crypto key pair signing this certificate. Optional means that this certificate will be a self-signed rootCA!</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            SignCertificate(CertificateTypes                                         CertificateType,
                            String                                                   SubjectName,
                            AsymmetricKeyParameter                                   SubjectPublicKey,
                            Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?>?  Issuer,
                            IEnumerable<GeneralName>?                                SubjectAltNames          = null,    // e.g. DNS/IP for server certs
                            TimeSpan?                                                LifeTime                 = null,
                            Byte?                                                    PathLenConstraint        = null,
                            NameConstraintsInput?                                    NameConstraints          = null,
                            IEnumerable<URL>?                                        CRL_DistributionPoints   = null,
                            IEnumerable<URL>?                                        AIA_OCSPURLs             = null,
                            IEnumerable<URL>?                                        AIA_CAIssuersURLs        = null,
                            Boolean?                                                 TLSMustStaple            = false,  // true -> id-pe-tlsfeature: status_request(5)
                            Boolean?                                                 TLSMustStapleV2          = false,  // when true and TLSMustStaple==true -> {5,17})
                            IEnumerable<CertificatePolicy>?                          CertificatePolicies      = null)

        {

            var certGen          = new X509V3CertificateGenerator();
            var now              = Timestamp.Now;

            var defaultLifeTime  = CertificateType switch {
                                       CertificateTypes.RootCA          => TimeSpan.FromDays(3650), // ~10y
                                       CertificateTypes.IntermediateCA  => TimeSpan.FromDays(1825), // ~5y
                                       CertificateTypes.Server          => TimeSpan.FromDays(30),
                                       CertificateTypes.Client          => TimeSpan.FromDays(30),
                                       _                                => TimeSpan.FromDays(7)
                                   };

            var notAfter         = now.Add(LifeTime ?? defaultLifeTime);
            if (notAfter <= now)
                throw new ArgumentException("The notAfter date must be after the notBefore date!");

            certGen.SetSerialNumber (RandomSerial());
            certGen.SetSubjectDN    (new X509Name($"CN={SubjectName}"));//, O=GraphDefined GmbH, OU=GraphDefined PKI Services"));
            certGen.SetPublicKey    (SubjectPublicKey);

            // Tolerate small clock skew!
            certGen.SetNotBefore    (now.     DateTime.AddMinutes(-5).ToUniversalTime());
            certGen.SetNotAfter     (notAfter.DateTime.               ToUniversalTime());

            certGen.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,  // non-critical
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(
                    SubjectPublicKey
                )
            );



            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/root-configuration-file.html
            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/intermediate-configuration-file.html


            if (CertificateType == CertificateTypes.RootCA)
            {

                // Self-signed root CA!
                certGen.SetIssuerDN(new X509Name($"CN={SubjectName}"));


                // Key Usage
                certGen.AddExtension(
                    X509Extensions.KeyUsage,
                    true,  // critical
                    new KeyUsage(
                        //KeyUsage.DigitalSignature |
                        KeyUsage.KeyCertSign |
                        KeyUsage.CrlSign
                    )
                );


                // Basic Constraints
                certGen.AddExtension(
                    X509Extensions.BasicConstraints,
                    true,  // critical
                    PathLenConstraint is Byte caPathLengthConstraint
                        ? new BasicConstraints(caPathLengthConstraint)
                        : new BasicConstraints(true) // isCA = true
                );


                // Authority Key Identifier
                certGen.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    false,  // non-critical
                    X509ExtensionUtilities.CreateAuthorityKeyIdentifier(
                        SubjectPublicKey
                    )
                );

            }

            else
            {

                if (Issuer is null)
                    throw new ArgumentException("Issuer must be provided for non-root CA certificates!");

                var issuerKey          = Issuer.Item1;
                var issuerCertificate  = Issuer.Item2;

                if (issuerCertificate is not null)
                {

                    // Guard: issuer must be a CA and not expired
                    var issuerIsCA  = BasicConstraints.GetInstance(
                                          X509ExtensionUtilities.FromExtensionValue(
                                              issuerCertificate.GetExtensionValue(X509Extensions.BasicConstraints)
                                          )
                                      )?.IsCA() ?? false;

                    if (!issuerIsCA)
                        throw new InvalidOperationException("Issuer certificate is not a CA (CA:FALSE).");


                    // Reduce certificate.NotAfter to issuer.NotAfter
                    if (notAfter > issuerCertificate.NotAfter.ToUniversalTime())
                        certGen.SetNotAfter(issuerCertificate.NotAfter.ToUniversalTime());

                    certGen.SetIssuerDN (issuerCertificate.SubjectDN);

                    certGen.AddExtension(
                        X509Extensions.AuthorityKeyIdentifier,
                        false,  // non-critical
                        X509ExtensionUtilities.CreateAuthorityKeyIdentifier(issuerCertificate)
                    );

                }

                else if (CertificateType == CertificateTypes.Server ||
                         CertificateType == CertificateTypes.Client)
                {

                    // Self-signed server/client certificate!
                    certGen.SetIssuerDN(new X509Name($"CN={SubjectName}"));

                    // Authority Key Identifier
                    certGen.AddExtension(
                        X509Extensions.AuthorityKeyIdentifier,
                        false,  // non-critical
                        X509ExtensionUtilities.CreateAuthorityKeyIdentifier(
                            issuerCertificate
                        )
                    );

                }

                else
                    throw new ArgumentException("An issuer certificate must be provided!");


                var keyUsageBits = KeyUsage.DigitalSignature;

                if (SubjectPublicKey is RsaKeyParameters)
                    keyUsageBits |= KeyUsage.KeyEncipherment;

                switch (CertificateType)
                {

                    case CertificateTypes.IntermediateCA:

                        // Key Usage
                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            new KeyUsage(
                                //KeyUsage.DigitalSignature |
                                KeyUsage.KeyCertSign      |
                                KeyUsage.CrlSign
                            )
                        );


                        // Basic Constraints
                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            PathLenConstraint is Byte caPathLengthConstraint
                                ? new BasicConstraints(caPathLengthConstraint)
                                : new BasicConstraints(true) // isCA = true
                        );


                        // Name Constraints
                        if (NameConstraints is not null)
                            certGen.AddExtension(
                                X509Extensions.NameConstraints,
                                true,  // critical is recommended
                                BuildNameConstraints(NameConstraints)
                            );

                    break;

                    case CertificateTypes.Server:

                        // Basic Constraints
                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            new BasicConstraints(false) // isCA = false
                        );


                        // Key Usage
                        keyUsageBits = 0;

                        if (SubjectPublicKey is RsaKeyParameters           ||
                            SubjectPublicKey is ECPublicKeyParameters      ||
                            SubjectPublicKey is Ed25519PublicKeyParameters ||
                            SubjectPublicKey is Ed448PublicKeyParameters   ||
                            SubjectPublicKey is FalconPublicKeyParameters  ||
                            SubjectPublicKey is SlhDsaPublicKeyParameters  ||
                            SubjectPublicKey is MLDsaPublicKeyParameters)
                        {
                            keyUsageBits |= KeyUsage.DigitalSignature;
                        }

                        if (SubjectPublicKey is RsaKeyParameters           ||
                            SubjectPublicKey is MLKemPublicKeyParameters)
                        {
                            keyUsageBits |= KeyUsage.KeyEncipherment;
                        }

                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            new KeyUsage(keyUsageBits)
                        );


                        // Extended Key Usage
                        certGen.AddExtension(
                            X509Extensions.ExtendedKeyUsage,
                            false,  // non-critical
                            new ExtendedKeyUsage(
                                KeyPurposeID.id_kp_serverAuth
                            )
                        );


                        // In case: Mirror CN as DNS!
                        SubjectAltNames ??= [
                                                new GeneralName(
                                                    GeneralName.DnsName,
                                                    SubjectName
                                                )
                                            ];

                        // SAN is mandatory for TLS name checks
                        certGen.AddExtension(
                            X509Extensions.SubjectAlternativeName,
                            false,  // non-critical
                            new GeneralNames([.. SubjectAltNames])
                        );


                        // TLS Must Staple
                        if (TLSMustStaple == true)
                        {

                            var features = new Asn1EncodableVector();

                            if (TLSMustStaple   == true) features.Add(new DerInteger(5));   // status_request_v1
                            if (TLSMustStapleV2 == true) features.Add(new DerInteger(17));  // status_request_v2

                            certGen.AddExtension(
                                new DerObjectIdentifier("1.3.6.1.5.5.7.1.24"),
                                false,            // non-critical is common; some CAs mark critical – keep non-critical unless you fully control clients
                                new DerOctetString(
                                    new DerSequence(features).GetEncoded()
                                )
                            );

                        }

                    break;

                    case CertificateTypes.Client:

                        // Basic Constraints
                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            new BasicConstraints(false) // isCA = false
                        );


                        // Key Usage
                        keyUsageBits = 0;

                        if (SubjectPublicKey is RsaKeyParameters           ||
                            SubjectPublicKey is ECPublicKeyParameters      ||
                            SubjectPublicKey is Ed25519PublicKeyParameters ||
                            SubjectPublicKey is Ed448PublicKeyParameters   ||
                            SubjectPublicKey is FalconPublicKeyParameters  ||
                            SubjectPublicKey is SlhDsaPublicKeyParameters  ||
                            SubjectPublicKey is MLDsaPublicKeyParameters)
                        {
                            keyUsageBits |= KeyUsage.DigitalSignature;
                        }

                        if (SubjectPublicKey is RsaKeyParameters           ||
                            SubjectPublicKey is MLKemPublicKeyParameters)
                        {
                            keyUsageBits |= KeyUsage.KeyEncipherment;
                        }

                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            new KeyUsage(keyUsageBits)
                        );


                        // Extended Key Usage
                        certGen.AddExtension(
                            X509Extensions.ExtendedKeyUsage,
                            false,  // non-critical
                            new ExtendedKeyUsage(
                                KeyPurposeID.id_kp_clientAuth
                            )
                        );

                    break;

                }

                if (keyUsageBits == 0)
                    throw new InvalidOperationException($"Unsupported key type for KeyUsage: {SubjectPublicKey.GetType().Name}!");

                if (CertificatePolicies?.Any() == true)
                    certGen.AddExtension(
                        X509Extensions.CertificatePolicies,
                        false,  // non-critical is typical
                        BuildCertificatePolicies(CertificatePolicies)
                    );

            }


            #region CRL Distribution Points

            if (CRL_DistributionPoints?.Any() == true)
            {

                // e.g. http://pki.example.com/crl/root.crl
                certGen.AddExtension(
                    X509Extensions.CrlDistributionPoints,
                    false,  // non-critical
                    new CrlDistPoint(
                        [.. CRL_DistributionPoints.Select(url =>
                                new DistributionPoint(
                                    new DistributionPointName(
                                        new GeneralNames(
                                            new GeneralName(
                                                GeneralName.UniformResourceIdentifier,
                                                url.ToString()
                                            )
                                        )
                                    ),
                                    null,
                                    null
                                )
                        )]
                    )
                );

            }

            #endregion

            #region Authority Info Access (optional)

            // optional, but for non-roots strongly recommended!

            if ((AIA_OCSPURLs?.     Any() == true) ||
                (AIA_CAIssuersURLs?.Any() == true))
            {

                var authorityInfos = new Asn1EncodableVector();

                // OCSP, e.g. http://ocsp.example.com
                foreach (var url in AIA_OCSPURLs ?? [])
                    authorityInfos.Add(
                        new AccessDescription(
                            AccessDescription.IdADOcsp,
                            new GeneralName(
                                GeneralName.UniformResourceIdentifier,
                                url.ToString()
                            )
                        )
                    );

                // CA Issuers, e.g. http://pki.example.com/ca.cer
                foreach (var url in AIA_CAIssuersURLs ?? [])
                    authorityInfos.Add(
                        new AccessDescription(
                            AccessDescription.IdADCAIssuers,
                            new GeneralName(
                                GeneralName.UniformResourceIdentifier,
                                url.ToString()
                            )
                        )
                    );

                certGen.AddExtension(
                    X509Extensions.AuthorityInfoAccess,
                    false,
                    new DerSequence(
                        authorityInfos
                    )
                );

            }

            #endregion


            return certGen.Generate(
                new Asn1SignatureFactory(
                    SelectSignatureAlgorithm(SubjectPublicKey),
                    Issuer?.Item1,
                    new SecureRandom()
                )
            );

        }

        #endregion



        #region (static) ToDotNet(this Certificate, PrivateKey = null, CACertificates = null)

        /// <summary>
        /// Convert the Bouncy Castle certificate to a .NET certificate.
        /// </summary>
        /// <param name="Certificate">A Bouncy Castle certificate.</param>
        /// <param name="PrivateKey">An optional private key to be included.</param>
        /// <param name="CACertificates">Optional CA certificates.</param>
        public static X509Certificate2? ToDotNet(this BCx509.X509Certificate           Certificate,
                                                 AsymmetricKeyParameter?               PrivateKey       = null,
                                                 IEnumerable<BCx509.X509Certificate>?  CACertificates   = null)
        {

            if (PrivateKey is null)
                return X509CertificateLoader.LoadCertificate(Certificate.GetEncoded());

            if (PrivateKey is RsaPrivateCrtKeyParameters)
            {

                var store             = new Pkcs12StoreBuilder().Build();
                var certificateEntry  = new X509CertificateEntry(Certificate);

                store.SetCertificateEntry(Certificate.SubjectDN.ToString(),
                                          certificateEntry);

                store.SetKeyEntry        (Certificate.SubjectDN.ToString(),
                                          new AsymmetricKeyEntry(PrivateKey),
                                          [ certificateEntry ]);

                foreach (var caCertificate in (CACertificates ?? []))
                {
                    store.SetCertificateEntry(caCertificate.SubjectDN.ToString(),
                                              new X509CertificateEntry(caCertificate));
                }

                using (var pfxStream = new MemoryStream())
                {

                    var password = RandomExtensions.RandomString(10);

                    store.Save(pfxStream,
                               password.ToCharArray(),
                               new SecureRandom());

                    return new X509Certificate2(
                               pfxStream.ToArray(),
                               password,
                               X509KeyStorageFlags.Exportable
                           );

                }

            }

            if (PrivateKey is ECPrivateKeyParameters)
            {

                var store             = new Pkcs12StoreBuilder().Build();
                var certificateEntry  = new X509CertificateEntry(Certificate);

                store.SetCertificateEntry(Certificate.SubjectDN.ToString(),
                                          certificateEntry);

                store.SetKeyEntry(Certificate.SubjectDN.ToString(),
                                  new AsymmetricKeyEntry(PrivateKey),
                                  [certificateEntry]);

                foreach (var caCertificate in CACertificates ?? [])
                {
                    store.SetCertificateEntry(caCertificate.SubjectDN.ToString(),
                                              new X509CertificateEntry(caCertificate));
                }

                using (var pfxStream = new MemoryStream())
                {
                    var password = RandomExtensions.RandomString(10);
                    store.Save(pfxStream,
                               password.ToCharArray(),
                               new SecureRandom());
                    return X509CertificateLoader.LoadPkcs12(
                               pfxStream.ToArray(),
                               password,
                               X509KeyStorageFlags.Exportable
                           );
                }

            }

            return null;

        }

        #endregion

        #region (static) ToDotNet2(this Certificate, PrivateKey = null, CACertificates = null)

        /// <summary>
        /// Convert the Bouncy Castle certificate to a .NET certificate.
        /// </summary>
        /// <param name="Certificate">A Bouncy Castle certificate.</param>
        /// <param name="PrivateKey">An optional private key to be included.</param>
        /// <param name="CACertificates">Optional CA certificates.</param>
        public static X509Certificate2? ToDotNet2(this BCx509.X509Certificate           Certificate,
                                                  AsymmetricKeyParameter?               PrivateKey       = null,
                                                  IEnumerable<BCx509.X509Certificate>?  CACertificates   = null)
        {

            if (PrivateKey is null)
                return X509CertificateLoader.LoadCertificate(Certificate.GetEncoded());

            X509Certificate2 LoadAsPkcs12(AsymmetricKeyParameter pk) {

                var store      = new Pkcs12StoreBuilder().Build();

                var leafEntry  = new X509CertificateEntry(Certificate);
                var alias      = Certificate.SubjectDN.ToString();

                store.SetCertificateEntry(alias, leafEntry);
                store.SetKeyEntry        (alias, new AsymmetricKeyEntry(pk), [ leafEntry ]);

                foreach (var caCertificate in CACertificates ?? [])
                {
                    var caAlias = caCertificate.SubjectDN.ToString();
                    store.SetCertificateEntry(caAlias, new X509CertificateEntry(caCertificate));
                }

                using var pfxStream  = new MemoryStream();
                var password         = RandomExtensions.RandomString(12);

                store.Save(
                    pfxStream,
                    password.ToCharArray(),
                    new SecureRandom()
                );

                return X509CertificateLoader.LoadPkcs12(
                           pfxStream.ToArray(),
                           password,
                           X509KeyStorageFlags.Exportable
                       );

            }

            if (PrivateKey is RsaPrivateCrtKeyParameters rsaCrt)
                return LoadAsPkcs12(rsaCrt);

            if (PrivateKey is RsaKeyParameters { IsPrivate: true } rsaPk)
                return LoadAsPkcs12(rsaPk);

            if (PrivateKey is ECPrivateKeyParameters ecPk)
                return LoadAsPkcs12(ecPk);

            throw new NotSupportedException($"Private key type '{PrivateKey.GetType().Name}' cannot be attached to X509Certificate2!");

        }

        #endregion



        #region (static) ValidateChain       (Certificate, IntermediateCertificate,      RootCertificate, ApplicationPolicy = null, VerificationTime = null)

        public static ChainReport ValidateChain(X509Certificate2  Certificate,
                                                X509Certificate2  IntermediateCertificate,
                                                X509Certificate2  RootCertificate,
                                                OidCollection?    ApplicationPolicy   = null,
                                                DateTimeOffset?   VerificationTime    = null)

            => ValidateChain(
                   Certificate,
                   [ IntermediateCertificate ],
                   RootCertificate,
                   ApplicationPolicy,
                   VerificationTime
               );

        #endregion

        #region (static) ValidateChain       (Certificate, IntermediateCertificate,      RootCertificate, ApplicationPolicy = null, VerificationTime = null)

        public static ChainReport ValidateChain(X509Certificate2               Certificate,
                                                IEnumerable<X509Certificate2>  IntermediateCertificates,
                                                X509Certificate2               RootCertificate,
                                                OidCollection?                 ApplicationPolicy   = null,
                                                DateTimeOffset?                VerificationTime    = null)
        {

            using var chain    = new X509Chain();
            chain.ChainPolicy  = new X509ChainPolicy {
                                     TrustMode                    = X509ChainTrustMode.   CustomRootTrust,
                                     VerificationFlags            = X509VerificationFlags.NoFlag,
                                     RevocationMode               = X509RevocationMode.   NoCheck,
                                     RevocationFlag               = X509RevocationFlag.   ExcludeRoot,
                                     DisableCertificateDownloads  = true,
                                     VerificationTimeIgnored      = false,
                                     VerificationTime             = (VerificationTime ?? Timestamp.Now).DateTime,
                                     //CertificatePolicy            = new System.Security.Cryptography.OidCollection(),
                                     UrlRetrievalTimeout          = TimeSpan.FromSeconds(10)
                                 };

            chain.ChainPolicy.CustomTrustStore.Add(RootCertificate);

            foreach (var IntermediateCertificate in IntermediateCertificates)
                chain.ChainPolicy.ExtraStore.Add(IntermediateCertificate);

            if (ApplicationPolicy is not null)
                foreach (var applicationPolicyOID in ApplicationPolicy)
                    chain.ChainPolicy.ApplicationPolicy.Add(applicationPolicyOID);

            var isValid        = chain.Build(Certificate);

            var topStatus      = chain.ChainStatus is { Length: > 0 }
                                     ? chain.ChainStatus.
                                           Select(chainStatus => new X509ChainStatus {
                                                                     Status            = chainStatus.Status,
                                                                     StatusInformation = chainStatus.StatusInformation
                                                                 }).
                                           ToArray()
                                     : [];

            var elements       = chain.ChainElements.Cast<X509ChainElement>().
                                       Select(chainElement => ( X509CertificateLoader.LoadCertificate(chainElement.Certificate.RawData),
                                                                chainElement.ChainElementStatus.Select(s => new X509ChainStatus {
                                                                                                                Status            = s.Status,
                                                                                                                StatusInformation = s.StatusInformation
                                                                                                            }).ToArray())).
                                       ToArray();

            return new ChainReport(
                       isValid,
                       topStatus,
                       elements
                   );

        }

        #endregion

        #region (static) ValidateChain       (Certificate, IntermediateCertificateChain, RootCertificate, ApplicationPolicy = null, VerificationTime = null)

        public static ChainReport ValidateChain(X509Certificate2  Certificate,
                                                X509Chain         IntermediateCertificateChain,
                                                X509Certificate2  RootCertificate,
                                                OidCollection?    ApplicationPolicy   = null,
                                                DateTimeOffset?   VerificationTime    = null)

            => ValidateChain(
                   Certificate,
                   IntermediateCertificateChain.ChainElements.
                       Select(chainElement => chainElement.Certificate).
                       Where (certificate  => certificate != Certificate),
                   RootCertificate,
                   ApplicationPolicy,
                   VerificationTime
               );

        #endregion


        #region (static) ValidateServerChain (Certificate, IntermediateCertificate,      RootCertificate, VerificationTime = null)

        public static ChainReport ValidateServerChain(X509Certificate2  Certificate,
                                                      X509Certificate2  IntermediateCertificate,
                                                      X509Certificate2  RootCertificate,
                                                      DateTimeOffset?   VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   [ IntermediateCertificate ],
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.1") ],  // serverAuth
                   VerificationTime
               );

        #endregion

        #region (static) ValidateServerChain (Certificate, IntermediateCertificates,     RootCertificate, VerificationTime = null)

        public static ChainReport ValidateServerChain(X509Certificate2               Certificate,
                                                      IEnumerable<X509Certificate2>  IntermediateCertificates,
                                                      X509Certificate2               RootCertificate,
                                                      DateTimeOffset?                VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   IntermediateCertificates,
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.1") ],  // serverAuth
                   VerificationTime
               );

        #endregion

        #region (static) ValidateServerChain (Certificate, IntermediateCertificateChain, RootCertificate, VerificationTime = null)

        public static ChainReport ValidateServerChain(X509Certificate2  Certificate,
                                                      X509Chain         IntermediateCertificateChain,
                                                      X509Certificate2  RootCertificate,
                                                      DateTimeOffset?   VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   IntermediateCertificateChain.ChainElements.
                       Select(chainElement => chainElement.Certificate).
                       Where (certificate  => certificate != Certificate),
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.1") ],  // serverAuth
                   VerificationTime
               );

        #endregion


        #region (static) ValidateClientChain (Certificate, IntermediateCertificate,      RootCertificate, VerificationTime = null)

        public static ChainReport ValidateClientChain(X509Certificate2  Certificate,
                                                      X509Certificate2  IntermediateCertificates,
                                                      X509Certificate2  RootCertificate,
                                                      DateTimeOffset?   VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   [ IntermediateCertificates ],
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.2") ],  // clientAuth
                   VerificationTime
               );

        #endregion

        #region (static) ValidateClientChain (Certificate, IntermediateCertificates,     RootCertificate, VerificationTime = null)

        public static ChainReport ValidateClientChain(X509Certificate2               Certificate,
                                                      IEnumerable<X509Certificate2>  IntermediateCertificates,
                                                      X509Certificate2               RootCertificate,
                                                      DateTimeOffset?                VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   IntermediateCertificates,
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.2") ],  // clientAuth
                   VerificationTime
               );

        #endregion

        #region (static) ValidateClientChain (Certificate, IntermediateCertificateChain, RootCertificate, VerificationTime = null)

        public static ChainReport ValidateClientChain(X509Certificate2  Certificate,
                                                      X509Chain         IntermediateCertificateChain,
                                                      X509Certificate2  RootCertificate,
                                                      DateTimeOffset?   VerificationTime   = null)

            => ValidateChain(
                   Certificate,
                   IntermediateCertificateChain.ChainElements.
                       Select(chainElement => chainElement.Certificate).
                       Where (certificate  => certificate != Certificate),
                   RootCertificate,
                   [ new Oid("1.3.6.1.5.5.7.3.2") ],  // clientAuth
                   VerificationTime
               );

        #endregion


        #region (static) GetCommonName(Name)

        public static String GetCommonName(X509Name Name)
        {

            var oids  = Name.GetOidList();
            var vals  = Name.GetValueList();

            for (var i = 0; i < oids.Count; i++)
                if (oids[i] is DerObjectIdentifier oid && oid.Equals(X509Name.CN))
                    return vals[i];

            return Name.ToString();

        }

        #endregion

        #region (static) ParseBasicConstraints(Certificate)

        public static BasicConstraints? ParseBasicConstraints(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.BasicConstraints);

            return octets is not null
                       ? BasicConstraints.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        #endregion

        #region (static) ParseKeyUsage(Certificate)

        public static KeyUsage? ParseKeyUsage(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.KeyUsage);

            return octets is not null
                       ? KeyUsage.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        #endregion

        #region (static) ParseExtendedKeyUsage(Certificate)

        public static SubjectKeyIdentifier? ParseSubjectKeyIdentifier(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);

            return octets is not null
                       ? SubjectKeyIdentifier.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        #endregion

        #region (static) ParseAuthorityKeyIdentifier(Certificate)

        public static AuthorityKeyIdentifier? ParseAuthorityKeyIdentifier(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);

            return octets is not null
                       ? AuthorityKeyIdentifier.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        #endregion


        public static String ExportCertificateAndPrivateKeyPEM(this X509Certificate2 Certificate)
        {

            if (!Certificate.HasPrivateKey)
                throw new InvalidOperationException("The given certificate does not have an associated private key!");

            var sb = new StringBuilder();

            sb.Append(Certificate.ExportCertificatePem());

            var privateKeyPEM = ExportPrivateKeyPkcs8PEM(Certificate);
            if (privateKeyPEM.IsNotNullOrEmpty())
            {
                sb.AppendLine();
                sb.Append(privateKeyPEM);
            }

            return sb.ToString();

        }

        private static String ExportPrivateKeyPkcs8PEM(X509Certificate2 cert)
        {

            try
            {

                if (cert.GetRSAPrivateKey()   is RSA   rsa)
                    return rsa.  ExportPkcs8PrivateKeyPem();

                if (cert.GetECDsaPrivateKey() is ECDsa ecdsa)
                    return ecdsa.ExportPkcs8PrivateKeyPem();

            }
            catch (Exception e)
            {
                return e.Message;
            }

            return String.Empty;

        }


        // BouncyCastle

        



    }

}
