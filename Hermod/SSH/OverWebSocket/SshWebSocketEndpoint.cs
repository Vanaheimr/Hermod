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

using System.IO.Pipelines;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// An SSH connection that came in over a WebSocket: the pipe SSH runs on, and
    /// what the WebSocket said of who is at the other end.
    /// </summary>
    /// <param name="Pipe">The pipe SSH runs on.</param>
    /// <param name="WebSocket">The WebSocket connection it goes over.</param>
    public sealed class SshWebSocketConnection(IDuplexPipe                Pipe,
                                               WebSocketServerConnection  WebSocket)
    {

        /// <summary>
        /// The pipe SSH runs on - for <c>SshServer.ServeAsync</c> where this end is the
        /// SSH server, or for <c>SSHClient.ConnectOver</c> where it is the SSH client.
        /// </summary>
        public IDuplexPipe                Pipe            { get; } = Pipe;

        /// <summary>
        /// The WebSocket connection the SSH stream goes over.
        /// </summary>
        public WebSocketServerConnection  WebSocket       { get; } = WebSocket;

        /// <summary>
        /// Who the HTTP authentication of the upgrade said the other end is - by any
        /// of the endpoint's ways: HTTP Basic, a one-time password, one bound to the
        /// TLS connection, a client certificate. Null where nobody authenticated.
        /// </summary>
        public String?                    Login           => WebSocket.Login;

        /// <summary>
        /// The request that asked for the upgrade: its path and its header fields.
        /// </summary>
        public HTTPRequest?               HTTPRequest     => WebSocket.HTTPRequest;

        /// <summary>
        /// Where the other end is.
        /// </summary>
        public IPSocket                   RemoteSocket    => WebSocket.RemoteSocket;

        /// <summary>
        /// Where this end is.
        /// </summary>
        public IPSocket                   LocalSocket     => WebSocket.LocalSocket;

    }


    /// <summary>
    /// What to do with an SSH connection that came in over a WebSocket: run an SSH
    /// server on it, or an SSH client - whoever opened the WebSocket need not be
    /// the SSH client. The WebSocket is closed once the returned task is done.
    /// </summary>
    /// <param name="Connection">The SSH connection that came in.</param>
    /// <param name="CancellationToken">Cancelled once the WebSocket has ended.</param>
    public delegate Task SshWebSocketConnectionHandler(SshWebSocketConnection  Connection,
                                                       CancellationToken       CancellationToken);


    /// <summary>
    /// A WebSocket endpoint for SSH over WebSocket, to be put on an HTTP path with
    /// <c>WebSocketUpgrade.For()</c>: it takes the subprotocol
    /// <see cref="SshOverWebSocket.Subprotocol"/> and nothing else, never compresses,
    /// and hands every connection, as a pipe, to <see cref="OnConnection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Who may connect is said by the HTTP authentication of the upgrade, as at any
    /// other endpoint: HTTP Basic, one-time passwords - bound to the TLS connection
    /// or not -, client certificates, or what an override of AuthenticateAsync
    /// knows. Somebody turned away there never reaches the SSH handshake. Who is who
    /// is said again by SSH itself, end to end, whatever is in between.
    /// </para>
    /// <para>
    /// Per-message compression stays off: the SSH stream is encrypted, and what is
    /// encrypted does not compress.
    /// </para>
    /// </remarks>
    public class SshWebSocketEndpoint : AWebSocketEndpoint
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
        /// Create a WebSocket endpoint for SSH over WebSocket.
        /// </summary>
        /// <param name="OnConnection">What to do with every SSH connection that comes in.</param>
        /// <param name="HTTPServiceName">An optional HTTP service name.</param>
        /// <param name="Description">An optional description of this endpoint.</param>
        /// <param name="RequireAuthentication">Whether clients must authenticate over HTTP before SSH; by default they must.</param>
        /// <param name="DisableWebSocketPings">Whether to send no pings - SSH keepalives keep a proxy from cutting an idle connection as well.</param>
        /// <param name="WebSocketPingEvery">The ping interval; 30 seconds by default.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public SshWebSocketEndpoint(SshWebSocketConnectionHandler  OnConnection,
                                    String?                        HTTPServiceName         = null,
                                    I18NString?                    Description             = null,
                                    Boolean?                       RequireAuthentication   = true,
                                    Boolean                        DisableWebSocketPings   = false,
                                    TimeSpan?                      WebSocketPingEvery      = null,
                                    ILoggerFactory?                LoggerFactory           = null)

            : base(HTTPServiceName,
                   Description,
                   RequireAuthentication,
                   [ SshOverWebSocket.Subprotocol ],
                   null,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   null,
                   null,
                   LoggerFactory)

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
        /// Close every connection, and wait for what ran on them to end.
        /// </summary>
        public override async ValueTask DisposeAsync()
        {

            await sessions.CloseAllAsync().ConfigureAwait(false);

            await base.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

    }

}
