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

#region Usings

using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An RFC 9111 cache for the HTTP/1.x client: set it as AHTTPClient.Cache.
    /// It stores responses to GET, serves fresh ones without contacting the
    /// origin, revalidates stale ones with If-None-Match and If-Modified-Since,
    /// keys variants by Vary, honours the request's and the response's
    /// Cache-Control, and invalidates a target when an unsafe request to it
    /// succeeds. The rules are <see cref="HTTPCache"/>'s, the ones the HTTP/2
    /// client's cache follows; this class is the store and the wiring.
    ///
    /// Private by default, the cache of one user agent. Shared semantics, for
    /// a client that serves many users, are a constructor argument away.
    ///
    /// One cache may serve several clients. Entries are keyed by scheme,
    /// authority and request-target, so two origins never share one.
    ///
    /// What does not go through it, and is sent as it is: HEAD, which a cache
    /// may answer from a stored GET but this one does not; a request with Range
    /// or with a precondition of its own, since this cache neither combines
    /// partial content nor evaluates a caller's conditions; DownloadAsync. A
    /// response whose body is still on the connection, an event stream, is not
    /// stored, and neither is one larger than MaxBodySize.
    /// </summary>
    public sealed class HTTPClientCache
    {

        #region Data

        /// <summary>
        /// Responses stored per key, several where Vary selects between them.
        /// </summary>
        private readonly Dictionary<String, List<HTTPStoredResponse>>  store     = [];

        /// <summary>
        /// When each stored response was last used, for evicting the oldest.
        /// </summary>
        private readonly Dictionary<HTTPStoredResponse, Int64>         lastUsed  = [];

        private readonly Lock                                          storeLock = new();
        private          Int64                                         useCounter;

        private          Int64                                         hits;
        private          Int64                                         misses;
        private          Int64                                         revalidations;

        /// <summary>
        /// The default for MaxEntries.
        /// </summary>
        public const     UInt32                                        DefaultMaxEntries   = 1024;

        /// <summary>
        /// The default for MaxBodySize: 4 MiB.
        /// </summary>
        public const     UInt64                                        DefaultMaxBodySize  = 4 * 1024 * 1024;

        #endregion

        #region Properties

        /// <summary>
        /// Private or shared semantics (RFC 9111, Section 1).
        /// </summary>
        public HTTPCacheMode  Mode            { get; }

        /// <summary>
        /// The clock ages and freshness are computed with.
        /// </summary>
        public TimeProvider   TimeProvider    { get; }

        /// <summary>
        /// How many responses are kept. Storing one more evicts the one used least recently.
        /// </summary>
        public UInt32         MaxEntries      { get; }

        /// <summary>
        /// The largest body that is stored. A response with a larger one is handed on and not kept.
        /// </summary>
        public UInt64         MaxBodySize     { get; }

        /// <summary>
        /// Stored responses served without a request to the origin, fresh or stale.
        /// </summary>
        public Int64          Hits
            => Interlocked.Read(ref hits);

        /// <summary>
        /// Requests that went to the origin because nothing usable was stored.
        /// </summary>
        public Int64          Misses
            => Interlocked.Read(ref misses);

        /// <summary>
        /// Conditional requests sent to revalidate a stored response, in the background included.
        /// </summary>
        public Int64          Revalidations
            => Interlocked.Read(ref revalidations);

        /// <summary>
        /// How many responses are stored.
        /// </summary>
        public Int32          Count
        {
            get
            {
                lock (storeLock)
                    return lastUsed.Count;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new cache for HTTP/1.x clients.
        /// </summary>
        /// <param name="Mode">Private, the default, or shared semantics.</param>
        /// <param name="MaxEntries">How many responses are kept.</param>
        /// <param name="MaxBodySize">The largest body that is stored.</param>
        /// <param name="TimeProvider">An optional clock, the system's by default.</param>
        public HTTPClientCache(HTTPCacheMode  Mode          = HTTPCacheMode.Private,
                               UInt32         MaxEntries    = DefaultMaxEntries,
                               UInt64         MaxBodySize   = DefaultMaxBodySize,
                               TimeProvider?  TimeProvider  = null)
        {

            this.Mode          = Mode;
            this.MaxEntries    = Math.Max(1, MaxEntries);
            this.MaxBodySize   = MaxBodySize;
            this.TimeProvider  = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion


        #region Clear()

        /// <summary>
        /// Forget every stored response.
        /// </summary>
        public void Clear()
        {
            lock (storeLock)
            {
                store.   Clear();
                lastUsed.Clear();
            }
        }

        #endregion

        #region (internal) SendAsync(Request, Origin, Send)

        /// <summary>
        /// Answer the request from the store where RFC 9111 allows it, and send
        /// it through Send where it does not.
        /// </summary>
        /// <param name="Request">The request.</param>
        /// <param name="Origin">The scheme and authority the client connects to, e.g. "https://example.org:8443".</param>
        /// <param name="Send">Sends one request to the origin.</param>
        internal async Task<HTTPResponse> SendAsync(HTTPRequest                            Request,
                                                    String                                 Origin,
                                                    Func<HTTPRequest, Task<HTTPResponse>>  Send)
        {

            var key = Origin + Request.RequestTarget;

            // Section 4.4: a non-error response to an unsafe method invalidates its target.
            if (!Request.HTTPMethod.IsSafe)
            {

                var response = await Send(Request).ConfigureAwait(false);

                if (HTTPCache.Invalidates(Request.HTTPMethod, (Int32) response.HTTPStatusCode.Code))
                    Invalidate(key);

                return response;

            }

            var requestHeaders = HeadersOf(Request);

            if (Request.HTTPMethod != HTTPMethod.GET || !IsCacheable(requestHeaders))
                return await Send(Request).ConfigureAwait(false);

            var requestCC  = RequestCacheControl(requestHeaders);
            var stored     = Lookup(key, requestHeaders);

            if (stored is not null)
            {

                var decision = HTTPCache.Evaluate(stored, requestCC, Mode, TimeProvider.GetUtcNow());

                switch (decision.Usability)
                {

                    case HTTPCacheUsability.Fresh:
                        Interlocked.Increment(ref hits);
                        return Serve(stored, Request, HTTPCacheStatus.Hit);

                    case HTTPCacheUsability.Stale:
                        Interlocked.Increment(ref hits);
                        return Serve(stored, Request, HTTPCacheStatus.Stale);

                    case HTTPCacheUsability.StaleWhileRevalidate:
                        Interlocked.Increment(ref hits);
                        var served = Serve(stored, Request, HTTPCacheStatus.Stale);
                        _ = Task.Run(async () => {
                                try
                                {
                                    await Revalidate(key, Request, requestHeaders, requestCC, stored, Send).ConfigureAwait(false);
                                }
                                catch (Exception e)
                                {
                                    DebugX.LogT($"{nameof(HTTPClientCache)} could not revalidate {key} in the background: {e.Message}");
                                }
                            });
                        return served;

                    default:
                        // Section 5.2.1.7: only-if-cached never goes to the origin.
                        if (requestCC.OnlyIfCached)
                            return Unavailable(Request);

                        return await Revalidate(key, Request, requestHeaders, requestCC, stored, Send).ConfigureAwait(false);

                }

            }

            if (requestCC.OnlyIfCached)
                return Unavailable(Request);

            Interlocked.Increment(ref misses);

            return await Forward(key, Request, requestHeaders, requestCC, Send).ConfigureAwait(false);

        }

        #endregion


        #region (private) Forward    (Key, Request, RequestHeaders, RequestCC, Send)

        /// <summary>
        /// Send the request as it is, and store its response where Section 3 allows it.
        /// </summary>
        private async Task<HTTPResponse> Forward(String                                 Key,
                                                 HTTPRequest                            Request,
                                                 List<(String Name, String Value)>      RequestHeaders,
                                                 HTTPCacheControl                       RequestCC,
                                                 Func<HTTPRequest, Task<HTTPResponse>>  Send)
        {

            var requestTime   = TimeProvider.GetUtcNow();
            var response      = await Send(Request).ConfigureAwait(false);
            var responseTime  = TimeProvider.GetUtcNow();

            TryStore(Key, Request, RequestHeaders, RequestCC, response, requestTime, responseTime);

            response.CacheStatus = HTTPCacheStatus.Miss;

            return response;

        }

        #endregion

        #region (private) Revalidate (Key, Request, RequestHeaders, RequestCC, Stored, Send)

        /// <summary>
        /// Ask the origin whether the stored response is still good (Section 4.3).
        /// A 304 that selects it updates it and serves it; any other response
        /// replaces it, except an error, for which a stale-if-error may let it
        /// stand in (RFC 5861, Section 4).
        /// </summary>
        private async Task<HTTPResponse> Revalidate(String                                 Key,
                                                    HTTPRequest                            Request,
                                                    List<(String Name, String Value)>      RequestHeaders,
                                                    HTTPCacheControl                       RequestCC,
                                                    HTTPStoredResponse                     Stored,
                                                    Func<HTTPRequest, Task<HTTPResponse>>  Send)
        {

            var conditions = HTTPCache.ConditionalHeaders(Stored);

            // Nothing to validate with: the request goes as it is, and its
            // response replaces the stored one.
            if (conditions.Count == 0)
            {
                Interlocked.Increment(ref misses);
                return await ForwardOrStale(Key, Request, RequestHeaders, RequestCC, Stored, Send).ConfigureAwait(false);
            }

            Interlocked.Increment(ref revalidations);

            var conditional = new HTTPRequest.Builder(Request);
            foreach (var (name, value) in conditions)
                conditional.SetHeaderField(name, value);

            var requestTime   = TimeProvider.GetUtcNow();
            var response      = await Send(conditional.AsImmutable).ConfigureAwait(false);
            var responseTime  = TimeProvider.GetUtcNow();

            if (response.HTTPStatusCode.Code == 304)
            {

                var notModified = HeadersOf(response);

                // Section 4.3.4: a 304 that names another representation updates
                // nothing, and the request is sent again without conditions.
                if (!HTTPCache.Selects(Stored, notModified))
                    return await ForwardOrStale(Key, Request, RequestHeaders, RequestCC, Stored, Send).ConfigureAwait(false);

                lock (storeLock)
                    HTTPCache.UpdateFrom304(Stored, notModified, requestTime, responseTime);

                return Serve(Stored, Request, HTTPCacheStatus.Revalidated);

            }

            if (IsError(response) &&
                HTTPCache.ServesStaleOnError(Stored, RequestCC, Mode, TimeProvider.GetUtcNow()))
            {
                return Serve(Stored, Request, HTTPCacheStatus.Stale);
            }

            Replace(Key, Request, RequestHeaders, RequestCC, response, requestTime, responseTime);

            response.CacheStatus = HTTPCacheStatus.Miss;

            return response;

        }

        #endregion

        #region (private) ForwardOrStale (Key, Request, RequestHeaders, RequestCC, Stored, Send)

        /// <summary>
        /// Send the request without conditions. Its response replaces the stored
        /// one, unless it is an error a stale-if-error lets the stored one stand in for.
        /// </summary>
        private async Task<HTTPResponse> ForwardOrStale(String                                 Key,
                                                        HTTPRequest                            Request,
                                                        List<(String Name, String Value)>      RequestHeaders,
                                                        HTTPCacheControl                       RequestCC,
                                                        HTTPStoredResponse                     Stored,
                                                        Func<HTTPRequest, Task<HTTPResponse>>  Send)
        {

            var requestTime   = TimeProvider.GetUtcNow();
            var response      = await Send(Request).ConfigureAwait(false);
            var responseTime  = TimeProvider.GetUtcNow();

            if (IsError(response) &&
                HTTPCache.ServesStaleOnError(Stored, RequestCC, Mode, TimeProvider.GetUtcNow()))
            {
                return Serve(Stored, Request, HTTPCacheStatus.Stale);
            }

            Replace(Key, Request, RequestHeaders, RequestCC, response, requestTime, responseTime);

            response.CacheStatus = HTTPCacheStatus.Miss;

            return response;

        }

        #endregion


        #region (private) Lookup     (Key, RequestHeaders)

        private HTTPStoredResponse? Lookup(String                             Key,
                                           List<(String Name, String Value)>  RequestHeaders)
        {
            lock (storeLock)
            {

                if (!store.TryGetValue(Key, out var variants))
                    return null;

                var stored = variants.FirstOrDefault(variant => HTTPCache.SelectionMatches(variant, RequestHeaders));

                if (stored is not null)
                    lastUsed[stored] = ++useCounter;

                return stored;

            }
        }

        #endregion

        #region (private) Replace    (Key, Request, RequestHeaders, RequestCC, Response, RequestTime, ResponseTime)

        /// <summary>
        /// A new response for a stored one: the stored one goes, whether or not
        /// the new one may be stored in its place. Otherwise a no-store answer
        /// would leave the response it replaced to be served again.
        /// </summary>
        private void Replace(String                             Key,
                             HTTPRequest                        Request,
                             List<(String Name, String Value)>  RequestHeaders,
                             HTTPCacheControl                   RequestCC,
                             HTTPResponse                       Response,
                             DateTimeOffset                     RequestTime,
                             DateTimeOffset                     ResponseTime)
        {

            if (Response.HTTPStatusCode.Code == 304)
                return;

            lock (storeLock)
            {
                if (store.TryGetValue(Key, out var variants))
                {
                    foreach (var variant in variants.Where(variant => HTTPCache.SelectionMatches(variant, RequestHeaders)).ToList())
                    {
                        variants.Remove(variant);
                        lastUsed.Remove(variant);
                    }
                }
            }

            TryStore(Key, Request, RequestHeaders, RequestCC, Response, RequestTime, ResponseTime);

        }

        #endregion

        #region (private) TryStore   (Key, Request, RequestHeaders, RequestCC, Response, RequestTime, ResponseTime)

        private void TryStore(String                             Key,
                              HTTPRequest                        Request,
                              List<(String Name, String Value)>  RequestHeaders,
                              HTTPCacheControl                   RequestCC,
                              HTTPResponse                       Response,
                              DateTimeOffset                     RequestTime,
                              DateTimeOffset                     ResponseTime)
        {

            // A body still on the connection is not in hand, and one past the
            // limit is not kept.
            if (Response.HTTPBodyStream is not null ||
                Response.HTTPBody       is null     ||
                (UInt64) Response.HTTPBody.LongLength > MaxBodySize)
                return;

            var responseHeaders = HeadersOf(Response);
            var responseCC      = HTTPCacheControl.FromHeaders(responseHeaders);

            if (!HTTPCache.IsStorable(Request.HTTPMethod,
                                      RequestHeaders.Any(header => header.Name == "authorization"),
                                      RequestCC,
                                      (Int32) Response.HTTPStatusCode.Code,
                                      responseHeaders,
                                      responseCC,
                                      Mode))
                return;

            var varyNames = HTTPCache.VaryFieldNames(responseHeaders);

            // Vary: * never matches a later request (Section 4.1).
            if (varyNames.Contains("*"))
                return;

            var connectionOptions = HTTPCache.ConnectionOptions(responseHeaders);

            var entry = new HTTPStoredResponse {
                            Status          = (Int32) Response.HTTPStatusCode.Code,
                            Headers         = [.. responseHeaders.Where(header => HTTPCache.IsStoredField(header.Name, connectionOptions))],
                            Body            = Response.HTTPBody,
                            RequestTime     = RequestTime,
                            ResponseTime    = ResponseTime,
                            VaryKeyHeaders  = HTTPCache.SelectRequestHeaders(RequestHeaders, varyNames)
                        };

            lock (storeLock)
            {

                if (!store.TryGetValue(Key, out var variants))
                    store[Key] = variants = [];

                // Replace any variant this request would have selected.
                foreach (var variant in variants.Where(variant => HTTPCache.SelectionMatches(variant, RequestHeaders)).ToList())
                {
                    variants.Remove(variant);
                    lastUsed.Remove(variant);
                }

                variants.Add(entry);
                lastUsed[entry] = ++useCounter;

                while (lastUsed.Count > MaxEntries)
                    Evict(lastUsed.MinBy(used => used.Value).Key);

            }

        }

        #endregion

        #region (private) Invalidate (Key)

        private void Invalidate(String Key)
        {
            lock (storeLock)
            {
                if (store.Remove(Key, out var variants))
                    foreach (var variant in variants)
                        lastUsed.Remove(variant);
            }
        }

        #endregion

        #region (private) Evict      (Stored)

        /// <summary>
        /// Remove one stored response. The caller holds the lock.
        /// </summary>
        private void Evict(HTTPStoredResponse Stored)
        {

            lastUsed.Remove(Stored);

            foreach (var (key, variants) in store)
            {
                if (variants.Remove(Stored))
                {
                    if (variants.Count == 0)
                        store.Remove(key);
                    return;
                }
            }

        }

        #endregion


        #region (private) Serve      (Stored, Request, Status)

        /// <summary>
        /// The stored response as an answer to this request, with its current
        /// Age (RFC 9111, Section 5.1) and a Content-Length for the stored octets.
        /// </summary>
        private HTTPResponse Serve(HTTPStoredResponse  Stored,
                                   HTTPRequest         Request,
                                   HTTPCacheStatus     Status)
        {

            String                             statusLine;
            List<(String Name, String Value)>  headers;
            Byte[]                             body;

            lock (storeLock)
            {

                var age     = HTTPCache.CurrentAge(Stored, TimeProvider.GetUtcNow());
                var seconds = (Int64) Math.Clamp(Math.Floor(age.TotalSeconds), 0, HTTPCacheControl.MaxDeltaSeconds);

                statusLine  = $"HTTP/1.1 {Stored.Status} {(HTTPStatusCode.TryParseUInt16((UInt16) Stored.Status, out var known) ? known.Name : "Unknown")}";
                headers     = [.. Stored.Headers.Where(header => header.Name is not "age" and not "content-length")];
                body        = Stored.Body;

                headers.Add(("age", seconds.ToString()));

                // RFC 9110, Section 8.6: no Content-Length in a 204.
                if (Stored.Status != 204)
                    headers.Add(("content-length", body.Length.ToString()));

            }

            var head = new StringBuilder(statusLine).Append("\r\n");

            foreach (var (name, value) in headers)
                head.Append(name).Append(": ").Append(value).Append("\r\n");

            var response = HTTPResponse.Parse(head.ToString(), body, Request);

            response.CacheStatus = Status;

            return response;

        }

        #endregion

        #region (private) Unavailable (Request)

        /// <summary>
        /// The 504 a cache answers only-if-cached with when nothing stored can (RFC 9111, Section 5.2.1.7).
        /// </summary>
        private static HTTPResponse Unavailable(HTTPRequest Request)
        {

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.GatewayTimeout,
                               Date            = Timestamp.Now,
                               ContentLength   = 0
                           }.AsImmutable;

            response.CacheStatus = HTTPCacheStatus.Unavailable;

            return response;

        }

        #endregion


        #region (private) HeadersOf  (PDU)

        /// <summary>
        /// The PDU's fields as RFC 9111's logic reads them: lowercase names, one
        /// entry per field line.
        /// </summary>
        private static List<(String Name, String Value)> HeadersOf(AHTTPPDU PDU)
        {

            var headers = new List<(String Name, String Value)>();

            foreach (var (name, value) in PDU)
            {

                var lowercase = name.ToLowerInvariant();

                switch (value)
                {

                    case String[] lines:
                        foreach (var line in lines)
                            headers.Add((lowercase, line));
                        break;

                    case IEnumerable<String> lines:
                        foreach (var line in lines)
                            headers.Add((lowercase, line));
                        break;

                    default:
                        headers.Add((lowercase, value.ToString() ?? ""));
                        break;

                }

            }

            return headers;

        }

        #endregion

        #region (private) IsCacheable (RequestHeaders)

        /// <summary>
        /// Whether a GET is one this cache looks up and stores at all: not one
        /// with Range, which asks for partial content this cache does not
        /// combine, and not one with a precondition of its own, which is the
        /// caller's to evaluate and the origin's to answer.
        /// </summary>
        private static Boolean IsCacheable(List<(String Name, String Value)> RequestHeaders)

            => !RequestHeaders.Any(header => header.Name is "range"
                                                          or "if-match"
                                                          or "if-none-match"
                                                          or "if-modified-since"
                                                          or "if-unmodified-since"
                                                          or "if-range");

        #endregion

        #region (private) RequestCacheControl (RequestHeaders)

        /// <summary>
        /// The request's Cache-Control, or, when it has none, a Pragma: no-cache
        /// read as Cache-Control: no-cache (RFC 9111, Section 5.4).
        /// </summary>
        private static HTTPCacheControl RequestCacheControl(List<(String Name, String Value)> RequestHeaders)
        {

            if (RequestHeaders.Any(header => header.Name == "cache-control"))
                return HTTPCacheControl.FromHeaders(RequestHeaders);

            return RequestHeaders.Any(header => header.Name == "pragma" &&
                                                header.Value.Split(',').Any(directive => directive.Trim().Equals("no-cache", StringComparison.OrdinalIgnoreCase)))
                       ? HTTPCacheControl.Parse("no-cache")
                       : HTTPCacheControl.Empty;

        }

        #endregion

        #region (private) IsError    (Response)

        /// <summary>
        /// What RFC 5861, Section 4 lets a stale response stand in for: a 500,
        /// 502, 503 or 504, or no response at all, which this client reports as
        /// ClientError.
        /// </summary>
        private static Boolean IsError(HTTPResponse Response)

            => HTTPCache.IsStaleIfErrorStatus((Int32) Response.HTTPStatusCode.Code) ||
               Response.HTTPStatusCode == HTTPStatusCode.ClientError;

        #endregion

    }

}
