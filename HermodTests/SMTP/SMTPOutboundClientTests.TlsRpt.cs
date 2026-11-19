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
using System.Collections.Concurrent;
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
    /// What the relay records for TLS-RPT (RFC 8460) about a session: through the smart host
    /// "localhost", whose MTA-STS policy (RFC 8461 §3.4) the test serves, to the recipient
    /// domain next.example. The findings T-2 to T-4 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        private static String StsPolicyText(String Mode, String Mx = "localhost")
            => $"version: STSv1\r\nmode: {Mode}\r\nmx: {Mx}\r\nmax_age: 604800\r\n";

        /// <summary>
        /// A self-signed certificate for "localhost": trusted by nobody, and expired when so asked.
        /// </summary>
        private static X509Certificate2 SelfSignedLocalhost(Boolean Expired = false)
        {

            using var rsa  = RSA.Create(2048);
            var request    = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names      = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());

            using var cert = Expired
                                 ? request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1))
                                 : request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);

        }

        /// <param name="Mode">The mode of the smart host's MTA-STS policy, or null for none.</param>
        /// <param name="Certificate">The certificate of the smart host; null: it offers no STARTTLS.</param>
        /// <param name="Mx">The mx of the policy.</param>
        /// <param name="TrustCertificate">Trust any certificate, through a validator of the operator's own.</param>
        private static async Task<(SendResult Result, TlsRptEvent[] Events)> SendUnderPolicy(String?            Mode,
                                                                                            X509Certificate2?  Certificate,
                                                                                            String             Mx                = "localhost",
                                                                                            Boolean            TrustCertificate  = false)
        {

            using var nextHop = new NextHop(Certificate: Certificate);
            using var cts     = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var events        = new ConcurrentQueue<TlsRptEvent>();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname               = "relay.hermod.test",
                                                    SmartHost                   = "localhost",
                                                    SmartHostPort               = nextHop.Port,
                                                    ConnectTimeoutMs            = 3_000,
                                                    ReadTimeoutMs               = 5_000,
                                                    WriteTimeoutMs              = 3_000,
                                                    MtaStsHttpHandler           = new PolicyHost { Domain = "localhost", Policy = StsPolicyText(Mode ?? "none", Mx) },
                                                    RemoteCertificateValidator  = TrustCertificate ? (_, _, _, _) => true : null
                                                },
                                                null,
                                                new PolicyDns { Domain = "localhost", Txt = Mode is null ? [] : [ "v=STSv1; id=20261119;" ] },
                                                new QuietLogger(),
                                                events.Enqueue);

            var result = await client.SendAsync(PolicyDomain, "sender@client.example", [ "you@next.example" ], "Subject: tlsrpt\r\n\r\nhello\r\n", ct: cts.Token);

            await Task.Delay(100);
            return (result, [ .. events ]);

        }

        private static String Explain(SendResult Result, TlsRptEvent[] Events)
            => $"{Result.Status} {Result.ResponseCode} {Result.ResponseText}; events: {String.Join(", ", Events.Select(e => e.ToString()))}";

        #endregion


        #region T-3: testing mode reports what the policy would have refused

        [TestCase(false, "certificate-not-trusted",   TestName = "T-3: in testing mode an untrusted certificate is reported as certificate-not-trusted")]
        [TestCase(true,  "certificate-expired",       TestName = "T-3: in testing mode an expired certificate is reported as certificate-expired")]
        [Description("RFC 8461 §5: in testing mode \"Sending MTAs that also implement the TLSRPT specification [RFC8460] send a report indicating policy application failures ...; in any case, messages may be delivered as though there were no MTA-STS validation failure\"; RFC 8460 §4.3.1")]
        public async Task In_testing_mode_a_certificate_the_policy_refuses_is_reported_as_a_failure(Boolean Expired, String ResultType)
        {

            var (result, events) = await SendUnderPolicy("testing", SelfSignedLocalhost(Expired));

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),      "testing mode does not hold the mail back: " + Explain(result, events));
                Assert.That(events.Select(e => e.Success),         Is.EqualTo(new[] { false }),         Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { ResultType }),    Explain(result, events));
                Assert.That(events.Select(e => e.PolicyType),      Is.EqualTo(new[] { TlsRptPolicyType.Sts }));
            });

        }


        [Test(Description = "RFC 8461 §5, RFC 8460 §4.3.1: \"certificate-host-mismatch\" - \"the MX hostname does not match any identities listed in the subject alternative name\"; in testing mode the mail goes out all the same")]
        public async Task In_testing_mode_a_certificate_for_another_host_is_reported_as_a_host_mismatch()
        {

            var (result, events) = await SendUnderPolicy("testing", SelfSigned());     // CN=127.0.0.1, not localhost

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),                          Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { "certificate-host-mismatch" }),       Explain(result, events));
            });

        }


        [Test(Description = "RFC 8461 §5, RFC 8460 §4.3.1: \"starttls-not-supported\" - in testing mode the mail goes out in cleartext all the same, and the report says so")]
        public async Task In_testing_mode_a_next_hop_without_STARTTLS_is_reported_as_a_failure()
        {

            var (result, events) = await SendUnderPolicy("testing", Certificate: null);

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),                          Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { "starttls-not-supported" }),          Explain(result, events));
            });

        }


        [Test(Description = "RFC 8461 §4.1, §5: in testing mode an MX host the policy does not name is a policy application failure - delivered, and reported")]
        public async Task In_testing_mode_an_mx_host_the_policy_does_not_name_is_reported_as_a_failure()
        {

            var (result, events) = await SendUnderPolicy("testing", SelfSignedLocalhost(), Mx: "mail.next.example", TrustCertificate: true);

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),                          Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { "validation-failure" }),              Explain(result, events));
            });

        }


        [Test(Description = "RFC 8461 §5: in testing mode a session the policy is content with is a success - a guard")]
        public async Task In_testing_mode_a_valid_session_is_reported_as_a_success()
        {

            var (result, events) = await SendUnderPolicy("testing", SelfSignedLocalhost(), TrustCertificate: true);

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),  Explain(result, events));
                Assert.That(events.Select(e => e.Success),         Is.EqualTo(new[] { true }),      Explain(result, events));
            });

        }


        [Test(Description = "RFC 8461 §5: under enforce an expired certificate fails the policy - the delivery waits, and the report says why: \"certificate-expired\" (RFC 8460 §4.3.1)")]
        public async Task Under_enforce_an_expired_certificate_is_reported_as_expired()
        {

            var (result, events) = await SendUnderPolicy("enforce", SelfSignedLocalhost(Expired: true));

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.TempFail),                 Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { "certificate-expired" }),     Explain(result, events));
            });

        }


        [Test(Description = "RFC 8461 §5: under enforce an untrusted certificate fails the policy - the delivery waits, and the report has a failure - a guard")]
        public async Task Under_enforce_an_untrusted_certificate_is_a_reported_failure()
        {

            var (result, events) = await SendUnderPolicy("enforce", SelfSignedLocalhost());

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.TempFail),                 Explain(result, events));
                Assert.That(events.Select(e => e.FailureType),     Is.EqualTo(new[] { "certificate-not-trusted" }), Explain(result, events));
            });

        }


        [Test(Description = "RFC 7435: without a policy, TLS is opportunistic - an untrusted certificate is taken, and the session is a success of no-policy-found - a guard")]
        public async Task Without_a_policy_an_untrusted_certificate_is_still_a_success()
        {

            var (result, events) = await SendUnderPolicy(null, SelfSignedLocalhost());

            Assert.Multiple(() => {
                Assert.That(result.Status,                         Is.EqualTo(SendStatus.Success),                          Explain(result, events));
                Assert.That(events.Select(e => (e.PolicyType, e.Success)), Is.EqualTo(new[] { (TlsRptPolicyType.NoPolicyFound, true) }), Explain(result, events));
            });

        }

        #endregion

        #region T-4: through a smart host

        [Test(Description = "RFC 8460 §4.4: \"policy-domain\" is \"The Policy Domain against which the MTA-STS or DANE policy is defined\" - through a smart host its own (RFC 8461 §3.4), not the recipient's")]
        public async Task Through_a_smart_host_the_session_is_reported_to_the_smart_hosts_domain()
        {

            var (result, events) = await SendUnderPolicy("enforce", SelfSignedLocalhost());

            Assert.That(events.Select(e => e.PolicyDomain), Is.EqualTo(new[] { "localhost" }), Explain(result, events));

        }

        #endregion

        #region T-2: the policy applied

        [Test(Description = "RFC 8460 §4.4, §4.5: \"policy-string\" - for MTA-STS the policy \"declared by the receiving site\", one string per line")]
        public async Task Under_MTA_STS_the_policy_lines_are_the_policy_string()
        {

            var (result, events) = await SendUnderPolicy("testing", SelfSignedLocalhost(), TrustCertificate: true);

            Assert.That(events.Single().PolicyString,
                        Is.EqualTo(new[] { "version: STSv1", "mode: testing", "mx: localhost", "max_age: 604800" }),
                        Explain(result, events));

        }


        [Test(Description = "RFC 8460 §4.5: for DANE \"a JSON array of strings each representing the RDATA of a single TLSA resource record as a space-separated list of its four TLSA fields\"")]
        public async Task Under_DANE_the_TLSA_records_are_the_policy_string()
        {

            var (_, server) = IssuedBy("whatever.example");
            using var nextHop = new NextHop(Certificate: server);
            using var dns     = new SignedDns();
            var tlsa          = Tlsa(nextHop.Port, TLSA_CertificateUsage.DANE_EE, server);
            dns.Answer($"_{nextHop.Port}._tcp.localhost", DNSResourceRecordTypes.TLSA, true, tlsa);

            var events = new ConcurrentQueue<TlsRptEvent>();
            var result = await SendWithDane(nextHop, dns, events.Enqueue);

            Assert.Multiple(() => {
                Assert.That(result.Status,                          Is.EqualTo(SendStatus.Success), result.ResponseText);
                Assert.That(events.Select(e => e.PolicyType),       Is.EqualTo(new[] { TlsRptPolicyType.Tlsa }));
                Assert.That(events.Single().PolicyString,           Is.EqualTo(new[] { $"3 1 1 {Convert.ToHexString(tlsa.CertificateAssociationData)}" }));
            });

        }

        #endregion

    }

}
