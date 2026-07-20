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
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 10008 — the HTTP QUERY method, via
    /// <c>HTTPSemantics.Wrap(..., QueryHandler)</c>: a safe, idempotent, cacheable
    /// read whose query travels in the request body. Driven by .NET HttpClient
    /// (production peer) and our own HTTP2Client. In-process.
    /// </summary>
    [TestFixture]
    public class QueryMethodTests
    {

        #region Data / handler

        private static readonly String[] Corpus = ["apple", "apricot", "avocado", "banana", "blueberry", "cherry", "date", "fig", "grape", "mango"];

        private static Byte[] Results(String term)
            => Encoding.UTF8.GetBytes("[" + String.Join(",", Corpus.Where(x => x.Contains(term, StringComparison.OrdinalIgnoreCase)).Select(x => $"\"{x}\"")) + "]");

        private static Task<HTTPResource?> ResourceHandler(String path, List<(String, String)> headers, CancellationToken ct)
            => Task.FromResult<HTTPResource?>(path != "/search" ? null
                : new HTTPResource { Body = Results(""), ContentType = "application/json" });

        private static Task<HTTPResource?> QueryHandler(String path, List<(String, String)> headers, Byte[]? content, String? contentType, CancellationToken ct)
        {
            if (path != "/search")
                return Task.FromResult<HTTPResource?>(null);
            var term = (content is null ? "" : Encoding.UTF8.GetString(content)).Trim();
            var key  = Convert.ToHexString(SHA256.HashData(content ?? [])).ToLowerInvariant()[..8];
            return Task.FromResult<HTTPResource?>(new HTTPResource {
                Body = Results(term), ContentType = "application/json", ContentLocation = $"/search/results/{key}"
            });
        }

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(
                         HTTPSemantics.Wrap((HTTPResourceHandler) ResourceHandler, QueryHandler: QueryHandler));

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion


        #region HttpClient_Query()

        /// <summary>
        /// .NET HttpClient: GET returns the whole corpus; QUERY filters by the body
        /// term and surfaces Content-Location + ETag; a matching If-None-Match
        /// revalidates to 304; OPTIONS/POST advertise QUERY in Allow.
        /// </summary>
        [Test]
        public async Task HttpClient_Query()
        {

            using var http = H2.MakeHttpClient();
            var baseUri = $"https://localhost:{srv.Port}";

            async Task<HttpResponseMessage> Send(HttpMethod method, String path, String? body = null, Action<HttpRequestMessage>? tweak = null)
            {
                var req = new HttpRequestMessage(method, baseUri + path)
                {
                    Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
                if (body is not null)
                    req.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                tweak?.Invoke(req);
                return await http.SendAsync(req);
            }

            var query = new HttpMethod("QUERY");

            // GET /search -> the whole corpus.
            var get     = await Send(HttpMethod.Get, "/search");
            var getBody = await get.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) get.StatusCode, Is.EqualTo(200));
                Assert.That(getBody, Does.Contain("banana").And.Contain("mango"), "full corpus");
            });

            // QUERY /search with a term in the body -> filtered results.
            var q     = await Send(query, "/search", "ap");
            var qBody = await q.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That((Int32) q.StatusCode,            Is.EqualTo(200));
                Assert.That(qBody,                           Is.EqualTo("[\"apple\",\"apricot\",\"grape\"]"), "filtered");
                Assert.That(q.Content.Headers.ContentLocation, Is.Not.Null, "Content-Location (RFC 10008 §3)");
                Assert.That(q.Headers.ETag,                    Is.Not.Null, "ETag");
            });

            // A different term -> a different result set.
            var q2 = await Send(query, "/search", "berry");
            Assert.That(await q2.Content.ReadAsStringAsync(), Is.EqualTo("[\"blueberry\"]"), "only the berries");

            // QUERY is safe/cacheable -> If-None-Match with the matching ETag -> 304.
            var etag = q.Headers.ETag!.ToString();
            var cond = await Send(query, "/search", "ap", r => r.Headers.TryAddWithoutValidation("If-None-Match", etag));
            Assert.That((Int32) cond.StatusCode, Is.EqualTo(304), "If-None-Match -> 304 Not Modified");

            // OPTIONS advertises QUERY in Allow.
            var opt   = await Send(HttpMethod.Options, "/search");
            var allow = opt.Content.Headers.Allow.Count > 0 ? String.Join(", ", opt.Content.Headers.Allow) : String.Join(", ", opt.Headers.GetValues("Allow"));
            Assert.Multiple(() =>
            {
                Assert.That((Int32) opt.StatusCode, Is.EqualTo(204));
                Assert.That(allow, Does.Contain("QUERY"), "OPTIONS Allow lists QUERY");
            });

            // POST (unsupported) -> 405, Allow still lists QUERY.
            var post      = await Send(HttpMethod.Post, "/search", "x");
            var postAllow = post.Content.Headers.Allow.Count > 0 ? String.Join(", ", post.Content.Headers.Allow) : "";
            Assert.Multiple(() =>
            {
                Assert.That((Int32) post.StatusCode, Is.EqualTo(405));
                Assert.That(postAllow, Does.Contain("QUERY"), "405 Allow lists QUERY");
            });

        }

        #endregion

        #region OurClient_Query()

        /// <summary>
        /// Our client: QUERY with a Content-Type + body returns filtered results
        /// with a Content-Location; a body without Content-Type is a 400 (RFC 10008
        /// §4); an unknown path is a 404.
        /// </summary>
        [Test]
        public async Task OurClient_Query()
        {

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var q = await conn.SendRequestAsync("QUERY", "https", authority, "/search",
                        ExtraHeaders: [("content-type", "text/plain")], Body: Encoding.UTF8.GetBytes("ap"));
            Assert.Multiple(() =>
            {
                Assert.That(q.Status,                            Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(q.Body),      Is.EqualTo("[\"apple\",\"apricot\",\"grape\"]"), "filtered");
                Assert.That(q.HeaderValue("content-location"),    Is.Not.Null, "Content-Location surfaced");
            });

            // RFC 10008 §4: a QUERY with content but no Content-Type MUST fail (400).
            var noCt = await conn.SendRequestAsync("QUERY", "https", authority, "/search", Body: Encoding.UTF8.GetBytes("ap"));
            Assert.That(noCt.Status, Is.EqualTo(400), "body without Content-Type -> 400");

            // QUERY to an unknown path -> the handler returns null -> 404.
            var missing = await conn.SendRequestAsync("QUERY", "https", authority, "/nope",
                              ExtraHeaders: [("content-type", "text/plain")], Body: Encoding.UTF8.GetBytes("x"));
            Assert.That(missing.Status, Is.EqualTo(404), "unknown path -> 404");

            await conn.CloseAsync();

        }

        #endregion

    }

}
