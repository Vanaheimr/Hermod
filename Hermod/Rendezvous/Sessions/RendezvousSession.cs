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
    /// A rendezvous: two or more TCP listeners waiting for their clients,
    /// and the data relay between those clients.
    ///
    /// The life cycle of a rendezvous:
    ///
    ///   1. Pending      - the listeners are waiting. When not all clients arrive
    ///                     before the rendezvous timeout elapses, everything is closed.
    ///   2. Established  - all clients arrived, their data is relayed. When no payload
    ///                     is relayed before the idle timeout elapses, everything is closed.
    ///   3. Closed       - all connections and listeners are gone, the TCP ports are free again.
    ///
    /// </summary>
    public sealed class RendezvousSession : IAsyncDisposable
    {

        #region Data

        private readonly SessionEndpoint[]         endpoints;
        private readonly String[]                  createdBy;
        private readonly TimeProvider              timeProvider;
        private readonly ILogger                   logger;
        private readonly CancellationTokenSource   lifetime      = new();
        private readonly TaskCompletionSource      closedSource  = new (TaskCreationOptions.RunContinuationsAsynchronously);

        private          Int32                     connectedCount;
        private          Int32                     state;
        private          Int32                     closeRequested;
        private          Int64                     lastActivityTicks;
        private          Int64                     bytesRelayed;
        private          Task?                     runTask;
        private          Task?                     relayTask;

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of this rendezvous.
        /// </summary>
        public Guid                            Id                 { get; }

        /// <summary>
        /// The transfer profile of this rendezvous.
        /// </summary>
        public TransferProfile                 Profile            { get; }

        /// <summary>
        /// The buffer sizes and TCP parameters of this rendezvous.
        /// </summary>
        public TransferProfileSettings         ProfileSettings    { get; }

        /// <summary>
        /// The endpoints of this rendezvous.
        /// </summary>
        public IReadOnlyList<SessionEndpoint>  Endpoints
            => endpoints;

        /// <summary>
        /// The TCP ports of this rendezvous.
        /// </summary>
        public IReadOnlyList<IPPort>           Ports              { get; }

        /// <summary>
        /// The timestamp when this rendezvous was created.
        /// </summary>
        public DateTimeOffset                  CreatedUtc         { get; }

        /// <summary>
        /// The identifications of the keys that opened this rendezvous, and that
        /// may therefore close it again. Empty when a caller within the same
        /// process opened it without naming itself.
        /// </summary>
        public IReadOnlyList<String>           CreatedBy
            => createdBy;

        /// <summary>
        /// An optional description of this rendezvous, e.g. what it is used for.
        /// </summary>
        public String?                         Description        { get; }

        /// <summary>
        /// Whether a client also receives what it sends itself.
        ///
        /// A sender otherwise never learns where its own bytes ended up within
        /// the conversation, as the service decides how the senders interleave.
        /// With the echo every client - the sender included - receives the very
        /// same byte stream, which makes the service the one and only sequencer.
        /// </summary>
        public Boolean                         EchoToSender       { get; }

        /// <summary>
        /// The timestamp when all clients had arrived, or null.
        /// </summary>
        public DateTimeOffset?                 EstablishedUtc     { get; private set; }

        /// <summary>
        /// The timestamp of the last relayed payload.
        /// </summary>
        public DateTimeOffset                  LastActivityUtc
            => new (Interlocked.Read(ref lastActivityTicks), TimeSpan.Zero);

        /// <summary>
        /// The current state of this rendezvous.
        /// </summary>
        public SessionState                    State
            => (SessionState) Volatile.Read(ref state);

        /// <summary>
        /// Why this rendezvous was closed, or null.
        /// </summary>
        public SessionCloseReason?             CloseReason        { get; private set; }

        /// <summary>
        /// The number of clients that already arrived.
        /// </summary>
        public Int32                           ConnectedClients
            => Volatile.Read(ref connectedCount);

        /// <summary>
        /// The total number of relayed bytes.
        /// </summary>
        public Int64                           BytesRelayed
            => Interlocked.Read(ref bytesRelayed);

        /// <summary>
        /// A task that completes when this rendezvous is closed
        /// and all of its TCP ports are free again.
        /// </summary>
        public Task                            Completion
            => closedSource.Task;

        #endregion

        #region Events

        /// <summary>
        /// An event fired when this rendezvous was closed.
        /// </summary>
        public event Action<RendezvousSession>? OnClosed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new rendezvous.
        /// </summary>
        /// <param name="Id">The unique identification of this rendezvous.</param>
        /// <param name="Endpoints">The already bound endpoints of this rendezvous.</param>
        /// <param name="Profile">The transfer profile of this rendezvous.</param>
        /// <param name="ProfileSettings">The buffer sizes and TCP parameters of this rendezvous.</param>
        /// <param name="CreatedBy">The identifications of the keys that opened this rendezvous.</param>
        /// <param name="Description">An optional description of this rendezvous.</param>
        /// <param name="EchoToSender">Whether a client also receives what it sends itself.</param>
        /// <param name="TimeProvider">A time provider.</param>
        /// <param name="Logger">A logger.</param>
        internal RendezvousSession(Guid                     Id,
                                   SessionEndpoint[]        Endpoints,
                                   TransferProfile          Profile,
                                   TransferProfileSettings  ProfileSettings,
                                   IEnumerable<String>?     CreatedBy,
                                   String?                  Description,
                                   Boolean                  EchoToSender,
                                   TimeProvider             TimeProvider,
                                   ILogger                  Logger)
        {

            this.Id                 = Id;
            this.endpoints          = Endpoints;
            this.Ports              = [.. Endpoints.Select(endpoint => endpoint.Port)];
            this.Profile            = Profile;
            this.ProfileSettings    = ProfileSettings;
            this.createdBy          = CreatedBy is null ? [] : [.. CreatedBy];
            this.Description        = Description;
            this.EchoToSender       = EchoToSender;
            this.timeProvider       = TimeProvider;
            this.logger             = Logger;
            this.CreatedUtc         = TimeProvider.GetUtcNow();
            this.lastActivityTicks  = CreatedUtc.UtcTicks;

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start waiting for the clients of this rendezvous.
        /// </summary>
        internal void Start()
        {
            runTask = RunAsync();
        }

        #endregion

        #region IsOwnedBy(KeyId)

        /// <summary>
        /// Whether the key of the given identification opened this rendezvous.
        /// </summary>
        /// <param name="KeyId">The unique identification of a key.</param>
        public Boolean IsOwnedBy(String KeyId)

            => createdBy.Contains(KeyId, StringComparer.Ordinal);

        #endregion

        #region Authorize(Authorization)

        /// <summary>
        /// Whether the given caller may close this rendezvous.
        ///
        /// A rendezvous belongs to the keys that opened it. Everybody else is
        /// turned away, so that one operator can not tear down the rendezvous of
        /// another one - unless an administrator key says otherwise, or the
        /// caller lives within the same process and is trusted anyway.
        /// </summary>
        /// <param name="Authorization">Who wants to close this rendezvous.</param>
        public Boolean Authorize(ControlAuthorization Authorization)

            => Authorization.IsTrusted       ||
               Authorization.IsAdministrator ||
               Authorization.KeyIds.Any(IsOwnedBy);

        #endregion

        #region Close(Reason)

        /// <summary>
        /// Close this rendezvous: stop all listeners and disconnect all clients.
        /// This method returns immediately, await <see cref="Completion"/> to wait
        /// until all TCP ports are free again.
        /// </summary>
        /// <param name="Reason">Why this rendezvous is closed.</param>
        public void Close(SessionCloseReason Reason)
        {

            if (Interlocked.Exchange(ref closeRequested, 1) != 0)
                return;

            CloseReason = Reason;

            logger.LogDebug("Rendezvous {SessionId}: closing ({Reason})...", Id, Reason);

            try
            {
                lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            { }

            // Unblock everything that might still wait on a socket.
            foreach (var endpoint in endpoints)
            {
                endpoint.StopListening();
                endpoint.CloseClient();
            }

        }

        #endregion

        #region ReportActivity(Bytes)

        /// <summary>
        /// Report relayed payload, which resets the idle timeout.
        /// </summary>
        /// <param name="Bytes">The number of relayed bytes.</param>
        internal void ReportActivity(Int32 Bytes)
        {
            Interlocked.Add     (ref bytesRelayed,      Bytes);
            Interlocked.Exchange(ref lastActivityTicks, timeProvider.GetUtcNow().UtcTicks);
        }

        #endregion


        #region (private) RunAsync()

        /// <summary>
        /// Wait for all clients, relay their data and clean up afterwards.
        /// </summary>
        private async Task RunAsync()
        {

            try
            {

                await Task.WhenAll(endpoints.Select(endpoint => AcceptLoopAsync(endpoint, lifetime.Token))).
                           ConfigureAwait(false);

            }
            catch (Exception e)
            {
                logger.LogError(e, "Rendezvous {SessionId}: unexpected error while waiting for clients!", Id);
            }
            finally
            {

                var relay = Volatile.Read(ref relayTask);

                if (relay is not null)
                {
                    try
                    {
                        await relay.ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        logger.LogDebug(e, "Rendezvous {SessionId}: the relay ended with an error.", Id);
                    }
                }

                foreach (var endpoint in endpoints)
                    endpoint.Dispose();

                Volatile.Write(ref state, (Int32) SessionState.Closed);

                logger.LogInformation("Rendezvous {SessionId} on TCP/[{Ports}] closed ({Reason}), {Bytes} bytes relayed.",
                                      Id, String.Join(", ", Ports), CloseReason ?? SessionCloseReason.ClientDisconnected, BytesRelayed);

                try
                {
                    OnClosed?.Invoke(this);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Rendezvous {SessionId}: an OnClosed event handler failed!", Id);
                }

                lifetime.Dispose();
                closedSource.TrySetResult();

            }

        }

        #endregion

        #region (private) AcceptLoopAsync(Endpoint, CancellationToken)

        /// <summary>
        /// Wait for the client of one endpoint and reject everyone else.
        /// </summary>
        private async Task AcceptLoopAsync(SessionEndpoint    Endpoint,
                                           CancellationToken  CancellationToken)
        {

            while (!CancellationToken.IsCancellationRequested)
            {

                Socket socket;

                try
                {
                    socket = await Endpoint.AcceptAsync(CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException e)
                {

                    if (!CancellationToken.IsCancellationRequested)
                        logger.LogDebug("Rendezvous {SessionId}: the listener on TCP/{Port} stopped ({SocketError}).",
                                        Id, Endpoint.Port, e.SocketErrorCode);

                    return;

                }

                #region Every endpoint serves exactly one client

                if (!Endpoint.TryAdopt(socket))
                {

                    logger.LogWarning("Rendezvous {SessionId}: rejected an additional connection from {RemoteSocket} on TCP/{Port}!",
                                      Id, socket.RemoteEndPoint, Endpoint.Port);

                    socket.ShutdownSafely(SocketShutdown.Both);
                    socket.Dispose();

                    continue;

                }

                #endregion

                socket.ApplyProfile(ProfileSettings, logger);

                var connected = Interlocked.Increment(ref connectedCount);

                logger.LogInformation("Rendezvous {SessionId}: client {RemoteSocket} arrived on TCP/{Port} ({Connected}/{Expected}).",
                                      Id, Endpoint.RemoteSocket, Endpoint.Port, connected, endpoints.Length);

                if (connected == endpoints.Length)
                    OnAllClientsArrived();

            }

        }

        #endregion

        #region (private) OnAllClientsArrived()

        /// <summary>
        /// All clients arrived: start relaying their data.
        /// </summary>
        private void OnAllClientsArrived()
        {

            if (Interlocked.CompareExchange(ref state,
                                            (Int32) SessionState.Established,
                                            (Int32) SessionState.Pending) != (Int32) SessionState.Pending)
            {
                return;
            }

            EstablishedUtc = timeProvider.GetUtcNow();
            Interlocked.Exchange(ref lastActivityTicks, EstablishedUtc.Value.UtcTicks);

            logger.LogInformation("Rendezvous {SessionId} established on TCP/[{Ports}] using the {Profile} profile.",
                                  Id, String.Join(", ", Ports), Profile.AsText());

            Volatile.Write(ref relayTask, RunRelayAsync());

        }

        #endregion

        #region (private) RunRelayAsync()

        /// <summary>
        /// Relay data between the clients: a plain pipe for two clients,
        /// a broadcast for three or more clients.
        ///
        /// Two clients that want to be echoed are relayed by the broadcast as
        /// well - the pipe has no queues it could echo into. They then lose the
        /// half-close propagation of the pipe, which is the right trade: whoever
        /// asks for an echo is having a conversation, not tunneling a protocol.
        /// </summary>
        private async Task RunRelayAsync()
        {

            try
            {

                var sockets = endpoints.Select(endpoint => endpoint.Client!).ToArray();

                if (sockets.Length == 2 && !EchoToSender)
                    await PipeRelay.RunAsync(this,
                                             sockets[0],
                                             sockets[1],
                                             ProfileSettings,
                                             logger,
                                             lifetime.Token).
                                    ConfigureAwait(false);

                else
                    await BroadcastRelay.RunAsync(this,
                                                  Ports,
                                                  sockets,
                                                  ProfileSettings,
                                                  EchoToSender,
                                                  logger,
                                                  lifetime.Token).
                                         ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                logger.LogError(e, "Rendezvous {SessionId}: the relay failed!", Id);
            }
            finally
            {
                // Either all clients are gone, or the rendezvous was closed
                // for another reason - which keeps the original reason.
                Close(SessionCloseReason.ClientDisconnected);
            }

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Close this rendezvous and wait until all of its TCP ports are free again.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            Close(SessionCloseReason.ServiceShutdown);

            var run = Volatile.Read(ref runTask);

            if (run is not null)
            {
                try
                {
                    await run.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogDebug(e, "Rendezvous {SessionId}: shutdown error.", Id);
                }
            }

            else
            {

                foreach (var endpoint in endpoints)
                    endpoint.Dispose();

                Volatile.Write(ref state, (Int32) SessionState.Closed);
                closedSource.TrySetResult();

            }

        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a text representation of this rendezvous.
        /// </summary>
        public override String ToString()

            => $"{Id}: TCP/[{String.Join(", ", Ports)}], {Profile.AsText()}, {State}" +
               (EchoToSender ? ", echoing" : "") +
               (createdBy.Length > 0 ? $", opened by {String.Join(", ", createdBy)}" : "") +
               (Description is not null ? $": {Description}" : "");

        #endregion

    }

}
