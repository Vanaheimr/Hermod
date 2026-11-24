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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// An RFC 9111 HTTP cache layered in front of a single-origin
    /// <see cref="HTTP2ClientConnection"/> — the client-side counterpart of the
    /// server's conditional-request handling. It stores responses, serves fresh
    /// hits without touching the origin, revalidates stale ones with conditional
    /// requests (<c>If-None-Match</c> / <c>If-Modified-Since</c> → 304), keys
    /// variants by <c>Vary</c>, and honors the request/response
    /// <c>Cache-Control</c> directives. All the caching *logic* is in the shared
    /// library's <see cref="HTTPCache"/>; this type is just the store plus the
    /// "when do I go to the origin?" wiring.
    ///
    /// The <see cref="HTTPCacheMode"/> selects private vs. shared semantics
    /// (s-maxage, the <c>private</c> directive, the authenticated-request rule).
    /// Bound to one origin (scheme + authority) because it wraps one connection.
    /// </summary>
    public sealed class HTTP2CachingClient
    {

        private readonly HTTP2ClientConnection connection;
        private readonly TimeProvider          timeProvider;
        private readonly URIScheme             scheme;
        private readonly string                authority;
        private readonly HTTPCacheMode         mode;

        // Keyed by :path; each path may hold several Vary-selected variants.
        private readonly Dictionary<string, List<HTTPStoredResponse>> store = [];
        private readonly object                                        storeLock = new();

        /// <summary>
        /// Fresh/stale cache hits served without an origin round trip.
        /// </summary>
        public int Hits           { get; private set; }
        /// <summary>
        /// Requests that went to the origin because nothing usable was cached.
        /// </summary>
        public int Misses         { get; private set; }
        /// <summary>
        /// Stale entries revalidated with the origin via a conditional request.
        /// </summary>
        public int Revalidations  { get; private set; }


        public HTTP2CachingClient(
            HTTP2ClientConnection Connection,
            URIScheme             Scheme,
            string                Authority,
            HTTPCacheMode         Mode         = HTTPCacheMode.Private,
            TimeProvider?         TimeProvider = null)
        {
            connection   = Connection;
            scheme       = Scheme;
            authority    = Authority;
            mode         = Mode;
            timeProvider = TimeProvider ?? global::System.TimeProvider.System;
        }


        /// <summary>
        /// Convenience GET.
        /// </summary>
        public Task<HTTP2Response> GetAsync(String                             Path,
                                            List<(String Name, String Value)>? ExtraHeaders = null,
                                            CancellationToken                  CancellationToken = default)

            => SendRequestAsync(
                   HTTPMethod.GET,
                   Path,
                   ExtraHeaders,
                   null,
                   CancellationToken
               );

        /// <summary>
        /// Send a request through the cache. GET/HEAD are cached per RFC 9111;
        /// other (unsafe) methods bypass the cache and, on success, invalidate any
        /// stored entry for the target (Section 4.4).
        /// </summary>
        public async Task<HTTP2Response> SendRequestAsync(HTTPMethod                          Method,
                                                          String                              Path,
                                                          List<(String Name, String Value)>?  ExtraHeaders        = null,
                                                          Byte[]?                             Body                = null,
                                                          CancellationToken                   CancellationToken   = default)
        {

            var extraHeaders = ExtraHeaders ?? [];
            var requestCC    = HTTPCacheControl.FromHeaders(extraHeaders);
            var hasAuth      = extraHeaders.Any(h => h.Name == "authorization");

            // Unsafe / non-cacheable methods: straight to origin, then invalidate.
            if (Method != HTTPMethod.GET && Method != HTTPMethod.HEAD)
            {
                var resp = await connection.SendRequestAsync(Method, scheme, authority, Path, extraHeaders, Body, CancellationToken: CancellationToken);
                if (resp.Status is >= 200 and < 400)
                    Invalidate(Path);
                return resp;
            }

            // HEAD, Range and the preconditions for an origin server go to the
            // origin as they are: entries are keyed by path, so a stored GET
            // would answer a HEAD with a body, and a stored 206 a GET with a
            // part. This cache combines no partial content, and If-Match and
            // If-Unmodified-Since apply to an origin server only (Section 4.3.2).
            // A 200 to a HEAD still updates the stored GET responses it could
            // have been answered from, or makes them stale (Section 4.3.5).
            if (Method == HTTPMethod.HEAD)
            {

                var requestTime   = timeProvider.GetUtcNow();
                var headResponse  = await connection.SendRequestAsync(Method, scheme, authority, Path, extraHeaders, Body, CancellationToken: CancellationToken);
                var responseTime  = timeProvider.GetUtcNow();

                if (headResponse.Status == 200)
                    UpdateFromHEAD(Path, extraHeaders, requestCC, hasAuth, headResponse, requestTime, responseTime);

                return headResponse;

            }

            if (Method != HTTPMethod.GET ||
                extraHeaders.Any(h => h.Name is "range" or "if-match" or "if-unmodified-since" or "if-range"))
                return await connection.SendRequestAsync(Method, scheme, authority, Path, extraHeaders, Body, CancellationToken: CancellationToken);

            // Request no-store: don't use or populate the cache.
            if (requestCC.NoStore)
                return await OriginAndMaybeStore(Method, Path, extraHeaders, Body, requestCC, hasAuth, store: false, CancellationToken);

            var stored = Lookup(Path, extraHeaders);

            if (stored is not null)
            {

                if (!HTTPCache.HasCachePreconditions(extraHeaders))
                    return await FromStoreAsync(Method, Path, extraHeaders, stored, requestCC, hasAuth, CancellationToken);

                // Section 4.3.2: the caller's If-None-Match and If-Modified-Since
                // are evaluated against the 200 the same request without them
                // gets, from the store or from a revalidation with this cache's
                // own validators, and where they match, the answer is a 304.
                // When that 200 was received, for one without Last-Modified and
                // Date, is taken as now: never earlier than it was, so an error
                // answers a 200 where a 304 would have done, never the reverse.
                var answer = await FromStoreAsync(Method, Path, [.. extraHeaders.Where(h => !HTTPCache.IsCachePrecondition(h.Name))],
                                                  stored, requestCC, hasAuth, CancellationToken);

                return answer.Status == 200 && HTTPCache.IsNotModified(extraHeaders, answer.Headers, timeProvider.GetUtcNow())
                           ? NotModified(answer)
                           : answer;

            }

            // Nothing cached: the caller's preconditions are the origin's to evaluate.
            if (requestCC.OnlyIfCached)
                return GatewayTimeout();   // Section 5.2.1.7

            return await OriginAndMaybeStore(Method, Path, extraHeaders, Body, requestCC, hasAuth, store: true, CancellationToken);

        }

        /// <summary>
        /// Answer the request with the stored response it selects: as it is,
        /// where Section 4.2 or the request's directives allow that, and after
        /// revalidating it, where they do not.
        /// </summary>
        private async Task<HTTP2Response> FromStoreAsync(HTTPMethod                         Method,
                                                         String                             Path,
                                                         List<(String Name, String Value)>  ExtraHeaders,
                                                         HTTPStoredResponse                 Stored,
                                                         HTTPCacheControl                   RequestCC,
                                                         Boolean                            HasAuth,
                                                         CancellationToken                  CancellationToken)
        {

            var decision = HTTPCache.Evaluate(Stored, RequestCC, mode, timeProvider.GetUtcNow());

            switch (decision.Usability)
            {

                case HTTPCacheUsability.Fresh:
                case HTTPCacheUsability.Stale:
                    Hits++;
                    return Serve(Stored, decision.Age);

                case HTTPCacheUsability.StaleWhileRevalidate:
                    Hits++;
                    var served = Serve(Stored, decision.Age);
                    _ = Task.Run(() => RevalidateAsync(Method, Path, ExtraHeaders, Stored, RequestCC, HasAuth, CancellationToken.None));
                    return served;

                default:
                    // Section 5.2.1.7: only-if-cached never goes to the origin.
                    if (RequestCC.OnlyIfCached)
                        return GatewayTimeout();
                    return await RevalidateAsync(Method, Path, ExtraHeaders, Stored, RequestCC, HasAuth, CancellationToken);

            }

        }


        #region Origin round trips

        private async Task<HTTP2Response> OriginAndMaybeStore(HTTPMethod                         Method,
                                                              String                             Path,
                                                              List<(String Name, String Value)>  ExtraHeaders,
                                                              Byte[]?                            Body,
                                                              HTTPCacheControl                   RequestCC,
                                                              Boolean                            HasAuth,
                                                              Boolean                            store,
                                                              CancellationToken                  CancellationToken)
        {

            var requestTime = timeProvider.GetUtcNow();
            var response    = await connection.SendRequestAsync(Method, scheme, authority, Path, ExtraHeaders, Body, CancellationToken: CancellationToken);
            var responseTime = timeProvider.GetUtcNow();

            Misses++;

            if (store)
                TryStore(Path, ExtraHeaders, RequestCC, HasAuth, Method, response, requestTime, responseTime);

            return response;

        }

        private async Task<HTTP2Response> RevalidateAsync(
            HTTPMethod Method, string Path, List<(string Name, string Value)> ExtraHeaders,
            HTTPStoredResponse Stored, HTTPCacheControl RequestCC, bool HasAuth, CancellationToken CancellationToken)
        {

            Revalidations++;

            var conditional = new List<(string Name, string Value)>(ExtraHeaders);
            conditional.AddRange(HTTPCache.ConditionalHeaders(Stored));

            var requestTime  = timeProvider.GetUtcNow();
            var response     = await connection.SendRequestAsync(Method, scheme, authority, Path, conditional, null, CancellationToken: CancellationToken);
            var responseTime = timeProvider.GetUtcNow();

            // Section 4.3.4: a 304 that names another representation updates
            // nothing, and the request is sent again without conditions.
            if (response.Status == 304 && !HTTPCache.Selects(Stored, response.Headers))
                return await OriginAndMaybeStore(Method, Path, ExtraHeaders, null, RequestCC, HasAuth, store: true, CancellationToken);

            if (response.Status == 304)
            {
                // Still valid — refresh the stored entry and serve it.
                lock (storeLock)
                    HTTPCache.UpdateFrom304(Stored, response.Headers, requestTime, responseTime);
                return Serve(Stored, HTTPCache.CurrentAge(Stored, timeProvider.GetUtcNow()));
            }

            // Changed — replace the stored entry with the new response, and drop
            // it even where the new one may not be stored.
            Remove(Path, ExtraHeaders);
            TryStore(Path, ExtraHeaders, RequestCC, HasAuth, Method, response, requestTime, responseTime);
            return response;

        }

        #endregion


        #region Store operations

        private HTTPStoredResponse? Lookup(string Path, List<(string Name, string Value)> RequestHeaders)
        {
            lock (storeLock)
            {
                if (!store.TryGetValue(Path, out var variants))
                    return null;
                return variants.FirstOrDefault(v => HTTPCache.SelectionMatches(v, RequestHeaders));
            }
        }

        private void TryStore(
            string Path, List<(string Name, string Value)> RequestHeaders, HTTPCacheControl RequestCC, bool HasAuth,
            HTTPMethod Method, HTTP2Response Response, DateTimeOffset RequestTime, DateTimeOffset ResponseTime)
        {

            var responseCC = HTTPCacheControl.FromHeaders(Response.Headers);

            if (!HTTPCache.IsStorable(Method, HasAuth, RequestCC, Response.Status, Response.Headers, responseCC, mode))
                return;

            var varyNames = HTTPCache.VaryFieldNames(Response.Headers);
            if (varyNames.Contains("*"))
                return;   // Never reusable — don't bother storing.

            var entry = new HTTPStoredResponse {
                Status         = Response.Status,
                Headers        = [.. Response.Headers],
                Body           = Response.Body,
                RequestTime    = RequestTime,
                ResponseTime   = ResponseTime,
                VaryKeyHeaders = HTTPCache.SelectRequestHeaders(RequestHeaders, varyNames)
            };

            lock (storeLock)
            {
                if (!store.TryGetValue(Path, out var variants))
                    store[Path] = variants = [];

                // Replace any variant this request would have selected.
                variants.RemoveAll(v => HTTPCache.SelectionMatches(v, RequestHeaders));
                variants.Add(entry);
            }

        }

        /// <summary>
        /// A 200 to a HEAD updates each stored GET response it could have been
        /// answered from, or makes it stale (RFC 9111, Section 4.3.5). One this
        /// cache could not store updates nothing and only makes stale.
        /// </summary>
        private void UpdateFromHEAD(
            string Path, List<(string Name, string Value)> RequestHeaders, HTTPCacheControl RequestCC, bool HasAuth,
            HTTP2Response Response, DateTimeOffset RequestTime, DateTimeOffset ResponseTime)
        {

            var mayUpdate = HTTPCache.IsStorable(HTTPMethod.HEAD, HasAuth, RequestCC, Response.Status, Response.Headers,
                                                 HTTPCacheControl.FromHeaders(Response.Headers), mode);

            lock (storeLock)
            {
                if (!store.TryGetValue(Path, out var variants))
                    return;

                foreach (var stored in variants.Where(v => HTTPCache.SelectionMatches(v, RequestHeaders)))
                {
                    if (mayUpdate)
                        HTTPCache.UpdateFromHEAD(stored, Response.Headers, RequestTime, ResponseTime);
                    else
                        stored.ConsideredStale = true;
                }
            }

        }

        private void Remove(string Path, List<(string Name, string Value)> RequestHeaders)
        {
            lock (storeLock)
                if (store.TryGetValue(Path, out var variants))
                    variants.RemoveAll(v => HTTPCache.SelectionMatches(v, RequestHeaders));
        }

        /// <summary>
        /// The 304 (Not Modified) for a 200 that the request's own preconditions
        /// match, with the fields RFC 9110, Section 15.4.5 has a 304 carry.
        /// </summary>
        private static HTTP2Response NotModified(HTTP2Response Answer)
            => new() { Status = 304, Headers = [(":status", "304"), .. HTTPCache.NotModifiedHeaders(Answer.Headers)], Body = [] };

        private static HTTP2Response GatewayTimeout()
            => new() { Status = 504, Headers = [(":status", "504")], Body = [] };

        private void Invalidate(string Path)
        {
            lock (storeLock)
                store.Remove(Path);
        }

        /// <summary>
        /// Build a response to serve from cache, stamping the computed <c>Age</c> (RFC 9111, Section 5.1).
        /// </summary>
        private static HTTP2Response Serve(HTTPStoredResponse Stored, TimeSpan Age)
        {
            var headers = Stored.Headers.Where(h => h.Name != "age").ToList();
            headers.Add(("age", ((long) Math.Max(0, Age.TotalSeconds)).ToString()));
            return new HTTP2Response { Status = Stored.Status, Headers = headers, Body = Stored.Body };
        }

        #endregion

    }

}
