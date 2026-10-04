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
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// What a cached NODATA answer looks like when it is handed out again.
    /// </summary>
    /// <remarks>
    /// A NODATA answer is its authority section: the SOA that bounds how long it
    /// may be cached, and in a signed zone the NSEC or NSEC3 records that prove
    /// the type absent. A validator reads the proof from there — the DS query of
    /// an unsigned delegation is the case that matters, because without the
    /// proof the delegation is a stripped DS, and Bogus.
    /// </remarks>
    [TestFixture]
    public class DNSClientNoDataCache_Tests
    {

        #region (class) ZoneApexServer

        /// <summary>
        /// A name server on the loopback interface for one zone apex that holds a
        /// DNSKEY RRset and no DS: DNSKEY is answered, DS is NODATA with an SOA
        /// and an NSEC in the authority section.
        /// </summary>
        private sealed class ZoneApexServer : IDisposable
        {

            private readonly UdpClient                socket;
            private readonly CancellationTokenSource  stop = new ();

            public  DomainName                        Apex     { get; } = DomainName.Parse("island.example.test");

            public  ConcurrentQueue<DNSResourceRecordTypes>  Queried  { get; } = new ();

            public  IPPort                            Port     { get; }

            private readonly DNSSECSigningKey         key;


            public ZoneApexServer()
            {

                key     = DNSSECSigningKey.Generate(Apex, 13, KeySigningKey: true);
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
                        var type      = questions[0].QueryType;

                        Queried.Enqueue(type);

                        IDNSResourceRecord[] answers     = type == DNSResourceRecordTypes.DNSKEY
                                                               ? [ key.DNSKEY ]
                                                               : [];

                        IDNSResourceRecord[] authorities = type == DNSResourceRecordTypes.DNSKEY
                                                               ? []
                                                               : [
                                                                     new SOA (Apex, DNSQueryClasses.IN, TimeSpan.FromHours(1),
                                                                              DomainName.Parse("ns.example.test"), SimpleEMailAddress.Parse("hostmaster@example.test"),
                                                                              1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromDays(7), TimeSpan.FromHours(1)),
                                                                     new NSEC(Apex, DNSQueryClasses.IN, TimeSpan.FromHours(1),
                                                                              DomainName.Parse("zz.island.example.test"), ADNSResourceRecord.EncodeTypeBitMaps([ "NS", "RRSIG", "NSEC" ]))
                                                                 ];

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
                                            AnswerRRs:            answers,
                                            AuthorityRRs:         authorities,
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
                key.Dispose();
            }

        }

        #endregion


        #region A_Cached_NODATA_Keeps_Its_Proof_After_Another_Type_Was_Cached()

        /// <summary>
        /// The order a chain walk asks in: the zone's DNSKEY, then its DS, and the
        /// same again on the next validation. The second DNSKEY answer is cached
        /// under the same name as the NODATA; the second DS lookup is a NODATA hit,
        /// and handed out the entry for the name — the DNSKEY answer, without the
        /// NSEC.
        /// </summary>
        [Test]
        public async Task A_Cached_NODATA_Keeps_Its_Proof_After_Another_Type_Was_Cached()
        {

            using var       server = new ZoneApexServer();
            await using var client = new DNSClient(
                                         ManualDNSServers:  [ new DNSServerConfig(IPv4Address.Localhost, server.Port) ],
                                         QueryTimeout:      TimeSpan.FromSeconds(2),
                                         UseQueryCache:     true
                                     );

            var name = DNSServiceName.Parse(server.Apex.FullName);

            await client.Query(name, [ DNSResourceRecordTypes.DNSKEY ]);
            var first   = await client.Query(name, [ DNSResourceRecordTypes.DS ]);
            await client.Query(name, [ DNSResourceRecordTypes.DNSKEY ]);
            var second  = await client.Query(name, [ DNSResourceRecordTypes.DS ]);

            Assert.Multiple(() => {

                Assert.That(first. Authorities.OfType<NSEC>().Count(),  Is.EqualTo(1));
                Assert.That(second.Authorities.OfType<NSEC>().Count(),  Is.EqualTo(1));
                Assert.That(second.Answers.    OfType<DNSKEY>(),        Is.Empty);

                // The second DS lookup was served from the cache.
                Assert.That(server.Queried.Count(type => type == DNSResourceRecordTypes.DS), Is.EqualTo(1));

            });

        }

        #endregion

    }

}
