/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using NUnit.Framework.Legacy;

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

        private HTTPServer? httpServer;

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            httpServer = new HTTPServer(
                             IPPort.Parse(81),
                             AutoStart: true
                         );

            #region /

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
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
        public void Shutdown_HTTPServer()
        {
            httpServer?.Shutdown();
        }

        #endregion

        #region NewTCPClientRequest()

        private static TCPClientRequest NewTCPClientRequest()

            => new ("localhost", 81);

        #endregion


        #region HTTP_TCPClientTest_001()

        [Test]
        public void HTTP_TCPClientTest_001()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/1.1\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains("200 OK"),       response);
            ClassicAssert.IsTrue(response.Contains("Hello World!"), response);

        }

        #endregion

        #region HTTP_TCPClientTest_002()

        [Test]
        public void HTTP_TCPClientTest_002()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/2.0\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_505_HTTPVersionNotSupported), response);

        }

        #endregion

        #region HTTP_TCPClientTest_003()

        [Test]
        public void HTTP_TCPClientTest_003()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP 2.0\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_400_BadRequest), response);

        }

        #endregion

        #region HTTP_TCPClientTest_004()

        [Test]
        public void HTTP_TCPClientTest_004()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTP/1\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_505_HTTPVersionNotSupported), response);

        }

        #endregion

        #region HTTP_TCPClientTest_005()

        [Test]
        public void HTTP_TCPClientTest_005()
        {

            var response = NewTCPClientRequest().
                           Send("GET / HTTo/2.0\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_400_BadRequest), response);

        }

        #endregion

        #region HTTP_TCPClientTest_006()

        [Test]
        public void HTTP_TCPClientTest_006()
        {

            var response = NewTCPClientRequest().
                           Send("GE").
                           Wait(100).
                           Send("T / HTTo/2.0\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_400_BadRequest), response);

        }

        #endregion

        #region HTTP_TCPClientTest_007()

        [Test]
        public void HTTP_TCPClientTest_007()
        {

            var response = NewTCPClientRequest().
                           Send("GETTT / HTTP/1.1\r\nHost: localhost").
                           FinishCurrentRequest().
                           Response;

            ClassicAssert.IsTrue(response.Contains(_405_MethodNotAllowed), response);

        }

        #endregion

    }

}
