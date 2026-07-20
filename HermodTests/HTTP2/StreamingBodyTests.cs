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
using System.Net.Security;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Streaming request/response bodies + response trailers (the
    /// bidirectional-streaming / gRPC enabler). A server with a streaming handler
    /// is driven by our own client and .NET HttpClient: server-streaming
    /// responses, streamed request bodies read chunk-by-chunk, bidirectional echo,
    /// and response trailers. In-process.
    /// </summary>
    [TestFixture]
    public class StreamingBodyTests
    {

        #region Server (streaming handler routed by :path)

        private static Task<(List<(String, String)>, Byte[]?)> Unused(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "500")], null));

        private static async Task Streaming(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {

            var path = req.Headers.First(h => h.Name == ":path").Value;

            switch (path)
            {

                case "/stream-response":
                    await resp.WriteHeadersAsync([(":status", "200"), ("content-type", "text/plain")], ct);
                    for (var i = 0; i < 5; i++)
                    {
                        await resp.WriteAsync(Encoding.UTF8.GetBytes($"chunk{i}"), ct);
                        await Task.Delay(15, ct);
                    }
                    await resp.CompleteAsync(null, ct);
                    break;

                case "/echo-stream":
                    await resp.WriteHeadersAsync([(":status", "200")], ct);
                    Byte[]? chunk;
                    while ((chunk = await req.ReadAsync(ct)) is not null)
                        await resp.WriteAsync(chunk, ct);
                    await resp.CompleteAsync(null, ct);
                    break;

                case "/trailers":
                    await resp.WriteHeadersAsync([(":status", "200"), ("content-type", "text/plain")], ct);
                    await resp.WriteAsync(Encoding.UTF8.GetBytes("hello"), ct);
                    await resp.CompleteAsync([("grpc-status", "0"), ("x-checksum", "abc123")], ct);
                    break;

                case "/count-body":
                    Int64 total = 0;
                    Byte[]? c;
                    while ((c = await req.ReadAsync(ct)) is not null)
                        total += c.Length;
                    await resp.WriteHeadersAsync([(":status", "200")], ct);
                    await resp.WriteAsync(Encoding.UTF8.GetBytes(total.ToString()), ct);
                    await resp.CompleteAsync([("x-received-bytes", total.ToString())], ct);
                    break;

                default:
                    await resp.WriteHeadersAsync([(":status", "404")], ct);
                    await resp.CompleteAsync(null, ct);
                    break;

            }

        }

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(Unused, StreamingHandler: Streaming);

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion


        #region OurClient_ServerStreamingResponse()

        [Test]
        public async Task OurClient_ServerStreamingResponse()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var sr   = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/stream-response");
            Assert.Multiple(() =>
            {
                Assert.That(sr.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(sr.Body),  Is.EqualTo("chunk0chunk1chunk2chunk3chunk4"), "chunks assemble in order");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_EchoStream()

        [Test]
        public async Task OurClient_EchoStream()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var echo = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo-stream",
                           Body: Encoding.UTF8.GetBytes("hello streaming world"));
            Assert.Multiple(() =>
            {
                Assert.That(echo.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(echo.Body),  Is.EqualTo("hello streaming world"), "streamed body echoed");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_ResponseTrailers()

        [Test]
        public async Task OurClient_ResponseTrailers()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var tr   = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/trailers");
            Assert.Multiple(() =>
            {
                Assert.That(tr.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(tr.Body),  Is.EqualTo("hello"), "response body");
                Assert.That(tr.Trailers.FirstOrDefault(t => t.Name == "grpc-status").Value, Is.EqualTo("0"),      "grpc-status trailer");
                Assert.That(tr.Trailers.FirstOrDefault(t => t.Name == "x-checksum").Value,  Is.EqualTo("abc123"), "x-checksum trailer");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_LargeStreamedRequestBody()

        [Test]
        public async Task OurClient_LargeStreamedRequestBody()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var big  = new Byte[200_000];
            new Random(7).NextBytes(big);
            var bc = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/count-body", Body: big);
            Assert.Multiple(() =>
            {
                Assert.That(bc.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(bc.Body),  Is.EqualTo(big.Length.ToString()), "large streamed body counted");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region HttpClient_ServerStreamingResponse()

        [Test]
        public async Task HttpClient_ServerStreamingResponse()
        {
            using var http = H2.MakeHttpClient();
            var resp = await http.GetAsync($"https://localhost:{srv.Port}/stream-response", HttpCompletionOption.ResponseHeadersRead);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) resp.StatusCode, Is.EqualTo(200));
                Assert.That(body, Is.EqualTo("chunk0chunk1chunk2chunk3chunk4"), "server-streamed response read");
            });
        }

        #endregion

        #region HttpClient_ResponseTrailers()

        [Test]
        public async Task HttpClient_ResponseTrailers()
        {
            using var http = H2.MakeHttpClient();
            var resp = await http.GetAsync($"https://localhost:{srv.Port}/trailers");
            var body = await resp.Content.ReadAsStringAsync();   // trailers populate after the body is read
            var hasGrpc = resp.TrailingHeaders.TryGetValues("grpc-status", out var vals) && vals.First() == "0";
            Assert.Multiple(() =>
            {
                Assert.That((Int32) resp.StatusCode, Is.EqualTo(200));
                Assert.That(body,    Is.EqualTo("hello"), "response body");
                Assert.That(hasGrpc, Is.True,             "grpc-status trailer exposed");
            });
        }

        #endregion

        #region HttpClient_StreamedRequestBody()

        [Test]
        public async Task HttpClient_StreamedRequestBody()
        {
            using var http = H2.MakeHttpClient();
            var resp = await http.PostAsync($"https://localhost:{srv.Port}/echo-stream", new ChunkedContent());
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) resp.StatusCode, Is.EqualTo(200));
                Assert.That(body, Is.EqualTo("part0part1part2"), "streamed request body echoed");
            });
        }

        #endregion


        #region (nested) ChunkedContent

        /// <summary>
        /// A request body of unknown length that writes 3 chunks with flushes, so it
        /// goes out as multiple DATA frames rather than one buffered body.
        /// </summary>
        private sealed class ChunkedContent : HttpContent
        {

            protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            {
                for (var i = 0; i < 3; i++)
                {
                    await stream.WriteAsync(Encoding.UTF8.GetBytes($"part{i}"));
                    await stream.FlushAsync();
                    await Task.Delay(20);
                }
            }

            protected override Boolean TryComputeLength(out Int64 length)
            {
                length = 0;
                return false;
            }

        }

        #endregion

    }

}
