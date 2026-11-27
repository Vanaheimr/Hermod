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

using org.GraphDefined.Vanaheimr.Illias;
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
                Assert.That(cookie.SameSite,         Is.EqualTo("Strict"));
                Assert.That(cookie.Secure,           Is.True);
                Assert.That(cookie.HTTPOnly,         Is.True);
                Assert.That(cookie.Expires,          Is.EqualTo(new DateTimeOffset(2026, 9, 13, 7, 17, 13, TimeSpan.Zero)));
            });

            // And it is written back as one cookie, attributes included, each
            // in the spelling of RFC 6265, Section 4.1.1.
            var text = cookie.ToString();

            Assert.Multiple(() => {
                Assert.That(text,  Does.StartWith("session=abc; "));
                Assert.That(text,  Does.Contain("Path=/"));
                Assert.That(text,  Does.Contain("SameSite=Strict"));
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


        #region Set-Cookie: the name-value-pair (draft-ietf-httpbis-rfc6265bis-22, Section 5.6)

        /// <summary>
        /// Step 1: a control character other than HTAB anywhere, and the
        /// set-cookie-string is ignored entirely.
        /// </summary>
        /// <remarks>
        /// The character is given by its code point and put in place here:
        /// a test named with "\u0001" or "\u001f" in it runs in a full run but
        /// not under a FullyQualifiedName filter, which the regression
        /// selection and CI's filter are - measured, two of five cases missing.
        /// </remarks>
        [TestCase("a=b",              0x00, "c")]
        [TestCase("a=b",              0x01, "")]
        [TestCase("a",                0x1F, "=b")]
        [TestCase("a=b; Path=/",      0x7F, "")]
        [TestCase("a=b; Domain=x.y",  0x08, "")]
        [TestCase("a=b",              0x0A, "")]
        public void AControlCharacter_IsNoCookie(String Before, Int32 CodePoint, String After)
        {
            Assert.That(HTTPCookie.TryParse(Before + (Char) CodePoint + After, out _), Is.False);
        }

        /// <summary>
        /// HTAB is whitespace, and trimmed like a space - from the name and the
        /// value, not from inside them.
        /// </summary>
        [Test]
        public void NameAndValue_LoseTheWhitespaceAroundThem()
        {

            var cookie = HTTPCookie.Parse(" \tsession \t= \tabc def\t ;Path=/");

            Assert.Multiple(() => {
                Assert.That(cookie.Name.ToString(),  Is.EqualTo("session"));
                Assert.That(cookie.Value,            Is.EqualTo("abc def"));
                Assert.That(cookie.Path,             Is.EqualTo("/"));
            });

        }

        /// <summary>
        /// The value is everything after the first "=", as it was sent. The
        /// old parser read the value as Hermod's crumbs, and Value was the
        /// first crumb's name: "YWJj==" read as "YWJj", "b:c" as "b", and the
        /// quotes the RFC keeps (Section 4.1.1) stayed only by luck.
        /// </summary>
        [TestCase("YWJj==")]
        [TestCase("b:c")]
        [TestCase("x=y")]
        [TestCase("a:a")]
        [TestCase("\"quoted\"")]
        [TestCase("")]
        public void TheValue_IsKeptAsSent(String Value)
        {

            var cookie = HTTPCookie.Parse("name=" + Value + "; Path=/");

            Assert.Multiple(() => {
                Assert.That(cookie.Value,         Is.EqualTo(Value));
                Assert.That(cookie.ToString(),    Does.StartWith("name=" + Value + "; "));
                Assert.That(cookie.ToCookiePair(), Is.EqualTo("name=" + Value));
            });

        }

        /// <summary>
        /// Step 5: more than 4096 octets of name and value together, and the
        /// cookie is ignored. 4096 is still a cookie.
        /// </summary>
        [Test]
        public void NameAndValue_Of4096Octets_AreTheMost()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.TryParse("ab=" + new String('x', 4094), out _),  Is.True);
                Assert.That(HTTPCookie.TryParse("ab=" + new String('x', 4095), out _),  Is.False);
                Assert.That(HTTPCookie.TryParse("ab=" + new String('\u00e4', 2047), out _),  Is.True,   "octets, not characters: 2 + 2 * 2047");
                Assert.That(HTTPCookie.TryParse("ab=" + new String('\u00e4', 2048), out _),  Is.False);
            });

        }

        #endregion

        #region Set-Cookie: Expires (Sections 5.1.1 and 5.6.1)

        /// <summary>
        /// The cookie-date algorithm reads every form servers have written,
        /// always as UTC. The old parser was DateTimeOffset.Parse: it threw on
        /// asctime's order, and read a date without "GMT" in the local time
        /// zone of whatever machine it ran on.
        /// </summary>
        [TestCase("Wed, 21 Oct 2015 07:28:00 GMT",    "2015-10-21T07:28:00")]
        [TestCase("Wednesday, 21-Oct-15 07:28:00 GMT", "2015-10-21T07:28:00")]
        [TestCase("Sunday, 06-Nov-94 08:49:37 GMT",   "1994-11-06T08:49:37")]
        [TestCase("Sun Nov  6 08:49:37 1994",         "1994-11-06T08:49:37")]
        [TestCase("Wed, 21-Oct-2015 07:28:00 GMT",    "2015-10-21T07:28:00")]
        [TestCase("21 Oct 2015 07:28:00 GMT",         "2015-10-21T07:28:00")]
        [TestCase("07:28:00 21 Oct 2015",             "2015-10-21T07:28:00")]
        [TestCase("Wed, 21 October 2015 7:28:0 GMT",  "2015-10-21T07:28:00")]
        [TestCase("wed, 21 oct 2015 07:28:00 gmt",    "2015-10-21T07:28:00")]
        [TestCase("Wed, 21 Oct 2015 07:28:00",        "2015-10-21T07:28:00")]
        [TestCase("Wed, 21 Oct 2015 07:28:00 +0200",  "2015-10-21T07:28:00")]
        [TestCase("Thu, 01 Jan 70 00:00:00 GMT",      "1970-01-01T00:00:00")]
        [TestCase("Sat, 01 Jan 69 00:00:00 GMT",      "2069-01-01T00:00:00")]
        [TestCase("Mon, 29 Feb 2016 23:59:59 GMT",    "2016-02-29T23:59:59")]
        public void Expires_IsACookieDate(String Text, String Expected)
        {

            var cookie = HTTPCookie.Parse("a=b; Expires=" + Text);

            Assert.That(cookie.Expires, Is.EqualTo(new DateTimeOffset(DateTime.Parse(Expected, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero)));

        }

        /// <summary>
        /// An Expires that is no cookie-date is ignored, and the cookie is kept
        /// (Section 5.6.1, step 2). The old parser threw, out of TryParse and
        /// out of reading the Set-Cookie header field of a response.
        /// </summary>
        [TestCase("garbage")]
        [TestCase("")]
        [TestCase("Wed, 21 Oct 2015")]
        [TestCase("2015-10-21T07:28:00Z")]
        [TestCase("Wed, 32 Oct 2015 07:28:00 GMT")]
        [TestCase("Wed, 31 Feb 2015 07:28:00 GMT")]
        [TestCase("Tue, 29 Feb 2015 07:28:00 GMT")]
        [TestCase("Wed, 21 Oct 1600 07:28:00 GMT")]
        [TestCase("Wed, 21 Oct 2015 24:00:00 GMT")]
        [TestCase("Wed, 21 Oct 2015 07:60:00 GMT")]
        [TestCase("Wed, 21 Oct 2015 07:28:60 GMT")]
        [TestCase("Wed, 21 Oct 20155 07:28:00 GMT")]
        [TestCase("Wed, 210 Oct 2015 07:28:00 GMT")]
        [TestCase("Wed, 21 Oct 2015 007:28:00 GMT")]
        [TestCase("Wed, 21 Oct 2015 07:28:000 GMT")]
        public void AnExpiresThatIsNoDate_IsIgnored(String Text)
        {

            Assert.That(HTTPCookie.TryParse("a=b; Expires=" + Text, out var cookie), Is.True);

            Assert.Multiple(() => {
                Assert.That(cookie!.Expires,     Is.Null);
                Assert.That(cookie.ToString(),   Is.EqualTo("a=b"));
            });

            var response = HTTPResponse.Parse("HTTP/1.1 200 OK\r\nSet-Cookie: a=b; Expires=" + Text + "\r\nContent-Length: 0\r\n\r\n");

            Assert.That(response.GetHeaderField(HTTPResponseHeaderField.SetCookie)?.Single().Value, Is.EqualTo("b"));

        }

        /// <summary>
        /// The last Expires that is a date counts; one that is not changes nothing.
        /// </summary>
        [Test]
        public void TheLastExpiresThatIsADate_Counts()
        {

            var cookie = HTTPCookie.Parse("a=b; Expires=Wed, 21 Oct 2015 07:28:00 GMT; Expires=Thu, 22 Oct 2015 07:28:00 GMT; Expires=never");

            Assert.That(cookie.Expires, Is.EqualTo(new DateTimeOffset(2015, 10, 22, 7, 28, 0, TimeSpan.Zero)));

        }

        /// <summary>
        /// What a server writes: an IMF-fixdate (Section 4.1.1), in UTC.
        /// </summary>
        [Test]
        public void Expires_IsWrittenAsAnIMFFixdate()
        {

            var cookie = HTTPCookie.Parse("a=b; expires=Sun Nov  6 08:49:37 1994");

            Assert.That(cookie.ToString(), Is.EqualTo("a=b; Expires=Sun, 06 Nov 1994 08:49:37 GMT"));

        }

        #endregion

        #region Set-Cookie: Max-Age, Domain, Path, Secure, HttpOnly, SameSite (Sections 5.6.2 to 5.6.7)

        /// <summary>
        /// Section 5.6.2: a DIGIT, or "-" and a DIGIT, and digits after it.
        /// Anything else is ignored. Hermod had no Max-Age at all.
        /// </summary>
        [TestCase("3600",                  3600L)]
        [TestCase("0",                     0L)]
        [TestCase("-1",                    -1L)]
        [TestCase("007",                   7L)]
        [TestCase("99999999999999999999",  Int64.MaxValue)]
        [TestCase("-99999999999999999999", -Int64.MaxValue)]
        [TestCase("",                      null)]
        [TestCase("+5",                    null)]
        [TestCase("-",                     null)]
        [TestCase("5s",                    null)]
        [TestCase("1.5",                   null)]
        [TestCase("1 000",                 null)]
        public void MaxAge_IsDigits(String Text, Int64? Expected)
        {
            Assert.That(HTTPCookie.Parse("a=b; Max-Age=" + Text).MaxAge, Is.EqualTo(Expected));
        }

        [Test]
        public void TheLastMaxAgeThatIsANumber_Counts()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.Parse("a=b; Max-Age=5; Max-Age=7").MaxAge,     Is.EqualTo(7));
                Assert.That(HTTPCookie.Parse("a=b; Max-Age=5; max-age=soon").MaxAge,  Is.EqualTo(5));
            });

        }

        /// <summary>
        /// Section 5.6.3: a leading dot is dropped and the domain is lower
        /// case. An empty one leaves the cookie host-only (Section 5.7, step 7).
        /// </summary>
        [TestCase(".Example.COM",  "example.com")]
        [TestCase("site.example",  "site.example")]
        [TestCase("",              null)]
        [TestCase(".",             null)]
        public void Domain_IsLowerCaseWithoutADot(String Text, String? Expected)
        {
            Assert.That(HTTPCookie.Parse("a=b; Domain=" + Text).Domain, Is.EqualTo(Expected));
        }

        /// <summary>
        /// Section 5.7, step 8: a domain with a character outside US-ASCII,
        /// and the cookie is ignored. An IDN arrives as its A-label.
        /// </summary>
        [Test]
        public void ADomainOutsideASCII_IsNoCookie()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.TryParse("a=b; Domain=b\u00fccher.example",        out _),  Is.False);
                Assert.That(HTTPCookie.TryParse("a=b; Domain=xn--bcher-kva.example",  out _),  Is.True);
            });

        }

        /// <summary>
        /// Section 5.6.4: a path that does not begin with "/" is the default-path,
        /// which only the request knows - and the last Path counts, even then.
        /// </summary>
        [TestCase("/",          "/")]
        [TestCase("/a/b",       "/a/b")]
        [TestCase("a/b",        null)]
        [TestCase("",           null)]
        public void Path_BeginsWithASlash(String Text, String? Expected)
        {
            Assert.That(HTTPCookie.Parse("a=b; Path=" + Text).Path, Is.EqualTo(Expected));
        }

        [Test]
        public void TheLastPath_Counts_EvenWhenItIsTheDefault()
        {
            Assert.That(HTTPCookie.Parse("a=b; Path=/x; Path=y").Path, Is.Null);
        }

        /// <summary>
        /// Sections 5.6.5 and 5.6.6: any case, and a value is ignored.
        /// </summary>
        [Test]
        public void SecureAndHttpOnly_InAnyCase_WithAnyValue()
        {

            var cookie = HTTPCookie.Parse("a=b; SECURE=no; hTTPoNLY=false");

            Assert.Multiple(() => {
                Assert.That(cookie.Secure,       Is.True);
                Assert.That(cookie.HTTPOnly,     Is.True);
                Assert.That(cookie.ToString(),   Is.EqualTo("a=b; Secure; HttpOnly"));
            });

        }

        /// <summary>
        /// Section 5.6.7: the three keywords in any case, and "Default" for any
        /// other value - which is not written back, because it is no keyword.
        /// </summary>
        [TestCase("strict",  "Strict")]
        [TestCase("LAX",     "Lax")]
        [TestCase("None",    "None")]
        [TestCase("bogus",   "Default")]
        [TestCase("",        "Default")]
        public void SameSite_IsAKeyword(String Text, String Expected)
        {

            var cookie = HTTPCookie.Parse("a=b; Secure; SameSite=" + Text);

            Assert.Multiple(() => {
                Assert.That(cookie.SameSite,     Is.EqualTo(Expected));
                Assert.That(cookie.ToString(),   Is.EqualTo(Expected == "Default" ? "a=b; Secure" : "a=b; Secure; SameSite=" + Expected));
            });

        }

        [Test]
        public void WithoutSameSite_ItIsNull()
        {
            Assert.That(HTTPCookie.Parse("a=b").SameSite, Is.Null);
        }

        /// <summary>
        /// Attributes this model does not know are kept, in their order, and
        /// written back; an empty cookie-av is nothing.
        /// </summary>
        [Test]
        public void UnknownAttributes_AreKept()
        {

            var cookie = HTTPCookie.Parse("a=b;; Partitioned; Priority = High ;Path=/");

            Assert.Multiple(() => {
                Assert.That(cookie.Extensions.Select(e => $"{e.Key}|{e.Value ?? "(none)"}"),  Is.EqualTo(new[] { "Partitioned|(none)", "Priority|High" }));
                Assert.That(cookie.ToString(),  Is.EqualTo("a=b; Path=/; Partitioned; Priority=High"));
            });

        }

        /// <summary>
        /// Section 5.6, step 6: an attribute value of more than 1024 octets is
        /// ignored, and only that attribute.
        /// </summary>
        [Test]
        public void AnAttributeValueOver1024Octets_IsIgnored()
        {

            var fits      = "/" + new String('p', 1023);
            var tooLong   = "/" + new String('p', 1024);

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.Parse("a=b; Path=" + fits).Path,                         Is.EqualTo(fits));
                Assert.That(HTTPCookie.Parse("a=b; Path=/x; Path=" + tooLong).Path,             Is.EqualTo("/x"));
                Assert.That(HTTPCookie.Parse("a=b; Path=" + tooLong + "; HttpOnly").HTTPOnly,   Is.True);
            });

        }

        /// <summary>
        /// What ToString writes reads back as the same cookie.
        /// </summary>
        [Test]
        public void ACookie_RoundTrips()
        {

            var cookie = HTTPCookie.Parse("__Host-id=\"x:y=z\"; expires=Sun Nov  6 08:49:37 1994; max-age=60; path=/; secure; httponly; samesite=lax; Partitioned");
            var again  = HTTPCookie.Parse(cookie.ToString());

            Assert.Multiple(() => {
                Assert.That(again,             Is.EqualTo(cookie));
                Assert.That(cookie.ToString(), Is.EqualTo("__Host-id=\"x:y=z\"; Expires=Sun, 06 Nov 1994 08:49:37 GMT; Max-Age=60; Path=/; Secure; HttpOnly; SameSite=Lax; Partitioned"));
            });

        }

        #endregion

        #region Set-Cookie: the cookies every user agent ignores (Section 5.7, steps 19 to 21)

        /// <summary>
        /// Section 4.1.3's own examples, and the prefixes in another case,
        /// which 6265bis matches case-insensitively. Hermod used to read every
        /// one of them as a cookie.
        /// </summary>
        [TestCase("__Secure-SID=12345; Domain=site.example",                  false)]
        [TestCase("__Secure-SID=12345; Domain=site.example; Secure",          true)]
        [TestCase("__Host-SID=12345",                                         false)]
        [TestCase("__Host-SID=12345; Secure",                                 false)]
        [TestCase("__Host-SID=12345; Domain=site.example",                    false)]
        [TestCase("__Host-SID=12345; Domain=site.example; Path=/",            false)]
        [TestCase("__Host-SID=12345; Secure; Domain=site.example; Path=/",    false)]
        [TestCase("__Host-SID=12345; Secure; Path=/",                         true)]
        [TestCase("__Host-SID=12345; Secure; Path=/app",                      false)]
        [TestCase("__host-SID=12345; Path=/",                                 false)]
        [TestCase("__SECURE-SID=12345",                                       false)]
        [TestCase("_Host-SID=12345",                                          true)]
        [TestCase("a=b; SameSite=None",                                       false)]
        [TestCase("a=b; SameSite=None; Secure",                               true)]
        [TestCase("a=b; SameSite=none; SameSite=Lax",                         true)]
        public void ThePrefixesAndSameSiteNone_NeedTheirAttributes(String Text, Boolean IsACookie)
        {
            Assert.That(HTTPCookie.TryParse(Text, out _), Is.EqualTo(IsACookie));
        }

        #endregion

        #region The Cookie header field (Sections 4.2 and 5.8.3)

        /// <summary>
        /// A Cookie header field has no attributes: "Path=/" there is a cookie
        /// named Path, and a prefixed cookie is whatever the user agent sent.
        /// </summary>
        [Test]
        public void InACookieHeader_EveryPairIsACookie()
        {

            Assert.That(HTTPCookies.TryParseCookieHeader("Path=/; __Host-id=1; SameSite=None", out var cookies), Is.True);

            Assert.That(cookies!.Select(c => c.ToCookiePair()), Is.EqualTo(new[] { "Path=/", "__Host-id=1", "SameSite=None" }));

        }

        /// <summary>
        /// The user agent sorts the cookies of a name longest path first, then
        /// oldest first (Section 5.8.3): the first is the one set for this
        /// resource. The old parser kept the last.
        /// </summary>
        [Test]
        public void OfTwoCookiesOfAName_TheFirstIsKept()
        {

            Assert.That(HTTPCookies.TryParseCookieHeader("session=specific; theme=dark; session=general", out var cookies), Is.True);

            Assert.Multiple(() => {
                Assert.That(cookies!.Get(HTTPCookieName.Parse("session"))?.Value,  Is.EqualTo("specific"));
                Assert.That(cookies.ToString(),                                    Is.EqualTo("session=specific; theme=dark"));
            });

        }

        /// <summary>
        /// A pair without a name, a nameless cookie or one with a control
        /// character is skipped, and not the header field with it.
        /// </summary>
        [Test]
        public void ABadPair_IsSkipped()
        {

            Assert.That(HTTPCookies.TryParseCookieHeader("a=1; nameless; =x; b\u0001=2;; c=3;", out var cookies), Is.True);

            Assert.Multiple(() => {
                Assert.That(cookies!.ToString(),                                             Is.EqualTo("a=1; c=3"));
                Assert.That(HTTPCookies.TryParseCookieHeader("nameless; =x",     out _),     Is.False);
                Assert.That(HTTPCookies.TryParseCookieHeader("a=1;b=2",          out var c2), Is.True);
                Assert.That(c2!.Count(),                                                     Is.EqualTo(2));
            });

        }

        /// <summary>
        /// A user agent may split its cookie-string across lines (Section
        /// 5.8.1, and RFC 9113, Section 8.2.3, for HTTP/2). Hermod read a
        /// request with two Cookie lines as one with no cookies - and wrote
        /// two Cookie lines itself whenever it sent two cookies.
        /// </summary>
        [Test]
        public void CookieLines_AreOneCookieString()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\n" +
                                             "Host: example.test\r\n" +
                                             "Cookie: a=1; b=2\r\n" +
                                             "Cookie: c=3\r\n" +
                                             "\r\n",
                                             out var request),
                        Is.True);

            Assert.That(request!.Cookies.ToString(), Is.EqualTo("a=1; b=2; c=3"));

        }

        /// <summary>
        /// Set-Cookie is one cookie per line. Hermod read a response with two
        /// Set-Cookie lines - what its own HTTP API sends at every sign-in - as
        /// one with no cookies.
        /// </summary>
        [Test]
        public void SetCookieLines_AreOneCookieEach()
        {

            var response = HTTPResponse.Parse("HTTP/1.1 200 OK\r\n" +
                                              "Set-Cookie: a=1; Expires=Wed, 21 Oct 2015 07:28:00 GMT; Path=/\r\n" +
                                              "Set-Cookie: not a cookie\r\n" +
                                              "Set-Cookie: b=2; HttpOnly\r\n" +
                                              "Content-Length: 0\r\n" +
                                              "\r\n");

            var cookies = response.GetHeaderField(HTTPResponseHeaderField.SetCookie);

            Assert.That(cookies?.Select(c => c.ToString()), Is.EqualTo(new[] { "a=1; Expires=Wed, 21 Oct 2015 07:28:00 GMT; Path=/", "b=2; HttpOnly" }));

        }

        /// <summary>
        /// What Hermod sends: one Cookie line of names and values, in their
        /// order and without attributes (Section 5.4). It used to send one
        /// line per cookie, each with its attributes.
        /// </summary>
        [Test]
        public void ARequest_HasOneCookieLine_WithoutAttributes()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: example.test\r\n\r\n", out var request), Is.True);

            var two        = new HTTPRequest.Builder(request!) {
                                 Cookie = HTTPCookies.Parse("session=abc; Path=/; HttpOnly", "theme=dark; Secure")
                             }.AsImmutable.EntirePDU;

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: example.test\r\nCookie: a=1\r\nCookie: b=2\r\n\r\n", out var received), Is.True);

            var forwarded  = new HTTPRequest.Builder(received!).AsImmutable.EntirePDU;

            Assert.Multiple(() => {
                Assert.That(two.Split("\r\n").Where(line => line.StartsWith("Cookie:")),        Is.EqualTo(new[] { "Cookie: session=abc; theme=dark" }));
                Assert.That(forwarded.Split("\r\n").Where(line => line.StartsWith("Cookie:")),  Is.EqualTo(new[] { "Cookie: a=1; b=2" }));
            });

        }

        #endregion

        #region On the wire

        /// <summary>
        /// Hermod's client and server, both ways: two cookies set, two cookies
        /// sent back.
        /// </summary>
        [Test]
        public async Task ClientAndServer_ExchangeTwoCookies()
        {

            await using var httpServer = new HTTPServer(TCPPort: IPPort.Zero, AutoStart: true);

            var httpAPI = new HTTPAPI(httpServer);

            httpAPI.AddHandler(HTTPPath.Root + "cookies",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                 new HTTPResponse.Builder(request) {
                                                     HTTPStatusCode  = HTTPStatusCode.OK,
                                                     ContentType     = HTTPContentType.Text.PLAIN,
                                                     Content         = request.Cookies.ToString().ToUTF8Bytes(),
                                                     SetCookie       = HTTPCookies.Parse("account=alice; Expires=Wed, 21 Oct 2037 07:28:00 GMT; Path=/",
                                                                                         "__Host-session=abc; Path=/; Secure; HttpOnly; SameSite=Strict")
                                                 }.AsImmutable));

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var response = await client.RunRequest(HTTPMethod.GET,
                                                   HTTPPath.Root + "cookies",
                                                   RequestBuilder: builder => builder.Cookie = HTTPCookies.ParseCookieHeader("a=1; b=2"),
                                                   RequestTimeout: TimeSpan.FromSeconds(10));

            var setCookies = response.GetHeaderField(HTTPResponseHeaderField.SetCookie);

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("a=1; b=2"));
                Assert.That(setCookies?.Select(c => c.Name.ToString()),  Is.EqualTo(new[] { "account", "__Host-session" }));
                Assert.That(setCookies?.Last().SameSite,  Is.EqualTo("Strict"));
            });

        }

        #endregion

        #region Create(...) - what a server may send (Section 4.1)

        [Test]
        public void Create_WritesTheCookieOfAServer()
        {

            var cookie = HTTPCookie.Create(HTTPCookieName.Parse("__Host-session"),
                                           "YWJj==",
                                           Expires:   new DateTimeOffset(2037, 10, 21, 9, 28, 0, TimeSpan.FromHours(2)),
                                           MaxAge:    3600,
                                           Path:      "/",
                                           Secure:    true,
                                           HTTPOnly:  true,
                                           SameSite:  "lax");

            Assert.That(cookie.ToString(), Is.EqualTo("__Host-session=YWJj==; Expires=Wed, 21 Oct 2037 07:28:00 GMT; Max-Age=3600; Path=/; Secure; HttpOnly; SameSite=Lax"));

        }

        [TestCase("a b",          "x",                       null,              null,     false, null)]
        [TestCase("a,b",          "x",                       null,              null,     false, null)]
        [TestCase("a",            "x y",                     null,              null,     false, null)]
        [TestCase("a",            "x,y",                     null,              null,     false, null)]
        [TestCase("a",            "x\\y",                    null,              null,     false, null)]
        [TestCase("a",            "x\"y",                    null,              null,     false, null)]
        [TestCase("a",            "\u00e4",                  null,              null,     false, null)]
        [TestCase("a",            "x",                       null,              "x",      false, null)]
        [TestCase("a",            "x",                       null,              "/a;b",   false, null)]
        [TestCase("a",            "x",                       "a.example;b",     null,     false, null)]
        [TestCase("a",            "x",                       "b\u00fccher.example", null, false, null)]
        [TestCase("a",            "x",                       null,              null,     false, "bogus")]
        [TestCase("a",            "x",                       null,              null,     false, "None")]
        [TestCase("__Secure-a",   "x",                       null,              null,     false, null)]
        [TestCase("__Host-a",     "x",                       null,              "/",      false, null)]
        [TestCase("__Host-a",     "x",                       "site.example",    "/",      true,  null)]
        [TestCase("__Host-a",     "x",                       null,              null,     true,  null)]
        public void Create_RefusesWhatAServerMustNotSend(String Name, String Value, String? Domain, String? Path, Boolean Secure, String? SameSite)
        {

            Assert.Throws<ArgumentException>(() => HTTPCookie.Create(HTTPCookieName.Parse(Name), Value, Domain: Domain, Path: Path, Secure: Secure, SameSite: SameSite));

        }

        [Test]
        public void Create_AcceptsAQuotedValue_AndRefusesTooLongACookie()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookie.Create(HTTPCookieName.Parse("a"), "\"abc\"").ToString(),        Is.EqualTo("a=\"abc\""));
                Assert.That(HTTPCookie.Create(HTTPCookieName.Parse("a"), new String('x', 4095)).Value,  Has.Length.EqualTo(4095));
                Assert.Throws<ArgumentException>(() => HTTPCookie.Create(HTTPCookieName.Parse("a"), new String('x', 4096)));
                Assert.That(HTTPCookie.Create(HTTPCookieName.Parse("a"), "x", Domain: ".Site.Example").Domain, Is.EqualTo("site.example"));
            });

        }

        #endregion

        #region The crumbs of Hermod's HTTP API

        /// <summary>
        /// Hermod's HTTP API keeps several values in one cookie, separated by
        /// ":" - its own convention, read as it always was, beside a Value that
        /// is now the value.
        /// </summary>
        [Test]
        public void TheCrumbs_AreReadAsBefore()
        {

            var cookie = HTTPCookie.Parse("GraphDefinedHTTPAPI=login=YWxpY2U=:username=QWxpY2U=:isAdminRO; Path=/");

            Assert.Multiple(() => {
                Assert.That(cookie.Value,                     Is.EqualTo("login=YWxpY2U=:username=QWxpY2U=:isAdminRO"));
                Assert.That(cookie["login"],                  Is.EqualTo("YWxpY2U="));
                Assert.That(cookie.Get("username"),           Is.EqualTo("QWxpY2U="));
                Assert.That(cookie.TryGet("isAdminRO", out _), Is.True);
                Assert.That(cookie.Select(c => c.Key),        Is.EqualTo(new[] { "login", "username", "isAdminRO" }));
            });

        }

        [Test]
        public void ACookieName_IsNotNullOrEmpty_WhenItHasAName()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPCookieName.Parse("a").IsNotNullOrEmpty,  Is.True);
                Assert.That(HTTPCookieName.Parse("a").IsNullOrEmpty,     Is.False);
            });

        }

        #endregion

    }

}
