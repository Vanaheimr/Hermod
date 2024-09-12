﻿/*
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

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    #region Delegates


    public delegate Task<WebSocketTextMessageResponse>    OnWebSocketServerTextMessageDelegate  (DateTime                     Timestamp,
                                                                                                 AWebSocketServer             Server,
                                                                                                 WebSocketServerConnection    Connection,
                                                                                                 EventTracking_Id             EventTrackingId,
                                                                                                 DateTime                     RequestTimestamp,
                                                                                                 String                       TextMessage,
                                                                                                 CancellationToken            CancellationToken);

    public delegate Task<WebSocketBinaryMessageResponse>  OnWebSocketServerBinaryMessageDelegate(DateTime                     Timestamp,
                                                                                                 AWebSocketServer             Server,
                                                                                                 WebSocketServerConnection    Connection,
                                                                                                 EventTracking_Id             EventTrackingId,
                                                                                                 DateTime                     RequestTimestamp,
                                                                                                 Byte[]                       BinaryMessage,
                                                                                                 CancellationToken            CancellationToken);

    #endregion


    /// <summary>
    /// The common interface for all HTTP WebSocket servers.
    /// </summary>
    public interface IWebSocketServer
    {

        #region Properties

        /// <summary>
        /// The optional description of this HTTP WebSocket server.
        /// </summary>
        I18NString                                    Description                     { get; set; }
        Boolean                                       DisableWebSocketPings           { get; set; }
        DNSClient?                                    DNSClient                       { get; }
        String                                        HTTPServiceName                 { get; }
        IIPAddress                                    IPAddress                       { get; }
        IPPort                                        IPPort                          { get; }
        IPSocket                                      IPSocket                        { get; }
        Boolean                                       IsRunning                       { get; }
        HashSet<String>                               SecWebSocketProtocols           { get; }
        Boolean                                       ServerThreadIsBackground        { get; }
        ServerThreadNameCreatorDelegate               ServerThreadNameCreator         { get; }
        ServerThreadPriorityDelegate                  ServerThreadPrioritySetter      { get; }
        TimeSpan?                                     SlowNetworkSimulationDelay      { get; set; }
        IEnumerable<WebSocketServerConnection>        WebSocketConnections            { get; }
        TimeSpan                                      WebSocketPingEvery              { get; set; }

        ConcurrentDictionary<String, SecurePassword>  ClientLogins                    { get; }
        Boolean                                       RequireAuthentication           { get; }

        List<X509Certificate2>                        TrustedClientCertificates       { get; }
        List<X509Certificate2>                        TrustedCertificatAuthorities    { get; }

        UInt64?                                       MaxTextMessageSize              { get; }

        UInt64?                                       MaxBinaryMessageSize            { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever the HTTP WebSocket server started.
        /// </summary>
        event OnServerStartedDelegate?                           OnServerStarted;


        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        event OnValidateTCPConnectionDelegate?                   OnValidateTCPConnection;

        /// <summary>
        /// An event sent whenever a new TCP connection was accepted.
        /// </summary>
        event OnNewTCPConnectionDelegate?                        OnNewTCPConnection;

        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        event OnTCPConnectionClosedDelegate?                     OnTCPConnectionClosed;


        /// <summary>
        /// An event sent whenever a HTTP request was received.
        /// </summary>
        event HTTPRequestLogDelegate?                            OnHTTPRequest;

        /// <summary>
        /// An event sent whenever the HTTP headers of a new web socket connection
        /// need to be validated or filtered by an upper layer application logic.
        /// </summary>
        event OnValidateWebSocketConnectionDelegate?             OnValidateWebSocketConnection;

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        event OnNewWebSocketConnectionDelegate?                  OnNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a reponse to a HTTP request was sent.
        /// </summary>
        event HTTPResponseLogDelegate?                           OnHTTPResponse;


        /// <summary>
        /// An event sent whenever a web socket frame was received.
        /// </summary>
        event OnWebSocketFrameDelegate?                          OnWebSocketFrameReceived;

        /// <summary>
        /// An event sent whenever a web socket frame was sent.
        /// </summary>
        event OnWebSocketFrameDelegate?                          OnWebSocketFrameSent;


        /// <summary>
        /// An event sent whenever a text message was sent.
        /// </summary>
        event OnWebSocketServerTextMessageSentDelegate?          OnTextMessageSent;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        event OnWebSocketServerTextMessageReceivedDelegate?      OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        event OnWebSocketServerBinaryMessageSentDelegate?        OnBinaryMessageSent;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        event OnWebSocketServerBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        event OnWebSocketServerPingMessageSentDelegate?          OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        event OnWebSocketServerPingMessageReceivedDelegate?      OnPingMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket pong frame was sent.
        /// </summary>
        event OnWebSocketServerPongMessageSentDelegate?          OnPongMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        event OnWebSocketServerPongMessageReceivedDelegate?      OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket close frame was sent.
        /// </summary>
        event OnWebSocketServerCloseMessageSentDelegate?         OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        event OnWebSocketServerCloseMessageReceivedDelegate?     OnCloseMessageReceived;


        /// <summary>
        /// An event sent whenever the HTTP WebSocket server stopped.
        /// </summary>
        event OnServerStoppedDelegate?                           OnServerStopped;

        #endregion


        #region Process(Text/Binary)Message

        /// <summary>
        /// The default HTTP WebSocket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        Task<WebSocketTextMessageResponse>    ProcessTextMessage  (DateTime                   RequestTimestamp,
                                                                   WebSocketServerConnection  Connection,
                                                                   String                     TextMessage,
                                                                   EventTracking_Id           EventTrackingId,
                                                                   CancellationToken          CancellationToken);

        /// <summary>
        /// The default HTTP WebSocket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        Task<WebSocketBinaryMessageResponse>  ProcessBinaryMessage(DateTime                   RequestTimestamp,
                                                                   WebSocketServerConnection  Connection,
                                                                   Byte[]                     BinaryMessage,
                                                                   EventTracking_Id           EventTrackingId,
                                                                   CancellationToken          CancellationToken);

        #endregion

        #region Send(Text/Binary)Message or WebSocketFrame

        /// <summary>
        /// Send a text web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The text message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        Task<SentStatus> SendTextMessage   (WebSocketServerConnection  Connection,
                                            String                     TextMessage,
                                            EventTracking_Id?          EventTrackingId     = null,
                                            CancellationToken          CancellationToken   = default);

        /// <summary>
        /// Send a binary web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The binary message to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        Task<SentStatus> SendBinaryMessage (WebSocketServerConnection  Connection,
                                            Byte[]                     BinaryMessage,
                                            EventTracking_Id?          EventTrackingId     = null,
                                            CancellationToken          CancellationToken   = default);

        /// <summary>
        /// Send a web socket frame.
        /// </summary>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="WebSocketFrame">The web socket frame to send.</param>
        /// <param name="EventTrackingId">An event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        Task<SentStatus> SendWebSocketFrame(WebSocketServerConnection  Connection,
                                            WebSocketFrame             WebSocketFrame,
                                            EventTracking_Id?          EventTrackingId     = null,
                                            CancellationToken          CancellationToken   = default);

        #endregion

        #region RemoveConnection  (Connection)

        /// <summary>
        /// Remove the given web socket connection.
        /// </summary>
        /// <param name="Connection">A HTTP WebSocket connection.</param>
        Boolean RemoveConnection(WebSocketServerConnection Connection);

        #endregion


        #region AddOrUpdateHTTPBasicAuth(Username, Password)

        /// <summary>
        /// Add the given HTTP Basic Authentication password for the given username.
        /// </summary>
        /// <param name="Username">The unique identification of the username.</param>
        /// <param name="Password">The password of the charging station.</param>
        HTTPBasicAuthentication AddOrUpdateHTTPBasicAuth(String Username,
                                                         String Password);

        #endregion

        #region RemoveHTTPBasicAuth     (Username)

        /// <summary>
        /// Remove the given HTTP Basic Authentication for the given username.
        /// </summary>
        /// <param name="Username">The unique identification of the username.</param>
        Boolean RemoveHTTPBasicAuth(String Username);

        #endregion



        /// <summary>
        /// Start the HTTP WebSocket listener thread.
        /// </summary>
        void Start();

        /// <summary>
        /// Shutdown the HTTP WebSocket listener thread.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        Task Shutdown(String?  Message   = null,
                      Boolean  Wait      = true);

    }

}
