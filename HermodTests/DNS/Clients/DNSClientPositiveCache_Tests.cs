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
    /// What a cached positive answer looks like when it is handed out again.
    /// </summary>
    /// <remarks>
    /// The cache keeps one entry per name, and every RRset fetched for the name
    /// lands in it. A validator fetches a zone's DNSKEY and DS right after the
    /// address it validates; the next lookup of the address was answered with
    /// all three RRsets and their signatures. An answer is the RRset asked for,
    /// with its RRSIGs and the aliases leading to it, and nothing else.
    /// </remarks>
    [TestFixture]
    public class DNSClientPositiveCache_Tests
    {

        #region (class) ZoneApexServer

        /// <summary>
        /// A name server on the loopback interface for one signed zone apex that
        /// holds two A records, a DNSKEY and a DS RRset, and for an alias below it
        /// that points at the apex. Every RRset is answered with its RRSIG.
        /// </summary>
        private sealed class ZoneApexServer : IDisposable
        {

            private readonly UdpClient                socket;
            private readonly CancellationTokenSource  stop = new ();

            public  DomainName                        Apex     { get; } = DomainName.Parse("island.example.test");

            public  DomainName                        Alias    { get; } = DomainName.Parse("www.island.example.test");

            public  ConcurrentQueue<(String Name, DNSResourceRecordTypes Type)>  Queried  { get; } = new ();

            public  IPPort                            Port     { get; }

            private readonly DNSSECSigningKey         key;

            private readonly IDNSResourceRecord[]     addresses;
            private readonly IDNSResourceRecord[]     dnsKeys;
            private readonly IDNSResourceRecord[]     ds;
            private readonly IDNSResourceRecord[]     alias;


            public ZoneApexServer()
            {

                key        = DNSSECSigningKey.Generate(Apex, 13, KeySigningKey: true);

                addresses  = Signed(
                                 new A(Apex, DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.1")),
                                 new A(Apex, DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.2"))
                             );

                dnsKeys    = Signed(key.DNSKEY);
                ds         = Signed(key.DelegationSigner());

                alias      = Signed(new CNAME(Alias, DNSQueryClasses.IN, TimeSpan.FromHours(1), Apex));

                socket     = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
                Port       = IPPort.Parse((UInt16) ((IPEndPoint) socket.Client.LocalEndPoint!).Port);

                _ = Task.Run(ReceiveLoop);

            }

            private IDNSResourceRecord[] Signed(params IDNSResourceRecord[] RRset)

                => [
                       .. RRset,
                       DNSSECZoneSigner.SignRRSet(
                           RRset,
                           key,
                           DateTime.UtcNow.AddDays(-1),
                           DateTime.UtcNow.AddDays(14)
                       )
                   ];

            private IDNSResourceRecord[] Answer(String Name, DNSResourceRecordTypes Type)
            {

                if (String.Equals(Name, Alias.FullName, StringComparison.OrdinalIgnoreCase))
                    return Type == DNSResourceRecordTypes.A
                               ? [ .. alias, .. addresses ]
                               : alias;

                return Type switch {
                           DNSResourceRecordTypes.A       => addresses,
                           DNSResourceRecordTypes.DNSKEY  => dnsKeys,
                           DNSResourceRecordTypes.DS      => ds,
                           _                              => []
                       };

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
                        var name      = questions[0].DomainName.FullName;
                        var type      = questions[0].QueryType;

                        Queried.Enqueue((name, type));

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
                                            AnswerRRs:            Answer(name, type),
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
                key.Dispose();
            }

        }

        #endregion

        #region (private static) RRsetTypes(Response)

        /// <summary>
        /// The answer section as RRset types, an RRSIG as "RRSIG(type covered)".
        /// </summary>
        private static String[] RRsetTypes(DNSInfo Response)

            => [.. Response.Answers.Select(rr => rr is RRSIG signature
                                                     ? $"RRSIG({signature.TypeCovered})"
                                                     : rr.Type.ToString())];

        #endregion


        #region A_Cached_Answer_Holds_Only_The_RRset_Asked_For()

        /// <summary>
        /// The order a validation asks in: the address, then the zone's DNSKEY and
        /// DS. The next lookup of the address is a cache hit, and was handed out
        /// the entry for the name — all three RRsets and their signatures.
        /// </summary>
        [Test]
        public async Task A_Cached_Answer_Holds_Only_The_RRset_Asked_For()
        {

            using var       server = new ZoneApexServer();
            await using var client = new DNSClient(
                                         ManualDNSServers:  [ new DNSServerConfig(IPv4Address.Localhost, server.Port) ],
                                         QueryTimeout:      TimeSpan.FromSeconds(2),
                                         UseQueryCache:     true
                                     );

            var name = DNSServiceName.Parse(server.Apex.FullName);

            var first   = await client.Query(name, [ DNSResourceRecordTypes.A      ]);
            await client.Query(name, [ DNSResourceRecordTypes.DNSKEY ]);
            await client.Query(name, [ DNSResourceRecordTypes.DS     ]);
            var second  = await client.Query(name, [ DNSResourceRecordTypes.A      ]);
            var dnsKey  = await client.Query(name, [ DNSResourceRecordTypes.DNSKEY ]);

            Assert.Multiple(() => {

                Assert.That(RRsetTypes(first),  Is.EqualTo(new[] { "A", "A", "RRSIG(A)" }));
                Assert.That(RRsetTypes(second), Is.EqualTo(new[] { "A", "A", "RRSIG(A)" }));
                Assert.That(RRsetTypes(dnsKey), Is.EqualTo(new[] { "DNSKEY", "RRSIG(DNSKEY)" }));

                // The second lookups were served from the cache.
                Assert.That(server.Queried.Count(query => query.Type == DNSResourceRecordTypes.A),      Is.EqualTo(1));
                Assert.That(server.Queried.Count(query => query.Type == DNSResourceRecordTypes.DNSKEY), Is.EqualTo(1));

            });

        }

        #endregion

        #region A_Cached_Answer_Keeps_The_Alias_That_Leads_To_It()

        /// <summary>
        /// An answer through an alias is cached under both names, alias chain and
        /// all. From the cache, the alias still answers with the CNAME in front of
        /// the addresses, and the target answers with its addresses alone.
        /// </summary>
        [Test]
        public async Task A_Cached_Answer_Keeps_The_Alias_That_Leads_To_It()
        {

            using var       server = new ZoneApexServer();
            await using var client = new DNSClient(
                                         ManualDNSServers:  [ new DNSServerConfig(IPv4Address.Localhost, server.Port) ],
                                         QueryTimeout:      TimeSpan.FromSeconds(2),
                                         UseQueryCache:     true
                                     );

            var alias   = DNSServiceName.Parse(server.Alias.FullName);
            var apex    = DNSServiceName.Parse(server.Apex. FullName);

            var first   = await client.Query(alias, [ DNSResourceRecordTypes.A      ]);
            await client.Query(apex,  [ DNSResourceRecordTypes.DNSKEY ]);
            var second  = await client.Query(alias, [ DNSResourceRecordTypes.A      ]);
            var target  = await client.Query(apex,  [ DNSResourceRecordTypes.A      ]);

            Assert.Multiple(() => {

                Assert.That(RRsetTypes(first),  Is.EqualTo(new[] { "CNAME", "RRSIG(CNAME)", "A", "A", "RRSIG(A)" }));
                Assert.That(RRsetTypes(second), Is.EqualTo(new[] { "CNAME", "RRSIG(CNAME)", "A", "A", "RRSIG(A)" }));
                Assert.That(RRsetTypes(target), Is.EqualTo(new[] { "A", "A", "RRSIG(A)" }));

                // Both address lookups after the first were served from the cache.
                Assert.That(server.Queried.Count(query => query.Type == DNSResourceRecordTypes.A), Is.EqualTo(1));

            });

        }

        #endregion

    }

}
