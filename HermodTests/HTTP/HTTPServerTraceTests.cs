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
    /// TRACE, which this server does not implement — and now says so.
    /// </summary>
    /// <remarks>
    /// RFC 9110 §9.3.8 asks the final recipient to reflect the request back as
    /// the content of a 200, and §9.1 makes that optional: every method but GET
    /// and HEAD is. The decision not to is deliberate and these tests are what
    /// make it a decision rather than an absence — H-9 in
    /// HTTP1ConformanceTests, which said "deliberately not implemented is a
    /// valid answer, but then document it".
    ///
    /// The reason is in the same section: a TRACE response carries the
    /// request's own fields back, so the recipient "SHOULD exclude any request
    /// fields that are likely to contain sensitive data" — a judgement about
    /// Authorization, Cookie and whatever an application invented, made by a
    /// library, wrong once and silently. Cross-Site Tracing was that mistake in
    /// 2003, and browsers now forbid the method rather than trust the answer.
    ///
    /// What changed is the status code. Routing answered 405, because no
    /// handler was registered; §9.1 keeps 405 for a method "recognized and
    /// implemented, but not allowed for the target resource" and puts one
    /// "unrecognized or not implemented" on the 501 side. This server's refusal
    /// is not about the resource.
    /// </remarks>
    [TestFixture]
    public class HTTPServerTraceTests
    {

        #region (private) CreateServer()

        private static HTTPServer CreateServer()
        {

            var server   = new HTTPServer(
                               IPv4Address.Localhost,
                               IPPort.Parse(0),
                               AutoStart: true
                           );

            var httpAPI  = server.AddHTTPAPI();

            httpAPI.AddHandler(HTTPPath.Root + "text",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "ok".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable
                                              ));

            return server;

        }

        #endregion

        #region (private) ReadAll(Port, Request)

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


        #region TRACEOnARoutedResourceIs501()

        [Test]
        public async Task TRACEOnARoutedResourceIs501()
        {

            var server = CreateServer();

            try
            {

                var response = await ReadAll(
                                   server.TCPPort,
                                   $"TRACE /text HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.Multiple(() => {

                    Assert.That(response, Does.StartWith("HTTP/1.1 501"),  response);

                    // Not a 405, so no Allow: the refusal is not about this
                    // resource and there is no other method to point at.
                    Assert.That(response, Does.Not.Contain("Allow:"),      response);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region TRACEOnAnUnknownPathIsAlso501()

        /// <summary>
        /// 501 rather than 404, and on purpose: the method is unsupported for
        /// every resource, so which resource was asked for does not come into
        /// it. A client that got 404 here would reasonably try another path.
        /// </summary>
        [Test]
        public async Task TRACEOnAnUnknownPathIsAlso501()
        {

            var server = CreateServer();

            try
            {

                var response = await ReadAll(
                                   server.TCPPort,
                                   $"TRACE /nothing-here HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"
                               );

                Assert.That(response, Does.StartWith("HTTP/1.1 501"), response);

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region TheRefusalReflectsNothingBack()

        /// <summary>
        /// The whole point of the decision: whatever the request carried stays
        /// out of the answer. A reflecting TRACE would have sent both of these
        /// back, which is what Cross-Site Tracing read.
        /// </summary>
        [Test]
        public async Task TheRefusalReflectsNothingBack()
        {

            var server = CreateServer();

            try
            {

                var response = await ReadAll(
                                   server.TCPPort,
                                   "TRACE /text HTTP/1.1\r\n"                        +
                                   "Host: localhost\r\n"                             +
                                   "Authorization: Bearer not-a-real-token-XYZ\r\n"  +
                                   "Cookie: session=secret-cookie-value\r\n"         +
                                   "Connection: close\r\n\r\n"
                               );

                Assert.Multiple(() => {

                    // First, because a response that never arrived reflects
                    // nothing either and would make the two checks below true
                    // for the wrong reason.
                    Assert.That(response, Does.StartWith("HTTP/1.1 501"),         response);

                    Assert.That(response, Does.Not.Contain("not-a-real-token"),   response);
                    Assert.That(response, Does.Not.Contain("secret-cookie"),      response);

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region ARegisteredTRACEHandlerStillDecides()

        /// <summary>
        /// The refusal is the server's default, not a prohibition: an
        /// application that registers TRACE has made the judgement §9.3.8 asks
        /// for and gets to keep it.
        /// </summary>
        [Test]
        public async Task ARegisteredTRACEHandlerStillDecides()
        {

            var server   = new HTTPServer(
                               IPv4Address.Localhost,
                               IPPort.Parse(0),
                               AutoStart: true
                           );

            var httpAPI  = server.AddHTTPAPI();

            httpAPI.AddHandler(HTTPPath.Root + "loopback",
                               HTTPMethod:    HTTPMethod.TRACE,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      // RFC 9110 §9.3.8 names "message/http" as one way to
                                                      // do this, and Hermod has no such content type. Not
                                                      // the point here: what this asserts is that the
                                                      // handler runs at all, and an application reflecting
                                                      // for real would add the type along with the
                                                      // judgement about which fields to leave out.
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = request.EntirePDU.ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable
                                              ));

            try
            {

                var response = await ReadAll(
                                   server.TCPPort,
                                   "TRACE /loopback HTTP/1.1\r\nHost: localhost\r\nX-Echo-Me: yes\r\nConnection: close\r\n\r\n"
                               );

                Assert.Multiple(() => {
                    Assert.That(response, Does.StartWith("HTTP/1.1 200"),  response);
                    Assert.That(response, Does.Contain("X-Echo-Me: yes"),  response);
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
