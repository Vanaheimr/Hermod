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

using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A WebSocket client for SSH over WebSocket: it offers the subprotocol
    /// <see cref="SshOverWebSocket.Subprotocol"/> and nothing else, never
    /// compresses, never reconnects, and hands the connection on as the pipe SSH
    /// runs on - for <c>SSHClient.Connect</c> where this end is the SSH client, or
    /// for <c>SshServer.ConnectOut</c> where it is the SSH server, as a charging
    /// station is that can only connect out.
    /// </summary>
    /// <remarks>
    /// Who this end is to the HTTP server is said as by any WebSocket client: HTTP
    /// Basic or any other HTTP authentication, a one-time password - bound to the TLS
    /// connection or not -, a client certificate. Who is who is said again by SSH.
    /// </remarks>
    public class SshWebSocketClient : WebSocketClient
    {

        #region Data

        private readonly Lock                 bridging  = new ();
        private          WebSocketSshBridge?  bridge;

        #endregion

        #region Properties

        /// <summary>
        /// The most bytes of the SSH stream one binary message sent carries.
        /// </summary>
        public Int32  MessageSize    { get; init; } = SshOverWebSocket.DefaultMessageSize;

        /// <summary>
        /// How many received bytes SSH may leave unread before the connection is read no further.
        /// </summary>
        public Int64  ReceiveBuffer  { get; init; } = SshOverWebSocket.DefaultReceiveBuffer;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a WebSocket client for SSH over WebSocket to the given URL.
        /// </summary>
        /// <param name="URL">Where the endpoint is, as "wss://csms.example.org/ssh/CP001".</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication, e.g. HTTP Basic.</param>
        /// <param name="RemoteCertificateValidator">The validator of the server's TLS certificate.</param>
        /// <param name="ClientCertificates">Optional TLS client certificates.</param>
        /// <param name="TOTPConfig">An optional one-time password configuration, bound to the TLS connection where it says so.</param>
        /// <param name="TOTPLogin">The login the one-time password is sent for.</param>
        /// <param name="RequestTimeout">How long to wait for the answer to the upgrade.</param>
        /// <param name="DisableWebSocketPings">Whether to send no pings - SSH keepalives keep a proxy from cutting an idle connection as well.</param>
        /// <param name="WebSocketPingEvery">The ping interval.</param>
        /// <param name="ConnectTimeout">How long to wait for the TCP connection.</param>
        /// <param name="DNSClient">The DNS client to resolve the URL's host with.</param>
        public SshWebSocketClient(URL                                                             URL,
                                  IHTTPAuthentication?                                            HTTPAuthentication           = null,
                                  RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator   = null,
                                  IEnumerable<X509Certificate2>?                                  ClientCertificates           = null,
                                  TOTPConfig?                                                     TOTPConfig                   = null,
                                  String?                                                         TOTPLogin                    = null,
                                  TimeSpan?                                                       RequestTimeout               = null,
                                  Boolean                                                         DisableWebSocketPings        = false,
                                  TimeSpan?                                                       WebSocketPingEvery           = null,
                                  TimeSpan?                                                       ConnectTimeout               = null,
                                  IDNSClient?                                                     DNSClient                    = null)

            : base(URL,
                   HTTPAuthentication:          HTTPAuthentication,
                   SecWebSocketProtocols:       [ SshOverWebSocket.Subprotocol ],
                   RequestTimeout:              RequestTimeout,
                   DisableWebSocketPings:       DisableWebSocketPings,
                   WebSocketPingEvery:          WebSocketPingEvery,
                   RemoteCertificateValidator:  RemoteCertificateValidator,
                   ClientCertificates:          ClientCertificates,
                   TOTPConfig:                  TOTPConfig,
                   TOTPLogin:                   TOTPLogin,
                   ConnectTimeout:              ConnectTimeout,
                   DNSClient:                   DNSClient)

        {

            EnablePerMessageDeflate  = false;
            ReconnectPolicy          = null;
            MaxBinaryMessageSizeIn   = SshOverWebSocket.MaxIncomingMessageSize;

        }

        #endregion


        #region ConnectAsync (CancellationToken = default)

        /// <summary>
        /// Open the WebSocket and give back the pipe SSH runs on - once: a second call
        /// is refused, as a closed connection is not opened again.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="SshWebSocketException">The server did not take the upgrade, or not with SSH over WebSocket.</exception>
        public async Task<DuplexPipe> ConnectAsync(CancellationToken CancellationToken = default)
        {

            if (bridge is not null || Networking is not null)
                throw new InvalidOperationException("This client has been connected already; SSH over WebSocket opens one connection a client.");

            var (connection, response) = await Connect(CancellationToken: CancellationToken).ConfigureAwait(false);

            if (connection is null || response.HTTPStatusCode != HTTPStatusCode.SwitchingProtocols)
            {
                await Close().ConfigureAwait(false);
                throw new SshWebSocketException($"The server did not take the upgrade to SSH over WebSocket: {response.HTTPStatusCode}.", response.HTTPStatusCode);
            }

            if (connection.Subprotocol != SshOverWebSocket.Subprotocol)
            {
                await connection.Close(WebSocketFrame.ClosingStatusCode.ProtocolError, "SSH over WebSocket was not agreed on.").ConfigureAwait(false);
                await Close().ConfigureAwait(false);
                throw new SshWebSocketException($"The server agreed on '{connection.Subprotocol}' and not on '{SshOverWebSocket.Subprotocol}'.", response.HTTPStatusCode);
            }

            var made = BridgeFor(connection);

            // However the connection ends - a close frame, a lost TCP connection, a
            // close from here -, it ends what SSH reads.
            _ = Networking?.ContinueWith(_ => made.EndAsync(), TaskScheduler.Default);

            return made.Pipe;

        }

        #endregion

        #region (private) BridgeFor (Connection)

        // Made by whatever comes first: ConnectAsync, or the first message - an SSH
        // server at the other end says who it is as soon as the 101 is out, and that
        // may be read before Connect() has come back.
        private WebSocketSshBridge BridgeFor(WebSocketClientConnection Connection)
        {
            lock (bridging)
            {

                if (bridge is null)
                {

                    Connection.MaxBinaryMessageSizeIn = SshOverWebSocket.MaxIncomingMessageSize;

                    bridge = new WebSocketSshBridge(Connection,
                                                    Connection.LocalSocket,
                                                    Connection.RemoteSocket,
                                                    MessageSize,
                                                    ReceiveBuffer);

                }

                return bridge;

            }
        }

        #endregion


        #region ProcessBinaryMessage (Timestamp, Connection, BinaryFrame, EventTrackingId, BinaryMessage, CancellationToken)

        /// <summary>
        /// Hand the bytes of a binary message to SSH - waited for, so that the
        /// connection is read no further while SSH has left too much unread.
        /// </summary>
        public override async Task ProcessBinaryMessage(DateTimeOffset             Timestamp,
                                                        WebSocketClientConnection  Connection,
                                                        WebSocketFrame             BinaryFrame,
                                                        EventTracking_Id           EventTrackingId,
                                                        Byte[]                     BinaryMessage,
                                                        CancellationToken          CancellationToken)
        {

            await base.ProcessBinaryMessage(Timestamp, Connection, BinaryFrame, EventTrackingId, BinaryMessage, CancellationToken).ConfigureAwait(false);

            await BridgeFor(Connection).ReceivedAsync(BinaryMessage, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region ProcessTextMessage   (Timestamp, Connection, TextFrame,   EventTrackingId, TextMessage,   CancellationToken)

        /// <summary>
        /// A text message: SSH is carried in binary messages alone, and the connection is closed with 1003.
        /// </summary>
        public override async Task ProcessTextMessage(DateTimeOffset             Timestamp,
                                                      WebSocketClientConnection  Connection,
                                                      WebSocketFrame             TextFrame,
                                                      EventTracking_Id           EventTrackingId,
                                                      String                     TextMessage,
                                                      CancellationToken          CancellationToken)
        {

            await base.ProcessTextMessage(Timestamp, Connection, TextFrame, EventTrackingId, TextMessage, CancellationToken).ConfigureAwait(false);

            await BridgeFor(Connection).TextReceivedAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

    }


    /// <summary>
    /// SSH over WebSocket could not be begun: the server did not take the upgrade,
    /// or took it without the subprotocol of SSH over WebSocket.
    /// </summary>
    /// <param name="Message">What went wrong.</param>
    /// <param name="HTTPStatusCode">The HTTP status the server answered with.</param>
    public sealed class SshWebSocketException(String          Message,
                                              HTTPStatusCode  HTTPStatusCode) : IOException(Message)
    {

        /// <summary>
        /// The HTTP status the server answered with - 401 where the HTTP
        /// authentication was not accepted, 400 where SSH over WebSocket is not
        /// spoken there.
        /// </summary>
        public HTTPStatusCode HTTPStatusCode { get; } = HTTPStatusCode;

    }

}
