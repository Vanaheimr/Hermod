/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UDP
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
        private readonly MapperDelegate?                 Mapper;
        private readonly MapReduceDelegate?              MapReduce;
        private          CancellationTokenSource         CancellationTokenSource;
        private          CancellationToken               CancellationToken;

        public  const    String                          DefaultServiceBanner      = "Vanaheimr UDP/CSV Service v0.8";

        #endregion

        #region Properties

        // readonly

        /// <summary>
        /// Gets the IPAddress on which the UDP receiver listens.
        /// </summary>
        public IIPAddress IPAddress
            => IPSocket.IPAddress;

        /// <summary>
        /// Whether this UDP receiver is listening
        /// on a multicast IP address or not.
        /// </summary>
        public Boolean IsMulticast
            => IPSocket.IPAddress.IsMulticast;

        /// <summary>
        /// Gets the port on which the UDP receiver listens.
        /// </summary>
        public IPPort TCPPort
            => IPSocket.Port;

        /// <summary>
        /// Gets the IP socket on which the UDP receiver listens.
        /// </summary>
        public IPSocket IPSocket { get; }




        /// <summary>
        /// The name of the UDP receiver thread.
        /// </summary>
        public String ReceiverThreadName { get; }

        /// <summary>
        /// The priority of the UDP receiver thread.
        /// </summary>
        public ThreadPriority ReceiverThreadPriority { get; }

        /// <summary>
        /// Whether the UDP receiver thread is a background thread or not.
        /// </summary>
        public Boolean ReceiverThreadIsBackground { get; }

        /// <summary>
        /// The priority of the UDP packet threads.
        /// </summary>
        public ThreadPriority PacketThreadsPriority { get; }

        /// <summary>
        /// Whether the UDP packet threads are background threads or not.
        /// </summary>
        public Boolean PacketThreadsAreBackground { get; }



        // mutatable

        public String ServiceBanner { get; set; }

        /// <summary>
        /// The size of the receive buffer.
        /// </summary>
        public UInt32 BufferSize { get; set; }

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

        private Int32 _IsRunning = 0;

        /// <summary>
        /// True while the server is listening for new clients
        /// </summary>
        public Boolean IsRunning
            => _IsRunning == 1;

        /// <summary>
        /// The server was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean StopRequested
            => this.CancellationToken.IsCancellationRequested;

        #endregion

        #region Events

        /// <summary>
        /// An event fired when the UDP receiver started.
        /// </summary>
        public event StartedEventHandler?                         OnStarted;

        /// <summary>
        /// An event fired for every incoming UDP packet.
        /// </summary>
        public event NotificationEventHandler<UDPPacket<TData>>?  OnNotification;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccurredEventHandler?                OnExceptionOccurred;

        /// <summary>
        /// An event fired when the UDP receiver stopped.
        /// </summary>
        public event CompletedEventHandler?                       OnCompleted;

        #endregion

        #region Delegates

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
                                             DateTimeOffset      Timestamp,
                                             IPSocket            LocalSocket,
                                             IPSocket            RemoteSocket,
                                             Byte[]              Payload);

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
                                                             DateTimeOffset      Timestamp,
                                                             IPSocket            LocalSocket,
                                                             IPSocket            RemoteSocket,
                                                             Byte[]              Payload);

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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort                           Port,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate?                  Mapper                       = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<TData>, String>?  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)


            : this(new IPSocket(IPv4Address.Any, Port),
                   ServiceBanner,
                   Mapper,
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   AutoStart)

        { }

        #endregion

        #region UDPReceiver(IPSocket, ServiceBanner, Mapper, ...) <= main constructor

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket                         IPSocket,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate?                  Mapper                       = null,
                           MapReduceDelegate?               MapReduce                    = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<TData>, String>?  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)

        {

            if (Mapper is null && MapReduce is null)
                throw new ArgumentNullException("The mapper and mapreduce delegate can not be both null!");

            this.IPSocket                     = IPSocket;
            this.ServiceBanner                = ServiceBanner;
            this.Mapper                       = Mapper;
            this.MapReduce                    = MapReduce;
            this.ReceiverThreadName           = ReceiverThreadName;
            this.ReceiverThreadPriority       = ReceiverThreadPriority;
            this.PacketThreadsNameCreator     = PacketThreadsNameCreator ?? (UDPpacket => "UDP packet from " + UDPpacket.RemoteSocket.IPAddress + ":" + UDPpacket.RemoteSocket.Port);
            this.PacketThreadsPriority        = PacketThreadsPriority;
            this.PacketThreadsAreBackground   = PacketThreadsAreBackground;

            var LocalIPEndPoint               = new IPEndPoint(new System.Net.IPAddress(this.IPAddress.GetBytes()), this.TCPPort.ToInt32());
            this.LocalSocket                  = IPSocket.FromIPEndPoint(LocalIPEndPoint);
            this.LocalDotNetSocket            = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.LocalDotNetSocket.Bind(LocalIPEndPoint);

            this.BufferSize                   = 65536;
            this.ReceiveTimeout               = 5000;

            if (IsMulticast)
            {

                LocalDotNetSocket.SetSocketOption(SocketOptionLevel.IP,
                                                  SocketOptionName.AddMembership,
                                                  new MulticastOption(System.Net.IPAddress.Parse(this.IPAddress.ToString()),
                                                                      System.Net.IPAddress.Any));

            }

            this.CancellationTokenSource  = new CancellationTokenSource();
            this.CancellationToken        = CancellationTokenSource.Token;

            if (AutoStart)
                Start();

        }

        #endregion

        #region UDPReceiver(IPAddress, Port, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress                       IPAddress,
                           IPPort                           Port,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate?                  Mapper                       = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<TData>, String>?  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)

            : this(new IPSocket(IPAddress, Port),
                   ServiceBanner,
                   Mapper,
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   AutoStart)

        { }

        #endregion

        #endregion


        #region Start(EventTrackingId = null)

        /// <summary>
        /// Start the UDP receiver.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public async Task<Boolean> Start(EventTracking_Id? EventTrackingId = null)
        {

            if (_IsRunning == 1)
                return false;

            EventTrackingId ??= EventTracking_Id.New;

            try
            {

                this.ReceiverTask = Task.Factory.StartNew(() => {

#if __MonoCS__
                    // Code for Mono C# compiler
#else
                    Thread.CurrentThread.Name          = ReceiverThreadName;
                    Thread.CurrentThread.Priority      = ReceiverThreadPriority;
                    Thread.CurrentThread.IsBackground  = PacketThreadsAreBackground;
#endif

                    EndPoint?       RemoteEndPoint = null;
                    Byte[]          UDPPayload;
                    Int32           NumberOfReceivedBytes;
                    DateTimeOffset  timestamp;
                    Int32           WaitForChildTaskCreation = 0;

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

                            timestamp = Timestamp.Now;

                            if (NumberOfReceivedBytes > 0)
                            {

                                Interlocked.Exchange(ref WaitForChildTaskCreation, 1);

                                #region Per packet task(s)...

                                Task.Factory.StartNew(() => {

                                    // Create a local copies as we do not want to wait
                                    // till the new thread has accepted the packet
                                    // (Behaviour may change in .NET 4.5!)
                                    var Timestamp_Local                = timestamp;
                                    var UDPPayload_Local               = UDPPayload;
                                    var OnNotificationLocal            = OnNotification;
                                    var RemoteSocket_Local             = IPSocket.FromIPEndPoint(RemoteEndPoint) ?? IPSocket.Zero;
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
                                        OnNotificationLocal?.Invoke(EventTracking_Id.New, NewUDPPacket);

                                    }

                                    else
                                    {

                                        foreach (var aaa in MapReduce(this,
                                                                      Timestamp_Local,
                                                                      this.LocalSocket,
                                                                      RemoteSocket_Local,
                                                                      UDPPayload_Local))
                                        {

                                            // Start upper-layer protocol processing
                                            OnNotificationLocal?.Invoke(
                                                EventTracking_Id.New,
                                                new UDPPacket<TData>(
                                                    this,
                                                    Timestamp_Local,
                                                    this.LocalSocket,
                                                    RemoteSocket_Local,
                                                    aaa
                                                )
                                            );

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
                            OnExceptionOccurred?.Invoke(this, Timestamp.Now, EventTrackingId, e);
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

                OnExceptionOccurred?.Invoke(
                    this,
                    Timestamp.Now,
                    EventTrackingId,
                    e
                );

            }

            OnStarted?.Invoke(
                this,
                Timestamp.Now,
                EventTrackingId
            );

            return true;

        }

        #endregion

        #region Start(Delay, EventTrackingId = null, InBackground = true)

        /// <summary>
        /// Start the UDP receiver after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public async Task<Boolean> Start(TimeSpan           Delay,
                                         EventTracking_Id?  EventTrackingId   = null,
                                         Boolean            InBackground      = true)
        {

            if (!InBackground)
            {
                await Task.Delay(Delay);
                return await Start();
            }

            else
            {
                await Task.Factory.StartNew(async () => {

                    Thread.Sleep(Delay);
                    await Start();

                }, CancellationTokenSource.Token,
                   TaskCreationOptions.AttachedToParent,
                   TaskScheduler.Default);
            }

            return true;

        }

        #endregion

        #region Shutdown(EventTrackingId = null, Message = null, Wait = true)

        /// <summary>
        /// Shutdown the UDP receiver.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public async Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                            String?            Message           = null,
                                            Boolean            Wait              = true)
        {

            if (IsMulticast)
            {

                LocalDotNetSocket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.DropMembership,
                    new MulticastOption(
                        System.Net.IPAddress.Parse(IPAddress.ToString()),
                        System.Net.IPAddress.Any
                    )
                );

            }

            this.CancellationTokenSource.Cancel();

            if (Wait)
                while (_IsRunning > 0)
                    Thread.Sleep(10);

            OnCompleted?.Invoke(
                this,
                Timestamp.Now,
                EventTrackingId ?? EventTracking_Id.New,
                Message
            );

            return true;

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

            return String.Concat(ServiceBanner, " [", _TypeName, _GenericType, "] on ", IPSocket.ToString(), _Running);

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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPPort                           Port,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate                   Mapper                       = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)

            : this(new IPSocket(IPv4Address.Any, Port),
                   ServiceBanner,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   AutoStart)

        { }

        #endregion

        #region UDPReceiver(IPSocket, ServiceBanner, Mapper, ...) <= main constructor

        /// <summary>
        /// Create a new UDP receiver listening on the given IP address and port.
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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IPSocket                         IPSocket,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate                   Mapper                       = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)

            : base(IPSocket,
                   ServiceBanner,
                   Mapper ?? ((UDPReceiver, Timestamp, LocalSocket, RemoteSocket, Message) => Message),
                   null,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   AutoStart)

        { }

        #endregion

        #region UDPReceiver(IPAddress, Port, ServiceBanner, Mapper, ...)

        /// <summary>
        /// Create a new UDP receiver listening on the given IP socket.
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
        /// <param name="AutoStart">Start the UDP receiver thread immediately.</param>
        public UDPReceiver(IIPAddress                       IPAddress,
                           IPPort                           Port,
                           String                           ServiceBanner                = DefaultServiceBanner,
                           MapperDelegate                   Mapper                       = null,
                           String                           ReceiverThreadName           = "UDP receiver thread",
                           ThreadPriority                   ReceiverThreadPriority       = ThreadPriority.AboveNormal,
                           Boolean                          ReceiverThreadIsBackground   = true,
                           Func<UDPPacket<Byte[]>, String>  PacketThreadsNameCreator     = null,
                           ThreadPriority                   PacketThreadsPriority        = ThreadPriority.AboveNormal,
                           Boolean                          PacketThreadsAreBackground   = true,
                           Boolean                          AutoStart                    = false)

            : this(new IPSocket(IPAddress, Port),
                   ServiceBanner,
                   Mapper,
                   ReceiverThreadName,
                   ReceiverThreadPriority,
                   ReceiverThreadIsBackground,
                   PacketThreadsNameCreator,
                   PacketThreadsPriority,
                   PacketThreadsAreBackground,
                   AutoStart)

        { }

        #endregion

    }

    #endregion

}

