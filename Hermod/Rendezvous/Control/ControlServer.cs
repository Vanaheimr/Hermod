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

using System.Collections.Concurrent;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The control endpoint of the rendezvous service: a TCP listener accepting
    /// UTF-8 commands, one command per line.
    ///
    /// This is a plain object with Start() and StopAsync(), not a hosted service.
    /// Whoever wants it within a generic host wraps it into an IHostedService of
    /// ten lines - the other way around a library would force a hosting framework
    /// onto everybody who just wants a control endpoint.
    /// </summary>
    public sealed class ControlServer : IAsyncDisposable
    {

        #region Data

        private readonly RendezvousManager                   manager;
        private readonly RendezvousOptions                   options;
        private readonly ILoggerFactory                      loggerFactory;
        private readonly ILogger<ControlServer>              logger;
        private readonly ConcurrentDictionary<Int64, Task>   connections   = [];
        private readonly CancellationTokenSource             stopSource    = new();
        private readonly NonceCache                          nonceCache    = new();
        private readonly ControlSigner                       responseSigner;
        private readonly TimeProvider                        timeProvider;

        private          TcpListener?                        listener;
        private          Task?                               acceptTask;
        private          Int32                               connectionCount;
        private          Int64                               connectionCounter;

        #endregion

        #region Properties

        /// <summary>
        /// The TCP socket this control endpoint is listening on.
        /// Only available after <see cref="Start"/> was called.
        /// </summary>
        public IPSocket? LocalSocket
            => IPSocket.FromIPEndPoint(listener?.LocalEndpoint);

        /// <summary>
        /// Whether this control endpoint is accepting connections.
        /// </summary>
        public Boolean IsRunning
            => acceptTask is not null && !stopSource.IsCancellationRequested;

        /// <summary>
        /// The number of currently open control connections.
        /// </summary>
        public Int32 OpenConnections
            => Volatile.Read(ref connectionCount);

        /// <summary>
        /// The public keys that may authorize control commands.
        /// Without at least one valid key this endpoint rejects everything,
        /// which is the entire point: the TCP port is open to the world.
        /// </summary>
        public ControlKeyRing  Keys              { get; }

        /// <summary>
        /// The public key this endpoint signs its responses with, so that a
        /// client can tell a real answer from an injected one.
        /// </summary>
        public ControlKey      ResponseKey
            => responseSigner.ToControlKey(Description: "The response key of this control endpoint");

        /// <summary>
        /// The nonces of recently accepted requests.
        /// </summary>
        public NonceCache      Nonces
            => nonceCache;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control endpoint.
        /// </summary>
        /// <param name="Manager">The rendezvous manager to control.</param>
        /// <param name="Options">An optional configuration, defaults are used otherwise.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        /// <param name="ResponseSigner">The key to sign the responses with. A fresh Ed25519 key is generated otherwise, and its public key is written to the log.</param>
        /// <param name="Keys">An optional key ring, an empty one is created otherwise.</param>
        /// <param name="TimeProvider">An optional time provider, e.g. for tests.</param>
        public ControlServer(RendezvousManager   Manager,
                             RendezvousOptions?  Options          = null,
                             ILoggerFactory?     LoggerFactory    = null,
                             ControlSigner?      ResponseSigner   = null,
                             ControlKeyRing?     Keys             = null,
                             TimeProvider?       TimeProvider     = null)
        {

            ArgumentNullException.ThrowIfNull(Manager);

            this.manager         = Manager;
            this.options         = Options        ?? new RendezvousOptions();
            this.loggerFactory   = LoggerFactory  ?? NullLoggerFactory.Instance;
            this.logger          = this.loggerFactory.CreateLogger<ControlServer>();
            this.Keys            = Keys           ?? new ControlKeyRing();
            this.timeProvider    = TimeProvider   ?? System.TimeProvider.System;
            this.responseSigner  = ResponseSigner ?? ControlSigner.GenerateEd25519("rendezvous-control-endpoint");

            if (ResponseSigner is null)
                logger.LogInformation(
                    "The control endpoint signs its responses with an ephemeral Ed25519 key: {PublicKey} " +
                    "- configure a persistent key, otherwise clients can not pin it across restarts.",
                    Convert.ToBase64String(this.responseSigner.PublicKey)
                );

        }

        #endregion


        #region Start()

        /// <summary>
        /// Bind the control endpoint and start accepting connections.
        /// The TCP port is bound synchronously, so that <see cref="LocalSocket"/>
        /// is known - and a port conflict is reported - as soon as this returns.
        /// </summary>
        /// <exception cref="SocketException">When the TCP port could not be bound.</exception>
        public void Start()
        {

            if (acceptTask is not null)
                return;

            var tcpListener = new TcpListener(options.ControlIPAddress.ToDotNet(),
                                              options.ControlPort.ToUInt16());

            // Do not allow other processes to bind the same port via SO_REUSEADDR.
            if (OperatingSystem.IsWindows())
                tcpListener.ExclusiveAddressUse = true;

            tcpListener.Start();

            listener    = tcpListener;
            acceptTask  = AcceptLoopAsync(tcpListener, stopSource.Token);

            logger.LogInformation("The rendezvous control endpoint is listening on {LocalSocket}.", LocalSocket);

        }

        #endregion

        #region StopAsync()

        /// <summary>
        /// Stop accepting new connections and wait for the open ones.
        /// </summary>
        public async Task StopAsync()
        {

            if (acceptTask is null)
                return;

            listener?.Stop();

            try
            {
                await stopSource.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            { }

            try
            {
                await acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }

            try
            {
                await Task.WhenAll(connections.Values).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "A control connection did not shut down cleanly.");
            }

            listener?.Dispose();

            logger.LogInformation("The rendezvous control endpoint was stopped.");

        }

        #endregion


        #region (private) AcceptLoopAsync(Listener, CancellationToken)

        /// <summary>
        /// Accept control connections until this control endpoint is stopped.
        /// </summary>
        private async Task AcceptLoopAsync(TcpListener        Listener,
                                           CancellationToken  CancellationToken)
        {

            // Do not run the accept loop within Start().
            await Task.Yield();

            while (!CancellationToken.IsCancellationRequested)
            {

                Socket socket;

                try
                {
                    socket = await Listener.AcceptSocketAsync(CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e)
                {

                    if (!CancellationToken.IsCancellationRequested)
                        logger.LogError("The control endpoint stopped accepting connections ({SocketError})!", e.SocketErrorCode);

                    break;

                }

                #region Do not accept an unlimited number of control connections

                if (Interlocked.Increment(ref connectionCount) > options.MaxControlConnections)
                {

                    Interlocked.Decrement(ref connectionCount);

                    logger.LogWarning("Rejected the control connection from {RemoteSocket}: more than {MaxControlConnections} open control connections!",
                                      socket.RemoteEndPoint, options.MaxControlConnections);

                    socket.ShutdownSafely(SocketShutdown.Both);
                    socket.Dispose();

                    continue;

                }

                #endregion

                var connectionId = Interlocked.Increment(ref connectionCounter);

                connections[connectionId] = HandleConnectionAsync(connectionId, socket, CancellationToken);

            }

        }

        #endregion

        #region (private) HandleConnectionAsync(ConnectionId, Socket, CancellationToken)

        /// <summary>
        /// Serve one control connection.
        /// </summary>
        private async Task HandleConnectionAsync(Int64              ConnectionId,
                                                 Socket             Socket,
                                                 CancellationToken  CancellationToken)
        {

            // Do not block the accept loop.
            await Task.Yield();

            try
            {

                var connection = new ControlConnection(Socket,
                                                       manager,
                                                       options,
                                                       Keys,
                                                       nonceCache,
                                                       responseSigner,
                                                       timeProvider,
                                                       loggerFactory.CreateLogger<ControlConnection>());

                await connection.RunAsync(CancellationToken).ConfigureAwait(false);

            }
            catch (Exception e)
            {
                logger.LogError(e, "A control connection failed!");
            }
            finally
            {

                Socket.Dispose();

                Interlocked.Decrement(ref connectionCount);
                connections.TryRemove(ConnectionId, out _);

            }

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop this control endpoint.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            await StopAsync().ConfigureAwait(false);

            stopSource.Dispose();

        }

        #endregion

    }

}
