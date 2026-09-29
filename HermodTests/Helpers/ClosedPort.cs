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
using System.Net.NetworkInformation;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// A TCP port nobody listens on for as long as a test holds it - rather than
    /// one nobody listened on a moment ago. A connection to it is refused, on
    /// 127.0.0.1 as on [::1], and no other socket can start listening there.
    /// </summary>
    /// <remarks>
    /// Tests used to find such a port by starting a listener on port 0, noting
    /// the port the operating system had chosen, and stopping the listener
    /// again. From then on the port was anybody's, and with other test runs on
    /// the same machine somebody took it: the client of a reconnect test found
    /// the WebSocket server of another run where nothing was meant to be, was
    /// answered 401, and stopped trying.
    ///
    /// Here the port is held by a socket that is bound to it and does not
    /// listen. A connection to it is refused as to any port nobody listens on -
    /// on Linux at once, on Windows after about two seconds, which it spends
    /// trying the refused connection again - and the operating system does not
    /// hand the port to anybody binding port 0. That no other socket can bind
    /// it explicitly and listen took three things, each found by trying every
    /// address and option such a socket might use, from another process, on
    /// Windows and on Debian:
    ///
    /// The socket is IPv6 in dual mode and bound to [::], which holds the port
    /// for every address of both families. Bound to 127.0.0.1 only, it left
    /// 0.0.0.0 and [::] to others on Windows, and a connection to 127.0.0.1
    /// went to a listener there; on Linux it left [::1] and [::] to them.
    ///
    /// On Windows it is bound with SO_EXCLUSIVEADDRUSE. Without, a listener on
    /// 127.0.0.1 alone could still be bound next to it, and was reached.
    ///
    /// Elsewhere SO_REUSEADDR is cleared again once it is bound. .NET sets it on
    /// every TCP socket it binds on Linux, so that a port with connections in
    /// TIME_WAIT can be bound again; set on both sockets, it lets the kernel
    /// bind a second one to the port and have it listen there.
    ///
    /// A test that starts a server of its own on the port hands the port over
    /// right before, and takes it back once that server has stopped. Handed
    /// over, the port is still not let go of: the socket that holds it is
    /// replaced at once by one bound without those options, which the server
    /// can bind next to - on 127.0.0.1 and [::1], as Hermod's servers do - but
    /// which still keeps the operating system from giving the port to anybody
    /// binding port 0. Without it, the port was free from the moment the server
    /// let go of it until the test took it back - a few milliseconds, in which
    /// a process binding port 0 fifteen hundred times a second was given it
    /// now and then: Windows hands ports out one after another, to every
    /// process alike, and a process that binds that often goes round all of
    /// them in seconds.
    ///
    /// Not every server binds next to that socket on Windows. One on [::] in
    /// dual mode - as HTTPServer binds on HermodIPAddress.Any - is refused the
    /// port while that socket is on it. The IPv4 half of such a bind is not
    /// what stands in the way, since a server on 0.0.0.0 is not refused, and so
    /// one on [::] without dual mode is taken to be refused as well. One on
    /// 127.0.0.1 or [::1] is not, and by what Windows documents, neither is one
    /// that binds 127.0.0.1 with SO_EXCLUSIVEADDRUSE, as the rendezvous manager
    /// does: a bind with that option to a specific address succeeds next to a
    /// socket that holds the wildcard address without it. A server on [::] is
    /// handed the port for [::], and the port is then let go of right before
    /// the server binds it, on every platform but Linux: it is anybody's for
    /// that moment alone, and for a process binding port 0 to be given it,
    /// Windows would have to come round to this very port within it. On Linux,
    /// where .NET sets SO_REUSEADDR on every socket it binds and a socket that
    /// does not listen keeps no other from listening, a server on [::] binds
    /// next to the socket like any other, and the port stays held.
    /// </remarks>
    public sealed class ClosedPort : IDisposable
    {

        #region Data

        /// <summary>
        /// The socket that holds the port closed, while the test holds it.
        /// </summary>
        private Socket?  holder;

        /// <summary>
        /// The socket that keeps the port bound, while it is handed over - for
        /// [::] on Linux alone.
        /// </summary>
        private Socket?  anchor;

        #endregion

        #region Properties

        /// <summary>
        /// The number of the port.
        /// </summary>
        public IPPort  Number    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A port the operating system chooses, held from the start.
        /// </summary>
        public ClosedPort()
        {

            holder  = Bind(0, Exclusively: true);

            Number  = IPPort.Parse((UInt16) ((IPEndPoint) holder.LocalEndPoint!).Port);

        }

        #endregion


        #region HandOver(ForIPv6Any = false)

        /// <summary>
        /// Hand the port over to a server of the test's own, which may bind it
        /// from now on - on 127.0.0.1, [::1] and 0.0.0.0, and, handed over for
        /// [::], on [::] as well.
        /// </summary>
        /// <remarks>
        /// So may anybody who asks for this very port by its number - but nobody
        /// is given it who asks for any free port, except, anywhere but on Linux,
        /// while a server it was handed over to for [::] has not bound it yet.
        /// </remarks>
        /// <param name="ForIPv6Any">Whether the server binds the port on [::], in dual mode or not, which Windows refuses next to what keeps the port held otherwise. Anywhere but on Linux, the port is let go of then, and so is best handed over right before that server binds it.</param>
        /// <exception cref="InvalidOperationException">The port could not be kept bound.</exception>
        public void HandOver(Boolean ForIPv6Any = false)
        {

            if (holder is null)
                return;

            holder.Dispose();
            holder = null;

            // Windows refuses a server on [::] the port for as long as the
            // anchor is on it. Linux does not, and elsewhere it is not known -
            // see the remarks on this class.
            if (ForIPv6Any && !OperatingSystem.IsLinux())
                return;

            // At once, with nothing to wait for in between.
            try
            {
                anchor = Bind(Number.ToUInt16(), Exclusively: false);
            }
            catch (SocketException e)
            {
                throw Lost("handed over", e);
            }

        }

        #endregion

        #region TakeBack()

        /// <summary>
        /// Hold the port closed again, once the test's own server on it has
        /// stopped - so that nothing answers on it while that server is away.
        /// </summary>
        /// <exception cref="InvalidOperationException">The port could not be held closed again.</exception>
        public void TakeBack()
        {

            if (holder is not null)
                return;

            anchor?.Dispose();
            anchor = null;

            try
            {
                holder = Bind(Number.ToUInt16(), Exclusively: true);
            }
            catch (SocketException e)
            {
                throw Lost("taken back", e);
            }

        }

        #endregion


        #region (private static) Bind(Port, Exclusively)

        /// <summary>
        /// A socket bound to the port on [::] in dual mode, and not listening.
        /// Exclusively, no other socket can be bound to the port next to it;
        /// otherwise the test's own server can, on 127.0.0.1 and [::1]. Port 0
        /// lets the operating system choose one.
        /// </summary>
        private static Socket Bind(UInt16   Port,
                                   Boolean  Exclusively)
        {

            // Without IPv6 there is no dual-mode socket either, neither this one
            // nor another that could be bound next to it on [::].
            var dualMode  = Socket.OSSupportsIPv6;

            var socket    = dualMode
                                ? new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { DualMode = true }
                                : new Socket(AddressFamily.InterNetwork,   SocketType.Stream, ProtocolType.Tcp);

            try
            {

                if (Exclusively && OperatingSystem.IsWindows())
                    socket.ExclusiveAddressUse = true;

                socket.Bind(new IPEndPoint(dualMode
                                               ? System.Net.IPAddress.IPv6Any
                                               : System.Net.IPAddress.Any,
                                           Port));

                // After Bind(), because Bind() is where .NET sets it.
                if (Exclusively && !OperatingSystem.IsWindows())
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                return socket;

            }
            catch
            {
                socket.Dispose();
                throw;
            }

        }

        #endregion

        #region (private) Lost(What, Exception)

        /// <summary>
        /// Why the port could not be kept, with what the operating system says is
        /// on it: listeners and connections, and their states.
        /// </summary>
        private InvalidOperationException Lost(String           What,
                                               SocketException  Exception)
        {

            var properties   = IPGlobalProperties.GetIPGlobalProperties();

            var listeners    = properties.GetActiveTcpListeners().
                                          Where (endPoint   => endPoint.Port == Number.ToInt32()).
                                          Select(endPoint   => $"listening on {endPoint}");

            var connections  = properties.GetActiveTcpConnections().
                                          Where (connection => connection.LocalEndPoint.Port == Number.ToInt32()).
                                          Select(connection => $"{connection.LocalEndPoint} -> {connection.RemoteEndPoint} {connection.State}");

            var onThePort    = listeners.Concat(connections).ToArray();

            return new InvalidOperationException(
                       $"Port {Number} could not be {What}: {Exception.SocketErrorCode}. On the port: {(onThePort.Length > 0 ? String.Join("; ", onThePort) : "nothing")}.",
                       Exception
                   );

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Let go of the port.
        /// </summary>
        public void Dispose()
        {

            holder?.Dispose();
            holder = null;

            anchor?.Dispose();
            anchor = null;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The number of the port, so that a ClosedPort goes into a URL as it is.
        /// </summary>
        public override String ToString()
            => Number.ToString();

        #endregion

    }

}
