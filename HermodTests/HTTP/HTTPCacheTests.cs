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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9111's rules as HTTPCache states them for every HTTP version: the
    /// parts a transport cannot show, and the seven deviations the HTTP/2 cache
    /// had when the logic moved to Hermod/HTTP (H-5).
    /// </summary>
    [TestFixture]
    public class HTTPCacheTests
    {

        #region (private) Helpers

        private static readonly DateTimeOffset T0 = new (2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        private static HTTPStoredResponse Stored(Int32                         Status,
                                                 params (String, String)[]     Headers)

            => new () {
                   Status          = Status,
                   Headers         = [ ("date", HTTPDate.ToHTTPDate(T0)), .. Headers ],
                   Body            = [],
                   RequestTime     = T0,
                   ResponseTime    = T0,
                   VaryKeyHeaders  = []
               };

        private static Boolean Storable(Int32                      Status,
                                        HTTPCacheMode              Mode,
                                        params (String, String)[]  Headers)
        {

            var headers = Headers.ToList();

            return HTTPCache.IsStorable(HTTPMethod.GET,
                                        false,
                                        HTTPCacheControl.Empty,
                                        Status,
                                        headers,
                                        HTTPCacheControl.FromHeaders(headers),
                                        Mode);

        }

        private static HTTPCacheUsability Usability(HTTPStoredResponse  Stored,
                                                    TimeSpan            After,
                                                    String?             RequestCacheControl  = null,
                                                    HTTPCacheMode       Mode                 = HTTPCacheMode.Private)

            => HTTPCache.Evaluate(Stored,
                                  HTTPCacheControl.Parse(RequestCacheControl),
                                  Mode,
                                  T0 + After).Usability;

        #endregion


        #region Cache-Control: the first occurrence of a directive counts

        /// <summary>
        /// RFC 9111, Section 4.2.1. The parser kept the last one, so a response
        /// sent with "max-age=0, max-age=3600" was fresh for an hour.
        /// </summary>
        [Test]
        public void TheFirstOccurrenceOfADirectiveCounts()
        {
            Assert.That(HTTPCacheControl.Parse("max-age=0, max-age=3600").MaxAge, Is.EqualTo(0));
        }

        #endregion

        #region Cache-Control: an invalid max-age is stale, not absent

        /// <summary>
        /// Section 4.2.1: "a cache SHOULD consider responses with invalid
        /// freshness information to be stale". It was read as absent, and the
        /// heuristic made the response fresh instead.
        /// </summary>
        [Test]
        public void AnInvalidMaxAgeIsStaleNotAbsent()
        {

            var stored = Stored(200, ("cache-control", "max-age=soon"),
                                     ("last-modified", HTTPDate.ToHTTPDate(T0.AddDays(-100))));

            Assert.Multiple(() => {
                Assert.That(HTTPCacheControl.Parse("max-age=soon").MaxAge,   Is.EqualTo(0));
                Assert.That(HTTPCacheControl.Parse("max-age=-1").MaxAge,     Is.EqualTo(0));
                Assert.That(HTTPCacheControl.Parse("s-maxage=1.5").SMaxAge,  Is.EqualTo(0));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(1)),      Is.EqualTo(HTTPCacheUsability.MustRevalidate));
            });

        }

        #endregion

        #region Cache-Control: delta-seconds too large are MaxDeltaSeconds, and a quoted argument is read

        /// <summary>
        /// Section 1.2.2: a value too large to represent is read as the
        /// greatest one the cache can conveniently represent.
        /// </summary>
        [Test]
        public void DeltaSecondsTooLargeAreMaxDeltaSeconds()
        {
            Assert.Multiple(() => {
                Assert.That(HTTPCacheControl.Parse("max-age=99999999999999999999999").MaxAge, Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
                Assert.That(HTTPCacheControl.Parse("max-age=9223372036854775808").MaxAge,     Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds), "2^63, which wraps to a negative Int64");
                Assert.That(HTTPCacheControl.Parse("max-age=\"60\"").MaxAge,                  Is.EqualTo(60));
            });
        }

        #endregion

        #region Delta-seconds: beyond 2^31, a value is what it says (H-46)

        /// <summary>
        /// Section 1.2.2 caps a value at 2^31 "or the greatest positive integer
        /// it can conveniently represent", and a TimeSpan represents far more.
        /// Every value was capped at 2^31: max-age=4294967295 lasted 68 years,
        /// and a response whose Age was 2147483646 was stale three seconds
        /// later.
        /// </summary>
        [Test]
        public void BeyondTwoToThe31AValueIsWhatItSays()
        {

            var stored = Stored(200, ("cache-control", "max-age=4294967295"), ("age", "2147483646"));

            Assert.Multiple(() => {
                Assert.That(HTTPCacheControl.Parse("max-age=4294967295").MaxAge,                Is.EqualTo(4294967295L));
                Assert.That(HTTPCacheControl.Parse("s-maxage=4294967296").SMaxAge,              Is.EqualTo(4294967296L));
                Assert.That(HTTPCacheControl.Parse("stale-if-error=2147483649").StaleIfError,   Is.EqualTo(2147483649L));
                Assert.That(HTTPCacheControl.Parse("max-stale=274877906944").MaxStale,          Is.EqualTo(274877906944L), "2^38, the cap itself");
                Assert.That(HTTPCacheControl.Parse("min-fresh=274877906945").MinFresh,          Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
                Assert.That(Stored(200, ("age", "4294967296")).AgeValue,                        Is.EqualTo(4294967296L));
                Assert.That(Stored(200, ("age", "274877906945")).AgeValue,                      Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(3)),                         Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(HTTPCache.CurrentAge(stored, T0.AddSeconds(3)),                     Is.EqualTo(TimeSpan.FromSeconds(2147483649)));
            });

        }

        /// <summary>
        /// Section 1.2.2: what matters is that an overflow is detected, not
        /// treated as a value. At the cap, with an Expires as far before its
        /// Date as HTTP-dates go, the arithmetic of Section 4.2.3 still holds.
        /// </summary>
        [Test]
        public void AtTheCapTheArithmeticDoesNotOverflow()
        {

            var stored = new HTTPStoredResponse() {
                             Status          = 200,
                             Headers         = [ ("date",    "Fri, 31 Dec 9999 23:59:59 GMT"),
                                                 ("expires", "Mon, 01 Jan 0001 00:00:00 GMT"),
                                                 ("age",     "99999999999999999999999") ],
                             Body            = [],
                             RequestTime     = T0,
                             ResponseTime    = T0,
                             VaryKeyHeaders  = []
                         };

            var request = HTTPCacheControl.Parse("max-stale=99999999999999999999, min-fresh=99999999999999999999, stale-if-error=99999999999999999999");

            Assert.Multiple(() => {
                Assert.That(stored.AgeValue,                                                      Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
                Assert.That(HTTPCache.FreshnessLifetime(stored, HTTPCacheMode.Shared),            Is.LessThan(-TimeSpan.FromDays(3_600_000)), "the Expires is read");
                Assert.That(HTTPCache.Evaluate(stored, request, HTTPCacheMode.Shared, T0).Usability, Is.EqualTo(HTTPCacheUsability.MustRevalidate), "stale by more than any max-stale");
                Assert.That(HTTPCache.ServesStaleOnError(stored, request, HTTPCacheMode.Shared, T0),  Is.False);
            });

        }

        #endregion

        #region Cache-Control: a comma inside a quoted argument does not end the directive

        [Test]
        public void ACommaInsideAQuotedArgumentDoesNotEndTheDirective()
        {

            var cc = HTTPCacheControl.Parse("no-cache=\"set-cookie, max-age=99\", max-age=5");

            Assert.Multiple(() => {
                Assert.That(cc.NoCache, Is.True);
                Assert.That(cc.MaxAge,  Is.EqualTo(5));
            });

        }

        #endregion


        #region Freshness: an invalid Expires has expired, and the heuristic does not apply

        /// <summary>
        /// Section 5.3: "A cache recipient MUST interpret invalid date formats,
        /// especially the value "0", as representing a time in the past". The
        /// HTTP/2 cache read "0" as no Expires at all and let the Last-Modified
        /// heuristic make the response fresh.
        /// </summary>
        [Test]
        public void AnInvalidExpiresHasExpired()
        {

            var lastModified = ("last-modified", HTTPDate.ToHTTPDate(T0.AddDays(-100)));

            Assert.Multiple(() => {
                Assert.That(HTTPCache.FreshnessLifetime(Stored(200, ("expires", "0"), lastModified), HTTPCacheMode.Private),
                            Is.EqualTo(TimeSpan.Zero));
                Assert.That(Usability(Stored(200, ("expires", "0"), lastModified), TimeSpan.FromSeconds(1)),
                            Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(HTTPCache.FreshnessLifetime(Stored(200, lastModified), HTTPCacheMode.Private),
                            Is.EqualTo(TimeSpan.FromDays(10)), "the heuristic itself, for comparison");
            });

        }

        #endregion

        #region Freshness: two Expires make the response stale

        /// <summary>
        /// Section 4.2.1: more than one Expires, and the response is considered stale.
        /// </summary>
        [Test]
        public void TwoExpiresMakeTheResponseStale()
        {

            var stored = Stored(200, ("expires", HTTPDate.ToHTTPDate(T0.AddHours(1))),
                                     ("expires", HTTPDate.ToHTTPDate(T0.AddHours(2))));

            Assert.That(HTTPCache.FreshnessLifetime(stored, HTTPCacheMode.Private), Is.EqualTo(TimeSpan.Zero));

        }

        #endregion

        #region Freshness: Expires counts from Date, s-maxage only in a shared cache

        [Test]
        public void ExpiresCountsFromDateAndSMaxAgeOnlyInASharedCache()
        {

            var expires  = Stored(200, ("expires",       HTTPDate.ToHTTPDate(T0.AddMinutes(10))));
            var sMaxAge  = Stored(200, ("cache-control", "max-age=10, s-maxage=600"));

            Assert.Multiple(() => {
                Assert.That(HTTPCache.FreshnessLifetime(expires, HTTPCacheMode.Private), Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(HTTPCache.FreshnessLifetime(sMaxAge, HTTPCacheMode.Private), Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(HTTPCache.FreshnessLifetime(sMaxAge, HTTPCacheMode.Shared),  Is.EqualTo(TimeSpan.FromSeconds(600)));
            });

        }

        #endregion

        #region Freshness: the heuristic is for heuristically cacheable statuses, or public

        /// <summary>
        /// Section 4.2.2.
        /// </summary>
        [Test]
        public void TheHeuristicIsForHeuristicallyCacheableStatusesOrPublic()
        {

            var lastModified = ("last-modified", HTTPDate.ToHTTPDate(T0.AddDays(-100)));

            Assert.Multiple(() => {
                Assert.That(HTTPCache.FreshnessLifetime(Stored(302, lastModified),                          HTTPCacheMode.Private), Is.Null);
                Assert.That(HTTPCache.FreshnessLifetime(Stored(302, lastModified, ("cache-control", "public")), HTTPCacheMode.Private), Is.EqualTo(TimeSpan.FromDays(10)));
                Assert.That(HTTPCache.FreshnessLifetime(Stored(404, lastModified),                          HTTPCacheMode.Private), Is.EqualTo(TimeSpan.FromDays(10)));
            });

        }

        #endregion

        #region Age: the Age field, the response delay and the resident time

        /// <summary>
        /// Section 4.2.3, with an Age that is not delta-seconds read as 0.
        /// </summary>
        [Test]
        public void AgeCountsTheAgeFieldTheResponseDelayAndTheResidentTime()
        {

            var stored = Stored(200, ("age", "30"));
            stored.RequestTime = T0.AddSeconds(-2);

            Assert.Multiple(() => {
                Assert.That(HTTPCache.CurrentAge(stored, T0.AddSeconds(10)),             Is.EqualTo(TimeSpan.FromSeconds(42)));
                Assert.That(Stored(200, ("age", "-5")).AgeValue,                         Is.EqualTo(0));
                Assert.That(Stored(200, ("age", "1e3")).AgeValue,                        Is.EqualTo(0));
                Assert.That(Stored(200, ("age", "99999999999999999999")).AgeValue,       Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
            });

        }

        #endregion

        #region Age: of a list, the first member counts (H-44)

        /// <summary>
        /// Section 5.1: Age is a singleton, and a cache that receives a list
        /// SHOULD use its first member, discarding the rest. "7200, 0" on one
        /// field line was read as invalid, so as 0, and a response two hours
        /// old was served as fresh; on two lines it was already 7200.
        /// </summary>
        [Test]
        public void OfAnAgeListTheFirstMemberCounts()
        {

            Assert.Multiple(() => {
                Assert.That(Stored(200, ("age", "7200, 0")).AgeValue,                    Is.EqualTo(7200));
                Assert.That(Stored(200, ("age", "0, 7200")).AgeValue,                    Is.EqualTo(0));
                Assert.That(Stored(200, ("age", "7200,0")).AgeValue,                     Is.EqualTo(7200));
                Assert.That(Stored(200, ("age", " 7200 ,\t0")).AgeValue,                 Is.EqualTo(7200));
                Assert.That(Stored(200, ("age", "7200"), ("age", "0")).AgeValue,         Is.EqualTo(7200));
                Assert.That(Stored(200, ("age", "0"),    ("age", "7200")).AgeValue,      Is.EqualTo(0));
                Assert.That(Stored(200, ("age", ", 7200")).AgeValue,                     Is.EqualTo(7200), "RFC 9110, Section 5.6.1: an empty member is not one");
                Assert.That(Stored(200, ("age", ""), ("age", "7200")).AgeValue,          Is.EqualTo(7200), "RFC 9110, Section 5.6.1: an empty member is not one");
                Assert.That(Stored(200, ("age", "abc, 7200")).AgeValue,                  Is.EqualTo(0),    "an invalid first member makes the field invalid");
                Assert.That(Stored(200, ("age", "7200;foo=bar, 0")).AgeValue,            Is.EqualTo(0),    "a parameter is not delta-seconds");
                Assert.That(Stored(200, ("age", "99999999999999999999, 0")).AgeValue,    Is.EqualTo(HTTPCacheControl.MaxDeltaSeconds));
            });

        }

        #endregion


        #region Storable: a validator alone does not make a response storable

        /// <summary>
        /// Section 3 lists what makes a response storable: public, private in a
        /// private cache, Expires, max-age, s-maxage in a shared cache, or a
        /// heuristically cacheable status. The HTTP/2 cache counted an ETag or
        /// a Last-Modified as well, so a 302 with an ETag was stored.
        /// </summary>
        [Test]
        public void AValidatorAloneDoesNotMakeAResponseStorable()
        {
            Assert.Multiple(() => {
                Assert.That(Storable(302, HTTPCacheMode.Private, ("etag", "\"a\"")),                             Is.False);
                Assert.That(Storable(302, HTTPCacheMode.Private, ("last-modified", HTTPDate.ToHTTPDate(T0))),    Is.False);
                Assert.That(Storable(302, HTTPCacheMode.Private, ("cache-control", "max-age=60")),               Is.True);
                Assert.That(Storable(302, HTTPCacheMode.Private, ("cache-control", "public")),                   Is.True);
                Assert.That(Storable(302, HTTPCacheMode.Private, ("cache-control", "private")),                  Is.True);
                Assert.That(Storable(302, HTTPCacheMode.Shared,  ("cache-control", "private")),                  Is.False);
                Assert.That(Storable(200, HTTPCacheMode.Private),                                                Is.True);
            });
        }

        #endregion

        #region Storable: neither a 206 nor a 304

        /// <summary>
        /// Neither cache combines partial content (Sections 3.3 and 3.4), and a
        /// 304 only updates (Section 4.3.4). The HTTP/2 cache stored a 206 as
        /// the response to its path, and then answered a GET without Range with
        /// the part.
        /// </summary>
        [Test]
        public void NeitherA206NorA304IsStorable()
        {
            Assert.Multiple(() => {
                Assert.That(Storable(206, HTTPCacheMode.Private, ("cache-control", "max-age=60")), Is.False);
                Assert.That(Storable(304, HTTPCacheMode.Private, ("cache-control", "max-age=60")), Is.False);
                Assert.That(Storable(100, HTTPCacheMode.Private, ("cache-control", "max-age=60")), Is.False);
            });
        }

        #endregion

        #region Storable: never a response with an invalid status code

        /// <summary>
        /// A code outside 100-599 is invalid (RFC 9110, Section 15). The client
        /// takes it as a 5xx since H-49, and a 5xx with a max-age may be stored;
        /// one with an invalid code is not, by either cache.
        /// </summary>
        [Test]
        public void AnInvalidStatusCodeIsNotStorable()
        {
            Assert.Multiple(() => {
                Assert.That(Storable(500, HTTPCacheMode.Shared,  ("cache-control", "max-age=60")), Is.True,  "the control: a 5xx with a max-age");
                Assert.That(Storable(599, HTTPCacheMode.Shared,  ("cache-control", "max-age=60")), Is.True,  "599 is valid");
                Assert.That(Storable(600, HTTPCacheMode.Shared,  ("cache-control", "max-age=60")), Is.False);
                Assert.That(Storable(999, HTTPCacheMode.Shared,  ("cache-control", "max-age=60")), Is.False);
                Assert.That(Storable(999, HTTPCacheMode.Private, ("cache-control", "max-age=60")), Is.False);
                Assert.That(Storable( 99, HTTPCacheMode.Private, ("cache-control", "max-age=60")), Is.False);
            });
        }

        #endregion

        #region Storable: a shared cache stores an authenticated response only when told so

        /// <summary>
        /// Section 3.5.
        /// </summary>
        [Test]
        public void ASharedCacheStoresAnAuthenticatedResponseOnlyWhenToldSo()
        {

            Boolean Authenticated(HTTPCacheMode Mode, String CacheControl)
            {
                List<(String, String)> headers = [ ("cache-control", CacheControl) ];
                return HTTPCache.IsStorable(HTTPMethod.GET, true, HTTPCacheControl.Empty, 200, headers, HTTPCacheControl.FromHeaders(headers), Mode);
            }

            Assert.Multiple(() => {
                Assert.That(Authenticated(HTTPCacheMode.Shared,  "max-age=60"),          Is.False);
                Assert.That(Authenticated(HTTPCacheMode.Shared,  "max-age=60, public"),  Is.True);
                Assert.That(Authenticated(HTTPCacheMode.Shared,  "s-maxage=60"),         Is.True);
                Assert.That(Authenticated(HTTPCacheMode.Private, "max-age=60"),          Is.True);
            });

        }

        #endregion


        #region Evaluate: a fresh must-revalidate response is used without validation

        /// <summary>
        /// Section 5.2.2.2: must-revalidate applies "once the response has
        /// become stale". The HTTP/2 cache revalidated it while fresh too, so a
        /// response with "max-age=3600, must-revalidate" was never served from
        /// the cache at all.
        /// </summary>
        [Test]
        public void AFreshMustRevalidateResponseIsUsedWithoutValidation()
        {

            var stored = Stored(200, ("cache-control", "max-age=60, must-revalidate"));

            Assert.Multiple(() => {
                Assert.That(Usability(stored, TimeSpan.FromSeconds(30)),                    Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(90), "max-stale=600"),   Is.EqualTo(HTTPCacheUsability.MustRevalidate));
            });

        }

        #endregion

        #region Evaluate: a request's max-age is honoured, except by an immutable response

        /// <summary>
        /// Section 5.2.1.1, and RFC 8246, Section 2. The HTTP/2 cache parsed a
        /// request's max-age and never read it.
        /// </summary>
        [Test]
        public void ARequestsMaxAgeIsHonouredExceptByAnImmutableResponse()
        {

            var ordinary   = Stored(200, ("cache-control", "max-age=600"));
            var immutable  = Stored(200, ("cache-control", "max-age=600, immutable"));

            Assert.Multiple(() => {
                Assert.That(Usability(ordinary,  TimeSpan.FromSeconds(30), "max-age=60"),  Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(Usability(ordinary,  TimeSpan.FromSeconds(90), "max-age=60"),  Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(ordinary,  TimeSpan.FromSeconds(1),  "max-age=0"),   Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(immutable, TimeSpan.FromSeconds(1),  "max-age=0"),   Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(Usability(immutable, TimeSpan.FromSeconds(1),  "no-cache"),    Is.EqualTo(HTTPCacheUsability.MustRevalidate));
            });

        }

        #endregion

        #region Evaluate: min-fresh, max-stale and stale-while-revalidate

        [Test]
        public void MinFreshMaxStaleAndStaleWhileRevalidate()
        {

            var stored  = Stored(200, ("cache-control", "max-age=60"));
            var swr     = Stored(200, ("cache-control", "max-age=60, stale-while-revalidate=30"));

            Assert.Multiple(() => {
                Assert.That(Usability(stored, TimeSpan.FromSeconds(30),  "min-fresh=40"),   Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(30),  "min-fresh=20"),   Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(90),  "max-stale=60"),   Is.EqualTo(HTTPCacheUsability.Stale));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(90),  "max-stale=10"),   Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(900), "max-stale"),      Is.EqualTo(HTTPCacheUsability.Stale));
                Assert.That(Usability(swr,    TimeSpan.FromSeconds(80)),                    Is.EqualTo(HTTPCacheUsability.StaleWhileRevalidate));
                Assert.That(Usability(swr,    TimeSpan.FromSeconds(100)),                   Is.EqualTo(HTTPCacheUsability.MustRevalidate));
            });

        }

        #endregion

        #region Evaluate: in a shared cache, s-maxage and proxy-revalidate forbid serving stale

        /// <summary>
        /// Sections 4.2.4 and 5.2.2.10.
        /// </summary>
        [Test]
        public void InASharedCacheSMaxAgeAndProxyRevalidateForbidServingStale()
        {

            var sMaxAge  = Stored(200, ("cache-control", "s-maxage=60"));
            var proxy    = Stored(200, ("cache-control", "max-age=60, proxy-revalidate"));

            Assert.Multiple(() => {
                Assert.That(Usability(sMaxAge, TimeSpan.FromSeconds(90), "max-stale", HTTPCacheMode.Shared),  Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(proxy,   TimeSpan.FromSeconds(90), "max-stale", HTTPCacheMode.Shared),  Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(proxy,   TimeSpan.FromSeconds(90), "max-stale", HTTPCacheMode.Private), Is.EqualTo(HTTPCacheUsability.Stale));
            });

        }

        #endregion

        #region Stale-if-error: within its window, and not where serving stale is forbidden

        /// <summary>
        /// RFC 5861, Section 4.
        /// </summary>
        [Test]
        public void StaleIfErrorWithinItsWindow()
        {

            var stored  = Stored(200, ("cache-control", "max-age=60, stale-if-error=60"));
            var strict  = Stored(200, ("cache-control", "max-age=60, stale-if-error=60, must-revalidate"));
            var plain   = Stored(200, ("cache-control", "max-age=60"));

            Boolean OnError(HTTPStoredResponse Stored, Int32 After, String? RequestCacheControl = null)
                => HTTPCache.ServesStaleOnError(Stored, HTTPCacheControl.Parse(RequestCacheControl), HTTPCacheMode.Private, T0.AddSeconds(After));

            Assert.Multiple(() => {
                Assert.That(OnError(stored, 90),                       Is.True);
                Assert.That(OnError(stored, 150),                      Is.False);
                Assert.That(OnError(strict, 90),                       Is.False);
                Assert.That(OnError(plain,  90),                       Is.False);
                Assert.That(OnError(plain,  90, "stale-if-error=60"),  Is.True);
            });

        }

        #endregion


        #region 304: selects the stored response by its validator

        /// <summary>
        /// Section 4.3.4. The HTTP/2 cache applied any 304 to the response it
        /// had asked about, a 304 for another entity tag included.
        /// </summary>
        [Test]
        public void A304SelectsTheStoredResponseByItsValidator()
        {

            var stored = Stored(200, ("etag", "\"v1\""), ("last-modified", HTTPDate.ToHTTPDate(T0.AddDays(-1))));

            Assert.Multiple(() => {
                Assert.That(HTTPCache.Selects(stored, [ ("etag", "\"v1\"") ]),                                       Is.True);
                Assert.That(HTTPCache.Selects(stored, [ ("etag", "\"v2\"") ]),                                       Is.False);
                Assert.That(HTTPCache.Selects(stored, [ ("last-modified", HTTPDate.ToHTTPDate(T0.AddDays(-1))) ]),   Is.True);
                Assert.That(HTTPCache.Selects(stored, [ ("last-modified", HTTPDate.ToHTTPDate(T0)) ]),               Is.False);
                Assert.That(HTTPCache.Selects(stored, [ ("cache-control", "max-age=60") ]),                         Is.True);
            });

        }

        #endregion

        #region 304: updates the stored fields, but not those describing the stored content

        /// <summary>
        /// Section 3.2: not Content-Length, and with it neither the coding nor
        /// the range of the octets the cache holds. The HTTP/2 cache replaced
        /// every field the 304 carried.
        /// </summary>
        [Test]
        public void A304UpdatesTheStoredFieldsButNotThoseDescribingTheStoredContent()
        {

            var stored = Stored(200, ("cache-control",  "max-age=60"),
                                     ("content-length", "5"),
                                     ("x-version",      "1"),
                                     ("x-version",      "1b"));

            HTTPCache.UpdateFrom304(stored,
                                    [ ("cache-control",    "max-age=600"),
                                      ("content-length",   "0"),
                                      ("content-encoding", "gzip"),
                                      ("connection",       "x-hop"),
                                      ("x-hop",            "1"),
                                      ("keep-alive",       "timeout=5"),
                                      ("x-version",        "2") ],
                                    T0.AddMinutes(1),
                                    T0.AddMinutes(1));

            Assert.Multiple(() => {
                Assert.That(stored.Header("cache-control"),                                   Is.EqualTo("max-age=600"));
                Assert.That(stored.Header("content-length"),                                  Is.EqualTo("5"));
                Assert.That(stored.Header("content-encoding"),                                Is.Null);
                Assert.That(stored.Header("x-hop"),                                           Is.Null);
                Assert.That(stored.Header("keep-alive"),                                      Is.Null);
                Assert.That(stored.Headers.Where(h => h.Name == "x-version").Select(h => h.Value), Is.EqualTo(new[] { "2" }));
                Assert.That(stored.ResponseTime,                                              Is.EqualTo(T0.AddMinutes(1)));
            });

        }

        #endregion


        #region HEAD: matches by the validators and the Content-Length it carries (H-47)

        /// <summary>
        /// Section 4.3.5: each validator the HEAD response carries is the stored
        /// one, and so is its Content-Length. What it does not carry is not
        /// compared. Without a stored Content-Length, the stored octets count.
        /// </summary>
        [Test]
        public void AHEADResponseMatchesByItsValidatorsAndItsContentLength()
        {

            var lastModified  = HTTPDate.ToHTTPDate(T0.AddDays(-1));
            var stored        = Stored(200, ("etag", "\"v1\""), ("last-modified", lastModified), ("content-length", "5"));
            var bare          = Stored(200);
            bare.Body         = [ 1, 2, 3 ];

            Assert.Multiple(() => {
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("cache-control", "max-age=60") ]),                 Is.True);
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("etag", "\"v1\""), ("content-length", "5") ]),      Is.True);
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("etag", "\"v2\"") ]),                               Is.False);
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("last-modified", lastModified) ]),                  Is.True);
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("last-modified", HTTPDate.ToHTTPDate(T0)) ]),       Is.False);
                Assert.That(HTTPCache.HEADResponseMatches(stored, [ ("etag", "\"v1\""), ("content-length", "6") ]),      Is.False);
                Assert.That(HTTPCache.HEADResponseMatches(bare,   [ ("etag", "\"v1\"") ]),                               Is.False, "a validator the stored response lacks");
                Assert.That(HTTPCache.HEADResponseMatches(bare,   [ ("content-length", "3") ]),                          Is.True,  "the stored octets");
                Assert.That(HTTPCache.HEADResponseMatches(bare,   [ ("content-length", "4") ]),                          Is.False, "the stored octets");
            });

        }

        #endregion

        #region HEAD: a match updates the stored response and its freshness (H-47)

        /// <summary>
        /// Section 4.3.5, as cache-tests' head-200-update has it: a stale stored
        /// response, a HEAD response with a new max-age and a new field, and the
        /// stored response is fresh again and carries the new field.
        /// </summary>
        [Test]
        public void AMatchingHEADUpdatesTheStoredResponseAndItsFreshness()
        {

            var stored = Stored(200, ("cache-control", "max-age=2"), ("template-a", "1"), ("content-length", "5"));

            var staleBefore  = Usability(stored, TimeSpan.FromSeconds(3));
            var updated      = HTTPCache.UpdateFromHEAD(stored,
                                                        [ ("date",           HTTPDate.ToHTTPDate(T0.AddSeconds(3))),
                                                          ("cache-control",  "max-age=1000"),
                                                          ("template-a",     "2"),
                                                          ("content-length", "5") ],
                                                        T0.AddSeconds(3),
                                                        T0.AddSeconds(3));

            Assert.Multiple(() => {
                Assert.That(staleBefore,                       Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(updated,                           Is.True);
                Assert.That(stored.Header("template-a"),       Is.EqualTo("2"));
                Assert.That(stored.ConsideredStale,            Is.False);
                Assert.That(Usability(stored, TimeSpan.FromSeconds(4)), Is.EqualTo(HTTPCacheUsability.Fresh));
            });

        }

        #endregion

        #region HEAD: a mismatch makes the stored response stale until it is validated (H-47)

        /// <summary>
        /// Section 4.3.5: otherwise the stored response is considered stale.
        /// Stale, and so served where a stale response may be, under max-stale,
        /// and fresh again once a 304 validates it.
        /// </summary>
        [Test]
        public void AMismatchingHEADMakesTheStoredResponseStaleUntilItIsValidated()
        {

            var stored = Stored(200, ("cache-control", "max-age=60"), ("etag", "\"v1\""));

            var freshBefore  = Usability(stored, TimeSpan.FromSeconds(10));
            var updated      = HTTPCache.UpdateFromHEAD(stored,
                                                        [ ("cache-control", "max-age=600"), ("etag", "\"v2\"") ],
                                                        T0.AddSeconds(10),
                                                        T0.AddSeconds(10));

            Assert.Multiple(() => {
                Assert.That(freshBefore,                                                Is.EqualTo(HTTPCacheUsability.Fresh));
                Assert.That(updated,                                                    Is.False);
                Assert.That(stored.Header("cache-control"),                             Is.EqualTo("max-age=60"), "nothing is updated");
                Assert.That(stored.ConsideredStale,                                     Is.True);
                Assert.That(Usability(stored, TimeSpan.FromSeconds(10)),                Is.EqualTo(HTTPCacheUsability.MustRevalidate));
                Assert.That(Usability(stored, TimeSpan.FromSeconds(10), "max-stale=5"), Is.EqualTo(HTTPCacheUsability.Stale));
            });

            HTTPCache.UpdateFrom304(stored, [ ("etag", "\"v1\"") ], T0.AddSeconds(20), T0.AddSeconds(20));

            Assert.Multiple(() => {
                Assert.That(stored.ConsideredStale,                         Is.False);
                Assert.That(Usability(stored, TimeSpan.FromSeconds(30)),    Is.EqualTo(HTTPCacheUsability.Fresh));
            });

        }

        #endregion


        #region Vary: several lines of one field are compared combined

        /// <summary>
        /// Section 4.1. The HTTP/2 cache compared the first line only.
        /// </summary>
        [Test]
        public void SeveralLinesOfOneFieldAreComparedCombined()
        {

            List<(String, String)> first   = [ ("accept-language", "de"), ("accept-language", "en") ];
            List<(String, String)> second  = [ ("accept-language", "de"), ("accept-language", "fr") ];

            var stored = Stored(200, ("vary", "Accept-Language"));
            stored.VaryKeyHeaders = HTTPCache.SelectRequestHeaders(first, HTTPCache.VaryFieldNames(stored.Headers));

            Assert.Multiple(() => {
                Assert.That(HTTPCache.SelectionMatches(stored, first),                       Is.True);
                Assert.That(HTTPCache.SelectionMatches(stored, second),                      Is.False);
                Assert.That(HTTPCache.SelectionMatches(stored, [ ("accept-language", "de, en") ]), Is.True);
            });

        }

        #endregion


        #region Preconditions received: If-None-Match first, by weak comparison (H-45)

        private static Boolean NotModified(List<(String Name, String Value)>     Response,
                                           params (String Name, String Value)[]  Request)

            => HTTPCache.IsNotModified([.. Request], Response, T0);

        /// <summary>
        /// RFC 9111, Section 4.3.2 and RFC 9110, Section 13.1.2: "*", or an
        /// entity-tag in the list weakly equal to the response's ETag, makes the
        /// answer a 304. If-None-Match takes precedence over If-Modified-Since,
        /// in either direction. An ETag that is not an entity-tag matches nothing.
        /// </summary>
        [Test]
        public void IfNoneMatchComparesWeaklyAndTakesPrecedence()
        {

            List<(String Name, String Value)> response = [ ("etag", "\"v1\""), ("last-modified", HTTPDate.ToHTTPDate(T0)) ];

            var earlier  = HTTPDate.ToHTTPDate(T0.AddDays(-1));
            var later    = HTTPDate.ToHTTPDate(T0.AddDays(1));

            Assert.Multiple(() => {
                Assert.That(NotModified(response, ("if-none-match", "\"v1\"")),                                   Is.True);
                Assert.That(NotModified(response, ("if-none-match", "\"v0\", W/\"v1\"")),                         Is.True,  "weakly, any member");
                Assert.That(NotModified(response, ("if-none-match", "\"v0\""), ("if-none-match", "\"v1\"")),      Is.True,  "two field lines are one list");
                Assert.That(NotModified(response, ("if-none-match", "*")),                                        Is.True);
                Assert.That(NotModified(response, ("if-none-match", "\"v0\"")),                                   Is.False);
                Assert.That(NotModified(response, ("if-none-match", "\"v0\""), ("if-modified-since", later)),     Is.False, "If-Modified-Since alone would match");
                Assert.That(NotModified(response, ("if-none-match", "\"v1\""), ("if-modified-since", earlier)),   Is.True,  "If-Modified-Since alone would not");
                Assert.That(NotModified(response, ("if-none-match", "v1")),                                       Is.False, "not an entity-tag");
                Assert.That(NotModified([ ("etag", "v1") ],        ("if-none-match", "v1")),                      Is.False, "neither is an entity-tag");
                Assert.That(NotModified([ ("etag", "w/\"v1\"") ],  ("if-none-match", "w/\"v1\"")),                Is.False, "W/ is case-sensitive");
                Assert.That(NotModified([ ("etag", "W/\"v1\"") ],  ("if-none-match", "\"v1\"")),                  Is.True);
                Assert.That(NotModified([ ("etag", "\"v 1\"") ],   ("if-none-match", "\"v 1\"")),                 Is.False, "a space is no etagc");
                Assert.That(NotModified([ ],                       ("if-none-match", "\"v1\"")),                  Is.False, "no ETag");
                Assert.That(NotModified([ ],                       ("if-none-match", "*")),                       Is.True);
            });

        }

        #endregion

        #region Preconditions received: If-Modified-Since against Last-Modified, Date, or receipt (H-45)

        /// <summary>
        /// RFC 9111, Section 4.3.2 and RFC 9110, Section 13.1.3: a 304 where the
        /// response's Last-Modified is not later than the date, or, without one,
        /// its Date, or, without either, the time it was received. A date that is
        /// not valid, or one in two field lines, counts as no precondition.
        /// </summary>
        [Test]
        public void IfModifiedSinceComparesLastModifiedThenDateThenTheTimeOfReceipt()
        {

            var at      = HTTPDate.ToHTTPDate(T0);
            var before  = HTTPDate.ToHTTPDate(T0.AddSeconds(-1));
            var after   = HTTPDate.ToHTTPDate(T0.AddSeconds(1));

            Assert.Multiple(() => {
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", at)),      Is.True,  "not later");
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", after)),   Is.True);
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", before)),  Is.False);
                Assert.That(NotModified([ ("last-modified", at), ("date", before) ], ("if-modified-since", before)), Is.False, "Last-Modified before Date");
                Assert.That(NotModified([ ("date", at) ],                           ("if-modified-since", at)),      Is.True,  "Date where there is no Last-Modified");
                Assert.That(NotModified([ ("date", at) ],                           ("if-modified-since", before)),  Is.False);
                Assert.That(HTTPCache.IsNotModified([ ("if-modified-since", at) ], [ ], T0),                         Is.True,  "the time of receipt");
                Assert.That(HTTPCache.IsNotModified([ ("if-modified-since", at) ], [ ], T0.AddSeconds(1)),           Is.False, "the time of receipt");
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", "Saturday, 21-Nov-26 12:00:00 GMT")), Is.True, "an rfc850-date");
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", "yesterday")),                        Is.False, "not an HTTP-date");
                Assert.That(NotModified([ ("last-modified", at) ],                  ("if-modified-since", at), ("if-modified-since", at)),      Is.False, "two field lines");
                Assert.That(NotModified([ ("last-modified", "yesterday") ],         ("if-modified-since", at)),      Is.False, "a Last-Modified that is not one");
                Assert.That(NotModified([ ("last-modified", at) ]),                                                  Is.False, "no precondition");
            });

        }

        #endregion

        #region Preconditions received: a 304 carries the validators and no representation metadata (H-45)

        /// <summary>
        /// RFC 9110, Section 15.4.5: Cache-Control, Content-Location, Date, ETag,
        /// Expires and Vary, as the 200 has them, and the cache's Age; Last-Modified
        /// only where there is no ETag; no Content-Type, no Content-Length.
        /// </summary>
        [Test]
        public void ANotModifiedCarriesTheValidatorsAndNoRepresentationMetadata()
        {

            var at = HTTPDate.ToHTTPDate(T0);

            List<(String Name, String Value)> withETag = [ ("cache-control",    "max-age=60"),
                                                          ("content-type",     "text/plain"),
                                                          ("content-length",   "4"),
                                                          ("content-location", "/r"),
                                                          ("date",             at),
                                                          ("etag",             "\"v1\""),
                                                          ("expires",          at),
                                                          ("last-modified",    at),
                                                          ("vary",             "accept"),
                                                          ("x-other",          "1"),
                                                          ("age",              "3") ];

            Assert.Multiple(() => {
                Assert.That(HTTPCache.NotModifiedHeaders(withETag).Select(header => header.Name),
                            Is.EqualTo(new[] { "cache-control", "content-location", "date", "etag", "expires", "vary", "age" }));
                Assert.That(HTTPCache.NotModifiedHeaders([ ("content-type", "text/plain"), ("last-modified", at) ]),
                            Is.EqualTo(new List<(String, String)> { ("last-modified", at) }));
            });

        }

        #endregion

    }

}
