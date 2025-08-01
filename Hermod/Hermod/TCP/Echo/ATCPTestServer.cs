﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Styx;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    // DotNetty
    // https://github.com/chronoxor/NetCoreServer

    public delegate Task TCPEchoLoggingDelegate(String Message);

    /// <summary>
    /// An abstract TCP server.
    /// </summary>
    public abstract class ATCPTestServer : ITCPServer,
                                           IDisposable,
                                           IAsyncDisposable
    {

        #region Data

        public static readonly TimeSpan                               DefaultReceiveTimeout         = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan                               DefaultSendTimeout            = TimeSpan.FromSeconds(30);
        public const           UInt32                                 DefaultMaxClientConnections   = 1024;

        private readonly       TcpListener?                           tcpListenerIPv6;
        private readonly       TcpListener?                           tcpListenerIPv4;
        private readonly       TCPEchoLoggingDelegate?                loggingHandler;
        private readonly       CancellationTokenSource                cts;
        private                Task?                                  serverTask;

        private readonly       ConcurrentDictionary<TcpClient, Task>  activeClients                 = [];

        #endregion

        #region Properties

        /// <summary>
        /// The IP address this TCP EchoTest server is listening on.
        /// </summary>
        public IIPAddress  IPAddress         { get; }

        /// <summary>
        /// The TCP port this TCP EchoTest server is listening on.
        /// </summary>
        public IPPort      TCPPort           { get; }

        /// <summary>
        /// The IP socket this TCP EchoTest server is listening on.
        /// </summary>
        public IPSocket    IPSocket          { get; }

        /// <summary>
        /// The receive timeout for the TCP stream.
        /// </summary>
        public TimeSpan    ReceiveTimeout    { get; }

        /// <summary>
        /// The send timeout for the TCP stream.
        /// </summary>
        public TimeSpan    SendTimeout       { get; }


        public ServerCertificateSelectorDelegate?                        ServerCertificateSelector     { get; }
        public RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator    { get; }
        public LocalCertificateSelectionHandler?                         LocalCertificateSelector      { get; }
        public SslProtocols?                                             AllowedTLSProtocols           { get; }
        public Boolean                                                   ClientCertificateRequired     { get; }
        public Boolean                                                   CheckCertificateRevocation    { get; }


        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder               ConnectionIdBuilder                    { get; }

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan                          ConnectionTimeout                      { get; set; }

        /// <summary>
        /// The maximum number of concurrent TCP client connections.
        /// </summary>
        public UInt32                            MaxClientConnections                   { get; }


        public Boolean                           IsRunning
            => serverTask is not null && !serverTask.IsCompleted && !cts.IsCancellationRequested;


        /// <summary>
        /// The number of currently connected clients.
        /// </summary>
        public UInt32                            NumberOfConnectedClients
            => (UInt32) activeClients.Count;

        /// <summary>
        /// Return an enumeration of sockets of all currently connected clients.
        /// </summary>
        public IEnumerable<IPSocket>             ClientSockets
            => activeClients.Keys.Select(static client => IPSocket.FromIPEndPoint((client.Client.RemoteEndPoint as IPEndPoint)!));

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP EchoTest server started.
        /// </summary>
        public event TCPServerStartedDelegate?        OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was rejected.
        /// </summary>
        public event NewConnectionRejectedDelegate?   NewConnectionRejected;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NewConnectionDelegate?           OnNewConnection;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedDelegate?        OnConnectionClosed;

        /// <summary>
        /// An event fired whenever the TCP EchoTest server stopped.
        /// </summary>
        public event TCPServerStoppedDelegate?        OnStopped;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract TCP server that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information. If null, the default connection identification will be used.</param>
        /// <param name="MaxClientConnections">An optional maximum number of concurrent TCP client connections. If null, the default maximum number of concurrent TCP client connections will be used.</param>
        /// 
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="Exception"></exception>
        public ATCPTestServer(IIPAddress?                                               IPAddress                    = null,
                              IPPort?                                                   TCPPort                      = null,
                              TimeSpan?                                                 ReceiveTimeout               = null,
                              TimeSpan?                                                 SendTimeout                  = null,
                              TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                              ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                              RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                              LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                              SslProtocols?                                             AllowedTLSProtocols          = null,
                              Boolean?                                                  ClientCertificateRequired    = null,
                              Boolean?                                                  CheckCertificateRevocation   = null,

                              ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                              UInt32?                                                   MaxClientConnections         = null)

        {

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.IPAddress                   = IPAddress ?? IPvXAddress.Localhost; // IPv4/6 localhost
            this.TCPPort                     = TCPPort   ?? IPPort.Zero;           // Random TCP port selection
            this.MaxClientConnections        = MaxClientConnections ?? DefaultMaxClientConnections;

            if (this.IPAddress.IsIPv6)
            {

                this.tcpListenerIPv6         = new TcpListener(this.IPAddress.ToDotNet(), this.TCPPort.ToUInt16());

                // Dual mode on ANY address!
                if (this.IPAddress.IsAny && this.IPAddress.IsIPv4)
                    tcpListenerIPv6.Server.DualMode = true;

                if (this.TCPPort.IsZero)
                    tcpListenerIPv6.Start((Int32) this.MaxClientConnections);

                // When the TCP port was == 0, then IPv6 will choose a random port!
                var localEndpoint            = tcpListenerIPv6?.LocalEndpoint as IPEndPoint ?? throw new Exception("The TCP listener's local endpoint is not an IPEndPoint!");
                this.TCPPort                 = IPPort.Parse(localEndpoint.Port);

            }

            // Listening on ::1 and 127.0.0.1 at the same time will need two sockets!
            // When port == 0, then IPv6 will choose a random port, and IPv4 will try to bind to the same port!
            if (this.IPAddress.IsIPv4 && this.IPAddress.IsLocalhost)
            {

                this.tcpListenerIPv4         = new TcpListener(IPv4Address.Localhost, this.TCPPort.ToUInt16());

                // When the TCP port is still == 0, then IPv4 will choose a random port!
                if (this.TCPPort.IsZero)
                {

                    if (this.TCPPort.IsZero)
                        tcpListenerIPv4.Start((Int32) this.MaxClientConnections);

                    var localEndpoint        = tcpListenerIPv4?.LocalEndpoint as IPEndPoint ?? throw new Exception("The TCP listener's local endpoint is not an IPEndPoint!");
                    this.TCPPort             = IPPort.Parse(localEndpoint.Port);

                }

            }

            this.IPSocket                    = new IPSocket(
                                                   this.IPAddress,
                                                   this.TCPPort
                                               );
            this.ReceiveTimeout              = ReceiveTimeout       ?? DefaultReceiveTimeout;
            this.SendTimeout                 = SendTimeout          ?? DefaultSendTimeout;
            this.ConnectionIdBuilder         = ConnectionIdBuilder  ?? ((sender, timestamp, localSocket, remoteSocket) => $"{remoteSocket} -> {localSocket}");
            this.loggingHandler              = LoggingHandler;
            this.cts                         = new CancellationTokenSource();

            this.ServerCertificateSelector   = ServerCertificateSelector;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.AllowedTLSProtocols         = AllowedTLSProtocols;
            this.ClientCertificateRequired   = ClientCertificateRequired  ?? false;
            this.CheckCertificateRevocation  = CheckCertificateRevocation ?? false;

        }

        #endregion


        #region (private) Log(Message)

        private Task Log(String Message)
        {

            if (loggingHandler is not null)
            {
                try
                {
                    return loggingHandler(Message);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error in logging handler: {e.Message}");
                }
            }

            return Task.CompletedTask;

        }

        #endregion


        #region Start     (EventTrackingId = null)

        /// <summary>
        /// Start the ATCPTestServer and begin accepting incoming TCP connections.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public async Task Start(EventTracking_Id? EventTrackingId = null)
        {

            var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

            try
            {

                if (IPAddress.IsIPv6)
                {
                    if (tcpListenerIPv6 is null)
                        throw new InvalidOperationException("Cannot start TCP Server on IPv6, because the IPv6 listener is null!");
                    tcpListenerIPv6.Start((Int32) MaxClientConnections);
                }

                if (IPAddress.IsIPv4 && !IPAddress.IsAny)
                {
                    if (tcpListenerIPv4 is null)
                        throw new InvalidOperationException("Cannot start TCP Server on IPv4, because the IPv4 listener is null!");
                    tcpListenerIPv4.Start((Int32) MaxClientConnections);
                }

                serverTask = Task.Factory.StartNew(async () => {

                    var ipAddress = IPAddress.ToString();

                    if (IPAddress.IsLocalhost)
                    {

                        if (IPAddress.IsIPv4 && !IPAddress.IsIPv6)
                            ipAddress = "127.0.0.1";

                        if (IPAddress.IsIPv6 && !IPAddress.IsIPv4)
                            ipAddress = "[::1]";

                    }

                    await LogEvent(
                        OnStarted,
                        loggingDelegate => loggingDelegate.Invoke(
                            this,
                            DateTimeOffset.UtcNow,
                            eventTrackingId,
                            $"TCP Server started on {ipAddress}:{TCPPort} with ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout}!"
                        )
                    );

                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {

                            var listenerTasks = new List<Task<TcpClient>>();

                            if (IPAddress.IsIPv6 && tcpListenerIPv6 is not null)
                                listenerTasks.Add(tcpListenerIPv6.AcceptTcpClientAsync(cts.Token).AsTask());

                            if (IPAddress.IsIPv4 && tcpListenerIPv4 is not null)
                                listenerTasks.Add(tcpListenerIPv4.AcceptTcpClientAsync(cts.Token).AsTask());

                            var clientTasks = await Task.WhenAny(listenerTasks);
                            var client      = await clientTasks;

                            #region Check MaxClientConnections

                            if (activeClients.Count >= MaxClientConnections)
                            {

                                var remoteIPEndPoint  = (client.Client.RemoteEndPoint as IPEndPoint)!;
                                var remoteSocket      = IPSocket.FromIPEndPoint(remoteIPEndPoint);

                                // This will/might cause a TCP RST (Reset) packet being send!
                                client.LingerState = new LingerOption(false, 0);

                                client.Close();

                                await LogEvent(
                                          NewConnectionRejected,
                                          loggingDelegate => loggingDelegate.Invoke(
                                              this,
                                              DateTimeOffset.UtcNow,
                                              eventTrackingId,
                                              remoteSocket,
                                              ConnectionIdBuilder(this, DateTimeOffset.UtcNow, IPSocket, remoteSocket),
                                              I18NString.Create($"Rejected new client connection due to maximum of {MaxClientConnections} concurrent connections reached!")
                                          )
                                      );

                                continue;

                            }

                            #endregion

                            activeClients.TryAdd(
                                client,
                                Task.CompletedTask
                            );

                            var clientTask = HandleNewTCPClientAsync(client);

                            if (!activeClients.TryUpdate(client, clientTask, Task.CompletedTask))
                            {
                                activeClients.TryRemove(client, out _);
                                activeClients.TryAdd   (client, clientTask);
                            }

                        }
                        catch (OperationCanceledException) {
                            // Graceful exit on cancel
                        }
                        catch (ObjectDisposedException) {
                            // Expected when listener is stopped
                        }
                        catch (SocketException se) {
                            await Log($"Socket error accepting client: {se.Message}");
                        }
                        catch (Exception e)
                        {
                            await Log($"Error accepting client: {e.Message}");
                        }
                    }

                },
                cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            }
            catch (Exception ex)
            {
                await Log($"Error starting ATCPTestServer: {ex.Message}");
                throw;
            }

        }

        #endregion

        #region (private) HandleNewTCPClientAsync(client)

        private async Task HandleNewTCPClientAsync(TcpClient client)
        {

            TCPConnection? tcpConnection = null;

            var eventTrackingId2  = EventTracking_Id.New;
            var remoteIPEndPoint  = (client.Client.RemoteEndPoint as IPEndPoint)!;
            var remoteSocket      = IPSocket.FromIPEndPoint(remoteIPEndPoint);
            var connectionId      = "";

            try
            {

                client.ReceiveTimeout  = (Int32) ReceiveTimeout.TotalMilliseconds;
                client.SendTimeout     = (Int32) SendTimeout.   TotalMilliseconds;
                client.LingerState     = new LingerOption(true, 1);

                #region Validate connection

                var status = await ValidateConnection(
                                       Timestamp.Now,
                                       this,
                                       client,
                                       eventTrackingId2,
                                       cts.Token
                                   );

                #endregion

                #region Rejected connection...

                if (status.Result == ConnectionFilterResult.Rejected)
                {

                    // This will/might cause a TCP RST (Reset) packet being send!
                    client.LingerState = new LingerOption(false, 0);

                    await LogEvent(
                              NewConnectionRejected,
                              loggingDelegate => loggingDelegate.Invoke(
                                  this,
                                  DateTimeOffset.UtcNow,
                                  eventTrackingId2,
                                  remoteSocket,
                                  connectionId,
                                  status.Reason
                              )
                          );

                }

                #endregion

                else
                {

                    tcpConnection  = new TCPConnection(
                                         TCPServer:                   this,
                                         TCPClient:                   client,
                                         ServerCertificateSelector:   ServerCertificateSelector,
                                         ClientCertificateValidator:  ClientCertificateValidator,
                                         LocalCertificateSelector:    LocalCertificateSelector,
                                         AllowedTLSProtocols:         AllowedTLSProtocols,
                                         ReadTimeout:                 ReceiveTimeout,
                                         WriteTimeout:                SendTimeout
                                     );

                    connectionId   = tcpConnection.ConnectionId;

                    await LogEvent(
                              OnNewConnection,
                              loggingDelegate => loggingDelegate.Invoke(
                                  this,
                                  DateTimeOffset.UtcNow,
                                  eventTrackingId2,
                                  remoteSocket,
                                  connectionId,
                                  tcpConnection
                              )
                          );

                    await HandleConnection(tcpConnection, cts.Token).ConfigureAwait(false);

                }

                try
                {
                    // Graceful shutdown after echo completes (client EOF received)
                    client.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                { }

            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (IOException ex)
            {
                // Connection closed or reset
                await Log($"Echo stream closed: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Stream closed on other side
            }
            catch (Exception ex)
            {
                await Log($"Unhandled error in client handler: {ex.Message}");
            }
            finally
            {

                try
                {
                    client.Close();
                }
                catch (Exception)
                { }

                activeClients.TryRemove(client, out _);

                await LogEvent(
                          OnConnectionClosed,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              DateTimeOffset.UtcNow,
                              eventTrackingId2,
                              remoteSocket,
                              connectionId,
                              ConnectionClosedBy.Client
                          )
                      );

            }
        }

        #endregion

        #region (virtual) ValidateConnection(Timestamp, Server, Connection, EventTrackingId, CancellationToken)

        /// <summary>
        /// Validate a new TCP connection. Maybe filter it based on remote IP address, port, etc.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the validation.</param>
        /// <param name="Server">The TCP server that is validating the connection.</param>
        /// <param name="Connection">The TCP connection to validate.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token to cancel the validation.</param>
        public virtual Task<ConnectionFilterResponse> ValidateConnection(DateTime           Timestamp,
                                                                         ITCPServer         Server,
                                                                         TcpClient          Connection,
                                                                         EventTracking_Id   EventTrackingId,
                                                                         CancellationToken  CancellationToken)
        {
            return Task.FromResult(ConnectionFilterResponse.Accepted());
        }

        #endregion


        protected abstract Task HandleConnection(TCPConnection      Connection,
                                                 CancellationToken  Token);


        #region Stop (EventTrackingId = null)

        /// <summary>
        /// Stop the ATCPTestServer and close all active client connections.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public async Task Stop(EventTracking_Id? EventTrackingId = null)
        {

            var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

            foreach (var client in activeClients.Keys)
            {
                try
                {
                    if (activeClients.TryRemove(client, out var task))
                    {

                        client.Close();

                        // Wait for client task to complete
                        await task;

                    }
                }
                catch (Exception ex)
                {
                    await Log($"Error stopping client: {ex.Message}");
                }
            }

            cts.Cancel();
            tcpListenerIPv6?.Stop();
            tcpListenerIPv4?.Stop();

            if (serverTask is not null)
                await serverTask;

            await LogEvent(
                      OnStopped,
                      loggingDelegate => loggingDelegate.Invoke(
                          this,
                          DateTimeOffset.UtcNow,
                          eventTrackingId,
                          $"Server on {IPAddress}:{TCPPort} stopped!"
                      )
                  );

        }

        #endregion


        #region (protected internal) SendConnectionClosed(ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        /// <summary>
        /// Send a "connection closed" event.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="RemoteSocket">The remote socket that was closed.</param>
        /// <param name="ConnectionId">The internal connection identification.</param>
        /// <param name="ClosedBy">Whether it was closed by us or by the client.</param>
        public async Task SendConnectionClosed(DateTimeOffset      ServerTimestamp,
                                               EventTracking_Id    EventTrackingId,
                                               IPSocket            RemoteSocket,
                                               String              ConnectionId,
                                               ConnectionClosedBy  ClosedBy)
        {

            await LogEvent(
                          OnConnectionClosed,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              DateTimeOffset.UtcNow,
                              EventTrackingId,
                              RemoteSocket,
                              ConnectionId,
                              ClosedBy
                          )
                      );

        }

        #endregion



        #region (protected) LogEvent     (Module, Logger, LogHandler, ...)

        protected async Task LogEvent<TDelegate>(String                                             Module,
                                                 TDelegate?                                         Logger,
                                                 Func<TDelegate, Task>                              LogHandler,
                                                 [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                                 [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

        {
            if (Logger is not null)
            {
                try
                {

                    await Task.WhenAll(
                              Logger.GetInvocationList().
                                     OfType<TDelegate>().
                                     Select(LogHandler)
                          ).ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    await HandleErrors(Module, $"{Command}.{EventName}", e);
                }
            }
        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ErrorResponse)

        public virtual Task HandleErrors(String  Module,
                                         String  Caller,
                                         String  ErrorResponse)
        {

            DebugX.Log($"{Module}.{Caller}: {ErrorResponse}");

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ExceptionOccurred)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccurred)
        {

            DebugX.LogException(ExceptionOccurred, $"{Module}.{Caller}");

            return Task.CompletedTask;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATCPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(ATCPTestServer)}: {IPAddress}:{TCPPort} (ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

        #region Dispose/Async()

        public async ValueTask DisposeAsync()
        {
            await Stop();
            cts.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
