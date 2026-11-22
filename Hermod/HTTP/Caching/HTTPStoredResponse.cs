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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A response stored in a cache, with the timing metadata RFC 9111, Section
    /// 4.2.3 needs to compute its age, and the request-header values that select
    /// it (for <c>Vary</c> matching). Field names are lowercase, as HTTP/2 and
    /// HTTP/3 send them, and an HTTP/1.x cache lowercases them when it stores.
    /// </summary>
    public sealed class HTTPStoredResponse
    {

        public required Int32                              Status          { get; set; }
        public required List<(String Name, String Value)>  Headers         { get; set; }
        public required Byte[]                             Body            { get; set; }

        /// <summary>
        /// When the cache sent the request that produced this response (for the age correction, Section 4.2.3).
        /// </summary>
        public required DateTimeOffset                     RequestTime     { get; set; }

        /// <summary>
        /// When the cache received this response (the reference point for resident time).
        /// </summary>
        public required DateTimeOffset                     ResponseTime    { get; set; }

        /// <summary>
        /// The values of the request-header fields named by this response's <c>Vary</c>, captured at store time.
        /// </summary>
        public required List<(String Name, String Value)>  VaryKeyHeaders  { get; set; }


        public String? Header(String Name)
            => Headers.FirstOrDefault(header => header.Name == Name).Value;

        public String?           ETag
            => Header("etag");

        public String?           LastModified
            => Header("last-modified");

        public HTTPCacheControl  CacheControl
            => HTTPCacheControl.FromHeaders(Headers);

        public DateTimeOffset?   DateValue
            => HTTPCache.TryParseHTTPDate(Header("date"));

        /// <summary>
        /// The Age field's value, 0 when there is none or it is not delta-seconds
        /// (RFC 9111, Section 5.1), 2^31 when it is too large to represent.
        /// Age is a singleton, and of a list Section 5.1 says a cache SHOULD use
        /// the first member and discard the rest: <c>Age: 7200, 0</c> is 7200,
        /// whether on one field line or on two. Empty members do not count
        /// (RFC 9110, Section 5.6.1), and a first member that is invalid makes
        /// the whole field invalid rather than letting a later one stand in.
        /// </summary>
        public Int64             AgeValue
        {
            get
            {

                var age = Headers.Where     (header => header.Name == "age")
                                 .SelectMany(header => header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                                 .FirstOrDefault();

                if (String.IsNullOrEmpty(age) || !age.All(Char.IsAsciiDigit))
                    return 0;

                return age.Length > 10 || !Int64.TryParse(age, out var seconds)
                           ? HTTPCacheControl.MaxDeltaSeconds
                           : Math.Min(seconds, HTTPCacheControl.MaxDeltaSeconds);

            }
        }

    }

}
