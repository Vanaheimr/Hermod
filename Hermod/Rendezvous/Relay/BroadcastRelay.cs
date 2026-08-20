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
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Relays data between three or more clients: a chat.
    /// Everything one client sends is forwarded to all other clients, and with
    /// EchoToSender back to the sender as well. This service does not add any
    /// framing, it is a plain byte relay - the chat protocol is up to the clients.
    ///
    /// Every client has its own bounded outbound queue, so that one slow reader
    /// can not stall the whole chat (head-of-line blocking). A client that
    /// exceeds its queue limits is disconnected.
    ///
    /// The fan-out of one chunk to all queues happens under a lock: without it
    /// two clients sending at the same time could reach the queues of the others
    /// in a different order, and every receiver would see its own version of the
    /// conversation. With it the service is the one and only sequencer, and all
    /// receivers see the very same sequence of chunks.
    /// </summary>
    internal sealed class BroadcastRelay
    {

        #region (class) Participant

        /// <summary>
        /// One client of a chat rendezvous.
        /// </summary>
        private sealed class Participant
        {

            public readonly IPPort                  Port;
            public readonly Socket                  Socket;
            public readonly Channel<RelayChunk>     Outbox;

            public          Int32                   QueuedBytes;
            public          Int32                   HasLeft;

            public Participant(IPPort  Port,
                               Socket  Socket,
                               Int32   QueueLength)
            {

                this.Port    = Port;
                this.Socket  = Socket;
                this.Outbox  = Channel.CreateBounded<RelayChunk>(
                                   new BoundedChannelOptions(QueueLength) {
                                       FullMode      = BoundedChannelFullMode.Wait,
                                       SingleReader  = true,
                                       SingleWriter  = false
                                   }
                               );

            }

        }

        #endregion

        #region Data

        private readonly RendezvousSession        session;
        private readonly Participant[]            participants;
        private readonly TransferProfileSettings  settings;
        private readonly Boolean                  echoToSender;
        private readonly ILogger                  logger;
        private readonly Lock                     fanOutLock  = new();

        private          Int32                    activeCount;

        #endregion

        #region Constructor(s)

        private BroadcastRelay(RendezvousSession        Session,
                               IReadOnlyList<IPPort>    Ports,
                               IReadOnlyList<Socket>    Sockets,
                               TransferProfileSettings  Settings,
                               Boolean                  EchoToSender,
                               ILogger                  Logger)
        {

            this.session       = Session;
            this.settings      = Settings;
            this.echoToSender  = EchoToSender;
            this.logger        = Logger;
            this.participants  = new Participant[Sockets.Count];

            for (var i = 0; i < Sockets.Count; i++)
                participants[i] = new Participant(Ports[i],
                                                  Sockets[i],
                                                  Settings.BroadcastQueueLength);

            this.activeCount   = participants.Length;

        }

        #endregion


        #region (static) RunAsync(Session, Ports, Sockets, Settings, Logger, CancellationToken)

        /// <summary>
        /// Relay data between the given clients until less than two of them are left.
        /// </summary>
        /// <param name="Session">The rendezvous.</param>
        /// <param name="Ports">The TCP ports of the clients, used for logging.</param>
        /// <param name="Sockets">The connected clients.</param>
        /// <param name="Settings">The settings of the transfer profile.</param>
        /// <param name="EchoToSender">Whether a client also receives what it sends itself.</param>
        /// <param name="Logger">A logger.</param>
        /// <param name="CancellationToken">A token to stop relaying.</param>
        public static Task RunAsync(RendezvousSession        Session,
                                    IReadOnlyList<IPPort>    Ports,
                                    IReadOnlyList<Socket>    Sockets,
                                    TransferProfileSettings  Settings,
                                    Boolean                  EchoToSender,
                                    ILogger                  Logger,
                                    CancellationToken        CancellationToken)

            => new BroadcastRelay(Session, Ports, Sockets, Settings, EchoToSender, Logger).
                   RunAsync(CancellationToken);

        #endregion

        #region (private) RunAsync(CancellationToken)

        private Task RunAsync(CancellationToken CancellationToken)
        {

            var tasks = new List<Task>(participants.Length * 2);

            foreach (var participant in participants)
            {
                tasks.Add(ReadLoopAsync (participant, CancellationToken));
                tasks.Add(WriteLoopAsync(participant, CancellationToken));
            }

            return Task.WhenAll(tasks);

        }

        #endregion

        #region (private) ReadLoopAsync (Participant, CancellationToken)

        /// <summary>
        /// Read from one client and broadcast to all others.
        /// </summary>
        private async Task ReadLoopAsync(Participant        Participant,
                                         CancellationToken  CancellationToken)
        {

            var buffer = ArrayPool<Byte>.Shared.Rent(settings.RelayBufferSize);

            try
            {

                while (!CancellationToken.IsCancellationRequested)
                {

                    var received = await Participant.Socket.ReceiveAsync(buffer.AsMemory(0, settings.RelayBufferSize),
                                                                         SocketFlags.None,
                                                                         CancellationToken).
                                                            ConfigureAwait(false);

                    if (received == 0)
                        break;

                    session.ReportActivity(received);

                    Broadcast(Participant, buffer.AsSpan(0, received));

                }

            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            catch (SocketException e)
            {
                logger.LogDebug("Rendezvous {SessionId}: the client on TCP/{Port} was reset ({SocketError}).",
                                session.Id, Participant.Port, e.SocketErrorCode);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Rendezvous {SessionId}: unexpected error while reading from TCP/{Port}!",
                                  session.Id, Participant.Port);
            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(buffer);
                Leave(Participant, "closed the connection", DiscardQueue: false);
            }

        }

        #endregion

        #region (private) WriteLoopAsync(Participant, CancellationToken)

        /// <summary>
        /// Send everything that was queued for one client.
        /// </summary>
        private async Task WriteLoopAsync(Participant        Participant,
                                          CancellationToken  CancellationToken)
        {

            try
            {

                await foreach (var chunk in Participant.Outbox.Reader.ReadAllAsync(CancellationToken).
                                                                      ConfigureAwait(false))
                {
                    try
                    {
                        await Participant.Socket.SendAllAsync(chunk.Data, CancellationToken).
                                                 ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Add(ref Participant.QueuedBytes, -chunk.Length);
                        chunk.Return();
                    }
                }

                // Everything that was queued has been sent and nobody is left
                // to send anything: tell the client that this is the end.
                Participant.Socket.ShutdownSafely(SocketShutdown.Send);

            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            catch (SocketException e)
            {
                logger.LogDebug("Rendezvous {SessionId}: sending to TCP/{Port} failed ({SocketError}).",
                                session.Id, Participant.Port, e.SocketErrorCode);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Rendezvous {SessionId}: unexpected error while sending to TCP/{Port}!",
                                  session.Id, Participant.Port);
            }
            finally
            {

                // Return everything that will never be sent.
                while (Participant.Outbox.Reader.TryRead(out var pending))
                {
                    Interlocked.Add(ref Participant.QueuedBytes, -pending.Length);
                    pending.Return();
                }

                Leave(Participant, "can no longer be reached", DiscardQueue: false);

            }

        }

        #endregion

        #region (private) Broadcast(Sender, Data)

        /// <summary>
        /// Queue the given data for every other client - and for the sender as
        /// well, when the rendezvous asked to be echoed.
        ///
        /// The whole fan-out happens under one lock, so that a chunk reaches all
        /// queues before the next one reaches any of them. That is what makes the
        /// service the single sequencer of the conversation: every receiver sees
        /// the same chunks in the same order. Nothing within the lock blocks -
        /// a full queue is reported by TryWrite instead of waiting.
        /// </summary>
        private void Broadcast(Participant         Sender,
                               ReadOnlySpan<Byte>  Data)
        {

            lock (fanOutLock)
            {

                foreach (var receiver in participants)
                {

                    if (Volatile.Read(ref receiver.HasLeft) != 0)
                        continue;

                    if (ReferenceEquals(receiver, Sender) && !echoToSender)
                        continue;

                    var chunk   = RelayChunk.Copy(Data);
                    var queued  = Interlocked.Add(ref receiver.QueuedBytes, chunk.Length);

                    if (queued > settings.BroadcastQueueBytes ||
                       !receiver.Outbox.Writer.TryWrite(chunk))
                    {

                        Interlocked.Add(ref receiver.QueuedBytes, -chunk.Length);
                        chunk.Return();

                        logger.LogWarning("Rendezvous {SessionId}: the client on TCP/{Port} can not keep up and is disconnected!",
                                          session.Id, receiver.Port);

                        Leave(receiver, "could not keep up", DiscardQueue: true);

                    }

                }

            }

        }

        #endregion

        #region (private) Leave(Participant, Reason, DiscardQueue)

        /// <summary>
        /// Remove one client from the chat. When less than two clients are left,
        /// the whole rendezvous is closed, as nobody could talk to anybody anymore.
        /// </summary>
        private void Leave(Participant  Participant,
                           String       Reason,
                           Boolean      DiscardQueue)
        {

            if (Interlocked.Exchange(ref Participant.HasLeft, 1) != 0)
                return;

            logger.LogInformation("Rendezvous {SessionId}: the client on TCP/{Port} {Reason}.",
                                  session.Id, Participant.Port, Reason);

            // Let the write loop drain whatever is left, and then stop.
            Participant.Outbox.Writer.TryComplete();

            if (DiscardQueue)
            {
                Participant.Socket.ShutdownSafely(SocketShutdown.Both);
                Participant.Socket.Dispose();
            }

            if (Interlocked.Decrement(ref activeCount) < 2)
                session.Close(SessionCloseReason.ClientDisconnected);

        }

        #endregion

    }

}
