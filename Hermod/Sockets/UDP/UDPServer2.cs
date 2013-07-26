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
using System.Net;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Threading.Tasks;

using eu.Vanaheimr.Hermod.Datastructures;
using eu.Vanaheimr.Styx;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    public class UDPPacket<TData>
    {

        public DateTime ServerTimestamp { get; private set; }
        public IPSocket LocalSocket     { get; private set; }
        public IPSocket RemoteSocket    { get; private set; }
        public TData    Message         { get; private set; }

        public UDPPacket(DateTime ServerTimestamp,
                         IPSocket LocalSocket,
                         IPSocket RemoteSocket,
                         TData    Message)
        {

            this.ServerTimestamp  = ServerTimestamp;
            this.LocalSocket      = LocalSocket;
            this.RemoteSocket     = RemoteSocket;
            this.Message          = Message;

        }

    }

    /// <summary>
    /// A class that listens on an UDP socket.
    /// </summary>
    public class UDPServer2<TData> : INotification<UDPPacket<TData>>,
                                     IServer
    {

        #region Data

        private          Thread          ListenerThread;

        private readonly Socket          LocalDotNetSocket;

        private readonly IPEndPoint      LocalIPEndPoint;

        private          Int32           WaitForChildTaskCreation = 0;

        private readonly MapperDelegate  Mapper;

        public  readonly IPSocket        LocalSocket;

        #endregion

        #region Properties

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

                if (LocalDotNetSocket != null)
                    return (UInt32) LocalDotNetSocket.ReceiveTimeout;

                return 0;

            }

            set
            {

                if (value > Int32.MaxValue)
                    throw new ArgumentException("The value for the ReceiveTimeout must be smaller than " + Int32.MaxValue + "!");

                if (LocalDotNetSocket != null)
                    LocalDotNetSocket.ReceiveTimeout = (Int32) value;

            }

        }

        #endregion

        #region IsRunning

        private Int32 _IsRunning = 0;

        /// <summary>
        /// True while the server is listening for new clients
        /// </summary>
        public Boolean IsRunning
        {
            get
            {
                return _IsRunning == 1;
            }
        }

        #endregion

        #region StopRequested

        private Int32 _StopRequested = 0;

        /// <summary>
        /// The server was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean StopRequested
        {
            get
            {
                return _StopRequested == 1;
            }
        }

        #endregion

        #endregion

        #region Events

        public delegate TData MapperDelegate(DateTime Timestamp, IPSocket LocalSocket, IPSocket RemoteSocket, Byte[] Message);

        // INotification<TData>
        public event NotificationEventHandler<UDPPacket<TData>> OnNotification;
        public event ExceptionEventHandler OnError;
        public event CompletedEventHandler OnCompleted;

        #endregion

        #region Constructor(s)

        #region UDPServer(Port, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public UDPServer2(IPPort          Port,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

            : this(IPv4Address.Any, Port, Mapper, BufferSize, ThreadName, ThreadPrio, IsBackground, Autostart)

        { }

        #endregion

        #region UDPServer(IPAddress, Port, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters
        /// </summary>
        /// <param name="IPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="Autostart"></param>
        public UDPServer2(IIPAddress      IPAddress,
                          IPPort          Port,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

        {

            if (Mapper == null)
                throw new ArgumentNullException("The mapper delegate must not be null!");

            this._IPAddress         = IPAddress;
            this._Port              = Port;
            this.Mapper             = Mapper;
            this.BufferSize         = BufferSize;

            this.LocalIPEndPoint    = new IPEndPoint(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());
            this.LocalSocket        = new IPSocket(LocalIPEndPoint);
            this.LocalDotNetSocket  = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.LocalDotNetSocket.Bind(LocalIPEndPoint);

            // Timeout will throw an exception which is a little bit stupid!
            //ReceiveTimeout  = 1000;

            Listen(ThreadName, ThreadPrio, IsBackground);

            if (Autostart)
                Start();

        }

        #endregion

        #region UDPServer(IPSocket, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewPacketHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer2(IPSocket        IPSocket,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

            : this(IPSocket.IPAddress, IPSocket.Port, Mapper, BufferSize, ThreadName, ThreadPrio, IsBackground, Autostart)

        { }

        #endregion

        #endregion


        #region (private, threaded) Listen()

        /// <summary>
        /// Processes an incoming UDP packet.
        /// </summary>
        private void Listen(String          ThreadName,
                            ThreadPriority  ThreadPrio,
                            Boolean         IsBackground)
        {

            if (_IsRunning == 1)
                return;

            this.ListenerThread = new Thread(() => {

                Thread.CurrentThread.Name          = ThreadName;
                Thread.CurrentThread.Priority      = ThreadPrio;
                Thread.CurrentThread.IsBackground  = IsBackground;

                EndPoint _RemoteEndPoint = null;
                Byte[]   _UDPPacket;
                Int32    _NumberOfReceivedBytes;
                DateTime Timestamp;

                try
                {

                    Interlocked.Exchange(ref _IsRunning, 1);

                    // Loop until interrupted
                    while (_StopRequested == 0)
                    {

                        // Break when a server stop was requested
                        if (_StopRequested > 0)
                            break;

                        _UDPPacket = new Byte[this.BufferSize];
                        _RemoteEndPoint = new IPEndPoint(0, 0);

                        // Wait for the next packet...
                        _NumberOfReceivedBytes = LocalDotNetSocket.ReceiveFrom(_UDPPacket, ref _RemoteEndPoint);
                        Timestamp = DateTime.Now;

                        if (_NumberOfReceivedBytes > 0)
                        {

                            Interlocked.Exchange(ref WaitForChildTaskCreation, 1);

                            Task.Factory.StartNew(TimestampLocal =>
                            {

                                // Create a local copy of the UDPPacket and RemoteEndPoint as we
                                // do not want to wait till the new thread has accepted the packet

                                Array.Resize(ref _UDPPacket, _NumberOfReceivedBytes);

                                var UDPPacketLocal       = _UDPPacket;
                                var RemoteSocketLocal    = new IPSocket((IPEndPoint) _RemoteEndPoint);
                                var OnNotificationLocal  = OnNotification;

                                Thread.CurrentThread.Name = "UDPPacket from " + RemoteSocketLocal.IPAddress + ":" + RemoteSocketLocal.Port;

                                Interlocked.Exchange(ref WaitForChildTaskCreation, 0);

                                // Start upper-layer protocol processing
                                if (OnNotification != null)
                                    OnNotification(new UDPPacket<TData>(
                                                      (DateTime)TimestampLocal,
                                                      this.LocalSocket,
                                                      RemoteSocketLocal,
                                                      Mapper((DateTime) TimestampLocal, this.LocalSocket, RemoteSocketLocal, UDPPacketLocal)
                                                  ));

                            }, Timestamp);

                            // Wait till the new Task had used some of its time to
                            // make a copy of the given references.
                            while (WaitForChildTaskCreation > 0)
                                Thread.Sleep(1);

                        }

                    }

                }
                catch (Exception ex)
                {
                    var OnErrorLocal = OnError;
                    if (OnErrorLocal != null)
                        OnErrorLocal(this, ex);
                }

                Interlocked.Exchange(ref _IsRunning, 0);

            });

        }

        #endregion


        #region Start()

        /// <summary>
        /// Starts the listener.
        /// </summary>
        public void Start()
        {

            if (_IsRunning == 1)
                return;

            Interlocked.Exchange(ref _StopRequested, 0);

            // Start the TCPListenerThread
            ListenerThread.Start();

            // Wait until socket has opened
            while (_IsRunning != 1)
                Thread.Sleep(10);

        }

        #endregion

        #region Shutdown()

        /// <summary>
        /// Shutdown the UDP listener.
        /// </summary>
        public void Shutdown()
        {

            if (ListenerThread == null)
                throw new Exception("You can not stop the listener if it wasn't started before!");

            Interlocked.Exchange(ref _StopRequested, 1);

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


    public class UDPServer2 : UDPServer2<Byte[]>
    {

        #region Constructor(s)

        #region UDPServer(Port, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using IPAddress.Any and the given parameters
        /// </summary>
        /// <param name="myPort">The listening port</param>
        /// <param name="Autostart"></param>
        public UDPServer2(IPPort          Port,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

            : base(Port,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   BufferSize,
                   ThreadName,
                   ThreadPrio,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #region UDPServer(IPAddress, Port, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters
        /// </summary>
        /// <param name="IPAddress">The listening IP address(es)</param>
        /// <param name="Port">The listening port</param>
        /// <param name="Autostart"></param>
        public UDPServer2(IIPAddress      IPAddress,
                          IPPort          Port,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

            : base(IPAddress,
                   Port,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   BufferSize,
                   ThreadName,
                   ThreadPrio,
                   IsBackground,
                   Autostart)

        {

            if (Mapper == null)
                throw new ArgumentNullException("The mapper delegate must not be null!");

        }

        #endregion

        #region UDPServer(IPSocket, Mapper = null, Autostart = false)

        /// <summary>
        /// Initialize the UDP server using the given parameters.
        /// </summary>
        /// <param name="myIPSocket">The listening IPSocket.</param>
        /// <param name="NewPacketHandler"></param>
        /// <param name="Autostart"></param>
        public UDPServer2(IPSocket        IPSocket,
                          MapperDelegate  Mapper        = null,
                          UInt32          BufferSize    = 1600,
                          String          ThreadName    = "UDPServer thread",
                          ThreadPriority  ThreadPrio    = ThreadPriority.AboveNormal,
                          Boolean         IsBackground  = true,
                          Boolean         Autostart     = false)

            : base(IPSocket.IPAddress,
                   IPSocket.Port,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   BufferSize,
                   ThreadName,
                   ThreadPrio,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #endregion

    }


}
