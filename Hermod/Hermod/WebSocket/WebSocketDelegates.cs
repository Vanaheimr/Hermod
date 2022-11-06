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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public delegate Task                                 OnNewTCPConnectionDelegate              (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              NewWebSocketConnection,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);

    /// <summary>
    /// The delegate for HTTP request logging.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP request.</param>
    /// <param name="WebSocketServer">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    public delegate Task                                 HTTPRequestLogDelegate                  (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  HTTPRequest                      Request);

    public delegate Task<HTTPResponse?>                  OnOnValidateWebSocketConnectionDelegate (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              NewWebSocketConnection,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);

    /// <summary>
    /// The delegate for HTTP response logging.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing HTTP response.</param>
    /// <param name="WebSocketServer">The sending web socket server.</param>
    /// <param name="Request">The incoming HTTP request.</param>
    /// <param name="Response">The outgoing HTTP response.</param>
    public delegate Task                                 HTTPResponseLogDelegate                 (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  HTTPRequest                      Request,
                                                                                                  HTTPResponse                     Response);

    public delegate Task                                 OnNewWebSocketConnectionDelegate        (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              NewWebSocketConnection,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);


    public delegate Task                                 OnWebSocketFrameDelegate                (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketFrame                   Frame,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);

    public delegate Task                                 OnWebSocketResponseFrameDelegate        (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketFrame                   RequestFrame,
                                                                                                  WebSocketFrame                   ResponseFrame,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);


    public delegate Task                                 OnWebSocketTextMessageRequestDelegate   (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketTextMessageRequest      TextRequestMessage,
                                                                                                  CancellationToken                CancellationToken);

    public delegate Task                                 OnWebSocketTextMessageResponseDelegate  (DateTime                         LogTimestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketTextMessageResponse     Response);


    public delegate Task                                 OnWebSocketBinaryMessageRequestDelegate (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketBinaryMessageRequest    BinaryRequestMessage,
                                                                                                  CancellationToken                CancellationToken);

    public delegate Task                                 OnWebSocketBinaryMessageResponseDelegate(DateTime                         LogTimestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              Sender,
                                                                                                  WebSocketBinaryMessageResponse   Response);


    public delegate Task                                 OnCloseMessageDelegate                  (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              WebSocketConnection,
                                                                                                  WebSocketFrame                   Message,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);

    public delegate Task                                 OnTCPConnectionClosedDelegate           (DateTime                         Timestamp,
                                                                                                  WebSocketServer                  WebSocketServer,
                                                                                                  WebSocketConnection              WebSocketConnection,
                                                                                                  String                           Reason,
                                                                                                  EventTracking_Id                 EventTrackingId,
                                                                                                  CancellationToken                CancellationToken);

}
