/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{


    public delegate Task<WebSocketTextMessageResponse>    OnWebSocketTextMessage2Delegate  (DateTime                     Timestamp,
                                                                                            AWebSocketServer             Server,
                                                                                            WebSocketServerConnection    Connection,
                                                                                            EventTracking_Id             EventTrackingId,
                                                                                            DateTime                     RequestTimestamp,
                                                                                            String                       TextMessage,
                                                                                            CancellationToken            CancellationToken);

    public delegate Task<WebSocketBinaryMessageResponse>  OnWebSocketBinaryMessage2Delegate(DateTime                     Timestamp,
                                                                                            AWebSocketServer             Server,
                                                                                            WebSocketServerConnection    Connection,
                                                                                            EventTracking_Id             EventTrackingId,
                                                                                            DateTime                     RequestTimestamp,
                                                                                            Byte[]                       BinaryMessage,
                                                                                            CancellationToken            CancellationToken);


    /// <summary>
    /// A HTTP web socket server.
    /// </summary>
    public abstract class AWebSocketServer : IWebSocketServer
    {

        #region Data

        private readonly  ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>  webSocketConnections;

        private           Thread?                                                                   listenerThread;

        private readonly  CancellationTokenSource                                                   cancellationTokenSource;

        private volatile  Boolean                                                                   isRunning = false;

        public readonly   TimeSpan                                                                  DefaultWebSocketPingEvery  = TimeSpan.FromSeconds(30);

        private const     String                                                                    LogfileName                = "HTTPWebSocketServer.log";

        #endregion

        #region Properties

        /// <summary>
        /// Return an enumeration of all currently connected HTTP web socket connections.
        /// </summary>
        public IEnumerable<WebSocketServerConnection>  WebSocketConnections
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
        public String                                  HTTPServiceName               { get; }

        /// <summary>
        /// The optional description of this HTTP Web Socket service.
        /// </summary>
        public I18NString                              Description                   { get; set; }

        public Func<X509Certificate2>?                 ServerCertificateSelector     { get; }
        public RemoteCertificateValidationHandler?     ClientCertificateValidator    { get; }
        public LocalCertificateSelectionHandler?       ClientCertificateSelector     { get; }
        public SslProtocols?                           AllowedTLSProtocols           { get; }
        public Boolean?                                ClientCertificateRequired     { get; }
        public Boolean?                                CheckCertificateRevocation    { get; }

        public ServerThreadNameCreatorDelegate         ServerThreadNameCreator       { get; }
        public ServerThreadPriorityDelegate            ServerThreadPrioritySetter    { get; }
        public Boolean                                 ServerThreadIsBackground      { get; }

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public IIPAddress                              IPAddress
            => IPSocket.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                                  IPPort
            => IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public IPSocket                                IPSocket                      { get; }

        /// <summary>
        /// Whether the web socket TCP listener is currently running.
        /// </summary>
        public Boolean                                 IsRunning
            => isRunning;

        /// <summary>
        /// The supported secondary web socket protocols.
        /// </summary>
        public HashSet<String>                         SecWebSocketProtocols         { get; }

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                                 DisableWebSocketPings         { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                                WebSocketPingEvery            { get; set; }

        /// <summary>
        /// An additional delay between sending each byte to the networking stack.
        /// This is intended for debugging other web socket stacks.
        /// </summary>
        public TimeSpan?                               SlowNetworkSimulationDelay    { get; set; }


        /// <summary>
        /// An optional DNS client to use.
        /// </summary>
        public DNSClient?                              DNSClient                     { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP web socket server started.
        /// </summary>
        public event OnServerStartedDelegate?                OnServerStarted;


        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnValidateTCPConnectionDelegate?        OnValidateTCPConnection;

        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnNewTCPConnectionDelegate?             OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public event OnTCPConnectionClosedDelegate?          OnTCPConnectionClosed;


        /// <summary>
        /// An event sent whenever a HTTP request was received.
        /// </summary>
        public event HTTPRequestLogDelegate?                 OnHTTPRequest;
        /// <summary>
        /// An event sent whenever the HTTP headers of a new web socket connection
        /// need to be validated or filtered by an upper layer application logic.
        /// </summary>
        public event OnValidateWebSocketConnectionDelegate?  OnValidateWebSocketConnection;

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        public event OnNewWebSocketConnectionDelegate?       OnNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a reponse to a HTTP request was sent.
        /// </summary>
        public event HTTPResponseLogDelegate?                OnHTTPResponse;


        /// <summary>
        /// An event sent whenever a web socket frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?               OnWebSocketFrameReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?               OnWebSocketFrameSent;


        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketTextMessageDelegate?         OnTextMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketTextMessageDelegate?         OnTextMessageSent;


        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketBinaryMessageDelegate?       OnBinaryMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageDelegate?       OnBinaryMessageSent;


        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?               OnPingMessageReceived;

        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?               OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?               OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        public event OnCloseMessageReceivedDelegate?         OnCloseMessageReceived;


        /// <summary>
        /// An event sent whenever the HTTP web socket server stopped.
        /// </summary>
        public event OnServerStoppedDelegate?                OnServerStopped;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="TCPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP Web Socket service.</param>
        /// 
        /// <param name="SecWebSocketProtocols"></param>
        /// <param name="DisableWebSocketPings"></param>
        /// <param name="WebSocketPingEvery"></param>
        /// <param name="SlowNetworkSimulationDelay"></param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="ClientCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ServerThreadNameCreator"></param>
        /// <param name="ServerThreadPrioritySetter"></param>
        /// <param name="ServerThreadIsBackground"></param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionTimeout"></param>
        /// <param name="MaxClientConnections"></param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public AWebSocketServer(IIPAddress?                          IPAddress                    = null,
                                IPPort?                              TCPPort                      = null,
                                String?                              HTTPServiceName              = null,
                                I18NString?                          Description                  = null,

                                IEnumerable<String>?                 SecWebSocketProtocols        = null,
                                Boolean                              DisableWebSocketPings        = false,
                                TimeSpan?                            WebSocketPingEvery           = null,
                                TimeSpan?                            SlowNetworkSimulationDelay   = null,

                                Func<X509Certificate2>?              ServerCertificateSelector    = null,
                                RemoteCertificateValidationHandler?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                                SslProtocols?                        AllowedTLSProtocols          = null,
                                Boolean?                             ClientCertificateRequired    = null,
                                Boolean?                             CheckCertificateRevocation   = null,

                                ServerThreadNameCreatorDelegate?     ServerThreadNameCreator      = null,
                                ServerThreadPriorityDelegate?        ServerThreadPrioritySetter   = null,
                                Boolean?                             ServerThreadIsBackground     = null,
                                ConnectionIdBuilder?                 ConnectionIdBuilder          = null,
                                TimeSpan?                            ConnectionTimeout            = null,
                                UInt32?                              MaxClientConnections         = null,

                                DNSClient?                           DNSClient                    = null,
                                Boolean                              AutoStart                    = false)

            : this(new IPSocket(
                       IPAddress ?? IPv4Address.Any,   // 0.0.0.0  IPv4+IPv6 sockets seem to fail on Win11!
                       TCPPort ?? IPPort.HTTP
                   ),
                   HTTPServiceName,
                   Description,

                   SecWebSocketProtocols,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   DNSClient,
                   AutoStart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="TCPSocket">The TCP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP Web Socket service.</param>
        /// 
        /// <param name="SecWebSocketProtocols"></param>
        /// <param name="DisableWebSocketPings"></param>
        /// <param name="WebSocketPingEvery"></param>
        /// <param name="SlowNetworkSimulationDelay"></param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="ClientCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ServerThreadNameCreator"></param>
        /// <param name="ServerThreadPrioritySetter"></param>
        /// <param name="ServerThreadIsBackground"></param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionTimeout"></param>
        /// <param name="MaxClientConnections"></param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public AWebSocketServer(IPSocket                             TCPSocket,
                                String?                              HTTPServiceName              = null,
                                I18NString?                          Description                  = null,

                                IEnumerable<String>?                 SecWebSocketProtocols        = null,
                                Boolean                              DisableWebSocketPings        = false,
                                TimeSpan?                            WebSocketPingEvery           = null,
                                TimeSpan?                            SlowNetworkSimulationDelay   = null,

                                Func<X509Certificate2>?              ServerCertificateSelector    = null,
                                RemoteCertificateValidationHandler?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                                SslProtocols?                        AllowedTLSProtocols          = null,
                                Boolean?                             ClientCertificateRequired    = null,
                                Boolean?                             CheckCertificateRevocation   = null,

                                ServerThreadNameCreatorDelegate?     ServerThreadNameCreator      = null,
                                ServerThreadPriorityDelegate?        ServerThreadPrioritySetter   = null,
                                Boolean?                             ServerThreadIsBackground     = null,
                                ConnectionIdBuilder?                 ConnectionIdBuilder          = null,
                                TimeSpan?                            ConnectionTimeout            = null,
                                UInt32?                              MaxClientConnections         = null,

                                DNSClient?                           DNSClient                    = null,
                                Boolean                              AutoStart                    = false)
        {

            this.IPSocket                    = TCPSocket;
            this.HTTPServiceName             = HTTPServiceName            ?? "GraphDefined HTTP Web Socket Service v2.0";
            this.Description                 = Description                ?? I18NString.Empty;
            this.ServerThreadNameCreator     = ServerThreadNameCreator    ?? (socket => $"AWebSocketServer {socket}");
            this.ServerThreadPrioritySetter  = ServerThreadPrioritySetter ?? (socket => ThreadPriority.AboveNormal);
            this.ServerThreadIsBackground    = ServerThreadIsBackground   ?? false;

            this.SecWebSocketProtocols       = SecWebSocketProtocols is not null
                                                   ? new HashSet<String>(SecWebSocketProtocols)
                                                   : [];

            this.DisableWebSocketPings       = DisableWebSocketPings;
            this.WebSocketPingEvery          = WebSocketPingEvery ?? DefaultWebSocketPingEvery;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

            this.ServerCertificateSelector   = ServerCertificateSelector;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.ClientCertificateSelector   = ClientCertificateSelector;
            this.AllowedTLSProtocols         = AllowedTLSProtocols;
            this.ClientCertificateRequired   = ClientCertificateRequired;
            this.CheckCertificateRevocation  = CheckCertificateRevocation;

            this.DNSClient                   = DNSClient;

            this.webSocketConnections        = new ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>();
            this.cancellationTokenSource     = new CancellationTokenSource();

            if (AutoStart)
                Start();

        }

        #endregion

        #endregion


        #region SendTextMessage   (Connection, TextMessage,    ...)

        /// <summary>
        /// Send a text web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The text message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public Task<SendStatus> SendTextMessage(WebSocketServerConnection  Connection,
                                                String                     TextMessage,
                                                EventTracking_Id?          EventTrackingId     = null,
                                                CancellationToken          CancellationToken   = default)

            => SendWebSocketFrame(
                   Connection,
                   WebSocketFrame.Text(TextMessage),
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region SendBinaryMessage (Connection, BinaryMessage,  ...)

        /// <summary>
        /// Send a binary web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The binary message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public Task<SendStatus> SendBinaryMessage(WebSocketServerConnection  Connection,
                                                  Byte[]                     BinaryMessage,
                                                  EventTracking_Id?          EventTrackingId     = null,
                                                  CancellationToken          CancellationToken   = default)

            => SendWebSocketFrame(
                   Connection,
                   WebSocketFrame.Binary(BinaryMessage),
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region SendWebSocketFrame(Connection, WebSocketFrame, ...)

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="WebSocketFrame">The web socket frame to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public async Task<SendStatus> SendWebSocketFrame(WebSocketServerConnection  Connection,
                                                         WebSocketFrame             WebSocketFrame,
                                                         EventTracking_Id?          EventTrackingId     = null,
                                                         CancellationToken          CancellationToken   = default)
        {

            var success = await Connection.SendWebSocketFrame(
                                    WebSocketFrame,
                                    CancellationToken
                                );

            if (success == SendStatus.Success)
            {

                var now              = Timestamp.Now;
                var eventTrackingId  = EventTrackingId ?? EventTracking_Id.New;

                #region Send OnWebSocketFrameSent event

                await SendOnWebSocketFrameSent(
                          now,
                          Connection,
                          eventTrackingId,
                          WebSocketFrame,
                          CancellationToken
                      );

                #endregion

                #region Send OnTextMessageSent    event

                if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Text)
                    await SendOnTextMessageSent(
                              now,
                              Connection,
                              eventTrackingId,
                              WebSocketFrame.Payload.ToUTF8String(),
                              CancellationToken
                          );

                #endregion

                #region Send OnBinaryMessageSent  event

                if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Binary)
                    await SendOnBinaryMessageSent(
                              now,
                              Connection,
                              eventTrackingId,
                              WebSocketFrame.Payload,
                              CancellationToken
                          );

                #endregion

            }

            return success;

        }

        #endregion


        #region RemoveConnection  (Connection)

        /// <summary>
        /// Remove the given web socket connection.
        /// </summary>
        /// <param name="Connection">A HTTP web socket connection.</param>
        public Boolean RemoveConnection(WebSocketServerConnection Connection)
        {

            DebugX.Log(nameof(AWebSocketServer), " Removing HTTP web socket connection with " + Connection.RemoteSocket);

            return webSocketConnections.Remove(Connection.RemoteSocket, out _);

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start the HTTP web socket listener thread.
        /// </summary>
        public void Start()
        {

            listenerThread = new Thread(async () => {

                try
                {

                    #region Server setup

                    Thread.CurrentThread.Name          = ServerThreadNameCreator(IPSocket);
                    Thread.CurrentThread.Priority      = ServerThreadPrioritySetter(IPSocket);
                    Thread.CurrentThread.IsBackground  = ServerThreadIsBackground;

                    var token        = cancellationTokenSource.Token;
                    var tcpListener  = new TcpListener(IPSocket.ToIPEndPoint());
                    tcpListener.Start();

                    isRunning        = true;

                    #endregion

                    #region Send OnServerStarted event

                    var onServerStarted = OnServerStarted;
                    if (onServerStarted is not null)
                    {
                        try
                        {

                            await Task.WhenAll(onServerStarted.GetInvocationList().
                                                   OfType <OnServerStartedDelegate>().
                                                   Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                  Timestamp.Now,
                                                                                  this,
                                                                                  EventTracking_Id.New,
                                                                                  token
                                                                              )).
                                                   ToArray());

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(OnNewTCPConnection)}");
                        }
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
                        SslStream? sslStream = null;

                        #endregion

                        #region OnValidateTCPConnection

                        var validatedTCPConnections = Array.Empty<ConnectionFilterResponse>();

                        var onValidateTCPConnection = OnValidateTCPConnection;
                        if (onValidateTCPConnection is not null)
                        {
                            try
                            {

                                validatedTCPConnections = await Task.WhenAll(
                                                                    onValidateTCPConnection.GetInvocationList().
                                                                        OfType <OnValidateTCPConnectionDelegate>().
                                                                        Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                                       Timestamp.Now,
                                                                                                       this,
                                                                                                       newTCPConnection,
                                                                                                       EventTracking_Id.New,
                                                                                                       token
                                                                                                   )).
                                                                        ToArray()
                                                                );

                            }
                            catch (Exception e)
                            {
                                DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(OnNewTCPConnection)}");
                            }
                        }

                        if (validatedTCPConnections.Length > 0 &&
                            validatedTCPConnections.First() == ConnectionFilterResponse.Rejected())
                        {
                            newTCPConnection.Close();
                        }

                        #endregion

                        else
                        {

                            try
                            {

                                #region Try SSL/TLS

                                var serverCertificate = ServerCertificateSelector?.Invoke();

                                if (serverCertificate is not null)
                                {

                                    //DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] New TLS connection using server certificate: " + this.ServerCertificate.Subject);

                                    sslStream      = new SslStream(innerStream:                         newTCPConnection.GetStream(),
                                                                   leaveInnerStreamOpen:                true,
                                                                   userCertificateValidationCallback:   ClientCertificateValidator is null
                                                                                                            ? null
                                                                                                            : (sender,
                                                                                                               certificate,
                                                                                                               chain,
                                                                                                               policyErrors) => ClientCertificateValidator(sender,
                                                                                                                                                           certificate is not null
                                                                                                                                                               ? new X509Certificate2(certificate)
                                                                                                                                                               : null,
                                                                                                                                                           chain,
                                                                                                                                                           policyErrors).Item1,
                                                                   userCertificateSelectionCallback:    ClientCertificateSelector is null
                                                                                                            ? null
                                                                                                            : (sender,
                                                                                                               targetHost,
                                                                                                               localCertificates,
                                                                                                               remoteCertificate,
                                                                                                               acceptableIssuers) => ClientCertificateSelector(sender,
                                                                                                                                                               targetHost,
                                                                                                                                                               localCertificates.
                                                                                                                                                                   Cast<X509Certificate>().
                                                                                                                                                                   Select(certificate => new X509Certificate2(certificate)),
                                                                                                                                                               remoteCertificate is not null
                                                                                                                                                                   ? new X509Certificate2(remoteCertificate)
                                                                                                                                                                   : null,
                                                                                                                                                               acceptableIssuers),
                                                                   encryptionPolicy:                    EncryptionPolicy.RequireEncryption);

                                    sslStream.AuthenticateAsServer(serverCertificate:                   serverCertificate,
                                                                   clientCertificateRequired:           ClientCertificateValidator is not null,
                                                                   enabledSslProtocols:                 AllowedTLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13,
                                                                   checkCertificateRevocation:          false);

                                    if (sslStream.RemoteCertificate is not null)
                                    {

                                        //this.ClientCertificate = new X509Certificate2(sslStream.RemoteCertificate);

                                        //DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] New TLS connection using client certificate: " + this.ClientCertificate.Subject);

                                    }

                                }

                                #endregion

                                var x = Task.Factory.StartNew(async context => {

                                    try
                                    {

                                        if (context is WebSocketServerConnection webSocketConnection)
                                        {

                                            #region Data

                                            Boolean        IsStillHTTP      = true;
                                            String?        httpMethod       = null;
                                            Byte[]         bytes            = [];
                                            Byte[]         bytesLeftOver    = [];
                                            HTTPResponse?  httpResponse     = null;

                                            var cts2                        = CancellationTokenSource.CreateLinkedTokenSource(token);
                                            var token2                      = cts2.Token;
                                            var lastWebSocketPingTimestamp  = Timestamp.Now;
                                            var sendErrors                  = 0;

                                            #endregion

                                            #region Config web socket connection

                                            webSocketConnection.ReadTimeout = TimeSpan.FromSeconds(20);
                                            webSocketConnection.WriteTimeout = TimeSpan.FromSeconds(3);

                                            if (!webSocketConnections.TryAdd(webSocketConnection.RemoteSocket,
                                                                             new WeakReference<WebSocketServerConnection>(webSocketConnection)))
                                            {

                                                webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                webSocketConnections.TryAdd   (webSocketConnection.RemoteSocket,
                                                                               new WeakReference<WebSocketServerConnection>(webSocketConnection));

                                            }

                                            #endregion

                                            #region Send OnNewTCPConnection event

                                            var onNewTCPConnection = OnNewTCPConnection;
                                            if (onNewTCPConnection is not null)
                                            {
                                                try
                                                {

                                                    await Task.WhenAll(onNewTCPConnection.GetInvocationList().
                                                                           OfType<OnNewTCPConnectionDelegate>().
                                                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                                          Timestamp.Now,
                                                                                                          this,
                                                                                                          webSocketConnection,
                                                                                                          EventTracking_Id.New,
                                                                                                          token2
                                                                                                      )).
                                                                           ToArray());

                                                }
                                                catch (Exception e)
                                                {
                                                    DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(OnNewTCPConnection)}");
                                                }
                                            }

                                            #endregion

                                            while (!token2.IsCancellationRequested &&
                                                   !webSocketConnection.IsClosed &&
                                                    sendErrors < 3)
                                            {

                                                #region Main loop waiting for data... and sending a regular web socket ping

                                                if (bytes.Length == 0)
                                                {

                                                    while (webSocketConnection.DataAvailable == false &&
                                                          !webSocketConnection.IsClosed &&
                                                           sendErrors < 3)
                                                    {

                                                        #region Send a regular web socket "ping"

                                                        if (!DisableWebSocketPings &&
                                                            Timestamp.Now > lastWebSocketPingTimestamp + WebSocketPingEvery)
                                                        {

                                                            var eventTrackingId = EventTracking_Id.New;
                                                            var frame = WebSocketFrame.Ping(Guid.NewGuid().ToByteArray());
                                                            var success = await SendWebSocketFrame(
                                                                                             webSocketConnection,
                                                                                             frame,
                                                                                             eventTrackingId,
                                                                                             token2
                                                                                         );

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
                                                                                              frame,
                                                                                              token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnPingMessageSent));
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
                                                    {

                                                        var read = webSocketConnection.Read(bytes,
                                                                                            bytesLeftOver.Length,
                                                                                            bytes.Length - bytesLeftOver.Length);

                                                        if (bytes.Length != (read + bytesLeftOver.Length))
                                                            Array.Resize(ref bytes, (Int32) (read + bytesLeftOver.Length));

                                                    }

                                                    httpMethod = IsStillHTTP
                                                                     ? Encoding.UTF8.GetString(bytes, 0, 4)
                                                                     : "";

                                                }

                                                #endregion


                                                #region A web socket handshake...

                                                if (httpMethod == "GET ")
                                                {

                                                    var sharedSubprotocols = Array.Empty<String>();

                                                    #region Invalid HTTP request...

                                                    // GET /websocket HTTP/1.1
                                                    // Host:                     example.com:33033
                                                    // Upgrade:                  websocket
                                                    // Connection:               Upgrade
                                                    // Sec-WebSocket-Key:        x3JJHMbDL1EzLkh9GBhXDw==
                                                    // Sec-WebSocket-Protocol:   ocpp1.6, ocpp1.5
                                                    // Sec-WebSocket-Version:    13
                                                    if (!HTTPRequest.TryParse(bytes, out var httpRequest))
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

                                                        var onHTTPRequest = OnHTTPRequest;
                                                        if (onHTTPRequest is not null)
                                                        {

                                                            await onHTTPRequest(Timestamp.Now,
                                                                                this,
                                                                                httpRequest,
                                                                                token2);

                                                        }

                                                        #endregion

                                                        #region OnValidateWebSocketConnection

                                                        var onValidateWebSocketConnection = OnValidateWebSocketConnection;
                                                        if (onValidateWebSocketConnection is not null)
                                                        {

                                                            httpResponse = await onValidateWebSocketConnection(Timestamp.Now,
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
                                                        var swk             = webSocketConnection.HTTPRequest?.SecWebSocketKey;
                                                        var swka            = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                                        var swkaSHA1        = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(swka));
                                                        sharedSubprotocols  = SecWebSocketProtocols.
                                                                                   Intersect(httpRequest.SecWebSocketProtocol).
                                                                                   OrderByDescending(protocol => protocol).
                                                                                   ToArray();
        #pragma warning restore SCS0006 // Weak hashing function.


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
                                                                             SecWebSocketProtocol  = sharedSubprotocols,
                                                                             SecWebSocketVersion   = "13"
                                                                         }.AsImmutable;

                                                        #endregion

                                                    }

                                                    #region Send HTTP response

                                                    var success = await webSocketConnection.SendText(httpResponse.EntirePDU + "\r\n\r\n");

                                                    if (success == SendStatus.Success)
                                                    {

                                                        #region OnHTTPResponse

                                                        var onHTTPResponse = OnHTTPResponse;
                                                        if (onHTTPResponse is not null)
                                                        {

                                                            await onHTTPResponse(Timestamp.Now,
                                                                                 this,
                                                                                 httpRequest,
                                                                                 httpResponse,
                                                                                 token2);

                                                        }

                                                        #endregion

                                                    }

                                                    #endregion

                                                    if (success != SendStatus.Success ||
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
                                                                                                                 sharedSubprotocols,
                                                                                                                 EventTracking_Id.New,
                                                                                                                 token2);

                                                                responseTask.Wait(TimeSpan.FromSeconds(10));

                                                            }

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnNewWebSocketConnection));
                                                        }

                                                        #endregion

                                                        IsStillHTTP = false;
                                                        bytes = [];

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
                                                                                out var errorResponse) &&
                                                        frame is not null)
                                                    {

                                                        var now             = Timestamp.Now;
                                                        var eventTrackingId = EventTracking_Id.New;
                                                        WebSocketFrame? responseFrame = null;

                                                        #region OnWebSocketFrameReceived

                                                        try
                                                        {

                                                            OnWebSocketFrameReceived?.Invoke(now,
                                                                                             this,
                                                                                             webSocketConnection,
                                                                                             eventTrackingId,
                                                                                             frame,
                                                                                             token2);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnWebSocketFrameReceived));
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
                                                                                                  frame.Payload.ToUTF8String(),
                                                                                                  token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnTextMessageReceived));
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
                                                                    if (textMessageResponse is not null &&
                                                                        textMessageResponse.ResponseMessage is not null &&
                                                                        textMessageResponse.ResponseMessage != "")
                                                                    {
                                                                        responseFrame = WebSocketFrame.Text(textMessageResponse.ResponseMessage);
                                                                    }

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(ProcessTextMessage));
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
                                                                                                    frame.Payload,
                                                                                                    token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnBinaryMessageReceived));
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
                                                                    if (binaryMessageResponse is not null &&
                                                                        binaryMessageResponse.ResponseMessage is not null &&
                                                                        binaryMessageResponse.ResponseMessage.Length > 0)
                                                                    {
                                                                        responseFrame = WebSocketFrame.Binary(binaryMessageResponse.ResponseMessage);
                                                                    }

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(ProcessBinaryMessage));
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
                                                                                                  frame,
                                                                                                  token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnPingMessageReceived));
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
                                                                                                  frame,
                                                                                                  token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnPongMessageReceived));
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
                                                                                                   frame.GetClosingReason(),
                                                                                                   token2);

                                                                }
                                                                catch (Exception e)
                                                                {
                                                                    DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnCloseMessageReceived));
                                                                }

                                                                #endregion

                                                                // The close handshake demands that we have to send a close frame back!
                                                                await SendWebSocketFrame(
                                                                          webSocketConnection,
                                                                          WebSocketFrame.Close(),
                                                                          eventTrackingId
                                                                      );

                                                                webSocketConnection.Close();

                                                                break;

                                                                #endregion

                                                        }

                                                        #region Send immediate response frame... when available

                                                        if (responseFrame is not null)
                                                        {

                                                            var success = await SendWebSocketFrame(webSocketConnection,
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
                                                            bytes = [];

                                                        else
                                                        {
                                                            // The buffer might contain additional web socket frames...
                                                            var newBytes = new Byte[(UInt64) bytes.Length - frameLength];
                                                            Array.Copy(bytes, (Int32) frameLength, newBytes, 0, newBytes.Length);
                                                            bytes = newBytes;
                                                        }

                                                        bytesLeftOver = [];

                                                    }
                                                    else
                                                    {
                                                        //DebugX.Log($"Could not parse the given web socket frame of {bytesLeftOver.Length} byte(s): {errorResponse}");
                                                        bytesLeftOver = bytes;
                                                        bytes = [];
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
                                                    DebugX.Log(nameof(AWebSocketServer) + ": Closing invalid TCP connection from: " + webSocketConnection.RemoteSocket);
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
                                                                              EventTracking_Id.New,
                                                                              "!!!",
                                                                              token2);

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnTCPConnectionClosed));
                                            }

                                            #endregion

                                        }
                                        else
                                            DebugX.Log(nameof(AWebSocketServer), " The given web socket connection is invalid!");

                                    }
                                    catch (Exception e)
                                    {
                                        DebugX.Log(nameof(AWebSocketServer), " Exception in web socket server: " + e.Message + Environment.NewLine + e.StackTrace);
                                    }

                                },
                                new WebSocketServerConnection(
                                    this,
                                    newTCPConnection,
                                    sslStream,
                                    SlowNetworkSimulationDelay: SlowNetworkSimulationDelay
                                ),
                                token);

                            }
                            catch (Exception e)
                            {
                                DebugX.Log(" [AWebSocketServer] TLS exception: ", e.Message, e.StackTrace is not null ? Environment.NewLine + e.StackTrace : "");
                                newTCPConnection.Close();
                            }

                        }

                    }

                    #region Stop TCP listener

                    DebugX.Log(nameof(AWebSocketServer), " Stopping server on " + IPSocket.IPAddress + ":" + IPSocket.Port);

                    tcpListener.Stop();

                    isRunning = false;

                    #endregion

                    //ToDo: Request all client connections to finish!

                    #region Send OnServerStopped event

                    var onServerStopped = OnServerStopped;
                    if (onServerStopped is not null)
                    {
                        try
                        {

                            await Task.WhenAll(onServerStopped.GetInvocationList().
                                                   OfType <OnServerStoppedDelegate>().
                                                   Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                  Timestamp.Now,
                                                                                  this,
                                                                                  EventTracking_Id.New,
                                                                                  "!!!",
                                                                                  token
                                                                              )).
                                                   ToArray());

                        }
                        catch (Exception e)
                        {
                            DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(OnNewTCPConnection)}");
                        }
                    }

                    #endregion

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(AWebSocketServer));
                }

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
        public async Task Shutdown(String?  Message   = null,
                                   Boolean  Wait      = true)
        {

            cancellationTokenSource.Cancel();

            if (Wait)
            {
                while (isRunning)
                {
                    await Task.Delay(10);
                }
            }

        }

        #endregion


        #region (virtual) ProcessTextMessage  (RequestTimestamp, Connection, TextMessage,   ...)

        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public virtual Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime                   RequestTimestamp,
                                                                             WebSocketServerConnection  Connection,
                                                                             String                     TextMessage,
                                                                             EventTracking_Id           EventTrackingId,
                                                                             CancellationToken          CancellationToken)

            => Task.FromResult(
                   new WebSocketTextMessageResponse(
                       RequestTimestamp,
                       TextMessage,
                       Timestamp.Now,
                       String.Empty,
                       EventTrackingId
                   )
               );

        #endregion

        #region (virtual) ProcessBinaryMessage(RequestTimestamp, Connection, BinaryMessage, ...)

        /// <summary>
        /// The default HTTP web socket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public virtual Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime                   RequestTimestamp,
                                                                                 WebSocketServerConnection  Connection,
                                                                                 Byte[]                     BinaryMessage,
                                                                                 EventTracking_Id           EventTrackingId,
                                                                                 CancellationToken          CancellationToken)

            => Task.FromResult(
                   new WebSocketBinaryMessageResponse(
                       RequestTimestamp,
                       BinaryMessage,
                       Timestamp.Now,
                       Array.Empty<Byte>(),
                       EventTrackingId
                   )
               );

        #endregion


        #region (protected) SendOnTextMessageSent   (Timestamp, Connection, EventTrackingId, TextMessage,   CancellationToken)

        /// <summary>
        /// Send an OnTextMessageSent event
        /// </summary>
        protected async Task SendOnTextMessageSent(DateTime                   Timestamp,
                                                   WebSocketServerConnection  Connection,
                                                   EventTracking_Id           EventTrackingId,
                                                   String                     TextMessage,
                                                   CancellationToken          CancellationToken)
        {

            try
            {

                var OnTextMessageSentLocal = OnTextMessageSent;
                if (OnTextMessageSentLocal is not null)
                {

                    var responseTask = OnTextMessageSentLocal(Timestamp,
                                                              this,
                                                              Connection,
                                                              EventTrackingId,
                                                              TextMessage,
                                                              CancellationToken);

                    await responseTask.WaitAsync(
                              TimeSpan.FromSeconds(10),
                              CancellationToken
                          );

                }

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnTextMessageSent));
            }

        }

        #endregion

        #region (protected) SendOnBinaryMessageSent (Timestamp, Connection, EventTrackingId, BinaryMessage, CancellationToken)

        /// <summary>
        /// Send an OnBinaryMessageSent event
        /// </summary>
        protected async Task SendOnBinaryMessageSent(DateTime                   Timestamp,
                                                     WebSocketServerConnection  Connection,
                                                     EventTracking_Id           EventTrackingId,
                                                     Byte[]                     BinaryMessage,
                                                     CancellationToken          CancellationToken)
        {

            try
            {

                var OnBinaryMessageSentLocal = OnBinaryMessageSent;
                if (OnBinaryMessageSentLocal is not null)
                {

                    var responseTask = OnBinaryMessageSentLocal(Timestamp,
                                                                this,
                                                                Connection,
                                                                EventTrackingId,
                                                                BinaryMessage,
                                                                CancellationToken);

                    await responseTask.WaitAsync(
                              TimeSpan.FromSeconds(10),
                              CancellationToken
                          );

                }

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnBinaryMessageSent));
            }

        }

        #endregion

        #region (protected) SendOnWebSocketFrameSent(Timestamp, Connection, EventTrackingId, Frame,         CancellationToken)

        /// <summary>
        /// Send an OnWebSocketFrameSent event
        /// </summary>
        protected async Task SendOnWebSocketFrameSent(DateTime                   Timestamp,
                                                      WebSocketServerConnection  Connection,
                                                      EventTracking_Id           EventTrackingId,
                                                      WebSocketFrame             Frame,
                                                      CancellationToken          CancellationToken)
        {

            try
            {

                var OnWebSocketFrameSentLocal = OnWebSocketFrameSent;
                if (OnWebSocketFrameSentLocal is not null)
                {

                    var responseTask = OnWebSocketFrameSentLocal(Timestamp,
                                                                 this,
                                                                 Connection,
                                                                 EventTrackingId,
                                                                 Frame,
                                                                 CancellationToken);

                    await responseTask.WaitAsync(
                              TimeSpan.FromSeconds(10),
                              CancellationToken
                          );

                }

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnWebSocketFrameSent));
            }

        }

        #endregion


    }

}
