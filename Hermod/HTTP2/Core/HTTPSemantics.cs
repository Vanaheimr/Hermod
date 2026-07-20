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
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Produces the current representation of Path, or null if it doesn't exist
    /// (404). The counterpart of <see cref="HTTP2RequestHandler"/> one level up
    /// the stack: this one only ever needs to answer "what is this resource
    /// right now", never headers/status/framing — <see cref="HTTPSemantics.Wrap"/>
    /// derives all of that.
    /// </summary>
    public delegate Task<HTTPResource?> HTTPResourceHandler(
        string                             Path,
        List<(string Name, string Value)>  RequestHeaders,
        CancellationToken                  CancellationToken
    );


    /// <summary>
    /// Produces every available representation of Path — the variants proactive
    /// content negotiation (RFC 9110, Section 12) will pick among — or an empty
    /// list if the resource doesn't exist (404). A one-element list is the
    /// ordinary "no negotiation" case; <see cref="HTTPResourceHandler"/> is just
    /// that, adapted. When several variants tie on client preference, the first
    /// in the list wins, so an app can express its own preference ordering.
    /// </summary>
    public delegate Task<IReadOnlyList<HTTPResource>> HTTPVariantHandler(
        string                             Path,
        List<(string Name, string Value)>  RequestHeaders,
        CancellationToken                  CancellationToken
    );


    /// <summary>
    /// Processes a QUERY request (RFC 10008): the request content (plus its
    /// declared <paramref name="ContentType"/>) is the query; the returned
    /// <see cref="HTTPResource"/> is the *result* of running it, or null for 404.
    /// QUERY is a safe, idempotent, cacheable method that carries its query in the
    /// request body rather than the URL — so unlike <see cref="HTTPResourceHandler"/>
    /// this one also receives the body. <see cref="HTTPSemantics.Wrap"/> then runs
    /// the result through the same conditional-request / negotiation / representation
    /// pipeline as a GET, so a QUERY result gets ETags, 304 revalidation, Accept
    /// negotiation and (optionally) a <c>Content-Location</c> for free.
    /// </summary>
    public delegate Task<HTTPResource?> HTTPQueryHandler(
        string                             Path,
        List<(string Name, string Value)>  RequestHeaders,
        byte[]?                            QueryContent,
        string?                            ContentType,
        CancellationToken                  CancellationToken
    );


    /// <summary>
    /// RFC 9110 (HTTP Semantics) "core mechanics" — the parts with directly
    /// observable, testable wire behavior — layered on top of an
    /// <see cref="HTTPVariantHandler"/> (or, for the single-representation case,
    /// an <see cref="HTTPResourceHandler"/>): GET/HEAD/OPTIONS method semantics
    /// (Section 9), proactive content negotiation (Section 12: Accept,
    /// Accept-Encoding, Accept-Language, Vary), conditional requests (Section 13:
    /// If-Match, If-None-Match, If-Modified-Since, If-Unmodified-Since), and
    /// Range requests (Section 14: Range, If-Range, Content-Range, Accept-Ranges —
    /// single-range 206 and multi-range multipart/byteranges). Optionally also the
    /// QUERY method (RFC 10008) — a safe,
    /// idempotent, cacheable method that carries its query in the request body —
    /// when an <see cref="HTTPQueryHandler"/> is supplied; its result runs through
    /// the very same conditional/negotiation/representation pipeline as a GET.
    ///
    /// Deliberately out of scope (see CLAUDE.md's Track D roadmap note): a generic
    /// authentication challenge framework (WWW-Authenticate / Authorization) and
    /// RFC 9111 caching semantics (Cache-Control interpretation, revalidation
    /// policy) — real RFC 9110/9111 territory, just not "core mechanics" with a
    /// single obviously-correct wire behavior to verify against.
    ///
    /// <see cref="Wrap"/> produces an ordinary <see cref="HTTP2RequestHandler"/>,
    /// so it plugs into <see cref="HTTP2Connection"/> exactly like a raw handler
    /// would — this file never touches frames, streams, or HPACK, and
    /// HTTP2Connection.cs is untouched by this feature entirely, which is the
    /// whole point: RFC 9110 semantics belong one layer above the framing that's
    /// the rest of this project's focus.
    /// </summary>
    public static class HTTPSemantics
    {

        #region Wrap

        /// <summary>
        /// Wrap a multi-variant handler with RFC 9110 GET/HEAD/OPTIONS semantics,
        /// proactive content negotiation, conditional requests, and Range support.
        /// The pipeline is: negotiate a variant, then run the ordinary conditional
        /// / Range / representation logic on the chosen one — negotiation is a
        /// front stage that only *selects* which representation feeds the rest, so
        /// everything downstream is identical to the single-representation case.
        /// </summary>
        /// <param name="CompressResponses">
        /// When true, a full 200 response whose body is a compressible type and
        /// isn't already content-encoded is compressed on the fly with the best
        /// coding the request's <c>Accept-Encoding</c> accepts (br &gt; gzip &gt;
        /// deflate), via <see cref="System.IO.Compression"/> — RFC 9110 Section 8.4
        /// content coding. Off by default: it's a transport optimization an app
        /// opts into, and it never fires unless the client positively lists a
        /// compression coding.
        /// </param>
        /// <param name="QueryHandler">
        /// Optional QUERY-method handler (RFC 10008). When supplied, a QUERY request
        /// runs its content through this handler and returns the result through the
        /// same pipeline as a GET (conditional requests, negotiation, Content-Location).
        /// When null, QUERY is answered <c>405</c> like any other unsupported method.
        /// Its presence also adds <c>QUERY</c> to the <c>Allow</c> header of OPTIONS
        /// and 405 responses.
        /// </param>
        public static HTTP2RequestHandler Wrap(HTTPVariantHandler VariantHandler, bool CompressResponses = false, HTTPQueryHandler? QueryHandler = null)
            => async (StreamId, RequestHeaders, RequestBody, CancellationToken) =>
            {

                var method = RequestHeaders.FirstOrDefault(h => h.Name == ":method").Value ?? "GET";
                var path   = RequestHeaders.FirstOrDefault(h => h.Name == ":path").Value   ?? "/";

                // Allow-set for OPTIONS/405 — QUERY only when a handler backs it.
                var allow = QueryHandler is null ? "GET, HEAD, OPTIONS" : "GET, HEAD, OPTIONS, QUERY";

                if (method == "OPTIONS")
                    return OptionsResponse(allow);

                // QUERY (RFC 10008): a safe, idempotent, cacheable method whose query
                // lives in the request body. Only handled if an app registered a
                // QueryHandler; otherwise it falls through to 405 like any other method.
                if (method == "QUERY" && QueryHandler is not null)
                {

                    var contentType = RequestHeaders.FirstOrDefault(h => h.Name == "content-type").Value;

                    // RFC 10008, Section 4: "Servers MUST fail the request if the
                    // Content-Type request field is missing" — we enforce that for a
                    // non-empty query (a bodyless QUERY has nothing to type).
                    if (RequestBody is { Length: > 0 } && contentType is null)
                        return BadRequest("QUERY request content requires a Content-Type (RFC 10008, Section 4)");

                    var queryResult = await QueryHandler(path, RequestHeaders, RequestBody, contentType, CancellationToken);

                    // The result runs through the identical representation pipeline as
                    // a GET — QUERY is "like GET" once the result exists.
                    return RunRepresentationPipeline(
                               RequestHeaders, method,
                               queryResult is null ? [] : [queryResult],
                               CompressResponses);

                }

                // This wrapper implements GET/HEAD/OPTIONS (Section 9.3.1, 9.3.2,
                // 9.3.7) and, optionally, QUERY (above) — anything else
                // (POST/PUT/DELETE/PATCH/...) is a resource-creation/mutation concern
                // this "core mechanics" slice deliberately doesn't take a position on.
                if (method is not ("GET" or "HEAD"))
                    return MethodNotAllowed(allow);

                var variants = await VariantHandler(path, RequestHeaders, CancellationToken);

                return RunRepresentationPipeline(RequestHeaders, method, variants, CompressResponses);

            };

        /// <summary>
        /// The shared tail of every representation-producing method (GET/HEAD and
        /// QUERY): pick a variant by content negotiation, then run the conditional /
        /// Range / representation logic and decorate with Vary / Content-* — so GET
        /// and QUERY are byte-identical downstream of "where did the variants come
        /// from". Compresses the final full-200 body when opted in.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) RunRepresentationPipeline(
            List<(string Name, string Value)>  RequestHeaders,
            string                              Method,
            IReadOnlyList<HTTPResource>         Variants,
            bool                                CompressResponses)
        {

            if (Variants.Count == 0)
                return NotFound();

            var negotiation = Negotiate(RequestHeaders, Variants);

            // No acceptable variant survived (everything was explicitly forbidden
            // via q=0) — 406, decorated with Vary so a cache still keys correctly on
            // the negotiating request headers.
            if (negotiation.Chosen is null)
                return Decorate(NotAcceptable(), null, negotiation.Vary);

            var resource = negotiation.Chosen;
            var etag     = resource.ETag ?? ComputeETag(resource.Body);

            var result    = BuildRepresentationResult(RequestHeaders, Method, resource, etag);
            var decorated = Decorate(result, resource, negotiation.Vary);

            return CompressResponses ? ApplyContentCoding(RequestHeaders, decorated) : decorated;

        }

        /// <summary>
        /// Single-representation convenience overload — an app that has exactly
        /// one representation per URL never has to think about variants. It's a
        /// thin adapter over the multi-variant path: null becomes an empty list
        /// (404), a resource becomes a one-element list (which the negotiator
        /// resolves trivially, emitting no Vary since there's nothing to vary on).
        /// </summary>
        public static HTTP2RequestHandler Wrap(HTTPResourceHandler ResourceHandler, bool CompressResponses = false, HTTPQueryHandler? QueryHandler = null)
            => Wrap(async (path, headers, ct) =>
            {
                var resource = await ResourceHandler(path, headers, ct);
                return resource is null ? [] : (IReadOnlyList<HTTPResource>) [resource];
            }, CompressResponses, QueryHandler);

        /// <summary>
        /// The representation-processing pipeline shared by every request that has
        /// a chosen variant: conditional preconditions first (may short-circuit to
        /// 304/412), then HEAD (metadata only), then Range (206/416), else a full
        /// 200. Factored out of <see cref="Wrap"/> so content negotiation can run
        /// ahead of it without duplicating any of this.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) BuildRepresentationResult(
            List<(string Name, string Value)>  RequestHeaders,
            string                              Method,
            HTTPResource                        Resource,
            string                              ETag)
        {

            var conditional = EvaluateConditionalRequest(RequestHeaders, Method, ETag, Resource.LastModified);
            if (conditional is not null)
                return conditional.Value;

            if (Method == "HEAD")
                return FullResponse(Resource, ETag, Body: null);

            // Range only applies to GET here — HEAD has no body to slice,
            // and RFC 9110 doesn't require honoring it there.
            var range = RequestHeaders.FirstOrDefault(h => h.Name == "range").Value;

            if (range is not null && IfRangePasses(RequestHeaders, ETag, Resource.LastModified))
            {
                var rangeResponse = ApplyRange(Resource, ETag, range);
                if (rangeResponse is not null)
                    return rangeResponse.Value;
            }

            return FullResponse(Resource, ETag, Resource.Body);

        }

        #endregion


        #region Content Negotiation (RFC 9110, Section 12)

        /// <summary>The chosen variant (null = 406) plus the request-header field names that influenced selection.</summary>
        private sealed record NegotiationOutcome(HTTPResource? Chosen, List<string> Vary);

        /// <summary>
        /// One parsed member of an Accept / Accept-Encoding / Accept-Language
        /// field value: the value token (media range, coding, or language range,
        /// lowercased) and its quality weight (RFC 9110, Section 12.4.2). Media
        /// range parameters other than "q" (e.g. "text/html;level=1") are parsed
        /// but ignored — a documented simplification; they only ever raise
        /// specificity, never change which type/subtype matches.
        /// </summary>
        private readonly record struct AcceptItem(string Value, double Q);

        /// <summary>
        /// The result of matching one variant against one negotiation axis:
        ///   - Acceptable: the axis permits this variant (q &gt; 0, or the axis's
        ///     request header was absent), with quality Q.
        ///   - Forbidden: a matching entry explicitly set q=0 — a hard rejection.
        ///   - neither (Acceptable false, Forbidden false): the header was present
        ///     but nothing matched — not a positive match, but not a hard rejection
        ///     either, so the variant can still be served as a last-resort default.
        /// </summary>
        private readonly record struct AxisMatch(bool Acceptable, bool Forbidden, double Q);

        /// <summary>
        /// Proactive (server-driven) content negotiation (RFC 9110, Section 12).
        /// Scores every variant on three independent axes — media type (Accept),
        /// content coding (Accept-Encoding), language (Accept-Language) — and
        /// picks the best positively-acceptable one, breaking ties by the app's
        /// own variant order (first wins).
        ///
        /// The 406-vs-default policy (Section 12.1 explicitly leaves this to the
        /// server): a variant explicitly forbidden by a q=0 on any axis is never
        /// served. But a variant that simply fails to positively match (e.g. the
        /// client asked for a language we don't have, without forbidding ours) is
        /// still eligible as a last-resort default — i.e. we "disregard" an
        /// unsatisfiable Accept rather than answer 406, which is the friendlier of
        /// the two RFC-sanctioned behaviors. Only when *every* variant is
        /// hard-forbidden do we fall through to 406.
        /// </summary>
        private static NegotiationOutcome Negotiate(List<(string Name, string Value)> RequestHeaders, IReadOnlyList<HTTPResource> Variants)
        {

            var acceptRanges   = ParseAcceptHeader(RequestHeaders, "accept");
            var encodingRanges = ParseAcceptHeader(RequestHeaders, "accept-encoding");
            var languageRanges = ParseAcceptHeader(RequestHeaders, "accept-language");

            HTTPResource? best      = null;
            var           bestScore = -1.0;
            HTTPResource? fallback  = null;

            foreach (var variant in Variants)
            {

                var media    = MatchMediaType(variant.ContentType,    acceptRanges);
                var encoding = MatchEncoding (variant.ContentEncoding, encodingRanges);
                var language = MatchLanguage (variant.Language,        languageRanges);

                // A single q=0 on any axis hard-rejects the variant outright.
                if (media.Forbidden || encoding.Forbidden || language.Forbidden)
                    continue;

                // Not forbidden anywhere ⇒ eligible as the last-resort default if
                // nothing positively matches. First such variant wins (app order).
                fallback ??= variant;

                if (media.Acceptable && encoding.Acceptable && language.Acceptable)
                {
                    var score = media.Q * encoding.Q * language.Q;
                    if (score > bestScore)   // strict '>' keeps the earliest variant on a tie
                    {
                        bestScore = score;
                        best      = variant;
                    }
                }

            }

            return new NegotiationOutcome(best ?? fallback, ComputeVary(Variants));

        }

        /// <summary>
        /// Parse a comma-separated Accept-family field value into weighted items,
        /// or null if the header is absent (an absent axis constrains nothing).
        /// </summary>
        private static List<AcceptItem>? ParseAcceptHeader(List<(string Name, string Value)> RequestHeaders, string Name)
        {

            var header = RequestHeaders.FirstOrDefault(h => h.Name == Name).Value;
            if (header is null)
                return null;

            var items = new List<AcceptItem>();

            foreach (var rawMember in header.Split(','))
            {

                var parts = rawMember.Split(';');
                var value = parts[0].Trim();
                if (value.Length == 0)
                    continue;

                var q = 1.0;   // RFC 9110, Section 12.4.2: default weight is 1.

                for (var i = 1; i < parts.Length; i++)
                {
                    var param = parts[i].Trim();
                    if (param.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(param[2..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        q = Math.Clamp(parsed, 0.0, 1.0);
                }

                items.Add(new AcceptItem(value.ToLowerInvariant(), q));

            }

            return items;

        }

        /// <summary>
        /// Match a variant's media type against the Accept ranges (RFC 9110,
        /// Section 12.5.1), honoring specificity: an exact type/subtype outranks
        /// "type/*", which outranks "*/*". The most specific matching range wins
        /// (so a q=0 on the exact type forbids the variant even if "*/*" would
        /// allow it); equal specificity ties go to the higher q.
        /// </summary>
        private static AxisMatch MatchMediaType(string MediaType, List<AcceptItem>? Ranges)
        {

            if (Ranges is null)
                return new AxisMatch(true, false, 1.0);

            // Strip any parameters (e.g. "; charset=utf-8") before matching.
            var bare  = MediaType.Split(';')[0].Trim();
            var slash = bare.IndexOf('/');
            var type  = slash < 0 ? bare : bare[..slash];
            var sub   = slash < 0 ? ""   : bare[(slash + 1)..];

            var bestSpecificity = -1;
            double? bestQ = null;

            foreach (var range in Ranges)
            {

                var rSlash = range.Value.IndexOf('/');
                var rType  = rSlash < 0 ? range.Value : range.Value[..rSlash];
                var rSub   = rSlash < 0 ? "*"         : range.Value[(rSlash + 1)..];

                int specificity;
                if (rType == "*")                                          specificity = 0;   // */*
                else if (!rType.Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
                else if (rSub == "*")                                      specificity = 1;   // type/*
                else if (rSub.Equals(sub, StringComparison.OrdinalIgnoreCase))   specificity = 2;   // type/subtype
                else continue;

                if (specificity > bestSpecificity ||
                   (specificity == bestSpecificity && range.Q > (bestQ ?? -1.0)))
                {
                    bestSpecificity = specificity;
                    bestQ           = range.Q;
                }

            }

            return Classify(bestQ);

        }

        /// <summary>
        /// Match a variant's content coding against Accept-Encoding (RFC 9110,
        /// Section 12.5.3). The identity coding is acceptable by default unless
        /// explicitly excluded ("identity;q=0", or "*;q=0" with no more specific
        /// identity entry); any other coding must be listed (directly or via "*")
        /// to be acceptable at all.
        /// </summary>
        private static AxisMatch MatchEncoding(string? ContentEncoding, List<AcceptItem>? Ranges)
        {

            // Absent Accept-Encoding ⇒ any coding is acceptable (Section 12.5.3).
            if (Ranges is null)
                return new AxisMatch(true, false, 1.0);

            var coding = ContentEncoding ?? "identity";

            double? exactQ = null;
            double? starQ  = null;

            foreach (var range in Ranges)
            {
                if (range.Value.Equals(coding, StringComparison.OrdinalIgnoreCase)) exactQ = range.Q;
                else if (range.Value == "*")                                        starQ  = range.Q;
            }

            // identity: acceptable by default (q=1) unless a matching entry lowers
            // or forbids it. A specific "identity" entry wins over "*".
            if (coding.Equals("identity", StringComparison.OrdinalIgnoreCase))
                return Classify(exactQ ?? starQ ?? 1.0);

            // Any other coding must be positively listed (exact or "*") to count.
            return Classify(exactQ ?? starQ);

        }

        /// <summary>
        /// Match a variant's language against Accept-Language (RFC 9110, Section
        /// 12.5.4, which permits either RFC 4647 basic filtering or lookup). We
        /// match a range against a tag when either is a prefix of the other on a
        /// subtag boundary — so "de" matches a "de-DE" variant (basic filtering)
        /// *and* a "de-AT" range matches a plain "de" variant (lookup-style
        /// truncation: an Austrian-German client is happy with generic German).
        /// "den" still doesn't match "de" (not a subtag boundary). A variant with
        /// no language is acceptable for any Accept-Language. Specificity is the
        /// length of the matched common prefix, so an exact/longer match outranks
        /// a truncated one; equal specificity ties go to the higher q.
        /// </summary>
        private static AxisMatch MatchLanguage(string? Language, List<AcceptItem>? Ranges)
        {

            if (Ranges is null || Language is null)
                return new AxisMatch(true, false, 1.0);

            var lang = Language.ToLowerInvariant();

            var bestLength = -1;
            double? bestQ = null;

            foreach (var range in Ranges)
            {

                bool matches;
                int  length;

                if (range.Value == "*")
                {
                    matches = true;
                    length  = 0;   // wildcard is the least specific match
                }
                else
                {
                    // Prefix match in either direction (on a subtag boundary):
                    // range is a prefix of the tag (filtering) OR the tag is a
                    // prefix of the range (lookup-style truncation).
                    matches = lang.Equals(range.Value, StringComparison.Ordinal)
                           || lang.StartsWith(range.Value + "-", StringComparison.Ordinal)
                           || range.Value.StartsWith(lang + "-", StringComparison.Ordinal);
                    length  = Math.Min(range.Value.Length, lang.Length);
                }

                if (!matches)
                    continue;

                if (length > bestLength ||
                   (length == bestLength && range.Q > (bestQ ?? -1.0)))
                {
                    bestLength = length;
                    bestQ      = range.Q;
                }

            }

            return Classify(bestQ);

        }

        /// <summary>
        /// Turn a best-matching quality (null = no entry matched at all) into an
        /// <see cref="AxisMatch"/>: no match ⇒ neither acceptable nor forbidden;
        /// q=0 ⇒ forbidden; q&gt;0 ⇒ acceptable.
        /// </summary>
        private static AxisMatch Classify(double? BestQ)
            => BestQ is null      ? new AxisMatch(false, false, 0.0)
             : BestQ.Value <= 0.0 ? new AxisMatch(false, true,  0.0)
             :                      new AxisMatch(true,  false, BestQ.Value);

        /// <summary>
        /// The Vary response header (RFC 9110, Section 12.5.5) must list every
        /// request-header field that could select a different representation — i.e.
        /// every axis on which the variants actually differ, whether or not this
        /// particular request carried that header (a *different* request with it
        /// could get a different variant, and caches must key on that).
        /// </summary>
        private static List<string> ComputeVary(IReadOnlyList<HTTPResource> Variants)
        {

            var vary = new List<string>();

            if (Variants.Select(v => v.ContentType.Split(';')[0].Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                vary.Add("accept");

            if (Variants.Select(v => v.ContentEncoding ?? "identity").Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                vary.Add("accept-encoding");

            if (Variants.Select(v => v.Language ?? "").Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                vary.Add("accept-language");

            return vary;

        }

        /// <summary>
        /// Attach negotiation metadata to a produced response: Vary on every
        /// negotiated response (so caches key correctly), plus Content-Language /
        /// Content-Encoding describing the chosen representation on the responses
        /// that actually carry it (200 / 206) — a 304/412 transfers no
        /// representation, so it gets only Vary.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) Decorate(
            (List<(string Name, string Value)> Headers, byte[]? Body) Result,
            HTTPResource?                                             Chosen,
            List<string>                                              Vary)
        {

            if (Vary.Count > 0 && !Result.Headers.Any(h => h.Name == "vary"))
                Result.Headers.Add(("vary", string.Join(", ", Vary)));

            if (Chosen is not null)
            {
                var status = Result.Headers.First(h => h.Name == ":status").Value;

                if (status is "200" or "206")
                {
                    if (Chosen.Language is not null && !Result.Headers.Any(h => h.Name == "content-language"))
                        Result.Headers.Add(("content-language", Chosen.Language));

                    if (Chosen.ContentEncoding is not null && !Result.Headers.Any(h => h.Name == "content-encoding"))
                        Result.Headers.Add(("content-encoding", Chosen.ContentEncoding));
                }
            }

            return Result;

        }

        #endregion


        #region On-the-fly content coding (RFC 9110, Section 8.4)

        /// <summary>Codings we can produce, in server preference order (best first).</summary>
        private static readonly string[] SupportedCodings = ["br", "gzip", "deflate"];

        /// <summary>Bodies smaller than this aren't worth the compression overhead.</summary>
        private const int MinCompressSize = 256;

        /// <summary>
        /// Compress a full 200 response body on the fly (RFC 9110, Section 8.4) with
        /// the best coding the request's Accept-Encoding accepts. Applied as a pure
        /// post-processing step so the conditional/Range pipeline above is entirely
        /// unaffected — it always runs on the identity representation. Skipped for a
        /// non-200, a bodiless/HEAD/304/206 response, a tiny or non-compressible
        /// body, an already-encoded response, or a client that lists no compression
        /// coding.
        ///
        /// The ETag is *weakened* when we compress (RFC 9110, Section 8.8.1): the
        /// bytes now differ from the identity representation the strong ETag named,
        /// but they're semantically equivalent — which is exactly what a weak
        /// validator means, and keeps conditional revalidation (weak comparison)
        /// correct across the identity and compressed variants of the same URL.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) ApplyContentCoding(
            List<(string Name, string Value)>                        RequestHeaders,
            (List<(string Name, string Value)> Headers, byte[]? Body) Result)
        {

            var headers = Result.Headers;
            var body    = Result.Body;

            if (body is null || body.Length < MinCompressSize)
                return Result;

            if (headers.First(h => h.Name == ":status").Value != "200")
                return Result;

            if (headers.Any(h => h.Name == "content-encoding"))
                return Result;   // an app-provided pre-encoded variant — don't double-encode

            var contentType = headers.FirstOrDefault(h => h.Name == "content-type").Value ?? "";
            if (!IsCompressible(contentType))
                return Result;

            var coding = SelectContentCoding(RequestHeaders);
            if (coding is null)
                return Result;

            var compressed = Compress(body, coding);

            // If it didn't actually shrink (already-dense data), send it uncompressed.
            if (compressed.Length >= body.Length)
                return Result;

            headers.RemoveAll(h => h.Name == "content-length");
            headers.Add(("content-length",   compressed.Length.ToString()));
            headers.Add(("content-encoding", coding));
            WeakenETag(headers);
            AddVaryField(headers, "accept-encoding");

            return (headers, compressed);

        }

        /// <summary>
        /// Pick the best content coding the client will accept (RFC 9110, Section
        /// 12.5.3): among the codings we support, the highest positive q wins, ties
        /// broken by our own preference order (br &gt; gzip &gt; deflate). Returns
        /// null when Accept-Encoding is absent or lists none of our codings with a
        /// positive q — in which case we serve identity (never compress a client
        /// that didn't ask).
        /// </summary>
        private static string? SelectContentCoding(List<(string Name, string Value)> RequestHeaders)
        {

            var ranges = ParseAcceptHeader(RequestHeaders, "accept-encoding");
            if (ranges is null)
                return null;

            string? best  = null;
            var     bestQ = 0.0;

            foreach (var coding in SupportedCodings)
            {

                double? exactQ = null, starQ = null;

                foreach (var range in ranges)
                {
                    if (range.Value.Equals(coding, StringComparison.OrdinalIgnoreCase)) exactQ = range.Q;
                    else if (range.Value == "*")                                        starQ  = range.Q;
                }

                var q = exactQ ?? starQ;   // must be positively listed (exact or "*")

                if (q is > 0 && q.Value > bestQ)   // strict '>' keeps server preference (br first) on a tie
                {
                    bestQ = q.Value;
                    best  = coding;
                }

            }

            return best;

        }

        /// <summary>Whether a media type is worth compressing (text and text-like structured formats).</summary>
        private static bool IsCompressible(string ContentType)
        {

            var bare = ContentType.Split(';')[0].Trim().ToLowerInvariant();

            if (bare.StartsWith("text/", StringComparison.Ordinal))
                return true;

            return bare is "application/json"          or "application/xml"
                        or "application/javascript"    or "application/ld+json"
                        or "application/manifest+json" or "application/xhtml+xml"
                        or "application/wasm"          or "image/svg+xml";

        }

        private static byte[] Compress(byte[] Data, string Coding)
        {

            using var output = new MemoryStream();

            using (Stream compressor = Coding switch
            {
                "br"      => new BrotliStream (output, CompressionLevel.Optimal, leaveOpen: true),
                "gzip"    => new GZipStream   (output, CompressionLevel.Optimal, leaveOpen: true),
                "deflate" => new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true),
                _         => throw new InvalidOperationException($"Unsupported coding '{Coding}'")
            })
                compressor.Write(Data, 0, Data.Length);

            return output.ToArray();

        }

        private static void WeakenETag(List<(string Name, string Value)> Headers)
        {
            var idx = Headers.FindIndex(h => h.Name == "etag");
            if (idx >= 0 && !Headers[idx].Value.StartsWith("W/", StringComparison.Ordinal))
                Headers[idx] = ("etag", "W/" + Headers[idx].Value);
        }

        private static void AddVaryField(List<(string Name, string Value)> Headers, string Field)
        {

            var idx = Headers.FindIndex(h => h.Name == "vary");

            if (idx < 0)
            {
                Headers.Add(("vary", Field));
                return;
            }

            var present = Headers[idx].Value.Split(',').Select(s => s.Trim());
            if (!present.Any(e => e.Equals(Field, StringComparison.OrdinalIgnoreCase)))
                Headers[idx] = ("vary", Headers[idx].Value + ", " + Field);

        }

        #endregion


        #region Conditional Requests (RFC 9110, Section 13)

        /// <summary>
        /// Evaluate If-Match / If-Unmodified-Since / If-None-Match / If-Modified-Since
        /// in the precedence order Section 13.2.2 mandates, and short-circuit to
        /// 412 or 304 if one of them fires. Returns null if none did (proceed
        /// with normal processing).
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body)? EvaluateConditionalRequest(
            List<(string Name, string Value)>  RequestHeaders,
            string                              Method,
            string                              ETag,
            DateTimeOffset?                     LastModified)
        {

            var ifMatch = RequestHeaders.FirstOrDefault(h => h.Name == "if-match").Value;

            if (ifMatch is not null)
            {
                // Section 13.1.1: strong comparison; "*" matches any current
                // representation (we already know one exists at this point).
                if (!IfMatchPasses(ifMatch, ETag))
                    return PreconditionFailed();
            }
            else
            {
                var ifUnmodifiedSince = RequestHeaders.FirstOrDefault(h => h.Name == "if-unmodified-since").Value;

                if (ifUnmodifiedSince is not null && LastModified is not null &&
                    TryParseHttpDate(ifUnmodifiedSince, out var iusDate) &&
                    Truncate(LastModified.Value) > Truncate(iusDate))
                {
                    return PreconditionFailed();
                }
            }

            var ifNoneMatch = RequestHeaders.FirstOrDefault(h => h.Name == "if-none-match").Value;

            if (ifNoneMatch is not null)
            {
                // Section 13.1.2: weak comparison — appropriate for cache
                // freshness checks. A match (precondition false) is 304 for the
                // safe/cacheable methods (GET/HEAD, and QUERY per RFC 10008, whose
                // conditional semantics mirror GET on the "equivalent resource"),
                // 412 for anything else this wrapper would otherwise have let through.
                if (!IfNoneMatchPasses(ifNoneMatch, ETag))
                    return Method is "GET" or "HEAD" or "QUERY" ? NotModified(ETag, LastModified) : PreconditionFailed();
            }
            else if (Method is "GET" or "HEAD" or "QUERY")
            {
                // Section 13.1.4: If-Modified-Since is only evaluated for
                // GET/HEAD, and only when If-None-Match was absent.
                var ifModifiedSince = RequestHeaders.FirstOrDefault(h => h.Name == "if-modified-since").Value;

                if (ifModifiedSince is not null && LastModified is not null &&
                    TryParseHttpDate(ifModifiedSince, out var imsDate) &&
                    Truncate(LastModified.Value) <= Truncate(imsDate))
                {
                    return NotModified(ETag, LastModified);
                }
            }

            return null;

        }

        private static bool IfMatchPasses(string HeaderValue, string CurrentETag)
        {
            if (HeaderValue.Trim() == "*")
                return true;

            return ParseETagList(HeaderValue).Any(c => !c.Weak && c.Tag == CurrentETag);
        }

        private static bool IfNoneMatchPasses(string HeaderValue, string CurrentETag)
        {
            if (HeaderValue.Trim() == "*")
                return false;

            return !ParseETagList(HeaderValue).Any(c => c.Tag == CurrentETag);
        }

        /// <summary>
        /// RFC 9110, Section 13.1.5: If-Range names either an entity-tag (strong
        /// comparison only — a weak validator can never safely resume a partial
        /// download) or an HTTP-date (exact match, at 1-second granularity).
        /// Range is unconditional (always honored) if If-Range is absent.
        /// </summary>
        private static bool IfRangePasses(List<(string Name, string Value)> RequestHeaders, string CurrentETag, DateTimeOffset? LastModified)
        {

            var ifRange = RequestHeaders.FirstOrDefault(h => h.Name == "if-range").Value;

            if (ifRange is null)
                return true;

            var trimmed = ifRange.Trim();

            if (trimmed.StartsWith('"') || trimmed.StartsWith("W/", StringComparison.Ordinal))
            {
                var weak = trimmed.StartsWith("W/", StringComparison.Ordinal);
                var tag  = weak ? trimmed[2..] : trimmed;
                return !weak && tag == CurrentETag;
            }

            return LastModified is not null &&
                   TryParseHttpDate(trimmed, out var ifRangeDate) &&
                   Truncate(LastModified.Value) == Truncate(ifRangeDate);

        }

        /// <summary>
        /// Parse an If-Match / If-None-Match field value's comma-separated
        /// entity-tag list ("*" is handled by the caller before reaching here).
        /// </summary>
        private static List<(string Tag, bool Weak)> ParseETagList(string Value)
        {

            var result = new List<(string, bool)>();

            foreach (var raw in Value.Split(','))
            {

                var token = raw.Trim();
                if (token.Length == 0)
                    continue;

                var weak = token.StartsWith("W/", StringComparison.Ordinal);
                if (weak)
                    token = token[2..];

                if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
                    result.Add((token, weak));

            }

            return result;

        }

        /// <summary>
        /// HTTP-date (RFC 9110, Section 5.6.7 — IMF-fixdate, e.g.
        /// "Sun, 06 Nov 1994 08:49:37 GMT"). .NET's "r"/"R" round-trip format
        /// specifier is exactly the RFC 1123 pattern HTTP uses; a lenient
        /// fallback via general DateTimeOffset parsing tolerates the handful of
        /// clients that send a slightly different (but still valid) format.
        /// </summary>
        private static bool TryParseHttpDate(string Value, out DateTimeOffset Result)
            => DateTimeOffset.TryParseExact(Value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out Result)
            || DateTimeOffset.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out Result);

        /// <summary>HTTP-date has no sub-second precision — compare at 1-second granularity.</summary>
        private static DateTimeOffset Truncate(DateTimeOffset Value)
            => new(Value.Year, Value.Month, Value.Day, Value.Hour, Value.Minute, Value.Second, Value.Offset);

        private static string ComputeETag(byte[] Body)
            => $"\"{Convert.ToHexString(SHA256.HashData(Body))[..32].ToLowerInvariant()}\"";

        #endregion


        #region Range Requests (RFC 9110, Section 14)

        private enum RangeParseResult { Unsupported, Unsatisfiable, Ok }

        /// <summary>
        /// A byte-range set may name more than this many ranges, but a request that
        /// does is refused (falls back to a full 200). Many tiny (often overlapping)
        /// ranges are a response-amplification / CPU DoS vector — the same reason
        /// RFC 9110, Section 14.1.2 lets a server reject "an unsatisfiable or
        /// excessive" range set. We don't coalesce overlaps; the cap is the guard.
        /// </summary>
        private const int MaxRanges = 100;

        /// <summary>
        /// Apply a Range header (already passed its If-Range check, if any) to
        /// Resource. A single satisfiable range → <c>206</c> with
        /// <c>Content-Range</c>; multiple satisfiable ranges → <c>206</c> as a
        /// <c>multipart/byteranges</c> body (Section 14.4). Returns null if the
        /// Range header names a unit/syntax this wrapper doesn't understand — per
        /// Section 14.2 an unusable Range is simply ignored, falling back to an
        /// ordinary 200; a set whose ranges are *all* out of bounds is a distinct
        /// 416, handled here rather than falling back. Unsatisfiable ranges within a
        /// set that also has satisfiable ones are dropped (Section 14.1.2).
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body)? ApplyRange(HTTPResource Resource, string ETag, string RangeHeader)
        {

            var length = (long) Resource.Body.Length;

            if (!RangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                return null;   // Unknown range unit — ignore the Range (RFC 9110 §14.2).

            var setSpec = RangeHeader["bytes=".Length..].Trim();
            if (setSpec.Length == 0)
                return null;

            var parts = setSpec.Split(',');
            if (parts.Length > MaxRanges)
                return null;   // Excessive range set — fall back to a full 200.

            var satisfiable = new List<(long Start, long End)>();

            foreach (var raw in parts)
            {
                var spec = raw.Trim();
                if (spec.Length == 0)
                    continue;   // Tolerate a stray empty element between commas.

                switch (ParseByteRangeSpec(spec, length, out var start, out var end))
                {
                    case RangeParseResult.Unsupported:
                        return null;   // Any malformed member ⇒ ignore the whole Range.
                    case RangeParseResult.Ok:
                        satisfiable.Add((start, end));
                        break;
                    // Unsatisfiable: drop this member, keep going.
                }
            }

            if (satisfiable.Count == 0)
                // Every range was out of bounds (or the set was empty) ⇒ 416.
                return ([(":status", "416"), ("content-range", $"bytes */{length}"), ("etag", ETag)], null);

            return satisfiable.Count == 1
                       ? BuildPartialResponse(Resource, ETag, satisfiable[0].Start, satisfiable[0].End, length)
                       : BuildMultipartResponse(Resource, ETag, satisfiable, length);

        }

        private static (List<(string Name, string Value)> Headers, byte[]? Body) BuildPartialResponse(
            HTTPResource Resource, string ETag, long Start, long End, long Length)
        {

            var slice = Resource.Body[(int) Start..(int) (End + 1)];

            var headers = new List<(string, string)>
            {
                (":status",       "206"),
                ("content-type",  Resource.ContentType),
                ("content-range", $"bytes {Start}-{End}/{Length}"),
                ("content-length", slice.Length.ToString()),
                ("accept-ranges", "bytes"),
                ("etag",          ETag)
            };

            if (Resource.LastModified is not null)
                headers.Add(("last-modified", Resource.LastModified.Value.ToString("r", CultureInfo.InvariantCulture)));

            return (headers, slice);

        }

        /// <summary>
        /// Build a <c>multipart/byteranges</c> 206 (RFC 9110, Section 14.4 / 14.6)
        /// for two or more satisfiable ranges: each part carries its own
        /// <c>Content-Type</c> and <c>Content-Range</c>, separated by a unique
        /// boundary; the response-level <c>Content-Type</c> is
        /// <c>multipart/byteranges; boundary=…</c> and there is no response-level
        /// <c>Content-Range</c> (each part has its own). The boundary is random so it
        /// can't collide with binary body content.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) BuildMultipartResponse(
            HTTPResource Resource, string ETag, List<(long Start, long End)> Ranges, long Length)
        {

            var boundary = Guid.NewGuid().ToString("N");
            var body     = new List<byte>();

            foreach (var (start, end) in Ranges)
            {
                // Part header (ASCII): boundary line + per-part Content-Type / Content-Range.
                body.AddRange(Encoding.ASCII.GetBytes(
                    $"--{boundary}\r\n" +
                    $"Content-Type: {Resource.ContentType}\r\n" +
                    $"Content-Range: bytes {start}-{end}/{Length}\r\n\r\n"));

                body.AddRange(Resource.Body[(int) start..(int) (end + 1)]);
                body.AddRange("\r\n"u8.ToArray());
            }

            body.AddRange(Encoding.ASCII.GetBytes($"--{boundary}--\r\n"));   // closing delimiter

            var bytes = body.ToArray();

            var headers = new List<(string, string)>
            {
                (":status",        "206"),
                ("content-type",   $"multipart/byteranges; boundary={boundary}"),
                ("content-length", bytes.Length.ToString()),
                ("accept-ranges",  "bytes"),
                ("etag",           ETag)
            };

            if (Resource.LastModified is not null)
                headers.Add(("last-modified", Resource.LastModified.Value.ToString("r", CultureInfo.InvariantCulture)));

            return (headers, bytes);

        }

        /// <summary>
        /// Parse one byte-range-spec (RFC 9110, Section 14.1.2), already stripped of
        /// the <c>bytes=</c> unit and surrounding whitespace: <c>first-last</c>,
        /// <c>first-</c> (to the end), or <c>-suffixLength</c> (the last N bytes).
        /// Returns Ok with [Start,End] resolved against ContentLength, Unsatisfiable
        /// if syntactically valid but out of bounds, or Unsupported if malformed.
        /// </summary>
        private static RangeParseResult ParseByteRangeSpec(string spec, long ContentLength, out long Start, out long End)
        {

            Start = 0;
            End   = 0;

            var dash = spec.IndexOf('-');
            if (dash < 0)
                return RangeParseResult.Unsupported;

            var firstPart = spec[..dash];
            var lastPart  = spec[(dash + 1)..];

            if (firstPart.Length == 0)
            {

                if (!long.TryParse(lastPart, out var suffixLength) || suffixLength <= 0)
                    return RangeParseResult.Unsupported;

                if (ContentLength == 0)
                    return RangeParseResult.Unsatisfiable;

                Start = Math.Max(0, ContentLength - suffixLength);
                End   = ContentLength - 1;

                return RangeParseResult.Ok;

            }

            if (!long.TryParse(firstPart, out Start) || Start < 0)
                return RangeParseResult.Unsupported;

            if (Start >= ContentLength)
                return RangeParseResult.Unsatisfiable;

            if (lastPart.Length == 0)
            {
                End = ContentLength - 1;
            }
            else
            {
                if (!long.TryParse(lastPart, out End) || End < Start)
                    return RangeParseResult.Unsupported;

                End = Math.Min(End, ContentLength - 1);
            }

            return RangeParseResult.Ok;

        }

        #endregion


        #region Response builders

        private static (List<(string Name, string Value)> Headers, byte[]? Body) FullResponse(HTTPResource Resource, string ETag, byte[]? Body)
        {

            var headers = new List<(string, string)>
            {
                (":status",        "200"),
                ("content-type",   Resource.ContentType),
                ("content-length", Resource.Body.Length.ToString()),
                ("accept-ranges",  "bytes"),
                ("etag",           ETag)
            };

            if (Resource.LastModified is not null)
                headers.Add(("last-modified", Resource.LastModified.Value.ToString("r", CultureInfo.InvariantCulture)));

            // RFC 9110, Section 8.7 / RFC 10008, Section 3: point at a resource that
            // corresponds to this representation (e.g. a QUERY result's own URI).
            if (Resource.ContentLocation is not null)
                headers.Add(("content-location", Resource.ContentLocation));

            return (headers, Body);

        }

        /// <summary>
        /// RFC 9110, Section 15.4.5: a 304 response never has a message body — only
        /// validators are sent here (plus Vary, added by <see cref="Decorate"/>
        /// when the resource was content-negotiated). Cache-Control/Expires stay
        /// out of scope (RFC 9111).
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) NotModified(string ETag, DateTimeOffset? LastModified)
        {

            var headers = new List<(string, string)> { (":status", "304"), ("etag", ETag) };

            if (LastModified is not null)
                headers.Add(("last-modified", LastModified.Value.ToString("r", CultureInfo.InvariantCulture)));

            return (headers, null);

        }

        private static (List<(string Name, string Value)> Headers, byte[]? Body) PreconditionFailed()
            => ([(":status", "412")], null);

        /// <summary>
        /// RFC 9110, Section 15.5.7: no variant is acceptable (every one was
        /// hard-forbidden by a q=0). A small text body naming the failure; Vary is
        /// attached by <see cref="Decorate"/>.
        /// </summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) NotAcceptable()
        {
            var body = "406 Not Acceptable"u8.ToArray();
            return ([(":status", "406"), ("content-type", "text/plain; charset=utf-8"), ("content-length", body.Length.ToString())], body);
        }

        private static (List<(string Name, string Value)> Headers, byte[]? Body) NotFound()
        {
            var body = "404 Not Found"u8.ToArray();
            return ([(":status", "404"), ("content-type", "text/plain; charset=utf-8"), ("content-length", body.Length.ToString())], body);
        }

        /// <summary>RFC 9110, Section 15.5.1: a malformed request the server won't process (e.g. RFC 10008's missing Content-Type on a QUERY).</summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) BadRequest(string Reason)
        {
            var body = System.Text.Encoding.UTF8.GetBytes("400 Bad Request: " + Reason);
            return ([(":status", "400"), ("content-type", "text/plain; charset=utf-8"), ("content-length", body.Length.ToString())], body);
        }

        /// <summary>RFC 9110, Section 15.5.6: a 405 MUST include Allow.</summary>
        private static (List<(string Name, string Value)> Headers, byte[]? Body) MethodNotAllowed(string Allow)
            => ([(":status", "405"), ("allow", Allow)], null);

        private static (List<(string Name, string Value)> Headers, byte[]? Body) OptionsResponse(string Allow)
            => ([(":status", "204"), ("allow", Allow)], null);

        #endregion

    }

}
