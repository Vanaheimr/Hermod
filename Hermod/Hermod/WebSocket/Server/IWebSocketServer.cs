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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Illias;

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{
    public interface IWebSocketServer
    {
        bool DisableWebSocketPings { get; set; }
        DNSClient? DNSClient { get; }
        string HTTPServiceName { get; }
        IIPAddress IPAddress { get; }
        IPPort IPPort { get; }
        IPSocket IPSocket { get; }
        bool IsRunning { get; }
        HashSet<string> SecWebSocketProtocols { get; }
        bool ServerThreadIsBackground { get; }
        ServerThreadNameCreatorDelegate ServerThreadNameCreator { get; }
        ServerThreadPriorityDelegate ServerThreadPrioritySetter { get; }
        TimeSpan? SlowNetworkSimulationDelay { get; set; }
        IEnumerable<WebSocketServerConnection> WebSocketConnections { get; }
        TimeSpan WebSocketPingEvery { get; set; }

        event OnWebSocketBinaryMessageDelegate? OnBinaryMessageReceived;
        event OnWebSocketBinaryMessageDelegate? OnBinaryMessageSent;
        event OnCloseMessageDelegate? OnCloseMessageReceived;
        event HTTPRequestLogDelegate? OnHTTPRequest;
        event HTTPResponseLogDelegate? OnHTTPResponse;
        event OnNewTCPConnectionDelegate? OnNewTCPConnection;
        event OnNewWebSocketConnectionDelegate? OnNewWebSocketConnection;
        event OnWebSocketFrameDelegate? OnPingMessageReceived;
        event OnWebSocketFrameDelegate? OnPingMessageSent;
        event OnWebSocketFrameDelegate? OnPongMessageReceived;
        event OnServerStartedDelegate? OnServerStarted;
        event OnTCPConnectionClosedDelegate? OnTCPConnectionClosed;
        event OnWebSocketTextMessageDelegate? OnTextMessageReceived;
        event OnWebSocketTextMessageDelegate? OnTextMessageSent;
        event OnValidateTCPConnectionDelegate? OnValidateTCPConnection;
        event OnValidateWebSocketConnectionDelegate? OnValidateWebSocketConnection;
        event OnWebSocketFrameDelegate? OnWebSocketFrameReceived;
        event OnWebSocketFrameDelegate? OnWebSocketFrameSent;

        Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime RequestTimestamp, WebSocketServerConnection Connection, byte[] BinaryMessage, EventTracking_Id EventTrackingId, CancellationToken CancellationToken);
        Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime RequestTimestamp, WebSocketServerConnection Connection, string TextMessage, EventTracking_Id EventTrackingId, CancellationToken CancellationToken);
        bool RemoveConnection(WebSocketServerConnection Connection);
        Task<SendStatus> SendBinaryMessage(WebSocketServerConnection Connection, byte[] BinaryMessage, EventTracking_Id? EventTrackingId = null);
        Task<SendStatus> SendTextMessage(WebSocketServerConnection Connection, string TextMessage, EventTracking_Id? EventTrackingId = null);
        Task<SendStatus> SendWebSocketFrame(WebSocketServerConnection Connection, WebSocketFrame WebSocketFrame, EventTracking_Id? EventTrackingId = null);
        void Shutdown(string? Message = null, bool Wait = true);
        void Start();
    }
}