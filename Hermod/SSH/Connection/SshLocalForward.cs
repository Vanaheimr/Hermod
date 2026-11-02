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
    /// Either side may half-close: an end of input on one side reaches the other as such (a channel EOF,
    /// or a shutdown of the socket's sending side), and the connection lasts until both have ended - or
    /// until the server closes the channel.
    /// </remarks>
    public sealed class SshLocalForward : IAsyncDisposable
    {

        #region Data

        private const    Int32                    RelayBuffer = 32 * 1024;

        private readonly SshChannelMultiplexer    mux;
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
        public UInt16    RemotePort       { get; }

        #endregion

        #region Constructor(s)

        private SshLocalForward(SshChannelMultiplexer    Mux,
                                Socket                   Listener,
                                CancellationTokenSource  CTS,
                                String                   RemoteHost,
                                UInt16                   RemotePort)
        {

            this.mux            = Mux;
            this.listener       = Listener;
            this.cts            = CTS;

            this.LocalEndPoint  = IPSocket.FromIPEndPoint((System.Net.IPEndPoint) Listener.LocalEndPoint!);
            this.RemoteHost     = RemoteHost;
            this.RemotePort     = RemotePort;

            this.acceptLoop     = AcceptLoopAsync(CTS.Token);

        }

        #endregion


        #region (internal static) Start(Mux, LocalSocket, RemoteHost, RemotePort, Lifetime)

        /// <summary>
        /// Listen on <paramref name="LocalSocket"/> and tunnel what arrives there through
        /// <paramref name="Mux"/>, until disposed or until <paramref name="Lifetime"/> ends.
        /// </summary>
        /// <exception cref="SocketException">The local socket cannot be bound.</exception>
        internal static SshLocalForward Start(SshChannelMultiplexer  Mux,
                                              IPSocket               LocalSocket,
                                              String                 RemoteHost,
                                              UInt16                 RemotePort,
                                              CancellationToken      Lifetime)
        {

            var endPoint = LocalSocket.ToIPEndPoint();
            var socket   = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {

                // An IPv6 address listens for IPv4 too, as with SshTcpListener.
                if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    socket.DualMode = true;

                socket.Bind(endPoint);
                socket.Listen(32);

            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return new SshLocalForward(Mux,
                                       socket,
                                       CancellationTokenSource.CreateLinkedTokenSource(Lifetime),
                                       RemoteHost,
                                       RemotePort);

        }

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


        #region (private) AcceptLoopAsync(CancellationToken)

        private async Task AcceptLoopAsync(CancellationToken CancellationToken)
        {
            try
            {
                while (true)
                {
                    var connection = await listener.AcceptAsync(CancellationToken).ConfigureAwait(false);
                    connection.NoDelay = true;
                    _ = Task.Run(() => TunnelAsync(connection, CancellationToken), CancellationToken);
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

        #region (private) TunnelAsync(Connection, CancellationToken)

        private async Task TunnelAsync(Socket Connection, CancellationToken CancellationToken)
        {
            using (Connection)
            {

                SshMuxChannel channel;

                try
                {

                    var origin  = (System.Net.IPEndPoint) Connection.RemoteEndPoint!;
                    var address = origin.Address.IsIPv4MappedToIPv6 ? origin.Address.MapToIPv4() : origin.Address;

                    channel = await mux.OpenChannelAsync("direct-tcpip",
                                                         DirectTcpIp(RemoteHost, RemotePort, address.ToString(), (UInt16) origin.Port),
                                                         CancellationToken).ConfigureAwait(false);

                }
                catch
                {
                    // Refused by the server, or the connection is gone: this one is dropped, the listener stays.
                    return;
                }

                await RelayAsync(channel, Connection, CancellationToken).ConfigureAwait(false);

            }
        }

        #endregion

        #region (private) relay

        /// <summary>
        /// Relay both ways until both have ended. A direction that ends passes its end on (EOF, or a
        /// shutdown of the socket's sending side) and leaves the other running; one that breaks ends both.
        /// </summary>
        private static async Task RelayAsync(SshMuxChannel Channel, Socket Connection, CancellationToken CancellationToken)
        {

            using var stream  = new NetworkStream(Connection, ownsSocket: false);
            using var upwards = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            // Once the server has closed the channel, nothing the local side still sends can go anywhere.
            // Downwards needs no such stop: what arrived before the close is still delivered, then it ends.
            _ = Channel.Closed.ContinueWith(_ => { try { upwards.Cancel(); } catch (ObjectDisposedException) { } },
                                            TaskScheduler.Default);

            var up   = SocketToChannelAsync(stream, Channel, upwards.Token);
            var down = ChannelToSocketAsync(Channel, stream, Connection, CancellationToken);

            if (!await await Task.WhenAny(up, down).ConfigureAwait(false))
            {
                await upwards.CancelAsync().ConfigureAwait(false);
                try { await Channel.CloseAsync().ConfigureAwait(false); } catch { }
            }

            await Task.WhenAll(up, down).ConfigureAwait(false);

            try { await Channel.CloseAsync().ConfigureAwait(false); } catch { }

        }

        private static async Task<Boolean> SocketToChannelAsync(NetworkStream From, SshMuxChannel To, CancellationToken CancellationToken)
        {
            var buffer = new Byte[RelayBuffer];
            try
            {
                Int32 read;
                while ((read = await From.ReadAsync(buffer, CancellationToken).ConfigureAwait(false)) > 0)
                    await To.SendDataAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                await To.SendEofAsync(CancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<Boolean> ChannelToSocketAsync(SshMuxChannel From, NetworkStream To, Socket Connection, CancellationToken CancellationToken)
        {
            var buffer = new Byte[RelayBuffer];
            var input  = From.Input;
            try
            {
                Int32 read;
                while ((read = await input.ReadAsync(buffer, CancellationToken).ConfigureAwait(false)) > 0)
                    await To.WriteAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                Connection.Shutdown(SocketShutdown.Send);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Byte[] DirectTcpIp(String Host, UInt16 Port, String OriginAddress, UInt16 OriginPort)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(Host);          w.WriteUInt32(Port);
            w.WriteString(OriginAddress); w.WriteUInt32(OriginPort);
            return abw.WrittenSpan.ToArray();
        }

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()
            => $"-L {LocalEndPoint}:{RemoteHost}:{RemotePort}";

        #endregion

    }

}
