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
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Tests between Hermod HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests_MinimalAPI : AMinimalDotNetWebAPI
    {

        #region Data

        public static readonly IPPort HTTPPort = IPPort.Parse(84);

        #endregion

        #region Constructor(s)

        public HTTPClientTests_MinimalAPI()
            : base(HTTPPort)
        { }

        #endregion


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root).
                                                 ConfigureAwait(false);



            var request = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:84

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                         request);
            Assert.IsTrue (request.Contains("GET / HTTP/1.1"),                request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);



            var response = httpResponse.EntirePDU;
            var httpBody = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Kestrel Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains("Hello World!"),      response);

            Assert.AreEqual("Hello World!",                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual("Hello World!".Length,                  httpResponse.ContentLength);

        }

        #endregion

        #region Test_002()

        [Test]
        public async Task Test_002()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root,
                                                     requestbuilder => {
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
            // Content-Type:    text/plain; charset=utf-8
            // Date:            Sat, 22 Jul 2023 16:53:59 GMT
            // Server:          Kestrel Test Server
            // 
            // Hello World!


            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Connection:                    close

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains("Hello World!"),      response);

            Assert.AreEqual("Hello World!",                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual("Hello World!".Length,                  httpResponse.ContentLength);

        }

        #endregion


        #region Test_NotForEveryone_MissingBasicAuth()

        [Test]
        public async Task Test_NotForEveryone_MissingBasicAuth()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:84

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                          request);
            Assert.IsTrue (request.Contains("GET /NotForEveryone HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),    request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Content-Length:    0
            // Date:              Sat, 22 Jul 2023 19:17:46 GMT
            // Server:            Kestrel Test Server
            // WWW-Authenticate:  Basic realm="Access to the staging site", charset ="UTF-8"

            Assert.IsTrue  (response.Contains("HTTP/1.1 401 Unauthorized"),                      response);

            Assert.AreEqual(String.Empty,                                                        httpBody);

            Assert.AreEqual("Kestrel Test Server",                                               httpResponse.Server);
            Assert.AreEqual(@"Basic realm=""Access to the staging site"", charset =""UTF-8""",   httpResponse.WWWAuthenticate);
            // Unclear why Kestrel sets the Content-Length HTTP response header!
            Assert.AreEqual(0,                                                                   httpResponse.ContentLength);

        }

        #endregion

        #region Test_NotForEveryone_ValidBasicAuth()

        [Test]
        public async Task Test_NotForEveryone_ValidBasicAuth()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone",
                                                     Authentication:  HTTPBasicAuthentication.Create("testUser1", "testPassword1")).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                          request);
            Assert.IsTrue (request.Contains("GET /NotForEveryone HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),    request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  18
            // Content-Type:    text/plain; charset=utf-8
            // Date:            Sat, 22 Jul 2023 17:32:30 GMT
            // Server:          Kestrel Test Server
            // 
            // Hello 'testUser1'!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);

            Assert.AreEqual("Hello 'testUser1'!",                   httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual("Hello 'testUser1'!".Length,            httpResponse.ContentLength);

        }

        #endregion

        #region Test_NotForEveryone_ValidBasicAuth_MissingAuthorization()

        [Test]
        public async Task Test_NotForEveryone_ValidBasicAuth_MissingAuthorization()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone",
                                                     Authentication:  HTTPBasicAuthentication.Create("testUser2", "testPassword2")).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                          request);
            Assert.IsTrue (request.Contains("GET /NotForEveryone HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),    request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 403 Forbidden
            // Content-Length:  52
            // Content-Type:    text/plain; charset=utf-8
            // Date:            Sat, 22 Jul 2023 17:41:50 GMT
            // Server:          Kestrel Test Server
            // 
            // Sorry 'testUser2' please contact your administrator!

            Assert.IsTrue  (response.Contains("HTTP/1.1 403 Forbidden"),                     response);

            Assert.AreEqual("Sorry 'testUser2' please contact your administrator!",          httpBody);

            Assert.AreEqual("Kestrel Test Server",                                           httpResponse.Server);
            Assert.AreEqual("Sorry 'testUser2' please contact your administrator!".Length,   httpResponse.ContentLength);

        }

        #endregion



        #region POST_MirrorRandomString_in_QueryString()

        [Test]
        public async Task POST_MirrorRandomString_in_QueryString()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.POST(HTTPPath.Root + "mirror" + ("queryString?q=" + randomString)).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/queryString?q=abcdefgh HTTP/1.1
            // Host:            127.0.0.1:{HTTPPort}
            // Content-Length:  0

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                                                 request);
            Assert.IsTrue (request.Contains($"POST /mirror/queryString?q={randomString} HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),                           request);
            // 'Content-Length: 0' is a recommended header for HTTP/1.1 POST requests without a body!
            Assert.IsTrue (request.Contains("Content-Length: 0"),                                     request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  8
            // Date:            Thu, 20 Jul 2023 00:20:32 GMT
            // Server:          Kestrel Test Server
            // 
            // hgfedcba

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains(mirroredString),      response);

            Assert.AreEqual(mirroredString,                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual(mirroredString.Length,                  httpResponse.ContentLength);

        }

        #endregion

        #region POST_MirrorRandomString_in_HTTPBody()

        [Test]
        public async Task POST_MirrorRandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.POST(HTTPPath.Root + "mirror" + "httpBody",
                                                      request => {
                                                          request.ContentType  = HTTPContentType.TEXT_UTF8;
                                                          request.Content      = randomString.ToUTF8Bytes();
                                                      }).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:{HTTPPort}
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                                    request);
            Assert.IsTrue (request.Contains("POST /mirror/httpBody HTTP/1.1"),           request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),              request);
            Assert.IsTrue (request.Contains($"Content-Length: {randomString.Length}"),   request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Length:  9
            // Date:            Thu, 20 Jul 2023 00:13:17 GMT
            // Server:          Kestrel Test Server
            // 
            // 987654321

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains(mirroredString),      response);

            Assert.AreEqual(mirroredString,                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual(mirroredString.Length,                  httpResponse.ContentLength);

        }

        #endregion

        #region MIRROR_RandomString_in_HTTPBody()

        [Test]
        public async Task MIRROR_RandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));
            var httpResponse  = await httpClient.MIRROR(HTTPPath.Root + "mirror" + "httpBody",
                                                        request => {
                                                            request.ContentType  = HTTPContentType.TEXT_UTF8;
                                                            request.Content      = randomString.ToUTF8Bytes();
                                                        }).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:{HTTPPort}
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                                    request);
            Assert.IsTrue (request.Contains("MIRROR /mirror/httpBody HTTP/1.1"),         request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),              request);
            Assert.IsTrue (request.Contains($"Content-Length: {randomString.Length}"),   request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Kestrel Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                9
            // Connection:                    close
            // 
            // 987654321

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains(mirroredString),      response);

            Assert.AreEqual(mirroredString,                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);
            Assert.AreEqual(mirroredString.Length,                  httpResponse.ContentLength);

        }

        #endregion



        #region Test_ChunkedEncoding_chunked()

        [Test]
        public async Task Test_ChunkedEncoding_chunked()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));

            httpClient.OnChunkDataRead += async (time,
                                                 blockNumber,
                                                 blockData,
                                                 blockLength,
                                                 currentTotalBytes) => {

                chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");

            };

            httpClient.OnChunkBlockFound += async (timestamp,
                                                   chunkNumber,
                                                   chunkLength,
                                                   chunkExtensions,
                                                   chunkData,
                                                   totalBytes) => {

                chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");

            };

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunked").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                         request);
            Assert.IsTrue (request.Contains("GET /chunked HTTP/1.1"),         request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Type:        text/plain
            // Date:                Fri, 28 Jul 2023 03:17:58 GMT
            // Server:              Kestrel Test Server
            // Transfer-Encoding:   chunked
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains("Hello World!"),      response);

            Assert.AreEqual("Hello World!",                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);

            Assert.AreEqual(1,                                                                                      chunkData.Count,   "chunkData.Count");
            Assert.AreEqual("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n\r\n' 32 byte(s), 32 byte(s) total",   chunkData.First());

            Assert.AreEqual(4,                                                                                      chunkBlocks.Count, "chunkBlocks.Count");
            Assert.AreEqual("1: 'Hello' 5 byte(s), 5 byte(s) total",                                                chunkBlocks.ElementAt(0));
            Assert.AreEqual("2: ' ' 1 byte(s), 6 byte(s) total",                                                    chunkBlocks.ElementAt(1));
            Assert.AreEqual("3: 'World!' 6 byte(s), 12 byte(s) total",                                              chunkBlocks.ElementAt(2));
            Assert.AreEqual("4: '' 0 byte(s), 12 byte(s) total",                                                    chunkBlocks.ElementAt(3));

        }

        #endregion

        #region Test_ChunkedEncoding_chunkedSlow()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedSlow()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));

            httpClient.OnChunkDataRead += async (time,
                                                 blockNumber,
                                                 blockData,
                                                 blockLength,
                                                 currentTotalBytes) => {

                chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");

            };

            httpClient.OnChunkBlockFound += async (timestamp,
                                                   chunkNumber,
                                                   chunkLength,
                                                   chunkExtensions,
                                                   chunkData,
                                                   totalBytes) => {

                chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");

            };

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunkedSlow").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                         request);
            Assert.IsTrue (request.Contains("GET /chunkedSlow HTTP/1.1"),     request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),   request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Type:        text/plain
            // Date:                Fri, 28 Jul 2023 03:16:43 GMT
            // Server:              Kestrel Test Server
            // Transfer-Encoding:   chunked
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains("Hello World!"),      response);

            Assert.AreEqual("Hello World!",                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);

            Assert.AreEqual(4,                                                                                      chunkData.Count,   "chunkData.Count");
            //Assert.AreEqual("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n\r\n' 32 byte(s), 32 byte(s) total",   chunkData.First());

            Assert.AreEqual(4,                                                                                      chunkBlocks.Count, "chunkBlocks.Count");
            Assert.AreEqual("1: 'Hello' 5 byte(s), 5 byte(s) total",                                                chunkBlocks.ElementAt(0));
            Assert.AreEqual("2: ' ' 1 byte(s), 6 byte(s) total",                                                    chunkBlocks.ElementAt(1));
            Assert.AreEqual("3: 'World!' 6 byte(s), 12 byte(s) total",                                              chunkBlocks.ElementAt(2));
            Assert.AreEqual("4: '' 0 byte(s), 12 byte(s) total",                                                    chunkBlocks.ElementAt(3));

        }

        #endregion

        #region Test_ChunkedEncoding_chunkedSlowTrailerHeaders()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedSlowTrailerHeaders()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{HTTPPort}"));

            httpClient.OnChunkDataRead += async (time,
                                                 blockNumber,
                                                 blockData,
                                                 blockLength,
                                                 currentTotalBytes) => {

                chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");

            };

            httpClient.OnChunkBlockFound += async (timestamp,
                                                   chunkNumber,
                                                   chunkLength,
                                                   chunkExtensions,
                                                   chunkData,
                                                   totalBytes) => {

                chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");

            };

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunkedSlowTrailerHeaders").
                                                     //request => {
                                                     //    request.TE = "trailers";
                                                     //}).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.IsFalse(request.Contains("Date:"),                                     request);
            Assert.IsTrue (request.Contains("GET /chunkedSlowTrailerHeaders HTTP/1.1"),   request);
            Assert.IsTrue (request.Contains($"Host: 127.0.0.1:{HTTPPort}"),               request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Content-Type:        text/plain
            // Date:                Fri, 28 Jul 2023 03:16:43 GMT
            // Server:              Kestrel Test Server
            // Transfer-Encoding:   chunked
            // 
            // Hello World!

            Assert.IsTrue  (response.Contains("HTTP/1.1 200 OK"),   response);
            Assert.IsTrue  (response.Contains("Hello World!"),      response);

            Assert.AreEqual("Hello World!",                         httpBody);

            Assert.AreEqual("Kestrel Test Server",                  httpResponse.Server);

            Assert.AreEqual(4,                                                                                      chunkData.Count,   "chunkData.Count");
            //Assert.AreEqual("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n\r\n' 32 byte(s), 32 byte(s) total",   chunkData.First());

            Assert.AreEqual(4,                                                                                      chunkBlocks.Count, "chunkBlocks.Count");
            Assert.AreEqual("1: 'Hello' 5 byte(s), 5 byte(s) total",                                                chunkBlocks.ElementAt(0));
            Assert.AreEqual("2: ' ' 1 byte(s), 6 byte(s) total",                                                    chunkBlocks.ElementAt(1));
            Assert.AreEqual("3: 'World!' 6 byte(s), 12 byte(s) total",                                              chunkBlocks.ElementAt(2));
            Assert.AreEqual("4: '' 0 byte(s), 12 byte(s) total",                                                    chunkBlocks.ElementAt(3));

        }

        #endregion


    }

}
