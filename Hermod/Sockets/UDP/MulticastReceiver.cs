///*
// * Copyright (c) 2011-2013, Achim 'ahzf' Friedland <achim@graphdefined.org>
// * This file is part of Styx <http://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;
//using System.Linq;
//using System.Text;
//using System.Net;
//using System.Net.Sockets;
//using System.Threading;
//using System.Threading.Tasks;

//using eu.Vanaheimr.Styx;
//using eu.Vanaheimr.Styx.Arrows;
//using eu.Vanaheimr.Hermod.Sockets.UDP;

//#endregion

//namespace eu.Vanaheimr.Hermod.Multicast
//{

//    /// <summary>
//    /// The UDPMulticastReceiverArrow receives messages from
//    /// to the given IP multicast group and forwards them to
//    /// this receivers.
//    /// </summary>
//    /// <typeparam name="TOut">The type of the consuming and emitting messages/objects.</typeparam>
//    public class MulticastReceiver<TOut> : IArrowSender<TOut>,
//                                           IArrowSender<UDPPacket<TOut>>,
//                                           IServer

//    {

//        #region Data

//        private readonly Socket                   MulticastSocket;
//        private readonly IPEndPoint               MulticastIPEndPoint;
//        private          Task                     ListenerTask;
//        private          EndPoint                 LocalEndPoint;
//        private          IPEndPoint               LocalIPEndPoint;
//        private          CancellationTokenSource  CancellationTokenSource;
//        private          CancellationToken        CancellationToken;
//        private readonly MapperDelegate           Mapper;
//        public  readonly IPSocket                 LocalSocket;

//        #endregion

//        #region Properties

//        #region IPAddress

//        private readonly IIPAddress _MulticastAddress;

//        /// <summary>
//        /// Gets the IPAddress on which the TCPServer listens.
//        /// </summary>
//        public IIPAddress IPAddress
//        {
//            get
//            {
//                return _MulticastAddress;
//            }
//        }

//        #endregion

//        #region Port

//        private readonly IPPort _Port;

//        /// <summary>
//        /// Gets the port on which the TCPServer listens.
//        /// </summary>
//        public IPPort Port
//        {
//            get
//            {
//                return _Port;
//            }
//        }

//        #endregion

//        #region BufferSize

//        /// <summary>
//        /// The size of the receive buffer.
//        /// </summary>
//        public UInt32 BufferSize { get; set; }

//        #endregion

//        #region HopCount

//        /// <summary>
//        /// The minimal acceptable IPv6 hop-count or IPv4 time-to-live value of the
//        /// incoming IP Multicast packets.
//        /// It is best practice for security applications to set the HopCount on the
//        /// sender side to its max value of 255 and configure an accept threshold on
//        /// the receiver side to 255. This way only packets from the local network
//        /// are accepted.
//        /// </summary>
//        public Byte HopCountThreshold { get; set; }

//        #endregion

//        #region IsRunning

//        private Int32 _IsRunning = 0;

//        /// <summary>
//        /// True while the server is listening for new clients
//        /// </summary>
//        public Boolean IsRunning
//        {
//            get
//            {
//                return _IsRunning == 1;
//            }
//        }

//        #endregion

//        #region StopRequested

//        /// <summary>
//        /// The server was requested to stop and will no
//        /// longer accept new client connections
//        /// </summary>
//        public Boolean StopRequested
//        {
//            get
//            {
//                return this.CancellationToken.IsCancellationRequested;
//            }
//        }

//        #endregion

//        public String          ThreadName   { get; private set; }
//        public ThreadPriority  ThreadPrio   { get; private set; }
//        public Boolean         IsBackground { get; private set; }

//        #endregion

//        #region Events

//        public delegate TOut MapperDelegate(DateTime Timestamp, IPSocket LocalSocket, IPSocket RemoteSocket, Byte[] Message);

//        event NotificationEventHandler<TOut>            OnNotification_Message;
//        event NotificationEventHandler<UDPPacket<TOut>> OnNotification_UDPPacket;

//        // INotification
//        event NotificationEventHandler<TOut> IArrowSender<TOut>.OnNotification
//        {
//            add    { OnNotification_Message += value; }
//            remove { OnNotification_Message -= value; }
//        }

//        event NotificationEventHandler<UDPPacket<TOut>> IArrowSender<UDPPacket<TOut>>.OnNotification
//        {
//            add    { OnNotification_UDPPacket += value; }
//            remove { OnNotification_UDPPacket -= value; }
//        }

//        public event ExceptionEventHandler OnException;
//        public event CompletedEventHandler OnCompleted;

//        #endregion

//        #region Constructor(s)

//        #region MulticastReceiver(MulticastAddress, IPPort, HopCountThreshold = 255)

