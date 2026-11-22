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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The HTTP/1.x client's RFC 9111 cache, AHTTPClient.Cache, against an
    /// origin that counts what reaches it: a hit is a request the origin never
    /// saw. The clock is the cache's own, so that a response goes stale without
    /// waiting for it.
    /// </summary>
    [TestFixture]
    public class HTTPClientCacheTests
    {

        #region (private) ManualClock

        /// <summary>
        /// A clock that moves when told to, starting at the real time, since the
        /// origin's Date fields are real.
        /// </summary>
        private sealed class ManualClock : TimeProvider
        {

            private DateTimeOffset now = DateTimeOffset.UtcNow;

            public override DateTimeOffset GetUtcNow()
                => now;

            public void Advance(TimeSpan By)
                => now += By;

        }

        #endregion

        #region (private) Origin

        /// <summary>
        /// An HTTP server whose answers a test sets per path, and which records
        /// every request that reaches it.
        /// </summary>
        private sealed class Origin : IAsyncDisposable
        {

            private readonly List<HTTPRequest> requests = [];

            public  HTTPServer                                           Server    { get; }

            /// <summary>
            /// The clock the origin's Date fields come from. A cache reads the
            /// gap between its own clock and Date as apparent age (RFC 9111,
            /// Section 4.2.3), so both have to move together.
            /// </summary>
            public  ManualClock                                          Clock     { get; } = new();
            public  Func<HTTPRequest, HTTPResponse.Builder>              Respond   { get; set; }
                        = request => Answer(request, "body");

            public  IReadOnlyList<HTTPRequest> Requests
            {
                get
                {
                    lock (requests)
                        return [.. requests];
                }
            }

            public  Int32 Hits(HTTPMethod Method)
                => Requests.Count(request => request.HTTPMethod == Method);

            private Origin(HTTPServer Server)
            {
                this.Server = Server;
            }

            public static async Task<Origin> Start()
            {

                var origin = new Origin(await HTTPServer.StartNew());
                var api    = origin.Server.AddHTTPAPI();

                foreach (var path in new[] { "r", "s", "old" })
                {
                    foreach (var method in new[] { HTTPMethod.GET, HTTPMethod.HEAD, HTTPMethod.POST })
                    {
                        api.AddHandler(HTTPPath.Root + path,
                                       HTTPMethod:    method,
                                       HTTPDelegate:  request => {
                                                          lock (origin.requests)
                                                              origin.requests.Add(request);
                                                          var response = origin.Respond(request);
                                                          response.Date = origin.Clock.GetUtcNow();
                                                          return Task.FromResult(response.AsImmutable);
                                                      });
                    }
                }

                return origin;

            }

            public HTTPClient Client(HTTPClientCache Cache)

                => new (URL.Parse($"http://127.0.0.1:{Server.TCPPort}")) {
                       Cache = Cache
                   };

            public ValueTask DisposeAsync()
                => Server.DisposeAsync();

        }

        #endregion

        #region (private) Helpers

        private static HTTPResponse.Builder Answer(HTTPRequest                Request,
                                                   String                     Body,
                                                   params (String, String)[]  Fields)
        {

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = Body.ToUTF8Bytes()
                           };

            foreach (var (name, value) in Fields)
                response.SetHeaderField(name, value);

            return response;

        }

        private static HTTPResponse.Builder Status(HTTPRequest                Request,
                                                   HTTPStatusCode             Code,
                                                   params (String, String)[]  Fields)
        {

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = Code
                           };

            foreach (var (name, value) in Fields)
                response.SetHeaderField(name, value);

            return response;

        }

        /// <summary>
        /// A representation with an entity tag, answered with a 304 when asked
        /// for with that tag.
        /// </summary>
        private static Func<HTTPRequest, HTTPResponse.Builder> Tagged(String  CacheControl,
                                                                      String  ETag,
                                                                      String  Body)

            => request => request.GetHeaderField("If-None-Match") == ETag
                              ? Status(request, HTTPStatusCode.NotModified, ("Cache-Control", CacheControl), ("ETag", ETag))
                              : Answer(request, Body,                       ("Cache-Control", CacheControl), ("ETag", ETag));

        private static Task<HTTPResponse> Get(HTTPClient  Client,
                                              String      Path,
                                              params (String, String)[] Fields)

            => Client.GET(HTTPPath.Root + Path,
                          RequestBuilder:  builder => {
                                               foreach (var (name, value) in Fields)
                                                   builder.SetHeaderField(name, value);
                                           },
                          RequestTimeout:  TimeSpan.FromSeconds(10));

        #endregion


        #region A fresh response is served from the cache

        [Test]
        public async Task AFreshResponseIsServedFromTheCache()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, "fresh", ("Cache-Control", "max-age=60"));

            var cache       = new HTTPClientCache(TimeProvider: origin.Clock);
            using var client = origin.Client(cache);

            var first   = await Get(client, "r");
            var second  = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(first. CacheStatus,           Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(second.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(second.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(second.HTTPBodyAsUTF8String,  Is.EqualTo("fresh"));
                Assert.That(second.GetHeaderField("Age"), Is.Not.Null, "RFC 9111, Section 5.1: a stored response carries its Age");
                Assert.That(second.HTTPRequest,           Is.Not.Null);
                Assert.That(origin.Hits(HTTPMethod.GET),           Is.EqualTo(1));
                Assert.That(cache.Hits,                   Is.EqualTo(1));
                Assert.That(cache.Misses,                 Is.EqualTo(1));
            });

        }

        #endregion

        #region An Age list counts its first member, so an old response is stale (H-44)

        /// <summary>
        /// RFC 9111, Section 5.1, as cache-tests' age-parse-suffix sends it: one
        /// field line "Age: 7200, 0" beside max-age=3600. The first member makes
        /// the response two hours old and so stale; read as invalid, the field
        /// was 0, and the response was served as fresh.
        /// </summary>
        [Test]
        public async Task AnAgeListCountsItsFirstMember()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, "old", ("Cache-Control", "max-age=3600"), ("Age", "7200, 0"));

            var cache       = new HTTPClientCache(TimeProvider: origin.Clock);
            using var client = origin.Client(cache);

            var first   = await Get(client, "r");
            var second  = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(first. GetHeaderField("Age"), Is.EqualTo("7200, 0"), "the field reaches the cache as one line");
                Assert.That(second.CacheStatus,           Is.Not.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(origin.Hits(HTTPMethod.GET),  Is.EqualTo(2));
                Assert.That(cache.Hits,                   Is.EqualTo(0));
            });

        }

        #endregion

        #region Without a cache, every request goes to the origin

        [Test]
        public async Task WithoutACacheEveryRequestGoesToTheOrigin()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, "fresh", ("Cache-Control", "max-age=60"));

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{origin.Server.TCPPort}"));

            await Get(client, "r");
            var second = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,  Is.Null);
                Assert.That(origin.Hits(HTTPMethod.GET),  Is.EqualTo(2));
            });

        }

        #endregion

        #region A stale response is revalidated with If-None-Match, and a 304 serves it

        /// <summary>
        /// RFC 9111, Sections 4.3.1 and 4.3.4.
        /// </summary>
        [Test]
        public async Task AStaleResponseIsRevalidatedAndA304ServesIt()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            var cache        = new HTTPClientCache(TimeProvider: clock);
            using var client = origin.Client(cache);

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            var second = await Get(client, "r");
            var third  = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,                              Is.EqualTo(HTTPCacheStatus.Revalidated));
                Assert.That(second.HTTPStatusCode,                           Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(second.HTTPBodyAsUTF8String,                     Is.EqualTo("version one"));
                Assert.That(origin.Requests[1].GetHeaderField("If-None-Match"), Is.EqualTo("\"v1\""));
                Assert.That(third.CacheStatus,                               Is.EqualTo(HTTPCacheStatus.Hit), "the 304 made it fresh again");
                Assert.That(origin.Hits(HTTPMethod.GET),                              Is.EqualTo(2));
                Assert.That(cache.Revalidations,                             Is.EqualTo(1));
            });

        }

        #endregion

        #region A stale response with only a Last-Modified is revalidated with If-Modified-Since

        [Test]
        public async Task AStaleResponseWithOnlyALastModifiedIsRevalidatedWithIfModifiedSince()
        {

            var lastModified = HTTPDate.ToHTTPDate(DateTimeOffset.UtcNow.AddDays(-1));

            await using var origin = await Origin.Start();
            origin.Respond = request => request.GetHeaderField("If-Modified-Since") == lastModified
                                            ? Status(request, HTTPStatusCode.NotModified, ("Cache-Control", "max-age=60"))
                                            : Answer(request, "dated",                    ("Cache-Control", "max-age=60"), ("Last-Modified", lastModified));

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            var second = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,                                   Is.EqualTo(HTTPCacheStatus.Revalidated));
                Assert.That(second.HTTPBodyAsUTF8String,                          Is.EqualTo("dated"));
                Assert.That(origin.Requests[1].GetHeaderField("If-Modified-Since"), Is.EqualTo(lastModified));
            });

        }

        #endregion

        #region A changed representation replaces the stored one

        [Test]
        public async Task AChangedRepresentationReplacesTheStoredOne()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            origin.Respond = Tagged("max-age=60", "\"v2\"", "version two");

            var second = await Get(client, "r");
            var third  = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(second.HTTPBodyAsUTF8String,  Is.EqualTo("version two"));
                Assert.That(third. CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(third. HTTPBodyAsUTF8String,  Is.EqualTo("version two"));
                Assert.That(origin.Hits(HTTPMethod.GET),           Is.EqualTo(2));
            });

        }

        #endregion

        #region A 304 for another entity tag is not applied, and the request goes again without conditions

        /// <summary>
        /// RFC 9111, Section 4.3.4: a 304 whose validator matches nothing stored
        /// updates nothing.
        /// </summary>
        [Test]
        public async Task A304ForAnotherEntityTagIsNotApplied()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));

            // A confused origin: a 304 that names another representation, and
            // the new one to a request without conditions.
            origin.Respond = request => request.GetHeaderField("If-None-Match") is not null
                                            ? Status(request, HTTPStatusCode.NotModified, ("Cache-Control", "max-age=60"), ("ETag", "\"v2\""))
                                            : Answer(request, "version two",              ("Cache-Control", "max-age=60"), ("ETag", "\"v2\""));

            var second = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.HTTPBodyAsUTF8String,                         Is.EqualTo("version two"));
                Assert.That(second.CacheStatus,                                  Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(origin.Hits(HTTPMethod.GET),                                  Is.EqualTo(3));
                Assert.That(origin.Requests[2].GetHeaderField("If-None-Match"),  Is.Null);
            });

        }

        #endregion

        #region A no-store answer to a revalidation removes the stored response

        /// <summary>
        /// The new response may not be stored, and the old one must not be
        /// served again in its place, not even to a request that accepts stale.
        /// </summary>
        [Test]
        public async Task ANoStoreAnswerToARevalidationRemovesTheStoredResponse()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            var cache        = new HTTPClientCache(TimeProvider: clock);
            using var client = origin.Client(cache);

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            origin.Respond = request => Answer(request, "version two", ("Cache-Control", "no-store"));

            await Get(client, "r");
            var third = await Get(client, "r", ("Cache-Control", "max-stale=600"));

            Assert.Multiple(() => {
                Assert.That(third.HTTPBodyAsUTF8String,  Is.EqualTo("version two"));
                Assert.That(third.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(cache.Count,                 Is.EqualTo(0));
            });

        }

        #endregion

        #region no-store is never stored

        [Test]
        public async Task NoStoreIsNeverStored()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, "secret", ("Cache-Control", "no-store, max-age=60"));

            var cache        = new HTTPClientCache(TimeProvider: origin.Clock);
            using var client = origin.Client(cache);

            await Get(client, "r");
            var second = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(origin.Hits(HTTPMethod.GET),  Is.EqualTo(2));
                Assert.That(cache.Count,         Is.EqualTo(0));
            });

        }

        #endregion

        #region Vary keeps variants apart, and Vary: * is never reused

        /// <summary>
        /// RFC 9111, Section 4.1.
        /// </summary>
        [Test]
        public async Task VaryKeepsVariantsApart()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => request.Path.ToString() == "/s"
                                            ? Answer(request, "star",                                          ("Cache-Control", "max-age=60"), ("Vary", "*"))
                                            : Answer(request, "lang=" + request.GetHeaderField("Accept-Language"), ("Cache-Control", "max-age=60"), ("Vary", "Accept-Language"));

            using var client = origin.Client(new HTTPClientCache(TimeProvider: origin.Clock));

            var de1   = await Get(client, "r", ("Accept-Language", "de"));
            var en1   = await Get(client, "r", ("Accept-Language", "en"));
            var de2   = await Get(client, "r", ("Accept-Language", "de"));
            var en2   = await Get(client, "r", ("Accept-Language", "en"));
            await Get(client, "s");
            var star  = await Get(client, "s");

            Assert.Multiple(() => {
                Assert.That(de2.HTTPBodyAsUTF8String,  Is.EqualTo("lang=de"));
                Assert.That(en2.HTTPBodyAsUTF8String,  Is.EqualTo("lang=en"));
                Assert.That(de2.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(en2.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(star.CacheStatus,          Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(origin.Hits(HTTPMethod.GET),        Is.EqualTo(4));
            });

        }

        #endregion

        #region A successful unsafe request invalidates its target, and a failed one does not

        /// <summary>
        /// RFC 9111, Section 4.4: "A cache MUST invalidate the target URI ...
        /// when it receives a non-error status code in response to an unsafe
        /// request method".
        /// </summary>
        [Test]
        public async Task ASuccessfulUnsafeRequestInvalidatesItsTarget()
        {

            await using var origin = await Origin.Start();

            var postStatus = HTTPStatusCode.InternalServerError;
            origin.Respond = request => request.HTTPMethod == HTTPMethod.POST
                                            ? Status(request, postStatus)
                                            : Answer(request, "fresh", ("Cache-Control", "max-age=60"));

            using var client = origin.Client(new HTTPClientCache(TimeProvider: origin.Clock));

            await Get(client, "r");
            await client.POST(HTTPPath.Root + "r", "x".ToUTF8Bytes(), HTTPContentType.Text.PLAIN, RequestTimeout: TimeSpan.FromSeconds(10));
            var afterFailure = await Get(client, "r");

            postStatus = HTTPStatusCode.OK;
            await client.POST(HTTPPath.Root + "r", "x".ToUTF8Bytes(), HTTPContentType.Text.PLAIN, RequestTimeout: TimeSpan.FromSeconds(10));
            var afterSuccess = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(afterFailure.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(afterSuccess.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(origin.Hits(HTTPMethod.GET),        Is.EqualTo(2));
            });

        }

        #endregion

        #region only-if-cached never reaches the origin

        /// <summary>
        /// RFC 9111, Section 5.2.1.7: a 504 when nothing stored can answer, and
        /// that includes a stale response that would need revalidating. The
        /// HTTP/2 cache revalidated that one with the origin.
        /// </summary>
        [Test]
        public async Task OnlyIfCachedNeverReachesTheOrigin()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            var nothing  = await Get(client, "r", ("Cache-Control", "only-if-cached"));
            await Get(client, "r");
            var fresh    = await Get(client, "r", ("Cache-Control", "only-if-cached"));
            clock.Advance(TimeSpan.FromSeconds(90));
            var stale    = await Get(client, "r", ("Cache-Control", "only-if-cached"));

            Assert.Multiple(() => {
                Assert.That(nothing.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.GatewayTimeout));
                Assert.That(nothing.CacheStatus,     Is.EqualTo(HTTPCacheStatus.Unavailable));
                Assert.That(fresh.  CacheStatus,     Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(stale.  HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.GatewayTimeout));
                Assert.That(origin.Hits(HTTPMethod.GET),      Is.EqualTo(1));
            });

        }

        #endregion

        #region A request's no-cache and Pragma: no-cache revalidate a fresh response

        /// <summary>
        /// RFC 9111, Sections 5.2.1.4 and 5.4.
        /// </summary>
        [TestCase("Cache-Control", "no-cache")]
        [TestCase("Cache-Control", "max-age=0")]
        [TestCase("Pragma",        "no-cache")]
        public async Task ARequestsNoCacheRevalidatesAFreshResponse(String Field, String Value)
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(1));
            var second = await Get(client, "r", (Field, Value));

            Assert.Multiple(() => {
                Assert.That(second.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Revalidated));
                Assert.That(origin.Hits(HTTPMethod.GET),  Is.EqualTo(2));
            });

        }

        #endregion

        #region A fresh must-revalidate response is served from the cache

        /// <summary>
        /// RFC 9111, Section 5.2.2.2: must-revalidate begins once the response is stale.
        /// </summary>
        [Test]
        public async Task AFreshMustRevalidateResponseIsServedFromTheCache()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60, must-revalidate", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            var fresh = await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            var stale = await Get(client, "r", ("Cache-Control", "max-stale=600"));

            Assert.Multiple(() => {
                Assert.That(fresh.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(stale.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Revalidated), "max-stale does not override must-revalidate");
            });

        }

        #endregion

        #region max-stale serves a stale response

        [Test]
        public async Task MaxStaleServesAStaleResponse()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            var stale = await Get(client, "r", ("Cache-Control", "max-stale=60"));

            Assert.Multiple(() => {
                Assert.That(stale.CacheStatus,                    Is.EqualTo(HTTPCacheStatus.Stale));
                Assert.That(stale.HTTPBodyAsUTF8String,           Is.EqualTo("version one"));
                Assert.That(UInt64.Parse(stale.GetHeaderField("Age")!), Is.GreaterThanOrEqualTo(90UL));
                Assert.That(origin.Hits(HTTPMethod.GET),                   Is.EqualTo(1));
            });

        }

        #endregion

        #region stale-while-revalidate serves stale, and revalidates in the background

        /// <summary>
        /// RFC 5861, Section 3.
        /// </summary>
        [Test]
        public async Task StaleWhileRevalidateServesStaleAndRevalidatesInTheBackground()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60, stale-while-revalidate=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            var cache        = new HTTPClientCache(TimeProvider: clock);
            using var client = origin.Client(cache);

            await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(90));
            var stale = await Get(client, "r");

            for (var i = 0; i < 100 && origin.Hits(HTTPMethod.GET) < 2; i++)
                await Task.Delay(50);

            for (var i = 0; i < 100 && cache.Revalidations < 1; i++)
                await Task.Delay(50);

            await Task.Delay(200);
            var after = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(stale.CacheStatus,                                   Is.EqualTo(HTTPCacheStatus.Stale));
                Assert.That(origin.Hits(HTTPMethod.GET),                                  Is.EqualTo(2));
                Assert.That(origin.Requests[1].GetHeaderField("If-None-Match"),  Is.EqualTo("\"v1\""));
                Assert.That(after.CacheStatus,                                   Is.EqualTo(HTTPCacheStatus.Hit), "the background 304 made it fresh again");
            });

        }

        #endregion

        #region stale-if-error serves stale when the origin fails, within its window

        /// <summary>
        /// RFC 5861, Section 4.
        /// </summary>
        [Test]
        public async Task StaleIfErrorServesStaleWhenTheOriginFails()
        {

            await using var origin = await Origin.Start();
            origin.Respond = Tagged("max-age=60, stale-if-error=60", "\"v1\"", "version one");

            var clock        = origin.Clock;
            using var client = origin.Client(new HTTPClientCache(TimeProvider: clock));

            await Get(client, "r");
            origin.Respond = request => Status(request, HTTPStatusCode.ServiceUnavailable);

            clock.Advance(TimeSpan.FromSeconds(90));
            var within = await Get(client, "r");
            clock.Advance(TimeSpan.FromSeconds(60));
            var beyond = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(within.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Stale));
                Assert.That(within.HTTPBodyAsUTF8String,  Is.EqualTo("version one"));
                Assert.That(beyond.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.ServiceUnavailable));
                Assert.That(beyond.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Miss));
            });

        }

        #endregion

        #region A Range request and a HEAD request bypass the cache

        /// <summary>
        /// Neither is answered from a stored GET, and neither response is stored.
        /// </summary>
        [Test]
        public async Task ARangeRequestAndAHEADRequestBypassTheCache()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => request.GetHeaderField("Range") is not null
                                            ? Status(request, HTTPStatusCode.PartialContent, ("Cache-Control", "max-age=60"), ("Content-Range", "bytes 0-0/4"))
                                            : Answer(request, "full",                        ("Cache-Control", "max-age=60"));

            using var client = origin.Client(new HTTPClientCache(TimeProvider: origin.Clock));

            await Get(client, "r");
            var range  = await Get(client, "r", ("Range", "bytes=0-0"));
            var head   = await client.RunRequest(HTTPMethod.HEAD, HTTPPath.Root + "r", RequestTimeout: TimeSpan.FromSeconds(10));
            var whole  = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(range.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.PartialContent));
                Assert.That(range.CacheStatus,           Is.Null);
                Assert.That(head. CacheStatus,           Is.Null);
                Assert.That(whole.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(whole.HTTPBodyAsUTF8String,  Is.EqualTo("full"));
                Assert.That(origin.Hits(HTTPMethod.GET),          Is.EqualTo(2));
                Assert.That(origin.Hits(HTTPMethod.HEAD),         Is.EqualTo(1));
            });

        }

        #endregion

        #region A shared cache does not store a private response, a private cache does

        [Test]
        public async Task ASharedCacheDoesNotStoreAPrivateResponse()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, "mine", ("Cache-Control", "private, max-age=60"));

            var shared  = new HTTPClientCache(HTTPCacheMode.Shared);
            var mine    = new HTTPClientCache(HTTPCacheMode.Private);

            using var sharedClient   = origin.Client(shared);
            using var privateClient  = origin.Client(mine);

            await Get(sharedClient,  "r");
            await Get(privateClient, "r");

            Assert.Multiple(() => {
                Assert.That(shared.Count,  Is.EqualTo(0));
                Assert.That(mine.  Count,  Is.EqualTo(1));
            });

        }

        #endregion

        #region MaxEntries evicts the response used least recently, MaxBodySize keeps large ones out

        [Test]
        public async Task MaxEntriesEvictsTheResponseUsedLeastRecently()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => Answer(request, request.Path.ToString() == "/old" ? "a larger body" : "small", ("Cache-Control", "max-age=60"));

            var cache        = new HTTPClientCache(MaxEntries: 1, MaxBodySize: 5);
            using var client = origin.Client(cache);

            await Get(client, "r");
            await Get(client, "s");
            var r     = await Get(client, "r");
            await Get(client, "old");
            var r2    = await Get(client, "r");

            Assert.Multiple(() => {
                Assert.That(r. CacheStatus,  Is.EqualTo(HTTPCacheStatus.Miss), "s evicted r");
                Assert.That(r2.CacheStatus,  Is.EqualTo(HTTPCacheStatus.Hit),  "old was too large to evict anything");
                Assert.That(cache.Count,     Is.EqualTo(1));
            });

        }

        #endregion

        #region One cache serves two origins without mixing them up

        [Test]
        public async Task OneCacheServesTwoOriginsWithoutMixingThemUp()
        {

            await using var first   = await Origin.Start();
            await using var second  = await Origin.Start();
            first. Respond = request => Answer(request, "first",  ("Cache-Control", "max-age=60"));
            second.Respond = request => Answer(request, "second", ("Cache-Control", "max-age=60"));

            var cache = new HTTPClientCache(TimeProvider: first.Clock);
            using var firstClient   = first. Client(cache);
            using var secondClient  = second.Client(cache);

            await Get(firstClient, "r");
            var fromSecond  = await Get(secondClient, "r");
            var fromFirst   = await Get(firstClient,  "r");

            Assert.Multiple(() => {
                Assert.That(fromSecond.HTTPBodyAsUTF8String,  Is.EqualTo("second"));
                Assert.That(fromSecond.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Miss));
                Assert.That(fromFirst. HTTPBodyAsUTF8String,  Is.EqualTo("first"));
                Assert.That(fromFirst. CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
            });

        }

        #endregion

        #region A stored redirect is followed without asking the origin again

        /// <summary>
        /// Every hop goes through the cache, so a 301 with a lifetime is stored
        /// like any other response (RFC 9110, Section 15.4.2).
        /// </summary>
        [Test]
        public async Task AStoredRedirectIsFollowedWithoutAskingTheOriginAgain()
        {

            await using var origin = await Origin.Start();
            origin.Respond = request => request.Path.ToString() == "/old"
                                            ? Status(request, HTTPStatusCode.MovedPermanently, ("Location", "/r"), ("Cache-Control", "max-age=60"), ("Content-Length", "0"))
                                            : Answer(request, "the new place",                 ("Cache-Control", "max-age=60"));

            using var client = origin.Client(new HTTPClientCache(TimeProvider: origin.Clock));
            client.MaxRedirects = 3;

            await Get(client, "old");
            var second = await Get(client, "old");

            Assert.Multiple(() => {
                Assert.That(second.HTTPBodyAsUTF8String,  Is.EqualTo("the new place"));
                Assert.That(second.CacheStatus,           Is.EqualTo(HTTPCacheStatus.Hit));
                Assert.That(second.RedirectChain.Count,   Is.EqualTo(1));
                Assert.That(origin.Requests.Count,        Is.EqualTo(2));
            });

        }

        #endregion

    }

}
