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
        private readonly string                scheme;
        private readonly string                authority;
        private readonly HTTPCacheMode         mode;

        // Keyed by :path; each path may hold several Vary-selected variants.
        private readonly Dictionary<string, List<HTTPStoredResponse>> store = [];
        private readonly object                                        storeLock = new();

        /// <summary>Fresh/stale cache hits served without an origin round trip.</summary>
        public int Hits           { get; private set; }
        /// <summary>Requests that went to the origin because nothing usable was cached.</summary>
        public int Misses         { get; private set; }
        /// <summary>Stale entries revalidated with the origin via a conditional request.</summary>
        public int Revalidations  { get; private set; }


        public HTTP2CachingClient(
            HTTP2ClientConnection Connection,
            string                Scheme,
            string                Authority,
            HTTPCacheMode         Mode = HTTPCacheMode.Private)
        {
            connection = Connection;
            scheme     = Scheme;
            authority  = Authority;
            mode       = Mode;
        }


        /// <summary>Convenience GET.</summary>
        public Task<HTTP2Response> GetAsync(
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            CancellationToken                  CancellationToken = default)
            => SendRequestAsync("GET", Path, ExtraHeaders, null, CancellationToken);

        /// <summary>
        /// Send a request through the cache. GET/HEAD are cached per RFC 9111;
        /// other (unsafe) methods bypass the cache and, on success, invalidate any
        /// stored entry for the target (Section 4.4).
        /// </summary>
        public async Task<HTTP2Response> SendRequestAsync(
            string                             Method,
            string                             Path,
            List<(string Name, string Value)>? ExtraHeaders = null,
            byte[]?                            Body         = null,
            CancellationToken                  CancellationToken = default)
        {

            var extraHeaders = ExtraHeaders ?? [];
            var requestCC    = HTTPCacheControl.FromHeaders(extraHeaders);
            var hasAuth      = extraHeaders.Any(h => h.Name == "authorization");

            // Unsafe / non-cacheable methods: straight to origin, then invalidate.
            if (Method is not ("GET" or "HEAD"))
            {
                var resp = await connection.SendRequestAsync(Method, scheme, authority, Path, extraHeaders, Body, CancellationToken: CancellationToken);
                if (resp.Status is >= 200 and < 400)
                    Invalidate(Path);
                return resp;
            }

            // Request no-store: don't use or populate the cache.
            if (requestCC.NoStore)
                return await OriginAndMaybeStore(Method, Path, extraHeaders, Body, requestCC, hasAuth, store: false, CancellationToken);

            var stored = Lookup(Path, extraHeaders);

            if (stored is not null)
            {

                var decision = HTTPCache.Evaluate(stored, requestCC, mode, DateTimeOffset.UtcNow);

                switch (decision.Usability)
                {

                    case HTTPCacheUsability.Fresh:
                    case HTTPCacheUsability.Stale:
                        Hits++;
                        return Serve(stored, decision.Age);

                    case HTTPCacheUsability.StaleWhileRevalidate:
                        Hits++;
                        var served = Serve(stored, decision.Age);
                        _ = Task.Run(() => RevalidateAsync(Method, Path, extraHeaders, stored, requestCC, hasAuth, CancellationToken.None));
                        return served;

                    case HTTPCacheUsability.MustRevalidate:
                        return await RevalidateAsync(Method, Path, extraHeaders, stored, requestCC, hasAuth, CancellationToken);

                }

            }

            // Nothing cached.
            if (requestCC.OnlyIfCached)
                return new HTTP2Response { Status = 504, Headers = [(":status", "504")], Body = [] };   // Section 5.2.1.7

            return await OriginAndMaybeStore(Method, Path, extraHeaders, Body, requestCC, hasAuth, store: true, CancellationToken);

        }


        #region Origin round trips

        private async Task<HTTP2Response> OriginAndMaybeStore(
            string Method, string Path, List<(string Name, string Value)> ExtraHeaders, byte[]? Body,
            HTTPCacheControl RequestCC, bool HasAuth, bool store, CancellationToken CancellationToken)
        {

            var requestTime = DateTimeOffset.UtcNow;
            var response    = await connection.SendRequestAsync(Method, scheme, authority, Path, ExtraHeaders, Body, CancellationToken: CancellationToken);
            var responseTime = DateTimeOffset.UtcNow;

            Misses++;

            if (store)
                TryStore(Path, ExtraHeaders, RequestCC, HasAuth, Method, response, requestTime, responseTime);

            return response;

        }

        private async Task<HTTP2Response> RevalidateAsync(
            string Method, string Path, List<(string Name, string Value)> ExtraHeaders,
            HTTPStoredResponse Stored, HTTPCacheControl RequestCC, bool HasAuth, CancellationToken CancellationToken)
        {

            Revalidations++;

            var conditional = new List<(string Name, string Value)>(ExtraHeaders);
            conditional.AddRange(HTTPCache.ConditionalHeaders(Stored));

            var requestTime  = DateTimeOffset.UtcNow;
            var response     = await connection.SendRequestAsync(Method, scheme, authority, Path, conditional, null, CancellationToken: CancellationToken);
            var responseTime = DateTimeOffset.UtcNow;

            if (response.Status == 304)
            {
                // Still valid — refresh the stored entry and serve it.
                lock (storeLock)
                    HTTPCache.UpdateFrom304(Stored, response.Headers, requestTime, responseTime);
                return Serve(Stored, HTTPCache.CurrentAge(Stored, DateTimeOffset.UtcNow));
            }

            // Changed — replace the stored entry with the new response.
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
            string Method, HTTP2Response Response, DateTimeOffset RequestTime, DateTimeOffset ResponseTime)
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

        private void Invalidate(string Path)
        {
            lock (storeLock)
                store.Remove(Path);
        }

        /// <summary>Build a response to serve from cache, stamping the computed <c>Age</c> (RFC 9111, Section 5.1).</summary>
        private static HTTP2Response Serve(HTTPStoredResponse Stored, TimeSpan Age)
        {
            var headers = Stored.Headers.Where(h => h.Name != "age").ToList();
            headers.Add(("age", ((long) Math.Max(0, Age.TotalSeconds)).ToString()));
            return new HTTP2Response { Status = Stored.Status, Headers = headers, Body = Stored.Body };
        }

        #endregion

    }

}
