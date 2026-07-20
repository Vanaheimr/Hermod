/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 9111 caching, via <see cref="HTTP2CachingClient"/> in front of an origin
    /// that counts how often each path is actually fetched (so a cache HIT is
    /// provably a no-origin-round-trip) and answers conditional revalidations with
    /// 304. Each test uses a distinct path, so the shared origin's per-path hit
    /// counters stay isolated. In-process.
    /// </summary>
    [TestFixture]
    public class CachingClientTests
    {

        #region Origin server (per-path fetch counter)

        private readonly ConcurrentDictionary<String, Int32> originHits = new();
        private TestH2Server srv       = null!;
        private String       authority = "";

        private Task<(List<(String, String)>, Byte[]?)> Origin(UInt32 sid, List<(String Name, String Value)> h, Byte[]? body, CancellationToken ct)
        {

            var path        = h.First(x => x.Name == ":path").Value;
            var ifNoneMatch = h.FirstOrDefault(x => x.Name == "if-none-match").Value;
            var acceptLang  = h.FirstOrDefault(x => x.Name == "accept-language").Value;
            originHits.AddOrUpdate(path, 1, (_, c) => c + 1);
            var date = DateTimeOffset.UtcNow.ToString("r");

            (List<(String, String)>, Byte[]?) Make(Int32 status, Byte[]? bodyOut, params (String, String)[] hdrs)
            {
                var headers = new List<(String, String)> { (":status", status.ToString()), ("date", date) };
                headers.AddRange(hdrs);
                if (bodyOut is not null) headers.Add(("content-length", bodyOut.Length.ToString()));
                return (headers, bodyOut);
            }

            (List<(String, String)>, Byte[]?) Cacheable(String cc, String etag, String bodyText, params (String, String)[] extra)
                => ifNoneMatch == etag
                    ? Make(304, null, [("cache-control", cc), ("etag", etag), .. extra])
                    : Make(200, Encoding.UTF8.GetBytes(bodyText), [("cache-control", cc), ("etag", etag), .. extra]);

            return Task.FromResult<(List<(String, String)>, Byte[]?)>(path switch
            {
                "/max-age-60"     => Cacheable("max-age=60",           "\"ma60\"",  "fresh"),
                "/revalidate"     => Cacheable("max-age=0",            "\"rev\"",   "revalidated"),
                "/no-store"       => Make(200, Encoding.UTF8.GetBytes("nostore"), ("cache-control", "no-store")),
                "/private"        => Cacheable("private, max-age=60",  "\"prv\"",   "private-body"),
                "/s-maxage"       => Cacheable("max-age=0, s-maxage=60", "\"sm\"",  "shared-body"),
                "/auth-cacheable" => Cacheable("max-age=60",           "\"auth\"",  "auth-body"),
                "/swr"            => Cacheable("max-age=0, stale-while-revalidate=60", "\"swr\"", "swr-body"),
                "/invalidate"     => Cacheable("max-age=60",           "\"inv\"",   "inv-body"),
                "/vary"           => Cacheable("max-age=60",           "\"vary-" + acceptLang + "\"",
                                               "lang=" + (acceptLang ?? "none"), ("vary", "accept-language")),
                "/heuristic"      => Make(200, Encoding.UTF8.GetBytes("heur"),
                                          ("last-modified", DateTimeOffset.UtcNow.AddHours(-1).ToString("r"))),
                _                 => Make(404, Encoding.UTF8.GetBytes("nope")),
            });

        }

        [OneTimeSetUp]
        public async Task StartServer()
        {
            srv       = await TestH2Server.StartAsync(Origin);
            authority = $"localhost:{srv.Port}";
        }

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        private async Task<HTTP2CachingClient> NewCache(HTTPCacheMode mode)
            => new(await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert), "https", authority, mode);

        private Int32 OriginHits(String path)
            => originHits.TryGetValue(path, out var c) ? c : 0;

        #endregion


        #region Freshness_MaxAge_MissThenHit()

        [Test]
        public async Task Freshness_MaxAge_MissThenHit()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            var r1 = await cache.GetAsync("/max-age-60");
            var r2 = await cache.GetAsync("/max-age-60");
            Assert.Multiple(() =>
            {
                Assert.That(r1.Status,                        Is.EqualTo(200));
                Assert.That(r2.Status,                        Is.EqualTo(200));
                Assert.That(OriginHits("/max-age-60"),         Is.EqualTo(1), "only one origin fetch (2nd served fresh)");
                Assert.That(Encoding.UTF8.GetString(r2.Body),  Is.EqualTo("fresh"), "cached body identical");
                Assert.That(r2.HeaderValue("age"),             Is.Not.Null, "served response carries Age");
            });
        }

        #endregion

        #region Revalidation_MaxAge0_304()

        [Test]
        public async Task Revalidation_MaxAge0_304()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            var v1 = await cache.GetAsync("/revalidate");
            var revalBefore = cache.Revalidations;
            var v2 = await cache.GetAsync("/revalidate");
            Assert.Multiple(() =>
            {
                Assert.That(v1.Status,                        Is.EqualTo(200));
                Assert.That(v2.Status,                        Is.EqualTo(200));
                Assert.That(OriginHits("/revalidate"),         Is.EqualTo(2), "origin contacted twice (revalidation)");
                Assert.That(cache.Revalidations,               Is.EqualTo(revalBefore + 1), "counted as a revalidation");
                Assert.That(Encoding.UTF8.GetString(v2.Body),  Is.EqualTo("revalidated"), "body served from cache after 304");
            });
        }

        #endregion

        #region NoStore_NeverCached()

        [Test]
        public async Task NoStore_NeverCached()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            await cache.GetAsync("/no-store");
            await cache.GetAsync("/no-store");
            Assert.That(OriginHits("/no-store"), Is.EqualTo(2), "origin fetched every time");
        }

        #endregion

        #region Vary_SeparateVariants()

        [Test]
        public async Task Vary_SeparateVariants()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            await cache.GetAsync("/vary", [("accept-language", "en")]);
            await cache.GetAsync("/vary", [("accept-language", "en")]);            // HIT (en)
            var deResp = await cache.GetAsync("/vary", [("accept-language", "de")]); // MISS (de)
            await cache.GetAsync("/vary", [("accept-language", "de")]);            // HIT (de)
            Assert.Multiple(() =>
            {
                Assert.That(OriginHits("/vary"),                  Is.EqualTo(2), "one fetch per distinct variant");
                Assert.That(Encoding.UTF8.GetString(deResp.Body),  Is.EqualTo("lang=de"), "de variant body correct");
            });
        }

        #endregion

        #region HeuristicFreshness()

        [Test]
        public async Task HeuristicFreshness()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            await cache.GetAsync("/heuristic");
            await cache.GetAsync("/heuristic");
            Assert.That(OriginHits("/heuristic"), Is.EqualTo(1), "heuristically fresh -> 2nd served from cache");
        }

        #endregion

        #region OnlyIfCached_NothingStored_504()

        [Test]
        public async Task OnlyIfCached_NothingStored_504()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            var oic = await cache.GetAsync("/never-seen", [("cache-control", "only-if-cached")]);
            Assert.That(oic.Status, Is.EqualTo(504), "only-if-cached with nothing stored -> 504");
        }

        #endregion

        #region Invalidation_OnUnsafeMethod()

        [Test]
        public async Task Invalidation_OnUnsafeMethod()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            await cache.GetAsync("/invalidate");
            await cache.GetAsync("/invalidate");                                  // HIT
            Assert.That(OriginHits("/invalidate"), Is.EqualTo(1), "cached before POST");
            await cache.SendRequestAsync("POST", "/invalidate", Body: Encoding.UTF8.GetBytes("x"));
            await cache.GetAsync("/invalidate");                                  // MISS again
            Assert.That(OriginHits("/invalidate"), Is.EqualTo(3), "re-fetched after POST invalidation");
        }

        #endregion

        #region StaleWhileRevalidate()

        [Test]
        public async Task StaleWhileRevalidate()
        {
            var cache = await NewCache(HTTPCacheMode.Private);
            await cache.GetAsync("/swr");                                          // MISS, store (max-age=0)
            var swrHitsBefore = cache.Hits;
            var swr = await cache.GetAsync("/swr");                                // stale -> served + bg revalidate
            Assert.Multiple(() =>
            {
                Assert.That(cache.Hits,                          Is.EqualTo(swrHitsBefore + 1), "stale served immediately (a hit)");
                Assert.That(Encoding.UTF8.GetString(swr.Body),    Is.EqualTo("swr-body"));
            });
            await Task.Delay(500);                                                 // let the background revalidation land
            Assert.That(OriginHits("/swr"), Is.GreaterThanOrEqualTo(2), "background revalidation contacted origin");
        }

        #endregion

        #region PrivateDirective_SharedVsPrivate()

        [Test]
        public async Task PrivateDirective_SharedVsPrivate()
        {
            var shared = await NewCache(HTTPCacheMode.Shared);
            await shared.GetAsync("/private");
            await shared.GetAsync("/private");
            Assert.That(OriginHits("/private"), Is.EqualTo(2), "shared cache does NOT store a 'private' response");

            var privateCache = await NewCache(HTTPCacheMode.Private);
            await privateCache.GetAsync("/private");
            await privateCache.GetAsync("/private");
            Assert.That(OriginHits("/private"), Is.EqualTo(3), "private cache DOES store it");
        }

        #endregion

        #region Shared_SMaxAge()

        [Test]
        public async Task Shared_SMaxAge()
        {
            var shared = await NewCache(HTTPCacheMode.Shared);
            await shared.GetAsync("/s-maxage");
            await shared.GetAsync("/s-maxage");
            Assert.That(OriginHits("/s-maxage"), Is.EqualTo(1), "shared: s-maxage=60 -> fresh HIT");

            var priv = await NewCache(HTTPCacheMode.Private);
            await priv.GetAsync("/s-maxage");
            await priv.GetAsync("/s-maxage");
            Assert.That(OriginHits("/s-maxage"), Is.EqualTo(3), "private: ignores s-maxage, max-age=0 -> revalidate");
        }

        #endregion

        #region Shared_Authorization()

        [Test]
        public async Task Shared_Authorization()
        {
            var auth = new List<(String Name, String Value)> { ("authorization", "Bearer t") };

            var shared = await NewCache(HTTPCacheMode.Shared);
            await shared.GetAsync("/auth-cacheable", auth);
            await shared.GetAsync("/auth-cacheable", auth);
            Assert.That(OriginHits("/auth-cacheable"), Is.EqualTo(2), "shared cache does NOT store an authenticated response");

            var priv = await NewCache(HTTPCacheMode.Private);
            await priv.GetAsync("/auth-cacheable", auth);
            await priv.GetAsync("/auth-cacheable", auth);
            Assert.That(OriginHits("/auth-cacheable"), Is.EqualTo(3), "private cache stores it");
        }

        #endregion

    }

}
