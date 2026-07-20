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
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// <see cref="HTTP2ClientPool"/> — a single-origin pool that keeps N warm
    /// connections, spreads requests across the least-loaded one, fails over a
    /// not-processed request to another connection, and self-heals a dead
    /// (GOAWAY'd) connection by reconnecting in the background. Driven against our
    /// real server and a raw multi-connection mock. In-process.
    /// </summary>
    [TestFixture]
    public class ClientPoolTests
    {

        #region (handler)

        private static Task<(List<(String, String)>, Byte[]?)> Ok(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(
                   ([(":status", "200"), ("content-length", "2")], "ok"u8.ToArray()));

        #endregion


        #region WarmPool_HappyPath()

        [Test]
        public async Task WarmPool_HappyPath()
        {
            await using var srv = await TestH2Server.StartAsync(Ok);

            await using var pool = await HTTP2ClientPool.ConnectAsync("127.0.0.1", srv.Port, H2.AcceptAnyServerCert, MaxConnections: 4);

            Assert.That(await H2.EventuallyAsync(() => pool.ConnectionCount == 4), Is.True, "pool warms up to 4 connections");

            var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ =>
                pool.SendRequestAsync("GET", "https", $"127.0.0.1:{srv.Port}", "/")));

            Assert.Multiple(() =>
            {
                Assert.That(results.All(r => r.Status == 200), Is.True,      "40 concurrent requests all 200");
                Assert.That(pool.ConnectionCount,              Is.EqualTo(4), "still 4 connections after the burst");
            });
        }

        #endregion

        #region LoadSpreading()

        [Test]
        public async Task LoadSpreading()
        {
            var usedConns = new ConcurrentDictionary<Int32, Byte>();

            // MCS=2 per connection; hold each stream briefly so several are in flight.
            await using var mock = MockH2Server.Start(2, async (idx, ssl, f, enc) =>
            {
                if (f.Type != HTTP2FrameType.HEADERS) return;
                usedConns[idx] = 1;
                await Task.Delay(300);
                await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
            });

            await using var pool = await HTTP2ClientPool.ConnectAsync("127.0.0.1", mock.Port, H2.AcceptAnyServerCert, MaxConnections: 4);
            await H2.EventuallyAsync(() => pool.ConnectionCount == 4);

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                pool.SendRequestAsync("GET", "https", "127.0.0.1", "/")));

            Assert.Multiple(() =>
            {
                Assert.That(results.All(r => r.Status == 200), Is.True,                  "8 concurrent requests all 200");
                Assert.That(usedConns.Count,                   Is.GreaterThanOrEqualTo(2), "requests spread across >= 2 connections");
            });
        }

        #endregion

        #region SelfHealing_DeadConnectionReplaced()

        [Test]
        public async Task SelfHealing_DeadConnectionReplaced()
        {
            // Every connection serves exactly one request, then GOAWAYs itself
            // (lastStreamId = that stream, NO_ERROR) — draining and dying.
            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type != HTTP2FrameType.HEADERS) return;
                await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
                await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateGoAway(f.StreamId, HTTP2ErrorCode.NO_ERROR, "bye"));
            });

            await using var pool = await HTTP2ClientPool.ConnectAsync("127.0.0.1", mock.Port, H2.AcceptAnyServerCert, MaxConnections: 2);
            await H2.EventuallyAsync(() => pool.ConnectionCount == 2);

            var allOk = true;
            for (var i = 0; i < 6; i++)
            {
                var r = await pool.SendRequestAsync("GET", "https", "127.0.0.1", "/");
                if (r.Status != 200) allOk = false;
            }

            Assert.Multiple(() =>
            {
                Assert.That(allOk, Is.True, "6 sequential requests all 200 despite connections dying");
            });
            Assert.That(await H2.EventuallyAsync(() => pool.Reconnects      >= 3), Is.True, "dead connections reconnected in the background");
            Assert.That(await H2.EventuallyAsync(() => pool.ConnectionCount == 2), Is.True, "pool recovers to full strength (2 connections)");
        }

        #endregion

        #region Failover_NotProcessedRequestRetried()

        [Test]
        public async Task Failover_NotProcessedRequestRetried()
        {
            // The FIRST connection GOAWAYs with lastStreamId=0 (NOT processed);
            // every later connection serves 200. With MaxConnections=1 the pool
            // must reconnect and retry the request there.
            await using var mock = MockH2Server.Start(0, async (idx, ssl, f, enc) =>
            {
                if (f.Type != HTTP2FrameType.HEADERS) return;
                if (idx == 0)
                    await MockH2Server.WriteFrameAsync(ssl, HTTP2Frame.CreateGoAway(0, HTTP2ErrorCode.NO_ERROR, "not processed"));
                else
                    await MockH2Server.Respond200Async(ssl, enc, f.StreamId);
            });

            await using var pool = await HTTP2ClientPool.ConnectAsync("127.0.0.1", mock.Port, H2.AcceptAnyServerCert, MaxConnections: 1);

            var resp = await pool.SendRequestAsync("GET", "https", "127.0.0.1", "/");
            Assert.Multiple(() =>
            {
                Assert.That(resp.Status,     Is.EqualTo(200),            "request succeeds after failing over to a fresh connection");
                Assert.That(pool.Failovers,  Is.GreaterThanOrEqualTo(1), "a failover was recorded");
                Assert.That(pool.Reconnects, Is.GreaterThanOrEqualTo(1), "the dead connection was reconnected");
            });
        }

        #endregion

        #region Disposal_RefusesFurtherRequests()

        [Test]
        public async Task Disposal_RefusesFurtherRequests()
        {
            await using var srv = await TestH2Server.StartAsync(Ok);

            var pool = await HTTP2ClientPool.ConnectAsync("127.0.0.1", srv.Port, H2.AcceptAnyServerCert, MaxConnections: 2);
            await pool.DisposeAsync();

            Assert.That(async () => await pool.SendRequestAsync("GET", "https", $"127.0.0.1:{srv.Port}", "/"),
                        Throws.TypeOf<ObjectDisposedException>(),
                        "disposed pool throws ObjectDisposedException");
        }

        #endregion

    }

}
