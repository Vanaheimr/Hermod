﻿/*
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
using org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    /// <summary>
    /// Tests between Hermod HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests_MinimalAPI : AMinimalDotNetWebAPI
    {

        #region HTTPClientTest_001()

        [Test]
        public async Task HTTPClientTest_001()
        {

            var httpClient = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse = await httpClient.GET(HTTPPath.Root).
                                                 ConfigureAwait(false);



            var request = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"), request);
            Assert.IsTrue(request.Contains("GET / HTTP/1.1"), request);
            Assert.IsTrue(request.Contains("Host: 127.0.0.1:82"), request);



            var response = httpResponse.EntirePDU;
            var httpBody = httpResponse.HTTPBodyAsUTF8String;

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

            Assert.IsTrue(response.Contains("HTTP/1.1 200 OK"), response);
            Assert.IsTrue(response.Contains("Hello World!"), response);

            Assert.AreEqual("Hello World!", httpBody);

            Assert.AreEqual("Hello World!".Length, httpResponse.ContentLength);

        }

        #endregion

        #region HTTPClientTest_002()

        [Test]
        public async Task HTTPClientTest_002()
        {

            var httpClient = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse = await httpClient.GET(HTTPPath.Root,
                                                     requestbuilder =>
                                                     {
                                                         requestbuilder.Host = HTTPHostname.Localhost;
                                                     }).
                                                 ConfigureAwait(false);



            var request = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: localhost

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"), request);
            Assert.IsTrue(request.Contains("GET / HTTP/1.1"), request);
            Assert.IsTrue(request.Contains("Host: localhost"), request);



            var response = httpResponse.EntirePDU;
            var httpBody = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  12
            // Date:            Thu, 20 Jul 2023 00:24:09 GMT
            // Server:          Kestrel
            // 
            // Hello World!


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

            Assert.IsTrue(response.Contains("HTTP/1.1 200 OK"), response);
            Assert.IsTrue(response.Contains("Hello World!"), response);

            Assert.AreEqual("Hello World!", httpBody);

            Assert.AreEqual("Hello World!".Length, httpResponse.ContentLength);

        }

        #endregion



        #region HTTPClientTest_003()

        [Test]
        public async Task HTTPClientTest_003()
        {

            var httpClient = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse = await httpClient.POST(HTTPPath.Root + "mirror" + "queryString?q=abcdefgh").
                                                 ConfigureAwait(false);



            var request = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/queryString?q=abcdefgh HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Length:  0

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"), request);
            Assert.IsTrue(request.Contains("POST /mirror/queryString?q=abcdefgh HTTP/1.1"), request);
            Assert.IsTrue(request.Contains("Host: 127.0.0.1:82"), request);
            // 'Content-Length: 0' is a recommended header for HTTP/1.1 POST requests without a body!
            Assert.IsTrue(request.Contains("Content-Length: 0"), request);



            var response = httpResponse.EntirePDU;
            var httpBody = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  8
            // Date:            Thu, 20 Jul 2023 00:20:32 GMT
            // Server:          Kestrel
            // 
            // hgfedcba

            Assert.IsTrue(response.Contains("HTTP/1.1 200 OK"), response);
            Assert.IsTrue(response.Contains("hgfedcba"), response);

            Assert.AreEqual("hgfedcba", httpBody);

            Assert.AreEqual("hgfedcba".Length, httpResponse.ContentLength);

        }

        #endregion

        #region HTTPClientTest_004()

        [Test]
        public async Task HTTPClientTest_004()
        {

            var httpClient = new HTTPClient(URL.Parse("http://127.0.0.1:82"));
            var httpResponse = await httpClient.POST(HTTPPath.Root + "mirror" + "httpBody",
                                                      request =>
                                                      {
                                                          request.ContentType = HTTPContentType.TEXT_UTF8;
                                                          request.Content = "123456789".ToUTF8Bytes();
                                                      }).
                                                 ConfigureAwait(false);



            var request = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789


            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"), request);
            Assert.IsTrue(request.Contains("POST /mirror/httpBody HTTP/1.1"), request);
            Assert.IsTrue(request.Contains("Host: 127.0.0.1:82"), request);
            Assert.IsTrue(request.Contains("Content-Length: 9"), request);



            var response = httpResponse.EntirePDU;
            var httpBody = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  9
            // Date:            Thu, 20 Jul 2023 00:13:17 GMT
            // Server:          Kestrel
            // 
            // 987654321

            Assert.IsTrue(response.Contains("HTTP/1.1 200 OK"), response);
            Assert.IsTrue(response.Contains("987654321"), response);

            Assert.AreEqual("987654321", httpBody);

            Assert.AreEqual("987654321".Length, httpResponse.ContentLength);

        }

        #endregion


        #region HTTPClientTest_Concurrent_001()

        [Test]
        public async Task HTTPClientTest_Concurrent_001()
        {

            var startTime = Timestamp.Now;
            var httpRequests = new List<Task<HTTPResponse>>();

            for (var i = 0; i < 100; i++)
            {
                httpRequests.Add(new HTTPClient(URL.Parse("http://127.0.0.1:82")).
                                         POST(HTTPPath.Root + "mirror" + "httpBody",
                                              request =>
                                              {
                                                  request.ContentType = HTTPContentType.TEXT_UTF8;
                                                  request.Content = i.ToString().ToUTF8Bytes();//.PadLeft(4, '0').ToUTF8Bytes();
                                              }));
            }

            var responeses = await Task.WhenAll(httpRequests.ToArray());

            var runtime = Timestamp.Now - startTime;

            var responseTuples = responeses.Select(response => new Tuple<string, string, TimeSpan>(response.HTTPRequest?.HTTPBodyAsUTF8String ?? "xxxx",
                                                                                                         response.HTTPBodyAsUTF8String ?? "yyyy",
                                                                                                         response.Runtime)).
                                                 ToArray();

            var responseErrors = responseTuples.Where(tuple => tuple.Item1 != tuple.Item2.Reverse()).
                                                 ToArray();

            var minRuntime = responseTuples.Min(tuple => tuple.Item3.TotalMicroseconds);
            var maxRuntime = responseTuples.Max(tuple => tuple.Item3.TotalMicroseconds);
            var avgRuntime = responseTuples.Average(tuple => tuple.Item3.TotalMicroseconds);

        }

        #endregion


    }

}