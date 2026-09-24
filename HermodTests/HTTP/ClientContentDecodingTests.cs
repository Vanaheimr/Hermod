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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The HTTP client asking for a content coding and undoing it again
    /// (RFC 9110, Sections 12.5.3 and 8.4).
    ///
    /// The server here is a bare TcpListener writing bytes chosen by the test,
    /// because the point is what the *client* does with a response — including
    /// the framings a well-behaved Hermod server would never produce, such as a
    /// gzipped event stream, which is what nginx puts in front of one.
    /// </summary>
    [TestFixture]
    public class ClientContentDecodingTests
    {

        #region (private) RawServer

        /// <summary>
        /// Answers each request on a connection with the next canned response,
        /// keeping the requests it saw and the number of connections it needed.
        /// </summary>
        private sealed class RawServer : IDisposable
        {

            private readonly TcpListener              listener  = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource  cts       = new ();
            private readonly List<Byte[]>             responses;
            private readonly Boolean                  closeWhenDone;

            public List<String>  Requests     { get; } = [];
            public Int32         Connections  { get; private set; }

            public Int32         Port
                => ((IPEndPoint) listener.LocalEndpoint).Port;

            public URL           URL
                => URL.Parse($"http://127.0.0.1:{Port}");

            public Task          Served       { get; }


            public RawServer(Boolean CloseWhenDone, params Byte[][] Responses)
            {

                responses      = [.. Responses];
                closeWhenDone  = CloseWhenDone;

                listener.Start();

                Served = Task.Run(Serve);

            }

            private async Task Serve()
            {
                try
                {

                    var pending = new Queue<Byte[]>(responses);

                    while (pending.Count > 0)
                    {

                        using var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                        Connections++;

                        var stream = tcpClient.GetStream();

                        while (pending.Count > 0)
                        {

                            var request = await ReadHead(stream);

                            if (request is null)
                                break;

                            Requests.Add(request);

                            await stream.WriteAsync(pending.Dequeue(), cts.Token);
                            await stream.FlushAsync(cts.Token);

                        }

                        if (!closeWhenDone)
                            // An event stream is not over when the bytes stop, so the
                            // socket has to stay up until the test is done with it.
                            await Task.Delay(Timeout.Infinite, cts.Token);

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (IOException)
                { }
                catch (SocketException)
                { }
            }

            private async Task<String?> ReadHead(NetworkStream Stream)
            {

                var buffer  = new Byte[8192];
                var length  = 0;

                while (length < buffer.Length)
                {

                    var read = await Stream.ReadAsync(buffer.AsMemory(length), cts.Token);

                    if (read == 0)
                        return null;

                    length += read;

                    var text = Encoding.UTF8.GetString(buffer, 0, length);

                    if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
                        return text;

                }

                return null;

            }

            public void Dispose()
            {
                cts.Cancel();
                listener.Stop();
                cts.Dispose();
            }

        }

        #endregion

        #region (private) Raw(Header, Body = null)

        private static Byte[] Raw(String   Header,
                                  Byte[]?  Body   = null)
        {

            var stream  = new MemoryStream();
            var header  = Encoding.ASCII.GetBytes(Header);

            stream.Write(header, 0, header.Length);

            if (Body is not null)
                stream.Write(Body, 0, Body.Length);

            return stream.ToArray();

        }

        private static Byte[] GZip(String Text)
            => HTTPContentCoding.Encode(Encoding.UTF8.GetBytes(Text), "gzip");

        private static Byte[] Chunked(Byte[] Payload)
        {

            var stream  = new MemoryStream();
            var head    = Encoding.ASCII.GetBytes($"{Payload.Length:x}\r\n");
            var tail    = Encoding.ASCII.GetBytes("\r\n0\r\n\r\n");

            stream.Write(head,    0, head.Length);
            stream.Write(Payload, 0, Payload.Length);
            stream.Write(tail,    0, tail.Length);

            return stream.ToArray();

        }

        #endregion


        #region TheClientOffersTheCodingsItCanUndo()

        [Test]
        public async Task TheClientOffersTheCodingsItCanUndo()
        {

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK")
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));
            await server.Served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(server.Requests[0], Does.Contain("Accept-Encoding: br, gzip, deflate"), server.Requests[0]);

        }

        #endregion

        #region WithoutTheOptInNothingIsOfferedAndNothingIsDecoded()

        /// <summary>
        /// The opt-in pinned from both ends: no field goes out, and a server that
        /// compresses anyway is taken at its word rather than second-guessed — the
        /// caller gets the octets and the Content-Encoding that describes them.
        /// </summary>
        [Test]
        public async Task WithoutTheOptInNothingIsOfferedAndNothingIsDecoded()
        {

            var encoded = GZip("would have been decoded");

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                      $"Content-Length: {encoded.Length}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       encoded)
                               );

            using var client   = new HTTPClient(server.URL);

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));
            await server.Served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(server.Requests[0],               Does.Not.Contain("Accept-Encoding"));
                Assert.That(response.HTTPBody,                Is.EqualTo(encoded));
                Assert.That(response.ContentEncoding,         Is.EqualTo(new[] { "gzip" }));
                Assert.That(response.DecodedContentEncoding,  Is.Null);
            });

        }

        #endregion

        #region ACallersOwnAcceptEncodingIsNotOverwritten()

        /// <summary>
        /// "identity" is how a caller switches compression off for one request. A
        /// client that wrote over it would make that impossible to express.
        /// </summary>
        [Test]
        public async Task ACallersOwnAcceptEncodingIsNotOverwritten()
        {

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK")
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            await client.GET(
                      HTTPPath.Root,
                      RequestBuilder:  builder => builder.AcceptEncoding = "identity",
                      RequestTimeout:  TimeSpan.FromSeconds(5)
                  );

            await server.Served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(server.Requests[0], Does.Contain("Accept-Encoding: identity"));
                Assert.That(server.Requests[0], Does.Not.Contain("br, gzip, deflate"));
            });

        }

        #endregion

        #region AGzippedResponseArrivesDecoded()

        [Test]
        public async Task AGzippedResponseArrivesDecoded()
        {

            var text     = "the representation, which is what the caller asked for";
            var encoded  = GZip(text);

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                      $"Content-Length: {encoded.Length}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       encoded)
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(text));
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));
                Assert.That(response.ContentEncoding,         Is.Empty);
            });

        }

        #endregion

        #region AChunkedGzippedResponseArrivesDecoded()

        /// <summary>
        /// Chunked *and* compressed is the ordinary shape of a dynamic page on the
        /// web, and the one an array-shaped decoder cannot reach: no
        /// Content-Length, no array, nothing to decode until it has all arrived.
        /// </summary>
        [Test]
        public async Task AChunkedGzippedResponseArrivesDecoded()
        {

            var text = "chunked and compressed";

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                       "Transfer-Encoding: chunked\r\n" +
                                       "Connection: close\r\n\r\n",
                                       Chunked(GZip(text)))
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(text));
            Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));

        }

        #endregion

        #region AChunkedBodyConsumedOnArrivalIsDecodedToo()

        /// <summary>
        /// With ConsumeResponseChunkedTEImmediately the chunked body is turned into
        /// an array before anything can wrap its stream, so this response takes the
        /// other decoder — the in-place one. Two code paths reaching the same
        /// result is exactly the arrangement that drifts apart unnoticed, so both
        /// are driven from outside.
        ///
        /// Here the decoded length *is* known, so Content-Length is corrected
        /// rather than dropped, and this test is where that difference is pinned.
        /// </summary>
        [Test]
        public async Task AChunkedBodyConsumedOnArrivalIsDecodedToo()
        {

            var text = "consumed on arrival, then decoded";

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                       "Transfer-Encoding: chunked\r\n" +
                                       "Connection: close\r\n\r\n",
                                       Chunked(GZip(text)))
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(
                                     HTTPPath.Root,
                                     ConsumeResponseChunkedTEImmediately:  true,
                                     RequestTimeout:                       TimeSpan.FromSeconds(5)
                                 );

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(text));
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));
                Assert.That(response.ContentEncoding,         Is.Empty);
                Assert.That(response.ContentLength,           Is.EqualTo((UInt64) text.Length));
            });

        }

        #endregion

        #region ACloseDelimitedGzippedResponseArrivesDecoded()

        [Test]
        public async Task ACloseDelimitedGzippedResponseArrivesDecoded()
        {

            var text = "no length, no chunks — the body ends when the socket does";

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                       "Connection: close\r\n\r\n",
                                       GZip(text))
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo(text));

        }

        #endregion

        #region TheConnectionSurvivesADecodedResponse()

        /// <summary>
        /// The decoding drops Content-Length from the parsed view, and the parsed
        /// view is not what frames the connection — the body stream was already
        /// bounded by the length before it was wrapped. If that were the other way
        /// round, the second request would go out on a second connection, or not at
        /// all.
        /// </summary>
        [Test]
        public async Task TheConnectionSurvivesADecodedResponse()
        {

            var first   = GZip("first");
            var second  = GZip("second");

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\n" +
                                      $"Content-Length: {first.Length}\r\nConnection: keep-alive\r\n\r\n", first),
                                   Raw("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\n" +
                                      $"Content-Length: {second.Length}\r\nConnection: keep-alive\r\n\r\n", second)
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response1 = await client.GET(HTTPPath.Root, Connection: ConnectionType.KeepAlive, RequestTimeout: TimeSpan.FromSeconds(5));
            var response2 = await client.GET(HTTPPath.Root, Connection: ConnectionType.KeepAlive, RequestTimeout: TimeSpan.FromSeconds(5));

            await server.Served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response1.HTTPBodyAsUTF8String,  Is.EqualTo("first"));
                Assert.That(response2.HTTPBodyAsUTF8String,  Is.EqualTo("second"));
                Assert.That(server.Connections,              Is.EqualTo(1), "both requests should have shared one connection");
            });

        }

        #endregion

        #region AnUnknownCodingLeavesTheBodyAsItCame()

        /// <summary>
        /// We only ever offered codings we can undo, so a server answering with
        /// something else is misbehaving — but a body we cannot decode is still a
        /// body, and handing it on labelled as identity would be worse than handing
        /// it on as it came.
        /// </summary>
        [Test]
        public async Task AnUnknownCodingLeavesTheBodyAsItCame()
        {

            var body = Encoding.UTF8.GetBytes("pretend this is exotic");

            using var server = new RawServer(
                                   CloseWhenDone: true,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/plain\r\n" +
                                       "Content-Encoding: exotic\r\n" +
                                      $"Content-Length: {body.Length}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       body)
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPBody,                Is.EqualTo(body));
                Assert.That(response.ContentEncoding,         Is.EqualTo(new[] { "exotic" }));
                Assert.That(response.DecodedContentEncoding,  Is.Null);
            });

        }

        #endregion

        #region AGzippedEventStreamIsDecodedAsItArrives()

        /// <summary>
        /// The path that has no alternative. An event stream is not supposed to
        /// end, so there is never a moment at which it could be buffered first and
        /// decoded afterwards — and a reverse proxy in front of one gzips it like
        /// anything else of type text/*.
        /// </summary>
        [Test]
        public async Task AGzippedEventStreamIsDecodedAsItArrives()
        {

            using var server = new RawServer(
                                   CloseWhenDone: false,
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Type: text/event-stream\r\n" +
                                       "Content-Encoding: gzip\r\n" +
                                       "Cache-Control: no-cache\r\n" +
                                       "Connection: keep-alive\r\n\r\n",
                                       GZip("retry: 1000\n\nevent: tick\ndata: 1\n\n"))
                               );

            using var client = new HTTPClient(server.URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.That(response.HTTPBodyStream,          Is.Not.Null, "an event stream is handed over as a stream");
            Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));

            var buffer  = new Byte[128];
            var read    = await response.HTTPBodyStream!.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Does.StartWith("retry: 1000\n\nevent: tick\ndata: 1\n\n"));

        }

        #endregion

    }

}
