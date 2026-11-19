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
    /// The HTTP/1.x client answering a 401 with its Credentials (RFC 9110,
    /// Section 11), through HTTPClientAuthenticator, which the HTTP/2 client had
    /// used alone until H-38. Digest is answered by this client and checked by
    /// this stack's DigestAuthenticationScheme, so the two ends of RFC 7616 are
    /// tested against each other.
    /// </summary>
    [TestFixture]
    public class HTTPClientChallengeTests
    {

        #region Data

        private const String Realm = "Hermod test";

        private static readonly HTTPClientCredentials Alice = HTTPClientCredentials.UserNameAndPassword("alice", "secret");

        #endregion

        #region (private) Helpers

        private static HTTPResponse Text(HTTPRequest  Request,
                                         String       Text)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.OK,
                   ContentType     = HTTPContentType.Text.PLAIN,
                   Content         = Text.ToUTF8Bytes()
               }.AsImmutable;

        private static HTTPResponse Unauthorized(HTTPRequest  Request,
                                                 String       Challenge)
        {

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "unauthorized".ToUTF8Bytes()
                           };

            response.SetHeaderField("WWW-Authenticate", Challenge);

            return response.AsImmutable;

        }

        /// <summary>
        /// A server whose /secret takes Basic or Bearer, and counts what it is sent.
        /// </summary>
        private sealed class BasicOrBearerServer : IAsyncDisposable
        {

            private Int32 requests;

            public HTTPServer   Server         { get; }
            public Int32        Requests       => requests;
            public List<String> Authorizations { get; } = [];

            private BasicOrBearerServer(HTTPServer Server)
            {
                this.Server = Server;
            }

            public static async Task<BasicOrBearerServer> Start(String Challenge)
            {

                var server = new BasicOrBearerServer(await HTTPServer.StartNew());
                var api    = server.Server.AddHTTPAPI();

                foreach (var method in new[] { HTTPMethod.GET, HTTPMethod.POST })
                    api.AddHandler(HTTPPath.Root + "secret", HTTPMethod: method,
                                   HTTPDelegate: request => {

                                       Interlocked.Increment(ref server.requests);

                                       lock (server.Authorizations)
                                           server.Authorizations.Add(request.GetHeaderField("Authorization") ?? "-");

                                       var authorized = request.Authorization switch {
                                                            HTTPBasicAuthentication  basic   => basic.Username == "alice" && basic.Password == "secret",
                                                            HTTPBearerAuthentication bearer  => bearer.Token   == "valid-token-123",
                                                            _                                => false
                                                        };

                                       return Task.FromResult(
                                                  authorized
                                                      ? Text(request, $"{request.HTTPMethod} body={Encoding.UTF8.GetString(request.HTTPBody ?? [])}")
                                                      : Unauthorized(request, Challenge)
                                              );

                                   });

                // Not counted: the way into /secret for the redirect test.
                api.AddHandler(HTTPPath.Root + "start", HTTPMethod: HTTPMethod.GET,
                               HTTPDelegate: request => {
                                   var response = new HTTPResponse.Builder(request) { HTTPStatusCode = HTTPStatusCode.Found };
                                   response.SetHeaderField("Location", "/secret");
                                   return Task.FromResult(response.AsImmutable);
                               });

                return server;

            }

            public HTTPClient Client(HTTPClientCredentials? Credentials)

                => new (URL.Parse($"http://127.0.0.1:{Server.TCPPort}")) {
                       Credentials = Credentials
                   };

            public ValueTask DisposeAsync()
                => Server.DisposeAsync();

        }

        #endregion


        #region Basic and Bearer are answered

        [Test]
        public async Task BasicIsAnswered()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\", charset=\"UTF-8\"");
            using var client       = server.Client(Alice);

            var response = await client.GET(HTTPPath.Root + "secret", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(server.Requests,               Is.EqualTo(2), "the request and its answer");
                Assert.That(server.Authorizations[0],      Is.EqualTo("-"), "nothing is sent before it is asked for");
                Assert.That(server.Authorizations[1],      Does.StartWith("Basic "));
            });

        }

        [Test]
        public async Task BearerIsAnswered()
        {

            await using var server = await BasicOrBearerServer.Start($"Bearer realm=\"{Realm}\"");
            using var client       = server.Client(HTTPClientCredentials.BearerToken("valid-token-123"));

            var response = await client.GET(HTTPPath.Root + "secret", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(server.Authorizations,         Is.EqualTo(new[] { "-", "Bearer valid-token-123" }));
            });

        }

        #endregion

        #region A request body is sent again with the answer

        [Test]
        public async Task ARequestBodyIsSentAgainWithTheAnswer()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\"");
            using var client       = server.Client(Alice);

            var response = await client.POST(HTTPPath.Root + "secret",
                                             "hello".ToUTF8Bytes(),
                                             HTTPContentType.Text.PLAIN,
                                             RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("POST body=hello"));
            });

        }

        #endregion

        #region Digest is answered, and checked by this stack's own server half

        [TestCase("SHA-256")]
        [TestCase("MD5")]
        public async Task DigestIsAnswered(String Algorithm)
        {

            var scheme = new DigestAuthenticationScheme(
                             Realm,
                             (username, cancellationToken) => Task.FromResult<String?>(username == "alice" ? "secret" : null),
                             Algorithm
                         );

            await using var server = await HTTPServer.StartNew();

            server.AddHTTPAPI().AddHandler(HTTPPath.Root + "digest", HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: async request => {

                    if (request.Authorization is HTTPDigestAuthentication digest &&
                        await scheme.AuthenticateAsync(digest.Credentials,
                                                       request.HTTPMethod,
                                                       request.RequestTarget,
                                                       request.CancellationToken) is not null)
                    {
                        return Text(request, "the secret");
                    }

                    return Unauthorized(request, scheme.BuildChallenges(Realm, Algorithm));

                });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}")) {
                                   Credentials = Alice
                               };

            var first  = await client.GET(HTTPPath.Root + "digest", RequestTimeout: TimeSpan.FromSeconds(10));
            var second = await client.GET(HTTPPath.Root + "digest", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(first. HTTPStatusCode,         Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(first. HTTPBodyAsUTF8String,   Is.EqualTo("the secret"));
                Assert.That(second.HTTPStatusCode,         Is.EqualTo(HTTPStatusCode.OK), "a fresh challenge on the next request is answered as well");
                Assert.That(first. HTTPRequest?.GetHeaderField("Authorization"), Does.StartWith("Digest username=\"alice\""));
            });

        }

        #endregion

        #region A Digest answer is not carried to the next hop

        /// <summary>
        /// Digest signs the request-target, so the Authorization field computed
        /// for /area/a is wrong for /area/b. The follow-up is built from the
        /// request that was redirected, without it, and /area/b's own challenge
        /// is answered. Built from the answered request instead, it carries the
        /// field for /area/a, is refused, and is not answered for again.
        /// </summary>
        [Test]
        public async Task ADigestAnswerIsNotCarriedToTheNextHop()
        {

            var scheme = new DigestAuthenticationScheme(
                             Realm,
                             (username, cancellationToken) => Task.FromResult<String?>(username == "alice" ? "secret" : null)
                         );

            await using var server = await HTTPServer.StartNew();

            var api = server.AddHTTPAPI();

            foreach (var name in new[] { "a", "b" })
                api.AddHandler(HTTPPath.Root + "area" + name, HTTPMethod: HTTPMethod.GET,
                    HTTPDelegate: async request => {

                        if (request.Authorization is not HTTPDigestAuthentication digest ||
                            await scheme.AuthenticateAsync(digest.Credentials,
                                                           request.HTTPMethod,
                                                           request.RequestTarget,
                                                           request.CancellationToken) is null)
                        {
                            return Unauthorized(request, scheme.BuildChallenges(Realm, "SHA-256"));
                        }

                        if (name == "b")
                            return Text(request, "arrived");

                        var moved = new HTTPResponse.Builder(request) { HTTPStatusCode = HTTPStatusCode.Found };
                        moved.SetHeaderField("Location", "/area/b");
                        return moved.AsImmutable;

                    });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}")) {
                                   Credentials   = Alice,
                                   MaxRedirects  = 2
                               };

            var response = await client.GET(HTTPPath.Root + "area" + "a", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("arrived"));
                Assert.That(response.HTTPRequest?.GetHeaderField("Authorization"), Does.Contain("uri=\"/area/b\""));
            });

        }

        #endregion

        #region The strongest scheme offered is the one answered

        /// <summary>
        /// Two WWW-Authenticate field lines, Basic first. Basic hands the password
        /// over; Digest does not. The client must answer the second.
        /// </summary>
        [Test]
        public async Task TheStrongestSchemeOfferedIsTheOneAnswered()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port     = ((IPEndPoint) listener.LocalEndpoint).Port;
            var requests = new List<String>();

            var served = Task.Run(async () => {

                using var tcp    = await listener.AcceptTcpClientAsync();
                var       stream = tcp.GetStream();

                foreach (var answer in new[] {
                                           "HTTP/1.1 401 Unauthorized\r\n" +
                                           $"WWW-Authenticate: Basic realm=\"{Realm}\"\r\n" +
                                           $"WWW-Authenticate: Digest realm=\"{Realm}\", qop=\"auth\", algorithm=SHA-256, nonce=\"abc\"\r\n" +
                                           "Content-Length: 0\r\n\r\n",
                                           "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"
                                       })
                {

                    var head = new StringBuilder();
                    var one  = new Byte[1];

                    while (!head.ToString().EndsWith("\r\n\r\n") &&
                           await stream.ReadAsync(one) == 1)
                    {
                        head.Append((Char) one[0]);
                    }

                    requests.Add(head.ToString());

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(answer));

                }

            });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{port}")) {
                                   Credentials = Alice
                               };

            var response = await client.GET(HTTPPath.Root + "secret", RequestTimeout: TimeSpan.FromSeconds(10));

            await served.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(requests,                      Has.Count.EqualTo(2));
                Assert.That(requests[1],                   Does.Contain("\r\nAuthorization: Digest username=\"alice\""));
                Assert.That(requests[1],                   Does.Contain("uri=\"/secret\""));
            });

        }

        #endregion

        #region Wrong credentials surface the 401 without a loop

        [Test]
        public async Task WrongCredentialsSurfaceThe401WithoutALoop()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\"");
            using var client       = server.Client(HTTPClientCredentials.UserNameAndPassword("alice", "wrong"));

            var response = await client.GET(HTTPPath.Root + "secret", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.Unauthorized));
                Assert.That(server.Requests,               Is.EqualTo(2), "one answer, not a second");
            });

        }

        #endregion

        #region The caller's own Authorization is not overridden

        [Test]
        public async Task TheCallersOwnAuthorizationIsNotOverridden()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\"");
            using var client       = server.Client(Alice);

            var response = await client.GET(HTTPPath.Root + "secret",
                                            Authentication: HTTPBasicAuthentication.Create("alice", "wrong"),
                                            RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.Unauthorized));
                Assert.That(server.Requests,               Is.EqualTo(1));
            });

        }

        #endregion

        #region Without Credentials nothing changes

        [Test]
        public async Task WithoutCredentialsNothingChanges()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\"");
            using var client       = server.Client(null);

            var response = await client.GET(HTTPPath.Root + "secret", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.Unauthorized));
                Assert.That(server.Requests,               Is.EqualTo(1));
            });

        }

        #endregion

        #region A redirect into an authenticated area is answered on arrival

        [Test]
        public async Task ARedirectIntoAnAuthenticatedAreaIsAnsweredOnArrival()
        {

            await using var server = await BasicOrBearerServer.Start($"Basic realm=\"{Realm}\"");
            using var client       = server.Client(Alice);
            client.MaxRedirects = 2;

            var response = await client.GET(HTTPPath.Root + "start", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.RedirectChain,        Has.Count.EqualTo(1));
                Assert.That(server.Authorizations,         Has.Count.EqualTo(2));
            });

        }

        #endregion

    }

}
