/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    /// <summary>
    /// Tests between Hermod HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests
    {

        #region Start/Stop HTTPServer

        private HTTPServer? httpServer;

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            httpServer = new HTTPServer(
                             IPPort.Parse(82),
                             Autostart: true
                         );

            #region /

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
                                         HTTPDelegate: Request => Task.FromResult(
                                                                       new HTTPResponse.Builder(Request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public void Shutdown_HTTPServer()
        {
            httpServer?.Shutdown();
        }

        #endregion


        #region HTTPClientTest_001()

        [Test]
        public async Task HTTPClientTest_001()
        {

            var httpClient    = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),               request);
            Assert.IsTrue (request.Contains("GET / HTTP/1.1"),      request);
            Assert.IsTrue (request.Contains("Host: 127.0.0.1:82"),  request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Access-Control-Allow-Headers:  Content-Type, Accept, Authorization
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),    response);
            Assert.IsTrue  (response.Contains("Hello World!"),       response);

            Assert.AreEqual("Hello World!",                          httpBody);

            Assert.AreEqual("Hello World!".Length,                   httpResponse.ContentLength);

        }

        #endregion

        #region HTTPClientTest_002()

        [Test]
        public async Task HTTPClientTest_002()
        {

            var httpClient    = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root,
                                                     requestbuilder => {
                                                         requestbuilder.Host = HTTPHostname.Localhost;
                                                     }).
                                                 ConfigureAwait(false);



            var request   = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: localhost

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),            request);
            Assert.IsTrue (request.Contains("GET / HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains("Host: localhost"),  request);



            var response  = httpResponse.EntirePDU;
            var httpBody  = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Access-Control-Allow-Headers:  Content-Type, Accept, Authorization
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),  response);
            Assert.IsTrue  (response.Contains("Hello World!"),     response);

            Assert.AreEqual("Hello World!",                        httpBody);

            Assert.AreEqual("Hello World!".Length,                 httpResponse.ContentLength);

        }

        #endregion


    }

}
