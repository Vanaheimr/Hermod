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

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// What a WebSocket connection does when a send would push its outgoing
    /// backpressure (the number of bytes queued and in-flight but not yet written
    /// to the network) beyond the configured maximum — modelled after the
    /// "maxBackpressure" behaviour of uWebSockets.
    /// </summary>
    public enum WebSocketBackpressureBehaviour
    {

        /// <summary>
        /// Close the connection with a 1009 (Message Too Big) close frame. This is
        /// the safe default (and the uWebSockets default): a peer that cannot keep
        /// up must not be allowed to make the sender accumulate unbounded memory.
        /// </summary>
        CloseConnection,

        /// <summary>
        /// Drop the outgoing message (the send returns <see cref="SentStatus.Dropped"/>)
        /// but keep the connection open. Suitable for loss-tolerant streams where a
        /// stale message may simply be skipped.
        /// </summary>
        DropMessage

    }

}
