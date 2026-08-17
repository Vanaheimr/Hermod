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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// Which of the resolved addresses ATCPClient connects to.
    /// </summary>
    /// <remarks>
    /// IPv4Only and IPv6Only had no arm in the switch that makes this choice and
    /// fell through to the default one, which picks from both families at random.
    /// The two members documented as "if no address of this family is available,
    /// the connection will fail" were therefore the two that ignored the family
    /// altogether.
    ///
    /// It cost a day of DNS-over-HTTPS failures on a host without an IPv6 route:
    /// the fixtures asked for IPv4Only, got a coin toss, and reported "Network is
    /// unreachable" whenever three tosses in a row came up AAAA.
    ///
    /// No server is started here. A refused connection and an unreachable family
    /// are both failures - what these tests read is which address was chosen.
    /// </remarks>
    [TestFixture]
    public class IPVersionPreferenceTests
    {

        #region (private static) ClosedPort()

        /// <summary>
        /// A TCP port nobody is listening on.
        /// </summary>
        private static UInt16 ClosedPort()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();

            return port;

        }

        #endregion

        #region IPv6Only_DoesNotFallBackToIPv4()

        /// <summary>
        /// The only address available is IPv4 and the caller asked for IPv6 only.
        /// That is a failure, and it must say so - connecting over IPv4 anyway is
        /// what "Only" exists to prevent.
        /// </summary>
        [Test]
        public async Task IPv6Only_DoesNotFallBackToIPv4()
        {

            using var client = new TCPClient(
                                   URL.Parse($"tcp://127.0.0.1:{ClosedPort()}"),
                                   PreferIPv4: IPVersionPreference.IPv6Only
                               );

            var result = await client.ConnectAsync();

            Assert.Multiple(() => {

                Assert.That(result.IsSuccess,  Is.False);

                // Not "connection refused": that would mean it dialled the IPv4
                // address the caller ruled out and merely found nobody home.
                Assert.That(result.Errors.Any(error => error.ToString().Contains("IPv6Only", StringComparison.Ordinal)),
                            Is.True,
                            result.Errors.Select(error => error.ToString()).AggregateWith(" | "));

                Assert.That(client.ResolvedIPAddress,  Is.Null);

            });

        }

        #endregion

        #region IPv4Only_IsNotACoinToss()

        /// <summary>
        /// Both families available, IPv4Only asked for. Every single attempt must
        /// choose the IPv4 address. Twelve rounds, because the behaviour this
        /// replaces got it right half the time.
        /// </summary>
        [Test]
        public async Task IPv4Only_IsNotACoinToss()
        {

            using var client = new TCPClient(
                                   URL.Parse($"tcp://dns.example:{ClosedPort()}"),
                                   PreferIPv4:      IPVersionPreference.IPv4Only,
                                   ConnectTimeout:  TimeSpan.FromSeconds(2)
                               );

            var chosen = new List<String>();

            for (var round = 1; round <= 12; round++)
            {

                // Seeded rather than resolved: ConnectAsync only looks a hostname
                // up when it has no addresses yet, so this keeps the test off the
                // network while exercising the real choice.
                client.ResolvedIPAddresses.Clear();
                client.ResolvedIPAddresses.Add(IPv4Address.Parse("127.0.0.1"));
                client.ResolvedIPAddresses.Add(IPv6Address.Parse("::1"));

                await client.ConnectAsync();

                chosen.Add(client.ResolvedIPAddress?.ToString() ?? "(none)");

            }

            Assert.That(chosen.All(address => address == "127.0.0.1"),
                        Is.True,
                        chosen.AggregateWith(", "));

        }

        #endregion

    }

}
