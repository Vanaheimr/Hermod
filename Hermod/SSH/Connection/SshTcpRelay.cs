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

using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The relay between a forwarding channel and its TCP connection, for every kind of forward: the
    /// client's <c>-L</c> listener, the server's <c>direct-tcpip</c> dial, and both ends of a <c>-R</c>.
    /// </summary>
    /// <remarks>
    /// Either side may half-close. A direction that ends passes its end on - a channel EOF, or a shutdown
    /// of the socket's sending side - and leaves the other running, so an answer that comes only after
    /// the end of the question (an HTTP/1.0 server, <c>nc -N</c>, a reader of whole files) still arrives.
    /// A direction that breaks ends both. Once the peer has closed the channel, nothing more is read from
    /// the socket, since it could go nowhere; what the channel had delivered before is still written out.
    /// </remarks>
    internal static class SshTcpRelay
    {

        #region Data

        private const Int32 Buffer = 32 * 1024;

        #endregion


        #region RelayAsync(Channel, Connection, CancellationToken)

        /// <summary>
        /// Relay both ways between <paramref name="Channel"/> and <paramref name="Connection"/> until both
        /// directions have ended, then close the channel and the socket.
        /// </summary>
        public static async Task RelayAsync(SshMuxChannel Channel, Socket Connection, CancellationToken CancellationToken)
        {

            using (Connection)
            {

                using var stream  = new NetworkStream(Connection, ownsSocket: false);
                using var upwards = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

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

        }

        #endregion


        #region (private) SocketToChannelAsync / ChannelToSocketAsync

        private static async Task<Boolean> SocketToChannelAsync(NetworkStream From, SshMuxChannel To, CancellationToken CancellationToken)
        {
            var buffer = new Byte[Buffer];
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
            var buffer = new Byte[Buffer];
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

        #endregion

    }

}
