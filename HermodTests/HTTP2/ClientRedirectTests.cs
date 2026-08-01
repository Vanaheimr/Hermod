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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Client-side redirect following (RFC 9110, Section 15.4).
    ///
    /// The interesting part is not the loop, it is the rewriting: 301/302 turn a POST
    /// into a GET, 303 turns everything except HEAD into a GET, and 307/308 must
    /// preserve method *and* body. Those rules are asymmetric on purpose — 307/308
    /// exist because the older codes cannot be trusted to preserve anything.
    ///
    /// Following deliberately stops at the origin boundary: a connection speaks to
    /// the origin it dialed, and pooling here is single-origin by design, so a
    /// cross-origin Location comes back unfollowed rather than turning the connection
    /// into a multi-origin client. That same boundary is what keeps credentials from
    /// travelling to an origin that never asked for them.
    /// </summary>
    [TestFixture]
    public class ClientRedirectTests
    {

        #region Data / server

        /// <summary>
        /// Records what each hop actually looked like, so the tests can assert on the
        /// rewriting rather than just on the final status.
        /// </summary>
        private sealed record Seen(String Method, String Path, Int32 BodyLength, String? ContentLength, String? Authorization);

        private static readonly List<Seen> seen = [];

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 streamId,
                                                                     List<(String Name, String Value)> headers,
                                                                     Byte[]? body,
                                                                     CancellationToken ct)
        {

            var method = headers.First(h => h.Name == ":method").Value;
            var path   = headers.First(h => h.Name == ":path").Value;

            lock (seen)
                seen.Add(new Seen(method, path, body?.Length ?? 0,
                                  headers.FirstOrDefault(h => h.Name == "content-length").Value,
                                  headers.FirstOrDefault(h => h.Name == "authorization").Value));

            List<(String, String)> Redirect(Int32 status, String location)
                => [(":status", status.ToString()), ("location", location)];

            return Task.FromResult<(List<(String, String)>, Byte[]?)>(path switch
            {

                "/301"        => (Redirect(301, "/final"),            null),
                "/302"        => (Redirect(302, "/final"),            null),
                "/303"        => (Redirect(303, "/final"),            null),
                "/307"        => (Redirect(307, "/final"),            null),
                "/308"        => (Redirect(308, "/final"),            null),

                // A relative reference that has to be resolved against the request
                // path, not against the root (RFC 3986, Section 5).
                "/deep/here"  => (Redirect(302, "sibling"),           null),

                // Off-origin: must come back unfollowed.
                "/offsite"    => (Redirect(302, "https://elsewhere.example/there"), null),

                // A chain, to exercise the hop limit.
                "/hop1"       => (Redirect(302, "/hop2"),             null),
                "/hop2"       => (Redirect(302, "/hop3"),            null),
                "/hop3"       => (Redirect(302, "/final"),            null),

                // A loop, which the hop limit must also stop.
                "/loop"       => (Redirect(302, "/loop"),             null),

                // 300 and 304 are not followable redirections.
                "/300"        => ([(":status", "300"), ("location", "/final")], null),

                _             => ([(":status", "200"), ("content-type", "text/plain")],
                                  Encoding.UTF8.GetBytes($"{method} {path}"))

            });

        }

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(Handle);

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        [SetUp]
        public void ClearLog()
        {
            lock (seen)
                seen.Clear();
        }

        private async Task<HTTP2ClientConnection> Connect(Int32 maxRedirects = 5)
            => await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                                               Options: new HTTP2ClientOptions { MaxRedirects = maxRedirects });

        private String Authority => $"localhost:{srv.Port}";

        #endregion

        #region Resolve_AppliesTheRewritingRules()

        [Test]
        public void Resolve_AppliesTheRewritingRules()
        {

            (HTTPMethod Method, Boolean KeepBody) Follow(Int32 status, HTTPMethod method)
            {
                Assert.That(HTTPRedirect.TryResolve(status, "/next", "https", "example.com", "/here", method, out var t), Is.True);
                return (t!.Method, t.KeepBody);
            }

            Assert.Multiple(() =>
            {
                // 301/302: POST becomes GET, everything else is preserved.
                Assert.That(Follow(301, HTTPMethod.POST),   Is.EqualTo((HTTPMethod.GET,    false)));
                Assert.That(Follow(302, HTTPMethod.POST),   Is.EqualTo((HTTPMethod.GET,    false)));
                Assert.That(Follow(302, HTTPMethod.PUT),    Is.EqualTo((HTTPMethod.PUT,    true)), "only POST is rewritten");
                Assert.That(Follow(302, HTTPMethod.GET),    Is.EqualTo((HTTPMethod.GET,    true)));

                // 303: always GET — except HEAD, which still only wants headers.
                Assert.That(Follow(303, HTTPMethod.POST),   Is.EqualTo((HTTPMethod.GET,    false)));
                Assert.That(Follow(303, HTTPMethod.PUT),    Is.EqualTo((HTTPMethod.GET,    false)));
                Assert.That(Follow(303, HTTPMethod.HEAD),   Is.EqualTo((HTTPMethod.HEAD,   false)));

                // 307/308 preserve both, which is the whole reason they exist.
                Assert.That(Follow(307, HTTPMethod.POST),   Is.EqualTo((HTTPMethod.POST,   true)));
                Assert.That(Follow(308, HTTPMethod.DELETE), Is.EqualTo((HTTPMethod.DELETE, true)));
            });

        }

        #endregion

        #region Resolve_RefusesWhatMustNotBeFollowed()

        [Test]
        public void Resolve_RefusesWhatMustNotBeFollowed()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTPRedirect.IsRedirect(300), Is.False, "300 needs a choice made, not a follow");
                Assert.That(HTTPRedirect.IsRedirect(304), Is.False, "304 is an answer, not a redirect");
                Assert.That(HTTPRedirect.IsRedirect(200), Is.False);

                Assert.That(HTTPRedirect.TryResolve(302, null,   "https", "a.example", "/", HTTPMethod.GET, out _), Is.False, "no Location");
                Assert.That(HTTPRedirect.TryResolve(302, "   ",  "https", "a.example", "/", HTTPMethod.GET, out _), Is.False, "blank Location");
                Assert.That(HTTPRedirect.TryResolve(200, "/x",   "https", "a.example", "/", HTTPMethod.GET, out _), Is.False, "not a redirect");

                // A client must not be talked into speaking another protocol.
                Assert.That(HTTPRedirect.TryResolve(302, "ftp://a.example/x",  "https", "a.example", "/", HTTPMethod.GET, out _), Is.False, "ftp");
                Assert.That(HTTPRedirect.TryResolve(302, "file:///etc/passwd", "https", "a.example", "/", HTTPMethod.GET, out _), Is.False, "file");
            });
        }

        #endregion

        #region Resolve_ResolvesReferencesAndDetectsTheOrigin()

        [Test]
        public void Resolve_ResolvesReferencesAndDetectsTheOrigin()
        {

            HTTPRedirectTarget R(String location, String path = "/deep/here")
            {
                Assert.That(HTTPRedirect.TryResolve(302, location, "https", "example.com", path, HTTPMethod.GET, out var t), Is.True, location);
                return t!;
            }

            Assert.Multiple(() =>
            {
                Assert.That(R("sibling").Path,           Is.EqualTo("/deep/sibling"), "relative to the request path");
                Assert.That(R("/root").Path,             Is.EqualTo("/root"),         "absolute path");
                Assert.That(R("?q=1").Path,              Is.EqualTo("/deep/here?q=1"), "query-only reference");
                Assert.That(R("/x#frag").Path,           Is.EqualTo("/x"),            "the fragment never travels");

                Assert.That(R("https://example.com/x").SameOrigin,      Is.True,  "same scheme + authority");
                Assert.That(R("https://EXAMPLE.com/x").SameOrigin,      Is.True,  "host comparison is case-insensitive");
                Assert.That(R("https://example.com:443/x").SameOrigin,  Is.True,  "the default port is the same origin");
                Assert.That(R("http://example.com/x").SameOrigin,       Is.False, "a scheme change is a different origin");
                Assert.That(R("https://other.example/x").SameOrigin,    Is.False);
                Assert.That(R("https://example.com:8443/x").SameOrigin, Is.False, "a different port is a different origin");
            });

        }

        #endregion

        #region Follows_SameOriginRedirect()

        [Test]
        public async Task Follows_SameOriginRedirect()
        {

            var conn = await Connect();
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/302");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(resp.Body), Is.EqualTo("GET /final"));
                Assert.That(resp.RedirectChain,                 Has.Count.EqualTo(1));
                Assert.That(resp.RedirectChain[0],              Does.EndWith("/final"));
                Assert.That(seen.Select(s => s.Path),           Is.EqualTo(new[] { "/302", "/final" }));
            });

        }

        #endregion

        #region Follows_RelativeLocation()

        [Test]
        public async Task Follows_RelativeLocation()
        {

            var conn = await Connect();
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/deep/here");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,              Is.EqualTo(200));
                Assert.That(seen.Select(s => s.Path), Is.EqualTo(new[] { "/deep/here", "/deep/sibling" }),
                            "resolved against the request path, not the root");
            });

        }

        #endregion

        #region Post_BecomesGet_On303_AndKeepsBodyOn307()

        // The rewriting rules, observed on the wire rather than in the unit test: the
        // 303 hop must arrive as a bodiless GET with no stale content-length, and the
        // 307 hop must arrive as the original POST with the body intact.
        [Test]
        public async Task Post_BecomesGet_On303_AndKeepsBodyOn307()
        {

            var payload = Encoding.UTF8.GetBytes("some form data");
            var conn    = await Connect();

            var after303 = await conn.SendRequestAsync(HTTPMethod.POST, "https", Authority, "/303",
                               ExtraHeaders: [("content-type", "text/plain"), ("content-length", payload.Length.ToString())],
                               Body: payload);

            var hops303  = seen.ToList();
            lock (seen) seen.Clear();

            var after307 = await conn.SendRequestAsync(HTTPMethod.POST, "https", Authority, "/307",
                               ExtraHeaders: [("content-type", "text/plain"), ("content-length", payload.Length.ToString())],
                               Body: payload);

            var hops307 = seen.ToList();
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(after303.Status,                Is.EqualTo(200));
                Assert.That(hops303[0].Method,              Is.EqualTo(HTTPMethod.POST.ToString()), "the original request");
                Assert.That(hops303[1].Method,              Is.EqualTo(HTTPMethod.GET.ToString()),  "303 -> GET");
                Assert.That(hops303[1].BodyLength,          Is.EqualTo(0),      "and the body is dropped");
                Assert.That(hops303[1].ContentLength,       Is.Null,            "no stale content-length left describing it");

                Assert.That(after307.Status,                Is.EqualTo(200));
                Assert.That(hops307[1].Method,              Is.EqualTo(HTTPMethod.POST.ToString()), "307 preserves the method");
                Assert.That(hops307[1].BodyLength,          Is.EqualTo(payload.Length), "and replays the body");
                Assert.That(hops307[1].ContentLength,       Is.EqualTo(payload.Length.ToString()));
            });

        }

        #endregion

        #region CrossOriginRedirect_IsReturnedUnfollowed()

        // The architectural boundary: a connection does not dial a second origin.
        [Test]
        public async Task CrossOriginRedirect_IsReturnedUnfollowed()
        {

            var conn = await Connect();
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/offsite");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                    Is.EqualTo(302),  "handed back for the caller to act on");
                Assert.That(resp.HeaderValue("location"),   Is.EqualTo("https://elsewhere.example/there"), "Location intact");
                Assert.That(resp.RedirectChain,             Is.Empty,         "nothing was followed");
                Assert.That(seen,                           Has.Count.EqualTo(1), "and no second request was made");
            });

        }

        #endregion

        #region HopLimit_StopsAChainAndALoop()

        [Test]
        public async Task HopLimit_StopsAChainAndALoop()
        {

            // Two hops allowed, three needed: the third 302 is returned as-is.
            var limited = await Connect(maxRedirects: 2);
            var chain   = await limited.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/hop1");
            await limited.CloseAsync();

            var seenChain = seen.Select(s => s.Path).ToList();
            lock (seen) seen.Clear();

            // A Location pointing at itself would spin forever without the limit.
            var looping = await Connect(maxRedirects: 3);
            var loop    = await looping.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/loop");
            await looping.CloseAsync();

            var seenLoop = seen.Count;

            Assert.Multiple(() =>
            {
                Assert.That(chain.Status,      Is.EqualTo(302),                              "gave up at the limit");
                Assert.That(seenChain,         Is.EqualTo(new[] { "/hop1", "/hop2", "/hop3" }));
                Assert.That(chain.RedirectChain, Has.Count.EqualTo(2));

                Assert.That(loop.Status,       Is.EqualTo(302),                              "the loop terminated");
                Assert.That(seenLoop,          Is.EqualTo(4),                                "initial request + 3 hops, then stop");
            });

        }

        #endregion

        #region MultipleChoices_IsNotFollowed()

        [Test]
        public async Task MultipleChoices_IsNotFollowed()
        {

            var conn = await Connect();
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/300");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status, Is.EqualTo(300), "300 needs a choice made, not an automatic follow");
                Assert.That(seen,        Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region WithoutTheOption_NothingIsFollowed()

        [Test]
        public async Task WithoutTheOption_NothingIsFollowed()
        {

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, "https", Authority, "/302");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                  Is.EqualTo(302), "off by default");
                Assert.That(resp.HeaderValue("location"), Is.EqualTo("/final"));
                Assert.That(resp.RedirectChain,           Is.Empty);
            });

        }

        #endregion

    }

}
