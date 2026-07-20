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

    using System.Globalization;

    /// <summary>
    /// A response stored in an <see cref="HTTP2CachingClient"/>'s cache, with the
    /// timing metadata RFC 9111 Section 4.2.3 needs to compute its age, and the
    /// request-header values that select it (for <c>Vary</c> matching).
    /// </summary>
    public sealed class HTTPStoredResponse
    {

        public required int                               Status         { get; set; }
        public required List<(string Name, string Value)> Headers        { get; set; }
        public required byte[]                            Body           { get; set; }

        /// <summary>When the cache sent the request that produced this response (for the age correction, Section 4.2.3).</summary>
        public required DateTimeOffset                    RequestTime    { get; set; }
        /// <summary>When the cache received this response (the reference point for resident time).</summary>
        public required DateTimeOffset                    ResponseTime   { get; set; }

        /// <summary>The values of the request-header fields named by this response's <c>Vary</c>, captured at store time.</summary>
        public required List<(string Name, string Value)> VaryKeyHeaders { get; set; }

        public string? Header(string Name) => Headers.FirstOrDefault(h => h.Name == Name).Value;

        public string?             ETag         => Header("etag");
        public string?             LastModified => Header("last-modified");
        public HTTPCacheControl    CacheControl => HTTPCacheControl.FromHeaders(Headers);

        public DateTimeOffset?     DateValue    => HTTPCache.TryParseHttpDate(Header("date"));
        public long                AgeValue     => long.TryParse(Header("age"), out var a) && a >= 0 ? a : 0;

    }

}
