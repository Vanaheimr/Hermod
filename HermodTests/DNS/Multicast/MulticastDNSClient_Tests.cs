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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

using static org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast.MulticastDNSTestHelpers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// Tests of the Multicast DNS client (RFC 6762 §5, §7 and §10) on the in-memory network:
    /// one-shot queries with cache, known answers, retransmission and negative answers,
    /// the cache-flush and goodbye rules, address filtering, argument validation and the
    /// hybrid client routing link-local names to Multicast DNS.
    /// </summary>
    [TestFixture]
    public class MulticastDNSClient_Tests
    {

        #region Data

        private static readonly IPv4Address  ServerAddress  = IPv4Address.Parse("10.0.0.7");
        private static readonly IPv4Address  ClientAddress  = IPv4Address.Parse("10.0.0.8");

        #endregion

        #region (class) RecordingDNSClient

        /// <summary>
        /// A unicast DNS client stub recording the queried names and whether it was disposed.
        /// </summary>
        private sealed class RecordingDNSClient : IDNSClient
        {

            public List<String>  Queries       { get; } = [];
            public Boolean       IsDisposed    { get; private set; }

            public Task<DNSInfo> Query(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = true,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Query(DNSServiceName.Parse(DomainName.FullName), ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = true,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)
            {

                lock (Queries)
                    Queries.Add(DNSServiceName.FullName);

                return Task.FromResult(DNSInfo.TimedOut(new DNSServerConfig(IPv4Address.Localhost), 0, TimeSpan.Zero));

            }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return ValueTask.CompletedTask;
            }

        }

        #endregion


        #region Query_ServesCachedRecords_WithoutSendingAQuery()

        [Test]
        public async Task Query_ServesCachedRecords_WithoutSendingAQuery()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            // The announcements fill the cache of the listening client.
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            Assert.That(client.CacheSize, Is.EqualTo(4));

            capture.Clear();

            var info = await client.Query(MyHostName, [ DNSResourceRecordTypes.A ]);

            Assert.Multiple(() => {
                Assert.That(info.IsTimeout,                                                     Is.False);
                Assert.That(info.IsValid,                                                       Is.True);
                Assert.That(info.ResponseCode,                                                  Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(info.Answers.OfType<A>().Select(a => a.IPv4Address),                Is.EqualTo(new[] { ServerAddress }));
                Assert.That(capture.Count,                                                      Is.EqualTo(0), "a cached record must not cause a query");
                Assert.That(client.CachedRecords(MyHostName, DNSResourceRecordTypes.A),         Has.Count.EqualTo(1));
                Assert.That(client.CachedRecords(MyHostName, DNSResourceRecordTypes.A)[0].Source, Is.EqualTo(PeerSocket("10.0.0.7")));
            });

        }

        #endregion

        #region Query_WithForceUpdate_BypassesTheCache_WithoutListingTheQueriedType()

        [Test]
        public async Task Query_WithForceUpdate_BypassesTheCache_WithoutListingTheQueriedType()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);
            var clock    = new ShiftableTimeProvider();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions(), clock);

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            // Age the cache: with less than half of its 120 s left, the cached A record travels
            // as known answer without suppressing the responder's answer (RFC 6762 §7.1).
            clock.Shift(TimeSpan.FromSeconds(70));

            capture.Clear();

            var info   = await client.Query(MyHostName, [ DNSResourceRecordTypes.A ], ForceUpdate: true);
            var query  = capture.Queries.FirstOrDefault(datagram => datagram.Sender == clientTransport);

            Assert.That(query, Is.Not.Null, "ForceUpdate must send a query");
            Assert.Multiple(() => {
                Assert.That(query!.IsMulticast,                                             Is.True);
                Assert.That(query.Message.Questions,                                        Has.Count.EqualTo(1));
                Assert.That(query.Message.Questions[0].Name.FullName,                       Is.EqualTo("myhost.local."));
                Assert.That(query.Message.Questions[0].Type,                                Is.EqualTo(DNSResourceRecordTypes.A));
                // A record of the queried type is never listed as known answer: the responder would
                // suppress its answer (RFC 6762 §7.1), and a forced update wants it answered.
                Assert.That(query.Message.Answers,                                          Is.Empty, "no known answer of the queried type");
                Assert.That(info.IsTimeout,                                                 Is.False);
                Assert.That(info.Answers.OfType<A>().Select(a => a.IPv4Address),            Is.EqualTo(new[] { ServerAddress }));
                Assert.That(capture.Responses,                                              Has.Count.EqualTo(1));
                Assert.That(capture.Responses[0].Destination,                               Is.EqualTo(new IPSocket(ClientAddress, network.Port)), "a QU query gets a unicast answer");
            });

        }

        #endregion

        #region Query_TimesOut_WhenNobodyAnswers_AndRetransmits()

        [Test]
        public async Task Query_TimesOut_WhenNobodyAnswers_AndRetransmits()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.StartAsync();

            var started  = DateTimeOffset.UtcNow;
            var info     = await client.Query(DNSServiceName.Parse("nobody.local."), [ DNSResourceRecordTypes.A ]);
            var elapsed  = DateTimeOffset.UtcNow - started;

            var queries  = capture.Queries;

            Assert.Multiple(() => {
                Assert.That(info.IsTimeout,                                                   Is.True);
                Assert.That(info.IsValid,                                                     Is.False);
                Assert.That(info.Answers,                                                     Is.Empty);
                Assert.That(info.AdditionalRecords,                                           Is.Empty);
                Assert.That(elapsed,                                                          Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450)), "the query timeout of 500 ms");
                Assert.That(elapsed,                                                          Is.LessThan(TimeSpan.FromMilliseconds(1500)));
                Assert.That(queries,                                                          Has.Count.GreaterThanOrEqualTo(2), "retransmitted every 100 ms");
                Assert.That(queries.All(query => query.Sender == clientTransport),            Is.True);
                Assert.That(queries.All(query => query.Message.Questions.Count == 1 &&
                                                 query.Message.Questions[0].Name.FullName == "nobody.local." &&
                                                 query.Message.Questions[0].Type == DNSResourceRecordTypes.A), Is.True);
                Assert.That(capture.Responses,                                                Is.Empty);
                Assert.That(client.CacheSize,                                                 Is.EqualTo(0));
            });

        }

        #endregion

        #region Query_CollectsAdditionalRecords_ForAPTRQuery()

        [Test]
        public async Task Query_CollectsAdditionalRecords_ForAPTRQuery()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            // Forget the announcements, so that the query really goes to the responder.
            client.ClearCache();

            Assert.That(client.CacheSize, Is.EqualTo(0));

            var info = await client.Query(TestService, [ DNSResourceRecordTypes.PTR ]);

            Assert.Multiple(() => {
                Assert.That(info.IsTimeout,                                                                Is.False);
                Assert.That(info.Answers.OfType<PTR>().Select(ptr => ptr.Target.FullName),                 Is.EqualTo(new[] { "myhost._test._tcp.local." }));
                Assert.That(info.AdditionalRecords.Select(record => record.Type),                          Is.EquivalentTo(new[] { DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.A }), "RFC 6763 §12.1");
                Assert.That(info.AdditionalRecords.OfType<SRV>().Single().Port.ToUInt16(),                 Is.EqualTo((UInt16) 8443));
                Assert.That(info.AdditionalRecords.OfType<TXT>().Single().KeyValues["txtver"],             Is.EqualTo("1"));
                Assert.That(info.AdditionalRecords.OfType<A>().Single().IPv4Address,                       Is.EqualTo(ServerAddress));
                Assert.That(client.CacheSize,                                                              Is.EqualTo(4), "answers and additionals are cached");
            });

        }

        #endregion

        #region OneShotQuery_SetsTheQUBitByDefault_AndNotWhenUnicastResponsesAreNotRequested()

        [Test]
        public async Task OneShotQuery_SetsTheQUBitByDefault_AndNotWhenUnicastResponsesAreNotRequested()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ClientAddress);
            await using var quClient   = new MulticastDNSClient(transport, FastClientOptions());
            await using var qmClient   = new MulticastDNSClient(transport, FastClientOptions(RequestUnicastResponses: false));

            await quClient.StartAsync();
            await qmClient.StartAsync();

            await quClient.Query(MyHostName, [ DNSResourceRecordTypes.A ], Timeout: TimeSpan.FromMilliseconds(50));

            var quQuery = capture.Queries.FirstOrDefault();

            capture.Clear();

            await qmClient.Query(MyHostName, [ DNSResourceRecordTypes.A ], Timeout: TimeSpan.FromMilliseconds(50));

            var qmQuery = capture.Queries.FirstOrDefault();

            Assert.That(quQuery, Is.Not.Null);
            Assert.That(qmQuery, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(quQuery!.Message.Questions[0].Name.FullName,             Is.EqualTo("myhost.local."));
                Assert.That(quQuery.Message.Questions[0].UnicastResponseRequested,   Is.True,  "RFC 6762 §5.4: a one-shot query asks for a unicast response");
                Assert.That(qmQuery!.Message.Questions[0].Name.FullName,             Is.EqualTo("myhost.local."));
                Assert.That(qmQuery.Message.Questions[0].UnicastResponseRequested,   Is.False, "unless the option says otherwise");
            });

        }

        #endregion


        #region CacheFlush_ExpiresOlderRecordsOfTheRRSet_OneSecondLater()

        [Test]
        public async Task CacheFlush_ExpiresOlderRecordsOfTheRRSet_OneSecondLater()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var clock    = new ShiftableTimeProvider();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions(), clock);

            await responder.StartAsync();
            await client.   StartAsync();

            var expired = new List<MulticastDNSCacheEntry>();
            client.OnRecordExpired += (timestamp, sender, entry, ct) => {
                lock (expired)
                    expired.Add(entry);
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: false);

            Assert.That(client.CachedRecords(MyHostName, DNSResourceRecordTypes.A), Has.Count.EqualTo(1));

            // RFC 6762 §10.2: the cache-flush bit only touches records received more than one
            // second ago, so the cached record is aged beyond that before the host changes its address.
            clock.Shift(TimeSpan.FromSeconds(2));

            await publication.UpdateAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.9")) ]);

            var now      = clock.GetUtcNow();
            var entries  = client.CachedRecords(MyHostName, DNSResourceRecordTypes.A);

            Assert.That(entries, Has.Count.EqualTo(2), "the old record gets one more second to live");

            var oldEntry  = entries.Single(entry => ((A) entry.Record).IPv4Address.Equals(IPv4Address.Parse("10.0.0.7")));
            var newEntry  = entries.Single(entry => ((A) entry.Record).IPv4Address.Equals(IPv4Address.Parse("10.0.0.9")));

            Assert.Multiple(() => {
                Assert.That(oldEntry.RemainingTimeToLive(now),  Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(newEntry.RemainingTimeToLive(now),  Is.GreaterThan(TimeSpan.FromSeconds(100)));
            });

            clock.Shift(TimeSpan.FromSeconds(1.1));

            Assert.That(client.CachedRecords(MyHostName, DNSResourceRecordTypes.A).Select(entry => ((A) entry.Record).IPv4Address),
                        Is.EqualTo(new[] { IPv4Address.Parse("10.0.0.9") }),
                        "one second after the cache-flush only the new record is left");

            await WaitUntil(() => { lock (expired) return expired.Any(entry => entry.Record is A a && a.IPv4Address.Equals(IPv4Address.Parse("10.0.0.7"))); });

            Assert.Multiple(() => {
                Assert.That(client.CacheSize,  Is.EqualTo(1));
                lock (expired)
                    Assert.That(expired,       Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region CacheFlush_KeepsRecordsReceivedWithinTheLastSecond()

        [Test]
        public async Task CacheFlush_KeepsRecordsReceivedWithinTheLastSecond()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var publication = await responder.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: false);

            // A cache-flush right after the first record: RFC 6762 §10.2 keeps records received
            // within the last second, as they may belong to the same multi-packet response.
            await publication.UpdateAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.8")) ]);

            var now      = client.TimeProvider.GetUtcNow();
            var entries  = client.CachedRecords(MyHostName, DNSResourceRecordTypes.A);

            Assert.Multiple(() => {
                Assert.That(entries.Select(entry => ((A) entry.Record).IPv4Address),                    Is.EquivalentTo(new[] { IPv4Address.Parse("10.0.0.7"), IPv4Address.Parse("10.0.0.8") }));
                Assert.That(entries.All(entry => entry.RemainingTimeToLive(now) > TimeSpan.FromSeconds(100)), Is.True, "neither record was flushed");
            });

        }

        #endregion

        #region Goodbye_ExpiresTheRecords_AndRaisesOnRecordExpired()

        [Test]
        public async Task Goodbye_ExpiresTheRecords_AndRaisesOnRecordExpired()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var expired = new List<MulticastDNSCacheEntry>();
            client.OnRecordExpired += (timestamp, sender, entry, ct) => {
                lock (expired)
                    expired.Add(entry);
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            Assert.That(client.CacheSize, Is.EqualTo(4));

            await publication.WithdrawAsync();

            Assert.That(client.CachedRecords(), Is.Empty, "with a GoodbyeDelay of zero the records expire at once");

            await WaitUntil(() => { lock (expired) return expired.Count == 4; }, TimeSpan.FromSeconds(1));

            Assert.Multiple(() => {
                Assert.That(client.CacheSize,  Is.EqualTo(0), "the sweep removed them");
                lock (expired)
                    Assert.That(expired.Select(entry => entry.Record.Type), Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.PTR }));
            });

        }

        #endregion

        #region NegativeAnswer_CompletesTheQueryEarly_WithoutAnswers()

        [Test]
        public async Task NegativeAnswer_CompletesTheQueryEarly_WithoutAnswers()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            var started  = DateTimeOffset.UtcNow;
            var info     = await client.Query(MyHostName, [ DNSResourceRecordTypes.AAAA ]);
            var elapsed  = DateTimeOffset.UtcNow - started;

            Assert.Multiple(() => {
                Assert.That(info.IsTimeout,                                                       Is.False, "an NSEC answer is a definite 'no', not a timeout");
                Assert.That(info.IsValid,                                                         Is.True);
                Assert.That(info.ResponseCode,                                                    Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(info.Answers,                                                         Is.Empty);
                Assert.That(elapsed,                                                              Is.LessThan(TimeSpan.FromMilliseconds(400)), "the query ends long before its 500 ms timeout");
                Assert.That(capture.Queries.Count(query => query.Sender == clientTransport),      Is.EqualTo(1), "no retransmission was needed");
                Assert.That(capture.Responses.SelectMany(response => response.AnswerRecords).OfType<NSEC>().Count(), Is.EqualTo(1));
            });

        }

        #endregion

        #region LinkLocalIPv6Addresses_AreFiltered_UnlessIncluded()

        [Test]
        public async Task LinkLocalIPv6Addresses_AreFiltered_UnlessIncluded()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var filtering        = new MulticastDNSClient   (clientTransport, FastClientOptions());
            await using var including        = new MulticastDNSClient   (clientTransport, FastClientOptions(IncludeLinkLocalIPv6: true));

            await responder.StartAsync();
            await filtering.StartAsync();
            await including.StartAsync();

            var linkLocal  = IPv6Address.Parse("fe80::1");
            var global     = IPv6Address.Parse("2001:db8::1");

            await responder.PublishAsync([
                      new A   (MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), ServerAddress),
                      new AAAA(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), linkLocal),
                      new AAAA(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), global)
                  ], Probe: false);

            var filtered  = (await filtering.Query_IPv6Addresses(MyHost)).ToArray();
            var all       = (await including.Query_IPv6Addresses(MyHost)).ToArray();
            var ipv4      = (await filtering.Query_IPv4Addresses(MyHost)).ToArray();

            Assert.Multiple(() => {
                Assert.That(filtered,  Is.EqualTo(new[] { global }), "a link-local address is useless without an interface scope");
                Assert.That(all,       Is.EquivalentTo(new[] { linkLocal, global }));
                Assert.That(ipv4,      Is.EqualTo(new[] { ServerAddress }));
            });

        }

        #endregion


        #region Query_BeforeStart_ThrowsInvalidOperationException()

        [Test]
        public async Task Query_BeforeStart_ThrowsInvalidOperationException()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ClientAddress);
            await using var client     = new MulticastDNSClient(transport, FastClientOptions());

            Assert.Multiple(() => {
                Assert.That(client.IsRunning, Is.False);
                Assert.ThrowsAsync<InvalidOperationException>(() => client.Query(MyHostName, [ DNSResourceRecordTypes.A ]));
                Assert.ThrowsAsync<InvalidOperationException>(() => client.Query(MyHost,     [ DNSResourceRecordTypes.A ]));
                Assert.ThrowsAsync<InvalidOperationException>(() => client.BrowseAsync(TestService));
            });

        }

        #endregion

        #region Query_WithACancelledToken_ThrowsOperationCanceledException()

        [Test]
        public async Task Query_WithACancelledToken_ThrowsOperationCanceledException()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ClientAddress);
            await using var client     = new MulticastDNSClient(transport, FastClientOptions());

            await client.StartAsync();

            Assert.CatchAsync<OperationCanceledException>(
                () => client.Query(MyHostName, [ DNSResourceRecordTypes.A ], CancellationToken: new CancellationToken(canceled: true))
            );

            // The client is still usable afterwards.
            var info = await client.Query(MyHostName, [ DNSResourceRecordTypes.A ], Timeout: TimeSpan.FromMilliseconds(50));

            Assert.That(info.IsTimeout, Is.True);

        }

        #endregion


        #region HybridClient_RoutesLinkLocalNamesToMulticast_AndOthersToUnicast()

        [Test]
        public async Task HybridClient_RoutesLinkLocalNamesToMulticast_AndOthersToUnicast()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());
            await using var client     = new MulticastDNSClient   (transport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var unicast = new RecordingDNSClient();
            var hybrid  = new HybridDNSClient(client, unicast);

            var local    = (await hybrid.Query_IPv4Addresses(MyHost)).ToArray();
            var service  =  await hybrid.Query(TestService, [ DNSResourceRecordTypes.PTR ]);
            var reverse  =  await hybrid.Query(DomainName.Parse("1.0.254.169.in-addr.arpa."), [ DNSResourceRecordTypes.PTR ], Timeout: TimeSpan.FromMilliseconds(50));
            var other    = (await hybrid.Query_IPv4Addresses(DomainName.Parse("example.com."))).ToArray();
            var otherSRV =  await hybrid.Query(DNSServiceName.Parse("_test._tcp.example.com."), [ DNSResourceRecordTypes.SRV ]);

            Assert.Multiple(() => {
                Assert.That(hybrid.MulticastClient,                                             Is.SameAs(client));
                Assert.That(hybrid.UnicastClient,                                               Is.SameAs(unicast));
                Assert.That(hybrid.OwnsClients,                                                 Is.False);
                Assert.That(local,                                                              Is.EqualTo(new[] { ServerAddress }));
                Assert.That(service.Answers.OfType<PTR>().Select(ptr => ptr.Target.FullName),   Is.EqualTo(new[] { "myhost._test._tcp.local." }));
                Assert.That(reverse.IsTimeout,                                                  Is.True, "the link-local reverse zone goes to Multicast DNS, where nobody answers");
                Assert.That(other,                                                              Is.Empty);
                Assert.That(otherSRV.IsTimeout,                                                 Is.True);
                Assert.That(unicast.Queries,                                                    Is.EqualTo(new[] { "example.com.", "_test._tcp.example.com." }), "only the non-local names reached the unicast client");
            });

        }

        #endregion

        #region HybridClient_WithOwnsClients_DisposesBothClients()

        [Test]
        public async Task HybridClient_WithOwnsClients_DisposesBothClients()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ClientAddress);
            await using var client     = new MulticastDNSClient(transport, FastClientOptions());

            await client.StartAsync();

            var unicast = new RecordingDNSClient();

            // Not owning: disposing the hybrid leaves both clients alone.
            var borrowing = new HybridDNSClient(client, unicast, OwnsClients: false);

            await borrowing.DisposeAsync();

            Assert.Multiple(() => {
                Assert.That(borrowing.OwnsClients,  Is.False);
                Assert.That(client.IsRunning,       Is.True);
                Assert.That(unicast.IsDisposed,     Is.False);
            });

            // Owning: both inner clients are disposed with the hybrid.
            var owning = new HybridDNSClient(client, unicast, OwnsClients: true);

            await owning.DisposeAsync();

            Assert.Multiple(() => {
                Assert.That(owning.OwnsClients,     Is.True);
                Assert.That(client.IsRunning,       Is.False);
                Assert.That(unicast.IsDisposed,     Is.True);
            });

            // Disposing twice is harmless.
            Assert.DoesNotThrowAsync(() => owning.DisposeAsync().AsTask());

        }

        #endregion

        #region Query_DomainNameOverload_EqualsTheDNSServiceNameOverload()

        [Test]
        public async Task Query_DomainNameOverload_EqualsTheDNSServiceNameOverload()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());
            await using var client     = new MulticastDNSClient   (transport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var byDomainName   = await client.Query(MyHost,     [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]);
            var byServiceName  = await client.Query(MyHostName, [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]);

            Assert.Multiple(() => {
                Assert.That(byDomainName. IsTimeout,                                            Is.False);
                Assert.That(byServiceName.IsTimeout,                                            Is.False);
                Assert.That(byDomainName. Answers.Select(record => record.RecordKey()),         Is.EqualTo(byServiceName.Answers.Select(record => record.RecordKey())));
                Assert.That(byDomainName. Answers.OfType<A>().Select(a => a.IPv4Address),       Is.EqualTo(new[] { ServerAddress }));
                Assert.That(byDomainName. AdditionalRecords.Select(record => record.RecordKey()), Is.EqualTo(byServiceName.AdditionalRecords.Select(record => record.RecordKey())));
            });

        }

        #endregion

        #region SendQueryAsync_RaisesOnResponseReceived_AndOnRecordReceived()

        [Test]
        public async Task SendQueryAsync_RaisesOnResponseReceived_AndOnRecordReceived()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var responses  = new List<(MulticastDNSMessage Response, IPSocket Source)>();
            var records    = new List<(MulticastDNSRecord Record, IPSocket Source)>();

            client.OnResponseReceived += (timestamp, sender, response, datagram, ct) => {
                lock (responses)
                    responses.Add((response, datagram.RemoteSocket));
                return Task.CompletedTask;
            };

            client.OnRecordReceived += (timestamp, sender, record, source, ct) => {
                lock (records)
                    records.Add((record, source));
                return Task.CompletedTask;
            };

            await client.SendQueryAsync([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A, UnicastResponseRequested: true) ]);

            await WaitUntil(() => { lock (responses) return responses.Count == 1; });
            await WaitUntil(() => { lock (records)   return records.  Count == 1; });

            Assert.Multiple(() => {
                Assert.That(responses[0].Source,                              Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(responses[0].Response.IsResponse,                 Is.True);
                Assert.That(responses[0].Response.Answers,                    Has.Count.EqualTo(1));
                Assert.That(records[0].Source,                                Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(records[0].Record.Type,                           Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(records[0].Record.CacheFlush,                     Is.True);
                Assert.That(((A) records[0].Record.Record).IPv4Address,       Is.EqualTo(ServerAddress));
            });

        }

        #endregion

    }

}
