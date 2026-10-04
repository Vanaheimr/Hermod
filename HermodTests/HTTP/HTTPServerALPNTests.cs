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

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// ALPN (RFC 7301): what this server answers when a client asks, over TLS,
    /// which application protocol it is about to speak.
    /// </summary>
    /// <remarks>
    /// It answered nothing until 2026-10-04 — the server's
    /// SslServerAuthenticationOptions carried no ApplicationProtocols, so a
    /// client's extension went unacknowledged and curl said "server did not
    /// agree on a protocol. Uses default." Every browser and every
    /// HTTP/2-capable client sends it; H-17 in HTTP1ConformanceTests.
    ///
    /// Only http/1.1 is offered, deliberately: this server does not speak h2,
    /// and ALPN is the one place where saying so is unambiguous rather than
    /// left to a client's guess.
    /// </remarks>
    [TestFixture]
    public class HTTPServerALPNTests
    {

        #region Data

        private X509Certificate2 certificate = null!;

        #endregion

        #region Setup

        [OneTimeSetUp]
        public void Init()
        {

            using var rsa  = RSA.Create(2048);

            var request    = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names      = new SubjectAlternativeNameBuilder();

            names.AddDnsName  ("localhost");
            names.AddIpAddress(System.Net.IPAddress.Loopback);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));
            request.CertificateExtensions.Add(names.Build());

            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),
                                                         DateTimeOffset.UtcNow.AddDays(1));

            // No intermediates, and so no SslStreamCertificateContext installing
            // anything into the machine's CA store: ServerCertificateChainTests
            // learned the hard way what a fixture that does leaves behind.
            certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx),
                                                           null,
                                                           X509KeyStorageFlags.Exportable);

        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            certificate?.Dispose();
        }

        #endregion

        #region (private) Negotiate(Port, Offer)

        /// <summary>
        /// Open a TLS connection offering the given protocols, and report what
        /// the server selected. Null for an offer it did not answer.
        /// </summary>
        private static async Task<String?> Negotiate(IPPort                             Port,
                                                     List<SslApplicationProtocol>?      Offer)
        {

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            using var tls = new SslStream(
                                client.GetStream(),
                                leaveInnerStreamOpen:               false,
                                userCertificateValidationCallback:  (sender, cert, chain, errors) => true
                            );

            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                          TargetHost                      = "localhost",
                          CertificateRevocationCheckMode  = X509RevocationMode.NoCheck,
                          ApplicationProtocols            = Offer
                      });

            var negotiated = tls.NegotiatedApplicationProtocol;

            return negotiated.Protocol.Length == 0
                       ? null
                       : negotiated.ToString();

        }

        #endregion


        #region AnOfferOfHTTP11IsAnswered()

        [Test]
        public async Task AnOfferOfHTTP11IsAnswered()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector: (tcpServer, tcpClient) => certificate
                               );

            try
            {
                Assert.That(
                    await Negotiate(httpServer.TCPPort, [ SslApplicationProtocol.Http11 ]),
                    Is.EqualTo("http/1.1")
                );
            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AClientThatWouldPreferH2GetsHTTP11()

        /// <summary>
        /// The case ALPN exists for: the client lists h2 first, and the server
        /// picks the one it actually speaks. Guessing from silence is what a
        /// client had to do before, and an HTTP/2-capable one guessing wrong
        /// sends a connection preface to a server that has never heard of it.
        /// </summary>
        [Test]
        public async Task AClientThatWouldPreferH2GetsHTTP11()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector: (tcpServer, tcpClient) => certificate
                               );

            try
            {
                Assert.That(
                    await Negotiate(
                        httpServer.TCPPort,
                        [ SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 ]
                    ),
                    Is.EqualTo("http/1.1")
                );
            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AClientThatOffersNoALPNIsStillServed()

        /// <summary>
        /// ALPN must not become a requirement. A client that sends no extension
        /// at all — which is every client that predates it, and openssl without
        /// -alpn — gets a connection and no selected protocol.
        /// </summary>
        [Test]
        public async Task AClientThatOffersNoALPNIsStillServed()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector: (tcpServer, tcpClient) => certificate
                               );

            try
            {
                Assert.That(
                    await Negotiate(httpServer.TCPPort, null),
                    Is.Null
                );
            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AClientThatInsistsOnH2IsRefused()

        /// <summary>
        /// RFC 7301 §3.2: a server finding no protocol it supports in the
        /// client's list "SHOULD respond with a fatal no_application_protocol
        /// alert". Being served HTTP/1.1 anyway is the outcome this prevents —
        /// the client would then speak h2 to a server that cannot.
        /// </summary>
        [Test]
        public async Task AClientThatInsistsOnH2IsRefused()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector: (tcpServer, tcpClient) => certificate
                               );

            try
            {
                Assert.That(
                    async () => await Negotiate(httpServer.TCPPort, [ SslApplicationProtocol.Http2 ]),
                    Throws.InstanceOf<AuthenticationException>()
                );
            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

    }

}
