/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
    /// SMTPServerConfig.DnsBlocklists (RFC 5782): a client a list names is refused at
    /// RCPT - but for the postmaster - or its mail marked; a list that refuses the
    /// question, or a resolver that answers every name, names nobody; a private
    /// address is not asked about.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// A records by owner name - what a blocklist answers; every question is counted.
        /// </summary>
        private sealed class BlocklistDNS(params (String Name, String Address)[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            public readonly ConcurrentQueue<String> Asked = new();

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();

                if (Types.Contains(DNSResourceRecordTypes.A))
                    Asked.Enqueue(name);

                var answers = Types.Contains(DNSResourceRecordTypes.A)
                                  ? Records.Where (record => record.Name == name).
                                            Select(record => (IDNSResourceRecord) new A(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Parse(record.Address))).
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

        // The wire tests come from 127.0.0.1 - asked about, here, as if it were any address.
        private static Func<SMTPServerConfig, SMTPServerConfig> Blocklists(DnsBlocklistAction Action, Boolean CheckPrivate = true, params String[] Zones)

            => config => config with {
                             DnsBlocklists = new DnsBlocklistConfig {
                                                 Zones                  = Zones.Length > 0 ? Zones : [ "dnsbl.test" ],
                                                 Action                 = Action,
                                                 CheckPrivateAddresses  = CheckPrivate
                                             }
                         };

        private static readonly (String, String) Listed = ("1.0.0.127.dnsbl.test", "127.0.0.2");

        /// <summary>
        /// One message to the given recipients; each RCPT's reply, and the reply to its end
        /// of data - null when no recipient was taken.
        /// </summary>
        private static async Task<(Int32[] Rcpt, Int32? Data)> SendTo(Server Server, params String[] Recipients)
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),              Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

            var replies = new List<Int32>();

            foreach (var recipient in Recipients)
                replies.Add(await wire.CommandAsync($"RCPT TO:<{recipient}>"));

            if (!replies.Contains(250))
                return ([.. replies], null);

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("From: sender@client.example\r\nSubject: listed\r\n\r\nbody\r\n.\r\n");

            return ([.. replies], (await wire.ReplyAsync()).Code);

        }


        [Test]
        public async Task A_client_a_blocklist_names_is_refused_but_for_the_postmaster()
        {

            await using var server = new Server(DNS: new BlocklistDNS(Listed), Configure: Blocklists(DnsBlocklistAction.Reject));

            using (var wire = await server.ConnectAsync())
            {

                Assert.That(await wire.CommandAsync("EHLO client.example"),              Is.EqualTo(250));
                Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
                var (code, lines) = await wire.CommandLinesAsync("RCPT TO:<alice@hermod.test>");

                Assert.That(code,              Is.EqualTo(554));
                Assert.That(lines.Single(),    Does.StartWith("5.7.1 ").And.Contain("[127.0.0.1]").And.Contain("dnsbl.test"));

            }

            var toPostmaster = await SendTo(server, "postmaster@hermod.test");

            Assert.Multiple(() => {
                Assert.That(toPostmaster.Rcpt,          Is.EqualTo(new[] { 250 }), "the postmaster is reachable even for a listed client (RFC 5321 §4.5.1)");
                Assert.That(toPostmaster.Data,          Is.EqualTo(250));
                Assert.That(server.Storage.Messages.Single(), Does.Contain("X-DNSBL: dnsbl.test=127.0.0.2\r\n"), "and marked");
                Assert.That(server.Storage.Verifications.Single()?.Blocklists, Is.EqualTo(new[] { "dnsbl.test" }));
            });

        }


        [Test]
        public async Task A_client_a_blocklist_names_is_marked_where_the_lists_only_mark()
        {

            var dns = new BlocklistDNS(Listed);
            await using var server = new Server(DNS: dns, Configure: Blocklists(DnsBlocklistAction.Mark, Zones: [ "dnsbl.test", "other.test" ]));

            var sent = await SendTo(server, "alice@hermod.test", "bob@hermod.test");

            Assert.Multiple(() => {
                Assert.That(sent.Rcpt,  Is.EqualTo(new[] { 250, 250 }));
                Assert.That(sent.Data,  Is.EqualTo(250));
                Assert.That(server.Storage.Messages.Single(), Does.Contain("X-DNSBL: dnsbl.test=127.0.0.2\r\n"));
                Assert.That(server.Storage.Verifications.Single()?.Blocklists, Is.EqualTo(new[] { "dnsbl.test" }), "other.test does not name it");
                Assert.That(dns.Asked.Where(name => name.StartsWith("1.0.0.127.")).Order(), Is.EqualTo(new[] { "1.0.0.127.dnsbl.test", "1.0.0.127.other.test" }),
                            "each list asked once a connection");
            });

        }


        [Test]
        public async Task A_list_that_refuses_the_question_and_a_resolver_that_answers_everything_name_nobody()
        {

            // Spamhaus' answer to a question through a public resolver; a resolver's own address for every name.
            await using var server = new Server(DNS:       new BlocklistDNS(("1.0.0.127.refusing.test", "127.255.255.254"), ("1.0.0.127.hijacked.test", "192.0.2.1")),
                                                Configure: Blocklists(DnsBlocklistAction.Reject, Zones: [ "refusing.test", "hijacked.test" ]));

            var sent = await SendTo(server, "alice@hermod.test");

            Assert.Multiple(() => {
                Assert.That(sent.Rcpt,  Is.EqualTo(new[] { 250 }));
                Assert.That(sent.Data,  Is.EqualTo(250));
                Assert.That(server.Storage.Messages.Single(), Does.Not.Contain("X-DNSBL"));
                Assert.That(server.Storage.Verifications.Single()?.Blocklists, Is.Null);
            });

        }


        [Test]
        public async Task A_private_address_is_not_asked_about()
        {

            var dns = new BlocklistDNS(Listed);
            await using var server = new Server(DNS: dns, Configure: Blocklists(DnsBlocklistAction.Reject, CheckPrivate: false));

            var sent = await SendTo(server, "alice@hermod.test");

            Assert.Multiple(() => {
                Assert.That(sent.Rcpt,  Is.EqualTo(new[] { 250 }));
                Assert.That(sent.Data,  Is.EqualTo(250));
                Assert.That(dns.Asked.Where(name => name.EndsWith(".dnsbl.test")), Is.Empty, "no list names a loopback address");
            });

        }


        [Test]
        public async Task An_address_is_asked_about_backwards_IPv6_by_its_nibbles()
        {

            var dns      = new BlocklistDNS(("99.2.0.192.dnsbl.test", "127.0.0.4"),
                                            ("1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.dnsbl.test", "127.0.0.3"));
            var verifier = new DNSVerifier(dns, new QuietLogger());
            var config   = new DnsBlocklistConfig { Zones = [ "dnsbl.test." ] };

            var v4       = await verifier.CheckBlocklistsAsync(System.Net.IPAddress.Parse("192.0.2.99"),      config);
            var mapped   = await verifier.CheckBlocklistsAsync(System.Net.IPAddress.Parse("::ffff:192.0.2.99"), config);
            var v6       = await verifier.CheckBlocklistsAsync(System.Net.IPAddress.Parse("2001:db8::1"),     config);
            var other    = await verifier.CheckBlocklistsAsync(System.Net.IPAddress.Parse("192.0.2.98"),      config);

            Assert.Multiple(() => {
                Assert.That(v4,      Is.EqualTo(new[] { new DnsBlocklisting("dnsbl.test", "127.0.0.4") }));
                Assert.That(mapped,  Is.EqualTo(new[] { new DnsBlocklisting("dnsbl.test", "127.0.0.4") }), "an IPv4 address in IPv6 clothes is asked as IPv4");
                Assert.That(v6,      Is.EqualTo(new[] { new DnsBlocklisting("dnsbl.test", "127.0.0.3") }));
                Assert.That(other,   Is.Empty);
            });

        }

    }

}
