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

using System.Text;
using System.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Interop for the hand-rolled <see cref="HTTP2Client"/>: it speaks real
    /// HTTP/2 (preface, SETTINGS, HPACK decode of a real encoder's output, DATA +
    /// flow control, multiplexing) against both our own server and a strict .NET
    /// Kestrel HTTP/2 server. In-process.
    /// </summary>
    [TestFixture]
    public class ClientInteropTests
    {

        #region (our server handler)

        private static async Task<(List<(String Name, String Value)>, Byte[]?)> Handle(
            UInt32 streamId, List<(String Name, String Value)> reqHeaders, Byte[]? reqBody, CancellationToken ct)
        {
            var path = reqHeaders.FirstOrDefault(h => h.Name == ":path").Value ?? "/";
            await Task.Yield();
            return path switch
            {
                "/"      => ([(":status", "200"), ("content-type", "text/plain")],               Encoding.UTF8.GetBytes("Hello from our server!")),
                "/echo"  => ([(":status", "200"), ("content-type", "application/octet-stream")], reqBody ?? []),
                "/large" => ([(":status", "200"), ("content-type", "application/octet-stream")], LargeBody()),
                "/slow"  => await Slow(ct),
                _        => ([(":status", "404")],                                                Encoding.UTF8.GetBytes("nope")),
            };

            static Byte[] LargeBody() { var b = new Byte[128 * 1024]; Random.Shared.NextBytes(b); return b; }
            static async Task<(List<(String, String)>, Byte[]?)> Slow(CancellationToken ct)
            {
                await Task.Delay(1500, ct);
                return ([(":status", "200")], Encoding.UTF8.GetBytes("slow done"));
            }
        }

        private static void MapKestrelRoutes(WebApplication app)
        {
            app.MapGet("/", () => "Hello from Kestrel!");
            app.MapPost("/echo", async (HttpContext ctx) =>
            {
                using var ms = new MemoryStream();
                await ctx.Request.Body.CopyToAsync(ms);
                ctx.Response.ContentType = "application/octet-stream";
                await ctx.Response.Body.WriteAsync(ms.ToArray());
            });
            app.MapGet("/big", async (HttpContext ctx) =>
            {
                var buf = new Byte[64 * 1024];
                Random.Shared.NextBytes(buf);
                ctx.Response.ContentType = "application/octet-stream";
                await ctx.Response.Body.WriteAsync(buf);
            });
        }

        #endregion


        #region OurServer_RequestsAndFlowControl()

        [Test]
        public async Task OurServer_RequestsAndFlowControl()
        {
            await using var srv = await TestH2Server.StartAsync(Handle);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var get = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.Multiple(() =>
            {
                Assert.That(get.Status, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(get.Body), Is.EqualTo("Hello from our server!"), "body correct");
            });

            var payload = Encoding.UTF8.GetBytes("round-trip 🚀");
            var echo = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo", Body: payload);
            Assert.Multiple(() =>
            {
                Assert.That(echo.Status, Is.EqualTo(200));
                Assert.That(echo.Body, Is.EqualTo(payload), "echo byte-exact");
            });

            var large = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/large");
            Assert.Multiple(() =>
            {
                Assert.That(large.Status,      Is.EqualTo(200));
                Assert.That(large.Body.Length, Is.EqualTo(128 * 1024), "128 KiB via flow control");
            });

            await conn.CloseAsync();
        }

        #endregion

        #region OurServer_Multiplexing()

        [Test]
        public async Task OurServer_Multiplexing()
        {
            await using var srv = await TestH2Server.StartAsync(Handle);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var sw     = Stopwatch.StartNew();
            var tSlow  = conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/slow");
            var tFast  = conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            var tLarge = conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/large");
            var fast   = await tFast;  var fastAt  = sw.ElapsedMilliseconds;
            _          = await tLarge;
            var slow   = await tSlow;  var slowAt  = sw.ElapsedMilliseconds;

            Assert.Multiple(() =>
            {
                Assert.That(fast.Status, Is.EqualTo(200));
                Assert.That(slow.Status, Is.EqualTo(200));
                Assert.That(fastAt, Is.LessThan(800),           $"fast completes early (fast={fastAt}ms)");
                Assert.That(slowAt, Is.GreaterThanOrEqualTo(1400), $"slow does not block the fast ones (slow={slowAt}ms)");
            });

            await conn.CloseAsync();
        }

        #endregion

        #region Kestrel_RequestsAndFlowControl()

        [Test]
        public async Task Kestrel_RequestsAndFlowControl()
        {
            await using var srv = await KestrelH2Server.StartAsync(MapKestrelRoutes);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var get = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.Multiple(() =>
            {
                Assert.That(get.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(get.Body),  Is.EqualTo("Hello from Kestrel!"), "Kestrel body decoded (HPACK/Huffman)");
                Assert.That(get.HeaderValue("content-type") ?? "", Does.Contain("text/plain"),     "content-type header decoded");
            });

            var payload = Encoding.UTF8.GetBytes("kestrel round-trip äöü");
            var echo = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo", Body: payload);
            Assert.Multiple(() =>
            {
                Assert.That(echo.Status, Is.EqualTo(200));
                Assert.That(echo.Body,   Is.EqualTo(payload), "echo byte-exact");
            });

            var big = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big");
            Assert.Multiple(() =>
            {
                Assert.That(big.Status,      Is.EqualTo(200));
                Assert.That(big.Body.Length, Is.EqualTo(64 * 1024), "64 KiB via flow control vs Kestrel");
            });

            await conn.CloseAsync();
        }

        #endregion

        #region Kestrel_ConcurrencyAndNotFound()

        [Test]
        public async Task Kestrel_ConcurrencyAndNotFound()
        {
            await using var srv = await KestrelH2Server.StartAsync(MapKestrelRoutes);
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var r = await Task.WhenAll(
                conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/"),
                conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big"),
                conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/"));
            Assert.That(r.All(x => x.Status == 200), Is.True, "3 concurrent requests all 200");

            var missing = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/does-not-exist");
            Assert.That(missing.Status, Is.EqualTo(404), "unknown path -> 404");

            await conn.CloseAsync();
        }

        #endregion

    }

}
