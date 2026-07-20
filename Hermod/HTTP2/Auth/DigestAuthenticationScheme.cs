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

    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// RFC 7616 "Digest" HTTP authentication: a challenge-response scheme that,
    /// unlike Basic, never sends the password over the wire. The server issues a
    /// one-time <c>nonce</c> in its <c>WWW-Authenticate</c> challenge; the client
    /// answers with <c>response = H(HA1:nonce:nc:cnonce:qop:HA2)</c> where
    /// <c>HA1 = H(username:realm:password)</c> and <c>HA2 = H(method:request-target)</c>
    /// (Section 3.4). The server recomputes the same hash from the password it looks
    /// up for that user and compares — so it proves knowledge of the password
    /// without ever receiving it.
    ///
    /// Store-agnostic like the other schemes: the app supplies a lookup from
    /// username to that user's password (a real deployment would more likely store
    /// the precomputed <c>H(username:realm:password)</c>; the plaintext lookup keeps
    /// the demo simple). <c>qop=auth</c> with client <c>nc</c>/<c>cnonce</c> is
    /// supported (and the legacy RFC 2069 no-<c>qop</c> form); <c>auth-int</c> is not
    /// advertised. The nonce is stateless — <c>base64(ticks ":" HMAC(secret, ticks))</c>
    /// — so the server validates its own integrity and age (default 5 min) without
    /// keeping per-nonce state. Default hash is SHA-256 (RFC 7616); MD5 (RFC 2617)
    /// is accepted for interop if a client echoes <c>algorithm=MD5</c>.
    /// </summary>
    public sealed class DigestAuthenticationScheme : IHTTPAuthenticationScheme
    {

        private readonly Func<string, CancellationToken, Task<string?>> lookupPassword;
        private readonly string   realm;
        private readonly string   algorithm;      // advertised in the challenge (default SHA-256)
        private readonly TimeSpan nonceMaxAge;
        private readonly byte[]   nonceSecret = RandomNumberGenerator.GetBytes(32);

        /// <param name="Realm">The protection space; folded into HA1, so it must match what the client used.</param>
        /// <param name="LookupPassword">Maps a username to that user's password (null ⇒ unknown user).</param>
        /// <param name="Algorithm">The algorithm advertised in the challenge — "SHA-256" (default) or "MD5".</param>
        /// <param name="NonceMaxAge">How long a nonce stays valid (default 5 minutes).</param>
        public DigestAuthenticationScheme(
            string                                        Realm,
            Func<string, CancellationToken, Task<string?>> LookupPassword,
            string                                        Algorithm   = "SHA-256",
            TimeSpan?                                     NonceMaxAge = null)
        {
            realm          = Realm;
            lookupPassword = LookupPassword;
            algorithm      = Algorithm;
            nonceMaxAge    = NonceMaxAge ?? TimeSpan.FromMinutes(5);
        }

        public string SchemeName => "Digest";

        // A fresh nonce every challenge; qop="auth" so clients include nc/cnonce
        // (protecting against replay of a captured response). No opaque/domain — both
        // optional (RFC 7616, Section 3.3).
        public string BuildChallenge(string Realm)
            => $"Digest realm=\"{Realm}\", qop=\"auth\", algorithm={algorithm}, nonce=\"{CreateNonce()}\"";

        public async Task<HTTPAuthenticatedIdentity?> AuthenticateAsync(
            string Credentials, string Method, string RequestTarget, CancellationToken CancellationToken)
        {

            var p = HTTPAuthParams.Parse(Credentials);

            // Required fields (RFC 7616, Section 3.4). username, realm, nonce, uri
            // and response must all be present.
            if (!p.TryGetValue("username", out var username) || username.Length == 0 ||
                !p.TryGetValue("nonce",    out var nonce)    ||
                !p.TryGetValue("uri",      out var uri)      ||
                !p.TryGetValue("response", out var clientResponse))
                return null;

            // The realm the client hashed must be our protection space, and the
            // digest-uri must be the request-target it actually sent — otherwise the
            // response was computed for a different scope/resource.
            if (p.TryGetValue("realm", out var clientRealm) && clientRealm != realm)
                return null;
            if (uri != RequestTarget)
                return null;

            // Reject a nonce we didn't issue, tampered with, or that has expired.
            if (!ValidateNonce(nonce))
                return null;

            // The algorithm the client used (defaults to MD5 when absent, RFC 2617);
            // accept only SHA-256 or MD5 (and their -sess variants).
            var alg = p.TryGetValue("algorithm", out var a) ? a : "MD5";
            if (!IsSupportedAlgorithm(alg))
                return null;

            var password = await lookupPassword(username, CancellationToken);
            if (password is null)
                return null;   // Unknown user — indistinguishable to the client from a wrong password.

            var ha1 = H(alg, $"{username}:{realm}:{password}");

            // algorithm "-sess" (Section 3.4.2) mixes the nonce/cnonce into HA1.
            if (alg.EndsWith("-sess", StringComparison.OrdinalIgnoreCase))
            {
                if (!p.TryGetValue("cnonce", out var sessCnonce))
                    return null;
                ha1 = H(alg, $"{ha1}:{nonce}:{sessCnonce}");
            }

            var ha2 = H(alg, $"{Method}:{uri}");

            string expected;
            if (p.TryGetValue("qop", out var qop) && qop.Length > 0)
            {
                // qop=auth (Section 3.4.1). We only advertise auth; reject auth-int.
                if (!string.Equals(qop, "auth", StringComparison.OrdinalIgnoreCase))
                    return null;
                if (!p.TryGetValue("nc", out var nc) || !p.TryGetValue("cnonce", out var cnonce))
                    return null;
                expected = H(alg, $"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");
            }
            else
                // Legacy RFC 2069 (no qop): response = H(HA1:nonce:HA2).
                expected = H(alg, $"{ha1}:{nonce}:{ha2}");

            // Constant-time compare of the two hex digests (equal length per algorithm).
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(clientResponse.ToLowerInvariant())))
                return null;

            return new HTTPAuthenticatedIdentity { Name = username };

        }

        private static bool IsSupportedAlgorithm(string Algorithm)
            => Algorithm.Equals("SHA-256",      StringComparison.OrdinalIgnoreCase)
            || Algorithm.Equals("SHA-256-sess", StringComparison.OrdinalIgnoreCase)
            || Algorithm.Equals("MD5",          StringComparison.OrdinalIgnoreCase)
            || Algorithm.Equals("MD5-sess",     StringComparison.OrdinalIgnoreCase);

        /// <summary>Hex-encode H(input) with the algorithm's hash (SHA-256 unless the client chose MD5).</summary>
        private static string H(string Algorithm, string Input)
        {
            var bytes  = Encoding.UTF8.GetBytes(Input);
            var digest = Algorithm.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase)
                             ? SHA256.HashData(bytes)
                             : MD5.HashData(bytes);
            return Convert.ToHexStringLower(digest);
        }

        /// <summary>A stateless nonce: <c>base64(ticks ":" base64(HMAC-SHA256(secret, ticks)))</c>.</summary>
        private string CreateNonce()
        {
            var ticks = DateTimeOffset.UtcNow.UtcTicks.ToString();
            var mac   = HMACSHA256.HashData(nonceSecret, Encoding.ASCII.GetBytes(ticks));
            return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ticks}:{Convert.ToBase64String(mac)}"));
        }

        /// <summary>Validate a nonce we issued: HMAC integrity + not older than <see cref="nonceMaxAge"/>.</summary>
        private bool ValidateNonce(string Nonce)
        {
            try
            {
                var raw   = Encoding.ASCII.GetString(Convert.FromBase64String(Nonce));
                var colon = raw.IndexOf(':');
                if (colon < 0)
                    return false;

                var ticksStr = raw[..colon];
                if (!long.TryParse(ticksStr, out var ticks))
                    return false;

                var expectedMac = HMACSHA256.HashData(nonceSecret, Encoding.ASCII.GetBytes(ticksStr));
                var providedMac = Convert.FromBase64String(raw[(colon + 1)..]);
                if (!CryptographicOperations.FixedTimeEquals(expectedMac, providedMac))
                    return false;

                var age = DateTimeOffset.UtcNow.UtcTicks - ticks;
                return age >= 0 && age <= nonceMaxAge.Ticks;
            }
            catch
            {
                return false;   // Not base64 / malformed — not a nonce we issued.
            }
        }

    }

}
