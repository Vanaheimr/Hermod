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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// End-to-end smoke tests of the Multicast DNS responder, client and browser on the
    /// in-memory network: publish, resolve, browse, negative answers, legacy unicast,
    /// goodbye and name conflicts.
    /// </summary>
    [TestFixture]
    public class MulticastDNS_SmokeTests
    {

        #region Helpers

        public static MulticastDNSResponderOptions FastResponderOptions()
            => new () {
                   ProbeInterval           = TimeSpan.FromMilliseconds(10),
                   MaxInitialProbeDelay    = TimeSpan.Zero,
                   AnnouncementInterval    = TimeSpan.FromMilliseconds(10),
                   MinSharedResponseDelay  = TimeSpan.Zero,
                   MaxSharedResponseDelay  = TimeSpan.FromMilliseconds(1),
                   MinTruncatedQueryDelay  = TimeSpan.FromMilliseconds(1),
                   MaxTruncatedQueryDelay  = TimeSpan.FromMilliseconds(2)
               };

        public static MulticastDNSClientOptions FastClientOptions()
            => new () {
                   QueryTimeout               = TimeSpan.FromMilliseconds(500),
                   ResponseGracePeriod        = TimeSpan.FromMilliseconds(20),
                   RetransmissionInterval     = TimeSpan.FromMilliseconds(100),
                   CacheSweepInterval         = TimeSpan.FromMilliseconds(50),
                   GoodbyeDelay               = TimeSpan.Zero,
                   BrowseMaintenanceInterval  = TimeSpan.FromMilliseconds(50),
                   InitialBrowseInterval      = TimeSpan.FromMilliseconds(50)
               };

        public static IDNSResourceRecord[] ServiceRecords(String   Host,
                                                          String   Address,
                                                          UInt16   Port,
                                                          String   ServiceType   = "_test._tcp.local.",
                                                          String?  Instance      = null)
        {

            var hostName      = DomainName.    Parse(Host);
            var serviceName   = DNSServiceName.Parse(ServiceType);
            var instanceName  = DNSServiceName.Parse($"{Instance ?? Host.Split('.')[0]}.{ServiceType}");

            return [
                new A  (hostName,     DNSQueryClasses.IN, TimeSpan.FromSeconds(120),  IPv4Address.Parse(Address)),
                new SRV(instanceName, DNSQueryClasses.IN, TimeSpan.FromSeconds(120),  0, 0, IPPort.Parse(Port), hostName),
                new TXT(instanceName, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), [ "txtver=1", $"url=https://{Host}:{Port}/" ]),
                new PTR(serviceName,  DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), instanceName)
            ];

        }

        private static async Task<T> WaitFor<T>(Func<T?> Probe, TimeSpan? Timeout = null)
            where T : class
        {

            var deadline = DateTimeOffset.UtcNow + (Timeout ?? TimeSpan.FromSeconds(5));

            while (DateTimeOffset.UtcNow < deadline)
            {
                var result = Probe();
                if (result is not null)
                    return result;
                await Task.Delay(10);
            }

            throw new TimeoutException("The condition was not met in time!");

        }

        private static async Task WaitUntil(Func<Boolean> Probe, TimeSpan? Timeout = null)
        {

            var deadline = DateTimeOffset.UtcNow + (Timeout ?? TimeSpan.FromSeconds(5));

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (Probe())
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException("The condition was not met in time!");

        }

        #endregion


        #region TXT_MultiString_RoundTrip()

        [Test]
        public void TXT_MultiString_RoundTrip()
        {

            var txt   = new TXT(DNSServiceName.Parse("myhost._test._tcp.local."),
                                DNSQueryClasses.IN,
                                TimeSpan.FromSeconds(4500),
                                [ "txtver=1", "e_name=Wärmepumpe Küche", "flag", "=ignored", "txtver=2" ]);

            var wire  = txt.ToWireFormat();

            var back  = DNSInfo.ReadResourceRecord(new MemoryStream(wire)) as TXT;

            Assert.That(back, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(back!.Strings,                       Is.EqualTo(new[] { "txtver=1", "e_name=Wärmepumpe Küche", "flag", "=ignored", "txtver=2" }));
                Assert.That(back.Text,                           Is.EqualTo("txtver=1e_name=Wärmepumpe Küche" + "flag" + "=ignored" + "txtver=2"));
                Assert.That(back.KeyValues["TXTVER"],            Is.EqualTo("1"));
                Assert.That(back.KeyValues["e_name"],            Is.EqualTo("Wärmepumpe Küche"));
                Assert.That(back.TryGetValue("flag", out var v), Is.True);
                Assert.That(v,                                   Is.Null);
                Assert.That(back.KeyValues.ContainsKey(""),      Is.False);
                Assert.That(back.DomainName.FullName,            Is.EqualTo("myhost._test._tcp.local."));
            });

        }

        #endregion

        #region Message_RoundTrip_KeepsBits()

        [Test]
        public void Message_RoundTrip_KeepsBits()
        {

            var a      = new A(DomainName.Parse("myhost.local."), DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7"));

            var query  = MulticastDNSMessage.Query(
                             [ new MulticastDNSQuestion(DNSServiceName.Parse("myhost.local."), DNSResourceRecordTypes.A, UnicastResponseRequested: true) ],
                             KnownAnswers: [ new MulticastDNSRecord(a, false, TimeSpan.FromSeconds(60)) ]
                         );

            Assert.That(MulticastDNSMessage.TryParse(query.Serialize(), out var parsedQuery, out var error), Is.True, error);
            Assert.Multiple(() => {
                Assert.That(parsedQuery!.IsQuery,                                  Is.True);
                Assert.That(parsedQuery.Questions,                                 Has.Count.EqualTo(1));
                Assert.That(parsedQuery.Questions[0].UnicastResponseRequested,     Is.True);
                Assert.That(parsedQuery.Questions[0].Class,                        Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(parsedQuery.Answers,                                   Has.Count.EqualTo(1));
                Assert.That(parsedQuery.Answers[0].CacheFlush,                     Is.False);
                Assert.That(parsedQuery.Answers[0].TimeToLive.TotalSeconds,        Is.EqualTo(60));
                Assert.That(parsedQuery.Warnings,                                  Is.Empty);
            });

            var response = MulticastDNSMessage.Response([ new MulticastDNSRecord(a, true) ]);

            Assert.That(MulticastDNSMessage.TryParse(response.Serialize(), out var parsedResponse, out error), Is.True, error);
            Assert.Multiple(() => {
                Assert.That(parsedResponse!.IsResponse,                            Is.True);
                Assert.That(parsedResponse.AuthoritativeAnswer,                    Is.True);
                Assert.That(parsedResponse.Answers[0].CacheFlush,                  Is.True);
                Assert.That(parsedResponse.Answers[0].Class,                       Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(((A) parsedResponse.Answers[0].Record).IPv4Address,    Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
                Assert.That(parsedResponse.Answers[0].Record.IsIdenticalTo(a),     Is.True);
            });

        }

        #endregion

        #region Publish_Resolve_Browse_Withdraw()

        [Test]
        public async Task Publish_Resolve_Browse_Withdraw()
        {

            var network    = new InMemoryMulticastDNSNetwork();

            await using var serverTransport  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var clientTransport  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));

            await using var responder  = new MulticastDNSResponder(serverTransport, FastResponderOptions());
            await using var client     = new MulticastDNSClient   (clientTransport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();

            var browser = await client.BrowseAsync(DNSServiceName.Parse("_test._tcp.local."));

            var added    = new List<MulticastDNSServiceInstance>();
            var removed  = new List<MulticastDNSServiceInstance>();

            browser.OnInstanceAdded   += (ts, b, instance, ct) => { lock (added)   added.  Add(instance); return Task.CompletedTask; };
            browser.OnInstanceRemoved += (ts, b, instance, ct) => { lock (removed) removed.Add(instance); return Task.CompletedTask; };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443));

            Assert.That(publication.State, Is.EqualTo(MulticastDNSPublicationState.Published));

            // Resolve the host name
            var addresses = (await client.Query_IPv4Addresses(DomainName.Parse("myhost.local."))).ToArray();
            Assert.That(addresses, Is.EqualTo(new[] { IPv4Address.Parse("10.0.0.7") }));

            // An IPv4-only host answers an AAAA query negatively (NSEC), without waiting for the timeout.
            var started  = DateTimeOffset.UtcNow;
            var ipv6     = (await client.Query_IPv6Addresses(DomainName.Parse("myhost.local."))).ToArray();
            var elapsed  = DateTimeOffset.UtcNow - started;
            Assert.Multiple(() => {
                Assert.That(ipv6,    Is.Empty);
                Assert.That(elapsed, Is.LessThan(TimeSpan.FromMilliseconds(400)), "the NSEC answer must end the query early");
            });

            // Browse
            var instance = await WaitFor(() => browser.Instances.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(instance.InstanceName.FullName,      Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(instance.HostName?.FullName,         Is.EqualTo("myhost.local."));
                Assert.That(instance.Port?.ToUInt16(),           Is.EqualTo((UInt16) 8443));
                Assert.That(instance.TXT?.KeyValues["url"],      Is.EqualTo("https://myhost.local.:8443/"));
            });

            await WaitUntil(() => instance.Addresses.Count > 0);
            Assert.That(instance.Addresses, Is.EqualTo(new IIPAddress[] { IPv4Address.Parse("10.0.0.7") }));

            lock (added)
                Assert.That(added, Has.Count.EqualTo(1));

            // Withdraw → goodbye → removed
            await publication.WithdrawAsync();

            Assert.That(publication.State, Is.EqualTo(MulticastDNSPublicationState.Withdrawn));

            await WaitUntil(() => { lock (removed) return removed.Count == 1; });
            Assert.That(browser.Instances, Is.Empty);

        }

        #endregion

        #region LegacyUnicastQuery_IsAnsweredUnicast()

        [Test]
        public async Task LegacyUnicastQuery_IsAnsweredUnicast()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var serverTransport = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var responder       = new MulticastDNSResponder(serverTransport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var sent = new List<(ReadOnlyMemory<Byte> Payload, IPSocket? Destination)>();
            network.OnDatagramSent += (ts, sender, payload, destination) => { lock (sent) sent.Add((payload, destination)); return Task.CompletedTask; };

            var legacySocket  = new IPSocket(IPv4Address.Parse("10.0.0.99"), IPPort.Parse(40000));
            var query         = MulticastDNSMessage.Query([ new MulticastDNSQuestion(DNSServiceName.Parse("myhost.local."), DNSResourceRecordTypes.A) ], TransactionId: 4711);

            await serverTransport.InjectAsync(query.Serialize(), legacySocket);

            var unicast = await WaitFor(() => { lock (sent) return sent.FirstOrDefault(s => s.Destination.HasValue) is var s && s.Destination.HasValue ? (Object) s : null; });
            var (payload, destination) = ((ReadOnlyMemory<Byte>, IPSocket?)) unicast;

            Assert.That(MulticastDNSMessage.TryParse(payload, out var response, out var error), Is.True, error);
            Assert.Multiple(() => {
                Assert.That(destination,                             Is.EqualTo(legacySocket));
                Assert.That(response!.TransactionId,                 Is.EqualTo((UInt16) 4711));
                Assert.That(response.Questions,                      Has.Count.EqualTo(1));
                Assert.That(response.Answers,                        Has.Count.EqualTo(1));
                Assert.That(response.Answers[0].CacheFlush,          Is.False);
                Assert.That(response.Answers[0].TimeToLive,          Is.LessThanOrEqualTo(TimeSpan.FromSeconds(10)));
            });

        }

        #endregion

        #region NameConflict_IsDetected()

        [Test]
        public async Task NameConflict_IsDetected()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var conflicts = new List<MulticastDNSPublication>();
            responder2.OnNameConflict += (ts, sender, publication, own, other, source, ct) => { lock (conflicts) conflicts.Add(publication); return Task.CompletedTask; };

            var first   = await responder1.PublishAsync([ new A(DomainName.Parse("myhost.local."), DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ]);
            var second  = await responder2.PublishAsync([ new A(DomainName.Parse("myhost.local."), DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.8")) ]);

            Assert.Multiple(() => {
                Assert.That(first. State,                          Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.State,                          Is.EqualTo(MulticastDNSPublicationState.Conflict));
                Assert.That(second.ConflictingRecord,              Is.Not.Null);
                Assert.That(((A) second.ConflictingRecord!).IPv4Address, Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
                Assert.That(conflicts,                             Has.Count.EqualTo(1));
                Assert.That(responder2.ActiveRecords,              Is.Empty);
            });

        }

        #endregion

        #region HybridClient_RoutesLocalNames()

        [Test]
        public async Task HybridClient_RoutesLocalNames()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());
            await using var client     = new MulticastDNSClient   (transport, FastClientOptions());

            await responder.StartAsync();
            await client.   StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var unicast  = new RecordingDNSClient();
            var hybrid   = new HybridDNSClient(client, unicast);

            var local    = (await hybrid.Query_IPv4Addresses(DomainName.Parse("myhost.local."))).ToArray();
            var other    = (await hybrid.Query_IPv4Addresses(DomainName.Parse("example.com."))).ToArray();

            Assert.Multiple(() => {
                Assert.That(local,            Is.EqualTo(new[] { IPv4Address.Parse("10.0.0.7") }));
                Assert.That(other,            Is.Empty);
                Assert.That(unicast.Queries,  Is.EqualTo(new[] { "example.com." }));
            });

        }

        private sealed class RecordingDNSClient : IDNSClient
        {

            public List<String> Queries { get; } = [];

            public override String ToString()
                => "recording DNS client";

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null, Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Query(DNSServiceName.Parse(DomainName.FullName), ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null, Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
            {
                Queries.Add(DNSServiceName.FullName);
                return Task.FromResult(DNSInfo.TimedOut(new DNSServerConfig(IPv4Address.Localhost), 0, TimeSpan.Zero));
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        }

        #endregion

    }

}
