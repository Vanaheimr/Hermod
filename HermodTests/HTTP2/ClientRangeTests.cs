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
    /// Client-side conditional requests and <c>Range</c> (RFC 9110, Sections 13 and
    /// 14) — the half of the semantics that had lived only on the server.
    ///
    /// <c>DownloadAsync</c> resumes an interrupted transfer, guarded by
    /// <c>If-Range</c> so the *server* decides whether the two halves belong to the
    /// same representation. The interruption is produced by a streaming handler that
    /// writes part of the body and then faults, which is the real client code path
    /// rather than a simulated one: the buffered response API discards a partial
    /// body, so the download had to be built on the streaming path.
    ///
    /// The shared primitives lifted out of <c>HTTPSemantics</c> for this —
    /// <c>HTTPContentRange</c> and <c>HTTPValidators</c> — are unit-tested here too,
    /// since both roles now depend on them.
    /// </summary>
    [TestFixture]
    public class ClientRangeTests
    {

        #region Data / servers

        private const  Int32  Total  = 4096;
        private const  Int32  Prefix = 1024;
        private const  String ETag   = "\"strong-etag-v1\"";

        private static readonly Byte[] Body = CreateBody();

        private static Byte[] CreateBody()
        {
            var body = new Byte[Total];
            for (var i = 0; i < body.Length; i++)
                body[i] = (Byte) (i % 251);          // deterministic, and not all-equal
            return body;
        }

        private static Task<(List<(String, String)>, Byte[]?)> Unused(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "500")], null));

        /// <summary>
        /// A deliberately unreliable origin. Without a <c>Range</c> it sends the
        /// response head plus the first <see cref="Prefix"/> bytes and then faults,
        /// which resets the stream mid-body. With a <c>Range</c> it behaves per path:
        ///
        ///   /resume       — serves the requested tail as a 206 (the happy resume)
        ///   /changed      — answers 200 with the whole body, as a server does when
        ///                   If-Range fails, forcing the client to start over
        ///   /novalidator  — offers neither ETag nor Last-Modified, so no resume is
        ///                   permissible at all
        /// </summary>
        private static async Task Flaky(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {

            var path  = req.Headers.First(h => h.Name == ":path").Value;
            var range = req.Headers.FirstOrDefault(h => h.Name == "range").Value;

            if (range is null)
            {

                var head = new List<(String, String)> {
                    (":status",       "200"),
                    ("content-type",  "application/octet-stream"),
                    ("content-length", Total.ToString()),
                    ("accept-ranges", "bytes")
                };

                if (path != "/novalidator")
                    head.Add(("etag", ETag));

                await resp.WriteHeadersAsync(head, ct);
                await resp.WriteAsync(Body[..Prefix], ct);

                // Fault mid-body: the handler's exception resets the stream, which is
                // what the client sees as an interrupted transfer.
                throw new IOException("connection eaten by a passing gnu");

            }

            if (path == "/changed")
            {
                await resp.WriteHeadersAsync([
                    (":status",        "200"),
                    ("content-type",   "application/octet-stream"),
                    ("content-length", Total.ToString()),
                    ("etag",           "\"strong-etag-v2\"")
                ], ct);
                await resp.WriteAsync(Body, ct);
                await resp.CompleteAsync(null, ct);
                return;
            }

            // "bytes=N-" — the open-ended form a resume uses.
            var from = Int64.Parse(range["bytes=".Length..].TrimEnd('-'));
            var tail = Body[(Int32) from..];

            await resp.WriteHeadersAsync([
                (":status",        "206"),
                ("content-type",   "application/octet-stream"),
                ("content-range",  new HTTPContentRange(from, Total - 1, Total).ToHeaderValue()),
                ("content-length", tail.Length.ToString()),
                ("etag",           ETag)
            ], ct);

            await resp.WriteAsync(tail, ct);
            await resp.CompleteAsync(null, ct);

        }

        #endregion

        #region ContentRange_ParsesAndFormats()

        [Test]
        public void ContentRange_ParsesAndFormats()
        {

            Assert.Multiple(() =>
            {
                Assert.That(HTTPContentRange.TryParse("bytes 500-999/8000", out var ok), Is.True);
                Assert.That(ok!.Start,          Is.EqualTo(500L));
                Assert.That(ok!.End,            Is.EqualTo(999L));
                Assert.That(ok!.CompleteLength, Is.EqualTo(8000L));
                Assert.That(ok!.Length,         Is.EqualTo(500L),  "inclusive end");
                Assert.That(ok!.IsUnsatisfied,  Is.False);
                Assert.That(ok!.ToHeaderValue(), Is.EqualTo("bytes 500-999/8000"), "round-trips");

                // The 416 form: no range, only the current length.
                Assert.That(HTTPContentRange.TryParse("bytes */8000", out var unsat), Is.True);
                Assert.That(unsat!.IsUnsatisfied,  Is.True);
                Assert.That(unsat!.CompleteLength, Is.EqualTo(8000L));
                Assert.That(unsat!.Length,         Is.Null);

                // An unknown complete length is legal.
                Assert.That(HTTPContentRange.TryParse("bytes 0-99/*", out var star), Is.True);
                Assert.That(star!.CompleteLength, Is.Null);

                Assert.That(HTTPContentRange.RequestFrom(1024), Is.EqualTo("bytes=1024-"));
            });

        }

        #endregion

        #region ContentRange_RejectsNonsense()

        // A client that cannot read where its bytes belong must not guess: every
        // one of these has to fail outright rather than half-parse.
        [Test]
        public void ContentRange_RejectsNonsense()
        {
            Assert.Multiple(() =>
            {
                foreach (var bad in new[] {
                             "items 1-2/3",          // unit we don't understand
                             "bytes 500-999",        // no complete length
                             "bytes 999-500/8000",   // end before start
                             "bytes -5-9/8000",      // negative
                             "bytes 0-8000/8000",    // end past the complete length
                             "bytes abc-def/8000",
                             "bytes 500-999/abc",
                             "nonsense"
                         })
                    Assert.That(HTTPContentRange.TryParse(bad, out _), Is.False, bad);
            });
        }

        #endregion

        #region Validators_CompareByStrength()

        [Test]
        public void Validators_CompareByStrength()
        {
            Assert.Multiple(() =>
            {
                Assert.That(HTTPValidators.StrongMatch("\"a\"",   "\"a\""),   Is.True);
                Assert.That(HTTPValidators.StrongMatch("W/\"a\"", "\"a\""),   Is.False, "a weak tag never matches strongly");
                Assert.That(HTTPValidators.StrongMatch("W/\"a\"", "W/\"a\""), Is.False);
                Assert.That(HTTPValidators.StrongMatch("\"a\"",   null),      Is.False);

                Assert.That(HTTPValidators.WeakMatch  ("W/\"a\"", "\"a\""),   Is.True,  "weak comparison ignores the flag");
                Assert.That(HTTPValidators.WeakMatch  ("\"a\"",   "\"b\""),   Is.False);

                Assert.That(HTTPValidators.SplitETag("W/\"x\""),              Is.EqualTo(("\"x\"", true)));

                // HTTP-date has no sub-second precision, so a stored timestamp with a
                // fractional part must still compare equal to what went on the wire.
                var precise = new DateTimeOffset(2026, 7, 25, 12, 0, 0, 500, TimeSpan.Zero);
                Assert.That(HTTPValidators.TryParseDate(HTTPValidators.FormatDate(precise), out var round), Is.True);
                Assert.That(HTTPValidators.SameInstant(precise, round), Is.True, "equal to the second");

                Assert.That(HTTPValidators.ParseETagList("\"a\", W/\"b\", garbage").Select(e => e.Tag),
                            Is.EqualTo(new[] { "\"a\"", "\"b\"" }), "unquoted junk is skipped, not guessed at");
            });
        }

        #endregion

        #region Download_Uninterrupted()

        [Test]
        public async Task Download_Uninterrupted()
        {

            await using var srv = await TestH2Server.StartAsync(
                                      Hermod.HTTP2.HTTPSemantics.Wrap((path, headers, ct) => Task.FromResult<HTTPResource?>(
                                          new HTTPResource { Body = Body, ContentType = "application/octet-stream" })));

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            using var destination = new MemoryStream();
            var result = await conn.DownloadAsync(URIScheme.https, $"localhost:{srv.Port}", "/file", destination);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status,           Is.EqualTo(200));
                Assert.That(result.BytesWritten,     Is.EqualTo(Total));
                Assert.That(destination.ToArray(),   Is.EqualTo(Body));
                Assert.That(result.WasUninterrupted, Is.True);
                Assert.That(result.Resumes,          Is.EqualTo(0));
            });

        }

        #endregion

        #region Download_ResumesAfterInterruption()

        // The point of the exercise: the transfer dies after 1024 of 4096 bytes and
        // is continued with "Range: bytes=1024-" + "If-Range: <strong etag>", and the
        // spliced result is byte-identical to the original.
        [Test]
        public async Task Download_ResumesAfterInterruption()
        {

            await using var srv = await TestH2Server.StartAsync(Unused, StreamingHandler: Flaky);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            using var destination = new MemoryStream();
            var result = await conn.DownloadAsync(URIScheme.https, $"localhost:{srv.Port}", "/resume", destination);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.BytesWritten,   Is.EqualTo(Total),  "everything arrived");
                Assert.That(destination.ToArray(), Is.EqualTo(Body),   "and the seam is exact");
                Assert.That(result.Attempts,       Is.EqualTo(2));
                Assert.That(result.Resumes,        Is.EqualTo(1));
                Assert.That(result.Restarts,       Is.EqualTo(0));
                Assert.That(result.Status,         Is.EqualTo(206),    "finished by a partial response");
                Assert.That(result.Validator,      Is.EqualTo(ETag),   "guarded by the strong validator");
            });

        }

        #endregion

        #region Download_RestartsWhenTheRepresentationChanged()

        // A server that answers a conditional Range with 200 is saying "If-Range
        // failed, here is the whole thing" — the bytes already held belong to a
        // different representation and must be discarded, not spliced.
        [Test]
        public async Task Download_RestartsWhenTheRepresentationChanged()
        {

            await using var srv = await TestH2Server.StartAsync(Unused, StreamingHandler: Flaky);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            using var destination = new MemoryStream();
            var result = await conn.DownloadAsync(URIScheme.https, $"localhost:{srv.Port}", "/changed", destination);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.BytesWritten,   Is.EqualTo(Total), "no double-counted prefix");
                Assert.That(destination.Length,    Is.EqualTo(Total), "the stale prefix was truncated away");
                Assert.That(destination.ToArray(), Is.EqualTo(Body));
                Assert.That(result.Restarts,       Is.EqualTo(1));
                Assert.That(result.Status,         Is.EqualTo(200));
            });

        }

        #endregion

        #region Download_WithoutAValidator_DoesNotResume()

        // RFC 9110 §13.1.5: no validator, no safe resume. The failure must surface —
        // quietly returning 1024 of 4096 bytes would be the worst possible outcome.
        [Test]
        public async Task Download_WithoutAValidator_DoesNotResume()
        {

            await using var srv = await TestH2Server.StartAsync(Unused, StreamingHandler: Flaky);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            using var destination = new MemoryStream();

            Assert.That(async () => await conn.DownloadAsync(URIScheme.https, $"localhost:{srv.Port}", "/novalidator", destination),
                        Throws.Exception, "the interruption is not swallowed");

            await conn.CloseAsync();

        }

        #endregion

        #region Download_NonSeekableDestination_CannotRestart()

        // A restart means rewinding. If the destination cannot rewind, that has to be
        // said plainly rather than producing a file with a stale prefix glued on.
        [Test]
        public async Task Download_NonSeekableDestination_CannotRestart()
        {

            await using var srv = await TestH2Server.StartAsync(Unused, StreamingHandler: Flaky);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            using var destination = new ForwardOnlyStream();

            Assert.That(async () => await conn.DownloadAsync(URIScheme.https, $"localhost:{srv.Port}", "/changed", destination),
                        Throws.InstanceOf<InvalidOperationException>());

            await conn.CloseAsync();

        }

        /// <summary>A stream that accepts writes but cannot seek — a socket or a pipe.</summary>
        private sealed class ForwardOnlyStream : Stream
        {
            private Int64 length;
            public override Boolean CanRead  => false;
            public override Boolean CanSeek  => false;
            public override Boolean CanWrite => true;
            public override Int64   Length   => length;
            public override Int64   Position { get => length; set => throw new NotSupportedException(); }
            public override void Write(Byte[] buffer, Int32 offset, Int32 count) => length += count;
            public override void Flush() { }
            public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
            public override Int64 Seek(Int64 offset, SeekOrigin origin)          => throw new NotSupportedException();
            public override void SetLength(Int64 value)                          => throw new NotSupportedException();
        }

        #endregion

        #region ConditionalGet_RoundTripsTo304()

        // The other half of §13: a client-built precondition, evaluated by our
        // server. The ETag and Last-Modified forms both have to work, and a
        // *stale* validator must still produce the full representation.
        [Test]
        public async Task ConditionalGet_RoundTripsTo304()
        {

            var lastModified = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

            await using var srv = await TestH2Server.StartAsync(
                                      Hermod.HTTP2.HTTPSemantics.Wrap((path, headers, ct) => Task.FromResult<HTTPResource?>(
                                          new HTTPResource {
                                              Body         = Body,
                                              ContentType  = "application/octet-stream",
                                              LastModified = lastModified
                                          })));

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var full = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/file");
            var etag = full.HeaderValue("etag")!;

            var byETag = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/file",
                             [("if-none-match", etag)]);

            var byDate = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/file",
                             [("if-modified-since", HTTPValidators.FormatDate(lastModified))]);

            var stale  = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, authority, "/file",
                             [("if-none-match", "\"something-else\"")]);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(full.Status,        Is.EqualTo(200));
                Assert.That(byETag.Status,      Is.EqualTo(304), "matching ETag -> 304");
                Assert.That(byETag.Body,        Is.Empty,        "and no body");
                Assert.That(byDate.Status,      Is.EqualTo(304), "matching Last-Modified -> 304");
                Assert.That(stale.Status,       Is.EqualTo(200), "stale validator -> full representation");
                Assert.That(stale.Body.Length,  Is.EqualTo(Total));

                Assert.That(HTTPValidators.StrongMatch(etag, byETag.HeaderValue("etag")), Is.True,
                            "the 304 echoes the same validator");
            });

        }

        #endregion

    }

}
