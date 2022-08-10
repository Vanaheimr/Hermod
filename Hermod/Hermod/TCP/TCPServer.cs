/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

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
        /// The default TCP service name.
        /// </summary>
        public  const            String                                         __DefaultServiceName            = "TCP Server";

        /// <summary>
        /// The default TCP service banner.
        /// </summary>
        public  const            String                                         __DefaultServiceBanner          = "Vanaheimr Hermod TCP Server v0.9";

        /// <summary>
        /// The default server thread name.
        /// </summary>
        public  const            String                                         __DefaultServerThreadName       = "TCP Server thread on ";

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

        private        volatile  Boolean                                        _IsRunning       = false;
        private        volatile  Boolean                                        _StopRequested   = false;

        // The internal thread
        private        readonly  Thread                                         _ListenerThread;
        private                  CancellationTokenSource                        CancellationTokenSource;
        private                  CancellationToken                              CancellationToken;

        #endregion

        #region Properties

        /// <summary>
        /// The TCP service name shown e.g. on service startup.
        /// </summary>
        public String                            ServiceName                            { get; set; }

        /// <summary>
        /// Gets the IPAddress on which the TCP server listens.
        /// </summary>
        public IIPAddress                        IPAddress                              { get; }

        /// <summary>
        /// Gets the port on which the TCP server listens.
        /// </summary>
        public IPPort                            Port                                   { get; }

        /// <summary>
        /// Gets the IP socket on which the TCP server listens.
        /// </summary>
        public IPSocket                          IPSocket                               { get; }

        /// <summary>
        /// The optional SSL/TLS certificate.
        /// </summary>
        //public X509Certificate2                  ServerCertificate                      { get; }

        /// <summary>
        /// Whether SSL/TLS client certification is required.
        /// </summary>
        public Boolean                           ClientCertificateRequired              { get; }

        /// <summary>
        /// Whether SSL/TLS client certificate revokation should be verified.
        /// </summary>
        public Boolean                           CheckCertificateRevocation             { get; }

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
        /// </summary>
        public String                            ServiceBanner                          { get; set; }


        /// <summary>
        /// The optional name of the TCP server thread.
        /// </summary>
        public String                            ServerThreadName                       { get; set; }

        /// <summary>
        /// The optional priority of the TCP server thread.
        /// </summary>
        public ThreadPriority                    ServerThreadPriority                   { get; set; }

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean                           ServerThreadIsBackground               { get; set; }



        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder               ConnectionIdBuilder                    { get; set; }


        /// <summary>
        /// A delegate to set the name of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder           { get; set; }

        /// <summary>
        /// A delegate to set the priority of the TCP connection threads.
        /// </summary>
        public ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder       { get; set; }

        /// <summary>
        /// Whether the TCP server thread is a background thread or not.
        /// </summary>
        public Boolean                           ConnectionThreadsAreBackground         { get; set; }

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan                          ConnectionTimeout                      { get; set; }

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32                            MaxClientConnections                   { get; set; }

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt64                            NumberOfClients
            => (UInt64) _TCPConnections.Count;

        /// <summary>
        /// True while the TCPServer is listening for new clients
        /// </summary>
        public Boolean                           IsRunning
            => _IsRunning;

        /// <summary>
        /// The TCPServer was requested to stop and will no
        /// longer accept new client connections
        /// </summary>
        public Boolean                           StopRequested
            => _StopRequested;

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        public event StartedEventHandler?                           OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// If this event closes the TCP connection the OnNotification event will never be fired!
        /// Therefore you can use this event for filtering connection initiation requests.
        /// </summary>
        public event NewConnectionHandler?                          OnNewConnection;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// </summary>
        public event NotificationEventHandler<TCPConnection>?       OnNotification;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccuredEventHandler?                  OnExceptionOccured;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        public event ConnectionClosedHandler?                       OnConnectionClosed;

        /// <summary>
        /// An event fired whenever the TCP servers instance was stopped.
        /// </summary>
        public event CompletedEventHandler?                         OnCompleted;

        #endregion

        #region Constructor(s)

        #region TCPServer(Port, ...)

        /// <summary>
        /// Initialize the TCP server using IPAddress.Any and the given parameters.
        /// </summary>
        /// <param name="Port">The listening port</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
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
        public TCPServer(IPPort                                Port,
                         ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                         RemoteCertificateValidationCallback?  ClientCertificateValidator         = null,
                         LocalCertificateSelectionCallback?    ClientCertificateSelector          = null,
                         SslProtocols?                         AllowedTLSProtocols                = null,
                         Boolean?                              ClientCertificateRequired          = null,
                         Boolean?                              CheckCertificateRevocation         = null,

                         String                                ServiceName                        = __DefaultServiceName,
                         String                                ServiceBanner                      = __DefaultServiceBanner,
                         String?                               ServerThreadName                   = null,
                         ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                         Boolean                               ServerThreadIsBackground           = true,

                         ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                         ConnectionThreadsNameBuilder?         ConnectionThreadsNameBuilder       = null,
                         ConnectionThreadsPriorityBuilder?     ConnectionThreadsPriorityBuilder   = null,
                         Boolean                               ConnectionThreadsAreBackground     = true,
                         TimeSpan?                             ConnectionTimeout                  = null,

                         UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                         Boolean                               Autostart                          = false)

            : this(IPv4Address.Any,
                   Port,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServiceName,
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
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
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
        public TCPServer(IIPAddress                            IIPAddress,
                         IPPort                                Port,
                         ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                         RemoteCertificateValidationCallback?  ClientCertificateValidator         = null,
                         LocalCertificateSelectionCallback?    ClientCertificateSelector          = null,
                         SslProtocols?                         AllowedTLSProtocols                = null,
                         Boolean?                              ClientCertificateRequired          = null,
                         Boolean?                              CheckCertificateRevocation         = null,

                         String?                               ServiceName                        = __DefaultServiceName,
                         String?                               ServiceBanner                      = __DefaultServiceBanner,
                         String?                               ServerThreadName                   = null,
                         ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                         Boolean                               ServerThreadIsBackground           = true,

                         ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                         ConnectionThreadsNameBuilder?         ConnectionThreadsNameBuilder       = null,
                         ConnectionThreadsPriorityBuilder?     ConnectionThreadsPriorityBuilder   = null,
                         Boolean                               ConnectionThreadsAreBackground     = true,
                         TimeSpan?                             ConnectionTimeout                  = null,

                         UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                         Boolean                               Autostart                          = false)

        {

            // TCP Socket
            this.IPAddress                          = IIPAddress;
            this.Port                               = Port;
           // this.ServerCertificate                  = ServerCertificate;
            this.ClientCertificateRequired          = ClientCertificateRequired  ?? false;
            this.CheckCertificateRevocation         = CheckCertificateRevocation ?? false;
            this.IPSocket                           = new IPSocket   (this.IPAddress,
                                                                      this.Port);
            this._TCPListener                       = new TcpListener(new System.Net.IPAddress(this.IPAddress.GetBytes()),
                                                                      this.Port.ToInt32());

            // TCP Server
            this.ServiceName                        = ServiceName      is not null && ServiceName.     Trim().IsNotNullOrEmpty()
                                                          ? ServiceName
                                                          : __DefaultServiceName;
            this.ServiceBanner                      = ServiceBanner    is not null && ServiceBanner.   Trim().IsNotNullOrEmpty()
                                                          ? ServiceBanner
                                                          : __DefaultServiceBanner;
            this.ServerThreadName                   = ServerThreadName is not null && ServerThreadName.Trim().IsNotNullOrEmpty()
                                                          ? ServerThreadName
                                                          : __DefaultServerThreadName;
            this.ServerThreadPriority               = ServerThreadPriority;
            this.ServerThreadIsBackground           = ServerThreadIsBackground;

            // TCP Connections
            this._TCPConnections                    = new ConcurrentDictionary<IPSocket, TCPConnection>();
            this.ConnectionIdBuilder                = ConnectionIdBuilder              ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP Server:"        + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port);
            this.ConnectionThreadsNameBuilder       = ConnectionThreadsNameBuilder     ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => "TCP Server thread " + RemoteIPSocket.IPAddress + ":" + RemoteIPSocket.Port);
            this.ConnectionThreadsPriorityBuilder   = ConnectionThreadsPriorityBuilder ?? ((Sender, Timestamp, LocalSocket, RemoteIPSocket) => ThreadPriority.AboveNormal);
            this.ConnectionThreadsAreBackground     = ConnectionThreadsAreBackground;
            this.ConnectionTimeout                  = ConnectionTimeout ?? TimeSpan.FromSeconds(30);

            this.MaxClientConnections               = MaxClientConnections;


            #region TCP Listener Thread

            this.CancellationTokenSource            = new CancellationTokenSource();
            this.CancellationToken                  = CancellationTokenSource.Token;

            _ListenerThread = new Thread(async ()  => {

                Thread.CurrentThread.Name           = this.ServerThreadName;
                Thread.CurrentThread.Priority       = this.ServerThreadPriority;
                Thread.CurrentThread.IsBackground   = this.ServerThreadIsBackground;

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

                        var tcpConnection = new TCPConnection(TCPServer:                   this,
                                                              TCPClient:                   _TCPListener.AcceptTcpClient(),
                                                              ServerCertificateSelector:   ServerCertificateSelector,
                                                              ClientCertificateValidator:  ClientCertificateValidator,
                                                              ClientCertificateSelector:   ClientCertificateSelector,
                                                              AllowedTLSProtocols:         AllowedTLSProtocols,
                                                              ReadTimeout:                 ConnectionTimeout,
                                                              WriteTimeout:                ConnectionTimeout);

                        // Store the new connection
                        //_SocketConnections.AddOrUpdate(_TCPConnection.Value.RemoteSocket,
                        //                               _TCPConnection.Value,
                        //                               (RemoteEndPoint, TCPConnection) => TCPConnection);

                        await Task.Factory.StartNew(connection => {

                            try
                            {

                                var newTCPConnection = connection as TCPConnection;

                                if (newTCPConnection is not null)
                                {

                                    #region Copy ExceptionOccured event handlers

                                    //foreach (var ExceptionOccuredHandler in MyEventStorage)
                                    //    _TCPConnection.Value.OnExceptionOccured += ExceptionOccuredHandler;

                                    #endregion


                                    DebugX.Log("New TCP connection on " + newTCPConnection.TCPServer.IPSocket.ToString() + " from " + (newTCPConnection.TCPClient.Client.RemoteEndPoint?.ToString() ?? ""));

                                    // If this event closes the TCP connection the OnNotification event will never be fired!
                                    // Therefore you can use this event for filtering connection initiation requests.
                                    OnNewConnection?.Invoke(newTCPConnection.TCPServer,
                                                            newTCPConnection.ServerTimestamp,
                                                            newTCPConnection.RemoteSocket,
                                                            newTCPConnection.ConnectionId,
                                                            newTCPConnection);

                                    if (!newTCPConnection.IsClosed)
                                        OnNotification?.Invoke(newTCPConnection);

                                }

                            }
                            catch (Exception e)
                            {

                                while (e.InnerException is not null)
                                    e = e.InnerException;

                                OnExceptionOccured?.Invoke(this,
                                                           Timestamp.Now,
                                                           e);

                                DebugX.LogT(Timestamp.Now + " [" + nameof(TCPServer) + "] " + e.Message + Environment.NewLine + e.StackTrace);

                            }

                        }, tcpConnection);

                    }

                    #region Shutdown

                    // Request all client connections to finish!
                    foreach (var _SocketConnection in _TCPConnections)
                        _SocketConnection.Value.StopRequested = true;

                    // After stopping the TCPListener wait for
                    // all client connections to finish!
                    while (!_TCPConnections.IsEmpty)
                        Thread.Sleep(5);

                    #endregion

                }

                #region Exception handling

                catch (Exception Exception)
                {
                    var OnExceptionLocal = OnExceptionOccured;
                    if (OnExceptionLocal is not null)
                        OnExceptionLocal(this, Timestamp.Now, Exception);
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
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServiceBanner">The TCP service banner.</param>
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
        public TCPServer(IPSocket                              IPSocket,
                         ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                         RemoteCertificateValidationCallback?  ClientCertificateValidator         = null,
                         LocalCertificateSelectionCallback?    ClientCertificateSelector          = null,
                         SslProtocols?                         AllowedTLSProtocols                = null,
                         Boolean?                              ClientCertificateRequired          = null,
                         Boolean?                              CheckCertificateRevocation         = null,

                         String                                ServiceName                        = __DefaultServiceName,
                         String                                ServiceBanner                      = __DefaultServiceBanner,
                         String                                ServerThreadName                   = null,
                         ThreadPriority                        ServerThreadPriority               = ThreadPriority.AboveNormal,
                         Boolean                               ServerThreadIsBackground           = true,

                         ConnectionIdBuilder                   ConnectionIdBuilder                = null,
                         ConnectionThreadsNameBuilder          ConnectionThreadsNameBuilder       = null,
                         ConnectionThreadsPriorityBuilder      ConnectionThreadsPriorityBuilder   = null,
                         Boolean                               ConnectionThreadsAreBackground     = true,
                         TimeSpan?                             ConnectionTimeout                  = null,

                         UInt32                                MaxClientConnections               = __DefaultMaxClientConnections,
                         Boolean                               Autostart                          = false)

            : this(IPSocket.IPAddress,
                   IPSocket.Port,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServiceName,
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

        /// <summary>
        /// Send a "new connection" event.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the request.</param>
        /// <param name="RemoteSocket">The remote socket that was closed.</param>
        /// <param name="ConnectionId">The internal connection identification.</param>
        /// <param name="TCPConnection">The connection itself.</param>
        protected internal void SendNewConnection(DateTime       ServerTimestamp,
                                                  IPSocket       RemoteSocket,
                                                  String         ConnectionId,
                                                  TCPConnection  TCPConnection)
        {

            try
            {

                OnNewConnection?.Invoke(this,
                                        ServerTimestamp,
                                        RemoteSocket,
                                        ConnectionId,
                                        TCPConnection);

            }
            catch (Exception)
            { }

        }

        #endregion

        #region (protected internal) SendConnectionClosed(ServerTimestamp, RemoteSocket, ConnectionId, ClosedBy)

        /// <summary>
        /// Send a "connection closed" event.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="RemoteSocket">The remote socket that was closed.</param>
        /// <param name="ConnectionId">The internal connection identification.</param>
        /// <param name="ClosedBy">Whether it was closed by us or by the client.</param>
        protected internal void SendConnectionClosed(DateTime            ServerTimestamp,
                                                     IPSocket            RemoteSocket,
                                                     String              ConnectionId,
                                                     ConnectionClosedBy  ClosedBy)
        {

            try
            {

                OnConnectionClosed?.Invoke(this,
                                           ServerTimestamp,
                                           RemoteSocket,
                                           ConnectionId,
                                           ClosedBy);

            }
            catch (Exception)
            { }

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public Boolean Start()

            => Start(__DefaultMaxClientConnections);

        #endregion

        #region Start(Delay, InBackground = true)

        /// <summary>
        /// Start the TCP receiver after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public Boolean Start(TimeSpan  Delay,
                             Boolean   InBackground  = true)
        {

            if (!InBackground)
            {
                Thread.Sleep(Delay);
                return Start();
            }

            else
            {
                Task.Factory.StartNew(() => {

                    Thread.Sleep(Delay);
                    Start();

                }, CancellationTokenSource.Token,
                   TaskCreationOptions.AttachedToParent,
                   TaskScheduler.Default);
            }

            return true;

        }

        #endregion

        #region Start(MaxClientConnections)

        /// <summary>
        /// Start the TCPServer thread
        /// </summary>
        public Boolean Start(UInt32 MaxClientConnections)
        {

            // volatile!
            if (_IsRunning)
                return false;

            if (MaxClientConnections != __DefaultMaxClientConnections)
                this.MaxClientConnections = MaxClientConnections;

            try
            {

                DebugX.LogT("Starting '" + ServiceName + "' on TCP port " + Port);

                _TCPListener.Start((Int32) this.MaxClientConnections);

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
                return false;
            }

            try
            {

                if (_ListenerThread is null)
                {
                    DebugX.LogT("An exception occured in Hermod.TCPServer.Start(MaxClientConnections) [_ListenerThread == null]!");
                    return false;
                }

                // Start the TCPListenerThread
                _ListenerThread.Start();

            }
            catch (Exception e)
            {
                DebugX.LogException(e);
                return false;
            }


            // Wait until socket has opened (volatile!)
            while (!_IsRunning)
                Thread.Sleep(10);

            OnStarted?.Invoke(this, Timestamp.Now);

            return true;

        }

        #endregion


        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the TCP listener.
        /// </summary>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        /// <param name="Message">An optional shutdown message.</param>
        public Boolean Shutdown(String?  Message   = null,
                                Boolean  Wait      = true)
        {

            _StopRequested = true;

            if (_TCPListener != null)
                _TCPListener.Stop();

            //if (Wait)
            //    while (_IsRunning > 0)
            //        Thread.Sleep(10);

            OnCompleted?.Invoke(this, Timestamp.Now, Message ?? "");

            return true;

        }

        #endregion

        #region StopAndWait()

        /// <summary>
        /// Stop the TCPServer and wait until all connections are closed.
        /// </summary>
        public Boolean StopAndWait()
        {

            _StopRequested = true;

            while (!_TCPConnections.IsEmpty)
                Thread.Sleep(10);

            if (_TCPListener != null)
                _TCPListener.Stop();

            return true;

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

            return String.Concat(ServiceName, " [", _TypeName, _GenericType, "] on ", IPSocket.ToString(), _Running);

        }

        #endregion

    }

}
