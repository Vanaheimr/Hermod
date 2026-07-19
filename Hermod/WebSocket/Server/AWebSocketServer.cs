/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Text.Unicode;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
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
    /// An HTTP WebSocket server.
    /// </summary>
    public abstract class AWebSocketServer : ATCPServer
    {

        #region Data

        private readonly  HashSet<String>                                                           secWebSocketProtocols         = [];

        private readonly  ConcurrentDictionary<IPSocket, WeakReference<WebSocketServerConnection>>  webSocketConnections          = [];

        public readonly   TimeSpan                                                                  DefaultWebSocketPingEvery     = TimeSpan.FromSeconds(30);

        private const     String                                                                    LogfileName                   = "HTTPWebSocketServer.log";

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

        public List<X509Certificate2>                                          TrustedClientCertificates       { get; } = [];
        public List<X509Certificate2>                                          TrustedCertificatAuthorities    { get; } = [];

        /// <summary>
        /// The IP address to listen on.
        /// </summary>
        public new IIPAddress                                                  IPAddress
            => base.IPAddress;

        /// <summary>
        /// The TCP port to listen on.
        /// </summary>
        public IPPort                                                          IPPort
            => IPSocket.Port;

        /// <summary>
        /// The IP socket to listen on.
        /// </summary>
        public new IPSocket                                                    IPSocket
            => base.IPSocket;

        /// <summary>
        /// Whether the web socket TCP listener is currently running.
        /// </summary>
        public new Boolean                                                     IsRunning
            => base.IsRunning;

        /// <summary>
        /// The supported secondary web socket protocols.
        /// </summary>
        public IEnumerable<String>                                             SecWebSocketProtocols
            => secWebSocketProtocols;

        /// <summary>
        /// Whether to offer the "permessage-deflate" (RFC 7692) message compression
        /// extension to connecting clients. Disabled by default.
        /// </summary>
        public Boolean                                                         EnablePerMessageDeflate       { get; set; }

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                                                         DisableWebSocketPings         { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                                                        WebSocketPingEvery            { get; set; }

        /// <summary>
        /// The number of consecutive web socket ping intervals without any received
        /// frame (data, pong or otherwise) after which the connection is considered
        /// dead and closed locally (zombie/half-open connection detection).
        /// Zero disables the check. Default: 3 (≈ 3 × WebSocketPingEvery of silence).
        /// </summary>
        public UInt32                                                          MaxOutstandingPings           { get; set; } = 3;


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


        public new UInt32                                                      MaxClientConnections
            => base.MaxClientConnections;

        /// <summary>
        /// The attached debug logger.
        /// </summary>
        public ILogger                                                         Logger                        { get; }

        /// <summary>
        /// The attached logger factory.
        /// </summary>
        public ILoggerFactory                                                  LoggerFactory                 { get; }

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
        public new event OnNewTCPConnectionDelegate?                        OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a new TLS connection was accepted.
        /// </summary>
        public event OnNewTLSConnectionDelegate?                        OnNewTLSConnection;

        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public new event OnTCPConnectionClosedDelegate?                     OnTCPConnectionClosed;


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
        /// An event sent whenever a response to a HTTP request was sent.
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

        ///// <summary>
        ///// An event sent whenever a text message was received.
        ///// </summary>
        //public event OnWebSocketServerTextMessageReceivedDelegate?      OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        public event OnWebSocketServerBinaryMessageSentDelegate?        OnBinaryMessageSent;

        ///// <summary>
        ///// An event sent whenever a binary message was received.
        ///// </summary>
        //public event OnWebSocketServerBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;


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

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: ATCPServer default.</param>
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
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionTimeout"></param>
        /// <param name="MaxClientConnections"></param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public AWebSocketServer(IIPAddress?                                               IPAddress                    = null,
                                IPPort?                                                   TCPPort                      = null,
                                String?                                                   HTTPServerName               = null,
                                I18NString?                                               Description                  = null,

                                Boolean?                                                  RequireAuthentication        = true,
                                IEnumerable<String>?                                      SecWebSocketProtocols        = null,
                                SubprotocolSelectorDelegate?                              SubprotocolSelector          = null,
                                Boolean                                                   DisableWebSocketPings        = false,
                                TimeSpan?                                                 WebSocketPingEvery           = null,
                                TimeSpan?                                                 SlowNetworkSimulationDelay   = null,

                                UInt32?                                                   BufferSize                   = null,
                                TimeSpan?                                                 ReceiveTimeout               = null,
                                TimeSpan?                                                 SendTimeout                  = null,
                                TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                                ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                                RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                                SslProtocols?                                             AllowedTLSProtocols          = null,
                                Boolean?                                                  ClientCertificateRequired    = null,
                                Boolean?                                                  CheckCertificateRevocation   = null,

                                ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                                UInt32?                                                   MaxClientConnections         = null,
                                IDNSClient?                                               DNSClient                    = null,

                                Boolean?                                                  DisableMaintenanceTasks      = false,
                                TimeSpan?                                                 MaintenanceInitialDelay      = null,
                                TimeSpan?                                                 MaintenanceEvery             = null,

                                Boolean?                                                  DisableWardenTasks           = false,
                                TimeSpan?                                                 WardenInitialDelay           = null,
                                TimeSpan?                                                 WardenCheckEvery             = null,

                                ILoggerFactory?                                           LoggerFactory                = null,
                                Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   TCPPort,
                   Description,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ConnectionIdBuilder,
                   MaxClientConnections,
                   DNSClient,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   LoggerFactory,
                   AutoStart: false)

        {

            this.HTTPServiceName             = HTTPServiceName            ?? "GraphDefined HTTP WebSocket Service v2.0";

            this.RequireAuthentication       = RequireAuthentication      ?? true;
            this.secWebSocketProtocols       = SecWebSocketProtocols is not null
                                                   ? [.. SecWebSocketProtocols]
                                                   : [];
            this.SubprotocolSelector         = SubprotocolSelector;

            this.DisableWebSocketPings       = DisableWebSocketPings;
            this.WebSocketPingEvery          = WebSocketPingEvery ?? DefaultWebSocketPingEvery;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

            this.LoggerFactory               = LoggerFactory              ?? NullLoggerFactory.Instance;
            this.Logger                      = Logger                     ?? this.LoggerFactory.CreateLogger(GetType().FullName ?? nameof(AWebSocketServer));

            base.OnTCPServerStarted += async (sender, timestamp, eventTrackingId, message) => {
                await LogEvent(
                          OnServerStarted,
                          loggingDelegate => loggingDelegate.Invoke(
                              timestamp,
                              this,
                              eventTrackingId,
                              CancellationToken.None
                          )
                      );
            };

            base.OnTCPServerStopped += async (sender, timestamp, eventTrackingId, message) => {
                await LogEvent(
                          OnServerStopped,
                          loggingDelegate => loggingDelegate.Invoke(
                              timestamp,
                              this,
                              eventTrackingId,
                              message,
                              CancellationToken.None
                          )
                      );
            };

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

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
                   Connection.PerMessageDeflate is not null
                       ? WebSocketFrame.TextRaw(
                             Connection.PerMessageDeflate.Compress(TextMessage.ToUTF8Bytes()),
                             Rsv1: WebSocketFrame.Rsv.On
                         )
                       : WebSocketFrame.Text(TextMessage),
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
                   Connection.PerMessageDeflate is not null
                       ? WebSocketFrame.Binary(
                             Connection.PerMessageDeflate.Compress(BinaryMessage),
                             Rsv1: WebSocketFrame.Rsv.On
                         )
                       : WebSocketFrame.Binary(BinaryMessage),
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
                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnTextMessageSent));
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
                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnBinaryMessageSent));
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
                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnPingMessageSent));
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
                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnPongMessageSent));
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
                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnCloseMessageSent));
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
        /// <param name="Connection">An HTTP WebSocket connection.</param>
        public Boolean RemoveConnection(WebSocketServerConnection Connection)
        {

            Logger.LogDebug("Removing HTTP WebSocket connection with {RemoteSocket}.", Connection.RemoteSocket);

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


        #region HandleConnection(Connection, Token)

        /// <summary>
        /// Handle a new TCP/TLS connection accepted by the shared ATCPServer infrastructure.
        /// </summary>
        protected override async Task HandleConnection(TCPConnection      Connection,
                                                       CancellationToken  token)
        {

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
                                                var lastActivityTimestamp       = Timestamp.Now;
                                                var sendErrors                  = 0;

                                                WebSocketFrame.Opcodes? fragmentOpcode      = null;
                                                var                     fragmentPayload     = new MemoryStream();
                                                var                     fragmentCompressed  = false;
                                                var                     utf8Validator       = new IncrementalUtf8Validator();

                                                #endregion

                                                #region Config web socket connection

                                                webSocketConnection.ReadTimeout  = TimeSpan.FromSeconds(20);
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
                                                        Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnNewTCPConnection));
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
                                                            Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnNewTLSConnection));
                                                        }
                                                    }

                                                }

                                                #endregion

                                                while (!token2.IsCancellationRequested &&
                                                       !webSocketConnection.IsClosed &&
                                                        sendErrors < 3)
                                                {

                                                    #region Main loop: asynchronously waiting for data... and sending a regular web socket ping

                                                    if (bytes.Length == 0)
                                                    {

                                                        var readBuffer  = new Byte[64 * 1024];
                                                        var read        = 0;
                                                        var readTimeout = false;

                                                        // Instead of busy-polling DataAvailable, asynchronously wait for
                                                        // data with a timeout equal to the ping cadence. On timeout a ping
                                                        // is sent; otherwise the received bytes are processed below.
                                                        using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(token2))
                                                        {

                                                            if (!DisableWebSocketPings)
                                                            {
                                                                var untilNextPing = (lastWebSocketPingTimestamp + WebSocketPingEvery) - Timestamp.Now;
                                                                readCts.CancelAfter(untilNextPing > TimeSpan.Zero ? untilNextPing : TimeSpan.FromMilliseconds(1));
                                                            }

                                                            try
                                                            {
                                                                read = await webSocketConnection.ReadAsync(readBuffer, readCts.Token);
                                                            }
                                                            catch (OperationCanceledException) when (!token2.IsCancellationRequested)
                                                            {
                                                                readTimeout = true;
                                                            }
                                                            catch (Exception e)
                                                            {
                                                                Logger.LogDebug(e, "Read error on WebSocket connection {RemoteSocket}.", webSocketConnection.RemoteSocket);
                                                                break;
                                                            }

                                                        }

                                                        #region Send a regular web socket "ping" on read timeout

                                                        if (readTimeout)
                                                        {

                                                            // Zombie/half-open detection: if no frame (data or pong) has been
                                                            // received within MaxOutstandingPings ping intervals, the peer is
                                                            // gone even though Socket.Poll cannot see the half-open TCP connection.
                                                            // Tear the connection down locally (no close frame to a dead peer;
                                                            // 1006 is reserved and must not be sent on the wire, RFC 6455 Section 7.4.1).
                                                            if (!DisableWebSocketPings &&
                                                                MaxOutstandingPings > 0 &&
                                                                Timestamp.Now - lastActivityTimestamp > TimeSpan.FromTicks(WebSocketPingEvery.Ticks * MaxOutstandingPings))
                                                            {
                                                                Logger.LogWarning(
                                                                    "WebSocket connection to {RemoteSocket} timed out (no data or pong for {Silence}); closing dead connection.",
                                                                    webSocketConnection.RemoteSocket,
                                                                    Timestamp.Now - lastActivityTimestamp
                                                                );
                                                                break;
                                                            }

                                                            if (!DisableWebSocketPings)
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
                                                                    Logger.LogWarning(
                                                                        "WebSocket connection to {RemoteSocket}: Ping failed ({SendErrors}).",
                                                                        webSocketConnection.RemoteSocket,
                                                                        sendErrors
                                                                    );
                                                                }

                                                                lastWebSocketPingTimestamp = Timestamp.Now;

                                                            }

                                                            continue;

                                                        }

                                                        #endregion

                                                        // A read of zero bytes means the remote endpoint closed the TCP connection!
                                                        if (read == 0)
                                                            break;

                                                        // Any received bytes prove the peer is alive (liveness for zombie detection).
                                                        lastActivityTimestamp = Timestamp.Now;

                                                        bytes = new Byte[bytesLeftOver.Length + read];

                                                        if (bytesLeftOver.Length > 0)
                                                            Array.Copy(bytesLeftOver, 0, bytes, 0, bytesLeftOver.Length);

                                                        Array.Copy(readBuffer, 0, bytes, bytesLeftOver.Length, read);

                                                        httpMethod = IsStillHTTP && bytes.Length >= 4
                                                                         ? Encoding.UTF8.GetString(bytes, 0, 4)
                                                                         : String.Empty;

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

                                                            #region Validate the WebSocket upgrade handshake (RFC 6455 Section 4.2.1)

                                                            if (httpResponse is null)
                                                            {

                                                                var connectionHeader  = httpRequest.GetHeaderField("Connection") ?? "";
                                                                var upgradeHeader     = httpRequest.GetHeaderField("Upgrade")    ?? "";
                                                                var secWebSocketKey   = httpRequest.SecWebSocketKey;
                                                                var secWSVersion      = httpRequest.GetHeaderField("Sec-WebSocket-Version") ?? "";

                                                                String? handshakeError = null;

                                                                if (!upgradeHeader.Split(',').Any(v => v.Trim().Equals("websocket", StringComparison.OrdinalIgnoreCase)))
                                                                    handshakeError = "The 'Upgrade: websocket' header is missing!";

                                                                else if (!connectionHeader.Split(',').Any(v => v.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)))
                                                                    handshakeError = "The 'Connection: Upgrade' header is missing!";

                                                                // The Sec-WebSocket-Key must be a base64-encoded 16 byte value.
                                                                else if (secWebSocketKey.IsNullOrEmpty() ||
                                                                         !TryDecodeBase64(secWebSocketKey, out var keyBytes) ||
                                                                         keyBytes.Length != 16)
                                                                    handshakeError = "The 'Sec-WebSocket-Key' header is missing or invalid!";

                                                                if (handshakeError is not null)
                                                                {
                                                                    httpResponse = new HTTPResponse.Builder(httpRequest) {
                                                                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                                                       Server          = HTTPServiceName,
                                                                                       Connection      = ConnectionType.Close,
                                                                                       ContentType     = HTTPContentType.Text.PLAIN,
                                                                                       Content         = handshakeError.ToUTF8Bytes()
                                                                                   }.AsImmutable;
                                                                }

                                                                // Only WebSocket version 13 is supported (RFC 6455 Section 4.4).
                                                                else if (secWSVersion.Trim() != "13")
                                                                {
                                                                    httpResponse = new HTTPResponse.Builder(httpRequest) {
                                                                                       HTTPStatusCode       = HTTPStatusCode.UpgradeRequired,
                                                                                       Server               = HTTPServiceName,
                                                                                       Connection           = ConnectionType.Close,
                                                                                       SecWebSocketVersion  = "13",
                                                                                       ContentType          = HTTPContentType.Text.PLAIN,
                                                                                       Content              = $"Unsupported Sec-WebSocket-Version '{secWSVersion}', only version 13 is supported!".ToUTF8Bytes()
                                                                                   }.AsImmutable;
                                                                }

                                                            }

                                                            #endregion

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

                                                                #region Negotiate the 'permessage-deflate' extension (RFC 7692)

                                                                String? secWebSocketExtensions = null;

                                                                if (EnablePerMessageDeflate &&
                                                                    WebSocketPerMessageDeflate.TryNegotiateAsServer(
                                                                        httpRequest.GetHeaderField("Sec-WebSocket-Extensions"),
                                                                        out var perMessageDeflate,
                                                                        out var deflateResponseHeader
                                                                    ))
                                                                {
                                                                    webSocketConnection.PerMessageDeflate = perMessageDeflate;
                                                                    secWebSocketExtensions                = deflateResponseHeader;
                                                                }

                                                                #endregion


                                                                // HTTP/1.1 101 Switching Protocols
                                                                // Connection:              Upgrade
                                                                // Upgrade:                 websocket
                                                                // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                                                                // Sec-WebSocket-Protocol:  ocpp2.1
                                                                // Sec-WebSocket-Version:   13
                                                                var httpResponseBuilder = new HTTPResponse.Builder(httpRequest) {
                                                                                           HTTPStatusCode        = HTTPStatusCode.SwitchingProtocols,
                                                                                           Server                = HTTPServiceName,
                                                                                           Connection            = ConnectionType.Upgrade,
                                                                                           Upgrade               = "websocket",
                                                                                           SecWebSocketAccept    = swkaSHA1BASE64,
                                                                                           SecWebSocketProtocol  = selectedSubprotocol,
                                                                                           SecWebSocketVersion   = "13"
                                                                                       };

                                                                if (secWebSocketExtensions is not null)
                                                                    httpResponseBuilder.SetHeaderField("Sec-WebSocket-Extensions", secWebSocketExtensions);

                                                                httpResponse         = httpResponseBuilder.AsImmutable;

                                                            }

                                                            #endregion

                                                        }

                                                        httpResponse ??= new HTTPResponse.Builder(

                                                                             Timestamp.Now,
                                                                             EventTracking_Id.New,
                                                                             TimeSpan.Zero,

                                                                             new HTTPSource(),
                                                                             webSocketConnection.LocalSocket,
                                                                             webSocketConnection.RemoteSocket,
                                                                             ConnectionType.Close,

                                                                             HTTPStatusCode.BadRequest

                                                                         ) {
                                                                             Server = HTTPServiceName
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

                                                        var parseResult = WebSocketFrame.Parse(bytes.AsSpan(),
                                                                                               out var frame,
                                                                                               out var frameLength,
                                                                                               out var errorResponse,
                                                                                               out var suggestedCloseCode,
                                                                                               Logger:    Logger,
                                                                                               AllowRsv1: webSocketConnection.PerMessageDeflate is not null);

                                                        if (parseResult == WebSocketFrame.ParseResult.Success &&
                                                            frame is not null)
                                                        {

                                                            // RFC 6455 Section 5.1: Client-to-server frames MUST be masked
                                                            if (!frame.IsMasked)
                                                            {
                                                                Logger.LogWarning(
                                                                    "Received unmasked frame from {RemoteSocket}, closing connection (RFC 6455 violation).",
                                                                    webSocketConnection.RemoteSocket
                                                                );
                                                                await webSocketConnection.Close(WebSocketFrame.ClosingStatusCode.ProtocolError);
                                                                break;
                                                            }

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

                                                            WebSocketFrame.ClosingStatusCode? failConnection = null;

                                                            switch (frame.Opcode)
                                                            {

                                                                #region Text   message received

                                                                case WebSocketFrame.Opcodes.Text:

                                                                    // RFC 6455 Section 5.4: A new data frame within
                                                                    // a fragmented message must fail the connection!
                                                                    if (fragmentOpcode is not null)
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.ProtocolError;
                                                                        break;
                                                                    }

                                                                    // The accumulated message size must not exceed the configured limit.
                                                                    if (MaxTextMessageSizeIn.HasValue &&
                                                                        (UInt64) frame.Payload.Length > MaxTextMessageSizeIn.Value)
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.MessageTooBig;
                                                                        break;
                                                                    }

                                                                    if (frame.IsFinal)
                                                                    {

                                                                        var textPayload = frame.Payload;

                                                                        // permessage-deflate (RFC 7692): decompress a compressed message.
                                                                        if (frame.IsCompressed && webSocketConnection.PerMessageDeflate is not null)
                                                                        {
                                                                            if (!webSocketConnection.PerMessageDeflate.TryDecompress(textPayload, out textPayload, out _, out var textExceededSizeLimit))
                                                                            {
                                                                                failConnection = textExceededSizeLimit
                                                                                                     ? WebSocketFrame.ClosingStatusCode.MessageTooBig
                                                                                                     : WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                                break;
                                                                            }
                                                                        }

                                                                        // RFC 6455 Section 8.1
                                                                        if (!Utf8.IsValid(textPayload))
                                                                        {
                                                                            failConnection = WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                            break;
                                                                        }

                                                                        try
                                                                        {

                                                                            await ProcessTextMessage(
                                                                                      now,
                                                                                      this,
                                                                                      webSocketConnection,
                                                                                      frame.EventTrackingId,
                                                                                      frame,
                                                                                      textPayload.ToUTF8String(),
                                                                                      token2
                                                                                  );

                                                                        }
                                                                        catch (Exception e)
                                                                        {
                                                                            Logger.LogError(e, "Exception while processing text WebSocket message.");
                                                                        }

                                                                    }
                                                                    else
                                                                    {

                                                                        fragmentOpcode      = WebSocketFrame.Opcodes.Text;
                                                                        fragmentCompressed  = frame.IsCompressed && webSocketConnection.PerMessageDeflate is not null;
                                                                        fragmentPayload.SetLength(0);
                                                                        fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);

                                                                        // A compressed message can only be UTF-8 validated after decompression.
                                                                        if (!fragmentCompressed)
                                                                        {
                                                                            utf8Validator.Reset();
                                                                            if (!utf8Validator.Append(frame.Payload, false))
                                                                                failConnection = WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                        }

                                                                    }

                                                                    break;

                                                                #endregion

                                                                #region Binary message received

                                                                case WebSocketFrame.Opcodes.Binary:

                                                                    // RFC 6455 Section 5.4: A new data frame within
                                                                    // a fragmented message must fail the connection!
                                                                    if (fragmentOpcode is not null)
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.ProtocolError;
                                                                        break;
                                                                    }

                                                                    // The accumulated message size must not exceed the configured limit.
                                                                    if (MaxBinaryMessageSizeIn.HasValue &&
                                                                        (UInt64) frame.Payload.Length > MaxBinaryMessageSizeIn.Value)
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.MessageTooBig;
                                                                        break;
                                                                    }

                                                                    if (frame.IsFinal)
                                                                    {

                                                                        var binaryPayload = frame.Payload;

                                                                        // permessage-deflate (RFC 7692): decompress a compressed message.
                                                                        if (frame.IsCompressed && webSocketConnection.PerMessageDeflate is not null)
                                                                        {
                                                                            if (!webSocketConnection.PerMessageDeflate.TryDecompress(binaryPayload, out binaryPayload, out _, out var binaryExceededSizeLimit))
                                                                            {
                                                                                failConnection = binaryExceededSizeLimit
                                                                                                     ? WebSocketFrame.ClosingStatusCode.MessageTooBig
                                                                                                     : WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                                break;
                                                                            }
                                                                        }

                                                                        try
                                                                        {

                                                                            await ProcessBinaryMessage(
                                                                                      now,
                                                                                      this,
                                                                                      webSocketConnection,
                                                                                      frame.EventTrackingId,
                                                                                      frame,
                                                                                      binaryPayload,
                                                                                      token2
                                                                                  );

                                                                        }
                                                                        catch (Exception e)
                                                                        {
                                                                            Logger.LogError(e, "Exception while processing binary WebSocket message.");
                                                                        }

                                                                    }
                                                                    else
                                                                    {
                                                                        fragmentOpcode      = WebSocketFrame.Opcodes.Binary;
                                                                        fragmentCompressed  = frame.IsCompressed && webSocketConnection.PerMessageDeflate is not null;
                                                                        fragmentPayload.SetLength(0);
                                                                        fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);
                                                                    }

                                                                    break;

                                                                #endregion

                                                                #region Continuation message received

                                                                case WebSocketFrame.Opcodes.Continuation:

                                                                    if (fragmentOpcode is null)
                                                                    {
                                                                        Logger.LogWarning(
                                                                            "Received Continuation frame from {RemoteSocket} without preceding Text/Binary frame.",
                                                                            webSocketConnection.RemoteSocket
                                                                        );
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.ProtocolError;
                                                                        break;
                                                                    }

                                                                    // The accumulated message size must not exceed the configured
                                                                    // limit. This is checked BEFORE buffering, to bound the memory
                                                                    // used by a maliciously fragmented message (RFC 6455 Section 7.4.1, 1009)!
                                                                    var maxFragmentedSize = fragmentOpcode == WebSocketFrame.Opcodes.Text
                                                                                                ? MaxTextMessageSizeIn
                                                                                                : MaxBinaryMessageSizeIn;

                                                                    if (maxFragmentedSize.HasValue &&
                                                                        (UInt64) fragmentPayload.Length + (UInt64) frame.Payload.Length > maxFragmentedSize.Value)
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.MessageTooBig;
                                                                        break;
                                                                    }

                                                                    fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);

                                                                    // RFC 6455 Section 8.1: Fail fast on invalid UTF-8! (Only for
                                                                    // uncompressed messages; a compressed message can only be
                                                                    // validated after decompression at the final frame.)
                                                                    if (fragmentOpcode == WebSocketFrame.Opcodes.Text &&
                                                                        !fragmentCompressed &&
                                                                        !utf8Validator.Append(frame.Payload, frame.IsFinal))
                                                                    {
                                                                        failConnection = WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                        break;
                                                                    }

                                                                    if (frame.IsFinal)
                                                                    {

                                                                        var completePayload = fragmentPayload.ToArray();

                                                                        // permessage-deflate (RFC 7692): decompress the reassembled message.
                                                                        if (fragmentCompressed && webSocketConnection.PerMessageDeflate is not null)
                                                                        {
                                                                            if (!webSocketConnection.PerMessageDeflate.TryDecompress(completePayload, out completePayload, out _, out var fragmentExceededSizeLimit))
                                                                            {
                                                                                failConnection = fragmentExceededSizeLimit
                                                                                                     ? WebSocketFrame.ClosingStatusCode.MessageTooBig
                                                                                                     : WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                                break;
                                                                            }
                                                                        }

                                                                        // RFC 6455 Section 8.1: Validate the decompressed text message.
                                                                        if (fragmentOpcode == WebSocketFrame.Opcodes.Text &&
                                                                            fragmentCompressed &&
                                                                            !Utf8.IsValid(completePayload))
                                                                        {
                                                                            failConnection = WebSocketFrame.ClosingStatusCode.InvalidPayloadData;
                                                                            break;
                                                                        }

                                                                        try
                                                                        {

                                                                            if (fragmentOpcode == WebSocketFrame.Opcodes.Text)
                                                                            {
                                                                                await ProcessTextMessage(
                                                                                          now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          frame.EventTrackingId,
                                                                                          frame,
                                                                                          completePayload.ToUTF8String(),
                                                                                          token2
                                                                                      );
                                                                            }
                                                                            else if (fragmentOpcode == WebSocketFrame.Opcodes.Binary)
                                                                            {
                                                                                await ProcessBinaryMessage(
                                                                                          now,
                                                                                          this,
                                                                                          webSocketConnection,
                                                                                          frame.EventTrackingId,
                                                                                          frame,
                                                                                          completePayload,
                                                                                          token2
                                                                                      );
                                                                            }

                                                                        }
                                                                        catch (Exception e)
                                                                        {
                                                                            Logger.LogError(e, "Exception while processing continuation WebSocket frame.");
                                                                        }

                                                                        fragmentOpcode      = null;
                                                                        fragmentCompressed  = false;
                                                                        fragmentPayload.SetLength(0);

                                                                    }

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
                                                                        Logger.LogWarning(
                                                                            "HTTP WebSocket connection {RemoteSocket} sending a Pong frame failed ({SendErrors}).",
                                                                            webSocketConnection.RemoteSocket,
                                                                            sendErrors
                                                                        );
                                                                    }

                                                                    #endregion

                                                                    break;

                                                                #endregion

                                                                #region Pong   message received

                                                                case WebSocketFrame.Opcodes.Pong:

                                                                    await LogEvent(
                                                                        OnPongMessageReceived,
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

                                                                    // The close handshake demands that we send a close frame back,
                                                                    // echoing the received close status code (RFC 6455 Section 5.5.1)!
                                                                    var receivedCloseCode = frame.GetClosingStatusCode();

                                                                    await webSocketConnection.Close(
                                                                              receivedCloseCode == WebSocketFrame.ClosingStatusCode.NoStatusReceived
                                                                                  ? WebSocketFrame.ClosingStatusCode.NormalClosure
                                                                                  : receivedCloseCode
                                                                          );

                                                                    webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                                    break;

                                                                #endregion

                                                            }

                                                            #region Fail the connection on protocol violations

                                                            if (failConnection.HasValue)
                                                            {

                                                                Logger.LogWarning(
                                                                    "Failing WebSocket connection to {RemoteSocket} with close code {CloseCode}.",
                                                                    webSocketConnection.RemoteSocket,
                                                                    failConnection.Value
                                                                );

                                                                await webSocketConnection.Close(failConnection.Value);

                                                                webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                                break;

                                                            }

                                                            #endregion

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
                                                        else if (parseResult == WebSocketFrame.ParseResult.ProtocolViolation)
                                                        {

                                                            Logger.LogWarning(
                                                                "WebSocket protocol violation from {RemoteSocket}: {ErrorResponse}",
                                                                webSocketConnection.RemoteSocket,
                                                                errorResponse
                                                            );

                                                            await webSocketConnection.Close(suggestedCloseCode);

                                                            webSocketConnections.TryRemove(webSocketConnection.RemoteSocket, out _);

                                                            break;

                                                        }
                                                        else
                                                        {
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

                                                        Logger.LogWarning(
                                                            "Closing invalid TCP connection from {RemoteSocket}.",
                                                            webSocketConnection.RemoteSocket
                                                        );

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
                                                Logger.LogWarning("The given WebSocket connection is invalid.");

                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogError(e, "Exception in HTTP WebSocket server connection loop.");
                                        }

                                    },
                                    new WebSocketServerConnection(

                                        WebSocketServer:              this,
                                        TcpClient:                    Connection.TCPClient,
                                        TLSStream:                    Connection.SSLStream,
                                        ClientCertificate:            Connection.ClientCertificate,

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

            await x.Unwrap().ConfigureAwait(false);

        }

        #endregion

        #region ValidateConnection(Timestamp, Server, Connection, EventTrackingId, CancellationToken)

        /// <summary>
        /// Validate a new TCP connection via the WebSocket server's legacy validation event.
        /// </summary>
        public override async Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                                ITCPServer         Server,
                                                                                TCPConnection      Connection,
                                                                                EventTracking_Id   EventTrackingId,
                                                                                CancellationToken  CancellationToken)
        {

            var validatedTCPConnections = Array.Empty<ConnectionFilterResponse>();

            var onValidateTCPConnection = OnValidateTCPConnection;
            if (onValidateTCPConnection is not null)
            {
                try
                {

                    validatedTCPConnections = await Task.WhenAll(
                                                        onValidateTCPConnection.GetInvocationList().
                                                            OfType<OnValidateTCPConnectionDelegate>().
                                                            Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                           Timestamp,
                                                                                           this,
                                                                                           Connection.TCPClient,
                                                                                           EventTrackingId,
                                                                                           CancellationToken
                                                                                       )).
                                                            ToArray()
                                                    );

                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnValidateTCPConnection));
                }
            }

            return validatedTCPConnections.Length > 0 &&
                   validatedTCPConnections.First() == ConnectionFilterResponse.Rejected()
                       ? validatedTCPConnections.First()
                       : ConnectionFilterResponse.Accepted();

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Shutdown the HTTP WebSocket listener.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        public async Task Shutdown(String?  Message   = null,
                                   Boolean  Wait      = true)
        {

            foreach (var webSocketConnection in WebSocketConnections)
                await webSocketConnection.Close();

            await Stop();

        }

        #endregion


        #region (virtual) ProcessTextMessage   (RequestTimestamp, Connection, TextMessage,   ...)

        /// <summary>
        /// The default HTTP WebSocket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public virtual Task ProcessTextMessage(DateTimeOffset             Timestamp,
                                               AWebSocketServer           Server,
                                               WebSocketServerConnection  Connection,
                                               EventTracking_Id           EventTrackingId,
                                               WebSocketFrame             TextFrame,
                                               String                     TextMessage,
                                               CancellationToken          CancellationToken)

            => Task.CompletedTask;

            //=> Task.FromResult(
            //       new WebSocketTextMessageResponse(
            //           RequestTimestamp,
            //           TextMessage,
            //           Timestamp.Now,
            //           "No text message handler found!!",
            //           EventTrackingId,
            //           CancellationToken
            //       )
            //   );

        #endregion

        #region (virtual) ProcessBinaryMessage (RequestTimestamp, Connection, BinaryMessage, ...)

        /// <summary>
        /// The default HTTP WebSocket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public virtual Task ProcessBinaryMessage(DateTimeOffset             Timestamp,
                                                 AWebSocketServer           Server,
                                                 WebSocketServerConnection  Connection,
                                                 EventTracking_Id           EventTrackingId,
                                                 WebSocketFrame             BinaryFrame,
                                                 Byte[]                     BinaryMessage,
                                                 CancellationToken          CancellationToken)

            => Task.CompletedTask;

            //=> Task.FromResult(
            //       new WebSocketBinaryMessageResponse(
            //           RequestTimestamp,
            //           BinaryMessage,
            //           Timestamp.Now,
            //           "No binary message handler found!".ToUTF8Bytes(),
            //           EventTrackingId,
            //           CancellationToken
            //       )
            //   );

        #endregion


        #region (protected) SendOnWebSocketFrameSent(Timestamp, Connection, EventTrackingId, Frame,         CancellationToken)

        /// <summary>
        /// Send an OnWebSocketFrameSent event
        /// </summary>
        protected async Task SendOnWebSocketFrameSent(DateTimeOffset             Timestamp,
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
                Logger.LogError(e, "Exception while invoking {EventName}.", nameof(OnWebSocketFrameSent));
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

        #region (private static) TryDecodeBase64(Text, out Bytes)

        /// <summary>
        /// Try to decode the given base64 text into a byte array.
        /// </summary>
        private static Boolean TryDecodeBase64(String Text, out Byte[] Bytes)
        {
            try
            {
                Bytes = Convert.FromBase64String(Text.Trim());
                return true;
            }
            catch
            {
                Bytes = [];
                return false;
            }
        }

        #endregion

        #region (private) HandleErrors(Caller, ExceptionOccurred)

        private Task HandleErrors(String     Caller,
                                  Exception  ExceptionOccurred)
        {

            Logger.LogError(ExceptionOccurred, "{Caller}", Caller);

            return Task.CompletedTask;

        }

        #endregion


    }

}
