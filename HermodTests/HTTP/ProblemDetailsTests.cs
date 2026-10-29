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

using System.Net.Sockets;
using System.Text;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Problem details for HTTP APIs (RFC 9457): the model, when a request's
    /// Accept field prefers it, and the server's own errors in both forms.
    /// </summary>
    /// <remarks>
    /// Until 2026-10-29 Hermod had nothing for RFC 9457. Every error the server
    /// makes itself went out as application/json carrying a "description",
    /// whatever the request accepted. That stays the answer for every client
    /// that does not ask: these tests pin it as firmly as the new form. H-15 in
    /// HTTP1ConformanceTests.
    /// </remarks>
    [TestFixture]
    public class ProblemDetailsTests
    {

        #region The model

        #region ForStatusIsAboutBlankWithTheReasonPhraseAsItsTitle()

        /// <summary>
        /// Section 4.2.1: about:blank means nothing beyond the status code, and its
        /// title SHOULD be the reason phrase. Section 3.1.1: an absent type is
        /// about:blank, so it need not be written.
        /// </summary>
        [Test]
        public void ForStatusIsAboutBlankWithTheReasonPhraseAsItsTitle()
        {

            var problem = HTTPProblemDetails.ForStatus(HTTPStatusCode.NotFound, "No such page.");
            var json    = problem.ToJSON();

            Assert.Multiple(() => {
                Assert.That(problem.Type,           Is.Null);
                Assert.That(problem.EffectiveType,  Is.EqualTo("about:blank"));
                Assert.That(problem.Title,          Is.EqualTo("Not Found"));
                Assert.That(problem.Status,         Is.EqualTo(HTTPStatusCode.NotFound));
                Assert.That(json.ContainsKey("type"), Is.False);
                Assert.That(json.ToString(Newtonsoft.Json.Formatting.None),
                            Is.EqualTo("{\"title\":\"Not Found\",\"status\":404,\"detail\":\"No such page.\"}"));
            });

        }

        #endregion

        #region TheMembersComeInTheSchemaOrderThenTheExtensions()

        [Test]
        public void TheMembersComeInTheSchemaOrderThenTheExtensions()
        {

            var problem = new HTTPProblemDetails(
                              Type:        "https://example.com/probs/out-of-credit",
                              Status:      HTTPStatusCode.Forbidden,
                              Title:       "You do not have enough credit.",
                              Detail:      "Your current balance is 30, but that costs 50.",
                              Instance:    "/account/12345/msgs/abc",
                              Extensions:  new JObject(
                                               new JProperty("balance",  30),
                                               new JProperty("accounts", new JArray("/account/12345", "/account/67890"))
                                           )
                          );

            Assert.That(problem.ToString(),
                        Is.EqualTo("{\"type\":\"https://example.com/probs/out-of-credit\"," +
                                   "\"title\":\"You do not have enough credit.\"," +
                                   "\"status\":403," +
                                   "\"detail\":\"Your current balance is 30, but that costs 50.\"," +
                                   "\"instance\":\"/account/12345/msgs/abc\"," +
                                   "\"balance\":30," +
                                   "\"accounts\":[\"/account/12345\",\"/account/67890\"]}"));

        }

        #endregion

        #region AnExtensionCannotTakeTheNameOfAMember(Name)

        /// <summary>
        /// The five names belong to Section 3.1. An extension by one of them would
        /// be serialized as that member, with a meaning nobody gave it.
        /// </summary>
        [TestCase("type")]
        [TestCase("status")]
        [TestCase("title")]
        [TestCase("detail")]
        [TestCase("instance")]
        public void AnExtensionCannotTakeTheNameOfAMember(String Name)
        {

            Assert.That(() => new HTTPProblemDetails(Extensions: new JObject(new JProperty(Name, "x"))),
                        Throws.ArgumentException);

        }

        #endregion

        #region TheExtensionsAreACopy()

        [Test]
        public void TheExtensionsAreACopy()
        {

            var extensions = new JObject(new JProperty("balance", 30));
            var problem    = new HTTPProblemDetails(Extensions: extensions);

            extensions["balance"] = 50;

            Assert.That(problem.Extensions["balance"]?.Value<Int32>(), Is.EqualTo(30));

        }

        #endregion

        #region TheRFCsFirstExampleParses()

        /// <summary>
        /// Section 3, verbatim but for the whitespace.
        /// </summary>
        [Test]
        public void TheRFCsFirstExampleParses()
        {

            var parsed = HTTPProblemDetails.TryParse(
                             """
                             {
                              "type": "https://example.com/probs/out-of-credit",
                              "title": "You do not have enough credit.",
                              "detail": "Your current balance is 30, but that costs 50.",
                              "instance": "/account/12345/msgs/abc",
                              "balance": 30,
                              "accounts": ["/account/12345",
                                           "/account/67890"]
                             }
                             """,
                             out var problem
                         );

            Assert.That(parsed, Is.True);
            Assert.Multiple(() => {
                Assert.That(problem!.Type,                     Is.EqualTo("https://example.com/probs/out-of-credit"));
                Assert.That(problem.EffectiveType,             Is.EqualTo("https://example.com/probs/out-of-credit"));
                Assert.That(problem.Title,                     Is.EqualTo("You do not have enough credit."));
                Assert.That(problem.Status,                    Is.Null);
                Assert.That(problem.Detail,                    Is.EqualTo("Your current balance is 30, but that costs 50."));
                Assert.That(problem.Instance,                  Is.EqualTo("/account/12345/msgs/abc"));
                Assert.That(problem.Extensions["balance"]?.Value<Int32>(), Is.EqualTo(30));
                Assert.That(problem.Extensions["accounts"]?.Values<String>(),
                            Is.EqualTo(new[] { "/account/12345", "/account/67890" }));
                Assert.That(problem.Extensions.Count,          Is.EqualTo(2));
            });

        }

        #endregion

        #region AMemberOfTheWrongTypeIsIgnored(JSON, Member)

        /// <summary>
        /// Section 3.1: "If a member's value type does not match the specified
        /// type, the member MUST be ignored -- i.e., processing will continue as
        /// if the member had not been present." Not present means not kept as an
        /// extension either.
        /// </summary>
        [TestCase("{\"type\":42}",          "type")]
        [TestCase("{\"type\":null}",        "type")]
        [TestCase("{\"title\":[\"x\"]}",    "title")]
        [TestCase("{\"detail\":{}}",        "detail")]
        [TestCase("{\"instance\":true}",    "instance")]
        [TestCase("{\"status\":\"404\"}",   "status")]
        public void AMemberOfTheWrongTypeIsIgnored(String JSON, String Member)
        {

            Assert.That(HTTPProblemDetails.TryParse(JSON, out var problem), Is.True);

            Assert.Multiple(() => {
                Assert.That(problem!.Type,                     Is.Null);
                Assert.That(problem.EffectiveType,             Is.EqualTo("about:blank"));
                Assert.That(problem.Status,                    Is.Null);
                Assert.That(problem.Title,                     Is.Null);
                Assert.That(problem.Detail,                    Is.Null);
                Assert.That(problem.Instance,                  Is.Null);
                Assert.That(problem.Extensions.ContainsKey(Member), Is.False);
                Assert.That(problem.ToString(),                Is.EqualTo("{}"));
            });

        }

        #endregion

        #region AStatusThatCannotBeAStatusCodeIsIgnored(Status)

        /// <summary>
        /// A status code is a three-digit integer, 100 to 599 (RFC 9110, Section
        /// 15). A number that cannot be one is ignored like a value of the wrong
        /// type, the integer too large for Int64 included, which arrives as a
        /// BigInteger and must not throw.
        /// </summary>
        [TestCase("404.5")]
        [TestCase("99")]
        [TestCase("600")]
        [TestCase("-404")]
        [TestCase("1e30")]
        [TestCase("123456789012345678901234567890")]
        public void AStatusThatCannotBeAStatusCodeIsIgnored(String Status)
        {

            Assert.That(HTTPProblemDetails.TryParse($"{{\"status\":{Status}}}", out var problem), Is.True);
            Assert.That(problem!.Status, Is.Null);

        }

        #endregion

        #region AnIntegralStatusIsRead(Status, Code)

        [TestCase("404",   404u)]
        [TestCase("404.0", 404u)]
        [TestCase("418",   418u)]
        [TestCase("599",   599u)]
        public void AnIntegralStatusIsRead(String Status, UInt32 Code)
        {

            Assert.That(HTTPProblemDetails.TryParse($"{{\"status\":{Status}}}", out var problem), Is.True);
            Assert.That(problem!.Status?.Code, Is.EqualTo(Code));

        }

        #endregion

        #region OnlyAJSONObjectIsProblemDetails(Text)

        [TestCase("[]")]
        [TestCase("\"about:blank\"")]
        [TestCase("404")]
        [TestCase("not json")]
        [TestCase("")]
        public void OnlyAJSONObjectIsProblemDetails(String Text)
        {

            Assert.That(HTTPProblemDetails.TryParse(Text, out var problem), Is.False);
            Assert.That(problem,                                            Is.Null);

        }

        #endregion

        #region ARoundTripKeepsEverything()

        [Test]
        public void ARoundTripKeepsEverything()
        {

            var problem = new HTTPProblemDetails(
                              Type:        "tag:example@example.org,2021-09-17:OutOfLuck",
                              Status:      HTTPStatusCode.UnprocessableEntity,
                              Title:       "Your request is not valid.",
                              Detail:      "Two fields are wrong.",
                              Instance:    "https://example.net/problems/7",
                              Extensions:  new JObject(
                                               new JProperty("errors", new JArray(
                                                   new JObject(new JProperty("detail", "must be a positive integer"), new JProperty("pointer", "#/age"))
                                               ))
                                           )
                          );

            Assert.That(HTTPProblemDetails.TryParse(problem.ToString(), out var parsed), Is.True);
            Assert.That(parsed!.ToString(), Is.EqualTo(problem.ToString()));

        }

        #endregion

        #endregion

        #region Negotiation

        #region IsPreferredBy(Accept, Expected)

        /// <summary>
        /// Problem details are preferred when Accept names application/problem+json
        /// itself, with a quality above 0 and at least the one the most specific
        /// range matching application/json has (RFC 9110, Section 12.5.1).
        /// </summary>
        [TestCase("application/problem+json",                                                    true)]
        [TestCase("application/json, application/problem+json",                                  true)]   // RFC 9457's first example
        [TestCase("application/problem+json, application/json",                                  true)]
        [TestCase("Application/Problem+JSON",                                                    true)]   // media types are case-insensitive
        [TestCase("text/html, application/problem+json;q=0.1",                                   true)]   // nothing else matches application/json
        [TestCase("*/*;q=0.1, application/problem+json;q=0.5",                                   true)]
        [TestCase("application/json;q=0.5, application/*, application/problem+json;q=0.7",      true)]   // application/json is the most specific
        [TestCase("application/json;q=1, application/problem+json;q=0.5",                        false)]
        [TestCase("application/*;q=0.9, application/problem+json;q=0.8",                         false)]
        [TestCase("application/problem+json;q=0",                                                false)]
        [TestCase("*/*",                                                                         false)]  // a wildcard alone does not ask
        [TestCase("application/*",                                                               false)]
        [TestCase("application/json",                                                            false)]
        [TestCase("text/html",                                                                   false)]
        public void IsPreferredBy(String Accept, Boolean Expected)
        {

            Assert.That(AcceptTypes.TryParse(Accept, out var acceptTypes), Is.True);
            Assert.That(HTTPProblemDetails.IsPreferredBy(acceptTypes),     Is.EqualTo(Expected));

        }

        [Test]
        public void NoAcceptFieldDoesNotPreferThem()
        {
            Assert.That(HTTPProblemDetails.IsPreferredBy(null), Is.False);
            Assert.That(HTTPProblemDetails.IsPreferredBy([]),   Is.False);
        }

        #endregion

        #region AMalformedElementDoesNotThrow(Accept, Expected)

        /// <summary>
        /// AcceptType.TryParse threw on a weight that is not a number, and on an
        /// element with nothing in it, so reading Request.Accept threw. Nothing on
        /// an error path read it until the server's errors began to negotiate.
        /// The malformed element is dropped, and the rest of the field stands.
        /// </summary>
        [TestCase("application/json;q=abc, application/problem+json",  1)]
        [TestCase("text/html, ;, application/problem+json",            2)]
        [TestCase("application/problem+json;q=",                       0)]
        public void AMalformedElementDoesNotThrow(String Accept, Int32 Expected)
        {

            AcceptTypes? acceptTypes = null;

            Assert.That(() => AcceptTypes.TryParse(Accept, out acceptTypes), Throws.Nothing);
            Assert.That(acceptTypes?.Count() ?? 0, Is.EqualTo(Expected));

        }

        #endregion

        #endregion

        #region The server's own errors

        #region (private static) StartServer(IncludeStackTraces = false)

        /// <summary>
        /// GET / answers, GET /throws throws, GET /conflict and GET /forbidden
        /// answer with problem details of their own, and the body limit is 16
        /// bytes. Everything else is the server's own error.
        /// </summary>
        private static async Task<HTTPServer> StartServer(Boolean IncludeStackTraces = false)
        {

            var httpServer = await HTTPServer.StartNew(
                                       MaxHTTPBodySize:                     16,
                                       IncludeStackTracesInErrorResponses:  IncludeStackTraces
                                   );

            var httpAPI    = httpServer.AddHTTPAPI();

            httpAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode  = HTTPStatusCode.OK,
                                                                ContentType     = HTTPContentType.Text.PLAIN,
                                                                Content         = "served".ToUTF8Bytes()
                                                            }.AsImmutable
                                                        ));

            httpAPI.AddHandler(HTTPPath.Root + "throws",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => throw new InvalidOperationException("boom"));

            // A handler's own problem details: one naming a status code other than
            // the one the builder started with, and one naming none.
            httpAPI.AddHandler(HTTPPath.Root + "conflict",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode  = HTTPStatusCode.OK
                                                            }.SetProblemDetails(
                                                                HTTPProblemDetails.ForStatus(HTTPStatusCode.Conflict, "Try again.")
                                                            ).AsImmutable
                                                        ));

            httpAPI.AddHandler(HTTPPath.Root + "forbidden",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode  = HTTPStatusCode.Forbidden
                                                            }.SetProblemDetails(
                                                                new HTTPProblemDetails(Title: "No.")
                                                            ).AsImmutable
                                                        ));

            return httpServer;

        }

        #endregion

        #region (private static) Exchange(Port, Request)

        /// <summary>
        /// Send the request as given, read until the server closes the connection
        /// or five seconds pass, and split what arrived into status code, header
        /// fields and content.
        /// </summary>
        private static async Task<(UInt32 Status, Dictionary<String, String> Fields, String Content)> Exchange(IPPort  Port,
                                                                                                                String  Request)
        {

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            await using var stream = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));
            await stream.FlushAsync();

            var received = new MemoryStream();
            var buffer   = new Byte[4096];

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (true)
                {

                    var read = await stream.ReadAsync(buffer, timeout.Token);

                    if (read == 0)
                        break;

                    received.Write(buffer, 0, read);

                }
            }
            catch (OperationCanceledException)
            { }
            catch (IOException)
            { }

            var text      = Encoding.UTF8.GetString(received.ToArray());
            var end       = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            Assert.That(end, Is.GreaterThan(0), $"no complete header section in: {text}");

            var lines     = text[..end].Split("\r\n");
            var fields    = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                fields[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }

            return (UInt32.Parse(lines[0].Split(' ')[1]),
                    fields,
                    text[(end + 4)..]);

        }

        #endregion

        #region (private static) Request(Method, Path, Accept, Extra = "")

        private static String Request(String   Method,
                                      String   Path,
                                      String?  Accept,
                                      String   Extra = "")

            => $"{Method} {Path} HTTP/1.1\r\nHost: localhost\r\n" +
               (Accept is not null ? $"Accept: {Accept}\r\n" : "") +
               Extra +
               "Connection: close\r\n\r\n";

        #endregion


        #region WithoutAskingTheErrorsAreWhatTheyWere(Accept)

        /// <summary>
        /// The contract every existing client relies on: application/json, a
        /// "description", and nothing of RFC 9457.
        /// </summary>
        [TestCase(null)]
        [TestCase("*/*")]
        [TestCase("application/*")]
        [TestCase("application/json")]
        [TestCase("application/json, application/problem+json;q=0.5")]
        public async Task WithoutAskingTheErrorsAreWhatTheyWere(String? Accept)
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort, Request("GET", "/nowhere", Accept));
            var json = JObject.Parse(content);

            Assert.Multiple(() => {
                Assert.That(status,                    Is.EqualTo(404));
                Assert.That(fields["Content-Type"],    Is.EqualTo("application/json; charset=utf-8"));
                Assert.That(json["description"]?.Type, Is.EqualTo(JTokenType.String));
                Assert.That(json["request"]?.Value<String>(), Is.EqualTo("GET /nowhere HTTP/1.1"));
                Assert.That(json.ContainsKey("title"),  Is.False);
                Assert.That(json.ContainsKey("status"), Is.False);
            });

        }

        #endregion

        #region A404AsProblemDetails()

        [Test]
        public async Task A404AsProblemDetails()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort, Request("GET", "/nowhere", "application/problem+json"));

            Assert.That(HTTPProblemDetails.TryParse(content, out var problem), Is.True, content);
            Assert.Multiple(() => {
                Assert.That(status,                     Is.EqualTo(404));
                Assert.That(fields["Content-Type"],     Is.EqualTo("application/problem+json"));
                Assert.That(problem!.Type,              Is.Null);
                Assert.That(problem.EffectiveType,      Is.EqualTo("about:blank"));
                Assert.That(problem.Title,              Is.EqualTo("Not Found"));
                Assert.That(problem.Status?.Code,       Is.EqualTo(status));
                Assert.That(problem.Detail,             Is.Not.Null.And.Not.Empty);
                Assert.That(problem.Extensions["request"]?.Value<String>(), Is.EqualTo("GET /nowhere HTTP/1.1"));
                Assert.That(problem.Extensions.ContainsKey("description"),  Is.False);
            });

        }

        #endregion

        #region A405KeepsItsAllowField()

        /// <summary>
        /// The form of the content changes, and nothing else about the response.
        /// </summary>
        [Test]
        public async Task A405KeepsItsAllowField()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort, Request("DELETE", "/", "application/problem+json"));

            Assert.That(HTTPProblemDetails.TryParse(content, out var problem), Is.True, content);
            Assert.Multiple(() => {
                Assert.That(status,                  Is.EqualTo(405));
                Assert.That(fields["Content-Type"],  Is.EqualTo("application/problem+json"));
                Assert.That(fields["Allow"],         Does.Contain("GET"));
                Assert.That(problem!.Title,          Is.EqualTo("Method Not Allowed"));
                Assert.That(problem.Status?.Code,    Is.EqualTo(status));
            });

        }

        #endregion

        #region A413CarriesItsLimitAsAnExtension()

        [Test]
        public async Task A413CarriesItsLimitAsAnExtension()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort,
                                                           Request("GET", "/", "application/problem+json", "Content-Length: 64\r\n") +
                                                           new String('x', 64));

            Assert.That(HTTPProblemDetails.TryParse(content, out var problem), Is.True, content);
            Assert.Multiple(() => {
                Assert.That(status,                  Is.EqualTo(413));
                Assert.That(fields["Content-Type"],  Is.EqualTo("application/problem+json"));
                Assert.That(problem!.Status?.Code,   Is.EqualTo(status));
                Assert.That(problem.Detail,          Is.EqualTo("The request body is too large."));
                Assert.That(problem.Extensions["maximumBytes"]?.Value<Int64>(), Is.EqualTo(16));
            });

        }

        #endregion

        #region A417AsProblemDetails()

        [Test]
        public async Task A417AsProblemDetails()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort,
                                                           Request("GET", "/", "application/problem+json", "Expect: 200-ok\r\n"));

            Assert.That(HTTPProblemDetails.TryParse(content, out var problem), Is.True, content);
            Assert.Multiple(() => {
                Assert.That(status,                  Is.EqualTo(417));
                Assert.That(fields["Content-Type"],  Is.EqualTo("application/problem+json"));
                Assert.That(problem!.Title,          Is.EqualTo("Expectation Failed"));
                Assert.That(problem.Detail,          Is.EqualTo("The request contains an unsupported Expect header field."));
            });

        }

        #endregion

        #region A500SaysNoMoreThanBefore()

        /// <summary>
        /// Section 5 warns against exposing implementation details. The problem
        /// form says what the JSON form says, and that is no stack trace unless
        /// the server was told to send one.
        /// </summary>
        [Test]
        public async Task A500SaysNoMoreThanBefore()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort, Request("GET", "/throws", "application/problem+json"));

            Assert.That(HTTPProblemDetails.TryParse(content, out var problem), Is.True, content);
            Assert.Multiple(() => {
                Assert.That(status,                  Is.EqualTo(500));
                Assert.That(fields["Content-Type"],  Is.EqualTo("application/problem+json"));
                Assert.That(problem!.Title,          Is.EqualTo("Internal Server Error"));
                Assert.That(problem.Detail,          Is.EqualTo("An internal server error occurred."));
                Assert.That(content,                 Does.Not.Contain("boom"));
                Assert.That(problem.Extensions.ContainsKey("stackTrace"), Is.False);
            });

        }

        #endregion

        #region AnExceptionsTypeIsNotTheProblemsType()

        /// <summary>
        /// With stack traces on, the JSON form names the exception's type in a
        /// member called "type", which in RFC 9457 is the problem type. Kept as
        /// it was in the JSON form, and renamed in the problem form, where
        /// it would otherwise claim the problem has a type it does not.
        /// </summary>
        [Test]
        public async Task AnExceptionsTypeIsNotTheProblemsType()
        {

            await using var httpServer = await StartServer(IncludeStackTraces: true);

            var asJSON    = await Exchange(httpServer.TCPPort, Request("GET", "/throws", null));
            var asProblem = await Exchange(httpServer.TCPPort, Request("GET", "/throws", "application/problem+json"));

            var json      = JObject.Parse(asJSON.Content);

            Assert.That(HTTPProblemDetails.TryParse(asProblem.Content, out var problem), Is.True, asProblem.Content);
            Assert.Multiple(() => {
                Assert.That(json.ContainsKey("type"),                       Is.True);
                Assert.That(json["description"]?.Value<String>(),           Is.EqualTo("boom"));
                Assert.That(problem!.Type,                                  Is.Null);
                Assert.That(problem.EffectiveType,                          Is.EqualTo("about:blank"));
                Assert.That(problem.Detail,                                 Is.EqualTo("boom"));
                Assert.That(problem.Extensions["exceptionType"]?.ToString(), Is.EqualTo(json["type"]?.ToString()));
                Assert.That(problem.Extensions.ContainsKey("stackTrace"),   Is.True);
            });

        }

        #endregion

        #region AMalformedAcceptStillGetsItsError()

        /// <summary>
        /// Reading Accept on an error path must not turn the error into another
        /// one. Before AcceptType.TryParse stopped throwing, this was a 500.
        /// </summary>
        [Test]
        public async Task AMalformedAcceptStillGetsItsError()
        {

            await using var httpServer = await StartServer();

            var (status, fields, content) = await Exchange(httpServer.TCPPort, Request("GET", "/nowhere", "application/json;q=abc"));

            Assert.Multiple(() => {
                Assert.That(status,                Is.EqualTo(404));
                Assert.That(fields["Content-Type"], Is.EqualTo("application/json; charset=utf-8"));
                Assert.That(JObject.Parse(content)["description"], Is.Not.Null);
            });

        }

        #endregion

        #region AHandlersProblemDetailsCarryTheStatusCodeTheyName()

        /// <summary>
        /// Section 3.1.2: a generator MUST send the status code the problem names.
        /// SetProblemDetails sets it, and a problem naming none leaves the
        /// response's own.
        /// </summary>
        [Test]
        public async Task AHandlersProblemDetailsCarryTheStatusCodeTheyName()
        {

            await using var httpServer = await StartServer();

            var conflict  = await Exchange(httpServer.TCPPort, Request("GET", "/conflict",  null));
            var forbidden = await Exchange(httpServer.TCPPort, Request("GET", "/forbidden", null));

            Assert.Multiple(() => {
                Assert.That(conflict.Status,                   Is.EqualTo(409));
                Assert.That(conflict.Fields["Content-Type"],   Is.EqualTo("application/problem+json"));
                Assert.That(conflict.Content,                  Is.EqualTo("{\"title\":\"Conflict\",\"status\":409,\"detail\":\"Try again.\"}"));
                Assert.That(forbidden.Status,                  Is.EqualTo(403));
                Assert.That(forbidden.Fields["Content-Type"],  Is.EqualTo("application/problem+json"));
                Assert.That(forbidden.Content,                 Is.EqualTo("{\"title\":\"No.\"}"));
            });

        }

        #endregion

        #region TheClientReadsProblemDetails()

        [Test]
        public async Task TheClientReadsProblemDetails()
        {

            await using var httpServer = await StartServer();

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var notFound = await client.RunRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Root + "nowhere",
                                     RequestBuilder:  builder => builder.Accept = [ new AcceptType(HTTPContentType.Application.ProblemJSON) ],
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            var served   = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(notFound.TryParseProblemDetails(out var problem), Is.True);
                Assert.That(problem!.Status,     Is.EqualTo(HTTPStatusCode.NotFound));
                Assert.That(problem.Title,       Is.EqualTo("Not Found"));
                Assert.That(served.TryParseProblemDetails(out var none), Is.False);
                Assert.That(none,                Is.Null);
            });

        }

        #endregion

        #endregion

    }

}
