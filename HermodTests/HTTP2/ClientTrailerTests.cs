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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Request trailers from the client (RFC 9113, Section 8.1) — the last
    /// asymmetry in the trailer story. The client could already *receive* them on
    /// both the buffered and the streaming path; it could not send any, because
    /// <c>CompleteRequestAsync</c> ended the request with an empty END_STREAM DATA
    /// frame and offered no way to append a trailing HEADERS block.
    ///
    /// Verified in both directions of the mirror-role rule: against our own server
    /// (which surfaces them on <c>IHTTP2RequestStream.Trailers</c>) and against
    /// Kestrel, whose <c>Request.GetTrailer</c> is an independent reading of the
    /// same frames — an agreement between our sender and our receiver would prove
    /// nothing on its own.
    /// </summary>
    [TestFixture]
    public class ClientTrailerTests
    {

        #region Data / handlers

        /// <summary>
        /// Reads the request body to the end and answers with whatever trailers
        /// arrived, flattened into the response body — plus its own response
        /// trailers, so one exchange exercises both directions at once.
        /// </summary>
        private static async Task EchoTrailers(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {

            var body = new MemoryStream();

            Byte[]? chunk;
            while ((chunk = await req.ReadAsync(ct)) is not null)
                await body.WriteAsync(chunk, ct);

            // Only meaningful once ReadAsync has returned null.
            var seen = Encoding.UTF8.GetBytes(
                           String.Join("\n", req.Trailers.Select(t => $"{t.Name}={t.Value}")));

            await resp.WriteHeadersAsync([
                (":status",         "200"),
                ("content-type",    "text/plain"),
                ("x-body-length",   body.Length.ToString())
            ], ct);

            await resp.WriteAsync(seen, ct);
            await resp.CompleteAsync([("x-response-trailer", "from-the-server")], ct);

        }

        private static Task<(List<(String, String)>, Byte[]?)> NotUsed(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "500")], null));

        /// <summary>Read the whole exchange: head, body to EOF, then the response trailers.</summary>
        private static async Task<(HTTP2ResponseHead Head, String Body, List<(String Name, String Value)> Trailers)> DrainAsync(HTTP2ClientStream Stream)
        {

            var head = await Stream.GetResponseAsync();
            var body = new MemoryStream();

            Byte[]? chunk;
            while ((chunk = await Stream.ReadAsync()) is not null)
                await body.WriteAsync(chunk);

            return (head, Encoding.UTF8.GetString(body.ToArray()), await Stream.GetTrailersAsync());

        }

        #endregion


        #region OurServer_ReceivesRequestTrailers()

        [Test]
        public async Task OurServer_ReceivesRequestTrailers()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: EchoTrailers);

            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var stream = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");

            await stream.WriteAsync("hello "u8.ToArray());
            await stream.WriteAsync("world"u8.ToArray());

            await stream.CompleteRequestAsync([
                ("grpc-status",  "0"),
                ("x-checksum",   "deadbeef")
            ]);

            var (head, body, responseTrailers) = await DrainAsync(stream);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(head.Status,                        Is.EqualTo(200));
                Assert.That(head.HeaderValue("x-body-length"),  Is.EqualTo("11"), "the body arrived intact alongside them");
                Assert.That(body,                               Is.EqualTo("grpc-status=0\nx-checksum=deadbeef"),
                            "both trailers, in the order sent");
                Assert.That(responseTrailers.Select(t => t.Name), Does.Contain("x-response-trailer"),
                            "and the response direction still works on the same stream");
            });

        }

        #endregion

        #region OurServer_ReceivesTrailersWithoutABody()

        // RFC 9113 §8.1 puts "zero or more" DATA frames between the header section
        // and the trailers, so a trailer-only request is legal — and is exactly the
        // shape of a client-streaming call that ends up having nothing to send.
        [Test]
        public async Task OurServer_ReceivesTrailersWithoutABody()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: EchoTrailers);

            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var stream = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");

            await stream.CompleteRequestAsync([("x-only", "trailer")]);

            var (head, body, _) = await DrainAsync(stream);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(head.Status,                       Is.EqualTo(200));
                Assert.That(head.HeaderValue("x-body-length"), Is.EqualTo("0"));
                Assert.That(body,                              Is.EqualTo("x-only=trailer"));
            });

        }

        #endregion

        #region NoTrailers_StillEndsTheRequestTheOldWay()

        // The default path must be untouched: no trailers means the same empty
        // END_STREAM DATA frame as before, which is what every existing streaming
        // caller (gRPC, DownloadAsync) relies on.
        [Test]
        public async Task NoTrailers_StillEndsTheRequestTheOldWay()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: EchoTrailers);

            var conn   = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var stream = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");

            await stream.WriteAsync("payload"u8.ToArray());
            await stream.CompleteRequestAsync();

            var (head, body, _) = await DrainAsync(stream);

            // An empty list is the same statement as no list at all.
            var empty  = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");
            await empty.WriteAsync("payload"u8.ToArray());
            await empty.CompleteRequestAsync([]);

            var (emptyHead, emptyBody, _) = await DrainAsync(empty);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(head.Status,                       Is.EqualTo(200));
                Assert.That(head.HeaderValue("x-body-length"), Is.EqualTo("7"));
                Assert.That(body,                              Is.Empty, "no trailers seen");
                Assert.That(emptyHead.HeaderValue("x-body-length"), Is.EqualTo("7"));
                Assert.That(emptyBody,                         Is.Empty);
            });

        }

        #endregion

        #region InvalidTrailers_NeverReachTheWire()

        // A pseudo-header or an uppercase name in a trailer block earns a stream
        // reset from the peer. Catching it locally turns a confusing remote failure
        // into an argument error at the call that caused it — and leaves the
        // connection usable, which the assertions after it prove.
        [Test]
        public async Task InvalidTrailers_NeverReachTheWire()
        {

            await using var srv = await TestH2Server.StartAsync(NotUsed, StreamingHandler: EchoTrailers);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var pseudo = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");
            Assert.That(async () => await pseudo.CompleteRequestAsync([(":status", "200")]),
                        Throws.InstanceOf<HTTP2StreamException>(), "pseudo-header fields belong to the leading section");

            var upper = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");
            Assert.That(async () => await upper.CompleteRequestAsync([("X-Upper", "no")]),
                        Throws.InstanceOf<HTTP2StreamException>(), "field names are lowercase on the wire");

            // Both requests were abandoned without END_STREAM; the connection itself
            // must be unharmed.
            var ok = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{srv.Port}", "/echo");
            await ok.CompleteRequestAsync([("x-fine", "yes")]);
            var (head, body, _) = await DrainAsync(ok);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(head.Status, Is.EqualTo(200));
                Assert.That(body,        Is.EqualTo("x-fine=yes"));
            });

        }

        #endregion

        #region Kestrel_ReadsOurRequestTrailers()

        // The mirror-role check: a production HTTP/2 server decoding the trailing
        // HEADERS block our client produced. Kestrel only surfaces trailers after
        // the request body has been read to the end, which is also the RFC's own
        // ordering.
        [Test]
        public async Task Kestrel_ReadsOurRequestTrailers()
        {

            await using var kestrel = await KestrelH2Server.StartAsync(app =>
                app.MapPost("/trailers", async (HttpContext ctx) =>
                {

                    using var body = new MemoryStream();
                    await ctx.Request.Body.CopyToAsync(body);

                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.WriteAsync(
                        $"supported={ctx.Request.SupportsTrailers()};" +
                        $"status={ctx.Request.GetTrailer("grpc-status")};" +
                        $"checksum={ctx.Request.GetTrailer("x-checksum")};" +
                        $"body={body.Length}");

                }));

            var conn   = await HTTP2Client.ConnectAsync("localhost", kestrel.Port, H2.AcceptAnyServerCert);
            var stream = await conn.StartStreamingRequestAsync("POST", "https", $"localhost:{kestrel.Port}", "/trailers");

            await stream.WriteAsync("twelve bytes"u8.ToArray());
            await stream.CompleteRequestAsync([
                ("grpc-status", "0"),
                ("x-checksum",  "deadbeef")
            ]);

            var (head, body, _) = await DrainAsync(stream);

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(head.Status, Is.EqualTo(200));
                Assert.That(body,        Is.EqualTo("supported=True;status=0;checksum=deadbeef;body=12"));
            });

        }

        #endregion

        #region Trailers_ValidateTheSameRulesInBothDirections()

        // The rules moved to Core so the server's response trailers and the client's
        // request trailers cannot drift apart. Pinning them directly is cheaper than
        // discovering the drift through two separate integration tests.
        [Test]
        public void Trailers_ValidateTheSameRulesInBothDirections()
        {

            Assert.Multiple(() =>
            {

                Assert.DoesNotThrow(() => HTTP2Trailers.Validate(1, [("x-ok", "1"), ("another-one", "")]));
                Assert.DoesNotThrow(() => HTTP2Trailers.Validate(1, []));

                foreach (var bad in new (String Name, String Value)[] {
                             (":status",   "200"),
                             (":path",     "/"),
                             ("X-Upper",   "no"),
                             ("mixedCase", "no"),
                             ("",          "no")
                         })
                    Assert.That(() => HTTP2Trailers.Validate(7, [bad]),
                                Throws.InstanceOf<HTTP2StreamException>(), bad.Name);

            });

        }

        #endregion

    }

}
