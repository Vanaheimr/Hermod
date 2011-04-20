/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;
using System.Net.Sockets;
using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.Sockets.TCP
{

    /// <summary>
    /// A multi-threaded TCPServer
    /// </summary>
    /// <typeparam name="TCPConnectionType">A class for processing a new client connection</typeparam>
    public class TCPServer<TCPConnectionType> : IServer
        where TCPConnectionType : class, ITCPConnection, new()
    {


        #region Data

        // The internal thread
        private readonly Thread _ListenerThread;

        // The TCP listener socket
        private readonly TcpListener _TCPListener;

        // Store each connection, in order to be able to stop them activily
        private readonly ConcurrentDictionary<IPSocket, TCPConnectionType> _SocketConnections;

        // The constructor for TCPConnectionType
        private readonly ConstructorInfo _Constructor;

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
        public UInt32 ClientTimeout { get; set; }

        #endregion

        #endregion

        #region Events

        public delegate void ExceptionOccuredHandler(Object mySender, Exception myException);
        public event         ExceptionOccuredHandler OnExceptionOccured;

        public delegate void NewConnectionHandler(TCPConnectionType myTCPConnectionType);
        public event         NewConnectionHandler OnNewConnection;

        #endregion


        #region Constructor(s)

        #region TCPServer(myPort, myNewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCPServer using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IPPort myPort, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
            : this(IPv4Address.Any, myPort, NewConnectionHandler, Autostart)
        { }

        #endregion

        #region TCPServer(myIIPAddress, myPort, myNewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCPServer using the given parameters.
        /// </summary>
        /// <param name="myIIPAddress">The listening IP address(es)</param>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IIPAddress myIIPAddress, IPPort myPort, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
        {

            _IPAddress          = myIIPAddress;
            _Port               = myPort;

            _SocketConnections  = new ConcurrentDictionary<IPSocket, TCPConnectionType>();
            _TCPListener        = new TcpListener(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());
             ClientTimeout      = 10000;

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


            _ListenerThread = new Thread(() =>
            {

                Thread.CurrentThread.Name         = "TCPServer thread";
                Thread.CurrentThread.Priority     = ThreadPriority.AboveNormal;
                Thread.CurrentThread.IsBackground = true;
                Listen();

            });

            if (NewConnectionHandler != null)
                OnNewConnection += NewConnectionHandler;

            if (Autostart)
                Start();

        }
        #endregion

        #region TCPServer(myIPSocket, myNewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCPServer using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IPSocket myIPSocket, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
            : this(myIPSocket.IPAddress, myIPSocket.Port, NewConnectionHandler, Autostart)
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
                        Thread.Sleep(10);

                    // Break when a server stop was requested
                    if (_StopRequested)
                        break;

                    // Processing the pending client connection within its own task
                    var _NewTCPClient = _TCPListener.AcceptTcpClient();
                    Task.Factory.StartNew(() => {
                        ProcessNewClientConnection(_NewTCPClient);
                    });
                    
                }
                
                // Request all client connections to finish!
                foreach (var _SocketConnection in _SocketConnections)
                    _SocketConnection.Value.StopRequested = true;

                // After stopping the TCPListener wait for
                // all client connections to finish!
                while (_SocketConnections.Count > 0)
                    Thread.Sleep(10);

            }

            catch (Exception ex)
            {
                if (OnExceptionOccured != null)
                    OnExceptionOccured(this, ex);
            }

            _IsRunning = false;

        }

        #endregion

        #region (private, threaded) ProcessNewClientConnection(myTCPClient)

        /// <summary>
        /// Processes a new client connection
        /// </summary>
        /// <param name="myTCPClient">A new client connection</param>
        private void ProcessNewClientConnection(TcpClient myTCPClient)
        {

            // Invoke constructor of TCPConnectionType
            // Create a new thread-local instance of the upper-layer protocol stack
            var _TCPConnection  = new ThreadLocal<TCPConnectionType>(
                                      () => _Constructor.Invoke(new Object[] { myTCPClient }) as TCPConnectionType
                                  );

            if (_TCPConnection.Value == null)
                throw new ArgumentException("A TCPConnectionType of type '" + typeof(TCPConnectionType).FullName + "' could not be created!");

            _TCPConnection.Value.Timeout       = ClientTimeout;
            _TCPConnection.Value.StopRequested = false;

            // Store the new connection
            _SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                                           _TCPConnection.Value,
                                           (RemoteEndPoint, TCPConnection) => TCPConnection);

            try
            {

                //Console.WriteLine("Incoming connection from " + _TCPConnection.Value.RemoteSocket.ToString());

                // Start upper-layer protocol processing savely!
                var OnNewConnection2 = OnNewConnection;
                if (OnNewConnection2 != null)
                    OnNewConnection2(_TCPConnection.Value);

                //// Finally start the upper-layer protocol processing
                //_TCPConnection.Value.ProcessUpperLayerProtocol();

            }
            catch (Exception ex)
            {

                //_TCPConnection.Value.ExceptionThrown(this, ex);
                //_TCPConnection.Value.StopRequested = true;

                //if (OnExceptionOccured != null)
                //    OnExceptionOccured(this, ex);

            }


            // Remove stored client connection
            var _ATCPConnectionType = default(TCPConnectionType);
            _SocketConnections.TryRemove(_TCPConnection.Value.RemoteSocket, out _ATCPConnectionType);

            _TCPConnection.Dispose();

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

        #region Start(myMaxClientConnections)

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public void Start(UInt32 myMaxClientConnections)
        {

            if (_IsRunning)
                return;
            
            if (myMaxClientConnections != _DefaultMaxClientConnections)
                _MaxClientConnections = myMaxClientConnections;

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
        public void Stop()
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



    #region TCPServer -> TCPServer<TCPConnection>

    /// <summary>
    /// A multi-threaded TCPServer using the default TCPConnection handler.
    /// </summary>
    public class TCPServer : TCPServer<TCPConnection>
    {

        #region Constructor(s)

        #region TCPServer(myPort, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IPPort myPort, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
            : base(IPv4Address.Any, myPort, NewConnectionHandler, Autostart)
        { }

        #endregion

        #region TCPServer(myIIPAddress, myPort, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="myIIPAddress">The listening IP address(es).</param>
        /// <param name="myPort">The listening port.</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IIPAddress myIIPAddress, IPPort myPort, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
            : base(myIIPAddress, myPort, NewConnectionHandler, Autostart)
        { }

        #endregion

        #region TCPServer(myIPSocket, myNewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the TCP server using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public TCPServer(IPSocket myIPSocket, NewConnectionHandler NewConnectionHandler = null, Boolean Autostart = false)
            : base(myIPSocket, NewConnectionHandler, Autostart)
        { }

        #endregion

        #endregion

        #region ToString()

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
