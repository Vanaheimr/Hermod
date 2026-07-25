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
    /// Trailer fields (RFC 9113, Section 8.1) — the trailing HEADERS block that may
    /// close a message after its content.
    ///
    /// The rules are the same whichever direction they travel in, which is why this
    /// sits in <c>Core</c>: a server sends response trailers, a client sends request
    /// trailers, and both must produce a block the other end will not treat as a
    /// protocol error. Getting it wrong is a connection-level failure at the peer,
    /// so it is checked before anything reaches the wire rather than after.
    /// </summary>
    public static class HTTP2Trailers
    {

        #region Validate (StreamId, Trailers)

        /// <summary>
        /// Reject a trailer list a peer would be entitled to kill the stream over:
        /// trailers carry no pseudo-header fields (they belong to the message's
        /// leading header section, which has already been sent), and — like every
        /// HTTP/2 field name — the names must be lowercase (RFC 9113, Section 8.2.1).
        /// </summary>
        /// <exception cref="HTTP2StreamException">A field name is a pseudo-header, empty, or not lowercase.</exception>
        public static void Validate(UInt32 StreamId, IEnumerable<(String Name, String Value)> Trailers)
        {

            foreach (var (name, _) in Trailers)
            {

                if (name.Length == 0 || name[0] == ':')
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                        "Trailers must not contain pseudo-header fields");

                foreach (var character in name)
                    if (character is >= 'A' and <= 'Z')
                        throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                            $"Trailer field name '{name}' must be lowercase");

            }

        }

        #endregion

    }

}
