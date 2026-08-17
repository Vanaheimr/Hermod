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
    /// M6 connection liveness: the keepalive / idle-timeout state machine, driven deterministically by a
    /// manual clock. Verifies exact dead-peer counting ("not one probe short"), that real traffic resets both
    /// timers, and that a merely-responsive peer still hits the idle timeout.
    /// </summary>
    [TestFixture]
    public class SshLivenessMonitorTests
    {

        #region (helper) ManualClock

        /// <summary>
        /// A trivial hand-advanced <see cref="TimeProvider"/> for deterministic Poll() testing.
        /// </summary>
        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;
            public ManualClock(DateTimeOffset Start) { now = Start; }
            public override DateTimeOffset GetUtcNow() => now;
            public void Advance(TimeSpan By) => now += By;
        }

        private static ManualClock NewClock()
            => new (new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));

        #endregion


        #region KeepAlive_SendsProbesThenDeclaresDead_NotOneProbeShort

        [Test]
        public void KeepAlive_SendsProbesThenDeclaresDead_NotOneProbeShort()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock,
                                                 KeepAliveInterval: TimeSpan.FromSeconds(10),
                                                 KeepAliveCountMax: 3);

            // Nothing due before the first interval elapses.
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.None));

            // Exactly three probes must be emitted, one per interval, with none of them yet fatal.
            for (var probe = 1; probe <= 3; probe++)
            {
                clock.Advance(TimeSpan.FromSeconds(10));
                Assert.That(monitor.Poll(),               Is.EqualTo(SshLivenessAction.SendKeepAlive), $"probe {probe}");
                Assert.That(monitor.OutstandingProbes,    Is.EqualTo(probe));
            }

            // Only after the third unanswered probe does the peer count as dead — not one probe short.
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.PeerIsDead));

        }

        #endregion

        #region KeepAlive_ReplyKeepsPeerAlive_NeverDies

        [Test]
        public void KeepAlive_ReplyKeepsPeerAlive_NeverDies()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock,
                                                 KeepAliveInterval: TimeSpan.FromSeconds(10),
                                                 KeepAliveCountMax: 3);

            for (var round = 0; round < 10; round++)
            {
                clock.Advance(TimeSpan.FromSeconds(10));
                Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.SendKeepAlive));
                monitor.RecordKeepAliveReply();                 // the peer answers each probe
                Assert.That(monitor.OutstandingProbes, Is.EqualTo(0));
            }

            // A promptly-answered probe stream never declares death.
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.None));

        }

        #endregion

        #region Idle_FiresOnlyAfterTrueInactivity

        [Test]
        public void Idle_FiresOnlyAfterTrueInactivity()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock, IdleTimeout: TimeSpan.FromSeconds(60));

            clock.Advance(TimeSpan.FromSeconds(59));
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.None));

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.IdleTimeout));

        }

        #endregion

        #region Idle_RealTrafficResetsTheTimer

        [Test]
        public void Idle_RealTrafficResetsTheTimer()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock, IdleTimeout: TimeSpan.FromSeconds(60));

            clock.Advance(TimeSpan.FromSeconds(50));
            monitor.RecordActivity();                            // genuine traffic pushes the deadline out

            clock.Advance(TimeSpan.FromSeconds(59));
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.None));

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.IdleTimeout));

        }

        #endregion

        #region Idle_ResponsivePeerStillTimesOut

        [Test]
        public void Idle_ResponsivePeerStillTimesOut()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock,
                                                 KeepAliveInterval: TimeSpan.FromSeconds(10),
                                                 KeepAliveCountMax: 3,
                                                 IdleTimeout:       TimeSpan.FromSeconds(60));

            // The peer answers every keepalive, but sends no real data …
            for (var t = 10; t <= 60; t += 10)
            {
                clock.Advance(TimeSpan.FromSeconds(10));
                if (monitor.Poll() == SshLivenessAction.SendKeepAlive)
                    monitor.RecordKeepAliveReply();
            }

            // … so at the idle deadline the session is torn down regardless of the healthy keepalives.
            Assert.That(monitor.Poll(), Is.EqualTo(SshLivenessAction.IdleTimeout));

        }

        #endregion

        #region TimeUntilNextEvent_TracksTheNearerDeadline

        [Test]
        public void TimeUntilNextEvent_TracksTheNearerDeadline()
        {

            var clock   = NewClock();
            var monitor = new SshLivenessMonitor(clock,
                                                 KeepAliveInterval: TimeSpan.FromSeconds(10),
                                                 IdleTimeout:       TimeSpan.FromSeconds(60));

            // The keepalive interval is the nearer of the two deadlines.
            Assert.That(monitor.TimeUntilNextEvent(), Is.EqualTo(TimeSpan.FromSeconds(10)));

            var idleOnly = new SshLivenessMonitor(clock);
            Assert.That(idleOnly.TimeUntilNextEvent(), Is.EqualTo(Timeout.InfiniteTimeSpan));

        }

        #endregion

    }

}
