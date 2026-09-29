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

using System.Net.Sockets;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A TCP server that is stopped and started again.
    ///
    /// TcpListener.Stop() closes the listener's socket, and TcpListener.Start()
    /// then makes a new one: bound to the endpoint the listener was made with,
    /// and with nothing of what had been set on the socket before. Started
    /// again like that, the listeners a server's constructor had made no longer
    /// listened where they had.
    ///
    /// The dual-stack default, IPvXAddress.Localhost - IPv4, IPv6 and localhost
    /// at once - listens with two sockets. On port 0 its constructor binds
    /// [::1]:0, reads the port P the operating system chose, and binds
    /// 127.0.0.1:P. Started again, the IPv6 listener bound [::1]:0 once more,
    /// which is a new port P', the IPv4 listener bound 127.0.0.1:P, the old
    /// one, and Start() read P' into TCPPort and IPSocket. The server answered
    /// on [::1]:P' and on 127.0.0.1:P, and a client of 127.0.0.1:TCPPort was
    /// refused.
    ///
    /// Every other server on a port of the system's choosing moved to a new
    /// port - one socket, so its TCPPort at least said where. And a server on
    /// IPvXAddress.Any, one socket on [::] in dual mode, came back without dual
    /// mode and refused IPv4, on a port of the system's choosing as on one it
    /// was given.
    ///
    /// Start() now makes the listeners again, the way the constructor made
    /// them, at the port the server names: where it was before, so that
    /// whoever was told that port finds it there again. A port of the system's
    /// choosing that somebody took while the server was stopped is chosen
    /// again - for both sockets together, as the constructor chooses it.
    /// </summary>
    [TestFixture]
    public class StopAndStartAgainTests
    {

        #region (record) Endpoint

        /// <summary>
        /// An address a listener binds, in dual mode or not.
        /// </summary>
        /// <param name="Name">What the tests call it.</param>
        /// <param name="Address">The address.</param>
        /// <param name="DualMode">Whether an IPv6 listener there takes IPv4 connections too.</param>
        public sealed record Endpoint(String                Name,
                                      System.Net.IPAddress  Address,
                                      Boolean               DualMode = false)
        {

            /// <summary>
            /// The loopback addresses a client reaches a listener here on.
            /// </summary>
            public IEnumerable<System.Net.IPAddress> ReachedOn
            {
                get
                {

                    if (Address.AddressFamily == AddressFamily.InterNetwork || DualMode)
                        yield return System.Net.IPAddress.Loopback;

                    if (Address.AddressFamily == AddressFamily.InterNetworkV6)
                        yield return System.Net.IPAddress.IPv6Loopback;

                }
            }

        }

        private static readonly Endpoint IPv4Loopback  = new ("127.0.0.1",         System.Net.IPAddress.Loopback);
        private static readonly Endpoint IPv6Loopback  = new ("[::1]",             System.Net.IPAddress.IPv6Loopback);
        private static readonly Endpoint IPv4Any       = new ("0.0.0.0",           System.Net.IPAddress.Any);
        private static readonly Endpoint IPv6Any       = new ("[::]",              System.Net.IPAddress.IPv6Any);
        private static readonly Endpoint DualModeAny   = new ("[::] in dual mode", System.Net.IPAddress.IPv6Any, DualMode: true);

        #endregion

        #region (record) Kind

        /// <summary>
        /// An address a server is given, and where it listens for it.
        /// </summary>
        /// <param name="Name">What the tests call it.</param>
        /// <param name="IPAddress">The address the server is given.</param>
        /// <param name="Endpoints">Where the server binds for it.</param>
        public sealed record Kind(String      Name,
                                  IIPAddress  IPAddress,
                                  Endpoint[]  Endpoints)
        {

            /// <summary>
            /// The loopback addresses a client reaches the server on.
            /// </summary>
            public IEnumerable<System.Net.IPAddress> ReachedOn
                => Endpoints.SelectMany(endpoint => endpoint.ReachedOn).Distinct();

            /// <summary>
            /// Whether the server binds [::], which ClosedPort has to be told.
            /// </summary>
            public Boolean OnIPv6Any
                => Endpoints.Any(endpoint => endpoint.Address.Equals(System.Net.IPAddress.IPv6Any));

            /// <summary>
            /// Whether the server binds an IPv6 address at all.
            /// </summary>
            public Boolean NeedsIPv6
                => Endpoints.Any(endpoint => endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6);

        }

        private static readonly Kind DualStackLocalhost  = new ("IPvXAddress.Localhost", IPvXAddress.Localhost, [ IPv6Loopback, IPv4Loopback ]);
        private static readonly Kind IPv4Localhost       = new ("IPv4Address.Localhost", IPv4Address.Localhost, [ IPv4Loopback ]);
        private static readonly Kind IPv6Localhost       = new ("IPv6Address.Localhost", IPv6Address.Localhost, [ IPv6Loopback ]);
        private static readonly Kind DualStackAny        = new ("IPvXAddress.Any",       IPvXAddress.Any,       [ DualModeAny ]);
        private static readonly Kind IPv4AnyAddress      = new ("IPv4Address.Any",       IPv4Address.Any,       [ IPv4Any ]);
        private static readonly Kind IPv6AnyAddress      = new ("IPv6Address.Any",       IPv6Address.Any,       [ IPv6Any ]);

        /// <summary>
        /// Every kind of address a server can be given.
        /// </summary>
        public static IEnumerable<TestCaseData> Kinds()
        {
            foreach (var kind in new[] { DualStackLocalhost, IPv4Localhost, IPv6Localhost, DualStackAny, IPv4AnyAddress, IPv6AnyAddress })
                yield return new TestCaseData(kind).SetArgDisplayNames(kind.Name);
        }

        /// <summary>
        /// A kind of address, and where somebody else takes the port while the
        /// server is stopped: each of the two sockets of the dual-stack default
        /// on its own - 127.0.0.1, whose bind fails after that of [::1] went
        /// through, and [::1], whose bind fails first - and the one socket of
        /// two others.
        /// </summary>
        public static IEnumerable<TestCaseData> PortsTaken()
        {
            yield return new TestCaseData(DualStackLocalhost, IPv4Loopback).SetArgDisplayNames(DualStackLocalhost.Name, IPv4Loopback.Name);
            yield return new TestCaseData(DualStackLocalhost, IPv6Loopback).SetArgDisplayNames(DualStackLocalhost.Name, IPv6Loopback.Name);
            yield return new TestCaseData(IPv4Localhost,      IPv4Loopback).SetArgDisplayNames(IPv4Localhost.Name,      IPv4Loopback.Name);
            yield return new TestCaseData(DualStackAny,       DualModeAny ).SetArgDisplayNames(DualStackAny.Name,       DualModeAny.Name);
        }

        #endregion


        #region ADualStackServerStartedAgainAnswersOnItsPortOverIPv4AndIPv6()

        /// <summary>
        /// The defect as it was found: the dual-stack default on a port of the
        /// system's choosing, started, stopped and started again, is on
        /// TCPPort - over IPv4 as over IPv6.
        /// </summary>
        [Test]
        public async Task ADualStackServerStartedAgainAnswersOnItsPortOverIPv4AndIPv6()
        {

            if (!Socket.OSSupportsIPv6)
                Assert.Ignore("IPv6 is not available on this host.");

            await using var server = new TCPEchoTestServer(
                                         TCPPort:  IPPort.Parse(0)
                                     );

            await server.Start();

            var before = server.TCPPort;

            await AssertAnswers(DualStackLocalhost, before, "before it was stopped");

            await server.Stop();
            await server.Start();

            var port      = server.TCPPort;
            var overIPv4  = await Answer(System.Net.IPAddress.Loopback,     port);
            var overIPv6  = await Answer(System.Net.IPAddress.IPv6Loopback, port);

            Assert.Multiple(() => {

                Assert.That(overIPv4, Is.EqualTo("ping"), $"127.0.0.1:{port}, TCPPort after the restart, did not answer - the server was on {before} before");
                Assert.That(overIPv6, Is.EqualTo("ping"), $"[::1]:{port}, TCPPort after the restart, did not answer - the server was on {before} before");

                Assert.That(server.IPSocket.Port, Is.EqualTo(port), "IPSocket and TCPPort disagree about the port");

            });

        }

        #endregion

        #region AServerStartedAgainOnAPortOfTheSystemsChoosingAnswersWhereItDidBefore(Kind)

        /// <summary>
        /// Every kind of address on a port of the system's choosing: started
        /// again, the server is on the port it was on before, and answers there
        /// on every address it answered on before.
        /// </summary>
        /// <remarks>
        /// The port is free while the server is stopped, and somebody else may
        /// be given it in that moment. The server is right to move then, and the
        /// test proves nothing either way - which it says rather than failing.
        /// </remarks>
        [Test]
        [TestCaseSource(nameof(Kinds))]
        public async Task AServerStartedAgainOnAPortOfTheSystemsChoosingAnswersWhereItDidBefore(Kind Kind)
        {

            if (Kind.NeedsIPv6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("IPv6 is not available on this host.");

            await using var server = new TCPEchoTestServer(
                                         IPAddress:  Kind.IPAddress,
                                         TCPPort:    IPPort.Parse(0)
                                     );

            await server.Start();

            var before = server.TCPPort;

            await AssertAnswers(Kind, before, "before it was stopped");

            await server.Stop();
            await server.Start();

            var after = server.TCPPort;

            if (after != before)
            {

                // Asked with the server stopped, because the server itself may
                // be what holds the port: the dual-stack default's IPv4
                // listener went back to it while TCPPort went elsewhere.
                await server.Stop();

                if (IsTaken(Kind, before))
                    Assert.Inconclusive($"Somebody else took port {before} while the server was stopped, and the server went to {after}: right, and nothing to learn from.");

                Assert.Fail($"The server started again on port {after} rather than {before}, which was free.");

            }

            await AssertAnswers(Kind, after, "started again");

            Assert.That(server.IPSocket.Port, Is.EqualTo(after), "IPSocket and TCPPort disagree about the port");

        }

        #endregion

        #region AServerStartedAgainOnAPortItWasGivenAnswersWhereItDidBefore(Kind)

        /// <summary>
        /// Every kind of address on a port the server was given: started again,
        /// it answers on that port on every address it answered on before.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(Kinds))]
        public async Task AServerStartedAgainOnAPortItWasGivenAnswersWhereItDidBefore(Kind Kind)
        {

            if (Kind.NeedsIPv6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("IPv6 is not available on this host.");

            // Held by the test whenever the server is not on it - see ClosedPort.
            using var port = new ClosedPort();

            await using var server = new TCPEchoTestServer(
                                         IPAddress:  Kind.IPAddress,
                                         TCPPort:    port.Number
                                     );

            port.HandOver(ForIPv6Any: Kind.OnIPv6Any);

            await server.Start();

            await AssertAnswers(Kind, port.Number, "before it was stopped");

            await server.Stop();

            port.TakeBack();
            port.HandOver(ForIPv6Any: Kind.OnIPv6Any);

            await server.Start();

            Assert.That(server.TCPPort, Is.EqualTo(port.Number), "the server started again on another port than the one it was given");

            await AssertAnswers(Kind, port.Number, "started again");

        }

        #endregion

        #region AServerStartedAgainAfterItsPortWasTakenAnswersOnAnotherOnEveryAddress(Kind, Taken)

        /// <summary>
        /// A port of the system's choosing that somebody else took while the
        /// server was stopped: started again, the server asks for another, and
        /// answers there on every address it answered on before - on both
        /// sockets of the dual-stack default, whichever of the two had lost the
        /// port.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(PortsTaken))]
        public async Task AServerStartedAgainAfterItsPortWasTakenAnswersOnAnotherOnEveryAddress(Kind      Kind,
                                                                                               Endpoint  Taken)
        {

            if (Kind.NeedsIPv6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("IPv6 is not available on this host.");

            await using var server = new TCPEchoTestServer(
                                         IPAddress:  Kind.IPAddress,
                                         TCPPort:    IPPort.Parse(0)
                                     );

            await server.Start();

            var before = server.TCPPort;

            await AssertAnswers(Kind, before, "before it was stopped");

            await server.Stop();

            // Null when somebody else took the port already - which is what
            // this test is about as well.
            using var squatter = Listen(Taken, before);

            Exception? failed = null;

            try
            {
                await server.Start();
            }
            catch (Exception e)
            {
                failed = e;
            }

            Assert.That(failed, Is.Null, $"with port {before} taken on {Taken.Name}, the server did not start again: {failed?.GetType().Name}: {failed?.Message}");

            var after = server.TCPPort;

            Assert.That(after, Is.Not.EqualTo(before), $"port {before} is taken on {Taken.Name}, and TCPPort still names it");

            await AssertAnswers(Kind, after, "started again");

            Assert.That(server.IPSocket.Port, Is.EqualTo(after), "IPSocket and TCPPort disagree about the port");

        }

        #endregion


        #region (private static) AssertAnswers(Kind, Port, When)

        /// <summary>
        /// Assert that the server answers on the port on every address a server
        /// of this kind is reached on.
        /// </summary>
        private static async Task AssertAnswers(Kind    Kind,
                                                IPPort  Port,
                                                String  When)
        {

            var answers = new List<(System.Net.IPAddress Address, String Answer)>();

            foreach (var address in Kind.ReachedOn)
                answers.Add((address, await Answer(address, Port)));

            Assert.Multiple(() => {
                foreach (var (address, answer) in answers)
                    Assert.That(answer, Is.EqualTo("ping"), $"{Kind.Name} on port {Port}, {When}: {(address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address)}:{Port} did not answer");
            });

        }

        #endregion

        #region (private static) Answer(Address, Port)

        /// <summary>
        /// What the echo server says to "ping" at the address and port: "ping"
        /// once more if it is there, and otherwise why not.
        /// </summary>
        private static async Task<String> Answer(System.Net.IPAddress  Address,
                                                 IPPort                Port)
        {

            using var client   = new TcpClient(Address.AddressFamily);
            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {

                await client.ConnectAsync(Address, Port.ToUInt16(), timeout.Token);

                var stream  = client.GetStream();

                await stream.WriteAsync(Encoding.ASCII.GetBytes("ping"), timeout.Token);

                var echo    = new Byte[4];
                var read    = 0;

                while (read < echo.Length)
                {

                    var justRead = await stream.ReadAsync(echo.AsMemory(read), timeout.Token);

                    if (justRead == 0)
                        break;

                    read += justRead;

                }

                return Encoding.ASCII.GetString(echo, 0, read);

            }
            catch (SocketException e)
            {
                return e.SocketErrorCode.ToString();
            }
            catch (IOException e)
            {
                return e.Message;
            }
            catch (OperationCanceledException)
            {
                return "nothing within ten seconds";
            }

        }

        #endregion

        #region (private static) Listen(Endpoint, Port)

        /// <summary>
        /// Somebody else's listener at the endpoint and port - or null, when
        /// somebody else has the port there already.
        /// </summary>
        private static TcpListener? Listen(Endpoint  Endpoint,
                                           IPPort    Port)
        {

            var listener = new TcpListener(Endpoint.Address, Port.ToUInt16());

            if (Endpoint.DualMode)
                listener.Server.DualMode = true;

            try
            {
                listener.Start();
                return listener;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                listener.Dispose();
                return null;
            }

        }

        #endregion

        #region (private static) IsTaken(Kind, Port)

        /// <summary>
        /// Whether somebody else has the port now on any of the endpoints a
        /// server of this kind binds.
        /// </summary>
        private static Boolean IsTaken(Kind    Kind,
                                       IPPort  Port)
        {

            var listeners = Kind.Endpoints.Select(endpoint => Listen(endpoint, Port)).ToArray();

            foreach (var listener in listeners)
                listener?.Dispose();

            return listeners.Any(listener => listener is null);

        }

        #endregion

    }

}
