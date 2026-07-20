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
    /// The direction-neutral RFC 9111 caching *logic* — storability, age and
    /// freshness computation, revalidation, <c>Vary</c> keying — with no notion of
    /// an actual store or transport. The store and the "when do I go to the
    /// origin?" plumbing live in <see cref="HTTP2CachingClient"/> (client-side);
    /// this class is the reusable brain, sitting in the shared library next to
    /// <see cref="HTTPSemantics"/> (whose conditional-request handling this is the
    /// cache/client counterpart of — a cache is what *generates* the
    /// If-None-Match/If-Modified-Since revalidations that HTTPSemantics answers).
    /// </summary>
    public static class HTTPCache
    {

        /// <summary>
        /// Status codes a response can be stored under heuristically, absent
        /// explicit freshness (RFC 9110, Section 15.1 "heuristically cacheable").
        /// </summary>
        private static readonly HashSet<int> HeuristicallyCacheableStatus =
            [200, 203, 204, 206, 300, 301, 308, 404, 405, 410, 414, 451, 501];

        #region Storability (Section 3)

        /// <summary>
        /// May a cache in Mode store this response to a request with the given
        /// method + request Cache-Control? (RFC 9111, Section 3, plus the shared-cache
        /// Authorization rule of Section 3.5.)
        /// </summary>
        public static bool IsStorable(
            string                            Method,
            bool                              RequestHasAuthorization,
            HTTPCacheControl                  RequestCacheControl,
            int                               Status,
            List<(string Name, string Value)> ResponseHeaders,
            HTTPCacheControl                  ResponseCacheControl,
            HTTPCacheMode                     Mode)
        {

            // Only responses to safe, cacheable methods (GET/HEAD here).
            if (Method is not ("GET" or "HEAD"))
                return false;

            // no-store on either side forbids storage.
            if (RequestCacheControl.NoStore || ResponseCacheControl.NoStore)
                return false;

            // A shared cache must not store a "private" response.
            if (Mode == HTTPCacheMode.Shared && ResponseCacheControl.Private)
                return false;

            // A shared cache must not store a response to an authenticated request
            // unless explicitly allowed (Section 3.5).
            if (Mode == HTTPCacheMode.Shared && RequestHasAuthorization &&
                !(ResponseCacheControl.Public || ResponseCacheControl.SMaxAge is not null || ResponseCacheControl.MustRevalidate))
                return false;

            // Storable if there's explicit freshness info or a validator or the
            // status is heuristically cacheable (Section 3).
            var hasExplicitFreshness = ResponseCacheControl.MaxAge is not null
                                    || ResponseCacheControl.SMaxAge is not null
                                    || ResponseHeaders.Any(h => h.Name == "expires");
            var hasValidator = ResponseHeaders.Any(h => h.Name is "etag" or "last-modified");

            return hasExplicitFreshness || hasValidator || HeuristicallyCacheableStatus.Contains(Status);

        }

        #endregion


        #region Age + freshness (Section 4.2)

        /// <summary>Current age of a stored response (RFC 9111, Section 4.2.3).</summary>
        public static TimeSpan CurrentAge(HTTPStoredResponse Stored, DateTimeOffset Now)
        {

            var dateValue    = Stored.DateValue ?? Stored.ResponseTime;
            var apparentAge  = Max(TimeSpan.Zero, Stored.ResponseTime - dateValue);
            var responseDelay = Stored.ResponseTime - Stored.RequestTime;
            var correctedAge = TimeSpan.FromSeconds(Stored.AgeValue) + responseDelay;
            var correctedInitialAge = Max(apparentAge, correctedAge);
            var residentTime = Now - Stored.ResponseTime;

            return correctedInitialAge + residentTime;

        }

        /// <summary>
        /// Freshness lifetime of a stored response (RFC 9111, Section 4.2.1), in
        /// the given cache mode. Returns null if none can be determined and the
        /// status isn't heuristically cacheable — such a response is always
        /// treated as needing revalidation.
        /// </summary>
        public static TimeSpan? FreshnessLifetime(HTTPStoredResponse Stored, HTTPCacheMode Mode)
        {

            var cc = Stored.CacheControl;

            // s-maxage takes precedence for a shared cache.
            if (Mode == HTTPCacheMode.Shared && cc.SMaxAge is not null)
                return TimeSpan.FromSeconds(cc.SMaxAge.Value);

            if (cc.MaxAge is not null)
                return TimeSpan.FromSeconds(cc.MaxAge.Value);

            // Expires - Date (Section 4.2.1).
            var expires = TryParseHttpDate(Stored.Header("expires"));
            if (expires is not null)
            {
                var date = Stored.DateValue ?? Stored.ResponseTime;
                return expires.Value - date;
            }

            // Heuristic: 10% of the interval since Last-Modified (Section 4.2.2).
            var lastModified = TryParseHttpDate(Stored.LastModified);
            if (lastModified is not null && HeuristicallyCacheableStatus.Contains(Stored.Status))
            {
                var date = Stored.DateValue ?? Stored.ResponseTime;
                var interval = date - lastModified.Value;
                if (interval > TimeSpan.Zero)
                    return interval * 0.1;
            }

            return null;

        }

        /// <summary>
        /// Decide how a stored response may be used for a request right now:
        /// served as fresh, served stale (max-stale / stale-while-revalidate),
        /// or must be revalidated first. Encodes the request/response directive
        /// interplay of RFC 9111 Sections 4.2 / 5.2.
        /// </summary>
        public static HTTPCacheDecision Evaluate(
            HTTPStoredResponse Stored,
            HTTPCacheControl   RequestCacheControl,
            HTTPCacheMode      Mode,
            DateTimeOffset     Now)
        {

            var responseCC = Stored.CacheControl;

            // no-cache (on request or response) and must-revalidate force validation.
            var mustRevalidate = RequestCacheControl.NoCache
                              || responseCC.NoCache
                              || responseCC.MustRevalidate
                              || (Mode == HTTPCacheMode.Shared && responseCC.ProxyRevalidate);

            var age      = CurrentAge(Stored, Now);
            var lifetime = FreshnessLifetime(Stored, Mode);

            // immutable: fresh within its lifetime, never revalidate early (we still
            // respect the lifetime; immutable mainly suppresses conditional requests
            // a client might otherwise send — modeled as "don't force revalidation").
            var freshnessLifetime = lifetime ?? TimeSpan.Zero;
            var staleness         = age - freshnessLifetime;   // >0 means stale by that much
            var isFresh           = staleness < TimeSpan.Zero;

            // Request min-fresh: the response must stay fresh for at least N more seconds.
            if (isFresh && RequestCacheControl.MinFresh is not null &&
                (freshnessLifetime - age) < TimeSpan.FromSeconds(RequestCacheControl.MinFresh.Value))
                isFresh = false;

            if (isFresh && !mustRevalidate)
                return new HTTPCacheDecision(HTTPCacheUsability.Fresh, age);

            // Stale. A stale response may still be served if the request allows it
            // via max-stale and the response doesn't demand revalidation.
            if (!mustRevalidate && staleness >= TimeSpan.Zero)
            {
                if (RequestCacheControl.MaxStaleAny)
                    return new HTTPCacheDecision(HTTPCacheUsability.Stale, age);

                if (RequestCacheControl.MaxStale is not null &&
                    staleness <= TimeSpan.FromSeconds(RequestCacheControl.MaxStale.Value))
                    return new HTTPCacheDecision(HTTPCacheUsability.Stale, age);

                // RFC 5861 stale-while-revalidate: serve stale now, revalidate in
                // the background, within the SWR window past expiry.
                if (responseCC.StaleWhileRevalidate is not null &&
                    staleness <= TimeSpan.FromSeconds(responseCC.StaleWhileRevalidate.Value))
                    return new HTTPCacheDecision(HTTPCacheUsability.StaleWhileRevalidate, age);
            }

            return new HTTPCacheDecision(HTTPCacheUsability.MustRevalidate, age);

        }

        #endregion


        #region Revalidation (Section 4.3)

        /// <summary>
        /// Build the conditional request-header fields to revalidate Stored
        /// (RFC 9111, Section 4.3.1): <c>If-None-Match</c> from its ETag and/or
        /// <c>If-Modified-Since</c> from its Last-Modified.
        /// </summary>
        public static List<(string Name, string Value)> ConditionalHeaders(HTTPStoredResponse Stored)
        {

            var headers = new List<(string Name, string Value)>();

            if (Stored.ETag is not null)
                headers.Add(("if-none-match", Stored.ETag));

            if (Stored.LastModified is not null)
                headers.Add(("if-modified-since", Stored.LastModified));

            return headers;

        }

        /// <summary>
        /// Apply a 304 (Not Modified) revalidation result to a stored response
        /// (RFC 9111, Section 3.2): refresh its stored header fields from the 304's
        /// (validators, Cache-Control, Date, …) and reset its timing so it counts
        /// as freshly validated.
        /// </summary>
        public static void UpdateFrom304(
            HTTPStoredResponse                Stored,
            List<(string Name, string Value)> NotModifiedHeaders,
            DateTimeOffset                    RequestTime,
            DateTimeOffset                    ResponseTime)
        {

            // Replace/refresh each header the 304 carries (except the framing pseudo :status).
            foreach (var (name, value) in NotModifiedHeaders)
            {
                if (name.StartsWith(':'))
                    continue;
                Stored.Headers.RemoveAll(h => h.Name == name);
                Stored.Headers.Add((name, value));
            }

            Stored.RequestTime  = RequestTime;
            Stored.ResponseTime = ResponseTime;

        }

        #endregion


        #region Vary keying (Section 4.1)

        /// <summary>
        /// The request-header field names a response's <c>Vary</c> selects on
        /// (lowercased). A <c>Vary: *</c> is reported as the single name "*",
        /// which <see cref="SelectionMatches"/> treats as never-reusable.
        /// </summary>
        public static List<string> VaryFieldNames(List<(string Name, string Value)> ResponseHeaders)
            => ResponseHeaders.Where(h => h.Name == "vary")
                              .SelectMany(h => h.Value.Split(','))
                              .Select(v => v.Trim().ToLowerInvariant())
                              .Where(v => v.Length > 0)
                              .Distinct()
                              .ToList();

        /// <summary>Capture the values of the Vary-selected request headers, for storing alongside a response.</summary>
        public static List<(string Name, string Value)> SelectRequestHeaders(
            List<(string Name, string Value)> RequestHeaders,
            List<string>                      VaryNames)
            => VaryNames.Where(n => n != "*")
                        .Select(n => (n, RequestHeaders.FirstOrDefault(h => h.Name == n).Value ?? ""))
                        .ToList();

        /// <summary>
        /// Does a new request's selecting headers match those a stored variant was
        /// keyed on (RFC 9111, Section 4.1)? A stored variant whose Vary included
        /// "*" never matches.
        /// </summary>
        public static bool SelectionMatches(
            HTTPStoredResponse                Stored,
            List<(string Name, string Value)> RequestHeaders)
        {

            var varyNames = VaryFieldNames(Stored.Headers);

            if (varyNames.Contains("*"))
                return false;

            foreach (var (name, storedValue) in Stored.VaryKeyHeaders)
            {
                var current = RequestHeaders.FirstOrDefault(h => h.Name == name).Value ?? "";
                if (!string.Equals(current, storedValue, StringComparison.Ordinal))
                    return false;
            }

            return true;

        }

        #endregion


        internal static DateTimeOffset? TryParseHttpDate(string? Value)
            => Value is not null &&
               (DateTimeOffset.TryParseExact(Value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var r)
                || DateTimeOffset.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out r))
                    ? r
                    : null;

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    }

}
