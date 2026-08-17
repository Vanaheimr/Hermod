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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// What a <see cref="SshLivenessMonitor"/> asks its caller to do at a given moment.
    /// </summary>
    public enum SshLivenessAction
    {
        /// <summary>
        /// Nothing to do yet — keep waiting.
        /// </summary>
        None,
        /// <summary>
        /// Send a keepalive probe to the peer (and expect a reply).
        /// </summary>
        SendKeepAlive,
        /// <summary>
        /// The peer failed to answer <c>KeepAliveCountMax</c> probes — treat the connection as dead.
        /// </summary>
        PeerIsDead,
        /// <summary>
        /// The session exceeded its idle timeout with no real traffic — close it.
        /// </summary>
        IdleTimeout
    }


    /// <summary>
    /// Connection-liveness state machine (the <c>ClientAlive*</c> / <c>ServerAlive*</c> equivalents): it
    /// decides when to send keepalive probes, when unanswered probes mean a dead peer, and when a session
    /// has been idle for too long. All timing goes through a <see cref="TimeProvider"/>, so the behaviour is
    /// fully deterministic under a fake clock.
    ///
    /// <para>
    /// Two clocks are tracked independently, matching OpenSSH semantics: <b>real traffic</b> (channel data)
    /// resets both the idle timer and the keepalive state, whereas a <b>keepalive reply</b> only proves the
    /// peer is alive — it clears the dead-peer counter but does <i>not</i> reset the idle timer. Thus an
    /// otherwise silent but responsive peer still eventually hits the idle timeout.
    /// </para>
    /// </summary>
    public sealed class SshLivenessMonitor
    {

        #region Data

        private readonly TimeProvider     timeProvider;
        private readonly Object            sync = new ();

        private DateTimeOffset            lastActivity;
        private DateTimeOffset            lastProbe;
        private Int32                     outstandingProbes;

        #endregion

        #region Properties

        /// <summary>
        /// The interval between keepalive probes, or null to disable keepalives.
        /// </summary>
        public TimeSpan?  KeepAliveInterval  { get; }

        /// <summary>
        /// How many consecutive unanswered probes are tolerated before the peer is declared dead.
        /// </summary>
        public Int32      KeepAliveCountMax  { get; }

        /// <summary>
        /// The idle timeout (no real traffic), or null to disable idle disconnection.
        /// </summary>
        public TimeSpan?  IdleTimeout        { get; }

        /// <summary>
        /// How many keepalive probes are currently outstanding (unanswered).
        /// </summary>
        public Int32      OutstandingProbes  { get { lock (sync) return outstandingProbes; } }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a liveness monitor.
        /// </summary>
        /// <param name="TimeProvider">The clock; defaults to <see cref="TimeProvider.System"/>.</param>
        /// <param name="KeepAliveInterval">The interval between keepalive probes, or null to disable them.</param>
        /// <param name="KeepAliveCountMax">Unanswered probes tolerated before the peer is declared dead (must be ≥ 1).</param>
        /// <param name="IdleTimeout">The idle timeout on real traffic, or null to disable it.</param>
        public SshLivenessMonitor(TimeProvider?  TimeProvider       = null,
                                  TimeSpan?      KeepAliveInterval  = null,
                                  Int32          KeepAliveCountMax  = 3,
                                  TimeSpan?      IdleTimeout        = null)
        {

            this.timeProvider       = TimeProvider ?? System.TimeProvider.System;
            this.KeepAliveInterval  = KeepAliveInterval;
            this.KeepAliveCountMax  = Math.Max(1, KeepAliveCountMax);
            this.IdleTimeout        = IdleTimeout;

            var now                 = this.timeProvider.GetUtcNow();
            this.lastActivity       = now;
            this.lastProbe          = now;

        }

        #endregion


        #region RecordActivity()

        /// <summary>
        /// Record genuine peer traffic (channel data, requests, …). Resets the idle timer and the keepalive
        /// state — the peer is demonstrably alive and the session is demonstrably not idle.
        /// </summary>
        public void RecordActivity()
        {
            lock (sync)
            {
                var now            = timeProvider.GetUtcNow();
                lastActivity       = now;
                lastProbe          = now;
                outstandingProbes  = 0;
            }
        }

        #endregion

        #region RecordKeepAliveReply()

        /// <summary>
        /// Record a reply to one of our keepalive probes. Clears the dead-peer counter (the peer answered)
        /// and defers the next probe by a full interval, but does <i>not</i> reset the idle timer — a merely
        /// responsive peer is still idle for idle-timeout purposes.
        /// </summary>
        public void RecordKeepAliveReply()
        {
            lock (sync)
            {
                lastProbe          = timeProvider.GetUtcNow();
                outstandingProbes  = 0;
            }
        }

        #endregion

        #region Poll()

        /// <summary>
        /// Evaluate the liveness state at the current instant and return the action the caller should take.
        /// A returned <see cref="SshLivenessAction.SendKeepAlive"/> counts the probe as outstanding, so the
        /// caller must actually transmit it.
        /// </summary>
        public SshLivenessAction Poll()
        {
            lock (sync)
            {

                var now = timeProvider.GetUtcNow();

                // 1. Idle timeout on real traffic wins — a dead OR idle session should go away.
                if (IdleTimeout is { } idle && now - lastActivity >= idle)
                    return SshLivenessAction.IdleTimeout;

                if (KeepAliveInterval is { } interval)
                {

                    // 2. Too many unanswered probes ⇒ the peer is gone.
                    if (outstandingProbes >= KeepAliveCountMax)
                        return SshLivenessAction.PeerIsDead;

                    // 3. Time for the next probe?
                    if (now - lastProbe >= interval)
                    {
                        lastProbe = now;
                        outstandingProbes++;
                        return SshLivenessAction.SendKeepAlive;
                    }

                }

                return SshLivenessAction.None;

            }
        }

        #endregion

        #region TimeUntilNextEvent()

        /// <summary>
        /// The time until the next moment <see cref="Poll"/> could return something other than
        /// <see cref="SshLivenessAction.None"/> — useful for scheduling an efficient wait. Never negative;
        /// <see cref="Timeout.InfiniteTimeSpan"/> when neither keepalives nor an idle timeout are configured.
        /// </summary>
        public TimeSpan TimeUntilNextEvent()
        {
            lock (sync)
            {

                var now   = timeProvider.GetUtcNow();
                var next  = TimeSpan.MaxValue;

                if (IdleTimeout is { } idle)
                    next = Min(next, (lastActivity + idle) - now);

                if (KeepAliveInterval is { } interval && outstandingProbes < KeepAliveCountMax)
                    next = Min(next, (lastProbe + interval) - now);

                if (next == TimeSpan.MaxValue)
                    return Timeout.InfiniteTimeSpan;

                return next < TimeSpan.Zero ? TimeSpan.Zero : next;

            }
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b)
            => a < b ? a : b;

        #endregion

    }

}
