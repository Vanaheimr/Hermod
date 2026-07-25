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
    /// A completed HTTP/2 response, as assembled by <see cref="HTTP2ClientConnection"/>.
    /// </summary>
    public sealed class HTTP2Response
    {
        public required int                               Status   { get; init; }
        public required List<(string Name, string Value)> Headers  { get; init; }
        public required byte[]                            Body     { get; init; }

        /// <summary>Trailer fields, if the server sent a trailing HEADERS block (RFC 9113, Section 8.1). Empty otherwise.</summary>
        public          List<(string Name, string Value)> Trailers { get; init; } = [];

        /// <summary>
        /// Interim (1xx) responses received before the final response, in order —
        /// e.g. a <c>100 Continue</c> (RFC 9110) or a <c>103 Early Hints</c>
        /// (RFC 8297) with <c>Link</c> preload headers. Empty if none were sent.
        /// </summary>
        public          List<(int Status, List<(string Name, string Value)> Headers)> InformationalResponses { get; init; } = [];

        /// <summary>
        /// The <c>content-encoding</c> that was undone before <see cref="Body"/> was
        /// handed over (RFC 9110, Section 8.4), or null if the body arrived as the
        /// identity representation — which is also the case whenever
        /// <see cref="HTTP2ClientOptions.AutomaticDecompression"/> is off, since then
        /// the encoded bytes and the <c>content-encoding</c> header are both left
        /// exactly as received.
        /// </summary>
        public          string?                           DecodedContentEncoding { get; init; }

        /// <summary>
        /// What became of the response's <c>Content-Digest</c> (RFC 9530), when
        /// <see cref="HTTP2ClientOptions.VerifyDigests"/> is on — checked against the
        /// octets as they arrived, before any decoding.
        ///
        /// <see cref="HTTPDigestVerification.Mismatch"/> never appears here: a
        /// disagreement fails the response instead. The value that matters is the
        /// difference between <see cref="HTTPDigestVerification.Match"/> and
        /// <see cref="HTTPDigestVerification.NotPresent"/> — a caller that requires
        /// integrity has to reject the latter too, and cannot do that unless it can
        /// see it. Always <see cref="HTTPDigestVerification.NotPresent"/> while
        /// verification is switched off, since then nothing was looked at.
        /// </summary>
        public          HTTPDigestVerification            DigestVerification { get; init; }

        /// <summary>
        /// The redirect targets that were followed to reach this response, in order
        /// (RFC 9110, Section 15.4) — empty when the first request answered directly,
        /// which is always the case unless
        /// <see cref="HTTP2ClientOptions.MaxRedirects"/> is set. The last entry is
        /// where this response actually came from, which is not necessarily the URI
        /// the caller asked for.
        /// </summary>
        public          IReadOnlyList<string>             RedirectChain { get; internal set; } = [];

        public string? HeaderValue(string Name)
            => Headers.FirstOrDefault(h => h.Name == Name).Value;
    }

}
