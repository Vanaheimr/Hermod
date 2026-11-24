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
    /// The version-neutral RFC 9111 caching *logic* — storability, age and
    /// freshness computation, revalidation, <c>Vary</c> keying — with no notion
    /// of an actual store or transport. The store and the "when do I go to the
    /// origin?" plumbing live in the clients: HTTP2CachingClient for HTTP/2 and
    /// HTTPClientCache for HTTP/1.x. It is the cache's counterpart of
    /// <see cref="HTTPResourceSemantics"/>: a cache is what generates the
    /// If-None-Match and If-Modified-Since revalidations that layer answers.
    ///
    /// It was HTTP/2's alone until 2026-11-21 (H-5 in HTTP1ConformanceTests),
    /// when it moved here from HTTP2/Core and lost its deviations on the way:
    /// a fresh must-revalidate response was revalidated anyway, a request's
    /// max-age was ignored, an invalid Expires let heuristic freshness apply
    /// instead of making the response stale, a validator alone made any status
    /// storable, a 206 was storable as if it were the whole representation, a
    /// 304 overwrote the stored Content-Length, a 304 updated a stored response
    /// whatever validator it carried, and Vary compared the first line of a
    /// field only. HTTP1/README.md, "The client: a cache", has the list.
    /// </summary>
    public static class HTTPCache
    {

        #region Data

        /// <summary>
        /// Status codes a response can be stored under heuristically, absent
        /// explicit freshness (RFC 9110, Section 15.1 "heuristically cacheable",
        /// and RFC 7725 for 451).
        /// </summary>
        private static readonly HashSet<Int32> HeuristicallyCacheableStatus =
            [200, 203, 204, 206, 300, 301, 308, 404, 405, 410, 414, 451, 501];

        /// <summary>
        /// Fields a cache does not store (RFC 9111, Section 3.1): the
        /// connection-specific ones of RFC 9110, Section 7.6.1, and the proxy
        /// authentication fields, which concern the hop and not the response.
        /// </summary>
        private static readonly HashSet<String> NotStored = [
            "connection", "proxy-connection", "keep-alive", "te", "transfer-encoding",
            "trailer", "upgrade", "proxy-authenticate", "proxy-authentication-info"
        ];

        /// <summary>
        /// Fields a 304 does not update (RFC 9111, Section 3.2): Content-Length
        /// by name, and the fields that describe the stored content's octets,
        /// which a response without content cannot speak for. A client that
        /// decoded the content it stored has dropped its Content-Encoding, and a
        /// 304 that brought it back would mislabel the octets.
        /// </summary>
        private static readonly HashSet<String> NotUpdated = [
            "content-length", "content-encoding", "content-range"
        ];

        #endregion


        #region Storability (Section 3)

        /// <summary>
        /// May a cache in Mode store this response to a request with the given
        /// method and request Cache-Control? (RFC 9111, Section 3, with the
        /// shared-cache Authorization rule of Section 3.5.)
        ///
        /// A 206 and a 304 are never storable here: neither cache combines
        /// partial content (Sections 3.3 and 3.4), and a 304 only updates what
        /// is stored already (Section 4.3.4).
        /// </summary>
        public static Boolean IsStorable(HTTPMethod                         Method,
                                         Boolean                            RequestHasAuthorization,
                                         HTTPCacheControl                   RequestCacheControl,
                                         Int32                              Status,
                                         List<(String Name, String Value)>  ResponseHeaders,
                                         HTTPCacheControl                   ResponseCacheControl,
                                         HTTPCacheMode                      Mode)
        {

            // Only responses to safe, cacheable methods (GET/HEAD here).
            if (Method != HTTPMethod.GET && Method != HTTPMethod.HEAD)
                return false;

            // Only final responses, and none this cache cannot use whole.
            if (Status < 200 || Status is 206 or 304)
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

            // Section 3: the response says how long it is fresh, says it may be
            // stored, or has a status that may be stored on a heuristic. A
            // validator alone is none of these.
            return ResponseCacheControl.Public
                || (Mode == HTTPCacheMode.Private && ResponseCacheControl.Private)
                || ResponseCacheControl.MaxAge is not null
                || (Mode == HTTPCacheMode.Shared  && ResponseCacheControl.SMaxAge is not null)
                || ResponseHeaders.Any(header => header.Name == "expires")
                || HeuristicallyCacheableStatus.Contains(Status);

        }

        #endregion

        #region IsStoredField(Name, ConnectionOptions)

        /// <summary>
        /// Whether a cache keeps this field of a response it stores (RFC 9111,
        /// Section 3.1): not a connection-specific one, and none that the
        /// response's Connection field names.
        /// </summary>
        /// <param name="Name">A lowercase field name.</param>
        /// <param name="ConnectionOptions">The lowercase names the response's Connection field lists.</param>
        public static Boolean IsStoredField(String               Name,
                                            IReadOnlySet<String>  ConnectionOptions)

            => !NotStored.Contains(Name) &&
               !ConnectionOptions.Contains(Name);

        #endregion

        #region ConnectionOptions(Headers)

        /// <summary>
        /// The lowercase field names a Connection field lists (RFC 9110, Section 7.6.1).
        /// </summary>
        public static HashSet<String> ConnectionOptions(List<(String Name, String Value)> Headers)

            => Headers.Where     (header => header.Name == "connection").
                       SelectMany(header => header.Value.Split(',')).
                       Select    (option => option.Trim().ToLowerInvariant()).
                       Where     (option => option.Length > 0).
                       ToHashSet();

        #endregion

        #region Invalidates(Method, Status)

        /// <summary>
        /// Whether a response to this request invalidates what is stored for its
        /// target (RFC 9111, Section 4.4): an unsafe method and a non-error
        /// status, which is a 2xx or a 3xx.
        /// </summary>
        public static Boolean Invalidates(HTTPMethod  Method,
                                          Int32       Status)

            => !Method.IsSafe && Status is >= 200 and < 400;

        #endregion


        #region Age + freshness (Section 4.2)

        /// <summary>
        /// Current age of a stored response (RFC 9111, Section 4.2.3).
        /// </summary>
        public static TimeSpan CurrentAge(HTTPStoredResponse  Stored,
                                          DateTimeOffset      Now)
        {

            var dateValue            = Stored.DateValue ?? Stored.ResponseTime;
            var apparentAge          = Max(TimeSpan.Zero, Stored.ResponseTime - dateValue);
            var responseDelay        = Stored.ResponseTime - Stored.RequestTime;
            var correctedAge         = TimeSpan.FromSeconds(Stored.AgeValue) + responseDelay;
            var correctedInitialAge  = Max(apparentAge, correctedAge);
            var residentTime         = Now - Stored.ResponseTime;

            return correctedInitialAge + residentTime;

        }

        /// <summary>
        /// Freshness lifetime of a stored response (RFC 9111, Section 4.2.1), in
        /// the given cache mode. Null when none can be determined, explicitly or
        /// by a heuristic: such a response is always revalidated.
        ///
        /// An Expires that is not one valid HTTP-date, "0" being the common
        /// case, means the response has already expired (Section 5.3), and so
        /// does more than one of them (Section 4.2.1). Neither falls back to the
        /// heuristic, which is only for responses that say nothing.
        /// </summary>
        public static TimeSpan? FreshnessLifetime(HTTPStoredResponse  Stored,
                                                  HTTPCacheMode       Mode)
        {

            var cc = Stored.CacheControl;

            // s-maxage takes precedence for a shared cache.
            if (Mode == HTTPCacheMode.Shared && cc.SMaxAge is not null)
                return TimeSpan.FromSeconds(cc.SMaxAge.Value);

            if (cc.MaxAge is not null)
                return TimeSpan.FromSeconds(cc.MaxAge.Value);

            // Expires - Date (Section 4.2.1).
            var expires = Stored.Headers.Where(header => header.Name == "expires").ToList();
            if (expires.Count > 0)
            {

                if (expires.Count > 1 || TryParseHTTPDate(expires[0].Value) is not DateTimeOffset expiry)
                    return TimeSpan.Zero;

                return expiry - (Stored.DateValue ?? Stored.ResponseTime);

            }

            // Heuristic: 10% of the interval since Last-Modified (Section 4.2.2),
            // for a status that allows it or a response marked public.
            var lastModified = TryParseHTTPDate(Stored.LastModified);
            if (lastModified is not null &&
                (HeuristicallyCacheableStatus.Contains(Stored.Status) || cc.Public))
            {

                var interval = (Stored.DateValue ?? Stored.ResponseTime) - lastModified.Value;

                if (interval > TimeSpan.Zero)
                    return interval * 0.1;

            }

            return null;

        }

        #endregion

        #region Evaluate(Stored, RequestCacheControl, Mode, Now)

        /// <summary>
        /// Decide how a stored response may be used for a request right now:
        /// served as fresh, served stale (max-stale / stale-while-revalidate),
        /// or must be revalidated first. Encodes the request/response directive
        /// interplay of RFC 9111, Sections 4.2 and 5.2.
        /// </summary>
        public static HTTPCacheDecision Evaluate(HTTPStoredResponse  Stored,
                                                 HTTPCacheControl    RequestCacheControl,
                                                 HTTPCacheMode       Mode,
                                                 DateTimeOffset      Now)
        {

            var responseCC  = Stored.CacheControl;
            var age         = CurrentAge(Stored, Now);
            var staleness   = Staleness(Stored, age, Mode);   // >= 0 means stale by that much
            var isFresh     = staleness < TimeSpan.Zero;

            // no-cache, on either side, allows no use without validation
            // (Sections 5.2.1.4 and 5.2.2.4), fresh or not.
            var mustValidate = RequestCacheControl.NoCache || responseCC.NoCache;

            // A request's max-age: the client wants no response older than this
            // (Section 5.2.1.1). RFC 8246, Section 2: an immutable response is
            // not revalidated while fresh, which is what a reload's max-age=0
            // would otherwise do. no-cache still does.
            if (isFresh && RequestCacheControl.MaxAge is not null && !responseCC.Immutable &&
                age > TimeSpan.FromSeconds(RequestCacheControl.MaxAge.Value))
                isFresh = false;

            // A request's min-fresh: the response must stay fresh for at least N more seconds.
            if (isFresh && RequestCacheControl.MinFresh is not null &&
                -staleness < TimeSpan.FromSeconds(RequestCacheControl.MinFresh.Value))
                isFresh = false;

            if (isFresh && !mustValidate)
                return new HTTPCacheDecision(HTTPCacheUsability.Fresh, age);

            // Stale. A stale response may still be served if the request allows it
            // via max-stale and the response does not forbid it (Section 4.2.4).
            if (!mustValidate && !ForbidsStale(responseCC, Mode) && staleness >= TimeSpan.Zero)
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

        #region ServesStaleOnError(Stored, RequestCacheControl, Mode, Now)

        /// <summary>
        /// Whether a stored response may stand in for an error (RFC 5861,
        /// Section 4): the origin answered 500, 502, 503 or 504, or could not
        /// be reached, and the response is stale by no more than the
        /// stale-if-error of the response or of the request. Not where Section
        /// 4.2.4 of RFC 9111 forbids serving stale at all.
        /// </summary>
        public static Boolean ServesStaleOnError(HTTPStoredResponse  Stored,
                                                 HTTPCacheControl    RequestCacheControl,
                                                 HTTPCacheMode       Mode,
                                                 DateTimeOffset      Now)
        {

            var responseCC = Stored.CacheControl;

            if (RequestCacheControl.NoCache || responseCC.NoCache || ForbidsStale(responseCC, Mode))
                return false;

            var window = Math.Max(responseCC.StaleIfError          ?? -1,
                                  RequestCacheControl.StaleIfError ?? -1);

            if (window < 0)
                return false;

            return Staleness(Stored, CurrentAge(Stored, Now), Mode) <= TimeSpan.FromSeconds(window);

        }

        /// <summary>
        /// Whether a status is one RFC 5861, Section 4 counts as an error: 500,
        /// 502, 503 or 504.
        /// </summary>
        public static Boolean IsStaleIfErrorStatus(Int32 Status)

            => Status is 500 or 502 or 503 or 504;

        #endregion

        #region (private) Staleness(Stored, Age, Mode)

        /// <summary>
        /// By how much a stored response of this age is stale: negative while
        /// it is fresh. A response <see cref="HTTPStoredResponse.ConsideredStale"/>
        /// is stale by at least zero, whatever its freshness lifetime says.
        /// </summary>
        private static TimeSpan Staleness(HTTPStoredResponse  Stored,
                                          TimeSpan            Age,
                                          HTTPCacheMode       Mode)
        {

            var staleness = Age - (FreshnessLifetime(Stored, Mode) ?? TimeSpan.Zero);

            return Stored.ConsideredStale
                       ? Max(staleness, TimeSpan.Zero)
                       : staleness;

        }

        #endregion

        #region (private) ForbidsStale(ResponseCacheControl, Mode)

        /// <summary>
        /// Whether the response forbids being served stale (RFC 9111, Section
        /// 4.2.4): must-revalidate, and in a shared cache proxy-revalidate and
        /// s-maxage, which implies it (Section 5.2.2.10).
        /// </summary>
        private static Boolean ForbidsStale(HTTPCacheControl  ResponseCacheControl,
                                            HTTPCacheMode     Mode)

            => ResponseCacheControl.MustRevalidate ||
               (Mode == HTTPCacheMode.Shared &&
                (ResponseCacheControl.ProxyRevalidate || ResponseCacheControl.SMaxAge is not null));

        #endregion


        #region Revalidation (Section 4.3)

        /// <summary>
        /// Build the conditional request-header fields to revalidate Stored
        /// (RFC 9111, Section 4.3.1): <c>If-None-Match</c> from its ETag and/or
        /// <c>If-Modified-Since</c> from its Last-Modified.
        /// </summary>
        public static List<(String Name, String Value)> ConditionalHeaders(HTTPStoredResponse Stored)
        {

            var headers = new List<(String Name, String Value)>();

            if (Stored.ETag is not null)
                headers.Add(("if-none-match",      Stored.ETag));

            if (Stored.LastModified is not null)
                headers.Add(("if-modified-since",  Stored.LastModified));

            return headers;

        }

        /// <summary>
        /// Whether a 304 speaks for this stored response (RFC 9111, Section
        /// 4.3.4): its entity tag, when it has one, is the stored one; else its
        /// Last-Modified, when it has one, is the stored one; else it carries no
        /// validator and selects the response it was asked about. A 304 that
        /// selects nothing must not update anything, and the request is sent
        /// again without conditions.
        /// </summary>
        public static Boolean Selects(HTTPStoredResponse                 Stored,
                                      List<(String Name, String Value)>  NotModifiedHeaders)
        {

            var etag = NotModifiedHeaders.FirstOrDefault(header => header.Name == "etag").Value;
            if (etag is not null)
                return Stored.ETag is not null && etag.Trim() == Stored.ETag.Trim();

            var lastModified = NotModifiedHeaders.FirstOrDefault(header => header.Name == "last-modified").Value;
            if (lastModified is not null)
                return Stored.LastModified is not null && lastModified.Trim() == Stored.LastModified.Trim();

            return true;

        }

        /// <summary>
        /// Apply a 304 (Not Modified) revalidation result to a stored response
        /// (RFC 9111, Sections 3.2 and 4.3.4): refresh its stored header fields
        /// from the 304's (validators, Cache-Control, Date, …), except the ones
        /// Section 3.2 keeps and the ones Section 3.1 never stores, and reset its
        /// timing so it counts as freshly validated.
        /// </summary>
        public static void UpdateFrom304(HTTPStoredResponse                 Stored,
                                         List<(String Name, String Value)>  NotModifiedHeaders,
                                         DateTimeOffset                     RequestTime,
                                         DateTimeOffset                     ResponseTime)

            => UpdateHeaders(Stored, NotModifiedHeaders, RequestTime, ResponseTime);

        #endregion

        #region HEAD (Section 4.3.5)

        /// <summary>
        /// Whether a 200 (OK) to a HEAD speaks for this stored GET response (RFC
        /// 9111, Section 4.3.5): each validator it carries, ETag and
        /// Last-Modified, is the stored one, and so is its Content-Length, when
        /// it has one. A HEAD response without any of them speaks for whatever
        /// it was asked about.
        ///
        /// The stored Content-Length is the stored field, or the length of the
        /// stored octets where there is none. An HTTP/1.x client that decoded
        /// the content it stored has rewritten that field to the decoded
        /// length, which the HEAD's encoded one does not match. Such a response
        /// is considered stale, and its next use revalidates it: one
        /// conditional request more than necessary, never one less.
        /// </summary>
        public static Boolean HEADResponseMatches(HTTPStoredResponse                 Stored,
                                                  List<(String Name, String Value)>  HEADHeaders)
        {

            static Boolean Same(String? Received, String? Held)
                => Received is null || (Held is not null && Received.Trim() == Held.Trim());

            return Same(HEADHeaders.FirstOrDefault(header => header.Name == "etag").          Value, Stored.ETag)         &&
                   Same(HEADHeaders.FirstOrDefault(header => header.Name == "last-modified"). Value, Stored.LastModified) &&
                   Same(HEADHeaders.FirstOrDefault(header => header.Name == "content-length").Value, Stored.Header("content-length") ?? Stored.Body.LongLength.ToString());

        }

        /// <summary>
        /// Apply a 200 (OK) to a HEAD to a stored GET response that it could
        /// have been answered from (RFC 9111, Section 4.3.5). Where the HEAD
        /// response matches it, see <see cref="HEADResponseMatches"/>, its header
        /// fields update the stored ones as a 304's do (Section 3.2), and the
        /// stored response counts as freshly validated. Otherwise the stored
        /// response is <see cref="HTTPStoredResponse.ConsideredStale"/>.
        /// </summary>
        /// <returns>Whether the stored response was updated.</returns>
        public static Boolean UpdateFromHEAD(HTTPStoredResponse                 Stored,
                                             List<(String Name, String Value)>  HEADHeaders,
                                             DateTimeOffset                     RequestTime,
                                             DateTimeOffset                     ResponseTime)
        {

            if (!HEADResponseMatches(Stored, HEADHeaders))
            {
                Stored.ConsideredStale = true;
                return false;
            }

            UpdateHeaders(Stored, HEADHeaders, RequestTime, ResponseTime);

            return true;

        }

        #endregion

        #region (private) UpdateHeaders(Stored, Headers, RequestTime, ResponseTime)

        /// <summary>
        /// Update a stored response's header fields from a response without
        /// content that speaks for it, a 304 or a 200 to a HEAD (RFC 9111,
        /// Section 3.2), and reset its timing: it has just been validated.
        /// </summary>
        private static void UpdateHeaders(HTTPStoredResponse                 Stored,
                                          List<(String Name, String Value)>  Headers,
                                          DateTimeOffset                     RequestTime,
                                          DateTimeOffset                     ResponseTime)
        {

            var connectionOptions = ConnectionOptions(Headers);

            var updates = Headers.Where(header => !header.Name.StartsWith(':')            &&
                                                  !NotUpdated.Contains(header.Name)       &&
                                                  IsStoredField(header.Name, connectionOptions)).
                                  ToList();

            // Replace each field the response carries, all of its lines at once.
            foreach (var name in updates.Select(header => header.Name).Distinct())
                Stored.Headers.RemoveAll(header => header.Name == name);

            Stored.Headers.AddRange(updates);

            Stored.RequestTime      = RequestTime;
            Stored.ResponseTime     = ResponseTime;
            Stored.ConsideredStale  = false;

        }

        #endregion

        #region Vary keying (Section 4.1)

        /// <summary>
        /// The request-header field names a response's <c>Vary</c> selects on
        /// (lowercased). A <c>Vary: *</c> is reported as the single name "*",
        /// which <see cref="SelectionMatches"/> treats as never-reusable.
        /// </summary>
        public static List<String> VaryFieldNames(List<(String Name, String Value)> ResponseHeaders)

            => ResponseHeaders.Where     (header => header.Name == "vary").
                               SelectMany(header => header.Value.Split(',')).
                               Select    (name   => name.Trim().ToLowerInvariant()).
                               Where     (name   => name.Length > 0).
                               Distinct  ().
                               ToList    ();

        /// <summary>
        /// Capture the values of the Vary-selected request headers, for storing
        /// alongside a response. Several lines of one field are combined, as
        /// Section 4.1 has them compared.
        /// </summary>
        public static List<(String Name, String Value)> SelectRequestHeaders(List<(String Name, String Value)>  RequestHeaders,
                                                                             List<String>                       VaryNames)

            => VaryNames.Where (name => name != "*").
                         Select(name => (name, CombinedValue(RequestHeaders, name))).
                         ToList();

        /// <summary>
        /// Does a new request's selecting headers match those a stored variant was
        /// keyed on (RFC 9111, Section 4.1)? A stored variant whose Vary included
        /// "*" never matches.
        /// </summary>
        public static Boolean SelectionMatches(HTTPStoredResponse                 Stored,
                                               List<(String Name, String Value)>  RequestHeaders)
        {

            var varyNames = VaryFieldNames(Stored.Headers);

            if (varyNames.Contains("*"))
                return false;

            foreach (var (name, storedValue) in Stored.VaryKeyHeaders)
            {
                if (!String.Equals(CombinedValue(RequestHeaders, name), storedValue, StringComparison.Ordinal))
                    return false;
            }

            return true;

        }

        private static String CombinedValue(List<(String Name, String Value)>  Headers,
                                            String                             Name)

            => String.Join(", ", Headers.Where (header => header.Name == Name).
                                         Select(header => header.Value.Trim()));

        #endregion


        #region TryParseHTTPDate(Value)

        /// <summary>
        /// An HTTP-date in any of its three formats (RFC 9110, Section 5.6.7),
        /// or null.
        /// </summary>
        public static DateTimeOffset? TryParseHTTPDate(String? Value)

            => Value is not null && HTTPDate.TryParse(Value, out var timestamp)
                   ? timestamp
                   : null;

        #endregion

        private static TimeSpan Max(TimeSpan a, TimeSpan b)
            => a > b ? a : b;

    }

}
