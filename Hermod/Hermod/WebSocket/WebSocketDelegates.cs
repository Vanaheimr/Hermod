﻿/*
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A delegate called whenever the WebSocket server started.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the WebSocket server start.</param>
    /// <param name="Server">The WebSocket server.</param>
    /// <param name="EventTrackingId">The unique event tracking identification.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task                                 OnServerStartedDelegate                 (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    /// <summary>
    /// A delegate called whenever a new TCP connection was accepted and needs to be accepted
    /// or rejected based on TCP information. This acts like a simple firewall.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the new TCP connection.</param>
    /// <param name="Server">The WebSocket server.</param>
    /// <param name="Connection">The TCP connection.</param>
    /// <param name="EventTrackingId">The unique event tracking identification.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task<ConnectionFilterResponse>       OnValidateTCPConnectionDelegate         (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  System.Net.Sockets.TcpClient       Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    public delegate Task                                 OnNewTCPConnectionDelegate              (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          NewTCPConnection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    public delegate Task                                 OnNewTLSConnectionDelegate              (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          NewTLSConnection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);


    /// <summary>
    /// A delegate for logging the initial WebSocket HTTP request.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP request.</param>
    /// <param name="Server">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task                                 HTTPRequestLogDelegate                  (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  HTTPRequest                        Request,
                                                                                                  CancellationToken                  CancellationToken);

    public delegate Task<HTTPResponse?>                  OnValidateWebSocketConnectionDelegate   (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    public delegate String?                              SubprotocolSelectorDelegate             (IWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  IEnumerable<String>                SubProtocols);


    /// <summary>
    /// A delegate for logging the initial WebSocket HTTP response.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing HTTP response.</param>
    /// <param name="Server">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    /// <param name="Response">The outgoing HTTP response.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task                                 HTTPResponseLogDelegate                 (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  HTTPRequest?                       Request,
                                                                                                  HTTPResponse                       Response,
                                                                                                  CancellationToken                  CancellationToken);

    /// <summary>
    /// A delegate for logging new HTTP WebSocket connections.
    /// </summary>
    /// <param name="Timestamp">The logging timestamp.</param>
    /// <param name="Server">The HTTP WebSocket server.</param>
    /// <param name="NewConnection">The new HTTP WebSocket connection.</param>
    /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
    /// <param name="SharedSubprotocols">An enumeration of shared HTTP WebSocket subprotocols.</param>
    /// <param name="SelectedSubprotocol">The selected HTTP WebSocket subprotocol.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task                                 OnNewWebSocketConnectionDelegate               (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          NewConnection,
                                                                                                         IEnumerable<String>                SharedSubprotocols,
                                                                                                         String?                            SelectedSubprotocol,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketFrameDelegate                       (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         CancellationToken                  CancellationToken);

    //public delegate Task                                 OnWebSocketResponseFrameDelegate               (DateTime                           Timestamp,
    //                                                                                                     IWebSocketServer                   Server,
    //                                                                                                     WebSocketConnection                Connection,
    //                                                                                                     WebSocketFrame                     RequestFrame,
    //                                                                                                     WebSocketFrame                     ResponseFrame,
    //                                                                                                     EventTracking_Id                   EventTrackingId);


    public delegate Task                                 OnWebSocketServerTextMessageSentDelegate       (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         String                             TextMessage,
                                                                                                         SentStatus                         SentStatus,
                                                                                                         CancellationToken                  CancellationToken);

    public delegate Task                                 OnWebSocketServerTextMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         String                             TextMessage,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketServerBinaryMessageSentDelegate     (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             BinaryMessage,
                                                                                                         SentStatus                         SentStatus,
                                                                                                         CancellationToken                  CancellationToken);

    public delegate Task                                 OnWebSocketServerBinaryMessageReceivedDelegate (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             BinaryMessage,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketServerPingMessageSentDelegate       (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             PingMessage,
                                                                                                         SentStatus                         SentStatus,
                                                                                                         CancellationToken                  CancellationToken);

    public delegate Task                                 OnWebSocketServerPingMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             PingMessage,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketServerPongMessageSentDelegate       (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             PongMessage,
                                                                                                         SentStatus                         SentStatus,
                                                                                                         CancellationToken                  CancellationToken);

    public delegate Task                                 OnWebSocketServerPongMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         Byte[]                             PongMessage,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketServerCloseMessageSentDelegate      (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                                                         String?                            Reason,
                                                                                                         SentStatus                         SentStatus,
                                                                                                         CancellationToken                  CancellationToken);

    public delegate Task                                 OnWebSocketServerCloseMessageReceivedDelegate  (DateTime                           Timestamp,
                                                                                                         AWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         WebSocketFrame                     Frame,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                                                         String?                            Reason,
                                                                                                         CancellationToken                  CancellationToken);


    public delegate Task                                 OnTCPConnectionClosedDelegate                  (DateTime                           Timestamp,
                                                                                                         IWebSocketServer                   Server,
                                                                                                         WebSocketServerConnection          Connection,
                                                                                                         EventTracking_Id                   EventTrackingId,
                                                                                                         String?                            Reason,
                                                                                                         CancellationToken                  CancellationToken);


    /// <summary>
    /// A delegate called whenever the WebSocket server stopped.
    /// </summary>
    /// <param name="Timestamp">The timestamp when the HTTP WebSocket server stopped.</param>
    /// <param name="Server">The WebSocket server.</param>
    /// <param name="EventTrackingId">The unique event tracking identification.</param>
    /// <param name="Reason">An optional reason for the stop of the server.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task                                 OnServerStoppedDelegate                 (DateTime                           Timestamp,
                                                                                                  IWebSocketServer                   Server,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  String?                            Reason,
                                                                                                  CancellationToken                  CancellationToken);

}
