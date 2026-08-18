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
    /// DNS-over-HTTPS server tests (RFC 8484).
    /// </summary>
    /// <remarks>
    /// The HTTP-layer assertions go through <see cref="HttpClient"/> rather than
    /// through Hermod's own DoH client, on purpose: what is under test here is the
    /// status code, the media type and the cache metadata, and asking the
    /// implementation's own client about those would only prove the two agree.
    /// The end-to-end tests do use <see cref="DNSHTTPSClient"/>, where agreeing is
    /// exactly the point.
    /// </remarks>
    [TestFixture]
    public class DNSOverHTTPSServer_Tests
    {

        #region Data

        private const String  DNSMessage  = "application/dns-message";

        #endregion

        #region (private static) CreateTestZone()

        private static InMemoryDNSZone CreateTestZone()

            => new InMemoryDNSZone().
                   Add(
                       new SOA(
                           DomainName.        Parse("example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.          FromHours  (1),
                           DomainName.        Parse("ns1.example.test."),
                           SimpleEMailAddress.Parse("hostmaster@example.test"),
                           2026081801,
                           TimeSpan.          FromHours  (1),
                           TimeSpan.          FromMinutes(15),
                           TimeSpan.          FromDays   (7),
                           TimeSpan.          FromMinutes(3)      // MINIMUM: the negative-answer lifetime
                       )
                   ).
                   Add(
                       new A(
                           DomainName.     Parse("api.example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.       FromMinutes(5),
                           IPv4Address.    Parse("127.0.0.42")
                       )
                   ).
                   Add(
                       new A(
                           DomainName.     Parse("api.example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.       FromMinutes(2),        // the smallest TTL in the answer
                           IPv4Address.    Parse("127.0.0.43")
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

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false)
            );

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.KeyEncipherment,
                    false
                )
            );

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false)
            );

            var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
            subjectAlternativeNameBuilder.AddDnsName  ("localhost");
            subjectAlternativeNameBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());

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

        #region (private static) StartServer(Options = null)

        /// <summary>
        /// A cleartext DoH endpoint on an ephemeral loopback port.
        /// </summary>
        private static async Task<DNSOverHTTPSServer> StartServer(DNSServerOptions? Options = null)

            => await DNSOverHTTPSServer.StartNew(
                         new AuthoritativeDNSRequestHandler(CreateTestZone()),
                         Options ?? new DNSServerOptions(),
                         IPv4Address.Localhost,
                         IPPort.Parse(0)
                     );

        #endregion

        #region (private static) UrlOf(Server)

        private static String UrlOf(DNSOverHTTPSServer Server)

            => $"http://127.0.0.1:{Server.TCPPort}{Server.DNSQueryPath}";

        #endregion


        #region DoHServer_Answers_A_GET_Query()

        /// <summary>
        /// RFC 8484 §4.1: "DoH servers MUST implement both the POST and GET methods."
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_A_GET_Query()
        {

            var server = await StartServer();

            try
            {

                await using var client = new DNSHTTPSClient(
                                             URL.Parse(UrlOf(server)),
                                             Mode:          DNSHTTPSMode.GET,
                                             QueryTimeout:  TimeSpan.FromSeconds(5)
                                         );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:  TimeSpan.FromSeconds(5)
                               );

                Assert.That(response.ResponseCode,            Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(), Is.EqualTo(2));
                Assert.That(response.FilteredAnswers.Select(a => a.IPv4Address.ToString()),
                            Is.EquivalentTo(new[] { "127.0.0.42", "127.0.0.43" }));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_A_POST_Query()

        /// <summary>
        /// RFC 8484 §4.1: "When using the POST method, the DNS query is included as
        /// the message body of the HTTP request."
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_A_POST_Query()
        {

            var server = await StartServer();

            try
            {

                await using var client = new DNSHTTPSClient(
                                             URL.Parse(UrlOf(server)),
                                             Mode:          DNSHTTPSMode.POST,
                                             QueryTimeout:  TimeSpan.FromSeconds(5)
                                         );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:  TimeSpan.FromSeconds(5)
                               );

                Assert.That(response.ResponseCode,            Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(), Is.EqualTo(2));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_With_The_DNSMessage_Media_Type()

        /// <summary>
        /// RFC 8484 §7.1 registers "application/dns-message" with "Optional
        /// parameters: N/A", and calls the payload "a binary format" — so the
        /// response names the media type and nothing else, no charset.
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_With_The_DNSMessage_Media_Type()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await PostQuery(http, server, "api.example.test.");

                Assert.That((Int32) httpResponse.StatusCode,                Is.EqualTo(200));
                Assert.That(httpResponse.Content.Headers.ContentType?.MediaType,
                                                                            Is.EqualTo(DNSMessage));
                Assert.That(httpResponse.Content.Headers.ContentType?.CharSet,
                                                                            Is.Null);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_NXDOMAIN_With_200()

        /// <summary>
        /// RFC 8484 §4.2.1: "a successful 2xx HTTP status code is used even with a
        /// DNS message whose DNS response code indicates failure, such as SERVFAIL
        /// or NXDOMAIN."
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_NXDOMAIN_With_200()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await PostQuery(http, server, "missing.example.test.");

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(200));

                var body      = await httpResponse.Content.ReadAsByteArrayAsync();
                var rcode     = (DNSResponseCodes) (body[3] & 0x0F);

                Assert.That(rcode, Is.EqualTo(DNSResponseCodes.NameError));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Sends_The_Smallest_Answer_TTL_As_MaxAge()

        /// <summary>
        /// RFC 8484 §5.1: "The assigned freshness lifetime of a DoH HTTP response
        /// MUST be less than or equal to the smallest TTL in the Answer section of
        /// the DNS response. A freshness lifetime equal to the smallest TTL in the
        /// Answer section is RECOMMENDED."
        /// </summary>
        [Test]
        public async Task DoHServer_Sends_The_Smallest_Answer_TTL_As_MaxAge()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await PostQuery(http, server, "api.example.test.");

                // The zone holds the same name twice, at 5 minutes and at 2.
                Assert.That(httpResponse.Headers.CacheControl?.MaxAge,
                            Is.EqualTo(TimeSpan.FromMinutes(2)));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Sends_The_SOA_Minimum_As_MaxAge_For_A_Denial()

        /// <summary>
        /// RFC 8484 §5.1: "If the DNS response has no records in the Answer
        /// section, and the DNS response has an SOA record in the Authority
        /// section, the response freshness lifetime MUST NOT be greater than the
        /// MINIMUM field from that SOA record."
        /// </summary>
        [Test]
        public async Task DoHServer_Sends_The_SOA_Minimum_As_MaxAge_For_A_Denial()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await PostQuery(http, server, "missing.example.test.");

                var maxAge = httpResponse.Headers.CacheControl?.MaxAge;

                Assert.That(maxAge, Is.Not.Null);
                Assert.That(maxAge!.Value, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(3)));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_An_Unknown_Path_With_404()

        [Test]
        public async Task DoHServer_Answers_An_Unknown_Path_With_404()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await http.GetAsync($"http://127.0.0.1:{server.TCPPort}/somewhere-else");

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(404));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_An_Unsupported_Method_With_405()

        [Test]
        public async Task DoHServer_Answers_An_Unsupported_Method_With_405()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpRequest  = new HttpRequestMessage(HttpMethod.Put, UrlOf(server));
                using var httpResponse = await http.SendAsync(httpRequest);

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(405));
                Assert.That(httpResponse.Content.Headers.Allow, Does.Contain("POST"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_A_Wrong_Media_Type_With_415()

        /// <summary>
        /// RFC 8484 §4.2.1 names 415 for "unsupported media types", and points out
        /// that a client may take it as a reason to try a different DoH server.
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_A_Wrong_Media_Type_With_415()
        {

            var server = await StartServer();

            try
            {

                var query   = DNSPacket.Query(
                                  DNSServiceName.Parse("api.example.test."),
                                  0,
                                  DNSResourceRecordTypes.A
                              ).ToByteArray();

                using var http    = new HttpClient();
                using var content = new ByteArrayContent(query);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var httpResponse = await http.PostAsync(UrlOf(server), content);

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(415));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_A_HEAD_Like_A_GET_Without_A_Body()

        /// <summary>
        /// RFC 9110 §9.1: "All general-purpose servers MUST support the methods GET
        /// and HEAD." RFC 8484 has no use for HEAD, but the headers it produces have
        /// to be the ones the GET would have produced — same status, same media
        /// type, same length — with the body left off.
        /// </summary>
        [Test]
        public async Task DoHServer_Answers_A_HEAD_Like_A_GET_Without_A_Body()
        {

            var server = await StartServer();

            try
            {

                var query = DNSPacket.Query(
                                DNSServiceName.Parse("api.example.test."),
                                0,
                                DNSResourceRecordTypes.A
                            ).ToByteArray();

                var url   = $"{UrlOf(server)}?dns={query.ToBase64URL()}";

                using var http         = new HttpClient();

                using var getResponse  = await http.GetAsync(url);
                using var headRequest  = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await http.SendAsync(headRequest);

                var getBody   = await getResponse. Content.ReadAsByteArrayAsync();
                var headBody  = await headResponse.Content.ReadAsByteArrayAsync();

                Assert.That((Int32) getResponse. StatusCode,  Is.EqualTo(200));
                Assert.That((Int32) headResponse.StatusCode,  Is.EqualTo(200));

                Assert.That(headResponse.Content.Headers.ContentType?.MediaType,
                            Is.EqualTo(DNSMessage));

                Assert.That(headResponse.Content.Headers.ContentLength,
                            Is.EqualTo(getBody.Length));

                Assert.That(getBody.Length,   Is.GreaterThan(0));
                Assert.That(headBody.Length,  Is.EqualTo(0));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Serves_A_Wildcard_Accept()

        /// <summary>
        /// RFC 9110 §12.4.3: a wildcard is there "to select unspecified values",
        /// so <c>*/*</c> and <c>application/*</c> both leave room for
        /// <c>application/dns-message</c> and must be served, not refused.
        /// </summary>
        [Test]
        [TestCase("*/*")]
        [TestCase("application/*")]
        [TestCase("application/dns-message")]
        [TestCase("text/html, application/dns-message;q=0.9, */*;q=0.1")]
        public async Task DoHServer_Serves_A_Wildcard_Accept(String Accept)
        {

            var server = await StartServer();

            try
            {

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Accept", Accept);

                using var httpResponse = await PostQuery(http, server, "api.example.test.");

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(200), $"Accept: {Accept}");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_An_Impossible_Accept_With_406()

        /// <summary>
        /// RFC 8484 §4.2.1 names 406 for "where the server cannot generate a
        /// representation suitable for the client". A client that lists media types
        /// and excludes this one has asked for something that does not exist here —
        /// including by naming it at <c>q=0</c>, which RFC 9110 §12.4.2 defines as
        /// "not acceptable".
        /// </summary>
        [Test]
        [TestCase("application/json")]
        [TestCase("application/dns-json, text/plain")]
        [TestCase("application/dns-message;q=0")]
        public async Task DoHServer_Answers_An_Impossible_Accept_With_406(String Accept)
        {

            var server = await StartServer();

            try
            {

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Accept", Accept);

                using var httpResponse = await PostQuery(http, server, "api.example.test.");

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(406), $"Accept: {Accept}");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_An_Undecodable_Query_With_400()

        [Test]
        public async Task DoHServer_Answers_An_Undecodable_Query_With_400()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await http.GetAsync($"{UrlOf(server)}?dns=not-base64url!!");

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(400));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Answers_A_Missing_Query_Parameter_With_400()

        [Test]
        public async Task DoHServer_Answers_A_Missing_Query_Parameter_With_400()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await http.GetAsync(UrlOf(server));

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(400));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Pads_A_Padded_Query_To_A_Block_Boundary()

        /// <summary>
        /// RFC 7830 §4: "Responders MUST pad DNS responses when the respective DNS
        /// query included the 'Padding' option", and RFC 8467 §4.1 recommends a
        /// multiple of 468 octets for the response.
        /// </summary>
        /// <remarks>
        /// The query advertises 200 octets of EDNS(0) payload size, which is less
        /// than one padding block. On a datagram transport RFC 7830 §4 would make
        /// that a ceiling — "Padded DNS messages MUST NOT exceed the number of
        /// octets specified in the Requestor's Payload Size field" — and the reply
        /// would stop at 200 bytes. Here it must not, because RFC 8484 §6 has a DoH
        /// server "ignore the value given for the EDNS UDP payload size in DNS
        /// requests". So 468 rather than 200 is the whole point of the number.
        /// </remarks>
        [Test]
        public async Task DoHServer_Pads_A_Padded_Query_To_A_Block_Boundary()
        {

            var server = await StartServer();

            try
            {

                var query   = DNSPacket.Query(
                                  DNSServiceName.Parse("api.example.test."),
                                  UDPPayloadSize:    200,
                                  RecursionDesired:  true,
                                  EDNSOptions:       [ new EDNSPaddingOption(0) ],
                                  DNSResourceRecordTypes.A
                              ).ToByteArray();

                using var http    = new HttpClient();
                using var content = new ByteArrayContent(query);
                content.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

                using var httpResponse = await http.PostAsync(UrlOf(server), content);

                var body = await httpResponse.Content.ReadAsByteArrayAsync();

                Assert.That((Int32) httpResponse.StatusCode, Is.EqualTo(200));
                Assert.That(body.Length, Is.EqualTo((Int32) DNSPadding.ResponseBlockSize),
                            "a padded DoH response lands on the 468-octet block boundary of RFC 8467 §4.1, " +
                            "and is not cut short at the 200 octets the query advertised");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DoHServer_Leaves_An_Unpadded_Query_Unpadded()

        /// <summary>
        /// RFC 7830 §4: "Responders MUST NOT pad DNS responses when the respective
        /// DNS query did not indicate EDNS(0) support."
        /// </summary>
        [Test]
        public async Task DoHServer_Leaves_An_Unpadded_Query_Unpadded()
        {

            var server = await StartServer();

            try
            {

                using var http         = new HttpClient();
                using var httpResponse = await PostQuery(http, server, "api.example.test.");

                var body = await httpResponse.Content.ReadAsByteArrayAsync();

                // The same answer padded is 468 bytes; unpadded it is under a
                // hundred, so "shorter than one block" is enough to tell them apart.
                Assert.That(body.Length, Is.GreaterThan(0));
                Assert.That(body.Length, Is.LessThan((Int32) DNSPadding.ResponseBlockSize));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DNSServer_Serves_DoH_Over_TLS_As_A_Fifth_Transport()

        /// <summary>
        /// The same zone, the same pipeline, one more listener — and over TLS,
        /// which RFC 8484 §5 requires: "This protocol MUST be used with the https
        /// URI scheme."
        /// </summary>
        [Test]
        public async Task DNSServer_Serves_DoH_Over_TLS_As_A_Fifth_Transport()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast      = false,
                                 EnableUDPMulticast    = false,
                                 EnableTCPUnicast      = false,
                                 EnableTLSUnicast      = false,
                                 EnableHTTPSUnicast    = true,
                                 HTTPSUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 TLSServerCertificate  = certificate
                             }
                         );

            try
            {

                await server.Start();

                var socket = server.ActiveHTTPSUnicastSocket;

                Assert.That(socket, Is.Not.Null);

                await using var client = new DNSHTTPSClient(
                                             URL.Parse($"https://127.0.0.1:{socket!.Value.Port}/dns-query"),
                                             Mode:                        DNSHTTPSMode.POST,
                                             QueryTimeout:                TimeSpan.FromSeconds(5),
                                             RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success()
                                         );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:  TimeSpan.FromSeconds(5)
                               );

                Assert.That(response.ResponseCode,            Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(), Is.EqualTo(2));

            }
            finally
            {
                await server.Stop();
            }

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region DNSServer_HTTPS_Start_Requires_ServerCertificate()

        /// <summary>
        /// RFC 8484 §5: "This protocol MUST be used with the https URI scheme."
        /// A cleartext endpoint is a <see cref="DNSOverHTTPSServer"/> of its own,
        /// never a listener this server calls HTTPS.
        /// </summary>
        [Test]
        public void DNSServer_HTTPS_Start_Requires_ServerCertificate()
        {

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast    = false,
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false,
                                 EnableHTTPSUnicast  = true,
                                 HTTPSUnicastSocket  = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0))
                             }
                         );

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await server.Start()
            );

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion


        #region (private static) PostQuery(HTTPClient, Server, DomainName)

        /// <summary>
        /// An RFC 8484 POST built without Hermod's DoH client, so the assertions
        /// above measure the server rather than the pair.
        /// </summary>
        private static async Task<HttpResponseMessage> PostQuery(HttpClient          HTTPClient,
                                                                 DNSOverHTTPSServer  Server,
                                                                 String              DomainName)
        {

            var query   = DNSPacket.Query(
                              DNSServiceName.Parse(DomainName),
                              0,
                              DNSResourceRecordTypes.A
                          ).ToByteArray();

            var content = new ByteArrayContent(query);
            content.Headers.ContentType = new MediaTypeHeaderValue(DNSMessage);

            return await HTTPClient.PostAsync(UrlOf(Server), content);

        }

        #endregion

    }

}
