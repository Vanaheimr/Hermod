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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M9: the typed audit-event catalog — envelope stamping, monotonic sequence numbers and bounded-queue overflow accounting.
    /// </summary>
    [TestFixture]
    public class AuditCatalogTests
    {

        private static DateTimeOffset Now => new (2026, 07, 24, 12, 00, 00, TimeSpan.Zero);


        #region Context_StampsEnvelope

        [Test]
        [CancelAfter(15000)]
        public async Task Context_StampsEnvelope(CancellationToken CancellationToken)
        {

            var collecting = new CollectingAuditSink();
            var context    = new SshAuditContext(collecting, "conn-42", "203.0.113.7:22", SshRole.Server);

            await context.WriteAsync(new AuthAttemptEvent(Now, "achim", "publickey", "SHA256:abc"), CancellationToken);

            var e = collecting.Events.Single();
            Assert.Multiple(() => {
                Assert.That(e.EventType,    Is.EqualTo("AuthAttemptEvent"));
                Assert.That(e.ConnectionId, Is.EqualTo("conn-42"));
                Assert.That(e.PeerEndpoint, Is.EqualTo("203.0.113.7:22"));
                Assert.That(e.Role,         Is.EqualTo(SshRole.Server));
            });

        }

        #endregion

        #region Bounded_StampsMonotonicSequence

        [Test]
        [CancelAfter(15000)]
        public async Task Bounded_StampsMonotonicSequence(CancellationToken CancellationToken)
        {

            var collecting = new CollectingAuditSink();
            var sink        = new BoundedAuditSink(collecting, Capacity: 100, Policy: AuditOverflowPolicy.DropOldest);

            await sink.WriteAsync(new ConnectionOpenedEvent(Now), CancellationToken);
            await sink.WriteAsync(new KexCompletedEvent(Now, "curve25519-sha256", "chacha20-poly1305@openssh.com", "implicit", "ssh-ed25519", PostQuantum: false, StrictKex: true), CancellationToken);
            await sink.WriteAsync(new DisconnectedEvent(Now, 11, "by application"), CancellationToken);

            await sink.DisposeAsync();   // flush the pump

            Assert.Multiple(() => {
                Assert.That(collecting.Events.Select(e => e.SequenceNumber), Is.EqualTo(new Int64[] { 1, 2, 3 }));
                Assert.That(collecting.Events[1], Is.TypeOf<KexCompletedEvent>());
                Assert.That(((KexCompletedEvent) collecting.Events[1]).StrictKex, Is.True);
            });

        }

        #endregion

        #region Bounded_Overflow_DropsAndCounts

        [Test]
        [CancelAfter(15000)]
        public async Task Bounded_Overflow_DropsAndCounts(CancellationToken CancellationToken)
        {

            // The inner sink blocks on the first event, so the pump is parked and the bounded queue fills.
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new SemaphoreSlim(0);
            var blocking = new DelegateAuditSink(async (e, ct) =>
            {
                entered.TrySetResult();
                await release.WaitAsync(ct);
            });

            await using var sink = new BoundedAuditSink(blocking, Capacity: 2, Policy: AuditOverflowPolicy.DropNewest);

            // First event is consumed by the pump, which then blocks.
            await sink.WriteAsync(new ConnectionOpenedEvent(Now), CancellationToken);
            await entered.Task.WaitAsync(CancellationToken);

            // Two more fit the queue; the next three overflow and are dropped.
            for (var i = 0; i < 5; i++)
                await sink.WriteAsync(new ConnectionOpenedEvent(Now), CancellationToken);

            Assert.That(sink.DroppedCount, Is.EqualTo(3), "queue capacity 2 + one in flight ⇒ three of the five extra are dropped");

            release.Release(10);   // let the pump drain so DisposeAsync completes

        }

        #endregion

    }

}
