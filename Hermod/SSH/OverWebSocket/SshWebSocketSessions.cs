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

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The SSH connections of a WebSocket endpoint or server that speaks SSH over
    /// WebSocket: what <see cref="SshWebSocketEndpoint"/> on an HTTP path and
    /// <see cref="SshWebSocketServer"/> on a port of its own - or on another server's,
    /// routed there by the subprotocol - have in common.
    /// </summary>
    /// <remarks>
    /// For a WebSocket server of another kind that is to speak SSH over WebSocket as
    /// well - one that knows its clients its own way, say: made in its constructor,
    /// and handed its binary and text messages from its overrides of
    /// ProcessBinaryMessage and ProcessTextMessage.
    /// </remarks>
    public sealed class SshWebSocketSessions
    {

        #region Data

        private readonly AWebSocketEndpoint                                                   owner;
        private readonly SshWebSocketConnectionHandler                                        onConnection;
        private readonly Func<Int32>                                                          messageSize;
        private readonly Func<Int64>                                                          receiveBuffer;
        private readonly ILogger                                                              logger;

        private readonly ConcurrentDictionary<WebSocketServerConnection, WebSocketSshBridge>  bridges   = new ();
        private readonly ConcurrentDictionary<WebSocketServerConnection, Task>                handlers  = new ();

        #endregion

        #region Properties

        /// <summary>
        /// How many SSH connections run now.
        /// </summary>
        public Int32  Count
            => handlers.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Take on the SSH connections of the given endpoint: it takes the subprotocol
        /// <see cref="SshOverWebSocket.Subprotocol"/> and nothing else, never
        /// compresses, and hands every connection to the given handler.
        /// </summary>
        /// <param name="Owner">The endpoint or server the connections come in at.</param>
        /// <param name="OnConnection">What to do with every SSH connection that comes in.</param>
        /// <param name="MessageSize">The most bytes of the SSH stream one binary message sent carries, asked for every connection.</param>
        /// <param name="ReceiveBuffer">How many received bytes SSH may leave unread, asked for every connection.</param>
        /// <param name="Logger">Where to say what went wrong.</param>
        public SshWebSocketSessions(AWebSocketEndpoint             Owner,
                                    SshWebSocketConnectionHandler  OnConnection,
                                    Func<Int32>                    MessageSize,
                                    Func<Int64>                    ReceiveBuffer,
                                    ILogger                        Logger)
        {

            this.owner                     = Owner;
            this.onConnection              = OnConnection;
            this.messageSize               = MessageSize;
            this.receiveBuffer             = ReceiveBuffer;
            this.logger                    = Logger;

            // A client that does not offer SSH over WebSocket is answered 400, and
            // what it sends is never read as SSH.
            Owner.RequireMatchingSubprotocol     = true;
            Owner.EnablePerMessageDeflate        = false;

            Owner.MaxBinaryMessageSizeIn         = SshOverWebSocket.MaxIncomingMessageSize;
            Owner.MaxTextMessageSizeIn           = 125;

            Owner.OnValidateWebSocketConnection += OffersSSH;
            Owner.OnNewWebSocketConnection      += Started;
            Owner.OnTCPConnectionClosed         += Closed;

        }

        #endregion


        #region (private) OffersSSH (...)

        // RequireMatchingSubprotocol turns away a client that offers other
        // subprotocols alone; one that offers none at all would be taken, and what
        // it sends read as SSH. Here SSH over WebSocket has to be asked for.
        private Task<HTTPResponse?> OffersSSH(DateTimeOffset             Timestamp,
                                              AWebSocketEndpoint         Server,
                                              WebSocketServerConnection  Connection,
                                              EventTracking_Id           EventTrackingId,
                                              CancellationToken          CancellationToken)
        {

            if (Connection.HTTPRequest is not HTTPRequest request ||
                request.SecWebSocketProtocol.Contains(SshOverWebSocket.Subprotocol))
            {
                return Task.FromResult<HTTPResponse?>(null);
            }

            return Task.FromResult<HTTPResponse?>(
                       new HTTPResponse.Builder(request) {
                           HTTPStatusCode  = HTTPStatusCode.BadRequest,
                           Server          = owner.HTTPServiceName,
                           Connection      = ConnectionType.Close,
                           ContentType     = HTTPContentType.Text.PLAIN,
                           Content         = $"SSH over WebSocket is asked for by the subprotocol '{SshOverWebSocket.Subprotocol}', and nothing else is spoken here.".ToUTF8Bytes()
                       }.AsImmutable
                   );

        }

        #endregion


        #region BinaryReceived (Connection, BinaryMessage, CancellationToken)

        /// <summary>
        /// Hand the bytes of a binary message to SSH - waited for, so that the
        /// connection is read no further while SSH has left too much unread.
        /// </summary>
        public Task BinaryReceived(WebSocketServerConnection  Connection,
                                   Byte[]                     BinaryMessage,
                                   CancellationToken          CancellationToken)

            => BridgeOf(Connection).ReceivedAsync(BinaryMessage, CancellationToken);

        #endregion

        #region TextReceived   (Connection, CancellationToken)

        /// <summary>
        /// A text message: SSH is carried in binary messages alone, and the connection is closed with 1003.
        /// </summary>
        public Task TextReceived(WebSocketServerConnection  Connection,
                                 CancellationToken          CancellationToken)

            => BridgeOf(Connection).TextReceivedAsync(CancellationToken);

        #endregion


        #region (private) BridgeOf (Connection)

        // One bridge a connection, made by whatever comes first - the connection's
        // start, or a message that came in before the start was told of.
        private WebSocketSshBridge BridgeOf(WebSocketServerConnection Connection)

            => bridges.GetOrAdd(Connection,
                                connection => {

                                    connection.MaxBinaryMessageSizeIn = SshOverWebSocket.MaxIncomingMessageSize;

                                    var bridge = new WebSocketSshBridge(connection,
                                                                        connection.LocalSocket,
                                                                        connection.RemoteSocket,
                                                                        messageSize(),
                                                                        receiveBuffer());

                                    // However the connection ends, it ends what SSH reads.
                                    connection.CancellationTokenSource.Token.Register(() => _ = bridge.EndAsync());

                                    return bridge;

                                });

        #endregion

        #region (private) Started  (...)

        // The connection is a WebSocket now: SSH runs on it off the connection's own
        // loop, which has to go on reading for SSH to read anything.
        private Task Started(DateTimeOffset             Timestamp,
                             AWebSocketEndpoint         Server,
                             WebSocketServerConnection  Connection,
                             IEnumerable<String>        SharedSubprotocols,
                             String?                    SelectedSubprotocol,
                             EventTracking_Id           EventTrackingId,
                             CancellationToken          CancellationToken)
        {

            var bridge  = BridgeOf(Connection);
            var token   = Connection.CancellationTokenSource.Token;

            handlers[Connection] = Task.Run(async () => {

                try
                {
                    await onConnection(new SshWebSocketConnection(bridge.Pipe, Connection), token).ConfigureAwait(false);
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "SSH over the WebSocket from {RemoteSocket} ended with an error.", Connection.RemoteSocket);
                }
                catch
                { }
                finally
                {

                    // What ran on it is done: so is the WebSocket.
                    await bridge.CloseAsync(MessageCloseCode.NormalClosure, null).ConfigureAwait(false);

                    handlers.TryRemove(Connection, out _);
                    bridges. TryRemove(Connection, out _);

                }

            });

            return Task.CompletedTask;

        }

        #endregion

        #region (private) Closed   (...)

        private Task Closed(DateTimeOffset             Timestamp,
                            AWebSocketEndpoint         Server,
                            WebSocketServerConnection  Connection,
                            EventTracking_Id           EventTrackingId,
                            String?                    Reason,
                            CancellationToken          CancellationToken)
        {

            if (bridges.TryGetValue(Connection, out var bridge))
                _ = bridge.EndAsync();

            // A connection that never became a WebSocket has no handler to remove it.
            if (!handlers.ContainsKey(Connection))
                bridges.TryRemove(Connection, out _);

            return Task.CompletedTask;

        }

        #endregion


        #region CloseAllAsync()

        /// <summary>
        /// Close every connection, and wait up to ten seconds for what ran on them to end.
        /// </summary>
        public async Task CloseAllAsync()
        {

            foreach (var bridge in bridges.Values)
                await bridge.CloseAsync(MessageCloseCode.GoingAway, null).ConfigureAwait(false);

            try
            {
                await Task.WhenAll(handlers.Values).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch
            { }

        }

        #endregion

    }

}
