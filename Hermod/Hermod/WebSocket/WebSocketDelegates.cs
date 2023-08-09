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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public delegate Task                                 OnServerStartedDelegate                 (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  EventTracking_Id                   EventTrackingId);

    public delegate Task<Boolean?>                       OnValidateTCPConnectionDelegate         (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  System.Net.Sockets.TcpClient       Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    public delegate Task                                 OnNewTCPConnectionDelegate              (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    /// <summary>
    /// The delegate for HTTP request logging.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP request.</param>
    /// <param name="Server">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    public delegate Task                                 HTTPRequestLogDelegate                  (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  HTTPRequest                        Request);

    public delegate Task<HTTPResponse?>                  OnValidateWebSocketConnectionDelegate   (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);

    /// <summary>
    /// The delegate for HTTP response logging.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing HTTP response.</param>
    /// <param name="Server">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    /// <param name="Response">The outgoing HTTP response.</param>
    public delegate Task                                 HTTPResponseLogDelegate                 (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  HTTPRequest                        Request,
                                                                                                  HTTPResponse                       Response);

    public delegate Task                                 OnNewWebSocketConnectionDelegate        (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  CancellationToken                  CancellationToken);


    public delegate Task                                 OnWebSocketFrameDelegate                (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  WebSocketFrame                     Frame);

    //public delegate Task                                 OnWebSocketResponseFrameDelegate        (DateTime                           Timestamp,
    //                                                                                              AWebSocketServer                   Server,
    //                                                                                              WebSocketConnection                Connection,
    //                                                                                              WebSocketFrame                     RequestFrame,
    //                                                                                              WebSocketFrame                     ResponseFrame,
    //                                                                                              EventTracking_Id                   EventTrackingId);


    public delegate Task                                 OnWebSocketTextMessageDelegate          (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  String                             TextMessage);

    public delegate Task                                 OnWebSocketBinaryMessageDelegate        (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  Byte[]                             BinaryMessage);


    public delegate Task                                 OnCloseMessageDelegate                  (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  EventTracking_Id                   EventTrackingId,
                                                                                                  WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                                                  String?                            Reason);

    public delegate Task                                 OnTCPConnectionClosedDelegate           (DateTime                           Timestamp,
                                                                                                  AWebSocketServer                   Server,
                                                                                                  WebSocketServerConnection          Connection,
                                                                                                  String                             Reason,
                                                                                                  EventTracking_Id                   EventTrackingId);

}
