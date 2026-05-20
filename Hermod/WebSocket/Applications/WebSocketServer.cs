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

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketServer : AWebSocketServer
    {

        #region Events

     //   /// <summary>
     //   /// An event sent whenever a text message was received.
     //   /// </summary>
     //   public event OnWebSocketServerTextMessageDelegate?    OnTextMessage;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketServerTextMessageReceivedDelegate?      OnTextMessageReceived;

    //    /// <summary>
    //    /// An event sent whenever a binary message was received.
    //    /// </summary>
    //    public event OnWebSocketServerBinaryMessageDelegate?  OnBinaryMessage;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketServerBinaryMessageReceivedDelegate?    OnBinaryMessageReceived;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP WebSocket server.
        /// </summary>
        /// <param name="IPAddress">An optional IP address to listen on. Default: ATCPServer default.</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP WebSocket server automatically.</param>
        public WebSocketServer(IIPAddress?                                               IPAddress                    = null,
                               IPPort?                                                   HTTPPort                     = null,
                               String?                                                   HTTPServerName               = null,

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

                               String?                                                   Description                  = null,
                               ILoggerFactory?                                           LoggerFactory                = null,
                               Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   HTTPPort,
                   HTTPServerName,

                   RequireAuthentication,
                   SecWebSocketProtocols,
                   SubprotocolSelector,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   BufferSize,
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

                   Description,
                   LoggerFactory,
                   AutoStart: false)

        {

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

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

            var onTextMessageReceived = OnTextMessageReceived;
            if (onTextMessageReceived is not null)
            {
                try
                {

                    await Task.WhenAll(onTextMessageReceived.GetInvocationList().
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
                    Logger.LogError(e, "Exception while processing {EventName}.", nameof(OnTextMessageReceived));
                }
            }

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

            var onBinaryMessageReceived = OnBinaryMessageReceived;
            if (onBinaryMessageReceived is not null)
            {
                try
                {

                    await Task.WhenAll(onBinaryMessageReceived.GetInvocationList().
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
                    Logger.LogError(e, "Exception while processing {EventName}.", nameof(OnBinaryMessageReceived));
                }
            }

        }

        #endregion


    }

}
