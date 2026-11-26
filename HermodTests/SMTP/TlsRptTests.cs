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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// SMTP TLS Reporting (RFC 8460) as the relay reports: which TXT record names the destinations
    /// (§3), and the policy each report names (§4.4) - through <see cref="TlsRptResolver"/>,
    /// <see cref="TlsRptAggregator"/>, <see cref="TlsRptReportService"/> and <see cref="TlsRptReportJson"/>.
    /// The findings T-1 and T-2 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class TlsRptTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// TXT records by owner name; every other query has no answer.
        /// </summary>
        private sealed class TxtDNS(params (String Name, String Text)[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = Types.Contains(DNSResourceRecordTypes.TXT)
                                  ? Records.Where (record => record.Name == name).
                                            Select(record => (IDNSResourceRecord) new TXT(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), record.Text)).
                                            ToArray()
                                  : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(TxtDNS);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private sealed class CapturingQueue : IMailQueue
        {
            public readonly List<QueuedMail> Enqueued = [];

            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
            {
                Enqueued.Add(mail);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(int maxItems = 50, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<QueuedMail>>(Enqueued);
            public Task<QueuedMail?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult(Enqueued.FirstOrDefault(m => m.Id == id));
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<int> GetQueueLengthAsync(CancellationToken ct = default) => Task.FromResult(Enqueued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(int maxItems = 100, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<bool> RetryCheckReader { get; } = Channel.CreateUnbounded<bool>().Reader;
        }

        private String stateDirectory = "";

        [SetUp]
        public void CreateStateDirectory()
            => stateDirectory = Directory.CreateTempSubdirectory("hermod-tlsrpt-").FullName;

        [TearDown]
        public void DeleteStateDirectory()
        {
            try { Directory.Delete(stateDirectory, recursive: true); } catch { }
        }

        private String StatePath
            => Path.Combine(stateDirectory, "tlsrpt-state.json");

        private static readonly String[] StsPolicy = [ "version: STSv1", "mode: testing", "mx: mx.policy.test", "max_age: 86400" ];

        private static TlsRptEvent Session(Boolean                 Success,
                                           String?                 FailureType   = null,
                                           IReadOnlyList<String>?  PolicyString  = null)

            => new ("policy.test", TlsRptPolicyType.Sts, "mx.policy.test", "192.0.2.25", "198.51.100.1", Success, FailureType, PolicyString);

        private (TlsRptReportService Service, CapturingQueue Queue) Reporter(params String[] Records)
        {

            var queue   = new CapturingQueue();
            var service = new TlsRptReportService(new TlsRptAggregator(StatePath, new QuietLogger()),
                                                  new TlsRptResolver(new TxtDNS([ .. Records.Select(record => ("_smtp._tls.policy.test", record)) ]), new QuietLogger()),
                                                  queue,
                                                  new TlsRptReportingOptions("Hermod tests", "TLS Reports <tls-reports@relay.hermod.test>",
                                                                             "tls-reports@relay.hermod.test", "relay.hermod.test",
                                                                             "mailto:postmaster@relay.hermod.test", TimeSpan.FromDays(1)),
                                                  new QuietLogger());

            return (service, queue);

        }

        /// <summary>
        /// The JSON report inside a queued report mail: the base64 of the application/tlsrpt+gzip part, gunzipped.
        /// </summary>
        private static JsonElement Json(QueuedMail Mail)
        {

            var content = Mail.MessageContent;
            var start   = content.IndexOf("Content-Transfer-Encoding: base64\r\n\r\n", StringComparison.Ordinal) + "Content-Transfer-Encoding: base64\r\n\r\n".Length;
            var end     = content.IndexOf("\r\n--", start, StringComparison.Ordinal);

            using var gzip   = new GZipStream(new MemoryStream(Convert.FromBase64String(content[start..end].Replace("\r\n", ""))), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);

            return JsonDocument.Parse(reader.ReadToEnd()).RootElement.Clone();

        }

        private static String[] Strings(JsonElement Array)
            => [ .. Array.EnumerateArray().Select(element => element.GetString()!) ];

        #endregion


        #region T-1: exactly one valid record

        [TestCase(new[] { "v=TLSRPTv1; rua=mailto:tlsrpt@policy.test" },                                              true,  TestName = "TLSRPT record: a valid one")]
        [TestCase(new[] { "v=TLSRPTv1;rua=mailto:tlsrpt@policy.test" },                                               true,  TestName = "TLSRPT record: no space after the delimiter")]
        [TestCase(new[] { "v=TLSRPTv1 ; rua=mailto:tlsrpt@policy.test;" },                                            true,  TestName = "TLSRPT record: space before the delimiter, and a trailing one")]
        [TestCase(new[] { "v=TLSRPTv1; rua=mailto:tlsrpt@policy.test; ext=future" },                                  true,  TestName = "TLSRPT record: an unknown field is ignored")]
        [TestCase(new[] { "v=TLSRPTv1; rua=mailto:tlsrpt@policy.test", "v=spf1 -all" },                              true,  TestName = "TLSRPT record: a record of another kind next to it")]
        [TestCase(new[] { "v=TLSRPTv1; rua=mailto:a@policy.test", "v=TLSRPTv1; rua=mailto:b@policy.test" },           false, TestName = "TLSRPT record: two records")]
        [TestCase(new[] { "rua=mailto:tlsrpt@policy.test; v=TLSRPTv1" },                                              false, TestName = "TLSRPT record: not beginning with v=TLSRPTv1")]
        [TestCase(new[] { "v=tlsrptv1; rua=mailto:tlsrpt@policy.test" },                                              false, TestName = "TLSRPT record: v=tlsrptv1 in lower case")]
        [TestCase(new[] { "v=TLSRPTv1; RUA=mailto:tlsrpt@policy.test" },                                              false, TestName = "TLSRPT record: RUA= in upper case")]
        [TestCase(new[] { "v=TLSRPTv10; rua=mailto:tlsrpt@policy.test" },                                             false, TestName = "TLSRPT record: another version")]
        [Description("T-1 - RFC 8460 §3: \"records that do not begin with \"v=TLSRPTv1;\" are discarded. If the number of resulting records is not one, senders MUST assume the recipient domain does not implement TLSRPT\"; %s\"v=TLSRPTv1\", %s\"rua=\", field-delim = *WSP \";\" *WSP")]
        public async Task Only_exactly_one_valid_record_names_a_destination(String[] Records, Boolean Reported)
        {

            var resolver = new TlsRptResolver(new TxtDNS([ .. Records.Select(record => ("_smtp._tls.policy.test", record)) ]), new QuietLogger());
            var policy   = await resolver.GetPolicyAsync("policy.test");

            Assert.That(policy.RuaMailto, Is.EqualTo(Reported ? new[] { "tlsrpt@policy.test" } : Array.Empty<String>()));

        }


        [TestCase("v=TLSRPTv1; rua=mailto:a@policy.test|v=TLSRPTv1; rua=mailto:b@policy.test", TestName = "A domain with two TLSRPT records gets no report")]
        [TestCase("rua=mailto:a@policy.test; v=TLSRPTv1",                                      TestName = "A domain whose record does not begin with v=TLSRPTv1 gets no report")]
        [TestCase("v=tlsrptv1; rua=mailto:a@policy.test",                                      TestName = "A domain whose record has v=tlsrptv1 in lower case gets no report")]
        [Description("T-1 - RFC 8460 §3: \"If the number of resulting records is not one, senders MUST assume the recipient domain does not implement TLSRPT\"")]
        public async Task A_domain_without_exactly_one_valid_record_gets_no_report(String Records)
        {

            var (service, queue) = Reporter(Records.Split('|'));

            service.Record(Session(true));
            await service.GenerateAndSendAsync(CancellationToken.None);

            Assert.That(queue.Enqueued, Is.Empty);

        }


        [Test(Description = "T-1 - RFC 8460 §3: a domain with one valid record gets its report - a guard for the service")]
        public async Task A_domain_with_one_valid_record_gets_its_report()
        {

            var (service, queue) = Reporter("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test", "v=spf1 -all");

            service.Record(Session(true));
            await service.GenerateAndSendAsync(CancellationToken.None);

            Assert.That(queue.Enqueued.Single().EnvelopeTo, Is.EqualTo(new[] { "tlsrpt@policy.test" }));

        }

        #endregion

        #region T-2: the policy applied

        [Test(Description = "T-2 - RFC 8460 §4.4: \"policy-string\": \"An encoding of the applied policy as a JSON array of strings\"; §4.5: for MTA-STS the lines of the policy")]
        public async Task The_report_names_the_policy_applied()
        {

            var (service, queue) = Reporter("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test");

            service.Record(Session(true,  PolicyString: StsPolicy));
            service.Record(Session(false, "certificate-expired", StsPolicy));
            await service.GenerateAndSendAsync(CancellationToken.None);

            var policy = Json(queue.Enqueued.Single()).GetProperty("policies")[0];

            Assert.Multiple(() => {
                Assert.That(Strings(policy.GetProperty("policy").GetProperty("policy-string")),                   Is.EqualTo(StsPolicy));
                Assert.That(policy.GetProperty("summary").GetProperty("total-successful-session-count").GetInt64(), Is.EqualTo(1));
                Assert.That(policy.GetProperty("summary").GetProperty("total-failure-session-count").GetInt64(),    Is.EqualTo(1));
            });

        }


        [Test(Description = "T-2 - RFC 8460 §4.4: the policy object has policy-type, policy-string, policy-domain and mx-host - with no policy found, policy-string is an empty array")]
        public void Without_a_policy_the_policy_string_is_an_empty_array()
        {

            var aggregator = new TlsRptAggregator(StatePath, new QuietLogger());
            aggregator.Record(new TlsRptEvent("policy.test", TlsRptPolicyType.NoPolicyFound, "mx.policy.test", null, null, true, null));

            var json   = JsonDocument.Parse(TlsRptReportJson.Build(aggregator.Drain().Single(), "Hermod tests", "mailto:postmaster@relay.hermod.test", "4711")).RootElement;
            var policy = json.GetProperty("policies")[0].GetProperty("policy");

            Assert.Multiple(() => {
                Assert.That(policy.GetProperty("policy-type").GetString(),           Is.EqualTo("no-policy-found"));
                Assert.That(policy.GetProperty("policy-string").ValueKind,           Is.EqualTo(JsonValueKind.Array));
                Assert.That(policy.GetProperty("policy-string").GetArrayLength(),    Is.Zero);
            });

        }


        [Test(Description = "T-2 - RFC 8460 §4.4: each policy block names the policy applied - sessions under a policy and under its successor are two blocks")]
        public void A_changed_policy_is_a_block_of_its_own()
        {

            String[] successor = [ "version: STSv1", "mode: enforce", "mx: mx.policy.test", "max_age: 86400" ];

            var aggregator = new TlsRptAggregator(StatePath, new QuietLogger());
            aggregator.Record(Session(true, PolicyString: StsPolicy));
            aggregator.Record(Session(true, PolicyString: StsPolicy));
            aggregator.Record(Session(true, PolicyString: successor));

            var policies = aggregator.Drain().Single().Policies;

            Assert.That(policies.Select(policy => (String.Join(" / ", policy.PolicyString ?? []), policy.SuccessCount)),
                        Is.EquivalentTo(new[] { (String.Join(" / ", StsPolicy), 2L), (String.Join(" / ", successor), 1L) }));

        }


        [Test(Description = "T-2 - the aggregator's state survives a restart: a state file written before policy-string was kept still loads, its counts reported with an empty policy-string")]
        public void A_state_file_without_policy_strings_still_loads()
        {

            File.WriteAllText(StatePath,
                """
                {"policy.test":{"PolicyDomain":"policy.test","WindowBegin":"2026-11-18T00:00:00+00:00","Policies":{"sts":{"PolicyType":"sts","MxHosts":["mx.policy.test"],"Success":2,"Failures":{"certificate-expired|198.51.100.1|mx.policy.test|192.0.2.25":{"ResultType":"certificate-expired","SendingMtaIp":"198.51.100.1","ReceivingMxHostname":"mx.policy.test","ReceivingIp":"192.0.2.25","Count":1}}}}}}
                """);

            var aggregator = new TlsRptAggregator(StatePath, new QuietLogger());
            aggregator.Record(Session(true));

            var report = aggregator.Drain().Single();
            var policy = report.Policies.Single();
            var json   = JsonDocument.Parse(TlsRptReportJson.Build(report, "Hermod tests", "mailto:postmaster@relay.hermod.test", "4711")).RootElement;

            Assert.Multiple(() => {
                Assert.That(report.PolicyDomain,                                   Is.EqualTo("policy.test"));
                Assert.That(policy.SuccessCount,                                   Is.EqualTo(3), "the two of the state file, and one since");
                Assert.That(policy.Failures.Single().ResultType,                   Is.EqualTo("certificate-expired"));
                Assert.That(policy.Failures.Single().FailedSessionCount,           Is.EqualTo(1));
                Assert.That(json.GetProperty("policies")[0].GetProperty("policy").GetProperty("policy-string").GetArrayLength(), Is.Zero);
            });

        }

        #endregion


        #region T-5 and T-6: where a report goes

        [TestCase("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test\r\nBcc: victim@policy.test",          new String[0],                             TestName = "TLSRPT rua: a CR LF in the URI - no address")]
        [TestCase("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test\nBcc: victim@policy.test",            new String[0],                             TestName = "TLSRPT rua: an LF in the URI - no address")]
        [TestCase("v=TLSRPTv1; rua=mailto:tlsrpt%0D%0ABcc:%20victim@policy.test",                 new String[0],                             TestName = "TLSRPT rua: a line break percent-encoded - no address")]
        [TestCase("v=TLSRPTv1; rua=mailto:tls%21reports@policy.test",                             new[] { "tls!reports@policy.test" },       TestName = "TLSRPT rua: percent-encoding decoded")]
        [TestCase("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test?subject=TLS%20report",               new[] { "tlsrpt@policy.test" },            TestName = "TLSRPT rua: header fields no part of the address")]
        [TestCase("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test,mailto:tlsrpt@policy.test",          new[] { "tlsrpt@policy.test" },            TestName = "TLSRPT rua: one address named twice, once")]
        [Description("T-5, T-6 - RFC 8460 §3: tlsrpt-uri = URI (RFC 3986), commas, exclamation points and semicolons of its own \"MUST be encoded\"; \"In the case of \"mailto\", reports should be submitted to the specified email address [RFC6068]\"")]
        public void A_report_goes_to_the_address_its_rua_URI_names(String Record, String[] Addresses)

            => Assert.That(TlsRptResolver.Parse(Record).RuaMailto, Is.EqualTo(Addresses));


        [Test(Description = "T-5 - RFC 5322 §2.2: no recipient, and no header field of the report, from a line break in the record's rua")]
        public async Task A_line_break_in_an_rua_URI_makes_no_recipient_and_no_header_field()
        {

            var (service, queue) = Reporter("v=TLSRPTv1; rua=mailto:tlsrpt@policy.test\r\nBcc: victim@policy.test, mailto:tls@policy.test");

            service.Record(Session(true));
            await service.GenerateAndSendAsync(CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(queue.Enqueued.SelectMany(mail => mail.EnvelopeTo), Is.EqualTo(new[] { "tls@policy.test" }));
                Assert.That(queue.Enqueued.Single().MessageContent,             Does.Not.Contain("Bcc:"));
            });

        }

        #endregion

    }

}
