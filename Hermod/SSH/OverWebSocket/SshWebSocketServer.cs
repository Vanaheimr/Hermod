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
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A WebSocket server for SSH over WebSocket. It listens on a port of its own,
    /// or is handed its WebSockets by another server on that server's port, with
    /// <c>RouteBySubprotocol</c>: a charging station then reaches SSH on the port
    /// it reaches OCPP on, and is told apart from OCPP by the subprotocol
    /// <see cref="SshOverWebSocket.Subprotocol"/>. Like <see cref="SshWebSocketEndpoint"/>,
    /// it takes that subprotocol and nothing else, never compresses, and hands every
    /// connection, as a pipe, to <see cref="OnConnection"/>.
    /// </summary>
    /// <remarks>
    /// Who may connect is said by the HTTP authentication of the upgrade. A server
    /// that is routed to knows only the clients it was told of, or what an override
    /// of AuthenticateAsync knows - such as the logins of the server it is routed
    /// from.
    /// </remarks>
    public class SshWebSocketServer : AWebSocketServer
    {

        #region Data

        private readonly SshWebSocketSessions  sessions;

        #endregion

        #region Properties

        /// <summary>
        /// What to do with every SSH connection that comes in.
        /// </summary>
        public SshWebSocketConnectionHandler  OnConnection    { get; }

        /// <summary>
        /// The most bytes of the SSH stream one binary message sent carries.
        /// </summary>
        public Int32                          MessageSize     { get; init; } = SshOverWebSocket.DefaultMessageSize;

        /// <summary>
        /// How many received bytes SSH may leave unread before a connection is read no further.
        /// </summary>
        public Int64                          ReceiveBuffer   { get; init; } = SshOverWebSocket.DefaultReceiveBuffer;

        /// <summary>
        /// How many SSH connections run now.
        /// </summary>
        public Int32                          SshConnections  => sessions.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a WebSocket server for SSH over WebSocket.
        /// </summary>
        /// <param name="OnConnection">What to do with every SSH connection that comes in.</param>
        /// <param name="IPAddress">The IP address to listen on, where it listens itself.</param>
        /// <param name="TCPPort">The TCP port to listen on, where it listens itself.</param>
        /// <param name="HTTPServerName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this server.</param>
        /// <param name="RequireAuthentication">Whether clients must authenticate over HTTP before SSH; by default they must.</param>
        /// <param name="DisableWebSocketPings">Whether to send no pings - SSH keepalives keep a proxy from cutting an idle connection as well.</param>
        /// <param name="WebSocketPingEvery">The ping interval; 30 seconds by default.</param>
        /// <param name="ServerCertificateSelector">The TLS certificate of the server, where it listens itself over TLS.</param>
        /// <param name="ClientCertificateValidator">The validator of TLS client certificates.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        /// <param name="AutoStart">Whether to start listening at once.</param>
        public SshWebSocketServer(SshWebSocketConnectionHandler                             OnConnection,
                                  IPAddress?                                                IPAddress                    = null,
                                  IPPort?                                                   TCPPort                      = null,
                                  String?                                                   HTTPServerName               = null,
                                  I18NString?                                               Description                  = null,
                                  Boolean?                                                  RequireAuthentication        = true,
                                  Boolean                                                   DisableWebSocketPings        = false,
                                  TimeSpan?                                                 WebSocketPingEvery           = null,
                                  ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                                  RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                                  IDNSClient?                                               DNSClient                    = null,
                                  ILoggerFactory?                                           LoggerFactory                = null,
                                  Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   TCPPort,
                   HTTPServerName,
                   Description,
                   RequireAuthentication,
                   [ SshOverWebSocket.Subprotocol ],
                   DisableWebSocketPings:       DisableWebSocketPings,
                   WebSocketPingEvery:          WebSocketPingEvery,
                   ServerCertificateSelector:   ServerCertificateSelector,
                   ClientCertificateValidator:  ClientCertificateValidator,
                   DNSClient:                   DNSClient,
                   LoggerFactory:               LoggerFactory,
                   AutoStart:                   AutoStart)

        {

            this.OnConnection  = OnConnection;
            this.sessions      = new SshWebSocketSessions(this, OnConnection, () => MessageSize, () => ReceiveBuffer, Logger);

        }

        #endregion


        #region ProcessBinaryMessage (Timestamp, Server, Connection, EventTrackingId, BinaryFrame, BinaryMessage, CancellationToken)

        /// <summary>
        /// Hand the bytes of a binary message to SSH - waited for, so that the
        /// connection is read no further while SSH has left too much unread.
        /// </summary>
        public override Task ProcessBinaryMessage(DateTimeOffset             Timestamp,
                                                  AWebSocketEndpoint         Server,
                                                  WebSocketServerConnection  Connection,
                                                  EventTracking_Id           EventTrackingId,
                                                  WebSocketFrame             BinaryFrame,
                                                  Byte[]                     BinaryMessage,
                                                  CancellationToken          CancellationToken)

            => sessions.BinaryReceived(Connection, BinaryMessage, CancellationToken);

        #endregion

        #region ProcessTextMessage   (Timestamp, Server, Connection, EventTrackingId, TextFrame,   TextMessage,   CancellationToken)

        /// <summary>
        /// A text message: SSH is carried in binary messages alone, and the connection is closed with 1003.
        /// </summary>
        public override Task ProcessTextMessage(DateTimeOffset             Timestamp,
                                                AWebSocketEndpoint         Server,
                                                WebSocketServerConnection  Connection,
                                                EventTracking_Id           EventTrackingId,
                                                WebSocketFrame             TextFrame,
                                                String                     TextMessage,
                                                CancellationToken          CancellationToken)

            => sessions.TextReceived(Connection, CancellationToken);

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Close every connection, wait for what ran on them to end, and stop listening.
        /// </summary>
        public override async ValueTask DisposeAsync()
        {

            await sessions.CloseAllAsync().ConfigureAwait(false);

            await base.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

    }

}
