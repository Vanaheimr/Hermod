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

        #region GET_ConnectionClose_Reconnects_Before_The_Next_Request()

        [Test]
        public async Task GET_ConnectionClose_Reconnects_Before_The_Next_Request()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var firstResponse  = await httpClient.GET(HTTPPath.Root + "close");
            var secondResponse = await httpClient.GET(HTTPPath.Root + "close");

            Assert.That(firstResponse.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(secondResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(firstResponse.HTTPBodyAsUTF8String,  Is.EqualTo("1"));
            Assert.That(secondResponse.HTTPBodyAsUTF8String, Is.EqualTo("1"));
            Assert.That(httpClient.KeepAliveMessageCount,    Is.EqualTo(1));

        }

        #endregion


        #region GET_KeepAlive_Reuses_Connection()

        [Test]
        public async Task GET_KeepAlive_Reuses_Connection()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var firstResponse  = await httpClient.GET(HTTPPath.Root + "keepalive");
            var secondResponse = await httpClient.GET(HTTPPath.Root + "keepalive");

            Assert.That(firstResponse.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(secondResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(firstResponse.HTTPBodyAsUTF8String,  Is.EqualTo("1"));
            Assert.That(secondResponse.HTTPBodyAsUTF8String, Is.EqualTo("2"));
            Assert.That(httpClient.KeepAliveMessageCount,    Is.EqualTo(2));

        }

        #endregion


        #region HEAD_Root_Has_No_Body()

        [Test]
        public async Task HEAD_Root_Has_No_Body()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.SendRequest(
                                         httpClient.HEADRequest(HTTPPath.Root)
                                     );

            Assert.That(httpResponse.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.ContentLength,   Is.EqualTo("Hello World!".Length));
            Assert.That(httpResponse.HTTPBody,        Is.Null.Or.Empty);

        }

        #endregion

        #region OPTIONS_Root_Has_No_Body()

        [Test]
        public async Task OPTIONS_Root_Has_No_Body()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.SendRequest(
                                         httpClient.OPTIONSRequest(HTTPPath.Root)
                                     );

            Assert.That(httpResponse.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.NoContent));
            Assert.That(httpResponse.HTTPBody,        Is.Empty);

        }

        #endregion

        #region GET_NotModified_Has_No_Body()

        [Test]
        public async Task GET_NotModified_Has_No_Body()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var httpResponse = await httpClient.GET(HTTPPath.Root + "notmodified");

            Assert.That(httpResponse.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.NotModified));
            Assert.That(httpResponse.HTTPBody,       Is.Null.Or.Empty);

        }

        #endregion


        #region GET_ResetContent_Has_No_Body()

        [Test]
        public async Task GET_ResetContent_Has_No_Body()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var httpResponse = await httpClient.GET(HTTPPath.Root + "resetcontent");

            Assert.That(httpResponse.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ResetContent));
            Assert.That(httpResponse.HTTPBody,       Is.Null.Or.Empty);

        }

        #endregion


        #region PUT_Root_Is_MethodNotAllowed()

        [Test]
        public async Task PUT_Root_Is_MethodNotAllowed()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.SendRequest(
                                         httpClient.PUTRequest(HTTPPath.Root)
                                     );

            Assert.That(httpResponse.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.MethodNotAllowed));
            Assert.That(httpResponse.Allow,          Does.Contain(HTTPMethod.GET));
            Assert.That(httpResponse.Allow,          Does.Contain(HTTPMethod.HEAD));
            Assert.That(httpResponse.Allow,          Does.Contain(HTTPMethod.OPTIONS));

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

            Assert.That(httpBody, Is.Null);

            Assert.That(httpResponse.Server, Is.EqualTo("Hermod Test Server"));
            Assert.That(httpResponse.WWWAuthenticate.ToString().Trim(), Is.EqualTo(@"Basic realm=""Access to the staging site"", charset=""UTF-8"""));
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
            Assert.That(httpResponse.WWWAuthenticate.ToString().Trim(), Is.EqualTo(@"Basic realm=""Access to the staging site"", charset=""UTF-8"""));
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

        #region POST_MirrorHTTPBody_With_Expect100Continue()

        [Test]
        public async Task POST_MirrorHTTPBody_With_Expect100Continue()
        {

            const String requestBody = "expect-continue";

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.POST(
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         requestBody.ToUTF8Bytes(),
                                         HTTPContentType.Text.PLAIN,
                                         RequestBuilder: requestBuilder => requestBuilder.Expect = "100-continue"
                                     ).ConfigureAwait(false);

            Assert.That(httpResponse.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String,  Is.EqualTo(requestBody.Reverse()));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU, Does.Contain("Expect: 100-continue"));

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


        #region QUERY_RandomString_in_HTTPBody()

        [Test]
        public async Task QUERY_RandomString_in_HTTPBody()
        {

            var queryContent  = RandomExtensions.RandomString(50);
            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse  = await httpClient.QUERY(
                                           HTTPPath.Root + "query",
                                           queryContent.ToUTF8Bytes(),
                                           HTTPContentType.Text.PLAIN
                                       );

            Assert.That(HTTPMethod.QUERY.IsSafe,       Is.True);
            Assert.That(HTTPMethod.QUERY.IsIdempotent, Is.True);
            Assert.That(httpResponse.HTTPStatusCode,   Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo(queryContent.Reverse()));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU,
                        Does.Contain("QUERY /query HTTP/1.1"));
            Assert.That(httpResponse.HTTPRequest?.ContentLength,
                        Is.EqualTo(queryContent.Length));

        }

        #endregion


        #region QUERY_ChunkedRandomString_in_HTTPBody()

        [Test]
        public async Task QUERY_ChunkedRandomString_in_HTTPBody()
        {

            var queryContent = RandomExtensions.RandomString(50);
            var chunkedBody  = $"{queryContent.Length:X}\r\n{queryContent}\r\n0\r\n\r\n".ToUTF8Bytes();
            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.QUERY(
                                         HTTPPath.Root + "query",
                                         chunkedBody,
                                         HTTPContentType.Text.PLAIN,
                                         RequestBuilder: requestBuilder => {
                                             requestBuilder.TransferEncoding = "chunked";
                                         }
                                     );

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo(queryContent.Reverse()));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU,
                        Does.Contain("Transfer-Encoding: chunked"));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU,
                        Does.Not.Contain("Content-Length:"));

        }

        #endregion


        #region QUERY_ChunkedRequestTrailer_RandomString_in_HTTPBody()

        [Test]
        public async Task QUERY_ChunkedRequestTrailer_RandomString_in_HTTPBody()
        {

            var queryContent = RandomExtensions.RandomString(50);
            var chunkedBody  = $"{queryContent.Length:X}\r\n{queryContent}\r\n0\r\nX-Query-Metadata: accepted\r\n\r\n".ToUTF8Bytes();
            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.QUERY(
                                         HTTPPath.Root + "query",
                                         chunkedBody,
                                         HTTPContentType.Text.PLAIN,
                                         RequestBuilder: requestBuilder => {
                                             requestBuilder.TransferEncoding = "chunked";
                                             requestBuilder.Trailer          = "X-Query-Metadata";
                                         }
                                     );

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo(queryContent.Reverse()));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU,
                        Does.Contain("X-Query-Metadata: accepted"));

        }

        #endregion


        #region QUERY_ChunkedRequestTrailer_Is_Exposed_To_The_Handler()

        [Test]
        public async Task QUERY_ChunkedRequestTrailer_Is_Exposed_To_The_Handler()
        {

            const String queryContent = "trailer-request";

            var chunkedBody = $"{queryContent.Length:X}\r\n{queryContent}\r\n0\r\nX-Query-Metadata: accepted\r\n\r\n".ToUTF8Bytes();
            var httpClient  = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.QUERY(
                                         HTTPPath.Root + "query" + "trailer",
                                         chunkedBody,
                                         HTTPContentType.Text.PLAIN,
                                         RequestBuilder: requestBuilder => {
                                             requestBuilder.TransferEncoding = "chunked";
                                             requestBuilder.Trailer          = "X-Query-Metadata";
                                         }
                                     );

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("tseuqer-reliart|accepted"));

        }

        #endregion


        #region POST_ChunkedMirrorRandomString_in_HTTPBody()

        [Test]
        public async Task POST_ChunkedMirrorRandomString_in_HTTPBody()
        {

            var randomString = RandomExtensions.RandomString(50);
            var chunkedBody  = $"{randomString.Length:X}\r\n{randomString}\r\n0\r\n\r\n".ToUTF8Bytes();
            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.RunRequest(
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         Content:     chunkedBody,
                                         ContentType: HTTPContentType.Text.PLAIN,
                                         RequestBuilder: requestBuilder => {
                                             requestBuilder.TransferEncoding = "chunked";
                                         }
                                     ).ConfigureAwait(false);

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo(randomString.Reverse()));
            Assert.That(httpResponse.HTTPRequest?.EntirePDU.Contains("Transfer-Encoding: chunked"), Is.True);
            Assert.That(httpResponse.HTTPRequest?.EntirePDU.Contains("Content-Length:"),              Is.False);

        }

        #endregion


        #region GET_EventStream_Parses_Multiple_Data_Lines()

        [Test]
        public async Task GET_EventStream_Parses_Multiple_Data_Lines()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var httpResponse = await httpClient.GET(HTTPPath.Root + "events" + "multiline");
            var events       = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(httpResponse);

            Assert.That(events.Count,             Is.EqualTo(1));
            Assert.That(events[0].Id,               Is.EqualTo(7));
            Assert.That(events[0].Subevent,         Is.EqualTo("status"));
            Assert.That(events[0].Data["message"]?.ToString(), Is.EqualTo("multiline"));

        }

        #endregion


        #region GET_EventStream()

        [Test]
        public async Task GET_EventStream()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.GET(HTTPPath.Root + "events");
            var events       = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(httpResponse);

            Assert.That(httpResponse.ContentType, Is.EqualTo(HTTPContentType.Text.EVENTSTREAM));
            Assert.That(events.Any(httpEvent => httpEvent.Subevent == "status" &&
                                                httpEvent.Data["message"]?.ToString() == "from Hermod server"), Is.True);

        }

        #endregion


        #region GET_LiveEventStreamWorker()

        [Test]
        public async Task GET_LiveEventStreamWorker()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.GET(HTTPPath.Root + "events" + "live");
            var events       = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(httpResponse);

            Assert.That(httpResponse.ContentType, Is.EqualTo(HTTPContentType.Text.EVENTSTREAM));
            Assert.That(events.Any(httpEvent => httpEvent.Subevent == "status" &&
                                                httpEvent.Data["message"]?.ToString() == "from live SSE worker"), Is.True);

        }

        #endregion


        #region GET_EventStream_Reconnects_Automatically()

        [Test]
        public async Task GET_EventStream_Reconnects_Automatically()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var started = Timestamp.Now;

            var events = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.GetEventsWithReconnect(
                                   httpClient,
                                   HTTPPath.Root + "events" + "reconnect",
                                   MaxNumberOfReconnects: 1
                               );

            Assert.That(events.Select(httpEvent => httpEvent.Id), Is.EqualTo([ 1UL, 2UL ]));
            Assert.That(Timestamp.Now - started, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(75)));

        }

        #endregion


        #region GET_EventStream_Reconnects_From_Last_Event_Id()

        [Test]
        public async Task GET_EventStream_Reconnects_From_Last_Event_Id()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var initialResponse = await httpClient.GET(HTTPPath.Root + "events" + "reconnect");
            var initialEvents   = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(initialResponse);

            var reconnectResponse = await httpClient.GET(
                                        HTTPPath.Root + "events" + "reconnect",
                                        RequestBuilder: requestBuilder => requestBuilder.LastEventId = 1
                                    );
            var reconnectEvents   = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(reconnectResponse);

            Assert.That(initialEvents.Select(httpEvent => httpEvent.Id), Is.EqualTo([ 1UL, 2UL ]));
            Assert.That(reconnectEvents.Select(httpEvent => httpEvent.Id), Is.EqualTo([ 2UL ]));
            Assert.That(reconnectResponse.HTTPRequest?.EntirePDU,
                        Does.Contain("Last-Event-Id: 1"));

        }

        #endregion


        #region GET_EventStream_Parsing_Honors_Cancellation()

        [Test]
        public async Task GET_EventStream_Parsing_Honors_Cancellation()
        {

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            using var cts        = new CancellationTokenSource();

            var httpResponse = await httpClient.GET(HTTPPath.Root + "events");

            cts.Cancel();

            var events = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(
                                   httpResponse,
                                   cancellationToken: cts.Token
                               );

            Assert.That(events, Is.Empty);

        }

        #endregion


        #region Test_ChunkedEncoding_chunked()

        [Test]
        public async Task Test_ChunkedEncoding_chunked()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

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

        }

        #endregion

        #region Test_ChunkedEncoding_chunkedSlow()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedSlow()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

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

        }

        #endregion

        #region GET_AutomaticallyChunkedResponse_Trailers_Are_Available()

        [Test]
        public async Task GET_AutomaticallyChunkedResponse_Trailers_Are_Available()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.GET(HTTPPath.Root + "chunkedAutomaticTrailerHeaders");

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.TrailingHeaders["X-Message-Length"],   Is.EqualTo("13"));
            Assert.That(httpResponse.TrailingHeaders["X-Protocol-Version"], Is.EqualTo("1.0"));

        }

        #endregion


        #region GET_ChunkedLiveResponse_Trailers_Are_Available()

        [Test]
        public async Task GET_ChunkedLiveResponse_Trailers_Are_Available()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.GET(HTTPPath.Root + "chunkedLiveTrailerHeaders");

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.TrailingHeaders.TryGetValue("X-Message-Length", out var messageLength), Is.True);
            Assert.That(messageLength, Is.EqualTo("13"));
            Assert.That(httpResponse.TrailingHeaders.TryGetValue("X-Protocol-Version", out var protocolVersion), Is.True);
            Assert.That(protocolVersion, Is.EqualTo("1.0"));

        }

        #endregion


        #region GET_ChunkedLiveResponse_Extensions_Are_Available()

        [Test]
        public async Task GET_ChunkedLiveResponse_Extensions_Are_Available()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.GET(HTTPPath.Root + "chunkedLiveExtensions");

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.ChunkExtensions,      Has.Count.EqualTo(2));
            Assert.That(httpResponse.ChunkExtensions[0]["part"], Is.EqualTo([ "one", "two" ]));
            Assert.That(httpResponse.ChunkExtensions[1]["flag"], Is.EqualTo([ "" ]));
            Assert.That(httpResponse.ChunkExtensions[1]["comment"], Is.EqualTo([ "two words" ]));

        }

        #endregion


        #region QUERY_ChunkedRequestExtensions_Are_Sent_By_The_Client_Worker()

        [Test]
        public async Task QUERY_ChunkedRequestExtensions_Are_Sent_By_The_Client_Worker()
        {

            const String queryContent = "client-worker";

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.QUERY(
                                   HTTPPath.Root + "query" + "extensions",
                                   queryContent.ToUTF8Bytes(),
                                   HTTPContentType.Text.PLAIN,
                                   RequestBuilder: requestBuilder => {
                                       requestBuilder.TransferEncoding = "chunked";
                                       requestBuilder.UseChunkWorker   = true;
                                       requestBuilder.ChunkWorker      = async (request, stream) => {
                                                                               await stream.WriteAsync(
                                                                                         queryContent.ToUTF8Bytes(),
                                                                                         [
                                                                                             new KeyValuePair<String, String?>("part", "one"),
                                                                                             new KeyValuePair<String, String?>("part", "two words")
                                                                                         ],
                                                                                         CancellationToken.None
                                                                                     );

                                                                               await stream.Finish(CancellationToken: CancellationToken.None);
                                                                           };
                                   }
                               );

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("client-worker|one|two words"));

        }

        #endregion


        #region QUERY_ChunkedRequestExtensions_Are_Exposed_To_The_Handler()

        [Test]
        public async Task QUERY_ChunkedRequestExtensions_Are_Exposed_To_The_Handler()
        {

            var httpClient   = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));
            var httpResponse = await httpClient.QUERY(
                                   HTTPPath.Root + "query" + "extensions",
                                   "5;part=one;part=\"two words\"\r\nhello\r\n0\r\n\r\n".ToUTF8Bytes(),
                                   HTTPContentType.Text.PLAIN,
                                   RequestBuilder: requestBuilder => requestBuilder.TransferEncoding = "chunked"
                               );

            Assert.That(httpResponse.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(httpResponse.HTTPBodyAsUTF8String, Is.EqualTo("hello|one|two words"));

        }

        #endregion


        #region Test_ChunkedEncoding_chunkedTrailerHeaders()

        [Test]
        public async Task Test_ChunkedEncoding_chunkedTrailerHeaders()
        {

            var httpClient    = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

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
            Assert.That(httpResponse.TrailingHeaders.TryGetValue("X-Message-Length", out var messageLength), Is.True);
            Assert.That(messageLength, Is.EqualTo("13"));
            Assert.That(httpResponse.TrailingHeaders.TryGetValue("X-Protocol-Version", out var protocolVersion), Is.True);
            Assert.That(protocolVersion, Is.EqualTo("1.0"));

        }

        #endregion


    }

}
