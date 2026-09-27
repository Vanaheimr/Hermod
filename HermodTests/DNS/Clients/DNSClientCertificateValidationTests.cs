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

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// What a resolver asks about the certificate of a name server it reaches
    /// over TLS: its own check where it was given one, with the server it is
    /// about - and the machine's where it was not.
    /// </summary>
    /// <remarks>
    /// Against a DNS server of Hermod's own on the loopback, with a certificate
    /// that signed itself: one no machine trusts, which is the case a check of
    /// the resolver's own is for.
    /// </remarks>
    [TestFixture]
    public class DNSClientCertificateValidationTests
    {

        #region Data

        private X509Certificate2  certificate = default!;
        private DNSServer         server      = default!;
        private DNSServerConfig   nameServer  = default!;

        #endregion

        #region Setup / TearDown

        [SetUp]
        public async Task Setup()
        {

            certificate  = CreateSelfSignedServerCertificate();

            server       = new DNSServer(
                               new AuthoritativeDNSRequestHandler(
                                   new InMemoryDNSZone().
                                       Add(new A(
                                               DomainName. Parse("api.example.test."),
                                               DNSQueryClasses.IN,
                                               TimeSpan.   FromMinutes(5),
                                               IPv4Address.Parse("127.0.0.42")
                                           ))
                               ),
                               new DNSServerOptions {
                                   EnableUDPUnicast      = false,
                                   EnableUDPMulticast    = false,
                                   EnableTCPUnicast      = false,
                                   EnableTLSUnicast      = true,
                                   TLSUnicastSocket      = new IPSocket(IPv4Address.Localhost, IPPort.Parse(0)),
                                   TLSServerCertificate  = certificate
                               }
                           );

            await server.Start();

            nameServer   = new DNSServerConfig(
                               IPv4Address.Localhost,
                               server.ActiveTLSUnicastSocket!.Value.Port,
                               DNSTransport.TLS
                           );

        }

        [TearDown]
        public async Task TearDown()
        {

            await server.Stop();

            certificate.Dispose();

        }

        #endregion


        #region (helper) CreateSelfSignedServerCertificate()

        private static X509Certificate2 CreateSelfSignedServerCertificate()
        {

            using var rsa    = RSA.Create(2048);

            var request      = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names        = new SubjectAlternativeNameBuilder();

            names.AddDnsName  ("localhost");
            names.AddIpAddress(System.Net.IPAddress.Loopback);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));
            request.CertificateExtensions.Add(names.Build());

            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);

        }

        #endregion

        #region (helper) Resolver()

        /// <summary>
        /// A resolver asking the test's name server and nobody else, and never
        /// its cache.
        /// </summary>
        private DNSClient Resolver()

            => new ([ nameServer ],
                    QueryTimeout:             TimeSpan.FromSeconds(5),
                    SearchForIPv4DNSServers:  false,
                    SearchForIPv6DNSServers:  false,
                    UseQueryCache:            false) {
                   MaxRetries = 0
               };

        #endregion

        #region (helper) Answers(Resolver)

        private static async Task<String[]> Answers(DNSClient Resolver)
        {

            try
            {

                var info = await Resolver.Query(DomainName.Parse("api.example.test."),
                                                [ DNSResourceRecordTypes.A ],
                                                Timeout: TimeSpan.FromSeconds(5));

                return [.. info.Answers.OfType<A>().Select(answer => answer.IPv4Address.ToString())];

            }
            catch (Exception)
            {
                // A resolver that got no answer from the only server it may ask
                // has no answer, whichever way it says so.
                return [];
            }

        }

        #endregion


        #region ANameServerOverTLSIsJudgedByTheResolversCheck()

        /// <summary>
        /// The resolver's check is asked at the handshake, with the server it is
        /// about and the certificate that server showed - and where it believes
        /// the certificate, the name server is asked.
        /// </summary>
        [Test]
        public async Task ANameServerOverTLSIsJudgedByTheResolversCheck()
        {

            var asked = new List<(DNSServerConfig Server, String? Thumbprint, SslPolicyErrors Errors)>();

            using var resolver = Resolver();

            resolver.RemoteCertificateValidator = (server, shown, chain, errors) => {
                lock (asked)
                    asked.Add((server, shown?.Thumbprint, errors));
                return TLSValidationResult.Success();
            };

            var answers = await Answers(resolver);

            Assert.Multiple(() => {
                Assert.That(answers,                     Is.EqualTo(new[] { "127.0.0.42" }));
                Assert.That(asked,                       Is.Not.Empty, "the check was never asked");
                Assert.That(asked[0].Server,             Is.EqualTo(nameServer), "and not about the server it was asking");
                Assert.That(asked[0].Thumbprint,         Is.EqualTo(certificate.Thumbprint));
                Assert.That(asked[0].Errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors),
                            Is.True,
                            "a certificate that signed itself is one the machine does not trust, and the check is told so");
            });

        }

        #endregion

        #region ANameServerTheCheckRefusesIsNotAsked()

        [Test]
        public async Task ANameServerTheCheckRefusesIsNotAsked()
        {

            var asked = 0;

            using var resolver = Resolver();

            resolver.RemoteCertificateValidator = (server, shown, chain, errors) => {
                Interlocked.Increment(ref asked);
                return TLSValidationResult.Failed("Not the name server this resolver is held to.");
            };

            var answers = await Answers(resolver);

            Assert.Multiple(() => {
                Assert.That(asked,    Is.GreaterThan(0));
                Assert.That(answers,  Is.Empty, "a name server whose certificate was refused was asked all the same");
            });

        }

        #endregion

        #region WithoutACheckTheMachineJudges()

        /// <summary>
        /// A resolver nobody gave a check behaves as it did before there was one
        /// to give: the machine does not trust a certificate that signed itself,
        /// and the name server behind it is not asked.
        /// </summary>
        [Test]
        public async Task WithoutACheckTheMachineJudges()
        {

            using var resolver = Resolver();

            Assert.That(await Answers(resolver), Is.Empty);

        }

        #endregion

        #region ACheckGivenAfterTheResolverWasMadeIsTheOneAsked()

        /// <summary>
        /// Asked at the handshake, and not when the transport client was made:
        /// a check set on a resolver that exists already is the one its next
        /// handshake goes by.
        /// </summary>
        [Test]
        public async Task ACheckGivenAfterTheResolverWasMadeIsTheOneAsked()
        {

            using var resolver = Resolver();

            var before = await Answers(resolver);

            resolver.RemoteCertificateValidator = (server, shown, chain, errors) => TLSValidationResult.Success();

            var after  = await Answers(resolver);

            Assert.Multiple(() => {
                Assert.That(before,  Is.Empty);
                Assert.That(after,   Is.EqualTo(new[] { "127.0.0.42" }));
            });

        }

        #endregion

        #region AConnectionClosedOnPurposeIsJudgedAgain()

        /// <summary>
        /// A connection kept open is not asked about again - it was judged when
        /// it was made - until whoever changed what the server is held to closes
        /// it, and the next query makes a new one.
        /// </summary>
        [Test]
        public async Task AConnectionClosedOnPurposeIsJudgedAgain()
        {

            using var resolver  = Resolver();

            resolver.RemoteCertificateValidator = (server, shown, chain, errors) => TLSValidationResult.Success();

            var first           = await Answers(resolver);

            resolver.RemoteCertificateValidator = (server, shown, chain, errors) => TLSValidationResult.Failed("Held to something else now.");

            var keptOpen        = await Answers(resolver);
            var closed          = resolver.CloseConnection(nameServer);
            var afterwards      = await Answers(resolver);

            Assert.Multiple(() => {
                Assert.That(first,       Is.EqualTo(new[] { "127.0.0.42" }));
                Assert.That(keptOpen,    Is.EqualTo(new[] { "127.0.0.42" }), "the connection kept open was judged when it was made");
                Assert.That(closed,      Is.True);
                Assert.That(afterwards,  Is.Empty, "a new connection is judged by the check in effect now");
            });

        }

        #endregion

    }

}
