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

        /// <summary>
        /// The maximum number of authentication attempts before the connection is dropped (default 6).
        /// </summary>
        public Int32      MaxAuthTries         { get; init; } = 6;

        /// <summary>
        /// How long a connection may take to authenticate before it is dropped (default 120 s).
        /// </summary>
        public TimeSpan   LoginGraceTime       { get; init; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The maximum number of concurrent sessions a connection may open (default 10).
        /// </summary>
        public Int32      MaxSessions          { get; init; } = 10;

        /// <summary>
        /// The maximum number of connections at once, authenticated or not (default 100). One more is
        /// closed as soon as it is accepted.
        /// </summary>
        public Int32      MaxConnections       { get; init; } = 100;

        /// <summary>
        /// The maximum number of connections at once that have not authenticated yet (default 10) - the
        /// OpenSSH <c>MaxStartups</c>. What somebody guessing passwords or keys holds open is these.
        /// </summary>
        public Int32      MaxUnauthenticated   { get; init; } = 10;

        /// <summary>
        /// The maximum number of unauthenticated connections from one address at once (default 3) - the
        /// OpenSSH <c>PerSourceMaxStartups</c>, so that one address cannot take every slot of
        /// <see cref="MaxUnauthenticated"/> and lock everybody else out.
        /// </summary>
        public Int32      MaxUnauthenticatedPerAddress  { get; init; } = 3;

        /// <summary>
        /// The largest SSH <c>packet_length</c> accepted from a client, in bytes (default and maximum
        /// 256 KiB). RFC 4253 §6.1 obliges every implementation to accept 35 000, so a smaller value is
        /// refused when the server is constructed. A larger packet ends the connection as a protocol
        /// error before it is read — from the first packet of the key exchange on.
        /// </summary>
        public Int32      MaxPacketSize        { get; init; } = 256 * 1024;

        /// <summary>
        /// The smallest <see cref="MaxPacketSize"/> allowed: what RFC 4253 §6.1 requires every
        /// implementation to accept.
        /// </summary>
        public const Int32 MinimumMaxPacketSize = 35000;

        /// <summary>
        /// The keepalive probe interval, or null to disable keepalive probing.
        /// </summary>
        public TimeSpan?  ClientAliveInterval  { get; init; }

        /// <summary>
        /// Consecutive unanswered keepalive probes tolerated before declaring the peer dead (default 3).
        /// </summary>
        public Int32      ClientAliveCountMax  { get; init; } = 3;

        /// <summary>
        /// How long a connection may go without channel traffic before it is closed, or null to disable
        /// it. Channel traffic is anything on a channel in either direction — opens, data, requests,
        /// closes; keepalive probes and their replies do not count, so an open but unused session is
        /// idle. The client is told why with a DISCONNECT.
        /// </summary>
        public TimeSpan?  IdleTimeout          { get; init; }


        /// <summary>
        /// Build a <see cref="SshLivenessMonitor"/> from the liveness portion of these limits.
        /// </summary>
        public SshLivenessMonitor CreateLivenessMonitor(TimeProvider? TimeProvider = null)
            => new (TimeProvider, ClientAliveInterval, ClientAliveCountMax, IdleTimeout);

    }

}