//        /// <summary>
//        /// The UDPMulticastReceiverArrow receives messages from
//        /// to the given IP multicast group and forwards them to
//        /// this receivers.
//        /// </summary>
//        /// <param name="MulticastAddress">The multicast address to join.</param>
//        /// <param name="IPPort">The outgoing IP port to use.</param>
//        /// <param name="HopCountThreshold">The minimal acceptable IPv6 hop-count or IPv4 time-to-live value of the incoming IP Multicast packets.</param>
//        public MulticastReceiver(IIPAddress      MulticastAddress,
//                                 IPPort          Port,
//                                 Byte            HopCountThreshold  = 255,
//                                 MapperDelegate  Mapper             = null,
//                                 String          ThreadName         = "UDPServer thread",
//                                 ThreadPriority  ThreadPrio         = ThreadPriority.AboveNormal,
//                                 Boolean         IsBackground       = true,
//                                 Boolean         Autostart          = false)
//        {

//            if (Mapper == null)
//                throw new ArgumentNullException("The mapper delegate must not be null!");

//            this._MulticastAddress        = MulticastAddress;
//            this._Port                    = Port;
//            this.Mapper                   = Mapper;
//            this.BufferSize               = BufferSize;
//            this.MulticastSocket          = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
//            this.MulticastIPEndPoint      = new IPEndPoint(System.Net.IPAddress.Parse(_MulticastAddress.ToString()), Port.ToInt32());
//            this.LocalIPEndPoint          = new IPEndPoint(System.Net.IPAddress.Any, Port.ToInt32());
//            this.LocalEndPoint            = (EndPoint) LocalIPEndPoint;
//            this.CancellationTokenSource  = new CancellationTokenSource();
//            this.CancellationToken        = CancellationTokenSource.Token;
//            this.BufferSize               = 65536;
//            this.ThreadName               = ThreadName;
//            this.ThreadPrio               = ThreadPrio;
//            this.IsBackground             = IsBackground;

//            MulticastSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1000);
//            MulticastSocket.Bind(LocalIPEndPoint);
//            MulticastSocket.SetSocketOption(SocketOptionLevel.IP,
//                                            SocketOptionName.AddMembership,
//                                            new MulticastOption(System.Net.IPAddress.Parse(_MulticastAddress.ToString())));

//        }

//        #endregion

//        #endregion


//        #region Close()

//        /// <summary>
//        /// Close the multicast socket.
//        /// </summary>
//        public void Close()
//        {
//            MulticastSocket.Close();
//        }

//        #endregion


//        #region Start()

//        /// <summary>
//        /// Start the multicast receiver.
//        /// </summary>
//        public void Start()
//        {

//            if (_IsRunning == 1)
//                return;

//            try
//            {

//                this.ListenerTask = Task.Factory.StartNew(() =>
//                {

//                    Thread.CurrentThread.Name          = ThreadName;
//                    Thread.CurrentThread.Priority      = ThreadPrio;
//                    Thread.CurrentThread.IsBackground  = IsBackground;

//                    EndPoint RemoteEndPoint = null;
//                    Byte[]   UDPPacket;
//                    Int32    NumberOfReceivedBytes;
//                    DateTime Timestamp;
//                    Int32    WaitForChildTaskCreation = 0;

//                    Interlocked.Exchange(ref _IsRunning, 1);

//                    #region ReceiverLoop

//                    while (!CancellationToken.IsCancellationRequested)
//                    {

//                        UDPPacket = new Byte[this.BufferSize];
//                        RemoteEndPoint = new IPEndPoint(0, 0);

//                        // Wait for the next packet...
//                        NumberOfReceivedBytes = MulticastSocket.ReceiveFrom(UDPPacket, ref RemoteEndPoint);
//                        Timestamp = DateTime.Now;

//                        if (NumberOfReceivedBytes > 0)
//                        {

//                            Interlocked.Exchange(ref WaitForChildTaskCreation, 1);

//                            #region Inner task

//                            Task.Factory.StartNew(() =>
//                            {

//                                var RemoteSocketLocal = new IPSocket((IPEndPoint) RemoteEndPoint);
//                                Thread.CurrentThread.Name = "Multicast packet from " + RemoteSocketLocal.IPAddress + ":" + RemoteSocketLocal.Port;

//                                // Create a local copy of the UDPPacket and RemoteEndPoint as we
//                                // do not want to wait till the new thread has accepted the packet

//                                Array.Resize(ref UDPPacket, NumberOfReceivedBytes);

//                                var TimestampLocal                  = Timestamp;
//                                var UDPPacketLocal                  = UDPPacket;
//                                var OnNotificationLocal             = OnNotification_UDPPacket;
//                                var OnNotification_Message_Local    = OnNotification_Message;
//                                var OnNotification_UDPPacket_Local  = OnNotification_UDPPacket;

//                                Interlocked.Exchange(ref WaitForChildTaskCreation, 0);

