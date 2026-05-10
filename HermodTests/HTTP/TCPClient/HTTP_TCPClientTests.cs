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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Hermod HTTP server via TCP client tests, to simulate strange network behaviors.
    /// </summary>
    [TestFixture]
    public class HTTP_TCPClientTests
    {

        #region Constants

        private const String _400_BadRequest               = "400 Bad Request";
        private const String _404_NotFound                 = "404 Not Found";
        private const String _405_MethodNotAllowed         = "405 Method Not Allowed";

        private const String _500_InternalServerError      = "500 Internal Server Error";
        private const String _505_HTTPVersionNotSupported  = "505 HTTP Version Not Supported";

        #endregion

        #region Start/Stop HTTPServer

        private HTTPTestServerX? httpServer;
        private HTTPAPI?         httpAPI;

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            httpServer = new HTTPTestServerX(
                             TCPPort: IPPort.Zero,
                             AutoStart: true
                          );

            httpAPI = new HTTPAPI(
                          httpServer
                      );

            #region /

            httpAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: Request => Task.FromResult(
                                                             new HTTPResponse.Builder(Request) {
                                                                 HTTPStatusCode             = HTTPStatusCode.OK,
                                                                 Server                     = "Test Server",
                                                                 Date                       = Timestamp.Now,
                                                                 AccessControlAllowOrigin   = "*",
                                                                 AccessControlAllowMethods  = [ "GET" ],
                                                                 AccessControlAllowHeaders  = [ "Content-Type", "Accept", "Authorization" ],
                                                                 ContentType                = HTTPContentType.Text.PLAIN,
                                                                 Content                    = "Hello World!".ToUTF8Bytes(),
                                                                 Connection                 = ConnectionType.Close
                                                             }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public async Task Shutdown_HTTPServer()
        {
            if (httpServer is not null)
                await httpServer.Stop();
        }

        #endregion

        #region NewTCPClientRequest()

        private TCPClientRequest NewTCPClientRequest()

            => new ("localhost", httpServer?.TCPPort.ToInt32() ?? throw new InvalidOperationException("HTTP server was not initialized!"));

        #endregion


        #region HTTP_TCPClientTest_HTTP1_1()

        [Test]
        public void HTTP_TCPClientTest_HTTP1_1()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains("200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_HTTP2_0()

        [Test]
        public void HTTP_TCPClientTest_HTTP2_0()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/2.0\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_505_HTTPVersionNotSupported), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_InvalidHTTPVersion1()

        [Test]
        public void HTTP_TCPClientTest_InvalidHTTPVersion1()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP 2.0\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_400_BadRequest), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_InvalidHTTPVersion2()

        [Test]
        public void HTTP_TCPClientTest_InvalidHTTPVersion2()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/1\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_400_BadRequest), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_InvalidHTTPVersion3()

        [Test]
        public void HTTP_TCPClientTest_InvalidHTTPVersion3()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTo/2.0\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_400_BadRequest), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_SlowClient()

        [Test]
        public void HTTP_TCPClientTest_SlowClient()
        {

            var response = NewTCPClientRequest().
                           Send("GE").
                           Wait(100).
                           Send("T / HTTo/2.0\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_400_BadRequest), Is.True, response);

        }

        #endregion

        #region HTTP_TCPClientTest_InvalidHTTPMethod()

        [Test]
        public void HTTP_TCPClientTest_InvalidHTTPMethod()
        {

            var response = NewTCPClientRequest().
                           Send("GETTT / HTTP/1.1\r\nHost: localhost\r\nConnection: close").
                           FinishCurrentRequest().
                           Response;

            Assert.That(response.Contains(_405_MethodNotAllowed), Is.True, response);

        }

        #endregion

    }

}
