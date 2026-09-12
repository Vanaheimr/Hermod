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
using System.IO.Compression;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Tests for MapSinglePageApplication: bundle files, the fallback to the
    /// application stub, entity tags, conditional requests, content coding
    /// and the security related header fields.
    /// </summary>
    [TestFixture]
    public class SinglePageAppTests
    {

        #region Data

        private const String ResourcePrefix  = "org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.HTTPRoot.SPA.";
        private const String AssetPath       = "/assets/app.0123456789abcdef.js";

        #endregion

        #region (private) StartAsync(Options = null)

        private static async Task<(HTTPServer Server, HTTPClient Client)> StartAsync(SinglePageAppOptions? Options = null)
        {

            var httpServer  = await HTTPServer.StartNew();
            var httpAPI     = httpServer.AddHTTPAPI();

            httpAPI.MapSinglePageApplication(
                new EmbeddedContentSource(ResourcePrefix, typeof(SinglePageAppTests).Assembly),
                Options
            );

            var httpClient  = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            return (httpServer, httpClient.Item1!);

        }

        #endregion


        #region Stub_ForRootAndPageURLs()

        [Test]
        public async Task Stub_ForRootAndPageURLs()
        {

            var (server, client) = await StartAsync(
                                       new SinglePageAppOptions {
                                           IndexTransform = html => html.Replace("{{ServerVersion}}", "v1.2.3")
                                       }
                                   );

            try
            {

                foreach (var path in new[] { "/", "/devices/42", "/a/b/c", "/index.html" })
                {

                    var response  = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(path)));
                    var body      = response.HTTPBodyAsUTF8String ?? "";

                    Assert.That(response.HTTPStatusCode,                      Is.EqualTo(HTTPStatusCode.OK),                   path);
                    Assert.That(response.ContentType?.ToString(),             Does.StartWith("text/html"),                     path);
                    Assert.That(body,                                         Does.Contain("<div id=\"app\">"),                path);
                    Assert.That(body,                                         Does.Contain("content=\"v1.2.3\""),              path);
                    Assert.That(body,                                         Does.Not.Contain("{{ServerVersion}}"),           path);
                    Assert.That(response.ETag,                                Is.Not.Null.And.StartWith("\""),                 path);
                    Assert.That(response.CacheControl,                        Is.EqualTo("no-cache"),                          path);
                    Assert.That(response.GetHeaderField("Content-Security-Policy"),  Does.Contain("default-src 'self'"),      path);
                    Assert.That(response.GetHeaderField("X-Content-Type-Options"),   Is.EqualTo("nosniff"),                   path);
                    Assert.That(response.GetHeaderField("X-Frame-Options"),          Is.EqualTo("DENY"),                      path);

                }

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region Asset_IsDeliveredWithLongTermCaching()

        [Test]
        public async Task Asset_IsDeliveredWithLongTermCaching()
        {

            var (server, client) = await StartAsync();

            try
            {

                var response  = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath)));
                var body      = response.HTTPBodyAsUTF8String ?? "";

                Assert.That(response.HTTPStatusCode,                              Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.ContentType?.ToString(),                     Does.StartWith("text/javascript"));
                Assert.That(body,                                                 Does.Contain("The quick brown fox"));
                Assert.That(response.CacheControl,                                Is.EqualTo("public, max-age=31536000, immutable"));
                Assert.That(response.ETag,                                        Is.Not.Null.And.StartWith("\""));
                Assert.That(response.Vary,                                        Is.EqualTo("Accept-Encoding"));
                Assert.That(response.GetHeaderField("Content-Encoding"),          Is.Null);
                Assert.That(response.GetHeaderField("X-Content-Type-Options"),    Is.EqualTo("nosniff"));
                Assert.That(response.GetHeaderField("Content-Security-Policy"),   Is.Null, "only documents carry a CSP");

                var favicon   = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/favicon.svg")));

                Assert.That(favicon.HTTPStatusCode,                               Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(favicon.ContentType?.ToString(),                      Does.StartWith("image/svg+xml"));
                Assert.That(favicon.CacheControl,                                 Is.EqualTo("public, max-age=3600"), "not a hashed file name");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region MissingAsset_Is404NotTheStub()

        [Test]
        public async Task MissingAsset_Is404NotTheStub()
        {

            var (server, client) = await StartAsync();

            try
            {

                var response = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/assets/missing.js")));

                Assert.That(response.HTTPStatusCode,           Is.EqualTo(HTTPStatusCode.NotFound));
                Assert.That(response.ContentType?.ToString(),  Does.StartWith("text/plain"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region IfNoneMatch_Gives304()

        [Test]
        public async Task IfNoneMatch_Gives304()
        {

            var (server, client) = await StartAsync();

            try
            {

                var first     = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath)));
                var etag      = first.ETag;

                Assert.That(etag, Is.Not.Null);

                var request   = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath));
                request.IfNoneMatch = etag;

                var second    = await client.SendRequest(request);

                Assert.That(second.HTTPStatusCode,                             Is.EqualTo(HTTPStatusCode.NotModified));
                Assert.That(second.ETag,                                       Is.EqualTo(etag));
                Assert.That(second.HTTPBody is null || second.HTTPBody.Length == 0, Is.True, "a 304 has no body");

                var weak      = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath));
                weak.IfNoneMatch = $"\"other\", W/{etag}";

                var third     = await client.SendRequest(weak);

                Assert.That(third.HTTPStatusCode,                              Is.EqualTo(HTTPStatusCode.NotModified), "weak comparison within a list");

                var stale     = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath));
                stale.IfNoneMatch = "\"stale\"";

                var fourth    = await client.SendRequest(stale);

                Assert.That(fourth.HTTPStatusCode,                             Is.EqualTo(HTTPStatusCode.OK));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region ContentCoding_WhenAccepted()

        [Test]
        public async Task ContentCoding_WhenAccepted()
        {

            var (server, client) = await StartAsync();

            try
            {

                var identity   = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath)));
                var original   = identity.HTTPBody ?? [];

                Assert.That(original.Length, Is.GreaterThanOrEqualTo(1024), "the test asset must exceed the minimum compression size");

                foreach (var (acceptEncoding, expectedCoding) in new[] {
                             ("gzip",                        "gzip"),
                             ("gzip, deflate, br",          "br"),
                             ("*",                          "br"),
                             ("br;q=0, gzip;q=0.5",         "gzip")
                         })
                {

                    var request = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath));
                    request.AcceptEncoding = acceptEncoding;

                    var response = await client.SendRequest(request);
                    var encoded  = response.HTTPBody ?? [];

                    Assert.That(response.HTTPStatusCode,                        Is.EqualTo(HTTPStatusCode.OK),                     acceptEncoding);
                    Assert.That(response.GetHeaderField("Content-Encoding"),    Is.EqualTo(expectedCoding),                        acceptEncoding);
                    Assert.That(response.ETag,                                  Does.EndWith($"-{expectedCoding}\""),              acceptEncoding);
                    Assert.That(response.Vary,                                  Is.EqualTo("Accept-Encoding"),                     acceptEncoding);
                    Assert.That(encoded.Length,                                 Is.LessThan(original.Length),                      acceptEncoding);
                    Assert.That(Decode(encoded, expectedCoding),                Is.EqualTo(original),                              acceptEncoding);

                }

                // The identity representation must not be confused with an encoded one.
                var noCoding = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse(AssetPath));
                noCoding.AcceptEncoding = "identity";

                var plain    = await client.SendRequest(noCoding);

                Assert.That(plain.GetHeaderField("Content-Encoding"),           Is.Null);
                Assert.That(plain.ETag,                                         Is.EqualTo(identity.ETag));

                // Small files are sent as they are, even when a coding is accepted.
                var small    = client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/favicon.svg"));
                small.AcceptEncoding = "gzip, br";

                var favicon  = await client.SendRequest(small);

                Assert.That(favicon.HTTPStatusCode,                             Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(favicon.GetHeaderField("Content-Encoding"),         Is.Null);

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region HEAD_HasHeadersButNoBody()

        [Test]
        public async Task HEAD_HasHeadersButNoBody()
        {

            var (server, client) = await StartAsync();

            try
            {

                var get   = await client.SendRequest(client.CreateRequest(HTTPMethod.GET,  HTTPPath.Parse(AssetPath)));
                var head  = await client.SendRequest(client.CreateRequest(HTTPMethod.HEAD, HTTPPath.Parse(AssetPath)));

                Assert.That(head.HTTPStatusCode,   Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(head.ETag,             Is.EqualTo(get.ETag));
                Assert.That(head.ContentLength,    Is.EqualTo(get.ContentLength));
                Assert.That(head.HTTPBody is null || head.HTTPBody.Length == 0, Is.True, "a HEAD response has no body");

                var page  = await client.SendRequest(client.CreateRequest(HTTPMethod.HEAD, HTTPPath.Parse("/devices/42")));

                Assert.That(page.HTTPStatusCode,                Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(page.ContentType?.ToString(),       Does.StartWith("text/html"));

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region (private) Decode(Data, Coding)

        private static Byte[] Decode(Byte[]  Data,
                                     String  Coding)
        {

            using var input   = new MemoryStream(Data);
            using var output  = new MemoryStream();

            using (Stream decoder = Coding switch {
                                        "gzip"  => new GZipStream  (input, CompressionMode.Decompress),
                                        "br"    => new BrotliStream(input, CompressionMode.Decompress),
                                        _       => throw new NotSupportedException(Coding)
                                    })
            {
                decoder.CopyTo(output);
            }

            return output.ToArray();

        }

        #endregion

    }

}
