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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Socket extension methods of the rendezvous service.
    /// </summary>
    internal static class SocketExtensions
    {

        #region ApplyProfile(this Socket, Settings, Logger)

        /// <summary>
        /// Apply the TCP parameters of the given transfer profile to the given socket.
        ///
        /// Not every option is available on every platform and some of them are
        /// rejected by some operating systems for perfectly normal sockets, therefore
        /// every option is applied on a best effort basis.
        /// </summary>
        /// <param name="Socket">A freshly accepted socket.</param>
        /// <param name="Settings">The settings of the transfer profile.</param>
        /// <param name="Logger">A logger.</param>
        public static void ApplyProfile(this Socket              Socket,
                                        TransferProfileSettings  Settings,
                                        ILogger                  Logger)
        {

            TrySetOption(Logger, nameof(Settings.NoDelay),
                         () => Socket.NoDelay = Settings.NoDelay);

            // A null value keeps the operating system default and therefore
            // its receive window auto-tuning.
            if (Settings.SocketReceiveBufferSize is Int32 receiveBufferSize)
                TrySetOption(Logger, nameof(Settings.SocketReceiveBufferSize),
                             () => Socket.ReceiveBufferSize = receiveBufferSize);

            if (Settings.SocketSendBufferSize    is Int32 sendBufferSize)
                TrySetOption(Logger, nameof(Settings.SocketSendBufferSize),
                             () => Socket.SendBufferSize    = sendBufferSize);

            if (Settings.TcpKeepAlive)
            {

                TrySetOption(Logger, "SO_KEEPALIVE",
                             () => Socket.SetSocketOption(SocketOptionLevel.Socket,
                                                          SocketOptionName.KeepAlive,
                                                          true));

                TrySetOption(Logger, "TCP_KEEPIDLE",
                             () => Socket.SetSocketOption(SocketOptionLevel.Tcp,
                                                          SocketOptionName.TcpKeepAliveTime,
                                                          ToSeconds(Settings.KeepAliveTime)));

                TrySetOption(Logger, "TCP_KEEPINTVL",
                             () => Socket.SetSocketOption(SocketOptionLevel.Tcp,
                                                          SocketOptionName.TcpKeepAliveInterval,
                                                          ToSeconds(Settings.KeepAliveInterval)));

                TrySetOption(Logger, "TCP_KEEPCNT",
                             () => Socket.SetSocketOption(SocketOptionLevel.Tcp,
                                                          SocketOptionName.TcpKeepAliveRetryCount,
                                                          Settings.KeepAliveRetryCount));

            }

            // Close gracefully: flush whatever is left within the send buffer.
            TrySetOption(Logger, "SO_LINGER",
                         () => Socket.LingerState = new LingerOption(false, 0));

        }

        #endregion

        #region SendAllAsync(this Socket, Data, CancellationToken)

        /// <summary>
        /// Send all given data, even when the socket accepts only a part of it.
        /// </summary>
        /// <param name="Socket">A connected socket.</param>
        /// <param name="Data">The data to send.</param>
        /// <param name="CancellationToken">A token to cancel sending.</param>
        public static async ValueTask SendAllAsync(this Socket           Socket,
                                                   ReadOnlyMemory<Byte>  Data,
                                                   CancellationToken     CancellationToken)
        {

            while (!Data.IsEmpty)
            {

                var sent = await Socket.SendAsync(Data,
                                                  SocketFlags.None,
                                                  CancellationToken).
                                        ConfigureAwait(false);

                if (sent <= 0)
                    throw new IOException("The socket did not accept any data!");

                Data = Data[sent..];

            }

        }

        #endregion

        #region ShutdownSafely(this Socket, How)

        /// <summary>
        /// Shutdown the given socket, ignoring a peer that is already gone.
        /// </summary>
        /// <param name="Socket">A socket.</param>
        /// <param name="How">Which part of the connection to shut down.</param>
        public static void ShutdownSafely(this Socket     Socket,
                                          SocketShutdown  How)
        {
            try
            {
                Socket.Shutdown(How);
            }
            catch (SocketException)
            { }
            catch (ObjectDisposedException)
            { }
        }

        #endregion


        #region (private, static) TrySetOption(Logger, Name, Setter)

        private static void TrySetOption(ILogger  Logger,
                                         String   Name,
                                         Action   Setter)
        {
            try
            {
                Setter();
            }
            catch (SocketException e)
            {
                Logger.LogDebug("The socket option {Option} is not supported on this platform: {Message}", Name, e.Message);
            }
            catch (PlatformNotSupportedException e)
            {
                Logger.LogDebug("The socket option {Option} is not supported on this platform: {Message}", Name, e.Message);
            }
            catch (ObjectDisposedException)
            { }
        }

        #endregion

        #region (private, static) ToSeconds(TimeSpan)

        private static Int32 ToSeconds(TimeSpan TimeSpan)

            => Math.Max(1, (Int32) Math.Round(TimeSpan.TotalSeconds));

        #endregion

    }

}
