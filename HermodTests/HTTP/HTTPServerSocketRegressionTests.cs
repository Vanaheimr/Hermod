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

        /// <summary>
        /// A connection whose TLS setup is still in progress must not occupy the
        /// accept loop: the next client has to be accepted while the first one is
        /// still in there.
        /// </summary>
        /// <remarks>
        /// The property is an ordering, not a latency, and the test says so. It used
        /// to connect a half-open client, connect a second one, and give the second
        /// accept a 500 ms budget - and it failed 9 of 10 fixture runs on a
        /// developer machine, and failed alone too, because the thing that budget
        /// was really measuring was how long the server spent building its
        /// certificate context: 578 - 2621 ms for the first connection of a
        /// process, with the thread pool idle. A budget cannot separate "the loop
        /// was free" from "the loop was busy but finished in time", so this one
        /// does not try:
        ///
        /// the first connection parks inside <c>ServerCertificateSelector</c> - which
        /// the server calls once per accepted connection, on whichever thread is
        /// setting that connection up - and stays parked on a gate that only this
        /// test opens, and only after it has seen the second connection accepted. So
        /// while the second client connects, the first one is provably still in its
        /// TLS setup. If that setup holds the accept loop, the second connection is
        /// never accepted at all and the second signal never arrives: the test fails
        /// because the ordering is wrong, not because a machine was slow. Its
        /// timeout is a liveness backstop of <see cref="AcceptSignalTimeout"/>,
        /// several orders of magnitude above a loopback accept, so a slow machine is
        /// slow rather than wrong.
        ///
        /// Verified against a server with the dispatch removed again - the accept
        /// loop building the connection and calling the handler itself - where it
        /// fails 10 of 10 runs on the signal that cannot arrive.
        /// </remarks>
        [Test]
        public async Task Slow_TLS_Handshake_Does_Not_Block_Following_Accepts()
        {

            var serverCertificate = CreateServerCertificate();

            // The first connection's TLS setup, held open for as long as this test
            // needs it. ManualResetEventSlim and not a Task: the selector is a
            // synchronous delegate, and blocking is the whole point - it is standing
            // in for every slow thing a real one does, from an SNI lookup to the
            // certificate-chain build the server itself does a moment later.
            using var gate          = new ManualResetEventSlim(false);

            var       firstSetup    = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var       secondAccept  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TcpClient? firstAccepted = null;

            var server            = new HTTPServer(
                                        TCPPort:                    IPPort.Zero,

                                        // Generous on purpose: the first connection has to
                                        // still be unfinished while the second one is
                                        // observed, and this is what guarantees it. Nothing
                                        // here waits for the timeout to fire - that is
                                        // Timed_Out_TLS_Handshake_Removes_Active_Client - and
                                        // Stop() closes the sockets, so it is never paid.
                                        ReceiveTimeout:             TimeSpan.FromMinutes(1),

                                        ServerCertificateSelector:  (tcpServer, tcpClient) => {

                                                                        // Identity, not a count: the
                                                                        // selector is asked again for
                                                                        // the same client when a
                                                                        // connection reaches the HTTP
                                                                        // layer, and that must not read
                                                                        // as a second accept.
                                                                        if (Interlocked.CompareExchange(ref firstAccepted, tcpClient, null) is null)
                                                                        {
                                                                            firstSetup.TrySetResult();
                                                                            gate.Wait();
                                                                        }

                                                                        else if (!ReferenceEquals(tcpClient, firstAccepted))
                                                                            secondAccept.TrySetResult();

                                                                        return serverCertificate;

                                                                    },
                                        AutoStart:                  true
                                    );

            RegisterRootHandler(new HTTPAPI(server));

            var slowClient    = new TcpClient(AddressFamily.InterNetwork);
            var anotherClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {

                await slowClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await Reached(firstSetup, "the first client's TLS setup"),
                            Is.True,
                            "The first slow TLS client was never accepted.");

                await anotherClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await Reached(secondAccept, "the second client's accept"),
                            Is.True,
                            "A half-open TLS client must not block later HTTPS accepts: the second " +
                            "connection was not accepted while the first one was still in its TLS setup.");

            }
            finally
            {
                // Before Stop(), and whatever happened above: a server whose accept
                // path is still parked here is one that Stop() would wait for.
                gate.Set();

                slowClient.Dispose();
                anotherClient.Dispose();
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Resource OPTIONS without a handler is answered automatically

        /// <summary>
        /// RFC 9110 §9.3.7 — OPTIONS asks what the target resource supports, and
        /// the router is the only thing that knows. Before this, an unregistered
        /// OPTIONS was answered 405 while carrying the very list it was asked for.
        ///
        /// Server-wide "OPTIONS *" was always automatic; this is the per-resource
        /// form behaving the same way.
        /// </summary>
        [Test]
        public async Task Resource_OPTIONS_Without_A_Handler_Is_Answered_Automatically()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "OPTIONS / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 204"), response);
                Assert.That(response,
                            Does.Match("(?m)^Allow: (?=.*\\bGET\\b)(?=.*\\bHEAD\\b)(?=.*\\bPOST\\b)(?=.*\\bOPTIONS\\b).*$"),
                            response);

                // RFC 9110 §15.3.5: a 204 carries no content. The 405 path it used
                // to share answers with emits a JSON description, and inheriting
                // that here would desynchronise every client that believes the
                // status.
                Assert.That(response, Does.Not.Contain("\"description\""), response);

                var body = response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
                Assert.That(body, Is.Empty, response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A registered OPTIONS handler still wins

        /// <summary>
        /// The automatic answer is a fallback, not an override: TryGetValue finds a
        /// registered handler first, and only its absence reaches the new branch.
        /// </summary>
        [Test]
        public async Task Registered_OPTIONS_Handler_Takes_Precedence()
        {

            // A second API at the root would collide with the one CreateHTTPServer
            // already registers there, so this one gets a root of its own.
            var server   = CreateHTTPServer(IPv4Address.Localhost);
            var httpAPI  = server.AddHTTPAPI(HTTPPath.Parse("/opt"));
            var reached  = false;

            httpAPI.AddHandler(HTTPPath.Root + "withhandler",
                               HTTPMethod:    HTTPMethod.OPTIONS,
                               HTTPDelegate:  request => {
                                                  reached = true;
                                                  return Task.FromResult(
                                                      new HTTPResponse.Builder(request) {
                                                          HTTPStatusCode  = HTTPStatusCode.OK,
                                                          ContentType     = HTTPContentType.Text.PLAIN,
                                                          Content         = "mine".ToUTF8Bytes()
                                                      }.AsImmutable
                                                  );
                                              });

            try
            {

                var response = await SendRawRequest(
                                   server.TCPPort,
                                   "OPTIONS /opt/withhandler HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(reached,  Is.True, response);
                Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);
                Assert.That(response, Does.Contain("mine"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Allow advertises OPTIONS on the 405 path as well

        /// <summary>
        /// RFC 9110 §10.2.1 — Allow lists the methods the target resource supports.
        /// The server now answers OPTIONS for every routed resource, so the Allow on
        /// a 405 has to say so too: it is the field a client consults precisely
        /// because it was just refused, and "OPTIONS / => 204" beside
        /// "DELETE / => 405, Allow: GET, HEAD" is one resource giving two different
        /// accounts of itself.
        /// </summary>
        [Test]
        public async Task Allow_Advertises_OPTIONS_On_The_MethodNotAllowed_Path_Too()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                var refused = await SendRawRequest(
                                  server.TCPPort,
                                  "DELETE / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                              );

                Assert.That(refused, Does.StartWith("HTTP/1.1 405"), refused);
                Assert.That(refused,
                            Does.Match("(?m)^Allow: .*\\bOPTIONS\\b.*$"),
                            refused);

                // And the claim is true, not merely advertised.
                var offered = await SendRawRequest(
                                  server.TCPPort,
                                  "OPTIONS / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                              );

                Assert.That(offered, Does.StartWith("HTTP/1.1 204"), offered);

            }
            finally
            {
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
        [TestCase("GET",     "http://example.org/a",  400)]
        [TestCase("GET",     "*",                     400)]
        [TestCase("CONNECT", "example.org:443",       501)]
        public async Task Invalid_Request_Target_Forms_Are_Rejected(String  HTTPMethod,
                                                                    String  RequestTarget,
                                                                    Int32   ExpectedStatusCode)
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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

                await Assert.ThrowsAsync<EndOfStreamException>(async () =>
                    await rawClient.ReadResponseAsync(CancellationToken: cts.Token)
                );

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        // The test above sends half a header and then nothing, so it passes
        // whether the deadline bounds the header section or only each read.
        // A Slowloris client keeps sending: one byte well inside the timeout,
        // then the next. A timeout that starts again with every read never
        // fires, and the connection stays open for as long as the client likes.
        [Test]
        public async Task Dripped_Request_Header_Is_Closed_At_The_Header_Deadline()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             HeaderReadTimeout: TimeSpan.FromMilliseconds(300)
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                await rawClient.SendAsync(
                          "GET / HTTP/1.1\r\nHost: localhost\r\nX-Drip: ",
                          cts.Token
                      );

                var response = rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                var (closed, bytesDripped) = await DripUntilClosed(
                                                       rawClient,
                                                       response,
                                                       TimeSpan.FromMilliseconds(100),
                                                       30
                                                   );

                Assert.That(closed, Is.True, $"The connection was still open after {bytesDripped} bytes, one every 100 ms, with a header deadline of 300 ms.");
                Assert.That(async () => await response, Throws.InstanceOf<IOException>());

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        // The deadline is one request's, not the connection's: it starts again
        // once a response has been sent, so a kept-alive connection serves
        // requests for longer than the deadline - and the next request's
        // header, dripped, is closed at its own deadline all the same.
        [Test]
        public async Task Each_Request_On_A_KeptAlive_Connection_Gets_Its_Own_Header_Deadline()
        {

            var server = CreateHTTPServer(
                             IPv4Address.Localhost,
                             ConnectionType.KeepAlive,
                             HeaderReadTimeout: TimeSpan.FromMilliseconds(500)
                         );

            try
            {

                await using var rawClient = await HTTPRawSocketClient.ConnectAsync(
                                              System.Net.IPAddress.Loopback,
                                              server.TCPPort
                                          );

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                for (var i = 1; i <= 5; i++)
                {

                    await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);

                    await rawClient.SendAsync(
                              "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n",
                              cts.Token
                          );

                    HTTPRawSocketResponse? okResponse = null;

                    try
                    {
                        okResponse = await rawClient.ReadResponseAsync(CancellationToken: cts.Token);
                    }
                    catch (IOException e)
                    {
                        Assert.Fail($"Request {i}, sent about {i * 200} ms after the connection was opened, found it closed with a header deadline of 500 ms: {e.Message}");
                    }

                    Assert.That(okResponse!.StatusCode, Is.EqualTo(200), $"Request {i}");

                }

                await rawClient.SendAsync(
                          "GET / HTTP/1.1\r\nHost: localhost\r\nX-Drip: ",
                          cts.Token
                      );

                var response = rawClient.ReadResponseAsync(CancellationToken: cts.Token);

                var (closed, bytesDripped) = await DripUntilClosed(
                                                       rawClient,
                                                       response,
                                                       TimeSpan.FromMilliseconds(100),
                                                       50
                                                   );

                Assert.That(closed, Is.True, $"The connection was still open after {bytesDripped} bytes, one every 100 ms, with a header deadline of 500 ms.");
                Assert.That(async () => await response, Throws.InstanceOf<IOException>());

            }
            finally
            {
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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

            await Assert.ThrowsAsync<ArgumentException>(async () =>
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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
                await server.DisposeAsync();
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

        /// <summary>
        /// Send one header byte every Interval until the server closes the
        /// connection - seen by the pending Response read ending, or by a
        /// send failing - or until MaxBytes have been sent.
        /// </summary>
        private static async Task<(Boolean Closed, Int32 BytesDripped)> DripUntilClosed(HTTPRawSocketClient  RawClient,
                                                                                        Task                 Response,
                                                                                        TimeSpan             Interval,
                                                                                        Int32                MaxBytes)
        {

            var bytesDripped = 0;

            while (bytesDripped < MaxBytes)
            {

                if (await Task.WhenAny(Response, Task.Delay(Interval)) == Response)
                    return (true, bytesDripped);

                try
                {
                    await RawClient.SendAsync("a");
                }
                catch (IOException)
                {
                    return (true, bytesDripped);
                }

                bytesDripped++;

            }

            return (Response.IsCompleted, bytesDripped);

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

        /// <summary>
        /// How long a test waits for a signal the server is supposed to send at once.
        /// </summary>
        /// <remarks>
        /// A liveness backstop, not a budget: the signals it is used for arrive in
        /// microseconds when the server is right and never when it is wrong, so the
        /// only thing this number decides is how long a broken server takes to say
        /// so. Generous enough that no load on any machine can make a working server
        /// look broken.
        /// </remarks>
        private static readonly TimeSpan AcceptSignalTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Wait for a signal the server completes, rather than polling for a state
        /// change within a deadline.
        /// </summary>
        /// <param name="Signal">The signal to wait for.</param>
        /// <param name="What">What the signal means, for the timeout message.</param>
        private static async Task<Boolean> Reached(TaskCompletionSource  Signal,
                                                   String                What,
                                                   TimeSpan?             Timeout   = null)
        {

            var timeout = Timeout ?? AcceptSignalTimeout;

            try
            {

                // WaitAsync, and not WhenAny(Signal.Task, Task.Delay(timeout)): the
                // delay in that pair is abandoned rather than cancelled, so every
                // call that succeeds - which is every call, in microseconds - leaves
                // a timer standing for the whole timeout. WaitAsync disposes its own
                // when the signal arrives.
                await Signal.Task.WaitAsync(timeout);

                return true;

            }
            catch (TimeoutException)
            {

                TestContext.Out.WriteLine($"Timed out after {timeout.TotalSeconds:N0} s waiting for {What}.");

                return false;

            }

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
