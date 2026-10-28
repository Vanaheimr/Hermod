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
    /// A Content-Digest on every response that carries content (RFC 9530,
    /// Section 2), for every handler at once — the HTTP/1.x counterpart of
    /// HTTPSemantics.Wrap(…, ContentDigests: true) on HTTP/2.
    ///
    /// The digest is of the octets as they travel, and a digest of the identity
    /// form on a gzipped response is one no client can reproduce. It runs after
    /// <see cref="HTTPContentCompression"/>, which would otherwise have to
    /// compute it again over what it compresses, as it does a handler's. The
    /// algorithm is the one the request's Want-Content-Digest prefers, sha-256
    /// when it states no preference, and none at all when it rules out
    /// everything this stack computes.
    /// </summary>
    public static class HTTPContentDigests
    {

        #region (static) ShouldDigest(Request, Response, out Algorithm)

        /// <summary>
        /// Whether this response gets a Content-Digest, and in which algorithm.
        /// </summary>
        /// <param name="Request">The request this is an answer to.</param>
        /// <param name="Response">The response as it is about to be sent.</param>
        /// <param name="Algorithm">The algorithm to use, when this returns true.</param>
        public static Boolean ShouldDigest(HTTPRequest   Request,
                                           HTTPResponse  Response,
                                           out String?   Algorithm)
        {

            Algorithm = null;

            // A digest the handler chose is the handler's to stand behind.
            if (Response.ContentDigest is not null)
                return false;

            // As in HTTPContentCompression, everything that can be decided from
            // the header is decided before HTTPBody is asked, which drains a body
            // stream — and a streamed body is a chunk worker's or an event
            // stream's, whose octets are not known before they are sent.
            if (Response.HTTPBodyStream is not null ||
                Response.UpgradeWorker  is not null ||
                Response.ContentType    == HTTPContentType.Text.EVENTSTREAM ||
                Response.IsChunkedTransferEncoding ||
                Response.AutomaticallyChunkContent)
                return false;

            // No content is sent, so there is nothing for a Content-Digest to be
            // of. A HEAD could be given the digest of the GET's content, but that
            // is a claim about octets this response does not carry.
            if (Request.HTTPMethod == HTTPMethod.HEAD ||
                Response.HTTPStatusCode.Code is >= 100 and < 200 or 204 or 205 or 304)
                return false;

            Algorithm = HTTPDigest.SelectAlgorithm(Request.WantContentDigest);

            return Algorithm is not null;

        }

        #endregion

        #region (static) Apply(Request, Response)

        /// <summary>
        /// The given response with a Content-Digest of its content, or unchanged
        /// if it should not get one.
        /// </summary>
        /// <param name="Request">The request this is an answer to.</param>
        /// <param name="Response">The response as it is about to be sent.</param>
        public static HTTPResponse Apply(HTTPRequest   Request,
                                         HTTPResponse  Response)
        {

            if (!ShouldDigest(Request, Response, out var algorithm) ||
                algorithm is null)
            {
                return Response;
            }

            var body    = Response.HTTPBody ?? [];

            var builder = new HTTPResponse.Builder(
                              Response.HTTPRequest ?? Request,
                              Response.Timestamp,
                              Response.Runtime
                          ) {
                              HTTPStatusCode = Response.HTTPStatusCode
                          };

            // Copied rather than mutated, for the reason HTTPContentCompression
            // gives: a response serialises from the header text it was built with.
            foreach (var field in Response)
                builder.Set(field.Key, field.Value);

            builder.Content        = body;
            builder.ContentLength  = (UInt64) body.LongLength;
            builder.ContentDigest  = HTTPDigest.FieldValue(body, algorithm);

            return builder.AsImmutable;

        }

        #endregion

    }

}
