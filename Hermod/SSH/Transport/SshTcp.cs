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
using System.IO.Pipelines;

using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Adapts TCP sockets to the <see cref="IDuplexPipe"/> the SSH transport is written against.
    /// The public surface uses Hermod's networking types (<see cref="IPSocket"/>, <see cref="IPPort"/>,
    /// <see cref="IIPAddress"/>); the conversion to the <c>System.Net</c> socket types happens here at
    /// the lowest layer. IPv6 is first-class: an IPv6 endpoint yields a dual-stack listener.
    /// </summary>
    public static class SshTcp
    {

        #region AsDuplexPipe(Socket)

        /// <summary>
        /// Wrap a connected socket as a duplex pipe. The pipe owns the socket and closes it on completion.
        /// </summary>
        public static IDuplexPipe AsDuplexPipe(Socket Socket)
        {
            var stream = new NetworkStream(Socket, ownsSocket: true);
            return new DuplexPipe(PipeReader.Create(stream), PipeWriter.Create(stream));
        }

        #endregion

        #region ConnectAsync(RemoteSocket, CancellationToken = default)

        /// <summary>
        /// Connect to a remote IP socket (IPv4 or IPv6) and return the connection as a duplex pipe.
        /// </summary>
        public static async Task<IDuplexPipe> ConnectAsync(IPSocket           RemoteSocket,
                                                           CancellationToken  CancellationToken = default)
        {

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(RemoteSocket.IPAddress.ToDotNet(), RemoteSocket.Port.ToInt32(), CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return AsDuplexPipe(socket);

        }

        #endregion

        #region ConnectAsync(Host, Port, CancellationToken = default)

        /// <summary>
        /// Connect to a host name (or IP literal) and port and return the connection as a duplex pipe.
        /// </summary>
        public static async Task<IDuplexPipe> ConnectAsync(String             Host,
                                                           IPPort             Port,
                                                           CancellationToken  CancellationToken = default)
        {

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(Host, Port.ToInt32(), CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return AsDuplexPipe(socket);

        }

        #endregion

    }


    /// <summary>
    /// A minimal TCP listener that hands each accepted connection to the SSH transport as an
    /// <see cref="IDuplexPipe"/>. Bind to an IPv6 endpoint to get a dual-stack listener.
    /// </summary>
    public sealed class SshTcpListener : IDisposable
    {

        #region Data

        private readonly Socket socket;

        #endregion

        #region Properties

        /// <summary>The local socket the listener is bound to (with the actual port when 0 was requested).</summary>
        public IPSocket LocalEndPoint
            => IPSocket.FromIPEndPoint((IPEndPoint) socket.LocalEndPoint!);

        #endregion

        #region Constructor(s)

        private SshTcpListener(Socket Socket)
        {
            this.socket = Socket;
        }

        #endregion


        #region (static) Start(EndPoint, Backlog = 32)

        /// <summary>
        /// Start listening on the given IP socket. An IPv6 address enables dual-stack mode (IPv4 and IPv6).
        /// Use <see cref="IPPort.Auto"/> (port 0) to let the OS choose a free port (read it back from
        /// <see cref="LocalEndPoint"/>).
        /// </summary>
        /// <param name="EndPoint">The IP socket to bind to.</param>
        /// <param name="Backlog">The listen backlog.</param>
        public static SshTcpListener Start(IPSocket EndPoint, Int32 Backlog = 32)
        {

            var dotNetAddress  = EndPoint.IPAddress.ToDotNet();
            var socket         = new Socket(dotNetAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            if (dotNetAddress.AddressFamily == AddressFamily.InterNetworkV6)
                socket.DualMode = true;   // accept IPv4-mapped connections too

            socket.Bind(new IPEndPoint(dotNetAddress, EndPoint.Port.ToInt32()));
            socket.Listen(Backlog);

            return new SshTcpListener(socket);

        }

        #endregion

        #region AcceptAsync(CancellationToken = default)

        /// <summary>
        /// Accept the next incoming connection as a duplex pipe.
        /// </summary>
        public async Task<IDuplexPipe> AcceptAsync(CancellationToken CancellationToken = default)
            => (await AcceptWithPeerAsync(CancellationToken).ConfigureAwait(false)).Pipe;

        /// <summary>
        /// Accept the next incoming connection, keeping the peer's address alongside the pipe. Needed
        /// wherever an authorization decision depends on <i>where</i> the client connected from — a
        /// certificate's <c>source-address</c> restriction, or address-based auditing.
        /// </summary>
        /// <param name="CancellationToken">An optional token to cancel the accept.</param>
        public async Task<(IDuplexPipe Pipe, IPSocket? Peer)> AcceptWithPeerAsync(CancellationToken CancellationToken = default)
        {

            var client = await socket.AcceptAsync(CancellationToken).ConfigureAwait(false);
            client.NoDelay = true;

            var peer = client.RemoteEndPoint is IPEndPoint remote
                           ? IPSocket.FromIPEndPoint(remote)
                           : (IPSocket?) null;

            return (SshTcp.AsDuplexPipe(client), peer);

        }

        #endregion

        #region Dispose()

        /// <summary>Stop listening.</summary>
        public void Dispose()
            => socket.Dispose();

        #endregion

    }

}
