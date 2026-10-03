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
    /// Raised for any violation of RFC 6455's framing rules (bad mask bit,
    /// fragmented/oversized control frame, reserved bits set, oversized payload),
    /// and for a frame that would take its message past the size limit.
    /// Caught by <see cref="WebSocketConnection.ReceiveAsync"/>, which answers
    /// with a Close frame carrying <see cref="CloseCode"/> — 1002 ("protocol
    /// error") for the former, 1009 ("message too big") for the latter — and
    /// ends the connection.
    /// </summary>
    public sealed class WebSocketProtocolException(string Message, ushort CloseCode = 1002) : Exception(Message)
    {

        /// <summary>
        /// The status code of the Close frame that answers this (RFC 6455 Section 7.4.1).
        /// </summary>
        public ushort CloseCode { get; } = CloseCode;

    }

}
