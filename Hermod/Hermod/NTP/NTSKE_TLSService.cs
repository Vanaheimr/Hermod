/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// A Network Time Secure Key Establishment (NTS-KE) TLS service.
    /// </summary>
    internal class NTSKE_TLSService : DefaultTlsServer
    {

        #region Data

        // Just for TLS optimization!
        private readonly Byte[] encodedCertificate;

        #endregion

        #region Properties

        /// <summary>
        /// The used TLS certificate.
        /// </summary>
        public X509Certificate         Certificate    { get; }

        /// <summary>
        /// The used TLS private key.
        /// </summary>
        public ECPrivateKeyParameters  PrivateKey     { get; }

        /// <summary>
        /// The optional X.509 subject name to use for new certificates.
        /// </summary>
        public String?                 SubjectName    { get; }


        /// <summary>
        /// The NTP-KE Client-2-Server Key
        /// </summary>
        public Byte[]?                 NTS_C2S_Key    { get; private set; }

        /// <summary>
        /// The NTP-KE Server-2-Client Key
        /// </summary>
        public Byte[]?                 NTS_S2C_Key    { get; private set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new NTS-KE TLS service.
        /// </summary>
        /// <param name="Certificate">The TLS certificate to use.</param>
        /// <param name="PrivateKey">The private key to use.</param>
        /// <param name="SubjectName">The optional X.509 subject name to use for new certificates.</param>
        public NTSKE_TLSService(X509Certificate?         Certificate   = null,
                                ECPrivateKeyParameters?  PrivateKey    = null,
                                String?                  SubjectName   = null) : base(new BcTlsCrypto(new SecureRandom()))
        {

            if (Certificate is not null &&
                PrivateKey  is not null)
            {

                this.Certificate  = Certificate;
                this.PrivateKey   = PrivateKey;
                this.SubjectName  = SubjectName;

            }

            (this.Certificate,
             this.PrivateKey) = GenerateSelfSignedServerCertificate(
                                    SubjectName.IsNotNullOrEmpty()
                                        ? $"CN={SubjectName}"
                                        : "CN=ntpKE.example.org"
                                );

            this.encodedCertificate = this.Certificate.GetEncoded();

        }

        #endregion


        #region (public    override) NotifyHandshakeComplete()

        public override void NotifyHandshakeComplete()
        {

            base.NotifyHandshakeComplete();

            // Export 32 bytes for AES-SIV-CMAC-256:
            NTS_C2S_Key = m_context.ExportKeyingMaterial(
                "EXPORTER-network-time-security",
                [0x00, 0x00, 0x00, 0x0f, 0x00],
                32
            );

            NTS_S2C_Key = m_context.ExportKeyingMaterial(
                "EXPORTER-network-time-security",
                [0x00, 0x00, 0x00, 0x0f, 0x01],
                32
            );

        }

        #endregion

        #region (public    override) GetCredentials()

        public override TlsCredentials GetCredentials()
        {

            //int keyExchangeAlgorithm = m_context.SecurityParameters.KeyExchangeAlgorithm;

            //switch (keyExchangeAlgorithm)
            //{
            //    case KeyExchangeAlgorithm.DHE_DSS:
            //        return GetDsaSignerCredentials();

            //    case KeyExchangeAlgorithm.ECDHE_ECDSA:
                    return GetECDsaSignerCredentials();

            //    case KeyExchangeAlgorithm.DHE_RSA:
            //    case KeyExchangeAlgorithm.ECDHE_RSA:
            //        return GetRsaSignerCredentials();

            //    case KeyExchangeAlgorithm.RSA:
            //        return GetRsaEncryptionCredentials();

            //    default:
            //        // Note: internal error here; selected a key exchange we don't implement!
            //        throw new TlsFatalAlert(AlertDescription.internal_error);
            //}

        }

        #endregion

        #region (protected override) GetECDsaSignerCredentials()

        protected override TlsCredentialedSigner GetECDsaSignerCredentials()
        {

            var certificateEntry    = new CertificateEntry(new BcTlsCertificate((BcTlsCrypto) Crypto, encodedCertificate), null);
            var certificateChain    = new Certificate(TlsUtilities.EmptyBytes, [ certificateEntry ]);

            var signatureAlgorithm  = new SignatureAndHashAlgorithm(
                                          HashAlgorithm.sha256,
                                          SignatureAlgorithm.ecdsa
                                      );

            return new DefaultTlsCredentialedSigner(
                       new TlsCryptoParameters(m_context),
                       new BcTlsECDsaSigner((BcTlsCrypto) Crypto, PrivateKey),
                       certificateChain,
                       signatureAlgorithm
                   );

        }

        #endregion


        #region GenerateSelfSignedServerCertificate(SubjectName)

        /// <summary>
        /// Generates a TLS server certificate and ECC private key.
        /// </summary>
        /// <param name="SubjectName">The X.509 subject name to use for the new certificate.</param>
        public static (X509Certificate         Certificate,
                       ECPrivateKeyParameters  PrivateKey)
            GenerateSelfSignedServerCertificate(String SubjectName)

        {

            var randomGenerator     = new CryptoApiRandomGenerator();
            var random              = new SecureRandom(randomGenerator);
            var ecKeyPairGenerator  = new ECKeyPairGenerator();
            var ecSpec              = SecNamedCurves.GetByName("secp256r1");
            var ecDomainParameters  = new ECDomainParameters(ecSpec.Curve, ecSpec.G, ecSpec.N, ecSpec.H, ecSpec.GetSeed());
            var keyGenParams        = new ECKeyGenerationParameters(ecDomainParameters, random);
            ecKeyPairGenerator.Init(keyGenParams);
            var keyPair             = ecKeyPairGenerator.GenerateKeyPair();

            var certGenerator       = new X509V3CertificateGenerator();

            certGenerator.SetSerialNumber(new BigInteger(128, random));

            certGenerator.SetSubjectDN   (new X509Name(SubjectName));
            certGenerator.SetIssuerDN    (new X509Name(SubjectName));  // self-signed!

            certGenerator.SetNotBefore   (DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter    (DateTime.UtcNow.AddDays(30));

            certGenerator.SetPublicKey   (keyPair.Public);

            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true,
                new BasicConstraints(true)
            );

            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyAgreement)
            );

            certGenerator.AddExtension(
                X509Extensions.ExtendedKeyUsage,
                false,
                new ExtendedKeyUsage([ KeyPurposeID.id_kp_serverAuth ])
            );

            certGenerator.AddExtension(
                X509Extensions.SubjectAlternativeName,
                false,
                new DerSequence(
                    new Asn1Encodable[] {
                        new GeneralName(GeneralName.DnsName, "ntpKE1.example.org"),
                        new GeneralName(GeneralName.DnsName, "ntpKE2.example.org")
                    }
                )
            );

            var signedCertificate = certGenerator.Generate(
                                        new Asn1SignatureFactory(
                                            "SHA256WithECDSA",
                                            keyPair.Private,
                                            random
                                        )
                                    );

            return (
                signedCertificate,
                (ECPrivateKeyParameters) keyPair.Private
            );

        }

        #endregion


    }

}
