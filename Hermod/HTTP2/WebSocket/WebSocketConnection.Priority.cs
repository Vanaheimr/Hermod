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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// What a WebSocket over HTTP/2 has beyond the framing: reprioritization
    /// among the other streams of its connection (RFC 9218). A part of its own,
    /// because WebSocketConnection.cs has a copy under HTTP3/WebSocket/ that
    /// differs in the namespace line alone, and this part has none there.
    /// </summary>
    public sealed partial class WebSocketConnection
    {

        /// <summary>
        /// Reprioritize this WebSocket among the other streams of its HTTP/2
        /// connection (RFC 9218) — a client's: what it sends from now on, and what
        /// is still queued, goes out by the new priority, and the server is told
        /// with a PRIORITY_UPDATE frame, for what it sends back
        /// (<see cref="HTTP2ClientTunnel.UpdatePriorityAsync"/>). A WebSocket that
        /// uploads a log file in the background, say, and must now hurry. A
        /// server's WebSocket has no such signal to send: servers must not send
        /// PRIORITY_UPDATE (RFC 9218, Section 7.1), and it throws.
        /// </summary>
        public Task UpdatePriorityAsync(HTTP2Priority Priority, CancellationToken CancellationToken = default)

            => tunnel is HTTP2ClientTunnel clientTunnel
                   ? clientTunnel.UpdatePriorityAsync(Priority, CancellationToken)
                   : throw new NotSupportedException("Only a client's WebSocket can be reprioritized: servers must not send PRIORITY_UPDATE (RFC 9218, Section 7.1)");

    }

}
