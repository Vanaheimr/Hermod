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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Digest fields (RFC 9530): <c>Content-Digest</c> over the octets a message
    /// carries, <c>Repr-Digest</c> over the representation those octets are part
    /// of, and the <c>Want-…</c> fields that ask for either.
    ///
    /// Two properties get most of the attention here, because both are the kind of
    /// thing that looks right in a green test and is wrong on the wire:
    ///
    ///   * the digest covers the <b>encoded</b> bytes, so a compressed response has
    ///     to verify against what came off the socket and not against what the
    ///     client decoded it into afterwards — the check therefore has to run
    ///     before decompression, and one test exists purely to pin that order;
    ///   * "no digest" must never be reported as "verified". Three of the four
    ///     <see cref="HTTPDigestVerification"/> outcomes mean nothing was checked,
    ///     and a caller relying on integrity has to be able to tell them apart.
    /// </summary>
    [TestFixture]
    public class DigestFieldTests
    {

        #region Data / handlers

        /// <summary>Big enough to be worth compressing, and compressible.</summary>
        private static readonly Byte[] Text =
            Encoding.UTF8.GetBytes(String.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 40)));

        private static HTTP2RequestHandler Serving(Boolean Compress = false)
            => Hermod.HTTP2.HTTPSemantics.Wrap(
                   (path, headers, ct) => Task.FromResult<HTTPResource?>(
                       new HTTPResource { Body = Text, ContentType = "text/plain; charset=utf-8" }),
                   CompressResponses: Compress,
                   ContentDigests:    true);

        /// <summary>Reports back which <c>want-…</c> fields the client actually sent.</summary>
        private static Task<(List<(String, String)>, Byte[]?)> EchoWants(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>((
                   [(":status", "200"),
                    ("x-seen-want", h.FirstOrDefault(header => header.Name == "want-content-digest").Value ?? "<none>"),
                    ("content-length", "0")],
                   []));

        /// <summary>
        /// An origin that lies: the digest it announces belongs to different bytes
        /// than the ones it sends. Stands in for a corrupting intermediary, which
        /// is the threat the field exists for.
        /// </summary>
        private static Task<(List<(String, String)>, Byte[]?)> Lying(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>((
                   [(":status", "200"),
                    ("content-type",   "text/plain"),
                    ("content-length", Text.Length.ToString()),
                    ("content-digest", HTTPDigest.FieldValue("something else entirely"u8.ToArray(), "sha-256"))],
                   Text));

        #endregion

        #region Digest_MatchesTheRFCsOwnExample()

        // RFC 9530 Section 2's worked example, byte for byte — the cheapest possible
        // check that our field value is the one everyone else computes.
        [Test]
        public void Digest_MatchesTheRFCsOwnExample()
        {

            var content = Encoding.ASCII.GetBytes("{\"hello\": \"world\"}");

            Assert.Multiple(() =>
            {

                Assert.That(HTTPDigest.FieldValue(content, "sha-256"),
                            Is.EqualTo("sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:"));

                Assert.That(HTTPDigest.Verify("sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:", content),
                            Is.EqualTo(HTTPDigestVerification.Match));

                // And round-trips through our own parser.
                Assert.That(HTTPDigest.Parse(HTTPDigest.FieldValue(content, "sha-512"))["sha-512"].Length,
                            Is.EqualTo(64));

            });

        }

        #endregion

        #region Digest_ParsesDefensively()

        // Every one of these is something a peer can put on the wire. None of them
        // may produce a digest we then treat as authoritative.
        [Test]
        public void Digest_ParsesDefensively()
        {

            Assert.Multiple(() =>
            {

                // Parameters after the byte sequence are none of our business.
                Assert.That(HTTPDigest.Parse("sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:;q=1").ContainsKey("sha-256"),
                            Is.True);

                // Several members, one of them junk: the good one survives, the bad
                // one is dropped rather than guessed at.
                var mixed = HTTPDigest.Parse("md5=:not-base64!:, sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:");
                Assert.That(mixed.ContainsKey("sha-256"), Is.True);
                Assert.That(mixed.ContainsKey("md5"),     Is.False);

                foreach (var bad in new[] {
                             "sha-256",                     // bare key: a Boolean member, not a digest
                             "sha-256=X48E9qOokq=",         // not a byte sequence
                             "sha-256=:unterminated",       // no closing colon
                             "=:abcd:",                     // no key
                             ""
                         })
                    Assert.That(HTTPDigest.Parse(bad), Is.Empty, bad);

                // A repeated key keeps the last one, as the structured-field grammar says.
                var repeated = HTTPDigest.Parse(
                                   $"sha-256=:{Convert.ToBase64String(new Byte[32])}:, " +
                                   HTTPDigest.FieldValue("x"u8.ToArray(), "sha-256"));

                Assert.That(repeated["sha-256"], Is.EqualTo(HTTPDigest.Compute("x"u8.ToArray(), "sha-256")));

            });

        }

        #endregion

        #region Digest_VerifyKeepsTheFourOutcomesApart()

        [Test]
        public void Digest_VerifyKeepsTheFourOutcomesApart()
        {

            var content = "payload"u8.ToArray();

            Assert.Multiple(() =>
            {

                Assert.That(HTTPDigest.Verify(null, content),
                            Is.EqualTo(HTTPDigestVerification.NotPresent));

                // Present, but only algorithms we refuse to compute — emphatically
                // not the same answer as "it matched".
                Assert.That(HTTPDigest.Verify($"md5=:{Convert.ToBase64String(new Byte[16])}:", content),
                            Is.EqualTo(HTTPDigestVerification.Unsupported));

                Assert.That(HTTPDigest.Verify(HTTPDigest.FieldValue(content, "sha-256"), content),
                            Is.EqualTo(HTTPDigestVerification.Match));

                Assert.That(HTTPDigest.Verify(HTTPDigest.FieldValue(content, "sha-512"), content),
                            Is.EqualTo(HTTPDigestVerification.Match));

                Assert.That(HTTPDigest.Verify(HTTPDigest.FieldValue(content, "sha-256"), "payloae"u8.ToArray()),
                            Is.EqualTo(HTTPDigestVerification.Mismatch));

                // Two digests, one of them wrong: a sender asserting both is wrong
                // about both as far as we are concerned.
                Assert.That(HTTPDigest.Verify(
                                HTTPDigest.FieldValue(content, "sha-256") + ", " +
                                HTTPDigest.FieldValue("other"u8.ToArray(), "sha-512"), content),
                            Is.EqualTo(HTTPDigestVerification.Mismatch));

            });

        }

        #endregion

        #region Digest_WantSelectsByPreference()

        [Test]
        public void Digest_WantSelectsByPreference()
        {

            Assert.Multiple(() =>
            {

                Assert.That(HTTPDigest.SelectAlgorithm("sha-512=3, sha-256=10"), Is.EqualTo("sha-256"));
                Assert.That(HTTPDigest.SelectAlgorithm("sha-512=7, sha-256=0"),  Is.EqualTo("sha-512"), "0 means unacceptable");
                Assert.That(HTTPDigest.SelectAlgorithm("sha-256=5, sha-512=5"),  Is.EqualTo("sha-256"), "our own order breaks a tie");
                Assert.That(HTTPDigest.SelectAlgorithm("sha-512"),               Is.EqualTo("sha-512"), "a bare key still wants it");

                // No opinion expressed — send our default rather than nothing.
                Assert.That(HTTPDigest.SelectAlgorithm(null),                    Is.EqualTo("sha-256"));
                Assert.That(HTTPDigest.SelectAlgorithm("   "),                   Is.EqualTo("sha-256"));

                // Every algorithm we have was actively ruled out: say nothing at all
                // rather than send a digest that was declined.
                Assert.That(HTTPDigest.SelectAlgorithm("sha-256=0, sha-512=0"),  Is.Null);
                Assert.That(HTTPDigest.SelectAlgorithm("crc32c=10, md5=10"),     Is.Null);

                Assert.That(HTTPDigest.ParseWant(HTTPDigest.Want),
                            Is.EquivalentTo(new Dictionary<String, Int32> { ["sha-256"] = 10, ["sha-512"] = 5 }));

            });

        }

        #endregion

        #region Server_AttachesAContentDigest()

        [Test]
        public async Task Server_AttachesAContentDigest()
        {

            await using var srv = await TestH2Server.StartAsync(Serving());

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var plain  = await conn.SendRequestAsync(HTTPMethod.GET, "https", authority, "/f");
            var wanted = await conn.SendRequestAsync(HTTPMethod.GET, "https", authority, "/f",
                             [("want-content-digest", "sha-512=10, sha-256=1")]);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {

                Assert.That(HTTPDigest.Verify(plain.HeaderValue("content-digest"), plain.Body),
                            Is.EqualTo(HTTPDigestVerification.Match));

                Assert.That(plain.HeaderValue("content-digest"), Does.StartWith("sha-256="),
                            "our default when the client expressed no preference");

                Assert.That(wanted.HeaderValue("content-digest"), Does.StartWith("sha-512="),
                            "the client's preference decides the algorithm");

                Assert.That(HTTPDigest.Verify(wanted.HeaderValue("content-digest"), wanted.Body),
                            Is.EqualTo(HTTPDigestVerification.Match));

                // A full 200 needs no repr-digest: it would assert the same thing twice.
                Assert.That(plain.HeaderValue("repr-digest"), Is.Null);

            });

        }

        #endregion

        #region Server_AddsReprDigestToAPartialResponse()

        // The one case where the two fields say different things — and the reason
        // repr-digest exists at all.
        [Test]
        public async Task Server_AddsReprDigestToAPartialResponse()
        {

            await using var srv = await TestH2Server.StartAsync(Serving());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var partial = await conn.SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{srv.Port}", "/f",
                              [("range", "bytes=0-99")]);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {

                Assert.That(partial.Status,     Is.EqualTo(206));
                Assert.That(partial.Body.Length, Is.EqualTo(100));

                Assert.That(HTTPDigest.Verify(partial.HeaderValue("content-digest"), partial.Body),
                            Is.EqualTo(HTTPDigestVerification.Match), "covers the slice that was sent");

                Assert.That(HTTPDigest.Verify(partial.HeaderValue("repr-digest"), Text),
                            Is.EqualTo(HTTPDigestVerification.Match), "covers the whole representation");

                Assert.That(partial.HeaderValue("content-digest"),
                            Is.Not.EqualTo(partial.HeaderValue("repr-digest")));

            });

        }

        #endregion

        #region Server_DigestsTheEncodedBytes()

        // RFC 9530 Section 3: representation data is in its content coding, so the
        // digest describes the compressed octets. Checked with decompression *off*,
        // so the test sees exactly what the socket delivered.
        [Test]
        public async Task Server_DigestsTheEncodedBytes()
        {

            await using var srv = await TestH2Server.StartAsync(Serving(Compress: true));

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var response = await conn.SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{srv.Port}", "/f",
                               [("accept-encoding", "gzip")]);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {

                Assert.That(response.HeaderValue("content-encoding"), Is.EqualTo("gzip"), "precondition");
                Assert.That(response.Body.Length, Is.LessThan(Text.Length));

                Assert.That(HTTPDigest.Verify(response.HeaderValue("content-digest"), response.Body),
                            Is.EqualTo(HTTPDigestVerification.Match), "over the compressed octets");

                Assert.That(HTTPDigest.Verify(response.HeaderValue("content-digest"), Text),
                            Is.EqualTo(HTTPDigestVerification.Mismatch), "and not over the identity ones");

            });

        }

        #endregion

        #region Client_AsksForAndVerifiesTheDigest()

        [Test]
        public async Task Client_AsksForAndVerifiesTheDigest()
        {

            await using var echo = await TestH2Server.StartAsync(EchoWants);

            var asking = await HTTP2Client.ConnectAsync("localhost", echo.Port, H2.AcceptAnyServerCert,
                             Options: new HTTP2ClientOptions { VerifyDigests = true });
            var quiet  = await HTTP2Client.ConnectAsync("localhost", echo.Port, H2.AcceptAnyServerCert);

            var asked   = await asking.SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{echo.Port}", "/f");
            var didnt   = await quiet. SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{echo.Port}", "/f");

            await asking.CloseAsync();
            await quiet.CloseAsync();

            await using var srv = await TestH2Server.StartAsync(Serving());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions { VerifyDigests = true });

            var verified = await conn.SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{srv.Port}", "/f");

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {

                Assert.That(asked.HeaderValue("x-seen-want"), Is.EqualTo(HTTPDigest.Want),
                            "a client that intends to check has to ask");

                Assert.That(didnt.HeaderValue("x-seen-want"), Is.EqualTo("<none>"),
                            "and one that does not, does not");

                Assert.That(verified.DigestVerification, Is.EqualTo(HTTPDigestVerification.Match));
                Assert.That(verified.Body,               Is.EqualTo(Text));

                // Nothing was checked here, and the response says so rather than
                // implying otherwise.
                Assert.That(didnt.DigestVerification,    Is.EqualTo(HTTPDigestVerification.NotPresent));

            });

        }

        #endregion

        #region Client_RefusesContentThatDoesNotMatch()

        [Test]
        public async Task Client_RefusesContentThatDoesNotMatch()
        {

            await using var srv = await TestH2Server.StartAsync(Lying);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions { VerifyDigests = true });

            var authority = $"localhost:{srv.Port}";

            Assert.That(async () => await conn.SendRequestAsync(HTTPMethod.GET, "https", authority, "/f"),
                        Throws.InstanceOf<HTTPDigestMismatchException>(),
                        "corrupt content is surfaced, not returned");

            // Off by default, the same response is delivered untouched — verification
            // is the caller's decision, and this is what declining it looks like.
            var unchecked_ = await (await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert))
                                 .SendRequestAsync(HTTPMethod.GET, "https", authority, "/f");

            await conn.CloseAsync();

            Assert.That(unchecked_.Body, Is.EqualTo(Text));

        }

        #endregion

        #region Client_VerifiesBeforeDecoding()

        // The ordering trap: content-digest covers the transferred octets, so the
        // check has to happen while we still have them. Get this backwards and every
        // compressed response fails verification — which is exactly why the client
        // asks for both at once here.
        [Test]
        public async Task Client_VerifiesBeforeDecoding()
        {

            await using var srv = await TestH2Server.StartAsync(Serving(Compress: true));

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                                        VerifyDigests          = true,
                                        AutomaticDecompression = true
                                    });

            var response = await conn.SendRequestAsync(HTTPMethod.GET, "https", $"localhost:{srv.Port}", "/f");

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.DecodedContentEncoding, Is.Not.Null, "precondition: it really was encoded");
                Assert.That(response.DigestVerification,     Is.EqualTo(HTTPDigestVerification.Match));
                Assert.That(response.Body,                   Is.EqualTo(Text), "and the caller still gets identity");
            });

        }

        #endregion

        #region Download_VerifiesTheSplicedRepresentation()

        // The payoff. A download assembled out of two range responses cannot be
        // checked against either one's content digest — but it can be checked
        // against the representation digest they both name, hashed as it is written.
        [Test]
        public async Task Download_VerifiesTheSplicedRepresentation()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: FlakyWithDigest);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions { VerifyDigests = true });

            using var destination = new MemoryStream();
            var result = await conn.DownloadAsync("https", $"localhost:{srv.Port}", "/resume", destination);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.BytesWritten,       Is.EqualTo(Text.Length));
                Assert.That(destination.ToArray(),     Is.EqualTo(Text));
                Assert.That(result.Resumes,            Is.EqualTo(1), "precondition: it really was spliced");
                Assert.That(result.DigestVerification, Is.EqualTo(HTTPDigestVerification.Match));
            });

        }

        #endregion

        #region Download_RejectsASplicedRepresentationThatIsWrong()

        // Same shape, but the tail the server sends back is not the tail of the
        // representation it promised. Nothing in the range machinery can notice
        // that; the representation digest is the only thing that can.
        [Test]
        public async Task Download_RejectsASplicedRepresentationThatIsWrong()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: FlakyWithDigest);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions { VerifyDigests = true });

            using var destination = new MemoryStream();

            Assert.That(async () => await conn.DownloadAsync("https", $"localhost:{srv.Port}", "/corrupt", destination),
                        Throws.InstanceOf<HTTPDigestMismatchException>());

            await conn.CloseAsync();

        }

        #endregion

        #region Query_WithAContentDigestThatDisagrees_Is400()

        [Test]
        public async Task Query_WithAContentDigestThatDisagrees_Is400()
        {

            var handler = Hermod.HTTP2.HTTPSemantics.Wrap(
                              (path, headers, ct) => Task.FromResult<HTTPResource?>(null),
                              QueryHandler: (path, headers, content, contentType, ct) => Task.FromResult<HTTPResource?>(
                                  new HTTPResource { Body = content ?? [], ContentType = "text/plain" }),
                              ContentDigests: true);

            await using var srv = await TestH2Server.StartAsync(handler);

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";
            var query     = "find: everything"u8.ToArray();

            var honest = await conn.SendRequestAsync(HTTPMethod.QUERY, "https", authority, "/search",
                             [("content-type",   "text/plain"),
                              ("content-digest", HTTPDigest.FieldValue(query, "sha-256"))], query);

            var corrupt = await conn.SendRequestAsync(HTTPMethod.QUERY, "https", authority, "/search",
                              [("content-type",   "text/plain"),
                               ("content-digest", HTTPDigest.FieldValue("find: something else"u8.ToArray(), "sha-256"))], query);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(honest.Status,  Is.EqualTo(200));
                Assert.That(honest.Body,    Is.EqualTo(query), "the query really was run");
                Assert.That(corrupt.Status, Is.EqualTo(400),   "a query we cannot trust the input of is a bad request");
            });

        }

        #endregion

        #region (private) Streaming origin for the download tests

        private static Task<(List<(String, String)>, Byte[]?)> NotUsed(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "500")], null));

        private const Int32  Prefix = 200;
        private const String ETag   = "\"digest-etag\"";

        /// <summary>
        /// Dies mid-body on the first request and serves the tail on the second,
        /// announcing a <c>Repr-Digest</c> for the whole representation both times.
        /// On <c>/corrupt</c> the tail is subtly wrong — the right length, from the
        /// right offset, but not the promised bytes.
        /// </summary>
        private static async Task FlakyWithDigest(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {

            var path   = req.Headers.First(h => h.Name == ":path").Value;
            var range  = req.Headers.FirstOrDefault(h => h.Name == "range").Value;
            var digest = HTTPDigest.FieldValue(Text, "sha-256");

            if (range is null)
            {

                await resp.WriteHeadersAsync([
                    (":status",        "200"),
                    ("content-type",   "application/octet-stream"),
                    ("content-length", Text.Length.ToString()),
                    ("accept-ranges",  "bytes"),
                    ("etag",           ETag),
                    ("repr-digest",    digest)
                ], ct);

                await resp.WriteAsync(Text[..Prefix], ct);

                throw new IOException("connection eaten by a passing gnu");

            }

            var from = Int64.Parse(range["bytes=".Length..].TrimEnd('-'));
            var tail = Text[(Int32) from..];

            if (path == "/corrupt")
            {
                tail    = (Byte[]) tail.Clone();
                tail[0] ^= 0xFF;
            }

            await resp.WriteHeadersAsync([
                (":status",        "206"),
                ("content-type",   "application/octet-stream"),
                ("content-range",  new HTTPContentRange(from, Text.Length - 1, Text.Length).ToHeaderValue()),
                ("content-length", tail.Length.ToString()),
                ("etag",           ETag),
                ("repr-digest",    digest)
            ], ct);

            await resp.WriteAsync(tail, ct);
            await resp.CompleteAsync(null, ct);

        }

        #endregion

    }

}
