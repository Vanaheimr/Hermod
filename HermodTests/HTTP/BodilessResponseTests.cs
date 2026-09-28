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

using System.Net;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9112, Section 6.3: a response that states neither a Content-Length
    /// nor a Transfer-Encoding has a body that ends when the connection does.
    ///
    /// What used to happen: a response built without a body - a redirect, a
    /// 401 or a 404 without a text - went out saying nothing about its length.
    /// On a connection kept alive, a client then waited for the rest of an
    /// empty body until the server gave the connection up, thirty seconds
    /// later. The builder says 0 for it now.
    ///
    /// The other tests here go the other way: where a response has no body
    /// by definition, where its Content-Length would be that of a
    /// representation it does not carry, or where chunks frame its body, it
    /// still says no length at all. They read the octets as sent, because a
    /// client that is asked for the length reports one it worked out itself
    /// where the server sent none.
    /// </summary>
    [TestFixture]
    public class BodilessResponseTests
    {

        #region Data

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

        private IPPort       Port
            => httpServer!.TCPPort;

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            // On 127.0.0.1, which is where the requests below go: a host
            // without IPv6 cannot open the default IPv4-and-IPv6 listener.
            httpServer = new HTTPServer(
                             IPAddress:  IPv4Address.Localhost,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            httpAPI    = new HTTPAPI(httpServer);

            #region GET  /redirect      - a redirect, and nothing else

            // The shape of the favicon redirect that found this: no body, no
            // length, and no Connection header, so the connection is kept.
            httpAPI.AddHandler(HTTPPath.Root + "redirect",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(Redirect(request)));

            #endregion

            #region HEAD /redirect      - the same redirect, asked for its header

            httpAPI.AddHandler(HTTPPath.Root + "redirect",
                               HTTPMethod:    HTTPMethod.HEAD,
                               HTTPDelegate:  request => Task.FromResult(Redirect(request)));

            #endregion

            #region GET  /nothing       - 204 No Content

            httpAPI.AddHandler(HTTPPath.Root + "nothing",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.NoContent
                                                  }.AsImmutable));

            #endregion

            #region GET  /unchanged     - 304 Not Modified

            httpAPI.AddHandler(HTTPPath.Root + "unchanged",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.NotModified,
                                                      ETag            = "\"v1\""
                                                  }.AsImmutable));

            #endregion

            #region GET  /chunked       - a body in chunks, and no Content

            httpAPI.AddHandler(HTTPPath.Root + "chunked",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked",
                                                      ChunkWorker       = async (response, stream) => {
                                                          await stream.WriteAsync("one\n".ToUTF8Bytes(), null);
                                                          await stream.Finish();
                                                      }
                                                  }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.Stop();
        }

        #endregion

        #region (private) Redirect(Request)

        private static HTTPResponse Redirect(HTTPRequest Request)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.TemporaryRedirect,
                   Location        = Location.From(HTTPPath.Parse("/elsewhere"))
               }.AsImmutable;

        #endregion

        #region (private) HeaderOf(Method, Path)

        /// <summary>
        /// The header of the response to one request, as sent. The request asks
        /// for the connection to be closed, so the close ends the transfer and
        /// no length has to be trusted in order to read it - the length being
        /// what is under test.
        /// </summary>
        private async Task<String> HeaderOf(String Method,
                                            String Path)
        {

            using var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            var stream  = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"{Method} {Path} HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n"));
            await stream.FlushAsync();

            using var buffer  = new MemoryStream();
            using var giveUp  = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await stream.CopyToAsync(buffer, giveUp.Token);

            var wire       = Encoding.Latin1.GetString(buffer.ToArray());
            var separator  = wire.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            Assert.That(separator, Is.GreaterThan(0), $"The response has no end of its header: {wire}");

            return wire[..separator];

        }

        #endregion


        #region ARedirectWithoutABodySaysItsLengthIsZero()

        /// <summary>
        /// The finding. The headers alone are asked for, so that the length is
        /// the one the server sent: asked for the whole response, the client
        /// reports the length of what it buffered, which is 0 whether or not
        /// the server said so - after waiting for the server to give up.
        /// </summary>
        [Test]
        public async Task ARedirectWithoutABodySaysItsLengthIsZero()
        {

            using var handler   = new HttpClientHandler { AllowAutoRedirect = false };
            using var http      = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{Port}") };

            var stopwatch       = Stopwatch.StartNew();

            using var response  = await http.GetAsync("redirect", HttpCompletionOption.ResponseHeadersRead);

            Byte[]? body        = null;

            using (var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    body = await response.Content.ReadAsByteArrayAsync(giveUp.Token);
                }
                catch (OperationCanceledException)
                { }
            }

            stopwatch.Stop();

            Assert.Multiple(() => {
                Assert.That(response.StatusCode,                     Is.EqualTo(HttpStatusCode.TemporaryRedirect));
                Assert.That(response.Headers.Location?.ToString(),   Is.EqualTo("/elsewhere"));
                Assert.That(response.Content.Headers.ContentLength,  Is.EqualTo(0),
                            "the redirect does not say that it has no body, so a client on a kept-alive connection waits for one");
                Assert.That(body,                                    Is.Not.Null.And.Empty,
                            "the body was still being waited for after five seconds");
                Assert.That(stopwatch.Elapsed,                       Is.LessThan(TimeSpan.FromSeconds(2)));
            });

        }

        #endregion

        #region TheSameRedirectAskedWithHEADSaysNoLength()

        /// <summary>
        /// The answer to a HEAD has no body whatever it says, and its
        /// Content-Length is that of the answer a GET would have had (RFC 9110,
        /// Section 8.6). A handler that built none has not said what that is,
        /// and 0 would be a claim about the GET.
        /// </summary>
        [Test]
        public async Task TheSameRedirectAskedWithHEADSaysNoLength()
        {

            var header = await HeaderOf("HEAD", "/redirect");

            Assert.Multiple(() => {
                Assert.That(header,  Does.StartWith("HTTP/1.1 307"));
                Assert.That(header,  Does.Not.Contain("Content-Length"));
            });

        }

        #endregion

        #region ANoContentSaysNoLength()

        /// <summary>
        /// RFC 9110, Section 8.6: a server must not send a Content-Length in a
        /// 204 - not even 0.
        /// </summary>
        [Test]
        public async Task ANoContentSaysNoLength()
        {

            var header = await HeaderOf("GET", "/nothing");

            Assert.Multiple(() => {
                Assert.That(header,  Does.StartWith("HTTP/1.1 204"));
                Assert.That(header,  Does.Not.Contain("Content-Length"));
            });

        }

        #endregion

        #region ANotModifiedSaysNoLengthOfItsOwn()

        /// <summary>
        /// A 304 has no body, and a Content-Length in it would be that of the
        /// representation the client already has (RFC 9110, Section 8.6) -
        /// which 0 is not.
        /// </summary>
        [Test]
        public async Task ANotModifiedSaysNoLengthOfItsOwn()
        {

            var header = await HeaderOf("GET", "/unchanged");

            Assert.Multiple(() => {
                Assert.That(header,  Does.StartWith("HTTP/1.1 304"));
                Assert.That(header,  Does.Not.Contain("Content-Length"));
            });

        }

        #endregion

        #region AChunkedResponseSaysNoLength()

        /// <summary>
        /// A chunked response has no Content either, but its chunks frame a
        /// body: a Content-Length must not stand next to a Transfer-Encoding
        /// (RFC 9112, Section 6.2), and a recipient that finds both ought to
        /// take the message for an error (Section 6.3).
        /// </summary>
        [Test]
        public async Task AChunkedResponseSaysNoLength()
        {

            var header = await HeaderOf("GET", "/chunked");

            Assert.Multiple(() => {
                Assert.That(header,  Does.StartWith("HTTP/1.1 200"));
                Assert.That(header,  Does.Contain("Transfer-Encoding: chunked"));
                Assert.That(header,  Does.Not.Contain("Content-Length"));
            });

        }

        #endregion

    }

}
