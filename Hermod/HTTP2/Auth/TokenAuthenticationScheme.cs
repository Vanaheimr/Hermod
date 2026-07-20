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
    /// "Token" HTTP authentication — <b>not an IETF standard</b> (the
    /// draft-hammer-http-token-auth I-D expired), but a widely used convention,
    /// popularized by Rails' <c>ActionController::HttpAuthentication::Token</c> and
    /// GitHub-style <c>Authorization: token &lt;token&gt;</c> APIs. Two on-the-wire
    /// forms are accepted:
    /// <list type="bullet">
    ///   <item>the bare form <c>Token &lt;token&gt;</c> (GitHub-style), where the
    ///   whole credential is the token; and</item>
    ///   <item>the parameterized form <c>Token token="&lt;token&gt;", nonce="…", …</c>
    ///   (Rails-style), an RFC 7235 auth-param list carrying a mandatory
    ///   <c>token</c> plus optional extra params.</item>
    /// </list>
    /// Functionally close to Bearer (RFC 6750) — a single opaque credential, no
    /// challenge-response — but with the <c>Token</c> scheme name and the optional
    /// structured parameters, which are handed to the validator alongside the token
    /// (e.g. to read a Rails-style <c>nonce</c>). Store-agnostic like the others:
    /// the app decides whether the token (and params) are valid.
    /// </summary>
    public sealed class TokenAuthenticationScheme : IHTTPAuthenticationScheme
    {

        private readonly Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<HTTPAuthenticatedIdentity?>> validate;

        /// <param name="Validate">
        /// Decides whether a token is valid; also receives any extra auth-params from
        /// the parameterized form (empty for the bare form), so an app can honor a
        /// Rails-style <c>nonce</c> or similar.
        /// </param>
        public TokenAuthenticationScheme(
            Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<HTTPAuthenticatedIdentity?>> Validate)
        {
            validate = Validate;
        }

        public string SchemeName => "Token";

        public string BuildChallenge(string Realm)
            => $"Token realm=\"{Realm}\"";

        public async Task<HTTPAuthenticatedIdentity?> AuthenticateAsync(
            string Credentials, string Method, string RequestTarget, CancellationToken CancellationToken)
        {

            var creds = Credentials.Trim();
            if (creds.Length == 0)
                return null;

            var p = HTTPAuthParams.Parse(creds);

            string token;
            if (p.TryGetValue("token", out var t))
                token = t;                 // Rails-style: Token token="…", …
            else if (p.Count == 0)
                token = creds;             // GitHub-style bare form: Token <token>
            else
                return null;               // structured params but no token= — malformed

            if (token.Length == 0)
                return null;

            return await validate(token, p, CancellationToken);

        }

    }

}
