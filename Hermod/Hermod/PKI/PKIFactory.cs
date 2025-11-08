/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using BCx509 = Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    public static class KeyUsageExtensions
    {
        public static Boolean HasFlag(this KeyUsage  KeyUsage,
                                      Int32          Flag)

            => (KeyUsage.GetBytes()[0] & Flag) != 0;


        public static Boolean HasNotFlag(this KeyUsage  KeyUsage,
                                         Int32          Flag)

            => (KeyUsage.GetBytes()[0] & Flag) == 0;

    }

    /// <summary>
    /// X.509 certificate types.
    /// </summary>
    public enum CertificateTypes
    {
        RootCA,
        IntermediateCA,
        Server,
        Client
    }

    public sealed record NameConstraintsInput(IEnumerable<String>?         PermittedDNS     = null,  // e.g. ["example.com", "sub.example.net"]
                                              IEnumerable<String>?         ExcludedDNS      = null,
                                              IEnumerable<IPAddressCidr>?  PermittedIP      = null,  // CIDR blocks
                                              IEnumerable<IPAddressCidr>?  ExcludedIP       = null,
                                              IEnumerable<String>?         PermittedEmail   = null,  // rfc822Name (domains like "example.com" or mailbox "user@example.com")
                                              IEnumerable<String>?         ExcludedEmail    = null);

    // Simple IPv4/IPv6 CIDR holder
    public sealed record IPAddressCidr(System.Net.IPAddress  Address,
                                       Byte                  PrefixLength);

    public sealed record CertificatePolicy(String   PolicyOID,  // e.g. "1.3.6.1.4.1.99999.1.1"
                                           Uri?     CpsUri           = null,
                                           String?  UserNoticeText   = null);

    /// <summary>
    /// Generating a RSA and/or ECC Public Key Infrastructure for testing.
    /// </summary>
    public static class PKIFactory
    {

        #region GenerateRSAKeyPair     (NumberOfBits = 4096)

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

        #region GenerateECCKeyPair     (ECCName      = "secp256r1")

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

        #region GenerateEd448KeyPair   ()

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

        #region GenerateEd25519KeyPair ()

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

        #region GenerateMLKEMKeyPair   (KEMName      = "ml_kem_768")

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

        #region GenerateMLDSAKeyPair   (DSAName      = "ml_dsa_65")

        /// <summary>
        /// Generate a new ML-DSA key pair.
        /// </summary>
        /// <param name="DSAName">The optional ML-DSA parameters to use.</param>
        public static AsymmetricCipherKeyPair GenerateMLDSAKeyPair(String DSAName = "ml_dsa_65")
        {

            var dsaKeyPairGenerator = new MLDsaKeyPairGenerator();

            dsaKeyPairGenerator.Init(
                new MLDsaKeyGenerationParameters(
                    new SecureRandom(),
                    DSAName switch {
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

        // ToDo: HybridKeyPair, PqcHybridKeyEncapsulation


        #region RandomSerial             (Length = 16)

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

        #region SelectSignatureAlgorithm (SigningKey)

        private static String SelectSignatureAlgorithm(AsymmetricKeyParameter  SigningKey,
                                                       AsymmetricKeyParameter  SubjectPublicKey)
        {

            // --- EdDSA (RFC 8032 / 8410 names) ---
            if (SigningKey is Ed25519PrivateKeyParameters)
                return "Ed25519";   // pure Ed25519

            if (SigningKey is Ed448PrivateKeyParameters)
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

            if (SigningKey is ECPrivateKeyParameters ecPrivateKey)
            {

                // Pick hash by elliptic curve strength
                var fieldSize = ecPrivateKey.Parameters.Curve.FieldSize;

                if      (fieldSize >= 521)  return "SHA512WITHECDSA";
                else if (fieldSize >= 384)  return "SHA384WITHECDSA";
                else                        return "SHA256WITHECDSA";

            }

            if (SigningKey is MLDsaPrivateKeyParameters mlDSAPrivateKey)
            {

                var parameters = mlDSAPrivateKey.Parameters;

                if (parameters == MLDsaParameters.ml_dsa_44)
                    return "ML-DSA-44";   // ~128-bit security (NIST Level 1)

                if (parameters == MLDsaParameters.ml_dsa_65)
                    return "ML-DSA-65";   // ~192-bit security (NIST Level 3)

                if (parameters == MLDsaParameters.ml_dsa_87)
                    return "ML-DSA-87";   // ~256-bit security (NIST Level 5)

            }

            if (SigningKey is MLKemPrivateKeyParameters mlKEMPrivateKey)
                throw new NotSupportedException("ML-KEM keys cannot sign X.509 certificates. Use a signature-capable issuer key (RSA/ECDSA/Ed25519/Ed448/ML-DSA)!");

            throw new ArgumentException("Unknown signing key type!");

        }

        #endregion


        private static Asn1Encodable BuildCertificatePolicies(IEnumerable<CertificatePolicy> CertificatePolicies)
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

        private static GeneralSubtree[] BuildDnsSubtrees(IEnumerable<string> dnsNames)
        {
            return dnsNames.Select(d =>
                new GeneralSubtree(new GeneralName(GeneralName.DnsName, d))).ToArray();
        }

        private static GeneralSubtree[] BuildEmailSubtrees(IEnumerable<string> emailsOrDomains)
        {
            // rfc822Name accepts either mailbox or domain; for domain-only constraints, just pass the domain string.
            return emailsOrDomains.Select(e =>
                new GeneralSubtree(new GeneralName(GeneralName.Rfc822Name, e))).ToArray();
        }

        private static GeneralSubtree[] BuildIpSubtrees(IEnumerable<IPAddressCidr> cidrs)
        {
            return cidrs.Select(c =>
            {
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

                return new GeneralSubtree(new GeneralName(GeneralName.IPAddress, new DerOctetString(outBytes)));
            }).ToArray();
        }

        private static NameConstraints BuildNameConstraints(NameConstraintsInput input)
        {

            var permitted = new List<GeneralSubtree>();
            var excluded  = new List<GeneralSubtree>();

            if (input.PermittedDNS?.  Any() == true) permitted.AddRange(BuildDnsSubtrees  (input.PermittedDNS));
            if (input.ExcludedDNS?.   Any() == true) excluded .AddRange(BuildDnsSubtrees  (input.ExcludedDNS));
            if (input.PermittedEmail?.Any() == true) permitted.AddRange(BuildEmailSubtrees(input.PermittedEmail));
            if (input.ExcludedEmail?. Any() == true) excluded .AddRange(BuildEmailSubtrees(input.ExcludedEmail));
            if (input.PermittedIP?.   Any() == true) permitted.AddRange(BuildIpSubtrees   (input.PermittedIP));
            if (input.ExcludedIP?.    Any() == true) excluded .AddRange(BuildIpSubtrees   (input.ExcludedIP));

            return new NameConstraints(
                       permitted as IList<GeneralSubtree>,
                       excluded  as IList<GeneralSubtree>
                   );

        }


        //ToDo: Allow hybrid certs!
        // var generator = new CmsSignedDataGenerator();
        // generator.AddSigner(eccKeyPair1.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // 1st ECC signature
        // generator.AddSigner(eccKeyPair2.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // 2nd ECC signature
        // var signedData = generator.Generate(certBytes, true);                                    // certBytes = serialized X.509 certificate
        // signedData.Verify();



        #region CreateRootCACertificate (RootKeyPair,         SubjectName,                                                  LifeTime = null)

        /// <summary>
        /// Generate a self-signed root certificate for the root certification authority.
        /// </summary>
        /// <param name="RootKeyPair">A crypto key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateRootCACertificate(AsymmetricCipherKeyPair          RootKeyPair,
                                    String                           SubjectName,
                                    TimeSpan?                        LifeTime                 = null,
                                    IEnumerable<URL>?                CRL_DistributionPoints   = null,
                                    IEnumerable<URL>?                AIA_OCSPURLs             = null,
                                    IEnumerable<URL>?                AIA_CAIssuersURLs        = null,
                                    IEnumerable<CertificatePolicy>?  CertificatePolicies      = null)

                => SignCertificate(
                       CertificateTypes.RootCA,
                       SubjectName,
                       RootKeyPair,
                       null, // self-signed!
                       LifeTime,
                       null,
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

        #region CreateIntermediateCA    (IntermediateKeyPair, SubjectName, RootPrivateKey,         RootCertificate,         LifeTime = null)

        /// <summary>
        /// Generate an intermediate server certification authority signed by the root CA.
        /// </summary>
        /// <param name="IntermediateKeyPair">A crypto key pair. Does not need to be of the same type as the root CA key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="RootPrivateKey">The private key for signing the new certificate.</param>
        /// <param name="RootCertificate">The certificate for signing the new certificate.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateIntermediateCA(AsymmetricCipherKeyPair  IntermediateKeyPair,
                                 String                   SubjectName,
                                 AsymmetricKeyParameter   RootPrivateKey,
                                 BCx509.X509Certificate   RootCertificate,
                                 TimeSpan?                LifeTime   = null)

                => SignCertificate(
                       CertificateTypes.IntermediateCA,
                       SubjectName,
                       IntermediateKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           RootPrivateKey,
                           RootCertificate
                       ),
                       LifeTime
                   );

        #endregion

        #region CreateServerCertificate (ServerKeyPair,       SubjectName, IntermediatePrivateKey, IntermediateCertificate, LifeTime = null)

        /// <summary>
        /// Generate a server certificate signed by the intermediate server CA.
        /// </summary>
        /// <param name="ServerKeyPair">A crypto key pair. Does not need to be of the same type as the root/intermediate CA key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="IntermediatePrivateKey">The private key for signing the new certificate.</param>
        /// <param name="IntermediateCertificate">The certificate for signing the new certificate.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateServerCertificate(AsymmetricCipherKeyPair  ServerKeyPair,
                                    String                   SubjectName,
                                    AsymmetricKeyParameter   IntermediatePrivateKey,
                                    BCx509.X509Certificate   IntermediateCertificate,
                                    TimeSpan?                LifeTime   = null)

                => SignCertificate(
                       CertificateTypes.Server,
                       SubjectName,
                       ServerKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           IntermediatePrivateKey,
                           IntermediateCertificate
                       ),
                       LifeTime
                   );

        #endregion

        #region CreateClientCertificate (ClientKeyPair,       SubjectName, IntermediatePrivateKey, IntermediateCertificate, LifeTime = null)

        /// <summary>
        /// Generate a client certificate signed by the intermediate client CA.
        /// </summary>
        /// <param name="ClientKeyPair">A crypto key pair. Does not need to be of the same type as the root/intermediate CA key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="IntermediatePrivateKey">The private key for signing the new certificate.</param>
        /// <param name="IntermediateCertificate">The certificate for signing the new certificate.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateClientCertificate(AsymmetricCipherKeyPair  ClientKeyPair,
                                    String                   SubjectName,
                                    AsymmetricKeyParameter   IntermediatePrivateKey,
                                    BCx509.X509Certificate   IntermediateCertificate,
                                    TimeSpan?                LifeTime   = null)

                => SignCertificate(
                       CertificateTypes.Client,
                       SubjectName,
                       ClientKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           IntermediatePrivateKey,
                           IntermediateCertificate
                       ),
                       LifeTime
                   );

        #endregion



        #region SignServerCertificate (                 SubjectName, SubjectKeyPair, Issuer = null, LifeTime = null)

        /// <summary>
        /// Sign a new X.509 server certificate.
        /// </summary>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectKeyPair">The crypto keys.</param>
        /// <param name="Issuer">The (optional) crypto key pair signing this certificate. Optional means that this certificate will be self-signed!</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            SignServerCertificate(String                                                  SubjectName,
                                  AsymmetricCipherKeyPair                                 SubjectKeyPair,
                                  Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>?  Issuer                   = null,
                                  TimeSpan?                                               LifeTime                 = null,
                                  GeneralNames?                                           SubjectAltNames          = null,  // e.g. DNS/IP for server certs
                                  Byte?                                                   PathLenConstraint        = null,
                                  IEnumerable<URL>?                                       CRL_DistributionPoints   = null,
                                  IEnumerable<URL>?                                       AIA_OCSPURLs             = null,
                                  IEnumerable<URL>?                                       AIA_CAIssuersURLs        = null)

                => SignCertificate(
                       CertificateTypes.Server,
                       SubjectName,
                       SubjectKeyPair,
                       Issuer,
                       LifeTime,
                       SubjectAltNames,
                       PathLenConstraint,
                       null,
                       CRL_DistributionPoints
                   );

        #endregion

        #region SignClientCertificate (                 SubjectName, SubjectKeyPair, Issuer = null, LifeTime = null)

        /// <summary>
        /// Sign a new X.509 client certificate.
        /// </summary>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectKeyPair">The crypto keys.</param>
        /// <param name="Issuer">The (optional) crypto key pair signing this certificate. Optional means that this certificate will be self-signed!</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            SignClientCertificate(String                                                  SubjectName,
                                  AsymmetricCipherKeyPair                                 SubjectKeyPair,
                                  Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>?  Issuer                   = null,
                                  TimeSpan?                                               LifeTime                 = null,
                                  Byte?                                                   PathLenConstraint        = null,
                                  IEnumerable<URL>?                                       CRL_DistributionPoints   = null,
                                  IEnumerable<URL>?                                       AIA_OCSPURLs             = null,
                                  IEnumerable<URL>?                                       AIA_CAIssuersURLs        = null)

                => SignCertificate(
                       CertificateTypes.Client,
                       SubjectName,
                       SubjectKeyPair,
                       Issuer,
                       LifeTime,
                       null,
                       PathLenConstraint,
                       null,
                       CRL_DistributionPoints
                   );

        #endregion

        #region SignCertificate       (CertificateType, SubjectName, SubjectKeyPair, Issuer = null, LifeTime = null)

        /// <summary>
        /// Sign a new X.509 certificate.
        /// </summary>
        /// <param name="CertificateType">The type of the certificate.</param>
        /// <param name="SubjectCommonName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectKeyPair">The crypto keys.</param>
        /// <param name="Issuer">The (optional) crypto key pair signing this certificate. Optional means that this certificate will be self-signed!</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            SignCertificate(CertificateTypes                                        CertificateType,
                            String                                                  SubjectCommonName,
                            AsymmetricCipherKeyPair                                 SubjectKeyPair,
                            Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>?  Issuer                   = null,
                            TimeSpan?                                               LifeTime                 = null,
                            GeneralNames?                                           SubjectAltNames          = null,    // e.g. DNS/IP for server certs
                            Byte?                                                   PathLenConstraint        = null,
                            NameConstraintsInput?                                   NameConstraints          = null,
                            IEnumerable<URL>?                                       CRL_DistributionPoints   = null,
                            IEnumerable<URL>?                                       AIA_OCSPURLs             = null,
                            IEnumerable<URL>?                                       AIA_CAIssuersURLs        = null,
                            Boolean?                                                TLSMustStaple            = false,  // true -> id-pe-tlsfeature: status_request(5)
                            Boolean?                                                TLSMustStapleV2          = false,  // when true and TLSMustStaple==true -> {5,17})
                            IEnumerable<CertificatePolicy>?                         CertificatePolicies      = null)

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
            certGen.SetSubjectDN    (new X509Name($"CN={SubjectCommonName}"));//, O=GraphDefined GmbH, OU=GraphDefined PKI Services"));
            certGen.SetPublicKey    (SubjectKeyPair.Public);

            // Tolerate small clock skew!
            certGen.SetNotBefore    (now.     DateTime.AddMinutes(-5).ToUniversalTime());
            certGen.SetNotAfter     (notAfter.DateTime.               ToUniversalTime());

            certGen.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,  // non-critical
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(
                    SubjectKeyPair.Public
                )
            );



            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/root-configuration-file.html
            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/intermediate-configuration-file.html


            if (CertificateType == CertificateTypes.RootCA)
            {

                // Self-signed root CA!
                certGen.SetIssuerDN(new X509Name($"CN={SubjectCommonName}"));

                certGen.AddExtension(
                    X509Extensions.KeyUsage,
                    true,  // critical
                    new KeyUsage(
                        //KeyUsage.DigitalSignature |
                        KeyUsage.KeyCertSign |
                        KeyUsage.CrlSign
                    )
                );

                certGen.AddExtension(
                    X509Extensions.BasicConstraints,
                    true,  // critical
                    PathLenConstraint is Byte caPathLengthConstraint
                        ? new BasicConstraints(caPathLengthConstraint)
                        : new BasicConstraints(true) // isCA = true
                );

                // Optional AKI-on-root mirroring SKI
                certGen.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    false,  // non-critical
                    X509ExtensionUtilities.CreateAuthorityKeyIdentifier(
                        SubjectKeyPair.Public
                    )
                );

            }

            else
            {

                if (Issuer is null)
                    throw new ArgumentException("Issuer must be provided for non-root CA certificates!");

                var issuerKey   = Issuer.Item1;
                var issuerCert  = Issuer.Item2;

                // Guard: issuer must be a CA and not expired
                var issuerIsCA  = BasicConstraints.GetInstance(
                                      X509ExtensionUtilities.FromExtensionValue(
                                          issuerCert.GetExtensionValue(X509Extensions.BasicConstraints)
                                      )
                                  )?.IsCA() ?? false;

                if (!issuerIsCA)
                    throw new InvalidOperationException("Issuer certificate is not a CA (CA:FALSE).");

                // Reduce certificate.NotAfter to issuer.NotAfter
                if (notAfter > issuerCert.NotAfter.ToUniversalTime())
                    certGen.SetNotAfter(issuerCert.NotAfter.ToUniversalTime());

                certGen.SetIssuerDN (issuerCert.SubjectDN);

                certGen.AddExtension(
                    X509Extensions.AuthorityKeyIdentifier,
                    false,  // non-critical
                    X509ExtensionUtilities.CreateAuthorityKeyIdentifier(issuerCert)
                );


                switch (CertificateType)
                {

                    case CertificateTypes.IntermediateCA:

                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            new KeyUsage(
                                //KeyUsage.DigitalSignature |
                                KeyUsage.KeyCertSign      |
                                KeyUsage.CrlSign
                            )
                        );

                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            PathLenConstraint is Byte caPathLengthConstraint
                                ? new BasicConstraints(caPathLengthConstraint)
                                : new BasicConstraints(true) // isCA = true
                        );

                        if (NameConstraints is not null)
                            certGen.AddExtension(
                                X509Extensions.NameConstraints,
                                true,  // critical is recommended
                                BuildNameConstraints(NameConstraints)
                            );

                    break;

                    case CertificateTypes.Server:

                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            new BasicConstraints(false) // isCA = false
                        );

                        // KeyUsage
                        //   RSA:         digitalSignature + keyEncipherment
                        //   ECDSA/EdDSA: digitalSignature only
                        var isRsa     = SubjectKeyPair.Public is RsaKeyParameters;
                        var keyUsage  = isRsa
                            ? new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment)
                            : new KeyUsage(KeyUsage.DigitalSignature);

                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            keyUsage
                        );

                        // EKU (non-critical): serverAuth
                        certGen.AddExtension(
                            X509Extensions.ExtendedKeyUsage,
                            false,  // non-critical
                            new ExtendedKeyUsage(
                                KeyPurposeID.id_kp_serverAuth
                            )
                        );


                        // In case: Mirror CN as DNS!
                        SubjectAltNames ??= new GeneralNames(
                                                new GeneralName(
                                                    GeneralName.DnsName,
                                                    SubjectCommonName
                                                )
                                            );

                        // SAN is mandatory for TLS name checks
                        certGen.AddExtension(
                            X509Extensions.SubjectAlternativeName,
                            false,  // non-critical
                            SubjectAltNames
                        );


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

                        certGen.AddExtension(
                            X509Extensions.BasicConstraints,
                            true,  // critical
                            new BasicConstraints(false) // isCA = false
                        );

                        certGen.AddExtension(
                            X509Extensions.KeyUsage,
                            true,  // critical
                            new KeyUsage(
                                KeyUsage.DigitalSignature
                            )
                        );

                        certGen.AddExtension(
                            X509Extensions.ExtendedKeyUsage,
                            false,  // non-critical
                            new ExtendedKeyUsage(
                                KeyPurposeID.id_kp_clientAuth
                            )
                        );

                    break;

                }


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
                    SelectSignatureAlgorithm(
                        Issuer?.Item1 ?? SubjectKeyPair.Private,
                        SubjectKeyPair.Public
                    ),
                    Issuer?.Item1 ?? SubjectKeyPair.Private,
                    new SecureRandom()
                )
            );

        }

        #endregion


        public static BCx509.X509Certificate SignCSR(Pkcs10CertificationRequest csr,
                                                     AsymmetricCipherKeyPair    caKeyPair,
                                                     DateTimeOffset             notBefore,
                                                     DateTimeOffset             notAfter)
        {

            var now           = Timestamp.Now;
            var x509v3        = new X509V3CertificateGenerator();

        //    // Extrahiere Daten aus CSR
        //    var subject = csr.;
        //    var publicKey = csr.SubjectPublicKey;
        //    var extensions = csr.GetCertificationRequestInfo().Attributes;  // Für Extensions (z. B. SAN)

        //    // Setze Zertifikats-Felder
        //    certificateGenerator.SetSerialNumber(BigInteger.ProbablePrime(120, random));  // Eindeutige Seriennummer
        //    certificateGenerator.SetIssuerDN(new X509Name("CN=MeineCA"));  // CA's Issuer (passe an)
        //    certificateGenerator.SetSubjectDN(subject);  // Subject aus CSR
        //    certificateGenerator.SetNotBefore(notBefore);
        //    certificateGenerator.SetNotAfter(notAfter);
        //    certificateGenerator.SetPublicKey(publicKey);  // Public Key aus CSR

        //    // Füge Extensions aus CSR hinzu (z. B. Key Usage, SAN)
        //    if (extensions != null)
        //    {
        //        foreach (var attr in extensions)
        //        {
        //            // Hier: Parse und addiere (z. B. via certificateGenerator.AddExtension)
        //            // Beispiel für BasicConstraints: certificateGenerator.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        //        }
        //    }

        //    // Signiere mit CA's privatem Key (ECC-Beispiel: ECDSA mit SHA256)
        //    var signatureFactory = new Asn1SignatureFactory("SHA256withECDSA", caPrivateKey, random);
        //    var certificate = certificateGenerator.Generate(signatureFactory);

        //    return certificate;

            return null;

        }




        #region ToDotNet(this Certificate, PrivateKey = null, CACertificates = null)

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
                return new (Certificate.GetEncoded());

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
                // ???
            }

            return null;

        }

        #endregion









        public static BasicConstraints? ParseBasicConstraints(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.BasicConstraints);

            return octets is not null
                       ? BasicConstraints.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        public static KeyUsage? ParseKeyUsage(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.KeyUsage);

            return octets is not null
                       ? KeyUsage.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        public static SubjectKeyIdentifier? ParseSubjectKeyIdentifier(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);

            return octets is not null
                       ? SubjectKeyIdentifier.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

        public static AuthorityKeyIdentifier? ParseAuthorityKeyIdentifier(BCx509.X509Certificate Certificate)
        {

            var octets = Certificate.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);

            return octets is not null
                       ? AuthorityKeyIdentifier.GetInstance(
                             X509ExtensionUtilities.FromExtensionValue(octets)
                         )
                       : null;

        }

    }

}
