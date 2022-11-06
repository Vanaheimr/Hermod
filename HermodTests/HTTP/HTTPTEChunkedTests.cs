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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

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
                                                        HTTPStatusCode  = HTTPStatusCode.OK,
                                                        Server          = HTTPServer.DefaultServerName,
                                                        Date            = Timestamp.Now,
                                                        ContentType     = HTTPContentType.TEXT_UTF8,
                                                        Content         = "MozillaDeveloperNetwork".ToUTF8Bytes(),
                                                        Connection      = "close"
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
                                                        HTTPStatusCode    = HTTPStatusCode.OK,
                                                        Server            = HTTPServer.DefaultServerName,
                                                        Date              = Timestamp.Now,
                                                        TransferEncoding  = "chunked",
                                                        ContentType       = HTTPContentType.TEXT_UTF8,
                                                        Content           = "7\r\nMozilla\r\n9\r\nDeveloper\r\n7\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection        = "close"
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
                                                        HTTPStatusCode    = HTTPStatusCode.OK,
                                                        Server            = HTTPServer.DefaultServerName,
                                                        Date              = Timestamp.Now,
                                                        TransferEncoding  = "chunked",
                                                        ContentType       = HTTPContentType.TEXT_UTF8,
                                                        Content           = "007\r\nMozilla\r\n009\r\nDeveloper\r\n007\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection        = "close"
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
                                                        HTTPStatusCode    = HTTPStatusCode.OK,
                                                        Server            = HTTPServer.DefaultServerName,
                                                        Date              = Timestamp.Now,
                                                        TransferEncoding  = "chunked",
                                                        ContentType       = HTTPContentType.TEXT_UTF8,
                                                        Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                                        Connection        = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test05

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test05
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test05",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode    = HTTPStatusCode.OK,
                                                        Server            = HTTPServer.DefaultServerName,
                                                        Date              = Timestamp.Now,
                                                        TransferEncoding  = "chunked",
                                                        ContentType       = HTTPContentType.TEXT_UTF8,
                                                        Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\nCache-Control: no-cache\r\n\r\n".ToUTF8Bytes(),
                                                        Connection        = "close"
                                                    }.AsImmutable),

                                             AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test06

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test06
                // -------------------------------------------------------------
                HTTPServer.AddMethodCallback(HTTPHostname.Any,
                                             HTTPMethod.GET,
                                             URLPathPrefix + "test06",
                                             HTTPDelegate: Request =>

                                                Task.FromResult(
                                                    new HTTPResponse.Builder(Request) {
                                                        HTTPStatusCode    = HTTPStatusCode.OK,
                                                        Server            = HTTPServer.DefaultServerName,
                                                        Date              = Timestamp.Now,
                                                        TransferEncoding  = "chunked",
                                                        ContentType       = HTTPContentType.TEXT_UTF8,
                                                        Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\nCache-Control: no-cache\r\nTrailingHeader: yes\r\nTrailingHeader2: yes\r\n\r\n".ToUTF8Bytes(),
                                                        Trailer           = "Cache-Control",
                                                        Connection        = "close"
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

            var chunkLengths     = new List<UInt32>();
            var chunkExtensions  = new List<Dictionary<String, List<String>>?>();
            var chunkData        = new List<String>();

            var client           = new HTTPClient(URL.Parse("http://127.0.0.1:1234"));
            client.OnChunkBlockFound += async (timestamp, number, length, extensions, data, totalBytes) => {
                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data != null ? data.ToUTF8String() : "");
            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test04"),
                                                                              requestbuilder => {
                                                                                  requestbuilder.Host = HTTPHostname.Localhost;
                                                                                  requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                  requestbuilder.Connection = "close";
                                                                              })).
                                          Result;


            Assert.AreEqual (200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual ("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            Assert.AreEqual (4,                         chunkExtensions.Count);

            Assert.AreEqual (7, chunkLengths[0]);
            Assert.IsNull   (chunkExtensions[0]);
            Assert.AreEqual ("Mozilla",                 chunkData[0]);

            Assert.AreEqual (9, chunkLengths[1]);
            Assert.IsNotNull(chunkExtensions[1]);
            Assert.AreEqual (1,                         chunkExtensions[1]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
            Assert.AreEqual ("Developer",               chunkData[1]);

            Assert.AreEqual (7, chunkLengths[2]);
            Assert.IsNotNull(chunkExtensions[2]);
            Assert.AreEqual (2,                         chunkExtensions[2]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
            Assert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
            Assert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
            Assert.AreEqual ("Network",                 chunkData[2]);

            Assert.AreEqual (0, chunkLengths[3]);
            Assert.IsNull   (chunkExtensions[3]);
            Assert.AreEqual ("",                        chunkData[3]);

            //var json = JObject.Parse(response?.HTTPBodyAsUTF8String);
            //Assert.IsNotNull(json);

        }

        #endregion

        #region ChunkedTest_05()

        [Test]
        public void ChunkedTest_05()
        {

            var chunkLengths     = new List<UInt32>();
            var chunkExtensions  = new List<Dictionary<String, List<String>>?>();
            var chunkData        = new List<String>();

            var client           = new HTTPClient(URL.Parse("http://127.0.0.1:1234"));
            client.OnChunkBlockFound += async (timestamp, number, length, extensions, data, totalBytes) => {
                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data != null ? data.ToUTF8String() : "");
            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test05"),
                                                                              requestbuilder => {
                                                                                  requestbuilder.Host = HTTPHostname.Localhost;
                                                                                  requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                  requestbuilder.Connection = "close";
                                                                              })).
                                          Result;


            Assert.AreEqual (200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual ("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            Assert.AreEqual (4,                         chunkExtensions.Count);

            Assert.AreEqual (7, chunkLengths[0]);
            Assert.IsNull   (chunkExtensions[0]);
            Assert.AreEqual ("Mozilla",                 chunkData[0]);

            Assert.AreEqual (9, chunkLengths[1]);
            Assert.IsNotNull(chunkExtensions[1]);
            Assert.AreEqual (1,                         chunkExtensions[1]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
            Assert.AreEqual ("Developer",               chunkData[1]);

            Assert.AreEqual (7, chunkLengths[2]);
            Assert.IsNotNull(chunkExtensions[2]);
            Assert.AreEqual (2,                         chunkExtensions[2]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
            Assert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
            Assert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
            Assert.AreEqual ("Network",                 chunkData[2]);

            Assert.AreEqual (0, chunkLengths[3]);
            Assert.IsNull   (chunkExtensions[3]);
            Assert.AreEqual ("",                        chunkData[3]);

            //var json = JObject.Parse(response?.HTTPBodyAsUTF8String);
            //Assert.IsNotNull(json);

        }

        #endregion

        #region ChunkedTest_06()

        [Test]
        public void ChunkedTest_06()
        {

            var chunkLengths     = new List<UInt32>();
            var chunkExtensions  = new List<Dictionary<String, List<String>>?>();
            var chunkData        = new List<String>();

            var client           = new HTTPClient(URL.Parse("http://127.0.0.1:1234"), RequestTimeout: TimeSpan.FromHours(1));
            client.OnChunkBlockFound += async (timestamp, number, length, extensions, data, totalBytes) => {
                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data != null ? data.ToUTF8String() : "");
            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test06"),
                                                                              requestbuilder => {
                                                                                  requestbuilder.Host = HTTPHostname.Localhost;
                                                                                  requestbuilder.Accept.Add(HTTPContentType.TEXT_UTF8);
                                                                                  requestbuilder.Connection = "close";
                                                                              })).
                                          Result;


            Assert.AreEqual (200,                       response?.HTTPStatusCode.Code);
            Assert.AreEqual ("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            Assert.AreEqual (4,                         chunkExtensions.Count);

            Assert.AreEqual (7, chunkLengths[0]);
            Assert.IsNull   (chunkExtensions[0]);
            Assert.AreEqual ("Mozilla",                 chunkData[0]);

            Assert.AreEqual (9, chunkLengths[1]);
            Assert.IsNotNull(chunkExtensions[1]);
            Assert.AreEqual (1,                         chunkExtensions[1]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
            Assert.AreEqual ("Developer",               chunkData[1]);

            Assert.AreEqual (7, chunkLengths[2]);
            Assert.IsNotNull(chunkExtensions[2]);
            Assert.AreEqual (2,                         chunkExtensions[2]?.Count);
            Assert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
            Assert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
            Assert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
            Assert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
            Assert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
            Assert.AreEqual ("Network",                 chunkData[2]);

            Assert.AreEqual (0, chunkLengths[3]);
            Assert.IsNull   (chunkExtensions[3]);
            Assert.AreEqual ("",                        chunkData[3]);

            Assert.AreEqual (String.Empty,              response.GetHeaderField(HTTPHeaderField.TransferEncoding));
            Assert.AreEqual ("no-cache",                response.GetHeaderField(HTTPHeaderField.CacheControl));
            Assert.AreEqual (String.Empty,              response.GetHeaderField("TrailingHeader"));
            Assert.AreEqual (String.Empty,              response.GetHeaderField("TrailingHeader2"));

        }

        #endregion

    }

}
