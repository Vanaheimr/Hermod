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
using System.Threading;

using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    public abstract class ACustomTCPServer : ITCPServer
    {

        #region Data

        protected readonly TCPServer _TCPServer;

        #endregion

        #region Properties

        public String ServerThreadName
        {
            get
            {
                return _TCPServer.ServerThreadName;
            }
            set
            {
                _TCPServer.ServerThreadName = value;
            }
        }

        public bool StopRequested
        {
            get
            {
                return _TCPServer.StopRequested;
            }
        }

        public ThreadPriority ServerThreadPriority
        {
            get
            {
                return _TCPServer.ServerThreadPriority;
            }
            set
            {
                _TCPServer.ServerThreadPriority = value;
            }
        }

        public bool ServerThreadIsBackground
        {
            get
            {
                return _TCPServer.ServerThreadIsBackground;
            }
            set
            {
                _TCPServer.ServerThreadIsBackground = value;
            }
        }

        public Func<IPSocket, String> ConnectionIdBuilder
        {
            get
            {
                return _TCPServer.ConnectionIdBuilder;
            }
            set
            {
                _TCPServer.ConnectionIdBuilder = value;
            }
        }

        public Func<TCPConnection, String> ConnectionThreadsNameCreator
        {
            get
            {
                return _TCPServer.ConnectionThreadsNameCreator;
            }
            set
            {
                _TCPServer.ConnectionThreadsNameCreator = value;
            }
        }

        public ThreadPriority ConnectionThreadsPriority
        {
            get
            {
                return _TCPServer.ConnectionThreadsPriority;
            }
            set
            {
                _TCPServer.ConnectionThreadsPriority = value;
            }
        }

        public Boolean ConnectionThreadsAreBackground
        {
            get
            {
                return _TCPServer.ConnectionThreadsAreBackground;
            }
            set
            {
                _TCPServer.ConnectionThreadsAreBackground = value;
            }
        }

        public TimeSpan ConnectionTimeout
        {
            get
            {
                return _TCPServer.ConnectionTimeout;
            }
            set
            {
                _TCPServer.ConnectionTimeout = value;
            }
        }

        public ulong NumberOfClients
        {
            get
            {
                return _TCPServer.NumberOfClients;
            }
        }

        public uint MaxClientConnections
        {
            get
            {
                return _TCPServer.MaxClientConnections;
            }
        }

        public bool IsRunning
        {
            get
            {
                return _TCPServer.IsRunning;
            }
        }

        public IIPAddress IPAddress
        {
            get
            {
                return _TCPServer.IPAddress;
            }
        }

        public IPPort Port
        {
            get
            {
                return _TCPServer.Port;
            }
        }

        public IPSocket IPSocket
        {
            get
            {
                return _TCPServer.IPSocket;
            }
        }

        public string ServiceBanner
        {
            get
            {
                return _TCPServer.ServiceBanner;
            }
            set
            {
                _TCPServer.ServiceBanner = value;
            }
        }

        #endregion

        #region Constructor(s)

        #region ACustomTCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds.</param>
        public ACustomTCPServer(IPPort                       Port,
                                String                       ServerThreadName                = null,
                                ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                                Boolean                      ServerThreadIsBackground        = true,
                                Func<IPSocket, String>       ConnectionIdBuilder             = null,
                                Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                                ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                                Boolean                      ConnectionThreadsAreBackground  = true,
                                TimeSpan?                    ConnectionTimeout               = null)

            : this(IPv4Address.Any,
                   Port,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout)

        { }

        #endregion

        #region ACustomTCPServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds.</param>
        public ACustomTCPServer(IIPAddress                   IIPAddress,
                                IPPort                       Port,
                                String                       ServerThreadName                = null,
                                ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                                Boolean                      ServerThreadIsBackground        = true,
                                Func<IPSocket, String>       ConnectionIdBuilder             = null,
                                Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                                ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                                Boolean                      ConnectionThreadsAreBackground  = true,
                                TimeSpan?                    ConnectionTimeout               = null)
        {

            this._TCPServer = new TCPServer(IIPAddress,
                                            Port,
                                            ServerThreadName,
                                            ServerThreadPriority,
                                            ServerThreadIsBackground,
                                            ConnectionIdBuilder,
                                            ConnectionThreadsNameCreator,
                                            ConnectionThreadsPriority,
                                            ConnectionThreadsAreBackground,
                                            ConnectionTimeout,
                                            false);

        }

        #endregion

        #region ACustomTCPServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds.</param>
        public ACustomTCPServer(IPSocket                     IPSocket,
                         String                       ServerThreadName                = null,
                         ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                      ServerThreadIsBackground        = true,
                         Func<IPSocket, String>       ConnectionIdBuilder             = null,
                         Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                      ConnectionThreadsAreBackground  = true,
                         TimeSpan?                    ConnectionTimeout               = null)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout)

        { }

        #endregion

        #endregion

        #region Events

        public event StartedEventHandler OnStarted
        {
            add
            {
                _TCPServer.OnStarted += value;
            }
            remove
            {
                _TCPServer.OnStarted -= value;
            }
        }

        public event NewConnectionHandler OnNewConnection;

        public event CompletedEventHandler OnCompleted
        {
            add
            {
                _TCPServer.OnCompleted += value;
            }
            remove
            {
                _TCPServer.OnCompleted -= value;
            }
        }

        public event ConnectionClosedHandler OnConnectionClosed;


        public event ExceptionOccuredEventHandler OnExceptionOccured
        {
            add
            {
                _TCPServer.OnExceptionOccured += value;
            }
            remove
            {
                _TCPServer.OnExceptionOccured -= value;
            }
        }

        #endregion




        protected internal void SendNewConnection(ITCPServer     TCPServer,
                                                  DateTime       Timestamp,
                                                  TCPConnection  TCPConnection)
        {

            var OnNewConnectionLocal = OnNewConnection;
            if (OnNewConnectionLocal != null)
                OnNewConnectionLocal(TCPServer, Timestamp, TCPConnection);

        }

        protected internal void SendConnectionClosed(ITCPServer          TCPServer,
                                                     DateTime            ServerTimestamp,
                                                     IPSocket            RemoteSocket,
                                                     String              ConnectionId,
                                                     ConnectionClosedBy  ClosedBy)
        {

            var OnConnectionClosedLocal = OnConnectionClosed;
            if (OnConnectionClosedLocal != null)
                OnConnectionClosedLocal(TCPServer, ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy);

        }







        public void Start()
        {
            _TCPServer.Start();
        }

        public void Start(TimeSpan Delay, bool InBackground = true)
        {
            _TCPServer.Start(Delay, InBackground);
        }



        public void Shutdown(string Message = null, bool Wait = true)
        {
            _TCPServer.Shutdown(Message, Wait);
        }



        public void Dispose()
        {
            _TCPServer.Dispose();
        }

    }

}
