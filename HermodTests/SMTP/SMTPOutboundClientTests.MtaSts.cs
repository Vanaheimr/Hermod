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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// MTA-STS (RFC 8461): which TXT records and policies count, how a policy is fetched and
    /// kept, which MX hosts it allows, and what the relay does under it. The findings M-1 to M-6
    /// of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        private const String PolicyDomain = "next.example";

        private const String EnforcePolicy = "version: STSv1\r\nmode: enforce\r\nmx: mail.next.example\r\nmx: *.mx.next.example\r\nmax_age: 604800\r\n";

        /// <summary>
        /// A DNS client with the _mta-sts TXT records and MX records a test gives it.
        /// </summary>
        private sealed class PolicyDns : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            public volatile String[]  Txt = [ "v=STSv1; id=20261004;" ];
            public          String[]  Mx  = [];

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var answers = new List<IDNSResourceRecord>();

                if (Types.Contains(DNSResourceRecordTypes.TXT) && Name.TrimEnd('.') == $"_mta-sts.{PolicyDomain}")
                    answers.AddRange(Txt.Select(text => new TXT(DomainName.ParseLenient(Name), DNSQueryClasses.IN, TimeSpan.FromHours(1), text)));

                if (Types.Contains(DNSResourceRecordTypes.MX) && Name.TrimEnd('.') == PolicyDomain)
                    answers.AddRange(Mx.Select(host => new MX(DomainName.Parse(PolicyDomain), DNSQueryClasses.IN, TimeSpan.FromHours(1), 10, DomainName.Parse(host))));

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        /// <summary>
        /// The Policy Host: answers https://mta-sts.next.example/.well-known/mta-sts.txt as told.
        /// </summary>
        private sealed class PolicyHost : HttpMessageHandler
        {

            public volatile String          Policy        = EnforcePolicy;
            public volatile String          ContentType   = "text/plain";
            public          HttpStatusCode  Status        = HttpStatusCode.OK;
            public          Uri?            RedirectedTo;
            public          Int32           Requests;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage Request, CancellationToken CancellationToken)
            {

                Interlocked.Increment(ref Requests);

                Assert.That(Request.RequestUri, Is.EqualTo(new Uri($"https://mta-sts.{PolicyDomain}/.well-known/mta-sts.txt")));

                var response = new HttpResponseMessage(Status) { Content = new StringContent(Policy, Encoding.UTF8, ContentType) };

                if (Status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found)
                    response.Headers.Location = new Uri($"https://elsewhere.example/policy.txt");

                // As a handler that follows redirects would hand it back: from where it ended up.
                response.RequestMessage = RedirectedTo is not null
                                              ? new HttpRequestMessage(HttpMethod.Get, RedirectedTo)
                                              : Request;

                return Task.FromResult(response);

            }

        }

        private static async Task<MtaStsPolicy> PolicyFor(PolicyDns Dns, PolicyHost Host)
        {
            using var resolver = new MtaStsResolver(Dns, new QuietLogger(), Host);
            return await resolver.GetPolicyAsync(PolicyDomain);
        }

        #endregion


        #region M-1: MX host validation

        [TestCase("*.example.com",    "mail.example.com",      true)]
        [TestCase("*.example.com",    "MAIL.Example.COM.",     true)]
        [TestCase("mail.example.com", "mail.example.com",      true)]
        [TestCase("*.example.com",    "example.com",           false)]
        [TestCase("*.example.com",    "foo.bar.example.com",   false)]
        [TestCase("*.example.com",    "mailexample.com",       false)]
        [TestCase("mail.example.com", "mail.example.com.evil", false)]
        [Description("M-1 - RFC 8461 §4.1: \"*\" matches exactly the left-most label")]
        public void An_mx_pattern_matches_as_RFC_8461_says(String Pattern, String MxHost, Boolean Matches)
        {
            Assert.That(new MtaStsPolicy { Mode = MtaStsMode.Enforce, MxPatterns = [ Pattern ], MaxAge = TimeSpan.FromDays(1) }.MatchesMx(MxHost),
                        Is.EqualTo(Matches));
        }

        #endregion

        #region M-2: the TXT record

        [TestCase(new[] { "v=STSv1; id=20261004;" },                                   MtaStsMode.Enforce, TestName = "TXT: a valid record")]
        [TestCase(new[] { "v=STSv1;id=20261004" },                                     MtaStsMode.Enforce, TestName = "TXT: no spaces, no trailing delimiter")]
        [TestCase(new[] { "v=STSv1; id=20261004; ext_1=x", "google-site-verification=abc" }, MtaStsMode.Enforce, TestName = "TXT: an extension, and a record of another kind next to it")]
        [TestCase(new[] { "id=20261004; v=STSv1;" },                                   MtaStsMode.None,    TestName = "TXT: not beginning with v=STSv1")]
        [TestCase(new[] { "v=STSv1; id=20261004;", "v=STSv1; id=20261005;" },         MtaStsMode.None,    TestName = "TXT: two records")]
        [TestCase(new[] { "v=STSv1;" },                                                MtaStsMode.None,    TestName = "TXT: no id")]
        [TestCase(new[] { "v=STSv1; id=2026-10-04;" },                                 MtaStsMode.None,    TestName = "TXT: an id with a dash")]
        [TestCase(new[] { "v=STSv1; id=123456789012345678901234567890123;" },          MtaStsMode.None,    TestName = "TXT: an id of 33 characters")]
        [TestCase(new[] { "v=STSv10; id=20261004;" },                                  MtaStsMode.None,    TestName = "TXT: another version")]
        [Description("M-2 - RFC 8461 §3.1: records not beginning with \"v=STSv1;\" are discarded; not exactly one left, or one syntactically invalid, means no policy")]
        public async Task Only_one_valid_TXT_record_announces_a_policy(String[] Txt, MtaStsMode Mode)
        {

            var host   = new PolicyHost();
            var policy = await PolicyFor(new PolicyDns { Txt = Txt }, host);

            Assert.That(policy.Mode, Is.EqualTo(Mode));

            if (Mode == MtaStsMode.None)
                Assert.That(host.Requests, Is.Zero, "no TXT record, no HTTPS request");

        }

        #endregion

        #region M-3: the policy

        [TestCase("version: STSv1\nmode: enforce\nmx: mail.next.example\nmax_age: 604800",                      MtaStsMode.Enforce, TestName = "Policy: LF line ends")]
        [TestCase("version: STSv1\r\nmode: testing\r\nmx: mail.next.example\r\nmax_age: 86400\r\nfoo: bar\r\n", MtaStsMode.Testing, TestName = "Policy: an unknown field is ignored")]
        [TestCase("version: STSv1\r\nmode: none\r\nmax_age: 86400\r\n",                                         MtaStsMode.None,    TestName = "Policy: mode none needs no mx")]
        [TestCase("version: STSv1\r\nmode: enforce\r\nmode: none\r\nmx: mail.next.example\r\nmax_age: 86400\r\n", MtaStsMode.Enforce, TestName = "Policy: a repeated field counts the first time")]
        [TestCase("mode: enforce\r\nmx: mail.next.example\r\nmax_age: 86400\r\n",                               MtaStsMode.None,    TestName = "Policy: no version")]
        [TestCase("version: STSv1\r\nmode: enforce\r\nmx: mail.next.example\r\n",                               MtaStsMode.None,    TestName = "Policy: no max_age")]
        [TestCase("version: STSv1\r\nmode: enforce\r\nmax_age: 86400\r\n",                                      MtaStsMode.None,    TestName = "Policy: enforce without mx")]
        [TestCase("version: STSv1\r\nmode: strict\r\nmx: mail.next.example\r\nmax_age: 86400\r\n",              MtaStsMode.None,    TestName = "Policy: an unknown mode")]
        [TestCase("version: STSv1\r\nmode: enforce\r\nmx: mail.next.example\r\nmax_age: soon\r\n",              MtaStsMode.None,    TestName = "Policy: max_age not a number")]
        [TestCase("<html><body>Not found</body></html>",                                                       MtaStsMode.None,    TestName = "Policy: not a policy")]
        [Description("M-3 - RFC 8461 §3.2: version, mode and max_age required once, mx at least once unless mode is none; a repeated field counts the first time, unknown ones are ignored")]
        public async Task Only_a_valid_policy_counts(String Policy, MtaStsMode Mode)
        {
            Assert.That((await PolicyFor(new PolicyDns(), new PolicyHost { Policy = Policy })).Mode, Is.EqualTo(Mode));
        }


        [Test(Description = "M-3 - RFC 8461 §3.2: max_age has a maximum of 31557600 seconds")]
        public async Task A_max_age_above_a_year_is_a_year()
        {

            var policy = await PolicyFor(new PolicyDns(), new PolicyHost { Policy = EnforcePolicy.Replace("604800", "9999999999") });

            Assert.That(policy.MaxAge, Is.EqualTo(TimeSpan.FromSeconds(MtaStsResolver.MaxMaxAge)));

        }


        [Test(Description = "M-3 - RFC 8461 §3.2: \"senders SHOULD validate that the media type is 'text/plain'\"")]
        public async Task A_policy_that_is_not_text_plain_does_not_count()
        {
            Assert.That((await PolicyFor(new PolicyDns(), new PolicyHost { ContentType = "text/html" })).Mode, Is.EqualTo(MtaStsMode.None));
        }

        #endregion

        #region M-4: redirects

        [Test(Description = "M-4 - RFC 8461 §3.3: \"Policies fetched via HTTPS are only valid if the HTTP response code is 200 (OK). HTTP 3xx redirects MUST NOT be followed\"")]
        public async Task A_redirect_is_not_a_policy()
        {
            Assert.That((await PolicyFor(new PolicyDns(), new PolicyHost { Status = HttpStatusCode.MovedPermanently })).Mode, Is.EqualTo(MtaStsMode.None));
        }


        [Test(Description = "M-4 - RFC 8461 §3.3: a policy that a handler of the operator's fetched by following a redirect is not taken")]
        public async Task A_policy_reached_through_a_redirect_is_not_taken()
        {
            Assert.That((await PolicyFor(new PolicyDns(), new PolicyHost { RedirectedTo = new Uri("https://elsewhere.example/policy.txt") })).Mode,
                        Is.EqualTo(MtaStsMode.None));
        }


        [Test(Description = "M-4 - RFC 8461 §3.3: the resolver's own handler follows no redirects")]
        public void The_default_handler_follows_no_redirects()
        {
            using var handler = (SocketsHttpHandler) MtaStsResolver.CreateDefaultHandler();
            Assert.That(handler.AllowAutoRedirect, Is.False);
        }

        #endregion

        #region M-5: a new policy is looked for; an enforce failure is transient

        [Test(Description = "M-5 - RFC 8461 §3, §5: a new id in the TXT record means a new policy - fetched, not the cached one used until its max_age")]
        public async Task A_new_policy_id_fetches_the_new_policy()
        {

            var dns  = new PolicyDns();
            var host = new PolicyHost();
            using var resolver = new MtaStsResolver(dns, new QuietLogger(), host);

            var first = await resolver.GetPolicyAsync(PolicyDomain);
            var again = await resolver.GetPolicyAsync(PolicyDomain);

            dns.Txt     = [ "v=STSv1; id=20261005;" ];
            host.Policy = "version: STSv1\r\nmode: none\r\nmax_age: 86400\r\n";

            var updated = await resolver.GetPolicyAsync(PolicyDomain);

            Assert.Multiple(() => {
                Assert.That(first.Mode,     Is.EqualTo(MtaStsMode.Enforce));
                Assert.That(again.Mode,     Is.EqualTo(MtaStsMode.Enforce));
                Assert.That(updated.Mode,   Is.EqualTo(MtaStsMode.None), "the policy of id 20261005");
                Assert.That(host.Requests,  Is.EqualTo(2),               "the same id is not fetched twice");
            });

        }


        [Test(Description = "M-5 - RFC 8461 §3.3: \"if no 'live' policy can be discovered via DNS or fetched via HTTPS, but a valid (non-expired) policy exists in the sender's cache, the sender MUST apply that cached policy\"")]
        public async Task The_cached_policy_applies_when_no_live_one_can_be_had()
        {

            var dns  = new PolicyDns();
            var host = new PolicyHost();
            using var resolver = new MtaStsResolver(dns, new QuietLogger(), host);

            await resolver.GetPolicyAsync(PolicyDomain);

            dns.Txt     = [ "v=STSv1; id=20261005;" ];
            host.Status = HttpStatusCode.InternalServerError;
            var fetchFails = await resolver.GetPolicyAsync(PolicyDomain);

            dns.Txt     = [];
            var noTxt   = await resolver.GetPolicyAsync(PolicyDomain);

            Assert.Multiple(() => {
                Assert.That(fetchFails.Mode, Is.EqualTo(MtaStsMode.Enforce));
                Assert.That(noTxt.Mode,      Is.EqualTo(MtaStsMode.Enforce));
            });

        }


        [Test(Description = "M-5 - RFC 8461 §5: under enforce, no MX the policy allows is no reason to fail for good - \"MTAs SHOULD treat such failures as transient errors\"")]
        public async Task No_allowed_MX_is_a_temporary_failure()
        {

            var client = new SMTPOutboundClient(new SmtpOutboundConfig { LocalHostname = "relay.hermod.test", ConnectTimeoutMs = 3_000, MtaStsHttpHandler = new PolicyHost() },
                                                null,
                                                new PolicyDns { Mx = [ "mx.elsewhere.example" ] },
                                                new QuietLogger());

            var result = await client.SendAsync(PolicyDomain, "sender@client.example", [ "you@next.example" ], "Subject: sts\r\n\r\nhello\r\n");

            Assert.Multiple(() => {
                Assert.That(result.Status,        Is.EqualTo(SendStatus.TempFail), result.ResponseText);
                Assert.That(result.ResponseText,  Does.Contain("MTA-STS"));
            });

        }

        #endregion

        #region M-6: through a smart host

        [Test(Description = "M-6 - RFC 8461 §3.4: \"When sending mail via a 'smart host' ... compliant senders MUST treat the smart host domain as the Policy Domain\" - the recipient domain's policy is not applied to the smart host")]
        public async Task Through_a_smart_host_the_recipient_domains_policy_does_not_apply()
        {

            using var nextHop = new NextHop();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname      = "relay.hermod.test",
                                                    SmartHost          = "127.0.0.1",
                                                    SmartHostPort      = nextHop.Port,
                                                    ConnectTimeoutMs   = 3_000,
                                                    ReadTimeoutMs      = 5_000,
                                                    MtaStsHttpHandler  = new PolicyHost()
                                                },
                                                null,
                                                new PolicyDns(),
                                                new QuietLogger());

            var result = await client.SendAsync(PolicyDomain, "sender@client.example", [ "you@next.example" ], "Subject: sts\r\n\r\nhello\r\n");

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success), "the smart host has no policy of its own; next.example's names other hosts: " + result.ResponseText);

        }

        #endregion

    }

}
