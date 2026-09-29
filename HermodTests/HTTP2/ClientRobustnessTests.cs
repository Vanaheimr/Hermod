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

using System.Net.Security;
using System.Text;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Client robustness against a misbehaving raw mock server: REFUSED_STREAM
    /// auto-retry (also when the refusal overtakes the client's half-close),
    /// MAX_CONCURRENT_STREAMS gating, GOAWAY retry-safe failure, and
    /// keepalive-based dead-connection detection. In-process.
    /// </summary>
    [TestFixture]
    public class ClientRobustnessTests
    {

        #region RefusedStream_AutoRetry()

        [Test]
        public async Task RefusedStream_AutoRetry()
        {
            var refusedOnce     = false;
            var refusedStreamId = 0u;
            var servedStreamId  = 0u;

            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type != HTTP2FrameType.HEADERS) return;
                if (!refusedOnce)
                {
                    refusedOnce     = true;
                    refusedStreamId = f.StreamId;
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateRstStream(f.StreamId, HTTP2ErrorCode.REFUSED_STREAM));
                }
                else
                {
                    servedStreamId = f.StreamId;
                    await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
                }
            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/");
            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,       Is.EqualTo(200), "request succeeds despite first-stream refusal");
                Assert.That(refusedStreamId,   Is.EqualTo(1u),  "server refused stream 1");
                Assert.That(servedStreamId,    Is.EqualTo(3u),  "retry served on stream 3");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region RefusedStream_PastRetryBudget_NotProcessed()

        [Test]
        public async Task RefusedStream_PastRetryBudget_NotProcessed()
        {
            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type == HTTP2FrameType.HEADERS)
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateRstStream(f.StreamId, HTTP2ErrorCode.REFUSED_STREAM));
            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert,
                Options: new HTTP2ClientOptions { MaxRefusedStreamRetries = 2 });

            Assert.That(async () => await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/"),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "persistent refusal throws HTTP2RequestNotProcessedException");
            await conn.CloseAsync();
        }

        #endregion

        #region RefusedStream_HandledBeforeHalfClose_NotProcessed(MaxRetries)

        /// <summary>
        /// Once a request's HEADERS with END_STREAM are on the wire, nothing orders
        /// the client's half-close of that stream against the read loop handling
        /// the server's refusal of it. The refusal usually comes second; under load
        /// it now and then came first, and RefusedStream_PastRetryBudget_NotProcessed
        /// failed with "Cannot close local on stream 1 in state Closed". Here the
        /// transport holds the client inside every HEADERS write until the refusal
        /// has been handled, so the refusal always comes first.
        /// </summary>
        [TestCase(0)]
        [TestCase(2)]
        public async Task RefusedStream_HandledBeforeHalfClose_NotProcessed(Int32 MaxRetries)
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => true);

            var conn     = await transport.ConnectAsync(new HTTP2ClientOptions { MaxRefusedStreamRetries = MaxRetries });
            var request  = conn.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");
            var refused  = new List<UInt32>();

            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                var headers = await transport.NextHeadersAsync();
                refused.Add(headers.StreamId);
                await transport.RefuseAsync(headers.StreamId);
                transport.Release(headers.StreamId);
            }

            Assert.That(async () => await request.WaitAsync(HoldingH2Transport.StepTimeout),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "a refusal handled before the half-close still throws HTTP2RequestNotProcessedException");

            Assert.That(refused, Is.EqualTo(Enumerable.Range(0, MaxRetries + 1).Select(attempt => (UInt32) (2 * attempt + 1))),
                        "one fresh stream per attempt, and no attempt past the retry budget");

            await conn.CloseAsync();

        }

        #endregion

        #region RefusedStream_HandledBeforeHalfClose_RetrySucceeds()

        /// <summary>
        /// The same order, with the retry served: a refusal that overtook the
        /// half-close is retried like any other, and the retry's response is the
        /// request's.
        /// </summary>
        [Test]
        public async Task RefusedStream_HandledBeforeHalfClose_RetrySucceeds()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId == 1);

            var conn     = await transport.ConnectAsync(new HTTP2ClientOptions { MaxRefusedStreamRetries = 2 });
            var request  = conn.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/");

            var first    = await transport.NextHeadersAsync();
            await transport.RefuseAsync(first.StreamId);
            transport.Release(first.StreamId);

            var retry    = await transport.NextHeadersAsync();
            await transport.RespondAsync(retry.StreamId, "ok");

            var response = await request.WaitAsync(HoldingH2Transport.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(first.StreamId,                          Is.EqualTo(1u),   "server refused stream 1");
                Assert.That(retry.StreamId,                          Is.EqualTo(3u),   "retry served on stream 3");
                Assert.That(response.Status,                         Is.EqualTo(200),  "request succeeds despite the refusal");
                Assert.That(Encoding.ASCII.GetString(response.Body), Is.EqualTo("ok"), "response body");
            });

            await conn.CloseAsync();

        }

        #endregion

        #region RefusedStream_HandledBeforeRetryHalfClose_NotProcessed()

        /// <summary>
        /// A retry meets the same race: the read loop issues it, and its HEADERS
        /// can be refused before it half-closes them — whereupon its state error
        /// went to the request in place of the refusals that followed. The first
        /// attempt here is half-closed before its refusal is sent, so only the
        /// retries meet the race.
        /// </summary>
        [Test]
        public async Task RefusedStream_HandledBeforeRetryHalfClose_NotProcessed()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => streamId > 1);

            var conn     = await transport.ConnectAsync(new HTTP2ClientOptions { MaxRefusedStreamRetries = 2 });

            // StartRequestAsync returns once the first attempt is on the wire and
            // half-closed, so its refusal strictly follows the half-close.
            var handle   = await conn.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", "/").
                                      WaitAsync(HoldingH2Transport.StepTimeout);

            var first    = await transport.NextHeadersAsync();
            await transport.RefuseAsync(first.StreamId);

            var retries  = new List<UInt32>();

            for (var retry = 1; retry <= 2; retry++)
            {
                var headers = await transport.NextHeadersAsync();
                retries.Add(headers.StreamId);
                await transport.RefuseAsync(headers.StreamId);
                transport.Release(headers.StreamId);
            }

            Assert.That(async () => await handle.Response.WaitAsync(HoldingH2Transport.StepTimeout),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "a retry's refusal handled before its half-close still throws HTTP2RequestNotProcessedException");

            Assert.That(retries, Is.EqualTo(new[] { 3u, 5u }), "both retries were refused");

            await conn.CloseAsync();

        }

        #endregion

        #region MaxConcurrentStreams_Gating()

        [Test]
        public async Task MaxConcurrentStreams_Gating()
        {
            var openLock = new Object();
            var openNow  = 0;
            var maxOpen  = 0;

            await using var mock = MockH2Server.Start(1, async (idx, ssl, f, enc) =>
            {
                if (f.Type != HTTP2FrameType.HEADERS) return;
                lock (openLock) { openNow++; maxOpen = Math.Max(maxOpen, openNow); }
                await Task.Delay(200);            // hold the stream open a while
                await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
                lock (openLock) { openNow--; }
            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);
            var all  = await Task.WhenAll(
                conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/a"),
                conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/b"),
                conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/c"));

            Assert.Multiple(() =>
            {
                Assert.That(all.All(r => r.Status == 200), Is.True,      "all 3 concurrent requests complete 200");
                Assert.That(maxOpen,                       Is.EqualTo(1), "client never exceeded MAX_CONCURRENT_STREAMS=1");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region Goaway_MarksUnprocessed_RetrySafe()

        [Test]
        public async Task Goaway_MarksUnprocessed_RetrySafe()
        {
            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type == HTTP2FrameType.HEADERS)
                    // lastStreamId=0 => this stream (id 1) was NOT processed.
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateGoAway(0, HTTP2ErrorCode.NO_ERROR, "go away"));
            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);
            Assert.That(async () => await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/"),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "GOAWAY-abandoned request throws HTTP2RequestNotProcessedException");
            await conn.CloseAsync();
        }

        #endregion

        #region Keepalive_DetectsSilentConnection()

        [Test]
        public async Task Keepalive_DetectsSilentConnection()
        {
            // The mock accepts the request's HEADERS but never responds, and never
            // ACKs a PING (it ignores everything after the handshake).
            await using var mock = MockH2Server.Start(0, (idx, ssl, f, enc) => Task.CompletedTask);

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert,
                Options: new HTTP2ClientOptions
                {
                    KeepAliveInterval = TimeSpan.FromMilliseconds(400),
                    KeepAliveTimeout  = TimeSpan.FromMilliseconds(600)
                });

            var sw = Stopwatch.StartNew();
            Exception? caught = null;
            try { await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/"); }
            catch (Exception ex) { caught = ex; }
            sw.Stop();

            Assert.Multiple(() =>
            {
                Assert.That(caught,      Is.Not.Null,                       "request to a silent server fails (not hangs)");
                Assert.That(sw.Elapsed,  Is.LessThan(TimeSpan.FromSeconds(5)), "keepalive detects it promptly");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region Baseline_NormalRequest()

        [Test]
        public async Task Baseline_NormalRequest()
        {
            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type == HTTP2FrameType.HEADERS)
                    await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
            });

            var conn = await HTTP2Client.ConnectAsync("localhost", mock.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync(HTTPMethod.GET, URIScheme.https, "localhost", "/");
            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.ASCII.GetString(resp.Body), Is.EqualTo("ok"), "response body");
            });
            await conn.CloseAsync();
        }

        #endregion

    }

}
