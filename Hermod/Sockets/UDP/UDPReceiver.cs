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
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

using eu.Vanaheimr.Styx;

#endregion

namespace eu.Vanaheimr.Hermod.Sockets.UDP
{

    #region UDPReceiver<TOut>

    /// <summary>
    /// A Styx arrow sender that listens on an UDP
    /// socket and notifies about incoming UDP packets.
    /// </summary>
    /// <typeparam name="TOut">The type of the Styx arrows to send.</typeparam>
    public class UDPReceiver<TOut> : IArrowSender<TOut>,
                                     IArrowSender<UDPPacket<TOut>>,
                                     IServer
    {

        #region Data

        private          Task                     ReceiverTask;
        private readonly Socket                   LocalDotNetSocket;
        public  readonly IPSocket                 LocalSocket;
        private readonly Func<IPSocket, String>   PacketThreadName;
        private readonly MapperDelegate           Mapper; 
        private          CancellationTokenSource  CancellationTokenSource;
        private          CancellationToken        CancellationToken;

        #endregion

        #region Properties

        // readonly

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

        #region IsMulticast

        private readonly Boolean _IsMulticast;

        /// <summary>
        /// Whether this UDP receiver is listening
        /// on a multicast IP address or not.
        /// </summary>
        public Boolean IsMulticast
        {
            get
            {
                return _IsMulticast;
            }
        }

        #endregion

        #region ReceiverThreadName

        private readonly String _ReceiverThreadName;

        /// <summary>
        /// The name of the UDP receiver thread.
        /// </summary>
        public String ReceiverThreadName
        {
            get
            {
                return _ReceiverThreadName;
            }
        }

        #endregion

        #region ReceiverThreadPriority

        private readonly ThreadPriority _ReceiverThreadPriority;

        /// <summary>
        /// The priority of the UDP receiver thread.
        /// </summary>
        public ThreadPriority ReceiverThreadPriority
        {
            get
            {
                return _ReceiverThreadPriority;
            }
        }

        #endregion

        #region PacketThreadPriority

        private readonly ThreadPriority _PacketThreadPriority;

        /// <summary>
        /// The priority of the UDP packet threads.
        /// </summary>
        public ThreadPriority PacketThreadPriority
        {
            get
            {
                return _PacketThreadPriority;
            }
        }

        #endregion

        #region IsBackground

        private readonly Boolean _IsBackground;

        /// <summary>
        /// Whether the UDP receiver thread is a background thread or not.
        /// </summary>
        public Boolean IsBackground
        {
            get
            {
                return _IsBackground;
            }
        }

        #endregion


        // mutatable

        #region ServiceBanner

        public String ServiceBanner { get; set; }

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

        /// <summary>
        /// The server was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean StopRequested
        {
            get
            {
                return this.CancellationToken.IsCancellationRequested;
            }
        }

        #endregion

        #endregion

        #region Events

        #region OnStarted

        /// <summary>
        /// An event fired whenever the service started.
        /// </summary>
        public event OnStartedDelegate OnStarted;

        #endregion

        #region OnStoppeded

        /// <summary>
        /// An event fired whenever the service stopped.
        /// </summary>
        public event OnStoppededDelegate OnStopped;

        #endregion

        /// <summary>
        /// A delegate to transform the incoming UDP packets into a custom data structure.
        /// </summary>
        /// <param name="Timestamp">The server timestamp of the UDP packet.</param>
        /// <param name="LocalSocket">The local UDP socket.</param>
        /// <param name="RemoteSocket">The remote UDP socket.</param>
        /// <param name="Payload">The payload of the UDP packet.</param>
        /// <returns>The payload/message of the UDP packet transformed into a custom data structure.</returns>
        public delegate TOut MapperDelegate(DateTime Timestamp, IPSocket LocalSocket, IPSocket RemoteSocket, Byte[] Payload);



        private event NotificationEventHandler<TOut>            OnNotification_Message;
        private event NotificationEventHandler<UDPPacket<TOut>> OnNotification_UDPPacket;



        // INotification
        event NotificationEventHandler<TOut> IArrowSender<TOut>.OnNotification
        {
            add    { OnNotification_Message   += value; }
            remove { OnNotification_Message   -= value; }
        }

        event NotificationEventHandler<UDPPacket<TOut>> IArrowSender<UDPPacket<TOut>>.OnNotification
        {
            add    { OnNotification_UDPPacket += value; }
            remove { OnNotification_UDPPacket -= value; }
        }



        public event ExceptionEventHandler OnError;
        public event CompletedEventHandler OnCompleted;

        #endregion

        #region Constructor(s)

        #region UDPReceiver(Port, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="PacketThreadName">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort Port,
                           MapperDelegate         Mapper,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)


