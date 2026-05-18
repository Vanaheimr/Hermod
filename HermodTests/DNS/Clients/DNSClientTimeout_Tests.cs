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

using NUnit.Framework;

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
                                                BypassCache:  true
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
                                      BypassCache:        true,
                                      CancellationToken:  cts.Token
                                  ),
                Throws.InstanceOf<OperationCanceledException>()
            );

        }

        #endregion


    }

}
