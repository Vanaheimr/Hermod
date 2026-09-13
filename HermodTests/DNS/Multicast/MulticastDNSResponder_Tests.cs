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
    /// Tests of the Multicast DNS responder (RFC 6762 §6 to §10) on the in-memory network:
    /// probing, announcing, answering queries, known-answer suppression, additional records,
    /// the per-record rate limit, legacy unicast, negative responses, goodbye packets,
    /// updates, name conflicts and argument validation.
    /// </summary>
    [TestFixture]
    public class MulticastDNSResponder_Tests
    {

        #region Data

        private static readonly IPv4Address  ServerAddress  = IPv4Address.Parse("10.0.0.7");

        /// <summary>
        /// Another Multicast DNS host on the link, speaking from port 5353.
        /// </summary>
        private static readonly IPSocket     Querier        = PeerSocket("10.0.0.99");

        private sealed class FirstDelayGateTimeProvider : TimeProvider
        {

            private readonly TaskCompletionSource<Boolean> firstTimerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private Int32        firstTimerClaimed;
            private GatedTimer?  firstTimer;

            public Task FirstTimerCreated
                => firstTimerCreated.Task;

            public void ReleaseFirstTimer()
                => firstTimer?.Fire();

            public override ITimer CreateTimer(TimerCallback  Callback,
                                               Object?        State,
                                               TimeSpan       DueTime,
                                               TimeSpan       Period)
            {

                if (Interlocked.CompareExchange(ref firstTimerClaimed, 1, 0) == 0)
                {

                    var timer = new GatedTimer(Callback, State);

                    Volatile.Write(ref firstTimer, timer);
                    firstTimerCreated.TrySetResult(true);

                    return timer;

                }

                return base.CreateTimer(Callback, State, DueTime, Period);

            }


            private sealed class GatedTimer(TimerCallback Callback,
                                            Object?       State) : ITimer
            {

                private Int32 disposed;
                private Int32 fired;

                public Boolean Change(TimeSpan DueTime, TimeSpan Period)
                    => Volatile.Read(ref disposed) == 0;

                public void Fire()
                {

                    if (Volatile.Read(ref disposed)               == 0 &&
                        Interlocked.CompareExchange(ref fired, 1, 0) == 0)
                    {
                        Callback(State);
                    }

                }

                public void Dispose()
                    => Volatile.Write(ref disposed, 1);

                public ValueTask DisposeAsync()
                {

                    Dispose();
                    return ValueTask.CompletedTask;

                }

            }

        }

        #endregion


        #region Probing_SendsProbeCountQueries_WithQUBitAndAuthorities()

        [Test]
        public async Task Probing_SendsProbeCountQueries_WithQUBitAndAuthorities()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var publication  = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: true);
            var probes       = capture.Queries;

            Assert.Multiple(() => {
                Assert.That(publication.State,    Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(probes,               Has.Count.EqualTo(3), "ProbeCount probes");
                Assert.That(capture.Responses,    Has.Count.EqualTo(2), "AnnouncementCount announcements");
                Assert.That(capture.ParseErrors,  Is.Empty);
            });

            foreach (var probe in probes)
            {
                Assert.Multiple(() => {
                    Assert.That(probe.IsMulticast,                                                                     Is.True);
                    Assert.That(probe.Sender,                                                                          Is.SameAs(transport));
                    Assert.That(probe.Message.TransactionId,                                                           Is.EqualTo((UInt16) 0));
                    Assert.That(probe.Message.Questions.Select(question => question.Name.FullName),                    Is.EquivalentTo(new[] { "myhost.local.", "myhost._test._tcp.local." }), "one question per unique name");
                    Assert.That(probe.Message.Questions.All(question => question.Type == DNSResourceRecordTypes.Any),  Is.True, "probes ask for type ANY");
                    Assert.That(probe.Message.Questions.All(question => question.UnicastResponseRequested),           Is.True, "probes set the QU bit");
                    Assert.That(probe.Message.Answers,                                                                 Is.Empty);
                    Assert.That(probe.Message.Authorities.Select(authority => authority.Type),                         Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT }), "the unique records are proposed, the PTR is not");
                    Assert.That(probe.Message.Authorities.All(authority => !authority.CacheFlush),                     Is.True);
                });
            }

        }

        #endregion

        #region Probing_IgnoresResponsesBeforeTheFirstProbe()

        [Test]
        public async Task Probing_IgnoresResponsesBeforeTheFirstProbe()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);
            var clock    = new FirstDelayGateTimeProvider();
            var options  = new MulticastDNSResponderOptions {
                               ProbeInterval         = TimeSpan.FromMilliseconds(10),
                               MaxInitialProbeDelay  = TimeSpan.FromMilliseconds(250),
                               AnnouncementInterval  = TimeSpan.FromMilliseconds(10)
                           };

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, options, clock);

            await responder.StartAsync();

            var conflicts = 0;
            responder.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => {
                Interlocked.Increment(ref conflicts);
                return Task.CompletedTask;
            };

            var publishing = responder.PublishAsync(
                                 [ new A(MyHost,
                                         DNSQueryClasses.IN,
                                         TimeSpan.FromSeconds(120),
                                         ServerAddress) ],
                                 Probe: true
                             );

            await clock.FirstTimerCreated.WaitAsync(TimeSpan.FromSeconds(1));
            await WaitUntil(() => responder.Publications.Any(publication => publication.State == MulticastDNSPublicationState.Probing));

            Assert.That(capture.Queries, Is.Empty, "the initial probe delay is still gated");

            await transport.InjectAsync(
                      MulticastDNSMessage.Response(
                          [ new MulticastDNSRecord(
                                new AAAA(MyHost,
                                         DNSQueryClasses.IN,
                                         TimeSpan.FromSeconds(120),
                                         IPv6Address.Parse("fe80::99")),
                                CacheFlush: true
                            ) ]
                      ).Serialize(),
                      Querier
                  );

            Assert.Multiple(() => {
                Assert.That(responder.Publications.Single().State,  Is.EqualTo(MulticastDNSPublicationState.Probing));
                Assert.That(conflicts,                              Is.EqualTo(0));
            });

            clock.ReleaseFirstTimer();

            var publication = await publishing.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Multiple(() => {
                Assert.That(publication.State,        Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(publication.ConflictSource, Is.Null);
                Assert.That(conflicts,                Is.EqualTo(0));
                Assert.That(capture.Queries,          Has.Count.EqualTo(3));
            });

        }

        #endregion

        #region Probing_IsSkipped_ForAPTROnlyPublication()

        [Test]
        public async Task Probing_IsSkipped_ForAPTROnlyPublication()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var transitions = new List<(MulticastDNSPublicationState OldState, MulticastDNSPublicationState NewState)>();
            responder.OnPublicationStateChanged += (timestamp, sender, changed, oldState, newState, ct) => {
                lock (transitions)
                    transitions.Add((oldState, newState));
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync(
                                  [ new PTR(TestService, DNSQueryClasses.IN, TimeSpan.FromSeconds(4500), MyInstance) ],
                                  Probe: true
                              );

            Assert.Multiple(() => {
                Assert.That(publication.State,          Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(publication.UniqueRecords,  Is.Empty);
                Assert.That(capture.Queries,            Is.Empty, "a shared record is never probed");
                Assert.That(capture.Responses,          Has.Count.EqualTo(2), "but it is announced");
                Assert.That(capture.Responses.All(announcement => announcement.Message.Answers.Count == 1 &&
                                                                  announcement.Message.Answers[0].Type == DNSResourceRecordTypes.PTR &&
                                                                 !announcement.Message.Answers[0].CacheFlush),
                            Is.True);
                Assert.That(transitions,                Is.EqualTo(new[] {
                                                            (MulticastDNSPublicationState.Pending,    MulticastDNSPublicationState.Announcing),
                                                            (MulticastDNSPublicationState.Announcing, MulticastDNSPublicationState.Published)
                                                        }));
            });

        }

        #endregion

        #region Announcing_SendsAnnouncementCountResponses_WithCacheFlushOnUniqueRecords()

        [Test]
        public async Task Announcing_SendsAnnouncementCountResponses_WithCacheFlushOnUniqueRecords()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var sent = new List<(MulticastDNSMessage Response, IPSocket? Destination)>();
            responder.OnResponseSent += (timestamp, sender, response, destination, ct) => {
                lock (sent)
                    sent.Add((response, destination));
                return Task.CompletedTask;
            };

            var publication    = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);
            var announcements  = capture.Responses;

            Assert.Multiple(() => {
                Assert.That(publication.State,        Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(capture.Queries,          Is.Empty);
                Assert.That(announcements,            Has.Count.EqualTo(2));
                Assert.That(sent,                     Has.Count.EqualTo(2), "OnResponseSent fires per announcement");
                Assert.That(sent.All(s => s.Destination is null), Is.True);
                Assert.That(responder.ActiveRecords,  Has.Count.EqualTo(4));
            });

            foreach (var announcement in announcements)
            {
                Assert.Multiple(() => {
                    Assert.That(announcement.IsMulticast,                                                                                Is.True);
                    Assert.That(announcement.Message.AuthoritativeAnswer,                                                                Is.True);
                    Assert.That(announcement.Message.TransactionId,                                                                      Is.EqualTo((UInt16) 0));
                    Assert.That(announcement.Message.Questions,                                                                          Is.Empty);
                    Assert.That(announcement.Message.Answers.Select(answer => answer.Type),                                              Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.PTR }));
                    Assert.That(announcement.Message.Answers.Where(answer => answer.Type != DNSResourceRecordTypes.PTR).All(answer => answer.CacheFlush), Is.True, "unique records carry the cache-flush bit");
                    Assert.That(announcement.Message.Answers.Single(answer => answer.Type == DNSResourceRecordTypes.PTR).CacheFlush,    Is.False,      "shared records do not");
                    Assert.That(announcement.Message.Answers.All(answer => !answer.IsGoodbye),                                           Is.True);
                    Assert.That(announcement.Message.Additionals,                                                                        Is.Empty);
                });
            }

        }

        #endregion

        #region PublicationState_MovesFromPendingViaProbingAndAnnouncingToPublished()

        [Test]
        public async Task PublicationState_MovesFromPendingViaProbingAndAnnouncingToPublished()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var transitions = new List<(MulticastDNSPublicationState OldState, MulticastDNSPublicationState NewState)>();
            var reported    = new List<MulticastDNSPublication>();
            responder.OnPublicationStateChanged += (timestamp, sender, changed, oldState, newState, ct) => {
                lock (transitions)
                {
                    transitions.Add((oldState, newState));
                    reported.   Add(changed);
                }
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: true);

            Assert.Multiple(() => {
                Assert.That(publication.State,     Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(publication.IsActive,  Is.True);
                Assert.That(transitions,           Is.EqualTo(new[] {
                                                       (MulticastDNSPublicationState.Pending,    MulticastDNSPublicationState.Probing),
                                                       (MulticastDNSPublicationState.Probing,    MulticastDNSPublicationState.Announcing),
                                                       (MulticastDNSPublicationState.Announcing, MulticastDNSPublicationState.Published)
                                                   }));
                Assert.That(reported.All(p => ReferenceEquals(p, publication)), Is.True);
            });

        }

        #endregion

        #region PublicationState_SkipsProbing_WhenProbeIsFalse()

        [Test]
        public async Task PublicationState_SkipsProbing_WhenProbeIsFalse()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var transitions = new List<(MulticastDNSPublicationState OldState, MulticastDNSPublicationState NewState)>();
            responder.OnPublicationStateChanged += (timestamp, sender, changed, oldState, newState, ct) => {
                lock (transitions)
                    transitions.Add((oldState, newState));
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            Assert.Multiple(() => {
                Assert.That(publication.State,  Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(capture.Queries,    Is.Empty);
                Assert.That(transitions,        Is.EqualTo(new[] {
                                                    (MulticastDNSPublicationState.Pending,    MulticastDNSPublicationState.Announcing),
                                                    (MulticastDNSPublicationState.Announcing, MulticastDNSPublicationState.Published)
                                                }));
            });

        }

        #endregion


        #region MulticastQuery_IsAnsweredByMulticast_WithTheCacheFlushBit()

        [Test]
        public async Task MulticastQuery_IsAnsweredByMulticast_WithTheCacheFlushBit()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            // Without the rate limit, as the A record was multicast by the announcement a moment ago.
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(MinRecordMulticastInterval: TimeSpan.Zero));

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var received = new List<(MulticastDNSMessage Query, IPSocket Source)>();
            responder.OnQueryReceived += (timestamp, sender, query, datagram, ct) => {
                lock (received)
                    received.Add((query, datagram.RemoteSocket));
                return Task.CompletedTask;
            };

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A) ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(received,                                              Has.Count.EqualTo(1));
                Assert.That(received[0].Source,                                    Is.EqualTo(Querier));
                Assert.That(received[0].Query.Questions[0].Name.FullName,          Is.EqualTo("myhost.local."));
                Assert.That(response.IsMulticast,                                  Is.True);
                Assert.That(response.Sender,                                       Is.SameAs(transport));
                Assert.That(response.Message.AuthoritativeAnswer,                  Is.True);
                Assert.That(response.Message.TransactionId,                        Is.EqualTo((UInt16) 0));
                Assert.That(response.Message.Questions,                            Is.Empty, "a multicast response repeats no question");
                Assert.That(response.Message.Answers,                              Has.Count.EqualTo(1));
                Assert.That(response.Message.Answers[0].Type,                      Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(response.Message.Answers[0].CacheFlush,                Is.True);
                Assert.That(response.Message.Answers[0].TimeToLive,                Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(((A) response.Message.Answers[0].Record).IPv4Address,  Is.EqualTo(ServerAddress));
                Assert.That(response.Message.Additionals,                          Is.Empty);
                Assert.That(capture.Responses,                                     Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region QueryWithQUBitOnAllQuestions_IsAnsweredByUnicastToTheSource()

        [Test]
        public async Task QueryWithQUBitOnAllQuestions_IsAnsweredByUnicastToTheSource()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            // The default rate limit stays: a unicast response is not subject to it.
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var sent = new List<(MulticastDNSMessage Response, IPSocket? Destination)>();
            responder.OnResponseSent += (timestamp, sender, response, destination, ct) => {
                lock (sent)
                    sent.Add((response, destination));
                return Task.CompletedTask;
            };

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([
                          new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A,   UnicastResponseRequested: true),
                          new MulticastDNSQuestion(MyInstance, DNSResourceRecordTypes.TXT, UnicastResponseRequested: true)
                      ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.IsMulticast,                                              Is.False);
                Assert.That(response.Destination,                                              Is.EqualTo(Querier));
                Assert.That(response.Message.Questions,                                        Is.Empty);
                Assert.That(response.Message.TransactionId,                                    Is.EqualTo((UInt16) 0));
                Assert.That(response.Message.Answers.Select(answer => answer.Type),            Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.TXT }));
                Assert.That(response.Message.Answers.All(answer => answer.CacheFlush),         Is.True);
                Assert.That(response.Message.Answers.All(answer => answer.TimeToLive > TimeSpan.FromSeconds(10)), Is.True, "no legacy TTL cap for a QU query from port 5353");
                Assert.That(capture.Responses,                                                 Has.Count.EqualTo(1));
                Assert.That(sent,                                                              Has.Count.EqualTo(1));
                Assert.That(sent[0].Destination,                                               Is.EqualTo(Querier));
            });

        }

        #endregion

        #region QueryWithMixedQUAndQMQuestions_IsAnsweredByMulticast()

        [Test]
        public async Task QueryWithMixedQUAndQMQuestions_IsAnsweredByMulticast()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(MinRecordMulticastInterval: TimeSpan.Zero));

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([
                          new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A,   UnicastResponseRequested: true),
                          new MulticastDNSQuestion(MyInstance, DNSResourceRecordTypes.TXT, UnicastResponseRequested: false)
                      ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.IsMulticast,                                     Is.True, "one QM question makes the whole response a multicast");
                Assert.That(response.Message.Answers.Select(answer => answer.Type),   Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.TXT }));
                Assert.That(capture.Responses,                                        Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region QueryForAnUnknownName_IsNotAnswered()

        [Test]
        public async Task QueryForAnUnknownName_IsNotAnswered()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(MinRecordMulticastInterval: TimeSpan.Zero));

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            var received = 0;
            responder.OnQueryReceived += (timestamp, sender, query, datagram, ct) => {
                Interlocked.Increment(ref received);
                return Task.CompletedTask;
            };

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([
                          new MulticastDNSQuestion(DNSServiceName.Parse("other.local."),            DNSResourceRecordTypes.A),
                          new MulticastDNSQuestion(DNSServiceName.Parse("other._test._tcp.local."), DNSResourceRecordTypes.Any, UnicastResponseRequested: true)
                      ]).Serialize(),
                      Querier
                  );

            await Task.Delay(50);

            Assert.Multiple(() => {
                Assert.That(received,           Is.EqualTo(1), "the query was received");
                Assert.That(capture.Responses,  Is.Empty,      "but a name we do not own gets no answer, not even a negative one");
                Assert.That(capture.Count,      Is.EqualTo(0));
            });

        }

        #endregion

        #region QueryOfTypeANY_ReturnsAllRecordsOfTheName()

        [Test]
        public async Task QueryOfTypeANY_ReturnsAllRecordsOfTheName()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyInstance, DNSResourceRecordTypes.Any, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.Destination,                                              Is.EqualTo(Querier));
                Assert.That(response.Message.Answers.Select(answer => answer.Type),            Is.EquivalentTo(new[] { DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT }), "every record of the instance name");
                Assert.That(response.Message.Answers.All(answer => answer.Name.Equals(MyInstance)), Is.True);
                Assert.That(response.Message.Additionals.Select(additional => additional.Type), Is.EqualTo(new[] { DNSResourceRecordTypes.A }), "plus the address of the SRV target");
                Assert.That(capture.Responses,                                                 Has.Count.EqualTo(1));
            });

        }

        #endregion


        #region KnownAnswer_WithAtLeastHalfTheTTL_SuppressesTheAnswer()

        [Test]
        public async Task KnownAnswer_WithAtLeastHalfTheTTL_SuppressesTheAnswer()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            // The querier already holds the A record (TTL 120 s) with 60 s left: exactly half.
            var known = new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), ServerAddress);

            await transport.InjectAsync(
                      MulticastDNSMessage.Query(
                          [ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A, UnicastResponseRequested: true) ],
                          KnownAnswers: [ new MulticastDNSRecord(known, false, TimeSpan.FromSeconds(60)) ]
                      ).Serialize(),
                      Querier
                  );

            await Task.Delay(50);

            Assert.Multiple(() => {
                Assert.That(capture.Responses,  Is.Empty, "RFC 6762 §7.1: a known answer with at least half the TTL suppresses the record");
                Assert.That(capture.Count,      Is.EqualTo(0));
            });

        }

        #endregion

        #region KnownAnswer_WithLessThanHalfTheTTL_DoesNotSuppressTheAnswer()

        [Test]
        public async Task KnownAnswer_WithLessThanHalfTheTTL_DoesNotSuppressTheAnswer()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            var known = new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), ServerAddress);

            await transport.InjectAsync(
                      MulticastDNSMessage.Query(
                          [ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A, UnicastResponseRequested: true) ],
                          KnownAnswers: [ new MulticastDNSRecord(known, false, TimeSpan.FromSeconds(59)) ]
                      ).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.Destination,                                        Is.EqualTo(Querier));
                Assert.That(response.Message.Answers,                                    Has.Count.EqualTo(1));
                Assert.That(response.Message.Answers[0].Type,                            Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(((A) response.Message.Answers[0].Record).IPv4Address,        Is.EqualTo(ServerAddress));
                Assert.That(response.Message.Answers[0].TimeToLive,                      Is.EqualTo(TimeSpan.FromSeconds(120)), "the full TTL is sent, so that the querier refreshes its copy");
            });

        }

        #endregion


        #region PTRQuery_CarriesSRVTXTAndA_AsAdditionalRecords()

        [Test]
        public async Task PTRQuery_CarriesSRVTXTAndA_AsAdditionalRecords()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(TestService, DNSResourceRecordTypes.PTR, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            // A response with a shared record is delayed by a few milliseconds (RFC 6762 §6).
            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.Destination,                                                      Is.EqualTo(Querier));
                Assert.That(response.Message.Answers,                                                  Has.Count.EqualTo(1));
                Assert.That(response.Message.Answers[0].Type,                                          Is.EqualTo(DNSResourceRecordTypes.PTR));
                Assert.That(response.Message.Answers[0].CacheFlush,                                    Is.False);
                Assert.That(((PTR) response.Message.Answers[0].Record).Target.FullName,                Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(response.Message.Additionals.Select(additional => additional.Type),        Is.EquivalentTo(new[] { DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.A }), "RFC 6763 §12.1");
                Assert.That(response.Message.Additionals.All(additional => additional.CacheFlush),     Is.True);
                Assert.That(((SRV) response.AdditionalRecords.Single(record => record is SRV)).Port.ToUInt16(), Is.EqualTo((UInt16) 8443));
                Assert.That(((A)   response.AdditionalRecords.Single(record => record is A)).IPv4Address,      Is.EqualTo(ServerAddress));
            });

        }

        #endregion

        #region SRVQuery_CarriesA_AsAdditionalRecord()

        [Test]
        public async Task SRVQuery_CarriesA_AsAdditionalRecord()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyInstance, DNSResourceRecordTypes.SRV, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.Message.Answers.Select(answer => answer.Type),              Is.EqualTo(new[] { DNSResourceRecordTypes.SRV }));
                Assert.That(((SRV) response.Message.Answers[0].Record).Target.FullName,          Is.EqualTo("myhost.local."));
                Assert.That(response.Message.Additionals.Select(additional => additional.Type),  Is.EqualTo(new[] { DNSResourceRecordTypes.A }), "RFC 6763 §12.2: the address of the target, but not the TXT record");
                Assert.That(((A) response.Message.Additionals[0].Record).IPv4Address,            Is.EqualTo(ServerAddress));
            });

        }

        #endregion


        #region RateLimit_SuppressesASecondMulticastOfTheSameRecord_ButNotAUnicast()

        [Test]
        public async Task RateLimit_SuppressesASecondMulticastOfTheSameRecord_ButNotAUnicast()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(MinRecordMulticastInterval: TimeSpan.FromMilliseconds(100)));

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            // Let the announcements fall out of the rate-limit window.
            await Task.Delay(200);

            capture.Clear();

            var multicastQuery = MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A) ]).Serialize();
            var unicastQuery   = MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A, UnicastResponseRequested: true) ]).Serialize();

            await transport.InjectAsync(multicastQuery, Querier);
            await transport.InjectAsync(multicastQuery, PeerSocket("10.0.0.98"));

            await Task.Delay(30);

            var multicast = capture.Responses.SingleOrDefault();

            Assert.That(multicast, Is.Not.Null, "RFC 6762 §6: a record is multicast at most once per interval");
            Assert.Multiple(() => {
                Assert.That(multicast!.IsMulticast,                                     Is.True);
                Assert.That(((A) multicast.Message.Answers.Single().Record).IPv4Address, Is.EqualTo(ServerAddress));
            });

            await transport.InjectAsync(unicastQuery, Querier);

            var unicast = await WaitFor(() => capture.Responses.FirstOrDefault(response => !response.IsMulticast));

            Assert.Multiple(() => {
                Assert.That(unicast.Destination,                                     Is.EqualTo(Querier), "a unicast response is not rate limited");
                Assert.That(((A) unicast.Message.Answers.Single().Record).IPv4Address, Is.EqualTo(ServerAddress));
                Assert.That(capture.Responses,                                       Has.Count.EqualTo(2));
            });

        }

        #endregion

        #region LegacyUnicastQuery_IsAnsweredWithIdRepeatedQuestionAndShortTTL()

        [Test]
        public async Task LegacyUnicastQuery_IsAnsweredWithIdRepeatedQuestionAndShortTTL()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            var resolver = PeerSocket("10.0.0.99", IPPort.Parse(40000));

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A) ], TransactionId: 4711).Serialize(),
                      resolver
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.Multiple(() => {
                Assert.That(response.IsMulticast,                                      Is.False);
                Assert.That(response.Destination,                                      Is.EqualTo(resolver));
                Assert.That(response.Message.TransactionId,                            Is.EqualTo((UInt16) 4711), "RFC 6762 §6.7: the ID of the query");
                Assert.That(response.Message.AuthoritativeAnswer,                      Is.True);
                Assert.That(response.Message.Questions,                                Has.Count.EqualTo(1), "the question is repeated");
                Assert.That(response.Message.Questions[0].Name.FullName,               Is.EqualTo("myhost.local."));
                Assert.That(response.Message.Questions[0].Type,                        Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(response.Message.Answers,                                  Has.Count.EqualTo(1));
                Assert.That(((A) response.Message.Answers[0].Record).IPv4Address,      Is.EqualTo(ServerAddress));
                Assert.That(response.Message.Answers[0].CacheFlush,                    Is.False, "no Multicast DNS bits for a conventional resolver");
                Assert.That(response.Message.Answers[0].TimeToLive,                    Is.LessThanOrEqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(response.Message.Answers[0].TimeToLive,                    Is.GreaterThan(TimeSpan.Zero));
            });

        }

        #endregion


        #region AAAAQuery_ForAnIPv4OnlyName_IsAnsweredWithNSEC()

        [Test]
        public async Task AAAAQuery_ForAnIPv4OnlyName_IsAnsweredWithNSEC()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.AAAA, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            var response  = await WaitFor(() => capture.Responses.FirstOrDefault());
            var nsec      = response.Message.Answers.Select(answer => answer.Record).OfType<NSEC>().SingleOrDefault();

            Assert.That(nsec, Is.Not.Null, "RFC 6762 §6.1: a missing type at an owned name is answered with an NSEC record");
            Assert.Multiple(() => {
                Assert.That(response.Destination,                                                        Is.EqualTo(Querier));
                Assert.That(response.Message.Answers,                                                    Has.Count.EqualTo(1));
                Assert.That(nsec!.DomainName.FullName,                                                   Is.EqualTo("myhost.local."));
                Assert.That(nsec.NextDomainName.FullName,                                                Is.EqualTo("myhost.local."));
                Assert.That(MulticastDNSTypeBitmap.Contains(nsec.TypeBitMaps, DNSResourceRecordTypes.A),    Is.True,  "the type bitmap lists the A record we have");
                Assert.That(MulticastDNSTypeBitmap.Contains(nsec.TypeBitMaps, DNSResourceRecordTypes.AAAA), Is.False, "but not the AAAA record we lack");
                Assert.That(MulticastDNSTypeBitmap.Decode(nsec.TypeBitMaps),                             Is.EqualTo(new[] { DNSResourceRecordTypes.A }));
                Assert.That(nsec.TimeToLive,                                                             Is.EqualTo(TimeSpan.FromSeconds(120)), "the smallest TTL at the name");
            });

        }

        #endregion

        #region AAAAQuery_WithNegativeResponsesDisabled_IsNotAnswered()

        [Test]
        public async Task AAAAQuery_WithNegativeResponsesDisabled_IsNotAnswered()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(SendNegativeResponses: false));

            await responder.StartAsync();
            await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.AAAA, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            await Task.Delay(50);

            Assert.That(capture.Count, Is.EqualTo(0));

        }

        #endregion


        #region Withdraw_SendsGoodbyeForAllRecords_StopsAnswering_AndIsIdempotent()

        [Test]
        public async Task Withdraw_SendsGoodbyeForAllRecords_StopsAnswering_AndIsIdempotent()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var transitions = new List<(MulticastDNSPublicationState OldState, MulticastDNSPublicationState NewState)>();
            responder.OnPublicationStateChanged += (timestamp, sender, changed, oldState, newState, ct) => {
                lock (transitions)
                    transitions.Add((oldState, newState));
                return Task.CompletedTask;
            };

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443), Probe: false);

            capture.Clear();

            await publication.WithdrawAsync();

            var goodbye = capture.Responses.SingleOrDefault();

            Assert.That(goodbye, Is.Not.Null, "exactly one goodbye packet");
            Assert.Multiple(() => {
                Assert.That(goodbye!.IsMulticast,                                                     Is.True);
                Assert.That(goodbye.Message.Answers.Select(answer => answer.Type),                   Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.PTR }));
                Assert.That(goodbye.Message.Answers.All(answer => answer.IsGoodbye),                 Is.True, "RFC 6762 §10.1: TTL zero");
                Assert.That(goodbye.Message.Answers.All(answer => answer.TimeToLive == TimeSpan.Zero), Is.True);
                Assert.That(goodbye.Message.Answers.Where(answer => answer.Type != DNSResourceRecordTypes.PTR).
                                                    All  (answer => answer.CacheFlush),                Is.True,  "RFC 6762 §10.2: unique records retain cache-flush");
                Assert.That(goodbye.Message.Answers.Single(answer => answer.Type == DNSResourceRecordTypes.PTR).
                                                    CacheFlush,                                       Is.False, "shared records never set cache-flush");
                Assert.That(publication.State,                                                       Is.EqualTo(MulticastDNSPublicationState.Withdrawn));
                Assert.That(publication.IsActive,                                                    Is.False);
                Assert.That(responder.Publications,                                                  Is.Empty);
                Assert.That(responder.ActiveRecords,                                                 Is.Empty);
                Assert.That(transitions,                                                             Does.Contain((MulticastDNSPublicationState.Published, MulticastDNSPublicationState.Withdrawn)));
            });

            // Withdrawn records are not answered any more...
            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.A, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            await Task.Delay(50);

            Assert.That(capture.Count, Is.EqualTo(0), "a withdrawn record is not answered");

            // ...and withdrawing twice neither throws nor sends anything.
            Assert.DoesNotThrowAsync(() => publication.WithdrawAsync());

            Assert.Multiple(() => {
                Assert.That(capture.Count,       Is.EqualTo(0));
                Assert.That(publication.State,   Is.EqualTo(MulticastDNSPublicationState.Withdrawn));
            });

        }

        #endregion

        #region Stop_SendsGoodbyeForEveryRemainingPublication()

        [Test]
        public async Task Stop_SendsGoodbyeForEveryRemainingPublication()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var first   = await responder.PublishAsync(ServiceRecords("host1.local.", "10.0.0.7", 8443), Probe: false);
            var second  = await responder.PublishAsync(ServiceRecords("host2.local.", "10.0.0.7", 8444), Probe: false);

            Assert.That(responder.Publications, Has.Count.EqualTo(2));

            capture.Clear();

            await responder.StopAsync();

            var goodbyes = capture.Responses;

            Assert.Multiple(() => {
                Assert.That(goodbyes,                                                                           Has.Count.EqualTo(2), "one goodbye per publication");
                Assert.That(goodbyes.All(goodbye => goodbye.Message.Answers.Count == 4 &&
                                                    goodbye.Message.Answers.All(answer => answer.IsGoodbye)),   Is.True);
                Assert.That(goodbyes.Select(goodbye => ((PTR) goodbye.AnswerRecords.Single(record => record is PTR)).Target.FullName),
                            Is.EquivalentTo(new[] { "host1._test._tcp.local.", "host2._test._tcp.local." }));
                Assert.That(capture.Queries,                                                                    Is.Empty);
                Assert.That(first. State,                                                                       Is.EqualTo(MulticastDNSPublicationState.Withdrawn));
                Assert.That(second.State,                                                                       Is.EqualTo(MulticastDNSPublicationState.Withdrawn));
                Assert.That(responder.IsRunning,                                                                Is.False);
                Assert.That(responder.Publications,                                                             Is.Empty);
                Assert.That(responder.ActiveRecords,                                                            Is.Empty);
            });

        }

        #endregion


        #region Update_ReplacingTheTXTRecord_AnnouncesTheNewOneWithCacheFlush()

        [Test]
        public async Task Update_ReplacingTheTXTRecord_AnnouncesTheNewOneWithCacheFlush()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var publication = await responder.PublishAsync(ServiceRecords("myhost.local.", "10.0.0.7", 8443, TXTStrings: [ "txtver=1" ]), Probe: false);

            capture.Clear();

            var updated = ServiceRecords("myhost.local.", "10.0.0.7", 8443, TXTStrings: [ "txtver=2", "url=https://myhost.local.:8443/v2/" ]);

            await publication.UpdateAsync(updated);

            var announcements = capture.Responses;

            Assert.Multiple(() => {
                Assert.That(publication.State,                                                              Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(publication.Records.Select(record => record.RecordKey()),                       Is.EqualTo(updated.Select(record => record.RecordKey())));
                Assert.That(capture.Queries,                                                                Is.Empty, "an update is not probed");
                Assert.That(announcements,                                                                  Has.Count.EqualTo(2));
                Assert.That(announcements.All(announcement => announcement.Message.Answers.All(answer => !answer.IsGoodbye)), Is.True, "a replaced record needs no goodbye");
            });

            foreach (var announcement in announcements)
            {

                var txt = announcement.Message.Answers.Single(answer => answer.Type == DNSResourceRecordTypes.TXT);

                Assert.Multiple(() => {
                    Assert.That(announcement.Message.Answers,       Has.Count.EqualTo(4));
                    Assert.That(txt.CacheFlush,                     Is.True, "RFC 6762 §8.4: the cache-flush bit replaces the old TXT record in every cache");
                    Assert.That(((TXT) txt.Record).KeyValues["txtver"], Is.EqualTo("2"));
                });

            }

            // A query now gets the new TXT record.
            capture.Clear();

            await transport.InjectAsync(
                      MulticastDNSMessage.Query([ new MulticastDNSQuestion(MyInstance, DNSResourceRecordTypes.TXT, UnicastResponseRequested: true) ]).Serialize(),
                      Querier
                  );

            var response = await WaitFor(() => capture.Responses.FirstOrDefault());

            Assert.That(((TXT) response.Message.Answers.Single().Record).KeyValues["txtver"], Is.EqualTo("2"));

        }

        #endregion

        #region Update_RemovingAnRRSet_SendsGoodbyeForIt()

        [Test]
        public async Task Update_RemovingAnRRSet_SendsGoodbyeForIt()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var records      = ServiceRecords("myhost.local.", "10.0.0.7", 8443);
            var publication  = await responder.PublishAsync(records, Probe: false);

            capture.Clear();

            await publication.UpdateAsync(records.Where(record => record is not TXT));

            var responses = capture.Responses;

            Assert.That(responses, Has.Count.EqualTo(3), "a goodbye and two announcements");
            Assert.Multiple(() => {
                Assert.That(responses[0].Message.Answers,                                    Has.Count.EqualTo(1));
                Assert.That(responses[0].Message.Answers[0].Type,                            Is.EqualTo(DNSResourceRecordTypes.TXT));
                Assert.That(responses[0].Message.Answers[0].IsGoodbye,                       Is.True, "the vanished RRSet gets a goodbye");
                Assert.That(responses[0].Message.Answers[0].CacheFlush,                      Is.True, "RFC 6762 §10.2 applies to unique goodbyes too");
                Assert.That(responses[1].Message.Answers.Select(answer => answer.Type),      Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.PTR }));
                Assert.That(responses[2].Message.Answers.Select(answer => answer.Type),      Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.PTR }));
                Assert.That(responses.Skip(1).All(announcement => announcement.Message.Answers.All(answer => !answer.IsGoodbye)), Is.True);
                Assert.That(responder.ActiveRecords.Select(record => record.Type),           Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.PTR }));
            });

        }

        #endregion

        #region Update_RejectsEmptyRecords_AndAWithdrawnPublication()

        [Test]
        public async Task Update_RejectsEmptyRecords_AndAWithdrawnPublication()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            var records      = ServiceRecords("myhost.local.", "10.0.0.7", 8443);
            var publication  = await responder.PublishAsync(records, Probe: false);

            Assert.ThrowsAsync<ArgumentException>(() => publication.UpdateAsync([]));

            await publication.WithdrawAsync();

            Assert.ThrowsAsync<InvalidOperationException>(() => publication.UpdateAsync(records));

        }

        #endregion


        #region Conflict_ViaResponse_MarksTheSecondPublisher()

        [Test]
        public async Task Conflict_ViaResponse_MarksTheSecondPublisher()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var conflicts1   = new List<MulticastDNSPublication>();
            var conflicts2   = new List<(MulticastDNSPublication Publication, IDNSResourceRecord Own, IDNSResourceRecord Other, IPSocket Source)>();
            var transitions2 = new List<(MulticastDNSPublicationState OldState, MulticastDNSPublicationState NewState)>();

            responder1.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => {
                lock (conflicts1)
                    conflicts1.Add(conflicted);
                return Task.CompletedTask;
            };

            responder2.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => {
                lock (conflicts2)
                    conflicts2.Add((conflicted, own, other, source));
                return Task.CompletedTask;
            };

            responder2.OnPublicationStateChanged += (timestamp, sender, changed, oldState, newState, ct) => {
                lock (transitions2)
                    transitions2.Add((oldState, newState));
                return Task.CompletedTask;
            };

            var first   = await responder1.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ]);
            var second  = await responder2.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.8")) ]);

            Assert.Multiple(() => {

                Assert.That(first. State,                                     Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(responder1.ActiveRecords,                         Has.Count.EqualTo(1));
                Assert.That(conflicts1,                                       Is.Empty, "the established host keeps its name");

                Assert.That(second.State,                                     Is.EqualTo(MulticastDNSPublicationState.Conflict));
                Assert.That(second.IsActive,                                  Is.False);
                Assert.That(second.ConflictingRecord,                         Is.Not.Null);
                Assert.That(((A) second.ConflictingRecord!).IPv4Address,      Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
                Assert.That(second.OwnConflictedRecord,                       Is.Not.Null);
                Assert.That(((A) second.OwnConflictedRecord!).IPv4Address,    Is.EqualTo(IPv4Address.Parse("10.0.0.8")));
                Assert.That(second.ConflictSource,                            Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(responder2.ActiveRecords,                         Is.Empty, "conflicting records are no longer answered");

                Assert.That(conflicts2,                                       Has.Count.EqualTo(1));
                Assert.That(conflicts2[0].Publication,                        Is.SameAs(second));
                Assert.That(((A) conflicts2[0].Own).  IPv4Address,            Is.EqualTo(IPv4Address.Parse("10.0.0.8")));
                Assert.That(((A) conflicts2[0].Other).IPv4Address,            Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
                Assert.That(conflicts2[0].Source,                             Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(transitions2,                                     Does.Contain((MulticastDNSPublicationState.Probing, MulticastDNSPublicationState.Conflict)));

            });

        }

        #endregion

        #region Probing_ResponseWithDifferentTypeAtSameName_IsConflict()

        [Test]
        public async Task Probing_ResponseWithDifferentTypeAtSameName_IsConflict()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var conflicts = new List<(IDNSResourceRecord Own, IDNSResourceRecord Other, IPSocket Source)>();
            responder2.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => {
                lock (conflicts)
                    conflicts.Add((own, other, source));
                return Task.CompletedTask;
            };

            var first = await responder1.PublishAsync(
                            [ new AAAA(MyHost,
                                       DNSQueryClasses.IN,
                                       TimeSpan.FromSeconds(120),
                                       IPv6Address.Parse("fe80::7")) ],
                            Probe: false
                        );

            var second = await responder2.PublishAsync(
                             [ new A(MyHost,
                                     DNSQueryClasses.IN,
                                     TimeSpan.FromSeconds(120),
                                     IPv4Address.Parse("10.0.0.8")) ],
                             Probe: true
                         );

            Assert.Multiple(() => {
                Assert.That(first.State,                  Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.State,                 Is.EqualTo(MulticastDNSPublicationState.Conflict));
                Assert.That(second.IsActive,              Is.False);
                Assert.That(second.OwnConflictedRecord,   Is.TypeOf<A>());
                Assert.That(second.ConflictingRecord,     Is.TypeOf<AAAA>());
                Assert.That(second.ConflictSource,        Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(conflicts,                    Has.Count.EqualTo(1));
                Assert.That(conflicts[0].Own,             Is.TypeOf<A>());
                Assert.That(conflicts[0].Other,           Is.TypeOf<AAAA>());
                Assert.That(conflicts[0].Source,          Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(responder2.ActiveRecords,     Is.Empty);
            });

        }

        #endregion

        #region Probing_ResponseWithIdenticalRecordAtSameName_IsConflict()

        [Test]
        public async Task Probing_ResponseWithIdenticalRecordAtSameName_IsConflict()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var record = new A(MyHost,
                               DNSQueryClasses.IN,
                               TimeSpan.FromSeconds(120),
                               IPv4Address.Parse("10.0.0.7"));

            var first   = await responder1.PublishAsync([ record ], Probe: false);
            var second  = await responder2.PublishAsync([ record ], Probe: true);

            Assert.Multiple(() => {
                Assert.That(first.State,                   Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.State,                  Is.EqualTo(MulticastDNSPublicationState.Conflict));
                Assert.That(second.OwnConflictedRecord,    Is.TypeOf<A>());
                Assert.That(second.ConflictingRecord,      Is.TypeOf<A>());
                Assert.That(second.ConflictSource,         Is.EqualTo(PeerSocket("10.0.0.7")));
                Assert.That(responder2.ActiveRecords,      Is.Empty);
            });

        }

        #endregion

        #region Published_IdenticalRecordsOnTwoResponders_AreNoConflict()

        [Test]
        public async Task Published_IdenticalRecordsOnTwoResponders_AreNoConflict()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var conflicts = 0;
            responder1.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => { Interlocked.Increment(ref conflicts); return Task.CompletedTask; };
            responder2.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => { Interlocked.Increment(ref conflicts); return Task.CompletedTask; };

            // RFC 6762 §9: identical RDATA is not an ongoing conflict after probing.
            var first   = await responder1.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: false);
            var second  = await responder2.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: false);

            Assert.Multiple(() => {
                Assert.That(first. State,              Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.State,              Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.ConflictingRecord,  Is.Null);
                Assert.That(conflicts,                 Is.EqualTo(0));
                Assert.That(responder1.ActiveRecords,  Has.Count.EqualTo(1));
                Assert.That(responder2.ActiveRecords,  Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region Published_DifferentRecordTypesAtSameName_AreNoConflict()

        [Test]
        public async Task Published_DifferentRecordTypesAtSameName_AreNoConflict()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport1  = network.CreateTransport(IPv4Address.Parse("10.0.0.7"));
            await using var transport2  = network.CreateTransport(IPv4Address.Parse("10.0.0.8"));
            await using var responder1  = new MulticastDNSResponder(transport1, FastResponderOptions());
            await using var responder2  = new MulticastDNSResponder(transport2, FastResponderOptions());

            await responder1.StartAsync();
            await responder2.StartAsync();

            var conflicts = 0;
            responder1.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => { Interlocked.Increment(ref conflicts); return Task.CompletedTask; };
            responder2.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => { Interlocked.Increment(ref conflicts); return Task.CompletedTask; };

            var first = await responder1.PublishAsync(
                            [ new A(MyHost,
                                    DNSQueryClasses.IN,
                                    TimeSpan.FromSeconds(120),
                                    IPv4Address.Parse("10.0.0.7")) ],
                            Probe: false
                        );

            var second = await responder2.PublishAsync(
                             [ new AAAA(MyHost,
                                        DNSQueryClasses.IN,
                                        TimeSpan.FromSeconds(120),
                                        IPv6Address.Parse("fe80::8")) ],
                             Probe: false
                         );

            Assert.Multiple(() => {
                Assert.That(first.State,               Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(second.State,              Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(first.ConflictingRecord,   Is.Null);
                Assert.That(second.ConflictingRecord,  Is.Null);
                Assert.That(conflicts,                 Is.EqualTo(0));
                Assert.That(responder1.ActiveRecords,  Has.Count.EqualTo(1));
                Assert.That(responder2.ActiveRecords,  Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region ProbeTieBreaking_LexicographicallyGreaterProposal_WinsAgainstOurProbe()

        [Test]
        public async Task ProbeTieBreaking_LexicographicallyGreaterProposal_WinsAgainstOurProbe()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(ProbeInterval: TimeSpan.FromMilliseconds(200)));

            await responder.StartAsync();

            var conflicts = new List<(IDNSResourceRecord Own, IDNSResourceRecord Other, IPSocket Source)>();
            responder.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => {
                lock (conflicts)
                    conflicts.Add((own, other, source));
                return Task.CompletedTask;
            };

            // The first probe goes out at once; the responder then waits for the probe interval.
            var publishing = responder.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: true);

            await WaitUntil(() => responder.Publications.Any(p => p.State == MulticastDNSPublicationState.Probing));
            await WaitUntil(() => capture.Queries.Count == 1);

            // A simultaneous probe of another host for the same name with a greater address (RFC 6762 §8.2.1).
            var rival = PeerSocket("10.0.0.8");

            await transport.InjectAsync(
                      MulticastDNSMessage.Query(
                          [ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.Any, UnicastResponseRequested: true) ],
                          Authorities: [ new MulticastDNSRecord(new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.8"))) ]
                      ).Serialize(),
                      rival
                  );

            var publication = await publishing;

            Assert.Multiple(() => {
                Assert.That(publication.State,                                    Is.EqualTo(MulticastDNSPublicationState.Conflict));
                Assert.That(((A) publication.ConflictingRecord!).IPv4Address,     Is.EqualTo(IPv4Address.Parse("10.0.0.8")));
                Assert.That(((A) publication.OwnConflictedRecord!).IPv4Address,   Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
                Assert.That(publication.ConflictSource,                           Is.EqualTo(rival));
                Assert.That(conflicts,                                            Has.Count.EqualTo(1));
                Assert.That(conflicts[0].Source,                                  Is.EqualTo(rival));
                Assert.That(responder.ActiveRecords,                              Is.Empty);
                Assert.That(capture.Queries,                                      Has.Count.EqualTo(1), "probing stops at once");
                Assert.That(capture.Responses,                                    Is.Empty,             "and nothing is announced");
            });

        }

        #endregion

        #region ProbeTieBreaking_LexicographicallySmallerProposal_LosesAgainstOurProbe()

        [Test]
        public async Task ProbeTieBreaking_LexicographicallySmallerProposal_LosesAgainstOurProbe()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions(ProbeInterval: TimeSpan.FromMilliseconds(200)));

            await responder.StartAsync();

            var conflicts = 0;
            responder.OnNameConflict += (timestamp, sender, conflicted, own, other, source, ct) => { Interlocked.Increment(ref conflicts); return Task.CompletedTask; };

            var publishing = responder.PublishAsync([ new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7")) ], Probe: true);

            await WaitUntil(() => responder.Publications.Any(p => p.State == MulticastDNSPublicationState.Probing));
            await WaitUntil(() => capture.Queries.Count == 1);

            // The rival proposes a smaller address: it loses, we carry on.
            await transport.InjectAsync(
                      MulticastDNSMessage.Query(
                          [ new MulticastDNSQuestion(MyHostName, DNSResourceRecordTypes.Any, UnicastResponseRequested: true) ],
                          Authorities: [ new MulticastDNSRecord(new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.6"))) ]
                      ).Serialize(),
                      PeerSocket("10.0.0.6")
                  );

            var publication = await publishing;

            Assert.Multiple(() => {
                Assert.That(publication.State,               Is.EqualTo(MulticastDNSPublicationState.Published));
                Assert.That(publication.ConflictingRecord,   Is.Null);
                Assert.That(conflicts,                       Is.EqualTo(0));
                Assert.That(responder.ActiveRecords,         Has.Count.EqualTo(1));
                Assert.That(capture.Queries,                 Has.Count.EqualTo(3), "all probes were sent");
                Assert.That(capture.Responses,               Has.Count.EqualTo(2), "and the records announced");
            });

        }

        #endregion


        #region Publish_RejectsEmptyRecords_NonINRecords_AndAnUnstartedResponder()

        [Test]
        public async Task Publish_RejectsEmptyRecords_NonINRecords_AndAnUnstartedResponder()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            var record       = new A(MyHost, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), ServerAddress);
            var chaosRecord  = new A(MyHost, DNSQueryClasses.CH, TimeSpan.FromSeconds(120), ServerAddress);

            Assert.ThrowsAsync<InvalidOperationException>(() => responder.PublishAsync([ record ]), "not started");

            await responder.StartAsync();

            Assert.Multiple(() => {
                Assert.ThrowsAsync<ArgumentException>(() => responder.PublishAsync([]),              "no records");
                Assert.ThrowsAsync<ArgumentException>(() => responder.PublishAsync([ chaosRecord ]), "class CH");
                Assert.That(responder.Publications,  Is.Empty);
                Assert.That(capture.Count,           Is.EqualTo(0));
            });

        }

        #endregion

        #region Options_RejectAProbeOrAnnouncementCountOfZero()

        [Test]
        public async Task Options_RejectAProbeOrAnnouncementCountOfZero()
        {

            var network = new InMemoryMulticastDNSNetwork();

            await using var transport = network.CreateTransport(ServerAddress);

            Assert.Multiple(() => {
                Assert.Throws<ArgumentException>(() => new MulticastDNSResponder(transport, new MulticastDNSResponderOptions { ProbeCount        = 0 }));
                Assert.Throws<ArgumentException>(() => new MulticastDNSResponder(transport, new MulticastDNSResponderOptions { AnnouncementCount = 0 }));
                Assert.DoesNotThrow             (() => new MulticastDNSResponder(transport, new MulticastDNSResponderOptions { ProbeCount        = 1, AnnouncementCount = 1 }));
            });

        }

        #endregion

        #region Publish_WithACancelledToken_Throws_AndLeavesNoPublication()

        [Test]
        public async Task Publish_WithACancelledToken_Throws_AndLeavesNoPublication()
        {

            var network  = new InMemoryMulticastDNSNetwork();
            var capture  = new PacketCapture(network);

            await using var transport  = network.CreateTransport(ServerAddress);
            await using var responder  = new MulticastDNSResponder(transport, FastResponderOptions());

            await responder.StartAsync();

            Assert.CatchAsync<OperationCanceledException>(
                () => responder.PublishAsync(
                          ServiceRecords("myhost.local.", "10.0.0.7", 8443),
                          Probe:             true,
                          CancellationToken: new CancellationToken(canceled: true)
                      )
            );

            Assert.Multiple(() => {
                Assert.That(responder.Publications,   Is.Empty, "a cancelled publication is removed silently");
                Assert.That(responder.ActiveRecords,  Is.Empty);
                Assert.That(capture.Responses,        Is.Empty, "nothing was announced");
            });

        }

        #endregion

    }

}
