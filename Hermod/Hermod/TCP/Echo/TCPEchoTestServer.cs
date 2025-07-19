/*
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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Styx;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using System.Security.Authentication;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public delegate Task TCPEchoServerStartedDelegate(TCPEchoTestServer Sender, DateTimeOffset Timestamp, EventTracking_Id EventTrackingId, String? Message = null);
    public delegate Task TCPEchoServerStoppedDelegate(TCPEchoTestServer Sender, DateTimeOffset Timestamp, EventTracking_Id EventTrackingId, String? Message = null);

    ///// <summary>
    ///// New connection delegate.
    ///// </summary>
    ///// <param name="TCPServer">The sender of this event.</param>
    ///// <param name="Timestamp">The timestamp of the new TCP connection event.</param>
    ///// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
    ///// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    ///// <param name="ConnectionId">The identification of this connection.</param>
    ///// <param name="TCPConnection">The new TCP connection.</param>
    //public delegate Task TCPEchoServerNewConnectionDelegate(TCPEchoTestServer  TCPServer,
    //                                                        DateTimeOffset     Timestamp,
    //                                                        EventTracking_Id   EventTrackingId,
    //                                                        IPSocket           RemoteSocket,
    //                                                        String             ConnectionId,
    //                                                        TCPConnection      TCPConnection);

    ///// <summary>
    ///// Connection closed delegate.
    ///// </summary>
    ///// <param name="TCPServer">The sender of this event.</param>
    ///// <param name="Timestamp">The timestamp of the event.</param>
    ///// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
    ///// <param name="RemoteSocket">The remote TCP/IP socket.</param>
    ///// <param name="ConnectionId">The identification of this connection.</param>
    ///// <param name="ClosedBy">Whether the connection was closed by the client or the server.</param>
    //public delegate Task TCPEchoServerConnectionClosedDelegate(TCPEchoTestServer   TCPServer,
    //                                                           DateTimeOffset      Timestamp,
    //                                                           EventTracking_Id    EventTrackingId,
    //                                                           IPSocket            RemoteSocket,
    //                                                           String              ConnectionId,
    //                                                           ConnectionClosedBy  ClosedBy);



    public delegate Task TCPEchoLoggingDelegate(String Message);

    /// <summary>
    /// A simple echo test server that listens for incoming TCP echo connections.
    /// </summary>
    public class TCPEchoTestServer : ITCPServer,
                                     IDisposable,
                                     IAsyncDisposable
    {

        #region Data

        public const           Int32                                  DefaultBufferSize      = 81920; // .NET default for CopyToAsync!
        public static readonly TimeSpan                               DefaultReceiveTimeout  = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan                               DefaultSendTimeout     = TimeSpan.FromSeconds(5);

        private readonly       TcpListener                            tcpListener;
        private readonly       ConnectionIdBuilder                    connectionIdBuilder;
        private readonly       TCPEchoLoggingDelegate?                loggingHandler;
        private readonly       CancellationTokenSource                cts;
        private                Task?                                  serverTask;

        private readonly       ConcurrentDictionary<TcpClient, Task>  activeClients          = [];

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
        /// The buffer size for the TCP stream.
        /// </summary>
        public UInt32      BufferSize        { get; }

        /// <summary>
        /// The receive timeout for the TCP stream.
        /// </summary>
        public TimeSpan    ReceiveTimeout    { get; }

        /// <summary>
        /// The send timeout for the TCP stream.
        /// </summary>
        public TimeSpan    SendTimeout       { get; }


        public ServerCertificateSelectorDelegate?                       ServerCertificateSelector     { get; }
        public RemoteTLSClientCertificateValidationHandler<TCPServer>?  ClientCertificateValidator    { get; }
        public LocalCertificateSelectionHandler?                        LocalCertificateSelector      { get; }
        public SslProtocols?                                            AllowedTLSProtocols           { get; }
        public Boolean                                                  ClientCertificateRequired     { get; }
        public Boolean                                                  CheckCertificateRevocation    { get; }


        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder               ConnectionIdBuilder                    { get; }

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan                          ConnectionTimeout                      { get; set; }

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32                            MaxClientConnections                   { get; set; }


        public Boolean                           IsRunning
            => serverTask is not null && !serverTask.IsCompleted && !cts.IsCancellationRequested;


        /// <summary>
        /// The number of currently connected clients.
        /// </summary>
        public UInt32      NumberOfConnectedClients
            => (UInt32) activeClients.Count;

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP EchoTest server started.
        /// </summary>
        public event TCPEchoServerStartedDelegate?   OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NewConnectionDelegate?          OnNewConnection;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedDelegate?       OnConnectionClosed;

        /// <summary>
        /// An event fired whenever the TCP EchoTest server stopped.
        /// </summary>
        public event TCPEchoServerStoppedDelegate?   OnStopped;

        #endregion

        #region Constructor(s)

        #region (private) EchoTestServer (TCPPort, IPAddress = null, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        private TCPEchoTestServer(IPPort                                                   TCPPort,
                                  IIPAddress?                                              IPAddress                    = null,
                                  UInt32?                                                  BufferSize                   = null,
                                  TimeSpan?                                                ReceiveTimeout               = null,
                                  TimeSpan?                                                SendTimeout                  = null,
                                  TCPEchoLoggingDelegate?                                  LoggingHandler               = null,

                                  ServerCertificateSelectorDelegate?                       ServerCertificateSelector    = null,
                                  RemoteTLSClientCertificateValidationHandler<TCPServer>?  ClientCertificateValidator   = null,
                                  LocalCertificateSelectionHandler?                        LocalCertificateSelector     = null,
                                  SslProtocols?                                            AllowedTLSProtocols          = null,
                                  Boolean?                                                 ClientCertificateRequired    = null,
                                  Boolean?                                                 CheckCertificateRevocation   = null,

                                  ConnectionIdBuilder?                                     ConnectionIdBuilder          = null,
                                  TimeSpan?                                                ConnectionTimeout            = null,
                                  UInt32?                                                  MaxClientConnections         = null)

        {

            if (ReceiveTimeout.HasValue && ReceiveTimeout.Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "Timeout too large for socket.");

            if (SendTimeout.   HasValue && SendTimeout.   Value.TotalMilliseconds > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout),    "Timeout too large for socket.");

            this.IPAddress                   = IPAddress ?? IPv6Address.Localhost;
            this.tcpListener                 = new TcpListener(this.IPAddress.ToDotNet(), TCPPort.ToUInt16());
            var localEndpoint                = tcpListener.LocalEndpoint as IPEndPoint ?? throw new Exception("The TCP listener's local endpoint is not an IPEndPoint!");
            this.TCPPort                     = IPPort.Parse(localEndpoint.Port);
            this.IPSocket                    = new IPSocket(
                                                   this.IPAddress,
                                                   this.TCPPort
                                               );
            this.BufferSize                  = BufferSize.HasValue
                                                   ? BufferSize.Value > Int32.MaxValue
                                                         ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                                         : (UInt32) BufferSize.Value
                                                   : DefaultBufferSize;
            this.ReceiveTimeout              = ReceiveTimeout      ?? DefaultReceiveTimeout;
            this.SendTimeout                 = SendTimeout         ?? DefaultSendTimeout;
            this.connectionIdBuilder         = ConnectionIdBuilder ?? ((sender, timestamp, localSocket, remoteSocket) => $"{remoteSocket} -> {localSocket}");
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


        #region EchoTestServer (         TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public TCPEchoTestServer(IPPort                   TCPPort,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : this(TCPPort,
                   IPv6Address.Localhost,
                   BufferSize,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        { }

        #endregion

        #region EchoTestServer (Address, TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public TCPEchoTestServer(IIPAddress               IPAddress,
                                 IPPort                   TCPPort,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : this(TCPPort,
                   IPAddress,
                   BufferSize,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        { }

        #endregion

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
                          );

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
                   nameof(TCPEchoTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region StartNew (         TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Start a new EchoTestServer that listens on the loopback address and the given TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public static async Task<TCPEchoTestServer>

            StartNew(IPPort                   TCPPort,
                     UInt32?                  BufferSize       = null,
                     TimeSpan?                ReceiveTimeout   = null,
                     TimeSpan?                SendTimeout      = null,
                     TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new TCPEchoTestServer(
                             TCPPort,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            await server.Start();

            return server;

        }

        #endregion

        #region StartNew (Address, TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Start a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public static async Task<TCPEchoTestServer>

            StartNew(IIPAddress               IPAddress,
                     IPPort                   TCPPort,
                     UInt32?                  BufferSize       = null,
                     TimeSpan?                ReceiveTimeout   = null,
                     TimeSpan?                SendTimeout      = null,
                     TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new TCPEchoTestServer(
                             IPAddress,
                             TCPPort,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            await server.Start();

            return server;

        }

        #endregion

        #region Start (EventTrackingId = null)

        /// <summary>
        /// Start the EchoTestServer and begin accepting incoming TCP connections.
        /// </summary>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public async Task Start(EventTracking_Id? EventTrackingId = null)
        {

            var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

            try
            {

                tcpListener.Start();

                serverTask = Task.Factory.StartNew(async () => {

                    await LogEvent(
                        OnStarted,
                        loggingDelegate => loggingDelegate.Invoke(
                            this,
                            DateTimeOffset.UtcNow,
                            eventTrackingId,
                            $"Server started on {IPAddress}:{TCPPort} with BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout}!"
                        )
                    );

                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {

                            var client     = await tcpListener.AcceptTcpClientAsync(cts.Token);

                            var clientTask = Task.Run(async () => {
                                                 try
                                                 {
                                                     await HandleClientAsync(client);
                                                 }
                                                 catch (Exception ex)
                                                 {
                                                     await Log($"Unhandled error in client handler wrapper: {ex.Message}");
                                                 }
                                                 finally
                                                 {
                                                     activeClients.TryRemove(client, out _);
                                                     client.Close();
                                                 }
                                             }, cts.Token);

                            activeClients.TryAdd(client, clientTask);

                        }
                        catch (OperationCanceledException) {
                            // Graceful exit on cancel
                        }
                        catch (ObjectDisposedException) {
                            // Expected when listener is stopped
                        }
                        catch (SocketException) {
                            // Expected during shutdown
                        }
                        catch (Exception ex)
                        {
                            await Log($"Error accepting client: {ex.Message}");
                        }
                    }

                },
                cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            }
            catch (Exception ex)
            {
                await Log($"Error starting EchoTestServer: {ex.Message}");
                throw;
            }

        }

        #endregion

        #region (private) HandleClientAsync(client)

        private async Task HandleClientAsync(TcpClient client)
        {

            TCPConnection? tcpConnection = null;

            var eventTrackingId2  = EventTracking_Id.New;
            var remoteIPEndPoint  = (client.Client.RemoteEndPoint as IPEndPoint)!;
            var remoteSocket      = IPSocket.AnyV6(IPPort.Auto);
            var connectionId      = this.connectionIdBuilder(this, Timestamp.Now, IPSocket, remoteSocket);

            try
            {

                client.ReceiveTimeout  = (Int32) ReceiveTimeout.TotalMilliseconds;
                client.SendTimeout     = (Int32) SendTimeout.   TotalMilliseconds;
                client.LingerState     = new LingerOption(true, 0);

                tcpConnection          = new TCPConnection(
                                             TCPServer:                    null,
                                             TCPClient:                    client,
                                             ServerCertificateSelector:    ServerCertificateSelector,
                                             ClientCertificateValidator:   ClientCertificateValidator,
                                             LocalCertificateSelector:     LocalCertificateSelector,
                                             AllowedTLSProtocols:          AllowedTLSProtocols,
                                             ReadTimeout:                  ReceiveTimeout,
                                             WriteTimeout:                 SendTimeout
                                         );

                await LogEvent(
                          OnNewConnection,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              DateTimeOffset.UtcNow,
                              eventTrackingId2,
                              remoteSocket,
                              connectionId,
                              new TCPConnection(
                                  null,
                                  client,
                                  null,
                                  null,
                                  null,
                                  null,
                                  ReceiveTimeout,
                                  SendTimeout
                              )
                          )
                      );

                await using var stream = client.GetStream();
                await stream.CopyToAsync(stream, bufferSize: (Int32) BufferSize, cts.Token).ConfigureAwait(false);

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
                    client.Client.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException) { }
                client.Close();

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

        #region Stop (EventTrackingId = null)

        /// <summary>
        /// Stop the EchoTestServer and close all active client connections.
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
            tcpListener.Stop();

            if (serverTask is not null)
                await serverTask;

            await LogEvent(
                      OnStarted,
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
        public Task SendConnectionClosed(DateTimeOffset      ServerTimestamp,
                                         EventTracking_Id    EventTrackingId,
                                         IPSocket            RemoteSocket,
                                         String              ConnectionId,
                                         ConnectionClosedBy  ClosedBy)
        {

            //try
            //{

            //    OnConnectionClosed?.Invoke(
            //        this,
            //        ServerTimestamp,
            //        EventTrackingId,
            //        RemoteSocket,
            //        ConnectionId,
            //        ClosedBy
            //    );

            //}
            //catch (Exception e)
            //{
            //    DebugX.LogException(e);
            //}

            return Task.CompletedTask;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(TCPEchoTestServer)}: {IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

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
