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
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketProxy : AWebSocketServer
    {

        #region Data

        private readonly ConcurrentDictionary<String, WebSocketServerConnection> connections = new();

        private readonly WebSocketClient webSocketClient;

        #endregion

        #region Properties

        /// <summary>
        /// The URL of the upstream HTTP web socket server.
        /// </summary>
        public URL? UpstreamServerURL { get; }

        /// <summary>
        /// Whether to connect to the HTTP web socket server automatically.
        /// </summary>
        public Boolean AutoConnect { get; }

        /// <summary>
        /// The HTTP response of the upstream HTTP web socket server.
        /// </summary>
        public HTTPResponse? UpstreamHTTPResponse { get; private set; }

        #endregion

        #region Constructor(s)

        #region WebSocketProxy(IPAddress = null, HTTPPort = null, HTTPServiceName = null, ..., AutoStart = false)

        /// <summary>
        /// Create a new HTTP web socket server.
        /// </summary>
        /// <param name="UpstreamServerURL">An URL of the upstream HTTP web socket server.</param>
        /// <param name="AutoConnect">Whether to connect to the HTTP web socket server automatically.</param>
        /// 
        /// <param name="IPAddress">An optional IP address to listen on. Default: IPv4Address.Any</param>
        /// <param name="HTTPPort">An optional TCP port to listen on. Default: HTTP.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this HTTP Web Socket service.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketProxy(URL                                                             UpstreamServerURL,
                              Boolean                                                         AutoConnect                  = true,

                              IIPAddress?                                                     IPAddress                    = null,
                              IPPort?                                                         HTTPPort                     = null,
                              String?                                                         HTTPServiceName              = null,
                              I18NString?                                                     Description                  = null,

                              IEnumerable<String>?                                            SecWebSocketProtocols        = null,
                              Boolean                                                         DisableWebSocketPings        = false,
                              TimeSpan?                                                       WebSocketPingEvery           = null,
                              TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

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

            this.UpstreamServerURL  = UpstreamServerURL;
            this.AutoConnect        = AutoConnect;

            this.webSocketClient    = new WebSocketClient(
                                          UpstreamServerURL,
                                          DNSClient: DNSClient
                                      );

            if (AutoConnect)
            {
                UpstreamHTTPResponse = webSocketClient.Connect().GetAwaiter().GetResult().Item2;
            }


            #region WSClient: OnTextMessageReceived

            this.webSocketClient.OnTextMessageReceived += async (timestamp,
                                                                 webSocketClient,
                                                                 webSocketClientConnection,
                                                                 webSocketFrame,
                                                                 eventTrackingId,
                                                                 textMessage,
                                                                 cancellationToken) =>
            {

                foreach (var connection in connections.Values)
                {

                    await connection.SendWebSocketFrame(webSocketFrame,
                                                        cancellationToken);

                    await SendOnWebSocketFrameSent(timestamp,
                                                   connection,
                                                   eventTrackingId,
                                                   webSocketFrame,
                                                   cancellationToken);

                    //await SendOnTextMessageSent(timestamp,
                    //                            connection,
                    //                            eventTrackingId,
                    //                            textMessage,
                    //                            cancellationToken);

                }

            };

            #endregion

            #region WSClient: OnBinaryMessageReceived

            this.webSocketClient.OnBinaryMessageReceived += async (timestamp,
                                                                   webSocketClient,
                                                                   webSocketClientConnection,
                                                                   webSocketFrame,
                                                                   eventTrackingId,
                                                                   binaryMessage,
                                                                   cancellationToken) =>
            {

                foreach (var connection in connections.Values)
                {

                    await connection.SendWebSocketFrame(webSocketFrame,
                                                        cancellationToken);

                    await SendOnWebSocketFrameSent(timestamp,
                                                   connection,
                                                   eventTrackingId,
                                                   webSocketFrame,
                                                   cancellationToken);

                    //await SendOnBinaryMessageSent(timestamp,
                    //                              connection,
                    //                              eventTrackingId,
                    //                              binaryMessage,
                    //                              cancellationToken);

                }

            };

            #endregion


            #region WSServer: OnNewWebSocketConnection

            OnNewWebSocketConnection += async (timestamp,
                                               webSocketServer,
                                               webSocketServerConnection,
                                               eventTrackingId,
                                               sharedSubprotocols,
                                               cancellationToken) =>
            {

                connections.TryAdd(webSocketServerConnection.RemoteSocket.ToString(),
                                   webSocketServerConnection);

                //ToDo: Logging etc.pp...

            };

            #endregion

            #region WSServer: OnWebSocketFrameReceived

            OnWebSocketFrameReceived += async (timestamp,
                                               webSocketServer,
                                               webSocketServerConnection,
                                               eventTrackingId,
                                               webSocketFrame,
                                               cancellationToken) =>
            {

                if (webSocketFrame.IsText || webSocketFrame.IsBinary)
                {

                    await webSocketClient.SendWebSocketFrame(webSocketFrame,
                                                             eventTrackingId,
                                                             cancellationToken);

                }

            };

            #endregion

        }

        #endregion

        #endregion



        #region Connect()

        public async Task<HTTPResponse> Connect()
        {

            UpstreamHTTPResponse = (await webSocketClient.Connect()).Item2;

            return UpstreamHTTPResponse;

        }

        #endregion

        #region Close(StatusCode = Normal, Reason = null)

        /// <summary>
        /// Close the connection.
        /// </summary>
        /// <param name="StatusCode">An optional status code for closing.</param>
        /// <param name="Reason">An optional reason for closing.</param>
        public async Task Close(WebSocketFrame.ClosingStatusCode  StatusCode   = WebSocketFrame.ClosingStatusCode.NormalClosure,
                                String?                           Reason       = null)
        {

            await webSocketClient.Close(StatusCode,
                                        Reason);

        }

        #endregion


    }

}
