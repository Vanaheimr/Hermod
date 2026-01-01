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

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pqc.Crypto.Falcon;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.PKI;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// Generate server certificates directly signed by a rootCA for a Public Key Infrastructure tests.
    /// </summary>
    [TestFixture]
    public class Generate_ServerCertificates_Direct_Tests
    {

        #region (private static) ValidateServerCertificate(IssuerCertificate, ServerCertificate)

        /// <summary>
        /// Validate the given server certificate.
        /// </summary>
        /// <param name="IssuerCertificate">A X.509 issuer certificate.</param>
        /// <param name="ServerCertificate">A X.509 server certificate.</param>
        private static void ValidateServerCertificate(X509Certificate  IssuerCertificate,
                                                      X509Certificate  ServerCertificate)
        {

            Assert.That(IssuerCertificate,                                                  Is.Not.Null);
            Assert.That(ServerCertificate,                                                  Is.Not.Null);

            var serverPublicKey = ServerCertificate.GetPublicKey();
            Assert.That(serverPublicKey,                                                    Is.Not.Null);
            Assert.That(ServerCertificate.Version,                                          Is.EqualTo(3),                                                         "Must be X.509v3 because of extensions!");
            //Assert.That(ServerCertificate.SubjectDN.ToString(),                             Is.EqualTo    ("CN=Hermod RootCA"),                                    "Unexpected rootCA certificate subject DN!");
            Assert.That(ServerCertificate.IssuerDN. ToString(),                             Is.EqualTo    (IssuerCertificate.SubjectDN.ToString()),                "The server certificate issuer DN must match the issuer subject DN!");

            var serialUnsigned          = IssuerCertificate.SerialNumber.ToByteArrayUnsigned();
            Assert.That(serialUnsigned.Length,                                              Is.LessThanOrEqualTo(20),                                              "Serial must be ≤ 20 octets.");
            Assert.That(serialUnsigned.Length,                                              Is.GreaterThan(0),                                                     "Serial must be positive!");

            Assert.That(ServerCertificate.NotBefore,                                        Is.GreaterThanOrEqualTo(IssuerCertificate.NotBefore),                  "The server certificate must not be valid before the issuer certificate!");
            Assert.That(ServerCertificate.NotAfter,                                         Is.LessThanOrEqualTo   (IssuerCertificate.NotAfter),                   "The server certificate must not be valid after the issuer certificate!");
            ServerCertificate.CheckValidity(Timestamp.Now.DateTime.ToUniversalTime());

            // Verify with the issuer public key
            ServerCertificate.Verify(IssuerCertificate.GetPublicKey());


            // Extensions: Criticality Sets
            var criticalOids    = new HashSet<String>(ServerCertificate.GetCriticalExtensionOids());
            var nonCriticalOids = new HashSet<String>(ServerCertificate.GetNonCriticalExtensionOids());


            // Extensions: BasicConstraints (CA = false, no pathLen)
            var basicConstraints        = PKIFactory.ParseBasicConstraints(ServerCertificate);
            Assert.That(basicConstraints,                                                   Is.Not.Null,                                                           "BasicConstraints must be present!");
            Assert.That(basicConstraints!.IsCA(),                                           Is.False,                                                              "Server certificates must have CA=false!");
            Assert.That(basicConstraints.PathLenConstraint,                                 Is.Null,                                                               "Server certificates must not have a path length constraint!");
            Assert.That(criticalOids.Contains(X509Extensions.BasicConstraints.Id),          Is.True,                                                               "BasicConstraints must be critical!");


            // Extensions: KeyUsage (DigitalSignature? + KeyEncipherment?)
            var keyUsage                = PKIFactory.ParseKeyUsage(ServerCertificate);
            Assert.That(keyUsage,                                                           Is.Not.Null,                                                           "KeyUsage must be present!");
            Assert.That(criticalOids.Contains(X509Extensions.KeyUsage.Id),                  Is.True,                                                               "KeyUsage must be critical!");

            if (serverPublicKey is RsaKeyParameters           ||
                serverPublicKey is ECPublicKeyParameters      ||
                serverPublicKey is Ed25519PublicKeyParameters ||
                serverPublicKey is Ed448PublicKeyParameters   ||
                serverPublicKey is FalconPublicKeyParameters  ||
                serverPublicKey is SlhDsaPublicKeyParameters  ||
                serverPublicKey is MLDsaPublicKeyParameters)
            {
                Assert.That(keyUsage!.HasFlag(KeyUsage.DigitalSignature),                   Is.True,                                                              $"{serverPublicKey.GetAlgorithmName()} server certificates shall allow digital signatures!");
            }

            if (serverPublicKey is RsaKeyParameters           ||
                serverPublicKey is MLKemPublicKeyParameters)
            {
                Assert.That(keyUsage!.HasFlag(KeyUsage.KeyEncipherment),                    Is.True,                                                              $"{serverPublicKey.GetAlgorithmName()} server certificates shall allow key encipherment!");
            }

            Assert.That(keyUsage!.HasFlag(KeyUsage.NonRepudiation),                         Is.False,                                                              "Server certificates must not allow non-repudiation!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.DataEncipherment),                       Is.False,                                                              "Server certificates must not allow data encipherment!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.KeyAgreement),                           Is.False,                                                              "Server certificates must not allow key agreement!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.KeyCertSign),                            Is.False,                                                              "Server certificates must not allow certificate signing!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.CrlSign),                                Is.False,                                                              "Server certificates must not allow CRL signing!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.EncipherOnly),                           Is.False,                                                              "Server certificates must not allow encipher only!");
            Assert.That(keyUsage!.HasFlag(KeyUsage.DecipherOnly),                           Is.False,                                                              "Server certificates must not allow decipher only!");


            // Extensions: Subject Key Identifier (SKI) present (20 bytes) & non-critical
            var subjectKeyIdentifier    = PKIFactory.ParseSubjectKeyIdentifier  (ServerCertificate);
            Assert.That(subjectKeyIdentifier,                                               Is.Not.Null,                                                           "SubjectKeyIdentifier should be present!");
            Assert.That(nonCriticalOids.Contains(X509Extensions.SubjectKeyIdentifier.Id),   Is.True,                                                               "SubjectKeyIdentifier should be non-critical!");
            Assert.That(subjectKeyIdentifier!.GetKeyIdentifier().Length,                    Is.EqualTo(20),                                                        "SubjectKeyIdentifier should be 20 bytes (SHA-1 keyIdentifier)!");


            // Extensions: Authority Key Identifier
            var authorityKeyIdentifier  = PKIFactory.ParseAuthorityKeyIdentifier(ServerCertificate);
            Assert.That(authorityKeyIdentifier,                              Is.Not.Null,                                                                          "AuthorityKeyIdentifier should be present!");
            Assert.That(nonCriticalOids.Contains(X509Extensions.AuthorityKeyIdentifier.Id), Is.True,                                                               "AuthorityKeyIdentifier should be non-critical.");

            var issuerKeyIdentifier     = PKIFactory.ParseAuthorityKeyIdentifier(IssuerCertificate);
            Assert.That(authorityKeyIdentifier!.GetKeyIdentifier().ToHexString(),           Is.EqualTo(issuerKeyIdentifier!.GetKeyIdentifier().ToHexString()),     "AKI.keyIdentifier must match the rootCA SKI.keyIdentifier!");


            // Extended Key Usage
            var ekuOctets    = ServerCertificate.GetExtensionValue(X509Extensions.ExtendedKeyUsage);
            Assert.That(ekuOctets,                                                          Is.Not.Null,                                                           "ExtendedKeyUsage must be present on server certificates!");
            var eku          = ExtendedKeyUsage.GetInstance(X509ExtensionUtilities.FromExtensionValue(ekuOctets));
            Assert.That(eku, Is.Not.Null);
            Assert.That(eku.HasKeyPurposeId(KeyPurposeID.id_kp_serverAuth),                 Is.True,                                                               "EKU must include 'id_kp_serverAuth'!");
            Assert.That(nonCriticalOids.Contains(X509Extensions.ExtendedKeyUsage.Id),       Is.True,                                                               "ExtendedKeyUsage should be non-critical on server certificates!");

            var allowedEKUs  = new HashSet<DerObjectIdentifier> {
                                   KeyPurposeID.id_kp_serverAuth
                                   //KeyPurposeID.id_kp_clientAuth   // allowed if you use the same certificate for mTLS!
                               };

            foreach (var kpid in eku.GetAllUsages())
                Assert.That(allowedEKUs.Contains(kpid), $"Unexpected Extended Key Usage '{kpid.Id}' on server certificate!");



            // Subject Alternative Names
            //Assert.That(RootCACertificate.GetExtensionValue(X509Extensions.SubjectAlternativeName),   Is.Null, "SubjectAlternativeNames should be absent on a rootCA!");




            //// AIA/CRLDP: if present, must be non-critical and URI-based
            //var crldpExt = ServerCertificate.GetExtensionValue(X509Extensions.CrlDistributionPoints);
            //if (crldpExt is not null)
            //{

            //    Assert.That(nonCriticalOids.Contains(X509Extensions.CrlDistributionPoints.Id), Is.True, "CRL DistributionPoints should be non-critical!");

            //    var dp = CrlDistPoint.GetInstance(X509ExtensionUtilities.FromExtensionValue(crldpExt));
            //    foreach (var distributionPoint in dp.GetDistributionPoints())
            //    {

            //        var name = distributionPoint.DistributionPointName;
            //        Assert.That(name?.Type, Is.EqualTo(DistributionPointName.FullName));

            //        foreach (var gn in GeneralNames.GetInstance(name.Name).GetNames())
            //            Assert.That(gn.TagNo, Is.EqualTo(GeneralName.UniformResourceIdentifier), "CRL DistributionPoints must be an Uniform Resource Identifier!");

            //    }

            //}


            //var aiaExt = ServerCertificate.GetExtensionValue(X509Extensions.AuthorityInfoAccess);
            //if (aiaExt is not null)
            //{

            //    Assert.That(nonCriticalOids.Contains(X509Extensions.AuthorityInfoAccess.Id), Is.True, "AuthorityInfoAccess should be non-critical!");

            //    var aia = AuthorityInformationAccess.GetInstance(X509ExtensionUtilities.FromExtensionValue(aiaExt));
            //    foreach (var accessDescription in aia.GetAccessDescriptions())
            //    {

            //        var oid = accessDescription.AccessMethod;

            //        Assert.That(oid.Equals(AccessDescription.IdADCAIssuers) ||
            //                    oid.Equals(AccessDescription.IdADOcsp),                             Is.True, "AIA must contain only caIssuers/OCSP!");

            //        Assert.That(accessDescription.AccessLocation.TagNo,   Is.EqualTo(GeneralName.UniformResourceIdentifier), "AuthorityInfoAccess locations must be an Uniform Resource Identifier!");

            //    }
            //}




            // --- DER RoundTrip
            var derBytes                = ServerCertificate.GetEncoded();
            var parsedCertificate       = new X509CertificateParser().ReadCertificate(derBytes);
            Assert.That(parsedCertificate,                                   Is.Not.Null);
            Assert.That(parsedCertificate.SubjectDN.ToString(),              Is.EqualTo(ServerCertificate.SubjectDN.ToString()));
            Assert.That(parsedCertificate.SerialNumber,                      Is.EqualTo(ServerCertificate.SerialNumber));
            parsedCertificate.Verify(IssuerCertificate.GetPublicKey());

        }

        #endregion


        #region Generate_ECC_secp521r1_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed ECC secp521r1 server certificate.
        /// </summary>
        [Test]
        public void Generate_ECC_secp521r1_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateECCKeyPair("secp521r1");
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

            var serverKeyPair      = PKIFactory.GenerateECCKeyPair("secp521r1");
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

            // ECDSA-specific certificate checks
            Assert.That(serverCertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ECDSA"),               "Expect ECDSA signature!");
            Assert.That(serverCertificate.SigAlgOid,                       Is.EqualTo("1.2.840.10045.4.3.4"),   "ecdsa-with-SHA512 expected!");

        }

        #endregion

        #region Generate_ECC_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed ECC server certificate.
        /// </summary>
        [Test]
        public void Generate_ECC_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateECCKeyPair();
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

            var serverKeyPair      = PKIFactory.GenerateECCKeyPair();
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

            // ECDSA-specific certificate checks
            Assert.That(serverCertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ECDSA"),               "Expect ECDSA signature!");
            Assert.That(serverCertificate.SigAlgOid,                       Is.EqualTo("1.2.840.10045.4.3.2"),   "ecdsa-with-SHA256 expected!");

        }

        #endregion


        #region Generate_Ed25519_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed Ed25519 server certificate.
        /// </summary>
        [Test]
        public void Generate_Ed25519_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateEd25519KeyPair();
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

            var serverKeyPair      = PKIFactory.GenerateEd25519KeyPair();
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

            // ED25519-specific certificate checks
            Assert.That(serverCertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ED25519"),     "Expect ED25519 signature!");
            Assert.That(serverCertificate.SigAlgOid,                       Is.EqualTo("1.3.101.112"),   "id-Ed25519 expected!");

        }

        #endregion

        #region Generate_Ed448_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed Ed448 server certificate.
        /// </summary>
        [Test]
        public void Generate_Ed448_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateEd448KeyPair();
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

            var serverKeyPair      = PKIFactory.GenerateEd448KeyPair();
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

            // ED448-specific certificate checks
            Assert.That(serverCertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ED448"),       "Expect ED448 signature!");
            Assert.That(serverCertificate.SigAlgOid,                       Is.EqualTo("1.3.101.113"),   "id-Ed448 expected!");

        }

        #endregion


        #region Generate_MLDSA_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed ML-DSA server certificate.
        /// </summary>
        [Test]
        public void Generate_MLDSA_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateMLDSAKeyPair();
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

            var serverKeyPair      = PKIFactory.GenerateMLDSAKeyPair();
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

            // ML-DSA-specific certificate checks
            Assert.That(serverCertificate.SigAlgName.ToUpperInvariant(),   Does.Contain("ML-DSA-65"),               "Expect ML-DSA-65 signature!");
            Assert.That(serverCertificate.SigAlgOid,                       Is.EqualTo("2.16.840.1.101.3.4.3.18"),   "id-ml-dsa-65 expected!");

        }

        #endregion


        #region Generate_RSA_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed RSA server certificate.
        /// </summary>
        [Test]
        public void Generate_RSA_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateRSAKeyPair();
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

            var serverKeyPair      = PKIFactory.GenerateRSAKeyPair();
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

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

        }

        #endregion

        #region Generate_RSA2048_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a directly signed RSA 2048 server certificate.
        /// </summary>
        [Test]
        public void Generate_RSA2048_ServerCertificate_Direct_Test()
        {

            var rootCAKeyPair      = PKIFactory.GenerateRSAKeyPair(2048);
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

            var serverKeyPair      = PKIFactory.GenerateRSAKeyPair(2048);
            var serverCertificate  = PKIFactory.SignServerCertificate(
                                         ServerName:               "server.example.org",
                                         SubjectAltNames:          [
                                                                       new GeneralName(
                                                                           GeneralName.DnsName,
                                                                           "server.example.org"
                                                                       ),
                                                                       new GeneralName(
                                                                           GeneralName.IPAddress,
                                                                           "127.0.0.1"
                                                                       )
                                                                   ],
                                         ServerPublicKey:          serverKeyPair.Public,
                                         IssuerPrivateKey:         rootCAKeyPair.Private,
                                         IssuerCertificate:        rootCACertificate,
                                         LifeTime:                 TimeSpan.FromDays(30),
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


            ValidateServerCertificate(
                rootCACertificate,
                serverCertificate
            );


            var certBytes          = serverCertificate.GetEncoded().ToHexString();
            var sd                 = serverCertificate.ToPEM();
            var dotNetCertificate  = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(sd);

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

        }

        #endregion

    }

}
