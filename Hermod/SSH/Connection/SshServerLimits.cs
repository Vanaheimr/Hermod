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
    /// The server's DoS-hardening and liveness limits (the OpenSSH <c>MaxAuthTries</c> / <c>LoginGraceTime</c>
    /// / <c>MaxSessions</c> / <c>ClientAlive*</c> family). A single place to configure the knobs that other
    /// components enforce (auth attempts, the login grace window, connection liveness).
    /// </summary>
    public sealed record SshServerLimits
    {

        /// <summary>The maximum number of authentication attempts before the connection is dropped (default 6).</summary>
        public Int32      MaxAuthTries         { get; init; } = 6;

        /// <summary>How long a connection may take to authenticate before it is dropped (default 120 s).</summary>
        public TimeSpan   LoginGraceTime       { get; init; } = TimeSpan.FromSeconds(120);

        /// <summary>The maximum number of concurrent sessions a connection may open (default 10).</summary>
        public Int32      MaxSessions          { get; init; } = 10;

        /// <summary>The maximum SSH packet payload accepted, in bytes (default 256 KiB; RFC 4253 floor is 35 000).</summary>
        public Int32      MaxPacketSize        { get; init; } = 256 * 1024;

        /// <summary>The keepalive probe interval, or null to disable keepalive probing.</summary>
        public TimeSpan?  ClientAliveInterval  { get; init; }

        /// <summary>Consecutive unanswered keepalive probes tolerated before declaring the peer dead (default 3).</summary>
        public Int32      ClientAliveCountMax  { get; init; } = 3;

        /// <summary>The idle timeout on real channel traffic, or null to disable it.</summary>
        public TimeSpan?  IdleTimeout          { get; init; }


        /// <summary>Build a <see cref="SshLivenessMonitor"/> from the liveness portion of these limits.</summary>
        public SshLivenessMonitor CreateLivenessMonitor(TimeProvider? TimeProvider = null)
            => new (TimeProvider, ClientAliveInterval, ClientAliveCountMax, IdleTimeout);

    }

}
