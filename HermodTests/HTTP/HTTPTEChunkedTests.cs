/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
                       true) // AutoStart

            {

                #region GET         ~/test01

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test01
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test01",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                             Server          = HTTPServer.DefaultServerName,
                                             Date            = Timestamp.Now,
                                             ContentType     = HTTPContentType.Text.PLAIN,
                                             Content         = "MozillaDeveloperNetwork".ToUTF8Bytes(),
                                             Connection      = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test02

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test02
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test02",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode    = HTTPStatusCode.OK,
                                             Server            = HTTPServer.DefaultServerName,
                                             Date              = Timestamp.Now,
                                             TransferEncoding  = "chunked",
                                             ContentType       = HTTPContentType.Text.PLAIN,
                                             Content           = "7\r\nMozilla\r\n9\r\nDeveloper\r\n7\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                             Connection        = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test03

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test03
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test03",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode    = HTTPStatusCode.OK,
                                             Server            = HTTPServer.DefaultServerName,
                                             Date              = Timestamp.Now,
                                             TransferEncoding  = "chunked",
                                             ContentType       = HTTPContentType.Text.PLAIN,
                                             Content           = "007\r\nMozilla\r\n009\r\nDeveloper\r\n007\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                             Connection        = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test04

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test04
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test04",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode    = HTTPStatusCode.OK,
                                             Server            = HTTPServer.DefaultServerName,
                                             Date              = Timestamp.Now,
                                             TransferEncoding  = "chunked",
                                             ContentType       = HTTPContentType.Text.PLAIN,
                                             Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\n\r\n".ToUTF8Bytes(),
                                             Connection        = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test05

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test05
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test05",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode    = HTTPStatusCode.OK,
                                             Server            = HTTPServer.DefaultServerName,
                                             Date              = Timestamp.Now,
                                             TransferEncoding  = "chunked",
                                             ContentType       = HTTPContentType.Text.PLAIN,
                                             Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\nCache-Control: no-cache\r\n\r\n".ToUTF8Bytes(),
                                             Connection        = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion

                #region GET         ~/test06

                // -------------------------------------------------------------
                // curl -v -H "Accept: text/html" http://127.0.0.1:1234/test06
                // -------------------------------------------------------------
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,
                                  URLPathPrefix + "test06",
                                  HTTPDelegate: Request =>

                                     Task.FromResult(
                                         new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode    = HTTPStatusCode.OK,
                                             Server            = HTTPServer.DefaultServerName,
                                             Date              = Timestamp.Now,
                                             TransferEncoding  = "chunked",
                                             ContentType       = HTTPContentType.Text.PLAIN,
                                             Content           = "007\r\nMozilla\r\n009;a=b\r\nDeveloper\r\n007;a=b;c=d\r\nNetwork\r\n0\r\nCache-Control: no-cache\r\nTrailingHeader: yes\r\nTrailingHeader2: yes\r\n\r\n".ToUTF8Bytes(),
                                             Trailer           = "Cache-Control",
                                             Connection        = ConnectionType.Close
                                         }.AsImmutable),

                                  AllowReplacement: URLReplacement.Allow);

                #endregion


            }


        }


        #region Start/Stop TEChunkedAPI

        private TEChunkedAPI? chunkedAPI;

        [OneTimeSetUp]
        public void Init_TEChunkedAPI()
        {

            chunkedAPI = new TEChunkedAPI();

        }

        [OneTimeTearDown]
        public void Shutdown_TEChunkedAPI()
        {
            chunkedAPI?.Shutdown();
        }

        #endregion


        #region ChunkedTest_01()

        [Test]
        public void ChunkedTest_01()
        {

            var response = HTTPClientFactory.Create (URL.Parse("http://127.0.0.1:1234")).
                                             Execute(client => client.GETRequest(
                                                                   HTTPPath.Parse("/test01"),
                                                                   RequestBuilder: requestBuilder => {
                                                                       requestBuilder.Host        = HTTPHostname.Localhost;
                                                                       requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                       requestBuilder.Connection  = ConnectionType.Close;
                                                                   }
                                                               )).

                                             Result;


            ClassicAssert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            ClassicAssert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

        }

        #endregion

        #region ChunkedTest_02()

        [Test]
        public void ChunkedTest_02()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test02"),
                                                                            RequestBuilder: requestBuilder => {
                                                                                requestBuilder.Host = HTTPHostname.Localhost;
                                                                                requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                                requestBuilder.Connection = ConnectionType.Close;
                                                                            })).

                                        Result;


            ClassicAssert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            ClassicAssert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

        }

        #endregion

        #region ChunkedTest_03()

        [Test]
        public void ChunkedTest_03()
        {

            var response = HTTPClientFactory.Create(URL.Parse("http://127.0.0.1:1234")).

                                        Execute(client => client.GETRequest(HTTPPath.Parse("/test03"),
                                                                            RequestBuilder: requestBuilder => {
                                                                                requestBuilder.Host = HTTPHostname.Localhost;
                                                                                requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                                requestBuilder.Connection = ConnectionType.Close;
                                                                            })).

                                        Result;


            ClassicAssert.AreEqual(200,                       response?.HTTPStatusCode.Code);
            ClassicAssert.AreEqual("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);

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

            client.OnChunkBlockFound += (timestamp, number, length, extensions, data, totalBytes) => {

                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data is not null ? data.ToUTF8String() : String.Empty);

                return Task.CompletedTask;

            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test04"),
                                                                              RequestBuilder: requestBuilder => {
                                                                                  requestBuilder.Host = HTTPHostname.Localhost;
                                                                                  requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                                  requestBuilder.Connection = ConnectionType.Close;
                                                                              })).
                                          Result;


            ClassicAssert.AreEqual (200,                       response?.HTTPStatusCode.Code);
            ClassicAssert.AreEqual ("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            ClassicAssert.AreEqual (4,                         chunkExtensions.Count);

            ClassicAssert.AreEqual (7, chunkLengths[0]);
            ClassicAssert.IsNull   (chunkExtensions[0]);
            ClassicAssert.AreEqual ("Mozilla",                 chunkData[0]);

            ClassicAssert.AreEqual (9, chunkLengths[1]);
            ClassicAssert.IsNotNull(chunkExtensions[1]);
            ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.Count);
            ClassicAssert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
            ClassicAssert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
            ClassicAssert.AreEqual ("Developer",               chunkData[1]);

            ClassicAssert.AreEqual (7, chunkLengths[2]);
            ClassicAssert.IsNotNull(chunkExtensions[2]);
            ClassicAssert.AreEqual (2,                         chunkExtensions[2]?.Count);
            ClassicAssert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
            ClassicAssert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
            ClassicAssert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
            ClassicAssert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
            ClassicAssert.AreEqual ("Network",                 chunkData[2]);

            ClassicAssert.AreEqual (0, chunkLengths[3]);
            ClassicAssert.IsNull   (chunkExtensions[3]);
            ClassicAssert.AreEqual ("",                        chunkData[3]);

            //var json = JObject.Parse(response?.HTTPBodyAsUTF8String);
            //ClassicAssert.IsNotNull(json);

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

            client.OnChunkBlockFound += (timestamp, number, length, extensions, data, totalBytes) => {

                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data is not null ? data.ToUTF8String() : String.Empty);

                return Task.CompletedTask;

            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test05"),
                                                                              RequestBuilder: requestBuilder => {
                                                                                  requestBuilder.Host = HTTPHostname.Localhost;
                                                                                  requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                                  requestBuilder.Connection = ConnectionType.Close;
                                                                              })).
                                          Result;


            ClassicAssert.AreEqual (200,                       response?.HTTPStatusCode.Code);
            ClassicAssert.AreEqual ("MozillaDeveloperNetwork", response?.HTTPBodyAsUTF8String);
            ClassicAssert.AreEqual (4,                         chunkExtensions.Count);

            ClassicAssert.AreEqual (7, chunkLengths[0]);
            ClassicAssert.IsNull   (chunkExtensions[0]);
            ClassicAssert.AreEqual ("Mozilla",                 chunkData[0]);

            ClassicAssert.AreEqual (9, chunkLengths[1]);
            ClassicAssert.IsNotNull(chunkExtensions[1]);
            ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.Count);
            ClassicAssert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
            ClassicAssert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
            ClassicAssert.AreEqual ("Developer",               chunkData[1]);

            ClassicAssert.AreEqual (7, chunkLengths[2]);
            ClassicAssert.IsNotNull(chunkExtensions[2]);
            ClassicAssert.AreEqual (2,                         chunkExtensions[2]?.Count);
            ClassicAssert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
            ClassicAssert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
            ClassicAssert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
            ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
            ClassicAssert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
            ClassicAssert.AreEqual ("Network",                 chunkData[2]);

            ClassicAssert.AreEqual (0, chunkLengths[3]);
            ClassicAssert.IsNull   (chunkExtensions[3]);
            ClassicAssert.AreEqual ("",                        chunkData[3]);

            //var json = JObject.Parse(response?.HTTPBodyAsUTF8String);
            //ClassicAssert.IsNotNull(json);

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

            client.OnChunkBlockFound += (timestamp, number, length, extensions, data, totalBytes) => {

                chunkLengths.   Add(length);
                chunkExtensions.Add(extensions);
                chunkData.      Add(data is not null ? data.ToUTF8String() : String.Empty);

                return Task.CompletedTask;

            };

            var response         = client.Execute(client => client.GETRequest(HTTPPath.Parse("/test06"),
                                                                              RequestBuilder: requestBuilder => {
                                                                                  requestBuilder.Host = HTTPHostname.Localhost;
                                                                                  requestBuilder.Accept.Add(HTTPContentType.Text.PLAIN);
                                                                                  requestBuilder.Connection = ConnectionType.Close;
                                                                              })).
                                          Result;

            ClassicAssert.IsNotNull(response);

            if (response is not null)
            {

                ClassicAssert.AreEqual (200,                       response.HTTPStatusCode.Code);
                ClassicAssert.AreEqual ("MozillaDeveloperNetwork", response.HTTPBodyAsUTF8String);
                ClassicAssert.AreEqual (4,                         chunkExtensions.Count);

                ClassicAssert.AreEqual (7, chunkLengths[0]);
                ClassicAssert.IsNull   (chunkExtensions[0]);
                ClassicAssert.AreEqual ("Mozilla",                 chunkData[0]);

                ClassicAssert.AreEqual (9, chunkLengths[1]);
                ClassicAssert.IsNotNull(chunkExtensions[1]);
                ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.Count);
                ClassicAssert.AreEqual ("a",                       chunkExtensions[1]?.First().Key);
                ClassicAssert.AreEqual (1,                         chunkExtensions[1]?.First().Value.Count);
                ClassicAssert.AreEqual ("b",                       chunkExtensions[1]?.First().Value.First());
                ClassicAssert.AreEqual ("Developer",               chunkData[1]);

                ClassicAssert.AreEqual (7, chunkLengths[2]);
                ClassicAssert.IsNotNull(chunkExtensions[2]);
                ClassicAssert.AreEqual (2,                         chunkExtensions[2]?.Count);
                ClassicAssert.AreEqual ("a",                       chunkExtensions[2]?.First().Key);
                ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.First().Value.Count);
                ClassicAssert.AreEqual ("b",                       chunkExtensions[2]?.First().Value.First());
                ClassicAssert.AreEqual ("c",                       chunkExtensions[2]?.Skip(1).First().Key);
                ClassicAssert.AreEqual (1,                         chunkExtensions[2]?.Skip(1).First().Value.Count);
                ClassicAssert.AreEqual ("d",                       chunkExtensions[2]?.Skip(1).First().Value.First());
                ClassicAssert.AreEqual ("Network",                 chunkData[2]);

                ClassicAssert.AreEqual (0, chunkLengths[3]);
                ClassicAssert.IsNull   (chunkExtensions[3]);
                ClassicAssert.AreEqual ("",                        chunkData[3]);

                ClassicAssert.AreEqual (String.Empty,              response.GetHeaderField(HTTPHeaderField.TransferEncoding));
                ClassicAssert.AreEqual ("no-cache",                response.GetHeaderField(HTTPHeaderField.CacheControl));
                ClassicAssert.AreEqual (String.Empty,              response.GetHeaderField("TrailingHeader"));
                ClassicAssert.AreEqual (String.Empty,              response.GetHeaderField("TrailingHeader2"));

            }

        }

        #endregion


    }

}
