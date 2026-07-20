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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// Robustness knobs for an <see cref="HTTP2ClientConnection"/>: how hard to try
    /// on the client's behalf before surfacing a failure, and how to detect a dead
    /// connection.
    /// </summary>
    public sealed record HTTP2ClientOptions
    {

        /// <summary>
        /// Maximum automatic retries of a request the server <i>refused</i>
        /// (RST_STREAM / REFUSED_STREAM), re-issued on a fresh stream of the same
        /// connection. A refusal guarantees the request was never processed
        /// (RFC 9113, Section 8.1), so retrying it is side-effect-safe.
        /// </summary>
        public int      MaxRefusedStreamRetries { get; init; } = 2;

        /// <summary>
        /// When greater than zero, send a PING after this much inactivity to check
        /// the connection is still alive. Zero (the default) disables keepalive.
        /// </summary>
        public TimeSpan KeepAliveInterval       { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// With keepalive enabled, tear the connection down if no PING ACK returns
        /// within this long — the peer (or the path) has gone silent.
        /// </summary>
        public TimeSpan KeepAliveTimeout        { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Cap on CONTINUATION frames per response header block. A server flooding
        /// the client with CONTINUATION frames is the same CVE-2024-27316 class the
        /// server guards against inbound — mirrored here.
        /// </summary>
        public int      MaxContinuationFrames   { get; init; } = 64;

        /// <summary>The default options.</summary>
        public static readonly HTTP2ClientOptions Default = new();

    }

}
