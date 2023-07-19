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

            #region GET   /

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region POST  /mirror/queryString

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "queryString",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = request.QueryString.GetString("q", "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region POST  /mirror/httpBody

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion


            #region POST  /mirrorBody2

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirrorBody2",
                                         HTTPDelegate: async request => {

                                             var queryParameter = request.HTTPBodyAsUTF8String ?? "";

                                             return new HTTPResponse.Builder(request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = "Test Server",
                                                        Date                       = Timestamp.Now,
                                                        AccessControlAllowOrigin   = "*",
                                                        AccessControlAllowMethods  = new[] { "GET" },
                                                        AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = queryParameter.Reverse().ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable;

                                         });

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



        #region HTTPClientTest_003()

        [Test]
        public async Task HTTPClientTest_003()
        {

            var httpClient    = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse  = await httpClient.POST(HTTPPath.Root + "mirror" + "queryString?q=abcdefgh").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/queryString?q=abcdefgh HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Length:  0

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                                         request);
            Assert.IsTrue (request.Contains("POST /mirror/queryString?q=abcdefgh HTTP/1.1"),  request);
            Assert.IsTrue (request.Contains("Host: 127.0.0.1:82"),                            request);
            // 'Content-Length: 0' is a recommended header for HTTP/1.1 POST requests without a body!
            Assert.IsTrue (request.Contains("Content-Length: 0"),                             request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                8
            // Connection:                    close
            // 
            // hgfedcba

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),    response);
            Assert.IsTrue  (response.Contains("hgfedcba"),          response);

            Assert.AreEqual("hgfedcba",                          httpBody);

            Assert.AreEqual("hgfedcba".Length,                   httpResponse.ContentLength);

        }

        #endregion

        #region HTTPClientTest_004()

        [Test]
        public async Task HTTPClientTest_004()
        {

            var httpClient    = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse  = await httpClient.POST(HTTPPath.Root + "mirror" + "httpBody",
                                                      request => {
                                                          request.ContentType  = HTTPContentType.TEXT_UTF8;
                                                          request.Content      = "123456789".ToUTF8Bytes();
                                                      }).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789


            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                           request);
            Assert.IsTrue (request.Contains("POST /mirror/httpBody HTTP/1.1"),  request);
            Assert.IsTrue (request.Contains("Host: 127.0.0.1:82"),              request);
            Assert.IsTrue (request.Contains("Content-Length: 9"),               request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                9
            // Connection:                    close
            // 
            // 987654321

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),    response);
            Assert.IsTrue  (response.Contains("987654321"),          response);

            Assert.AreEqual("987654321",                             httpBody);

            Assert.AreEqual("987654321".Length,                      httpResponse.ContentLength);

        }

        #endregion


    }

}
