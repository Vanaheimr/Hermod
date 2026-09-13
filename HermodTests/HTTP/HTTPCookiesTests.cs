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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTP cookies travel in two shapes, and the semicolon means something
    /// different in each: in a "Cookie" request header it separates one cookie
    /// from the next ("session=abc; theme=dark"), in a "Set-Cookie" response
    /// header it separates one cookie from its attributes ("session=abc;
    /// Path=/; HttpOnly"). A parser that does not know which of the two it is
    /// looking at turns "Path" and "HttpOnly" into cookies of their own.
    /// </summary>
    /// <remarks>
    /// That is what HTTPCookies.Parse did when handed a single text: it took
    /// the request-header route, and a session cookie set on its own arrived
    /// at the browser as four Set-Cookie lines, the real one without its path
    /// and without HttpOnly. The HTTPExtAPI never noticed, because it always
    /// sets two cookies at once and thereby took the other route.
    /// </remarks>
    [TestFixture]
    public class HTTPCookiesTests
    {

        #region Data

        private const String setCookie = "session=abc; Expires=Sun, 13 Sep 2026 07:17:13 GMT; Path=/; SameSite=strict; secure; HttpOnly";

        #endregion


        #region ASetCookieValue_IsOneCookieWithItsAttributes()

        [Test]
        public void ASetCookieValue_IsOneCookieWithItsAttributes()
        {

            var cookies  = HTTPCookies.ParseSetCookie(setCookie);
            var cookie   = cookies.Single();

            Assert.Multiple(() => {
                Assert.That(cookie.Name.ToString(),  Is.EqualTo("session"));
                Assert.That(cookie.Value,            Is.EqualTo("abc"));
                Assert.That(cookie.Path,             Is.EqualTo("/"));
                Assert.That(cookie.SameSite,         Is.EqualTo("strict"));
                Assert.That(cookie.Secure,           Is.True);
                Assert.That(cookie.HTTPOnly,         Is.True);
                Assert.That(cookie.Expires,          Is.EqualTo(new DateTimeOffset(2026, 9, 13, 7, 17, 13, TimeSpan.Zero)));
            });

            // And it is written back as it came: one cookie, attributes included.
            var text = cookie.ToString();

            Assert.Multiple(() => {
                Assert.That(text,  Does.StartWith("session=abc; "));
                Assert.That(text,  Does.Contain("Path=/"));
                Assert.That(text,  Does.Contain("SameSite=strict"));
                Assert.That(text,  Does.Contain("HttpOnly"));
            });

        }

        #endregion

        #region Parse_WithASingleText_IsOneCookieAsWell()

        /// <summary>
        /// The regression: one text used to be split at the semicolon like a
        /// Cookie header.
        /// </summary>
        [Test]
        public void Parse_WithASingleText_IsOneCookieAsWell()
        {

            var cookies = HTTPCookies.Parse(setCookie);

            Assert.Multiple(() => {
                Assert.That(cookies.Count(),                 Is.EqualTo(1));
                Assert.That(cookies.Single().Name.ToString(), Is.EqualTo("session"));
                Assert.That(cookies.Single().Path,            Is.EqualTo("/"));
                Assert.That(cookies.Single().HTTPOnly,        Is.True);
            });

        }

        #endregion

        #region Parse_WithSeveralTexts_IsOneCookieEach()

        /// <summary>
        /// The route the HTTPExtAPI takes: an account cookie and a session
        /// cookie, one text each.
        /// </summary>
        [Test]
        public void Parse_WithSeveralTexts_IsOneCookieEach()
        {

            var cookies = HTTPCookies.Parse("account=alice; Path=/",
                                            "session=abc; Path=/; HttpOnly");

            Assert.Multiple(() => {
                Assert.That(cookies.Count(),                                         Is.EqualTo(2));
                Assert.That(cookies.Get(HTTPCookieName.Parse("account"))?.Value,     Is.EqualTo("alice"));
                Assert.That(cookies.Get(HTTPCookieName.Parse("account"))?.HTTPOnly,  Is.False);
                Assert.That(cookies.Get(HTTPCookieName.Parse("session"))?.Value,     Is.EqualTo("abc"));
                Assert.That(cookies.Get(HTTPCookieName.Parse("session"))?.HTTPOnly,  Is.True);
            });

        }

        #endregion

        #region ACookieHeaderValue_IsSeveralCookies()

        [Test]
        public void ACookieHeaderValue_IsSeveralCookies()
        {

            var cookies = HTTPCookies.ParseCookieHeader("session=abc; theme=dark");

            Assert.Multiple(() => {
                Assert.That(cookies.Count(),                                     Is.EqualTo(2));
                Assert.That(cookies.Get(HTTPCookieName.Parse("session"))?.Value,  Is.EqualTo("abc"));
                Assert.That(cookies.Get(HTTPCookieName.Parse("theme"))?.Value,    Is.EqualTo("dark"));
            });

            // The old name means the same thing.
            Assert.That(HTTPCookies.TryParse("session=abc; theme=dark", out var sameCookies),  Is.True);
            Assert.That(sameCookies?.Count(),                                                  Is.EqualTo(2));

        }

        #endregion

        #region TheSetCookieHeaderField_ReadsOneCookie()

        /// <summary>
        /// A response read by Hermod's own parser, e.g. on the client side.
        /// </summary>
        [Test]
        public void TheSetCookieHeaderField_ReadsOneCookie()
        {

            var response  = HTTPResponse.Parse("HTTP/1.1 200 OK\r\n" +
                                               "Set-Cookie: " + setCookie + "\r\n" +
                                               "Content-Length: 0\r\n" +
                                               "\r\n");

            var cookies   = response.GetHeaderField(HTTPResponseHeaderField.SetCookie);

            Assert.That(cookies, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(cookies!.Count(),                 Is.EqualTo(1));
                Assert.That(cookies.Single().Name.ToString(), Is.EqualTo("session"));
                Assert.That(cookies.Single().Path,            Is.EqualTo("/"));
                Assert.That(cookies.Single().HTTPOnly,        Is.True);
            });

        }

        #endregion

        #region TheCookieHeaderField_ReadsSeveralCookies()

        [Test]
        public void TheCookieHeaderField_ReadsSeveralCookies()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\n" +
                                             "Host: example.test\r\n" +
                                             "Cookie: session=abc; theme=dark\r\n" +
                                             "\r\n",
                                             out var request),
                        Is.True);

            Assert.Multiple(() => {
                Assert.That(request!.Cookies.Count(),                                     Is.EqualTo(2));
                Assert.That(request.Cookies.Get(HTTPCookieName.Parse("session"))?.Value,  Is.EqualTo("abc"));
                Assert.That(request.Cookies.Get(HTTPCookieName.Parse("theme"))?.Value,    Is.EqualTo("dark"));
            });

        }

        #endregion

        #region ASetCookie_IsWrittenAsOneLine()

        /// <summary>
        /// What reaches the browser: one Set-Cookie line per cookie, with the
        /// attributes on that line and not as lines of their own.
        /// </summary>
        [Test]
        public void ASetCookie_IsWrittenAsOneLine()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: example.test\r\n\r\n", out var request), Is.True);

            var one  = new HTTPResponse.Builder(request!) {
                           HTTPStatusCode  = HTTPStatusCode.OK,
                           SetCookie       = HTTPCookies.ParseSetCookie(setCookie)
                       }.AsImmutable.EntirePDU;

            var lines = one.Split("\r\n").Where(line => line.StartsWith("Set-Cookie:")).ToList();

            Assert.Multiple(() => {
                Assert.That(lines,     Has.Count.EqualTo(1));
                Assert.That(lines[0],  Does.StartWith("Set-Cookie: session=abc; "));
                Assert.That(lines[0],  Does.Contain("Path=/"));
                Assert.That(lines[0],  Does.Contain("HttpOnly"));
            });

            var two  = new HTTPResponse.Builder(request!) {
                           HTTPStatusCode  = HTTPStatusCode.OK,
                           SetCookie       = HTTPCookies.Parse("account=alice; Path=/", "session=abc; Path=/; HttpOnly")
                       }.AsImmutable.EntirePDU;

            Assert.That(two.Split("\r\n").Count(line => line.StartsWith("Set-Cookie:")), Is.EqualTo(2));

        }

        #endregion

        #region ATextWithoutAnEqualsSign_IsNoCookie()

        /// <summary>
        /// "HttpOnly" on its own is an attribute without a cookie. It used to
        /// throw inside the parser rather than say so.
        /// </summary>
        [Test]
        public void ATextWithoutAnEqualsSign_IsNoCookie()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.TryParse("HttpOnly",              out _),  Is.False);
                Assert.That(HTTPCookie.TryParse("=abc",                  out _),  Is.False);
                Assert.That(HTTPCookies.TryParseSetCookie("HttpOnly",    out _),  Is.False);
                Assert.That(HTTPCookies.TryParseSetCookie("",            out _),  Is.False);
                Assert.That(HTTPCookies.TryParseCookieHeader("",         out _),  Is.False);
            });

        }

        #endregion

    }

}
