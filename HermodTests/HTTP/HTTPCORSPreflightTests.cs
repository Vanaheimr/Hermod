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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// CORS preflights, which routing answers before any handler is reached.
    ///
    /// A browser is the only client that sends one, so these are raw-socket
    /// tests: the thing under test is not reachable by a client that does not
    /// implement the Fetch rules.
    /// </summary>
    [TestFixture]
    public class HTTPCORSPreflightTests
    {

        #region (private) CreateServer(Policy = null)

        private static HTTPServer CreateServer(CORSPolicy? Policy = null)
        {

            var server   = new HTTPServer(
                               IPv4Address.Localhost,
                               IPPort.Parse(0),
                               AutoStart: true
                           );

            var httpAPI  = server.AddHTTPAPI();

            foreach (var method in new[] { HTTPMethod.GET, HTTPMethod.POST })
                httpAPI.AddHandler(HTTPPath.Root + "cors",
                                   HTTPMethod:    method,
                                   HTTPDelegate:  request => Task.FromResult(
                                                      new HTTPResponse.Builder(request) {
                                                          HTTPStatusCode            = HTTPStatusCode.OK,
                                                          ContentType               = HTTPContentType.Text.PLAIN,
                                                          AccessControlAllowOrigin  = "*",
                                                          Content                   = "ok".ToUTF8Bytes()
                                                      }.AsImmutable
                                                  ));

            if (Policy is not null)
                server.AddPipeline(new HTTPCORSPipeline(Policy));

            return server;

        }

        #endregion

        #region (private) ReadAll(Port, Request)

        private static async Task<String> ReadAll(IPPort Port, String Request)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));

            using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var received  = new MemoryStream();
            var       buffer    = new Byte[4096];

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                    break;
                received.Write(buffer, 0, read);
            }

            return Encoding.ASCII.GetString(received.ToArray());

        }

        #endregion

        #region (private) Preflight(Port, Origin, Method, Headers = null)

        private static Task<String> Preflight(IPPort   Port,
                                              String   Origin,
                                              String   Method,
                                              String?  Headers   = null)

            => ReadAll(
                   Port,
                   "OPTIONS /cors HTTP/1.1\r\nHost: localhost\r\n" +
                   "Origin: " + Origin + "\r\n" +
                   "Access-Control-Request-Method: " + Method + "\r\n" +
                   (Headers is not null ? "Access-Control-Request-Headers: " + Headers + "\r\n" : "") +
                   "Connection: close\r\n\r\n"
               );

        #endregion


        #region Without a pipeline the preflight is not answered

        /// <summary>
        /// The state this fixture exists to pin as a decision rather than an
        /// accident: a server that installs no CORS pipeline does not do CORS.
        /// The preflight gets the automatic OPTIONS answer and no
        /// Access-Control-Allow-*, which is a refusal as far as a browser is
        /// concerned.
        /// </summary>
        [Test]
        public async Task Without_A_Pipeline_No_Preflight_Is_Answered()
        {

            var server = CreateServer();

            try
            {

                var response = await Preflight(server.TCPPort, "https://example.org", "POST");

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(response, Does.Not.Contain("Access-Control-Allow-Origin"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A permitted preflight is answered

        [Test]
        public async Task Permitted_Preflight_Is_Answered()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins:  [ "https://example.org" ],
                                          AllowedMethods:  [ HTTPMethod.GET, HTTPMethod.POST ],
                                          AllowedHeaders:  [ "X-Demo" ],
                                          MaxAge:          TimeSpan.FromMinutes(10)
                                      ));

            try
            {

                var response = await Preflight(server.TCPPort, "https://example.org", "POST", "x-demo");

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(response, Does.Contain("Access-Control-Allow-Origin: https://example.org"), response);
                Assert.That(response, Does.Match("(?mi)^Access-Control-Allow-Methods: .*POST.*$"), response);
                Assert.That(response, Does.Match("(?mi)^Access-Control-Allow-Headers: .*X-Demo.*$"), response);
                Assert.That(response, Does.Contain("Access-Control-Max-Age: 600"), response);

                // The answer depends on who asked, so a cache that ignored Origin
                // would hand one origin permission granted to another.
                Assert.That(response, Does.Match("(?mi)^Vary: .*Origin.*$"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Refusals

        [Test]
        public async Task An_Unlisted_Origin_Is_Refused()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins: [ "https://example.org" ],
                                          AllowedMethods: [ HTTPMethod.POST ]
                                      ));

            try
            {

                var response = await Preflight(server.TCPPort, "https://evil.example", "POST");

                // 403 rather than falling through to the automatic 204: a success
                // the browser then rejects for a missing header reads in a console
                // as "the server is broken", not as "the server said no".
                Assert.That(response, Does.StartWith("HTTP/1.1 403"), response);
                Assert.That(response, Does.Not.Contain("Access-Control-Allow-Origin"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        [Test]
        public async Task An_Unlisted_Method_Is_Refused()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins: [ "https://example.org" ],
                                          AllowedMethods: [ HTTPMethod.GET ]
                                      ));

            try
            {
                Assert.That(await Preflight(server.TCPPort, "https://example.org", "DELETE"),
                            Does.StartWith("HTTP/1.1 403"));
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        [Test]
        public async Task An_Unlisted_Header_Is_Refused()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins: [ "https://example.org" ],
                                          AllowedMethods: [ HTTPMethod.POST ],
                                          AllowedHeaders: [ "X-Demo" ]
                                      ));

            try
            {
                Assert.That(await Preflight(server.TCPPort, "https://example.org", "POST", "x-demo, x-secret"),
                            Does.StartWith("HTTP/1.1 403"));
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A wildcard policy with credentials echoes the origin

        /// <summary>
        /// WHATWG Fetch rejects "*" together with Access-Control-Allow-Credentials,
        /// and a browser discards the whole answer. A wildcard policy that allows
        /// credentials therefore has to echo the concrete origin instead — which
        /// is also why Vary: Origin is not optional here.
        /// </summary>
        [Test]
        public async Task Wildcard_With_Credentials_Echoes_The_Concrete_Origin()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins:    [ "*" ],
                                          AllowedMethods:    [ HTTPMethod.POST ],
                                          AllowCredentials:  true
                                      ));

            try
            {

                var response = await Preflight(server.TCPPort, "https://example.org", "POST");

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(response, Does.Contain("Access-Control-Allow-Origin: https://example.org"), response);
                Assert.That(response, Does.Not.Contain("Access-Control-Allow-Origin: *"), response);
                Assert.That(response, Does.Contain("Access-Control-Allow-Credentials: true"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region An ordinary OPTIONS still reaches the router

        /// <summary>
        /// The pipeline must not swallow every OPTIONS. Without the Fetch
        /// conditions the request is an ordinary one, and the router answers it
        /// per RFC 9110 §9.3.7.
        /// </summary>
        [Test]
        public async Task An_Ordinary_OPTIONS_Is_Not_Treated_As_A_Preflight()
        {

            var server = CreateServer(new CORSPolicy(
                                          AllowedOrigins: [ "https://example.org" ],
                                          AllowedMethods: [ HTTPMethod.POST ]
                                      ));

            try
            {

                var response = await ReadAll(
                                   server.TCPPort,
                                   "OPTIONS /cors HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(response, Does.Match("(?m)^Allow: .*GET.*$"), response);
                Assert.That(response, Does.Not.Contain("Access-Control-Allow-Origin"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
