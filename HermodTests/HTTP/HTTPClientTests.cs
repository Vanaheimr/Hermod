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
    /// Tests between Hermod HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests : AHTTPServerTests
    {

        #region Data

        public static readonly IPPort HTTPPort = IPPort.Parse(82);

        #endregion

        #region Constructor(s)

        public HTTPClientTests()
            : base(IPPort.Zero)
        { }

        #endregion


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello World!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo("Hello World!".Length));

        }

        #endregion

        #region Test_002()

        [Test]
        public async Task Test_002()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root,
                                                     RequestBuilder: requestBuilder => {
                                                         requestBuilder.Host = HTTPHostname.Localhost;
                                                     }).
                                                 ConfigureAwait(false);



            var request   = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: localhost

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET / HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: localhost"), Is.True, request);



            var response  = httpResponse.EntirePDU;
            var httpBody  = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                12
            // Connection:                    close
            // 
            // Hello World!

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello World!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo("Hello World!".Length));

        }

        #endregion



        #region Test_NotForEveryone_MissingBasicAuth()

        [Test]
        public async Task Test_NotForEveryone_MissingBasicAuth()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /NotForEveryone HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 401 Unauthorized
            // Date:                          Sat, 22 Jul 2023 14:26:49 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Access-Control-Allow-Headers:  Authorization
            // WWWAuthenticate:               Basic realm="Access to the staging site"
            // Connection:                    close

            Assert.That(response.Contains("HTTP/1.1 401 Unauthorized"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo(String.Empty));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.WWWAuthenticate, Is.EqualTo(@"Basic realm=""Access to the staging site"", charset =""UTF-8"""));
            Assert.That(httpResponse.ContentLength, Is.Null);

        }

        #endregion

        #region Test_NotForEveryone_ValidBasicAuth()

        [Test]
        public async Task Test_NotForEveryone_ValidBasicAuth()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone",
                                                     Authentication:  HTTPBasicAuthentication.Create("testUser1", "testPassword1")).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /NotForEveryone HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                           Sat, 22 Jul 2023 15:13:54 GMT
            // Server:                         Hermod Test Server
            // Access-Control-Allow-Origin:    *
            // Access-Control-Allow-Methods:   GET
            // Access-Control-Allow-Headers:   Authorization
            // Content-Type:                   text/plain; charset=utf-8
            // Content-Length:                 16
            // Connection:                     close
            // X-Environment-ManagedThreadId:  10

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello 'testUser1'!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo("Hello 'testUser1'!".Length));

        }

        #endregion

        #region Test_NotForEveryone_ValidBasicAuth_MissingAuthorization()

        [Test]
        public async Task Test_NotForEveryone_ValidBasicAuth_MissingAuthorization()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.GET(HTTPPath.Root + "NotForEveryone",
                                                     Authentication:  HTTPBasicAuthentication.Create("testUser2", "testPassword2")).
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /NotForEveryone HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 403 Forbidden
            // Date:                           Sat, 22 Jul 2023 15:16:42 GMT
            // Server:                         Hermod Test Server
            // Access-Control-Allow-Origin:    *
            // Access-Control-Allow-Methods:   GET
            // Access-Control-Allow-Headers:   Authorization
            // Content-Type:                   text/plain; charset=utf-8
            // Content-Length:                 52
            // Connection:                     close
            // X-Environment-ManagedThreadId:  11
            // 
            // Sorry 'testUser2' please contact your administrator!

            Assert.That(response.Contains("HTTP/1.1 403 Forbidden"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Sorry 'testUser2' please contact your administrator!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.WWWAuthenticate, Is.EqualTo(@"Basic realm=""Access to the staging site"", charset =""UTF-8"""));
            Assert.That(httpResponse.ContentLength, Is.EqualTo("Sorry 'testUser2' please contact your administrator!".Length));

        }

        #endregion



        #region POST_MirrorRandomString_in_QueryString()

        [Test]
        public async Task POST_MirrorRandomString_in_QueryString()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.POST(
                                          HTTPPath.Root + "mirror" + ("queryString?q=" + randomString),
                                          null,
                                          null
                                      ).ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/queryString?q=abcdefgh HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Length:  0

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains($"POST /mirror/queryString?q={randomString} HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);
            // 'Content-Length: 0' is a recommended header for HTTP/1.1 POST requests without a body!
            Assert.That(request.Contains("Content-Length: 0"), Is.True, request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                8
            // Connection:                    close
            // 
            // hgfedcba

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains(mirroredString), Is.True, response);

            Assert.That(httpBody, Is.EqualTo(mirroredString));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo(mirroredString.Length));

        }

        #endregion

        #region POST_MirrorRandomString_in_HTTPBody()

        [Test]
        public async Task POST_MirrorRandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.POST(
                                          HTTPPath.Root + "mirror" + "httpBody",
                                          randomString.ToUTF8Bytes(),
                                          HTTPContentType.Text.PLAIN
                                      ).ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("POST /mirror/httpBody HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);
            Assert.That(request.Contains($"Content-Length: {randomString.Length}"), Is.True, request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                9
            // Connection:                    close
            // 
            // 987654321

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains(mirroredString), Is.True, response);

            Assert.That(httpBody, Is.EqualTo(mirroredString));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo(mirroredString.Length));

        }

        #endregion

        #region MIRROR_RandomString_in_HTTPBody()

        [Test]
        public async Task MIRROR_RandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.MIRROR(
                                          HTTPPath.Root + "mirror" + "httpBody",
                                          randomString.ToUTF8Bytes(),
                                          HTTPContentType.Text.PLAIN
                                      ).ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // POST /mirror/httpBody HTTP/1.1
            // Host:            127.0.0.1:82
            // Content-Type:    text/plain; charset=utf-8
            // Content-Length:  9
            //
            // 123456789

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("MIRROR /mirror/httpBody HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);
            Assert.That(request.Contains($"Content-Length: {randomString.Length}"), Is.True, request);



            var mirroredString  = randomString.Reverse();
            var response        = httpResponse.EntirePDU;
            var httpBody        = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                          Wed, 19 Jul 2023 14:54:44 GMT
            // Server:                        Hermod Test Server
            // Access-Control-Allow-Origin:   *
            // Access-Control-Allow-Methods:  GET
            // Content-Type:                  text/plain; charset=utf-8
            // Content-Length:                9
            // Connection:                    close
            // 
            // 987654321

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains(mirroredString), Is.True, response);

            Assert.That(httpBody, Is.EqualTo(mirroredString));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.ContentLength, Is.EqualTo(mirroredString.Length));

        }

        #endregion


        #region Test_ChunkedEncoding_chunked()

        [Test]
        public async Task Test_ChunkedEncoding_chunked()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            //httpClient.OnChunkDataRead += (time,
            //                               blockNumber,
            //                               blockData,
            //                               blockLength,
            //                               currentTotalBytes) => {

            //    chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            //httpClient.OnChunkBlockFound += (timestamp,
            //                                 chunkNumber,
            //                                 chunkLength,
            //                                 chunkExtensions,
            //                                 chunkData,
            //                                 totalBytes) => {

            //    chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunked").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /chunked HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                            Thu, 27 Jul 2023 23:23:02 GMT
            // Server:                          Hermod Test Server
            // Access-Control-Allow-Origin:     *
            // Access-Control-Allow-Methods:    GET
            // Transfer-Encoding:               chunked
            // Content-Type:                    text/plain; charset=utf-8
            // Connection:                      close
            // X-Environment-ManagedThreadId:   11
            // 
            // Hello World!

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello World!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));

            Assert.That(chunkData.Count, Is.EqualTo(1), "chunkData.Count");
            Assert.That(chunkData.First(), Is.EqualTo("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n\r\n' 32 byte(s), 32 byte(s) total"));

            Assert.That(chunkBlocks.Count, Is.EqualTo(4), "chunkBlocks.Count");
            Assert.That(chunkBlocks.ElementAt(0), Is.EqualTo("1: 'Hello' 5 byte(s), 5 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(1), Is.EqualTo("2: ' ' 1 byte(s), 6 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(2), Is.EqualTo("3: 'World!' 6 byte(s), 12 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(3), Is.EqualTo("4: '' 0 byte(s), 12 byte(s) total"));

        }

        #endregion

        #region Test_ChunkedEncoding_chunkedSlow()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedSlow()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            //httpClient.OnChunkDataRead += (time,
            //                               blockNumber,
            //                               blockData,
            //                               blockLength,
            //                               currentTotalBytes) => {

            //    chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            //httpClient.OnChunkBlockFound += (timestamp,
            //                                 chunkNumber,
            //                                 chunkLength,
            //                                 chunkExtensions,
            //                                 chunkData,
            //                                 totalBytes) => {

            //    chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunkedSlow").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /chunkedSlow HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                            Thu, 27 Jul 2023 23:23:02 GMT
            // Server:                          Hermod Test Server
            // Access-Control-Allow-Origin:     *
            // Access-Control-Allow-Methods:    GET
            // Transfer-Encoding:               chunked
            // Content-Type:                    text/plain; charset=utf-8
            // Connection:                      close
            // X-Environment-ManagedThreadId:   11
            // 
            // Hello World!

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello World!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));

            Assert.That(chunkData.Count, Is.EqualTo(1), "chunkData.Count");
            Assert.That(chunkData.First(), Is.EqualTo("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n' 30 byte(s), 30 byte(s) total"));

            Assert.That(chunkBlocks.Count, Is.EqualTo(4), "chunkBlocks.Count");
            Assert.That(chunkBlocks.ElementAt(0), Is.EqualTo("1: 'Hello' 5 byte(s), 5 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(1), Is.EqualTo("2: ' ' 1 byte(s), 6 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(2), Is.EqualTo("3: 'World!' 6 byte(s), 12 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(3), Is.EqualTo("4: '' 0 byte(s), 12 byte(s) total"));

        }

        #endregion

        #region Test_ChunkedEncoding_chunkedTrailerHeaders()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedTrailerHeaders()
        {

            var chunkData     = new List<String>();
            var chunkBlocks   = new List<String>();
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            //httpClient.OnChunkDataRead += (time,
            //                               blockNumber,
            //                               blockData,
            //                               blockLength,
            //                               currentTotalBytes) => {

            //    chunkData.Add($"{blockNumber}: '{blockData.ToUTF8String()}' {blockLength} byte(s), {currentTotalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            //httpClient.OnChunkBlockFound += (timestamp,
            //                                 chunkNumber,
            //                                 chunkLength,
            //                                 chunkExtensions,
            //                                 chunkData,
            //                                 totalBytes) => {

            //    chunkBlocks.Add($"{chunkNumber}: '{chunkData.ToUTF8String()}' {chunkLength} byte(s), {totalBytes} byte(s) total");
            //    return Task.CompletedTask;

            //};

            var httpResponse  = await httpClient.GET(HTTPPath.Root + "chunkedTrailerHeaders").
                                                 ConfigureAwait(false);



            var request       = httpResponse.HTTPRequest?.EntirePDU ?? "";

            // GET / HTTP/1.1
            // Host: 127.0.0.1:82

            // HTTP requests should not have a "Date"-header!
            Assert.That(request.Contains("Date:"), Is.False, request);
            Assert.That(request.Contains("GET /chunkedTrailerHeaders HTTP/1.1"), Is.True, request);
            Assert.That(request.Contains("Host: 127.0.0.1"), Is.True, request);



            var response      = httpResponse.EntirePDU;
            var httpBody      = httpResponse.HTTPBodyAsUTF8String;

            // HTTP/1.1 200 OK
            // Date:                            Fri, 28 Jul 2023 10:58:54 GMT
            // Server:                          Hermod Test Server
            // Access-Control-Allow-Origin:     *
            // Access-Control-Allow-Methods:    GET
            // Transfer-Encoding:               chunked
            // Trailer:                         X-Message-Length, X-Protocol-Version
            // Content-Type:                    text/plain; charset=utf-8
            // Connection:                      close
            // X-Environment-ManagedThreadId:   10
            // X-Message-Length:                13
            // X-Protocol-Version:              1.0
            // 
            // Hello World!

            Assert.That(response.Contains("HTTP/1.1 200 OK"), Is.True, response);
            Assert.That(response.Contains("Hello World!"), Is.True, response);

            Assert.That(httpBody, Is.EqualTo("Hello World!"));

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));

            Assert.That(chunkData.Count, Is.EqualTo(1), "chunkData.Count");
            Assert.That(chunkData.First(), Is.EqualTo("1: '5\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\n' 30 byte(s), 30 byte(s) total"));

            Assert.That(chunkBlocks.Count, Is.EqualTo(4), "chunkBlocks.Count");
            Assert.That(chunkBlocks.ElementAt(0), Is.EqualTo("1: 'Hello' 5 byte(s), 5 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(1), Is.EqualTo("2: ' ' 1 byte(s), 6 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(2), Is.EqualTo("3: 'World!' 6 byte(s), 12 byte(s) total"));
            Assert.That(chunkBlocks.ElementAt(3), Is.EqualTo("4: '' 0 byte(s), 12 byte(s) total"));

        }

        #endregion


    }

}
