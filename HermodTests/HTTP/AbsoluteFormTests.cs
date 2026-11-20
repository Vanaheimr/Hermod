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
    /// RFC 9112, Section 3.2.2: "A server MUST accept the absolute-form in
    /// requests", and an origin server receiving it "MUST ignore the received
    /// Host header field (if any) and instead use the host information of the
    /// request-target".
    /// </summary>
    /// <remarks>
    /// Until H-42 (2026-11-20) the server refused the form with 400, and said
    /// so on purpose: it is not a proxy, and a server reading the target's path
    /// while a front end routes on Host was given as a way to smuggle past that
    /// front end. The section binds every server, and its own rule is what
    /// closes that gap: the target's authority is the one both of them use.
    ///
    /// Two APIs on two host names, so that which authority won is visible in
    /// which one answered.
    /// </remarks>
    [TestFixture]
    public class AbsoluteFormTests
    {

        #region Data

        private HTTPServer?  httpServer;
        private String?      resourcePath;

        #endregion

        #region Setup

        [OneTimeSetUp]
        public async Task Init()
        {

            httpServer = await HTTPServer.StartNew();

            foreach (var name in new[] { "example.test", "other.test" })
            {

                var api = httpServer.AddHTTPAPI(Hostname: HTTPHostname.Parse(name));

                foreach (var path in new[] { HTTPPath.Root, HTTPPath.Root + "a" })
                    api.AddHandler(path,
                                   HTTPMethod:   HTTPMethod.GET,
                                   HTTPDelegate: request => Task.FromResult(
                                                     new HTTPResponse.Builder(request) {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         ContentType     = HTTPContentType.Text.PLAIN,
                                                         Content         = $"{name} {request.Host} {request.TargetScheme ?? "-"} {request.OriginFormTarget}".ToUTF8Bytes()
                                                     }.AsImmutable
                                                 ));

                if (name == "example.test")
                    api.AddResource(HTTPPath.Root + "res",
                                    (path, headers, cancellationToken) => {
                                        resourcePath = path;
                                        return Task.FromResult<HTTPResource?>(
                                                   new HTTPResource {
                                                       Body         = "resource".ToUTF8Bytes(),
                                                       ContentType  = "text/plain"
                                                   }
                                               );
                                    });

            }

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.DisposeAsync();
        }

        #endregion

        #region (private) Exchange(Request)

        private async Task<(Int32 Status, String Body)> Exchange(String Request)
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(
                                               System.Net.IPAddress.Loopback,
                                               httpServer!.TCPPort
                                           );

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync(Request, giveUp.Token);

            var response = await client.ReadResponseAsync(false, giveUp.Token);

            return (response.StatusCode, Encoding.UTF8.GetString(response.Body));

        }

        #endregion


        #region AnAbsoluteFormTargetIsServedLikeItsOriginForm()

        [Test]
        public async Task AnAbsoluteFormTargetIsServedLikeItsOriginForm()
        {

            var origin    = await Exchange("GET /a?x=1 HTTP/1.1\r\nHost: example.test\r\n\r\n");
            var absolute  = await Exchange("GET http://example.test/a?x=1 HTTP/1.1\r\nHost: example.test\r\n\r\n");

            Assert.That(origin,   Is.EqualTo((200, "example.test example.test - /a?x=1")));
            Assert.That(absolute, Is.EqualTo((200, "example.test example.test http /a?x=1")));

        }

        #endregion

        #region TheTargetsAuthorityWinsOverTheHostField(Target, Host, Expected)

        /// <summary>
        /// The Host field is ignored, not merely outranked: a request whose two
        /// names disagree is routed, and seen by its handler, by the target's.
        /// </summary>
        [TestCase("http://example.test/a", "other.test",   "example.test example.test http /a")]
        [TestCase("http://other.test/a",   "example.test", "other.test other.test http /a")]
        public async Task TheTargetsAuthorityWinsOverTheHostField(String Target, String Host, String Expected)
        {

            var response = await Exchange($"GET {Target} HTTP/1.1\r\nHost: {Host}\r\n\r\n");

            Assert.That(response, Is.EqualTo((200, Expected)));

        }

        #endregion

        #region TheSchemeIsCaseInsensitive()

        /// <summary>
        /// RFC 3986, Section 3.1: "schemes are case-insensitive".
        /// </summary>
        [Test]
        public async Task TheSchemeIsCaseInsensitive()
        {

            var response = await Exchange("GET HTTP://example.test/a HTTP/1.1\r\nHost: example.test\r\n\r\n");

            Assert.That(response, Is.EqualTo((200, "example.test example.test http /a")));

        }

        #endregion

        #region AnEmptyPathIsTheRoot(Target, Expected)

        /// <summary>
        /// An http URI's path may be empty, and an origin-form one may not; the
        /// path of "http://example.test" is "/" (RFC 9112, Section 3.2.1).
        /// </summary>
        [TestCase("http://example.test",      "example.test example.test http /")]
        [TestCase("http://example.test?x=1",  "example.test example.test http /?x=1")]
        public async Task AnEmptyPathIsTheRoot(String Target, String Expected)
        {

            var response = await Exchange($"GET {Target} HTTP/1.1\r\nHost: example.test\r\n\r\n");

            Assert.That(response, Is.EqualTo((200, Expected)));

        }

        #endregion

        #region AnHTTP11RequestWithoutHostIsStillRefused()

        /// <summary>
        /// RFC 9112, Section 3.2: "A server MUST respond with a 400 (Bad Request)
        /// status code to any HTTP/1.1 request message that lacks a Host header
        /// field", and Section 3.2.2 says a client sends one with the
        /// absolute-form too. Ignoring its value is not doing without it.
        /// </summary>
        [Test]
        public async Task AnHTTP11RequestWithoutHostIsStillRefused()
        {

            var (status, _) = await Exchange("GET http://example.test/a HTTP/1.1\r\n\r\n");

            Assert.That(status, Is.EqualTo(400));

        }

        #endregion

        #region AnHTTP10RequestTakesItsHostFromTheTarget()

        [Test]
        public async Task AnHTTP10RequestTakesItsHostFromTheTarget()
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(
                                               System.Net.IPAddress.Loopback,
                                               httpServer!.TCPPort
                                           );

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync("GET http://other.test/a HTTP/1.0\r\n\r\n", giveUp.Token);

            var response = await client.ReadResponseAsync(false, giveUp.Token);

            Assert.That(response.StatusCode,                      Is.EqualTo(200));
            Assert.That(Encoding.UTF8.GetString(response.Body),   Is.EqualTo("other.test other.test http /a"));

        }

        #endregion

        #region AnInvalidAbsoluteFormTargetIsRefused(Target)

        /// <summary>
        /// A scheme this server cannot be authoritative for; an empty host, which
        /// RFC 9110, Section 4.2.1 says a recipient "MUST reject"; userinfo, which
        /// Section 4.2.4 says to treat as an error; an authority no Host field
        /// could carry; a path that would be refused in origin form; and a
        /// scheme without the "//" of an authority, which is no absolute-form
        /// this server accepts.
        /// </summary>
        [TestCase("ftp://example.test/a")]
        [TestCase("http:///a")]
        [TestCase("http://user@example.test/a")]
        [TestCase("http://user:secret@example.test/a")]
        [TestCase("http://*/a")]
        [TestCase("http://example.test/a//b")]
        [TestCase("http://example.test/%2e%2e/a")]
        [TestCase("http://example.test/a#fragment")]
        [TestCase("http:example.test/a")]
        public async Task AnInvalidAbsoluteFormTargetIsRefused(String Target)
        {

            var (status, _) = await Exchange($"GET {Target} HTTP/1.1\r\nHost: example.test\r\n\r\n");

            Assert.That(status, Is.EqualTo(400));

        }

        #endregion

        #region TheResourceLayerIsGivenThePathAndQuery()

        /// <summary>
        /// H-37's layer takes the target as HTTP/2's :path carries it, and the
        /// absolute-form's scheme and authority are not part of that.
        /// </summary>
        [Test]
        public async Task TheResourceLayerIsGivenThePathAndQuery()
        {

            var response = await Exchange("GET http://example.test/res?x=1 HTTP/1.1\r\nHost: example.test\r\n\r\n");

            Assert.That(response,     Is.EqualTo((200, "resource")));
            Assert.That(resourcePath, Is.EqualTo("/res?x=1"));

        }

        #endregion

        #region TheParserOutsideTheServerAgrees()

        /// <summary>
        /// HTTPRequest.TryParse outside the server has a parse path of its own,
        /// and H-30 was three of them disagreeing about the same octets.
        /// </summary>
        [Test]
        public void TheParserOutsideTheServerAgrees()
        {

            Assert.That(HTTPRequest.TryParse("GET http://example.test/a?x=1 HTTP/1.1\r\nHost: other.test\r\n\r\n", out var request),
                        Is.True);

            Assert.That(request!.Host.ToString(),   Is.EqualTo("example.test"));
            Assert.That(request.Path.ToString(),    Is.EqualTo("/a"));
            Assert.That(request.TargetScheme,       Is.EqualTo("http"));
            Assert.That(request.OriginFormTarget,   Is.EqualTo("/a?x=1"));
            Assert.That(request.RequestTarget,      Is.EqualTo("http://example.test/a?x=1"));

        }

        #endregion

    }

}
