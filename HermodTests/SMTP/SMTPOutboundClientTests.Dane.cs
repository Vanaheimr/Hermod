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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// DANE (RFC 7672) on the relay: a smart host "localhost" in a zone signed for the test, with the
    /// test's trust anchor, and a next hop whose certificate the TLSA records describe. The
    /// findings N-1 to N-4 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        /// <summary>
        /// A DNS client for a zone signed with a key of its own, whose DS is the trust anchor.
        /// </summary>
        private sealed class SignedDns : IDNSClientWithDNSSEC, IDisposable
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<(String, DNSResourceRecordTypes), IDNSResourceRecord[]>  answers   = [];
            private readonly Dictionary<(String, DNSResourceRecordTypes), DNSResponseCodes>      failures  = [];
            private readonly HashSet   <(String, DNSResourceRecordTypes)>                        timeouts  = [];
            private readonly Dictionary<(String, DNSResourceRecordTypes), IDNSResourceRecord[]>  proofs    = [];
            private readonly String                                                              zone;

            public DNSSECSigningKey  Key          { get; }
            public DS                TrustAnchor  { get; }
            public Boolean           DnssecOK     { get; set; }

            public SignedDns(String Zone = "localhost", Boolean SignedAddress = true)
            {
                zone         = Zone;
                Key          = DNSSECSigningKey.Generate(DomainName.ParseLenient(Zone), 13, KeySigningKey: true);
                TrustAnchor  = Key.DelegationSigner();
                Answer(Zone, DNSResourceRecordTypes.DNSKEY, true, Key.DNSKEY);
                Answer(Zone, DNSResourceRecordTypes.A,      SignedAddress, new A(DomainName.Parse(Zone), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("127.0.0.1")));
            }

            public SignedDns Answer(String Name, DNSResourceRecordTypes Type, Boolean Signed, params IDNSResourceRecord[] RRSet)
            {
                answers[(Name.TrimEnd('.').ToLowerInvariant(), Type)] =
                    Signed && RRSet.Length > 0
                        ? [ .. RRSet, DNSSECZoneSigner.SignRRSet(RRSet, Key, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1)) ]
                        : RRSet;
                return this;
            }

            public SignedDns Fail(String Name, DNSResourceRecordTypes Type, DNSResponseCodes Code = DNSResponseCodes.ServerFailure)
            {
                failures[(Name.ToLowerInvariant(), Type)] = Code;
                return this;
            }

            public SignedDns TimeOut(String Name, DNSResourceRecordTypes Type)
            {
                timeouts.Add((Name.ToLowerInvariant(), Type));
                return this;
            }

            /// <summary>
            /// Answer a name that does not exist as a signed zone does: NXDOMAIN, with the zone's NSEC
            /// record and its signature as the proof (RFC 4035 §3.1.3.2).
            /// </summary>
            public SignedDns NoSuchName(String Name, DNSResourceRecordTypes Type)
            {
                var signedZone = DNSSECZoneSigner.Sign([ .. answers.Values.SelectMany(rrset => rrset).Where(record => record.Type is not DNSResourceRecordTypes.RRSIG and not DNSResourceRecordTypes.DNSKEY) ],
                                                       DomainName.ParseLenient(zone), [ Key ]);
                proofs[(Name.ToLowerInvariant(), Type)] = [ .. signedZone.Where(record => record.Type == DNSResourceRecordTypes.NSEC ||
                                                                                          record is RRSIG rrsig && rrsig.TypeCovered == DNSResourceRecordTypes.NSEC) ];
                failures[(Name.ToLowerInvariant(), Type)] = DNSResponseCodes.NameError;
                return this;
            }

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name        = Name.TrimEnd('.').ToLowerInvariant();
                var records     = new List<IDNSResourceRecord>();
                var authorities = new List<IDNSResourceRecord>();
                var code        = DNSResponseCodes.NoError;

                foreach (var type in Types)
                {
                    if (timeouts.Contains((name, type)))
                        throw new TimeoutException($"{Name} {type} timed out");
                    if (failures.TryGetValue((name, type), out var failure))
                        code = failure;
                    else if (answers.TryGetValue((name, type), out var rrset))
                        records.AddRange(rrset);
                    if (proofs.TryGetValue((name, type), out var proof))
                        authorities.AddRange(proof);
                }

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, code,
                                                   records, authorities, [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public void Dispose()
                => Key.Dispose();

            public ValueTask DisposeAsync()
            {
                Key.Dispose();
                return ValueTask.CompletedTask;
            }

        }

        /// <summary>
        /// A trust anchor - an intermediate CA under a root of its own, as a public CA's intermediate
        /// is - and a server certificate it issued for the given name. The anchor is what the server
        /// sends along: a self-signed root is not sent on every platform (.NET on Linux leaves it
        /// out of the chain), an intermediate is.
        /// </summary>
        private static (X509Certificate2 TrustAnchor, X509Certificate2 Server) IssuedBy(String ServerName)
        {

            using var rootKey     = RSA.Create(2048);
            var rootRequest       = new CertificateRequest("CN=DANE test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var root        = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddDays(3));

            using var caKey       = RSA.Create(2048);
            var caRequest         = new CertificateRequest("CN=DANE trust anchor", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var caIssued    = caRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2), RandomNumberGenerator.GetBytes(8));
            using var ca          = caIssued.CopyWithPrivateKey(caKey);

            using var key     = RSA.Create(2048);
            var request       = new CertificateRequest($"CN={ServerName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names         = new SubjectAlternativeNameBuilder();
            names.AddDnsName(ServerName);
            request.CertificateExtensions.Add(names.Build());
            using var issued  = request.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(8));
            using var server  = issued.CopyWithPrivateKey(key);

            return (X509CertificateLoader.LoadCertificate(ca.RawData),
                    X509CertificateLoader.LoadPkcs12(server.Export(X509ContentType.Pfx), null));

        }

        private static TLSA Tlsa(UInt16 Port, TLSA_CertificateUsage Usage, X509Certificate2 Certificate)
            => new (DomainName.ParseLenient($"_{Port}._tcp.localhost"), DNSQueryClasses.IN, TimeSpan.FromHours(1),
                    (Byte) Usage, (Byte) TLSA_Selector.SubjectPublicKeyInfo, (Byte) TLSA_MatchingType.SHA256,
                    SHA256.HashData(Certificate.PublicKey.ExportSubjectPublicKeyInfo()));

        private static async Task<SendResult> SendWithDane(NextHop NextHop, SignedDns Dns)
        {

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname       = "relay.hermod.test",
                                                    SmartHost           = "localhost",
                                                    SmartHostPort       = NextHop.Port,
                                                    ConnectTimeoutMs    = 3_000,
                                                    ReadTimeoutMs       = 5_000,
                                                    WriteTimeoutMs      = 3_000,
                                                    EnableDane          = true,
                                                    DnssecTrustAnchors  = [ Dns.TrustAnchor ]
                                                },
                                                null,
                                                Dns,
                                                new QuietLogger());

            var result = await client.SendAsync("next.example", "sender@client.example", [ "you@next.example" ], "Subject: dane\r\n\r\nhello\r\n", ct: cts.Token);

            await Task.Delay(100);
            return result;

        }

        private static Boolean Delivered(NextHop NextHop)
            => NextHop.Commands.Any(command => command.StartsWith("MAIL", StringComparison.OrdinalIgnoreCase));

        #endregion


        [Test(Description = "RFC 7672 §3.1.1: a DANE-EE(3) record authenticates the certificate - a guard for the signed test zone")]
        public async Task DANE_EE_authenticates_the_next_hop()
        {

            var (_, server) = IssuedBy("whatever.example");
            using var nextHop = new NextHop(Certificate: server);
            using var dns     = new SignedDns();
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, Tlsa(nextHop.Port, TLSA_CertificateUsage.DANE_EE, server));

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,    Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(nextHop.TlsUsed,  Is.True);
            });

        }

        #region N-1: a failed TLSA lookup defers delivery

        [TestCase(false, TestName = "N-1: TLSA lookup fails with SERVFAIL")]
        [TestCase(true,  TestName = "N-1: TLSA lookup times out")]
        [Description("RFC 7672 §2.1.2: \"If any DNS queries used to locate TLSA records fail ... the SMTP client MUST treat that server as unreachable and MUST NOT deliver the message via that server\"")]
        public async Task A_failed_TLSA_lookup_defers_delivery(Boolean Timeout)
        {

            using var nextHop = new NextHop();
            using var dns     = new SignedDns();

            if (Timeout) dns.TimeOut($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA);
            else         dns.Fail   ($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA);

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), result.ResponseText);
                Assert.That(Delivered(nextHop),  Is.False, "not delivered via that server");
            });

        }


        [Test(Description = "RFC 7672 §2.2.2: a host whose address records are not signed is not asked for TLSA records - a SERVFAIL there, as nameservers of some large providers give, does not hold the mail")]
        public async Task An_unsigned_host_is_not_asked_for_TLSA_records()
        {

            using var nextHop = new NextHop();
            using var dns     = new SignedDns(SignedAddress: false);
            dns.Fail($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA);

            var result = await SendWithDane(nextHop, dns);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

        }

        #endregion

        #region N-2: no TLSA records in a signed zone needs a proof

        [Test(Description = "RFC 7672 §2.1.1, RFC 4035 §5.4: in a signed zone \"no TLSA records\" is a fact only with a validated denial of existence - an empty answer without one is what stripping the records produces")]
        public async Task An_empty_TLSA_answer_without_proof_in_a_signed_zone_defers_delivery()
        {

            using var nextHop = new NextHop();
            using var dns     = new SignedDns();

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), result.ResponseText);
                Assert.That(Delivered(nextHop),  Is.False);
            });

        }


        [Test(Description = "RFC 7672 §2.2: with a validated denial of the TLSA records, DANE does not apply and the message goes out with opportunistic TLS - the everyday case of a signed zone without DANE")]
        public async Task A_proven_absence_of_TLSA_records_is_no_DANE()
        {

            using var nextHop = new NextHop();
            using var dns     = new SignedDns();
            dns.NoSuchName($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA);

            var result = await SendWithDane(nextHop, dns);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

        }

        #endregion

        #region N-3: DANE-TA needs the name

        [Test(Description = "RFC 7672 §3.2.2: \"With DANE-TA(2), the server certificate MUST contain a name that matches one of the reference identifiers\" - a certificate of the trust anchor for another name does not do")]
        public async Task DANE_TA_refuses_a_certificate_for_another_name()
        {

            var (anchor, server) = IssuedBy("evil.example");
            using var nextHop = new NextHop(Certificate: server, Chain: [ anchor ]);
            using var dns     = new SignedDns();
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, Tlsa(nextHop.Port, TLSA_CertificateUsage.DANE_TA, anchor));

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), result.ResponseText);
                Assert.That(Delivered(nextHop),  Is.False);
            });

        }


        [Test(Description = "RFC 7672 §3.2.2, §3.2.3: a certificate of the trust anchor for the TLSA base domain is accepted (a guard for N-3)")]
        public async Task DANE_TA_accepts_a_certificate_for_the_host()
        {

            var (anchor, server) = IssuedBy("localhost");
            using var nextHop = new NextHop(Certificate: server, Chain: [ anchor ]);
            using var dns     = new SignedDns();
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, Tlsa(nextHop.Port, TLSA_CertificateUsage.DANE_TA, anchor));

            var result = await SendWithDane(nextHop, dns);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);

        }

        #endregion

        #region N-4: secure records, none usable

        [Test(Description = "RFC 7672 §2.2: with a secure TLSA RRset whose records are all unusable, \"Any connection to the MTA MUST be made via TLS, but authentication is not required\"")]
        public async Task Unusable_TLSA_records_need_TLS_but_no_authentication()
        {

            var (_, server) = IssuedBy("whatever.example");
            using var nextHop = new NextHop(Certificate: server);
            using var dns     = new SignedDns();
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, Tlsa(nextHop.Port, TLSA_CertificateUsage.PKIX_EE, server));

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,    Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(nextHop.TlsUsed,  Is.True);
            });

        }


        [Test(Description = "RFC 7672 §2.2.3: with secure TLSA records, usable or not, \"The SMTP client MUST NOT deliver mail via the corresponding host unless a TLS session is negotiated via STARTTLS\" (a guard for N-4)")]
        public async Task Unusable_TLSA_records_still_need_TLS()
        {

            var (_, server) = IssuedBy("whatever.example");
            using var nextHop = new NextHop();
            using var dns     = new SignedDns();
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, Tlsa(nextHop.Port, TLSA_CertificateUsage.PKIX_EE, server));

            var result = await SendWithDane(nextHop, dns);

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), result.ResponseText);
                Assert.That(Delivered(nextHop),  Is.False);
            });

        }

        #endregion

    }

}
