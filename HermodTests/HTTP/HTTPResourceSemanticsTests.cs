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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9110's core mechanics on HTTP/1.x, through HTTPResourceHandlers: the
    /// same HTTPResourceSemantics the HTTP/2 stack has used since it existed, now
    /// in front of an HTTPAPI (H-37).
    ///
    /// Before H-37 a handler on HTTP/1.x implemented its own 304, 206 and 416 —
    /// the demo host's ServeResource did, and HTTPStandardHandlers did not — and
    /// the rules HTTP/2 applied in one place were reimplemented, or not, per
    /// handler.
    /// </summary>
    /// <remarks>
    /// Raw sockets: what is under test is what goes over the wire — which
    /// fields, which spelling, a 304 and a HEAD that carry no body and leave the
    /// connection usable — and a client that already knows how to read them
    /// would hide the difference.
    /// </remarks>
    [TestFixture]
    public class HTTPResourceSemanticsTests
    {

        #region Data

        private static readonly Byte[]          Text          = "Hello from RFC 9110, served by the library.\n".ToUTF8Bytes();
        private static readonly DateTimeOffset  LastModified  = new (2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        private static readonly String          ETag          = HTTPValidators.ComputeETag(Text);
        private const           String          ProfiledType  = "application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"";

        private HTTPServer? httpServer;

        private String?     lastPath;

        #endregion

        #region Init/Shutdown

        [OneTimeSetUp]
        public void Init()
        {

            httpServer = new HTTPServer(
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            var api = httpServer.AddHTTPAPI();

            // One representation, its ETag derived from its octets.
            api.AddResource(
                HTTPPath.Root + "resource.txt",
                (path, headers, ct) => Task.FromResult<HTTPResource?>(
                    new HTTPResource {
                        Body          = Text,
                        ContentType   = "text/plain; charset=utf-8",
                        LastModified  = LastModified
                    }
                )
            );

            // Two languages of one greeting: negotiation, Vary, 406.
            api.AddResource(
                HTTPPath.Root + "greeting",
                (path, headers, ct) => Task.FromResult<IReadOnlyList<HTTPResource>>([
                    new HTTPResource { Body = "Hello!\n".ToUTF8Bytes(), ContentType = "text/plain; charset=utf-8", Language = "en" },
                    new HTTPResource { Body = "Hallo!\n".ToUTF8Bytes(), ContentType = "text/plain; charset=utf-8", Language = "de" }
                ])
            );

            // A media type with a parameter of its own (RFC 6906's profile).
            api.AddResource(
                HTTPPath.Root + "profiled",
                (path, headers, ct) => Task.FromResult<HTTPResource?>(
                    new HTTPResource {
                        Body         = "{}".ToUTF8Bytes(),
                        ContentType  = ProfiledType
                    }
                )
            );

            // A handler that has nothing: 404 from the layer.
            api.AddResource(
                HTTPPath.Root + "missing",
                (path, headers, ct) => Task.FromResult<HTTPResource?>(null)
            );

            // What the resource handler is given as its path.
            api.AddResource(
                HTTPPath.Root + "path",
                (path, headers, ct) => {
                    lastPath = path;
                    return Task.FromResult<HTTPResource?>(
                               new HTTPResource { Body = path.ToUTF8Bytes(), ContentType = "text/plain" }
                           );
                }
            );

            // QUERY (RFC 10008): the result runs through the GET pipeline.
            api.AddResource(
                HTTPPath.Root + "search",
                (path, headers, ct) => Task.FromResult<HTTPResource?>(
                    new HTTPResource { Body = "[]".ToUTF8Bytes(), ContentType = "application/json" }
                ),
                QueryHandler: (path, headers, content, contentType, ct) => Task.FromResult<HTTPResource?>(
                    new HTTPResource {
                        Body             = Encoding.UTF8.GetBytes($"[\"{Encoding.UTF8.GetString(content ?? [])}\"]"),
                        ContentType      = "application/json",
                        ContentLocation  = "/search/results/1"
                    }
                )
            );

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.DisposeAsync();
        }

        #endregion


        #region (private) Exchange(Request, ResponseHasNoBody = false)

        /// <summary>
        /// One request on a connection of its own, one framed response.
        /// </summary>
        private async Task<HTTPRawSocketResponse> Exchange(String   Request,
                                                           Boolean  ResponseHasNoBody   = false)
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(
                                               System.Net.IPAddress.Loopback,
                                               httpServer!.TCPPort
                                           );

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync(Request, giveUp.Token);

            return await client.ReadResponseAsync(ResponseHasNoBody, giveUp.Token);

        }

        private static String Get(String Path, params String[] Fields)

            => $"GET {Path} HTTP/1.1\r\nHost: 127.0.0.1\r\n{String.Concat(Fields.Select(field => field + "\r\n"))}\r\n";

        private static String Field(HTTPRawSocketResponse Response, String Name)

            => Response.Headers.TryGetValue(Name, out var values) && values.Count == 1
                   ? values[0]
                   : throw new AssertionException($"Expected exactly one '{Name}' field, got {(values is null ? "none" : String.Join(" | ", values))}:\r\n{Response.StatusLine}");

        #endregion


        #region A_GET_carries_the_validators_the_layer_derives()

        [Test]
        public async Task A_GET_carries_the_validators_the_layer_derives()
        {

            var response = await Exchange(Get("/resource.txt"));

            Assert.That(response.StatusCode,                    Is.EqualTo(200));
            Assert.That(response.Body,                          Is.EqualTo(Text));
            Assert.That(Field(response, "ETag"),                Is.EqualTo(ETag));
            Assert.That(Field(response, "Last-Modified"),       Is.EqualTo("Wed, 01 Jul 2026 12:00:00 GMT"));
            Assert.That(Field(response, "Accept-Ranges"),       Is.EqualTo("bytes"));
            Assert.That(Field(response, "Content-Type"),        Is.EqualTo("text/plain; charset=utf-8"));
            Assert.That(Field(response, "Content-Length"),      Is.EqualTo(Text.Length.ToString()));

            // The spelling HTTP/1.x readers expect, not HTTP/2's lower case.
            Assert.That(response.Headers.Keys,                  Does.Contain("ETag"));

        }

        #endregion

        #region A_HEAD_states_the_length_of_the_GET_and_sends_nothing()

        /// <summary>
        /// RFC 9110, Section 9.3.2: HEAD is GET without the content, the
        /// Content-Length the GET would have had included. The layer answers HEAD
        /// with no content and that length, and the length has to survive the
        /// HTTP/1.x response it is turned into. The second request on the same
        /// connection is what proves nothing followed the header section.
        /// </summary>
        [Test]
        public async Task A_HEAD_states_the_length_of_the_GET_and_sends_nothing()
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort);
            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync("HEAD /resource.txt HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", giveUp.Token);
            var head = await client.ReadResponseAsync(ResponseHasNoBody: true, giveUp.Token);

            await client.SendAsync(Get("/resource.txt"), giveUp.Token);
            var get  = await client.ReadResponseAsync(CancellationToken: giveUp.Token);

            Assert.That(head.StatusCode,                Is.EqualTo(200));
            Assert.That(Field(head, "Content-Length"),  Is.EqualTo(Text.Length.ToString()));
            Assert.That(Field(head, "ETag"),            Is.EqualTo(ETag));

            Assert.That(get.StatusCode,                 Is.EqualTo(200), "the HEAD left something on the connection");
            Assert.That(get.Body,                       Is.EqualTo(Text));

        }

        #endregion


        #region Conditional requests (RFC 9110, Section 13)

        [TestCase("If-None-Match: \"other\", {0}",                        304)]
        [TestCase("If-None-Match: *",                                     304)]
        [TestCase("If-None-Match: W/{0}",                                 304)]   // weak comparison (Section 13.1.2)
        [TestCase("If-None-Match: \"other\"",                             200)]
        [TestCase("If-Modified-Since: Wed, 01 Jul 2026 12:00:00 GMT",     304)]
        [TestCase("If-Modified-Since: Tue, 30 Jun 2026 12:00:00 GMT",     200)]
        [TestCase("If-Match: {0}",                                        200)]
        [TestCase("If-Match: *",                                          200)]
        [TestCase("If-Match: \"other\"",                                  412)]
        [TestCase("If-Match: W/{0}",                                      412)]   // strong comparison (Section 13.1.1)
        [TestCase("If-Unmodified-Since: Tue, 30 Jun 2026 12:00:00 GMT",   412)]
        [TestCase("If-Unmodified-Since: Wed, 01 Jul 2026 12:00:00 GMT",   200)]
        public async Task A_precondition_is_evaluated_by_the_layer(String Precondition, Int32 Expected)
        {

            var response = await Exchange(Get("/resource.txt", String.Format(Precondition, ETag)));

            Assert.That(response.StatusCode, Is.EqualTo(Expected));

        }

        /// <summary>
        /// Section 13.2.2: If-None-Match is evaluated instead of
        /// If-Modified-Since, not as well — a non-matching entity-tag wins over a
        /// date that would have said "not modified".
        /// </summary>
        [Test]
        public async Task If_None_Match_decides_over_If_Modified_Since()
        {

            var response = await Exchange(Get("/resource.txt",
                                              "If-None-Match: \"other\"",
                                              "If-Modified-Since: Wed, 01 Jul 2026 12:00:00 GMT"));

            Assert.That(response.StatusCode, Is.EqualTo(200));

        }

        /// <summary>
        /// RFC 9110, Section 15.4.5: a 304 has no content and repeats the
        /// validators. Section 8.6: it MUST NOT carry a Content-Length other than
        /// the one the 200 would have had — a "Content-Length: 0" there is a
        /// statement about the representation, and a false one. The second
        /// request on the same connection proves the 304 ended at its header
        /// section.
        /// </summary>
        [Test]
        public async Task A_304_repeats_the_validators_and_leaves_the_connection_usable()
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort);
            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync(Get("/resource.txt", $"If-None-Match: {ETag}"), giveUp.Token);
            var notModified = await client.ReadResponseAsync(CancellationToken: giveUp.Token);

            await client.SendAsync(Get("/resource.txt"), giveUp.Token);
            var full        = await client.ReadResponseAsync(CancellationToken: giveUp.Token);

            Assert.That(notModified.StatusCode,               Is.EqualTo(304));
            Assert.That(Field(notModified, "ETag"),           Is.EqualTo(ETag));
            Assert.That(Field(notModified, "Last-Modified"),  Is.EqualTo("Wed, 01 Jul 2026 12:00:00 GMT"));

            if (notModified.Headers.TryGetValue("Content-Length", out var lengths))
                Assert.That(lengths, Is.EqualTo(new[] { Text.Length.ToString() }), "a 304's Content-Length must be the 200's (RFC 9110, Section 8.6)");

            Assert.That(full.StatusCode,                      Is.EqualTo(200), "the 304 left something on the connection");
            Assert.That(full.Body,                            Is.EqualTo(Text));

        }

        /// <summary>
        /// The server derives HEAD from GET and calls the handler with the
        /// request's own method, so a conditional HEAD is a conditional request
        /// too.
        /// </summary>
        [Test]
        public async Task A_conditional_HEAD_gets_its_304()
        {

            var response = await Exchange($"HEAD /resource.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nIf-None-Match: {ETag}\r\n\r\n",
                                          ResponseHasNoBody: true);

            Assert.That(response.StatusCode, Is.EqualTo(304));

        }

        #endregion

        #region Range requests (RFC 9110, Section 14)

        [TestCase("bytes=0-4",    0,  4)]
        [TestCase("bytes=-6",    38, 43)]
        [TestCase("bytes=10-",   10, 43)]
        [TestCase("bytes=40-99", 40, 43)]
        public async Task A_single_range_is_a_206(String Range, Int32 First, Int32 Last)
        {

            Assert.That(Text.Length, Is.EqualTo(44), "the expectations below are written for this length");

            var response = await Exchange(Get("/resource.txt", $"Range: {Range}"));

            Assert.That(response.StatusCode,                Is.EqualTo(206));
            Assert.That(Field(response, "Content-Range"),   Is.EqualTo($"bytes {First}-{Last}/44"));
            Assert.That(response.Body,                      Is.EqualTo(Text[First..(Last + 1)]));
            Assert.That(Field(response, "ETag"),            Is.EqualTo(ETag));

        }

        /// <summary>
        /// Section 14.6: two ranges are one multipart/byteranges body, and its
        /// boundary is a parameter of the Content-Type — which a typed setter
        /// that keeps only the media type and charset would drop, leaving a body
        /// nobody can split.
        /// </summary>
        [Test]
        public async Task Two_ranges_are_one_multipart_byteranges_body()
        {

            var response     = await Exchange(Get("/resource.txt", "Range: bytes=0-4,6-9"));
            var contentType  = Field(response, "Content-Type");
            var body         = Encoding.ASCII.GetString(response.Body);

            Assert.That(response.StatusCode,  Is.EqualTo(206));
            Assert.That(contentType,          Does.StartWith("multipart/byteranges; boundary="));

            var boundary = contentType["multipart/byteranges; boundary=".Length..];

            Assert.That(body, Does.StartWith($"--{boundary}\r\n"));
            Assert.That(body, Does.Contain("Content-Range: bytes 0-4/44\r\n\r\nHello\r\n"));
            Assert.That(body, Does.Contain("Content-Range: bytes 6-9/44\r\n\r\nfrom\r\n"));
            Assert.That(body, Does.EndWith($"--{boundary}--\r\n"));

        }

        /// <summary>
        /// Section 14.2: a server MUST ignore Range on a method for which range
        /// handling is not defined, and GET is the only one that has it. A HEAD
        /// with a Range is answered as a HEAD without one: 200, and the length of
        /// the whole representation. The derived HEAD reaches the handler as the
        /// GET handler, so this holds only if the layer is told the method the
        /// request had rather than the one it was routed under.
        /// </summary>
        [Test]
        public async Task A_HEAD_ignores_Range()
        {

            var response = await Exchange("HEAD /resource.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nRange: bytes=0-4\r\n\r\n",
                                          ResponseHasNoBody: true);

            Assert.That(response.StatusCode,                Is.EqualTo(200));
            Assert.That(Field(response, "Content-Length"),  Is.EqualTo("44"));
            Assert.That(response.Headers.ContainsKey("Content-Range"), Is.False);

        }

        [Test]
        public async Task A_range_past_the_end_is_a_416_stating_the_length()
        {

            var response = await Exchange(Get("/resource.txt", "Range: bytes=999-"));

            Assert.That(response.StatusCode,               Is.EqualTo(416));
            Assert.That(Field(response, "Content-Range"),  Is.EqualTo("bytes */44"));

        }

        /// <summary>
        /// Section 13.1.5: If-Range with the current strong entity-tag lets the
        /// range through; with any other, or with a weak one, the whole
        /// representation comes back as a 200, which is what keeps a client from
        /// splicing a part of a new representation onto an old one.
        /// </summary>
        [TestCase("{0}",                              206)]
        [TestCase("\"other\"",                        200)]
        [TestCase("W/{0}",                            200)]
        [TestCase("Wed, 01 Jul 2026 12:00:00 GMT",    206)]
        [TestCase("Tue, 30 Jun 2026 12:00:00 GMT",    200)]
        public async Task If_Range_lets_the_range_through_only_on_the_current_representation(String IfRange, Int32 Expected)
        {

            var response = await Exchange(Get("/resource.txt", "Range: bytes=0-4", $"If-Range: {String.Format(IfRange, ETag)}"));

            Assert.That(response.StatusCode, Is.EqualTo(Expected));
            Assert.That(response.Body,       Is.EqualTo(Expected == 206 ? Text[..5] : Text));

        }

        #endregion

        #region Content negotiation (RFC 9110, Section 12)

        [TestCase("de",              "Hallo!\n", "de")]
        [TestCase("en",              "Hello!\n", "en")]
        [TestCase("fr",              "Hello!\n", "en")]   // nothing matches, nothing forbidden: the first variant
        [TestCase("de;q=0.5, en",    "Hello!\n", "en")]
        public async Task The_language_is_negotiated_and_Vary_says_on_what(String AcceptLanguage, String Body, String Language)
        {

            var response = await Exchange(Get("/greeting", $"Accept-Language: {AcceptLanguage}"));

            Assert.That(response.StatusCode,                   Is.EqualTo(200));
            Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo(Body));
            Assert.That(Field(response, "Content-Language"),   Is.EqualTo(Language));
            Assert.That(Field(response, "Vary"),               Is.EqualTo("accept-language"));

        }

        [Test]
        public async Task Every_variant_refused_is_a_406_that_still_varies()
        {

            var response = await Exchange(Get("/greeting", "Accept-Language: de;q=0, en;q=0"));

            Assert.That(response.StatusCode,      Is.EqualTo(406));
            Assert.That(Field(response, "Vary"),  Is.EqualTo("accept-language"));

        }

        #endregion

        #region The resource handler

        /// <summary>
        /// The representation's media type goes out as the handler gave it,
        /// parameters and all. Through the typed Content-Type setter a profile
        /// would be dropped and a charset added that nobody asked for.
        /// </summary>
        [Test]
        public async Task The_media_type_goes_out_with_its_own_parameters()
        {

            var response = await Exchange(Get("/profiled"));

            Assert.That(response.StatusCode,               Is.EqualTo(200));
            Assert.That(Field(response, "Content-Type"),   Is.EqualTo(ProfiledType));

        }

        [Test]
        public async Task A_resource_the_handler_does_not_have_is_a_404()
        {

            var response = await Exchange(Get("/missing"));

            Assert.That(response.StatusCode, Is.EqualTo(404));

        }

        /// <summary>
        /// The resource handler is given what HTTP/2 gives it, the target in
        /// origin form with its query — byte for byte as sent, not re-encoded
        /// and reordered from the parsed query string.
        /// </summary>
        [TestCase("/path",              "/path")]
        [TestCase("/path?b=2&a=%20",    "/path?b=2&a=%20")]
        public async Task The_handler_is_given_the_target_in_origin_form(String Target, String Expected)
        {

            var response = await Exchange($"GET {Target} HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");

            Assert.That(response.StatusCode,                    Is.EqualTo(200));
            Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo(Expected));

        }

        /// <summary>
        /// The layer reads its fields by their lower-case names, as HTTP/2
        /// delivers them. On HTTP/1.x they arrive spelled however the client
        /// spelled them, and a field the layer looked up under one spelling and
        /// found under another would be a precondition silently not evaluated.
        /// </summary>
        [TestCase("if-none-match")]
        [TestCase("IF-NONE-MATCH")]
        [TestCase("If-None-Match")]
        public async Task A_field_is_read_whatever_its_spelling(String Name)
        {

            var response = await Exchange(Get("/resource.txt", $"{Name}: {ETag}"));

            Assert.That(response.StatusCode, Is.EqualTo(304));

        }

        /// <summary>
        /// RFC 9110, Section 5.3: a field sent on two lines is one field whose
        /// value is the two joined with a comma. An entity-tag on the second
        /// If-None-Match line is as much a match as one on the first.
        /// </summary>
        [Test]
        public async Task A_field_sent_on_two_lines_is_one_list()
        {

            var response = await Exchange(Get("/resource.txt", "If-None-Match: \"other\"", $"If-None-Match: {ETag}"));

            Assert.That(response.StatusCode, Is.EqualTo(304));

        }

        #endregion

        #region QUERY (RFC 10008) and the methods the server derives

        [Test]
        public async Task A_QUERY_runs_through_the_same_pipeline_as_a_GET()
        {

            var response = await Exchange("QUERY /search HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: text/plain\r\nContent-Length: 5\r\n\r\nhello");

            Assert.That(response.StatusCode,                    Is.EqualTo(200));
            Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo("[\"hello\"]"));
            Assert.That(Field(response, "Content-Location"),    Is.EqualTo("/search/results/1"));
            Assert.That(Field(response, "ETag"),                Is.EqualTo(HTTPValidators.ComputeETag(response.Body)));

        }

        [Test]
        public async Task A_QUERY_without_a_Content_Type_is_refused()
        {

            var response = await Exchange("QUERY /search HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 5\r\n\r\nhello");

            Assert.That(response.StatusCode, Is.EqualTo(400));

        }

        /// <summary>
        /// OPTIONS and the 405 stay the server's, and read the methods
        /// AddResource registered: QUERY only where a query handler was given.
        /// </summary>
        [TestCase("/search",        true)]
        [TestCase("/resource.txt",  false)]
        public async Task OPTIONS_names_QUERY_only_where_there_is_a_query_handler(String Path, Boolean HasQuery)
        {

            var response = await Exchange($"OPTIONS {Path} HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
            var allow    = Field(response, "Allow").Split(',').Select(method => method.Trim()).ToArray();

            Assert.That(allow, Does.Contain("GET"));
            Assert.That(allow, Does.Contain("HEAD"));
            Assert.That(allow, HasQuery ? Does.Contain("QUERY") : Does.Not.Contain("QUERY"));

        }

        [Test]
        public async Task A_method_the_resource_does_not_have_is_a_405()
        {

            var response = await Exchange("POST /resource.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 0\r\n\r\n");

            Assert.That(response.StatusCode, Is.EqualTo(405));

        }

        #endregion

    }

}
