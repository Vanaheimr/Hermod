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

using dotSec  = System.Net.Security;
using dotX509 = System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Org.BouncyCastle.Asn1.X509;

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

        #region Mutual_TLS_ECC__1_usingLocalCertificateSelector_Test1()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect using a local certificate selector.
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__1_usingLocalCertificateSelector_Test1()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9001),
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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                      var SANs = serverCertificate.DecodeSubjectAlternativeNames();

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

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo($"Hello, 'client #{clientId}'!"));

        }

        #endregion

        #region Mutual_TLS_ECC__2_usingClientCertificate_Test1()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect using a client certificate.
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__2_usingClientCertificate_Test1()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9002),
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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                      var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                      return (chainReport.IsValid,
                                                                              chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                 },
                                   ClientCertificates:           [ clientCertificate2! ]
                               );

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo($"Hello, 'client #{clientId}'!"));

        }

        #endregion

        #region Mutual_TLS_ECC__3_usingClientClientCertificateContext_Test1()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect using the client certificate context.
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__3_usingClientClientCertificateContext_Test1()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9003),
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

                                                                    if (clientCertificateChain is null)
                                                                         return (false, [ "The client certificate chain must not be null!" ]);

                                                                    if (clientCertificateChain.ChainElements.Count != 2)
                                                                         return (false, [ "The client certificate chain must contain exactly 2 elements!" ]);

                                                                    var c1  = clientCertificateChain.ChainElements[0].Certificate;
                                                                    var cn1 = c1.SubjectName.ToMap().CommonName;

                                                                    var c2  = clientCertificateChain.ChainElements[1].Certificate;
                                                                    var cn2 = c2.SubjectName.ToMap().CommonName;

                                                                    var chainReport = PKIFactory.ValidateClientChain(
                                                                                          c1,
                                                                                          c2,
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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                      var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                      return (chainReport.IsValid,
                                                                              chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                 },
                                   ClientCertificates:           [ clientCertificate2! ],
                                   ClientCertificateContext:     dotSec.SslStreamCertificateContext.Create(
                                                                     clientCertificate2!,
                                                                     new dotX509.X509Certificate2Collection(clientCACertificate2!)
                                                                 )
                               );

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo($"Hello, 'client #{clientId}'!"));

        }

        #endregion

        #region Mutual_TLS_ECC__4_usingClientCertificateChain_Test1()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect using a client certificate chain.
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__4_usingClientCertificateChain_Test1()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9004),
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

                                                                    if (clientCertificateChain is null)
                                                                         return (false, [ "The client certificate chain must not be null!" ]);

                                                                    if (clientCertificateChain.ChainElements.Count != 2)
                                                                         return (false, [ "The client certificate chain must contain exactly 2 elements!" ]);

                                                                    var c1  = clientCertificateChain.ChainElements[0].Certificate;
                                                                    var cn1 = c1.SubjectName.ToMap().CommonName;

                                                                    var c2  = clientCertificateChain.ChainElements[1].Certificate;
                                                                    var cn2 = c2.SubjectName.ToMap().CommonName;

                                                                    var chainReport = PKIFactory.ValidateClientChain(
                                                                                          c1,
                                                                                          c2,
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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                      var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                      return (chainReport.IsValid,
                                                                              chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                 },
                                   ClientCertificateChain:       [
                                                                     clientCertificate2!,
                                                                     clientCACertificate2!
                                                                 ]
                               );

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo($"Hello, 'client #{clientId}'!"));

        }

        #endregion


        #region Mutual_TLS_ECC__5_WithoutAnyClientCert_WillFail_Test()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect without any client certificate.
        /// The HTTP server does not accept connections without a valid client certificate!
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__5_WithoutAnyClientCert_WillFail_Test()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9005),
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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                     var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                     return (chainReport.IsValid,
                                                                             chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                  },
                                   MaxNumberOfRetries:            1
                               );

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo("Maximum HTTP retries reached!"));

        }

        #endregion

        #region Mutual_TLS_ECC__6_WithAndWithoutAnyClientCert_Test()

        /// <summary>
        /// Create a ECC mutual TLS PKI and connect with and without any client certificate.
        /// The HTTP server accepts requests without client certificates!
        /// </summary>
        [Test]
        public async Task Mutual_TLS_ECC__6_WithAndWithoutAnyClientCert_Test()
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

            var clientId             = RandomExtensions.RandomNumberString(4);
            var clientKeyPair        = PKIFactory.GenerateECCKeyPair();
            var clientCertificate    = PKIFactory.SignClientCertificate(
                                           ClientName:               $"client #{clientId}, O=GraphDefined GmbH, OU=GraphDefined PKI Services",
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

            #region Validate .NET certificate conversion

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
            Assert.That(cc3.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(cc3.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(dd2.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(dd2.Elements.First().Status.First().Status,   Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
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
            Assert.That(ee1.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee1.Status.  First().Status,                  Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee1.Elements.Length,                          Is.EqualTo(3));

            var ee2 = PKIFactory.ValidateServerChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee2.IsValid,                                  Is.False);
            Assert.That(ee2.Status.  Length,                          Is.EqualTo(2));
            Assert.That(ee2.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee2.Status[1].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotValidForUsage));
            Assert.That(ee2.Elements.Length,                          Is.EqualTo(3));

            var ee3 = PKIFactory.ValidateClientChain(
                          clientCertificate2!,
                          serverCACertificate2!,
                          rootCACertificate2!
                      );

            Assert.That(ee3.IsValid,                                  Is.False);
            Assert.That(ee3.Status.  Length,                          Is.EqualTo(1));
            Assert.That(ee3.Status[0].Status,                         Is.EqualTo(dotX509.X509ChainStatusFlags.NotSignatureValid));
            Assert.That(ee3.Elements.Length,                          Is.EqualTo(3));

            #endregion

            #endregion

            #region Setup HTTP Server

            var httpServer = new HTTPTestServerX(
                                 TCPPort:                      IPPort.Parse(9006),
                                 ServerCertificateSelector:    (tcpServer, tcpClient) => {
                                                                   return serverCertificate2!;
                                                               },
                                 ClientCertificateValidator:   (sender,
                                                                clientCertificate,
                                                                clientCertificateChain,
                                                                tlsServer,
                                                                policyErrors) => {

                                                                    if (clientCertificate is null)
                                                                        return (true, [ "The client certificate is null, anyway we proceed... :)" ]);

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
                                   Content         = $"Hello, '{subject?.CommonName ?? "anonymous"}'!".ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable);

                }
            );

            await httpServer.Start();

            #endregion


            var httpClient1  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                     var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                     return (chainReport.IsValid,
                                                                             chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                 }
                               );

            var response1    = await httpClient1.GET(HTTPPath.Root);
            var data1        = response1.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data1,  Is.EqualTo("Hello, 'anonymous'!"));



            var httpClient2  = new HTTPTestClient(
                                   URL:                           URL.Parse($"https://localhost:{httpServer.TCPPort}"),
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

                                                                     var SANs = serverCertificate.DecodeSubjectAlternativeNames();

                                                                     return (chainReport.IsValid,
                                                                             chainReport.Status.Select(chainStatus => chainStatus.Status.ToString()));

                                                                 },
                                   ClientCertificates:           [ clientCertificate2! ]
                               );

            var response2    = await httpClient2.GET(HTTPPath.Root);
            var data2        = response2.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.That(data2,  Is.EqualTo($"Hello, 'client #{clientId}'!"));

        }

        #endregion


    }

}
