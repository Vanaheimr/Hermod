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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The HTTP/1.x client following a redirection (RFC 9110, Section 15.4) when
    /// its MaxRedirects lets it, with the rewriting rules of HTTPRedirect, which
    /// the HTTP/2 client had used alone until H-38.
    /// </summary>
    [TestFixture]
    public class HTTPClientRedirectTests
    {

        #region (private) Helpers

        private static HTTPResponse Redirect(HTTPRequest     Request,
                                             HTTPStatusCode  Status,
                                             String          Location)
        {

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = Status,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "moved".ToUTF8Bytes()
                           };

            response.SetHeaderField("Location", Location);

            return response.AsImmutable;

        }

        private static HTTPResponse Text(HTTPRequest  Request,
                                         String       Text)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.OK,
                   ContentType     = HTTPContentType.Text.PLAIN,
                   Content         = Text.ToUTF8Bytes()
               }.AsImmutable;

        /// <summary>
        /// What a handler saw: the method, the query, the content length field
        /// and the body.
        /// </summary>
        private static String Describe(HTTPRequest Request)

            => $"{Request.HTTPMethod} {Request.Path}{Request.QueryString} " +
               $"length={Request.GetHeaderField("Content-Length") ?? "-"} " +
               $"type={Request.GetHeaderField("Content-Type") ?? "-"} " +
               $"body={Encoding.UTF8.GetString(Request.HTTPBody ?? [])}";

        private static async Task<(HTTPServer Server, HTTPClient Client)> Start(Action<HTTPAPI>  Configure,
                                                                                Byte             MaxRedirects)
        {

            var server = await HTTPServer.StartNew();

            Configure(server.AddHTTPAPI());

            var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}")) {
                             MaxRedirects = MaxRedirects
                         };

            return (server, client);

        }

        #endregion


        #region A same-origin redirect is followed

        [Test]
        public async Task ASameOriginRedirectIsFollowed()
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "old", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Redirect(request, HTTPStatusCode.MovedPermanently, "/new")));
                                       api.AddHandler(HTTPPath.Root + "new", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, "the new place")));
                                   }, MaxRedirects: 5);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "old", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,              Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String,        Is.EqualTo("the new place"));
                Assert.That(response.RedirectChain,               Is.EqualTo(new[] { $"http://127.0.0.1:{server.TCPPort}/new" }));
                Assert.That(response.HTTPRequest?.Path.ToString(), Is.EqualTo("/new"));
            });

        }

        #endregion

        #region A relative Location resolves against the request, and keeps its query

        /// <summary>
        /// RFC 9110, Section 10.2.2: a relative reference, resolved against the
        /// target of the request that was redirected (RFC 3986, Section 5).
        /// </summary>
        [Test]
        public async Task ARelativeLocationResolvesAgainstTheRequestAndKeepsItsQuery()
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "dir/old", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Redirect(request, HTTPStatusCode.Found, "new?a=1&b=two")));
                                       api.AddHandler(HTTPPath.Root + "dir/new", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, Describe(request))));
                                   }, MaxRedirects: 5);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "dir/old", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String, Does.StartWith("GET /dir/new?a=1&b=two "));
            });

        }

        #endregion

        #region POST becomes GET after 301, 302 and 303, and keeps its body after 307 and 308

        [TestCase(301, "GET /target length=- type=- body=")]
        [TestCase(302, "GET /target length=- type=- body=")]
        [TestCase(303, "GET /target length=- type=- body=")]
        [TestCase(307, "POST /target length=5 type=text/plain; charset=utf-8 body=hello")]
        [TestCase(308, "POST /target length=5 type=text/plain; charset=utf-8 body=hello")]
        public async Task PostIsRewrittenAsTheStatusSays(Int32 Status, String Expected)
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "form", HTTPMethod: HTTPMethod.POST,
                                                      HTTPDelegate: request => Task.FromResult(Redirect(request, HTTPStatusCode.ParseUInt16((UInt16) Status), "/target")));
                                       api.AddHandler(HTTPPath.Root + "target", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, Describe(request))));
                                       api.AddHandler(HTTPPath.Root + "target", HTTPMethod: HTTPMethod.POST,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, Describe(request))));
                                   }, MaxRedirects: 5);

            await using var _ = server;
            using var __ = client;

            var response = await client.POST(HTTPPath.Root + "form",
                                             "hello".ToUTF8Bytes(),
                                             HTTPContentType.Text.PLAIN,
                                             RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String, Does.StartWith(Expected));
            });

        }

        #endregion

        #region A redirect to another origin is handed back unfollowed

        [Test]
        public async Task ARedirectToAnotherOriginIsHandedBackUnfollowed()
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "away", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Redirect(request, HTTPStatusCode.Found, "https://example.invalid/elsewhere")));
                                   }, MaxRedirects: 5);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "away", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,               Is.EqualTo(HTTPStatusCode.Found));
                Assert.That(response.GetHeaderField("Location"),   Is.EqualTo("https://example.invalid/elsewhere"));
                Assert.That(response.RedirectChain,                Is.Empty);
            });

        }

        #endregion

        #region The hop limit ends a loop

        [Test]
        public async Task TheHopLimitEndsALoop()
        {

            var requests = 0;

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "loop", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => {
                                                          Interlocked.Increment(ref requests);
                                                          return Task.FromResult(Redirect(request, HTTPStatusCode.Found, "/loop"));
                                                      });
                                   }, MaxRedirects: 3);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "loop", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.Found));
                Assert.That(response.RedirectChain,        Has.Count.EqualTo(3));
                Assert.That(requests,                      Is.EqualTo(4), "the request and three hops");
            });

        }

        #endregion

        #region 300 and 304 are not followed

        [TestCase(300)]
        [TestCase(304)]
        public async Task MultipleChoicesAndNotModifiedAreNotFollowed(Int32 Status)
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "choice", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => {
                                                          var response = new HTTPResponse.Builder(request) {
                                                                             HTTPStatusCode = HTTPStatusCode.ParseUInt16((UInt16) Status)
                                                                         };
                                                          response.SetHeaderField("Location", "/target");
                                                          return Task.FromResult(response.AsImmutable);
                                                      });
                                       api.AddHandler(HTTPPath.Root + "target", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, "followed")));
                                   }, MaxRedirects: 5);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "choice", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.That(response.HTTPStatusCode.Code, Is.EqualTo((UInt32) Status));

        }

        #endregion

        #region Without MaxRedirects nothing is followed

        [Test]
        public async Task WithoutMaxRedirectsNothingIsFollowed()
        {

            var (server, client) = await Start(api => {
                                       api.AddHandler(HTTPPath.Root + "old", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Redirect(request, HTTPStatusCode.MovedPermanently, "/new")));
                                       api.AddHandler(HTTPPath.Root + "new", HTTPMethod: HTTPMethod.GET,
                                                      HTTPDelegate: request => Task.FromResult(Text(request, "followed")));
                                   }, MaxRedirects: 0);

            await using var _ = server;
            using var __ = client;

            var response = await client.GET(HTTPPath.Root + "old", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.MovedPermanently));
                Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("moved"));
                Assert.That(response.RedirectChain,        Is.Empty);
            });

        }

        #endregion

        #region A response whose body is an event stream is handed back as it is

        /// <summary>
        /// This client reads every body before it returns, except an event
        /// stream's: that body is the connection from then on, and need never
        /// end. A 3xx or a 401 that comes as one is handed back, neither followed
        /// nor answered. Reading it first would wait for good, and sending
        /// another request would go out on a connection the stream now owns.
        /// </summary>
        [TestCase("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Type: text/event-stream\r\n\r\n",                                 302)]
        [TestCase("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Type: text/event-stream\r\n\r\n", 401)]
        public async Task AResponseWhoseBodyIsAnEventStreamIsHandedBackAsItIs(String Answer, Int32 Status)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port     = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);
            var requests = 0;
            var done     = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var served = Task.Run(async () => {

                using var tcp    = await listener.AcceptTcpClientAsync();
                var       stream = tcp.GetStream();
                var       head   = new StringBuilder();
                var       one    = new Byte[1];

                while (!head.ToString().EndsWith("\r\n\r\n") &&
                       await stream.ReadAsync(one) == 1)
                {
                    head.Append((Char) one[0]);
                }

                Interlocked.Increment(ref requests);

                await stream.WriteAsync(Encoding.ASCII.GetBytes(Answer));
                await stream.FlushAsync();

                // An event stream that has not sent anything yet, and stays open.
                await done.Task;

            });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{port}")) {
                                   MaxRedirects  = 5,
                                   Credentials   = HTTPClientCredentials.UserNameAndPassword("alice", "secret")
                               };

            var response = await client.GET(HTTPPath.Root + "a", RequestTimeout: TimeSpan.FromSeconds(5));

            done.SetResult();
            await served.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode.Code,  Is.EqualTo((UInt32) Status));
                Assert.That(response.HTTPBodyStream,       Is.Not.Null, "the stream is the caller's");
                Assert.That(response.RedirectChain,        Is.Empty);
                Assert.That(requests,                      Is.EqualTo(1));
            });

            response.HTTPBodyStream?.Dispose();

        }

        #endregion

    }

}
