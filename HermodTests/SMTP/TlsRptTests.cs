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
using System.Linq;
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
    /// (§3) - through <see cref="TlsRptResolver"/> and <see cref="TlsRptReportService"/>.
    /// The finding T-1 of SMTPConformanceTests.
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

        private static TlsRptEvent Session(Boolean  Success,
                                           String?  FailureType  = null)

            => new ("policy.test", TlsRptPolicyType.Sts, "mx.policy.test", "192.0.2.25", "198.51.100.1", Success, FailureType);

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

    }

}
