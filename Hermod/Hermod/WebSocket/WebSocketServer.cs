/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Text;
using System.Net.Sockets;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{


    public delegate Task<WebSocketTextMessageResponse> OnWebSocketTextMessage2Delegate(DateTime             Timestamp,
                                                                                       WebSocketServer      Server,
                                                                                       WebSocketServerConnection  Connection,
                                                                                       EventTracking_Id     EventTrackingId,
                                                                                       DateTime             RequestTimestamp,
                                                                                       String               RequestMessage);


    public class WebSocketServer2 : WebSocketServer
    {

        #region Events

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketTextMessage2Delegate? OnTextMessage;

        #endregion



        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer2(IIPAddress?                       IPAddress                    = null,
                                IPPort?                           HTTPPort                     = null,
                                String?                           HTTPServiceName              = null,
                                ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                                ThreadPriority?                   ServerThreadPriority         = null,
                                Boolean?                          ServerThreadIsBackground     = null,

                                IEnumerable<String>?              SecWebSocketProtocols        = null,
                                Boolean                           DisableWebSocketPings        = false,
                                TimeSpan?                         WebSocketPingEvery           = null,
                                TimeSpan?                         SlowNetworkSimulationDelay   = null,

                                DNSClient?                        DNSClient                    = null,
                                Boolean                           Autostart                    = false)

            : base(IPAddress,
                   HTTPPort,
                   HTTPServiceName,
                   ServerThreadNameCreator,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   SecWebSocketProtocols,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   DNSClient,
                   Autostart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer2(IPSocket                          IPSocket,
                                String?                           HTTPServiceName              = null,
                                ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                                ThreadPriority?                   ServerThreadPriority         = null,
                                Boolean?                          ServerThreadIsBackground     = null,

                                IEnumerable<String>?              SecWebSocketProtocols        = null,
                                Boolean                           DisableWebSocketPings        = false,
                                TimeSpan?                         WebSocketPingEvery           = null,
                                TimeSpan?                         SlowNetworkSimulationDelay   = null,

                                DNSClient?                        DNSClient                    = null,
                                Boolean                           Autostart                    = false)

            : base(IPSocket,
                   HTTPServiceName,
                   ServerThreadNameCreator,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   SecWebSocketProtocols,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   DNSClient,
                   Autostart)

        { }

        #endregion

        #endregion



        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime             RequestTimestamp,
                                                                                    WebSocketServerConnection  Connection,
                                                                                    String               TextMessage,
                                                                                    EventTracking_Id     EventTrackingId,
                                                                                    CancellationToken    CancellationToken)
        {

            WebSocketTextMessageResponse? response = null;

            var onTextMessage = OnTextMessage;
            if (onTextMessage is not null)
                response = await onTextMessage.Invoke(RequestTimestamp,
                                                      this,
                                                      Connection,
                                                      EventTrackingId,
                                                      RequestTimestamp,
                                                      TextMessage);

            response ??= new WebSocketTextMessageResponse(
                             RequestTimestamp,
                             TextMessage,
                             Timestamp.Now,
                             TextMessage.Reverse(),
                             EventTrackingId
                         );

            return response;

        }

    }


    /// <summary>
    /// A HTTP web socket server.
    /// </summary>
    public class WebSocketServer
    {

        #region Data

        private readonly  ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>  webSocketConnections;

        private           Thread?                                                             listenerThread;

        private readonly  CancellationTokenSource                                             cancellationTokenSource;

        private volatile  Boolean                                                             isRunning                  = false;

        private const     String                                                              LogfileName                = "HTTPWebSocketServer.log";

        public readonly   TimeSpan                                                            DefaultWebSocketPingEvery  = TimeSpan.FromSeconds(30);

        #endregion

        #region Properties

        /// <summary>
        /// Return an enumeration of all currently connected HTTP web socket connections.
        /// </summary>
        public IEnumerable<WebSocketServerConnection> WebSocketConnections
        {
            get
            {

                var webSocketConnectionsList = new List<WebSocketServerConnection>();

                foreach (var weakReference in webSocketConnections.Values)
                {
                    if (weakReference.TryGetTarget(out var webSocketConnection) &&
                        webSocketConnection is not null &&
                        webSocketConnection.IsClosed == false)
                    {
                        webSocketConnectionsList.Add(webSocketConnection);
                    }
                }

                return webSocketConnectionsList;

            }
        }

        /// <summary>
        /// The HTTP service name.
        /// </summary>
        public String                            HTTPServiceName               { get; }

        public ServerThreadNameCreatorDelegate?  ServerThreadNameCreator       { get; }

        public ThreadPriority                    ServerThreadPriority          { get; }

        public Boolean                           ServerThreadIsBackground      { get; }

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public IIPAddress                        IPAddress
            => IPSocket.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                            IPPort
            => IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public IPSocket                          IPSocket                      { get; }

        /// <summary>
        /// Whether the web socket TCP listener is currently running.
        /// </summary>
        public Boolean                           IsRunning
            => isRunning;

        /// <summary>
        /// The supported secondary web socket protocols.
        /// </summary>
        public HashSet<String>                   SecWebSocketProtocols         { get; }

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                           DisableWebSocketPings         { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                          WebSocketPingEvery            { get; set; }

        /// <summary>
        /// An additional delay between sending each byte to the networking stack.
        /// This is intended for debugging other web socket stacks.
        /// </summary>
        public TimeSpan?                         SlowNetworkSimulationDelay    { get; set; }


        /// <summary>
        /// An optional DNS client to use.
        /// </summary>
        public DNSClient?                        DNSClient                     { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP web socket server started.
        /// </summary>
        public event OnServerStartedDelegate?                 OnServerStarted;


        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnValidateTCPConnectionDelegate?         OnValidateTCPConnection;

        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnNewTCPConnectionDelegate?              OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a HTTP request was received.
        /// </summary>
        public event HTTPRequestLogDelegate?                  OnHTTPRequest;

        /// <summary>
        /// An event sent whenever the HTTP headers of a new web socket connection
        /// need to be validated or filtered by an upper layer application logic.
        /// </summary>
        public event OnValidateWebSocketConnectionDelegate?   OnValidateWebSocketConnection;

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        public event OnNewWebSocketConnectionDelegate?        OnNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a reponse to a HTTP request was sent.
        /// </summary>
        public event HTTPResponseLogDelegate?                 OnHTTPResponse;


        /// <summary>
        /// An event sent whenever a web socket frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                OnWebSocketFrameReceived;

        ///// <summary>
        ///// An event sent whenever the response to a web socket frame was sent.
        ///// </summary>
        //public event OnWebSocketResponseFrameDelegate?            OnWebSocketFrameResponseSent;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?                OnWebSocketFrameSent;

        ///// <summary>
        ///// An event sent whenever the response to a web socket frame was received.
        ///// </summary>
        //public event OnWebSocketResponseFrameDelegate?            OnWebSocketFrameResponseReceived;


        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketTextMessageDelegate?          OnTextMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketTextMessageDelegate?          OnTextMessageSent;


        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketBinaryMessageDelegate?        OnBinaryMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageDelegate?        OnBinaryMessageSent;


        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                OnPingMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?                OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        public event OnCloseMessageDelegate?                  OnCloseMessageReceived;


        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public event OnTCPConnectionClosedDelegate?           OnTCPConnectionClosed;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IIPAddress?                       IPAddress                    = null,
                               IPPort?                           HTTPPort                     = null,
                               String?                           HTTPServiceName              = null,
                               ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                               ThreadPriority?                   ServerThreadPriority         = null,
                               Boolean?                          ServerThreadIsBackground     = null,

                               IEnumerable<String>?              SecWebSocketProtocols        = null,
                               Boolean                           DisableWebSocketPings        = false,
                               TimeSpan?                         WebSocketPingEvery           = null,
                               TimeSpan?                         SlowNetworkSimulationDelay   = null,

                               DNSClient?                        DNSClient                    = null,
                               Boolean                           Autostart                    = false)

            : this(new IPSocket(IPAddress ?? IPv4Address.Any,   // 0.0.0.0  IPv4+IPv6 sockets seem to fail on Win11!
                                HTTPPort  ?? IPPort.HTTP),
                   HTTPServiceName,
                   ServerThreadNameCreator,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   SecWebSocketProtocols,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   DNSClient,
                   Autostart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., Autostart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="Autostart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketServer(IPSocket                          IPSocket,
                               String?                           HTTPServiceName              = null,
                               ServerThreadNameCreatorDelegate?  ServerThreadNameCreator      = null,
                               ThreadPriority?                   ServerThreadPriority         = null,
                               Boolean?                          ServerThreadIsBackground     = null,

                               IEnumerable<String>?              SecWebSocketProtocols        = null,
                               Boolean                           DisableWebSocketPings        = false,
                               TimeSpan?                         WebSocketPingEvery           = null,
                               TimeSpan?                         SlowNetworkSimulationDelay   = null,

                               DNSClient?                        DNSClient                    = null,
                               Boolean                           Autostart                    = false)
        {

            this.IPSocket                    = IPSocket;
            this.HTTPServiceName             = HTTPServiceName                   ?? "GraphDefined HTTP Web Socket Service v2.0";
            this.ServerThreadNameCreator     = ServerThreadNameCreator;
            this.ServerThreadPriority        = ServerThreadPriority              ?? ThreadPriority.Normal;
            this.ServerThreadIsBackground    = ServerThreadIsBackground          ?? false;

            this.SecWebSocketProtocols       = SecWebSocketProtocols is not null
                                                   ? new HashSet<String>(SecWebSocketProtocols)
                                                   : new HashSet<String>();

            this.DisableWebSocketPings       = DisableWebSocketPings;
            this.WebSocketPingEvery          = WebSocketPingEvery                ?? DefaultWebSocketPingEvery;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;
            this.DNSClient                   = DNSClient;

            this.webSocketConnections        = new ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>();
            this.cancellationTokenSource     = new CancellationTokenSource();

            if (Autostart)
                Start();

        }

        #endregion

        #endregion


        #region SendText  (Connection, Text,  EventTrackingId = null)

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="Text">Text data to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        public SendStatus SendText(WebSocketServerConnection  Connection,
                                   String               Text,
                                   EventTracking_Id?    EventTrackingId   = null)
        {

            var success = SendFrame(Connection,
                                    WebSocketFrame.Text(Text),
                                    EventTrackingId);

            return success;

        }

        #endregion

        #region SendBinary(Connection, Data,  EventTrackingId = null)

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="Data">Binary data to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        public SendStatus SendBinary(WebSocketServerConnection  Connection,
                                     Byte[]               Data,
                                     EventTracking_Id?    EventTrackingId   = null)
        {

            var success = SendFrame(Connection,
                                    WebSocketFrame.Binary(Data),
                                    EventTrackingId);

            return success;

        }

        #endregion

        #region SendFrame (Connection, Frame, EventTrackingId = null)

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="Frame">The web socket frame to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        public SendStatus SendFrame(WebSocketServerConnection  Connection,
                                    WebSocketFrame       Frame,
                                    EventTracking_Id?    EventTrackingId   = null)
        {

            var success = Connection.SendWebSocketFrame(Frame);

            if (success == SendStatus.Success)
            {

                var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

                #region Send OnWebSocketFrameSent event

                try
                {

                    var OnWebSocketFrameSentLocal = OnWebSocketFrameSent;
                    if (OnWebSocketFrameSentLocal is not null)
                    {

                        var responseTask = OnWebSocketFrameSentLocal(Timestamp.Now,
                                                                     this,
                                                                     Connection,
                                                                     eventTrackingId,
                                                                     Frame);

                        responseTask.Wait(TimeSpan.FromSeconds(10));

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameSent));
                }

                #endregion

                #region Send OnTextMessageSent    event

                if (Frame.Opcode == WebSocketFrame.Opcodes.Text)
                {

                    try
                    {

                        var OnTextMessageSentLocal = OnTextMessageSent;
                        if (OnTextMessageSentLocal is not null)
                        {

                            var responseTask = OnTextMessageSentLocal(Timestamp.Now,
                                                                      this,
                                                                      Connection,
                                                                      eventTrackingId,
                                                                      Frame.Payload.ToUTF8String());

                            responseTask.Wait(TimeSpan.FromSeconds(10));

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageSent));
                    }

                }

                #endregion

                #region Send OnBinaryMessageSent  event

                if (Frame.Opcode == WebSocketFrame.Opcodes.Binary)
                {

                    try
                    {

                        var OnBinaryMessageSentLocal = OnBinaryMessageSent;
                        if (OnBinaryMessageSentLocal is not null)
                        {

                            var responseTask = OnBinaryMessageSentLocal(Timestamp.Now,
                                                                        this,
                                                                        Connection,
                                                                        eventTrackingId,
                                                                        Frame.Payload);

                            responseTask.Wait(TimeSpan.FromSeconds(10));

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageSent));
                    }

                }

                #endregion

            }

            return success;

        }

        #endregion

        #region RemoveConnection(Connection)

        /// <summary>
        /// Remove the given web socket connection.
        /// </summary>
        /// <param name="Connection">A HTTP web socket connection.</param>
        public Boolean RemoveConnection(WebSocketServerConnection Connection)
        {

            DebugX.Log(nameof(WebSocketServer), " Removing HTTP web socket connection with " + Connection.RemoteSocket);

            return webSocketConnections.Remove(Connection.RemoteSocket, out _);

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start the HTTP web socket listener thread.
        /// </summary>
        public void Start()
        {

            listenerThread = new Thread(() => {

                #region Server setup

                Thread.CurrentThread.Name          = ServerThreadNameCreator?.Invoke(IPSocket) ?? "HTTP Web Socket Server :" + IPSocket.Port;
                Thread.CurrentThread.Priority      = ServerThreadPriority;
                Thread.CurrentThread.IsBackground  = ServerThreadIsBackground;

                var token                          = cancellationTokenSource.Token;
                var tcpListener                    = new TcpListener(IPSocket.ToIPEndPoint());

                tcpListener.Start();

                isRunning = true;

                #endregion

                #region Send OnServerStarted event

                try
                {

                    // DebugX.Log("Web socket server has started on " + server.IPSocket);

                    var OnServerStartedLocal = OnServerStarted;
                    if (OnServerStartedLocal is not null)
                    {

                        var responseTask = OnServerStartedLocal(Timestamp.Now,
                                                                this,
                                                                EventTracking_Id.New);

                        responseTask.Wait(TimeSpan.FromSeconds(10));

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewTCPConnection));
                }

                #endregion


                while (!token.IsCancellationRequested)
                {

                    #region Accept new TCP connection

                    // Wait for a new/pending client connection
                    while (!token.IsCancellationRequested && !tcpListener.Pending())
                        Thread.Sleep(5);

                    if (token.IsCancellationRequested)
                        break;

                    var newTCPConnection = tcpListener.AcceptTcpClient();

                    #endregion

                    #region OnValidateTCPConnection

                    var validatedTCPConnection = true;

                    var OnValidateTCPConnectionLocal = OnValidateTCPConnection;
                    if (OnValidateTCPConnectionLocal is not null)
                    {

                        validatedTCPConnection = OnValidateTCPConnectionLocal(Timestamp.Now,
                                                                              this,
                                                                              newTCPConnection,
                                                                              EventTracking_Id.New,
                                                                              token).Result ?? false;

                    }

                    if (!validatedTCPConnection)
                        newTCPConnection.Close();

                    else

                    #endregion

                    Task.Factory.StartNew(async context =>
                    {

                        try
                        {

                            if (context is WebSocketServerConnection webSocketConnection)
                            {

                                #region Data

                                Boolean       IsStillHTTP        = true;
                                String?       httpMethod         = null;
                                Byte[]        bytes              = Array.Empty<Byte>();
                                Byte[]        bytesLeftOver      = Array.Empty<Byte>();
                                HTTPResponse? httpResponse       = null;

                                var cts2                         = CancellationTokenSource.CreateLinkedTokenSource(token);
                                var token2                       = cts2.Token;
                                var lastWebSocketPingTimestamp   = Timestamp.Now;
                                var sendErrors                   = 0;

                                #endregion

                                #region Config web socket connection

                                webSocketConnection.ReadTimeout   = TimeSpan.FromSeconds(20);
                                webSocketConnection.WriteTimeout  = TimeSpan.FromSeconds(3);

                                if (!webSocketConnections.TryAdd(webSocketConnection.RemoteSocket,
                                                                 new WeakReference<WebSocketServerConnection>(webSocketConnection)))
                                {

                                    webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                    webSocketConnections.TryAdd   (webSocketConnection.RemoteSocket,
                                                                   new WeakReference<WebSocketServerConnection>(webSocketConnection));

                                }

                                #endregion

                                #region Send OnNewTCPConnection event

                                try
                                {

                                    var OnNewTCPConnectionLocal = OnNewTCPConnection;
                                    if (OnNewTCPConnectionLocal is not null)
                                    {

                                        var responseTask = OnNewTCPConnectionLocal(Timestamp.Now,
                                                                                   this,
                                                                                   webSocketConnection,
                                                                                   EventTracking_Id.New,
                                                                                   token2);

                                        responseTask.Wait(TimeSpan.FromSeconds(10));

                                    }

                                }
                                catch (Exception e)
                                {
                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewTCPConnection));
                                }

                                #endregion

                                while (!token2.IsCancellationRequested &&
                                       !webSocketConnection.IsClosed   &&
                                        sendErrors < 3)
                                {

                                    #region Main loop waiting for data... and sending a regular web socket ping

                                    if (bytes.Length == 0)
                                    {

                                        while (webSocketConnection.DataAvailable == false &&
                                              !webSocketConnection.IsClosed               &&
                                               sendErrors < 3)
                                        {

                                            #region Send a regular web socket "ping"

                                            if (!DisableWebSocketPings &&
                                                Timestamp.Now > lastWebSocketPingTimestamp + WebSocketPingEvery)
                                            {

                                                var eventTrackingId  = EventTracking_Id.New;

                                                var frame            = WebSocketFrame.Ping(Guid.NewGuid().ToByteArray());

                                                var success          = SendFrame(webSocketConnection,
                                                                                 frame,
                                                                                 eventTrackingId);

                                                if (success == SendStatus.Success)
                                                {

                                                    sendErrors = 0;

                                                    #region OnPingMessageSent

                                                    try
                                                    {

                                                        OnPingMessageSent?.Invoke(Timestamp.Now,
                                                                                  this,
                                                                                  webSocketConnection,
                                                                                  eventTrackingId,
                                                                                  frame);

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPingMessageSent));
                                                    }

                                                    #endregion

                                                }
                                                else if (success == SendStatus.FatalError)
                                                {
                                                    webSocketConnection.Close();
                                                }
                                                else
                                                {
                                                    sendErrors++;
                                                    DebugX.Log("Web socket connection with " + webSocketConnection.RemoteSocket + " ping failed (" + sendErrors + ")!");
                                                }

                                                lastWebSocketPingTimestamp = Timestamp.Now;

                                            }

                                            #endregion

                                            Thread.Sleep(5);

                                        };

                                        bytes = new Byte[bytesLeftOver.Length + webSocketConnection.Available];

                                        if (bytesLeftOver.Length > 0)
                                            Array.Copy(bytesLeftOver, 0, bytes, 0, bytesLeftOver.Length);

                                        if (bytes.Length > 0)
                                            webSocketConnection.Read(bytes,
                                                                     bytesLeftOver.Length,
                                                                     bytes.Length - bytesLeftOver.Length);

                                        httpMethod = IsStillHTTP
                                                         ? Encoding.UTF8.GetString(bytes, 0, 4)
                                                         : "";

                                    }

                                    #endregion


                                    #region A web socket handshake...

                                    if (httpMethod == "GET ")
                                    {

                                        #region Invalid HTTP request...

                                        // GET /websocket HTTP/1.1
                                        // Host:                     example.com:33033
                                        // Upgrade:                  websocket
                                        // Connection:               Upgrade
                                        // Sec-WebSocket-Key:        x3JJHMbDL1EzLkh9GBhXDw==
                                        // Sec-WebSocket-Protocol:   ocpp1.6, ocpp1.5
                                        // Sec-WebSocket-Version:    13
                                        if (!HTTPRequest.TryParse(bytes, out var httpRequest) ||
                                             httpRequest is null)
                                        {
                                            httpResponse = new HTTPResponse.Builder() {
                                                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                               Server          = HTTPServiceName,
                                                               Date            = Timestamp.Now,
                                                               Connection      = "close"
                                                           }.AsImmutable;
                                        }

                                        #endregion

                                        else
                                        {

                                            webSocketConnection.HTTPRequest = httpRequest;

                                            #region OnHTTPRequest

                                            var OnHTTPRequestLocal = OnHTTPRequest;
                                            if (OnHTTPRequestLocal is not null)
                                            {

                                                await OnHTTPRequestLocal(Timestamp.Now,
                                                                         this,
                                                                         httpRequest);

                                            }

                                            #endregion

                                            #region OnValidateWebSocketConnection

                                            var OnValidateWebSocketConnectionLocal = OnValidateWebSocketConnection;
                                            if (OnValidateWebSocketConnectionLocal is not null)
                                            {

                                                httpResponse  = await OnValidateWebSocketConnectionLocal(Timestamp.Now,
                                                                                                         this,
                                                                                                         webSocketConnection,
                                                                                                         EventTracking_Id.New,
                                                                                                         token2);

                                            }

                                            #endregion

                                            #region Validate HTTP web socket request

                                            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                                            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                                            // 3. Compute SHA-1 and Base64 hash of the new value
                                            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
#pragma warning disable SCS0006 // Weak hashing function.
                                            var swk       = webSocketConnection.HTTPRequest?.SecWebSocketKey;
                                            var swka      = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                            var swkaSHA1  = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(swka));
#pragma warning restore SCS0006 // Weak hashing function.

                                            var secWebSocketProtocols = new List<String>();

                                            if (httpRequest.SecWebSocketProtocol. Any() &&
                                                this.       SecWebSocketProtocols.Any())
                                            {
                                                foreach (var protocol in httpRequest.SecWebSocketProtocol)
                                                {
                                                    if (this.SecWebSocketProtocols.Contains(protocol))
                                                        secWebSocketProtocols.Add(protocol);
                                                }
                                            }

                                            // HTTP/1.1 101 Switching Protocols
                                            // Connection:              Upgrade
                                            // Upgrade:                 websocket
                                            // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                            // Sec-WebSocket-Protocol:  ocpp1.6
                                            // Sec-WebSocket-Version:   13
                                            httpResponse ??= new HTTPResponse.Builder(httpRequest) {
                                                                 HTTPStatusCode        = HTTPStatusCode.SwitchingProtocols,
                                                                 Server                = HTTPServiceName,
                                                                 Connection            = "Upgrade",
                                                                 Upgrade               = "websocket",
                                                                 SecWebSocketAccept    = Convert.ToBase64String(swkaSHA1),
                                                                 SecWebSocketProtocol  = secWebSocketProtocols,
                                                                 SecWebSocketVersion   = "13"
                                                             }.AsImmutable;

                                            #endregion

                                        }

                                        #region Send HTTP response

                                        var success = webSocketConnection.SendText(httpResponse.EntirePDU + "\r\n\r\n");

                                        if (success == SendStatus.Success)
                                        {

                                            #region OnHTTPResponse

                                            var OnHTTPResponseLocal = OnHTTPResponse;
                                            if (OnHTTPResponseLocal is not null)
                                            {

                                                await OnHTTPResponseLocal(Timestamp.Now,
                                                                          this,
                                                                          httpRequest,
                                                                          httpResponse);

                                            }

                                            #endregion

                                        }

                                        #endregion

                                        if (success                 != SendStatus.Success ||
                                            httpResponse.Connection == "close")
                                        {
                                            webSocketConnection.Close();
                                        }

                                        else
                                        {

                                            #region Send OnNewWebSocketConnection event

                                            try
                                            {

                                                var OnNewWebSocketConnectionLocal = OnNewWebSocketConnection;
                                                if (OnNewWebSocketConnectionLocal is not null)
                                                {

                                                    var responseTask = OnNewWebSocketConnectionLocal(Timestamp.Now,
                                                                                                     this,
                                                                                                     webSocketConnection,
                                                                                                     EventTracking_Id.New,
                                                                                                     token2);

                                                    responseTask.Wait(TimeSpan.FromSeconds(10));

                                                }

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnNewWebSocketConnection));
                                            }

                                            #endregion

                                            IsStillHTTP  = false;
                                            bytes        = Array.Empty<Byte>();

                                        }

                                    }

                                    #endregion

                                    #region ...or a web socket frame...

                                    else if (IsStillHTTP == false &&
                                             bytes.Length > 0)
                                    {

                                        if (WebSocketFrame.TryParse(bytes,
                                                                    out var frame,
                                                                    out var frameLength,
                                                                    out var errorResponse))
                                        {

                                            if (frame is not null)
                                            {

                                                var              now              = Timestamp.Now;
                                                var              eventTrackingId  = EventTracking_Id.New;
                                                WebSocketFrame?  responseFrame    = null;

                                                #region OnWebSocketFrameReceived

                                                try
                                                {

                                                    OnWebSocketFrameReceived?.Invoke(now,
                                                                                     this,
                                                                                     webSocketConnection,
                                                                                     eventTrackingId,
                                                                                     frame);

                                                }
                                                catch (Exception e)
                                                {
                                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameReceived));
                                                }

                                                #endregion

                                                switch (frame.Opcode)
                                                {

                                                    #region Text   message

                                                    case WebSocketFrame.Opcodes.Text:

                                                        #region OnTextMessageReceived

                                                        try
                                                        {

                                                            OnTextMessageReceived?.Invoke(now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          eventTrackingId,
                                                                                          frame.Payload.ToUTF8String());

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageReceived));
                                                        }

                                                        #endregion

                                                        #region ProcessTextMessage

                                                        WebSocketTextMessageResponse? textMessageResponse = null;

                                                        try
                                                        {

                                                            textMessageResponse = await ProcessTextMessage(now,
                                                                                                           webSocketConnection,
                                                                                                           frame.Payload.ToUTF8String(),
                                                                                                           eventTrackingId,
                                                                                                           token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (textMessageResponse                 is not null &&
                                                                textMessageResponse.ResponseMessage is not null &&
                                                                textMessageResponse.ResponseMessage != "")
                                                            {
                                                                responseFrame = WebSocketFrame.Text(textMessageResponse.ResponseMessage);
                                                            }

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(ProcessTextMessage));
                                                        }

                                                        #endregion

                                                        #region OnTextMessageResponseSent

                                                        //try
                                                        //{

                                                        //    if (responseFrame is not null && textMessageResponse is not null)
                                                        //        OnTextMessageResponseSent?.Invoke(Timestamp.Now,
                                                        //                                          this,
                                                        //                                          webSocketConnection,
                                                        //                                          textMessageResponse.EventTrackingId,
                                                        //                                          textMessageResponse.RequestTimestamp,
                                                        //                                          textMessageResponse.RequestMessage,
                                                        //                                          textMessageResponse.ResponseTimestamp,
                                                        //                                          textMessageResponse.ResponseMessage);

                                                        //}
                                                        //catch (Exception e)
                                                        //{
                                                        //    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTextMessageResponseSent));
                                                        //}

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Binary message

                                                    case WebSocketFrame.Opcodes.Binary:

                                                        #region OnBinaryMessageReceived

                                                        try
                                                        {

                                                            OnBinaryMessageReceived?.Invoke(now,
                                                                                            this,
                                                                                            webSocketConnection,
                                                                                            eventTrackingId,
                                                                                            frame.Payload);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageReceived));
                                                        }

                                                        #endregion

                                                        #region ProcessBinaryMessage

                                                        WebSocketBinaryMessageResponse? binaryMessageResponse = null;

                                                        try
                                                        {

                                                            binaryMessageResponse = await ProcessBinaryMessage(now,
                                                                                                               webSocketConnection,
                                                                                                               frame.Payload,
                                                                                                               eventTrackingId,
                                                                                                               token2);

                                                            // Incoming higher protocol level respones will not produce another response!
                                                            if (binaryMessageResponse                 is not null &&
                                                                binaryMessageResponse.ResponseMessage is not null &&
                                                                binaryMessageResponse.ResponseMessage.Length > 0)
                                                            {
                                                                responseFrame = WebSocketFrame.Binary(binaryMessageResponse.ResponseMessage);
                                                            }

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(ProcessBinaryMessage));
                                                        }

                                                        #endregion

                                                        #region OnBinaryMessageResponseSent

                                                        //try
                                                        //{

                                                        //    if (responseFrame is not null && binaryMessageResponse is not null)
                                                        //        OnBinaryMessageResponseSent?.Invoke(Timestamp.Now,
                                                        //                                            this,
                                                        //                                            webSocketConnection,
                                                        //                                            binaryMessageResponse.EventTrackingId,
                                                        //                                            binaryMessageResponse.RequestTimestamp,
                                                        //                                            binaryMessageResponse.RequestMessage,
                                                        //                                            binaryMessageResponse.ResponseTimestamp,
                                                        //                                            binaryMessageResponse.ResponseMessage);

                                                        //}
                                                        //catch (Exception e)
                                                        //{
                                                        //    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnBinaryMessageResponseSent));
                                                        //}

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Ping   message

                                                    case WebSocketFrame.Opcodes.Ping:

                                                        #region OnPingMessageReceived

                                                        try
                                                        {

                                                            OnPingMessageReceived?.Invoke(now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          eventTrackingId,
                                                                                          frame);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPingMessageReceived));
                                                        }

                                                        #endregion

                                                        responseFrame = WebSocketFrame.Pong(frame.Payload);

                                                        break;

                                                    #endregion

                                                    #region Pong   message

                                                    case WebSocketFrame.Opcodes.Pong:

                                                        #region OnPongMessageReceived

                                                        try
                                                        {

                                                            OnPongMessageReceived?.Invoke(now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          eventTrackingId,
                                                                                          frame);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnPongMessageReceived));
                                                        }

                                                        #endregion

                                                        break;

                                                    #endregion

                                                    #region Close  message

                                                    case WebSocketFrame.Opcodes.Close:

                                                        webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                        #region OnCloseMessage

                                                        try
                                                        {

                                                            OnCloseMessageReceived?.Invoke(now,
                                                                                           this,
                                                                                           webSocketConnection,
                                                                                           eventTrackingId,
                                                                                           frame.GetClosingStatusCode(),
                                                                                           frame.GetClosingReason());

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnCloseMessageReceived));
                                                        }

                                                        #endregion

                                                        // The close handshake demands that we have to send a close frame back!
                                                        SendFrame(webSocketConnection,
                                                                  WebSocketFrame.Close(),
                                                                  eventTrackingId);

                                                        webSocketConnection.Close();

                                                        break;

                                                    #endregion

                                                }

                                                #region Send immediate response frame... when available

                                                if (responseFrame is not null)
                                                {

                                                    var success = SendFrame(webSocketConnection,
                                                                            responseFrame,
                                                                            eventTrackingId);

                                                    if (success == SendStatus.Success)
                                                    {

                                                        sendErrors = 0;

                                                        #region OnWebSocketFrameResponseSent

                                                        //try
                                                        //{

                                                        //    if (responseFrame is not null)
                                                        //        OnWebSocketFrameResponseSent?.Invoke(Timestamp.Now,
                                                        //                                             this,
                                                        //                                             webSocketConnection,
                                                        //                                             frame,
                                                        //                                             responseFrame,
                                                        //                                             eventTrackingId);

                                                        //}
                                                        //catch (Exception e)
                                                        //{
                                                        //    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnWebSocketFrameResponseSent));
                                                        //}

                                                        #endregion

                                                    }
                                                    else if (success == SendStatus.FatalError)
                                                    {
                                                        webSocketConnection.Close();
                                                    }
                                                    else
                                                    {
                                                        sendErrors++;
                                                        DebugX.Log("Web socket connection with " + webSocketConnection.RemoteSocket + " sending a web socket frame failed (" + sendErrors + ")!");
                                                    }

                                                }

                                                #endregion

                                                //if (frame.Opcode.IsControl())
                                                //{
                                                //    if (frame.FIN    == WebSocketFrame.Fin.More)
                                                //        Console.WriteLine(">>> A control frame is fragmented!");
                                                //}

                                                if ((UInt64) bytes.Length == frameLength)
                                                    bytes = Array.Empty<Byte>();

                                                else
                                                {
                                                    // The buffer might contain additional web socket frames...
                                                    var newBytes = new Byte[(UInt64) bytes.Length - frameLength];
                                                    Array.Copy(bytes, (Int32) frameLength, newBytes, 0, newBytes.Length);
                                                    bytes = newBytes;
                                                }

                                                bytesLeftOver = Array.Empty<Byte>();

                                            }

                                        }
                                        else
                                        {
                                            bytesLeftOver = bytes;
                                            DebugX.Log("Could not parse the given web socket frame of " + bytesLeftOver.Length + " byte(s): " + errorResponse);
                                            bytes = Array.Empty<Byte>();
                                        }

                                    }

                                    #endregion

                                    #region ...0 bytes read...

                                    else if (bytes.Length == 0)
                                    {
                                        // Some kind of network error...
                                    }

                                    #endregion

                                    #region ...some other crap!

                                    // Can e.g. be web crawlers etc.pp

                                    else
                                    {
                                        DebugX.Log(nameof(WebSocketServer) + ": Closing invalid TCP connection from: " + webSocketConnection.RemoteSocket);
                                        webSocketConnection.Close();
                                    }

                                    #endregion

                                }

                                webSocketConnection.Close();

                                webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                #region OnTCPConnectionClosed

                                try
                                {

                                    OnTCPConnectionClosed?.Invoke(Timestamp.Now,
                                                                  this,
                                                                  webSocketConnection,
                                                                  "!!!",
                                                                  EventTracking_Id.New);

                                }
                                catch (Exception e)
                                {
                                    DebugX.Log(e, nameof(WebSocketServer) + "." + nameof(OnTCPConnectionClosed));
                                }

                                #endregion

                            }
                            else
                                DebugX.Log(nameof(WebSocketServer), " The given web socket connection is invalid!");

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(nameof(WebSocketServer), " Exception in web socket server: " + e.Message + Environment.NewLine + e.StackTrace);
                        }

                    },
                    new WebSocketServerConnection(this,
                                            newTCPConnection,
                                            SlowNetworkSimulationDelay: SlowNetworkSimulationDelay),
                    token);

                }

                #region Stop TCP listener

                DebugX.Log(nameof(WebSocketServer), " Stopping server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

                tcpListener.Stop();

                isRunning = false;

                //ToDo: Request all client connections to finish!

                DebugX.Log(nameof(WebSocketServer), " Stopped server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

                #endregion

            });

            listenerThread.Start();

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the HTTP web socket listener thread.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public void Shutdown(String?  Message   = null,
                             Boolean  Wait      = true)
        {

            cancellationTokenSource.Cancel();

            if (Wait) {
                while (isRunning) {
                    Thread.Sleep(10);
                }
            }

        }

        #endregion


        #region (virtual) ProcessTextMessage  (RequestTimestamp, Connection, TextMessage, ...)

        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public virtual Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime             RequestTimestamp,
                                                                             WebSocketServerConnection  Connection,
                                                                             String               TextMessage,
                                                                             EventTracking_Id     EventTrackingId,
                                                                             CancellationToken    CancellationToken)
        {

            return Task.FromResult(
                       new WebSocketTextMessageResponse(
                           RequestTimestamp,
                           TextMessage,
                           Timestamp.Now,
                           String.Empty,
                           EventTrackingId
                       )
                   );

        }

        #endregion

        #region (virtual) ProcessBinaryMessage(RequestTimestamp, Connection, BinaryMessage, ...)

        /// <summary>
        /// The default HTTP web socket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public virtual Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime             RequestTimestamp,
                                                                                 WebSocketServerConnection  Connection,
                                                                                 Byte[]               BinaryMessage,
                                                                                 EventTracking_Id     EventTrackingId,
                                                                                 CancellationToken    CancellationToken)
        {

            return Task.FromResult(
                       new WebSocketBinaryMessageResponse(
                           RequestTimestamp,
                           BinaryMessage,
                           Timestamp.Now,
                           Array.Empty<Byte>(),
                           EventTrackingId
                       )
                   );

        }

        #endregion


    }

}
