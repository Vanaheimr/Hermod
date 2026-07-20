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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Client robustness against a misbehaving raw mock server: REFUSED_STREAM
    /// auto-retry, MAX_CONCURRENT_STREAMS gating, GOAWAY retry-safe failure, and
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
            var resp = await conn.SendRequestAsync("GET", "https", "localhost", "/");
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

            Assert.That(async () => await conn.SendRequestAsync("GET", "https", "localhost", "/"),
                        Throws.TypeOf<HTTP2RequestNotProcessedException>(),
                        "persistent refusal throws HTTP2RequestNotProcessedException");
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
                conn.SendRequestAsync("GET", "https", "localhost", "/a"),
                conn.SendRequestAsync("GET", "https", "localhost", "/b"),
                conn.SendRequestAsync("GET", "https", "localhost", "/c"));

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
            Assert.That(async () => await conn.SendRequestAsync("GET", "https", "localhost", "/"),
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
            try { await conn.SendRequestAsync("GET", "https", "localhost", "/"); }
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
            var resp = await conn.SendRequestAsync("GET", "https", "localhost", "/");
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
