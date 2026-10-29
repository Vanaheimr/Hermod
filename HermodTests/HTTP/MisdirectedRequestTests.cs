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

using System.Collections.Concurrent;
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
    /// RFC 9110, Section 7.4: "a request for an "https" resource MUST be rejected
    /// unless it has been received over a connection that has been secured via a
    /// certificate valid for that target URI's origin", and the rejection is 421
    /// (Section 15.5.20).
    /// </summary>
    /// <remarks>
    /// Until 2026-10-29 the HTTP/1.x server answered every origin a request named:
    /// HTTP1ConformanceTests' demo, whose certificate is for localhost, served
    /// GET https://evil.example/ with a 200. HTTP2Server had refused it all
    /// along, with HTTPAuthority in HTTP2/Core where this server could not reach
    /// it. H-36 in HTTP1ConformanceTests.
    ///
    /// Every route here answers any Host, as an HTTPAPI's routes do, so a 200
    /// for evil.example is what the server says when nothing checks.
    /// </remarks>
    [TestFixture]
    public class MisdirectedRequestTests
    {

        #region Data

        private X509Certificate2 localhost = null!;
        private X509Certificate2 other     = null!;

        #endregion

        #region Setup

        [OneTimeSetUp]
        public void Init()
        {
            localhost = CreateCertificate("localhost",  names => { names.AddDnsName("localhost"); names.AddIpAddress(System.Net.IPAddress.Loopback); });
            other     = CreateCertificate("other.test", names =>   names.AddDnsName("other.test"));
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            localhost?.Dispose();
            other?.    Dispose();
        }

        #endregion

        #region (private static) CreateCertificate(CommonName, AddNames)

        private static X509Certificate2 CreateCertificate(String                                 CommonName,
                                                          Action<SubjectAlternativeNameBuilder>  AddNames)
        {

            using var rsa  = RSA.Create(2048);

            var request    = new CertificateRequest($"CN={CommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names      = new SubjectAlternativeNameBuilder();

            AddNames(names);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));
            request.CertificateExtensions.Add(names.Build());

            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),
                                                         DateTimeOffset.UtcNow.AddDays(1));

            // No intermediates, and so nothing installed into the machine's CA
            // store, as in HTTPServerALPNTests.
            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx),
                                                    null,
                                                    X509KeyStorageFlags.Exportable);

        }

        #endregion

        #region (private) StartServer(Certificates, Handled)

        /// <summary>
        /// A server answering GET / and POST / for any Host. With certificates,
        /// each connection gets the next one in turn; without, it is cleartext.
        /// </summary>
        private static async Task<HTTPServer> StartServer(X509Certificate2[]  Certificates,
                                                          Action              Handled)
        {

            // Chosen once per connection and remembered, because the selector is
            // asked twice for each: for the handshake, and again when AHTTPServer
            // wraps the TCPConnection in an HTTPConnection. Counting calls would
            // give every handshake the first certificate.
            var connections   = 0;
            var byConnection  = new ConcurrentDictionary<String, X509Certificate2>();

            var httpServer = Certificates.Length > 0
                                 ? await HTTPServer.StartNew(
                                       ServerCertificateSelector: (tcpServer, tcpClient) =>
                                           byConnection.GetOrAdd(
                                               tcpClient.Client.RemoteEndPoint?.ToString() ?? "",
                                               _ => Certificates[(Interlocked.Increment(ref connections) - 1) % Certificates.Length]
                                           )
                                   )
                                 : await HTTPServer.StartNew();

            var httpAPI    = httpServer.AddHTTPAPI();

            foreach (var method in new[] { HTTPMethod.GET, HTTPMethod.POST })
                httpAPI.AddHandler(HTTPPath.Root,
                                   HTTPMethod:   method,
                                   HTTPDelegate: request => {
                                       Handled();
                                       return Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "served".ToUTF8Bytes()
                                                  }.AsImmutable
                                              );
                                   });

            return httpServer;

        }

        #endregion

        #region (private static) Exchange(Port, Request, TLS = true, SNI = "localhost")

        /// <summary>
        /// Send the request as given, and read until the server closes the
        /// connection or five seconds pass. Returns everything that arrived, and
        /// whether the server was the one to close.
        /// </summary>
        private static async Task<(String Received, Boolean Closed)> Exchange(IPPort   Port,
                                                                              String   Request,
                                                                              Boolean  TLS   = true,
                                                                              String   SNI   = "localhost")
        {

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            Stream stream = client.GetStream();

            if (TLS)
            {

                var tls = new SslStream(
                              stream,
                              leaveInnerStreamOpen:               false,
                              userCertificateValidationCallback:  (sender, cert, chain, errors) => true
                          );

                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                              TargetHost                      = SNI,
                              CertificateRevocationCheckMode  = X509RevocationMode.NoCheck
                          });

                stream = tls;

            }

            await using (stream)
            {

                await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));
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
                            return (Encoding.ASCII.GetString(received.ToArray()), true);

                        received.Write(buffer, 0, read);

                    }
                }
                catch (OperationCanceledException)
                {
                    return (Encoding.ASCII.GetString(received.ToArray()), false);
                }
                catch (IOException)
                {
                    return (Encoding.ASCII.GetString(received.ToArray()), true);
                }

            }

        }

        #endregion

        #region (private static) Get(Host, Close = true)

        private static String Get(String Host, Boolean Close = true)

            => $"GET / HTTP/1.1\r\nHost: {Host}\r\n{(Close ? "Connection: close\r\n" : "")}\r\n";

        #endregion


        #region AnOriginTheCertificateNamesIsServed(Host)

        /// <summary>
        /// The certificate's dNSName and its iPAddress, with and without a port,
        /// and in another case: host names compare case-insensitively.
        /// </summary>
        [TestCase("localhost")]
        [TestCase("LOCALHOST")]
        [TestCase("localhost:{0}")]
        [TestCase("127.0.0.1")]
        [TestCase("127.0.0.1:{0}")]
        public async Task AnOriginTheCertificateNamesIsServed(String Host)
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            try
            {

                var (received, _) = await Exchange(httpServer.TCPPort, Get(String.Format(Host, httpServer.TCPPort)));

                Assert.That(received, Does.StartWith("HTTP/1.1 200"));
                Assert.That(handled,  Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AnOriginTheCertificateDoesNotNameIsMisdirected(Host)

        /// <summary>
        /// Another name, with and without a port; a subdomain of the certificate's
        /// name, which only a wildcard would cover; an address the certificate
        /// does not carry; and an IPv6 literal, which only an iPAddress entry
        /// could match, and this certificate has none for ::1.
        /// </summary>
        [TestCase("evil.example")]
        [TestCase("evil.example:{0}")]
        [TestCase("sub.localhost")]
        [TestCase("127.0.0.2")]
        [TestCase("[::1]:{0}")]
        public async Task AnOriginTheCertificateDoesNotNameIsMisdirected(String Host)
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            try
            {

                // Without Connection: close, so that a closed connection is the
                // server's doing.
                var (received, closed) = await Exchange(httpServer.TCPPort, Get(String.Format(Host, httpServer.TCPPort), Close: false));

                Assert.That(received, Does.StartWith("HTTP/1.1 421"));
                Assert.That(received, Does.Contain("Connection: close"));
                Assert.That(closed,   Is.True,       "the server closes after a 421");
                Assert.That(handled,  Is.EqualTo(0), "no handler is asked");

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region TheSNIDoesNotMakeAnOriginValid()

        /// <summary>
        /// A client that names evil.example in SNI as well is given the same
        /// certificate, and that certificate is what Section 7.4 asks about.
        /// </summary>
        [Test]
        public async Task TheSNIDoesNotMakeAnOriginValid()
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            try
            {

                var (received, _) = await Exchange(httpServer.TCPPort, Get("evil.example"), SNI: "evil.example");

                Assert.That(received, Does.StartWith("HTTP/1.1 421"));
                Assert.That(handled,  Is.EqualTo(0));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region TheBodyOfAMisdirectedRequestIsNotReadAsTheNextRequest()

        /// <summary>
        /// The refusal comes before the body is read, so the connection has to
        /// end with it: otherwise the body, here a complete request of its own,
        /// would be read as the next one and served.
        /// </summary>
        [Test]
        public async Task TheBodyOfAMisdirectedRequestIsNotReadAsTheNextRequest()
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            try
            {

                var smuggled          = Get("localhost");
                var (received, closed) = await Exchange(
                                            httpServer.TCPPort,
                                            $"POST / HTTP/1.1\r\nHost: evil.example\r\nContent-Length: {smuggled.Length}\r\n\r\n{smuggled}"
                                        );

                Assert.That(received,                                   Does.StartWith("HTTP/1.1 421"));
                Assert.That(received.Split("HTTP/1.1 ").Length - 1,     Is.EqualTo(1), "one response, not two");
                Assert.That(closed,                                     Is.True);
                Assert.That(handled,                                    Is.EqualTo(0));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region TheCertificateOfEachConnectionDecides()

        /// <summary>
        /// With a ServerCertificateSelector the certificate is per connection, and
        /// so is the answer: the same request is served on the connection that
        /// presented the localhost certificate and refused on the next one, which
        /// presented other.test's.
        /// </summary>
        [Test]
        public async Task TheCertificateOfEachConnectionDecides()
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost, other ], () => Interlocked.Increment(ref handled));

            try
            {

                var (first,  _) = await Exchange(httpServer.TCPPort, Get("localhost"));
                var (second, _) = await Exchange(httpServer.TCPPort, Get("localhost"));
                var (third,  _) = await Exchange(httpServer.TCPPort, Get("other.test"));

                Assert.That(first,   Does.StartWith("HTTP/1.1 200"), "localhost's certificate");
                Assert.That(second,  Does.StartWith("HTTP/1.1 421"), "other.test's certificate");
                Assert.That(third,   Does.StartWith("HTTP/1.1 421"), "localhost's certificate again");
                Assert.That(handled, Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region ACleartextConnectionIsNotChecked()

        /// <summary>
        /// Section 7.4's MUST is about "https" resources, and a cleartext
        /// connection has no certificate to ask.
        /// </summary>
        [Test]
        public async Task ACleartextConnectionIsNotChecked()
        {

            var handled    = 0;
            var httpServer = await StartServer([], () => Interlocked.Increment(ref handled));

            try
            {

                var (received, _) = await Exchange(httpServer.TCPPort, Get("evil.example"), TLS: false);

                Assert.That(received, Does.StartWith("HTTP/1.1 200"));
                Assert.That(handled,  Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region ATrustedGatewayIsTheExceptionAndIsSaidSo()

        /// <summary>
        /// "Unless the connection is from a trusted gateway": a server cannot
        /// tell that for itself, so it is told, with a predicate that accepts
        /// every origin.
        /// </summary>
        [Test]
        public async Task ATrustedGatewayIsTheExceptionAndIsSaidSo()
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            httpServer.IsAuthorityServed = authority => true;

            try
            {

                var (received, _) = await Exchange(httpServer.TCPPort, Get("evil.example"));

                Assert.That(received, Does.StartWith("HTTP/1.1 200"));
                Assert.That(handled,  Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AGivenPredicateIsAskedOnCleartextToo()

        /// <summary>
        /// A predicate given in place of the certificate is asked on every
        /// connection, as HTTP2Server asks its own.
        /// </summary>
        [Test]
        public async Task AGivenPredicateIsAskedOnCleartextToo()
        {

            var handled    = 0;
            var httpServer = await StartServer([], () => Interlocked.Increment(ref handled));

            httpServer.IsAuthorityServed = authority => HTTPAuthority.HostOf(authority) == "localhost";

            try
            {

                var (served,  _) = await Exchange(httpServer.TCPPort, Get("localhost"),    TLS: false);
                var (refused, _) = await Exchange(httpServer.TCPPort, Get("evil.example"), TLS: false);

                Assert.That(served,  Does.StartWith("HTTP/1.1 200"));
                Assert.That(refused, Does.StartWith("HTTP/1.1 421"));
                Assert.That(handled, Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region AnHTTP10RequestWithoutHostIsServed()

        /// <summary>
        /// HTTP/1.0 does not require Host, and a request that names no origin
        /// cannot have named the wrong one: its target URI's authority is the
        /// server's own (RFC 9110, Section 7.1).
        /// </summary>
        [Test]
        public async Task AnHTTP10RequestWithoutHostIsServed()
        {

            var handled    = 0;
            var httpServer = await StartServer([ localhost ], () => Interlocked.Increment(ref handled));

            try
            {

                var (received, _) = await Exchange(httpServer.TCPPort, "GET / HTTP/1.0\r\n\r\n");

                Assert.That(received, Does.StartWith("HTTP/1.0 200"));
                Assert.That(handled,  Is.EqualTo(1));

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

    }

}
