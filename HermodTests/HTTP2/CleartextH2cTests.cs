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
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Cleartext HTTP/2 with prior knowledge (RFC 9113 §3.3): no TLS, no ALPN —
    /// the client sends the connection preface straight over plain TCP. Verified
    /// on all three interop legs: our client ↔ our server, .NET HttpClient
    /// (prior-knowledge) → our server, and our client → .NET Kestrel h2c.
    /// In-process.
    /// </summary>
    [TestFixture]
    public class CleartextH2cTests
    {

        #region (our cleartext server handler)

        private static Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 s, List<(String Name, String Value)> h, Byte[]? body, CancellationToken ct)
        {
            var path = h.FirstOrDefault(x => x.Name == ":path").Value ?? "/";
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(path switch
            {
                "/"      => ([(":status", "200"), ("content-type", "text/plain")],               Encoding.UTF8.GetBytes("Hello over h2c!")),
                "/echo"  => ([(":status", "200"), ("content-type", "application/octet-stream")], body ?? []),
                "/large" => ([(":status", "200"), ("content-type", "application/octet-stream")], LargeBody()),
                _        => ([(":status", "404")],                                                Encoding.UTF8.GetBytes("nope")),
            });

            static Byte[] LargeBody() { var b = new Byte[128 * 1024]; Random.Shared.NextBytes(b); return b; }
        }

        #endregion


        #region OurClient_OurServer_Cleartext()

        [Test]
        public async Task OurClient_OurServer_Cleartext()
        {
            await using var srv = await TestH2Server.StartAsync(Handle, Cleartext: true);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, Cleartext: true);

            var get = await conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/");
            Assert.Multiple(() =>
            {
                Assert.That(get.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(get.Body),  Is.EqualTo("Hello over h2c!"), "body correct");
            });

            var payload = Encoding.UTF8.GetBytes("cleartext round-trip 🚀");
            var echo = await conn.SendRequestAsync("POST", "http", $"localhost:{srv.Port}", "/echo", Body: payload);
            Assert.Multiple(() =>
            {
                Assert.That(echo.Status, Is.EqualTo(200));
                Assert.That(echo.Body,   Is.EqualTo(payload), "byte-exact echo");
            });

            var large = await conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/large");
            Assert.Multiple(() =>
            {
                Assert.That(large.Status,      Is.EqualTo(200));
                Assert.That(large.Body.Length, Is.EqualTo(128 * 1024), "128 KiB via flow control, no TLS");
            });

            var r = await Task.WhenAll(
                conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/"),
                conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/large"),
                conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/"));
            Assert.That(r.All(x => x.Status == 200), Is.True, "3 concurrent requests all 200");

            await conn.CloseAsync();
        }

        #endregion

        #region HttpClient_PriorKnowledge_OurServer()

        [Test]
        public async Task HttpClient_PriorKnowledge_OurServer()
        {
            await using var srv = await TestH2Server.StartAsync(Handle, Cleartext: true);

            // http:// scheme + exact HTTP/2 => HttpClient speaks h2c with prior
            // knowledge (sends the preface directly, no Upgrade).
            using var http = new HttpClient(new SocketsHttpHandler())
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact
            };

            var get     = await http.GetAsync($"http://localhost:{srv.Port}/");
            var getBody = await get.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) get.StatusCode, Is.EqualTo(200));
                Assert.That(get.Version,            Is.EqualTo(HttpVersion.Version20), "negotiated HTTP/2");
                Assert.That(getBody,                Is.EqualTo("Hello over h2c!"),     "body decoded");
            });

            var payload  = Encoding.UTF8.GetBytes("httpclient over cleartext");
            var echo     = await http.PostAsync($"http://localhost:{srv.Port}/echo", new ByteArrayContent(payload));
            var echoBody = await echo.Content.ReadAsByteArrayAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) echo.StatusCode, Is.EqualTo(200));
                Assert.That(echoBody,                Is.EqualTo(payload), "byte-exact echo");
            });
        }

        #endregion

        #region OurClient_KestrelH2c()

        [Test]
        public async Task OurClient_KestrelH2c()
        {
            await using var srv = await KestrelH2Server.StartAsync(app =>
            {
                app.MapGet("/", () => "Hello from Kestrel h2c!");
                app.MapPost("/echo", async (HttpContext ctx) =>
                {
                    using var ms = new MemoryStream();
                    await ctx.Request.Body.CopyToAsync(ms);
                    ctx.Response.ContentType = "application/octet-stream";
                    await ctx.Response.Body.WriteAsync(ms.ToArray());
                });
            }, Cleartext: true);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, Cleartext: true);

            var get = await conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/");
            Assert.Multiple(() =>
            {
                Assert.That(get.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(get.Body),  Is.EqualTo("Hello from Kestrel h2c!"), "Kestrel body decoded (HPACK/Huffman)");
            });

            var payload = Encoding.UTF8.GetBytes("kestrel h2c round-trip äöü");
            var echo = await conn.SendRequestAsync("POST", "http", $"localhost:{srv.Port}", "/echo", Body: payload);
            Assert.Multiple(() =>
            {
                Assert.That(echo.Status, Is.EqualTo(200));
                Assert.That(echo.Body,   Is.EqualTo(payload), "byte-exact echo");
            });

            var rr = await Task.WhenAll(
                conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/"),
                conn.SendRequestAsync("GET", "http", $"localhost:{srv.Port}", "/"));
            Assert.That(rr.All(x => x.Status == 200), Is.True, "2 concurrent requests all 200");

            await conn.CloseAsync();
        }

        #endregion

    }

}
