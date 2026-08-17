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
    /// Options for keystroke-timing obfuscation on interactive PTY sessions.
    /// </summary>
    public sealed record KeystrokeTimingObfuscation
    {

        /// <summary>
        /// Whether obfuscation is enabled.
        /// </summary>
        public Boolean   Enabled   { get; init; } = true;

        /// <summary>
        /// The fixed cadence on which packets are emitted while typing (default ≈ 20 ms grid).
        /// </summary>
        public TimeSpan  Interval  { get; init; } = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// How long after the last keystroke the chaff cadence keeps running before stopping (default ≈ 1 s).
        /// </summary>
        public TimeSpan  IdleStop  { get; init; } = TimeSpan.FromSeconds(1);


        /// <summary>
        /// The OpenSSH-like default: on for interactive PTY sessions.
        /// </summary>
        public static KeystrokeTimingObfuscation InteractiveDefault => new ();

        /// <summary>
        /// Obfuscation disabled.
        /// </summary>
        public static KeystrokeTimingObfuscation Off => new () { Enabled = false };

    }


    /// <summary>
    /// What a <see cref="KeystrokeTimingObfuscator"/> emits at a cadence tick.
    /// </summary>
    public enum KeystrokeEmit
    {
        /// <summary>
        /// Emit a real, queued keystroke packet.
        /// </summary>
        Real,
        /// <summary>
        /// Emit a chaff <c>SSH_MSG_PING</c> (no real data was ready).
        /// </summary>
        Chaff,
        /// <summary>
        /// Nothing to emit — typing has paused past the idle window; the cadence stops.
        /// </summary>
        Idle
    }


    /// <summary>
    /// Decorrelates interactive keystroke timing from the encrypted packet stream (Song/Wagner/Tian 2001;
    /// matches OpenSSH ≥ 9.5). While typing is active, packets leave on a <b>fixed cadence</b>: a real
    /// keystroke if one is queued, otherwise a chaff <c>SSH_MSG_PING</c> — so an observer sees a constant-rate
    /// stream instead of the typing rhythm. The cadence stops shortly after typing pauses (the idle window),
    /// to bound overhead. All timing goes through a <see cref="TimeProvider"/>, so it is deterministic under a
    /// fake clock. This is a pure decision engine; the connection layer performs the actual sends and turns a
    /// PING into a PONG.
    /// </summary>
    public sealed class KeystrokeTimingObfuscator
    {

        #region Data

        private readonly TimeProvider        timeProvider;
        private readonly TimeSpan            interval;
        private readonly TimeSpan            idleStop;
        private readonly Queue<Byte[]>       pending = new ();
        private readonly Lock                sync    = new ();

        private DateTimeOffset               lastActivity;

        #endregion

        #region Properties

        /// <summary>
        /// The fixed cadence between emissions.
        /// </summary>
        public TimeSpan Interval => interval;

        /// <summary>
        /// How many real keystrokes are queued.
        /// </summary>
        public Int32 PendingCount { get { lock (sync) return pending.Count; } }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an obfuscator.
        /// </summary>
        public KeystrokeTimingObfuscator(KeystrokeTimingObfuscation? Options = null, TimeProvider? TimeProvider = null)
        {
            var options        = Options ?? KeystrokeTimingObfuscation.InteractiveDefault;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.interval      = options.Interval;
            this.idleStop      = options.IdleStop;
            this.lastActivity  = this.timeProvider.GetUtcNow() - idleStop - interval;   // start idle
        }

        #endregion


        #region Enqueue(Keystroke)

        /// <summary>
        /// Queue a real keystroke packet to be released on the next cadence tick; (re)activates the cadence.
        /// </summary>
        public void Enqueue(Byte[] Keystroke)
        {
            lock (sync)
            {
                pending.Enqueue(Keystroke);
                lastActivity = timeProvider.GetUtcNow();
            }
        }

        #endregion

        #region Poll()

        /// <summary>
        /// Decide what to emit at a cadence tick: a queued real keystroke, a chaff PING (while still within
        /// the idle window), or nothing (the cadence has gone idle and should stop until the next keystroke).
        /// </summary>
        public (KeystrokeEmit Kind, Byte[]? Payload) Poll()
        {
            lock (sync)
            {

                if (pending.Count > 0)
                    return (KeystrokeEmit.Real, pending.Dequeue());

                var now = timeProvider.GetUtcNow();
                if (now - lastActivity < idleStop)
                    return (KeystrokeEmit.Chaff, null);

                return (KeystrokeEmit.Idle, null);

            }
        }

        #endregion

    }

}