//                                // Start upper-layer protocol processing
//                                if (OnNotification_Message_Local != null)
//                                    OnNotification_Message_Local(Mapper(TimestampLocal,
//                                                                        this.LocalSocket,
//                                                                        RemoteSocketLocal,
//                                                                        UDPPacketLocal));

//                                if (OnNotification_UDPPacket_Local != null)
//                                    OnNotification_UDPPacket_Local(new UDPPacket<TOut>(
//                                                                       TimestampLocal,
//                                                                       this.LocalSocket,
//                                                                       RemoteSocketLocal,
//                                                                       Mapper(TimestampLocal,
//                                                                              this.LocalSocket,
//                                                                              RemoteSocketLocal,
//                                                                              UDPPacketLocal)
//                                                                  ));


//                            }, CancellationTokenSource.Token,
//                               TaskCreationOptions.AttachedToParent,
//                               TaskScheduler.Default);

//                            #endregion

//                            // Wait till the new Task had used some of its time to
//                            // make a copy of the given references.
//                            while (WaitForChildTaskCreation > 0)
//                                Thread.Sleep(1);

//                        }

//                    }

//                    #endregion

//                    Interlocked.Exchange(ref _IsRunning, 0);

//                }, CancellationTokenSource.Token,
//                   TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent,
//                   TaskScheduler.Default);

//            }
//            catch (Exception ex)
//            {
//                var OnErrorLocal = OnException;
//                if (OnErrorLocal != null)
//                    OnErrorLocal(this, ex);
//            }

//        }

//        #endregion

//        #region Start(Delay, InBackground = true)

//        /// <summary>
//        /// Start the multicast receiver after a little delay.
//        /// </summary>
//        /// <param name="Delay">The delay.</param>
//        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
//        public void Start(TimeSpan Delay, Boolean InBackground = true)
//        {

//            if (!InBackground)
//            {
//                Thread.Sleep(Delay);
//                Start();
//            }

//            else
//                Task.Factory.StartNew(() =>
//                {

//                    Thread.Sleep(Delay);
//                    Start();

//                }, CancellationTokenSource.Token,
//                   TaskCreationOptions.AttachedToParent,
//                   TaskScheduler.Default);

//        }

//        #endregion


//        public void Start_old()
//        {

//            //ListenerThread = Task.Factory.StartNew((Object) =>
//            //{

//            //    MulticastSocket.SetSocketOption(SocketOptionLevel.Socket,
//            //                                    SocketOptionName.ReceiveTimeout, 1000);

//            //    MulticastSocket.Bind(LocalIPEndPoint);

//            //    MulticastSocket.SetSocketOption(SocketOptionLevel.IP,
//            //                                    SocketOptionName.AddMembership,
//            //                                    new MulticastOption(System.Net.IPAddress.Parse(_MulticastAddress.ToString())));

//            //    DateTime Timestamp;


//            //    while (!CancellationToken.IsCancellationRequested)
//            //    {

//            //        var data = new Byte[65536];

//            //        try
//            //        {

//            //            var _NumberOfReceivedBytes = MulticastSocket.ReceiveFrom(data, ref LocalEndPoint);
//            //            Timestamp = DateTime.Now;

//            //            Array.Resize(ref data, _NumberOfReceivedBytes);

//            //            if (OnNotification_Message != null)
//            //                OnNotification_Message(
//            //                    //new ArrowIPSource(
//            //                    //    (LocalEndPoint as IPEndPoint).Address.ToString(),
//            //                    //    IPPort.Parse((LocalEndPoint as IPEndPoint).Port)
//            //                    //),
//            //                                 Mapper(Timestamp, null, null, data));

//            //        }

//            //        // Catch ReadTimeout...
//            //        catch (SocketException SocketException)
//            //        { }

//            //    }

//            //}, TaskCreationOptions.LongRunning,
//            //   CancellationTokenSource.Token,
//            //   TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent,
//            //   TaskScheduler.Default);

//        }

//        #region Shutdown(Wait = true)

//        /// <summary>
//        /// Shutdown the UDP multicast listener.
//        /// </summary>
//        /// <param name="Wait">Wait until the server finally shutted down.</param>
//        public void Shutdown(Boolean Wait = true)
//        {

//            if (ListenerTask == null)
//                throw new Exception("You can not stop the listener if it wasn't started before!");

//            this.CancellationTokenSource.Cancel();

//            if (Wait)
//                while (_IsRunning > 0)
//                    Thread.Sleep(10);

//        }

//        #endregion


//        public void Dispose()
//        {
//            throw new NotImplementedException();
//        }



//        public string ServiceBanner
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//            set
//            {
//                throw new NotImplementedException();
//            }
//        }


//        public event OnStartedDelegate OnStarted;


//    }

//}
