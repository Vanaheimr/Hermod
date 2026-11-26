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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// How check_host() comes to its result (RFC 7208 §4 to §6): the record set, DNS errors,
    /// the syntax of the whole record, redirect= and exp=, ptr, the limits of mx and of void
    /// lookups, and clients on IPv4-mapped addresses. The findings V-5 to V-12 of
    /// SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class SpfEvaluationTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// Records by owner name and type, answers with an error code, and lookups that throw.
        /// </summary>
        private sealed class ZoneDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<(String, DNSResourceRecordTypes), List<IDNSResourceRecord>>  records  = [];
            private readonly Dictionary<(String, DNSResourceRecordTypes), DNSResponseCodes>          errors   = [];
            private readonly HashSet<(String, DNSResourceRecordTypes)>                                timeouts = [];

            public ZoneDNS Add(String Name, DNSResourceRecordTypes Type, IDNSResourceRecord Record)
            {
                var key = (Name, Type);
                if (!records.TryGetValue(key, out var list))
                    records[key] = list = [];
                list.Add(Record);
                return this;
            }

            public ZoneDNS Txt(String Name, String Text)
                => Add(Name, DNSResourceRecordTypes.TXT, new TXT(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Text));

            public ZoneDNS A(String Name, String Address)
                => Add(Name, DNSResourceRecordTypes.A, new A(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Parse(Address)));

            public ZoneDNS Mx(String Name, UInt16 Preference, String Host)
                => Add(Name, DNSResourceRecordTypes.MX, new MX(DomainName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Preference, DomainName.Parse(Host)));

            public ZoneDNS Ptr(String Name, String Target)
                => Add(Name, DNSResourceRecordTypes.PTR, new PTR(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), DNSServiceName.Parse(Target)));

            public ZoneDNS Error(String Name, DNSResourceRecordTypes Type, DNSResponseCodes Code = DNSResponseCodes.ServerFailure)
            {
                errors[(Name, Type)] = Code;
                return this;
            }

            public ZoneDNS Timeout(String Name, DNSResourceRecordTypes Type)
            {
                timeouts.Add((Name, Type));
                return this;
            }

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = new List<IDNSResourceRecord>();
                var code    = DNSResponseCodes.NoError;

                foreach (var type in Types)
                {

                    if (timeouts.Contains((name, type)))
                        throw new TimeoutException($"{name} {type}");

                    if (errors.TryGetValue((name, type), out var error))
                        code = error;

                    else if (records.TryGetValue((name, type), out var list))
                        answers.AddRange(list);

                }

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, code,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(ZoneDNS);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String Client = "192.0.2.25";

        private static async Task<SPFResult> CheckHost(ZoneDNS DNS, String Domain, String ClientAddress = Client)
            => (await new DNSVerifier(DNS, new QuietLogger()).CheckSpfAsync(Domain, System.Net.IPAddress.Parse(ClientAddress))).Result;

        #endregion


        #region DNS errors (V-5)

        [Test(Description = "RFC 7208 §4.4: a SERVFAIL or a timeout of the record lookup is \"temperror\"; NXDOMAIN is \"none\" (§4.3)")]
        public async Task A_DNS_error_of_the_record_lookup_is_a_temporary_error()
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(new ZoneDNS().Error  ("x.example", DNSResourceRecordTypes.TXT), "x.example"),                              Is.EqualTo(SPFResult.TempError));
                Assert.That(await CheckHost(new ZoneDNS().Timeout("x.example", DNSResourceRecordTypes.TXT), "x.example"),                              Is.EqualTo(SPFResult.TempError));
                Assert.That(await CheckHost(new ZoneDNS().Error  ("x.example", DNSResourceRecordTypes.TXT, DNSResponseCodes.NameError), "x.example"),  Is.EqualTo(SPFResult.None));
            });
        }

        [TestCase("v=spf1 a:mail.x.example -all",        "mail.x.example",   DNSResourceRecordTypes.A)]
        [TestCase("v=spf1 mx -all",                      "x.example",        DNSResourceRecordTypes.MX)]
        [TestCase("v=spf1 exists:e.x.example -all",      "e.x.example",      DNSResourceRecordTypes.A)]
        [TestCase("v=spf1 include:i.x.example -all",     "i.x.example",      DNSResourceRecordTypes.TXT)]
        [TestCase("v=spf1 redirect=r.x.example",         "r.x.example",      DNSResourceRecordTypes.TXT)]
        public async Task A_DNS_error_of_a_mechanism_is_a_temporary_error(String Record, String Name, DNSResourceRecordTypes Type)
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", Record).Error  (Name, Type), "x.example"), Is.EqualTo(SPFResult.TempError), "SERVFAIL");
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", Record).Timeout(Name, Type), "x.example"), Is.EqualTo(SPFResult.TempError), "timeout");
            });
        }

        [Test(Description = "RFC 7208 §5: NXDOMAIN in a mechanism's lookup is no records - evaluation goes on")]
        public async Task NXDOMAIN_in_a_mechanism_is_no_error()
        {
            var dns = new ZoneDNS().Txt("x.example", $"v=spf1 a:gone.x.example ip4:{Client} -all").
                                    Error("gone.x.example", DNSResourceRecordTypes.A, DNSResponseCodes.NameError);

            Assert.That(await CheckHost(dns, "x.example"), Is.EqualTo(SPFResult.Pass));
        }

        [Test(Description = "RFC 7208 §5.2: a temporary error inside an include: is the topmost result")]
        public async Task A_DNS_error_inside_an_include_is_the_topmost_result()
        {
            var dns = new ZoneDNS().Txt("x.example", "v=spf1 include:i.x.example -all").
                                    Txt("i.x.example", "v=spf1 a:mail.i.x.example -all").
                                    Error("mail.i.x.example", DNSResourceRecordTypes.A);

            Assert.That(await CheckHost(dns, "x.example"), Is.EqualTo(SPFResult.TempError));
        }

        #endregion

        #region The record set (V-6)

        [TestCase("v=spf1",                   true)]
        [TestCase("v=spf1 -all",              true)]
        [TestCase("V=SPF1 -all",              true,  Description = "ABNF strings are case-insensitive")]
        [TestCase("v=spf10 -all",             false)]
        [TestCase("v=spf1-all",               false)]
        [TestCase("spf2.0/pra -all",          false)]
        public void Which_TXT_records_are_SPF_records(String Record, Boolean IsSpf)
        {
            Assert.That(DNSVerifier.IsSpfRecord(Record), Is.EqualTo(IsSpf));
        }

        [Test(Description = "RFC 7208 §4.5: two SPF records are \"permerror\"; a v=spf10 record and other TXT records are not SPF records")]
        public async Task Only_one_record_of_exactly_v_spf1_counts()
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", $"v=spf1 ip4:{Client} -all").Txt("x.example", "v=spf1 -all"), "x.example"),  Is.EqualTo(SPFResult.PermError));
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", $"v=spf10 ip4:{Client} -all"), "x.example"),                                 Is.EqualTo(SPFResult.None));
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", "v=spf10 -all").Txt("x.example", $"v=spf1 ip4:{Client} -all"), "x.example"), Is.EqualTo(SPFResult.Pass));
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", "site-verification=4711").Txt("x.example", $"v=spf1 ip4:{Client} -all"), "x.example"), Is.EqualTo(SPFResult.Pass));
            });
        }

        #endregion

        #region Syntax (V-7, V-8)

        [TestCase("v=spf1 -all",                                         true)]
        [TestCase("v=spf1 ip4:192.0.2.0/24 ip6:2001:db8::/32 -all",      true)]
        [TestCase("v=spf1 ip4:127.0.0.1 ip6:::1 -all",                   true)]
        [TestCase("v=spf1 a mx a:mail.example.com/24 mx//64 a/24//64 -all", true)]
        [TestCase("v=spf1 include:_spf.example.com ~all",               true)]
        [TestCase("v=spf1 exists:%{i}._spf.%{d} -all",                  true)]
        [TestCase("v=spf1 exists:%{ir}.%{l1r+-}._spf.%{d} -all",        true)]
        [TestCase("v=spf1 ptr ptr:example.com ?all",                    true)]
        [TestCase("v=spf1 redirect=_spf.example.com",                   true)]
        [TestCase("v=spf1 -all exp=explain._spf.%{d}",                  true)]
        [TestCase("v=spf1 foo=bar foo.bar=%{c} -all",                   true,  Description = "unknown modifiers, any macro letter")]
        [TestCase("v=spf1 +all frobnicate:example.com",                 false, Description = "unknown mechanism")]
        [TestCase("v=spf1 ip4:192.0.2.1/33",                            false)]
        [TestCase("v=spf1 ip4:192.0.2",                                 false)]
        [TestCase("v=spf1 ip4:192.0.2.01",                              false, Description = "a leading zero")]
        [TestCase("v=spf1 ip4:192.0.2.256",                             false)]
        [TestCase("v=spf1 ip6:2001:db8::/129",                          false)]
        [TestCase("v=spf1 ip6:192.0.2.1",                               false)]
        [TestCase("v=spf1 a/33",                                        false)]
        [TestCase("v=spf1 include",                                     false)]
        [TestCase("v=spf1 include:",                                    false)]
        [TestCase("v=spf1 include:example",                             false, Description = "no domain-end")]
        [TestCase("v=spf1 all:example.com",                             false)]
        [TestCase("v=spf1 exists:%{x}.example.com",                     false, Description = "no such macro letter")]
        [TestCase("v=spf1 exists:%{c}.example.com",                     false, Description = "c only in explanations")]
        [TestCase("v=spf1 exists:%example.com",                         false)]
        [TestCase("v=spf1 redirect=a.example.com redirect=b.example.com", false)]
        [TestCase("v=spf1 exp=a.example.com exp=b.example.com -all",    false)]
        [TestCase("v=spf1 -all exp=",                                   false)]
        public void The_syntax_of_a_record(String Record, Boolean Valid)
        {
            Assert.That(DNSVerifier.IsValidSpfRecord(Record), Is.EqualTo(Valid));
        }

        [TestCase("v=spf1 +all frobnicate:example.com")]
        [TestCase($"v=spf1 ip4:{Client} ip4:192.0.2.1/33")]
        [TestCase($"v=spf1 ip4:192.0.2 ip4:{Client} -all")]
        [TestCase($"v=spf1 ip4:{Client} exp=a.example exp=b.example -all")]
        [TestCase("v=spf1 redirect=a.x.example redirect=b.x.example")]
        public async Task A_syntax_error_anywhere_is_a_permanent_error(String Record)
        {
            var dns = new ZoneDNS().Txt("x.example", Record).
                                    Txt("a.x.example", $"v=spf1 ip4:{Client} -all").
                                    Txt("b.x.example", "v=spf1 -all");

            Assert.That(await CheckHost(dns, "x.example"), Is.EqualTo(SPFResult.PermError));
        }

        [Test(Description = "RFC 7208 §4.6: the record an include: names is checked as a whole too")]
        public async Task A_syntax_error_in_an_included_record_is_a_permanent_error()
        {
            var dns = new ZoneDNS().Txt("x.example", "v=spf1 include:i.x.example -all").
                                    Txt("i.x.example", $"v=spf1 ip4:{Client} frobnicate");

            Assert.That(await CheckHost(dns, "x.example"), Is.EqualTo(SPFResult.PermError));
        }

        #endregion

        #region ptr (V-9)

        private static ZoneDNS Reverse(ZoneDNS DNS, String AddressOfName = Client)
            => DNS.Ptr("25.2.0.192.in-addr.arpa", "mail.x.example").
                   A  ("mail.x.example", AddressOfName);

        [Test(Description = "RFC 7208 §5.5: ptr matches a validated reverse name within the target, with or without domain-spec, and not one that is not validated or is elsewhere")]
        public async Task The_ptr_mechanism()
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(Reverse(new ZoneDNS().Txt("x.example",   "v=spf1 ptr -all")),             "x.example"),    Is.EqualTo(SPFResult.Pass));
                Assert.That(await CheckHost(Reverse(new ZoneDNS().Txt("y.example",   "v=spf1 ptr:x.example -all")),   "y.example"),    Is.EqualTo(SPFResult.Pass));
                Assert.That(await CheckHost(Reverse(new ZoneDNS().Txt("y.example",   "v=spf1 ptr -all")),             "y.example"),    Is.EqualTo(SPFResult.Fail),  "a name outside the domain");
                Assert.That(await CheckHost(Reverse(new ZoneDNS().Txt("x.example",   "v=spf1 ptr -all"), "192.0.2.99"), "x.example"),  Is.EqualTo(SPFResult.Fail),  "a name that is not validated");
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", "v=spf1 ptr -all").Error("25.2.0.192.in-addr.arpa", DNSResourceRecordTypes.PTR), "x.example"),
                            Is.EqualTo(SPFResult.Fail), "a DNS error of the PTR lookup does not match, and is no temperror");
            });
        }

        #endregion

        #region Limits (V-10, V-11)

        private static ZoneDNS MxHosts(Int32 Count)
        {
            var dns = new ZoneDNS().Txt("x.example", "v=spf1 mx -all").A("mx01.x.example", Client);
            for (var i = 1; i <= Count; i++)
                dns.Mx("x.example", (UInt16) i, $"mx{i:00}.x.example");
            return dns;
        }

        [Test(Description = "RFC 7208 §4.6.4: more than ten MX names are a \"permerror\" of the mx mechanism; ten are evaluated")]
        public async Task The_mx_mechanism_takes_ten_names_at_most()
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(MxHosts(10), "x.example"), Is.EqualTo(SPFResult.Pass));
                Assert.That(await CheckHost(MxHosts(11), "x.example"), Is.EqualTo(SPFResult.PermError));
            });
        }

        [Test(Description = "RFC 7208 §4.6.4: the third void lookup is a \"permerror\"")]
        public async Task Two_void_lookups_at_most()
        {
            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", $"v=spf1 a:v1.x.example a:v2.x.example ip4:{Client} -all"), "x.example"),                Is.EqualTo(SPFResult.Pass));
                Assert.That(await CheckHost(new ZoneDNS().Txt("x.example", $"v=spf1 a:v1.x.example a:v2.x.example a:v3.x.example ip4:{Client} -all"), "x.example"), Is.EqualTo(SPFResult.PermError));
            });
        }

        [Test(Description = "RFC 7208 §4.6.4: the eleventh DNS-querying term is a \"permerror\"")]
        public async Task Ten_DNS_querying_terms_at_most()
        {

            static ZoneDNS Terms(Int32 Count)
            {
                var dns = new ZoneDNS().Txt("x.example", "v=spf1 " + String.Join(' ', Enumerable.Range(1, Count).Select(i => $"a:e{i}.x.example")) + " -all");
                for (var i = 1; i <= Count; i++)
                    dns.A($"e{i}.x.example", "192.0.2.99");
                return dns;
            }

            await Assert.MultipleAsync(async () => {
                Assert.That(await CheckHost(Terms(10), "x.example"), Is.EqualTo(SPFResult.Fail));
                Assert.That(await CheckHost(Terms(11), "x.example"), Is.EqualTo(SPFResult.PermError));
            });

        }

        #endregion

        #region IPv4-mapped clients (V-12)

        [TestCase($"v=spf1 ip4:{Client} -all")]
        [TestCase("v=spf1 a -all")]
        [TestCase("v=spf1 exists:%{i}.allow.x.example -all")]
        public async Task A_client_on_an_IPv4_mapped_address_is_an_IPv4_client(String Record)
        {
            var dns = new ZoneDNS().Txt("x.example", Record).
                                    A("x.example", Client).
                                    A($"{Client}.allow.x.example", "127.0.0.2");

            Assert.That(await CheckHost(dns, "x.example", "::ffff:" + Client), Is.EqualTo(SPFResult.Pass));
        }

        #endregion

    }

}
