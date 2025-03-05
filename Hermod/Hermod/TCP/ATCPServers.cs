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

using System.Collections;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// An abstract TCP service allowing to attach multiple TCP servers on different IP sockets.
    /// </summary>
    public abstract class ATCPServers : IEnumerable<TCPServer>,
                                        IDisposable
    {

        #region Data

        /// <summary>
        /// The internal list of TCP servers.
        /// </summary>
        protected readonly List<TCPServer> tcpServers;

        private readonly SemaphoreSlim semaphoreSlim = new (1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// The DNS defines which DNS servers to use.
        /// </summary>
        public DNSClient                                                DNSClient                       { get; }

        /// <summary>
        /// The optional delegate to select a TLS server certificate.
        /// </summary>
        public ServerCertificateSelectorDelegate?                       ServerCertificateSelector       { get; }

        /// <summary>
        /// The optional delegate to verify the TLS client certificate used for authentication.
        /// </summary>
        public RemoteTLSClientCertificateValidationHandler<TCPServer>?  ClientCertificateValidator      { get; set; }

        /// <summary>
        /// The optional delegate to select the TLS client certificate used for authentication.
        /// </summary>
        public LocalCertificateSelectionHandler?                        LocalCertificateSelector       { get; }

        /// <summary>
        /// The TLS protocol(s) allowed for this connection.
        /// </summary>
        public SslProtocols                                             AllowedTLSProtocols             { get; }

        /// <summary>
        /// Whether a TLS client certificate is required.
        /// </summary>
        public Boolean                                                  ClientCertificateRequired       { get; }

        /// <summary>
        /// Whether the TLS client certificate should be checked for revocation.
        /// </summary>
        public Boolean                                                  CheckCertificateRevocation      { get; }


        #region ServiceName

        private String serviceName = TCPServer.__DefaultServiceName;

        /// <summary>
        /// The service banner transmitted to a TCP client
        /// after connection initialization.
        /// </summary>
        public String ServiceName
        {

            get
            {
                return serviceName;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                {
                    lock (tcpServers)
                    {
                        serviceName = value;
                        tcpServers.ForEach(tcpServer => tcpServer.ServiceName = value);
                    }
                }
            }

        }

        #endregion

        #region ServiceBanner

        private String serviceBanner = TCPServer.__DefaultServiceBanner;

        /// <summary>
        /// The service banner transmitted to a TCP client
        /// after connection initialization.
        /// </summary>
        public String ServiceBanner
        {

            get
            {
                return serviceBanner;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                {
                    lock (tcpServers)
                    {
                        serviceBanner = value;
                        tcpServers.ForEach(tcpServer => tcpServer.ServiceBanner = value);
                    }
                }
            }

        }

        #endregion

        #region ServerThreadName

        private ServerThreadNameCreatorDelegate serverThreadNameCreator;

        /// <summary>
        /// The name of the TCP service threads.
        /// </summary>
        public ServerThreadNameCreatorDelegate ServerThreadNameCreator
        {

            get
            {
                return serverThreadNameCreator;
            }

            set
            {
                lock (tcpServers)
                {
                    serverThreadNameCreator = value;
                    tcpServers.ForEach(tcpServer => tcpServer.ServerThreadNameCreator = value);
                }
            }

        }

        #endregion

        #region ServerThreadPrioritySetter

        private ServerThreadPriorityDelegate serverThreadPrioritySetter;

        /// <summary>
        /// The priority of the TCP service threads.
        /// </summary>
        public ServerThreadPriorityDelegate ServerThreadPrioritySetter
        {

            get
            {
                return serverThreadPrioritySetter;
            }

            set
            {
                lock (tcpServers)
                {
                    serverThreadPrioritySetter = value;
                    tcpServers.ForEach(tcpServer => tcpServer.ServerThreadPrioritySetter = value);
                }
            }

        }

        #endregion

        #region ServerThreadIsBackground

        private Boolean serverThreadIsBackground;

        /// <summary>
        /// Weather the TCP service threads are background or not (default: yes).
        /// </summary>
        public Boolean ServerThreadIsBackground
        {

            get
            {
                return serverThreadIsBackground;
            }

            set
            {
                lock (tcpServers)
                {
                    serverThreadIsBackground = value;
                    tcpServers.ForEach(tcpServer => tcpServer.ServerThreadIsBackground = value);
                }
            }

        }

        #endregion


        #region ConnectionIdBuilder

        private ConnectionIdBuilder connectionIdBuilder;

        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder ConnectionIdBuilder
        {

            get
            {
                return connectionIdBuilder;
            }

            set
            {
                lock (tcpServers)
                {
                    connectionIdBuilder = value;
                    tcpServers.ForEach(tcpServer => tcpServer.ConnectionIdBuilder = value);
                }
            }

        }

        #endregion

        #region ConnectionTimeout

        private TimeSpan connectionTimeout  = TCPServer.__DefaultConnectionTimeout;

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan ConnectionTimeout
        {

            get
            {
                return connectionTimeout;
            }

            set
            {
                lock (tcpServers)
                {
                    connectionTimeout = value;
                    tcpServers.ForEach(tcpServer => tcpServer.ConnectionTimeout = value);
                }
            }

        }

        #endregion

        #region MaxClientConnections

        private UInt32 maxClientConnections  = TCPServer.__DefaultMaxClientConnections;

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32 MaxClientConnections
        {

            get
            {
                return maxClientConnections;
            }

            set
            {
                lock (tcpServers)
                {
                    maxClientConnections = value;
                    tcpServers.ForEach(tcpServer => tcpServer.MaxClientConnections = value);
                }
            }

        }

        #endregion


        #region IsStarted

        private Boolean isStarted = false;

        /// <summary>
        /// Is the server already started?
        /// </summary>
        public Boolean IsStarted

            => isStarted;

        #endregion

        #region NumberOfClients

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {
                lock (tcpServers)
                {
                    return tcpServers.Sum(tcpServer => tcpServer.NumberOfClients);
                }
            }
        }

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        public event StartedEventHandler?           OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP socket was attached.
        /// </summary>
        public event TCPSocketAttachedHandler?      OnTCPSocketAttached;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NewConnectionHandler?          OnNewConnection;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler?  OnExceptionOccured;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedHandler?       OnConnectionClosed;

        /// <summary>
        /// An event fired whenever a new TCP socket was detached.
        /// </summary>
        public event TCPSocketDetachedHandler?      OnTCPSocketDetached;

        /// <summary>
        /// An event fired whenever the TCP servers instance was stopped.
        /// </summary>
        public event CompletedEventHandler?         OnCompleted;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP service allowing to attach multiple TCP servers on different IP sockets.
        /// </summary>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The service banner transmitted to a TCP client after connection initialization.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// <param name="ClientCertificateRequired">Whether a TLS client certificate is required.</param>
        /// <param name="CheckCertificateRevocation">Whether the TLS client certificate should be checked for revocation.</param>
        /// 
        /// <param name="ServerThreadName">An optional name of the TCP server threads.</param>
        /// <param name="ServerThreadPriority">An optional priority of the TCP server threads (default: AboveNormal).</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server threads are a background thread or not (default: yes).</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the TCP server threads immediately (default: no).</param>
        public ATCPServers(String?                                                  ServiceName                  = null,
                           String?                                                  ServiceBanner                = null,

                           ServerCertificateSelectorDelegate?                       ServerCertificateSelector    = null,
                           RemoteTLSClientCertificateValidationHandler<TCPServer>?  ClientCertificateValidator   = null,
                           LocalCertificateSelectionHandler?                        LocalCertificateSelector     = null,
                           SslProtocols?                                            AllowedTLSProtocols          = null,
                           Boolean?                                                 ClientCertificateRequired    = null,
                           Boolean?                                                 CheckCertificateRevocation   = null,

                           ServerThreadNameCreatorDelegate?                         ServerThreadNameCreator      = null,
                           ServerThreadPriorityDelegate?                            ServerThreadPrioritySetter   = null,
                           Boolean?                                                 ServerThreadIsBackground     = null,
                           ConnectionIdBuilder?                                     ConnectionIdBuilder          = null,
                           TimeSpan?                                                ConnectionTimeout            = null,
                           UInt32?                                                  MaxClientConnections         = null,

                           DNSClient?                                               DNSClient                    = null,
                           Boolean                                                  AutoStart                    = false)

        {

            this.tcpServers                  = [];

            this.ServerCertificateSelector   = ServerCertificateSelector;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.AllowedTLSProtocols         = AllowedTLSProtocols        ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.ClientCertificateRequired   = ClientCertificateRequired  ?? false;
            this.CheckCertificateRevocation  = CheckCertificateRevocation ?? false;

            this.serviceName                 = ServiceName                ?? TCPServer.__DefaultServiceName;
            this.serviceBanner               = ServiceBanner              ?? TCPServer.__DefaultServiceBanner;

            this.serverThreadNameCreator     = ServerThreadNameCreator    ?? (socket => TCPServer.__DefaultServerThreadName);
            this.serverThreadPrioritySetter  = ServerThreadPrioritySetter ?? (socket => ThreadPriority.AboveNormal);
            this.serverThreadIsBackground    = ServerThreadIsBackground   ?? true;

            this.connectionIdBuilder         = ConnectionIdBuilder        ?? ((sender, timestamp, localSocket, remoteIPSocket) => $"TCP: {remoteIPSocket.IPAddress}:{remoteIPSocket.Port}");
            this.connectionTimeout           = ConnectionTimeout          ?? TimeSpan.FromSeconds(30);
            this.maxClientConnections        = MaxClientConnections       ?? TCPServer.__DefaultMaxClientConnections;

            this.DNSClient                   = DNSClient                  ?? new DNSClient();

            if (AutoStart)
                Start(EventTracking_Id.New).Wait();

        }

        #endregion


        // Manage the underlying TCP sockets...

        #region (protected) AttachTCPPorts  (Action, params Ports)

        protected async Task AttachTCPPorts(Action<TCPServer>  Action,
                                            params IPPort[]    Ports)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                foreach (var port in Ports)
                {

                    var tcpServer = tcpServers.AddAndReturnElement(
                                        new TCPServer(
                                            port,
                                            ServiceName,
                                            ServiceBanner,

                                            ServerCertificateSelector,
                                            ClientCertificateValidator,
                                            LocalCertificateSelector,
                                            AllowedTLSProtocols,
                                            ClientCertificateRequired,
                                            CheckCertificateRevocation,

                                            serverThreadNameCreator,
                                            serverThreadPrioritySetter,
                                            serverThreadIsBackground,
                                            connectionIdBuilder,
                                            connectionTimeout,
                                            maxClientConnections,

                                            false
                                        )
                                    );

                    tcpServer.OnStarted          += (sender, timestamp, eventTrackingId, message) => SendTCPSocketAttached(timestamp, eventTrackingId, tcpServer.IPSocket, message);
                    tcpServer.OnNewConnection    += SendNewConnection;
                    tcpServer.OnConnectionClosed += SendConnectionClosed;
                    tcpServer.OnCompleted        += (sender, timestamp, eventTrackingId, message) => SendTCPSocketDetached(timestamp, eventTrackingId, tcpServer.IPSocket, message);
                    tcpServer.OnExceptionOccured += SendExceptionOccured;

                    Action(tcpServer);

                }

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion

        #region (protected) AttachTCPSockets(Action, params Sockets)

        protected async Task AttachTCPSockets(Action<TCPServer>  Action,
                                              params IPSocket[]  Sockets)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                foreach (var socket in Sockets)
                {

                    var tcpServer = tcpServers.AddAndReturnElement(new TCPServer(
                                                                       socket,
                                                                       ServiceName,
                                                                       ServiceBanner,

                                                                       ServerCertificateSelector,
                                                                       ClientCertificateValidator,
                                                                       LocalCertificateSelector,
                                                                       AllowedTLSProtocols,
                                                                       ClientCertificateRequired,
                                                                       CheckCertificateRevocation,

                                                                       serverThreadNameCreator,
                                                                       serverThreadPrioritySetter,
                                                                       serverThreadIsBackground,
                                                                       connectionIdBuilder,
                                                                       connectionTimeout,
                                                                       maxClientConnections,

                                                                       false)
                                                                   );

                    tcpServer.OnStarted          += (sender, timestamp, eventTrackingId, message) => SendTCPSocketAttached(timestamp, eventTrackingId, tcpServer.IPSocket, message);
                    tcpServer.OnNewConnection    += SendNewConnection;
                    tcpServer.OnConnectionClosed += SendConnectionClosed;
                    tcpServer.OnCompleted        += (sender, timestamp, eventTrackingId, message) => SendTCPSocketDetached(timestamp, eventTrackingId, tcpServer.IPSocket, message);
                    tcpServer.OnExceptionOccured += SendExceptionOccured;

                    Action(tcpServer);

                    SendTCPSocketAttached(
                        Timestamp.Now,
                        EventTracking_Id.New,
                        tcpServer.IPSocket
                    );

                }

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion

        #region (protected) DetachTCPPorts  (Action, params Ports)

        protected async Task DetachTCPPorts(Action<TCPServer>  Action,
                                            params IPPort[]    Ports)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                foreach (var Port in Ports)
                {
                    foreach (var tcpServer in tcpServers)
                    {
                        if ((IPv4Address) tcpServer.IPAddress == IPv4Address.Any &&
                                          tcpServer.Port      == Port)
                        {

                            tcpServer.OnStarted           -= SendStarted;
                            tcpServer.OnNewConnection     -= SendNewConnection;
                            tcpServer.OnConnectionClosed  -= SendConnectionClosed;
                            tcpServer.OnCompleted         -= SendCompleted;
                            tcpServer.OnExceptionOccured  -= SendExceptionOccured;

                            Action(tcpServer);

                        }
                    }
                }

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion


        #region IPPorts

        /// <summary>
        /// All attached IP ports.
        /// </summary>
        public IEnumerable<IPPort> IPPorts

            => tcpServers.Select(tcpServer => tcpServer.Port);

        #endregion

        #region IPSockets

        /// <summary>
        /// All attached IP sockets.
        /// </summary>
        public IEnumerable<IPSocket> IPSockets

            => tcpServers.Select(tcpServer => tcpServer.IPSocket);

        #endregion

        #region GetEnumerator()

        IEnumerator IEnumerable.GetEnumerator()

            => tcpServers.GetEnumerator();

        public IEnumerator<TCPServer> GetEnumerator()

            => tcpServers.GetEnumerator();

        #endregion



        // Send events...

        #region (protected) SendStarted           (Sender, Timestamp, EventTrackingId = null, Message = null)

        protected void SendStarted(Object             Sender,
                                   DateTime           Timestamp,
                                   EventTracking_Id?  EventTrackingId   = null,
                                   String?            Message           = null)
        {

            OnStarted?.Invoke(
                Sender,
                Timestamp,
                EventTrackingId ?? EventTracking_Id.New,
                Message
            );

        }

        #endregion

        #region (protected) SendTCPSocketAttached (Timestamp, EventTrackingId, TCPSocket, Message = null)

        protected void SendTCPSocketAttached(DateTime          Timestamp,
                                             EventTracking_Id  EventTrackingId,
                                             IPSocket          TCPSocket,
                                             String?           Message   = null)
        {

            OnTCPSocketAttached?.Invoke(
                this,
                Timestamp,
                EventTrackingId,
                TCPSocket,
                Message
            );

        }

        #endregion

        #region (protected) SendNewConnection     (TCPServer, Timestamp, EventTrackingId, RemoteSocket, ConnectionId, TCPConnection)

        protected void SendNewConnection(TCPServer         TCPServer,
                                         DateTime          Timestamp,
                                         EventTracking_Id  EventTrackingId,
                                         IPSocket          RemoteSocket,
                                         String            ConnectionId,
                                         TCPConnection     TCPConnection)
        {

            OnNewConnection?.Invoke(
                TCPServer,
                Timestamp,
                EventTrackingId,
                RemoteSocket,
                ConnectionId,
                TCPConnection
            );

        }

        #endregion

        #region (protected) SendConnectionClosed  (TCPServer, Timestamp, EventTrackingId, RemoteSocket, ConnectionId, ClosedBy)

        protected void SendConnectionClosed(TCPServer           TCPServer,
                                            DateTime            Timestamp,
                                            EventTracking_Id    EventTrackingId,
                                            IPSocket            RemoteSocket,
                                            String              ConnectionId,
                                            ConnectionClosedBy  ClosedBy)
        {

            OnConnectionClosed?.Invoke(
                TCPServer,
                Timestamp,
                EventTrackingId,
                RemoteSocket,
                ConnectionId,
                ClosedBy
            );

        }

        #endregion

        #region (protected) SendTCPSocketDetached (Timestamp, EventTrackingId, TCPSocket, Message = null)

        protected void SendTCPSocketDetached(DateTime          Timestamp,
                                             EventTracking_Id  EventTrackingId,
                                             IPSocket          TCPSocket,
                                             String?           Message   = null)
        {

            OnTCPSocketDetached?.Invoke(
                this,
                Timestamp,
                EventTrackingId,
                TCPSocket,
                Message
            );

        }

        #endregion

        #region (protected) SendCompleted         (Sender, EventTrackingId, Timestamp, Message = null)

        protected void SendCompleted(Object            Sender,
                                     DateTime          Timestamp,
                                     EventTracking_Id  EventTrackingId,
                                     String?           Message   = null)
        {

            OnCompleted?.Invoke(
                Sender,
                Timestamp,
                EventTrackingId,
                Message
            );

        }

        #endregion

        #region (protected) SendExceptionOccured  (Sender, Timestamp, EventTrackingId, Exception)

        protected void SendExceptionOccured(Object            Sender,
                                            DateTime          Timestamp,
                                            EventTracking_Id  EventTrackingId,
                                            Exception         Exception)
        {

            OnExceptionOccured?.Invoke(
                Sender,
                Timestamp,
                EventTrackingId,
                Exception
            );

        }

        #endregion



        // Start/stop the TCP servers...

        #region Start(EventTrackingId = null)

        /// <summary>
        /// Start the TCP servers.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public async Task<Boolean> Start(EventTracking_Id? EventTrackingId = null)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                foreach (var tcpServer in tcpServers)
                    await tcpServer.Start(EventTrackingId);

                isStarted = true;

                SendStarted(this, Timestamp.Now);

                return true;

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion

        #region Start(Delay, EventTrackingId = null, InBackground = true)

        public async Task<Boolean> Start(TimeSpan           Delay,
                                         EventTracking_Id?  EventTrackingId   = null,
                                         Boolean            InBackground      = true)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                EventTrackingId ??= EventTracking_Id.New;

                foreach (var TCPServer in tcpServers)
                    await TCPServer.Start(
                              Delay,
                              EventTrackingId,
                              InBackground
                          );

                SendStarted(this, Timestamp.Now);

                return true;

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion

        #region Shutdown(EventTrackingId = null, Message = null, Wait = true)

        public virtual async Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                                    String?            Message           = null,
                                                    Boolean            Wait              = true)
        {

            await semaphoreSlim.WaitAsync();

            try
            {

                EventTrackingId ??= EventTracking_Id.New;

                foreach (var tcpServer in tcpServers)
                    await tcpServer.Shutdown(
                              EventTrackingId,
                              Message,
                              Wait
                          );

                SendCompleted(
                    this,
                    Timestamp.Now,
                    EventTracking_Id.New,
                    Message
                );

                return true;

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {

            semaphoreSlim.Wait();

            try
            {

                foreach (var tcpServer in tcpServers)
                    tcpServer.Dispose();

            }
            finally
            {
                semaphoreSlim.Release();
            }

        }

        #endregion


    }

}
