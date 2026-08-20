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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Relays data between exactly two clients.
    ///
    /// Both directions are pumped independently and without any intermediate
    /// queue, so that the TCP flow control of the slower side propagates all the
    /// way through to the faster side. A half-close of one side is forwarded to
    /// the other side, which is what SSH port forwarding and the usual
    /// "pipe a file through the connection" tools expect.
    /// </summary>
    internal static class PipeRelay
    {

        #region RunAsync(Session, ClientA, ClientB, Settings, Logger, CancellationToken)

        /// <summary>
        /// Relay data between the two given clients until both directions are closed.
        /// </summary>
        /// <param name="Session">The rendezvous.</param>
        /// <param name="ClientA">The first client.</param>
        /// <param name="ClientB">The second client.</param>
        /// <param name="Settings">The settings of the transfer profile.</param>
        /// <param name="Logger">A logger.</param>
        /// <param name="CancellationToken">A token to stop relaying.</param>
        public static Task RunAsync(RendezvousSession        Session,
                                    Socket                   ClientA,
                                    Socket                   ClientB,
                                    TransferProfileSettings  Settings,
                                    ILogger                  Logger,
                                    CancellationToken        CancellationToken)

            => Task.WhenAll(

                   PumpAsync(Session, ClientA, ClientB, Settings, Logger, CancellationToken),
                   PumpAsync(Session, ClientB, ClientA, Settings, Logger, CancellationToken)

               );

        #endregion

        #region (private, static) PumpAsync(Session, Source, Destination, Settings, Logger, CancellationToken)

        /// <summary>
        /// Copy everything from the source socket to the destination socket.
        /// </summary>
        private static async Task PumpAsync(RendezvousSession        Session,
                                            Socket                   Source,
                                            Socket                   Destination,
                                            TransferProfileSettings  Settings,
                                            ILogger                  Logger,
                                            CancellationToken        CancellationToken)
        {

            var buffer = ArrayPool<Byte>.Shared.Rent(Settings.RelayBufferSize);

            try
            {

                while (!CancellationToken.IsCancellationRequested)
                {

                    var received = await Source.ReceiveAsync(buffer.AsMemory(0, Settings.RelayBufferSize),
                                                             SocketFlags.None,
                                                             CancellationToken).
                                                ConfigureAwait(false);

                    if (received == 0)
                    {
                        // The source closed its sending side: forward the half-close,
                        // but keep the other direction alive.
                        Destination.ShutdownSafely(SocketShutdown.Send);
                        return;
                    }

                    Session.ReportActivity(received);

                    await Destination.SendAllAsync(buffer.AsMemory(0, received),
                                                   CancellationToken).
                                      ConfigureAwait(false);

                }

            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            catch (SocketException e)
            {

                Logger.LogDebug("Rendezvous {SessionId}: the connection was reset ({SocketError}).",
                                Session.Id, e.SocketErrorCode);

                Session.Close(SessionCloseReason.ClientDisconnected);

            }
            catch (IOException e)
            {

                Logger.LogDebug("Rendezvous {SessionId}: the connection failed: {Message}",
                                Session.Id, e.Message);

                Session.Close(SessionCloseReason.ClientDisconnected);

            }
            catch (Exception e)
            {

                Logger.LogWarning(e, "Rendezvous {SessionId}: unexpected relay error!", Session.Id);

                Session.Close(SessionCloseReason.Error);

            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(buffer);
            }

        }

        #endregion

    }

}
