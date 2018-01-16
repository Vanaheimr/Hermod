/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    /// <summary>
    /// A multi-threaded Styx arrow sender that listens on a TCP
    /// socket and notifies about incoming TCP connections.
    /// </summary>
    public class TCPServer : IArrowSender<TCPConnection>,
                             IServer
    {

        #region Data

        /// <summary>
        /// The default service banner.
        /// </summary>
        public  const            String                                         __DefaultServiceBanner          = "Vanaheimr Hermod TCP Server v0.9";

        /// <summary>
        /// The default server thread name.
        /// </summary>
        public  const            String                                         __DefaultServerThreadName       = "TCP thread on ";

        /// <summary>
        /// The default maximum number of concurrent TCP client connections.
        /// </summary>
        public  const            UInt32                                         __DefaultMaxClientConnections   = 4096;

        /// <summary>
        /// The default TCP client timeout for all incoming client connections.
        /// </summary>
        public  static readonly  TimeSpan                                       __DefaultConnectionTimeout      = TimeSpan.FromSeconds(30);


        // The TCP listener socket
        private        readonly  TcpListener                                    _TCPListener;

        // Store each connection, in order to be able to stop them activily
        private        readonly  ConcurrentDictionary<IPSocket, TCPConnection>  _TCPConnections;


        // The internal thread
        private        readonly  Thread                                         _ListenerThread;
        private                  CancellationTokenSource                        CancellationTokenSource;
        private                  CancellationToken                              CancellationToken;

        #endregion

        #region Properties

        #region IPAdress

        private readonly IIPAddress _IPAddress;

        /// <summary>
        /// Gets the IPAddress on which the TCP server listens.
        /// </summary>
        public IIPAddress IPAddress
        {
            get
            {
                return _IPAddress;
            }
        }

        #endregion

        #region Port

        private readonly IPPort _Port;

        /// <summary>
        /// Gets the port on which the TCP server listens.
        /// </summary>
        public IPPort Port
        {
            get
            {
                return _Port;
            }
        }

        #endregion

        #region IPSocket

        private readonly IPSocket _IPSocket;

        /// <summary>
        /// Gets the IP socket on which the TCP server listens.
        /// </summary>
        public IPSocket IPSocket
        {
            get
            {
                return _IPSocket;
            }
        }

        #endregion


        #region ServiceBanner

        private String _ServiceBanner  = __DefaultServiceBanner;

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
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

        #region ServerThreadName

        private String _ServerThreadName  = __DefaultServerThreadName;

        /// <summary>
        /// The optional name of the TCP server thread.
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
                    _ServerThreadName = value;
            }

        }

        #endregion

        #region ServerThreadPriority

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        public ThreadPriority ServerThreadPriority { get; set; }

        #endregion

        #region ServerThreadIsBackground

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean ServerThreadIsBackground { get; set; }

        #endregion


        #region ConnectionIdBuilder

        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder ConnectionIdBuilder { get; set; }

        #endregion

        #region ConnectionThreadsNameBuilder

        /// <summary>
        /// A delegate to set the name of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsNameBuilder ConnectionThreadsNameBuilder { get; set; }

        #endregion

        #region ConnectionThreadsPriorityBuilder

        /// <summary>
        /// A delegate to set the priority of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsPriorityBuilder ConnectionThreadsPriorityBuilder { get; set; }

        #endregion

        #region ConnectionThreadsAreBackground

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean ConnectionThreadsAreBackground { get; set; }

        #endregion

        #region ConnectionTimeout

        private TimeSpan _ConnectionTimeout  = __DefaultConnectionTimeout;

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
                if (value > TimeSpan.Zero)
                    _ConnectionTimeout = value;
            }

        }

        #endregion

        #region MaxClientConnections

        private UInt32 _MaxClientConnections  = __DefaultMaxClientConnections;

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
                _MaxClientConnections = value;
            }

        }

        #endregion


        #region NumberOfClients

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {
                return (UInt64) _TCPConnections.LongCount();
            }
        }

        #endregion

        #region IsRunning

        private volatile Boolean _IsRunning = false;

        /// <summary>
        /// True while the TCPServer is listening for new clients
        /// </summary>
        public Boolean IsRunning
        {
            get
            {
                return _IsRunning;
            }
        }

        #endregion

        #region StopRequested

        private volatile Boolean _StopRequested = false;

        /// <summary>
        /// The TCPServer was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean StopRequested
        {
            get
            {
                return _StopRequested;
            }
        }

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        public event StartedEventHandler                            OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// If this event closes the TCP connection the OnNotification event will never be fired!
        /// Therefore you can use this event for filtering connection initiation requests.
        /// </summary>
        public event NewConnectionHandler                           OnNewConnection;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NotificationEventHandler<TCPConnection>        OnNotification;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler                   OnExceptionOccured;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedHandler                        OnConnectionClosed;

        /// <summary>
        /// An event fired whenever the TCP servers instance was stopped.
        /// </summary>
        public event CompletedEventHandler                          OnCompleted;

        #endregion

        #region Constructor(s)

        #region TCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IPPort                            Port,
                         String                            ServiceBanner                     = __DefaultServiceBanner,
                         String                            ServerThreadName                  = null,
                         ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                         Boolean                           ServerThreadIsBackground          = true,
                         ConnectionIdBuilder               ConnectionIdBuilder               = null,
                         ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                         ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                         Boolean                           ConnectionThreadsAreBackground    = true,
                         TimeSpan?                         ConnectionTimeout                 = null,
                         UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                         Boolean                           Autostart                         = false)

            : this(IPv4Address.Any,
                   Port,
                   ServiceBanner,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   Autostart)

        { }

        #endregion

        #region TCPServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IIPAddress                        IIPAddress,
                         IPPort                            Port,
                         String                            ServiceBanner                     = __DefaultServiceBanner,
                         String                            ServerThreadName                  = null,
                         ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                         Boolean                           ServerThreadIsBackground          = true,
                         ConnectionIdBuilder               ConnectionIdBuilder               = null,
                         ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                         ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                         Boolean                           ConnectionThreadsAreBackground    = true,
                         TimeSpan?                         ConnectionTimeout                 = null,
                         UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                         Boolean                           Autostart                         = false)

        {

            #region TCP Socket

            this._IPAddress                         = IIPAddress;
            this._Port                              = Port;
            this._IPSocket                          = new IPSocket(_IPAddress, _Port);
            this._TCPListener                       = new TcpListener(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());

            #endregion

            #region TCP Server

            this._ServiceBanner                     = (ServiceBanner.IsNotNullOrEmpty())
                                                          ? ServiceBanner
                                                          : __DefaultServiceBanner;

            this.ServerThreadName                   = (ServerThreadName != null)
                                                          ? ServerThreadName
                                                          : __DefaultServerThreadName + this.IPSocket.ToString();

            this.ServerThreadPriority               = ServerThreadPriority;
            this.ServerThreadIsBackground           = ServerThreadIsBackground;

            #endregion

            #region TCP Connections

            this._TCPConnections                    = new ConcurrentDictionary<IPSocket, TCPConnection>();


            this.ConnectionIdBuilder                = (ConnectionIdBuilder              != null)
                                                          ? ConnectionIdBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP:" + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port;

            this.ConnectionThreadsNameBuilder       = (ConnectionThreadsNameBuilder     != null)
                                                          ? ConnectionThreadsNameBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP thread " + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port;

            this.ConnectionThreadsPriorityBuilder   = (ConnectionThreadsPriorityBuilder != null)
                                                          ? ConnectionThreadsPriorityBuilder
                                                          : (Sender, Timestamp, LocalSocket, RemoteIPSocket) => ThreadPriority.AboveNormal;

            this.ConnectionThreadsAreBackground     = ConnectionThreadsAreBackground;

            this._ConnectionTimeout                 = ConnectionTimeout.HasValue
                                                          ? ConnectionTimeout.Value
                                                          : TimeSpan.FromSeconds(30);

            this._MaxClientConnections              = MaxClientConnections;

            #endregion

            #region TCP Listener Thread

            this.CancellationTokenSource            = new CancellationTokenSource();
            this.CancellationToken                  = CancellationTokenSource.Token;

            _ListenerThread = new Thread(() => {

#if __MonoCS__
                // Code for Mono C# compiler
#else
                Thread.CurrentThread.Name           = this.ServerThreadName;
                Thread.CurrentThread.Priority       = this.ServerThreadPriority;
                Thread.CurrentThread.IsBackground   = this.ServerThreadIsBackground;
#endif

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

                    _IsRunning = true;

                    while (!_StopRequested)
                    {

                        // Wait for a new/pending client connection
                        while (!_StopRequested && !_TCPListener.Pending())
                            Thread.Sleep(5);

                        // Break when a server stop was requested
                        if (_StopRequested)
                            break;

                        // Processing the pending client connection within its own task
                        var NewTCPClient = _TCPListener.AcceptTcpClient();
                      //  var NewTCPConnection = _TCPListener.AcceptTcpClientAsync().
                      //                                      ContinueWith(a => new TCPConnection(this, a.Result));
                      //                                  //    ConfigureAwait(false);

                        // Store the new connection
                        //_SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                        //                               _TCPConnection.Value,
                        //                               (RemoteEndPoint, TCPConnection) => TCPConnection);

                        Task.Factory.StartNew(Tuple => {

                            try
                            {

                                var _Tuple           = Tuple as Tuple<TCPServer, TcpClient>;

                                var NewTCPConnection = new ThreadLocal<TCPConnection>(
                                                           () => new TCPConnection(_Tuple.Item1, _Tuple.Item2)
                                                       );

                                #region Copy ExceptionOccured event handlers

                                //foreach (var ExceptionOccuredHandler in MyEventStorage)
                                //    _TCPConnection.Value.OnExceptionOccured += ExceptionOccuredHandler;

                                #endregion

                                #region OnNewConnection

                                // If this event closes the TCP connection the OnNotification event will never be fired!
                                // Therefore you can use this event for filtering connection initiation requests.
                                OnNewConnection?.Invoke(NewTCPConnection.Value.TCPServer,
                                                        NewTCPConnection.Value.ServerTimestamp,
                                                        NewTCPConnection.Value.RemoteSocket,
                                                        NewTCPConnection.Value.ConnectionId,
                                                        NewTCPConnection.Value);

                                if (!NewTCPConnection.Value.IsClosed)
                                    OnNotification?.Invoke(NewTCPConnection.Value);

                                #endregion

                            }
                            catch (Exception e)
                            {

                                while (e.InnerException != null)
                                    e = e.InnerException;

                                OnExceptionOccured?.Invoke(this, DateTime.UtcNow, e);
                                Console.WriteLine(DateTime.UtcNow + " " + e.Message + Environment.NewLine + e.StackTrace);

                            }

                        }, new Tuple<TCPServer, TcpClient>(this, NewTCPClient));

                    }

                    #region Shutdown

                    // Request all client connections to finish!
                    foreach (var _SocketConnection in _TCPConnections)
                        _SocketConnection.Value.StopRequested = true;

                    // After stopping the TCPListener wait for
                    // all client connections to finish!
                    while (_TCPConnections.Count > 0)
                        Thread.Sleep(5);

                    #endregion

                }

                #region Exception handling

                catch (Exception Exception)
                {
                    var OnExceptionLocal = OnExceptionOccured;
                    if (OnExceptionLocal != null)
                        OnExceptionLocal(this, DateTime.UtcNow, Exception);
                }

                #endregion

                _IsRunning = false;

            });

            #endregion

            if (Autostart)
                Start();

        }

        #endregion

        #region TCPServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="Autostart">Start the TCP server thread immediately (default: no).</param>
        public TCPServer(IPSocket                          IPSocket,
                         String                            ServiceBanner                     = __DefaultServiceBanner,
                         String                            ServerThreadName                  = null,
                         ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                         Boolean                           ServerThreadIsBackground          = true,
                         ConnectionIdBuilder               ConnectionIdBuilder               = null,
                         ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                         ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                         Boolean                           ConnectionThreadsAreBackground    = true,
                         TimeSpan?                         ConnectionTimeout                 = null,
                         UInt32                            MaxClientConnections              = __DefaultMaxClientConnections,
                         Boolean                           Autostart                         = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceBanner,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   Autostart)

        { }

        #endregion

        #endregion


        #region (protected internal) SendNewConnection(ServerTimestamp, RemoteSocket, ConnectionId, TCPConnection)

        protected internal void SendNewConnection(DateTime       ServerTimestamp,
                                                  IPSocket       RemoteSocket,
                                                  String         ConnectionId,
                                                  TCPConnection  TCPConnection)
        {

            OnNewConnection?.Invoke(this,
                                    ServerTimestamp,
                                    RemoteSocket,
                                    ConnectionId,
                                    TCPConnection);

        }

        #endregion

        #region (protected internal) SendConnectionClosed(ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        protected internal void SendConnectionClosed(DateTime            ServerTimestamp,
                                                     IPSocket            RemoteSocket,
                                                     String              ConnectionId,
                                                     ConnectionClosedBy  ClosedBy)
        {

            OnConnectionClosed?.Invoke(this,
                                       ServerTimestamp,
                                       RemoteSocket,
                                       ConnectionId,
                                       ClosedBy);

        }

        #endregion



        #region Start()

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public void Start()
        {
            Start(__DefaultMaxClientConnections);
        }

        #endregion

        #region Start(Delay, InBackground = true)

        /// <summary>
        /// Start the UDP receiver after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public void Start(TimeSpan Delay, Boolean InBackground = true)
        {

            if (!InBackground)
            {
                Thread.Sleep(Delay);
                Start();
            }

            else
                Task.Factory.StartNew(() => {

                    Thread.Sleep(Delay);
                    Start();

                }, CancellationTokenSource.Token,
                   TaskCreationOptions.AttachedToParent,
                   TaskScheduler.Default);

        }

        #endregion

        #region Start(MaxClientConnections)

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public void Start(UInt32 MaxClientConnections)
        {

            if (_IsRunning)
                return;

            if (MaxClientConnections != __DefaultMaxClientConnections)
                _MaxClientConnections = MaxClientConnections;

            try
            {

                DebugX.LogT("Starting TCP listener on port " + _Port);

                _TCPListener.Start((Int32) _MaxClientConnections);

            }
            catch (Exception e)
            {
                DebugX.LogT("An exception occured in Hermod.TCPServer.Start(MaxClientConnections) [_TCPListener.Start((Int32) _MaxClientConnections)]: " + e.Message + Environment.NewLine + e.StackTrace);
            }

            try
            {

                if (_ListenerThread == null)
                    DebugX.LogT("An exception occured in Hermod.TCPServer.Start(MaxClientConnections) [_ListenerThread == null]!");

                // Start the TCPListenerThread
                _ListenerThread.Start();

            }
            catch (Exception e)
            {
                DebugX.LogT("An exception occured in Hermod.TCPServer.Start(MaxClientConnections) [_ListenerThread.Start()]: " + e.Message + Environment.NewLine + e.StackTrace);
            }


            // Wait until socket has opened
            while (!_IsRunning)
                Thread.Sleep(10);

            OnStarted?.Invoke(this, DateTime.UtcNow);

        }

        #endregion


        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the TCP listener.
        /// </summary>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        /// <param name="Message">An optional shutdown message.</param>
        public void Shutdown(String  Message  = null,
                             Boolean Wait     = true)
        {

            _StopRequested = true;

            if (_TCPListener != null)
                _TCPListener.Stop();

            //if (Wait)
            //    while (_IsRunning > 0)
            //        Thread.Sleep(10);

            var OnCompletedLocal = OnCompleted;
            if (OnCompletedLocal != null)
                OnCompletedLocal(this, DateTime.UtcNow, (Message != null) ? Message : String.Empty);

        }

        #endregion

        #region StopAndWait()

        /// <summary>
        /// Stop the TCPServer and wait until all connections are closed.
        /// </summary>
        public void StopAndWait()
        {

            _StopRequested = true;

            while (_TCPConnections.Count > 0)
                Thread.Sleep(10);

            if (_TCPListener != null)
                _TCPListener.Stop();

        }

        #endregion


        #region IDisposable Members

        public void Dispose()
        {

            StopAndWait();

            if (_TCPListener != null)
                _TCPListener.Stop();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            var _Type              = this.GetType();
            var _GenericArguments  = _Type.GetGenericArguments();
            var _TypeName          = (_GenericArguments.Length > 0) ? _Type.Name.Remove(_Type.Name.Length - 2) : _Type.Name;
            var _GenericType       = (_GenericArguments.Length > 0) ? "<" + _GenericArguments[0].Name + ">"    : String.Empty;
            var _Running           = (IsRunning)                    ? " (running)"                             : String.Empty;

            return String.Concat(ServiceBanner, " [", _TypeName, _GenericType, "] on ", _IPSocket.ToString(), _Running);

        }

        #endregion

    }

}
