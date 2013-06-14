/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Reflection;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.TCP
{

    #region TCPServer<TCPConnection>

    /// <summary>
    /// A multi-threaded TCPServer
    /// </summary>
    /// <typeparam name="TCPConnectionType">A class for processing a new client connection</typeparam>
    public class TCPServer<TCPConnectionType> : ITCPServer
        where TCPConnectionType : class, ITCPConnection, new()
    {

        #region Data

        // The internal thread
        private readonly Thread                                             _ListenerThread;

        // The TCP listener socket
        private readonly TcpListener                                        _TCPListener;

        // Store each connection, in order to be able to stop them activily
        private readonly ConcurrentDictionary<IPSocket, TCPConnectionType>  _SocketConnections;

        // The constructor for TCPConnectionType
        private readonly ConstructorInfo                                    _Constructor;

        #endregion

        #region Properties

        #region IPAdress

        private readonly IIPAddress _IPAddress;

        /// <summary>
        /// Gets the IPAddress on which the TCPServer listens.
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
        /// Gets the port on which the TCPServer listens.
        /// </summary>
        public IPPort Port
        {
            get
            {
                return _Port;
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

        #region ClientTimeout

        /// <summary>
        /// Will set the ClientTimeout for all incoming client connections
        /// </summary>
        public Int32 ClientTimeout { get; set; }

        #endregion

        #endregion

        #region Events

        #region ExceptionOccured

        private event OnExceptionOccuredDelegate _OnExceptionOccured;

        private List<OnExceptionOccuredDelegate> MyEventStorage = new List<OnExceptionOccuredDelegate>();

        public event OnExceptionOccuredDelegate OnExceptionOccured
        {

            add
            {
                MyEventStorage.Add(value);
                _OnExceptionOccured += value;
            }

            remove
            {
                MyEventStorage.Remove(value);
                _OnExceptionOccured -= value;
            }

        }

        #endregion

        #region OnNewConnection

        /// <summary>
        /// A delegate called for every incoming TCP connection.
        /// </summary>
        public delegate void OnNewClientConnectionDelegate(TCPConnectionType myTCPConnectionType);

        /// <summary>
        /// A event called for every incoming connection.
        /// </summary>
        public event OnNewClientConnectionDelegate OnNewConnection;

        #endregion

        #endregion

        #region Constructor(s)

        #region TCPServer(Port, NewConnectionHandler = null, Autostart = false, ThreadDescription = "...")

        /// <summary>
        /// Initialize the TCPServer using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IPPort Port, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false, String ThreadDescription = "...")
            : this(IPv4Address.Any, Port, NewConnectionHandler, Autostart, ThreadDescription)
        { }

        #endregion

        #region TCPServer(IIPAddress, Port, NewConnectionHandler = null, Autostart = false, ThreadDescription = "...")

        /// <summary>
        /// Initialize the TCPServer using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IIPAddress IIPAddress, IPPort Port, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false, String ThreadDescription = "...")
        {

            _IPAddress          = IIPAddress;
            _Port               = Port;

            _SocketConnections  = new ConcurrentDictionary<IPSocket, TCPConnectionType>();
            _TCPListener        = new TcpListener(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());
             ClientTimeout      = 30000;

            // Get constructor for TCPConnectionType
            _Constructor        = typeof(TCPConnectionType).
                                      GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                     null,
                                                     new Type[] {
                                                         typeof(TcpClient)
                                                     },
                                                     null);

            if (_Constructor == null)
                 throw new ArgumentException("A appropriate constructor for type '" + typeof(TCPConnectionType).FullName + "' could not be found!");


            _ListenerThread = new Thread(() => {
                Thread.CurrentThread.Name          = "TCPServer<" + ThreadDescription + ">";
                Thread.CurrentThread.Priority      = ThreadPriority.AboveNormal;
                Thread.CurrentThread.IsBackground  = true;
                Listen();
            });

            if (NewConnectionHandler != null)
                OnNewConnection += NewConnectionHandler;

            if (Autostart)
                Start();

        }

        #endregion

        #region TCPServer(IPSocket, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCPServer using the given parameters.
        /// </summary>
        /// <param name="IPSocket">The listening IPSocket.</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IPSocket IPSocket, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false)
            : this(IPSocket.IPAddress, IPSocket.Port, NewConnectionHandler, Autostart)
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
                    var _NewTCPClient = _TCPListener.AcceptTcpClient();
                    Task.Factory.StartNew(() => {
                        ProcessNewClientConnection(_NewTCPClient);
                    });

                }

                #region Shutdown

                // Request all client connections to finish!
                foreach (var _SocketConnection in _SocketConnections)
                    _SocketConnection.Value.StopRequested = true;

                // After stopping the TCPListener wait for
                // all client connections to finish!
                while (_SocketConnections.Count > 0)
                    Thread.Sleep(10);

                #endregion

            }

            #region Exception handling

            catch (Exception Exception)
            {
                var OnExceptionOccured_Local = _OnExceptionOccured;
                if (OnExceptionOccured_Local != null)
                    OnExceptionOccured_Local(this, Exception);
            }

            #endregion

            _IsRunning = false;

        }

        #endregion

        #region (private, threaded) ProcessNewClientConnection(TCPClient)

        /// <summary>
        /// Processes a new client connection
        /// </summary>
        /// <param name="TCPClient">A new client connection</param>
        private void ProcessNewClientConnection(TcpClient TCPClient)
        {

            #region Create a new thread-local instance of the upper-layer protocol stack

            // Invoke constructor of TCPConnectionType
            var _TCPConnection = new ThreadLocal<TCPConnectionType>(
                                      () => _Constructor.Invoke(new Object[] { TCPClient }) as TCPConnectionType
                                  );

            if (_TCPConnection.Value == null)
                throw new ArgumentException("A TCPConnectionType of type '" + typeof(TCPConnectionType).FullName + "' could not be created!");

            _TCPConnection.Value.ReadTimeout   = ClientTimeout;
            _TCPConnection.Value.StopRequested = false;

            // Copy ExceptionOccured event handlers
            foreach (var ExceptionOccuredHandler in MyEventStorage)
                _TCPConnection.Value.OnExceptionOccured += ExceptionOccuredHandler;

            #endregion

            #region Store the new connection

            _SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                                           _TCPConnection.Value,
                                           (RemoteEndPoint, TCPConnection) => TCPConnection);

            #endregion

            try
            {

                // Call delegates for upper-layer protocol processing
                var OnNewConnection_Local = OnNewConnection;
                if (OnNewConnection_Local != null)
                    OnNewConnection_Local(_TCPConnection.Value);

            }
            catch (Exception Exception)
            {

                // Call delegates for exception handling
                var OnExceptionOccured_Local = _OnExceptionOccured;
                if (OnExceptionOccured_Local != null)
                    OnExceptionOccured_Local(this, Exception);

            }

            #region Remove and close client connection

            var _ATCPConnectionType = default(TCPConnectionType);
            _SocketConnections.TryRemove(_TCPConnection.Value.RemoteSocket, out _ATCPConnectionType);

            _TCPConnection.Dispose();

            #endregion

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

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop the TCPSocketListener
        /// </summary>
        public void Shutdown()
        {

            _StopRequested = true;

            if (_TCPListener != null)
                _TCPListener.Stop();

        }

        #endregion

        #region StopAndWait()

        /// <summary>
        /// Stop the TCPServer and wait until all connections are closed
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

            var _Running = "";
            if (IsRunning) _Running = " (running)";

            return String.Concat(_TypeName.Remove(_TypeName.Length - 2), "<", _GenericType, "> ", _IPAddress.ToString(), ":", _Port, _Running);

        }

        #endregion

    }

    #endregion

    #region TCPServer -> TCPServer<TCPConnection>

    /// <summary>
    /// A multi-threaded TCPServer using the default TCPConnection handler.
    /// </summary>
    public class TCPServer : TCPServer<TCPConnection>
    {

        #region Constructor(s)

        #region TCPServer(Port, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IPPort Port, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false)
            : base(IPv4Address.Any, Port, NewConnectionHandler, Autostart)
        { }

        #endregion

        #region TCPServer(IIPAddress, Port, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IIPAddress">The listening IP address(es).</param>
        /// <param name="Port">The listening port.</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IIPAddress IIPAddress, IPPort Port, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false)
            : base(IIPAddress, Port, NewConnectionHandler, Autostart)
        { }

        #endregion

        #region TCPServer(IPSocket, myNewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="IPSocket">The listening IPSocket.</param>
        /// <param name="NewConnectionHandler">A delegate called for every new tcp connection.</param>
        /// <param name="Autostart">Autostart the tcp server.</param>
        public TCPServer(IPSocket IPSocket, OnNewClientConnectionDelegate NewConnectionHandler = null, Boolean Autostart = false)
            : base(IPSocket, NewConnectionHandler, Autostart)
        { }

        #endregion

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {

            var _Running = "";
            if (IsRunning) _Running = " (running)";

            return String.Concat(this.GetType().Name, " ", IPAddress.ToString(), ":", Port, _Running);

        }

        #endregion

    }

    #endregion

}
