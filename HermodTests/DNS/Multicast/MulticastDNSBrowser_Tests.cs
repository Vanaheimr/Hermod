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
    /// Tests of the continuous DNS-SD browser (RFC 6763 §4, RFC 6762 §5.2) on the in-memory
    /// network: resolving instances, updates, removal by goodbye and by expiry, seeding from
    /// the cache, subtypes, the refresh of ageing records, the query loop and stopping.
    /// </summary>
    [TestFixture]
    public class MulticastDNSBrowser_Tests
    {

        #region Data

        private static readonly IPv4Address  ServerAddress  = IPv4Address.Parse("10.0.0.7");
        private static readonly IPv4Address  ClientAddress  = IPv4Address.Parse("10.0.0.8");

        #endregion


        #region Instance_IsReportedOnlyOnceResolved_WithHostPortTXTAndAddresses()

        [Test]
        public async Task Instance_IsReportedOnlyOnceResolved_WithHostPortTXTAndAddresses()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var resolvedWhenAdded = new List<Boolean>();

            browser.OnInstanceAdded += (timestamp, sender, instance, ct) => {
                lock (added)
                {
                    added.Add(instance);
                    resolvedWhenAdded.Add(instance.IsResolved);
                }
                return Task.CompletedTask;
            };

            Assert.Multiple(() => {
                Assert.That(browser.IsRunning,     Is.True);
                Assert.That(browser.ServiceType,   Is.EqualTo(TestService));
                Assert.That(browser.Client,        Is.SameAs(client));
                Assert.That(client.Browsers,       Does.Contain(browser));
            });

            // Only the PTR record: the instance is known, but neither resolved nor reported.
            await responder.PublishAsync([ new PTR(TestService, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), MyInstance) ], Probe: false);

            await WaitUntil(() => browser.AllInstances.Count == 1);
            await Task.Delay(50);

            Assert.Multiple(() => {
                Assert.That(browser.Instances,                     Is.Empty, "an unresolved instance is not listed");
                Assert.That(browser.AllInstances[0].IsResolved,    Is.False);
                Assert.That(browser.AllInstances[0].InstanceName,  Is.EqualTo(MyInstance));
                lock (added)
                    Assert.That(added,                             Is.Empty, "and not reported");
            });

            // SRV, TXT and A complete it.
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443).Where(record => record is not PTR), Probe: false);

            var resolved = await WaitFor(() => { lock (added) return added.FirstOrDefault(); });

            Assert.Multiple(() => {
                Assert.That(resolved.IsResolved,                   Is.True);
                Assert.That(resolvedWhenAdded,                     Is.EqualTo(new[] { true }), "reported only once host, port and TXT are known");
                Assert.That(resolved.InstanceName.FullName,        Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(resolved.ServiceType,                  Is.EqualTo(TestService));
                Assert.That(resolved.HostName?.FullName,           Is.EqualTo("myhost.local."));
                Assert.That(resolved.Port?.ToUInt16(),             Is.EqualTo((UInt16) 8443));
                Assert.That(resolved.Priority,                     Is.EqualTo((UInt16) 0));
                Assert.That(resolved.Weight,                       Is.EqualTo((UInt16) 0));
                Assert.That(resolved.TXT?.KeyValues["txtver"],     Is.EqualTo("1"));
                Assert.That(resolved.TXT?.KeyValues["url"],        Is.EqualTo("https://myhost.local.:8443/"));
                Assert.That(resolved.FirstSeen,                    Is.LessThanOrEqualTo(resolved.LastSeen));
                Assert.That(browser.Instances,                     Has.Count.EqualTo(1));
                Assert.That(browser.Instances[0],                  Is.SameAs(resolved));
            });

            await WaitUntil(() => resolved.Addresses.Count > 0);

            Assert.Multiple(() => {
                Assert.That(resolved.Addresses,  Is.EqualTo(new IIPAddress[] { ServerAddress }));
                lock (added)
                    Assert.That(added,           Has.Count.EqualTo(1), "reported exactly once");
            });

        }

        #endregion

        #region TwoInstancesOnTwoHosts_AreBothListed_AndWithdrawingOneRemovesOnlyThatOne()

        [Test]
        public async Task TwoInstancesOnTwoHosts_AreBothListed_AndWithdrawingOneRemovesOnlyThatOne()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var serverTransport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.9"));
            await using var clientTransport   = network.CreateTransport(ClientAddress);
            await using var responder1        = new MulticastDNSResponder(serverTransport1, FastResponderOptions());
            await using var responder2        = new MulticastDNSResponder(serverTransport2, FastResponderOptions());
            await using var client            = new MulticastDNSClient   (clientTransport,  FastClientOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();
            await client.    StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var removed  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (timestamp, sender, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceRemoved += (timestamp, sender, instance, ct) => { lock (removed) removed.Add(instance); return Task.CompletedTask; };

            var first   = await responder1.PublishAsync(ServiceRecords("host1.local.", "10.0.0.7", 8443), Probe: false);
            var second  = await responder2.PublishAsync(ServiceRecords("host2.local.", "10.0.0.9", 8444), Probe: false);

            await WaitUntil(() => browser.Instances.Count == 2);

            Assert.Multiple(() => {
                Assert.That(browser.Instances.Select(instance => instance.InstanceName.FullName),  Is.EquivalentTo(new[] { "host1._test._tcp.local.", "host2._test._tcp.local." }));
                Assert.That(browser.Instances.Select(instance => instance.HostName?.FullName),      Is.EquivalentTo(new[] { "host1.local.", "host2.local." }));
                lock (added)
                    Assert.That(added,                                                             Has.Count.EqualTo(2));
            });

            await first.WithdrawAsync();

            await WaitUntil(() => { lock (removed) return removed.Count == 1; });
            await Task.Delay(50);

            Assert.Multiple(() => {
                lock (removed)
                {
                    Assert.That(removed,                                  Has.Count.EqualTo(1));
                    Assert.That(removed[0].InstanceName.FullName,         Is.EqualTo("host1._test._tcp.local."));
                }
                Assert.That(browser.Instances,                            Has.Count.EqualTo(1));
                Assert.That(browser.Instances[0].InstanceName.FullName,   Is.EqualTo("host2._test._tcp.local."));
                Assert.That(browser.AllInstances,                         Has.Count.EqualTo(1));
                Assert.That(second.State,                                 Is.EqualTo(MulticastDNSPublicationState.Published));
            });

        }

        #endregion

        #region TXTUpdate_RaisesOnInstanceUpdated_WithTheNewTXTRecord()

        [Test]
        public async Task TXTUpdate_RaisesOnInstanceUpdated_WithTheNewTXTRecord()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var updated  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (timestamp, sender, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceUpdated += (timestamp, sender, instance, ct) => { lock (updated) updated.Add(instance); return Task.CompletedTask; };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443, TXTStrings: [ "txtver=1", "state=idle" ]), Probe: false);

            var instance = await WaitFor(() => { lock (added) return added.FirstOrDefault(); });
            await WaitUntil(() => instance.Addresses.Count > 0);

            Assert.That(instance.TXT?.KeyValues["state"], Is.EqualTo("idle"));

            await publication.UpdateAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443, TXTStrings: [ "txtver=1", "state=charging" ]));

            await WaitUntil(() => { lock (updated) return updated.Count >= 1; });
            await Task.Delay(50);

            Assert.Multiple(() => {
                lock (updated)
                {
                    Assert.That(updated,                          Has.Count.EqualTo(1), "one update for the changed TXT record, none for the unchanged records");
                    Assert.That(updated[0],                       Is.SameAs(instance));
                }
                Assert.That(instance.TXT?.KeyValues["state"],     Is.EqualTo("charging"));
                Assert.That(instance.TXT?.KeyValues["txtver"],    Is.EqualTo("1"));
                Assert.That(instance.HostName?.FullName,          Is.EqualTo("myhost.local."));
                Assert.That(instance.Addresses,                   Is.EqualTo(new IIPAddress[] { ServerAddress }));
                Assert.That(browser.Instances,                    Has.Count.EqualTo(1));
                lock (added)
                    Assert.That(added,                            Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region AddressChange_RaisesOnInstanceUpdated_AndReplacesTheAddress()

        [Test]
        public async Task AddressChange_RaisesOnInstanceUpdated_AndReplacesTheAddress()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var clock    = new ShiftableTimeProvider();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions(), clock);

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var updated  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (timestamp, sender, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceUpdated += (timestamp, sender, instance, ct) => { lock (updated) updated.Add(instance); return Task.CompletedTask; };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var instance = await WaitFor(() => { lock (added) return added.FirstOrDefault(); });
            await WaitUntil(() => instance.Addresses.Count > 0);

            Assert.That(instance.Addresses, Is.EqualTo(new IIPAddress[] { IPv4Address.Parse("10.0.0.7") }));

            // RFC 6762 §10.2: a cache-flush only retires records received more than one second
            // ago, so the cached A record is aged beyond that before the host changes its address.
            clock.Shift(TimeSpan.FromSeconds(2));

            await publication.UpdateAsync(ServiceRecords("myhost.local.", "10.0.0.9", 8443));

            await WaitUntil(() => { lock (updated) return updated.Count >= 1; });

            Assert.Multiple(() => {
                Assert.That(instance.Addresses,  Does.Contain(IPv4Address.Parse("10.0.0.9")), "the new address is known at once");
                Assert.That(instance.Addresses,  Does.Contain(IPv4Address.Parse("10.0.0.7")), "the old one lives one more second");
            });

            // One second later the old record expires and leaves the instance.
            clock.Shift(TimeSpan.FromSeconds(1.1));

            await WaitUntil(() => { lock (updated) return updated.Count >= 2; });

            Assert.Multiple(() => {
                Assert.That(instance.Addresses,             Is.EqualTo(new IIPAddress[] { IPv4Address.Parse("10.0.0.9") }));
                Assert.That(instance.HostName?.FullName,    Is.EqualTo("myhost.local."));
                Assert.That(browser.Instances,              Has.Count.EqualTo(1));
                lock (updated)
                    Assert.That(updated.All(reported => ReferenceEquals(reported, instance)), Is.True);
                lock (added)
                    Assert.That(added,                      Has.Count.EqualTo(1));
            });

        }

        #endregion


        #region Browser_IsSeededFromTheCache_WithoutAnAddedEvent()

        [Test]
        public async Task Browser_IsSeededFromTheCache_WithoutAnAddedEvent()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();

            // Published before the client listens: its cache starts empty.
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            await client.StartAsync();

            Assert.That(client.CacheSize, Is.EqualTo(0));

            // A one-shot query resolves the service (PTR plus SRV, TXT and A as additionals) into the cache.
            var info = await client.Query(TestService, [ DNSResourceRecordTypes.PTR ]);

            Assert.Multiple(() => {
                Assert.That(info.Answers.OfType<PTR>().Count(),  Is.EqualTo(1));
                Assert.That(client.CacheSize,                    Is.EqualTo(4));
            });

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded += (timestamp, sender, instance, ct) => { lock (added) added.Add(instance); return Task.CompletedTask; };

            // Seeded from the cache: resolved immediately, without waiting for the network.
            Assert.That(browser.Instances, Has.Count.EqualTo(1));

            var instance = browser.Instances[0];

            Assert.Multiple(() => {
                Assert.That(instance.InstanceName.FullName,   Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(instance.HostName?.FullName,      Is.EqualTo("myhost.local."));
                Assert.That(instance.Port?.ToUInt16(),        Is.EqualTo((UInt16) 8443));
                Assert.That(instance.TXT?.KeyValues["txtver"], Is.EqualTo("1"));
                Assert.That(instance.Addresses,               Is.EqualTo(new IIPAddress[] { ServerAddress }));
            });

            await Task.Delay(150);

            Assert.Multiple(() => {
                lock (added)
                    Assert.That(added,             Is.Empty, "a seeded instance is not reported as newly added");
                Assert.That(browser.Instances,     Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region SubtypeBrowsing_FindsInstancesOfTheSubtype_AndNothingElse()

        [Test]
        public async Task SubtypeBrowsing_FindsInstancesOfTheSubtype_AndNothingElse()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var cemSubtype  = DNSServiceName.Parse("_cem._sub._test._tcp.local.");
            var rmSubtype   = DNSServiceName.Parse("_rm._sub._test._tcp.local.");

            var cemBrowser  = await client.BrowseAsync(cemSubtype);
            var rmBrowser   = await client.BrowseAsync(rmSubtype);

            var cemAdded    = new List<MulticastDNSServiceInstance>();
            var rmAdded     = new List<MulticastDNSServiceInstance>();

            cemBrowser.OnInstanceAdded += (timestamp, sender, instance, ct) => { lock (cemAdded) cemAdded.Add(instance); return Task.CompletedTask; };
            rmBrowser. OnInstanceAdded += (timestamp, sender, instance, ct) => { lock (rmAdded)  rmAdded. Add(instance); return Task.CompletedTask; };

            // RFC 6763 §7.1: the instance is also a member of the "_cem" subtype.
            await responder.PublishAsync([
                      .. ServiceRecords("myhost.local.", "10.0.0.7", 8443),
                      new PTR(cemSubtype, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), MyInstance)
                  ], Probe: false);

            var instance = await WaitFor(() => { lock (cemAdded) return cemAdded.FirstOrDefault(); });
            await WaitUntil(() => instance.Addresses.Count > 0);
            await Task.Delay(100);

            Assert.Multiple(() => {
                Assert.That(instance.InstanceName.FullName,   Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(instance.ServiceType,             Is.EqualTo(cemSubtype));
                Assert.That(instance.HostName?.FullName,      Is.EqualTo("myhost.local."));
                Assert.That(instance.Port?.ToUInt16(),        Is.EqualTo((UInt16) 8443));
                Assert.That(instance.TXT,                     Is.Not.Null);
                Assert.That(instance.Addresses,               Is.EqualTo(new IIPAddress[] { ServerAddress }));
                Assert.That(cemBrowser.Instances,             Has.Count.EqualTo(1));
                Assert.That(rmBrowser. Instances,             Is.Empty, "no PTR record names the _rm subtype");
                Assert.That(rmBrowser. AllInstances,          Is.Empty);
                lock (rmAdded)
                    Assert.That(rmAdded,                      Is.Empty);
                Assert.That(client.Browsers,                  Has.Count.EqualTo(2));
            });

        }

        #endregion


        #region PTRExpiry_RemovesTheInstance_WhenTheHostFallsSilent()

        [Test]
        public async Task PTRExpiry_RemovesTheInstance_WhenTheHostFallsSilent()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var removed  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (timestamp, sender, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceRemoved += (timestamp, sender, instance, ct) => { lock (removed) removed.Add(instance); return Task.CompletedTask; };

            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443, PTRTimeToLive: TimeSpan.FromSeconds(1)), Probe: false);

            var instance  = await WaitFor(() => { lock (added) return added.FirstOrDefault(); });
            var silenced  = DateTimeOffset.UtcNow;

            // The host vanishes without a goodbye: nothing it sends reaches the link any more.
            network.Filter = (sender, receiver) => sender != serverTransport;

            await WaitUntil(() => { lock (removed) return removed.Count == 1; }, TimeSpan.FromSeconds(3));

            var elapsed = DateTimeOffset.UtcNow - silenced;

            Assert.Multiple(() => {
                lock (removed)
                    Assert.That(removed[0],           Is.SameAs(instance));
                Assert.That(elapsed,                  Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)), "the PTR record lived its full second");
                Assert.That(elapsed,                  Is.LessThan(TimeSpan.FromSeconds(2.5)));
                Assert.That(browser.Instances,        Is.Empty);
                Assert.That(browser.AllInstances,     Is.Empty);
                Assert.That(client.CachedRecords(TestService, DNSResourceRecordTypes.PTR), Is.Empty);
            });

        }

        #endregion

        #region Refresh_ReQueriesThePTRAtEightyPercentOfItsLifetime_AndKeepsTheInstance()

        [Test]
        public async Task Refresh_ReQueriesThePTRAtEightyPercentOfItsLifetime_AndKeepsTheInstance()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            // A long browse interval, so that every PTR query after the first one is a refresh.
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions(InitialBrowseInterval: TimeSpan.FromSeconds(10)));

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var removed  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceRemoved += (timestamp, sender, instance, ct) => { lock (removed) removed.Add(instance); return Task.CompletedTask; };

            // The query loop's first query.
            await WaitUntil(() => capture.PTRQueriesFor(TestService).Count == 1);

            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443, PTRTimeToLive: TimeSpan.FromSeconds(2)), Probe: false);

            var published = DateTimeOffset.UtcNow;

            await WaitUntil(() => browser.Instances.Count == 1);

            // RFC 6762 §5.2: at 80 % of the lifetime the browser asks again.
            var refresh = await WaitFor(() => capture.PTRQueriesFor(TestService).FirstOrDefault(query => query.Timestamp > published),
                                        TimeSpan.FromSeconds(3));

            Assert.Multiple(() => {
                Assert.That(refresh.Timestamp - published,   Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.4)).And.LessThan(TimeSpan.FromSeconds(2)));
                Assert.That(refresh.Sender,                  Is.SameAs(clientTransport));
                Assert.That(refresh.IsMulticast,             Is.True);
                Assert.That(refresh.Message.Answers,         Is.Empty, "a record about to expire is no known answer");
            });

            // The responder answers the refresh: the PTR record is renewed and the instance stays.
            await WaitUntil(() => client.CachedRecords(TestService, DNSResourceRecordTypes.PTR).Any(entry => entry.ExpiresAt > published + TimeSpan.FromSeconds(3)));

            Assert.Multiple(() => {
                Assert.That(browser.Instances,  Has.Count.EqualTo(1));
                lock (removed)
                    Assert.That(removed,        Is.Empty);
            });

        }

        #endregion

        #region QueryLoop_SendsAnInitialPTRQuery_AndRepeatsItWithADoublingInterval()

        [Test]
        public async Task QueryLoop_SendsAnInitialPTRQuery_AndRepeatsItWithADoublingInterval()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.StartAsync();

            var browser = await client.BrowseAsync(TestService);

            // Queries at 0, 50, 150 and 350 ms.
            await Task.Delay(400);

            var queries = capture.PTRQueriesFor(TestService);

            Assert.That(queries, Has.Count.GreaterThanOrEqualTo(3));

            Assert.Multiple(() => {

                foreach (var query in queries)
                {
                    Assert.That(query.IsMulticast,                                     Is.True);
                    Assert.That(query.Sender,                                          Is.SameAs(clientTransport));
                    Assert.That(query.Message.Questions,                               Has.Count.EqualTo(1));
                    Assert.That(query.Message.Questions[0].Name,                       Is.EqualTo(TestService));
                    Assert.That(query.Message.Questions[0].Type,                       Is.EqualTo(DNSResourceRecordTypes.PTR));
                    Assert.That(query.Message.Questions[0].UnicastResponseRequested,   Is.False, "a continuous querier asks for multicast responses");
                    Assert.That(query.Message.Answers,                                 Is.Empty);
                }

                var firstGap   = queries[1].Timestamp - queries[0].Timestamp;
                var secondGap  = queries[2].Timestamp - queries[1].Timestamp;

                Assert.That(secondGap,  Is.GreaterThan(firstGap), "RFC 6762 §5.2: the interval doubles");
                Assert.That(capture.Responses, Is.Empty);

            });

            await browser.StopAsync();

        }

        #endregion

        #region QueryNowAsync_IncludesTheKnownPTRRecords()

        [Test]
        public async Task QueryNowAsync_IncludesTheKnownPTRRecords()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser = await client.BrowseAsync(TestService);

            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            await WaitUntil(() => browser.Instances.Count == 1);

            capture.Clear();

            await browser.QueryNowAsync();

            var query = capture.PTRQueriesFor(TestService).FirstOrDefault();

            Assert.That(query, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(query!.Message.Questions,                                     Has.Count.EqualTo(1));
                Assert.That(query.Message.Questions[0].Type,                              Is.EqualTo(DNSResourceRecordTypes.PTR));
                Assert.That(query.Message.Answers,                                        Has.Count.EqualTo(1), "RFC 6762 §7.1: the known PTR record");
                Assert.That(query.Message.Answers[0].Type,                                Is.EqualTo(DNSResourceRecordTypes.PTR));
                Assert.That(query.Message.Answers[0].CacheFlush,                          Is.False);
                Assert.That(((PTR) query.Message.Answers[0].Record).Target.FullName,      Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(query.Message.Answers[0].TimeToLive,                          Is.InRange(TimeSpan.FromSeconds(4400), TimeSpan.FromSeconds(4500)), "the remaining time-to-live");
            });

        }

        #endregion


        #region Stop_UnsubscribesTheBrowser_AndRemovesItFromTheClient()

        [Test]
        public async Task Stop_UnsubscribesTheBrowser_AndRemovesItFromTheClient()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();
            var removed  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (timestamp, sender, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceRemoved += (timestamp, sender, instance, ct) => { lock (removed) removed.Add(instance); return Task.CompletedTask; };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            await WaitUntil(() => { lock (added) return added.Count == 1; });

            await browser.StopAsync();

            Assert.Multiple(() => {
                Assert.That(browser.IsRunning,   Is.False);
                Assert.That(client.Browsers,     Does.Not.Contain(browser));
                Assert.That(client.Browsers,     Is.Empty);
                Assert.That(client.IsRunning,    Is.True, "the client keeps running");
            });

            capture.Clear();

            // Neither a goodbye nor a new instance reaches a stopped browser, and it sends no more queries.
            await publication.WithdrawAsync();
            await responder.PublishAsync(ServiceRecords("other.local.", "10.0.0.7", 8444), Probe: false);

            await Task.Delay(200);

            Assert.Multiple(() => {
                lock (removed)
                    Assert.That(removed,                                                          Is.Empty);
                lock (added)
                    Assert.That(added,                                                            Has.Count.EqualTo(1));
                Assert.That(capture.Queries.Where(query => query.Sender == clientTransport),      Is.Empty, "the query loop stopped");
            });

            // Stopping twice is harmless.
            Assert.DoesNotThrowAsync(() => browser.StopAsync());

        }

        #endregion

        #region Instances_AreUnaffectedByRecordsOfOtherServiceTypes()

        [Test]
        public async Task Instances_AreUnaffectedByRecordsOfOtherServiceTypes()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(ServerAddress);
            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var responder        = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client           = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var added    = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded += (timestamp, sender, instance, ct) => { lock (added) added.Add(instance); return Task.CompletedTask; };

            // A printer of another service type on the same link.
            await responder.PublishAsync(ServiceRecords("printer.local.", "10.0.0.7", 631, "_ipp._tcp.local."), Probe: false);

            await Task.Delay(100);

            Assert.Multiple(() => {
                Assert.That(browser.Instances,     Is.Empty);
                Assert.That(browser.AllInstances,  Is.Empty, "not even an unresolved instance");
                lock (added)
                    Assert.That(added,             Is.Empty);
                Assert.That(client.CacheSize,      Is.EqualTo(4), "the client caches the records nevertheless");
            });

            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            await WaitUntil(() => browser.Instances.Count == 1);

            Assert.Multiple(() => {
                Assert.That(browser.Instances[0].InstanceName.FullName,   Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(browser.Instances[0].ServiceType,             Is.EqualTo(TestService));
                Assert.That(browser.AllInstances,                         Has.Count.EqualTo(1));
                lock (added)
                    Assert.That(added,                                    Has.Count.EqualTo(1));
            });

        }

        #endregion

    }

}
