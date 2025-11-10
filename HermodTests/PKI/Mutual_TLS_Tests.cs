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

using dotCrypto = System.Security.Cryptography;
using dotSec    = System.Security.Cryptography.X509Certificates;

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
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.PKI
{

    /// <summary>
    /// Generate a rootCA, a serverCA, a clientCA and server/client certificates for mutual TLS PKI tests.
    /// </summary>
    [TestFixture]
    public class Mutual_TLS_Tests
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


        #region Generate_ECC_ServerCertificate_Direct_Test()

        /// <summary>
        /// Create a ECC mutual TLS PKI.
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC_Test()
        {

            #region Generate rootCA

            var rootCAKeyPair        = PKIFactory.GenerateECCKeyPair();
            var rootCACertificate    = PKIFactory.CreateRootCACertificate(
                                           SubjectName:             "Hermod RootCA",
                                           RootKeyPair:              rootCAKeyPair,
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

            #endregion

            #region Generate serverCA/server certificate

            var serverCAKeyPair      = PKIFactory.GenerateECCKeyPair();
            var serverCACertificate  = PKIFactory.CreateIntermediateCA(
                                           SubjectName:             "Hermod ServerCA",
                                           IntermediatePublicKey:    serverCAKeyPair.Public,
                                           IssuerPrivateKey:         rootCAKeyPair.Private,
                                           IssuerCertificate:        rootCACertificate,
                                           CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/server.crl") ],
                                           AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                           AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/server.cer") ],
                                           CertificatePolicies:      [
                                                                         new CertificatePolicy(
                                                                             "1.3.6.1.4.1.99999.2.10",
                                                                             URL.Parse("https://pki.example.com/cps.html")
                                                                         )
                                                                     ]
                                       );

            var serverKeyPair        = PKIFactory.GenerateECCKeyPair();
            var serverCertificate    = PKIFactory.SignServerCertificate(
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
                                           IssuerPrivateKey:         serverCAKeyPair.Private,
                                           IssuerCertificate:        serverCACertificate,
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

            #endregion

            #region Generate clientCA/client certificate

            var clientCAKeyPair      = PKIFactory.GenerateECCKeyPair();
            var clientCACertificate  = PKIFactory.CreateIntermediateCA(
                                           SubjectName:             "Hermod ClientCA",
                                           IntermediatePublicKey:    clientCAKeyPair.Public,
                                           IssuerPrivateKey:         rootCAKeyPair.Private,
                                           IssuerCertificate:        rootCACertificate,
                                           CRL_DistributionPoints:   [ URL.Parse("http://pki.example.com/client.crl") ],
                                           AIA_OCSPURLs:             [ URL.Parse("http://ocsp.example.com") ],
                                           AIA_CAIssuersURLs:        [ URL.Parse("http://pki.example.com/client.cer") ],
                                           CertificatePolicies:      [
                                                                         new CertificatePolicy(
                                                                             "1.3.6.1.4.1.99999.2.10",
                                                                             URL.Parse("https://pki.example.com/cps.html")
                                                                         )
                                                                     ]
                                       );

            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               "client #1, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
                                           ClientPublicKey:          clientKeyPair.Public,
                                           IssuerPrivateKey:         clientCAKeyPair.Private,
                                           IssuerCertificate:        clientCACertificate,
                                           LifeTime:                 TimeSpan.FromDays(7),
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

            #endregion


            var rootCACertificate2    = rootCACertificate.  ToDotNet2();

            var serverCACertificate2  = serverCACertificate.ToDotNet2(CACertificates: [ rootCACertificate ]);
            var clientCACertificate2  = clientCACertificate.ToDotNet2(CACertificates: [ rootCACertificate ]);

            var serverCertificate2    = serverCertificate.  ToDotNet2(serverKeyPair.Private, [ serverCACertificate, rootCACertificate ]);
            var clientCertificate2    = clientCertificate.  ToDotNet2(clientKeyPair.Private, [ clientCACertificate, rootCACertificate ]);

            Assert.That(rootCACertificate2,     Is.Not.Null);
            Assert.That(serverCACertificate2,   Is.Not.Null);
            Assert.That(clientCACertificate2,   Is.Not.Null);
            Assert.That(serverCertificate2,     Is.Not.Null);
            Assert.That(clientCertificate2,     Is.Not.Null);

            #region Validate (server, serverCA, rootCA)

            var cc1 = PKIFactory.ValidateChain(
                          serverCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(cc1.IsValid,                                  Is.True);

            var cc2 = PKIFactory.ValidateServerChain(
                          serverCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(cc2.IsValid,                                  Is.True);

            var cc3 = PKIFactory.ValidateClientChain(
                          serverCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(cc3.IsValid,                                  Is.False);
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotSec.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotSec.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #region Validate (client, clientCA, rootCA)

            var dd1 = PKIFactory.ValidateChain(
                          clientCertificate2!,
                          clientCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(dd1.IsValid,                                  Is.True);

            var dd2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          clientCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(dd2.IsValid,                                  Is.False);
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotSec.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotSec.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.Length,                          Is.EqualTo(3));

            var dd3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          clientCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(dd3.IsValid,                                  Is.True);

            #endregion

            #region Validate (client, serverCA, rootCA) => Must fail!

            var ee1 = PKIFactory.ValidateChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee1.IsValid,                                  Is.False);
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotSec.X509ChainStatusFlags.PartialChain));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(1));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  First().Status,                  Is.EqualTo(dotSec.X509ChainStatusFlags.PartialChain));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(1));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  First().Status,                  Is.EqualTo(dotSec.X509ChainStatusFlags.PartialChain));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(1));

            #endregion


            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9999),
                                 ServerCertificateSelector:    (tcpServer, tcpClient) => {
                                                                   return serverCertificate2!;
                                                               },
                                 ClientCertificateValidator:   (sender,
                                                                clientCertificate,
                                                                clientCertificateChain,
                                                                tlsServer,
                                                                policyErrors) => {

                                                                    if (clientCertificate is null)
                                                                         return (false, [ "The client certificate must not be null!" ]);

                                                                    var chainReport = PKIFactory.ValidateClientChain(
                                                                                          clientCertificate,
                                                                                          clientCACertificate2!,
                                                                                          rootCACertificate2!
                                                                                      );

                                                                    return (chainReport.IsValid,
                                                                            chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                               },
                                 LocalCertificateSelector:     (sender,
                                                                targetHost,
                                                                localCertificates,
                                                                remoteCertificate,
                                                                acceptableIssuers) => {
                                                                    return serverCertificate2!;
                                                               }
                             );

            var httpAPI = httpServer.AddHTTPAPI();

            httpAPI.AddHandler(
                HTTPMethod.GET,
                HTTPPath.Root,
                request => {

                    var subject     = request.ClientCertificate?.SubjectName.ToMap();
                    var issuer      = request.ClientCertificate?.IssuerName. ToMap();
                    var notBefore   = request.ClientCertificate?.NotBefore;
                    var notAfter    = request.ClientCertificate?.NotAfter;
                    var extensions  = request.ClientCertificate?.Extensions;

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                   ContentType     = HTTPContentType.Text.PLAIN,
                                   Content         = $"Hello, '{subject?.CommonName}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();


            var httpClient  = new HTTPTestClient(
                                  URL:                           URL.Parse("https://localhost:9999"),
                                  RemoteCertificateValidator:   (sender,
                                                                 serverCertificate,
                                                                 serverCertificateChain,
                                                                 httpClient,
                                                                 policyErrors) => {

                                                                    if (serverCertificate is null)
                                                                        return (false, [ "The server certificate must not be null!" ]);

                                                                    var chainReport = PKIFactory.ValidateServerChain(
                                                                                          serverCertificate,
                                                                                          serverCACertificate2!,
                                                                                          rootCACertificate2!
                                                                                      );

                                                                    return (chainReport.IsValid,
                                                                            chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                },
                                  LocalCertificateSelector:    (sender,
                                                                targetHost,
                                                                localCertificates,
                                                                remoteCertificate,
                                                                acceptableIssuers) => {
                                                                    return clientCertificate2!;
                                                                }
                              );

            var response    = await httpClient.GET(HTTPPath.Root);
            var data        = response.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data,  Is.EqualTo("Hello, 'client #1'!"));


        }

        #endregion


    }

}
