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

using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketChatServer : AWebSocketServer
    {

        #region Data

        private readonly ConcurrentDictionary<String, WebSocketServerConnection>  connections = new();

        #endregion

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
        /// <param name="Description">An optional description of this HTTP Web Socket service.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketChatServer(IIPAddress?                                                     IPAddress                    = null,
                                   IPPort?                                                         HTTPPort                     = null,
                                   String?                                                         HTTPServiceName              = null,
                                   I18NString?                                                     Description                  = null,

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

                                   IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                                   Boolean                                                         DisableWebSocketPings        = false,
                                   TimeSpan?                                                       WebSocketPingEvery           = null,
                                   TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                   DNSClient?                                                      DNSClient                    = null,
                                   Boolean                                                         AutoStart                    = false)

            : base(IPAddress,
                   HTTPPort,
                   HTTPServiceName,
                   Description,

                   SecWebSocketProtocols,
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

        {

            OnNewWebSocketConnection += async (timestamp,
                                               webSocketServer,
                                               webSocketServerConnection,
                                               eventTrackingId,
                                               sharedSubprotocols,
                                               cancellationToken) => {

                connections.TryAdd(webSocketServerConnection.RemoteSocket.ToString(),
                                   webSocketServerConnection);

                await Task.Delay(10, cancellationToken);
                await webSocketServerConnection.SendWebSocketFrame(WebSocketFrame.Text($"Welcome '{webSocketServerConnection.RemoteSocket}' to the '{HTTPServiceName}' web socket chat server!"), cancellationToken);

            };

        }

        #endregion

        #endregion


        #region ProcessTextMessage  (RequestTimestamp, Connection, TextMessage,   EventTrackingId, CancellationToken)

        /// <summary>
        /// The default HTTP web socket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime                   RequestTimestamp,
                                                                                    WebSocketServerConnection  Connection,
                                                                                    String                     TextMessage,
                                                                                    EventTracking_Id           EventTrackingId,
                                                                                    CancellationToken          CancellationToken)
        {

            var responses = Array.Empty<WebSocketTextMessageResponse>();

            var onTextMessage = OnTextMessage;
            if (onTextMessage is not null)
            {
                try
                {

                    responses = await Task.WhenAll(onTextMessage.GetInvocationList().
                                                       OfType<OnWebSocketTextMessage2Delegate>().
                                                       Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                     RequestTimestamp,
                                                                                     this,
                                                                                     Connection,
                                                                                     EventTrackingId,
                                                                                     RequestTimestamp,
                                                                                     TextMessage,
                                                                                     CancellationToken
                                                                                 )).
                                                       ToArray());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, $"{nameof(WebSocketChatServer)}.{nameof(OnTextMessage)}");
                }
            }

            var response = responses.Where(response => response                 is not null &&
                                                       response.ResponseMessage.IsNotNullOrEmpty()).
                                     FirstOrDefault();


            if (TextMessage.StartsWith("chat::"))
            {
                foreach (var connection in connections.Values)
                {
                    await connection.SendWebSocketFrame(
                              WebSocketFrame.Text($"'{TextMessage}' from {Connection.RemoteSocket}"),
                              CancellationToken
                          );
                }
            }


            response ??= new WebSocketTextMessageResponse(
                             RequestTimestamp,
                             TextMessage,
                             Timestamp.Now,
                             "Unknown Error!",
                             EventTrackingId,
                             CancellationToken
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
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime                   RequestTimestamp,
                                                                                        WebSocketServerConnection  Connection,
                                                                                        Byte[]                     BinaryMessage,
                                                                                        EventTracking_Id           EventTrackingId,
                                                                                        CancellationToken          CancellationToken)
        {

            var responses = Array.Empty<WebSocketBinaryMessageResponse>();

            var onTextMessage = OnTextMessage;
            if (onTextMessage is not null)
            {
                try
                {

                    responses = await Task.WhenAll(onTextMessage.GetInvocationList().
                                                       OfType<OnWebSocketBinaryMessage2Delegate>().
                                                       Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                     RequestTimestamp,
                                                                                     this,
                                                                                     Connection,
                                                                                     EventTrackingId,
                                                                                     RequestTimestamp,
                                                                                     BinaryMessage,
                                                                                     CancellationToken
                                                                                 )).
                                                       ToArray());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, $"{nameof(WebSocketChatServer)}.{nameof(OnTextMessage)}");
                }
            }

            var response = responses.Where(response => response                 is not null &&
                                                       response.ResponseMessage.IsNeitherNullNorEmpty()).
                                     FirstOrDefault();


            response ??= new WebSocketBinaryMessageResponse(
                             RequestTimestamp,
                             BinaryMessage,
                             Timestamp.Now,
                             "Unkown error!".ToUTF8Bytes(),
                             EventTrackingId,
                             CancellationToken
                         );

            return response;

        }

        #endregion


    }

}
