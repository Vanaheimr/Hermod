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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// What a test that uses a ClosedPort relies on: a connection to it is
    /// refused, nobody else can listen on it, and handed over it is a port like
    /// any other - on each platform the suite runs on.
    /// </summary>
    /// <remarks>
    /// None of it is what the obvious way of holding a port gives: a socket
    /// bound to 127.0.0.1 left the port to a listener on 0.0.0.0 on Windows,
    /// and to any listener at all on Linux. These tests are here so that a
    /// platform or a .NET release that binds differently says so, rather than
    /// bringing back the flakes ClosedPort was made against.
    /// </remarks>
    [TestFixture]
    public class ClosedPortTests
    {

        #region AConnectionToItIsRefused()

        /// <summary>
        /// On 127.0.0.1, and on [::1] where there is one.
        /// </summary>
        [Test]
        public async Task AConnectionToItIsRefused()
        {

            using var port = new ClosedPort();

            Assert.That(await Connect(System.Net.IPAddress.Loopback, port), Is.EqualTo(SocketError.ConnectionRefused), "127.0.0.1");

            if (Socket.OSSupportsIPv6)
                Assert.That(await Connect(System.Net.IPAddress.IPv6Loopback, port), Is.EqualTo(SocketError.ConnectionRefused), "[::1]");

        }

        #endregion

        #region NobodyElseCanListenOnIt(Where, ReuseAddress)

        /// <summary>
        /// Another socket asking for the port is told no - wherever it binds, and
        /// whether it asks to share the address or not.
        /// </summary>
        [Test]
        public void NobodyElseCanListenOnIt([Values("127.0.0.1", "0.0.0.0", "[::] in dual mode", "[::]", "[::1]")]  String   Where,
                                            [Values]                                                                  Boolean  ReuseAddress)
        {

            var (family, address, dualMode) = Where switch {
                                                  "127.0.0.1"          => (AddressFamily.InterNetwork,   System.Net.IPAddress.Loopback,     false),
                                                  "0.0.0.0"            => (AddressFamily.InterNetwork,   System.Net.IPAddress.Any,          false),
                                                  "[::] in dual mode"  => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Any,      true),
                                                  "[::]"               => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Any,      false),
                                                  _                    => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Loopback, false)
                                              };

            if (family == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("There is no IPv6 here to bind to.");

            using var port   = new ClosedPort();
            using var other  = new Socket(family, SocketType.Stream, ProtocolType.Tcp);

            if (family == AddressFamily.InterNetworkV6)
                other.DualMode = dualMode;

            if (ReuseAddress)
                other.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            Assert.That(() => {
                            other.Bind(new IPEndPoint(address, port.Number.ToInt32()));
                            other.Listen();
                        },
                        Throws.InstanceOf<SocketException>(),
                        $"Another socket was bound to {Where}:{port} and listened there.");

        }

        #endregion

        #region HandedOverItTakesAServerAndTakenBackItIsClosedAgain()

        /// <summary>
        /// Handed over, the port takes a server, and a client reaches it. Taken
        /// back once that server has stopped - while the connection the server
        /// closed still lingers on the port - a connection to it is refused
        /// again.
        /// </summary>
        [Test]
        public async Task HandedOverItTakesAServerAndTakenBackItIsClosedAgain()
        {

            using var port    = new ClosedPort();

            port.HandOver();

            await using var server        = new WebSocketMirrorServer(HTTPPort: port.Number, RequireAuthentication: false, AutoStart: true);

            using var client  = new TcpClient();

            try
            {

                await client.ConnectAsync(System.Net.IPAddress.Loopback, port.Number.ToInt32());

                // Until the server has accepted it, Stop() would not close it.
                var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

                while (DateTimeOffset.UtcNow < giveUp && server.NumberOfConnectedClients == 0)
                    await Task.Delay(20);

                Assert.That(server.NumberOfConnectedClients, Is.EqualTo(1), "The server did not accept the connection, so this test tested nothing.");

            }
            finally
            {
                await server.Stop();
            }

            port.TakeBack();

            Assert.That(await Connect(System.Net.IPAddress.Loopback, port), Is.EqualTo(SocketError.ConnectionRefused));

        }

        #endregion

        #region HandedOverItTakesAServerOn(Where)

        /// <summary>
        /// Handed over, the port takes a server on each address a test starts
        /// one on - on 127.0.0.1 with SO_EXCLUSIVEADDRUSE, as the rendezvous
        /// manager binds on Windows, too - and a client reaches it. Taken back
        /// once that server has stopped, a connection to it is refused again.
        /// A server on [::] is handed the port for [::]; on Linux the port stays
        /// held until the server binds it, whoever it is handed over to.
        /// </summary>
        [Test]
        public async Task HandedOverItTakesAServerOn([Values("127.0.0.1", "127.0.0.1 with SO_EXCLUSIVEADDRUSE", "[::1]", "0.0.0.0", "[::] in dual mode", "[::]")] String Where)
        {

            var (family, address, dualMode, loopback) = Where switch {
                                                            "[::1]"              => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Loopback, false, System.Net.IPAddress.IPv6Loopback),
                                                            "0.0.0.0"            => (AddressFamily.InterNetwork,   System.Net.IPAddress.Any,          false, System.Net.IPAddress.Loopback),
                                                            "[::] in dual mode"  => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Any,      true,  System.Net.IPAddress.Loopback),
                                                            "[::]"               => (AddressFamily.InterNetworkV6, System.Net.IPAddress.IPv6Any,      false, System.Net.IPAddress.IPv6Loopback),
                                                            _                    => (AddressFamily.InterNetwork,   System.Net.IPAddress.Loopback,     false, System.Net.IPAddress.Loopback)
                                                        };

            if (family == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("There is no IPv6 here to bind to.");

            using var port = new ClosedPort();

            port.HandOver(ForIPv6Any: address.Equals(System.Net.IPAddress.IPv6Any));

            if (OperatingSystem.IsLinux())
                Assert.That(() => ListenWithoutSharing(port), Throws.InstanceOf<SocketException>(),
                            "The port was let go of on Linux, where every server can be bound next to what holds it.");

            using (var server = new Socket(family, SocketType.Stream, ProtocolType.Tcp))
            {

                if (family == AddressFamily.InterNetworkV6)
                    server.DualMode = dualMode;

                // As the rendezvous manager asks for it, on Windows alone: .NET
                // has the option nowhere else.
                if (Where.EndsWith("SO_EXCLUSIVEADDRUSE") && OperatingSystem.IsWindows())
                    server.ExclusiveAddressUse = true;

                server.Bind(new IPEndPoint(address, port.Number.ToInt32()));
                server.Listen();

                Assert.That(await Connect(loopback, port), Is.EqualTo(SocketError.Success), $"The server on {Where} was not reached.");

            }

            port.TakeBack();

            Assert.That(await Connect(loopback, port), Is.EqualTo(SocketError.ConnectionRefused));

        }

        #endregion


        #region (private static) Connect(Address, Port)

        /// <summary>
        /// How a connection to the port ends: Success where it was accepted, the
        /// socket error where it was not.
        /// </summary>
        private static async Task<SocketError> Connect(System.Net.IPAddress  Address,
                                                       ClosedPort            Port)
        {

            using var client = new TcpClient(Address.AddressFamily);

            try
            {
                await client.ConnectAsync(Address, Port.Number.ToInt32()).WaitAsync(TimeSpan.FromSeconds(10));
                return SocketError.Success;
            }
            catch (SocketException e)
            {
                return e.SocketErrorCode;
            }

        }

        #endregion

        #region (private static) ListenWithoutSharing(Port)

        /// <summary>
        /// Listen on the port with a socket that does not share it: bound like
        /// any other, and SO_REUSEADDR cleared before it listens - which on Linux
        /// it cannot while another socket is bound to the port.
        /// </summary>
        private static void ListenWithoutSharing(ClosedPort Port)
        {

            using var socket = Socket.OSSupportsIPv6
                                   ? new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { DualMode = true }
                                   : new Socket(AddressFamily.InterNetwork,   SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(Socket.OSSupportsIPv6
                                           ? System.Net.IPAddress.IPv6Any
                                           : System.Net.IPAddress.Any,
                                       Port.Number.ToInt32()));

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            socket.Listen();

        }

        #endregion

    }

}
