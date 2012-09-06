/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;

using de.ahzf.Vanaheimr.Hermod.Datastructures;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.Sockets.UDP
{

    /// <summary>
    /// A class that listens on an UDP socket.
    /// </summary>
    public class UDPServer<UDPPacketType> : IServer
        where UDPPacketType : class, IUDPPacket, new()
    {

        #region Data

        // The internal thread
        private readonly Thread          _ListenerThread;

        private readonly ConstructorInfo _UDPPacketConstructor;

        private readonly Socket          _LocaldotNetSocket;

        private readonly IPEndPoint      _LocalIPEndPoint;

        private volatile Boolean         _WaitForChildTask;

        #endregion

        #region Properties

        public IPSocket LocalSocket { get; private set; }

        #region IPAddress

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

        #region BufferSize

        /// <summary>
        /// The size of the receive buffer.
        /// </summary>
        public UInt32 BufferSize { get; set; }

        #endregion

        #region ReceiveTimeout

        /// <summary>
        /// Gets or sets a value that specifies the amount of time in milliseconds
        /// after which a synchronous Socket.Receive(...) call will time out.
        /// </summary>
        public UInt32 ReceiveTimeout
        {
            
            get
            {

                if (_LocaldotNetSocket != null)
                    return (UInt32) _LocaldotNetSocket.ReceiveTimeout;

                return 0;

            }

            set
            {

                if (value > Int32.MaxValue)
                    throw new ArgumentException("The value for the ReceiveTimeout must be smaller than " + Int32.MaxValue + "!");

                if (_LocaldotNetSocket != null)
                    _LocaldotNetSocket.ReceiveTimeout = (Int32) value;

            }

        }

        #endregion

        #region IsRunning

        private volatile Boolean _IsRunning = false;

        /// <summary>
        /// True while the server is listening for new clients
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
        /// The server was requested to stop and will no
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

        public delegate void ExceptionOccuredHandler(Object mySender, Exception myException);
        public event         ExceptionOccuredHandler OnExceptionOccured;

        public delegate void NewPacketHandler(UDPPacketType myUDPPacketType);
        public event         NewPacketHandler OnNewPacket;

        #endregion

        #region Constructor(s)

        #region UDPServer(myPort, NewPacketHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewPacketHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IPPort myPort, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
            : this(IPv4Address.Any, myPort, NewPacketHandler, Autostart)
        { }

        #endregion

        #region UDPServer(myIIPAddress, myPort, NewPacketHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters
        /// </summary>
        /// <param name="myIIPAddress">The listening IP address(es)</param>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewPacketHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IIPAddress myIIPAddress, IPPort myPort, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
        {

            _IPAddress      = myIIPAddress;
            _Port           = myPort;

            BufferSize      = 1600;

            _LocalIPEndPoint    = new IPEndPoint(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());
            LocalSocket         = new IPSocket(_LocalIPEndPoint);
            _LocaldotNetSocket  = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _LocaldotNetSocket.Bind(_LocalIPEndPoint);

            // Timeout will throw an exception which is a little bit stupid!
            //ReceiveTimeout  = 1000;


            // Get constructor for UDPPacketType
            _UDPPacketConstructor = typeof(UDPPacketType).
                                     GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                    null,
                                                    new Type[] {
                                                       typeof(Byte[]),
                                                       typeof(IPSocket),
                                                       typeof(IPSocket)
                                                   },
                                                    null);

            if (_UDPPacketConstructor == null)
                throw new ArgumentException("A appropriate constructor for type '" + typeof(UDPPacketType).FullName + "' could not be found!");


            _ListenerThread = new Thread(() =>
            {
                Thread.CurrentThread.Name         = "UDPServer thread";
                Thread.CurrentThread.Priority     = ThreadPriority.AboveNormal;
                Thread.CurrentThread.IsBackground = true;
                Listen();
            });

            if (NewPacketHandler != null)
                OnNewPacket += NewPacketHandler;

            if (Autostart)
                Start();

        }

        #endregion

        #region UDPServer(myIPSocket, NewPacketHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewPacketHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IPSocket myIPSocket, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
            : this(myIPSocket.IPAddress, myIPSocket.Port, NewPacketHandler, Autostart)
        { }

        #endregion

        #endregion


        #region (private, threaded) Listen()

        /// <summary>
        /// Processes an incoming UDP packet.
        /// </summary>
        private void Listen()
        {

            EndPoint _RemoteEndPoint = null;
            Byte[]   _UDPPacket;
            Int32    _NumberOfReceivedBytes;

            try
            {

                _IsRunning = true;

                // Loop until interrupted
                while (!_StopRequested)
                {

                    // Break when a server stop was requested
                    if (_StopRequested)
                        break;

                    _UDPPacket      = new Byte[BufferSize];
                    _RemoteEndPoint = new IPEndPoint(0,0);

                    // Wait for the next packet...
                    _NumberOfReceivedBytes = _LocaldotNetSocket.ReceiveFrom(_UDPPacket, ref _RemoteEndPoint);

                    if (_NumberOfReceivedBytes > 0)
                    {

                        _WaitForChildTask = true;

                        Task.Factory.StartNew(() =>
                        {

                            // Create a local copy of the UDPPacket and RemoteEndPoint as we
                            // do not want to wait till the new thread has accepted the packet

                            //var _UDPPacketCopy      = new Byte[_NumberOfReceivedBytes];
                            //Array.Copy(_UDPPacket, 0, _UDPPacketCopy, 0, _NumberOfReceivedBytes);

                            //var _UDPPacketCopy = new Byte[_NumberOfReceivedBytes];
                            //Buffer.BlockCopy(_UDPPacket, 0, _UDPPacketCopy, 0, _NumberOfReceivedBytes);

                            Array.Resize(ref _UDPPacket, _NumberOfReceivedBytes);
                            
                            var _UDPPacketCopy = _UDPPacket;
                            var _RemoteSocket  = new IPSocket((IPEndPoint) _RemoteEndPoint);

                            _WaitForChildTask = false;

                            ProcessNewPacket(_UDPPacketCopy, _RemoteSocket);

                        });

                        // Wait till the new Task had used some of its time to
                        // make a copy of the given references.
                        while (_WaitForChildTask)
                        { }

                    }

                }

            }
            catch (Exception ex)
            {
                if (OnExceptionOccured != null)
                    OnExceptionOccured(this, ex);
            }

            _IsRunning = false;

        }

        #endregion

        #region (private, threaded) ProcessNewPacket(UDPPacketData, RemoteSocket)

        /// <summary>
        /// Processes a new received packet.
        /// </summary>
        private void ProcessNewPacket(Byte[] UDPPacketData, IPSocket RemoteSocket)
        {

            // Invoke constructor of UDPPacketType
            // Create a new thread-local instance of the upper-layer protocol stack
            var _UDPPacketThreadLocal = new ThreadLocal<UDPPacketType>(
                                            () => _UDPPacketConstructor.Invoke(new Object[] { UDPPacketData, LocalSocket, RemoteSocket }) as UDPPacketType
                                        );

            if (_UDPPacketThreadLocal.Value == null)
                throw new ArgumentException("An UDPPacketType of type '" + typeof(UDPPacketType).FullName + "' could not be created!");


            // Start upper-layer protocol processing
            if (OnNewPacket != null)
                OnNewPacket(_UDPPacketThreadLocal.Value);

        }

        #endregion


        #region Start()

        /// <summary>
        /// Starts the listener.
        /// </summary>
        public void Start()
        {

            if (_IsRunning)
                return;

            // Start the TCPListenerThread
            _ListenerThread.Start();

            // Wait until socket has opened
            while (!_IsRunning)
                Thread.Sleep(10);

            _StopRequested = false;

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stops the listener.
        /// </summary>
        public void Shutdown()
        {

            if (_ListenerThread == null)
                throw new Exception("You can not stop the listener if it wasn't started before!");

            _StopRequested = true;

        }

        #endregion


        #region IDisposable Members

        public void Dispose()
        {

            //StopAndWait();

            //if (_TCPListener != null)
            //    _TCPListener.Stop();

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


    #region UDPServer -> UDPServer<UDPPacket>

    /// <summary>
    /// A multi-threaded UDPServer using the default UDPPacket handler.
    /// </summary>
    public class UDPServer : UDPServer<UDPPacket>
    {

        #region Constructor(s)

        #region UDPServer(myPort, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IPPort myPort, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
            : base(IPv4Address.Any, myPort, NewPacketHandler, Autostart)
        { }

        #endregion

        #region UDPServer(myIIPAddress, myPort, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters
        /// </summary>
        /// <param name="myIPAddress">The listening IP address(es)</param>
        /// <param name="myPort">The listening port</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IPv4Address myIIPAddress, IPPort myPort, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
            : base(myIIPAddress, myPort, NewPacketHandler, Autostart)
        { }

        #endregion

        #region UDPServer(myIPSocket, NewConnectionHandler = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewConnectionHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer(IPSocket myIPSocket, NewPacketHandler NewPacketHandler = null, Boolean Autostart = false)
            : base(myIPSocket, NewPacketHandler, Autostart)
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
