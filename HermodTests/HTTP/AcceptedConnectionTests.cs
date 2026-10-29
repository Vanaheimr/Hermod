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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The HTTPConnection a handler reaches through request.TCPConnection is the
    /// connection the TCP server accepted, and not a second one built from it.
    /// </summary>
    /// <remarks>
    /// Until 2026-10-29 AHTTPServer wrapped every accepted TCPConnection in an
    /// HTTPConnection whose constructor was the full TCPConnection constructor.
    /// It asked the certificate selectors and the ConnectionIdBuilder again, and
    /// it never learned the client certificate, because the handshake had run on
    /// the TCPConnection before. Found writing H-36's tests, where three
    /// connections made six selector calls. H-39 in HTTP1ConformanceTests.
    /// </remarks>
    [TestFixture]
    public class AcceptedConnectionTests
    {

        #region Data

        private X509Certificate2 first   = null!;
        private X509Certificate2 second  = null!;
        private X509Certificate2 client  = null!;

        #endregion

        #region Setup

        [OneTimeSetUp]
        public void Init()
        {
            first   = CreateCertificate("first",  "1.3.6.1.5.5.7.3.1");
            second  = CreateCertificate("second", "1.3.6.1.5.5.7.3.1");
            client  = CreateCertificate("client", "1.3.6.1.5.5.7.3.2");
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            first?. Dispose();
            second?.Dispose();
            client?.Dispose();
        }

        #endregion

        #region (private static) CreateCertificate(CommonName, Usage)

        private static X509Certificate2 CreateCertificate(String CommonName,
                                                          String Usage)
        {

            using var rsa  = RSA.Create(2048);

            var request    = new CertificateRequest($"CN={CommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names      = new SubjectAlternativeNameBuilder();

            names.AddDnsName("localhost");
            names.AddIpAddress(System.Net.IPAddress.Loopback);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid(Usage) ], false));
            request.CertificateExtensions.Add(names.Build());

            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),
                                                         DateTimeOffset.UtcNow.AddDays(1));

            // Nothing installed into the machine's stores, as in MisdirectedRequestTests.
            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx),
                                                    null,
                                                    X509KeyStorageFlags.Exportable);

        }

        #endregion

        #region (private static) AddReporter(HTTPServer, Report)

        /// <summary>
        /// GET / answers with what the handler sees of its connection.
        /// </summary>
        private static void AddReporter(HTTPServer                Server,
                                        Func<HTTPRequest, String>  Report)
        {

            Server.AddHTTPAPI().AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                 Content         = Report(request).ToUTF8Bytes(),
                                                 Connection      = ConnectionType.Close
                                             }.AsImmutable
                                         )
            );

        }

        #endregion

        #region (private static) Exchange(Port, TLS = true, ClientCertificate = null)

        /// <summary>
        /// One GET / on a connection of its own. Returns the content of the
        /// response, and the certificate the server presented in the handshake.
        /// </summary>
        private static async Task<(String Content, String? HandshakeThumbprint)> Exchange(IPPort             Port,
                                                                                          Boolean            TLS                 = true,
                                                                                          X509Certificate2?  ClientCertificate   = null)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            Stream  stream               = tcpClient.GetStream();
            String? handshakeThumbprint  = null;

            if (TLS)
            {

                var tls = new SslStream(
                              stream,
                              leaveInnerStreamOpen:               false,
                              userCertificateValidationCallback:  (sender, cert, chain, errors) => true
                          );

                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                              TargetHost                      = "localhost",
                              CertificateRevocationCheckMode  = X509RevocationMode.NoCheck,
                              ClientCertificates              = ClientCertificate is not null ? [ ClientCertificate ] : null
                          });

                handshakeThumbprint = tls.RemoteCertificate is not null
                                          ? new X509Certificate2(tls.RemoteCertificate).Thumbprint
                                          : null;

                stream = tls;

            }

            await using (stream)
            {

                await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));
                await stream.FlushAsync();

                var received = new MemoryStream();
                var buffer   = new Byte[4096];

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                try
                {
                    while (true)
                    {

                        var read = await stream.ReadAsync(buffer, timeout.Token);

                        if (read == 0)
                            break;

                        received.Write(buffer, 0, read);

                    }
                }
                catch (OperationCanceledException)
                { }
                catch (IOException)
                { }

                var text = Encoding.ASCII.GetString(received.ToArray());
                var end  = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                Assert.That(end, Is.GreaterThan(0), $"no complete response in: {text}");

                return (text[(end + 4)..], handshakeThumbprint);

            }

        }

        #endregion


        #region TheSelectorIsAskedOncePerConnection()

        [Test]
        public async Task TheSelectorIsAskedOncePerConnection()
        {

            var calls = 0;

            await using var httpServer = await HTTPServer.StartNew(
                                                   ServerCertificateSelector: (tcpServer, tcpClient) => {
                                                       Interlocked.Increment(ref calls);
                                                       return first;
                                                   }
                                               );

            AddReporter(httpServer, request => "served");

            for (var i = 0; i < 3; i++)
                Assert.That((await Exchange(httpServer.TCPPort)).Content, Is.EqualTo("served"));

            Assert.That(calls, Is.EqualTo(3));

        }

        #endregion

        #region TheChainSelectorIsAskedOncePerConnection()

        [Test]
        public async Task TheChainSelectorIsAskedOncePerConnection()
        {

            var calls = 0;

            await using var httpServer = await HTTPServer.StartNew(
                                                   ServerCertificateSelector: (tcpServer, tcpClient) => first
                                               );

            httpServer.ServerCertificateChainSelector = (tcpServer, tcpClient) => {
                                                            Interlocked.Increment(ref calls);
                                                            return new ServerCertificateChain(first);
                                                        };

            AddReporter(httpServer, request => "served");

            for (var i = 0; i < 3; i++)
                Assert.That((await Exchange(httpServer.TCPPort)).Content, Is.EqualTo("served"));

            Assert.That(calls, Is.EqualTo(3));

        }

        #endregion

        #region AHandlerSeesTheCertificateTheHandshakePresented()

        /// <summary>
        /// A selector that answers differently each time it is asked, as one does
        /// in the middle of a certificate rotation. Whatever it said for the
        /// handshake is what the connection presented, and what a handler must see.
        /// </summary>
        [Test]
        public async Task AHandlerSeesTheCertificateTheHandshakePresented()
        {

            var calls = 0;

            await using var httpServer = await HTTPServer.StartNew(
                                                   ServerCertificateSelector: (tcpServer, tcpClient) =>
                                                       Interlocked.Increment(ref calls) % 2 == 1 ? first : second
                                               );

            AddReporter(httpServer, request => request.TCPConnection?.ServerCertificate?.Thumbprint ?? "none");

            for (var i = 0; i < 3; i++)
            {
                var (content, handshake) = await Exchange(httpServer.TCPPort);
                Assert.That(content, Is.EqualTo(handshake), $"connection {i + 1}");
            }

        }

        #endregion

        #region AHandlerSeesTheClientCertificate()

        /// <summary>
        /// The handshake runs on the TCPConnection the server accepted, and the
        /// client certificate is known only after it. request.ClientCertificate
        /// always had it; the connection a handler reaches had none.
        /// </summary>
        [Test]
        public async Task AHandlerSeesTheClientCertificate()
        {

            await using var httpServer = await HTTPServer.StartNew(
                                                   ServerCertificateSelector:   (tcpServer, tcpClient) => first,
                                                   ClientCertificateValidator:  (sender, certificate, chain, server, errors) => TLSValidationResult.Success(),
                                                   ClientCertificateRequired:   true
                                               );

            AddReporter(httpServer, request => $"{request.TCPConnection?.ClientCertificate?.Thumbprint ?? "none"} {request.ClientCertificate?.Thumbprint ?? "none"}");

            var (content, _) = await Exchange(httpServer.TCPPort, ClientCertificate: client);

            Assert.That(content, Is.EqualTo($"{client.Thumbprint} {client.Thumbprint}"));

        }

        #endregion

        #region TheConnectionIdIsBuiltOnce()

        /// <summary>
        /// A ConnectionIdBuilder may use the timestamp it is given, or count, as
        /// this one does. Asked twice, it named the connection a handler sees
        /// differently from the one the TCP server accepted and tracks.
        /// </summary>
        [Test]
        public async Task TheConnectionIdIsBuiltOnce()
        {

            var built = 0;

            await using var httpServer = await HTTPServer.StartNew(
                                                   ConnectionIdBuilder: (sender, timestamp, localSocket, remoteSocket) =>
                                                       $"connection {Interlocked.Increment(ref built)}"
                                               );

            AddReporter(httpServer, request => request.TCPConnection?.ConnectionId ?? "none");

            var (content, _) = await Exchange(httpServer.TCPPort, TLS: false);

            Assert.Multiple(() => {
                Assert.That(content, Is.EqualTo("connection 1"));
                Assert.That(built,   Is.EqualTo(1));
            });

        }

        #endregion

    }

}
