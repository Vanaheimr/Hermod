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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    [TestFixture]
    public class DNSServer_Tests
    {

        private static InMemoryDNSZone CreateTestZone()

            => new InMemoryDNSZone().
                   Add(
                       new A(
                           DomainName. Parse("api.example.test."),
                           DNSQueryClasses.IN,
                           TimeSpan.   FromMinutes(5),
                           IPv4Address.Parse("127.0.0.42")
                       )
                   );


        private static A CreateARecord(String     DomainNameText,
                                       String     IPv4AddressText,
                                       TimeSpan?  TimeToLive   = null)

            => new (
                   DomainName.Parse(DomainNameText),
                   DNSQueryClasses.IN,
                   TimeToLive ?? TimeSpan.FromMinutes(5),
                   IPv4Address.Parse(IPv4AddressText)
               );


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
                new X509BasicConstraintsExtension(
                    false,
                    false,
                    0,
                    false
                )
            );

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.KeyEncipherment,
                    false
                )
            );

            // Server Authentication
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [ new Oid("1.3.6.1.5.5.7.3.1") ],
                    false
                )
            );

            var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
            subjectAlternativeNameBuilder.AddDnsName("localhost");
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


        private sealed class TestLogger<T> : ILogger<T>
        {

            private readonly Lock entryLock = new();

            public List<(LogLevel LogLevel, String Message, Exception? Exception)> Entries { get; } = [];


            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull

                => NullScope.Instance;


            public Boolean IsEnabled(LogLevel logLevel)

                => true;


            public void Log<TState>(LogLevel                         logLevel,
                                    EventId                          eventId,
                                    TState                           state,
                                    Exception?                       exception,
                                    Func<TState, Exception?, String> formatter)
            {
                lock (entryLock)
                    Entries.Add((logLevel, formatter(state, exception), exception));
            }


            private sealed class NullScope : IDisposable
            {

                public static readonly NullScope Instance = new();

                public void Dispose()
                { }

            }

        }


        #region AuthoritativeHandler_Returns_Matching_Record()

        [Test]
        public async Task AuthoritativeHandler_Returns_Matching_Record()
        {

            var handler   = new AuthoritativeDNSRequestHandler(CreateTestZone());
            var request   = DNSPacket.Query(
                                DNSServiceName.Parse("api.example.test."),
                                0,
                                DNSResourceRecordTypes.A
                            );

            var response  = await handler.ProcessDNSRequest(request);

            Assert.That(response,                         Is.Not.Null);
            Assert.That(response!.ResponseCode,           Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.AuthoritativeAnswer,     Is.True);
            Assert.That(response.AnswerRRs.Count(),       Is.EqualTo(1));
            Assert.That(response.AnswerRRs.OfType<A>().First().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.42")));

        }

        #endregion

        #region AuthoritativeHandler_Returns_NoData_For_Known_Name_And_Missing_Type()

        [Test]
        public async Task AuthoritativeHandler_Returns_NoData_For_Known_Name_And_Missing_Type()
        {

            var handler   = new AuthoritativeDNSRequestHandler(CreateTestZone());
            var request   = DNSPacket.Query(
                                DNSServiceName.Parse("api.example.test."),
                                0,
                                DNSResourceRecordTypes.AAAA
                            );

            var response  = await handler.ProcessDNSRequest(request);

            Assert.That(response,                    Is.Not.Null);
            Assert.That(response!.ResponseCode,      Is.EqualTo(DNSResponseCodes.NoError));
            Assert.That(response.AnswerRRs.Count(),  Is.EqualTo(0));

        }

        #endregion

        #region AuthoritativeHandler_Returns_NameError_For_Unknown_Name()

        [Test]
        public async Task AuthoritativeHandler_Returns_NameError_For_Unknown_Name()
        {

            var handler   = new AuthoritativeDNSRequestHandler(CreateTestZone());
            var request   = DNSPacket.Query(
                                DNSServiceName.Parse("missing.example.test."),
                                0,
                                DNSResourceRecordTypes.A
                            );

            var response  = await handler.ProcessDNSRequest(request);

            Assert.That(response,                Is.Not.Null);
            Assert.That(response!.ResponseCode,  Is.EqualTo(DNSResponseCodes.NameError));

        }

        #endregion

        #region UDPServer_Answers_From_Configured_Zone_On_Ephemeral_Port()

        [Test]
        public async Task UDPServer_Answers_From_Configured_Zone_On_Ephemeral_Port()
        {

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 UDPUnicastSocket    = new IPSocket(
                                                           IPv4Address.Localhost,
                                                           IPPort.     Zero
                                                       ),
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false
                             }
                         );

            try
            {

                await server.Start();

                var activeUDPUnicastSocket = server.ActiveUDPUnicastSocket;

                Assert.That(activeUDPUnicastSocket, Is.Not.Null);

                using var client = new DNSClient(
                                       IPv4Address.Localhost,
                                       Port:           activeUDPUnicastSocket!.Value.Port,
                                       QueryTimeout:   TimeSpan.FromSeconds(2),
                                       UseQueryCache:  false
                                   );

                var response = await client.Query<A>(
                                         DomainName.Parse("api.example.test."),
                                         Timeout:      TimeSpan.FromSeconds(2),
                                         ForceUpdate:  true
                                     );

                Assert.That(response.ResponseCode,                          Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(),               Is.EqualTo(1));
                Assert.That(response.FilteredAnswers.First().IPv4Address,   Is.EqualTo(IPv4Address.Parse("127.0.0.42")));

            }
            finally
            {
                await server.Stop();
            }

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region TCPServer_Answers_From_Configured_Zone_On_Ephemeral_Port()

        [Test]
        public async Task TCPServer_Answers_From_Configured_Zone_On_Ephemeral_Port()
        {

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast    = false,
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = true,
                                 TCPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0))
                             }
                         );

            try
            {

                await server.Start();

                var activeTCPUnicastSocket = server.ActiveTCPUnicastSocket;

                Assert.That(activeTCPUnicastSocket, Is.Not.Null);

                using var client = new DNSTCPClient(
                                       IPv4Address.Localhost,
                                       Port:          activeTCPUnicastSocket!.Value.Port,
                                       QueryTimeout:  TimeSpan.FromSeconds(2)
                                   );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:  TimeSpan.FromSeconds(2)
                               );

                Assert.That(response.ResponseCode,              Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(),   Is.EqualTo(1));
                Assert.That(response.FilteredAnswers.First().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.42")));

            }
            finally
            {
                await server.Stop();
            }

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region UDPServer_Logs_Questions_Via_ILogger()

        [Test]
        public async Task UDPServer_Logs_Questions_Via_ILogger()
        {

            var logger = new TestLogger<DNSServer>();

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false
                             },
                             logger
                         );

            try
            {

                await server.Start();

                var activeUDPUnicastSocket = server.ActiveUDPUnicastSocket;

                Assert.That(activeUDPUnicastSocket, Is.Not.Null);

                using var client = new DNSClient(
                                       IPv4Address.Localhost,
                                       Port:           activeUDPUnicastSocket!.Value.Port,
                                       QueryTimeout:   TimeSpan.FromSeconds(2),
                                       UseQueryCache:  false
                                   );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:      TimeSpan.FromSeconds(2),
                                   ForceUpdate:  true
                               );

                Assert.That(response.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(
                    logger.Entries.Any(entry =>
                        entry.LogLevel == LogLevel.Debug &&
                        entry.Message.Contains("Question") &&
                        entry.Message.Contains("api.example.test", StringComparison.OrdinalIgnoreCase)
                    ),
                    Is.True
                );

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DNSClient_Cache_Returns_Stale_Record_Until_Invalidated()

        [Test]
        public async Task DNSClient_Cache_Returns_Stale_Record_Until_Invalidated()
        {

            var domainName = DNSServiceName.Parse("cache.example.test.");
            var zone       = new InMemoryDNSZone().
                                 Set(CreateARecord("cache.example.test.", "127.0.0.10", TimeSpan.FromMinutes(5)));

            var server     = new DNSServer(
                                 new AuthoritativeDNSRequestHandler(zone),
                                 new DNSServerOptions {
                                     UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                     EnableUDPMulticast  = false,
                                     EnableTCPUnicast    = false
                                 }
                             );

            try
            {

                await server.Start();

                var activeUDPUnicastSocket = server.ActiveUDPUnicastSocket;

                Assert.That(activeUDPUnicastSocket, Is.Not.Null);

                using var client = new DNSClient(
                                       IPv4Address.Localhost,
                                       Port:           activeUDPUnicastSocket!.Value.Port,
                                       QueryTimeout:   TimeSpan.FromSeconds(2),
                                       UseQueryCache:  true
                                   );

                var firstResponse = await client.Query<A>(
                                        DomainName.Parse("cache.example.test."),
                                        Timeout:  TimeSpan.FromSeconds(2)
                                    );

                Assert.That(firstResponse.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(firstResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.10")));

                zone.Set(CreateARecord("cache.example.test.", "127.0.0.11", TimeSpan.FromMinutes(5)));

                var cachedResponse = await client.Query<A>(
                                         DomainName.Parse("cache.example.test."),
                                         Timeout:  TimeSpan.FromSeconds(2)
                                     );

                Assert.That(cachedResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.10")));
                Assert.That(client.RemoveFromCache(domainName), Is.True);

                var refreshedResponse = await client.Query<A>(
                                            DomainName.Parse("cache.example.test."),
                                            Timeout:  TimeSpan.FromSeconds(2)
                                        );

                Assert.That(refreshedResponse.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(refreshedResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.11")));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DNSClient_ForceUpdate_Sees_Runtime_Zone_Changes()

        [Test]
        public async Task DNSClient_ForceUpdate_Sees_Runtime_Zone_Changes()
        {

            var zone   = new InMemoryDNSZone().
                             Set(CreateARecord("force-update.example.test.", "127.0.0.20", TimeSpan.FromMinutes(5)));

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(zone),
                             new DNSServerOptions {
                                 UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false
                             }
                         );

            try
            {

                await server.Start();

                var activeUDPUnicastSocket = server.ActiveUDPUnicastSocket;

                Assert.That(activeUDPUnicastSocket, Is.Not.Null);

                using var client = new DNSClient(
                                       IPv4Address.Localhost,
                                       Port:           activeUDPUnicastSocket!.Value.Port,
                                       QueryTimeout:   TimeSpan.FromSeconds(2),
                                       UseQueryCache:  true
                                   );

                var firstResponse = await client.Query<A>(
                                        DomainName.Parse("force-update.example.test."),
                                        Timeout:  TimeSpan.FromSeconds(2)
                                    );

                Assert.That(firstResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.20")));

                zone.Set(CreateARecord("force-update.example.test.", "127.0.0.21", TimeSpan.FromMinutes(5)));

                var forceUpdatedResponse = await client.Query<A>(
                                           DomainName.Parse("force-update.example.test."),
                                           Timeout:      TimeSpan.FromSeconds(2),
                                           ForceUpdate:  true
                                       );

                Assert.That(forceUpdatedResponse.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(forceUpdatedResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.21")));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region DNSClient_Cache_Expires_And_Requeries_Server()

        [Test]
        public async Task DNSClient_Cache_Expires_And_Requeries_Server()
        {

            var zone   = new InMemoryDNSZone().
                             Set(CreateARecord("expires.example.test.", "127.0.0.30", TimeSpan.FromSeconds(1)));

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(zone),
                             new DNSServerOptions {
                                 UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false
                             }
                         );

            try
            {

                await server.Start();

                var activeUDPUnicastSocket = server.ActiveUDPUnicastSocket;

                Assert.That(activeUDPUnicastSocket, Is.Not.Null);

                using var client = new DNSClient(
                                       IPv4Address.Localhost,
                                       Port:           activeUDPUnicastSocket!.Value.Port,
                                       QueryTimeout:   TimeSpan.FromSeconds(2),
                                       UseQueryCache:  true
                                   );

                var firstResponse = await client.Query<A>(
                                        DomainName.Parse("expires.example.test."),
                                        Timeout:  TimeSpan.FromSeconds(2)
                                    );

                Assert.That(firstResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.30")));

                zone.Set(CreateARecord("expires.example.test.", "127.0.0.31", TimeSpan.FromMinutes(5)));

                await Task.Delay(TimeSpan.FromMilliseconds(1500));

                var refreshedResponse = await client.Query<A>(
                                            DomainName.Parse("expires.example.test."),
                                            Timeout:  TimeSpan.FromSeconds(2)
                                        );

                Assert.That(refreshedResponse.ResponseCode, Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(refreshedResponse.FilteredAnswers.Single().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.31")));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region TLSServer_Start_Requires_ServerCertificate()

        [Test]
        public void TLSServer_Start_Requires_ServerCertificate()
        {

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast    = false,
                                 EnableUDPMulticast  = false,
                                 EnableTCPUnicast    = false,
                                 EnableTLSUnicast    = true,
                                 TLSUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0))
                             }
                         );

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await server.Start()
            );

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

        #region TLSServer_Answers_From_Configured_Zone_On_Ephemeral_Port()

        [Test]
        public async Task TLSServer_Answers_From_Configured_Zone_On_Ephemeral_Port()
        {

            using var certificate = CreateSelfSignedServerCertificate();

            var server = new DNSServer(
                             new AuthoritativeDNSRequestHandler(CreateTestZone()),
                             new DNSServerOptions {
                                 EnableUDPUnicast     = false,
                                 EnableUDPMulticast   = false,
                                 EnableTCPUnicast     = false,
                                 EnableTLSUnicast     = true,
                                 TLSUnicastSocket     = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                 TLSServerCertificate = certificate
                             }
                         );

            try
            {

                await server.Start();

                var activeTLSUnicastSocket = server.ActiveTLSUnicastSocket;

                Assert.That(activeTLSUnicastSocket, Is.Not.Null);

                await using var client = new DNSTLSClient(
                                             IPv4Address.Localhost,
                                             TCPPort:                     activeTLSUnicastSocket!.Value.Port,
                                             QueryTimeout:                TimeSpan.FromSeconds(2),
                                             RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success()
                                         );

                var response = await client.Query<A>(
                                   DomainName.Parse("api.example.test."),
                                   Timeout:  TimeSpan.FromSeconds(2)
                               );

                Assert.That(response.ResponseCode,              Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.FilteredAnswers.Count(),   Is.EqualTo(1));
                Assert.That(response.FilteredAnswers.First().IPv4Address, Is.EqualTo(IPv4Address.Parse("127.0.0.42")));

            }
            finally
            {
                await server.Stop();
            }

            Assert.That(server.IsRunning, Is.False);

        }

        #endregion

    }

}
