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

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// A multi-threaded Styx arrow sender that listens on a TCP
    /// socket and notifies about incoming TCP connections.
    /// </summary>
    public class TCPServer : ITCPServer,
                             IArrowSender<TCPConnection>,
                             IServer
    {

        #region Data

        /// <summary>
        /// The default TCP service name.
        /// </summary>
        public  const            String                                         __DefaultServiceName            = "TCP Server";

        /// <summary>
        /// The default TCP service banner.
        /// </summary>
        public  const            String                                         __DefaultServiceBanner          = "Vanaheimr Hermod TCP Server v0.10";

        /// <summary>
        /// The default server thread name.
        /// </summary>
        public  const            String                                         __DefaultServerThreadName       = "TCP Server thread on ";

        /// <summary>
        /// The default maximum number of concurrent TCP client connections.
        /// </summary>
        public  const            UInt32                                         __DefaultMaxClientConnections   = 4096;

        /// <summary>
        /// The default TCP client timeout for all incoming client connections.
        /// </summary>
        public  static readonly  TimeSpan                                       __DefaultConnectionTimeout      = TimeSpan.FromSeconds(30);

        private        readonly  TcpListener                                    tcpListener;
        private        readonly  ConcurrentDictionary<IPSocket, TCPConnection>  tcpConnections;

        private        readonly  Thread                                         listenerThread;
        private        readonly  CancellationTokenSource                        cancellationTokenSource;
        private        volatile  Boolean                                        isRunning = false;

        #endregion

        #region Properties

        /// <summary>
        /// The TCP service name shown e.g. on service startup.
        /// </summary>
        public String                            ServiceName                            { get; set; }

        /// <summary>
        /// Gets the IPAddress on which the TCP server listens.
        /// </summary>
        public IIPAddress                        IPAddress                              { get; }

        /// <summary>
        /// Gets the port on which the TCP server listens.
        /// </summary>
        public IPPort                            TCPPort                                   { get; private set; }

        /// <summary>
        /// Gets the IP socket on which the TCP server listens.
        /// </summary>
        public IPSocket                          IPSocket                               { get; private set; }

        /// <summary>
        /// The optional TLS certificate.
        /// </summary>
        //public X509Certificate2                  ServerCertificate                      { get; }

        /// <summary>
        /// Whether TLS client certification is required.
        /// </summary>
        public Boolean                           ClientCertificateRequired              { get; }

        /// <summary>
        /// Whether TLS client certificate revocation should be verified.
        /// </summary>
        public Boolean                           CheckCertificateRevocation             { get; }

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
        /// </summary>
        public String                            ServiceBanner                          { get; set; }


        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        public ServerThreadNameCreatorDelegate   ServerThreadNameCreator                { get; set; }

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        public ServerThreadPriorityDelegate      ServerThreadPrioritySetter             { get; set; }

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean                           ServerThreadIsBackground               { get; set; }



        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder               ConnectionIdBuilder                    { get; set; }

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan                          ConnectionTimeout                      { get; set; }

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32                            MaxClientConnections                   { get; set; }

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt32                            NumberOfConnectedClients
            => (UInt32) tcpConnections.Count;

        /// <summary>
        /// Return an enumeration of sockets of all currently connected clients.
        /// </summary>
        public IEnumerable<IPSocket>             ClientSockets
            => [.. tcpConnections
                   .Select(connection => connection.Key)
                   .Distinct()];

        /// <summary>
        /// True while the TCPServer is listening for new clients
        /// </summary>
        public Boolean                           IsRunning
            => isRunning;

        /// <summary>
        /// The TCPServer was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        //public Boolean                           StopRequested
        //    => _StopRequested;


        public RemoteTLSClientCertificateValidationHandler<ITCPServer>? ClientCertificateValidator { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        public event StartedEventHandler?                        OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// If this event closes the TCP connection the OnNotification event will never be fired!
        /// Therefore you can use this event for filtering connection initiation requests.
        /// </summary>
        public event NewConnectionDelegate?                      OnNewConnection;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NotificationEventHandler<TCPConnection>?    OnNotification;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccurredEventHandler?              OnExceptionOccurred;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedDelegate?                   OnConnectionClosed;

        /// <summary>
        /// An event fired whenever the TCP servers instance was stopped.
        /// </summary>
        public event CompletedEventHandler?                      OnCompleted;

        #endregion

        #region Constructor(s)

        #region TCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// 
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IPPort                                                    Port,
                         String?                                                   ServiceName                  = null,
                         String?                                                   ServiceBanner                = null,

                         ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                         RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                         LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                         SslProtocols?                                             AllowedTLSProtocols          = null,
                         Boolean?                                                  ClientCertificateRequired    = null,
                         Boolean?                                                  CheckCertificateRevocation   = null,

                         ServerThreadNameCreatorDelegate?                          ServerThreadNameCreator      = null,
                         ServerThreadPriorityDelegate?                             ServerThreadPrioritySetter   = null,
                         Boolean?                                                  ServerThreadIsBackground     = null,
                         ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                         TimeSpan?                                                 ConnectionTimeout            = null,
                         UInt32?                                                   MaxClientConnections         = null,

                         Boolean                                                   AutoStart                    = false)

            : this(IPv4Address.Any,
                   Port,
                   ServiceName,
                   ServiceBanner,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   AutoStart)

        { }

        #endregion

        #region TCPServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector#">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// 
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IIPAddress                                                IPAddress,
                         IPPort                                                    Port,
                         String?                                                   ServiceName                  = null,
                         String?                                                   ServiceBanner                = null,

                         ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                         RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                         LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                         SslProtocols?                                             AllowedTLSProtocols          = null,
                         Boolean?                                                  ClientCertificateRequired    = null,
                         Boolean?                                                  CheckCertificateRevocation   = null,

                         ServerThreadNameCreatorDelegate?                          ServerThreadNameCreator      = null,
                         ServerThreadPriorityDelegate?                             ServerThreadPrioritySetter   = null,
                         Boolean?                                                  ServerThreadIsBackground     = null,
                         ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                         TimeSpan?                                                 ConnectionTimeout            = null,
                         UInt32?                                                   MaxClientConnections         = null,

                         Boolean                                                   AutoStart                    = false)

        {

            // TCP Socket
            this.IPAddress                   = IPAddress;
            this.TCPPort                        = Port;
            this.ClientCertificateRequired   = ClientCertificateRequired  ?? false;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.CheckCertificateRevocation  = CheckCertificateRevocation ?? false;

            this.IPSocket                    = new IPSocket   (
                                                   this.IPAddress,
                                                   this.TCPPort
                                               );

            this.tcpListener                 = new TcpListener(
                                                   new System.Net.IPAddress(
                                                       this.IPAddress.GetBytes()
                                                   ),
                                                   this.TCPPort.ToInt32()
                                               );

            // TCP Server
            this.ServiceName                 = ServiceName      is not null && ServiceName.  Trim().IsNotNullOrEmpty()
                                                   ? ServiceName
                                                   : __DefaultServiceName;
            this.ServiceBanner               = ServiceBanner    is not null && ServiceBanner.Trim().IsNotNullOrEmpty()
                                                   ? ServiceBanner
                                                   : __DefaultServiceBanner;
            this.ServerThreadNameCreator     = ServerThreadNameCreator    ?? (socket => $"TCPServer {socket}");
            this.ServerThreadPrioritySetter  = ServerThreadPrioritySetter ?? (socket => ThreadPriority.AboveNormal);
            this.ServerThreadIsBackground    = ServerThreadIsBackground   ?? true;

            // TCP Connections
            this.tcpConnections              = new ConcurrentDictionary<IPSocket, TCPConnection>();
            this.ConnectionIdBuilder         = ConnectionIdBuilder        ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP Server:"        + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port);
            this.ConnectionTimeout           = ConnectionTimeout          ?? TimeSpan.FromSeconds(30);

            this.MaxClientConnections        = MaxClientConnections       ?? __DefaultMaxClientConnections;


            #region TCP Listener Thread

            this.cancellationTokenSource            = new CancellationTokenSource();

            listenerThread = new Thread(() => {

                Thread.CurrentThread.Name           = this.ServerThreadNameCreator   (IPSocket);
                Thread.CurrentThread.Priority       = this.ServerThreadPrioritySetter(IPSocket);
                Thread.CurrentThread.IsBackground   = this.ServerThreadIsBackground;

                var token                           = cancellationTokenSource.Token;
                var connectionAcceptTime1           = Timestamp.Now;
                var connectionAcceptTime2           = Timestamp.Now;

                #region SetSocketOptions

                // IOControlCode.*

                // fd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, tcpKeepalive);

                // bytes.PutInteger(endian, tcpKeepalive,      0);
                // bytes.PutInteger(endian, tcpKeepaliveIdle,  4);
                // bytes.PutInteger(endian, tcpKeepaliveIntvl, 8);

                // fd.IOControl(IOControlCode.KeepAliveValues, (byte[])bytes, null);

                #endregion

                try
                {

                    isRunning = true;

                    while (!token.IsCancellationRequested)
                    {

                        // Wait for a new/pending client connection
                        while (!token.IsCancellationRequested && !tcpListener.Pending())
                            Thread.Sleep(10);

                        if (token.IsCancellationRequested)
                            break;

                        #region  Accept new TCP connection

                        //DebugX.Log(" [", nameof(TCPServer), ":", Port.ToString(), "] New TCP connection pending...");

                        connectionAcceptTime1 = Timestamp.Now;

                        TCPConnection? tcpConnection = null;

                        try
                        {

                            var newTCPClient = tcpListener.AcceptTcpClient();

                            if (newTCPClient is not null)
                            {

                                //if (newTCPClient.Client.LocalEndPoint  is IPEndPoint localIPEndPoint &&
                                //    newTCPClient.Client.RemoteEndPoint is IPEndPoint remoteIPEndPoint)
                                //{
                                //    DebugX.Log(" [", nameof(TCPServer), ":", localIPEndPoint.Port.ToString(), "] New TCP connection accepted: " + remoteIPEndPoint.Address.ToString(), ":", remoteIPEndPoint.Port.ToString());
                                //}

                                tcpConnection = new TCPConnection(
                                                    TCPServer:                    this,
                                                    TCPClient:                    newTCPClient,
                                                    ServerCertificateSelector:    ServerCertificateSelector,
                                                    ClientCertificateValidator:   ClientCertificateValidator,
                                                    LocalCertificateSelector:     LocalCertificateSelector,
                                                    AllowedTLSProtocols:          AllowedTLSProtocols,
                                                    ReadTimeout:                  ConnectionTimeout,
                                                    WriteTimeout:                 ConnectionTimeout
                                                );

                                // Store the new connection
                                //_SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                                //                               _TCPConnection.Value,
                                //                               (RemoteEndPoint, TCPConnection) => TCPConnection);

                                //DebugX.Log(" [", nameof(TCPServer), ":", tcpConnection.LocalPort.ToString(), "] New TCP connection created: " + tcpConnection.RemoteSocket.ToString());

                            }

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(" [", nameof(TCPServer), "] Could not accept new TCP connection: ", e.Message, e.StackTrace is not null ? Environment.NewLine + e.StackTrace : "");
                        }

                        #endregion

                        #region  Process new TCP connection

                        if (tcpConnection is not null)
                        {

                            connectionAcceptTime2 = Timestamp.Now;

                            var x = Task.Factory.StartNew(connection => {
                                if (connection is TCPConnection newTCPConnection)

                                {

                                    try
                                    {

                                        #region Copy ExceptionOccurred event handlers

                                        //foreach (var ExceptionOccurredHandler in MyEventStorage)
                                        //    _TCPConnection.Value.OnExceptionOccurred += ExceptionOccurredHandler;

                                        #endregion

                                        //DebugX.Log(" [", nameof(TCPServer), ":", newTCPConnection.LocalPort.ToString(), "] New TCP connection task created: ", newTCPConnection.RemoteSocket.ToString());

                                        // If this event closes the TCP connection the OnNotification event will never be fired!
                                        // Therefore you can use this event for filtering connection initiation requests.
                                        OnNewConnection?.Invoke(newTCPConnection.TCPServer,
                                                                newTCPConnection.ServerTimestamp,
                                                                EventTracking_Id.New,
                                                                newTCPConnection.RemoteSocket,
                                                                newTCPConnection.ConnectionId,
                                                                newTCPConnection);

                                        if (!newTCPConnection.IsClosed)
                                            OnNotification?.Invoke(
                                                EventTracking_Id.New,
                                                newTCPConnection
                                            );

                                    }
                                    catch (Exception e)
                                    {

                                        while (e.InnerException is not null)
                                            e = e.InnerException;

                                        OnExceptionOccurred?.Invoke(
                                            this,
                                            Timestamp.Now,
                                            EventTracking_Id.New,
                                            e
                                        );

                                        DebugX.Log(" [", nameof(TCPServer), ":", tcpConnection.LocalPort.ToString(), "] Connection exception: ", e.Message, e.StackTrace is not null ? Environment.NewLine + e.StackTrace : "");

                                        newTCPConnection?.Close();

                                    }

                                }
                            },
                            tcpConnection,
                            CancellationToken.None,
                            TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default);

                            //DebugX.Log("New TCP connection from ", tcpConnection.RemoteSocket.ToString(), " created after: ", (connectionAcceptTime2 - connectionAcceptTime1).TotalMilliseconds.ToString(), " ms / ", (Timestamp.Now - connectionAcceptTime2).TotalMilliseconds.ToString(), " ms");

                            Thread.Sleep(15);

                        }

                        #endregion

                    }

                    #region Shutdown

                    // Request all client connections to finish!
                    foreach (var socketConnection in tcpConnections)
                        socketConnection.Value.StopRequested = true;

                    // After stopping the TCPListener wait for
                    // all client connections to finish!
                    while (!tcpConnections.IsEmpty)
                        Thread.Sleep(5);

                    #endregion

                }
                catch (Exception e)
                {

                    DebugX.Log(" [", nameof(TCPServer), "] Exception occurred: ", e.Message, e.StackTrace is not null ? Environment.NewLine + e.StackTrace : "");

                    var OnExceptionLocal = OnExceptionOccurred;
                    if (OnExceptionLocal is not null)
                        OnExceptionLocal(
                            this,
                            Timestamp.Now,
                            EventTracking_Id.New,
                            e
                        );

                }

                isRunning = false;

            });

            #endregion


            if (AutoStart)
                Start();

        }

        #endregion

        #region TCPServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="AutoStart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IPSocket                                                  IPSocket,
                         String                                                    ServiceName                  = __DefaultServiceName,
                         String                                                    ServiceBanner                = __DefaultServiceBanner,

                         ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                         RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                         LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                         SslProtocols?                                             AllowedTLSProtocols          = null,
                         Boolean?                                                  ClientCertificateRequired    = null,
                         Boolean?                                                  CheckCertificateRevocation   = null,

                         ServerThreadNameCreatorDelegate?                          ServerThreadNameCreator      = null,
                         ServerThreadPriorityDelegate?                             ServerThreadPrioritySetter   = null,
                         Boolean?                                                  ServerThreadIsBackground     = null,
                         ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                         TimeSpan?                                                 ConnectionTimeout            = null,
                         UInt32?                                                   MaxClientConnections         = null,

                         Boolean                                                   AutoStart                    = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceName,
                   ServiceBanner,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   AutoStart)

        { }

        #endregion

        #endregion


        private (Boolean, IEnumerable<String>) DoClientCertificateValidator(Object             Sender,
                                                                            X509Certificate2?  Certificate,
                                                                            X509Chain?         CertificateChain,
                                                                            SslPolicyErrors    PolicyErrors)

            => this.ClientCertificateValidator?.Invoke(
                   Sender,
                   Certificate,
                   CertificateChain,
                   this,
                   PolicyErrors
               ) ?? (false, []);


        #region (protected internal) SendNewConnection(ServerTimestamp, RemoteSocket, ConnectionId, TCPConnection)

        /// <summary>
        /// Send a "new connection" event.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the request.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="RemoteSocket">The remote socket that was closed.</param>
        /// <param name="ConnectionId">The internal connection identification.</param>
        /// <param name="TCPConnection">The connection itself.</param>
        protected internal void SendNewConnection(DateTime          ServerTimestamp,
                                                  EventTracking_Id  EventTrackingId,
                                                  IPSocket          RemoteSocket,
                                                  String            ConnectionId,
                                                  TCPConnection     TCPConnection)
        {

            try
            {

                OnNewConnection?.Invoke(
                    this,
                    ServerTimestamp,
                    EventTrackingId,
                    RemoteSocket,
                    ConnectionId,
                    TCPConnection
                );

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }

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

            try
            {

                OnConnectionClosed?.Invoke(
                    this,
                    ServerTimestamp,
                    EventTrackingId,
                    RemoteSocket,
                    ConnectionId,
                    ClosedBy
                );

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
            }

            return Task.CompletedTask;

        }

        #endregion


        #region Start(EventTrackingId = null)

        /// <summary>
        /// Start the TCP receiver.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public async Task<Boolean> Start(EventTracking_Id? EventTrackingId = null)

            => await Start(
                         __DefaultMaxClientConnections,
                         EventTrackingId ?? EventTracking_Id.New
                     );

        #endregion

        #region Start(Delay, EventTrackingId = null, InBackground = true)

        /// <summary>
        /// Start the TCP receiver after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public async Task<Boolean> Start(TimeSpan           Delay,
                                         EventTracking_Id?  EventTrackingId   = null,
                                         Boolean            InBackground      = true)
        {

            if (!InBackground)
            {
                await Task.Delay(Delay);
                return await Start(EventTrackingId ?? EventTracking_Id.New);
            }

            else
            {

                await Task.Factory.StartNew(async () => {

                    Thread.Sleep(Delay);
                    await Start(EventTrackingId ?? EventTracking_Id.New);

                }, cancellationTokenSource.Token,
                   TaskCreationOptions.AttachedToParent,
                   TaskScheduler.Default);

            }

            return true;

        }

        #endregion

        #region Start(MaxClientConnections, EventTrackingId = null)

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public async Task<Boolean> Start(UInt32             MaxClientConnections,
                                         EventTracking_Id?  EventTrackingId   = null)
        {

            // volatile!
            if (isRunning)
                return false;

            EventTrackingId ??= EventTracking_Id.New;

            if (MaxClientConnections != __DefaultMaxClientConnections)
                this.MaxClientConnections = MaxClientConnections;

            try
            {

                tcpListener.Start((Int32) this.MaxClientConnections);

                TCPPort      = IPPort.Parse(((IPEndPoint) tcpListener.LocalEndpoint).Port);

                IPSocket  = new IPSocket(
                                IPAddress,
                                TCPPort
                            );

                DebugX.LogT($"Started '{ServiceName}' on TCP port {TCPPort} ({EventTrackingId})...");

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
                return false;
            }

            try
            {

                if (listenerThread is null)
                {
                    DebugX.LogT($"An exception occured in Hermod.{nameof(TCPServer)}.{nameof(Start)}(MaxClientConnections) [_ListenerThread == null]!");
                    return false;
                }

                listenerThread.Start();

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
                return false;
            }


            // Wait until socket has opened (volatile!)
            while (!isRunning)
                await Task.Delay(10);

            OnStarted?.Invoke(
                this,
                Timestamp.Now,
                EventTrackingId
            );

            return true;

        }

        #endregion


        #region Shutdown(EventTrackingId = null, Message = null, Wait = true)

        /// <summary>
        /// Shutdown the TCP listener.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        /// <param name="Message">An optional shutdown message.</param>
        public async Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                            String?            Message           = null,
                                            Boolean            Wait              = true)
        {

            cancellationTokenSource.Cancel();

            while (!tcpConnections.IsEmpty)
                await Task.Delay(10);

            tcpListener?.Stop();

            if (Wait) {
                while (isRunning) {
                    await Task.Delay(10);
                }
            }

            OnCompleted?.Invoke(
                this,
                Timestamp.Now,
                EventTrackingId ?? EventTracking_Id.New,
                Message ?? ""
            );

            return true;

        }

        #endregion

        #region StopAndWait()

        ///// <summary>
        ///// Stop the TCPServer and wait until all connections are closed.
        ///// </summary>
        //public async Task<Boolean> StopAndWait()
        //{

        //    cancellationTokenSource.Cancel();

        //    while (!tcpConnections.IsEmpty)
        //        Thread.Sleep(10);

        //    tcpListener?.Stop();

        //    return true;

        //}

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            var type              = this.GetType();
            var genericArguments  = type.GetGenericArguments();
            var typeName          = (genericArguments.Length > 0) ? type.Name.Remove(type.Name.Length - 2) : type.Name;
            var genericType       = (genericArguments.Length > 0) ? "<" + genericArguments[0].Name + ">"   : String.Empty;
            var running           = (IsRunning)                   ? " (running)"                           : String.Empty;

            return $"{ServiceName} [{typeName}{genericType}] on {IPSocket}, {running}";

        }

        #endregion


        #region IDisposable Members

        public void Dispose()
        {

            //StopAndWait();

            //tcpListener?.Stop();

            GC.SuppressFinalize(this);

        }

        #endregion


    }

}
