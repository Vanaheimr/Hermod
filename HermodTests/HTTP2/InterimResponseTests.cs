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
    /// 1xx interim responses (RFC 9110, Section 15.2): automatic 100-continue
    /// (the server sends <c>:status 100</c> when a body-bearing request carries
    /// <c>Expect: 100-continue</c>) and 103 Early Hints (RFC 8297) emitted by a
    /// streaming handler before the final response. Verified against our own
    /// client (which surfaces interim responses via
    /// <c>HTTP2Response.InformationalResponses</c>) and .NET HttpClient. In-process.
    /// </summary>
    [TestFixture]
    public class InterimResponseTests
    {

        #region (handlers)

        // Buffered handler that echoes the request body — exercises automatic
        // 100-continue.
        private static Task<(List<(String, String)>, Byte[]?)> Echo(UInt32 StreamId,
                                                                    List<(String Name, String Value)> Headers,
                                                                    Byte[]? Body,
                                                                    CancellationToken CancellationToken)
        {
            var b = Body ?? [];
            return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                ([(":status", "200"), ("content-length", b.Length.ToString())], b));
        }

        private static Task<(List<(String, String)>, Byte[]?)> Unused(UInt32 StreamId,
                                                                      List<(String Name, String Value)> Headers,
                                                                      Byte[]? Body,
                                                                      CancellationToken CancellationToken)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "404")], null));

        // Streaming handler that sends 103 Early Hints (with Link preload hints)
        // before the final 200.
        private static async Task EarlyHints(IHTTP2RequestStream Request, IHTTP2ResponseStream Response, CancellationToken CancellationToken)
        {
            await Response.WriteInterimResponseAsync(103,
                [("link", "</style.css>; rel=preload; as=style"), ("link", "</app.js>; rel=preload; as=script")], CancellationToken);
            await Response.WriteHeadersAsync([(":status", "200"), ("content-type", "text/html")], CancellationToken);
            await Response.WriteAsync(Encoding.UTF8.GetBytes("<html>hi</html>"), CancellationToken);
            await Response.CompleteAsync(null, CancellationToken);
        }

        #endregion


        #region OurClient_100Continue()

        /// <summary>
        /// Our client: a body-bearing POST with <c>Expect: 100-continue</c>
        /// receives an interim 100 before the echoed 200; a POST without it does not.
        /// </summary>
        [Test]
        public async Task OurClient_100Continue()
        {

            await using var srv = await TestH2Server.StartAsync(Echo);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var body = Encoding.UTF8.GetBytes("the request body");
            var resp = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo",
                           new List<(String, String)> { ("expect", "100-continue") }, body);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                       Is.EqualTo(200),                "request succeeds");
                Assert.That(Encoding.UTF8.GetString(resp.Body), Is.EqualTo("the request body"), "body echoed");
                Assert.That(resp.InformationalResponses.Any(i => i.Status == 100), Is.True,     "received an interim 100 Continue");
            });

            // A request WITHOUT expect gets no interim 100.
            var plain = await conn.SendRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo", null, body);
            Assert.That(plain.InformationalResponses.All(i => i.Status != 100), Is.True, "no expect -> no interim 100");

            await conn.CloseAsync();

        }

        #endregion

        #region OurClient_103EarlyHints()

        /// <summary>
        /// Our client: a streaming handler's 103 Early Hints (with two Link
        /// preload hints) arrive before the final 200 response body.
        /// </summary>
        [Test]
        public async Task OurClient_103EarlyHints()
        {

            await using var srv = await TestH2Server.StartAsync(Unused, StreamingHandler: EarlyHints);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/page");

            var early = resp.InformationalResponses.FirstOrDefault(i => i.Status == 103);
            var links = early.Headers?.Where(h => h.Name == "link").Select(h => h.Value).ToList() ?? [];

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                        Is.EqualTo(200),              "final response is 200");
                Assert.That(Encoding.UTF8.GetString(resp.Body),  Is.EqualTo("<html>hi</html>"), "final body");
                Assert.That(early.Status,                        Is.EqualTo(103),              "received 103 Early Hints");
                Assert.That(links, Has.Count.EqualTo(2),                                        "103 carried two Link hints");
                Assert.That(links.Any(l => l.Contains("style.css")) && links.Any(l => l.Contains("app.js")), Is.True,
                            "Link hints reference style.css and app.js");
            });

            await conn.CloseAsync();

        }

        #endregion

        #region HttpClient_Expect100Continue()

        /// <summary>
        /// .NET HttpClient interop: an <c>Expect: 100-continue</c> POST round-trips
        /// through the automatic handshake.
        /// </summary>
        [Test]
        public async Task HttpClient_Expect100Continue()
        {

            await using var srv = await TestH2Server.StartAsync(Echo);

            using var http = H2.MakeHttpClient();

            var req = new HttpRequestMessage(HttpMethod.Post, $"https://localhost:{srv.Port}/echo")
            {
                Content       = new StringContent("hello from httpclient"),
                Version       = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            req.Headers.ExpectContinue = true;   // triggers the 100-continue handshake

            var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That((Int32) resp.StatusCode, Is.EqualTo(200),                    "status 200");
                Assert.That(text,                    Is.EqualTo("hello from httpclient"), "body round-trips");
            });

        }

        #endregion

    }

}
