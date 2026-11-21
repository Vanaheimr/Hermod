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
    /// RFC 9110's "core mechanics" on HTTP/1.x: a resource whose handler only says
    /// what its representations are, and whose 200, 206, 304, 406, 412 and 416, its
    /// ETag, Last-Modified, Vary and Content-Range all come from
    /// <see cref="HTTPResourceSemantics"/> — the same rules HTTP/2 applies through
    /// <see cref="HTTP2.HTTPSemantics"/>.
    ///
    /// Opt-in, as on HTTP/2. A handler registered with <c>AddHandler</c> still sees
    /// every precondition and range as typed fields and decides for itself; one
    /// registered with <see cref="AddResource(HTTPAPI, HTTPPath, HTTPVariantHandler, HTTPQueryHandler?, Boolean, Boolean)"/>
    /// gets the library's answer instead.
    ///
    /// <c>HEAD</c> and <c>OPTIONS</c> stay the server's: the server derives
    /// <c>HEAD</c> from the <c>GET</c> handler and calls it with the request's own
    /// method, so a conditional <c>HEAD</c> still gets its <c>304</c>, and it
    /// answers <c>OPTIONS</c> and the <c>405</c> from the methods registered.
    /// </summary>
    public static class HTTPResourceHandlers
    {

        #region Wrap(VariantHandler,  CompressResponses = false, QueryHandler = null, ContentDigests = false)

        /// <summary>
        /// An HTTP/1.x request handler answering for a resource with several
        /// representations: proactive negotiation, conditional requests and
        /// ranges. See <see cref="HTTPResourceSemantics.Answer"/> for each option.
        /// </summary>
        /// <param name="VariantHandler">Produces every representation of a path.</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTPDelegate Wrap(HTTPVariantHandler  VariantHandler,
                                        Boolean             CompressResponses   = false,
                                        HTTPQueryHandler?   QueryHandler        = null,
                                        Boolean             ContentDigests      = false)

            => async request => {

                   var (headers, body) = await HTTPResourceSemantics.Answer(
                                                   request.HTTPMethod,
                                                   PathOf(request),
                                                   FieldsOf(request),
                                                   request.HTTPBody,
                                                   VariantHandler,
                                                   CompressResponses,
                                                   QueryHandler,
                                                   ContentDigests,
                                                   request.CancellationToken
                                               );

                   return ResponseOf(request, headers, body);

               };

        #endregion

        #region Wrap(ResourceHandler, CompressResponses = false, QueryHandler = null, ContentDigests = false)

        /// <summary>
        /// An HTTP/1.x request handler answering for a resource with one
        /// representation: conditional requests and ranges.
        /// </summary>
        /// <param name="ResourceHandler">Produces the one representation of a path, or null.</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTPDelegate Wrap(HTTPResourceHandler  ResourceHandler,
                                        Boolean              CompressResponses   = false,
                                        HTTPQueryHandler?    QueryHandler        = null,
                                        Boolean              ContentDigests      = false)

            => Wrap(HTTPResourceSemantics.Variants(ResourceHandler),
                    CompressResponses,
                    QueryHandler,
                    ContentDigests);

        #endregion


        #region (internal) Answer       (Request, Resource, Decorate = null)

        /// <summary>
        /// Answer a request for one representation already in hand: the
        /// conditional requests and ranges of <see cref="HTTPResourceSemantics"/>,
        /// without a route of its own. For handlers that find the representation
        /// themselves, as the static-file handlers do (H-43).
        /// </summary>
        /// <param name="Request">The request.</param>
        /// <param name="Resource">The representation.</param>
        /// <param name="Decorate">Sets what the layer does not, Cache-Control say, on every answer.</param>
        internal static async Task<HTTPResponse> Answer(HTTPRequest                    Request,
                                                        HTTPResource                   Resource,
                                                        Action<HTTPResponse.Builder>?  Decorate   = null)
        {

            var (headers, body) = await HTTPResourceSemantics.Answer(
                                            Request.HTTPMethod,
                                            PathOf(Request),
                                            FieldsOf(Request),
                                            Request.HTTPBody,
                                            (path, requestHeaders, cancellationToken) => Task.FromResult<IReadOnlyList<HTTPResource>>([ Resource ]),
                                            CancellationToken: Request.CancellationToken
                                        );

            return ResponseOf(Request, headers, body, Decorate);

        }

        #endregion

        #region (internal) Preconditions(Request, ETag, LastModified, Decorate = null)

        /// <summary>
        /// The 304 or 412 that a request's preconditions call for (RFC 9110,
        /// Section 13.2.2), or null when the request is to be answered in full.
        /// For a representation too large to hold in memory, which is streamed and
        /// so cannot go through <see cref="Answer"/>.
        /// </summary>
        /// <param name="Request">The request.</param>
        /// <param name="ETag">The strong entity tag of the representation.</param>
        /// <param name="LastModified">When it was last modified, if known.</param>
        /// <param name="Decorate">Sets what the layer does not, on the answer.</param>
        internal static HTTPResponse? Preconditions(HTTPRequest                    Request,
                                                    String                         ETag,
                                                    DateTimeOffset?                LastModified,
                                                    Action<HTTPResponse.Builder>?  Decorate   = null)
        {

            var answer = HTTPResourceSemantics.EvaluateConditionalRequest(
                             FieldsOf(Request),
                             Request.HTTPMethod,
                             ETag,
                             LastModified
                         );

            return answer is null
                       ? null
                       : ResponseOf(Request, answer.Value.Headers, answer.Value.Body, Decorate);

        }

        #endregion


        #region AddResource(this API, URLTemplate, VariantHandler,  QueryHandler = null, CompressResponses = false, ContentDigests = false)

        /// <summary>
        /// Register a resource with several representations: <c>GET</c>, and with
        /// it <c>HEAD</c>, and <c>QUERY</c> when a query handler is given.
        /// </summary>
        /// <param name="API">The HTTP API.</param>
        /// <param name="URLTemplate">The URL template of the resource.</param>
        /// <param name="VariantHandler">Produces every representation of a path.</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTPAPI AddResource(this HTTPAPI        API,
                                          HTTPPath            URLTemplate,
                                          HTTPVariantHandler  VariantHandler,
                                          HTTPQueryHandler?   QueryHandler        = null,
                                          Boolean             CompressResponses   = false,
                                          Boolean             ContentDigests      = false)
        {

            var handler = Wrap(VariantHandler, CompressResponses, QueryHandler, ContentDigests);

            API.AddHandler(HTTPMethod.GET,   URLTemplate, handler);

            if (QueryHandler is not null)
                API.AddHandler(HTTPMethod.QUERY, URLTemplate, handler);

            return API;

        }

        #endregion

        #region AddResource(this API, URLTemplate, ResourceHandler, QueryHandler = null, CompressResponses = false, ContentDigests = false)

        /// <summary>
        /// Register a resource with one representation: <c>GET</c>, and with it
        /// <c>HEAD</c>, and <c>QUERY</c> when a query handler is given.
        /// </summary>
        /// <param name="API">The HTTP API.</param>
        /// <param name="URLTemplate">The URL template of the resource.</param>
        /// <param name="ResourceHandler">Produces the one representation of a path, or null.</param>
        /// <param name="QueryHandler">An optional QUERY handler (RFC 10008).</param>
        /// <param name="CompressResponses">Whether to compress a full 200 on the fly (RFC 9110, Section 8.4).</param>
        /// <param name="ContentDigests">Whether to use RFC 9530 digest fields in both directions.</param>
        public static HTTPAPI AddResource(this HTTPAPI         API,
                                          HTTPPath             URLTemplate,
                                          HTTPResourceHandler  ResourceHandler,
                                          HTTPQueryHandler?    QueryHandler        = null,
                                          Boolean              CompressResponses   = false,
                                          Boolean              ContentDigests      = false)

            => API.AddResource(URLTemplate,
                               HTTPResourceSemantics.Variants(ResourceHandler),
                               QueryHandler,
                               CompressResponses,
                               ContentDigests);

        #endregion


        #region (private) PathOf    (Request)

        /// <summary>
        /// The request target in origin form, query included, as HTTP/2's
        /// <c>:path</c> carries it: what the resource handler is given on either
        /// version: the target as sent, not one put together again from the
        /// parsed query string, which would encode it anew and order it by name.
        /// A target in absolute form is given as its path and query, as RFC 9112,
        /// Section 3.2.2 has the server take its authority for Host. Asterisk form
        /// is answered by the server itself, so the fallback is there for a
        /// request built in process.
        /// </summary>
        private static String PathOf(HTTPRequest Request)

            => Request.OriginFormTarget?.StartsWith('/') == true
                   ? Request.OriginFormTarget
                   : Request.Path.ToString();

        #endregion

        #region (private) FieldsOf  (Request)

        /// <summary>
        /// The request's header fields as the version-neutral layer reads them:
        /// names in lower case, as HTTP/2 and HTTP/3 carry them, and a field sent
        /// on several lines as one value joined with commas, which RFC 9110,
        /// Section 5.3 makes the same field.
        /// </summary>
        /// <remarks>
        /// The server's own parser joins a list field sent on several lines as it
        /// reads it, and refuses a repeated field that is not one, so from the
        /// wire a value is always one String. A String[] comes only from a request
        /// built in process, and is joined the same way.
        /// </remarks>
        private static List<(String Name, String Value)> FieldsOf(HTTPRequest Request)
        {

            var fields = new List<(String Name, String Value)>();

            foreach (var field in Request)
                fields.Add((
                    field.Key.ToLowerInvariant(),
                    field.Value switch {
                        String   text   => text,
                        String[] values => String.Join(", ", values),
                        var      other  => other.ToString() ?? ""
                    }
                ));

            return fields;

        }

        #endregion

        #region (private) ResponseOf(Request, Headers, Body)

        /// <summary>
        /// The layer's answer as an HTTP/1.x response.
        /// </summary>
        /// <remarks>
        /// A response the layer gives no content but a Content-Length is the answer
        /// to a HEAD: the length is the one the GET would have had (RFC 9110,
        /// Section 9.3.2), and stays on the response without content. Every other
        /// field goes on verbatim under its usual spelling, Content-Type included:
        /// the typed HTTPContentType keeps a charset and nothing else, so a
        /// representation's own parameters — a JSON-LD profile, say — would be
        /// lost, and it does not parse a multipart boundary at all.
        /// </remarks>
        private static HTTPResponse ResponseOf(HTTPRequest                        Request,
                                               List<(String Name, String Value)>  Headers,
                                               Byte[]?                            Body,
                                               Action<HTTPResponse.Builder>?      Decorate   = null)
        {

            var builder = new HTTPResponse.Builder(Request) {
                              HTTPStatusCode = HTTPStatusCode.ParseString(Headers.First(h => h.Name == ":status").Value)
                          };

            String? contentLength = null;

            foreach (var (name, value) in Headers)
            {

                if (name == ":status")
                    continue;

                if (name == "content-length")
                {
                    contentLength = value;
                    continue;
                }

                builder.Set(Spelling(name), value);

            }

            if (Body is not null)
                builder.Content       = Body;

            else if (contentLength is not null && UInt64.TryParse(contentLength, out var length))
                builder.ContentLength = length;

            Decorate?.Invoke(builder);

            return builder.AsImmutable;

        }

        #endregion

        #region (private) Spelling  (Name)

        /// <summary>
        /// A field name as HTTP/1.x usually spells it. Field names are
        /// case-insensitive (RFC 9110, Section 5.1); this is for whoever reads the
        /// wire, and for ETag, the one field this layer writes whose usual spelling
        /// is not the capitalised words of its name.
        /// </summary>
        private static String Spelling(String Name)

            => Name == "etag"
                   ? "ETag"
                   : String.Join('-', Name.Split('-').Select(word => word.Length == 0
                                                                         ? word
                                                                         : Char.ToUpperInvariant(word[0]) + word[1..]));

        #endregion

    }

}
