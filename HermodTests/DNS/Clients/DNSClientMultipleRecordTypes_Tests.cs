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

using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// What a DNS client puts on the wire when it is asked for more than one
    /// resource record type at once.
    /// </summary>
    /// <remarks>
    /// The answer has to be "one question per query", and it has to be checked
    /// against something that behaves like the internet rather than like this
    /// project.
    ///
    /// RFC 1035 §4.1.2 lets a query carry several questions, and no resolver in
    /// the world answers one that does: a responder has one response code and
    /// one authority section to give, and no way to say "yes to this question,
    /// no to that one". Such a query is dropped rather than refused, so the
    /// asker waits out its whole timeout and is handed nothing.
    ///
    /// Hermod's own DNS server is the exception - it answers every question in
    /// the packet - and that is exactly why this is tested against a socket
    /// that stays silent instead. A client that sent one packet with two
    /// questions would pass every test written against this project's server
    /// and fail against every name server on the internet, which is how this
    /// went unnoticed: asking for A and AAAA together, the most ordinary pair
    /// there is, timed out.
    /// </remarks>
    [TestFixture]
    public class DNSClientMultipleRecordTypes_Tests
    {

        #region (class) SilentToMultiQuestionServer

        /// <summary>
        /// A name server on the loopback interface that answers a query with
        /// one question and says nothing at all to a query with more, the way
        /// the rest of the world does.
        /// </summary>
        private sealed class SilentToMultiQuestionServer : IDisposable
        {

            private readonly UdpClient                  socket;
            private readonly CancellationTokenSource    stop = new ();

            /// <summary>
            /// How many questions each received query carried, in the order
            /// they arrived.
            /// </summary>
            public ConcurrentQueue<Int32>  QuestionsPerQuery   { get; } = new ();

            public IPPort                  Port                { get; }


            public SilentToMultiQuestionServer()
            {

                socket  = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
                Port    = IPPort.Parse((UInt16) ((IPEndPoint) socket.Client.LocalEndPoint!).Port);

                _ = Task.Run(ReceiveLoop);

            }

            private async Task ReceiveLoop()
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {

                        var received  = await socket.ReceiveAsync(stop.Token);

                        var request   = DNSPacket.Parse(
                                            IPSocket.Parse($"127.0.0.1:{Port}"),
                                            IPSocket.Parse($"{received.RemoteEndPoint.Address}:{received.RemoteEndPoint.Port}"),
                                            new MemoryStream(received.Buffer)
                                        );

                        var questions = request.Questions.ToArray();

                        QuestionsPerQuery.Enqueue(questions.Length);

                        // The whole point: more than one question and nothing
                        // comes back.
                        if (questions.Length != 1)
                            continue;

                        var question  = questions[0];

                        IDNSResourceRecord? answer = question.QueryType switch {

                            var type when type == DNSResourceRecordTypes.A
                                => new A   (DomainName.Parse(question.DomainName.ToString()), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Parse("127.0.0.42")),

                            var type when type == DNSResourceRecordTypes.AAAA
                                => new AAAA(DomainName.Parse(question.DomainName.ToString()), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv6Address.Parse("::42")),

                            _   => null

                        };

                        var response  = new DNSPacket(
                                            TransactionId:        request.TransactionId,
                                            QueryOrResponse:      DNSQueryResponse.Response,
                                            Opcode:               0x00,
                                            AuthoritativeAnswer:  true,
                                            Truncation:           false,
                                            RecursionDesired:     request.RecursionDesired,
                                            RecursionAvailable:   true,
                                            ResponseCode:         DNSResponseCodes.NoError,
                                            Questions:            questions,
                                            AnswerRRs:            answer is null ? [] : [ answer ],
                                            AuthorityRRs:         [],
                                            AdditionalRRs:        []
                                        );

                        var bytes     = response.ToByteArray();

                        await socket.SendAsync(bytes, bytes.Length, received.RemoteEndPoint);

                    }
                }
                catch (OperationCanceledException)
                { }
                catch (ObjectDisposedException)
                { }
            }

            public void Dispose()
            {
                try { stop.Cancel(); } catch { }
                try { socket.Dispose(); } catch { }
                stop.Dispose();
            }

        }

        #endregion

        #region (private) ClientFor(Server)

        private static DNSClient ClientFor(SilentToMultiQuestionServer Server)

            => new (
                   ManualDNSServers:  [
                                          new DNSServerConfig(
                                              IPv4Address.Localhost,
                                              Server.Port
                                          )
                                      ],
                   // Two seconds, so that a client which went back to putting
                   // both questions in one packet fails this test quickly
                   // instead of holding the suite up for ten seconds.
                   QueryTimeout:      TimeSpan.FromSeconds(2),
                   UseQueryCache:     false
               );

        #endregion


        #region TwoRecordTypes_AreAskedAsTwoQueries()

        [Test]
        public async Task TwoRecordTypes_AreAskedAsTwoQueries()
        {

            using var server  = new SilentToMultiQuestionServer();
            var       client  = ClientFor(server);

            var answer        = await client.Query(
                                          DNSServiceName.Parse("two.example.test."),
                                          [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]
                                      );

            Assert.Multiple(() => {

                Assert.That(answer.ResponseCode,                      Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(answer.IsTimeout,                         Is.False);

                // Both types came back, from two queries put together.
                Assert.That(answer.Answers.OfType<A>().   Count(),    Is.EqualTo(1));
                Assert.That(answer.Answers.OfType<AAAA>().Count(),    Is.EqualTo(1));

                // And every packet that went out carried exactly one question.
                Assert.That(server.QuestionsPerQuery,                 Has.Count.EqualTo(2));
                Assert.That(server.QuestionsPerQuery,                 Is.All.EqualTo(1));

            });

        }

        #endregion

        #region OneRecordType_IsStillOneQuery()

        [Test]
        public async Task OneRecordType_IsStillOneQuery()
        {

            using var server  = new SilentToMultiQuestionServer();
            var       client  = ClientFor(server);

            var answer        = await client.Query(
                                          DNSServiceName.Parse("one.example.test."),
                                          [ DNSResourceRecordTypes.A ]
                                      );

            Assert.Multiple(() => {
                Assert.That(answer.ResponseCode,                    Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(answer.Answers.OfType<A>().Count(),     Is.EqualTo(1));
                Assert.That(server.QuestionsPerQuery,               Has.Count.EqualTo(1));
                Assert.That(server.QuestionsPerQuery,               Is.All.EqualTo(1));
            });

        }

        #endregion

        #region TheSameTypeTwice_IsOneQuery()

        /// <summary>
        /// Asking the same server twice for the same type in the same breath
        /// cannot produce a second answer, so it does not become a second
        /// query.
        /// </summary>
        [Test]
        public async Task TheSameTypeTwice_IsOneQuery()
        {

            using var server  = new SilentToMultiQuestionServer();
            var       client  = ClientFor(server);

            var answer        = await client.Query(
                                          DNSServiceName.Parse("twice.example.test."),
                                          [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.A ]
                                      );

            Assert.Multiple(() => {
                Assert.That(answer.ResponseCode,         Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(server.QuestionsPerQuery,    Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region RepeatedRecords_AreNotShownTwice()

        /// <summary>
        /// Two queries for one name come back with the same shared records -
        /// a CNAME chain, in the world outside this test - and the caller is
        /// shown each of them once rather than once per query it did not ask
        /// to be split into.
        /// </summary>
        [Test]
        public async Task RepeatedRecords_AreNotShownTwice()
        {

            using var server  = new SilentToMultiQuestionServer();
            var       client  = ClientFor(server);

            // This server answers both A and AAAA with a record of its own, so
            // nothing is shared and nothing may be dropped either: the count is
            // the check in both directions.
            var answer        = await client.Query(
                                          DNSServiceName.Parse("shared.example.test."),
                                          [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]
                                      );

            var written       = answer.Answers.Select(record => record.ToZoneFileString()).ToArray();

            Assert.That(written, Is.Unique);
            Assert.That(written, Has.Length.EqualTo(2));

        }

        #endregion

    }

}
