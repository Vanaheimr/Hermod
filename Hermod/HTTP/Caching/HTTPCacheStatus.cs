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
    /// Where a response a client's cache handed back came from: what RFC 9211's
    /// Cache-Status field reports for an intermediary, kept on the response for
    /// a client that has no one to report it to.
    /// </summary>
    public enum HTTPCacheStatus
    {

        /// <summary>
        /// Nothing usable was stored: the request went to the origin, and its
        /// response is handed on, stored or not.
        /// </summary>
        Miss,

        /// <summary>
        /// A fresh stored response, served without contacting the origin.
        /// </summary>
        Hit,

        /// <summary>
        /// A stale stored response, served because the request's max-stale,
        /// the response's stale-while-revalidate or a stale-if-error allowed it
        /// (RFC 9111, Section 4.2.4; RFC 5861).
        /// </summary>
        Stale,

        /// <summary>
        /// A stored response the origin confirmed with a 304 (RFC 9111, Section 4.3.4).
        /// </summary>
        Revalidated,

        /// <summary>
        /// The request said only-if-cached and nothing stored could answer it:
        /// the cache's own 504 (RFC 9111, Section 5.2.1.7).
        /// </summary>
        Unavailable

    }

}
