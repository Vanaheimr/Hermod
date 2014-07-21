/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    public abstract class ACustomTCPServers
    {

        #region Data

        protected readonly List<TCPServer>  _TCPServers;
        protected const    String           DefaultServiceBanner = "Vanaheimr Hermod TCP Server v0.9";

        #endregion

        #region Properties

        #region ServiceBanner

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
        /// </summary>
        public String ServiceBanner { get; set; }

        #endregion

        #region ServerThreadName

        private String _ServerThreadName;

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
                        _TCPServers.ForEach(_ => _.ServerThreadName = value);
                    }
                }
            }

        }

        #endregion

        #region ServerThreadPriority

        private ThreadPriority _ServerThreadPriority;

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

        private Func<IPSocket, String> _ConnectionIdBuilder;

        public Func<IPSocket, String> ConnectionIdBuilder
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
                    _TCPServers.ForEach(_ => _.ConnectionIdBuilder = value);
                }
            }

        }

        #endregion

        #region ConnectionIdBuilder

        private Func<TCPConnection, String> _ConnectionThreadsNameCreator;

        public Func<TCPConnection, String> ConnectionThreadsNameCreator
        {

            get
            {
                return _ConnectionThreadsNameCreator;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionThreadsNameCreator = value;
                    _TCPServers.ForEach(_ => _.ConnectionThreadsNameCreator = value);
                }
            }

        }

        #endregion

        #region ConnectionThreadsPriority

        private ThreadPriority _ConnectionThreadsPriority;

        public ThreadPriority ConnectionThreadsPriority
        {

            get
            {
                return _ConnectionThreadsPriority;
            }

            set
            {
                lock (_TCPServers)
                {
                    _ConnectionThreadsPriority = value;
                    _TCPServers.ForEach(_ => _.ConnectionThreadsPriority = value);
                }
            }

        }

        #endregion

        #region ConnectionThreadsAreBackground

        private Boolean _ConnectionThreadsAreBackground;

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
                    _TCPServers.ForEach(_ => _.ConnectionThreadsAreBackground = value);
                }
            }

        }

        #endregion

        #region ConnectionTimeout

        private TimeSpan _ConnectionTimeout;

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
                    _TCPServers.ForEach(_ => _.ConnectionTimeout = value);
                }
            }

        }

        #endregion

        //public ulong NumberOfClients
        //{
        //    get
        //    {
        //        return _NumberOfClients;
        //    }
        //}

        //public uint MaxClientConnections
        //{
        //    get
        //    {
        //        return _MaxClientConnections;
        //    }
        //}

        //public bool IsRunning
        //{
        //    get
        //    {
        //        return _IsRunning;
        //    }
        //}

        //public bool StopRequested
        //{
        //    get
        //    {
        //        return _StopRequested;
        //    }
        //}

        #endregion

        #region Events

        public event StartedEventHandler                                                    OnStarted;

        public event TCPSocketAttachedHandler                                               OnTCPSocketAttached;

        public event NewConnectionHandler                                                   OnNewConnection;

        public event ConnectionClosedHandler                                                OnConnectionClosed;

        public event TCPSocketDetachedHandler                                               OnTCPSocketDetached;

        public event CompletedEventHandler                                                  OnCompleted;

        public event ExceptionOccuredEventHandler                                           OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initialize the custom TCP server using the given parameters.
        /// </summary>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The TCP client timeout for all incoming client connections in seconds.</param>
        public ACustomTCPServers(String                       ServiceBanner                   = DefaultServiceBanner,
                                 String                       ServerThreadName                = null,
                                 ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                                 Boolean                      ServerThreadIsBackground        = true,
                                 Func<IPSocket, String>       ConnectionIdBuilder             = null,
                                 Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                                 ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                                 Boolean                      ConnectionThreadsAreBackground  = true,
                                 TimeSpan?                    ConnectionTimeout               = null)

        {

            this._TCPServers                       = new List<TCPServer>();
            this.ServiceBanner                     = ServiceBanner;
            this._ServerThreadName                 = ServerThreadName;
            this._ServerThreadPriority             = ServerThreadPriority;
            this._ServerThreadIsBackground         = ServerThreadIsBackground;
            this._ConnectionIdBuilder              = ConnectionIdBuilder;
            this._ConnectionThreadsNameCreator     = ConnectionThreadsNameCreator;
            this._ConnectionThreadsPriority        = ConnectionThreadsPriority;
            this._ConnectionThreadsAreBackground   = ConnectionThreadsAreBackground;
            this._ConnectionTimeout                = ConnectionTimeout.HasValue ? ConnectionTimeout.Value : TimeSpan.FromSeconds(30);

        }

        #endregion


        // Underlying TCP sockets...

        #region (protected) _AttachTCPPorts(Action, params Ports)

        protected void _AttachTCPPorts(Action<TCPServer>  Action,
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
                                                                                   _ConnectionThreadsNameCreator,
                                                                                   _ConnectionThreadsPriority,
                                                                                   _ConnectionThreadsAreBackground,
                                                                                   _ConnectionTimeout,
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

        #region (protected) _AttachTCPSockets(Action, params Sockets)

        public void _AttachTCPSockets(Action<TCPServer>  Action,
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
                                                                                   _ConnectionThreadsNameCreator,
                                                                                   _ConnectionThreadsPriority,
                                                                                   _ConnectionThreadsAreBackground,
                                                                                   _ConnectionTimeout,
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


        #region (protected) _DetachTCPPorts(Action, params Ports)

        protected void _DetachTCPPorts(Action<TCPServer>  Action,
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


        // Events...

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

        #region (protected) SendNewConnection(TCPServer, Timestamp, TCPConnection)

        protected void SendNewConnection(ITCPServer     TCPServer,
                                         DateTime       Timestamp,
                                         TCPConnection  TCPConnection)
        {

            var OnNewConnectionLocal = OnNewConnection;
            if (OnNewConnectionLocal != null)
                OnNewConnectionLocal(TCPServer, Timestamp, TCPConnection);

        }

        #endregion

        #region (protected) SendConnectionClosed(TCPServer, ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        protected void SendConnectionClosed(ITCPServer          TCPServer,
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







        // Start/Stop the HTTP server

        #region Start()

        public void Start()
        {

            lock (_TCPServers)
            {

                foreach (var TCPServer in _TCPServers)
                    TCPServer.Start();

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
