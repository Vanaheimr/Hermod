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
    /// The head of a response — its <c>:status</c> and header fields — surfaced by a
    /// streaming exchange (<see cref="HTTP2ClientStream"/>) as soon as the response
    /// HEADERS arrive, before (and independently of) its body.
    /// </summary>
    public sealed record HTTP2ResponseHead(int Status, List<(string Name, string Value)> Headers)
    {

        public string? HeaderValue(string Name)
            => Headers.FirstOrDefault(h => h.Name == Name).Value;

        /// <summary>
        /// The <c>ETag</c>, if the server sent one (RFC 9110, Section 8.8.3).
        /// </summary>
        public string? ETag
            => HeaderValue("etag");

        /// <summary>
        /// The <c>Last-Modified</c> date, if present and parseable (Section 8.8.2).
        /// </summary>
        public DateTimeOffset? LastModified
            => HeaderValue("last-modified") is String value &&
               HTTPValidators.TryParseDate(value, out var parsed)
                   ? parsed
                   : null;

        /// <summary>
        /// The strongest validator this response offers, in the form it should be
        /// echoed back in a precondition: the <c>ETag</c> if there is one, otherwise
        /// the <c>Last-Modified</c> date. Null if the server offered neither, in
        /// which case a range request cannot be made safe and a download must not be
        /// resumed (RFC 9110, Section 13.1.5).
        /// </summary>
        public string? Validator
            => ETag ?? (HeaderValue("last-modified"));

        /// <summary>
        /// Whether the server advertised byte-range support (<c>Accept-Ranges:
        /// bytes</c>, Section 14.3). Advisory: a server may still honour a range
        /// without saying so, and may still ignore one after saying so.
        /// </summary>
        public bool AcceptsByteRanges
            => HeaderValue("accept-ranges")?.Contains("bytes", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// The parsed <c>Content-Range</c> of a 206 or 416, if present and well-formed.
        /// </summary>
        public HTTPContentRange? ContentRange
            => HeaderValue("content-range") is String value &&
               HTTPContentRange.TryParse(value, out var parsed)
                   ? parsed
                   : null;

    }

}
