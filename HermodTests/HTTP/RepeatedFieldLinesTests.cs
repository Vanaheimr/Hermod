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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A field whose definition makes it a list means the same on two lines as
    /// on one: "A recipient MAY combine multiple field lines within a field
    /// section that have the same field name into one field line, without
    /// changing the semantics of the message, by appending each subsequent
    /// field line value to the initial field line value in order, separated by
    /// a comma (",") and optional whitespace" (RFC 9110, Section 5.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured before the fix (H-52). The header section keeps a field that
    /// came on two lines as a String[], and the typed lookup parsed only a
    /// String. On a parsed response, and on a request parsed by any path but
    /// the server's own, every typed list field on two lines read as absent -
    /// Cache-Control, Allow, Link, Alt-Svc, WWW-Authenticate, Content-Encoding
    /// - and every one read by name as the text "System.String[]": Vary,
    /// Accept-Ranges, Proxy-Authenticate. A client that got "Content-Encoding:
    /// br" and "Content-Encoding: gzip" handed the caller the compressed
    /// octets as the representation.
    /// </para>
    /// <para>
    /// The server's own parse joined seventeen fields by name and answered 400
    /// to any other that came twice, lists of RFC 6455, 7240, 8288, 9218 and
    /// 9530 among them, and the two content fields of a request body.
    /// </para>
    /// <para>
    /// One list of the fields that are lists now serves both: the server joins
    /// them, and a typed or a by-name lookup joins them where the header section
    /// did not. A field that is not a list still may not come twice to the
    /// server, and reads as absent on two lines elsewhere, because no definition
    /// says what its two values would mean.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class RepeatedFieldLinesTests
    {

        #region (private static) Response(Name, First, Second)

        private static HTTPResponse Response(String Name, String First, String Second)

            => HTTPResponse.Parse("HTTP/1.1 200 OK\r\n"   +
                                  "Content-Length: 0\r\n" +
                                 $"{Name}: {First}\r\n"   +
                                 $"{Name}: {Second}\r\n"  +
                                  "\r\n");

        #endregion

        #region (private static) ServerParse(Name, First, Second, out Request, out ErrorResponse)

        /// <summary>
        /// Parse a request as the HTTP server does: its header without the empty
        /// line that ends it, and with the strict rules that apply to what a
        /// server receives.
        /// </summary>
        private static Boolean ServerParse(String                                 Name,
                                           String                                 First,
                                           String                                 Second,
                                           [NotNullWhen(true)]  out HTTPRequest?   Request,
                                           [NotNullWhen(false)] out HTTPResponse?  ErrorResponse)
        {

            var socket = IPSocket.LocalhostV4(IPPort.HTTPS);

            return HTTPRequest.TryParse(Illias.Timestamp.Now,
                                        new HTTPSource(socket),
                                        socket,
                                        socket,
                                        "GET / HTTP/1.1\r\n"       +
                                        "Host: example.test\r\n"   +
                                       $"{Name}: {First}\r\n"      +
                                       $"{Name}: {Second}",
                                        out Request,
                                        out ErrorResponse);

        }

        #endregion


        #region A_TypedListOnTwoLines_ReadsAsOneList()

        [Test]
        public void A_TypedListOnTwoLines_ReadsAsOneList()
        {

            Assert.Multiple(() => {

                Assert.That(Response("Cache-Control",                 "no-store",          "max-age=0").        CacheControl,
                            Is.EqualTo("no-store, max-age=0"));

                Assert.That(Response("Content-Encoding",              "br",                "gzip").             ContentEncoding,
                            Is.EqualTo(new[] { "br", "gzip" }));

                Assert.That(Response("Content-Language",              "en",                "de").               ContentLanguage,
                            Is.EqualTo(new[] { "en", "de" }));

                Assert.That(Response("Allow",                         "GET",               "POST").             GetHeaderField(HTTPResponseHeaderField.Allow),
                            Is.EqualTo(new[] { HTTPMethod.GET, HTTPMethod.POST }));

                Assert.That(Response("Access-Control-Allow-Methods",  "GET",               "POST").             GetHeaderField(HTTPResponseHeaderField.AccessControlAllowMethods),
                            Is.EqualTo(new[] { HTTPMethod.GET, HTTPMethod.POST }));

                Assert.That(Response("Link",                          "</a>; rel=next",    "</b>; rel=prev").   GetHeaderField(HTTPResponseHeaderField.Link)?.Count(),
                            Is.EqualTo(2));

                Assert.That(Response("Alt-Svc",                       "h2=\":8443\"",      "h3=\":8443\"").     GetHeaderField(HTTPResponseHeaderField.AltSvc)?.ToString(),
                            Does.Contain("h2=\":8443\"").And.Contain("h3=\":8443\""));

                Assert.That(Response("WWW-Authenticate",              "Basic realm=\"a\"", "Bearer realm=\"b\"").GetHeaderField(HTTPResponseHeaderField.WWWAuthenticate),
                            Is.Not.Null);

                Assert.That(Response("Upgrade",                       "websocket",         "h2c").              Upgrade,
                            Is.EqualTo("websocket, h2c"));

                Assert.That(Response("Via",                           "1.1 a",             "1.1 b").            Via,
                            Is.EqualTo("1.1 a, 1.1 b"));

            });

        }

        #endregion

        #region A_ListReadByName_IsNeverTheNameOfAnArray()

        /// <summary>
        /// Read by name, a list on two lines was "System.String[]": the name of
        /// the type that held it, and not a word of what it said.
        /// </summary>
        [Test]
        public void A_ListReadByName_IsNeverTheNameOfAnArray()
        {

            Assert.Multiple(() => {

                Assert.That(Response("Vary",               "Accept",           "Accept-Encoding").Vary,
                            Is.EqualTo("Accept, Accept-Encoding"));

                Assert.That(Response("Accept-Ranges",      "bytes",            "none").           AcceptRanges,
                            Is.EqualTo("bytes, none"));

                Assert.That(Response("Proxy-Authenticate", "Basic realm=\"a\"", "Bearer").         GetHeaderField(HTTPResponseHeaderField.ProxyAuthenticate),
                            Is.EqualTo("Basic realm=\"a\", Bearer"));

                Assert.That(Response("Cache-Control",      "no-store",         "max-age=0").      GetHeaderField("cache-control"),
                            Is.EqualTo("no-store, max-age=0"),
                            "by a name in other letters, too");

            });

        }

        #endregion

        #region A_FieldThatIsNoList_HasNoValueOnTwoLines()

        /// <summary>
        /// Two Content-Type lines are not one Content-Type of "text/plain,
        /// text/html": the field is not a list, and no value is the only
        /// honest reading - the one the typed lookup always gave.
        /// </summary>
        [Test]
        public void A_FieldThatIsNoList_HasNoValueOnTwoLines()
        {

            Assert.Multiple(() => {

                Assert.That(Response("Content-Type", "text/plain", "text/html").ContentType,                     Is.Null);
                Assert.That(Response("Content-Type", "text/plain", "text/html").GetHeaderField("Content-Type"),  Is.Null);
                Assert.That(Response("ETag",         "\"a\"",      "\"b\"").    GetHeaderField(HTTPResponseHeaderField.ETag), Is.Null);
                Assert.That(Response("X-Unknown",    "a",          "b").        GetHeaderField("X-Unknown"),     Is.Null);

                Assert.That(Response("ETag",         "\"a\"",      "\"b\"").    TryGetHeaderField("ETag", out var lines) && lines is String[] { Length: 2 },
                            Is.True,
                            "the lines as they came are still there to be read");

            });

        }

        #endregion

        #region A_RequestParsedOutsideTheServer_JoinsItsListsToo()

        /// <summary>
        /// HTTPRequest.TryParse(Text) is the parse of tests and of the WebSocket
        /// server on a port of its own, and keeps repeated lines as they came.
        /// </summary>
        [Test]
        public void A_RequestParsedOutsideTheServer_JoinsItsListsToo()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\n"                 +
                                             "Host: example.test\r\n"             +
                                             "Accept: text/plain\r\n"             +
                                             "Accept: application/json\r\n"       +
                                             "If-None-Match: \"a\"\r\n"           +
                                             "If-None-Match: \"b\"\r\n"           +
                                             "Sec-WebSocket-Protocol: one\r\n"    +
                                             "Sec-WebSocket-Protocol: two\r\n"    +
                                             "\r\n",
                                             out var request),
                        Is.True);

            Assert.Multiple(() => {
                Assert.That(request!.Accept.Select(accept => accept.ContentType.MediaType),
                                                                                  Is.EqualTo(new[] { "text/plain", "application/json" }));
                Assert.That(request.IfNoneMatch,                                  Is.EqualTo("\"a\", \"b\""));
                Assert.That(request.SecWebSocketProtocol,                         Is.EqualTo(new[] { "one", "two" }));
                Assert.That(request.GetHeaderField("Sec-WebSocket-Protocol"),     Is.EqualTo("one, two"));
            });

        }

        #endregion

        #region A_BuilderMadeFromAParsedMessage_JoinsItsListsToo()

        [Test]
        public void A_BuilderMadeFromAParsedMessage_JoinsItsListsToo()
        {

            Assert.That(HTTPRequest.TryParse("POST / HTTP/1.1\r\n"                +
                                             "Host: example.test\r\n"             +
                                             "Cache-Control: no-cache\r\n"        +
                                             "Cache-Control: max-age=0\r\n"       +
                                             "Content-Encoding: br\r\n"           +
                                             "Content-Encoding: gzip\r\n"         +
                                             "If-None-Match: \"a\"\r\n"           +
                                             "If-None-Match: \"b\"\r\n"           +
                                             "\r\n",
                                             out var request),
                        Is.True);

            var builder = new HTTPRequest.Builder(request!);

            Assert.Multiple(() => {
                Assert.That(builder.CacheControl,     Is.EqualTo("no-cache, max-age=0"));
                Assert.That(builder.ContentEncoding,  Is.EqualTo(new[] { "br", "gzip" }));
                Assert.That(builder.IfNoneMatch,      Is.EqualTo("\"a\", \"b\""));
            });

        }

        #endregion

        #region A_ResponseBuilderGivenTheLinesOfAList_ReadsThemAsOne()

        /// <summary>
        /// The lines of a field as they came, handed on to a response - what a
        /// proxy does with a field it does not interpret.
        /// </summary>
        [Test]
        public void A_ResponseBuilderGivenTheLinesOfAList_ReadsThemAsOne()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: example.test\r\n\r\n", out var request), Is.True);

            var builder = new HTTPResponse.Builder(request!).
                              Set("Allow", new[] { "GET", "POST" });

            Assert.That(builder.Allow, Is.EqualTo(new[] { HTTPMethod.GET, HTTPMethod.POST }));

        }

        #endregion


        #region TheServer_JoinsAListOnTwoLines(Name, First, Second)

        /// <summary>
        /// Each of these was answered 400 by the server before, as a field that
        /// may not come twice.
        /// </summary>
        [TestCase("Sec-WebSocket-Extensions",  "x-hermod-unknown",  "permessage-deflate")]
        [TestCase("Sec-WebSocket-Protocol",    "chat",              "superchat")]
        [TestCase("Content-Encoding",          "br",                "gzip")]
        [TestCase("Content-Language",          "en",                "de")]
        [TestCase("Want-Content-Digest",       "sha-256=1",         "sha-512=3")]
        [TestCase("Want-Repr-Digest",          "sha-256=1",         "sha-512=3")]
        [TestCase("Prefer",                    "return=minimal",    "wait=10")]
        [TestCase("Priority",                  "u=1",               "i")]
        [TestCase("Link",                      "</a>; rel=next",    "</b>; rel=prev")]
        [TestCase("Pragma",                    "no-cache",          "x-hermod")]
        [TestCase("Access-Control-Request-Headers", "x-a",          "x-b")]
        public void TheServer_JoinsAListOnTwoLines(String Name, String First, String Second)
        {

            Assert.That(ServerParse(Name, First, Second, out var request, out var errorResponse),
                        Is.True,
                        $"{Name} on two lines was answered {errorResponse?.HTTPStatusCode}");

            Assert.That(request.GetHeaderField(Name), Is.EqualTo($"{First}, {Second}"));

        }

        #endregion

        #region TheServer_StillJoinsWhatItJoinedBefore(Name)

        /// <summary>
        /// The seventeen fields the server joined by a list of its own before.
        /// </summary>
        [TestCase("Accept")]
        [TestCase("Accept-Charset")]
        [TestCase("Accept-Encoding")]
        [TestCase("Accept-Language")]
        [TestCase("Cache-Control")]
        [TestCase("Client-Cert-Chain")]
        [TestCase("Connection")]
        [TestCase("Expect")]
        [TestCase("Forwarded")]
        [TestCase("If-Match")]
        [TestCase("If-None-Match")]
        [TestCase("TE")]
        [TestCase("Trailer")]
        [TestCase("Upgrade")]
        [TestCase("Via")]
        [TestCase("Warning")]
        [TestCase("X-Forwarded-For")]
        public void TheServer_StillJoinsWhatItJoinedBefore(String Name)
        {

            Assert.That(HTTPHeaderField.IsListBasedField(Name),  Is.True);

            Assert.That(ServerParse(Name, "a", "b", out var request, out var errorResponse),
                        Is.True,
                        $"{Name} on two lines was answered {errorResponse?.HTTPStatusCode}");

            Assert.That(request.GetHeaderField(Name), Is.EqualTo("a, b"));

        }

        #endregion

        #region TheServer_StillRefusesAFieldThatIsNoList(Name, Value)

        /// <summary>
        /// A field that is not a list may still not come twice to the server.
        /// </summary>
        [TestCase("Content-Type",           "text/plain")]
        [TestCase("If-Modified-Since",      "Sun, 06 Nov 1994 08:49:37 GMT")]
        [TestCase("Range",                  "bytes=0-1")]
        [TestCase("Sec-WebSocket-Key",      "dGhlIHNhbXBsZSBub25jZQ==")]
        [TestCase("Client-Cert",            ":AAAA:")]
        [TestCase("X-Unknown",              "a")]
        public void TheServer_StillRefusesAFieldThatIsNoList(String Name, String Value)
        {

            Assert.Multiple(() => {
                Assert.That(HTTPHeaderField.IsListBasedField(Name),                       Is.False);
                Assert.That(ServerParse(Name, Value, Value, out _, out var errorResponse), Is.False);
                Assert.That(errorResponse?.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.BadRequest));
            });

        }

        #endregion

        #region NeitherCookieField_IsJoinedByCommas()

        /// <summary>
        /// Cookie joins by "; " and Set-Cookie not at all, so neither may be in
        /// the list of fields that join by commas: a comma inside an Expires
        /// date would split one Set-Cookie into two.
        /// </summary>
        [Test]
        public void NeitherCookieField_IsJoinedByCommas()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPHeaderField.IsListBasedField("Cookie"),                     Is.False);
                Assert.That(HTTPHeaderField.IsListBasedField("Set-Cookie"),                 Is.False);
                Assert.That(HTTPHeaderField.IsListBasedField("Strict-Transport-Security"),  Is.False,
                            "RFC 6797, Section 8.1: only the first of several is processed");
            });

        }

        #endregion

    }

}
