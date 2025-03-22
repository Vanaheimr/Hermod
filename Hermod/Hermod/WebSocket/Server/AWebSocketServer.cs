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

using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// Extensions methods for HTTP WebSocket servers.
    /// </summary>
    public static class AWebSocketServerExtensions
    {

        #region BroadcastTextMessage   (this WebSocketServer, Message,                    EventTrackingId = null, CancellationToken = default)

        public static Task BroadcastTextMessage(this WebSocketServer  WebSocketServer,
                                                String                Message,
                                                EventTracking_Id?     EventTrackingId     = null,
                                                CancellationToken     CancellationToken   = default)
        {

            if (WebSocketServer is null)
                return Task.CompletedTask;

            var connections = WebSocketServer.WebSocketConnections.ToArray();

            return Task.WhenAll(
                       connections.
                           Select(connection => WebSocketServer.SendTextMessage(
                                                    connection,
                                                    Message,
                                                    EventTrackingId,
                                                    CancellationToken
                                                ))
                   );

        }

        #endregion

        #region BroadcastJSONMessage   (this WebSocketServer, Message, Formatting = None, EventTrackingId = null, CancellationToken = default)

        public static Task BroadcastJSONMessage(this WebSocketServer        WebSocketServer,
                                                JObject                     Message,
                                                Newtonsoft.Json.Formatting  Formatting          = Newtonsoft.Json.Formatting.None,
                                                EventTracking_Id?           EventTrackingId     = null,
                                                CancellationToken           CancellationToken   = default)
        {

            if (WebSocketServer is null)
                return Task.CompletedTask;

            var connections = WebSocketServer.WebSocketConnections.ToArray();

            return Task.WhenAll(
                       connections.
                           Select(connection => WebSocketServer.SendTextMessage(
                                                    connection,
                                                    Message.ToString(Formatting),
                                                    EventTrackingId,
                                                    CancellationToken
                                                ))
                   );

        }


        public static Task BroadcastJSONMessage(this WebSocketServer        WebSocketServer,
                                                JArray                      Message,
                                                Newtonsoft.Json.Formatting  Formatting          = Newtonsoft.Json.Formatting.None,
                                                EventTracking_Id?           EventTrackingId     = null,
                                                CancellationToken           CancellationToken   = default)
        {

            if (WebSocketServer is null)
                return Task.CompletedTask;

            var connections = WebSocketServer.WebSocketConnections.ToArray();

            return Task.WhenAll(
                       connections.
                           Select(connection => WebSocketServer.SendTextMessage(
                                                    connection,
                                                    Message.ToString(Formatting),
                                                    EventTrackingId,
                                                    CancellationToken
                                                ))
                   );

        }

        #endregion

        #region BroadcastBinaryMessage (this WebSocketServer, Message,                    EventTrackingId = null, CancellationToken = default)

        public static Task BroadcastBinaryMessage(this WebSocketServer  WebSocketServer,
                                                  Byte[]                Message,
                                                  EventTracking_Id?     EventTrackingId     = null,
                                                  CancellationToken     CancellationToken   = default)
        {

            if (WebSocketServer is null)
                return Task.CompletedTask;

            var connections = WebSocketServer.WebSocketConnections.ToArray();

            return Task.WhenAll(
                       connections.
                           Select(connection => WebSocketServer.SendBinaryMessage(
                                                    connection,
                                                    Message,
                                                    EventTrackingId,
                                                    CancellationToken
                                                ))
                   );

        }

        #endregion

    }


    /// <summary>
    /// A HTTP WebSocket server.
    /// </summary>
    public abstract class AWebSocketServer : IWebSocketServer
    {

        #region Data

        private readonly  HashSet<String>                                                           secWebSocketProtocols         = [];

        private readonly  ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>  webSocketConnections          = [];

        private           Thread?                                                                   listenerThread;

        private readonly  CancellationTokenSource                                                   cancellationTokenSource;

        private volatile  Boolean                                                                   isRunning                     = false;

        public readonly   TimeSpan                                                                  DefaultWebSocketPingEvery     = TimeSpan.FromSeconds(30);

        private const     String                                                                    LogfileName                   = "HTTPWebSocketServer.log";

        public  const     UInt16                                                                    DefaultMaxClientConnections   = 16;

        #endregion

        #region Properties

        /// <summary>
        /// Return an enumeration of all currently connected HTTP WebSocket connections.
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
        public String                                                          HTTPServiceName                 { get; }

        /// <summary>
        /// The optional description of this HTTP WebSocket server.
        /// </summary>
        public I18NString                                                      Description                     { get; set; }

        public Func<X509Certificate2>?                                         ServerCertificateSelector       { get; }
        public RemoteTLSClientCertificateValidationHandler<IWebSocketServer>?  ClientCertificateValidator      { get; }
        public LocalCertificateSelectionHandler?                               LocalCertificateSelector        { get; }
        public SslProtocols?                                                   AllowedTLSProtocols             { get; }
        public Boolean?                                                        ClientCertificateRequired       { get; }
        public Boolean?                                                        CheckCertificateRevocation      { get; }

        public List<X509Certificate2>                                          TrustedClientCertificates       { get; } = [];
        public List<X509Certificate2>                                          TrustedCertificatAuthorities    { get; } = [];

        public ServerThreadNameCreatorDelegate                                 ServerThreadNameCreator         { get; }
        public ServerThreadPriorityDelegate                                    ServerThreadPrioritySetter      { get; }
        public Boolean                                                         ServerThreadIsBackground        { get; }

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public IIPAddress                                                      IPAddress
            => IPSocket.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                                                          IPPort
            => IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public IPSocket                                                        IPSocket                      { get; private set; }

        /// <summary>
        /// Whether the web socket TCP listener is currently running.
        /// </summary>
        public Boolean                                                         IsRunning
            => isRunning;

        /// <summary>
        /// The supported secondary web socket protocols.
        /// </summary>
        public IEnumerable<String>                                             SecWebSocketProtocols
            => secWebSocketProtocols;

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                                                         DisableWebSocketPings         { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                                                        WebSocketPingEvery            { get; set; }


        public UInt64?                                                         MaxTextMessageSizeIn          { get; set; }
        public UInt64?                                                         MaxTextMessageSizeOut         { get; set; }
        public UInt64?                                                         MaxTextFragmentLengthIn       { get; set; }
        public UInt64?                                                         MaxTextFragmentLengthOut      { get; set; }

        public UInt64?                                                         MaxBinaryMessageSizeIn        { get; set; }
        public UInt64?                                                         MaxBinaryMessageSizeOut       { get; set; }
        public UInt64?                                                         MaxBinaryFragmentLengthIn     { get; set; }
        public UInt64?                                                         MaxBinaryFragmentLengthOut    { get; set; }


        /// <summary>
        /// An additional delay between sending each byte to the networking stack.
        /// This is intended for debugging other web socket stacks.
        /// </summary>
        public TimeSpan?                                                       SlowNetworkSimulationDelay    { get; set; }


        /// <summary>
        /// Logins and passwords for HTTP Basic Authentication.
        /// </summary>
        public ConcurrentDictionary<String , SecurePassword>                   ClientLogins                  { get; } = [];

        /// <summary>
        /// Logins and TOTP config (shared secrets, ...) for HTTP TOTP Authentication.
        /// </summary>
        public ConcurrentDictionary<String , TOTPConfig>                       ClientTOTPConfig              { get; } = [];


        public Boolean                                                         RequireAuthentication         { get; set; }


        public UInt32                                                          MaxClientConnections          { get; set; } = DefaultMaxClientConnections;

        /// <summary>
        /// An optional DNS client to use.
        /// </summary>
        public DNSClient?                                                      DNSClient                     { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP WebSocket server started.
        /// </summary>
        public event OnServerStartedDelegate?                           OnServerStarted;


        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnValidateTCPConnectionDelegate?                   OnValidateTCPConnection;

        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        public event OnNewTCPConnectionDelegate?                        OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a new TLS connection was accepted.
        /// </summary>
        public event OnNewTLSConnectionDelegate?                        OnNewTLSConnection;

        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public event OnTCPConnectionClosedDelegate?                     OnTCPConnectionClosed;


        /// <summary>
        /// An event sent whenever a HTTP request was received.
        /// </summary>
        public event HTTPRequestLogDelegate?                            OnHTTPRequest;

        /// <summary>
        /// An event sent whenever the HTTP headers of a new web socket connection
        /// need to be validated or filtered by an upper layer application logic.
        /// </summary>
        public event OnValidateWebSocketConnectionDelegate?             OnValidateWebSocketConnection;

        /// <summary>
        /// An event sent whenever the client sent a list of supported subprotocols
        /// and the server should select one of them.
        /// </summary>
        public       SubprotocolSelectorDelegate?                       SubprotocolSelector;

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        public event OnNewWebSocketConnectionDelegate?                  OnNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a reponse to a HTTP request was sent.
        /// </summary>
        public event HTTPResponseLogDelegate?                           OnHTTPResponse;


        /// <summary>
        /// An event sent whenever a web socket frame was received.
        /// </summary>
        public event OnWebSocketFrameDelegate?                          OnWebSocketFrameReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        public event OnWebSocketFrameDelegate?                          OnWebSocketFrameSent;


        /// <summary>
        /// An event sent whenever a text message was sent.
        /// </summary>
        public event OnWebSocketServerTextMessageSentDelegate?          OnTextMessageSent;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketServerTextMessageReceivedDelegate?      OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        public event OnWebSocketServerBinaryMessageSentDelegate?        OnBinaryMessageSent;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketServerBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        public event OnWebSocketServerPingMessageSentDelegate?          OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        public event OnWebSocketServerPingMessageReceivedDelegate?      OnPingMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket pong frame was sent.
        /// </summary>
        public event OnWebSocketServerPongMessageSentDelegate?          OnPongMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        public event OnWebSocketServerPongMessageReceivedDelegate?      OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket close frame was sent.
        /// </summary>
        public event OnWebSocketServerCloseMessageSentDelegate?         OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        public event OnWebSocketServerCloseMessageReceivedDelegate?     OnCloseMessageReceived;


        /// <summary>
        /// An event sent whenever the HTTP WebSocket server stopped.
        /// </summary>
        public event OnServerStoppedDelegate?                           OnServerStopped;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="TCPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="SecWebSocketProtocols"></param>
        /// <param name="DisableWebSocketPings"></param>
        /// <param name="WebSocketPingEvery"></param>
        /// <param name="SlowNetworkSimulationDelay"></param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
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
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public AWebSocketServer(IIPAddress?                                                     IPAddress                    = null,
                                IPPort?                                                         TCPPort                      = null,
                                String?                                                         HTTPServiceName              = null,
                                I18NString?                                                     Description                  = null,

                                Boolean?                                                        RequireAuthentication        = true,
                                IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                                SubprotocolSelectorDelegate?                                    SubprotocolSelector          = null,
                                Boolean                                                         DisableWebSocketPings        = false,
                                TimeSpan?                                                       WebSocketPingEvery           = null,
                                TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                Func<X509Certificate2>?                                         ServerCertificateSelector    = null,
                                RemoteTLSClientCertificateValidationHandler<IWebSocketServer>?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?                               LocalCertificateSelector     = null,
                                SslProtocols?                                                   AllowedTLSProtocols          = null,
                                Boolean?                                                        ClientCertificateRequired    = null,
                                Boolean?                                                        CheckCertificateRevocation   = null,

                                ServerThreadNameCreatorDelegate?                                ServerThreadNameCreator      = null,
                                ServerThreadPriorityDelegate?                                   ServerThreadPrioritySetter   = null,
                                Boolean?                                                        ServerThreadIsBackground     = null,
                                ConnectionIdBuilder?                                            ConnectionIdBuilder          = null,
                                TimeSpan?                                                       ConnectionTimeout            = null,
                                UInt32?                                                         MaxClientConnections         = null,

                                DNSClient?                                                      DNSClient                    = null,
                                Boolean                                                         AutoStart                    = false)

            : this(new IPSocket(
                       IPAddress ?? IPv4Address.Any,   // 0.0.0.0  IPv4+IPv6 sockets seem to fail on Win11!
                       TCPPort   ?? IPPort.HTTP
                   ),
                   HTTPServiceName,
                   Description,

                   RequireAuthentication,
                   SecWebSocketProtocols,
                   SubprotocolSelector,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
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
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="TCPSocket">The TCP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="SecWebSocketProtocols"></param>
        /// <param name="DisableWebSocketPings"></param>
        /// <param name="WebSocketPingEvery"></param>
        /// <param name="SlowNetworkSimulationDelay"></param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
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
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public AWebSocketServer(IPSocket                                                        TCPSocket,
                                String?                                                         HTTPServiceName              = null,
                                I18NString?                                                     Description                  = null,

                                Boolean?                                                        RequireAuthentication        = true,
                                IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                                SubprotocolSelectorDelegate?                                    SubprotocolSelector          = null,
                                Boolean                                                         DisableWebSocketPings        = false,
                                TimeSpan?                                                       WebSocketPingEvery           = null,
                                TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                Func<X509Certificate2>?                                         ServerCertificateSelector    = null,
                                RemoteTLSClientCertificateValidationHandler<IWebSocketServer>?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?                               LocalCertificateSelector     = null,
                                SslProtocols?                                                   AllowedTLSProtocols          = null,
                                Boolean?                                                        ClientCertificateRequired    = null,
                                Boolean?                                                        CheckCertificateRevocation   = null,

                                ServerThreadNameCreatorDelegate?                                ServerThreadNameCreator      = null,
                                ServerThreadPriorityDelegate?                                   ServerThreadPrioritySetter   = null,
                                Boolean?                                                        ServerThreadIsBackground     = null,
                                ConnectionIdBuilder?                                            ConnectionIdBuilder          = null,
                                TimeSpan?                                                       ConnectionTimeout            = null,
                                UInt32?                                                         MaxClientConnections         = null,

                                DNSClient?                                                      DNSClient                    = null,
                                Boolean                                                         AutoStart                    = false)
        {

            this.IPSocket                    = TCPSocket;
            this.HTTPServiceName             = HTTPServiceName            ?? "GraphDefined HTTP WebSocket Service v2.0";
            this.Description                 = Description                ?? I18NString.Empty;
            this.ServerThreadNameCreator     = ServerThreadNameCreator    ?? (socket => $"AWebSocketServer {socket}");
            this.ServerThreadPrioritySetter  = ServerThreadPrioritySetter ?? (socket => ThreadPriority.AboveNormal);
            this.ServerThreadIsBackground    = ServerThreadIsBackground   ?? false;

            this.RequireAuthentication       = RequireAuthentication      ?? true;
            this.secWebSocketProtocols       = SecWebSocketProtocols is not null
                                                   ? new HashSet<String>(SecWebSocketProtocols)
                                                   : [];
            this.SubprotocolSelector         = SubprotocolSelector;

            this.DisableWebSocketPings       = DisableWebSocketPings;
            this.WebSocketPingEvery          = WebSocketPingEvery ?? DefaultWebSocketPingEvery;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

            this.ServerCertificateSelector   = ServerCertificateSelector;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.AllowedTLSProtocols         = AllowedTLSProtocols;
            this.ClientCertificateRequired   = ClientCertificateRequired;
            this.CheckCertificateRevocation  = CheckCertificateRevocation;
            this.MaxClientConnections        = MaxClientConnections       ?? DefaultMaxClientConnections;

            this.DNSClient                   = DNSClient;

            this.webSocketConnections        = new ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>();
            this.cancellationTokenSource     = new CancellationTokenSource();

            if (AutoStart)
                Start();

        }

        #endregion

        #endregion


        #region SendTextMessage    (Connection, TextMessage,    ...)

        /// <summary>
        /// Send a text web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The text message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public Task<SentStatus> SendTextMessage(WebSocketServerConnection  Connection,
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

        #region SendBinaryMessage  (Connection, BinaryMessage,  ...)

        /// <summary>
        /// Send a binary web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The binary message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public Task<SentStatus> SendBinaryMessage(WebSocketServerConnection  Connection,
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

        #region SendWebSocketFrame (Connection, WebSocketFrame, ...)

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="WebSocketFrame">The web socket frame to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public async Task<SentStatus> SendWebSocketFrame(WebSocketServerConnection  Connection,
                                                         WebSocketFrame             WebSocketFrame,
                                                         EventTracking_Id?          EventTrackingId     = null,
                                                         CancellationToken          CancellationToken   = default)
        {

            EventTrackingId ??= EventTracking_Id.New;

            var sentStatus = await Connection.SendWebSocketFrame(
                                       WebSocketFrame,
                                       CancellationToken
                                   );

            #region Send OnWebSocketFrameSent event

            await SendOnWebSocketFrameSent(
                      Timestamp.Now,
                      Connection,
                      EventTrackingId,
                      WebSocketFrame,
                      CancellationToken
                  );

            #endregion

            #region Send OnTextMessageSent    event

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Text)
            {
                var logger = OnTextMessageSent;
                if (logger is not null)
                {
                    try
                    {

                        await Task.WhenAll(logger.GetInvocationList().
                                                OfType<OnWebSocketServerTextMessageSentDelegate>().
                                                Select(loggingDelegate => loggingDelegate.Invoke(
                                                                              Timestamp.Now,
                                                                              this,
                                                                              Connection,
                                                                              WebSocketFrame,
                                                                              EventTrackingId,
                                                                              WebSocketFrame.Payload.ToUTF8String(),
                                                                              sentStatus,
                                                                              CancellationToken
                                                                          )));

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnTextMessageSent));
                    }
                }
            }

            #endregion

            #region Send OnBinaryMessageSent  event

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Binary)
            {
                var logger = OnBinaryMessageSent;
                if (logger is not null)
                {
                    try
                    {

                        await Task.WhenAll(logger.GetInvocationList().
                                                OfType<OnWebSocketServerBinaryMessageSentDelegate>().
                                                Select(loggingDelegate => loggingDelegate.Invoke(
                                                                              Timestamp.Now,
                                                                              this,
                                                                              Connection,
                                                                              WebSocketFrame,
                                                                              EventTrackingId,
                                                                              WebSocketFrame.Payload,
                                                                              sentStatus,
                                                                              CancellationToken
                                                                          )));

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnBinaryMessageSent));
                    }
                }
            }

            #endregion

            #region Send OnPingMessageSent    event

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Ping)
            {
                var logger = OnPingMessageSent;
                if (logger is not null)
                {
                    try
                    {

                        await Task.WhenAll(logger.GetInvocationList().
                                                OfType<OnWebSocketServerPingMessageSentDelegate>().
                                                Select(loggingDelegate => loggingDelegate.Invoke(
                                                                              Timestamp.Now,
                                                                              this,
                                                                              Connection,
                                                                              WebSocketFrame,
                                                                              EventTrackingId,
                                                                              WebSocketFrame.Payload,
                                                                              sentStatus,
                                                                              CancellationToken
                                                                          )));

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnPingMessageSent));
                    }
                }
            }

            #endregion

            #region Send OnPongMessageSent    event

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Pong)
            {
                var logger = OnPongMessageSent;
                if (logger is not null)
                {
                    try
                    {

                        await Task.WhenAll(logger.GetInvocationList().
                                                OfType<OnWebSocketServerPongMessageSentDelegate>().
                                                Select(loggingDelegate => loggingDelegate.Invoke(
                                                                              Timestamp.Now,
                                                                              this,
                                                                              Connection,
                                                                              WebSocketFrame,
                                                                              EventTrackingId,
                                                                              WebSocketFrame.Payload,
                                                                              sentStatus,
                                                                              CancellationToken
                                                                          )));

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnPongMessageSent));
                    }
                }
            }

            #endregion

            #region Send OnCloseMessageSent   event

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Close)
            {
                var logger = OnCloseMessageSent;
                if (logger is not null)
                {
                    try
                    {

                        await Task.WhenAll(logger.GetInvocationList().
                                                OfType<OnWebSocketServerCloseMessageSentDelegate>().
                                                Select(loggingDelegate => loggingDelegate.Invoke(
                                                                              Timestamp.Now,
                                                                              this,
                                                                              Connection,
                                                                              WebSocketFrame,
                                                                              EventTrackingId,
                                                                              WebSocketFrame.GetClosingStatusCode(),
                                                                              WebSocketFrame.GetClosingReason(),
                                                                              sentStatus,
                                                                              CancellationToken
                                                                          )));

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(e, nameof(AWebSocketServer) + "." + nameof(OnCloseMessageSent));
                    }
                }
            }

            #endregion

            return sentStatus;

        }

        #endregion

        #region RemoveConnection   (Connection)

        /// <summary>
        /// Remove the given web socket connection.
        /// </summary>
        /// <param name="Connection">A HTTP WebSocket connection.</param>
        public Boolean RemoveConnection(WebSocketServerConnection Connection)
        {

            DebugX.Log(nameof(AWebSocketServer), " Removing HTTP WebSocket connection with " + Connection.RemoteSocket);

            return webSocketConnections.Remove(Connection.RemoteSocket, out _);

        }

        #endregion


        #region AddOrUpdateHTTPBasicAuth(Username, Password)

        /// <summary>
        /// Add the given HTTP Basic Authentication password for the given username.
        /// </summary>
        /// <param name="Username">The unique identification of the username.</param>
        /// <param name="Password">The password of the charging station.</param>
        public HTTPBasicAuthentication AddOrUpdateHTTPBasicAuth(String Username,
                                                                String Password)
        {

            ClientLogins.AddOrUpdate(
                             Username,
                             SecurePassword.Parse(Password),
                             (chargingStationId, password) => SecurePassword.Parse(Password)
                         );

            return HTTPBasicAuthentication.Create(
                       Username,
                       Password
                   );

        }

        #endregion

        #region RemoveHTTPBasicAuth     (Username)

        /// <summary>
        /// Remove the given HTTP Basic Authentication for the given username.
        /// </summary>
        /// <param name="Username">The unique identification of the username.</param>
        public Boolean RemoveHTTPBasicAuth(String Username)
        {

            if (ClientLogins.ContainsKey(Username))
                return ClientLogins.TryRemove(Username, out _);

            return true;

        }

        #endregion


        public void AddSecWebSocketProtocol(String Protocol)
        {
            secWebSocketProtocols.Add(Protocol);
        }


        #region Start()

        /// <summary>
        /// Start the HTTP WebSocket listener thread.
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

                    DebugX.Log($"{Description.FirstText()}: {IPSocket}");

                    var tcpListener  = new TcpListener(IPSocket.ToIPEndPoint());
                    tcpListener.Start();

                    isRunning        = true;

                    this.IPSocket    = new IPSocket(
                                           IPAddress,
                                           IPPort.Parse(((IPEndPoint) tcpListener.LocalEndpoint).Port)
                                       );

                    var token        = cancellationTokenSource.Token;

                    #endregion

                    #region Send OnServerStarted event

                    await LogEvent(
                              OnServerStarted,
                              loggingDelegate => loggingDelegate.Invoke(
                                  Timestamp.Now,
                                  this,
                                  EventTracking_Id.New,
                                  token
                              )
                          );

                    #endregion

                    try
                    {

                        while (!token.IsCancellationRequested)
                        {

                            #region Accept a new TCP connection

                            // Wait for a new/pending client connection
                            //while (!token.IsCancellationRequested && !tcpListener.Pending())
                                Thread.Sleep(5);

                            if (token.IsCancellationRequested)
                                break;

                            var newTCPConnection = await tcpListener.AcceptTcpClientAsync(token);
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

                                        sslStream      = new SslStream(
                                                             innerStream:                         newTCPConnection.GetStream(),
                                                             leaveInnerStreamOpen:                true,
                                                             userCertificateValidationCallback:   ClientCertificateValidator is null
                                                                                                      ? null
                                                                                                      : (sender,
                                                                                                         certificate,
                                                                                                         chain,
                                                                                                         policyErrors) => ClientCertificateValidator(
                                                                                                                              sender,
                                                                                                                              certificate is not null
                                                                                                                                  ? new X509Certificate2(certificate)
                                                                                                                                  : null,
                                                                                                                              chain,
                                                                                                                              this,
                                                                                                                              policyErrors
                                                                                                                          ).Item1,
                                                             userCertificateSelectionCallback:    LocalCertificateSelector is null
                                                                                                      ? null
                                                                                                      : (sender,
                                                                                                         targetHost,
                                                                                                         localCertificates,
                                                                                                         remoteCertificate,
                                                                                                         acceptableIssuers) => LocalCertificateSelector(
                                                                                                                                   sender,
                                                                                                                                   targetHost,
                                                                                                                                   localCertificates.
                                                                                                                                       Cast<X509Certificate>().
                                                                                                                                       Select(certificate => new X509Certificate2(certificate)),
                                                                                                                                   remoteCertificate is not null
                                                                                                                                       ? new X509Certificate2(remoteCertificate)
                                                                                                                                       : null,
                                                                                                                                   acceptableIssuers
                                                                                                                               ),
                                                             encryptionPolicy:                    EncryptionPolicy.RequireEncryption
                                                         );

                                        sslStream.AuthenticateAsServer(
                                            serverCertificate:            serverCertificate,
                                            clientCertificateRequired:    ClientCertificateValidator is not null,
                                            enabledSslProtocols:          AllowedTLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13,
                                            checkCertificateRevocation:   false
                                        );

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
                                                var            pingCounter      = 1UL;

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

                                                        await Task.WhenAll(
                                                                  onNewTCPConnection.GetInvocationList().
                                                                      OfType<OnNewTCPConnectionDelegate>().
                                                                      Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                                     Timestamp.Now,
                                                                                                     this,
                                                                                                     webSocketConnection,
                                                                                                     EventTracking_Id.New,
                                                                                                     token2
                                                                                                 ))
                                                              );

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(OnNewTCPConnection)}");
                                                    }
                                                }

                                                #endregion

                                                #region Send OnNewTLSConnection event

                                                if (webSocketConnection.ClientCertificate is not null)
                                                {

                                                    var onNewTLSConnection = OnNewTLSConnection;
                                                    if (onNewTLSConnection is not null)
                                                    {
                                                        try
                                                        {

                                                            await Task.WhenAll(
                                                                      onNewTLSConnection.GetInvocationList().
                                                                          OfType<OnNewTLSConnectionDelegate>().
                                                                          Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                                         Timestamp.Now,
                                                                                                         this,
                                                                                                         webSocketConnection,
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

                                                                var eventTrackingId  = EventTracking_Id.New;
                                                                var payload          = $"{pingCounter++}:{UUIDv7.Generate()}";
                                                                var frame            = WebSocketFrame.Ping(payload.ToUTF8Bytes());
                                                                var sentStatus       = await SendWebSocketFrame(
                                                                                                 webSocketConnection,
                                                                                                 frame,
                                                                                                 eventTrackingId,
                                                                                                 token2
                                                                                             );

                                                                if      (sentStatus == SentStatus.Success)
                                                                { }
                                                                else if (sentStatus == SentStatus.FatalError)
                                                                {
                                                                    await webSocketConnection.Close(
                                                                              WebSocketFrame.ClosingStatusCode.ProtocolError
                                                                          );
                                                                }
                                                                else
                                                                {
                                                                    sendErrors++;
                                                                    DebugX.Log($"Web socket connection to {webSocketConnection.RemoteSocket}: Ping failed ({sendErrors})!");
                                                                }

                                                                lastWebSocketPingTimestamp = Timestamp.Now;

                                                            }

                                                            #endregion

                                                            await Task.Delay(5);

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

                                                        #region Parse HTTP request...

                                                        var sharedSubprotocols   = Enumerable.Empty<String>();
                                                        var selectedSubprotocol  = String.Empty;

                                                        // GET / HTTP/1.1
                                                        // Host:                    127.0.0.1:51693
                                                        // Connection:              Upgrade
                                                        // Upgrade:                 websocket
                                                        // Sec-WebSocket-Key:       I7uShTZm0dkbf5TqbL7QGg==
                                                        // Sec-WebSocket-Protocol:  ocpp2.0.1, ocpp2.1
                                                        // Sec-WebSocket-Version:   13
                                                        // Authorization:           Basic Z3c6Z3cyY3NtczFfMTIzNDU2Nzg=
                                                        // X-OCPP-NetworkingMode:   OverlayNetwork

                                                        // GET / HTTP/1.1
                                                        // Host:                    127.0.0.1:51535
                                                        // Connection:              Upgrade
                                                        // Upgrade:                 websocket
                                                        // Sec-WebSocket-Key:       UxdM/tiYhE4N7VhSAwX84w==
                                                        // Sec-WebSocket-Version:   13
                                                        // Authorization:           Basic Z3c6Z3cyY3NtczJfMTIzNDU2Nzg=
                                                        // X-OCPP-NetworkingMode:   OverlayNetwork

                                                        if (HTTPRequest.TryParse(bytes, out var httpRequest))
                                                        {

                                                            webSocketConnection.Login       = webSocketConnection.RemoteSocket.ToString();
                                                            webSocketConnection.HTTPRequest = httpRequest;

                                                            #region Log OnHTTPRequest

                                                            await LogEvent(
                                                                      OnHTTPRequest,
                                                                      loggingDelegate => loggingDelegate.Invoke(
                                                                          Timestamp.Now,
                                                                          this,
                                                                          httpRequest,
                                                                          token2
                                                                      )
                                                                  );

                                                            #endregion

                                                            #region OnValidateWebSocketConnection

                                                            var onValidateWebSocketConnection = OnValidateWebSocketConnection;
                                                            if (onValidateWebSocketConnection is not null)
                                                            {

                                                                var httpResponseTasks = await Task.WhenAll(onValidateWebSocketConnection.GetInvocationList().
                                                                                                   Cast<OnValidateWebSocketConnectionDelegate>().
                                                                                                   Select(e => e(Timestamp.Now,
                                                                                                                 this,
                                                                                                                 webSocketConnection,
                                                                                                                 EventTracking_Id.New,
                                                                                                                 token2))).
                                                                                                   ConfigureAwait(false);

                                                                httpResponse = httpResponseTasks.FirstOrDefault();

                                                            }

                                                            #endregion

                                                            #region In case of a successful request

                                                            if (httpResponse is null)
                                                            {

                                                                if (webSocketConnection.HTTPRequest.Authorization is HTTPBasicAuthentication basicAuthentication)
                                                                    webSocketConnection.Login = basicAuthentication.Username;

                                                                // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                                                                // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                                                                // 3. Compute SHA-1 and Base64 hash of the new value
                                                                // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
    #pragma warning disable SCS0006 // Weak hashing function.
                                                                var swk              = webSocketConnection.HTTPRequest?.SecWebSocketKey;
                                                                var swka             = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                                                                var swkaSHA1BASE64   = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(swka)));
    #pragma warning restore SCS0006 // Weak hashing function.

                                                                sharedSubprotocols   = httpRequest.SecWebSocketProtocol.Where(protocol => SecWebSocketProtocols.Contains(protocol));
                                                                selectedSubprotocol  = SubprotocolSelector?.Invoke(this, webSocketConnection, sharedSubprotocols) ??
                                                                                           httpRequest.SecWebSocketProtocol.FirstOrDefault(protocol => SecWebSocketProtocols.Contains(protocol));


                                                                // HTTP/1.1 101 Switching Protocols
                                                                // Connection:              Upgrade
                                                                // Upgrade:                 websocket
                                                                // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                                                // Sec-WebSocket-Protocol:  ocpp2.1
                                                                // Sec-WebSocket-Version:   13
                                                                httpResponse         = new HTTPResponse.Builder(httpRequest) {
                                                                                           HTTPStatusCode        = HTTPStatusCode.SwitchingProtocols,
                                                                                           Server                = HTTPServiceName,
                                                                                           Connection            = ConnectionType.Upgrade,
                                                                                           Upgrade               = "websocket",
                                                                                           SecWebSocketAccept    = swkaSHA1BASE64,
                                                                                           SecWebSocketProtocol  = selectedSubprotocol,
                                                                                           SecWebSocketVersion   = "13"
                                                                                       }.AsImmutable;

                                                            }

                                                            #endregion

                                                        }

                                                        httpResponse ??= new HTTPResponse.Builder() {
                                                                             HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                                             Server          = HTTPServiceName,
                                                                             Date            = Timestamp.Now,
                                                                             Connection      = ConnectionType.Close
                                                                         }.AsImmutable;

                                                        #endregion

                                                        #region Send HTTP response

                                                        webSocketConnection.HTTPResponse = httpResponse;

                                                        var success = await webSocketConnection.Send($"{httpResponse.EntirePDU}\r\n\r\n".ToUTF8Bytes());

                                                        await LogEvent(
                                                                  OnHTTPResponse,
                                                                  loggingDelegate => loggingDelegate.Invoke(
                                                                      Timestamp.Now,
                                                                      this,
                                                                      httpRequest,
                                                                      httpResponse,
                                                                      token2
                                                                  )
                                                              );

                                                        #endregion

                                                        if (success != SentStatus.Success ||
                                                            httpResponse.Connection == ConnectionType.Close)
                                                        {
                                                            await webSocketConnection.Close(
                                                                      WebSocketFrame.ClosingStatusCode.ProtocolError
                                                                  );
                                                        }

                                                        else
                                                        {

                                                            #region Send OnNewWebSocketConnection event

                                                            await LogEvent(
                                                                      OnNewWebSocketConnection,
                                                                      loggingDelegate => loggingDelegate.Invoke(
                                                                          Timestamp.Now,
                                                                          this,
                                                                          webSocketConnection,
                                                                          sharedSubprotocols,
                                                                          selectedSubprotocol,
                                                                          EventTracking_Id.New,
                                                                          token2
                                                                      )
                                                                  );

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
                                                                                    out var errorResponse))
                                                        {

                                                            var now = Timestamp.Now;
                                                            webSocketConnection.LastReceivedTimestamp = now;
                                                            webSocketConnection.IncFramesReceivedCounter();

                                                            if (frame.IsFinal)
                                                                webSocketConnection.IncMessagesReceivedCounter();

                                                            #region OnWebSocketFrameReceived

                                                            await LogEvent(
                                                                      OnWebSocketFrameReceived,
                                                                      loggingDelegate => loggingDelegate.Invoke(
                                                                          now,
                                                                          this,
                                                                          webSocketConnection,
                                                                          frame.EventTrackingId,
                                                                          frame,
                                                                          token2
                                                                      )
                                                                  );

                                                            #endregion

                                                            switch (frame.Opcode)
                                                            {

                                                                #region Text   message received

                                                                case WebSocketFrame.Opcodes.Text:

                                                                    await LogEvent(
                                                                        OnTextMessageReceived,
                                                                        loggingDelegate => loggingDelegate.Invoke(
                                                                            now,
                                                                            this,
                                                                            webSocketConnection,
                                                                            frame,
                                                                            frame.EventTrackingId,
                                                                            frame.Payload.ToUTF8String(),
                                                                            token2
                                                                        )
                                                                    );

                                                                    break;

                                                                #endregion

                                                                #region Binary message received

                                                                case WebSocketFrame.Opcodes.Binary:

                                                                    await LogEvent(
                                                                        OnBinaryMessageReceived,
                                                                        loggingDelegate => loggingDelegate.Invoke(
                                                                            now,
                                                                            this,
                                                                            webSocketConnection,
                                                                            frame,
                                                                            frame.EventTrackingId,
                                                                            frame.Payload,
                                                                            token2
                                                                        )
                                                                    );

                                                                    break;

                                                                #endregion

                                                                #region Ping   message received

                                                                case WebSocketFrame.Opcodes.Ping:

                                                                    await LogEvent(
                                                                        OnPingMessageReceived,
                                                                        loggingDelegate => loggingDelegate.Invoke(
                                                                            now,
                                                                            this,
                                                                            webSocketConnection,
                                                                            frame,
                                                                            frame.EventTrackingId,
                                                                            frame.Payload,
                                                                            token2
                                                                        )
                                                                    );

                                                                    #region Send Pong

                                                                    var sentStatus = await SendWebSocketFrame(
                                                                                               webSocketConnection,
                                                                                               WebSocketFrame.Pong(frame.Payload),
                                                                                               frame.EventTrackingId
                                                                                           );

                                                                    if (sentStatus == SentStatus.Success)
                                                                    {
                                                                        sendErrors = 0;
                                                                    }
                                                                    else if (sentStatus == SentStatus.FatalError)
                                                                    {
                                                                        await webSocketConnection.Close(
                                                                                  WebSocketFrame.ClosingStatusCode.ProtocolError
                                                                              );
                                                                    }
                                                                    else
                                                                    {
                                                                        sendErrors++;
                                                                        DebugX.Log($"HTTP WebSocket connection '{webSocketConnection.RemoteSocket}' sending a CLOSE frame failed ({sendErrors})!");
                                                                    }

                                                                    #endregion

                                                                    break;

                                                                #endregion

                                                                #region Pong   message received

                                                                case WebSocketFrame.Opcodes.Pong:

                                                                    await LogEvent(
                                                                        OnPingMessageReceived,
                                                                        loggingDelegate => loggingDelegate.Invoke(
                                                                            now,
                                                                            this,
                                                                            webSocketConnection,
                                                                            frame,
                                                                            frame.EventTrackingId,
                                                                            frame.Payload,
                                                                            token2
                                                                        )
                                                                    );

                                                                    break;

                                                                #endregion

                                                                #region Close  message received

                                                                case WebSocketFrame.Opcodes.Close:

                                                                    await LogEvent(
                                                                        OnCloseMessageReceived,
                                                                        loggingDelegate => loggingDelegate.Invoke(
                                                                            now,
                                                                            this,
                                                                            webSocketConnection,
                                                                            frame,
                                                                            frame.EventTrackingId,
                                                                            frame.GetClosingStatusCode(),
                                                                            frame.GetClosingReason(),
                                                                            token2
                                                                        )
                                                                    );

                                                                    // The close handshake demands that we send a close frame back!
                                                                    await webSocketConnection.Close(
                                                                              WebSocketFrame.ClosingStatusCode.NormalClosure
                                                                          );

                                                                    webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                                    break;

                                                                #endregion

                                                            }

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

                                                        DebugX.Log($"{nameof(AWebSocketServer)}: Closing invalid TCP connection from: {webSocketConnection.RemoteSocket}!");

                                                        await webSocketConnection.Close(
                                                                  WebSocketFrame.ClosingStatusCode.ProtocolError
                                                              );

                                                    }

                                                    #endregion

                                                }

                                                await webSocketConnection.Close(
                                                          WebSocketFrame.ClosingStatusCode.ProtocolError
                                                      );

                                                webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                #region OnTCPConnectionClosed

                                                await LogEvent(
                                                          OnTCPConnectionClosed,
                                                          loggingDelegate => loggingDelegate.Invoke(
                                                              Timestamp.Now,
                                                              this,
                                                              webSocketConnection,
                                                              EventTracking_Id.New,
                                                              "closed!",
                                                              token2
                                                          )
                                                      );

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

                                        WebSocketServer:              this,
                                        TcpClient:                    newTCPConnection,
                                        TLSStream:                    sslStream,
                                        ClientCertificate:            sslStream?.RemoteCertificate is not null
                                                                          ? new X509Certificate2(sslStream.RemoteCertificate)
                                                                          : null,

                                        HTTPRequest:                  null,
                                        HTTPResponse:                 null,

                                        MaxTextMessageSizeIn:         MaxTextMessageSizeIn,
                                        MaxTextMessageSizeOut:        MaxTextMessageSizeOut,
                                        MaxTextFragmentLengthIn:      MaxTextFragmentLengthIn,
                                        MaxTextFragmentLengthOut:     MaxTextFragmentLengthOut,

                                        MaxBinaryMessageSizeIn:       MaxBinaryMessageSizeIn,
                                        MaxBinaryMessageSizeOut:      MaxBinaryMessageSizeOut,
                                        MaxBinaryFragmentLengthIn:    MaxBinaryFragmentLengthIn,
                                        MaxBinaryFragmentLengthOut:   MaxBinaryFragmentLengthOut,

                                        SlowNetworkSimulationDelay:   SlowNetworkSimulationDelay

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

                    }
                    catch (OperationCanceledException)
                    {
                        DebugX.Log($" Accepting new TCP connections on HTTP WebSocket server '{Description.FirstText()}' on {IPSocket} was canceled!");
                    }
                    catch (ObjectDisposedException)
                    {
                        DebugX.Log($" Accepting new TCP connections on HTTP WebSocket server '{Description.FirstText()}' on {IPSocket} was disposed!");
                    }
                    finally
                    {

                        #region Stop TCP listener

                        DebugX.Log($" Stopping HTTP WebSocket server '{Description.FirstText()}' on {IPSocket}");

                        tcpListener.Stop();

                        isRunning = false;

                        #endregion

                        //ToDo: Request all client connections to finish!

                        #region Send OnServerStopped event

                        await LogEvent(
                                  OnServerStopped,
                                  loggingDelegate => loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      this,
                                      EventTracking_Id.New,
                                      "HTTP WebSocket Server stopped!",
                                      token
                                  )
                              );

                        #endregion

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(e, $"{nameof(AWebSocketServer)}.{nameof(Start)}()");
                }

            });

            listenerThread.Start();

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the HTTP WebSocket listener thread.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public async Task Shutdown(String?  Message   = null,
                                   Boolean  Wait      = true)
        {

            cancellationTokenSource.Cancel();

            foreach (var webSocketConnection in WebSocketConnections)
                await webSocketConnection.Close(CancellationToken: cancellationTokenSource.Token);

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
        /// The default HTTP WebSocket text message processor.
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
                       "No text message handler found!!",
                       EventTrackingId,
                       CancellationToken
                   )
               );

        #endregion

        #region (virtual) ProcessBinaryMessage(RequestTimestamp, Connection, BinaryMessage, ...)

        /// <summary>
        /// The default HTTP WebSocket binary message processor.
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
                       "No binary message handler found!".ToUTF8Bytes(),
                       EventTrackingId,
                       CancellationToken
                   )
               );

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


        #region (private) LogEvent(Logger, LogHandler, ...)

        private async Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                               Func<TDelegate, Task>                              LogHandler,
                                               [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                               [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

        {
            if (Logger is not null)
            {
                try
                {

                    await Task.WhenAll(
                              Logger.GetInvocationList().
                                     OfType<TDelegate>().
                                     Select(LogHandler)
                          );

                }
                catch (Exception e)
                {
                    await HandleErrors($"WebSocketClient: {Command}.{EventName}", e);
                }
            }
        }

        #endregion

        #region (private) HandleErrors(Caller, ExceptionOccured)

        private Task HandleErrors(String     Caller,
                                  Exception  ExceptionOccured)
        {

            DebugX.LogException(ExceptionOccured, Caller);

            return Task.CompletedTask;

        }

        #endregion


    }

}
