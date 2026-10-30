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
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Interim responses on HTTP/1.1 (RFC 9110, Section 15.2), and the one they
    /// are mostly for: 103 Early Hints (RFC 8297), which names what the final
    /// response will need while the server is still working on it.
    /// </summary>
    [TestFixture]
    public class EarlyHintsTests
    {

        #region Data

        private static readonly HTTPLinks Preload = new (new HTTPLink("/style.css", "preload", Attributes: [ new ("as", "style") ]));

        private const String PreloadHints = "HTTP/1.1 103 Early Hints\r\nLink: </style.css>; rel=\"preload\"; as=style\r\n\r\n";

        #endregion

        #region (private static) ReadUntil(Stream, Received, Marker)

        private static async Task<String> ReadUntil(NetworkStream  Stream,
                                                    StringBuilder  Received,
                                                    String?        Marker)
        {

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var buffer = new Byte[4096];

            while (Marker is null || !Received.ToString().Contains(Marker))
            {

                var read = await Stream.ReadAsync(buffer, timeout.Token);

                if (read == 0)
                    break;

                Received.Append(Encoding.ASCII.GetString(buffer, 0, read));

            }

            return Received.ToString();

        }

        #endregion


        #region The server

        /// <summary>
        /// RFC 8297, Section 2: the hints are for while the server is still
        /// working on the final response. The handler waits until the client has
        /// read the 103, which it could not do if the 103 went out with the
        /// final response.
        /// </summary>
        [Test]
        public async Task TheHintsGoOutWhileTheHandlerIsStillWorking()
        {

            var hintsRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sent      = false;

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "page",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: async request => {

                    sent = await request.SendEarlyHints(Preload);

                    await hintsRead.Task.WaitAsync(TimeSpan.FromSeconds(10));

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Link            = Preload,
                               Content         = "page".ToUTF8Bytes()
                           }.AsImmutable;

                }
            );

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, httpServer.TCPPort.ToInt32());

            var stream   = client.GetStream();
            var received = new StringBuilder();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /page HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));

            var first = await ReadUntil(stream, received, "\r\n\r\n");

            hintsRead.SetResult();

            var all = await ReadUntil(stream, received, null);

            Assert.Multiple(() => {
                Assert.That(first,  Does.StartWith(PreloadHints), first);
                Assert.That(sent,   Is.True);
                Assert.That(all[PreloadHints.Length..], Does.StartWith("HTTP/1.1 200 OK\r\n"), all);
                Assert.That(all,    Does.EndWith("\r\n\r\npage"), all);
            });

        }

        /// <summary>
        /// RFC 9110, Section 15.2: "A server MUST NOT send a 1xx response to an
        /// HTTP/1.0 client."
        /// </summary>
        [Test]
        public async Task AnHTTP10ClientGetsNoInterimResponse()
        {

            Boolean? sent = null;

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "page",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: async request => {

                    sent = await request.SendEarlyHints(Preload);

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "page".ToUTF8Bytes()
                           }.AsImmutable;

                }
            );

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, httpServer.TCPPort.ToInt32());

            var stream = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /page HTTP/1.0\r\n\r\n"));

            var all = await ReadUntil(stream, new StringBuilder(), null);

            Assert.Multiple(() => {
                Assert.That(sent, Is.False);
                Assert.That(all,  Does.StartWith("HTTP/1.0 200"), all);
                Assert.That(all,  Does.Not.Contain("103"),        all);
            });

        }

        /// <summary>
        /// Once the handler has returned, the final response comes next on the
        /// connection, and an interim response sent then would be read as the
        /// answer to whatever was asked next.
        /// </summary>
        [Test]
        public async Task NothingIsSentOnceTheHandlerHasReturned()
        {

            HTTPRequest? kept = null;

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "page",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    kept ??= request;

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                   ContentType     = HTTPContentType.Text.PLAIN,
                                   Content         = "page".ToUTF8Bytes()
                               }.AsImmutable
                           );

                }
            );

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, httpServer.TCPPort.ToInt32());

            var stream = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /page HTTP/1.1\r\nHost: localhost\r\n\r\n"));
            var first   = await ReadUntil(stream, new StringBuilder(), "\r\n\r\npage");

            var sent    = await kept!.SendEarlyHints(Preload);

            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /page HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));
            var second  = await ReadUntil(stream, new StringBuilder(), null);

            Assert.Multiple(() => {
                Assert.That(first,  Does.StartWith("HTTP/1.1 200 OK\r\n"), first);
                Assert.That(sent,   Is.False);
                Assert.That(second, Does.StartWith("HTTP/1.1 200 OK\r\n"), second);
            });

        }

        /// <summary>
        /// A 1xx other than 101, field names that are tokens, printable values,
        /// and no Content-Length or Transfer-Encoding (RFC 9110, Section 8.6;
        /// RFC 9112, Section 6.1). A value with a line break in it would put a
        /// field of the caller's choosing on the wire.
        /// </summary>
        [Test]
        public async Task WhatCannotBeAnInterimResponseIsRefused()
        {

            var refused = new List<String>();

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "page",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: async request => {

                    var attempts = new (String Name, Func<Task<Boolean>> Send)[] {
                        ("200",               () => request.SendInformationalResponse(HTTPStatusCode.OK)),
                        ("101",               () => request.SendInformationalResponse(HTTPStatusCode.SwitchingProtocols)),
                        ("field name",        () => request.SendInformationalResponse(HTTPStatusCode.EarlyHints, [ new ("Bad Name",          "x") ])),
                        ("Content-Length",    () => request.SendInformationalResponse(HTTPStatusCode.EarlyHints, [ new ("content-length",    "0") ])),
                        ("Transfer-Encoding", () => request.SendInformationalResponse(HTTPStatusCode.EarlyHints, [ new ("Transfer-Encoding", "chunked") ])),
                        ("line break",        () => request.SendInformationalResponse(HTTPStatusCode.EarlyHints, [ new ("Link",              "</a>; rel=preload\r\nSet-Cookie: x=y") ])),
                        ("non-ASCII",         () => request.SendInformationalResponse(HTTPStatusCode.EarlyHints, [ new ("X-Title",           "Grüße") ]))
                    };

                    foreach (var (name, send) in attempts)
                    {
                        try
                        {
                            await send();
                        }
                        catch (ArgumentException)
                        {
                            refused.Add(name);
                        }
                    }

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "page".ToUTF8Bytes()
                           }.AsImmutable;

                }
            );

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var response = await client.GET(HTTPPath.Root + "page", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,          Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.InformationalResponses,  Is.Empty);
                Assert.That(refused, Is.EqualTo(new[] { "200", "101", "field name", "Content-Length", "Transfer-Encoding", "line break", "non-ASCII" }));
            });

        }

        #endregion

        #region The client, end to end

        /// <summary>
        /// A handler sends a 102 and two 103s, and the client reads all three, in
        /// order, before the final response. The 103s' Link fields are theirs:
        /// RFC 8297 does not let them change how the final response is
        /// processed, so the final response has no Link of its own.
        /// </summary>
        [Test]
        public async Task TheClientKeepsTheInterimResponsesInOrder()
        {

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "page",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: async request => {

                    await request.SendInformationalResponse(HTTPStatusCode.Processing);
                    await request.SendEarlyHints(Preload);
                    await request.SendEarlyHints(new HTTPLinks(new HTTPLink("/app.js", "preload", Attributes: [ new ("as", "script") ])));

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "page".ToUTF8Bytes()
                           }.AsImmutable;

                }
            );

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var response = await client.GET(HTTPPath.Root + "page", RequestTimeout: TimeSpan.FromSeconds(10));
            var interim  = response.InformationalResponses;

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("page"));
                Assert.That(interim.Select(r => r.HTTPStatusCode.Code), Is.EqualTo(new[] { 102, 103, 103 }));
                Assert.That(interim[1].Link?.Single().Target,  Is.EqualTo("/style.css"));
                Assert.That(interim[1].Link?.Single().HasRelation("preload"), Is.True);
                Assert.That(interim[2].Link?.Single().Target,  Is.EqualTo("/app.js"));
                Assert.That(response.Link,                  Is.Null);
            });

        }

        #endregion

        #region The client, against a raw server

        private static async Task<HTTPResponse> AgainstARawServer(String Responses)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var server = Task.Run(async () => {

                using var accepted = await listener.AcceptTcpClientAsync();
                var stream         = accepted.GetStream();

                await ReadUntil(stream, new StringBuilder(), "\r\n\r\n");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(Responses));

            });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"));

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));

            await server;

            return response;

        }

        private const String FinalResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK";

        /// <summary>
        /// A 100 Continue nobody asked for is an interim response like any other,
        /// and a client "MUST be able to parse" it (RFC 9110, Section 15.2).
        /// </summary>
        [Test]
        public async Task A100ContinueIsKeptBesideTheHints()
        {

            var response = await AgainstARawServer("HTTP/1.1 100 Continue\r\n\r\n" + PreloadHints + FinalResponse);

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("OK"));
                Assert.That(response.InformationalResponses.Select(r => r.HTTPStatusCode.Code), Is.EqualTo(new[] { 100, 103 }));
            });

        }

        /// <summary>
        /// RFC 9110 sets no limit on interim responses. The client reads every
        /// one, and keeps the first MaxInformationalResponses.
        /// </summary>
        [Test]
        public async Task TheClientKeepsNoMoreThanItsLimit()
        {

            var hints = String.Concat(Enumerable.Range(0, 40).Select(i => $"HTTP/1.1 103 Early Hints\r\nLink: </n{i}>; rel=preload\r\n\r\n"));

            var response = await AgainstARawServer(hints + FinalResponse);
            var interim  = response.InformationalResponses;

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("OK"));
                Assert.That(interim.Count,                  Is.EqualTo(AHTTPClient.MaxInformationalResponses));
                Assert.That(interim[0]. Link?.Single().Target, Is.EqualTo("/n0"));
                Assert.That(interim[^1].Link?.Single().Target, Is.EqualTo($"/n{AHTTPClient.MaxInformationalResponses - 1}"));
            });

        }

        #endregion

    }

}
