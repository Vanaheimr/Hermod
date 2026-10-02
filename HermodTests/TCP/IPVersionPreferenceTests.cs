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
    /// The first two tests start no server. A refused connection and an
    /// unreachable family are both failures - what they read is which address
    /// was chosen.
    ///
    /// The loopback tests listen on every address in question, on one port, as
    /// the default ATCPServer listens on [::1] and on 127.0.0.1 - so a wrong
    /// choice is not refused but taken by the wrong listener, and the test reads
    /// which listener took the connection.
    /// </remarks>
    [TestFixture]
    public class IPVersionPreferenceTests
    {

        #region IPv6Only_DoesNotFallBackToIPv4()

        /// <summary>
        /// The only address available is IPv4 and the caller asked for IPv6 only.
        /// That is a failure, and it must say so - connecting over IPv4 anyway is
        /// what "Only" exists to prevent.
        /// </summary>
        [Test]
        public async Task IPv6Only_DoesNotFallBackToIPv4()
        {

            using var port   = new ClosedPort();

            using var client = new TCPClient(
                                   URL.Parse($"tcp://127.0.0.1:{port}"),
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

            using var port   = new ClosedPort();

            using var client = new TCPClient(
                                   URL.Parse($"tcp://dns.example:{port}"),
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


        #region Localhost_ReachesTheLoopbackOfThePreferredFamily(Preference, Expected)

        /// <summary>
        /// IPvXAddress.Localhost - what the port-only ConnectNew connects to - is
        /// both loopback addresses, and the preference picks one of them, as it
        /// does for a host name with both an A and an AAAA record: [::1] without
        /// one and for IPv6, 127.0.0.1 for IPv4.
        /// </summary>
        /// <remarks>
        /// The choice tells the families apart by type, and IPvXAddress is
        /// neither an IPv4Address nor an IPv6Address. IPv4Only and IPv6Only found
        /// no address of their family and failed without dialling. PreferIPv4
        /// fell back to the IPvX address itself, which ToDotNet() turns into
        /// [::1], and never reached 127.0.0.1.
        /// </remarks>
        [TestCase(null,                            "::1")]
        [TestCase(IPVersionPreference.PreferIPv6,  "::1")]
        [TestCase(IPVersionPreference.IPv6Only,    "::1")]
        [TestCase(IPVersionPreference.PreferIPv4,  "127.0.0.1")]
        [TestCase(IPVersionPreference.IPv4Only,    "127.0.0.1")]
        public async Task Localhost_ReachesTheLoopbackOfThePreferredFamily(IPVersionPreference?  Preference,
                                                                            String                Expected)
        {

            using var listeners = Listeners.On(System.Net.IPAddress.IPv6Loopback,
                                               System.Net.IPAddress.Loopback);

            using var client    = await TCPClient.ConnectNew(
                                            IPPort.Parse(listeners.Port),
                                            PreferIPv4:      Preference,
                                            ConnectTimeout:  TimeSpan.FromSeconds(5)
                                        );

            await AssertReached(client, listeners, Expected);

        }

        #endregion

        #region LocalhostByName_IsIPv4UnlessOnlyIPv6WillDo(Preference, Expected)

        /// <summary>
        /// The host name "localhost" is 127.0.0.1, unless only IPv6 will do:
        /// then it is [::1].
        /// </summary>
        /// <remarks>
        /// IPv6Only used to fail here - "IPv6Only was asked for, and none of the
        /// resolved addresses is of that family!".
        ///
        /// PreferIPv6 does not get [::1] here, although its name asks for it: a
        /// client that names no preference has PreferIPv6, and its "localhost"
        /// has always reached the servers that listen on 127.0.0.1 or 0.0.0.0
        /// alone - among them S2ConformanceTests' reference servers, which a
        /// WWCP_S2 client dials as https://localhost.
        /// </remarks>
        [TestCase(null,                            "127.0.0.1")]
        [TestCase(IPVersionPreference.PreferIPv6,  "127.0.0.1")]
        [TestCase(IPVersionPreference.PreferIPv4,  "127.0.0.1")]
        [TestCase(IPVersionPreference.IPv4Only,    "127.0.0.1")]
        [TestCase(IPVersionPreference.IPv6Only,    "::1")]
        public async Task LocalhostByName_IsIPv4UnlessOnlyIPv6WillDo(IPVersionPreference?  Preference,
                                                                      String                Expected)
        {

            using var listeners = Listeners.On(System.Net.IPAddress.IPv6Loopback,
                                               System.Net.IPAddress.Loopback);

            using var client    = new TCPClient(
                                      URL.Parse($"tcp://localhost:{listeners.Port}"),
                                      PreferIPv4:      Preference,
                                      ConnectTimeout:  TimeSpan.FromSeconds(5)
                                  );

            var result          = await client.ConnectAsync();

            await AssertReached(client, listeners, Expected, result.Errors.Select(error => error.ToString()).AggregateWith(" | "));

        }

        #endregion

        #region AnotherLoopbackAddress_IsDialledAsItself()

        /// <summary>
        /// A host that is an IP address is dialled as that address, a loopback
        /// address other than 127.0.0.1 included.
        /// </summary>
        /// <remarks>
        /// Every host starting with "127." was taken for localhost and dialled
        /// as 127.0.0.1, so the listener there took the connection meant for
        /// 127.0.0.2.
        /// </remarks>
        [Test]
        public async Task AnotherLoopbackAddress_IsDialledAsItself()
        {

            using var listeners = Listeners.On(System.Net.IPAddress.Parse("127.0.0.2"),
                                               System.Net.IPAddress.Loopback);

            using var client    = new TCPClient(
                                      URL.Parse($"tcp://127.0.0.2:{listeners.Port}"),
                                      ConnectTimeout:  TimeSpan.FromSeconds(5)
                                  );

            var result          = await client.ConnectAsync();

            await AssertReached(client, listeners, "127.0.0.2", result.Errors.Select(error => error.ToString()).AggregateWith(" | "));

        }

        #endregion


        #region (private) AssertReached(Client, Listeners, Expected, Errors = null)

        /// <summary>
        /// The client dialled the expected address, and the listener there took
        /// the connection.
        /// </summary>
        private static async Task AssertReached(TCPClient  Client,
                                                Listeners  Listeners,
                                                String     Expected,
                                                String?    Errors   = null)
        {

            var tookIt = await Listeners.WhichTook(Client);

            Assert.Multiple(() => {

                // What reached a listener, not what the client says it did.
                Assert.That(tookIt,
                            Is.EqualTo(Expected),
                            $"The listener that took the connection. {Errors}");

                // As ToDotNet() has it, because that is what is dialled - and
                // IPvXAddress.Localhost writes itself "localhost".
                Assert.That(Client.ResolvedIPAddress?.ToDotNet().ToString(),
                            Is.EqualTo(Expected),
                            "The address the client chose.");

            });

        }

        #endregion

        #region (private class) Listeners

        /// <summary>
        /// A listener on each of the given addresses, all on one port, so that a
        /// test can tell which of them a client reached.
        /// </summary>
        private sealed class Listeners : IDisposable
        {

            private readonly TcpListener[] listeners;

            public UInt16 Port { get; }

            private Listeners(TcpListener[]  Listeners,
                              UInt16         Port)
            {
                this.listeners  = Listeners;
                this.Port       = Port;
            }

            /// <summary>
            /// The system gives the first address a port, and the others are
            /// bound to that port. Where it is taken on one of theirs, all start
            /// again on another - as ATCPServer does for [::1] and 127.0.0.1.
            /// </summary>
            public static Listeners On(params System.Net.IPAddress[] Addresses)
            {

                if (Addresses.Any(address => address.AddressFamily == AddressFamily.InterNetworkV6) &&
                    !Socket.OSSupportsIPv6)
                {
                    Assert.Ignore("IPv6 is not available on this host.");
                }

                for (var attempt = 1; ; attempt++)
                {

                    var started = new List<TcpListener>();

                    try
                    {

                        foreach (var address in Addresses)
                        {

                            var listener = new TcpListener(
                                               address,
                                               started.Count == 0
                                                   ? 0
                                                   : ((IPEndPoint) started[0].LocalEndpoint).Port
                                           );

                            started.Add(listener);

                            listener.Start();

                        }

                        return new Listeners(
                                   [.. started],
                                   (UInt16) ((IPEndPoint) started[0].LocalEndpoint).Port
                               );

                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse && attempt < 16)
                    {

                        foreach (var listener in started)
                            listener.Dispose();

                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressNotAvailable)
                    {

                        foreach (var listener in started)
                            listener.Dispose();

                        // 127.0.0.2 is a loopback address on Windows and on
                        // Linux, but not on every system.
                        Assert.Ignore($"This host cannot listen on {Addresses.Select(address => address.ToString()).AggregateWith(" and ")}: {e.Message}");

                    }

                }

            }

            /// <summary>
            /// The address of the listener that took the client's connection: the
            /// one with a connection waiting that comes from the client's port.
            /// </summary>
            public async Task<String> WhichTook(TCPClient Client)
            {

                var clientPort = Client.CurrentLocalEndPoint?.Port;

                if (!Client.IsConnected || clientPort is null)
                    return "none: the client is not connected";

                // The client is through once its side of the handshake is, and
                // the listener has the connection waiting a moment later at most.
                var waited = Stopwatch.StartNew();

                while (waited.Elapsed < TimeSpan.FromSeconds(5))
                {

                    foreach (var listener in listeners)
                    {
                        if (listener.Pending())
                        {

                            using var accepted = listener.AcceptSocket();

                            if ((accepted.RemoteEndPoint as IPEndPoint)?.Port == clientPort)
                                return ((IPEndPoint) listener.LocalEndpoint).Address.ToString();

                        }
                    }

                    await Task.Delay(10);

                }

                return "none: no listener took the connection";

            }

            public void Dispose()
            {
                foreach (var listener in listeners)
                    listener.Dispose();
            }

        }

        #endregion

    }

}
