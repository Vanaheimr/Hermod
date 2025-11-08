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
using Org.BouncyCastle.Utilities;

using BCx509 = Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS
{

    /// <summary>
    /// X.509 certificate types.
    /// </summary>
    public enum CertificateTypes
    {
        RootCA,
        CA,
        Server,
        Client
    }

    /// <summary>
    /// Generating a RSA and/or ECC Public Key Infrastructure for testing.
    /// </summary>
    public static class PKIFactory
    {

        #region CreateRootCA           (RootKeyPair,         SubjectName,                                                  LifeTime = null)

        /// <summary>
        /// Generate a self-signed root certificate for the root certification authority.
        /// </summary>
        /// <param name="RootKeyPair">A crypto key pair.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            CreateRootCA(AsymmetricCipherKeyPair  RootKeyPair,
                         String                   SubjectName,
                         TimeSpan?                LifeTime   = null)

                => GenerateCertificate(
                       CertificateTypes.RootCA,
                       SubjectName,
                       RootKeyPair,
                       null, // self-signed!
                       LifeTime
                   );

        #endregion

        #region CreateIntermediateCA   (IntermediateKeyPair, SubjectName, RootPrivateKey,         RootCertificate,         LifeTime = null)

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

                => GenerateCertificate(
                       CertificateTypes.CA,
                       SubjectName,
                       IntermediateKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           RootPrivateKey,
                           RootCertificate
                       ),
                       LifeTime
                   );

        #endregion

        #region CreateServerCertificate(ServerKeyPair,       SubjectName, IntermediatePrivateKey, IntermediateCertificate, LifeTime = null)

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

                => GenerateCertificate(
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

        #region CreateClientCertificate(ClientKeyPair,       SubjectName, IntermediatePrivateKey, IntermediateCertificate, LifeTime = null)

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

                => GenerateCertificate(
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


        #region GenerateRSAKeyPair   (NumberOfBits = 4096)

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

        #region GenerateECCKeyPair   (ECCName      = "secp256r1")

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

        #region GenerateMLKEMKeyPair (KEMName      = "ml_kem_768")

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

        #region GenerateMLDSAKeyPair (DSAName      = "ml_dsa_65")

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
                        "ml_dsa_44" => MLDsaParameters.ml_dsa_44,  // AES-128-Security (NIST Level 1)
                        "ml_dsa_65" => MLDsaParameters.ml_dsa_65,  // AES-192-Security (NIST Level 3)
                        "ml_dsa_87" => MLDsaParameters.ml_dsa_87,  // AES-256-Security (NIST Level 5)
                        _ => throw new ArgumentException("Invalid ML-DSA parameters!")
                    }
                )
            );

            return dsaKeyPairGenerator.GenerateKeyPair();

        }

        #endregion

        // ToDo: HybridKeyPair, PqcHybridKeyEncapsulation

        // var generator = new CmsSignedDataGenerator();
        // generator.AddSigner(eccKeyPair1.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // Erste ECC-Signatur
        // generator.AddSigner(eccKeyPair2.Private, "SHA256withECDSA", CmsSignerDigestCalculator);  // Zweite
        // var signedData = generator.Generate(certBytes, true);                                    // certBytes = serialisiertes X.509
        // signedData.Verify();


        #region GenerateCertificate(CertificateType, SubjectName, SubjectKeyPair, Issuer = null, LifeTime = null)

        /// <summary>
        /// Generate a new certificate.
        /// </summary>
        /// <param name="CertificateType">The type of the certificate.</param>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectKeyPair">The crypto keys.</param>
        /// <param name="Issuer">The (optional) crypto key pair signing this certificate. Optional means that this certificate will be self-signed!</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        public static BCx509.X509Certificate

            GenerateCertificate(CertificateTypes                                        CertificateType,
                                String                                                  SubjectName,
                                AsymmetricCipherKeyPair                                 SubjectKeyPair,
                                Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>?  Issuer     = null,
                                TimeSpan?                                               LifeTime   = null)

        {

            var now           = Timestamp.Now;
            var secureRandom  = new SecureRandom();
            var x509v3        = new X509V3CertificateGenerator();

            x509v3.SetSerialNumber(//BigInteger.ProbablePrime(120, new Random()));
                                   BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), secureRandom));
            x509v3.SetSubjectDN   (new X509Name($"CN={SubjectName}, O=GraphDefined GmbH, OU=GraphDefined PKI Services"));
            x509v3.SetPublicKey   (SubjectKeyPair.Public);
            x509v3.SetNotBefore   (now.DateTime);
            x509v3.SetNotAfter   ((now + (LifeTime ?? TimeSpan.FromDays(365))).DateTime);

            if (Issuer is null)
                x509v3.SetIssuerDN(new X509Name($"CN={SubjectName}")); // self-signed

            else
            {

                x509v3.SetIssuerDN (Issuer.Item2.SubjectDN);
                x509v3.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id,
                                    false,
                                    new AuthorityKeyIdentifierStructure(Issuer.Item2));

            }

            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/root-configuration-file.html
            // https://jamielinux.com/docs/openssl-certificate-authority/appendix/intermediate-configuration-file.html
            switch (CertificateType)
            {

                case CertificateTypes.RootCA:

                    x509v3.AddExtension(X509Extensions.BasicConstraints.Id,
                                        true,
                                        new BasicConstraints(true));

                    x509v3.AddExtension(X509Extensions.KeyUsage,
                                        true,
                                        new KeyUsage(
                                            KeyUsage.DigitalSignature |
                                            KeyUsage.KeyCertSign |
                                            KeyUsage.CrlSign
                                        ));

                    break;


                case CertificateTypes.CA:

                    x509v3.AddExtension(X509Extensions.BasicConstraints.Id,
                                        true,
                                        // A CA certificate, but it cannot be used to sign other CA certificates,
                                        // only end-entity certificates.
                                        new BasicConstraints(0));

                    x509v3.AddExtension(X509Extensions.KeyUsage,
                                        true,
                                        new KeyUsage(
                                            KeyUsage.DigitalSignature |
                                            KeyUsage.KeyCertSign |
                                            KeyUsage.CrlSign
                                        ));

                    break;


                case CertificateTypes.Server:

                    // Set Key Usage for server certificates
                    x509v3.AddExtension(X509Extensions.KeyUsage.Id,
                                        true,
                                        new KeyUsage(
                                            KeyUsage.DigitalSignature |
                                            KeyUsage.KeyEncipherment
                                        ));

                    // Set Extended Key Usage for server authentication
                    x509v3.AddExtension(X509Extensions.ExtendedKeyUsage.Id,
                                        false,
                                        new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));

                    break;


                case CertificateTypes.Client:

                    // Set Key Usage for client certificates
                    x509v3.AddExtension(X509Extensions.KeyUsage.Id,
                                        true,
                                        new KeyUsage(
                                            KeyUsage.NonRepudiation   |
                                            KeyUsage.DigitalSignature |
                                            KeyUsage.KeyEncipherment
                                        ));

                    // Set Extended Key Usage for client authentication
                    x509v3.AddExtension(X509Extensions.ExtendedKeyUsage.Id,
                                        false,
                                        new ExtendedKeyUsage(KeyPurposeID.id_kp_clientAuth));

                    break;

            }

            return x509v3.Generate(new Asn1SignatureFactory(
                                       SubjectKeyPair.Public is RsaKeyParameters
                                           ? "SHA256WITHRSA"
                                           : "SHA256WITHECDSA",
                                       Issuer?.Item1 ?? SubjectKeyPair.Private)
                                   );

        }

        #endregion

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


    }

}
