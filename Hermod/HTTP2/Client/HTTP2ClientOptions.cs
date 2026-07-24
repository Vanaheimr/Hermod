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

    using System.Net.Security;


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
        /// The time provider used for keep-alive pacing, liveness tracking and
        /// cache-age calculations. Inject a test clock for deterministic tests.
        /// </summary>
        public TimeProvider TimeProvider        { get; init; } = TimeProvider.System;

        /// <summary>
        /// Cap on CONTINUATION frames per response header block. A server flooding
        /// the client with CONTINUATION frames is the same CVE-2024-27316 class the
        /// server guards against inbound — mirrored here.
        /// </summary>
        public int      MaxContinuationFrames   { get; init; } = 64;

        /// <summary>
        /// Decides whether a TLS 1.2 cipher suite the server negotiated is
        /// unacceptable for HTTP/2, in which case the connection is refused instead
        /// of used (RFC 9113, Section 9.2.2 — the requirement is on "a deployment",
        /// so it binds the client too). Null (the default) applies the RFC's own
        /// rule, <see cref="HTTP2CipherSuites.IsBlocklisted(TlsCipherSuite)"/>; pass
        /// <c>_ => false</c> to reach a server stuck on a legacy suite, or a
        /// stricter predicate of your own. Only consulted for TLS 1.2 — every
        /// TLS 1.3 suite qualifies, and h2c has no TLS at all.
        /// </summary>
        public Func<TlsCipherSuite, Boolean>? IsBlocklistedCipherSuite { get; init; }

        /// <summary>
        /// Advertise <c>accept-encoding: br, gzip, deflate</c> on requests that do
        /// not already carry one, and transparently decode the
        /// <c>content-encoding</c> of the responses that come back (RFC 9110,
        /// Section 8.4). Off by default: it changes what goes out on the wire and
        /// what the caller gets back, so it is the caller's decision, not ours.
        /// </summary>
        public bool     AutomaticDecompression  { get; init; }

        /// <summary>
        /// Ceiling on a decoded response body, enforced *during* decompression.
        /// A few kilobytes of gzip can expand to gigabytes, so a client that
        /// decodes automatically needs a bound just as much as a server that
        /// buffers request bodies — this is the client-side counterpart of the
        /// server's <c>MaxRequestBodySize</c>, and shares its 16 MiB default.
        /// </summary>
        public long     MaxDecodedBodySize      { get; init; } = 16 * 1024 * 1024;

        /// <summary>
        /// Credentials to answer a <c>401 Unauthorized</c> with (RFC 9110, Section
        /// 11): the request is re-issued once, carrying an <c>Authorization</c>
        /// field built from the server's <c>WWW-Authenticate</c> challenge — Digest
        /// preferred, then Bearer, then Token, then Basic. Null (the default)
        /// leaves the 401 to the caller.
        ///
        /// Nothing is ever sent preemptively: credentials go out only in answer to
        /// a challenge, and only to the origin that issued it (the retry re-sends
        /// the very same request, so the authority cannot change underneath it).
        /// </summary>
        public HTTPClientCredentials? Credentials { get; init; }

        /// <summary>The default options.</summary>
        public static readonly HTTP2ClientOptions Default = new();

    }

}
