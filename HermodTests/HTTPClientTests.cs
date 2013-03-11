/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;

using eu.Vanaheimr.Hermod.HTTP;
using eu.Vanaheimr.Hermod.Datastructures;

using NUnit.Framework;

#endregion

namespace eu.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// HTTPClientTests test.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests
    {

        #region Constants

        private const String _400_BadRequest                = "400 Bad Request";
        private const String _404_NotFound                  = "404 Not Found";
        private const String _405_MethodNotAllowed          = "405 Method Not Allowed";

        private const String _500_InternalServerError     = "500 Internal Server Error";
        private const String _505_HTTPVersionNotSupported = "505 HTTP Version Not Supported";

        #endregion

        #region Start/Stop HTTPServer

        private HTTPServer<IRESTService> _HTTPServer;

        [TestFixtureSetUp]
        public void Init_HTTPServer()
        {
            _HTTPServer = new HTTPServer<IRESTService>(IPv4Address.Any, new IPPort(81), Autostart: true);
        }

        [TestFixtureTearDown]
        public void Shutdown_HTTPServer()
        {
            _HTTPServer.Shutdown();
        }

        #endregion

        #region NewTCPClientRequest()

        private TCPClientRequest NewTCPClientRequest()
        {
            return new TCPClientRequest("localhost", 81);
        }

        #endregion


        #region HTTPClientTests_001()

        [Test]
        public void HTTPClientTests_001()
        {

            var Response = NewTCPClientRequest().
                           Send("GET / HTTP/1.1").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains("200 OK"), Response);
            Assert.IsTrue(Response.Contains("Hello World!"), Response);

        }

        #endregion

        #region HTTPClientTests_002()

        [Test]
        public void HTTPClientTests_002()
        {

            var Response = NewTCPClientRequest().
                           Send("GET / HTTP/2.0").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains(_505_HTTPVersionNotSupported), Response);

        }

        #endregion

        #region HTTPClientTests_003()

        [Test]
        public void HTTPClientTests_003()
        {

            var Response = NewTCPClientRequest().
                           Send("GET / HTTP 2.0").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains(_400_BadRequest), Response);

        }

        #endregion

        #region HTTPClientTests_004()

        [Test]
        public void HTTPClientTests_004()
        {

            var Response = NewTCPClientRequest().
                           Send("GET / HTTP/1").
                           FinishCurrentRequest().
                           Response;
            
            Assert.IsTrue(Response.Contains(_505_HTTPVersionNotSupported), Response);

        }

        #endregion

        #region HTTPClientTests_005()

        [Test]
        public void HTTPClientTests_005()
        {

            var Response = NewTCPClientRequest().
                           Send("GET / HTTo/2.0").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains(_500_InternalServerError), Response);

        }

        #endregion

        #region HTTPClientTests_006()

        [Test]
        public void HTTPClientTests_006()
        {

            var Response = NewTCPClientRequest().
                           Send("GE").
                           Wait(100).
                           Send("T / HTTo/2.0").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains(_500_InternalServerError), Response);

        }

        #endregion

        #region HTTPClientTests_007()

        [Test]
        public void HTTPClientTests_007()
        {
            
            var Response = NewTCPClientRequest().
                           Send("GETTT / HTTP/1.1").
                           FinishCurrentRequest().
                           Response;

            Assert.IsTrue(Response.Contains(_405_MethodNotAllowed), Response);

        }

        #endregion

    }

}
