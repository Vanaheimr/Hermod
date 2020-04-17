/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.UDP
{

    #region UDPReceiver<TOut>

    /// <summary>
    /// A Styx arrow sender that listens on an UDP
    /// socket and notifies about incoming UDP packets.
    /// </summary>
    /// <typeparam name="TData">The type of the Styx arrows to send.</typeparam>
    public class UDPReceiver<TData> : IUDPReceiver<TData>
    {

        #region Data

        private          Task                            ReceiverTask;
        private readonly Socket                          LocalDotNetSocket;
        public  readonly IPSocket                        LocalSocket;
        private readonly Func<UDPPacket<TData>, String>  PacketThreadsNameCreator;
        private readonly MapperDelegate                  Mapper; 
        private readonly MapReduceDelegate               MapReduce; 
        private          CancellationTokenSource         CancellationTokenSource;
        private          CancellationToken               CancellationToken;

        public  const    String                          DefaultServiceBanner      = "Vanaheimr UDP/CSV Service v0.8";

        #endregion

        #region Properties

        // readonly

        #region IPAddress

        private readonly IIPAddress _IPAddress;

        /// <summary>
        /// Gets the IPAddress on which the UDP receiver listens.
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
        /// Gets the port on which the UDP receiver listens.
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
        /// Gets the IP socket on which the UDP receiver listens.
        /// </summary>
        public IPSocket IPSocket
        {
            get
            {
                return _IPSocket;
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

        #region ReceiverThreadIsBackground

        private readonly Boolean _ReceiverThreadIsBackground;

        /// <summary>
        /// Whether the UDP receiver thread is a background thread or not.
        /// </summary>
        public Boolean ReceiverThreadIsBackground
        {
            get
            {
                return _ReceiverThreadIsBackground;
            }
        }

        #endregion

        #region PacketThreadsPriority

        private readonly ThreadPriority _PacketThreadsPriority;

        /// <summary>
        /// The priority of the UDP packet threads.
        /// </summary>
        public ThreadPriority PacketThreadsPriority
        {
            get
            {
                return _PacketThreadsPriority;
            }
        }

        #endregion

        #region PacketThreadsAreBackground

        private readonly Boolean _PacketThreadsAreBackground;

        /// <summary>
        /// Whether the UDP packet threads are background threads or not.
        /// </summary>
        public Boolean PacketThreadsAreBackground
        {
            get
            {
                return _PacketThreadsAreBackground;
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
        /// An event fired when the UDP receiver started.
        /// </summary>
        public event StartedEventHandler OnStarted;

        #endregion

        #region OnNotification

        /// <summary>
        /// An event fired for every incoming UDP packet.
        /// </summary>
        public event NotificationEventHandler<UDPPacket<TData>> OnNotification;

        #endregion

        #region OnExceptionOccured

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler OnExceptionOccured;

        #endregion

        #region OnCompleted

        /// <summary>
        /// An event fired when the UDP receiver stopped.
        /// </summary>
        public event CompletedEventHandler OnCompleted;

        #endregion

        #endregion

        #region Delegates

        #region MapperDelegate

        /// <summary>
        /// A delegate to transform the incoming UDP packets into custom data structures.
        /// </summary>
        /// <param name="UDPReceiver">The UDP receiver.</param>
        /// <param name="Timestamp">The server timestamp of the UDP packet.</param>
        /// <param name="LocalSocket">The local UDP socket.</param>
        /// <param name="RemoteSocket">The remote UDP socket.</param>
        /// <param name="Payload">The payload of the UDP packet.</param>
        /// <returns>The payload/message of the UDP packet transformed into custom data structures.</returns>
        public delegate TData MapperDelegate(UDPReceiver<TData>  UDPReceiver,
                                             DateTime            Timestamp,
                                             IPSocket            LocalSocket,
                                             IPSocket            RemoteSocket,
                                             Byte[]              Payload);

        #endregion

        #region MapReduceDelegate

        /// <summary>
        /// A delegate to transform the incoming UDP packets into custom data structures.
        /// </summary>
        /// <param name="UDPReceiver">The UDP receiver.</param>
        /// <param name="Timestamp">The server timestamp of the UDP packet.</param>
        /// <param name="LocalSocket">The local UDP socket.</param>
        /// <param name="RemoteSocket">The remote UDP socket.</param>
        /// <param name="Payload">The payload of the UDP packet.</param>
        /// <returns>The payload/message of the UDP packet transformed into custom data structures.</returns>
        public delegate IEnumerable<TData> MapReduceDelegate(UDPReceiver<TData>  UDPReceiver,
                                                             DateTime            Timestamp,
                                                             IPSocket            LocalSocket,
                                                             IPSocket            RemoteSocket,
                                                             Byte[]              Payload);

        #endregion

        #endregion

        #region Constructor(s)

        #region UDPReceiver(Port, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort                          Port,
                           String                          ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                  Mapper                      = null,
                           String                          ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                  ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                         ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<TData>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                  PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                         PacketThreadsAreBackground  = true,
                           Boolean                         Autostart                   = false)


            : this(IPv4Address.Any,
                   Port,
                   ServiceBanner,
                   Mapper,
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPAddress, ServiceBanner, Port, Mapper, ...) <= main constructor

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress                      IPAddress,
                           IPPort                          Port,
                           String                          ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                  Mapper                      = null,
                           MapReduceDelegate               MapReduce                   = null,
                           String                          ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                  ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                         ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<TData>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                  PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                         PacketThreadsAreBackground  = true,
                           Boolean                         Autostart                   = false)

        {

            if (Mapper == null && MapReduce == null)
                throw new ArgumentNullException("The mapper and mapreduce delegate can not be both null!");

            this._IPAddress                 = IPAddress;
            this._IsMulticast               = IPAddress.IsMulticast;
            this._Port                      = Port;
            this._IPSocket                  = new IPSocket(_IPAddress, _Port);
            this.ServiceBanner              = ServiceBanner;
            this.Mapper                     = Mapper;
            this.MapReduce                  = MapReduce;
            this._ReceiverThreadName        = ReceiverThreadName;
            this._ReceiverThreadPriority    = ReceiverThreadPriority;
            this.PacketThreadsNameCreator   = (PacketThreadsNameCreator == null)
                                                  ? UDPpacket => "UDP packet from " + UDPpacket.RemoteSocket.IPAddress + ":" + UDPpacket.RemoteSocket.Port
                                                  : PacketThreadsNameCreator;
            this._PacketThreadsPriority      = PacketThreadsPriority;
            this._PacketThreadsAreBackground              = PacketThreadsAreBackground;

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

            if (Autostart)
                Start();

        }

        #endregion

        #region UDPReceiver(IPSocket, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket                        IPSocket,
                           String                          ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                  Mapper                      = null,
                           String                          ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                  ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                         ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<TData>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                  PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                         PacketThreadsAreBackground  = true,
                           Boolean                         Autostart                   = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceBanner,
                   Mapper,
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
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
                    Thread.CurrentThread.IsBackground  = PacketThreadsAreBackground;
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

                            Timestamp = DateTime.UtcNow;

                            if (NumberOfReceivedBytes > 0)
                            {

                                Interlocked.Exchange(ref WaitForChildTaskCreation, 1);

                                #region Per packet task(s)...

                                Task.Factory.StartNew(() =>
                                {

                                    // Create a local copies as we do not want to wait
                                    // till the new thread has accepted the packet
                                    // (Behaviour may change in .NET 4.5!)
                                    var Timestamp_Local                = Timestamp;
                                    var UDPPayload_Local               = UDPPayload;
                                    var OnNotificationLocal            = OnNotification;
                                    var RemoteSocket_Local             = new IPSocket((IPEndPoint) RemoteEndPoint);
                                    var NumberOfReceivedBytes_Local    = NumberOfReceivedBytes;
                                    var MapperLocal                    = Mapper;
                                    var MapReduceLocal                 = MapReduce;

#if __MonoCS__
                                    // Code for Mono C# compiler
#else
                                    Thread.CurrentThread.Priority      = PacketThreadsPriority;
                                    Thread.CurrentThread.IsBackground  = PacketThreadsAreBackground;
#endif

                                    Interlocked.Exchange(ref WaitForChildTaskCreation, 0);

                                    Array.Resize(ref UDPPayload_Local, NumberOfReceivedBytes_Local);

                                    if (Mapper != null)
                                    {

                                        var NewUDPPacket = new UDPPacket<TData>(this,
                                                                                Timestamp_Local,
                                                                                this.LocalSocket,
                                                                                RemoteSocket_Local,
                                                                                Mapper(this,
                                                                                       Timestamp_Local,
                                                                                       this.LocalSocket,
                                                                                       RemoteSocket_Local,
                                                                                       UDPPayload_Local)
                                                                           );

#if __MonoCS__
                                        // Code for Mono C# compiler
#else
                                        Thread.CurrentThread.Name = PacketThreadsNameCreator(NewUDPPacket);
#endif

                                        // Start upper-layer protocol processing
                                        if (OnNotificationLocal != null)
                                            OnNotificationLocal(NewUDPPacket);

                                    }

                                    else
                                    {

                                        foreach (var aaa in MapReduce(this,
                                                                      Timestamp_Local,
                                                                      this.LocalSocket,
                                                                      RemoteSocket_Local,
                                                                      UDPPayload_Local))
                                        {

                                            var NewUDPPacket = new UDPPacket<TData>(this,
                                                                                    Timestamp_Local,
                                                                                    this.LocalSocket,
                                                                                    RemoteSocket_Local,
                                                                                    aaa);

                                            // Start upper-layer protocol processing
                                            if (OnNotificationLocal != null)
                                                OnNotificationLocal(NewUDPPacket);

                                        }

                                    }


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
                            var OnErrorLocal = OnExceptionOccured;
                            if (OnErrorLocal != null)
                                OnErrorLocal(this, DateTime.UtcNow, e);
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

                OnExceptionOccured?.Invoke(this,
                                           DateTime.UtcNow,
                                           e);

            }

            OnStarted?.Invoke(this,
                              DateTime.UtcNow);

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

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the UDP receiver.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public void Shutdown(String  Message  = null,
                             Boolean Wait     = true)
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

            var OnCompletedLocal = OnCompleted;
            if (OnCompletedLocal != null)
                OnCompletedLocal(this, DateTime.UtcNow, Message);

        }

        #endregion


        #region (override) ToString()

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

        #region IDisposable Members

        public void Dispose()
        {

            //StopAndWait();

            //if (_TCPListener != null)
            //    _TCPListener.Stop();

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

        #region UDPReceiver(Port, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort                           Port,
                           String                           ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                   Mapper                      = null,
                           String                           ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                   PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground  = true,
                           Boolean                          Autostart                   = false)

            : this(IPv4Address.Any,
                   Port,
                   ServiceBanner,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPAddress, Port, ServiceBanner, Mapper, ...) <= main constructor

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen.</param>
        /// <param name="Port">The port to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress                       IPAddress,
                           IPPort                           Port,
                           String                           ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                   Mapper                      = null,
                           String                           ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                   PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground  = true,
                           Boolean                          Autostart                   = false)

            : base(IPAddress,
                   Port,
                   ServiceBanner,
                   (Mapper != null)
                       ? Mapper
                       : (UDPReceiver, Timestamp, LocalSocket, RemoteSocket, Message) => Message,
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

        #region UDPReceiver(IPSocket, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen.</param>
        /// <param name="ServiceBanner">Service banner.</param>
        /// <param name="Mapper">A delegate to transform the incoming UDP packets into custom data structures.</param>
        /// <param name="ReceiverThreadName">The optional name of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadPriority">The optional priority of the UDP receiver thread.</param>
        /// <param name="ReceiverThreadIsBackground">Whether the UDP receiver thread is a background thread or not.</param>
        /// <param name="PacketThreadsNameCreator">An optional delegate to set the name of the UDP packet threads.</param>
        /// <param name="PacketThreadsPriority">The optional priority of the UDP packet threads.</param>
        /// <param name="PacketThreadsAreBackground">Whether the UDP packet threads are background threads or not.</param>
        /// <param name="Autostart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket                         IPSocket,
                           String                           ServiceBanner               = DefaultServiceBanner,
                           MapperDelegate                   Mapper                      = null,
                           String                           ReceiverThreadName          = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority      = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground  = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator    = null,
                           ThreadPriority                   PacketThreadsPriority       = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground  = true,
                           Boolean                          Autostart                   = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServiceBanner,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   Autostart)

        { }

        #endregion

    }

    #endregion

}

