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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    #region TCPServer<TCPConnection>

    /// <summary>
    /// A multi-threaded Styx arrow sender that listens on a TCP
    /// socket and notifies about incoming TCP connections.
    /// </summary>
    /// <typeparam name="TData">The type of the Styx arrows to send.</typeparam>
    public class TCPServer<TData> : ITCPServer<TData>
    {

        #region Data

        private const String DefaultServiceBanner = "Vanaheimr Hermod TCP Server v0.9";

        private readonly Func<TCPConnection<TData>, String>  PacketThreadName;
        private readonly MapperDelegate                      Mapper; 

        // The internal thread
        private readonly Thread                                                _ListenerThread;

        // The TCP listener socket
        private readonly TcpListener                                           _TCPListener;

        // Store each connection, in order to be able to stop them activily
        private readonly ConcurrentDictionary<IPSocket, TCPConnection<TData>>  _SocketConnections;

        // The constructor for TCPConnectionType
        //private readonly ConstructorInfo                                       _Constructor;

        private          CancellationTokenSource  CancellationTokenSource;
        private          CancellationToken        CancellationToken;

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


        #region ServerThreadName

        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        public String ServerThreadName { get; set; }

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
        /// The optional name of the TCP server thread.
        /// </summary>
        public Func<IPSocket, String> ConnectionIdBuilder { get; set; }

        #endregion

        #region ConnectionThreadsNameCreator

        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        public String ConnectionThreadsNameCreator { get; set; }

        #endregion

        #region ConnectionThreadsPriority

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        public ThreadPriority ConnectionThreadsPriority { get; set; }

        #endregion

        #region ConnectionThreadsAreBackground

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean ConnectionThreadsAreBackground { get; set; }

        #endregion

        #region ConnectionTimeout

        /// <summary>
        /// The tcp client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; }

        #endregion


        #region ServiceBanner

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
        /// </summary>
        public String ServiceBanner { get; set; }

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

        #region NumberOfClients

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {
                return (UInt32) _SocketConnections.Count;
            }
        }

        #endregion

        #region MaxClientConnections

        private const UInt32 _DefaultMaxClientConnections = 5000;
        
        private UInt32 _MaxClientConnections = _DefaultMaxClientConnections;

        /// <summary>
        /// The maximum number of pending client connections
        /// </summary>
        public UInt32 MaxClientConnections
        {
            get
            {
                return _MaxClientConnections;
            }
        }

        #endregion

        #endregion

        #region Events

        #region OnStarted

        /// <summary>
        /// An event fired when the TCP server started.
        /// </summary>
        public event StartedEventHandler OnStarted;

        #endregion

        #region OnNotification

        /// <summary>
        /// An event fired for every incoming TCP connection.
        /// </summary>
        public event NotificationEventHandler<TCPConnection<TData>> OnNotification;

        #endregion

        #region OnExceptionOccured

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler OnExceptionOccured;

        #endregion

        #region OnCompleted

        /// <summary>
        /// An event fired when the TCP server stopped.
        /// </summary>
        public event CompletedEventHandler OnCompleted;

        #endregion

        #endregion

        #region MapperDelegate

        /// <summary>
        /// A delegate to transform the incoming TCP connection data into custom data structures.
        /// </summary>
        /// <param name="TCPServer">The TCP server.</param>
        /// <param name="Timestamp">The server timestamp of the TCP connection.</param>
        /// <param name="LocalSocket">The local TCP socket.</param>
        /// <param name="RemoteSocket">The remote TCP socket.</param>
        /// <param name="Payload">The payload of the TCP connection.</param>
        /// <returns>The payload/message of the TCP connection transformed into custom data structures.</returns>
        public delegate TData MapperDelegate(TCPServer<TData>  TCPServer,
                                             DateTime          Timestamp,
                                             IPSocket          LocalSocket,
                                             IPSocket          RemoteSocket,
                                             Byte[]            Payload);

        #endregion

        #region Constructor(s)

        #region TCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IPPort                              Port,
                         MapperDelegate                      Mapper,
                         String                              ServerThreadName                = null,
                         ThreadPriority                      ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                             ServerThreadIsBackground        = true,
                         Func<IPSocket, String>              ConnectionIdBuilder             = null,
                         Func<TCPConnection<TData>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                      ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                             ConnectionThreadsAreBackground  = true,
                         UInt64                              ConnectionTimeoutSeconds        = 30,
                         Boolean                             Autostart                       = false)

            : this(IPv4Address.Any,
                   Port,
                   Mapper,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #region TCPServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IIPAddress                          IIPAddress,
                         IPPort                              Port,
                         MapperDelegate                      Mapper,
                         String                              ServerThreadName                = null,
                         ThreadPriority                      ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                             ServerThreadIsBackground        = true,
                         Func<IPSocket, String>              ConnectionIdBuilder             = null,
                         Func<TCPConnection<TData>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                      ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                             ConnectionThreadsAreBackground  = true,
                         UInt64                              ConnectionTimeoutSeconds        = 30,
                         Boolean                             Autostart                       = false)
        {

            if (Mapper == null)
                throw new ArgumentNullException("The mapper delegate must not be null!");

            this._IPAddress                         = IIPAddress;
            this._Port                              = Port;
            this._IPSocket                          = new IPSocket(_IPAddress, _Port);
            this.Mapper                             = Mapper;

            this.ServerThreadName                   = (ServerThreadName != null)
                                                          ? ServerThreadName
                                                          : "TCP server on " + this.IPSocket.ToString();
            this.ServerThreadPriority               = ServerThreadPriority;
            this.ServerThreadIsBackground           = ServerThreadIsBackground;

            this.ConnectionIdBuilder                = (ConnectionIdBuilder != null)
                                                          ? ConnectionIdBuilder
                                                          : (RemoteIPSocket) => "TCP:" + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port;
            this.ConnectionThreadsNameCreator       = ServerThreadName;
            this.ConnectionThreadsPriority          = ServerThreadPriority;
            this.ConnectionThreadsAreBackground     = ServerThreadIsBackground;
            this.ConnectionTimeout                  = TimeSpan.FromSeconds(ConnectionTimeoutSeconds);




            this._SocketConnections         = new ConcurrentDictionary<IPSocket, TCPConnection<TData>>();
            this._TCPListener               = new TcpListener(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());

            this.CancellationTokenSource    = new CancellationTokenSource();
            this.CancellationToken          = CancellationTokenSource.Token;


            // Get constructor for TCPConnectionType
            //_Constructor        = typeof(TData).
            //                          GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            //                                         null,
            //                                         new Type[] {
            //                                             typeof(TcpClient)
            //                                         },
            //                                         null);

            //if (_Constructor == null)
            //     throw new ArgumentException("A appropriate constructor for type '" + typeof(TData).FullName + "' could not be found!");


            _ListenerThread = new Thread(() => {

#if __MonoCS__
                // Code for Mono C# compiler
#else
                Thread.CurrentThread.Name          = this.ServerThreadName;
                Thread.CurrentThread.Priority      = this.ServerThreadPriority;
                Thread.CurrentThread.IsBackground  = this.ServerThreadIsBackground;
#endif

                Listen();

            });

            if (Autostart)
                Start();

        }

        #endregion

        #region TCPServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IPSocket                            IPSocket,
                         MapperDelegate                      Mapper,
                         String                              ServerThreadName                = null,
                         ThreadPriority                      ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                             ServerThreadIsBackground        = true,
                         Func<IPSocket, String>              ConnectionIdBuilder             = null,
                         Func<TCPConnection<TData>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                      ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                             ConnectionThreadsAreBackground  = true,
                         UInt64                              ConnectionTimeoutSeconds        = 30,
                         Boolean                             Autostart                       = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   Mapper,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #endregion


        #region (private, threaded) Listen()

        /// <summary>
        /// The thread which will wait for client connections and accepts them
        /// </summary>
        private void Listen()
        {

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

                    // Store the new connection
                    //_SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                    //                               _TCPConnection.Value,
                    //                               (RemoteEndPoint, TCPConnection) => TCPConnection);

                    Task.Factory.StartNew(Tuple => {

                        var _Tuple                 = Tuple as Tuple<TCPServer<TData>, TcpClient>;
                        var _TCPServer             = _Tuple.Item1;
                        var _TCPClient             = _Tuple.Item2;
                        var _IPEndPoint            = _TCPClient.Client.RemoteEndPoint as IPEndPoint;
                        var _RemoteSocket          = new IPSocket(new IPv4Address(_IPEndPoint.Address), new IPPort((UInt16) _IPEndPoint.Port));

#if __MonoCS__
                                            // Code for Mono C# compiler
#else
                        Thread.CurrentThread.Name  = (ConnectionThreadsNameCreator != null)
                                                         ? ServerThreadName
                                                         : "TCP connection from " +
                                                                 _RemoteSocket.IPAddress.ToString() +
                                                                 ":" +
                                                                 _RemoteSocket.Port.ToString();
#endif


                  //      _TCPClient.ReceiveTimeout = (Int32) ConnectionTimeout.TotalMilliseconds;


                        try
                        {

                            var NewTCPConnection = new TCPConnection<TData>(_TCPServer, DateTime.Now, _TCPServer.IPSocket, _RemoteSocket, _TCPClient);
                            //         NewTCPConnection.Run();

                            var OnNotificationLocal = OnNotification;
                            if (OnNotificationLocal != null)
                                OnNotificationLocal(NewTCPConnection);

                        }
                        catch (Exception Exception)
                        {
                            var OnExceptionLocal = OnExceptionOccured;
                            if (OnExceptionLocal != null)
                                OnExceptionLocal(this, DateTime.Now, Exception);
                        }

                    }, new Tuple<TCPServer<TData>, TcpClient>(this, NewTCPClient));

                }

                #region Shutdown

                // Request all client connections to finish!
                foreach (var _SocketConnection in _SocketConnections)
                    _SocketConnection.Value.StopRequested = true;

                // After stopping the TCPListener wait for
                // all client connections to finish!
                while (_SocketConnections.Count > 0)
                    Thread.Sleep(5);

                #endregion

            }

            #region Exception handling

            catch (Exception Exception)
            {
                var OnExceptionLocal = OnExceptionOccured;
                if (OnExceptionLocal != null)
                    OnExceptionLocal(this, DateTime.Now, Exception);
            }

            #endregion

            _IsRunning = false;

        }

        #endregion



        #region Start()

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public void Start()
        {
            Start(_DefaultMaxClientConnections);
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
                Task.Factory.StartNew(() =>
                {

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

            if (MaxClientConnections != _DefaultMaxClientConnections)
                _MaxClientConnections = MaxClientConnections;

            // Start the TCPListener
            _TCPListener.Start((Int32) _MaxClientConnections);

            // Start the TCPListenerThread
            _ListenerThread.Start();

            // Wait until socket has opened
            while (!_IsRunning)
                Thread.Sleep(10);

            if (OnStarted != null)
                OnStarted(this, DateTime.Now);

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
                OnCompletedLocal(this, DateTime.Now, (Message != null) ? Message : String.Empty);

        }

        #endregion

        #region StopAndWait()

        /// <summary>
        /// Stop the TCPServer and wait until all connections are closed.
        /// </summary>
        public void StopAndWait()
        {

            _StopRequested = true;

            while (_SocketConnections.Count > 0)
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

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {

            var _TypeName    = this.GetType().Name;
            var _GenericType = this.GetType().GetGenericArguments()[0].Name;

            return String.Concat(_TypeName.Remove(_TypeName.Length - 2), "<", _GenericType, "> ", IPSocket.ToString() + ((IsRunning) ? " (running)" : ""));

        }

        #endregion

    }

    #endregion

    #region TCPServer -> TCPServer<TCPConnection>

    /// <summary>
    /// A multi-threaded TCPServer using the default TCPConnection handler.
    /// </summary>
    public class TCPServer : TCPServer<Byte[]>
    {

        #region Constructor(s)

        #region TCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IPPort                               Port,
                         MapperDelegate                       Mapper                          = null,
                         String                               ServerThreadName                = "TCP server thread",
                         ThreadPriority                       ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                              ServerThreadIsBackground        = true,
                         Func<IPSocket, String>               ConnectionIdBuilder             = null,
                         Func<TCPConnection<Byte[]>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                       ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                              ConnectionThreadsAreBackground  = true,
                         UInt64                               ConnectionTimeoutSeconds        = 30,
                         Boolean                              Autostart                       = false)

            : this(IPv4Address.Any,
                   Port,
                   Mapper,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #region TCPServer(IIPAddress, Port, ...)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IIPAddress                           IIPAddress,
                         IPPort                               Port,
                         MapperDelegate                       Mapper                          = null,
                         String                               ServerThreadName                = null,
                         ThreadPriority                       ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                              ServerThreadIsBackground        = true,
                         Func<IPSocket, String>               ConnectionIdBuilder             = null,
                         Func<TCPConnection<Byte[]>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                       ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                              ConnectionThreadsAreBackground  = true,
                         UInt64                               ConnectionTimeoutSeconds        = 30,
                         Boolean                              Autostart                       = false)

            : base(IIPAddress,
                   Port,
                   (Mapper == null) ? (TCPServer, Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #region TCPServer(IPSocket, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="Mapper">A delegate to transform the incoming TCP connection data into custom data structures.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The tcp client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public TCPServer(IPSocket                             IPSocket,
                         MapperDelegate                       Mapper                          = null,
                         String                               ServerThreadName                = null,
                         ThreadPriority                       ServerThreadPriority            = ThreadPriority.AboveNormal,
                         Boolean                              ServerThreadIsBackground        = true,
                         Func<IPSocket, String>               ConnectionIdBuilder             = null,
                         Func<TCPConnection<Byte[]>, String>  ConnectionThreadsNameCreator    = null,
                         ThreadPriority                       ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                         Boolean                              ConnectionThreadsAreBackground  = true,
                         UInt64                               ConnectionTimeoutSeconds        = 30,
                         Boolean                              Autostart                       = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   Mapper,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeoutSeconds,
                   Autostart)

        { }

        #endregion

        #endregion

    }

    #endregion

}
