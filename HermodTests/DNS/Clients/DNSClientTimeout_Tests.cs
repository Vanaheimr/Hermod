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
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// DNS timeout and cancellation regression tests.
    /// </summary>
    [TestFixture]
    public class DNSClientTimeout_Tests
    {

        #region (private static) CreateSilentUDPServer(out UDPPort)

        private static UdpClient CreateSilentUDPServer(out IPPort UDPPort)
        {

            var udpClient  = new UdpClient(
                                 new IPEndPoint(
                                     System.Net.IPAddress.Loopback,
                                     0
                                 )
                             );

            UDPPort        = IPPort.Parse(
                                 ((IPEndPoint) udpClient.Client.LocalEndPoint!).Port
                             );

            return udpClient;

        }

        #endregion

        #region Query_Uses_PerCall_Timeout()

        [Test]
        public async Task Query_Uses_PerCall_Timeout()
        {

            var timeout = TimeSpan.FromMilliseconds(75);

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var client        = new DNSClient(
                                          IPv4Address.Localhost,
                                          Port:           port,
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false
                                      );

            var stopwatch           = Stopwatch.StartNew();

            var response            = await client.Query<A>(
                                                DomainName.Parse("timeout.example"),
                                                Timeout:      timeout,
                                                ForceUpdate:  true
                                            );

            stopwatch.Stop();

            Assert.That(response.IsTimeout,   Is.True);
            Assert.That(response.Timeout,     Is.EqualTo(timeout));
            Assert.That(stopwatch.Elapsed,    Is.LessThan(TimeSpan.FromSeconds(1)));

        }

        #endregion

        #region Query_Honors_CancellationToken()

        [Test]
        public void Query_Honors_CancellationToken()
        {

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var client        = new DNSClient(
                                          IPv4Address.Localhost,
                                          Port:           port,
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false
                                      );

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            Assert.That(
                async () => await client.Query<A>(
                                      DomainName.Parse("canceled.example"),
                                      Timeout:            TimeSpan.FromSeconds(5),
                                      ForceUpdate:        true,
                                      CancellationToken:  cts.Token
                                  ),
                Throws.InstanceOf<OperationCanceledException>()
            );

        }

        #endregion

        #region Query_Uses_A_Servers_Own_Timeout()

        /// <summary>
        /// A server with a timeout of its own is given that, and not the
        /// client's: a DNSServerConfig says how long its server may take.
        /// </summary>
        /// <remarks>
        /// Measured on a WWCP node before this was so: two name servers that
        /// never answer, configured at three seconds each, kept a lookup
        /// waiting 10.0 seconds - the client's timeout. The one each server
        /// was configured with was read by nobody.
        /// </remarks>
        [Test]
        public async Task Query_Uses_A_Servers_Own_Timeout()
        {

            var own = TimeSpan.FromMilliseconds(150);

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var client        = new DNSClient(
                                          [ new DNSServerConfig(IPv4Address.Localhost, port, DNSTransport.UDP, QueryTimeout: own) ],
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false
                                      ) { MaxRetries = 0 };

            var stopwatch           = Stopwatch.StartNew();

            var response            = await client.Query<A>(
                                                DomainName.Parse("own-timeout.example"),
                                                ForceUpdate:  true
                                            );

            stopwatch.Stop();

            Assert.That(response.IsTimeout,   Is.True);
            Assert.That(response.Timeout,     Is.EqualTo(own));
            Assert.That(stopwatch.Elapsed,    Is.LessThan(TimeSpan.FromSeconds(1)));

        }

        #endregion

        #region Query_Asks_All_Servers_At_Once_Each_For_Its_Own_Timeout()

        /// <summary>
        /// Several servers are raced, not asked in turn: three that never
        /// answer, at 300 ms each, cost 300 ms - not 900, and not the five
        /// seconds of the client.
        /// </summary>
        [Test]
        public async Task Query_Asks_All_Servers_At_Once_Each_For_Its_Own_Timeout()
        {

            var own = TimeSpan.FromMilliseconds(300);

            using var first   = CreateSilentUDPServer(out var firstPort);
            using var second  = CreateSilentUDPServer(out var secondPort);
            using var third   = CreateSilentUDPServer(out var thirdPort);

            using var client  = new DNSClient(
                                    [
                                        new DNSServerConfig(IPv4Address.Localhost, firstPort,  DNSTransport.UDP, QueryTimeout: own),
                                        new DNSServerConfig(IPv4Address.Localhost, secondPort, DNSTransport.UDP, QueryTimeout: own),
                                        new DNSServerConfig(IPv4Address.Localhost, thirdPort,  DNSTransport.UDP, QueryTimeout: own)
                                    ],
                                    QueryTimeout:   TimeSpan.FromSeconds(5),
                                    UseQueryCache:  false
                                ) { MaxRetries = 0 };

            var stopwatch     = Stopwatch.StartNew();

            var response      = await client.Query<A>(
                                          DomainName.Parse("all-at-once.example"),
                                          ForceUpdate:  true
                                      );

            stopwatch.Stop();

            Assert.That(response.ResponseCode,  Is.Not.EqualTo(DNSResponseCodes.NoError));
            Assert.That(stopwatch.Elapsed,      Is.LessThan(own * 2.5));

        }

        #endregion

        #region Query_Uses_A_PerCall_Timeout_Over_A_Servers_Own()

        /// <summary>
        /// And a timeout given to one query is that query's, whatever the
        /// server was configured with: the caller asked for it.
        /// </summary>
        [Test]
        public async Task Query_Uses_A_PerCall_Timeout_Over_A_Servers_Own()
        {

            var timeout = TimeSpan.FromMilliseconds(100);

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var client        = new DNSClient(
                                          [ new DNSServerConfig(IPv4Address.Localhost, port, DNSTransport.UDP, QueryTimeout: TimeSpan.FromSeconds(5)) ],
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false
                                      ) { MaxRetries = 0 };

            var stopwatch           = Stopwatch.StartNew();

            var response            = await client.Query<A>(
                                                DomainName.Parse("per-call.example"),
                                                Timeout:      timeout,
                                                ForceUpdate:  true
                                            );

            stopwatch.Stop();

            Assert.That(response.Timeout,     Is.EqualTo(timeout));
            Assert.That(stopwatch.Elapsed,    Is.LessThan(TimeSpan.FromSeconds(1)));

        }

        #endregion

        #region Query_For_Several_Types_Keeps_A_Servers_Own_Timeout()

        /// <summary>
        /// A and AAAA are two queries, each of which races the servers the way
        /// a single one does - and so each gives a server its own timeout, and
        /// not the client's.
        /// </summary>
        [Test]
        public async Task Query_For_Several_Types_Keeps_A_Servers_Own_Timeout()
        {

            var own = TimeSpan.FromMilliseconds(150);

            using var silentServer  = CreateSilentUDPServer(out var port);
            using var client        = new DNSClient(
                                          [ new DNSServerConfig(IPv4Address.Localhost, port, DNSTransport.UDP, QueryTimeout: own) ],
                                          QueryTimeout:   TimeSpan.FromSeconds(5),
                                          UseQueryCache:  false
                                      ) { MaxRetries = 0 };

            var stopwatch           = Stopwatch.StartNew();

            var response            = await client.Query(
                                                DomainName.Parse("several-types.example"),
                                                [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ],
                                                ForceUpdate:  true
                                            );

            stopwatch.Stop();

            Assert.That(response.Answers,     Is.Empty);
            Assert.That(stopwatch.Elapsed,    Is.LessThan(TimeSpan.FromSeconds(1)));

        }

        #endregion

    }

}
