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

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using NUnit.Framework;

using Org.BouncyCastle.Crypto;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Socket-level regression tests for the Hermod HTTP test server.
    /// </summary>
    [TestFixture]
    public class HTTPServerSocketRegressionTests
    {

        #region TLS handshake timeout does not block accept loop

        [Test]
        public async Task Slow_TLS_Handshake_Does_Not_Block_Following_Accepts()
        {

            var server        = CreateHTTPSServer(TimeSpan.FromSeconds(2));
            var slowClient    = new TcpClient(AddressFamily.InterNetwork);
            var anotherClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {

                await slowClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients > 0,
                        TimeSpan.FromSeconds(1)
                    ), Is.True, "The first slow TLS client was never accepted.");

                await anotherClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients >= 2,
                        TimeSpan.FromMilliseconds(500)
                    ), Is.True, "A half-open TLS client must not block later HTTPS accepts.");

            }
            finally
            {
                slowClient.Dispose();
                anotherClient.Dispose();
                await server.Stop();
            }

        }

        #endregion

        #region TLS handshake timeout cleans active clients

        [Test]
        public async Task Timed_Out_TLS_Handshake_Removes_Active_Client()
        {

            var server     = CreateHTTPSServer(TimeSpan.FromMilliseconds(750));
            var slowClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {

                await slowClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients > 0,
                        TimeSpan.FromSeconds(1)
                    ), Is.True, "The slow TLS client was never observed as active.");

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients == 0,
                        TimeSpan.FromSeconds(4)
                    ), Is.True, "A timed-out TLS handshake must be removed from activeClients.");

            }
            finally
            {
                slowClient.Dispose();
                await server.Stop();
            }

        }

        #endregion

        #region IPv4 and IPv6 accept loops

        [Test]
        public async Task AcceptLoops_Handle_IPv4_And_IPv6_Clients()
        {

            var server = CreateHTTPServer(IPvXAddress.Localhost);

            try
            {

                var ipv4Response = await SendRawHTTPRequest(
                                        System.Net.IPAddress.Loopback,
                                        server.TCPPort,
                                        "localhost"
                                    );

                Assert.That(ipv4Response.Contains("200 OK"), Is.True, ipv4Response);

                try
                {

                    var ipv6Response = await SendRawHTTPRequest(
                                            System.Net.IPAddress.IPv6Loopback,
                                            server.TCPPort,
                                            "localhost"
                                        );

                    Assert.That(ipv6Response.Contains("200 OK"), Is.True, ipv6Response);

                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressFamilyNotSupported ||
                                                e.SocketErrorCode == SocketError.NetworkUnreachable       ||
                                                e.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    Assert.Inconclusive($"IPv6 loopback is not available on this host: {e.SocketErrorCode}");
                }

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Invalid HTTP message framing is rejected

        [TestCase("Content-Length: 4\r\nContent-Length: 4\r\n\r\ntest")]
        [TestCase("Content-Length: 4, 4\r\n\r\ntest")]
        [TestCase("Content-Length: 4\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n")]
        [TestCase("Transfer-Encoding: gzip\r\n\r\n")]
        [TestCase("Transfer-Encoding: chunked, gzip\r\n\r\n")]
        [TestCase("Transfer-Encoding: , chunked\r\n\r\n")]
        public async Task Invalid_HTTP_Message_Framing_Is_Rejected(String FramingHeadersAndBody)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"POST / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n{FramingHeadersAndBody}"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region Expect 100-continue handshake

        [Test]
        public async Task Expect_100Continue_Is_Sent_Before_The_Request_Body_Is_Read()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                                  System.Net.IPAddress.Loopback,
                                                  server.TCPPort
                                              );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 4\r\nExpect: 100-continue\r\nConnection: close\r\n\r\n",
                          cts.Token
                      );

                var continueResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(continueResponse.StatusCode, Is.EqualTo(100));
                Assert.That(continueResponse.Body,       Is.Empty);

                await rawClient.SendAsync("test", cts.Token);

                var finalResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(finalResponse.StatusCode, Is.EqualTo(200));

            }
            finally
            {
                await server.Stop();
            }

        }

        [TestCase("HTTP/1.1", "something-else")]
        [TestCase("HTTP/1.1", "100-continue, something-else")]
        [TestCase("HTTP/1.0", "something-else")]
        public async Task Unsupported_Expect_Header_Is_Rejected_Before_Reading_Body(String ProtocolVersion,
                                                                                      String Expectation)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                         server.TCPPort,
                                         $"POST / {ProtocolVersion}\r\nHost: localhost\r\nContent-Length: 4\r\nExpect: {Expectation}\r\nConnection: close\r\n\r\n"
                                     );

                Assert.That(response,
                            Does.StartWith($"{ProtocolVersion} 417"),
                            response);

            }
            finally
            {
                await server.Stop();
            }

        }

        [Test]
        public async Task HTTP_1_0_Expect_100Continue_Does_Not_Send_An_Interim_Response()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.0\r\nContent-Length: 4\r\nExpect: 100-continue\r\n\r\ntest"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 200"), response);
                Assert.That(response, Does.Not.Contain("100 Continue"), response);
                Assert.That(response, Does.Contain("tset"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Server-wide OPTIONS is bodyless

        [Test]
        public async Task ServerWide_OPTIONS_Is_NoContent_And_Has_No_Body()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "OPTIONS * HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                var headerEnd = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(headerEnd, Is.GreaterThan(0), response);
                Assert.That(response[(headerEnd + 4)..], Is.Empty, response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Segmented raw request and framed response

        [Test]
        public async Task Segmented_Request_Is_Parsed_And_Response_Is_ContentLength_Framed()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                                  System.Net.IPAddress.Loopback,
                                                  server.TCPPort
                                              );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendSegmentsAsync(
                          [
                              HTTPRawSocketSegment.Text("GE"),
                              HTTPRawSocketSegment.Text("T / HTTP/1.1\r\nHo"),
                              HTTPRawSocketSegment.Text("st: localhost\r\nConnection: close\r\n\r\n")
                          ],
                          cts.Token
                      );

                var response = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(response.StatusCode,     Is.EqualTo(200));
                Assert.That(response.ContentLength,  Is.EqualTo((UInt64) "Hello World!".Length));
                Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo("Hello World!"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Raw pipelined requests receive separately framed responses

        [Test]
        public async Task Pipelined_Requests_Are_Processed_In_Order()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             ConnectionType.KeepAlive
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                                  System.Net.IPAddress.Loopback,
                                                  server.TCPPort
                                              );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 3\r\n\r\none" +
                          "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 3\r\n\r\ntwo",
                          cts.Token
                      );

                var firstResponse  = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);
                var secondResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(firstResponse.StatusCode,   Is.EqualTo(200));
                Assert.That(secondResponse.StatusCode,  Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(firstResponse.Body),  Is.EqualTo("eno"));
                Assert.That(Encoding.UTF8.GetString(secondResponse.Body), Is.EqualTo("owt"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Pipelined chunked requests are delimited before the following request

        [Test]
        public async Task Pipelined_Chunked_Request_Is_Delimited_Before_The_Following_Request()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             ConnectionType.KeepAlive
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                                  System.Net.IPAddress.Loopback,
                                                  server.TCPPort
                                              );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\nX-Checksum: 42\r\n\r\n" +
                          "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n",
                          cts.Token
                      );

                var firstResponse  = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);
                var secondResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(firstResponse.StatusCode,  Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(firstResponse.Body), Is.EqualTo("olleh"));
                Assert.That(secondResponse.StatusCode, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(secondResponse.Body), Is.EqualTo("Hello World!"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Invalid leading pipeline request closes the connection

        [Test]
        public async Task Invalid_Leading_Pipeline_Request_Closes_Before_The_Following_Request()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\n\r\nG\r\nhello\r\n0\r\n\r\n" +
                                   "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);
                Assert.That(response.Split("HTTP/1.1 ", StringSplitOptions.None).Length - 1, Is.EqualTo(1), response);
                Assert.That(response, Does.Not.Contain("Hello World!"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Method not allowed responses advertise allowed methods

        [Test]
        public async Task MethodNotAllowed_Response_Contains_Allow_Header()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "PUT / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 405"), response);
                Assert.That(response,
                            Does.Match("(?m)^Allow: (?=.*\\bGET\\b)(?=.*\\bHEAD\\b)(?=.*\\bPOST\\b).*$"),
                            response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Invalid Host and header syntax is rejected

        [TestCase("GET / HTTP/1.1\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: localhost\r\nHost: example.org\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: \r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost : localhost\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: localhost\r\nMalformedHeader\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: localhost\r\nX-Test: value\u0001injected\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: localhost\r\nX-Test: value\u007Finjected\r\nConnection: close\r\n\r\n")]
        [TestCase("GET / HTTP/1.1\r\nHost: localhost\r\nX-Test: value\r\n continued\r\nConnection: close\r\n\r\n")]
        public async Task Invalid_Host_And_Header_Syntax_Are_Rejected(String Request)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(server.TCPPort, Request);

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Invalid request-target forms are rejected

        [TestCase("GET",     "/a//b",                 400)]
        [TestCase("GET",     "/./a",                  400)]
        [TestCase("GET",     "/%2e",                  400)]
        [TestCase("GET",     "/%2e%2e",               400)]
        [TestCase("GET",     "/a%2Fb",                400)]
        [TestCase("GET",     "/a%5Cb",                400)]
        [TestCase("GET",     "/%",                    400)]
        [TestCase("GET",     "/%GG",                  400)]
        [TestCase("GET",     "/%25%32%46",            400)]
        [TestCase("GET",     "/a#fragment",           400)]
        [TestCase("GET",     "http://example.org/a",   400)]
        [TestCase("GET",     "*",                     400)]
        [TestCase("CONNECT", "example.org:443",        501)]
        public async Task Invalid_Request_Target_Forms_Are_Rejected(String HTTPMethod,
                                                                     String RequestTarget,
                                                                     Int32  ExpectedStatusCode)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"{HTTPMethod} {RequestTarget} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response,
                            Does.StartWith($"HTTP/1.1 {ExpectedStatusCode}"),
                            response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Configured request header limits are enforced

        [Test]
        public async Task Request_Header_Section_Exceeding_Configured_Limit_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPHeaderSize: 64
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"GET / HTTP/1.1\r\nHost: localhost\r\nX-Padding: {new String('x', 64)}\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 431"), response);

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Request_Header_Field_Line_Exceeding_Configured_Limit_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPHeaderLineLength:    16,
                             MaxHTTPRequestTargetLength: 16
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"GET / HTTP/1.1\r\nHost: localhost\r\nX-Long: {new String('x', 16)}\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 431"), response);

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Request_Exceeding_Configured_Header_Count_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPHeaderCount: 1
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.1\r\nHost: localhost\r\nX-Extra: value\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 431"), response);

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Request_Target_Exceeding_Configured_Limit_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPHeaderLineLength:    64,
                             MaxHTTPRequestTargetLength: 4
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET /long HTTP/1.1\r\nHost: localhost\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 414"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Invalid Content-Length syntax is rejected

        [TestCase("abc")]
        [TestCase("+1")]
        [TestCase("-1")]
        [TestCase("0x10")]
        [TestCase("1 0")]
        [TestCase("")]
        [TestCase("18446744073709551616")]
        public async Task Invalid_ContentLength_Syntax_Is_Rejected(String ContentLength)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: {ContentLength}\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Configured request read timeouts are enforced

        [Test]
        public async Task Incomplete_Request_Header_Is_Closed_After_Configured_Timeout()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             HeaderReadTimeout: TimeSpan.FromMilliseconds(150)
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "GET / HTTP/1.1\r\nHost: localhost\r\n",
                          cts.Token
                      );

                Assert.ThrowsAsync<EndOfStreamException>(async () =>
                    await rawClient.ReadResponseAsync(CancellationToken: cts.Token)
                );

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Incomplete_ContentLength_Request_Body_Returns_RequestTimeout()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             BodyReadTimeout: TimeSpan.FromMilliseconds(150)
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 4\r\nConnection: close\r\n\r\nab"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 408"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Configured request body and chunk metadata limits are enforced

        [Test]
        public async Task ContentLength_Request_Body_Exceeding_Configured_Limit_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPBodySize: 3
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 4\r\n\r\ntest"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 413"), response);

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Truncated_ContentLength_Request_Body_Is_Rejected()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "POST / HTTP/1.1\r\nHost: localhost\r\nContent-Length: 5\r\n\r\nabc",
                          cts.Token
                      );
                rawClient.ShutdownSend();

                var response = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(response.StatusCode, Is.EqualTo(400));
                Assert.That(response.Headers["Connection"][0], Is.EqualTo("close"));

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Truncated_Chunked_Request_Body_Is_Rejected()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nabc",
                          cts.Token
                      );
                rawClient.ShutdownSend();

                var response = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(response.StatusCode, Is.EqualTo(400));
                Assert.That(response.Headers["Connection"][0], Is.EqualTo("close"));

            }
            finally
            {
                await server.Stop();
            }

        }


        [Test]
        public async Task Chunked_Request_Body_Exceeding_Configured_Limit_Is_Rejected()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             MaxHTTPBodySize: 3
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n4\r\ntest\r\n0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 413"), response);

            }
            finally
            {
                await server.Stop();
            }

        }


        [TestCase("chunk-size-line")]
        [TestCase("trailer-line")]
        [TestCase("trailer-count")]
        [TestCase("trailer-size")]
        [TestCase("metadata-size")]
        public async Task Chunk_Metadata_Exceeding_Configured_Limit_Is_Rejected(String limit)
        {

            var server = limit switch {
                             "chunk-size-line" => CreateHTTPServer(IPv4Address.Localhost, MaxHTTPChunkSizeLineLength:    1),
                             "trailer-line"    => CreateHTTPServer(IPv4Address.Localhost, MaxHTTPChunkTrailerLineLength: 3),
                             "trailer-count"   => CreateHTTPServer(IPv4Address.Localhost, MaxHTTPChunkTrailerCount:      1),
                             "trailer-size"    => CreateHTTPServer(IPv4Address.Localhost, MaxHTTPChunkTrailerSize:       4),
                             "metadata-size"   => CreateHTTPServer(IPv4Address.Localhost, MaxHTTPChunkMetadataSize:      1),
                             _                  => throw new ArgumentOutOfRangeException(nameof(limit), limit, null)
                         };

            var chunkedBody = limit switch {
                                  "chunk-size-line" => "00\r\n\r\n",
                                  "trailer-line"    => "0\r\nX:12\r\n\r\n",
                                  "trailer-count"   => "0\r\nX: 1\r\nY: 2\r\n\r\n",
                                  "trailer-size"    => "0\r\nX: 1\r\n\r\n",
                                  "metadata-size"   => "0\r\n\r\n",
                                  _                  => throw new ArgumentOutOfRangeException(nameof(limit), limit, null)
                              };

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\n\r\n" + chunkedBody
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 Content-Length request bodies are supported

        [Test]
        public async Task HTTP_1_0_ContentLength_Request_Body_Is_Processed_And_Connection_Is_Closed()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.0\r\nContent-Length: 5\r\n\r\nhello"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 200"), response);
                Assert.That(response, Does.Contain("olleh"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 chunked requests are rejected

        [Test]
        public async Task HTTP_1_0_Chunked_Request_Is_Rejected()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.0\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 400"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 automatically chunked responses fall back to close-delimited bodies

        [Test]
        public async Task HTTP_1_0_AutomaticallyChunked_Response_Is_CloseDelimited()
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );
            var httpAPI = new HTTPAPI(server);

            httpAPI.AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                            new HTTPResponse.Builder(request) {
                                                HTTPStatusCode             = HTTPStatusCode.OK,
                                                TransferEncoding           = "chunked",
                                                Content                    = "Hello World!".ToUTF8Bytes(),
                                                Connection                 = ConnectionType.Close,
                                                AutomaticallyChunkContent  = true,
                                                TrailingHeaders            = {
                                                    ["X-Message-Length"] = "13"
                                                }
                                            }.AsImmutable
                                        )
            );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 200"), response);
                Assert.That(response, Does.Not.Contain("Transfer-Encoding:"), response);
                Assert.That(response, Does.Not.Contain("Trailer:"), response);
                Assert.That(response, Does.Contain("Hello World!"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 manually chunked responses are rejected

        [Test]
        public async Task HTTP_1_0_ManuallyChunked_Response_Is_Rejected()
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            var httpAPI = new HTTPAPI(server);

            httpAPI.AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                            new HTTPResponse.Builder(request) {
                                                HTTPStatusCode  = HTTPStatusCode.OK,
                                                TransferEncoding = "chunked",
                                                ContentType     = HTTPContentType.Text.PLAIN,
                                                ContentStream   = new ChunkedTransferEncodingStream(request.NetworkStream!, true),
                                                Connection      = ConnectionType.Close,
                                                ChunkWorker     = async (response, stream) => {
                                                                      await stream.WriteAsync("Hello World!".ToUTF8Bytes(), response.CancellationToken);
                                                                      await stream.Finish(CancellationToken: response.CancellationToken);
                                                                  }
                                            }.AsImmutable
                                        )
            );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 500"), response);
                Assert.That(response, Does.Not.Contain("Transfer-Encoding:"), response);
                Assert.That(response, Does.Not.Contain("\r\nC\r\nHello World!"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 bodyless responses preserve status and framing semantics

        [TestCase("HEAD / HTTP/1.0\r\n\r\n", 200)]
        public async Task HTTP_1_0_Bodyless_Response_Has_No_Body(String Request,
                                                                  Int32  StatusCode)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response  = await SendRawRequest(server.TCPPort, Request);
                var headerEnd = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                Assert.That(response, Does.StartWith($"HTTP/1.0 {StatusCode}"), response);
                Assert.That(headerEnd, Is.GreaterThan(0), response);
                Assert.That(response[(headerEnd + 4)..], Is.Empty, response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP/1.0 requests are supported

        [Test]
        public async Task HTTP_1_0_Request_Is_Processed_And_Connection_Is_Closed()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.0\r\nHost: localhost\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 200"), response);
                Assert.That(response, Does.Contain("Hello World!"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        [Test]
        public async Task HTTP_1_0_Request_Does_Not_Require_A_Host_Header()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.0 200"), response);
                Assert.That(response, Does.Contain("Hello World!"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        [Test]
        public async Task HTTP_1_0_KeepAlive_Is_Honoured_When_Negotiated_In_Both_Directions()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             ConnectionType.KeepAlive
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await rawClient.SendAsync(
                          "GET / HTTP/1.0\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n",
                          cts.Token
                      );

                var firstResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(firstResponse.StatusCode, Is.EqualTo(200));
                Assert.That(firstResponse.Headers["Connection"][0], Is.EqualTo("keep-alive"));

                await rawClient.SendAsync(
                          "GET / HTTP/1.0\r\nHost: localhost\r\nConnection: close\r\n\r\n",
                          cts.Token
                      );

                var secondResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                Assert.That(secondResponse.StatusCode, Is.EqualTo(200));
                Assert.That(secondResponse.Headers["Connection"][0], Is.EqualTo("close"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Connection close overrides keep-alive

        [Test]
        public async Task Connection_Close_Token_Overrides_KeepAlive()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             ConnectionType.KeepAlive
                         );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive, close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);
                Assert.That(response, Does.Contain("Connection: close"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Non-canonical HTTP-version syntax is rejected

        [TestCase("HTTP/01.1")]
        [TestCase("HTTP/1.01")]
        [TestCase("http/1.1")]
        [TestCase("HTTP//1.1")]
        public async Task NonCanonical_HTTP_Version_Syntax_Is_Rejected(String HTTPVersion)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"GET / {HTTPVersion}\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region Chunk extensions are emitted on the wire

        [Test]
        public async Task ChunkedTransferEncodingStream_Writes_Extensions_On_The_Wire()
        {

            await using var wireStream    = new MemoryStream();
            await using var chunkedStream = new ChunkedTransferEncodingStream(wireStream, LeaveInnerStreamOpen: true);

            await chunkedStream.WriteAsync(
                      "Hello".ToUTF8Bytes(),
                      [
                          new KeyValuePair<String, String?>("part",    "one"),
                          new KeyValuePair<String, String?>("part",    "two"),
                          new KeyValuePair<String, String?>("flag",    null),
                          new KeyValuePair<String, String?>("comment", "two words")
                      ]
                  );

            await chunkedStream.Finish();

            Assert.That(
                Encoding.ASCII.GetString(wireStream.ToArray()),
                Is.EqualTo("5;part=one;part=two;flag;comment=\"two words\"\r\nHello\r\n0\r\n\r\n")
            );

        }

        #endregion


        #region Forbidden outgoing trailer fields are rejected before the terminal chunk

        [Test]
        public async Task Forbidden_Outgoing_Trailer_Fields_Are_Rejected_Before_Writing()
        {

            await using var wireStream    = new MemoryStream();
            await using var chunkedStream = new ChunkedTransferEncodingStream(wireStream, LeaveInnerStreamOpen: true);

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await chunkedStream.Finish(
                          new Dictionary<String, String> {
                              ["Content-Length"] = "13"
                          }
                      )
            );

            Assert.That(wireStream.Length, Is.Zero);

        }

        #endregion


        #region Invalid chunk extensions are rejected

        [TestCase("5;")]
        [TestCase("5;=invalid")]
        [TestCase("5;name@value")]
        [TestCase("5;name=\u0001")]
        public async Task Invalid_Chunk_Extensions_Are_Rejected(String ChunkSizeLine)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n{ChunkSizeLine}\r\nhello\r\n0\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region Malformed chunks are rejected

        [TestCase("G\r\nhello\r\n0\r\n\r\n")]
        [TestCase("5\nhello\r\n0\r\n\r\n")]
        [TestCase("5\r\nhelloX0\r\n\r\n")]
        public async Task Malformed_Chunks_Are_Rejected(String ChunkedBody)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n{ChunkedBody}"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region Forbidden request trailer fields are rejected

        [TestCase("Authorization: Basic dGVzdDp0ZXN0")]
        [TestCase("Content-Length: 5")]
        [TestCase("Host: attacker.example")]
        [TestCase("Transfer-Encoding: chunked")]
        public async Task Forbidden_Request_Trailer_Fields_Are_Rejected(String Trailer)
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   $"POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n5\r\nhello\r\n0\r\n{Trailer}\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 400"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Chunked request bodies are decoded before routing

        [Test]
        public async Task Chunked_Request_Body_Is_Decoded_Before_Routing()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "POST / HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n5;part=one\r\nHello\r\n1\r\n \r\n6\r\nWorld!\r\n0\r\nX-Checksum: 42\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);
                Assert.That(response, Does.Contain("!dlroW olleH"),    response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region 204, 205 and 304 responses do not contain a response body

        [TestCase(204)]
        [TestCase(205)]
        [TestCase(304)]
        public async Task Bodyless_Status_Response_Does_Not_Contain_A_Body(Int32 statusCode)
        {

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            var httpAPI = new HTTPAPI(server);

            httpAPI.AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                            new HTTPResponse.Builder(request) {
                                                HTTPStatusCode  = statusCode == 204
                                                                      ? HTTPStatusCode.NoContent
                                                                      : statusCode == 205
                                                                            ? HTTPStatusCode.ResetContent
                                                                            : HTTPStatusCode.NotModified,
                                                Server          = "Hermod Test Server",
                                                Date            = Timestamp.Now,
                                                ContentType     = HTTPContentType.Text.PLAIN,
                                                Content         = "This body must not be sent.".ToUTF8Bytes(),
                                                Connection      = ConnectionType.Close
                                            }.AsImmutable
                                        )
            );

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                var headerEnd = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                Assert.That(response, Does.StartWith($"HTTP/1.1 {statusCode}"), response);
                Assert.That(headerEnd, Is.GreaterThan(0), response);
                Assert.That(response[(headerEnd + 4)..], Is.Empty, response);

                if (statusCode == 204)
                    Assert.That(response, Does.Not.Contain("Content-Length:"), response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HEAD responses do not contain a response body

        [Test]
        public async Task HEAD_Response_Does_Not_Contain_A_Body()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "HEAD / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                var headerEnd = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);
                Assert.That(headerEnd, Is.GreaterThan(0), response);
                Assert.That(response[(headerEnd + 4)..], Is.Empty, response);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region SSE client abort stops the writer and cleans the connection

        [Test]
        public async Task SSE_Client_Abort_Stops_Worker_And_Cleans_Connection()
        {

            var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var server = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            var httpAPI = new HTTPAPI(server);

            httpAPI.AddHandler(
                HTTPPath.Root,
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                            new HTTPResponse.Builder(request) {
                                                HTTPStatusCode  = HTTPStatusCode.OK,
                                                ContentType     = HTTPContentType.Text.EVENTSTREAM,
                                                CacheControl    = "no-cache",
                                                Connection      = ConnectionType.Close,
                                                HTTPSSEWorker   = async (response, writer) => {

                                                                      workerStarted.TrySetResult();

                                                                      try
                                                                      {

                                                                          while (true)
                                                                          {
                                                                              await writer.WriteHeartbeat(
                                                                                        "client-abort",
                                                                                        response.CancellationToken
                                                                                    );

                                                                              await Task.Delay(
                                                                                        TimeSpan.FromMilliseconds(10),
                                                                                        response.CancellationToken
                                                                                    );
                                                                          }

                                                                      }
                                                                      finally
                                                                      {
                                                                          workerStopped.TrySetResult();
                                                                      }

                                                                  }
                                            }.AsImmutable
                                        )
            );

            try
            {

                var tcpClient = new TcpClient(AddressFamily.InterNetwork) {
                                    LingerState = new LingerOption(true, 0)
                                };

                try
                {

                    await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                    await using var stream = tcpClient.GetStream();
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"));

                    await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

                    var buffer = new Byte[1024];
                    var read   = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(1));

                    Assert.That(read, Is.GreaterThan(0), "The SSE response did not send a status line or heartbeat.");
                    Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Does.StartWith("HTTP/1.1 200"));

                    tcpClient.Dispose();

                    await workerStopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

                    Assert.That(await WaitUntilAsync(
                            () => server.NumberOfConnectedClients == 0,
                            TimeSpan.FromSeconds(2)
                        ), Is.True, "An aborted SSE connection must not remain in activeClients.");

                }
                finally
                {
                    tcpClient.Dispose();
                }

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region HTTP activeClients cleanup

        [Test]
        public async Task Completed_HTTP_Requests_Remove_HTTPConnection_ActiveClient()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                using var httpClient = new HttpClient(
                                           new HttpClientHandler {
                                               UseProxy = false
                                           }
                                       ) {
                                           Timeout = TimeSpan.FromSeconds(3)
                                       };

                for (var i = 0; i < 3; i++)
                {
                    var responseBody = await httpClient.GetStringAsync($"http://127.0.0.1:{server.TCPPort}/");
                    Assert.That(responseBody, Is.EqualTo("Hello World!"));
                }

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients == 0,
                        TimeSpan.FromSeconds(2)
                    ), Is.True, "Completed HTTP requests must not leave HTTPConnection entries in activeClients.");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Helpers

        private static HTTPServer CreateHTTPServer(IIPAddress?      ListenAddress                 = null,
                                                   ConnectionType?   ResponseConnection            = null,
                                                   UInt64?           MaxHTTPBodySize              = null,
                                                   UInt32?           MaxHTTPHeaderSize            = null,
                                                   UInt32?           MaxHTTPHeaderLineLength      = null,
                                                   UInt32?           MaxHTTPRequestTargetLength   = null,
                                                   UInt32?           MaxHTTPHeaderCount           = null,
                                                   UInt32?           MaxHTTPChunkSizeLineLength   = null,
                                                    UInt32?           MaxHTTPChunkTrailerLineLength = null,
                                                    UInt32?           MaxHTTPChunkTrailerCount     = null,
                                                    UInt32?           MaxHTTPChunkTrailerSize      = null,
                                                    UInt32?           MaxHTTPChunkMetadataSize     = null,
                                                    TimeSpan?         HeaderReadTimeout            = null,
                                                    TimeSpan?         BodyReadTimeout              = null)
        {

            var server = new HTTPServer(
                             IPAddress:  ListenAddress,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true,
                             MaxHTTPBodySize:            MaxHTTPBodySize,
                             MaxHTTPHeaderSize:          MaxHTTPHeaderSize,
                             MaxHTTPHeaderLineLength:    MaxHTTPHeaderLineLength,
                             MaxHTTPRequestTargetLength: MaxHTTPRequestTargetLength,
                             MaxHTTPHeaderCount:         MaxHTTPHeaderCount,
                             MaxHTTPChunkSizeLineLength: MaxHTTPChunkSizeLineLength,
                              MaxHTTPChunkTrailerLineLength: MaxHTTPChunkTrailerLineLength,
                              MaxHTTPChunkTrailerCount:   MaxHTTPChunkTrailerCount,
                              MaxHTTPChunkTrailerSize:    MaxHTTPChunkTrailerSize,
                              MaxHTTPChunkMetadataSize:   MaxHTTPChunkMetadataSize,
                              HeaderReadTimeout:          HeaderReadTimeout,
                              BodyReadTimeout:            BodyReadTimeout
                          );

            RegisterRootHandler(
                new HTTPAPI(server),
                ResponseConnection
            );

            return server;

        }

        private static HTTPServer CreateHTTPSServer(TimeSpan ReceiveTimeout)
        {

            var serverCertificate = CreateServerCertificate();
            var server            = new HTTPServer(
                                        TCPPort:                    IPPort.Zero,
                                        ReceiveTimeout:             ReceiveTimeout,
                                        ServerCertificateSelector:  (tcpServer, tcpClient) => serverCertificate,
                                        AutoStart:                  true
                                    );

            RegisterRootHandler(new HTTPAPI(server));

            return server;

        }

        private static void RegisterRootHandler(HTTPAPI         HTTPAPI,
                                                ConnectionType?  ResponseConnection = null)
        {

            HTTPAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                             new HTTPResponse.Builder(request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                                 Server          = "Hermod Test Server",
                                                                 Date            = Timestamp.Now,
                                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                                 Content         = "Hello World!".ToUTF8Bytes(),
                                                                  Connection      = ResponseConnection ?? ConnectionType.Close
                                                             }.AsImmutable));

            HTTPAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.HEAD,
                               HTTPDelegate: request => Task.FromResult(
                                                             new HTTPResponse.Builder(request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                                 Server          = "Hermod Test Server",
                                                                 Date            = Timestamp.Now,
                                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                                 Content         = "Hello World!".ToUTF8Bytes(),
                                                                  Connection      = ResponseConnection ?? ConnectionType.Close
                                                              }.AsImmutable));

            HTTPAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.POST,
                               HTTPDelegate: request => Task.FromResult(
                                                               new HTTPResponse.Builder(request) {
                                                                  HTTPStatusCode  = HTTPStatusCode.OK,
                                                                  Server          = "Hermod Test Server",
                                                                  Date            = Timestamp.Now,
                                                                  ContentType     = HTTPContentType.Text.PLAIN,
                                                                   Content         = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                   Connection      = ResponseConnection ?? ConnectionType.Close
                                                               }.AsImmutable));

        }

        private static X509Certificate2 CreateServerCertificate()
        {

            var rootCAKeyPair        = PKIFactory.GenerateRSAKeyPair(2048);
            var rootCACertificate    = PKIFactory.CreateRootCACertificate(
                                           "HTTPServerSocketRegressionTests Root CA",
                                           rootCAKeyPair
                                       );

            var serverKeyPair        = PKIFactory.GenerateRSAKeyPair(2048);

            return PKIFactory.SignServerCertificate(
                       "HTTPServerSocketRegressionTests Server Certificate",
                       null,
                       serverKeyPair.Public,
                       rootCAKeyPair.Private,
                       rootCACertificate
                   ).ToDotNet(serverKeyPair.Private)!;

        }

        private static async Task<String> SendRawHTTPRequest(System.Net.IPAddress Address,
                                                             IPPort               Port,
                                                             String               Host)
        {

            using var tcpClient = new TcpClient(Address.AddressFamily);
            await tcpClient.ConnectAsync(Address, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            var request            = Encoding.ASCII.GetBytes(
                                         $"GET / HTTP/1.1\r\nHost: {Host}\r\nConnection: close\r\n\r\n"
                                     );

            await stream.WriteAsync(request);

            using var cts          = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response     = new MemoryStream();
            var       buffer       = new Byte[4096];

            while (true)
            {

                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                    break;

                response.Write(buffer, 0, read);

            }

            return Encoding.ASCII.GetString(response.ToArray());

        }

        private static Task<String> SendRawRequest(IPPort Port,
                                                   String Request)
            => SendRawRequest(
                   System.Net.IPAddress.Loopback,
                   Port,
                   Request
               );

        private static async Task<String> SendRawRequest(System.Net.IPAddress Address,
                                                         IPPort               Port,
                                                         String               Request)
        {

            using var tcpClient = new TcpClient(Address.AddressFamily);
            await tcpClient.ConnectAsync(Address, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));

            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = new MemoryStream();
            var       buffer   = new Byte[4096];

            while (true)
            {

                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                    break;

                response.Write(buffer, 0, read);

            }

            return Encoding.ASCII.GetString(response.ToArray());

        }

        private static async Task<Boolean> WaitUntilAsync(Func<Boolean> Predicate,
                                                          TimeSpan      Timeout)
        {

            var end = DateTime.UtcNow + Timeout;

            while (DateTime.UtcNow < end)
            {

                if (Predicate())
                    return true;

                await Task.Delay(25);

            }

            return Predicate();

        }

        #endregion

    }

}
