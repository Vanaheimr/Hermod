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

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A WebSocket endpoint for an HTTP path, without a TCP server of its own:
    /// its received messages are its events.
    /// </summary>
    /// <example>
    /// <code>
    /// var endpoint = new WebSocketEndpoint(RequireAuthentication: false);
    ///
    /// endpoint.OnTextMessageReceived += (timestamp, server, connection, frame, eventTrackingId, text, ct)
    ///                                       => endpoint.SendTextMessage(connection, $"Echo: {text}");
    ///
    /// httpAPI.AddHandler(HTTPPath.Parse("/ws"),
    ///                    HTTPMethod:   HTTPMethod.GET,
    ///                    HTTPDelegate: WebSocketUpgrade.For(endpoint));
    /// </code>
    /// </example>
    public class WebSocketEndpoint : AWebSocketEndpoint
    {

        #region Events

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketServerTextMessageReceivedDelegate?      OnTextMessageReceived;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketServerBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new WebSocket endpoint, to be put on an HTTP path with WebSocketUpgrade.For().
        /// </summary>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this endpoint.</param>
        /// <param name="RequireAuthentication">Whether clients must authenticate; by default they must.</param>
        /// <param name="SecWebSocketProtocols">The supported subprotocols, e.g. "ocpp2.1".</param>
        /// <param name="SubprotocolSelector">An optional selector of the subprotocol among those a client offers.</param>
        /// <param name="DisableWebSocketPings">Whether to send no pings.</param>
        /// <param name="WebSocketPingEvery">The ping interval; 30 seconds by default.</param>
        /// <param name="SlowNetworkSimulationDelay">An optional delay between the bytes sent, for debugging other stacks.</param>
        /// <param name="ConnectionIdBuilder">Builds the identification of a connection handed over without one.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public WebSocketEndpoint(String?                       HTTPServiceName              = null,
                                 I18NString?                   Description                  = null,

                                 Boolean?                      RequireAuthentication        = true,
                                 IEnumerable<String>?          SecWebSocketProtocols        = null,
                                 SubprotocolSelectorDelegate?  SubprotocolSelector          = null,
                                 Boolean                       DisableWebSocketPings        = false,
                                 TimeSpan?                     WebSocketPingEvery           = null,
                                 TimeSpan?                     SlowNetworkSimulationDelay   = null,

                                 ConnectionIdBuilder?          ConnectionIdBuilder          = null,
                                 ILoggerFactory?               LoggerFactory                = null)

            : base(HTTPServiceName,
                   Description,
                   RequireAuthentication,
                   SecWebSocketProtocols,
                   SubprotocolSelector,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,
                   ConnectionIdBuilder,
                   LoggerFactory)

        { }

        #endregion


        #region ProcessTextMessage   (RequestTimestamp, Server, Connection, EventTrackingId, TextFrame,   TextMessage,   CancellationToken)

        /// <summary>
        /// Tell of a received text message.
        /// </summary>
        public override Task ProcessTextMessage(DateTimeOffset             RequestTimestamp,
                                                AWebSocketEndpoint         Server,
                                                WebSocketServerConnection  Connection,
                                                EventTracking_Id           EventTrackingId,
                                                WebSocketFrame             TextFrame,
                                                String                     TextMessage,
                                                CancellationToken          CancellationToken)

            => OnTextMessageReceived.InvokeAllAsync(
                   handler => handler(
                                  RequestTimestamp,
                                  this,
                                  Connection,
                                  TextFrame,
                                  EventTrackingId,
                                  TextMessage,
                                  CancellationToken
                              ),
                   Logger
               );

        #endregion

        #region ProcessBinaryMessage (RequestTimestamp, Server, Connection, EventTrackingId, BinaryFrame, BinaryMessage, CancellationToken)

        /// <summary>
        /// Tell of a received binary message.
        /// </summary>
        public override Task ProcessBinaryMessage(DateTimeOffset             RequestTimestamp,
                                                  AWebSocketEndpoint         Server,
                                                  WebSocketServerConnection  Connection,
                                                  EventTracking_Id           EventTrackingId,
                                                  WebSocketFrame             BinaryFrame,
                                                  Byte[]                     BinaryMessage,
                                                  CancellationToken          CancellationToken)

            => OnBinaryMessageReceived.InvokeAllAsync(
                   handler => handler(
                                  RequestTimestamp,
                                  this,
                                  Connection,
                                  BinaryFrame,
                                  EventTrackingId,
                                  BinaryMessage,
                                  CancellationToken
                              ),
                   Logger
               );

        #endregion

    }

}
