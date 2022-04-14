/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// HTTP TE Chunked tests.
    /// </summary>
    [TestFixture]
    public class HTTPTEChunkedTests
    {

        public class TEChunkedAPI : HTTPAPI
        {

            public TEChunkedAPI()

                : base(HTTPHostname.Any,
                       "",
                       IPPort.Parse(1234),
                       null,
                       "HTTP TE Chunked API",

                       null,
                       "HTTP TE Chunked API",
                       null,
                       null,

                       null,
                       null,
                       null,
                       null,

                       null,
                       null,
                       null,
                       null,
                       null,
                       null,
                       null,
                       null,
                       null,

                       true,
                       null,
                       null,

                       true,
                       null,
                       null,

                       true,
                       null,
                       false,
                       null,
                       null,
                       null,
                       null,
                       true) // Autostart

            {

                #region GET         ~/test01

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test01
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test01",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = HTTPServer.DefaultServerName,
                                                        Date                       = Timestamp.Now,
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = "MozillaDeveloperNetwork".ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test02

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test02
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test02",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = HTTPServer.DefaultServerName,
                                                        Date                       = Timestamp.Now,
                                                        TransferEncoding           = "chunked",
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = "7\r\nMozilla\r\n9\r\nDeveloper\r\n7\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test03

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test03
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test03",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = HTTPServer.DefaultServerName,
                                                        Date                       = Timestamp.Now,
                                                        TransferEncoding           = "chunked",
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = "007\r\nMozilla\r\n009\r\nDeveloper\r\n007\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test04

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test04
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test04",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = HTTPServer.DefaultServerName,
                                                        Date                       = Timestamp.Now,
                                                        TransferEncoding           = "chunked",
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion


            }


        }


        #region Start/Stop HTTPServer

        private TEChunkedAPI _HTTPServer;

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            _HTTPServer = new TEChunkedAPI();

        }

        [OneTimeTearDown]
        public void Shutdown_HTTPServer()
        {
            _HTTPServer.Shutdown();
        }

        #endregion


        #region ChunkedTest_01()

        [Test]
        public void ChunkedTest_01()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test01"),
                                                                            requestbuilder => {
                                                                                requestbuilder.Host        = HTTPHostname.Localhost;
                                                                                requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                requestbuilder.Connection  = "close";
                                                                            })).

                                        Result;


            Assert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

        }

        #endregion

        #region ChunkedTest_02()

        [Test]
        public void ChunkedTest_02()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test02"),
                                                                            requestbuilder => {
                                                                                requestbuilder.Host = HTTPHostname.Localhost;
                                                                                requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                requestbuilder.Connection = "close";
                                                                            })).

                                        Result;


            Assert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

        }

        #endregion

        #region ChunkedTest_03()

        [Test]
        public void ChunkedTest_03()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test03"),
                                                                            requestbuilder => {
                                                                                requestbuilder.Host = HTTPHostname.Localhost;
                                                                                requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                requestbuilder.Connection = "close";
                                                                            })).

                                        Result;


            Assert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

        }

        #endregion

        #region ChunkedTest_04()

        [Test]
        public void ChunkedTest_04()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test04"),
                                                                            requestbuilder => {
                                                                                requestbuilder.Host = HTTPHostname.Localhost;
                                                                                requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                requestbuilder.Connection = "close";
                                                                            })).

                                        Result;


            Assert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            //var json = JObject.Parse(response?.HTTPBodyAsUTF8String);
            //Assert.IsNotNull(json);

        }

        #endregion

    }

}
