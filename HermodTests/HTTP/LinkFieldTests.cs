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

using System.Net;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The Link header field (RFC 8288, Section 3), and the RFC 8187 encoding
    /// its title* parameter uses.
    /// </summary>
    /// <remarks>
    /// Until 2026-10-29 Hermod had no Link field: a handler wrote the text
    /// itself, and a client read the raw header. H-14 in HTTP1ConformanceTests.
    /// </remarks>
    [TestFixture]
    public class LinkFieldTests
    {

        #region (private static) Single(Text)

        private static HTTPLink Single(String Text)
        {

            Assert.That(HTTPLinks.TryParse(Text, out var links), Is.True, Text);
            Assert.That(links!.Count, Is.EqualTo(1), Text);

            return links.First();

        }

        #endregion


        #region RFC 8288, Section 3.5

        [Test]
        public void ThePreviousChapter()
        {

            var link = Single("<http://example.com/TheBook/chapter2>; rel=\"previous\"; title=\"previous chapter\"");

            Assert.Multiple(() => {
                Assert.That(link.Target,     Is.EqualTo("http://example.com/TheBook/chapter2"));
                Assert.That(link.Relations,  Is.EqualTo(new[] { "previous" }));
                Assert.That(link.Title,      Is.EqualTo("previous chapter"));
                Assert.That(link.Anchor,     Is.Null);
            });

        }

        [Test]
        public void AnExtensionRelationType()
        {

            var link = Single("</>; rel=\"http://example.net/foo\"");

            Assert.That(link.Target,    Is.EqualTo("/"));
            Assert.That(link.Relations, Is.EqualTo(new[] { "http://example.net/foo" }));

        }

        [Test]
        public void AnAnchorIsAnotherContext()
        {

            var link = Single("</terms>; rel=\"copyright\"; anchor=\"#foo\"");

            Assert.That(link.Relations, Is.EqualTo(new[] { "copyright" }));
            Assert.That(link.Anchor,    Is.EqualTo("#foo"));

        }

        [Test]
        public void TwoLinksWithTitlesInGerman()
        {

            Assert.That(HTTPLinks.TryParse("</TheBook/chapter2>; rel=\"previous\"; title*=UTF-8'de'letztes%20Kapitel, " +
                                           "</TheBook/chapter4>; rel=\"next\"; title*=UTF-8'de'n%c3%a4chstes%20Kapitel",
                                           out var links), Is.True);

            var (previous, next) = (links!.First(), links!.Last());

            Assert.Multiple(() => {
                Assert.That(links!.Count,            Is.EqualTo(2));
                Assert.That(previous.Target,         Is.EqualTo("/TheBook/chapter2"));
                Assert.That(previous.Title,          Is.EqualTo("letztes Kapitel"));
                Assert.That(previous.TitleLanguage,  Is.EqualTo("de"));
                Assert.That(next.Target,             Is.EqualTo("/TheBook/chapter4"));
                Assert.That(next.Title,              Is.EqualTo("nächstes Kapitel"));
                Assert.That(next.TitleLanguage,      Is.EqualTo("de"));
            });

        }

        [Test]
        public void OneTargetTwoRelationTypes()
        {

            var link = Single("<http://example.org/>; rel=\"start http://example.net/relation/other\"");

            Assert.That(link.Relations, Is.EqualTo(new[] { "start", "http://example.net/relation/other" }));
            Assert.That(link.HasRelation("start"), Is.True);

        }

        [Test]
        public void CommasSeparateLinkValues()
        {

            Assert.That(HTTPLinks.TryParse("<https://example.org/>; rel=\"start\", <https://example.org/index>; rel=\"index\"", out var links), Is.True);

            Assert.That(links!.Select(link => link.Target),        Is.EqualTo(new[] { "https://example.org/", "https://example.org/index" }));
            Assert.That(links!.Select(link => link.Relations[0]),  Is.EqualTo(new[] { "start", "index" }));

        }

        #endregion

        #region Parsing rules

        /// <summary>
        /// Section 3: "recipients MUST be able to parse both forms".
        /// </summary>
        [Test]
        public void ATokenAndAQuotedStringAreTheSameValue()
        {

            var token   = Single("</a>; rel=next; title=x; type=text/html");
            var quoted  = Single("</a>; rel=\"next\"; title=\"x\"; type=\"text/html\"");

            Assert.That(token.ToString(), Is.EqualTo(quoted.ToString()));

        }

        /// <summary>
        /// Sections 3.2–3.4.1: "occurrences after the first MUST be ignored by
        /// parsers", for rel, media, title, title* and type; and only one
        /// context, so only one anchor.
        /// </summary>
        [Test]
        public void OnlyTheFirstOfASingleParameterCounts()
        {

            var link = Single("</a>; rel=next; rel=prev; anchor=\"#x\"; anchor=\"#y\"; media=screen; media=print; " +
                              "title=one; title=two; type=\"text/html\"; type=\"text/plain\"");

            Assert.Multiple(() => {
                Assert.That(link.Relations,  Is.EqualTo(new[] { "next" }));
                Assert.That(link.Anchor,     Is.EqualTo("#x"));
                Assert.That(link.Media,      Is.EqualTo("screen"));
                Assert.That(link.Title,      Is.EqualTo("one"));
                Assert.That(link.Type,       Is.EqualTo("text/html"));
            });

        }

        /// <summary>
        /// Section 3.4.1: several hreflang say the target is there in several languages.
        /// </summary>
        [Test]
        public void HrefLangMayRepeat()
        {
            Assert.That(Single("</a>; rel=alternate; hreflang=de; hreflang=en-GB").HrefLang, Is.EqualTo(new[] { "de", "en-GB" }));
        }

        /// <summary>
        /// Section 3.4.1: "applications SHOULD use the title* link-param's
        /// value", whichever comes first.
        /// </summary>
        [TestCase("</a>; rel=next; title=\"plain\"; title*=UTF-8''st%C3%A4r")]
        [TestCase("</a>; rel=next; title*=UTF-8''st%C3%A4r; title=\"plain\"")]
        public void TitleStarWinsOverTitle(String Text)
        {
            Assert.That(Single(Text).Title, Is.EqualTo("stär"));
        }

        /// <summary>
        /// Section 3.4.2 lets a recipient fall back to the plain value when the
        /// starred one does not decode.
        /// </summary>
        [TestCase("</a>; rel=next; title=\"plain\"; title*=UTF-8''%ZZ")]
        [TestCase("</a>; rel=next; title=\"plain\"; title*=KOI8-R''x")]
        [TestCase("</a>; rel=next; title=\"plain\"; title*=\"UTF-8''x y\"")]
        public void ATitleStarThatDoesNotDecodeLeavesTheTitle(String Text)
        {

            var link = Single(Text);

            Assert.That(link.Title,          Is.EqualTo("plain"));
            Assert.That(link.TitleLanguage,  Is.Null);

        }

        /// <summary>
        /// Section 3.3: rel "MUST be present". A link-value without one, or with
        /// an empty one, is no link; the ones beside it still are.
        /// </summary>
        [Test]
        public void ALinkValueWithoutRelIsNoLink()
        {

            Assert.That(HTTPLinks.TryParse("</a>; title=x",  out _), Is.False);
            Assert.That(HTTPLinks.TryParse("</a>; rel=\"\"",  out _), Is.False);
            Assert.That(HTTPLinks.TryParse("</a>; rel",      out _), Is.False);

            Assert.That(Single("</a>; title=x, </b>; rel=next").Target, Is.EqualTo("/b"));

        }

        /// <summary>
        /// Sections 2.1.1 and 2.1.2: relation types compare case-insensitively.
        /// Appendix B keeps them in lower case.
        /// </summary>
        [Test]
        public void RelationTypesAreKeptInLowerCase()
        {

            var link = Single("</a>; rel=\"Next HTTP://Example.NET/Rel\"");

            Assert.That(link.Relations,            Is.EqualTo(new[] { "next", "http://example.net/rel" }));
            Assert.That(link.HasRelation("NEXT"),  Is.True);

        }

        /// <summary>
        /// Appendix B.2, step 2.2: parsing ends at the first thing that is not a
        /// link-value, and keeps the links before it.
        /// </summary>
        [Test]
        public void ParsingStopsAtWhatIsNotALinkValue()
        {

            Assert.That(Single("</a>; rel=next, garbage, </b>; rel=prev").Target, Is.EqualTo("/a"));
            Assert.That(HTTPLinks.TryParse("</a; rel=next",  out _), Is.False);
            Assert.That(HTTPLinks.TryParse("rel=next",       out _), Is.False);
            Assert.That(HTTPLinks.TryParse("",               out _), Is.False);

        }

        /// <summary>
        /// Section 3.4.2: every other link-param is a target attribute. A
        /// "name*" that decodes replaces "name", where "name" stood.
        /// </summary>
        [Test]
        public void ExtensionAttributesAreKeptInOrder()
        {

            var link = Single("</a>; rel=next; foo=plain; bar=\"q x\"; rev=made; foo*=UTF-8''%C3%BC; baz*=UTF-8''%ZZ; baz=kept");

            Assert.That(link.Attributes.Select(attribute => $"{attribute.Key}={attribute.Value}"),
                        Is.EqualTo(new[] { "foo=ü", "bar=q x", "rev=made", "baz=kept" }));

        }

        [Test]
        public void QuotedStringsUnescape()
        {
            Assert.That(Single("</a>; rel=next; title=\"a \\\"b\\\" \\\\ c\"").Title, Is.EqualTo("a \"b\" \\ c"));
        }

        /// <summary>
        /// A comma or a semicolon inside a quoted-string delimits nothing.
        /// </summary>
        [Test]
        public void DelimitersInsideAQuotedStringAreText()
        {

            Assert.That(HTTPLinks.TryParse("</a>; rel=\"next\"; title=\"one, two; three\", </b>; rel=prev", out var links), Is.True);

            Assert.That(links!.Count,         Is.EqualTo(2));
            Assert.That(links!.First().Title, Is.EqualTo("one, two; three"));

        }

        /// <summary>
        /// OWS between the parts, and BWS around "=".
        /// </summary>
        [Test]
        public void WhitespaceBetweenThePartsIsAllowed()
        {

            Assert.That(HTTPLinks.TryParse("  </a> ;rel = next ;\ttitle = \"x\"  ,  </b>;rel=prev  ", out var links), Is.True);

            Assert.That(links!.Select(link => link.Relations[0]), Is.EqualTo(new[] { "next", "prev" }));
            Assert.That(links!.First().Title,                     Is.EqualTo("x"));

        }

        #endregion

        #region Serialisation

        [Test]
        public void EveryAttributeGoesOutInItsForm()
        {

            var link = new HTTPLink("/TheBook/chapter2",
                                    "previous",
                                    Anchor:      "#foo",
                                    HrefLang:    [ "de", "en" ],
                                    Media:       "screen, print",
                                    Title:       "previous chapter",
                                    Type:        "text/html",
                                    Attributes:  [ new ("rev", "made"), new ("note", "two words"), new ("motto", "Grüße") ]);

            Assert.That(link.ToString(),
                        Is.EqualTo("</TheBook/chapter2>; rel=\"previous\"; anchor=\"#foo\"; hreflang=de; hreflang=en; " +
                                   "media=\"screen, print\"; title=\"previous chapter\"; type=\"text/html\"; " +
                                   "rev=made; note=\"two words\"; motto*=UTF-8''Gr%C3%BC%C3%9Fe"));

            Assert.That(Single(link.ToString()).ToString(), Is.EqualTo(link.ToString()));

        }

        /// <summary>
        /// A title that is not printable ASCII, or that has a language, can only
        /// go out as title* (RFC 8187), UTF-8 as Section 3.2.1 of that RFC
        /// requires of a producer.
        /// </summary>
        [Test]
        public void ATitleThatNeedsItGoesOutAsTitleStar()
        {

            var link = new HTTPLink("/TheBook/chapter4", "next", Title: "nächstes Kapitel", TitleLanguage: "de");

            Assert.That(link.ToString(), Is.EqualTo("</TheBook/chapter4>; rel=\"next\"; title*=UTF-8'de'n%C3%A4chstes%20Kapitel"));
            Assert.That(new HTTPLink("/a", "next", Title: "\"quoted\" \\ back").ToString(),
                        Is.EqualTo("</a>; rel=\"next\"; title=\"\\\"quoted\\\" \\\\ back\""));

            var parsed = Single(link.ToString());

            Assert.That(parsed.Title,          Is.EqualTo("nächstes Kapitel"));
            Assert.That(parsed.TitleLanguage,  Is.EqualTo("de"));

        }

        [Test]
        public void SeveralLinksAreSeparatedByCommas()
        {

            var links = new HTTPLinks(new HTTPLink("/items?page=3", "next"),
                                      new HTTPLink("/items?page=1", "prev first"));

            Assert.That(links.ToString(), Is.EqualTo("</items?page=3>; rel=\"next\", </items?page=1>; rel=\"prev first\""));
            Assert.That(links.WithRelation("FIRST").Single().Target, Is.EqualTo("/items?page=1"));

        }

        #endregion

        #region What a sender must get right

        [TestCase("/a b",            "next")]   // not a URI reference
        [TestCase("/a>b",            "next")]
        [TestCase("/ä",              "next")]   // an IRI, to be converted first (Section 3.1)
        [TestCase("/a",              "")]       // Section 3.3: rel MUST be present
        [TestCase("/a",              "1st")]    // neither LOALPHA first nor a URI
        [TestCase("/a",              "foo_bar")]
        [TestCase("/a",              "/relative")]
        public void ATargetOrRelationThatCannotBeSentIsRefused(String Target, String Relation)
        {
            Assert.That(() => new HTTPLink(Target, Relation), Throws.ArgumentException);
        }

        [Test]
        public void AttributesThatCannotBeSentAreRefused()
        {

            Assert.Multiple(() => {
                Assert.That(() => new HTTPLink("/a", "next", HrefLang:       [ "de_DE" ]),               Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Type:           "text"),                   Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", TitleLanguage:  "de"),                     Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Anchor:         "#a b"),                   Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Attributes:     [ new ("title", "x") ]),   Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Attributes:     [ new ("REL",   "x") ]),   Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Attributes:     [ new ("x*",    "y") ]),   Throws.ArgumentException);
                Assert.That(() => new HTTPLink("/a", "next", Attributes:     [ new ("a b",   "y") ]),   Throws.ArgumentException);
            });

        }

        #endregion

        #region RFC 8187

        /// <summary>
        /// RFC 8187, Section 3.2.3, and its UTF-8 and ISO-8859-1 support.
        /// </summary>
        [TestCase("utf-8'en'%C2%A3%20rates",                  "£ rates",        "en")]
        [TestCase("UTF-8''%c2%a3%20and%20%e2%82%ac%20rates",  "£ and € rates",  null)]
        [TestCase("iso-8859-1'en'%A3%20rates",                "£ rates",        "en")]
        [TestCase("UTF-8''",                                  "",               null)]
        public void AnExtendedValueDecodes(String Text, String Value, String? Language)
        {

            Assert.That(HTTPExtendedValue.TryDecode(Text, out var value, out var language), Is.True);
            Assert.That(value,    Is.EqualTo(Value));
            Assert.That(language, Is.EqualTo(Language));

        }

        [TestCase("KOI8-R''x")]            // a charset neither required nor encouraged
        [TestCase("''abc")]                // the charset is required
        [TestCase("UTF-8'abc")]            // one quote
        [TestCase("abc")]
        [TestCase("UTF-8''%G1")]           // not a percent-encoding
        [TestCase("UTF-8''%C")]
        [TestCase("UTF-8''%C3")]           // half a UTF-8 sequence
        [TestCase("UTF-8''a b")]           // a space is not an attr-char
        [TestCase("UTF-8''\"x\"")]         // quoted-string notation is not allowed
        [TestCase("UTF-8''ä")]             // nor a raw non-ASCII character
        [TestCase("UTF-8'de_DE'x")]        // not a language tag
        public void AMalformedExtendedValueIsRefused(String Text)
        {
            Assert.That(HTTPExtendedValue.TryDecode(Text, out _, out _), Is.False);
        }

        [Test]
        public void EncodingKeepsAttrCharsAndEncodesTheRest()
        {

            Assert.That(HTTPExtendedValue.Encode("a-b.c_d~e!f#g$h&i+j^k`l|m"), Is.EqualTo("UTF-8''a-b.c_d~e!f#g$h&i+j^k`l|m"));
            Assert.That(HTTPExtendedValue.Encode("a b*'%ü", "de"),              Is.EqualTo("UTF-8'de'a%20b%2A%27%25%C3%BC"));
            Assert.That(() => HTTPExtendedValue.Encode("x", "de_DE"),           Throws.ArgumentException);

        }

        #endregion


        #region End to end

        /// <summary>
        /// A handler sets HTTPResponse.Builder.Link, and the client reads
        /// HTTPResponse.Link.
        /// </summary>
        [Test]
        public async Task AHandlersLinksReachTheClient()
        {

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                 Content         = "page 2".ToUTF8Bytes(),
                                                 Link            = new HTTPLinks(
                                                                       new HTTPLink("/items?page=3", "next"),
                                                                       new HTTPLink("/items?page=1", "prev", Title: "erste Seite", TitleLanguage: "de")
                                                                   )
                                             }.AsImmutable
                                         )
            );

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));
            var links    = response.Link;

            Assert.Multiple(() => {
                Assert.That(response.RawHTTPHeader,
                            Does.Contain("Link: </items?page=3>; rel=\"next\", </items?page=1>; rel=\"prev\"; title*=UTF-8'de'erste%20Seite"));
                Assert.That(links?.Count,                                    Is.EqualTo(2));
                Assert.That(links?.WithRelation("next").Single().Target,     Is.EqualTo("/items?page=3"));
                Assert.That(links?.WithRelation("prev").Single().Title,      Is.EqualTo("erste Seite"));
            });

        }

        /// <summary>
        /// Section 3.5: two field lines are the same as one with a comma.
        /// </summary>
        [Test]
        public async Task TwoFieldLinesAreOneValue()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port   = ((IPEndPoint) listener.LocalEndpoint).Port;

            var server = Task.Run(async () => {

                using var accepted = await listener.AcceptTcpClientAsync();
                var stream         = accepted.GetStream();
                var request        = new StringBuilder();
                var buffer         = new Byte[4096];

                while (!request.ToString().Contains("\r\n\r\n"))
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0) break;
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                          "HTTP/1.1 200 OK\r\n" +
                          "Content-Length: 0\r\n" +
                          "Link: <https://example.org/>; rel=\"start\"\r\n" +
                          "Link: <https://example.org/index>; rel=\"index\"\r\n" +
                          "Connection: close\r\n" +
                          "\r\n"));

            });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{port}"));

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));

            await server;

            Assert.That(response.Link?.Select(link => link.Relations[0]), Is.EqualTo(new[] { "start", "index" }));

        }

        #endregion

    }

}
