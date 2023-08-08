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

using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

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
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="AutoStart">Whether to start the HTTP web socket server automatically.</param>
        public WebSocketProxy(URL                                   UpstreamServerURL,
                              Boolean                               AutoConnect                  = true,

                              IIPAddress?                           IPAddress                    = null,
                              IPPort?                               HTTPPort                     = null,
                              String?                               HTTPServiceName              = null,

                              ServerCertificateSelectorDelegate?    ServerCertificateSelector    = null,
                              RemoteCertificateValidationCallback?  ClientCertificateValidator   = null,
                              LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                              SslProtocols?                         AllowedTLSProtocols          = null,
                              Boolean?                              ClientCertificateRequired    = null,
                              Boolean?                              CheckCertificateRevocation   = null,

                              ServerThreadNameCreatorDelegate?      ServerThreadNameCreator      = null,
                              ThreadPriority?                       ServerThreadPriority         = null,
                              Boolean?                              ServerThreadIsBackground     = null,

                              IEnumerable<String>?                  SecWebSocketProtocols        = null,
                              Boolean                               DisableWebSocketPings        = false,
                              TimeSpan?                             WebSocketPingEvery           = null,
                              TimeSpan?                             SlowNetworkSimulationDelay   = null,

                              DNSClient?                            DNSClient                    = null,
                              Boolean                               AutoStart                    = false)

            : base(IPAddress,
                   HTTPPort,
                   HTTPServiceName,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPriority,
                   ServerThreadIsBackground,

                   SecWebSocketProtocols,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

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
                UpstreamHTTPResponse = webSocketClient.Connect().GetAwaiter().GetResult();
            }


            #region WSClient: OnTextMessageReceived

            this.webSocketClient.OnTextMessageReceived += async (timestamp,
                                                                 webSocketClient,
                                                                 webSocketClientConnection,
                                                                 webSocketFrame,
                                                                 eventTrackingId,
                                                                 textMessage) =>
            {

                foreach (var connection in connections.Values)
                {

                    await connection.SendWebSocketFrame(webSocketFrame);

                    await SendOnWebSocketFrameSent(timestamp,
                                                   connection,
                                                   eventTrackingId,
                                                   webSocketFrame);

                    await SendOnTextMessageSent(timestamp,
                                                connection,
                                                eventTrackingId,
                                                textMessage);

                }

            };

            #endregion

            #region WSClient: OnBinaryMessageReceived

            this.webSocketClient.OnBinaryMessageReceived += async (timestamp,
                                                                   webSocketClient,
                                                                   webSocketClientConnection,
                                                                   webSocketFrame,
                                                                   eventTrackingId,
                                                                   binaryMessage) =>
            {

                foreach (var connection in connections.Values)
                {

                    await connection.SendWebSocketFrame(webSocketFrame);

                    await SendOnWebSocketFrameSent(timestamp,
                                                   connection,
                                                   eventTrackingId,
                                                   webSocketFrame);

                    await SendOnBinaryMessageSent(timestamp,
                                                  connection,
                                                  eventTrackingId,
                                                  binaryMessage);

                }

            };

            #endregion


            #region WSServer: OnNewWebSocketConnection

            OnNewWebSocketConnection += async (timestamp,
                                               webSocketServer,
                                               webSocketServerConnection,
                                               eventTrackingId,
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
                                               webSocketFrame) =>
            {

                if (webSocketFrame.IsText || webSocketFrame.IsBinary)
                {

                    

                    await webSocketClient.SendWebSocketFrame(webSocketFrame);

                }

            };

            #endregion

        }

        #endregion

        #endregion



        #region Connect()

        public async Task<HTTPResponse> Connect()
        {

            UpstreamHTTPResponse = await webSocketClient.Connect();

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
