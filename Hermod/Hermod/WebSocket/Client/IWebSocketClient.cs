/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    #region Delegates

    public delegate Task  OnWebSocketClientFrameSentDelegate             (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame                     Frame,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientFrameReceivedDelegate         (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame                     Frame,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientTextMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          String                             TextMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientTextMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          String                             TextMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientBinaryMessageSentDelegate     (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             BinaryMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientBinaryMessageReceivedDelegate (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             BinaryMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientPingMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PingMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientPingMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PingMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientPongMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PongMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientPongMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PongMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientCloseMessageSentDelegate      (DateTime                           Timestamp,
                                                                          IWebSocketClient                   Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                          String?                            Reason,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientCloseMessageReceivedDelegate  (DateTime                           Timestamp,
                                                                          IWebSocketClient                   Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                          String?                            Reason,
                                                                          CancellationToken                  CancellationToken);

    #endregion


    /// <summary>
    /// The common interface of all HTTP WebSocket clients.
    /// </summary>
    public interface IWebSocketClient : IHTTPClient
    {

        #region Events

        /// <summary>
        /// An event sent whenever a text message was sent.
        /// </summary>
        event OnWebSocketClientTextMessageSentDelegate?         OnTextMessageSent;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        event OnWebSocketClientTextMessageReceivedDelegate?     OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        event OnWebSocketClientBinaryMessageSentDelegate?       OnBinaryMessageSent;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        event OnWebSocketClientBinaryMessageReceivedDelegate?   OnBinaryMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        event OnWebSocketClientPingMessageSentDelegate?         OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        event OnWebSocketClientPingMessageReceivedDelegate?     OnPingMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket pong frame was sent.
        /// </summary>
        event OnWebSocketClientPongMessageSentDelegate?         OnPongMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        event OnWebSocketClientPongMessageReceivedDelegate?     OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a HTTP WebSocket CLOSE frame was sent.
        /// </summary>
        event OnWebSocketClientCloseMessageSentDelegate?        OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a HTTP WebSocket CLOSE frame was received.
        /// </summary>
        event OnWebSocketClientCloseMessageReceivedDelegate     OnCloseMessageReceived;

        #endregion

        #region Properties
        IEnumerable<String>                                                 SecWebSocketProtocols         { get; }
        Boolean                                                             DisableWebSocketPings         { get; }
        TimeSpan                                                            WebSocketPingEvery            { get; }

        TimeSpan?                                                           SlowNetworkSimulationDelay    { get; }

        new RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator    { get; }

        #endregion

    }

}
