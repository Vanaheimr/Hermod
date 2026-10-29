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
    /// The rules a Multicast DNS querier keeps when it asks again and when it lists what it
    /// knows (RFC 6762 §5.2, §5.4, §7.1 to §7.3, §17), and the two RFC 6763 rules a browser
    /// keeps when it reads TXT and SRV records (§5, §6.1).
    /// </summary>
    [TestFixture]
    public class MulticastDNSQuerierRules_Tests
    {

        #region Data

        private static readonly IPv4Address  ClientAddress  = IPv4Address.Parse("10.0.0.8");
        private static readonly IPv4Address  PeerAddress    = IPv4Address.Parse("10.0.0.9");

        #endregion

        #region (private) Answer(Records)

        /// <summary>
        /// A response carrying the given records, unique ones with the cache-flush bit.
        /// </summary>
        private static Byte[] Answer(params IDNSResourceRecord[] Records)

            => MulticastDNSMessage.Response(
                   Records.Select(record => new MulticastDNSRecord(record, record.Type != DNSResourceRecordTypes.PTR))
               ).Serialize();

        #endregion


        #region TheDefaults_AreTheOnesTheRfcNames()

        [Test]
        public void TheDefaults_AreTheOnesTheRfcNames()
        {

            var options = new MulticastDNSClientOptions();

            Assert.Multiple(() => {
                Assert.That(options.RetransmissionInterval,  Is.EqualTo(TimeSpan.FromSeconds(1)),         "RFC 6762 §5.2: at least one second between the first two queries");
                Assert.That(options.MinInitialBrowseDelay,   Is.EqualTo(TimeSpan.FromMilliseconds(20)),   "RFC 6762 §5.2: 20-120 ms");
                Assert.That(options.MaxInitialBrowseDelay,   Is.EqualTo(TimeSpan.FromMilliseconds(120)),  "RFC 6762 §5.2: 20-120 ms");
                Assert.That(options.MaxQuerySize,            Is.EqualTo(1472),                            "an Ethernet MTU less the IPv4 and UDP headers");
            });

        }

        #endregion

        #region Retransmissions_AreQMQuestions_AtDoublingIntervals()

        [Test]
        public async Task Retransmissions_AreQMQuestions_AtDoublingIntervals()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.StartAsync();

            // A 500 ms query with a first retransmission after 100 ms: queries at 0, 100 and
            // 300 ms, and none at the deadline.
            await client.Query(DNSServiceName.Parse("nobody.local."), [ DNSResourceRecordTypes.A ]);

            var queries = capture.Queries;

            Assert.That(queries, Has.Count.EqualTo(3));

            var firstGap   = queries[1].Timestamp - queries[0].Timestamp;
            var secondGap  = queries[2].Timestamp - queries[1].Timestamp;

            Assert.Multiple(() => {
                Assert.That(queries[0].Message.Questions[0].UnicastResponseRequested,                                Is.True,  "the first question asks for a unicast reply");
                Assert.That(queries.Skip(1).Select(query => query.Message.Questions[0].UnicastResponseRequested),    Is.All.False, "RFC 6762 §5.4: retransmissions are QM questions");
                Assert.That(secondGap, Is.GreaterThanOrEqualTo(firstGap * 2 - TimeSpan.FromMilliseconds(30)),       "RFC 6762 §5.2: every interval at least twice the one before");
            });

        }

        #endregion

        #region TheFirstBrowseQuery_IsDelayed()

        [Test]
        public async Task TheFirstBrowseQuery_IsDelayed()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.StartAsync();

            var started  = DateTimeOffset.UtcNow;
            var browser  = await client.BrowseAsync(TestService);

            await WaitUntil(() => capture.PTRQueriesFor(TestService).Count > 0);

            Assert.That(capture.PTRQueriesFor(TestService)[0].Timestamp - started, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15)),
                        "RFC 6762 §5.2: the first query of the series is delayed by 20-120 ms");

            await browser.StopAsync();

        }

        #endregion

        #region ADuplicateQuestion_CountsAsAsked_UnlessItKnowsMore()

        [TestCase(false, ExpectedResult = 1)]
        [TestCase(true,  ExpectedResult = 2)]
        public async Task<Int32> ADuplicateQuestion_CountsAsAsked_UnlessItKnowsMore(Boolean WithAnUnknownKnownAnswer)
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var peerTransport    = network.CreateTransport(PeerAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions(InitialBrowseInterval: TimeSpan.FromMilliseconds(300)));

            await client.       StartAsync();
            await peerTransport.StartAsync();

            var browser = await client.BrowseAsync(TestService);

            await WaitUntil(() => capture.PTRQueriesFor(TestService).Count == 1);

            // The other host asks the same QM question 100 ms later. A known answer the browser
            // does not have means the other host's query may suppress answers this one needs.
            await Task.Delay(100);

            await peerTransport.SendAsync(MulticastDNSMessage.Query(
                                              [ new MulticastDNSQuestion(TestService, DNSResourceRecordTypes.PTR) ],
                                              WithAnUnknownKnownAnswer
                                                  ? [ new MulticastDNSRecord(new PTR(TestService, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), DNSServiceName.Parse("other._test._tcp.local."))) ]
                                                  : null
                                          ).Serialize());

            // The browser's own second query was due 300 ms after its first.
            await Task.Delay(400);

            var count = capture.PTRQueriesFor(TestService).Count(query => query.Sender == clientTransport);

            await browser.StopAsync();

            return count;

        }

        #endregion

        #region KnownAnswers_BelowHalfTheirTimeToLive_AreNotListed()

        [Test]
        public async Task KnownAnswers_BelowHalfTheirTimeToLive_AreNotListed()
        {

            var clock    = new ShiftableTimeProvider();
            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var peerTransport    = network.CreateTransport(PeerAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions(InitialBrowseInterval: TimeSpan.FromHours(1)), clock);

            await client.       StartAsync();
            await peerTransport.StartAsync();

            var browser = await client.BrowseAsync(TestService);

            // The first query of the series comes 20-120 ms late, and must not be counted below.
            await WaitUntil(() => capture.PTRQueriesFor(TestService).Count > 0);

            await peerTransport.SendAsync(Answer(new PTR(TestService, DNSQueryClasses.IN, TimeSpan.FromSeconds(100), MyInstance)));

            clock.Shift(TimeSpan.FromSeconds(40));
            capture.Clear();
            await browser.QueryNowAsync();
            var atSixty = capture.PTRQueriesFor(TestService).Single().Message.Answers.Count;

            clock.Shift(TimeSpan.FromSeconds(20));
            capture.Clear();
            await browser.QueryNowAsync();
            var atForty = capture.PTRQueriesFor(TestService).Single().Message.Answers.Count;

            Assert.Multiple(() => {
                Assert.That(atSixty, Is.EqualTo(1), "sixty of a hundred seconds left");
                Assert.That(atForty, Is.EqualTo(0), "RFC 6762 §7.1: not below half of the original TTL");
            });

            await browser.StopAsync();

        }

        #endregion

        #region ManyKnownAnswers_ContinueInTruncatedPackets()

        [Test]
        public async Task ManyKnownAnswers_ContinueInTruncatedPackets()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var peerTransport    = network.CreateTransport(PeerAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions(InitialBrowseInterval: TimeSpan.FromHours(1)));

            await client.       StartAsync();
            await peerTransport.StartAsync();

            var browser   = await client.BrowseAsync(TestService);
            var pointers  = Enumerable.Range(1, 250).
                                       Select(n => new PTR(TestService,
                                                           DNSQueryClasses.IN,
                                                           TimeSpan.FromSeconds(4500),
                                                           DNSServiceName.Parse($"Instance {n:D3} with a name long enough to matter._test._tcp.local."))).
                                       ToArray();

            await WaitUntil(() => capture.PTRQueriesFor(TestService).Count > 0);

            await peerTransport.SendAsync(Answer(pointers));

            capture.Clear();
            await browser.QueryNowAsync();

            var packets  = capture.Queries.Where(query => query.Sender == clientTransport).ToArray();
            var sizes    = packets.Select(packet => packet.Message.Serialize().Length).ToArray();

            Assert.Multiple(() => {
                Assert.That(packets,                                                       Has.Length.GreaterThan(1));
                Assert.That(sizes,                                                         Is.All.LessThanOrEqualTo(1472), "RFC 6762 §17");
                Assert.That(packets.SkipLast(1).Select(packet => packet.Message.Truncated), Is.All.True,   "RFC 6762 §7.2: TC on every packet but the last");
                Assert.That(packets.Last().Message.Truncated,                              Is.False);
                Assert.That(packets[0].Message.Questions,                                  Has.Count.EqualTo(1));
                Assert.That(packets.Skip(1).Select(packet => packet.Message.Questions.Count), Is.All.EqualTo(0), "RFC 6762 §7.2: no questions in the packets that follow");
                Assert.That(packets.Sum(packet => packet.Message.Answers.Count),           Is.EqualTo(250));
            });

            await browser.StopAsync();

        }

        #endregion

        #region AnEmptyTXT_IsNoChange_ToAnInstanceWithoutOne()

        [Test]
        public async Task AnEmptyTXT_IsNoChange_ToAnInstanceWithoutOne()
        {

            var network  = new InMemoryMulticastDNSNetwork();

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var peerTransport    = network.CreateTransport(PeerAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.       StartAsync();
            await peerTransport.StartAsync();

            var browser  = await client.BrowseAsync(TestService);
            var updates  = 0;

            browser.OnInstanceUpdated += (_, _, _, _) => { Interlocked.Increment(ref updates); return Task.CompletedTask; };

            var records  = ServiceRecords("myhost.local.", "10.0.0.7", 8443);

            await peerTransport.SendAsync(Answer([.. records.Where(record => record is not TXT)]));
            await WaitUntil(() => browser.Instances.Count == 1);

            await peerTransport.SendAsync(Answer(new TXT(MyInstance, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), [ "" ])));
            await Task.Delay(100);

            Assert.That(updates, Is.EqualTo(0), "RFC 6763 §6.1: no TXT record and an empty one are equivalent");

            await browser.StopAsync();

        }

        #endregion

        #region ThePreferredSRV_DescribesTheInstance_InEitherOrder()

        [TestCase(true)]
        [TestCase(false)]
        public async Task ThePreferredSRV_DescribesTheInstance_InEitherOrder(Boolean PreferredFirst)
        {

            var network  = new InMemoryMulticastDNSNetwork();

            await using var clientTransport  = network.CreateTransport(ClientAddress);
            await using var peerTransport    = network.CreateTransport(PeerAddress);
            await using var client           = new MulticastDNSClient(clientTransport, FastClientOptions());

            await client.       StartAsync();
            await peerTransport.StartAsync();

            var browser    = await client.BrowseAsync(TestService);

            var preferred  = new SRV(MyInstance, DNSQueryClasses.IN, TimeSpan.FromSeconds(120),  0, 0, IPPort.Parse(1000), DomainName.Parse("first-choice.local."));
            var fallback   = new SRV(MyInstance, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), 10, 0, IPPort.Parse(2000), DomainName.Parse("second-choice.local."));

            await peerTransport.SendAsync(Answer([ new PTR(TestService, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), MyInstance),
                                                   .. PreferredFirst ? new IDNSResourceRecord[] { preferred, fallback } : [ fallback, preferred ] ]));

            await WaitUntil(() => browser.Instances.Count == 1);

            var instance = browser.Instances.Single();

            Assert.Multiple(() => {
                Assert.That(instance.Priority,           Is.EqualTo((UInt16) 0), "RFC 6763 §5: lower-numbered priority servers should be used in preference");
                Assert.That(instance.Port?.ToUInt16(),   Is.EqualTo((UInt16) 1000));
                Assert.That(instance.HostName?.FullName, Is.EqualTo("first-choice.local."));
            });

            await browser.StopAsync();

        }

        #endregion

    }

}
