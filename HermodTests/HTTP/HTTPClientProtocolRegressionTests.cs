/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Net;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Regression tests for HTTP protocol behavior of the Hermod HTTP client.
    /// </summary>
    /// <remarks>
    /// The rejections below expect 0-ClientError, not 400 Bad Request. A status
    /// code the client wrote for itself is not a verdict from the server: these
    /// servers answered "200 OK" and then truncated the body, or sent a status
    /// line this client would not parse. Calling that 400 reported them as
    /// having refused the request, which none of them did — and left a caller
    /// unable to tell such a 400 from a real one. AHTTPClient does not invent
    /// server status codes any more; everything it writes itself is code 0.
    /// </remarks>
    [TestFixture]
    public class HTTPClientProtocolRegressionTests
    {

        [TestCase("100 Continue",  false)]
        [TestCase("100 Continue",  true)]
        [TestCase("103 Early Hints", false)]
        public async Task GET_Skips_Headerless_InformationalResponses_And_Returns_Final_Response(String   informationalStatus,
                                                                                                    Boolean  sendResponsesSeparately)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendHeaderlessInformationalResponse(
                                 listener,
                                 $"HTTP/1.1 {informationalStatus}\r\n\r\n",
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Length: 2\r\n" +
                                 "Connection: close\r\n\r\n" +
                                 "OK",
                                 sendResponsesSeparately
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("OK"));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Rejects_Invalid_Informational_Response_Header()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendHeaderlessInformationalResponse(
                                 listener,
                                 "HTTP/1.1 100 Continue\r\nMalformedHeader\r\n\r\n",
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Length: 2\r\n" +
                                 "Connection: close\r\n\r\n" +
                                 "OK",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task GET_Reads_CloseDelimited_Response_Body(Boolean sendBodySeparately)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Type: text/plain; charset=utf-8\r\n" +
                                 "Connection: close\r\n\r\n",
                                 "close-delimited response body",
                                 sendBodySeparately
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("close-delimited response body"));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Does_Not_Reuse_Connection_When_Close_And_KeepAlive_Are_Both_Present()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Length: 2\r\n" +
                                 "Connection: close, keep-alive\r\n\r\n",
                                 "OK",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK));
            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("OK"));
            Assert.That(httpClient.IsHTTPConnected,    Is.False,
                        "The close token must take precedence over keep-alive.");

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Rejects_Truncated_ContentLength_Response()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Length: 5\r\n" +
                                 "Connection: close\r\n\r\n",
                                 "abc",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));
            Assert.That(httpClient.IsHTTPConnected, Is.False);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Rejects_Truncated_Chunked_Response()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Transfer-Encoding: chunked\r\n" +
                                 "Connection: close\r\n\r\n",
                                 "5\r\nabc",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));
            Assert.That(httpClient.IsHTTPConnected, Is.False);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Rejects_Chunked_Response_With_Forbidden_Trailer_Field()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Transfer-Encoding: chunked\r\n" +
                                 "Connection: close\r\n\r\n",
                                 "2\r\nOK\r\n0\r\nContent-Length: 2\r\n\r\n",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));
            Assert.That(httpClient.IsHTTPConnected, Is.False);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_EventStream_Ignores_Comments_And_Unknown_Fields_And_Reports_Retry()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 "Content-Type: text/event-stream\r\n" +
                                 "Connection: close\r\n\r\n",
                                 ": keep-alive\n\n" +
                                 "retry: 25\n" +
                                 "unknown: ignored\n\n" +
                                 "id: 9\n" +
                                 "event: update\n" +
                                 "data: {\"message\":\"ok\"}\n\n",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response      = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));
            var retryInterval = (TimeSpan?) null;
            var events        = await HTTPEventSource<Newtonsoft.Json.Linq.JObject>.ParseHTTPResponseStream(
                                          response,
                                          RetryInterval: retry => retryInterval = retry
                                      );

            Assert.That(events.Count,                    Is.EqualTo(1));
            Assert.That(events[0].Id,                    Is.EqualTo(9));
            Assert.That(events[0].Subevent,              Is.EqualTo("update"));
            Assert.That(events[0].Data["message"]?.ToString(), Is.EqualTo("ok"));
            Assert.That(retryInterval,                   Is.EqualTo(TimeSpan.FromMilliseconds(25)));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [TestCase(204, "No Content")]
        [TestCase(205, "Reset Content")]
        [TestCase(304, "Not Modified")]
        public async Task GET_Ignores_Body_For_Bodyless_Status_Responses(Int32   statusCode,
                                                                           String  reasonPhrase)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
                                 "Content-Type: text/plain; charset=utf-8\r\n" +
                                 "Content-Length: 27\r\n" +
                                 "Connection: close\r\n\r\n",
                                 "This body must be ignored.",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode.Code, Is.EqualTo((UInt16) statusCode), response.EntirePDU);
            Assert.That(response.HTTPBody,            Is.Null.Or.Empty);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [Test]
        public async Task GET_Closes_KeepAlive_Connection_After_Bodyless_Response_With_Body()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 204 No Content\r\n" +
                                 "Content-Length: 3\r\n" +
                                 "Connection: keep-alive\r\n\r\n",
                                 "BAD",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.NoContent));
            Assert.That(response.HTTPBody,       Is.Null.Or.Empty);
            Assert.That(httpClient.IsHTTPConnected, Is.False,
                        "A connection with unread bytes after a bodyless response must not be reused.");

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [TestCase("Content-Length: 2\r\nContent-Length: 3\r\n", "OK")]
        [TestCase("Content-Length: 2\r\nTransfer-Encoding: chunked\r\n", "2\r\nOK\r\n0\r\n\r\n")]
        [TestCase("Transfer-Encoding: gzip\r\n", "OK")]
        [TestCase("Transfer-Encoding: gzip, chunked\r\n", "2\r\nOK\r\n0\r\n\r\n")]
        [TestCase("MalformedHeader\r\n", "")]
        [TestCase("Bad Header: value\r\n", "")]
        public async Task GET_Rejects_Ambiguous_Response_Framing(String responseFramingHeaders,
                                                                   String responseBody)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 responseFramingHeaders +
                                 "Connection: close\r\n\r\n",
                                 responseBody,
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [TestCase("X-Test: value\u0001injected\r\n")]
        [TestCase("X-Test: value\u007Finjected\r\n")]
        public async Task GET_Rejects_Response_Header_Values_With_Control_Characters(String responseHeader)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 "HTTP/1.1 200 OK\r\n" +
                                 responseHeader +
                                 "Connection: close\r\n\r\n",
                                 "",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        [TestCase("HTP/1.1 200 OK")]
        [TestCase("HTTP/1.1 20 OK")]
        [TestCase("HTTP/1.1 2000 Four digits")]
        [TestCase("HTTP/1.1 000 Zero")]
        public async Task GET_Rejects_Invalid_Response_Status_Line(String statusLine)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 $"{statusLine}\r\nConnection: close\r\n\r\n",
                                 "",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            // The reason, not just the code: a "000" read as a status code
            // would be a ClientError too, code 0 being that sentinel.
            Assert.That(response.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.ClientError));
            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("Invalid HTTP response status line."));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        #region An invalid status code is read as a server error (H-49)

        /// <summary>
        /// RFC 9110, Section 15: a status code outside 100-599 is invalid, and
        /// a client that receives one SHOULD process the response as if it had
        /// a 5xx status code. Until H-49 this client refused the status line,
        /// so a "999" came back as its own ClientError and the body was never
        /// read. Found by cache-tests (A12), whose origin answers 999 to a
        /// request it expected to be conditional.
        /// </summary>
        [TestCase("999", 999)]
        [TestCase("600", 600)]
        [TestCase("099",  99)]
        public async Task An_Invalid_Status_Code_Is_Read_As_A_Server_Error(String StatusCode, Int32 Code)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = SendResponseAndClose(
                                 listener,
                                 $"HTTP/1.1 {StatusCode} Not One Of Ours\r\nContent-Length: 5\r\nConnection: close\r\n\r\n",
                                 "hello",
                                 false
                             );

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode.Code,          Is.EqualTo(Code));
                Assert.That(response.HTTPStatusCode.IsServerError, Is.True);
                Assert.That(response.HTTPStatusCode.IsInvalid,     Is.True);
                Assert.That(response.HTTPBodyAsUTF8String,         Is.EqualTo("hello"));
            });

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        /// <summary>
        /// The response with the invalid status code is framed and read like any
        /// other, so the connection carries the next request. Refusing it had
        /// dropped the connection with the body still in the socket.
        /// </summary>
        [Test]
        public async Task An_Invalid_Status_Code_Leaves_The_Connection_Usable()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = Task.Run(async () => {

                using var tcpClient = await listener.AcceptTcpClientAsync();
                await using var stream = tcpClient.GetStream();

                foreach (var response in new[] {
                                             "HTTP/1.1 999 304 Not Generated\r\nContent-Length: 5\r\n\r\nfirst",
                                             "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nsecond"
                                         })
                {

                    var buffer  = new Byte[4096];
                    var request = "";

                    while (!request.Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead == 0)
                            return;
                        request += Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    }

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                    await stream.FlushAsync();

                }

            });

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var first  = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));
            var second = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.Multiple(() => {
                Assert.That(first. HTTPStatusCode.Code,  Is.EqualTo(999));
                Assert.That(first. HTTPBodyAsUTF8String, Is.EqualTo("first"));
                Assert.That(second.HTTPStatusCode,       Is.EqualTo(HTTPStatusCode.OK), "The second request needs the connection the first one left");
                Assert.That(second.HTTPBodyAsUTF8String, Is.EqualTo("second"));
            });

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }

        #endregion

        [Test]
        public async Task POST_Does_Not_Send_Body_After_Expectation_Failed()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = RejectExpectationAndCheckForRequestBody(listener);

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.POST(
                                     HTTPPath.Root,
                                     Encoding.UTF8.GetBytes("request body must not be sent"),
                                     HTTPContentType.Text.PLAIN,
                                     RequestBuilder: requestBuilder => requestBuilder.Expect = "100-continue",
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ExpectationFailed));
            Assert.That(await serverTask.WaitAsync(TimeSpan.FromSeconds(3)), Is.False,
                        "The request body was sent although the server rejected the expectation.");

        }

        #region A request without content states no length unless its method anticipates content

        /// <summary>
        /// RFC 9110, Section 8.6: a user agent SHOULD NOT send Content-Length
        /// when the request has no content and the method anticipates none, and
        /// SHOULD send it when the method defines a meaning for content, 0
        /// included. Until H-41 every one of these went out with
        /// "Content-Length: 0", because the builder's Content setter writes 0
        /// for null.
        /// </summary>
        [TestCase("GET",     false)]
        [TestCase("HEAD",    false)]
        [TestCase("DELETE",  false)]
        [TestCase("OPTIONS", false)]
        [TestCase("TRACE",   false)]
        [TestCase("POST",    true)]
        [TestCase("PUT",     true)]
        [TestCase("PATCH",   true)]
        public async Task ARequestWithoutContentStatesNoLengthUnlessItsMethodAnticipatesContent(String   Method,
                                                                                               Boolean  ExpectLength)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var served = Task.Run(async () => {

                using var tcp    = await listener.AcceptTcpClientAsync();
                var       stream = tcp.GetStream();
                var       head   = new StringBuilder();
                var       one    = new Byte[1];

                while (!head.ToString().EndsWith("\r\n\r\n") &&
                       await stream.ReadAsync(one) == 1)
                {
                    head.Append((Char) one[0]);
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n"));
                await stream.FlushAsync();

                return head.ToString();

            });

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 0
                                   );

            var response = await httpClient.RunRequest(HTTPMethod.Parse(Method),
                                                       HTTPPath.Root + "x",
                                                       RequestTimeout: TimeSpan.FromSeconds(5));

            var request  = await served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.NoContent));
                Assert.That(request,                 Does.StartWith($"{Method} /x HTTP/1.1\r\n"));
                if (ExpectLength)
                    Assert.That(request, Does.Contain("\r\nContent-Length: 0\r\n"), request);
                else
                    Assert.That(request, Does.Not.Contain("Content-Length"),        request);
            });

        }

        #endregion

        #region ARefusedConnection_IsReportedAsSuchOverTLSToo()

        /// <summary>
        /// The same closed port, once without TLS and once with. The plain client
        /// named the refusal; the TLS client reported
        /// "StartTLS.tlsStream.AuthenticateAsClientAsync: Cannot access a disposed
        /// object", because ATLSClient.ReconnectAsync passed a failed connect
        /// result to AfterSuccessfulConnect without looking at it, and the
        /// handshake then ran over the TcpClient that the failed connect had
        /// already disposed.
        /// </summary>
        [Test]
        public async Task ARefusedConnection_IsReportedAsSuchOverTLSToo()
        {

            using var closedPort   = new ClosedPort();

            using var httpClient   = new HTTPClient(
                                         URL.Parse($"http://127.0.0.1:{closedPort}"),
                                         MaxNumberOfRetries: 0
                                     );

            using var httpsClient  = new HTTPSClient(
                                         URL.Parse($"https://127.0.0.1:{closedPort}"),
                                         (sender, certificate, chain, client, errors) => TLSValidationResult.Success(),
                                         MaxNumberOfRetries: 0
                                     );

            var plainResponse   = await httpClient. GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));
            var secureResponse  = await httpsClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));

            var plainReason     = plainResponse. GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);
            var secureReason    = secureResponse.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.Multiple(() => {

                // The refusal text itself comes from the operating system and is
                // localised; "Error connecting ATCPClient" is Hermod's own.
                Assert.That(plainReason,   Does.Contain("Error connecting ATCPClient"),  plainReason);
                Assert.That(secureReason,  Does.Contain("Error connecting ATCPClient"),  secureReason);

                // And no TLS handshake was attempted over a socket that never
                // opened. This is what replaced the reason above.
                Assert.That(secureReason,  Does.Not.Contain("AuthenticateAsClientAsync"),  secureReason);

            });

        }

        #endregion

        #region MaxNumberOfRetries counts the attempts after the first (H-48)

        /// <summary>
        /// A server that reads each request head, counts it, and closes the
        /// connection without an answer, so every attempt fails - until the
        /// one numbered AnswerFrom, which gets a 200.
        /// </summary>
        private sealed class DroppingServer : IDisposable
        {

            private readonly TcpListener              listener  = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource  stop      = new ();
            private readonly Int32                    answerFrom;
            private          Int32                    requests;

            public UInt16  Port
                => (UInt16) ((IPEndPoint) listener.LocalEndpoint).Port;

            public Int32   Requests
                => Volatile.Read(ref requests);

            public DroppingServer(Int32 AnswerFrom = Int32.MaxValue)
            {
                answerFrom = AnswerFrom;
                listener.Start();
                _ = Task.Run(AcceptAll);
            }

            private async Task AcceptAll()
            {
                while (!stop.IsCancellationRequested)
                {

                    TcpClient client;

                    try
                    {
                        client = await listener.AcceptTcpClientAsync(stop.Token);
                    }
                    catch
                    {
                        return;
                    }

                    _ = Task.Run(async () => {
                        using (client)
                        {
                            try
                            {

                                var stream  = client.GetStream();
                                var buffer  = new Byte[4096];
                                var length  = 0;

                                while (!Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal))
                                {
                                    var read = await stream.ReadAsync(buffer.AsMemory(length), stop.Token);
                                    if (read == 0 || (length += read) == buffer.Length)
                                        return;
                                }

                                if (Interlocked.Increment(ref requests) >= answerFrom)
                                {
                                    await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"u8.ToArray(), stop.Token);
                                    await stream.FlushAsync(stop.Token);
                                }

                            }
                            catch
                            { }
                        }
                    });

                }
            }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
                stop.Dispose();
            }

        }

        /// <summary>
        /// MaxNumberOfRetries counted attempts: 0 sent nothing at all and
        /// answered "Giving up ... after 0 attempts", and 1 never tried again.
        /// It counts what its name says now, the attempts after the first one.
        /// </summary>
        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        public async Task MaxNumberOfRetries_CountsTheAttemptsAfterTheFirst(Int32 Retries,
                                                                            Int32 Attempts)
        {

            using var server      = new DroppingServer();
            using var httpClient  = new HTTPClient(
                                        URL.Parse($"http://127.0.0.1:{server.Port}"),
                                        MaxNumberOfRetries: (UInt16) Retries
                                    );

            var response  = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));
            var reason    = response.GetResponseBodyAsUTF8String(HTTPContentType.Text.PLAIN);

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.ClientError),  reason);
                Assert.That(server.Requests,          Is.EqualTo(Attempts),                    "requests the server received");
                Assert.That(reason,                   Does.Contain($" after {Attempts} {(Attempts == 1 ? "attempt" : "attempts")}: "));
            });

        }

        /// <summary>
        /// The default keeps the three attempts it always made: two retries.
        /// </summary>
        [Test]
        public async Task WithoutMaxNumberOfRetries_TwoRetries_ThreeAttempts()
        {

            using var server      = new DroppingServer();
            using var httpClient  = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.Port}"));

            var response  = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(httpClient.MaxNumberOfRetries,  Is.EqualTo(2));
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.ClientError));
                Assert.That(server.Requests,                Is.EqualTo(3),  "requests the server received");
            });

        }

        /// <summary>
        /// One retry is a second attempt, and a second attempt that is answered
        /// is the answer.
        /// </summary>
        [Test]
        public async Task OneRetry_IsASecondAttempt_ThatCanSucceed()
        {

            using var server      = new DroppingServer(AnswerFrom: 2);
            using var httpClient  = new HTTPClient(
                                        URL.Parse($"http://127.0.0.1:{server.Port}"),
                                        MaxNumberOfRetries: 1
                                    );

            var response  = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK),  response.EntirePDU);
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("OK"));
                Assert.That(server.Requests,                Is.EqualTo(2),                  "requests the server received");
            });

        }

        #endregion

        private static async Task SendHeaderlessInformationalResponse(TcpListener  listener,
                                                                        String       informationalResponse,
                                                                        String       finalResponse,
                                                                        Boolean      sendResponsesSeparately)
        {

            using var tcpClient = await listener.AcceptTcpClientAsync();
            await using var stream = tcpClient.GetStream();

            var buffer = new Byte[4096];
            var length = 0;

            while (length < buffer.Length)
            {

                var bytesRead = await stream.ReadAsync(buffer.AsMemory(length));
                if (bytesRead == 0)
                    break;

                length += bytesRead;

                if (Encoding.UTF8.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal))
                    break;

            }

            if (sendResponsesSeparately)
            {

                await stream.WriteAsync(Encoding.UTF8.GetBytes(informationalResponse));
                await stream.FlushAsync();
                await Task.Delay(50);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(finalResponse));

            }

            else
                await stream.WriteAsync(Encoding.UTF8.GetBytes(informationalResponse + finalResponse));

            await stream.FlushAsync();

            await Task.Delay(500);

        }

        private static async Task<Boolean> RejectExpectationAndCheckForRequestBody(TcpListener Listener)
        {

            using var tcpClient = await Listener.AcceptTcpClientAsync();
            await using var stream = tcpClient.GetStream();

            var buffer = new Byte[4096];
            var length = 0;

            while (length < buffer.Length)
            {

                var bytesRead = await stream.ReadAsync(buffer.AsMemory(length));
                if (bytesRead == 0)
                    break;

                length += bytesRead;

                if (Encoding.UTF8.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal))
                    break;

            }

            var request = Encoding.UTF8.GetString(buffer, 0, length);
            var headerEnd = request.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

            var bodyAlreadyReceived = length > headerEnd;

            await stream.WriteAsync(
                      Encoding.UTF8.GetBytes(
                          "HTTP/1.1 417 Expectation Failed\r\n" +
                          "Content-Length: 0\r\n" +
                          "Connection: close\r\n\r\n"
                      )
                  );
            await stream.FlushAsync();

            if (bodyAlreadyReceived)
                return true;

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            try
            {
                return await stream.ReadAsync(buffer, cancellationTokenSource.Token) > 0;
            }
            catch (OperationCanceledException)
            {
                return false;
            }

        }

        private static async Task SendResponseAndClose(TcpListener  listener,
                                                        String       responseHeader,
                                                        String       responseBody,
                                                        Boolean      sendBodySeparately)
        {

            using var tcpClient = await listener.AcceptTcpClientAsync();
            await using var stream = tcpClient.GetStream();

            var buffer = new Byte[4096];
            var length = 0;

            while (length < buffer.Length)
            {

                var bytesRead = await stream.ReadAsync(buffer.AsMemory(length));
                if (bytesRead == 0)
                    break;

                length += bytesRead;

                if (Encoding.UTF8.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal))
                    break;

            }

            await stream.WriteAsync(Encoding.UTF8.GetBytes(responseHeader));
            await stream.FlushAsync();

            if (sendBodySeparately)
                await Task.Delay(50);

            await stream.WriteAsync(Encoding.UTF8.GetBytes(responseBody));
            await stream.FlushAsync();

        }

    }

}
