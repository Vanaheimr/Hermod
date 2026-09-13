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
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.SOAP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Handlers registered at the HTTP server itself take server-wide URL
    /// templates and land in the HTTP API owning the path, so they are
    /// reachable like handlers registered at that API directly.
    /// </summary>
    [TestFixture]
    public class HTTPServerHandlerTests
    {

        #region (private) Text(Request, Text)

        private static Task<HTTPResponse> Text(HTTPRequest  Request,
                                               String       Text)

            => Task.FromResult(
                   new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.OK,
                       ContentType     = HTTPContentType.Text.PLAIN,
                       Content         = Text.ToUTF8Bytes(),
                       Connection      = ConnectionType.Close
                   }.AsImmutable
               );

        #endregion

        #region (private) NewClient(Server)

        private static HttpClient NewClient(HTTPServer Server)

            => new (new HttpClientHandler { AllowAutoRedirect = false }) {
                   BaseAddress = new Uri($"http://127.0.0.1:{Server.TCPPort}/")
               };

        #endregion


        #region AddMethodCallback_Without_An_API_Creates_The_Root_API()

        [Test]
        public async Task AddMethodCallback_Without_An_API_Creates_The_Root_API()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                server.AddMethodCallback(
                    HTTPHostname.Any,
                    HTTPMethod.GET,
                    HTTPPath.Parse("/hello"),
                    HTTPDelegate:  request => Text(request, "hello")
                );

                using var client    = NewClient(server);
                var       response  = await client.GetAsync("hello");

                Assert.That(response.StatusCode,                                             Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await response.Content.ReadAsStringAsync(),                      Is.EqualTo("hello"));
                Assert.That(server.TryGetHTTPAPI(HTTPPath.Root, out var rootAPI),            Is.True,  "a default HTTP API at '/' was created");
                Assert.That(rootAPI?.GetRequestHandle(HTTPPath.Parse("/hello")).RouteNode,   Is.Not.Null);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region AddMethodCallback_Registers_At_The_API_Owning_The_Path()

        [Test]
        public async Task AddMethodCallback_Registers_At_The_API_Owning_The_Path()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                var rootAPI  = new HTTPAPI(server);
                var subAPI   = server.AddHTTPAPI(HTTPPath.Parse("/api"));

                // No HTTP API given: the most specific API for the path takes the handler.
                server.AddMethodCallback(
                    HTTPHostname.Any,
                    HTTPMethod.GET,
                    HTTPPath.Parse("/api/ping"),
                    HTTPDelegate:  request => Text(request, "pong")
                );

                // An HTTP API given: the template is still server-wide.
                server.AddMethodCallback(
                    rootAPI,
                    HTTPHostname.Any,
                    HTTPMethod.GET,
                    HTTPPath.Parse("/ping"),
                    HTTPDelegate:  request => Text(request, "root pong")
                );

                Assert.That(subAPI. GetRequestHandle(HTTPPath.Parse("/ping")).RouteNode,      Is.Not.Null,  "registered relative to /api");
                Assert.That(rootAPI.GetRequestHandle(HTTPPath.Parse("/api/ping")).RouteNode,  Is.Null,      "not registered at the root API");

                using var client = NewClient(server);

                Assert.That(await client.GetStringAsync("api/ping"),  Is.EqualTo("pong"));
                Assert.That(await client.GetStringAsync("ping"),      Is.EqualTo("root pong"));

                // A template outside the given HTTP API is refused.
                Assert.That(() => server.AddMethodCallback(
                                      subAPI,
                                      HTTPHostname.Any,
                                      HTTPMethod.GET,
                                      HTTPPath.Parse("/elsewhere"),
                                      HTTPDelegate:  request => Text(request, "never")
                                  ),
                            Throws.ArgumentException);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region OpenEnd_Widens_The_Last_Parameter_To_The_Rest_Of_The_Path()

        [Test]
        public async Task OpenEnd_Widens_The_Last_Parameter_To_The_Rest_Of_The_Path()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                server.AddMethodCallback(
                    HTTPHostname.Any,
                    HTTPMethod.GET,
                    HTTPPath.Parse("/files/{path}"),
                    OpenEnd:       true,
                    HTTPDelegate:  request => Text(request, request.ParsedURLParametersX["path"])
                );

                using var client = NewClient(server);

                Assert.That(await client.GetStringAsync("files/a/b/c.txt"),  Is.EqualTo("a/b/c.txt"));
                Assert.That(await client.GetStringAsync("files/single"),     Is.EqualTo("single"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region The_Register_Helpers_Are_Reachable()

        [Test]
        public async Task The_Register_Helpers_Are_Reachable()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                var api = new HTTPAPI(server);

                server.RegisterRAWRequestHandler      (api, HTTPHostname.Any, HTTPPath.Parse("/raw"));
                server.RegisterMovedPermanentlyHandler(api, HTTPHostname.Any, HTTPPath.Parse("/old"),   Location.From(HTTPPath.Parse("/new")));
                server.RegisterMovedTemporarilyHandler(api, HTTPHostname.Any, HTTPPath.Parse("/later"), Location.From(HTTPPath.Parse("/now")));

                using var client = NewClient(server);

                var raw = await client.GetAsync("raw");

                Assert.That(raw.StatusCode,                              Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await raw.Content.ReadAsStringAsync(),       Does.Contain("Method => GET"));

                var moved = await client.GetAsync("old");

                Assert.That(moved.StatusCode,                            Is.EqualTo(HttpStatusCode.MovedPermanently));
                Assert.That(moved.Headers.Location?.ToString(),          Is.EqualTo("/new"));

                var later = await client.GetAsync("later");

                Assert.That(later.StatusCode,                            Is.EqualTo(HttpStatusCode.TemporaryRedirect));
                Assert.That(later.Headers.Location?.ToString(),          Is.EqualTo("/now"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region SOAP_Delegates_With_Header_And_Body_Are_Reachable()

        [Test]
        public async Task SOAP_Delegates_With_Header_And_Body_Are_Reachable()
        {

            var server = await HTTPServer.StartNew();

            try
            {

                var api         = new HTTPAPI(server);
                var soapServer  = new SOAPServer(server);

                soapServer.RegisterSOAPDelegate(
                    api,
                    HTTPHostname.Any,
                    HTTPPath.Parse("/soap"),
                    "Ping",
                    xml => xml.Descendants().First(element => element.Name.LocalName == "Ping"),
                    (request, header, body) => Text(request, "pong")
                );

                using var client = NewClient(server);

                var info = await client.GetAsync("soap");

                Assert.That(info.StatusCode,                             Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await info.Content.ReadAsStringAsync(),      Does.Contain("Ping"));

                var pong = await client.PostAsync(
                               "soap",
                               new StringContent(
                                   "<Envelope xmlns=\"http://www.w3.org/2003/05/soap-envelope\"><Body><Ping/></Body></Envelope>",
                                   Encoding.UTF8,
                                   "application/soap+xml"
                               )
                           );

                Assert.That(pong.StatusCode,                             Is.EqualTo(HttpStatusCode.OK));
                Assert.That(await pong.Content.ReadAsStringAsync(),      Is.EqualTo("pong"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

    }

}
