/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public delegate Task                                 OnNewTCPConnectionDelegate              (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  NewWebSocketConnection,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task<HTTPResponse>                   OnOnValidateWebSocketConnectionDelegate (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  NewWebSocketConnection,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task                                 OnNewWebSocketConnectionDelegate        (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  NewWebSocketConnection,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);


    public delegate Task<WebSocketMessageRespose>        OnWebSocketMessageDelegate              (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  WebSocketFrame       Message,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);


    public delegate Task                                 OnWebSocketTextMessageRequestDelegate   (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  String               TextRequestMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task<WebSocketTextMessageRespose>    OnWebSocketTextMessageDelegate          (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  String               TextMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task                                 OnWebSocketTextMessageResponseDelegate  (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  String               TextRequestMessage,
                                                                                                  String               TextResponseMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);


    public delegate Task                                 OnWebSocketBinaryMessageRequestDelegate (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  Byte[]               BinaryRequestMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task<WebSocketBinaryMessageRespose>  OnWebSocketBinaryMessageDelegate        (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  Byte[]               BinaryMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

    public delegate Task                                 OnWebSocketBinaryMessageResponseDelegate(DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  Sender,
                                                                                                  Byte[]               BinaryRequestMessage,
                                                                                                  Byte[]               BinaryResponseMessage,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);


    public delegate Task                                 OnCloseMessageDelegate                  (DateTime             Timestamp,
                                                                                                  WebSocketServer      WebSocketServer,
                                                                                                  WebSocketConnection  WebSocketConnection,
                                                                                                  WebSocketFrame       Message,
                                                                                                  EventTracking_Id     EventTrackingId,
                                                                                                  CancellationToken    CancellationToken);

}
