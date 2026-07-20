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

    using System.Threading.Channels;

    /// <summary>
    /// One item of body/tunnel bytes queued on a stream's <see cref="HTTP2OutboundQueue"/>,
    /// waiting for the connection's single writer loop to send it as DATA frame(s).
    /// </summary>
    internal sealed class HTTP2OutboundItem
    {
        public required byte[]               Data        { get; init; }
        public required bool                 EndStream   { get; init; }
        public          int                  Offset;
        public required TaskCompletionSource Completion  { get; init; }

        /// <summary>
        /// Trailer fields to send as a trailing HEADERS block (with END_STREAM)
        /// once this item's DATA has gone out — set only on the final,
        /// end-of-stream item of a response that carries trailers (RFC 9113,
        /// Section 8.1). Null for an ordinary item.
        /// </summary>
        public List<(string Name, string Value)>? Trailers { get; init; }
    }

}
