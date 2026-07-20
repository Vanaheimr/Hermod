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
    /// RFC 7692 "permessage-deflate" negotiation at the WebSocket opening-handshake
    /// layer. This stack implements the extension only in <i>no-context-takeover</i>
    /// mode (both directions) — the mode a fixed-window <see cref="System.IO.Compression.DeflateStream"/>
    /// can honor by compressing each message independently — so both the offer it
    /// makes and the response it returns always carry
    /// <c>client_no_context_takeover; server_no_context_takeover</c>.
    ///
    /// The helpers are deliberately transport-agnostic: they operate on the raw
    /// <c>Sec-WebSocket-Extensions</c> header <i>value</i>, not on any particular
    /// handshake, so the same negotiation serves the HTTP/2 RFC 8441 CONNECT path
    /// (the header is an ordinary field on the CONNECT request/response), a classic
    /// HTTP/1.1 Upgrade handshake, or any future transport. The parsed result flips
    /// <see cref="WebSocketConnection"/>'s <c>PerMessageDeflate</c> flag on.
    /// </summary>
    public static class WebSocketDeflate
    {

        /// <summary>The permessage-deflate extension token (RFC 7692 Section 7).</summary>
        public const string ExtensionName = "permessage-deflate";

        /// <summary>
        /// The <c>Sec-WebSocket-Extensions</c> value a client offers to request
        /// permessage-deflate in the only mode this stack supports.
        /// </summary>
        public const string Offer = "permessage-deflate; client_no_context_takeover; server_no_context_takeover";

        /// <summary>
        /// The <c>Sec-WebSocket-Extensions</c> value a server echoes back when it
        /// accepts the offer (RFC 7692 Section 5.1).
        /// </summary>
        public const string Response = "permessage-deflate; server_no_context_takeover; client_no_context_takeover";

        /// <summary>
        /// Server side: whether a client's <c>Sec-WebSocket-Extensions</c> offer
        /// includes permessage-deflate. When it does, <paramref name="ResponseValue"/>
        /// is the header value to echo back on the accepting response; otherwise it
        /// is null and the connection runs uncompressed.
        /// </summary>
        public static bool ShouldAccept(string? ClientOffer, out string? ResponseValue)
        {
            if (Lists(ClientOffer))
            {
                ResponseValue = Response;
                return true;
            }
            ResponseValue = null;
            return false;
        }

        /// <summary>
        /// Client side: whether a server's <c>Sec-WebSocket-Extensions</c> response
        /// accepted permessage-deflate.
        /// </summary>
        public static bool WasAccepted(string? ServerResponse)
            => Lists(ServerResponse);

        /// <summary>Whether a comma-separated Sec-WebSocket-Extensions value lists permessage-deflate.</summary>
        private static bool Lists(string? HeaderValue)
            => HeaderValue is not null &&
               HeaderValue.Split(',').Any(e => e.Trim().StartsWith(ExtensionName, StringComparison.OrdinalIgnoreCase));

    }

}
