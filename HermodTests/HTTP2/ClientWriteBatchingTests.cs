/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Reflection;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The client's writes of request HEADERS, with the frame writer's buffer in
    /// place. A request start used to hold the lock that orders the starts
    /// across the write and the flush of its HEADERS, and that write, a TLS
    /// record and a send, was nearly all of its time under the lock: h2bench put
    /// one connection at about half of what .NET's HttpClient sends. Now the
    /// starts in line for that lock share one batch, and the last to leave the
    /// line flushes for all of them — but a start ends the batch before it waits
    /// for a stream slot, which only the server's answer to a request still in
    /// the buffer could free.
    ///
    /// Each test holds the client in the write of its answer to a PING, with the
    /// connection's write lock and without a stream slot, while the requests
    /// line up behind it.
    /// </summary>
    [TestFixture]
    public class ClientWriteBatchingTests
    {

        #region (helpers)

        /// <summary>
        /// Connect, then send a PING, and return once the client is held in the
        /// write of its answer, until <c>Release(0)</c>. With
        /// <paramref name="MaxConcurrentStreams"/>, the server limits the
        /// streams first.
        /// </summary>
        private static async Task<HTTP2ClientConnection> ConnectHeldAsync(HoldingH2Transport  Transport,
                                                                          UInt32?             MaxConcurrentStreams = null)
        {

            var connection = await Transport.ConnectAsync(new HTTP2ClientOptions());

            if (MaxConcurrentStreams is UInt32 limit)
                await Transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.MAX_CONCURRENT_STREAMS, limit)));

            // Not SendAsync: the read loop stays in the answer's write, and
            // reads nothing more until the release.
            await Transport.WriteAsync(HTTP2Frame.CreatePing(new Byte[8]));

            using var timeout = new CancellationTokenSource(HoldingH2Transport.StepTimeout);

            while (true)
            {

                var frame = await Transport.NextFrameAsync(timeout.Token)
                                ?? throw new EndOfStreamException("The client closed before it answered the PING");

                if (frame.Type == HTTP2FrameType.PING && frame.IsAck)
                    return connection;

            }

        }

        /// <summary>
        /// Start a GET, which sends nothing but its HEADERS.
        /// </summary>
        private static Task<HTTP2RequestHandle> StartAsync(HTTP2ClientConnection  Connection,
                                                           String                 Path,
                                                           CancellationToken      CancellationToken = default)

            => Connection.StartRequestAsync(HTTPMethod.GET, URIScheme.http, "localhost", Path,
                                            CancellationToken: CancellationToken);

        /// <summary>
        /// Wait until Count request starts are in line: holding the lock that
        /// orders them, or waiting for it.
        /// </summary>
        private static async Task InLineAsync(HTTP2ClientConnection Connection, Int32 Count)
        {

            var startsInLine = typeof(HTTP2ClientConnection).GetField("startsInLine", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var waited       = Stopwatch.StartNew();

            while ((Int32) startsInLine.GetValue(Connection)! < Count)
            {

                if (waited.Elapsed > HoldingH2Transport.StepTimeout)
                    throw new TimeoutException($"Only {startsInLine.GetValue(Connection)} of {Count} request starts were ever in line");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// The stream of the client's next HEADERS, or null if none comes
        /// within the step timeout.
        /// </summary>
        private static async Task<UInt32?> NextHeadersOnAsync(HoldingH2Transport Transport)
        {

            try
            {
                return (await Transport.NextHeadersAsync()).StreamId;
            }
            catch (OperationCanceledException)
            {
                return null;
            }

        }

        /// <summary>
        /// The status of the response to a request, within the step timeout.
        /// </summary>
        private static async Task<Int32> StatusOf(Task<HTTP2RequestHandle> Start)

            => (await (await Start.WaitAsync(HoldingH2Transport.StepTimeout)).Response.WaitAsync(HoldingH2Transport.StepTimeout)).Status;

        #endregion


        #region StartsInLine_GoOutInOneWrite()

        /// <summary>
        /// Eight requests start while the client is held in another write: the
        /// first waits for the write lock, the others for the lock that orders
        /// the starts. Each writes its HEADERS and leaves the flush to those
        /// behind it, so the one write after the held one carries all eight.
        /// With the line's batch taken out, each went out in a write of its own.
        /// </summary>
        [Test]
        public async Task StartsInLine_GoOutInOneWrite()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false, HoldPings: true);

            var connection = await ConnectHeldAsync(transport);
            var before     = transport.Writes.Count;

            var starts     = Enumerable.Range(0, 8).
                                 Select(i => StartAsync(connection, $"/{i}")).
                                 ToArray();

            await InLineAsync(connection, starts.Length);

            transport.Release(0);

            // The last start returns once it has flushed for the line.
            var handles    = await Task.WhenAll(starts).WaitAsync(HoldingH2Transport.StepTimeout);
            var written    = transport.Writes.Skip(before).ToList();

            Assert.That(written.Select(write => write.Count(frame => frame.Type == HTTP2FrameType.HEADERS)),
                        Is.EqualTo(new[] { starts.Length }),
                        "HEADERS per write after the held one: " +
                        String.Join(" | ", written.Select(write => String.Join(", ", write))));

            foreach (var handle in handles)
                await transport.RespondAsync(handle.StreamId, "hello");

            foreach (var start in starts)
                Assert.That(await StatusOf(start), Is.EqualTo(200));

        }

        #endregion

        #region AStartWaitingForASlot_FlushesTheLineFirst()

        /// <summary>
        /// The server takes one stream at a time, and two requests start while
        /// the client is held in another write. The first writes its HEADERS
        /// and leaves the flush to the second, which then finds no stream slot:
        /// the first holds it, and the server answers the first only once it
        /// has its HEADERS. So the second ends the line's batch before it waits,
        /// and the first goes out. Left in the buffer, the first waited for
        /// good, and the second with it.
        /// </summary>
        [Test]
        public async Task AStartWaitingForASlot_FlushesTheLineFirst()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false, HoldPings: true);

            var connection = await ConnectHeldAsync(transport, MaxConcurrentStreams: 1);

            var first      = StartAsync(connection, "/first");
            var second     = StartAsync(connection, "/second");

            await InLineAsync(connection, 2);

            transport.Release(0);

            Assert.That(await NextHeadersOnAsync(transport), Is.EqualTo(1u),
                        "the first request's HEADERS went out while the second waited for its stream slot");

            await transport.RespondAsync(1, "first");

            Assert.That(await NextHeadersOnAsync(transport), Is.EqualTo(3u),
                        "the second request went out once the first one's stream had closed");

            await transport.RespondAsync(3, "second");

            Assert.That(await StatusOf(first),  Is.EqualTo(200));
            Assert.That(await StatusOf(second), Is.EqualTo(200));

        }

        #endregion

        #region AStartGivenUpInLine_LeavesTheFlushToTheLast()

        /// <summary>
        /// Two requests start while the client is held in another write, and
        /// the second is given up while it waits in line. It leaves the line as
        /// it goes, so the first, now last, flushes its own HEADERS. Counted on,
        /// the second would have been left the flush, and nobody made it.
        /// </summary>
        [Test]
        public async Task AStartGivenUpInLine_LeavesTheFlushToTheLast()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false, HoldPings: true);

            var connection = await ConnectHeldAsync(transport);

            using var giveUp = new CancellationTokenSource();

            var first      = StartAsync(connection, "/first");
            var second     = StartAsync(connection, "/second", giveUp.Token);

            await InLineAsync(connection, 2);

            giveUp.Cancel();

            Assert.That(async () => await second.WaitAsync(HoldingH2Transport.StepTimeout),
                        Throws.InstanceOf<OperationCanceledException>());

            transport.Release(0);

            Assert.That(await NextHeadersOnAsync(transport), Is.EqualTo(1u),
                        "the first request's HEADERS went out");

            await transport.RespondAsync(1, "first");

            Assert.That(await StatusOf(first), Is.EqualTo(200));

        }

        #endregion

    }

}
