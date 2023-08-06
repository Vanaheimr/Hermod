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
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketServer : AWebSocketServer
    {

        #region Events

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketTextMessage2Delegate?    OnTextMessage;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketBinaryMessage2Delegate?  OnBinaryMessage;

        #endregion

        #region Constructor(s)

        #region WebSocketServer(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
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
                               Boolean                           AutoStart                    = false)

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
                   AutoStart)

        { }

        #endregion

        #region WebSocketServer(IPSocket, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
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
                               Boolean                           AutoStart                    = false)

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
                   AutoStart)

        { }

        #endregion

        #endregion


        #region ProcessTextMessage  (RequestTimestamp, Connection, TextMessage,   EventTrackingId, CancellationToken)

        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime                   RequestTimestamp,
                                                                                    WebSocketServerConnection  Connection,
                                                                                    String                     TextMessage,
                                                                                    EventTracking_Id           EventTrackingId,
                                                                                    CancellationToken          CancellationToken)
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

        #endregion

        #region ProcessBinaryMessage(RequestTimestamp, Connection, BinaryMessage, EventTrackingId, CancellationToken)

        /// <summary>
        /// The default HTTP web socket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">The event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime                   RequestTimestamp,
                                                                                        WebSocketServerConnection  Connection,
                                                                                        Byte[]                     BinaryMessage,
                                                                                        EventTracking_Id           EventTrackingId,
                                                                                        CancellationToken          CancellationToken)
        {

            WebSocketBinaryMessageResponse? response = null;

            var onBinaryMessage = OnBinaryMessage;
            if (onBinaryMessage is not null)
                response = await onBinaryMessage.Invoke(RequestTimestamp,
                                                        this,
                                                        Connection,
                                                        EventTrackingId,
                                                        RequestTimestamp,
                                                        BinaryMessage);

            var binaryResponse = new Byte[BinaryMessage.Length];
            Array.Copy(BinaryMessage, binaryResponse, BinaryMessage.Length);
            binaryResponse.Reverse();

            response ??= new WebSocketBinaryMessageResponse(
                             RequestTimestamp,
                             BinaryMessage,
                             Timestamp.Now,
                             binaryResponse,
                             EventTrackingId
                         );

            return response;

        }

        #endregion


    }

}
