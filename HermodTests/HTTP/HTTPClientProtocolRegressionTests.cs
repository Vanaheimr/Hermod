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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Regression tests for HTTP protocol behavior of the Hermod HTTP client.
    /// </summary>
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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));

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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(
                                     HTTPPath.Root,
                                     RequestTimeout: TimeSpan.FromSeconds(3)
                                 );

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));
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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));

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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }


        [TestCase("HTP/1.1 200 OK")]
        [TestCase("HTTP/1.1 20 OK")]
        [TestCase("HTTP/1.1 600 Out of range")]
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
                                       MaxNumberOfRetries: 1
                                   );

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(3));

            Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));

            await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        }


        [Test]
        public async Task POST_Does_Not_Send_Body_After_Expectation_Failed()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var serverTask = RejectExpectationAndCheckForRequestBody(listener);

            using var httpClient = new HTTPClient(
                                       URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                       MaxNumberOfRetries: 1
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
