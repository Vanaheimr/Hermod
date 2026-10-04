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
    /// The TLS versions a server accepts, which it was asked for and ignored.
    /// </summary>
    /// <remarks>
    /// H-32 in HTTP1ConformanceTests, found while giving the server an ALPN
    /// answer: both settings live in the same
    /// SslServerAuthenticationOptions, and only one of them was being filled
    /// in. HTTPServer took an AllowedTLSProtocols, kept it, handed it to
    /// ATCPServer — and the TCPConnection that builds the handshake read a
    /// property of its own that nothing ever assigned. It was null on every
    /// connection, so the handshake fell back to "TLS 1.2 and 1.3" however the
    /// server had been configured, and a deployment that had switched 1.2 off
    /// still spoke it. The doc comments said so themselves: "kept in
    /// AllowedTLSProtocols, but not read yet".
    ///
    /// Fixed in "A TLS listener allows the TLS protocols its server was told
    /// to allow", whose own commit message says "built, not tested here" and
    /// names a downstream OCPI test as the evidence. These are that fix's
    /// tests, in the suite that owns the behaviour: a conformance finding
    /// closed by a downstream test is closed on somebody else's schedule.
    /// </remarks>
    [TestFixture]
    public class HTTPServerAllowedTLSProtocolsTests
    {

        #region Data

        private X509Certificate2 certificate = null!;

        #endregion

        #region Setup / Teardown

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

        #region (private) Handshake(Port, Protocols)

        /// <summary>
        /// Open a TLS connection offering exactly the given protocol versions,
        /// and report the one that was negotiated.
        /// </summary>
        private static async Task<SslProtocols> Handshake(IPPort        Port,
                                                          SslProtocols  Protocols)
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
                          EnabledSslProtocols             = Protocols
                      });

            return tls.SslProtocol;

        }

        #endregion


        #region ByDefaultBothVersionsAreAccepted()

        /// <summary>
        /// The default is unchanged, and that matters more than the rest of
        /// this fixture: a null setting still means "TLS 1.2 and 1.3", so
        /// nothing that did not configure this moves.
        /// </summary>
        [Test]
        public async Task ByDefaultBothVersionsAreAccepted()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector: (tcpServer, tcpClient) => certificate
                               );

            try
            {
                Assert.Multiple(async () => {
                    Assert.That(await Handshake(httpServer.TCPPort, SslProtocols.Tls12), Is.EqualTo(SslProtocols.Tls12));
                    Assert.That(await Handshake(httpServer.TCPPort, SslProtocols.Tls13), Is.EqualTo(SslProtocols.Tls13));
                });
            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AServerAskedForTLS13AloneRefusesTLS12()

        [Test]
        public async Task AServerAskedForTLS13AloneRefusesTLS12()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector:  (tcpServer, tcpClient) => certificate,
                                   AllowedTLSProtocols:        SslProtocols.Tls13
                               );

            try
            {

                // First, so that the refusal below is a refusal of 1.2 and not
                // of everything: a server nobody can reach would pass the
                // second check for the wrong reason.
                Assert.That(await Handshake(httpServer.TCPPort, SslProtocols.Tls13), Is.EqualTo(SslProtocols.Tls13));

                Assert.That(
                    async () => await Handshake(httpServer.TCPPort, SslProtocols.Tls12),
                    Throws.InstanceOf<AuthenticationException>()
                );

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AServerAskedForTLS12AloneRefusesTLS13()

        /// <summary>
        /// The other direction, because a setting that is read must be read as
        /// given rather than as a minimum.
        /// </summary>
        [Test]
        public async Task AServerAskedForTLS12AloneRefusesTLS13()
        {

            var httpServer = await HTTPServer.StartNew(
                                   ServerCertificateSelector:  (tcpServer, tcpClient) => certificate,
                                   AllowedTLSProtocols:        SslProtocols.Tls12
                               );

            try
            {

                Assert.That(await Handshake(httpServer.TCPPort, SslProtocols.Tls12), Is.EqualTo(SslProtocols.Tls12));

                Assert.That(
                    async () => await Handshake(httpServer.TCPPort, SslProtocols.Tls13),
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
