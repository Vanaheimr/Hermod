/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using System.Security.Cryptography.X509Certificates;
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

        #region DNSClient

        private readonly DNSClient _DNSClient;

        /// <summary>
        /// The DNS server to use.
        /// </summary>
        public DNSClient DNSClient
        {
            get
            {
                return _DNSClient;
            }
        }

        #endregion

        #region ServiceBanner

        private String _ServiceBanner  = TCPServer.__DefaultServiceBanner;

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
                    _ServiceBanner = value;
            }

        }

        #endregion

        #region X509Certificate

        private readonly X509Certificate2 _X509Certificate;

        /// <summary>
        /// The X509 certificate.
        /// </summary>
        public X509Certificate2 X509Certificate
        {
            get
            {
                return _X509Certificate;
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
        /// <param name="ServiceBanner">The service banner transmitted to a TCP client after connection initialization.</param>
        /// <param name="X509Certificate">Use this X509 certificate for TLS.</param>
        /// <param name="ServerThreadName">An optional name of the TCP server threads.</param>
        /// <param name="ServerThreadPriority">An optional priority of the TCP server threads (default: AboveNormal).</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server threads are a background thread or not (default: yes).</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the TCP server threads immediately (default: no).</param>
        public ATCPServers(String                            ServiceBanner                     = TCPServer.__DefaultServiceBanner,
                           X509Certificate2                  X509Certificate                   = null,
                           String                            ServerThreadName                  = TCPServer.__DefaultServerThreadName,
                           ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                           Boolean                           ServerThreadIsBackground          = true,
                           ConnectionIdBuilder               ConnectionIdBuilder               = null,
                           ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                           ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                           Boolean                           ConnectionThreadsAreBackground    = true,
                           TimeSpan?                         ConnectionTimeout                 = null,
                           UInt32                            MaxClientConnections              = TCPServer.__DefaultMaxClientConnections,
                           DNSClient                         DNSClient                         = null,
                           Boolean                           Autostart                         = false)

        {

            this._TCPServers  = new List<TCPServer>();
            this._DNSClient   = DNSClient;

            #region TCP Server

            this._ServiceBanner                    = ServiceBanner;
            this._X509Certificate                  = X509Certificate;

            #endregion

            #region Server thread related

            this._ServerThreadName                 = ServerThreadName;
            this._ServerThreadPriority             = ServerThreadPriority;
            this._ServerThreadIsBackground         = ServerThreadIsBackground;

            #endregion

            #region TCP Connection

            this._ConnectionIdBuilder              = (ConnectionIdBuilder               != null)
                                                          ? ConnectionIdBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP:" + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port;

            this._ConnectionThreadsNameBuilder     = (ConnectionThreadsNameBuilder      != null)
                                                          ? ConnectionThreadsNameBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP thread " + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port;

            this._ConnectionThreadsPriorityBuilder  = (ConnectionThreadsPriorityBuilder != null)
                                                          ? ConnectionThreadsPriorityBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => ThreadPriority.AboveNormal;

            this._ConnectionThreadsAreBackground   = ConnectionThreadsAreBackground;
            this._ConnectionTimeout                = ConnectionTimeout.HasValue ? ConnectionTimeout.Value : TimeSpan.FromSeconds(30);

            #endregion

            if (Autostart)
                Start();

        }

        #endregion


        // Manage the underlying TCP sockets...

        #region (protected) AttachTCPPorts(Action, params Ports)

        protected void AttachTCPPorts(Action<TCPServer> Action,
                                      params IPPort[]    Ports)
        {

            lock (_TCPServers)
            {

                foreach (var Port in Ports)
                {

                    var _TCPServer = _TCPServers.AddAndReturnElement(new TCPServer(Port,
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

                    SendTCPSocketAttached(DateTime.Now, _TCPServer.IPSocket);

                }

            }

        }

        #endregion

        #region (protected) DetachTCPPorts(Action, params Ports)

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

            var OnStartedLocal = OnStarted;
            if (OnStartedLocal != null)
                OnStartedLocal(Sender, Timestamp, Message);

        }

        #endregion

        #region (protected) SendTCPSocketAttached(Timestamp, TCPSocket, Message = null)

        protected void SendTCPSocketAttached(DateTime Timestamp, IPSocket TCPSocket, String Message = null)
        {

            var OnTCPSocketAttachedLocal = OnTCPSocketAttached;
            if (OnTCPSocketAttachedLocal != null)
                OnTCPSocketAttachedLocal(this, Timestamp, TCPSocket, Message);

        }

        #endregion

        #region (protected) SendNewConnection(TCPServer, Timestamp, RemoteSocket, ConnectionId, TCPConnection)

        protected void SendNewConnection(TCPServer      TCPServer,
                                         DateTime       Timestamp,
                                         IPSocket       RemoteSocket,
                                         String         ConnectionId,
                                         TCPConnection  TCPConnection)
        {

            var OnNewConnectionLocal = OnNewConnection;
            if (OnNewConnectionLocal != null)
                OnNewConnectionLocal(TCPServer, Timestamp, RemoteSocket, ConnectionId, TCPConnection);

        }

        #endregion

        #region (protected) SendConnectionClosed(TCPServer, ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        protected void SendConnectionClosed(TCPServer           TCPServer,
                                            DateTime            ServerTimestamp,
                                            IPSocket            RemoteSocket,
                                            String              ConnectionId,
                                            ConnectionClosedBy  ClosedBy)
        {

            var OnConnectionClosedLocal = OnConnectionClosed;
            if (OnConnectionClosedLocal != null)
                OnConnectionClosedLocal(TCPServer, ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy);

        }

        #endregion

        #region (protected) SendTCPSocketDetached(Timestamp, TCPSocket, Message = null)

        protected void SendTCPSocketDetached(DateTime Timestamp, IPSocket TCPSocket, String Message = null)
        {

            var OnTCPSocketDetachedLocal = OnTCPSocketDetached;
            if (OnTCPSocketDetachedLocal != null)
                OnTCPSocketDetachedLocal(this, Timestamp, TCPSocket, Message);

        }

        #endregion

        #region (protected) SendCompleted(Sender, Timestamp, Message = null)

        protected void SendCompleted(Object Sender, DateTime Timestamp, String Message = null)
        {

            var OnCompletedLocal = OnCompleted;
            if (OnCompletedLocal != null)
                OnCompletedLocal(Sender, Timestamp, Message);

        }

        #endregion

        #region (protected) SendExceptionOccured(Sender, Timestamp, Exception)

        protected void SendExceptionOccured(Object Sender, DateTime Timestamp, Exception Exception)
        {

            var OnExceptionOccuredLocal = OnExceptionOccured;
            if (OnExceptionOccuredLocal != null)
                OnExceptionOccuredLocal(Sender, Timestamp, Exception);

        }

        #endregion



        // Start/stop the TCP servers...

        #region Start()

        public void Start()
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Start();

                _IsStarted = true;

                SendStarted(this, DateTime.Now);

            }

        }

        #endregion

        #region Start(Delay, InBackground = true)

        public void Start(TimeSpan Delay, Boolean InBackground = true)
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Start(Delay, InBackground);

                SendStarted(this, DateTime.Now);

            }

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public void Shutdown(String Message = null, Boolean Wait = true)
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Shutdown(Message, Wait);

                SendCompleted(this, DateTime.Now, Message);

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
