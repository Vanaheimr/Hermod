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
    /// Connection-level options for a session, chiefly the liveness settings that drive keepalive probing
    /// and idle disconnection (the <c>ClientAlive*</c> / <c>ServerAlive*</c> equivalents). All timing is
    /// resolved through <see cref="TimeProvider"/> for deterministic testing.
    /// </summary>
    public sealed record SshConnectionOptions
    {

        /// <summary>
        /// The interval between keepalive probes to the peer, or null to disable keepalives. When set, a
        /// probe is sent after this much silence; after <see cref="KeepAliveCountMax"/> unanswered probes the
        /// connection is treated as lost.
        /// </summary>
        public TimeSpan?     KeepAliveInterval  { get; init; }

        /// <summary>Consecutive unanswered keepalive probes tolerated before the peer is declared dead (default 3).</summary>
        public Int32         KeepAliveCountMax  { get; init; } = 3;

        /// <summary>The idle timeout on real (channel-data) traffic, or null to disable idle disconnection.</summary>
        public TimeSpan?     IdleTimeout        { get; init; }

        /// <summary>The clock used for all liveness timing; defaults to <see cref="TimeProvider.System"/>.</summary>
        public TimeProvider  TimeProvider       { get; init; } = TimeProvider.System;


        /// <summary>Whether any liveness feature (keepalive or idle timeout) is enabled.</summary>
        public Boolean HasLiveness
            => KeepAliveInterval is not null || IdleTimeout is not null;

        /// <summary>Build a fresh liveness monitor from these options.</summary>
        public SshLivenessMonitor CreateLivenessMonitor()
            => new (TimeProvider, KeepAliveInterval, KeepAliveCountMax, IdleTimeout);

    }


    /// <summary>
    /// Thrown when a session is torn down because the peer stopped responding: either it failed to answer
    /// keepalive probes (dead peer) or the idle timeout elapsed.
    /// </summary>
    public sealed class SshConnectionLostException : Exception
    {

        /// <summary>Whether the loss was due to the idle timeout (as opposed to unanswered keepalives).</summary>
        public Boolean WasIdleTimeout { get; }

        /// <summary>Create a new connection-lost exception.</summary>
        public SshConnectionLostException(String Message, Boolean WasIdleTimeout = false)
            : base(Message)
        {
            this.WasIdleTimeout = WasIdleTimeout;
        }

    }

}
