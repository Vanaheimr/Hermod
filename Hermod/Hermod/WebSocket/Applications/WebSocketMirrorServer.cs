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

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketMirrorServer : AWebSocketServer,
                                         IWebSocketServer
    {

        #region Events

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketServerTextMessageReceivedDelegate?    OnTextMessageReceived;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketServerBinaryMessageReceivedDelegate?  OnBinaryMessageReceived;

        #endregion

        #region Constructor(s)

        #region WebSocketMirrorServer (IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public WebSocketMirrorServer(IIPAddress?                                                     IPAddress                    = null,
                                     IPPort?                                                         HTTPPort                     = null,
                                     String?                                                         HTTPServiceName              = null,
                                     I18NString?                                                     Description                  = null,

                                     Boolean?                                                        RequireAuthentication        = true,
                                     IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                                     SubprotocolSelectorDelegate?                                    SubprotocolSelector          = null,
                                     Boolean                                                         DisableWebSocketPings        = false,
                                     TimeSpan?                                                       WebSocketPingEvery           = null,
                                     TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                     Func<X509Certificate2>?                                         ServerCertificateSelector    = null,
                                     RemoteTLSClientCertificateValidationHandler<AWebSocketServer>?  ClientCertificateValidator   = null,
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

            : base(IPAddress,
                   HTTPPort,
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

        #region WebSocketMirrorServer (IPSocket, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPSocket">The IP socket to listen on.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public WebSocketMirrorServer(IPSocket                                                        IPSocket,
                                     String?                                                         HTTPServiceName              = null,
                                     I18NString?                                                     Description                  = null,

                                     Boolean?                                                        RequireAuthentication        = true,
                                     IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                                     SubprotocolSelectorDelegate?                                    SubprotocolSelector          = null,
                                     Boolean                                                         DisableWebSocketPings        = false,
                                     TimeSpan?                                                       WebSocketPingEvery           = null,
                                     TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                     Func<X509Certificate2>?                                         ServerCertificateSelector    = null,
                                     RemoteTLSClientCertificateValidationHandler<AWebSocketServer>?  ClientCertificateValidator   = null,
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

            : base(IPSocket,
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

        #endregion


        #region ProcessTextMessage   (RequestTimestamp, Connection, TextMessage,   EventTrackingId, CancellationToken)

        /// <summary>
        /// The default HTTP WebSocket text message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="TextMessage">The web socket text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task ProcessTextMessage(DateTimeOffset             RequestTimestamp,
                                                      AWebSocketServer           Server,
                                                      WebSocketServerConnection  Connection,
                                                      EventTracking_Id           EventTrackingId,
                                                      WebSocketFrame             TextFrame,
                                                      String                     TextMessage,
                                                      CancellationToken          CancellationToken)
        {

            var responses = Array.Empty<WebSocketTextMessageResponse>();

            var onTextMessage = OnTextMessageReceived;
            if (onTextMessage is not null)
            {
                try
                {

                    await Task.WhenAll(onTextMessage.GetInvocationList().
                                           OfType<OnWebSocketServerTextMessageReceivedDelegate>().
                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                         RequestTimestamp,
                                                                         this,
                                                                         Connection,
                                                                         TextFrame,
                                                                         EventTrackingId,
                                                                         TextMessage,
                                                                         CancellationToken
                                                                     )));

                }
                catch (Exception e)
                {
                    DebugX.LogException(e, $"{nameof(WebSocketChatServer)}.{nameof(OnTextMessageReceived)}");
                }
            }


            var result = await SendTextMessage(
                                   Connection,
                                   TextFrame.Payload.ToUTF8String().Reverse(),
                                   EventTrackingId,
                                   CancellationToken
                               );

            if (result != SentStatus.Success)
                DebugX.LogT(result.ToString());

        }

        #endregion

        #region ProcessBinaryMessage (RequestTimestamp, Connection, BinaryMessage, EventTrackingId, CancellationToken)

        /// <summary>
        /// The default HTTP WebSocket binary message processor.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request message.</param>
        /// <param name="Connection">The web socket connection.</param>
        /// <param name="BinaryMessage">The web socket binary message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async override Task ProcessBinaryMessage(DateTimeOffset             RequestTimestamp,
                                                        AWebSocketServer           Server,
                                                        WebSocketServerConnection  Connection,
                                                        EventTracking_Id           EventTrackingId,
                                                        WebSocketFrame             BinaryFrame,
                                                        Byte[]                     BinaryMessage,
                                                        CancellationToken          CancellationToken)
        {

            var responses = Array.Empty<WebSocketBinaryMessageResponse>();

            var onBinaryMessage = OnBinaryMessageReceived;
            if (onBinaryMessage is not null)
            {
                try
                {

                    await Task.WhenAll(onBinaryMessage.GetInvocationList().
                                           OfType<OnWebSocketServerBinaryMessageReceivedDelegate>().
                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                         RequestTimestamp,
                                                                         this,
                                                                         Connection,
                                                                         BinaryFrame,
                                                                         EventTrackingId,
                                                                         BinaryMessage,
                                                                         CancellationToken
                                                                     )));

                }
                catch (Exception e)
                {
                    DebugX.LogException(e, $"{nameof(WebSocketChatServer)}.{nameof(OnBinaryMessageReceived)}");
                }
            }


            var bytes = new Byte[BinaryFrame.Payload.Length];
            Array.Copy(BinaryFrame.Payload, bytes, BinaryFrame.Payload.Length);
            bytes.Reverse();

            var result = await SendBinaryMessage(
                                   Connection,
                                   bytes,
                                   EventTrackingId,
                                   CancellationToken
                               );

            if (result != SentStatus.Success)
                DebugX.LogT(result.ToString());

        }

        #endregion


    }

}
