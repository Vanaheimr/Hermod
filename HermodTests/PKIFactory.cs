/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

using BCx509 = Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

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
                       SubjectName,
                       RootKeyPair,
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                       null, // self-signed!
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                       LifeTime,
                       IsCA:  true
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
                       SubjectName,
                       IntermediateKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           RootPrivateKey,
                           RootCertificate),
                       LifeTime,
                       IsCA:  true
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
                       SubjectName,
                       ServerKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           IntermediatePrivateKey,
                           IntermediateCertificate),
                       LifeTime,
                       IsCA:     false,
                       IsClient:  false
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
                       SubjectName,
                       ClientKeyPair,
                       new Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>(
                           IntermediatePrivateKey,
                           IntermediateCertificate),
                       LifeTime,
                       IsCA:     false,
                       IsClient:  true
                   );

        #endregion


        #region GenerateRSAKeyPair(NumberOfBits = 4096)

        /// <summary>
        /// Generate a new RSA key pair.
        /// </summary>
        /// <param name="NumberOfBits">The optional number of RSA bits to use.</param>
        public static AsymmetricCipherKeyPair GenerateRSAKeyPair(UInt16 NumberOfBits = 4096)
        {

            var keyPairGenerator = new RsaKeyPairGenerator();

            keyPairGenerator.Init(new KeyGenerationParameters(
                                      new SecureRandom(),
                                      NumberOfBits
                                  ));

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion

        #region GenerateECCKeyPair(ECCName      = "secp256r1")

        /// <summary>
        /// Generate a new ECC key pair.
        /// </summary>
        /// <param name="ECCName">The optional ECC curve to use.</param>
        public static AsymmetricCipherKeyPair GenerateECKeyPair(String ECCName = "secp256r1")
        {

            var keyPairGenerator = new ECKeyPairGenerator();

            keyPairGenerator.Init(new ECKeyGenerationParameters(
                                      Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetOid(ECCName),
                                      new SecureRandom()
                                  ));

            return keyPairGenerator.GenerateKeyPair();

        }

        #endregion


        #region GenerateCertificate(SubjectName, SubjectKeyPair, Issuer, LifeTime = null, IsCA = false, IsClient = false)

        /// <summary>
        /// Generate a new certificate.
        /// </summary>
        /// <param name="SubjectName">A friendly name for the owner of the crypto keys.</param>
        /// <param name="SubjectKeyPair">The crypto keys.</param>
        /// <param name="Issuer">The crypto key pait signing this certificate.</param>
        /// <param name="LifeTime">The life time of the certificate.</param>
        /// <param name="IsCA">Whether this certificate is for a certification authority.</param>
        /// <param name="IsClient">Whether this certificate is used for e.g. TLS client authentication.</param>
        public static BCx509.X509Certificate

            GenerateCertificate(String                                                 SubjectName,
                                AsymmetricCipherKeyPair                                SubjectKeyPair,
                                Tuple<AsymmetricKeyParameter, BCx509.X509Certificate>  Issuer,
                                TimeSpan?                                              LifeTime   = null,
                                Boolean                                                IsCA       = false,
                                Boolean                                                IsClient   = false)

        {

            var now     = Timestamp.Now;
            var x509v3  = new X509V3CertificateGenerator();

            x509v3.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
            x509v3.SetSubjectDN   (new X509Name($"CN={SubjectName}"));
            x509v3.SetPublicKey   (SubjectKeyPair.Public);
            x509v3.SetNotBefore   (now);
            x509v3.SetNotAfter    (now + (LifeTime ?? TimeSpan.FromDays(365)));

            if (Issuer is null)
                x509v3.SetIssuerDN(new X509Name($"CN={SubjectName}")); // self-signed

            else
            {

                x509v3.SetIssuerDN(Issuer.Item2.SubjectDN);
                x509v3.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id,
                                    false,
                                    new AuthorityKeyIdentifierStructure(Issuer.Item2));

            }

            if (IsCA)
                x509v3.AddExtension(X509Extensions.BasicConstraints.Id,
                                    true,
                                    new BasicConstraints(true));

            if (IsClient)
            {

                // Set Key Usage for client certificates
                x509v3.AddExtension(X509Extensions.KeyUsage.Id,
                                    true,
                                    new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

                // Set Extended Key Usage for client authentication
                x509v3.AddExtension(X509Extensions.ExtendedKeyUsage.Id,
                                    false,
                                    new ExtendedKeyUsage(KeyPurposeID.IdKPClientAuth));

            }

            return x509v3.Generate(new Asn1SignatureFactory(
                                       SubjectKeyPair.Public is RsaKeyParameters
                                           ? "SHA256WITHRSA"
                                           : "SHA256WITHECDSA",
                                       SubjectKeyPair.Private)
                                   );

        }

        #endregion

        #region ToDotNet(this X509Certificate)

        /// <summary>
        /// Convert the Bouncy Castle X.509 certificate to a .NET X.509 certificate.
        /// </summary>
        /// <param name="X509Certificate">A Bouncy Castle X.509 certificate.</param>
        public static X509Certificate2 ToDotNet(this BCx509.X509Certificate X509Certificate)

            => new (X509Certificate.GetEncoded());

        #endregion

    }

}
