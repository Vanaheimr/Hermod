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
    /// Early data and <c>425 (Too Early)</c> (RFC 8470).
    ///
    /// This stack terminates no TLS 1.3 early data of its own — <c>SslStream</c>
    /// exposes no 0-RTT API to offer it, accept it, or detect it — so there is no
    /// replay window on a connection we terminate. What these tests cover is the
    /// other, reachable case the RFC defines: an intermediary that accepted early
    /// data, forwarded the request, and marked it <c>Early-Data: 1</c>. The origin
    /// behind it holds the replay risk without having seen the handshake, and the
    /// field exists so it can decide.
    ///
    /// The default decision is by method safety, not idempotence: replaying a
    /// <c>PUT</c> <i>after</i> a later request changed the resource undoes that
    /// change, so idempotence is not enough.
    /// </summary>
    [TestFixture]
    public class EarlyDataTests
    {

        #region Data / handlers

        /// <summary>Answers 200 and echoes the method, so a processed request is unmistakable.</summary>
        private static Task<(List<(String, String)>, Byte[]?)> Ok(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
        {
            var method = h.FirstOrDefault(header => header.Name == ":method").Value ?? "?";
            return Task.FromResult<(List<(String, String)>, Byte[]?)>((
                       [(":status", "200"), ("content-type", "text/plain")],
                       Encoding.UTF8.GetBytes($"processed {method}")));
        }

        /// <summary>The streaming twin of <see cref="Ok"/>.</summary>
        private static async Task OkStreaming(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {

            while (await req.ReadAsync(ct) is not null) { }

            var method = req.Headers.FirstOrDefault(header => header.Name == ":method").Value ?? "?";

            await resp.WriteHeadersAsync([(":status", "200"), ("content-type", "text/plain")], ct);
            await resp.WriteAsync(Encoding.UTF8.GetBytes($"processed {method}"), ct);
            await resp.CompleteAsync(null, ct);

        }

        private static readonly List<(String Name, String Value)> Flagged = [("early-data", "1")];

        /// <summary>
        /// One request, with the client's own 425 handling out of the way.
        ///
        /// <c>SendRequestAsync</c> repeats a 425 without the <c>Early-Data</c> field,
        /// which is exactly what RFC 8470 asks a client to do — and which means it
        /// hides our own server's refusal behind the retry's 200. Right end to end,
        /// useless for observing the refusal itself, so the server-side tests go one
        /// layer down to <c>StartRequestAsync</c> and read the first answer.
        /// </summary>
        private static async Task<HTTP2Response> RawAsync(HTTP2ClientConnection  Connection,
                                                          HTTPMethod             Method,
                                                          String                 Authority,
                                                          Byte[]?                Body = null)
        {
            var handle = await Connection.StartRequestAsync(Method, "https", Authority, "/", Flagged, Body);
            return await handle.Response;
        }

        #endregion

        #region EarlyData_IsRecognisedAndJudgedBySafety()

        [Test]
        public void EarlyData_IsRecognisedAndJudgedBySafety()
        {

            Assert.Multiple(() => {

                Assert.That(HTTP2EarlyData.IsFlagged([("early-data", "1")]),   Is.True);
                Assert.That(HTTP2EarlyData.IsFlagged([("early-data", " 1 ")]), Is.True,  "whitespace is not significant");
                Assert.That(HTTP2EarlyData.IsFlagged([("early-data", "0")]),   Is.False, "only the value 1 is defined");
                Assert.That(HTTP2EarlyData.IsFlagged([("x", "1")]),            Is.False);
                Assert.That(HTTP2EarlyData.IsFlagged([]),                      Is.False);

                foreach (var safe in new[] { HTTPMethod.GET, HTTPMethod.HEAD, HTTPMethod.OPTIONS, HTTPMethod.TRACE, HTTPMethod.QUERY })
                    Assert.That(HTTP2EarlyData.IsSafeMethod(safe), Is.True, safe.ToString());

                // Idempotent is not the same as safe: replaying a PUT after a later
                // change undoes it, so PUT and DELETE are refused too.
                foreach (var unsafeMethod in new[] { HTTPMethod.POST, HTTPMethod.PUT, HTTPMethod.DELETE, HTTPMethod.PATCH, HTTPMethod.CONNECT, null })
                    Assert.That(HTTP2EarlyData.IsSafeMethod(unsafeMethod), Is.False, unsafeMethod?.ToString() ?? "(null)");

                Assert.That(HTTP2EarlyData.IsSafeToProcess([(":method", "GET". ToString())]),  Is.True);
                Assert.That(HTTP2EarlyData.IsSafeToProcess([(":method", HTTPMethod.POST.ToString())]), Is.False);

            });

        }

        #endregion

        #region Server_RefusesAnUnsafeMethodForwardedFromEarlyData()

        [Test]
        public async Task Server_RefusesAnUnsafeMethodForwardedFromEarlyData()
        {

            await using var srv = await TestH2Server.StartAsync(Ok);

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var safe    = await RawAsync(conn, HTTPMethod.GET,  authority);
            var refused = await RawAsync(conn, HTTPMethod.POST, authority, "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {

                Assert.That(safe.Status,                       Is.EqualTo(200), "a safe method is harmless to replay");
                Assert.That(Encoding.UTF8.GetString(safe.Body), Is.EqualTo("processed GET"));

                Assert.That(refused.Status,                    Is.EqualTo(425), "an unsafe one is not");
                Assert.That(refused.HeaderValue("cache-control"), Is.EqualTo("no-store"),
                            "a refusal must not outlive the reason for it");

            });

        }

        #endregion

        #region Server_IgnoresTheQuestionWhenNothingFlaggedIt()

        // No Early-Data field means no evidence of early data — and since we
        // terminate none ourselves, the field is the only evidence there is. An
        // ordinary POST must be entirely unaffected.
        [Test]
        public async Task Server_IgnoresTheQuestionWhenNothingFlaggedIt()
        {

            await using var srv = await TestH2Server.StartAsync(Ok);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var post = await conn.SendRequestAsync(HTTPMethod.POST, "https", $"localhost:{srv.Port}", "/", Body: "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(post.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(post.Body), Is.EqualTo("processed POST"));
            });

        }

        #endregion

        #region Server_RefusesOnTheStreamingPathToo()

        // The 421 lesson, applied: a streaming handler is dispatched at
        // HEADERS-complete and never passes through the buffered path's checks, so a
        // refusal implemented in only one place silently does not exist in the other.
        [Test]
        public async Task Server_RefusesOnTheStreamingPathToo()
        {

            await using var srv = await TestH2Server.StartAsync(Ok, StreamingHandler: OkStreaming);

            var conn      = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var authority = $"localhost:{srv.Port}";

            var safe    = await RawAsync(conn, HTTPMethod.GET,  authority);
            var refused = await RawAsync(conn, HTTPMethod.POST, authority, "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(safe.Status,    Is.EqualTo(200));
                Assert.That(refused.Status, Is.EqualTo(425));
            });

        }

        #endregion

        #region Server_PolicyCanAcceptTheRiskDeliberately()

        // Section 5.2 states the refusal as a choice, not an obligation. An origin
        // that knows its POSTs are replay-tolerant can say so — and that is also how
        // a deployment restores the old behaviour of ignoring the field.
        [Test]
        public async Task Server_PolicyCanAcceptTheRiskDeliberately()
        {

            await using var srv = await TestH2Server.StartAsync(Ok, AcceptEarlyData: _ => true);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var post = await RawAsync(conn, HTTPMethod.POST, $"localhost:{srv.Port}", "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.That(post.Status, Is.EqualTo(200));

        }

        #endregion

        #region BothHalvesTogether_RecoverWithoutTheCallerNoticing()

        // Our server refuses the flagged POST; our client repeats it without the
        // flag; the caller sees a 200 and never learns any of it happened. That is
        // the whole point of the mechanism, and it is also what made the two
        // server-side tests above need RawAsync — the recovery hides the refusal.
        [Test]
        public async Task BothHalvesTogether_RecoverWithoutTheCallerNoticing()
        {

            await using var srv = await TestH2Server.StartAsync(Ok);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var post = await conn.SendRequestAsync(HTTPMethod.POST, "https", $"localhost:{srv.Port}", "/",
                           Flagged, "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(post.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(post.Body), Is.EqualTo("processed POST"),
                            "and it really was processed, on the second attempt");
            });

        }

        #endregion

        #region Client_RepeatsA425Once_WithoutTheField()

        // The client half. A 425 says the request was not processed and should be
        // repeated once the handshake has completed — which, on our own connection,
        // it long since has. The repeat must drop the Early-Data field: leaving it on
        // would restate the very thing the origin refused.
        [Test]
        public async Task Client_RepeatsA425Once_WithoutTheField()
        {

            var attempts = 0;

            Task<(List<(String, String)>, Byte[]?)> CountingOrigin(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            {

                var seen = Interlocked.Increment(ref attempts);

                // Refuse anything still claiming to be early data — which is exactly
                // what an origin behind an intermediary does.
                return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                           HTTP2EarlyData.IsFlagged(h)
                               ? ([(":status", "425")], null)
                               : ([(":status", "200"), ("x-attempt", seen.ToString())],
                                  Encoding.UTF8.GetBytes("processed")));

            }

            await using var srv = await TestH2Server.StartAsync(CountingOrigin, AcceptEarlyData: _ => true);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var response = await conn.SendRequestAsync(HTTPMethod.POST, "https", $"localhost:{srv.Port}", "/",
                               Flagged, "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.Status,                    Is.EqualTo(200), "the repeat succeeded");
                Assert.That(response.HeaderValue("x-attempt"),  Is.EqualTo("2"));
                Assert.That(attempts,                           Is.EqualTo(2),   "exactly one repeat");
            });

        }

        #endregion

        #region Client_StopsAfterASecondRefusal()

        // A second 425 is an answer, not a hint: it is handed back rather than
        // retried into a loop.
        [Test]
        public async Task Client_StopsAfterASecondRefusal()
        {

            var attempts = 0;

            Task<(List<(String, String)>, Byte[]?)> AlwaysTooEarly(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            {
                Interlocked.Increment(ref attempts);
                return Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "425")], null));
            }

            await using var srv = await TestH2Server.StartAsync(AlwaysTooEarly, AcceptEarlyData: _ => true);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            var response = await conn.SendRequestAsync(HTTPMethod.POST, "https", $"localhost:{srv.Port}", "/",
                               Flagged, "payload"u8.ToArray());

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.Status, Is.EqualTo(425), "surfaced to the caller");
                Assert.That(attempts,        Is.EqualTo(2),   "and not retried again");
            });

        }

        #endregion

    }

}
