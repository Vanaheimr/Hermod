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
    /// The generic RFC 9110 Section 11 authentication framework — the scheme-agnostic
    /// plumbing that sits in front of application logic: it reads the request's
    /// <c>Authorization</c> header, dispatches to whichever registered
    /// <see cref="IHTTPAuthenticationScheme"/> matches, and — when no credentials
    /// are present or none validate — answers <c>401 Unauthorized</c> with a
    /// <c>WWW-Authenticate</c> challenge for every registered scheme (RFC 9110,
    /// Sections 11.6.1 / 15.5.2). It never validates credentials itself; each
    /// scheme defers that to an app callback, so `Core` stays BCL-only and free of
    /// any credential store.
    ///
    /// Like <see cref="HTTPSemantics"/> this is version-independent (RFC 9110), so
    /// it lives in the shared library and is reusable in front of any transport.
    /// It composes with `HTTPSemantics` by wrapping the final
    /// <see cref="HTTP2RequestHandler"/>.
    /// </summary>
    public sealed class HTTPAuthenticator
    {

        private readonly string                          realm;
        private readonly IReadOnlyList<IHTTPAuthenticationScheme> schemes;

        /// <param name="Realm">The protection space (RFC 9110, Section 11.5) named in every challenge.</param>
        /// <param name="Schemes">The accepted schemes, in the order they'll be advertised in the challenge.</param>
        public HTTPAuthenticator(string Realm, params IHTTPAuthenticationScheme[] Schemes)
        {
            if (Schemes.Length == 0)
                throw new ArgumentException("At least one authentication scheme is required", nameof(Schemes));

            realm   = Realm;
            schemes = Schemes;
        }

        /// <summary>
        /// Attempt to authenticate a request from its headers. Returns the identity
        /// if a registered scheme validated the credentials, or null if there were
        /// no credentials, the scheme is unsupported, or validation failed — in
        /// which case the caller should reply with <see cref="BuildChallengeHeaders"/>.
        /// </summary>
        public async Task<HTTPAuthenticatedIdentity?> AuthenticateAsync(
            List<(string Name, string Value)> RequestHeaders,
            CancellationToken                 CancellationToken)
        {

            var authorization = RequestHeaders.FirstOrDefault(h => h.Name == "authorization").Value;

            if (string.IsNullOrEmpty(authorization))
                return null;

            // "auth-scheme SP credentials" (RFC 9110, Section 11.6.2).
            var space       = authorization.IndexOf(' ');
            var schemeName  = space < 0 ? authorization : authorization[..space];
            var credentials = space < 0 ? ""            : authorization[(space + 1)..];

            var scheme = schemes.FirstOrDefault(s => string.Equals(s.SchemeName, schemeName, StringComparison.OrdinalIgnoreCase));

            if (scheme is null)
                return null;   // Unsupported scheme — challenge with what we do support.

            // The request's method + target, which a challenge-binding scheme (Digest)
            // folds into its hash; Basic/Bearer ignore them.
            var method = RequestHeaders.FirstOrDefault(h => h.Name == ":method").Value ?? "GET";
            var target = RequestHeaders.FirstOrDefault(h => h.Name == ":path").Value   ?? "/";

            return await scheme.AuthenticateAsync(credentials, method, target, CancellationToken);

        }

        /// <summary>
        /// The <c>WWW-Authenticate</c> response header fields for a 401 — one per
        /// registered scheme (RFC 9110, Section 11.6.1 permits, and this uses,
        /// multiple challenges so the client can pick a scheme it supports).
        /// </summary>
        public List<(string Name, string Value)> BuildChallengeHeaders()
            => schemes.Select(s => ("www-authenticate", s.BuildChallenge(realm))).ToList();

    }

}
