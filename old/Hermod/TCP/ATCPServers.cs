﻿/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// An abstract TCP service allowing to attach multiple TCP servers on different IP sockets.
    /// </summary>
    public abstract class ATCPServers : IEnumerable<TCPServer>
    {

        #region Data

        /// <summary>
        /// The internal TCP servers.
        /// </summary>
        protected readonly List<TCPServer>  _TCPServers;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS defines which DNS servers to use.
        /// </summary>
        public DNSClient                            DNSClient                       { get; }

        /// <summary>
        /// The optional delegate to select a SSL/TLS server certificate.
        /// </summary>
        public ServerCertificateSelectorDelegate    ServerCertificateSelector       { get; }

        /// <summary>
        /// The optional delegate to verify the SSL/TLS client certificate used for authentication.
        /// </summary>
        public RemoteCertificateValidationCallback  ClientCertificateValidator      { get; }

        /// <summary>
        /// The optional delegate to select the SSL/TLS client certificate used for authentication.
        /// </summary>
        public LocalCertificateSelectionCallback    ClientCertificateSelector       { get; }

        /// <summary>
        /// The SSL/TLS protocol(s) allowed for this connection.
        /// </summary>
        public SslProtocols                         AllowedTLSProtocols             { get; }


        #region ServiceName

        private String _ServiceName = TCPServer.__DefaultServiceName;

        /// <summary>
        /// The service banner transmitted to a TCP client
        /// after connection initialization.
        /// </summary>
        public String ServiceName
        {

            get
            {
                return _ServiceName;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                {
                    lock (_TCPServers)
                    {
                        _ServiceName = value;
                        _TCPServers.ForEach(_TCPServer => _TCPServer.ServiceName = value);
                    }
                }
            }

        }

        #endregion

        #region ServiceBanner

        private String _ServiceBanner = TCPServer.__DefaultServiceBanner;

        /// <summary>
        /// The service banner transmitted to a TCP client
        /// after connection initialization.
        /// </summary>
        public String ServiceBanner
        {

            get
            {
                return _ServiceBanner;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                {
                    lock (_TCPServers)
                    {
                        _ServiceBanner = value;
                        _TCPServers.ForEach(_TCPServer => _TCPServer.ServiceBanner = value);
                    }
                }
            }

        }

        #endregion

        #region ServerThreadName

        private String _ServerThreadName  = TCPServer.__DefaultServerThreadName;

        /// <summary>
        /// The name of the TCP service threads.
        /// </summary>
        public String ServerThreadName
        {

            get
            {
                return _ServerThreadName;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                {
                    lock (_TCPServers)
                    {
                        _ServerThreadName = value;
                        _TCPServers.ForEach(_TCPServer => _TCPServer.ServerThreadName = value);
                    }
                }
            }

        }

        #endregion

        #region ServerThreadPriority

        private ThreadPriority _ServerThreadPriority;

        /// <summary>
        /// The priority of the TCP service threads.
        /// </summary>
        public ThreadPriority ServerThreadPriority
        {

            get
            {
                return _ServerThreadPriority;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ServerThreadPriority = value;
                    _TCPServers.ForEach(_ => _.ServerThreadPriority = value);
                }
            }

        }

        #endregion

        #region ServerThreadIsBackground

        private Boolean _ServerThreadIsBackground;

        /// <summary>
        /// Weather the TCP service threads are background or not (default: yes).
        /// </summary>
        public Boolean ServerThreadIsBackground
        {

            get
            {
                return _ServerThreadIsBackground;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ServerThreadIsBackground = value;
                    _TCPServers.ForEach(_ => _.ServerThreadIsBackground = value);
                }
            }

        }

        #endregion


        #region ConnectionIdBuilder

        private ConnectionIdBuilder _ConnectionIdBuilder;

        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder ConnectionIdBuilder
        {

            get
            {
                return _ConnectionIdBuilder;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionIdBuilder = value;
                    _TCPServers.ForEach(_TCPServer => _TCPServer.ConnectionIdBuilder = value);
                }
            }

        }

        #endregion

        #region ConnectionThreadsNameBuilder

        private ConnectionThreadsNameBuilder _ConnectionThreadsNameBuilder;

        /// <summary>
        /// A delegate to set the name of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsNameBuilder ConnectionThreadsNameBuilder
        {

            get
            {
                return _ConnectionThreadsNameBuilder;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionThreadsNameBuilder = value;
                    _TCPServers.ForEach(_ => _.ConnectionThreadsNameBuilder = value);
                }
            }

        }

        #endregion

        #region ConnectionThreadsPriorityBuilder

        private ConnectionThreadsPriorityBuilder _ConnectionThreadsPriorityBuilder;

        /// <summary>
        /// A delegate to set the priority of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsPriorityBuilder ConnectionThreadsPriorityBuilder
        {

            get
            {
                return _ConnectionThreadsPriorityBuilder;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionThreadsPriorityBuilder = value;
                    _TCPServers.ForEach(_TCPServer => _TCPServer.ConnectionThreadsPriorityBuilder = value);
                }
            }

        }

        #endregion

        #region ConnectionThreadsAreBackground

        private Boolean _ConnectionThreadsAreBackground;

        /// <summary>
        /// Whether the TCP connection threads are background threads or not (default: yes).
        /// </summary>
        public Boolean ConnectionThreadsAreBackground
        {

            get
            {
                return _ConnectionThreadsAreBackground;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionThreadsAreBackground = value;
                    _TCPServers.ForEach(_TCPServer => _TCPServer.ConnectionThreadsAreBackground = value);
                }
            }

        }

        #endregion

        #region ConnectionTimeout

        private TimeSpan _ConnectionTimeout  = TCPServer.__DefaultConnectionTimeout;

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan ConnectionTimeout
        {

            get
            {
                return _ConnectionTimeout;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionTimeout = value;
                    _TCPServers.ForEach(_TCPServer => _TCPServer.ConnectionTimeout = value);
                }
            }

        }

        #endregion

        #region MaxClientConnections

        private UInt32 _MaxClientConnections  = TCPServer.__DefaultMaxClientConnections;

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32 MaxClientConnections
        {

            get
            {
                return _MaxClientConnections;
            }

            set
            {
                lock (_TCPServers)
                {
                    _MaxClientConnections = value;
                    _TCPServers.ForEach(_TCPServer => _TCPServer.MaxClientConnections = value);
                }
            }

        }

        #endregion


        #region IsStarted

        private Boolean _IsStarted = false;

        /// <summary>
        /// Is the server already started?
        /// </summary>
        public Boolean IsStarted
        {
            get
            {
                return _IsStarted;
            }
        }

        #endregion

        #region NumberOfClients

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {
                lock (_TCPServers)
                {

                    return _TCPServers.Select(_TCPServer => _TCPServer.NumberOfClients).
                                       Aggregate((a,b) => a + b);

                }
            }
        }

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        public event StartedEventHandler                OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP socket was attached.
        /// </summary>
        public event TCPSocketAttachedHandler           OnTCPSocketAttached;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NewConnectionHandler               OnNewConnection;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler       OnExceptionOccured;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedHandler            OnConnectionClosed;

        /// <summary>
        /// An event fired whenever a new TCP socket was detached.
        /// </summary>
        public event TCPSocketDetachedHandler           OnTCPSocketDetached;

        /// <summary>
        /// An event fired whenever the TCP servers instance was stopped.
        /// </summary>
        public event CompletedEventHandler              OnCompleted;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP service allowing to attach multiple TCP servers on different IP sockets.
        /// </summary>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The service banner transmitted to a TCP client after connection initialization.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadName">An optional name of the TCP server threads.</param>
        /// <param name="ServerThreadPriority">An optional priority of the TCP server threads (default: AboveNormal).</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server threads are a background thread or not (default: yes).</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the TCP server threads immediately (default: no).</param>
        public ATCPServers(String                               ServiceName                        = TCPServer.__DefaultServiceName,
                           String                               ServiceBanner                      = TCPServer.__DefaultServiceBanner,

                           ServerCertificateSelectorDelegate    ServerCertificateSelector          = null,
                           LocalCertificateSelectionCallback    ClientCertificateSelector          = null,
                           RemoteCertificateValidationCallback  ClientCertificateValidator         = null,
                           SslProtocols?                        AllowedTLSProtocols                = SslProtocols.Tls12 | SslProtocols.Tls13,

                           String                               ServerThreadName                   = TCPServer.__DefaultServerThreadName,
                           ThreadPriority?                      ServerThreadPriority               = ThreadPriority.AboveNormal,
                           Boolean?                             ServerThreadIsBackground           = true,
                           ConnectionIdBuilder                  ConnectionIdBuilder                = null,
                           ConnectionThreadsNameBuilder         ConnectionThreadsNameBuilder       = null,
                           ConnectionThreadsPriorityBuilder     ConnectionThreadsPriorityBuilder   = null,
                           Boolean?                             ConnectionThreadsAreBackground     = true,
                           TimeSpan?                            ConnectionTimeout                  = null,
                           UInt32?                              MaxClientConnections               = TCPServer.__DefaultMaxClientConnections,

                           DNSClient                            DNSClient                          = null,
                           Boolean                              Autostart                          = false)

        {

            this._TCPServers  = new List<TCPServer>();
            this.DNSClient = DNSClient;

            // TCP Server
            this.ServerCertificateSelector          = ServerCertificateSelector;
            this.ClientCertificateSelector          = ClientCertificateSelector;
            this.ClientCertificateValidator         = ClientCertificateValidator;
            this.AllowedTLSProtocols                = AllowedTLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13;

            this._ServiceName                       = ServiceName                      ?? TCPServer.__DefaultServiceName;
            this._ServiceBanner                     = ServiceBanner                    ?? TCPServer.__DefaultServiceBanner;

            // Server thread related
            this._ServerThreadName                  = ServerThreadName;
            this._ServerThreadPriority              = ServerThreadPriority             ?? ThreadPriority.AboveNormal;
            this._ServerThreadIsBackground          = ServerThreadIsBackground         ?? true;

            // TCP Connection
            this._ConnectionIdBuilder               = ConnectionIdBuilder              ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP:" + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port);
            this._ConnectionThreadsNameBuilder      = ConnectionThreadsNameBuilder     ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP thread " + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port);
            this._ConnectionThreadsPriorityBuilder  = ConnectionThreadsPriorityBuilder ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => ThreadPriority.AboveNormal);
            this._ConnectionThreadsAreBackground    = ConnectionThreadsAreBackground   ?? true;
            this._ConnectionTimeout                 = ConnectionTimeout                ?? TimeSpan.FromSeconds(30);
            this._MaxClientConnections              = MaxClientConnections             ?? TCPServer.__DefaultMaxClientConnections;

            if (Autostart)
                Start();

        }

        #endregion


        // Manage the underlying TCP sockets...

        #region (protected) AttachTCPPorts  (Action, params Ports)

        protected void AttachTCPPorts(Action<TCPServer> Action,
                                      params IPPort[]    Ports)
        {

            lock (_TCPServers)
            {

                foreach (var Port in Ports)
                {

                    var _TCPServer = _TCPServers.AddAndReturnElement(new TCPServer(Port,
                                                                                   ServerCertificateSelector,
                                                                                   ClientCertificateValidator,
                                                                                   ClientCertificateSelector,
                                                                                   AllowedTLSProtocols,
                                                                                   ServiceName,
                                                                                   ServiceBanner,
                                                                                   _ServerThreadName,
                                                                                   _ServerThreadPriority,
                                                                                   _ServerThreadIsBackground,
                                                                                   _ConnectionIdBuilder,
                                                                                   _ConnectionThreadsNameBuilder,
                                                                                   _ConnectionThreadsPriorityBuilder,
                                                                                   _ConnectionThreadsAreBackground,
                                                                                   _ConnectionTimeout,
                                                                                   _MaxClientConnections,
                                                                                   false));

                    _TCPServer.OnStarted          += (Sender, Timestamp, Message) => SendTCPSocketAttached(Timestamp, _TCPServer.IPSocket, Message);
                    _TCPServer.OnNewConnection    += SendNewConnection;
                    _TCPServer.OnConnectionClosed += SendConnectionClosed;
                    _TCPServer.OnCompleted        += (Sender, Timestamp, Message) => SendTCPSocketDetached(Timestamp, _TCPServer.IPSocket, Message);
                    _TCPServer.OnExceptionOccured += SendExceptionOccured;

                    Action(_TCPServer);

                }

            }

        }

        #endregion

        #region (protected) AttachTCPSockets(Action, params Sockets)

        protected void AttachTCPSockets(Action<TCPServer> Action,
                                        params IPSocket[]  Sockets)
        {

            lock (_TCPServers)
            {

                foreach (var Socket in Sockets)
                {

                    var _TCPServer = _TCPServers.AddAndReturnElement(new TCPServer(Socket,
                                                                                   ServerCertificateSelector,
                                                                                   ClientCertificateValidator,
                                                                                   ClientCertificateSelector,
                                                                                   AllowedTLSProtocols,
                                                                                   ServiceName,
                                                                                   ServiceBanner,
                                                                                   _ServerThreadName,
                                                                                   _ServerThreadPriority,
                                                                                   _ServerThreadIsBackground,
                                                                                   _ConnectionIdBuilder,
                                                                                   _ConnectionThreadsNameBuilder,
                                                                                   _ConnectionThreadsPriorityBuilder,
                                                                                   _ConnectionThreadsAreBackground,
                                                                                   _ConnectionTimeout,
                                                                                   _MaxClientConnections,
                                                                                   false));

                    _TCPServer.OnStarted          += (Sender, Timestamp, Message) => SendTCPSocketAttached(Timestamp, _TCPServer.IPSocket, Message);
                    _TCPServer.OnNewConnection    += SendNewConnection;
                    _TCPServer.OnConnectionClosed += SendConnectionClosed;
                    _TCPServer.OnCompleted        += (Sender, Timestamp, Message) => SendTCPSocketDetached(Timestamp, _TCPServer.IPSocket, Message);
                    _TCPServer.OnExceptionOccured += SendExceptionOccured;

                    Action(_TCPServer);

                    SendTCPSocketAttached(DateTime.UtcNow, _TCPServer.IPSocket);

                }

            }

        }

        #endregion

        #region (protected) DetachTCPPorts  (Action, params Ports)

        protected void DetachTCPPorts(Action<TCPServer> Action,
                                      params IPPort[]    Ports)
        {

            lock (_TCPServers)
            {

                foreach (var Port in Ports)
                {
                    foreach (var _TCPServer in _TCPServers)
                    {
                        if ((IPv4Address) _TCPServer.IPAddress == IPv4Address.Any &&
                                          _TCPServer.Port      == Port)
                        {

                            _TCPServer.OnStarted           -= SendStarted;
                            _TCPServer.OnNewConnection     -= SendNewConnection;
                            _TCPServer.OnConnectionClosed  -= SendConnectionClosed;
                            _TCPServer.OnCompleted         -= SendCompleted;
                            _TCPServer.OnExceptionOccured  -= SendExceptionOccured;

                            Action(_TCPServer);

                        }
                    }
                }

            }

        }

        #endregion

        #region IPPorts

        public IEnumerable<IPPort> IPPorts
            => _TCPServers.SafeSelect(server => server.Port);

        #endregion

        #region GetEnumerator()

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _TCPServers.GetEnumerator();
        }

        public IEnumerator<TCPServer> GetEnumerator()
        {
            return _TCPServers.GetEnumerator();
        }

        #endregion



        // Send events...

        #region (protected) SendStarted(Sender, Timestamp, Message = null)

        protected void SendStarted(Object Sender, DateTime Timestamp, String Message = null)
        {

            OnStarted?.Invoke(Sender,
                              Timestamp,
                              Message);

        }

        #endregion

        #region (protected) SendTCPSocketAttached(Timestamp, TCPSocket, Message = null)

        protected void SendTCPSocketAttached(DateTime Timestamp, IPSocket TCPSocket, String Message = null)
        {

            OnTCPSocketAttached?.Invoke(this,
                                        Timestamp,
                                        TCPSocket,
                                        Message);

        }

        #endregion

        #region (protected) SendNewConnection(TCPServer, Timestamp, RemoteSocket, ConnectionId, TCPConnection)

        protected void SendNewConnection(TCPServer      TCPServer,
                                         DateTime       Timestamp,
                                         IPSocket       RemoteSocket,
                                         String         ConnectionId,
                                         TCPConnection  TCPConnection)
        {

            OnNewConnection?.Invoke(TCPServer,
                                    Timestamp,
                                    RemoteSocket,
                                    ConnectionId,
                                    TCPConnection);

        }

        #endregion

        #region (protected) SendConnectionClosed(TCPServer, ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        protected void SendConnectionClosed(TCPServer           TCPServer,
                                            DateTime            ServerTimestamp,
                                            IPSocket            RemoteSocket,
                                            String              ConnectionId,
                                            ConnectionClosedBy  ClosedBy)
        {

            OnConnectionClosed?.Invoke(TCPServer,
                                       ServerTimestamp,
                                       RemoteSocket,
                                       ConnectionId,
                                       ClosedBy);

        }

        #endregion

        #region (protected) SendTCPSocketDetached(Timestamp, TCPSocket, Message = null)

        protected void SendTCPSocketDetached(DateTime Timestamp, IPSocket TCPSocket, String Message = null)
        {

            OnTCPSocketDetached?.Invoke(this,
                                        Timestamp,
                                        TCPSocket,
                                        Message);

        }

        #endregion

        #region (protected) SendCompleted(Sender, Timestamp, Message = null)

        protected void SendCompleted(Object Sender, DateTime Timestamp, String Message = null)
        {

            OnCompleted?.Invoke(Sender,
                                Timestamp,
                                Message);

        }

        #endregion

        #region (protected) SendExceptionOccured(Sender, Timestamp, Exception)

        protected void SendExceptionOccured(Object Sender, DateTime Timestamp, Exception Exception)
        {

            OnExceptionOccured?.Invoke(Sender,
                                       Timestamp,
                                       Exception);

        }

        #endregion



        // Start/stop the TCP servers...

        #region Start()

        public Boolean Start()
        {

            lock (_TCPServers)
            {

                foreach (var _TCPServer in _TCPServers)
                    _TCPServer.Start();

                _IsStarted = true;

                SendStarted(this, Timestamp.Now);

                return true;

            }

        }

        #endregion

        #region Start(Delay, InBackground = true)

        public Boolean Start(TimeSpan Delay, Boolean InBackground = true)
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Start(Delay, InBackground);

                SendStarted(this, DateTime.UtcNow);

                return true;

            }

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public Boolean Shutdown(String Message = null, Boolean Wait = true)
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Shutdown(Message, Wait);

                SendCompleted(this, DateTime.UtcNow, Message);

                return true;

            }

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {

            lock (_TCPServers)
            {
                foreach (var TCPServer in _TCPServers)
                    TCPServer.Dispose();
            }

        }

        #endregion


    }

}
