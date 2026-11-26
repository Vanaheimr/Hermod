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

using System.Buffers;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A local (<c>ssh -L</c>) forward: this side listens on <see cref="LocalEndPoint"/>, and every
    /// connection made there is tunnelled through the server to <see cref="RemoteHost"/>:
    /// <see cref="RemotePort"/>, in a <c>direct-tcpip</c> channel of its own. Disposing it stops listening
    /// and ends the connections it carries; so does the end of the client it belongs to.
    /// </summary>
    /// <remarks>
    /// A connection the server refuses to tunnel is closed, as <c>ssh</c> does; the listener stays.
    /// Either side may half-close (see <see cref="SshTcpRelay"/>).
    /// </remarks>
    public sealed class SSHLocalForward : IAsyncDisposable
    {

        #region Data

        private readonly SshChannelMultiplexer    channelMultiplexer;
        private readonly Socket                   listener;
        private readonly CancellationTokenSource  cts;
        private readonly Task                     acceptLoop;
        private          Int32                    disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The local socket this side listens on, with the port the OS chose when asked for port 0.
        /// </summary>
        public IPSocket  LocalEndPoint    { get; }

        /// <summary>
        /// The host the server connects each tunnelled connection to, as the server resolves it.
        /// </summary>
        public String    RemoteHost       { get; }

        /// <summary>
        /// The port the server connects each tunnelled connection to.
        /// </summary>
        public IPPort    RemotePort       { get; }

        #endregion

        #region Constructor(s)

        private SSHLocalForward(SshChannelMultiplexer    ChannelMultiplexer,
                                Socket                   Listener,
                                CancellationTokenSource  CTS,
                                String                   RemoteHost,
                                IPPort                   RemotePort)
        {

            this.channelMultiplexer  = ChannelMultiplexer;
            this.listener            = Listener;
            this.cts                 = CTS;

            this.LocalEndPoint       = IPSocket.FromIPEndPoint((System.Net.IPEndPoint) Listener.LocalEndPoint!);
            this.RemoteHost          = RemoteHost;
            this.RemotePort          = RemotePort;

            this.acceptLoop          = AcceptLoop(CTS.Token);

        }

        #endregion


        #region (internal static) Start (ChannelMultiplexer, LocalSocket, RemoteHost, RemotePort, Lifetime)

        /// <summary>
        /// Listen on <paramref name="LocalSocket"/> and tunnel what arrives there through
        /// <paramref name="ChannelMultiplexer"/>, until disposed or until <paramref name="Lifetime"/> ends.
        /// </summary>
        /// <exception cref="SocketException">The local socket cannot be bound.</exception>
        internal static SSHLocalForward Start(SshChannelMultiplexer  ChannelMultiplexer,
                                              IPSocket               LocalSocket,
                                              String                 RemoteHost,
                                              IPPort                 RemotePort,
                                              CancellationToken      Lifetime)
        {

            var endPoint = LocalSocket.ToIPEndPoint();
            var socket   = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {

                if (!OperatingSystem.IsWindows())
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // An IPv6 address listens for IPv4 too, as with SshTcpListener.
                if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    socket.DualMode = true;

                socket.Bind(endPoint);
                socket.Listen(128);

            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return new SSHLocalForward(
                       ChannelMultiplexer,
                       socket,
                       CancellationTokenSource.CreateLinkedTokenSource(Lifetime, ChannelMultiplexer.EndedToken),
                       RemoteHost,
                       RemotePort
                   );

        }

        #endregion


        #region (private) AcceptLoop    (CancellationToken)

        private async Task AcceptLoop(CancellationToken CancellationToken)
        {
            try
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    Socket connection;
                    try
                    {
                        connection = await listener.AcceptAsync(CancellationToken).ConfigureAwait(false);
                    }
                    catch (SocketException) when (!CancellationToken.IsCancellationRequested)
                    {
                        // Transient accept error (e.g. peer reset before accept, temporary FD pressure):
                        // keep the listener alive rather than killing the entire forward.
                        await Task.Delay(50, CancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    SshTcp.ConfigureConnectedSocket(connection);
                    // Do not pass CancellationToken to Task.Run: if cancelled before scheduling, the
                    // lambda would never run and the accepted socket would leak.
                    _ = Task.Run(() => Tunnel(connection, CancellationToken));
                }
            }
            catch
            { }
            finally
            {
                // Whoever ended the loop - DisposeAsync, or the end of the client - the port is free again.
                listener.Dispose();
            }
        }

        #endregion

        #region (private) Tunnel        (Connection, CancellationToken)

        private async Task Tunnel(Socket             Connection,
                                  CancellationToken  CancellationToken)
        {

            SshMuxChannel channel;

            try
            {

                var origin  = (System.Net.IPEndPoint) Connection.RemoteEndPoint!;
                var address = origin.Address.IsIPv4MappedToIPv6 ? origin.Address.MapToIPv4() : origin.Address;

                channel = await channelMultiplexer.OpenChannelAsync(
                                    "direct-tcpip",
                                    DirectTCP_IP(
                                        RemoteHost,
                                        RemotePort,
                                        address.ToString(),
                                        IPPort.Parse(origin.Port)
                                    ),
                                    CancellationToken
                                ).ConfigureAwait(false);

            }
            catch
            {
                // Refused by the server, or the connection is gone: this one is dropped, the listener stays.
                Connection.Dispose();
                return;
            }

            await SshTcpRelay.RelayAsync(
                      channel,
                      Connection,
                      CancellationToken
                  ).ConfigureAwait(false);

        }

        #endregion

        #region (private) DirectTCP_IP  (Host, Port, OriginAddress, OriginPort)

        private static Byte[] DirectTCP_IP(String  Host,
                                           IPPort  Port,
                                           String  OriginAddress,
                                           IPPort  OriginPort)
        {

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);

            w.WriteString(Host);
            w.WriteUInt32(Port.      ToUInt16());
            w.WriteString(OriginAddress);
            w.WriteUInt32(OriginPort.ToUInt16());

            return abw.WrittenSpan.ToArray();

        }

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()
            => $"-L {LocalEndPoint}:{RemoteHost}:{RemotePort}";

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Stop listening and end the connections under way.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            await cts.CancelAsync().ConfigureAwait(false);

            try   { await acceptLoop.ConfigureAwait(false); }
            catch { }

            cts.Dispose();

        }

        #endregion


    }

}
