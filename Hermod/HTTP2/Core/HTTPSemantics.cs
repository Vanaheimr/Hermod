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
    using org.GraphDefined.Vanaheimr.Hermod.HTTP;

    /// <summary>
    /// RFC 9110's "core mechanics" — negotiation, preconditions, ranges, QUERY —
    /// as an <see cref="HTTP2RequestHandler"/>. The rules themselves are
    /// <see cref="HTTPResourceSemantics"/>, which knows no HTTP version and is what
    /// HTTP/1.x reaches through <see cref="HTTPResourceHandlers"/>; this class
    /// only reads <c>:method</c> and <c>:path</c> and hands the rest on.
    ///
    /// <see cref="Wrap(HTTPVariantHandler, Boolean, HTTPQueryHandler?, Boolean)"/>
    /// produces an ordinary <see cref="HTTP2RequestHandler"/>, so it plugs into
    /// <see cref="HTTP2Connection"/> exactly like a raw handler would — nothing here
    /// touches frames, streams, or HPACK.
    /// </summary>
    public static class HTTPSemantics
    {

        #region Wrap(VariantHandler, CompressResponses = false, QueryHandler = null, ContentDigests = false)

        /// <summary>
        /// Wrap a multi-variant handler with RFC 9110 GET/HEAD/OPTIONS semantics,
        /// proactive content negotiation, conditional requests, and Range support.
        /// See <see cref="HTTPResourceSemantics.Answer"/> for what each option does.
        /// </summary>
        /// <param name="VariantHandler">Produces every representation of a path.</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTP2RequestHandler Wrap(HTTPVariantHandler  VariantHandler,
                                               Boolean             CompressResponses   = false,
                                               HTTPQueryHandler?   QueryHandler        = null,
                                               Boolean             ContentDigests      = false)

            => (StreamId, RequestHeaders, RequestBody, CancellationToken) =>

                   HTTPResourceSemantics.Answer(
                       HTTPMethod.TryParse(RequestHeaders.FirstOrDefault(h => h.Name == ":method").Value) ?? HTTPMethod.GET,
                       RequestHeaders.FirstOrDefault(h => h.Name == ":path").Value ?? "/",
                       RequestHeaders,
                       RequestBody,
                       VariantHandler,
                       CompressResponses,
                       QueryHandler,
                       ContentDigests,
                       CancellationToken
                   );

        #endregion

        #region Wrap(ResourceHandler, CompressResponses = false, QueryHandler = null, ContentDigests = false)

        /// <summary>
        /// Single-representation convenience overload — an app that has exactly
        /// one representation per URL never has to think about variants.
        /// </summary>
        /// <param name="ResourceHandler">Produces the one representation of a path, or null.</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTP2RequestHandler Wrap(HTTPResourceHandler  ResourceHandler,
                                               Boolean              CompressResponses   = false,
                                               HTTPQueryHandler?    QueryHandler        = null,
                                               Boolean              ContentDigests      = false)

            => Wrap(HTTPResourceSemantics.Variants(ResourceHandler),
                    CompressResponses,
                    QueryHandler,
                    ContentDigests);

        #endregion

    }

}
