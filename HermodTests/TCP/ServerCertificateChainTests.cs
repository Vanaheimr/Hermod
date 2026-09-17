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

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// What a TLS server actually sends a client: its certificate alone, or the
    /// intermediates along with it.
    /// </summary>
    /// <remarks>
    /// A client is expected to know roots, not intermediates, so TLS has the
    /// server send everything in between (RFC 8446, section 4.4.2). A server
    /// that sends only its own certificate works against browsers - they cache
    /// intermediates and fetch what they lack - and fails against most
    /// everything else, which is the kind of failure that shows up in somebody
    /// else's log weeks later.
    ///
    /// The certificates here are made on the spot: a root, an intermediate
    /// signed by it, and a server certificate signed by that.
    ///
    /// <b>What these tests establish is the Hermod side of it:</b> that the
    /// chain selector is asked for every connection, that it takes precedence
    /// over the narrower one, and that a handshake still completes with it.
    /// What the platform then puts on the wire is deliberately not asserted,
    /// because it cannot be observed from here: on Windows the chain a client's
    /// validation callback receives is built by SChannel, so
    /// ChainPolicy.ExtraStore stays empty and ChainElements shows whatever the
    /// machine itself can assemble - and a machine that has just minted an
    /// intermediate assembles it whether or not the server sent it. Only an
    /// outside observer such as "openssl s_client -showcerts" answers that.
    /// </remarks>
    [TestFixture]
    public class ServerCertificateChainTests
    {

        #region Data

        private X509Certificate2  root          = null!;
        private X509Certificate2  intermediate  = null!;
        private X509Certificate2  server        = null!;

        #endregion

        #region (private static) UniqueName(Role)

        /// <summary>
        /// A subject name no certificate has ever used before.
        /// </summary>
        /// <remarks>
        /// The same lesson as <c>Mutual_TLS_Tests.UniqueCAName</c>, and these
        /// tests had to learn it again. Windows resolves an issuer by name, and
        /// <see cref="System.Net.Security.SslStreamCertificateContext"/> installs
        /// the intermediates it is given into the current user's CA store so that
        /// SChannel can send them. A fixed name therefore leaves one more
        /// "Hermod Test Intermediate" behind on every run, each with a different
        /// key, until the chain engine can no longer tell which one signed this
        /// leaf: CertGetCertificateChain stops answering PartialChain and fails
        /// outright, and every test here that builds a context dies with "An
        /// unknown chain building error occurred" - on a machine where they
        /// passed the day before, and for a reason that is not in this file.
        ///
        /// So the tests poisoned the machine by doing the very thing they test,
        /// a hundred runs before anybody noticed.
        /// </remarks>
        private static String UniqueName(String Role)

            => $"Hermod Test {Role} {Guid.NewGuid().ToString("N")[..8]}";

        #endregion

        #region Setup / TearDown

        [SetUp]
        public void CreateCertificates()
        {

            var now = DateTimeOffset.UtcNow;

            #region A root, signing itself

            using var rootKey = RSA.Create(2048);

            var rootRequest = new CertificateRequest($"CN={UniqueName("Root")}",
                                                     rootKey,
                                                     HashAlgorithmName.SHA256,
                                                     RSASignaturePadding.Pkcs1);

            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));

            root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));

            #endregion

            #region An intermediate, signed by the root

            using var intermediateKey = RSA.Create(2048);

            var intermediateRequest = new CertificateRequest($"CN={UniqueName("Intermediate")}",
                                                             intermediateKey,
                                                             HashAlgorithmName.SHA256,
                                                             RSASignaturePadding.Pkcs1);

            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            intermediateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(intermediateRequest.PublicKey, false));

            intermediate = intermediateRequest.Create(root,
                                                      now.AddDays(-1),
                                                      now.AddYears(3),
                                                      Serial());

            using var intermediateSigner = intermediate.CopyWithPrivateKey(intermediateKey);

            #endregion

            #region The server certificate, signed by the intermediate

            using var serverKey = RSA.Create(2048);

            var serverRequest = new CertificateRequest("CN=localhost",
                                                       serverKey,
                                                       HashAlgorithmName.SHA256,
                                                       RSASignaturePadding.Pkcs1);

            serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));

            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(System.Net.IPAddress.Loopback);
            serverRequest.CertificateExtensions.Add(names.Build());

            using var leaf       = serverRequest.Create(intermediateSigner, now.AddDays(-1), now.AddYears(1), Serial());
            using var leafWithKey = leaf.CopyWithPrivateKey(serverKey);

            // Through PKCS#12, or the key stays ephemeral and SslStream on
            // Windows refuses it.
            server = X509CertificateLoader.LoadPkcs12(leafWithKey.Export(X509ContentType.Pfx), null);

            #endregion

        }

        [TearDown]
        public void DisposeCertificates()
        {
            root?.        Dispose();
            intermediate?.Dispose();
            server?.      Dispose();
        }


        private static Byte[] Serial()
            => RandomNumberGenerator.GetBytes(16);

        #endregion

        #region (private) HandshakeAsync(Port)

        /// <summary>
        /// Opens a TLS connection and reports the subject of the certificate
        /// the server presented.
        /// </summary>
        private static async Task<String?> HandshakeAsync(IPPort Port)
        {

            String? subject = null;

            using var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            using var tls = new SslStream(
                                client.GetStream(),
                                leaveInnerStreamOpen:               false,
                                userCertificateValidationCallback:  (sender, certificate, chain, errors) => {
                                                                        subject = certificate?.Subject;
                                                                        // The root of this PKI is trusted nowhere;
                                                                        // whether it validates is not the question.
                                                                        return true;
                                                                    }
                            );

            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                          TargetHost                      = "localhost",
                          CertificateRevocationCheckMode  = X509RevocationMode.NoCheck
                      });

            return subject;

        }

        #endregion


        #region TheChainSelector_IsAskedForEveryConnection()

        [Test]
        public async Task TheChainSelector_IsAskedForEveryConnection()
        {

            var asked      = 0;
            var httpServer = await HTTPServer.StartNew();

            httpServer.ServerCertificateChainSelector = (tcpServer, tcpClient) => {
                                                            Interlocked.Increment(ref asked);
                                                            return new ServerCertificateChain(server, [ intermediate ]);
                                                        };

            try
            {

                await HandshakeAsync(httpServer.TCPPort);
                var first = asked;

                await HandshakeAsync(httpServer.TCPPort);

                Assert.Multiple(() =>
                {
                    Assert.That(first, Is.GreaterThan(0),     "asked while the connection was being accepted");
                    Assert.That(asked, Is.GreaterThan(first), "and asked again for the next one, which is what lets a renewal take over");
                });

            }
            finally
            {
                await httpServer.Stop();
            }

        }

        #endregion

        #region TheChainSelector_WinsOverTheNarrowOne()

        /// <summary>
        /// Both may be set - the narrow one is what older code passes to the
        /// constructor - and then the wider one decides, so that one connection
        /// cannot end up with the certificate of one and the chain of the
        /// other.
        /// </summary>
        [Test]
        public async Task TheChainSelector_WinsOverTheNarrowOne()
        {

            var narrowAsked = 0;

            var httpServer  = await HTTPServer.StartNew(
                                  ServerCertificateSelector: (tcpServer, tcpClient) => {
                                                                 Interlocked.Increment(ref narrowAsked);
                                                                 return server;
                                                             }
                              );

            httpServer.ServerCertificateChainSelector = (tcpServer, tcpClient) => new ServerCertificateChain(server, [ intermediate ]);

            try
            {

                var certificate = await HandshakeAsync(httpServer.TCPPort);

                Assert.Multiple(() =>
                {
                    Assert.That(certificate,  Is.EqualTo("CN=localhost"));
                    Assert.That(narrowAsked,  Is.EqualTo(0), "the narrow selector is not even asked");
                });

            }
            finally
            {
                await httpServer.Stop();
            }

        }

        #endregion

        #region AChainWithIntermediates_StillCompletesAHandshake()

        /// <summary>
        /// The intermediates must not cost the handshake, whatever the platform
        /// makes of them.
        /// </summary>
        [Test]
        public async Task AChainWithIntermediates_StillCompletesAHandshake()
        {

            var httpServer = await HTTPServer.StartNew();

            httpServer.ServerCertificateChainSelector = (tcpServer, tcpClient) => new ServerCertificateChain(server, [ intermediate ]);

            try
            {
                Assert.That(await HandshakeAsync(httpServer.TCPPort), Is.EqualTo("CN=localhost"));
            }
            finally
            {
                await httpServer.Stop();
            }

        }

        #endregion


        #region AChain_DropsWhatWouldOnlyBeBytesOnTheWire()

        [Test]
        public void AChain_DropsWhatWouldOnlyBeBytesOnTheWire()
        {

            var withRoot   = new ServerCertificateChain(server, [ intermediate, root ]);
            var withItself = new ServerCertificateChain(server, [ server, intermediate ]);
            var alone      = new ServerCertificateChain(server);

            Assert.Multiple(() =>
            {

                Assert.That(withRoot.Intermediates,          Has.Count.EqualTo(1), "a root is either trusted already or sending it changes nothing");
                Assert.That(withRoot.HasIntermediates,       Is.True);

                Assert.That(withItself.Intermediates,        Has.Count.EqualTo(1), "the certificate is sent anyway; twice is not better");

                Assert.That(alone.Intermediates,             Is.Empty);
                Assert.That(alone.HasIntermediates,          Is.False);
                Assert.That(alone.CacheKey,                  Is.EqualTo(server.Thumbprint));
                Assert.That(withRoot.CacheKey,               Is.Not.EqualTo(alone.CacheKey), "or a context built without them would be reused with them");

            });

        }

        #endregion

    }

}
