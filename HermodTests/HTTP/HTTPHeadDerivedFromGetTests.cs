/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HEAD on a resource that registered only GET.
    ///
    /// RFC 9110, Section 9.1: "All general-purpose servers MUST support the
    /// methods GET and HEAD." That MUST binds the server and not the resource —
    /// Section 9.1 names the 405 as how a resource refuses a method it does not
    /// allow, and nothing obliges any particular resource to allow HEAD. But a
    /// server answering 405 to HEAD on every resource it serves GET for does not
    /// support HEAD in any sense a client can use, and every GET route had to
    /// register HEAD by hand to get one.
    ///
    /// Section 9.3.2 defines HEAD as GET without the content, so the GET handler
    /// answers it and the writer leaves the body out.
    /// </summary>
    /// <remarks>
    /// Raw sockets rather than HTTPClient: what is under test is the absence of
    /// a body and the reusability of the connection afterwards, and a client
    /// that already knows a HEAD response has no body cannot distinguish a
    /// server that got that right from one that got it wrong.
    /// </remarks>
    [TestFixture]
    public class HTTPHeadDerivedFromGetTests
    {

        #region Data

        /// <summary>
        /// What GET /text answers. Its length is the Content-Length a derived
        /// HEAD has to repeat.
        /// </summary>
        private const String representation = "the representation\n";

        #endregion

        #region (private) CreateServer()

        private static HTTPServer CreateServer()
        {

            var server   = new HTTPServer(
                               IPv4Address.Localhost,
                               IPPort.Parse(0),
                               AutoStart: true
                           );

            var httpAPI  = server.AddHTTPAPI();

            // GET alone: the case this fixture is about.
            httpAPI.AddHandler(HTTPPath.Root + "text",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = representation.ToUTF8Bytes(),
                                                      Connection      = ConnectionType.KeepAlive
                                                  }.AsImmutable
                                              ));

            // No GET: nothing to derive a HEAD from.
            httpAPI.AddHandler(HTTPPath.Root + "post-only",
                               HTTPMethod:    HTTPMethod.POST,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "posted".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.KeepAlive
                                                  }.AsImmutable
                                              ));

            // Both: the registered HEAD must win over the derived one, and says
            // so in a field of its own.
            httpAPI.AddHandler(HTTPPath.Root + "own-head",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "from the GET handler".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.KeepAlive
                                                  }.AsImmutable
                                              ));

            httpAPI.AddHandler(HTTPPath.Root + "own-head",
                               HTTPMethod:    HTTPMethod.HEAD,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      ContentLength   = 4096,
                                                      Connection      = ConnectionType.KeepAlive
                                                  }.
                                                  Set("X-Answered-By", "the HEAD handler").
                                                  AsImmutable
                                              ));

            // A chunked GET. Under HEAD the worker must not run, and the
            // message must still end at the blank line: RFC 9112, Section 6.3,
            // item 1 makes that so regardless of the framing fields present.
            httpAPI.AddHandler(HTTPPath.Root + "chunked",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked",
                                                      Connection        = ConnectionType.KeepAlive,
                                                      ChunkWorker       = async (response, stream) => {
                                                          await stream.WriteAsync("chunk-one\n".ToUTF8Bytes(), null);
                                                          await stream.Finish();
                                                      }
                                                  }.AsImmutable
                                              ));

            return server;

        }

        #endregion

        #region (private) ReadAll  (Port, Request)

        /// <summary>
        /// Send one request asking for the connection to be closed, and read
        /// everything that comes back until the server closes it.
        /// </summary>
        private static async Task<String> ReadAll(IPPort Port, String Request)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Request));

            using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var received  = new MemoryStream();
            var       buffer    = new Byte[4096];

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                    break;
                received.Write(buffer, 0, read);
            }

            return Encoding.ASCII.GetString(received.ToArray());

        }

        #endregion

        #region (private) ReadTwo  (Port, First, Second)

        /// <summary>
        /// Send one request, wait for its header section, then send a second one
        /// on the same connection and read until the server closes it.
        /// </summary>
        /// <remarks>
        /// The second request is the one asking for the connection to be closed,
        /// which is what makes reading to EOF the right thing to do here and
        /// gives a deterministic end.
        ///
        /// The first version of this stopped as soon as a second status line had
        /// arrived, and the second response's header section and body do not
        /// have to arrive in one read: it returned a complete header section and
        /// no body, which looks exactly like a server that answered and then
        /// failed to send the content. The harness was wrong, not the server —
        /// "two status lines have arrived" is not "the second response is
        /// complete".
        /// </remarks>
        private static async Task<String> ReadTwo(IPPort  Port,
                                                  String  First,
                                                  String  Second)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(First));

            using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var received  = new MemoryStream();
            var       buffer    = new Byte[4096];
            var       sentSecond = false;

            try
            {
                while (true)
                {

                    var read = await stream.ReadAsync(buffer, cts.Token);

                    if (read == 0)
                        break;

                    received.Write(buffer, 0, read);

                    var text = Encoding.ASCII.GetString(received.ToArray());

                    // The first response has arrived whole once its header
                    // section is complete; a HEAD response is nothing but a
                    // header section, so that is the whole message.
                    if (!sentSecond && text.Contains("\r\n\r\n"))
                    {
                        sentSecond = true;
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(Second), cts.Token);
                    }

                }
            }
            catch (OperationCanceledException)
            { }

            return Encoding.ASCII.GetString(received.ToArray());

        }

        #endregion

        #region (private) Request  (Method, Path)

        private static String Request(String Method, String Path)

            => $"{Method} {Path} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n";

        #endregion


        #region An unregistered HEAD is answered by the GET handler

        [Test]
        public async Task An_Unregistered_HEAD_Is_Answered_By_The_GET_Handler()
        {

            var server = CreateServer();

            try
            {

                var response = await ReadAll(server.TCPPort, Request("HEAD", "/text"));

                Assert.Multiple(() => {

                    Assert.That(response, Does.StartWith("HTTP/1.1 200"),                     response);
                    Assert.That(response, Does.Contain("Content-Type: text/plain"),           response);
                    Assert.That(response, Does.Not.Contain(representation.Trim()),            response);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region The derived HEAD repeats the header fields GET would have sent

        /// <summary>
        /// RFC 9110, Section 9.3.2: the server should send the same header
        /// fields as it would have for GET. Content-Length is the one that
        /// matters most and the one that is lost by any implementation that
        /// answers HEAD without running the handler.
        /// </summary>
        [Test]
        public async Task The_Derived_HEAD_Repeats_What_GET_Would_Have_Sent()
        {

            var server = CreateServer();

            try
            {

                var get   = await ReadAll(server.TCPPort, Request("GET",  "/text"));
                var head  = await ReadAll(server.TCPPort, Request("HEAD", "/text"));

                Assert.Multiple(() => {

                    // First, because a GET that did not arrive makes every
                    // comparison below vacuous.
                    Assert.That(get,  Does.StartWith("HTTP/1.1 200"),                                  get);
                    Assert.That(get,  Does.Contain(representation.Trim()),                             get);

                    Assert.That(head, Does.Contain($"Content-Length: {representation.Length}"),        head);
                    Assert.That(get,  Does.Contain($"Content-Length: {representation.Length}"),        get);

                    Assert.That(head, Does.Contain("Content-Type: text/plain"),                        head);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A registered HEAD handler still wins

        [Test]
        public async Task A_Registered_HEAD_Handler_Still_Wins()
        {

            var server = CreateServer();

            try
            {

                var response = await ReadAll(server.TCPPort, Request("HEAD", "/own-head"));

                Assert.Multiple(() => {

                    Assert.That(response, Does.StartWith("HTTP/1.1 200"),                     response);
                    Assert.That(response, Does.Contain("X-Answered-By: the HEAD handler"),    response);
                    Assert.That(response, Does.Contain("Content-Length: 4096"),               response);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Allow names HEAD wherever GET is registered

        /// <summary>
        /// The same answer from both fields that carry it: the 405 a client
        /// consults because it was refused, and the automatic OPTIONS.
        /// RFC 9110, Section 10.2.1 and Section 15.5.6.
        /// </summary>
        [Test]
        public async Task Allow_Names_HEAD_Wherever_GET_Is_Registered()
        {

            var server = CreateServer();

            try
            {

                var refused  = await ReadAll(server.TCPPort, Request("DELETE",  "/text"));
                var options  = await ReadAll(server.TCPPort, Request("OPTIONS", "/text"));

                Assert.Multiple(() => {

                    Assert.That(refused, Does.StartWith("HTTP/1.1 405"),  refused);
                    Assert.That(refused, Does.Contain("Allow:"),          refused);
                    Assert.That(refused, Does.Contain("HEAD"),            refused);
                    Assert.That(refused, Does.Contain("GET"),             refused);
                    Assert.That(refused, Does.Contain("OPTIONS"),         refused);

                    Assert.That(options, Does.StartWith("HTTP/1.1 204"),  options);
                    Assert.That(options, Does.Contain("HEAD"),            options);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A resource without GET does not claim HEAD

        /// <summary>
        /// The other half of the rule, and the one that makes the test above
        /// mean something: HEAD is derived from GET, so a resource that has no
        /// GET must neither answer HEAD nor advertise it.
        /// </summary>
        [Test]
        public async Task A_Resource_Without_GET_Does_Not_Claim_HEAD()
        {

            var server = CreateServer();

            try
            {

                var head     = await ReadAll(server.TCPPort, Request("HEAD",    "/post-only"));
                var options  = await ReadAll(server.TCPPort, Request("OPTIONS", "/post-only"));

                Assert.Multiple(() => {

                    Assert.That(head,    Does.StartWith("HTTP/1.1 405"),       head);

                    Assert.That(options, Does.StartWith("HTTP/1.1 204"),       options);
                    Assert.That(options, Does.Contain("POST"),                 options);
                    Assert.That(options, Does.Not.Contain("HEAD"),             options);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region A chunked GET answered as HEAD leaves the connection usable

        /// <summary>
        /// The case a naive implementation hangs on. The response carries
        /// Transfer-Encoding: chunked, because Section 9.3.2 asks for the
        /// fields GET would have sent — and RFC 9112, Section 6.3, item 1 says
        /// any response to HEAD ends at the first empty line after the header
        /// fields "regardless of the header fields present". So there is no
        /// terminal chunk to wait for, and the connection carries the next
        /// request.
        /// </summary>
        [Test]
        public async Task A_Chunked_GET_Answered_As_HEAD_Leaves_The_Connection_Usable()
        {

            var server = CreateServer();

            try
            {

                var both = await ReadTwo(
                               server.TCPPort,
                               "HEAD /chunked HTTP/1.1\r\nHost: localhost\r\n\r\n",
                               "GET /text HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                           );

                Assert.Multiple(() => {

                    Assert.That(both, Does.StartWith("HTTP/1.1 200"),                 both);
                    Assert.That(both, Does.Contain("Transfer-Encoding: chunked"),      both);
                    Assert.That(both, Does.Not.Contain("chunk-one"),                   both);

                    // The second response is the evidence: a connection left
                    // waiting for a terminal chunk never carries it.
                    Assert.That(both.Split("HTTP/1.1 ").Length, Is.EqualTo(3),         both);
                    Assert.That(both, Does.Contain(representation.Trim()),             both);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
