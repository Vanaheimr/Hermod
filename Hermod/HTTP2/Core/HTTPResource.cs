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
    /// A representation of a resource, for the purposes of GET/HEAD/OPTIONS and
    /// the conditional/range machinery in <see cref="HTTPSemantics"/>. Deliberately
    /// has no notion of streams, frames, or HPACK — RFC 9110 (HTTP Semantics) is
    /// version-independent, shared unchanged across HTTP/1.1, HTTP/2, and HTTP/3;
    /// this type and <see cref="HTTPSemantics"/> could sit in front of any of
    /// them without modification.
    /// </summary>
    public sealed class HTTPResource
    {
        public required byte[]         Body          { get; init; }

        /// <summary>
        /// The media type (RFC 9110, Section 8.3) — the "Accept" negotiation axis.
        /// May carry parameters (e.g. "text/plain; charset=utf-8"); only the
        /// type/subtype before the first ';' participates in Accept matching, but
        /// the whole string is sent as the Content-Type of the response.
        /// </summary>
        public required string         ContentType   { get; init; }

        /// <summary>
        /// The content coding already applied to <see cref="Body"/> (RFC 9110,
        /// Section 8.4) — the "Accept-Encoding" negotiation axis, e.g. "gzip".
        /// Null means the body is unencoded ("identity"). An app can pre-provide
        /// encoded variants here (picked by negotiation among the variants it
        /// supplies); separately, <see cref="HTTPSemantics.Wrap"/> can compress an
        /// identity body on the fly when <c>CompressResponses</c> is enabled — a
        /// pre-encoded variant is never re-compressed.
        /// </summary>
        public string?                 ContentEncoding { get; init; }

        /// <summary>
        /// The language of the representation (RFC 9110, Section 8.5) — the
        /// "Accept-Language" negotiation axis, e.g. "en" or "de". Null means the
        /// representation isn't language-specific (acceptable for any
        /// Accept-Language).
        /// </summary>
        public string?                 Language      { get; init; }

        /// <summary>
        /// A strong validator (RFC 9110, Section 8.8.3). If null, one is derived
        /// from the body's content hash — stable for as long as the body doesn't
        /// change, which is all a strong ETag promises. Because the default is a
        /// hash of the body, two negotiated variants (e.g. a German and an English
        /// body) get distinct ETags for free, keeping conditional requests correct
        /// across variants of the same URL.
        /// </summary>
        public string?                 ETag          { get; init; }

        public DateTimeOffset?         LastModified  { get; init; }

        /// <summary>
        /// An optional identifier for a resource that corresponds to this
        /// representation (RFC 9110, Section 8.7; RFC 10008, Section 3, for QUERY:
        /// "a successful response can include a Content-Location header field
        /// containing an identifier for a resource corresponding to the results of
        /// the operation" — a URI a client can later GET instead of resending the
        /// query). Emitted as <c>Content-Location</c> on a 200/206. Null = omitted.
        /// </summary>
        public string?                 ContentLocation { get; init; }
    }

}