            : this(IPv4Address.Any,
                   Port,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   PacketThreadName,
                   PacketThreadPriority,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPAddress, Port, Mapper, ...) <= main constructor

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="PacketThreadName">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress             IPAddress,
                           IPPort                 Port,
                           MapperDelegate         Mapper,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)

        {

            if (Mapper == null)
                throw new ArgumentNullException("The mapper delegate must not be null!");

            this._IPAddress                 = IPAddress;
            this._IsMulticast               = IPAddress.IsMulticast;
            this._Port                      = Port;
            this.Mapper                     = Mapper;
            this._ReceiverThreadName        = ReceiverThreadName;
            this._ReceiverThreadPriority    = ReceiverThreadPriority;
            this.PacketThreadName           = (PacketThreadName == null)
                                                  ? socket => "UDP packet from " + socket.IPAddress + ":" + socket.Port
                                                  : PacketThreadName;
            this._PacketThreadPriority      = PacketThreadPriority;
            this._IsBackground              = IsBackground;

            var LocalIPEndPoint             = new IPEndPoint(new System.Net.IPAddress(_IPAddress.GetBytes()), _Port.ToInt32());
            this.LocalSocket                = new IPSocket(LocalIPEndPoint);
            this.LocalDotNetSocket          = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.LocalDotNetSocket.Bind(LocalIPEndPoint);

            this.BufferSize                 = 65536;
            this.ReceiveTimeout             = 5000;

            if (IsMulticast)
            {

                LocalDotNetSocket.SetSocketOption(SocketOptionLevel.IP,
                                                  SocketOptionName.AddMembership,
                                                  new MulticastOption(System.Net.IPAddress.Parse(_IPAddress.ToString()),
                                                                      System.Net.IPAddress.Any));

            }

            this.CancellationTokenSource  = new CancellationTokenSource();
            this.CancellationToken        = CancellationTokenSource.Token;

            this.ServiceBanner            = "UDPReceiver";

            if (Autostart)
                Start();

        }

        #endregion

        #region UDPReceiver(IPSocket, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="PacketThreadName">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket               IPSocket,
                           MapperDelegate         Mapper,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   PacketThreadName,
                   PacketThreadPriority,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #endregion


        #region Start()

        /// <summary>
        /// Start the UDP receiver.
        /// </summary>
        public void Start()
        {

            if (_IsRunning == 1)
                return;

            try
            {

                this.ReceiverTask = Task.Factory.StartNew(() =>
                {

#if __MonoCS__
                    // Code for Mono C# compiler
#else
                    Thread.CurrentThread.Name          = ReceiverThreadName;
                    Thread.CurrentThread.Priority      = ReceiverThreadPriority;
                    Thread.CurrentThread.IsBackground  = IsBackground;
#endif

                    EndPoint RemoteEndPoint = null;
                    Byte[]   UDPPayload;
                    Int32    NumberOfReceivedBytes;
                    DateTime Timestamp;
                    Int32    WaitForChildTaskCreation = 0;

                    Interlocked.Exchange(ref _IsRunning, 1);

                    #region Receiver loop

                    while (!CancellationToken.IsCancellationRequested)
                    {

                        UDPPayload     = new Byte[this.BufferSize];
                        RemoteEndPoint = new IPEndPoint(0, 0);

                        try
                        {

                            // Wait for the next packet...
                            // Will throw an exception every ReceiveTimeout when no packet was received!
                            NumberOfReceivedBytes = LocalDotNetSocket.ReceiveFrom(UDPPayload, ref RemoteEndPoint);

                            if (CancellationToken.IsCancellationRequested)
                                break;

                            Timestamp = DateTime.Now;

                            if (NumberOfReceivedBytes > 0)
                            {

                                Interlocked.Exchange(ref WaitForChildTaskCreation, 1);

                                #region Per packet task(s)...

                                Task.Factory.StartNew(() =>
                                {

                                    // Create a local copies as we do not want to wait
                                    // till the new thread has accepted the packet
                                    // (Behaviour may change in .NET 4.5!)
                                    var Timestamp_Local                 = Timestamp;
                                    var UDPPayload_Local                = UDPPayload;
                                    var OnNotification_Message_Local    = OnNotification_Message;
                                    var OnNotification_UDPPacket_Local  = OnNotification_UDPPacket;
                                    var RemoteSocket_Local              = new IPSocket((IPEndPoint) RemoteEndPoint);

#if __MonoCS__
                                    // Code for Mono C# compiler
#else
                                    Thread.CurrentThread.Name           = PacketThreadName(RemoteSocket_Local);
                                    Thread.CurrentThread.Priority       = PacketThreadPriority;
                                    Thread.CurrentThread.IsBackground   = IsBackground;
#endif

                                    Array.Resize(ref UDPPayload_Local, NumberOfReceivedBytes);

                                    Interlocked.Exchange(ref WaitForChildTaskCreation, 0);

                                    // Start upper-layer protocol processing
                                    if (OnNotification_Message_Local != null)
                                        OnNotification_Message_Local(Mapper(Timestamp_Local,
                                                                            this.LocalSocket,
                                                                            RemoteSocket_Local,
                                                                            UDPPayload_Local));

                                    if (OnNotification_UDPPacket_Local != null)
                                        OnNotification_UDPPacket_Local(new UDPPacket<TOut>(
                                                                           Timestamp_Local,
                                                                           this.LocalSocket,
                                                                           RemoteSocket_Local,
                                                                           Mapper(Timestamp_Local,
                                                                                  this.LocalSocket,
                                                                                  RemoteSocket_Local,
                                                                                  UDPPayload_Local)
                                                                      ));



                                }, CancellationTokenSource.Token,
                                   TaskCreationOptions.AttachedToParent,
                                   TaskScheduler.Default);

                                #endregion

                                // Wait till the new Task had used some of its time to
                                // make a copy of the given references.
                                while (WaitForChildTaskCreation > 0)
                                    Thread.Sleep(1);

                            }

                        }
                        catch (SocketException)
                        {
                            // Will mainly be called for ReceiveTimeouts!
                        }
                        catch (Exception e)
                        {
                            var OnErrorLocal = OnError;
                            if (OnErrorLocal != null)
                                OnErrorLocal(this, e);
                        }

                    }

                    #endregion

                    Interlocked.Exchange(ref _IsRunning, 0);

                }, CancellationTokenSource.Token,
                   TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent,
                   TaskScheduler.Default);

            }
            catch (Exception e)
            {
                var OnErrorLocal = OnError;
                if (OnErrorLocal != null)
                    OnErrorLocal(this, e);
            }

            if (OnStarted != null)
                OnStarted(this, DateTime.Now);

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

        #region Shutdown(Wait = true)

        /// <summary>
        /// Shutdown the UDP listener.
        /// </summary>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public void Shutdown(Boolean Wait = true)
        {

            if (IsMulticast)
            {

                LocalDotNetSocket.SetSocketOption(SocketOptionLevel.IP,
                                                  SocketOptionName.DropMembership,
                                                  new MulticastOption(System.Net.IPAddress.Parse(_IPAddress.ToString()),
                                                                      System.Net.IPAddress.Any));

            }

            this.CancellationTokenSource.Cancel();

            if (Wait)
                while (_IsRunning > 0)
                    Thread.Sleep(10);

            if (OnStopped != null)
                OnStopped(this, DateTime.Now);

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

    #endregion

    #region UDPReceiver

    /// <summary>
    /// A Styx arrow sender that listens on an UDP socket
    /// and notifies about incoming UDP packets.
    /// </summary>
    public class UDPReceiver : UDPReceiver<Byte[]>
    {

        #region UDPReceiver(Port, Mapper = null, ...)

        /// <summary>
        /// Create a new UDP receiver using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">An optional delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ThreadPrio">The optional priority of the UDP receiver thread.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort                 Port,
                           MapperDelegate         Mapper                  = null,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)

            : base(Port,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   PacketThreadName,
                   PacketThreadPriority,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPAddress, Port, Mapper = null, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">An optional delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="PacketThreadName">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress             IPAddress,
                           IPPort                 Port,
                           MapperDelegate         Mapper                  = null,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)

            : base(IPAddress,
                   Port,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   PacketThreadName,
                   PacketThreadPriority,
                   IsBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPSocket, Mapper = null, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="Mapper">An optional delegate to transform the incoming UDP packets into a custom data structure.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="PacketThreadName">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="IsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket IPSocket,
                           MapperDelegate         Mapper                  = null,
                           String                 ReceiverThreadName      = "UDPReceiver thread",
                           ThreadPriority         ReceiverThreadPriority  = ThreadPriority.AboveNormal,
                           Func<IPSocket, String> PacketThreadName        = null,
                           ThreadPriority         PacketThreadPriority    = ThreadPriority.AboveNormal,
                           Boolean                IsBackground            = true,
                           Boolean                Autostart               = false)

            : base(IPSocket,
                   (Mapper == null) ? (Timestamp, LocalSocket, RemoteSocket, Message) => Message : Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   PacketThreadName,
                   PacketThreadPriority,
                   IsBackground,
                   Autostart)

        { }

        #endregion

    }

    #endregion

}

