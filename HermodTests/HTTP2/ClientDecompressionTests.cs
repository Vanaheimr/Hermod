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

using System.IO.Compression;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Client-side content coding (RFC 9110, Section 8.4) — the decode direction,
    /// which until now existed nowhere in the stack: the server could compress a
    /// response, but nothing could undo one.
    ///
    /// With <c>AutomaticDecompression</c> the client advertises what it can undo
    /// and hands the caller the identity representation. The codec itself is
    /// tested directly (round-trips, chained codings, the zlib/raw deflate split,
    /// and the decompression-bomb ceiling), then end-to-end against our own
    /// compressing server.
    /// </summary>
    [TestFixture]
    public class ClientDecompressionTests
    {

        #region Data / server

        private static readonly String BigText = String.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 14));
        private static readonly Byte[] BigBody = Encoding.UTF8.GetBytes(BigText);

        private static readonly HTTPResourceHandler Resource = (path, headers, ct) => Task.FromResult<HTTPResource?>(path switch
        {
            "/big" => new HTTPResource { Body = BigBody, ContentType = "text/plain; charset=utf-8" },
            _      => null
        });

        private TestH2Server srv = null!;

        [OneTimeSetUp]
        public async Task StartServer()
            => srv = await TestH2Server.StartAsync(HTTPSemantics.Wrap(Resource, CompressResponses: true));

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        #endregion


        #region Codec_RoundTripsEveryCoding()

        [Test]
        public void Codec_RoundTripsEveryCoding()
        {
            Assert.Multiple(() =>
            {
                foreach (var coding in HTTPContentCoding.Supported)
                {
                    var encoded = HTTPContentCoding.Encode(BigBody, coding);
                    Assert.That(encoded,                                                Is.Not.EqualTo(BigBody), $"{coding}: actually encoded");
                    Assert.That(HTTPContentCoding.Decode(encoded, coding, 1024 * 1024), Is.EqualTo(BigBody),     $"{coding}: round-trips");
                }
            });
        }

        #endregion

        #region Codec_ReadsBothDeflateFlavours()

        // "deflate" is the coding the wire disagrees about: RFC 9110 names the
        // zlib-wrapped form (RFC 1950), plenty of servers send the raw one
        // (RFC 1951), and .NET's DeflateStream reads only raw. Both must work.
        [Test]
        public void Codec_ReadsBothDeflateFlavours()
        {

            Byte[] Wrap(Func<Stream, Stream> make)
            {
                using var output = new MemoryStream();
                using (var s = make(output))
                    s.Write(BigBody, 0, BigBody.Length);
                return output.ToArray();
            }

            var raw  = Wrap(o => new DeflateStream(o, CompressionLevel.Optimal, leaveOpen: true));
            var zlib = Wrap(o => new ZLibStream   (o, CompressionLevel.Optimal, leaveOpen: true));

            Assert.Multiple(() =>
            {
                Assert.That(HTTPContentCoding.Decode(raw,  "deflate", 1024 * 1024), Is.EqualTo(BigBody), "raw RFC 1951");
                Assert.That(HTTPContentCoding.Decode(zlib, "deflate", 1024 * 1024), Is.EqualTo(BigBody), "zlib-wrapped RFC 1950");
            });

        }

        #endregion

        #region Codec_RefusesADecompressionBomb()

        // The cap has to bite *during* decompression: checking the output size
        // afterwards would mean the bomb had already gone off in memory.
        [Test]
        public void Codec_RefusesADecompressionBomb()
        {

            var bomb    = HTTPContentCoding.Encode(new Byte[8 * 1024 * 1024], "gzip");   // 8 MiB of zeroes
            var headers = new List<(String Name, String Value)> { ("content-encoding", "gzip") };

            Assert.Multiple(() =>
            {
                Assert.That(bomb.Length,                                     Is.LessThan(64 * 1024), "compresses to almost nothing");
                Assert.That(() => HTTPContentCoding.Decode(bomb, "gzip", 1024 * 1024),
                            Throws.InstanceOf<InvalidDataException>(),                                "refused at the ceiling");
                Assert.That(() => HTTPContentCoding.DecodeBody(headers, bomb, 1024 * 1024),
                            Throws.InstanceOf<InvalidDataException>(),                                "and through DecodeBody too");
            });

        }

        #endregion

        #region Codec_DecodeBody_HandlesTheAwkwardCases()

        [Test]
        public void Codec_DecodeBody_HandlesTheAwkwardCases()
        {

            // Chained codings are undone right to left (§8.4).
            var chained = HTTPContentCoding.Encode(HTTPContentCoding.Encode(BigBody, "deflate"), "gzip");
            var h1      = new List<(String Name, String Value)> { ("content-encoding", "deflate, gzip"), ("content-length", chained.Length.ToString()) };
            var (b1, d1) = HTTPContentCoding.DecodeBody(h1, chained, 1024 * 1024);

            // An unknown coding leaves the message exactly as received: handing back
            // undecodable bytes labelled "identity" would be worse than doing nothing.
            var h2       = new List<(String Name, String Value)> { ("content-encoding", "exotic-lz") };
            var (b2, d2) = HTTPContentCoding.DecodeBody(h2, BigBody, 1024 * 1024);

            // "identity" means nothing was applied.
            var h3       = new List<(String Name, String Value)> { ("content-encoding", "identity") };
            var (b3, d3) = HTTPContentCoding.DecodeBody(h3, BigBody, 1024 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(b1,                                             Is.EqualTo(BigBody),          "chained: decoded");
                Assert.That(d1,                                             Is.EqualTo("deflate, gzip"),  "chained: both reported");
                Assert.That(h1.Any(h => h.Name == "content-encoding"),       Is.False,                     "chained: header consumed");
                Assert.That(h1.First(h => h.Name == "content-length").Value, Is.EqualTo(BigBody.Length.ToString()),
                                                                                                           "chained: length now describes the decoded bytes");

                Assert.That(b2,                                             Is.EqualTo(BigBody),          "unknown: body untouched");
                Assert.That(d2,                                             Is.Null,                      "unknown: nothing claimed");
                Assert.That(h2.Any(h => h.Name == "content-encoding"),       Is.True,                      "unknown: header left in place");

                Assert.That(b3,                                             Is.EqualTo(BigBody),          "identity: body untouched");
                Assert.That(d3,                                             Is.Null);
                Assert.That(h3.Any(h => h.Name == "content-encoding"),       Is.False,                     "identity: header dropped as noise");
            });

        }

        #endregion


        #region OurClient_DecodesOurServersResponse()

        [Test]
        public async Task OurClient_DecodesOurServersResponse()
        {

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                                                       Options: new HTTP2ClientOptions { AutomaticDecompression = true });

            var resp = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                             Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(resp.Body),      Is.EqualTo(BigText),   "caller sees the identity representation");
                Assert.That(resp.DecodedContentEncoding,             Is.EqualTo("br"),      "server picked our first preference");
                Assert.That(resp.HeaderValue("content-encoding"),    Is.Null,               "header consumed with the coding");
                Assert.That(resp.HeaderValue("content-length"),      Is.EqualTo(BigBody.Length.ToString()));
            });

        }

        #endregion

        #region WithoutTheOption_NothingChanges()

        // Off by default: no accept-encoding goes out, so the server has nothing to
        // negotiate and the response arrives as identity — byte-identical to how
        // this client behaved before the feature existed.
        [Test]
        public async Task WithoutTheOption_NothingChanges()
        {

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(resp.Body), Is.EqualTo(BigText));
                Assert.That(resp.HeaderValue("content-encoding"), Is.Null, "server compressed nothing");
                Assert.That(resp.DecodedContentEncoding,          Is.Null, "and we decoded nothing");
            });

        }

        #endregion

        #region CallersOwnAcceptEncoding_Wins()

        // A caller who states an accept-encoding means it — we must not widen it
        // behind their back. "identity" switches compression off for one request,
        // and the response then needs no decoding.
        [Test]
        public async Task CallersOwnAcceptEncoding_Wins()
        {

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                                                       Options: new HTTP2ClientOptions { AutomaticDecompression = true });

            var identity = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big",
                                                        [("accept-encoding", "identity")]);

            var gzipOnly = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big",
                                                        [("accept-encoding", "gzip")]);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(identity.Body), Is.EqualTo(BigText));
                Assert.That(identity.DecodedContentEncoding,        Is.Null,             "nothing to decode");

                Assert.That(Encoding.UTF8.GetString(gzipOnly.Body), Is.EqualTo(BigText), "still transparently decoded");
                Assert.That(gzipOnly.DecodedContentEncoding,        Is.EqualTo("gzip"),  "narrowed to what the caller allowed");
            });

        }

        #endregion

        #region OversizedBody_SurfacesAsAFailure()

        // A body past MaxDecodedBodySize must fail the request, not arrive
        // truncated or still-compressed but labelled identity.
        [Test]
        public async Task OversizedBody_SurfacesAsAFailure()
        {

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                                                       Options: new HTTP2ClientOptions {
                                                           AutomaticDecompression = true,
                                                           MaxDecodedBodySize     = 16   // smaller than /big
                                                       });

            Assert.That(async () => await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/big"),
                        Throws.InstanceOf<InvalidDataException>());

            await conn.CloseAsync();

        }

        #endregion

    }

}
