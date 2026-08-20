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

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    /// <summary>
    /// DNS-over-HTTPS over HTTP/2 — the version RFC 8484 §5.2 recommends.
    /// </summary>
    /// <remarks>
    /// The RFC 8484 requirements themselves are pinned once, against the shared
    /// <c>DNSOverHTTPSResource</c>, by <c>DNSOverHTTPSServer_Tests</c>. What is
    /// worth asserting here is what the HTTP/2 rendering could get wrong on its
    /// own: that h2 is what actually gets negotiated, that pseudo-headers are
    /// read and written correctly, and that the parallelism §5.2 exists for is
    /// really there.
    /// </remarks>
    [TestFixture]
    public class DNSOverHTTP2Server_Tests
    {

        #region Data

        private const String DNSMessage = "application/dns-message";

        #endregion

        #region (private static) CreateTestZone()

        private static InMemoryDNSZone CreateTestZone()

            => new InMemoryDNSZone().
                   Add(
                       new SOA(
                           DomainName.        Parse("example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.          FromHours(1),
                           DomainName.        Parse("ns1.example.test."),
                           SimpleEMailAddress.Parse("hostmaster@example.test"),
                           2026081801,
                           TimeSpan.          FromHours  (1),
                           TimeSpan.          FromMinutes(15),
                           TimeSpan.          FromDays   (7),
                           TimeSpan.          FromMinutes(3)
                       )
                   ).
                   Add(
                       new A(
                           DomainName.     Parse("api.example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.       FromMinutes(2),
                           IPv4Address.    Parse("127.0.0.42")
                       )
                   );

        #endregion

        #region (private static) CreateSelfSignedServerCertificate()

        private static X509Certificate2 CreateSelfSignedServerCertificate()
        {

            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                              "CN=localhost",
                              rsa,
                              HashAlgorithmName.SHA256,
                              RSASignaturePadding.Pkcs1
                          );

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false
                )
            );

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false)
            );

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName  ("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            var certificate = request.CreateSelfSigned(
                                  DateTimeOffset.UtcNow.AddMinutes(-5),
                                  DateTimeOffset.UtcNow.AddDays(1)
                              );

            return X509CertificateLoader.LoadPkcs12(
                       certificate.Export(X509ContentType.Pfx),
                       null,
                       X509KeyStorageFlags.Exportable
                   );

        }

        #endregion

        #region (private static) StartServer(Certificate = null)

        /// <summary>
        /// h2c on an ephemeral loopback port, or h2 over TLS when given a
        /// certificate.
        /// </summary>
        private static async Task<DNSOverHTTP2Server> StartServer(X509Certificate2? Certificate = null)

            => await DNSOverHTTP2Server.StartNew(
                         new AuthoritativeDNSRequestHandler(CreateTestZone()),
                         new DNSServerOptions {
                             TLSServerCertificate = Certificate
                         },
                         IPv4Address.Localhost,
                         IPPort.Parse(0)
                     );

        #endregion

        #region (private static) UrlOf / NewClient / Query helpers

        private static String UrlOf(DNSOverHTTP2Server Server)

            => $"{(Server.IsSecured ? "https" : "http")}://127.0.0.1:{Server.TCPPort}{Server.DNSQueryPath}";

        /// <summary>
        /// A client that will speak HTTP/2 and nothing else — RequestVersionExact
        /// makes a silent fall back to HTTP/1.1 an error rather than a passing
        /// test.
        /// </summary>
        private static HttpClient NewClient()

            => new (
                   new HttpClientHandler {
                       ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                   }
               ) {
                   DefaultRequestVersion       = HttpVersion.Version20,
                   DefaultVersionPolicy        = HttpVersionPolicy.RequestVersionExact,
                   Timeout                     = TimeSpan.FromSeconds(10)
               };

        private static Byte[] QueryFor(String DomainName)

            => DNSPacket.Query(
                   DNSServiceName.Parse(DomainName),
                   0,
                   DNSResourceRecordTypes.A
               ).ToByteArray();

        private static async Task<HttpResponseMessage> PostQuery(HttpClient          HTTPClient,
                                                                 DNSOverHTTP2Server  Server,
                                                                 String              DomainName)
        {

            var content = new ByteArrayContent(QueryFor(DomainName));
            content.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

            return await HTTPClient.PostAsync(UrlOf(Server), content);

        }

        #endregion


        #region DoH2Server_Answers_A_POST_Over_H2C()

        [Test]
        public async Task DoH2Server_Answers_A_POST_Over_H2C()
        {

            var server = await StartServer();

            try
            {

                using var http     = NewClient();
                using var response = await PostQuery(http, server, "api.example.test.");

                var body = await response.Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {
                    Assert.That(response.Version,     Is.EqualTo(HttpVersion.Version20), "the exchange really is HTTP/2");
                    Assert.That((Int32) response.StatusCode, Is.EqualTo(200));
                    Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo(DNSMessage));
                    Assert.That(response.Content.Headers.ContentType?.CharSet,   Is.Null);
                    Assert.That(body.Length,          Is.GreaterThan(12));
                    Assert.That(body[3] & 0x0F,       Is.EqualTo(0), "NOERROR");
                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Answers_A_GET_Over_H2C()

        [Test]
        public async Task DoH2Server_Answers_A_GET_Over_H2C()
        {

            var server = await StartServer();

            try
            {

                using var http     = NewClient();
                var       url      = $"{UrlOf(server)}?dns={QueryFor("api.example.test.").ToBase64URL()}";
                using var response = await http.GetAsync(url);

                var body = await response.Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {
                    Assert.That(response.Version,            Is.EqualTo(HttpVersion.Version20));
                    Assert.That((Int32) response.StatusCode, Is.EqualTo(200));
                    Assert.That(body.Length,                 Is.GreaterThan(12));
                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Answers_Over_Tls_With_Alpn_H2()

        /// <summary>
        /// RFC 8484 §5 requires the https scheme; RFC 9113 §3.2 requires ALPN to
        /// select h2. Both at once is the shape a deployment actually has.
        /// </summary>
        [Test]
        public async Task DoH2Server_Answers_Over_Tls_With_Alpn_H2()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = await StartServer(certificate);

            try
            {

                Assert.That(server.IsSecured, Is.True);

                using var http     = NewClient();
                using var response = await PostQuery(http, server, "api.example.test.");

                var body = await response.Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {
                    Assert.That(response.Version,            Is.EqualTo(HttpVersion.Version20));
                    Assert.That((Int32) response.StatusCode, Is.EqualTo(200));
                    Assert.That(body[3] & 0x0F,              Is.EqualTo(0));
                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Answers_Many_Queries_On_One_Connection()

        /// <summary>
        /// RFC 8484 §5.2 wants HTTP/2 because "A competitive HTTP transport needs
        /// to support reordering, parallelism, priority, and header compression".
        /// This is the parallelism: twenty queries in flight at once, all
        /// answered, on a single connection.
        /// </summary>
        [Test]
        public async Task DoH2Server_Answers_Many_Queries_On_One_Connection()
        {

            var server = await StartServer();

            try
            {

                using var http = NewClient();

                var responses = await Task.WhenAll(
                                          Enumerable.Range(0, 20).
                                                     Select(_ => PostQuery(http, server, "api.example.test."))
                                      );

                try
                {

                    Assert.Multiple(() => {
                        foreach (var response in responses)
                        {
                            Assert.That(response.Version,            Is.EqualTo(HttpVersion.Version20));
                            Assert.That((Int32) response.StatusCode, Is.EqualTo(200));
                        }
                    });

                }
                finally
                {
                    foreach (var response in responses)
                        response.Dispose();
                }

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Renders_The_Same_Refusals_As_HTTP11()

        /// <summary>
        /// The status codes come from the shared resource, so what this checks is
        /// the rendering: that they survive the trip through <c>:status</c> and
        /// that an Allow field is produced as RFC 9110 §10.2.1 requires.
        /// </summary>
        [Test]
        public async Task DoH2Server_Renders_The_Same_Refusals_As_HTTP11()
        {

            var server = await StartServer();

            try
            {

                using var http = NewClient();

                using var wrongPath   = await http.PostAsync(
                                                  $"http://127.0.0.1:{server.TCPPort}/elsewhere",
                                                  new ByteArrayContent(QueryFor("api.example.test."))
                                              );

                using var wrongMedia  = await http.PostAsync(
                                                  UrlOf(server),
                                                  new ByteArrayContent(QueryFor("api.example.test.")) {
                                                      Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                                                  }
                                              );

                using var wrongMethod = await http.SendAsync(
                                                  new HttpRequestMessage(HttpMethod.Put, UrlOf(server)) {
                                                      Version        = HttpVersion.Version20,
                                                      VersionPolicy  = HttpVersionPolicy.RequestVersionExact
                                                  }
                                              );

                Assert.Multiple(() => {
                    Assert.That((Int32) wrongPath.  StatusCode, Is.EqualTo(404));
                    Assert.That((Int32) wrongMedia. StatusCode, Is.EqualTo(415));
                    Assert.That((Int32) wrongMethod.StatusCode, Is.EqualTo(405));
                    Assert.That(wrongMethod.Content.Headers.Allow, Does.Contain("POST"));
                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Lets_Alpn_Choose_On_One_Port()

        /// <summary>
        /// RFC 9113 §3.2 requires ALPN to select h2, and a client that cannot
        /// speak it asks for http/1.1 in the same handshake. One port, two
        /// clients, and the negotiation decides which pipeline answers.
        /// </summary>
        [Test]
        public async Task DoH2Server_Lets_Alpn_Choose_On_One_Port()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = await StartServer(certificate);

            try
            {

                Assert.That(server.ServesHTTP11, Is.True, "the listener offers http/1.1 as well");

                // One query, sent twice — DNSPacket.Query draws its ID at random,
                // so two separately built queries would differ in the two octets
                // the answers echo back.
                var query = QueryFor("api.example.test.");

                using var overH2  = NewClient();
                using var overH11 = new HttpClient(
                                        new HttpClientHandler {
                                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                                        }
                                    ) {
                                        DefaultRequestVersion  = HttpVersion.Version11,
                                        DefaultVersionPolicy   = HttpVersionPolicy.RequestVersionExact,
                                        Timeout                = TimeSpan.FromSeconds(10)
                                    };

                var content2  = new ByteArrayContent(query);
                content2.Headers.ContentType  = new MediaTypeHeaderValue(DNSMessage);

                var content11 = new ByteArrayContent(query);
                content11.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

                using var h2Response  = await overH2. PostAsync(UrlOf(server), content2);
                using var h11Response = await overH11.PostAsync(UrlOf(server), content11);

                var h2Body  = await h2Response. Content.ReadAsByteArrayAsync();
                var h11Body = await h11Response.Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {

                    Assert.That(h2Response. Version, Is.EqualTo(HttpVersion.Version20),
                                "a client offering h2 gets h2");
                    Assert.That(h11Response.Version, Is.EqualTo(HttpVersion.Version11),
                                "a client offering only http/1.1 is served, not turned away at the handshake");

                    Assert.That((Int32) h2Response. StatusCode, Is.EqualTo(200));
                    Assert.That((Int32) h11Response.StatusCode, Is.EqualTo(200));

                    Assert.That(h11Response.Content.Headers.ContentType?.MediaType, Is.EqualTo(DNSMessage));

                    // Same port, same resource, same pipeline — so the same octets.
                    Assert.That(h11Body, Is.EqualTo(h2Body),
                                "ALPN chooses the framing, never the answer");

                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoH2Server_Can_Be_H2_Only()

        /// <summary>
        /// Turning the fallback off has to stop the endpoint *advertising*
        /// http/1.1, not just stop serving it — offering a protocol and then not
        /// serving it is worse than not offering it, since a client that could
        /// have spoken h2 may pick the other one and get nothing.
        /// </summary>
        [Test]
        public async Task DoH2Server_Can_Be_H2_Only()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = await DNSOverHTTP2Server.StartNew(
                                   new AuthoritativeDNSRequestHandler(CreateTestZone()),
                                   new DNSServerOptions { TLSServerCertificate = certificate },
                                   IPv4Address.Localhost,
                                   IPPort.Parse(0),
                                   ServeHTTP11ViaALPN: false
                               );

            try
            {

                Assert.That(server.ServesHTTP11, Is.False);

                using var overH2  = NewClient();
                using var overH11 = new HttpClient(
                                        new HttpClientHandler {
                                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                                        }
                                    ) {
                                        DefaultRequestVersion  = HttpVersion.Version11,
                                        DefaultVersionPolicy   = HttpVersionPolicy.RequestVersionExact,
                                        Timeout                = TimeSpan.FromSeconds(10)
                                    };

                var content = new ByteArrayContent(QueryFor("api.example.test."));
                content.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

                using var h2Response = await overH2.PostAsync(UrlOf(server), content);

                Assert.That((Int32) h2Response.StatusCode, Is.EqualTo(200), "h2 is still served");

                Assert.ThrowsAsync<HttpRequestException>(
                    async () => await overH11.PostAsync(
                                          UrlOf(server),
                                          new ByteArrayContent(QueryFor("api.example.test."))
                                      ),
                    "an http/1.1-only client fails ALPN rather than being accepted and ignored"
                );

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DNSServer_Serves_Both_DoH_Versions_Side_By_Side()

        /// <summary>
        /// One zone, one pipeline, two RFC 8484 listeners — and the same answer
        /// from both, which is the claim §5.2 makes when it calls HTTP/2 a
        /// performance recommendation rather than a semantic one.
        /// </summary>
        [Test]
        public async Task DNSServer_Serves_Both_DoH_Versions_Side_By_Side()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast      = false,
                                 EnableUDPMulticast    = false,
                                 EnableTCPUnicast      = false,
                                 EnableHTTPSUnicast    = true,
                                 HTTPSUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 EnableHTTP2Unicast    = true,
                                 HTTP2UnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 TLSServerCertificate  = certificate
                             }
                         );

            try
            {

                await server.Start();

                var http1Socket = server.ActiveHTTPSUnicastSocket;
                var http2Socket = server.ActiveHTTP2UnicastSocket;

                Assert.That(http1Socket, Is.Not.Null);
                Assert.That(http2Socket, Is.Not.Null);

                using var http11 = new HttpClient(
                                       new HttpClientHandler {
                                           ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                                       }
                                   ) {
                                       DefaultRequestVersion  = HttpVersion.Version11,
                                       DefaultVersionPolicy   = HttpVersionPolicy.RequestVersionExact,
                                       Timeout                = TimeSpan.FromSeconds(10)
                                   };

                using var http2  = NewClient();

                // One query, sent twice. DNSPacket.Query draws its transaction ID
                // at random, so two separately built queries would differ in the
                // two octets the answers echo back — and the comparison below
                // would be measuring the random number generator.
                var query    = QueryFor("api.example.test.");

                var content1 = new ByteArrayContent(query);
                content1.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

                var content2 = new ByteArrayContent(query);
                content2.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

                using var over11 = await http11.PostAsync($"https://127.0.0.1:{http1Socket!.Value.Port}/dns-query", content1);
                using var over2  = await http2. PostAsync($"https://127.0.0.1:{http2Socket!.Value.Port}/dns-query", content2);

                var body11 = await over11.Content.ReadAsByteArrayAsync();
                var body2  = await over2. Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {

                    Assert.That(over11.Version, Is.EqualTo(HttpVersion.Version11));
                    Assert.That(over2. Version, Is.EqualTo(HttpVersion.Version20));

                    Assert.That((Int32) over11.StatusCode, Is.EqualTo(200));
                    Assert.That((Int32) over2. StatusCode, Is.EqualTo(200));

                    // Same query, same zone, same pipeline — so the same octets.
                    // §5.2 recommends HTTP/2 for performance, and this is the
                    // other half of that: it is not supposed to change the answer.
                    Assert.That(body2, Is.EqualTo(body11),
                                "the version of HTTP must not change the DNS message it carries");

                });

            }
            finally
            {
                await server.Stop();
            }

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region DNSServer_Refuses_Both_DoH_Listeners_On_One_Port()

        [Test]
        public void DNSServer_Refuses_Both_DoH_Listeners_On_One_Port()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast      = false,
                                 EnableUDPMulticast    = false,
                                 EnableTCPUnicast      = false,
                                 EnableHTTPSUnicast    = true,
                                 HTTPSUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(8443)),
                                 EnableHTTP2Unicast    = true,
                                 HTTP2UnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(8443)),
                                 TLSServerCertificate  = certificate
                             }
                         );

            // ALPN is what lets one port carry both, and this server cannot do
            // that yet — so it says so rather than failing inside a listener task.
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await server.Start()
            );

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region DoH2Server_Answers_A_HEAD_Without_A_Body()

        /// <summary>
        /// RFC 9110 §9.3.2: the header fields of a HEAD response are "the same as
        /// … a GET", with no content. Over HTTP/2 that is the handler's own
        /// doing — nothing below it knows the method.
        /// </summary>
        [Test]
        public async Task DoH2Server_Answers_A_HEAD_Without_A_Body()
        {

            var server = await StartServer();

            try
            {

                using var http = NewClient();

                var url = $"{UrlOf(server)}?dns={QueryFor("api.example.test.").ToBase64URL()}";

                using var getResponse  = await http.GetAsync(url);
                using var headResponse = await http.SendAsync(
                                                   new HttpRequestMessage(HttpMethod.Head, url) {
                                                       Version        = HttpVersion.Version20,
                                                       VersionPolicy  = HttpVersionPolicy.RequestVersionExact
                                                   }
                                               );

                var getBody  = await getResponse. Content.ReadAsByteArrayAsync();
                var headBody = await headResponse.Content.ReadAsByteArrayAsync();

                Assert.Multiple(() => {
                    Assert.That((Int32) headResponse.StatusCode, Is.EqualTo(200));
                    Assert.That(headResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(DNSMessage));
                    Assert.That(headResponse.Content.Headers.ContentLength, Is.EqualTo(getBody.Length));
                    Assert.That(headBody.Length, Is.EqualTo(0));
                });

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

    }

}
