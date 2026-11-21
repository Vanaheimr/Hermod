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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// How the relay finds its next hop (RFC 5321 §5.1): a temporary error of the MX lookup
    /// queues the message, a domain or MX hosts that do not exist are an error, MX hosts of one
    /// preference are tried in random order, and the relay does not send to itself or behind
    /// itself. What the client does is seen in its DNS queries: it asks for the addresses of a
    /// host when it is about to connect to it. No host here has an address. The findings O-9
    /// to O-12 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class SMTPOutboundRoutingTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// MX records by owner name, answers with an error code, lookups that throw; every query
        /// noted.
        /// </summary>
        private sealed class ZoneDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<String, List<IDNSResourceRecord>>                   mx       = [];
            private readonly Dictionary<(String, DNSResourceRecordTypes), DNSResponseCodes>  errors   = [];
            private readonly HashSet<(String, DNSResourceRecordTypes)>                        timeouts = [];

            public ConcurrentQueue<(String Name, DNSResourceRecordTypes Type)> Queries { get; } = new();

            public ZoneDNS Mx(String Name, UInt16 Preference, String Host)
            {
                if (!mx.TryGetValue(Name, out var list))
                    mx[Name] = list = [];
                list.Add(new MX(DomainName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Preference, DomainName.Parse(Host)));
                return this;
            }

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

            public Boolean LookedUpAddressOf(String Host)
                => Queries.Any(query => query.Name == Host && query.Type is DNSResourceRecordTypes.A or DNSResourceRecordTypes.AAAA);

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = new List<IDNSResourceRecord>();
                var code    = DNSResponseCodes.NoError;

                foreach (var type in Types)
                {

                    Queries.Enqueue((name, type));

                    if (timeouts.Contains((name, type)))
                        throw new TimeoutException($"{name} {type}");

                    if (errors.TryGetValue((name, type), out var error))
                        code = error;

                    else if (type == DNSResourceRecordTypes.MX && mx.TryGetValue(name, out var list))
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

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String Own = "relay.hermod.test";

        private static Task<SendResult> Send(ZoneDNS DNS, String Domain = "next.example")

            => new SMTPOutboundClient(new SmtpOutboundConfig {
                                          LocalHostname     = Own,
                                          ConnectTimeoutMs  = 3_000,
                                          ReadTimeoutMs     = 5_000
                                      },
                                      null, DNS, new QuietLogger()).
                   SendAsync(Domain, "sender@client.example", [ $"you@{Domain}" ], "Subject: routing\r\n\r\nhello\r\n");

        #endregion


        #region A temporary error of the MX lookup (O-9)

        [Test(Description = "RFC 5321 §5.1: \"If a temporary error is returned, the message MUST be queued and retried later\" - not sent to the domain's own address")]
        public async Task A_temporary_error_of_the_MX_lookup_queues_the_message()
        {

            var servfail = new ZoneDNS().Error  ("next.example", DNSResourceRecordTypes.MX);
            var timeout  = new ZoneDNS().Timeout("next.example", DNSResourceRecordTypes.MX);

            var first    = await Send(servfail);
            var second   = await Send(timeout);

            Assert.Multiple(() => {
                Assert.That(first.Status,                                 Is.EqualTo(SendStatus.TempFail));
                Assert.That(first.ResponseText,                           Does.StartWith("4.4.3 "));
                Assert.That(servfail.LookedUpAddressOf("next.example"),   Is.False);
                Assert.That(second.Status,                                Is.EqualTo(SendStatus.TempFail));
                Assert.That(timeout.LookedUpAddressOf("next.example"),    Is.False);
            });

        }

        [Test(Description = "RFC 5321 §5.1: \"If an empty list of MXs is returned, the address is treated as if it was associated with an implicit MX RR\"")]
        public async Task A_domain_without_MX_records_is_its_own_MX()
        {
            var dns = new ZoneDNS();
            await Send(dns);
            Assert.That(dns.LookedUpAddressOf("next.example"), Is.True);
        }

        #endregion

        #region Domains and MX hosts that do not exist (O-10)

        [Test(Description = "RFC 5321 §5.1: \"If a non-existent domain error is returned, this situation MUST be reported as an error\"")]
        public async Task A_domain_that_does_not_exist_is_a_permanent_failure()
        {

            var result = await Send(new ZoneDNS().Error("next.example", DNSResourceRecordTypes.MX, DNSResponseCodes.NameError));

            Assert.Multiple(() => {
                Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
                Assert.That(result.ResponseText, Does.StartWith("5.1.2 "));
            });

        }

        [Test(Description = "RFC 5321 §5.1: \"If MX records are present, but none of them are usable, or the implicit MX is unusable, this situation MUST be reported as an error\"")]
        public async Task MX_hosts_without_an_address_are_a_permanent_failure()
        {

            var none      = await Send(new ZoneDNS().Mx("next.example", 10, "mx1.next.example").
                                                     Mx("next.example", 20, "mx2.next.example").
                                                     Error("mx1.next.example", DNSResourceRecordTypes.A,    DNSResponseCodes.NameError).
                                                     Error("mx1.next.example", DNSResourceRecordTypes.AAAA, DNSResponseCodes.NameError));
            var implicitMx = await Send(new ZoneDNS());

            Assert.Multiple(() => {
                Assert.That(none.Status,              Is.EqualTo(SendStatus.PermFail), "two MX hosts, NXDOMAIN and no records");
                Assert.That(none.ResponseText,        Does.StartWith("5.4.4 "));
                Assert.That(implicitMx.Status,        Is.EqualTo(SendStatus.PermFail), "the implicit MX without an address");
            });

        }

        [Test(Description = "RFC 5321 §5.1: an MX host whose address lookup fails now may have one later - the message is queued")]
        public async Task An_MX_host_whose_address_lookup_fails_is_a_temporary_failure()
        {

            var result = await Send(new ZoneDNS().Mx("next.example", 10, "mx1.next.example").
                                                  Mx("next.example", 20, "mx2.next.example").
                                                  Error("mx2.next.example", DNSResourceRecordTypes.A).
                                                  Error("mx2.next.example", DNSResourceRecordTypes.AAAA));

            Assert.That(result.Status, Is.EqualTo(SendStatus.TempFail));

        }

        #endregion

        #region MX hosts of the same preference (O-11)

        [Test(Description = "RFC 5321 §5.1: \"the sender-SMTP MUST randomize them\" - each of two MX hosts of preference 10 is tried first in some of 32 deliveries, and the one of preference 20 never")]
        public async Task MX_hosts_of_the_same_preference_are_tried_in_random_order()
        {

            var first = new HashSet<String>();

            for (var i = 0; i < 32; i++)
            {

                var dns = new ZoneDNS().Mx("next.example", 20, "c.next.example").
                                        Mx("next.example", 10, "a.next.example").
                                        Mx("next.example", 10, "b.next.example");

                await Send(dns);

                first.Add(dns.Queries.First(query => query.Type is DNSResourceRecordTypes.A or DNSResourceRecordTypes.AAAA).Name);

            }

            Assert.That(first, Is.EquivalentTo(new[] { "a.next.example", "b.next.example" }));

        }

        #endregion

        #region The relay among the MX hosts (O-12)

        [Test(Description = "RFC 5321 §5.1: \"If a matching record is found, all records at that preference level and higher-numbered ones MUST be discarded ... If there are no records left at that point, it is an error condition, and the message MUST be returned as undeliverable\"")]
        public async Task The_relay_as_the_best_MX_host_is_a_routing_loop()
        {

            var only   = new ZoneDNS().Mx("next.example", 10, Own);
            var backup = new ZoneDNS().Mx("next.example", 10, Own).Mx("next.example", 10, "same.next.example").Mx("next.example", 20, "backup.next.example");

            var first  = await Send(only);
            var second = await Send(backup);

            Assert.Multiple(() => {
                Assert.That(first.Status,                                    Is.EqualTo(SendStatus.PermFail));
                Assert.That(first.ResponseText,                              Does.StartWith("5.4.6 "));
                Assert.That(only.LookedUpAddressOf(Own),                     Is.False);
                Assert.That(second.Status,                                   Is.EqualTo(SendStatus.PermFail));
                Assert.That(backup.LookedUpAddressOf("same.next.example"),   Is.False, "an MX of the relay's own preference");
                Assert.That(backup.LookedUpAddressOf("backup.next.example"), Is.False, "an MX behind the relay");
            });

        }

        [Test(Description = "RFC 5321 §5.1: \"If records do remain, they SHOULD be tried, best preference first\" - the better MX host, not the relay or what is behind it")]
        public async Task The_relay_as_a_backup_MX_host_tries_the_better_ones_only()
        {

            var dns = new ZoneDNS().Mx("next.example", 10, "mx.next.example").Mx("next.example", 20, Own.ToUpperInvariant() + ".").Mx("next.example", 30, "last.next.example");

            await Send(dns);

            Assert.Multiple(() => {
                Assert.That(dns.LookedUpAddressOf("mx.next.example"),    Is.True);
                Assert.That(dns.LookedUpAddressOf(Own),                  Is.False);
                Assert.That(dns.LookedUpAddressOf("last.next.example"),  Is.False);
            });

        }

        #endregion

    }

}
