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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3
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

        /// <summary>
        /// The permessage-deflate extension token (RFC 7692 Section 7).
        /// </summary>
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
        /// Server side: whether a client's <c>Sec-WebSocket-Extensions</c> offer is one
        /// this stack can actually honor. When it is, <paramref name="ResponseValue"/> is
        /// the header value to echo back on the accepting response; otherwise it is null
        /// and the connection runs uncompressed.
        ///
        /// The parameters are parsed rather than ignored, and that distinction is the
        /// whole point of this method. Until 2026-09-22 it returned true for any value
        /// whose text merely contained "permessage-deflate", so a client offering
        /// <c>server_max_window_bits=9</c> — capping the window this server may compress
        /// with — was answered "accepted", after which we compressed with the full 15-bit
        /// window anyway. A peer that had sized its inflate window to 9 bits could not
        /// have decoded that. RFC 7692 Section 7.1.2.1 is explicit: a server that cannot
        /// satisfy the offer must decline it, and the fallback is simply no compression.
        ///
        /// DeflateStream exposes no control over the window size, so 15 is the only value
        /// we can promise. The sibling HTTP/1.1 implementation
        /// (<c>WebSocketPerMessageDeflate.TryNegotiateAsServer</c>) has always done this;
        /// this is that logic, not a new policy.
        /// </summary>
        public static bool ShouldAccept(string? ClientOffer, out string? ResponseValue)
        {

            ResponseValue = null;

            if (ClientOffer is null)
                return false;

            // A client may stack several offers, comma-separated, most-preferred
            // first (RFC 7692 Section 5.1). Take the first one we can honor —
            // which is what makes an offer list containing both "with 9 bits" and
            // "without the parameter" negotiable rather than a flat refusal.
            foreach (var offer in ClientOffer.Split(','))
            {

                var parameters = offer.Split(';').Select(p => p.Trim()).ToArray();

                if (parameters.Length == 0 ||
                    !parameters[0].Equals(ExtensionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var canHonor = true;

                foreach (var parameter in parameters.Skip(1))
                {

                    var kv     = parameter.Split('=', 2);
                    var name   = kv[0].Trim();
                    var value  = kv.Length > 1 ? kv[1].Trim().Trim('"') : null;

                    // The only parameter that can make an offer un-honorable here.
                    // A bare or unparseable value is malformed per Section 7.1.2.1
                    // (it MUST carry 8..15), and is treated as un-honorable rather
                    // than silently ignored.
                    //
                    // client_max_window_bits needs no check: it constrains the
                    // *client's* window, and inflating with 15 decodes a stream
                    // produced with any smaller window, so we never have to care.
                    if (name.Equals("server_max_window_bits", StringComparison.OrdinalIgnoreCase) &&
                        (value is null ||
                         !Byte.TryParse(value, out var bits) ||
                         bits != 15))
                    {
                        canHonor = false;
                    }

                }

                if (!canHonor)
                    continue;

                ResponseValue = Response;
                return true;

            }

            return false;

        }

        /// <summary>
        /// Client side: whether a server's <c>Sec-WebSocket-Extensions</c> response
        /// accepted permessage-deflate.
        /// </summary>
        public static bool WasAccepted(string? ServerResponse)
            => Lists(ServerResponse);

        /// <summary>
        /// Whether a comma-separated Sec-WebSocket-Extensions value lists permessage-deflate.
        /// </summary>
        private static bool Lists(string? HeaderValue)
            => HeaderValue is not null &&
               HeaderValue.Split(',').Any(e => e.Trim().StartsWith(ExtensionName, StringComparison.OrdinalIgnoreCase));

    }

}
