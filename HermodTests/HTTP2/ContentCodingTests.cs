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
using System.IO.Compression;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// On-the-fly content coding (RFC 9110, Section 8.4):
    /// <c>HTTPSemantics.Wrap(..., CompressResponses: true)</c> compresses a
    /// compressible 200 body with the best coding the client's
    /// <c>Accept-Encoding</c> accepts. Verified against our own client (exact
    /// control over Accept-Encoding + manual decompression) and .NET HttpClient
    /// (transparent AutomaticDecompression). In-process.
    /// </summary>
    [TestFixture]
    public class ContentCodingTests
    {

        #region Data / handler

        private static readonly String BigText  = String.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 14));
        private static readonly Byte[] BigBody   = Encoding.UTF8.GetBytes(BigText);
        private static readonly Byte[] TinyBody  = "tiny"u8.ToArray();

        private static readonly HTTPResourceHandler Resource = (path, headers, ct) => Task.FromResult<HTTPResource?>(path switch
        {
            "/big"  => new HTTPResource { Body = BigBody,  ContentType = "text/plain; charset=utf-8" },
            "/tiny" => new HTTPResource { Body = TinyBody, ContentType = "text/plain; charset=utf-8" },
            _       => null
        });

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(HTTPSemantics.Wrap(Resource, CompressResponses: true));

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion

        #region (helpers)

        private static Byte[] Decompress(Byte[] data, String coding)
        {
            using var input  = new MemoryStream(data);
            using var output = new MemoryStream();
            using (Stream d = coding switch
            {
                "br"      => new BrotliStream (input, CompressionMode.Decompress),
                "gzip"    => new GZipStream   (input, CompressionMode.Decompress),
                "deflate" => new DeflateStream(input, CompressionMode.Decompress),
                _         => throw new InvalidOperationException()
            })
                d.CopyTo(output);
            return output.ToArray();
        }

        // Open a connection, send one GET (optionally with Accept-Encoding), close.
        private async Task<HTTP2Response> Get(String path, String? acceptEncoding)
        {
            var conn  = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var extra = acceptEncoding is null ? null : new List<(String, String)> { ("accept-encoding", acceptEncoding) };
            var resp  = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", path, extra);
            await conn.CloseAsync();
            return resp;
        }

        #endregion


        #region OurClient_Gzip()

        [Test]
        public async Task OurClient_Gzip()
        {
            var r = await Get("/big", "gzip");
            Assert.Multiple(() =>
            {
                Assert.That(r.HeaderValue("content-encoding"),      Is.EqualTo("gzip"),          "content-encoding");
                Assert.That(r.Body.Length,                          Is.LessThan(BigBody.Length), "compressed smaller");
                Assert.That(Decompress(r.Body, "gzip"),             Is.EqualTo(BigBody),         "decodes to the original");
                Assert.That(r.HeaderValue("vary") ?? "",            Does.Contain("accept-encoding"), "Vary includes accept-encoding");
                Assert.That(r.HeaderValue("etag") ?? "",            Does.StartWith("W/"),        "ETag weakened");
            });
        }

        #endregion

        #region OurClient_Brotli()

        [Test]
        public async Task OurClient_Brotli()
        {
            var r = await Get("/big", "br");
            Assert.Multiple(() =>
            {
                Assert.That(r.HeaderValue("content-encoding"), Is.EqualTo("br"),    "content-encoding");
                Assert.That(Decompress(r.Body, "br"),          Is.EqualTo(BigBody), "decodes to the original");
            });
        }

        #endregion

        #region OurClient_ServerPrefersGzipOverDeflate()

        [Test]
        public async Task OurClient_ServerPrefersGzipOverDeflate()
        {
            var r = await Get("/big", "deflate, gzip");
            Assert.That(r.HeaderValue("content-encoding"), Is.EqualTo("gzip"), "deflate+gzip offered -> gzip chosen");
        }

        #endregion

        #region OurClient_NoAcceptEncoding_Identity()

        [Test]
        public async Task OurClient_NoAcceptEncoding_Identity()
        {
            var r = await Get("/big", null);
            Assert.Multiple(() =>
            {
                Assert.That(r.HeaderValue("content-encoding"), Is.Null,             "no content-encoding");
                Assert.That(r.Body,                            Is.EqualTo(BigBody), "full identity body");
            });
        }

        #endregion

        #region OurClient_GzipQ0_Identity()

        [Test]
        public async Task OurClient_GzipQ0_Identity()
        {
            var r = await Get("/big", "gzip;q=0");
            Assert.Multiple(() =>
            {
                Assert.That(r.HeaderValue("content-encoding"), Is.Null,             "gzip;q=0 forbids gzip -> identity");
                Assert.That(r.Body,                            Is.EqualTo(BigBody), "full identity body");
            });
        }

        #endregion

        #region OurClient_TinyBody_NotCompressed()

        [Test]
        public async Task OurClient_TinyBody_NotCompressed()
        {
            var r = await Get("/tiny", "gzip");
            Assert.Multiple(() =>
            {
                Assert.That(r.HeaderValue("content-encoding"), Is.Null,              "below MinCompressSize -> not compressed");
                Assert.That(r.Body,                            Is.EqualTo(TinyBody), "identity body");
            });
        }

        #endregion

        #region OurClient_Revalidation_WeakETag_304()

        [Test]
        public async Task OurClient_Revalidation_WeakETag_304()
        {
            var first = await Get("/big", "gzip");
            var etag  = first.HeaderValue("etag")!;

            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var second = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big",
                             new List<(String, String)> { ("accept-encoding", "gzip"), ("if-none-match", etag) });
            await conn.CloseAsync();

            Assert.That(second.Status, Is.EqualTo(304), "revalidation with the weak ETag -> 304");
        }

        #endregion

        #region HttpClient_AutomaticDecompression()

        [Test]
        public async Task HttpClient_AutomaticDecompression()
        {

            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli | DecompressionMethods.Deflate,
                SslOptions             = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            };
            using var http = new HttpClient(handler)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact
            };

            var resp = await http.GetAsync($"https://localhost:{srv.Port}/big");
            var body = await resp.Content.ReadAsStringAsync();
            var vary = resp.Headers.Vary.Count > 0
                           ? String.Join(",", resp.Headers.Vary)
                           : (resp.Content.Headers.TryGetValues("vary", out var v) ? String.Join(",", v) : "");

            Assert.Multiple(() =>
            {
                Assert.That((Int32) resp.StatusCode, Is.EqualTo(200),     "status 200");
                Assert.That(body,                    Is.EqualTo(BigText), "transparently decompressed to the original");
                Assert.That(vary, Does.Contain("accept-encoding").IgnoreCase, "response carried Vary: accept-encoding");
            });

        }

        #endregion

    }

}
