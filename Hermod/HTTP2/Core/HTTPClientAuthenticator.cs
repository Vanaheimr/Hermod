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
    /// The client half of RFC 9110, Section 11: turn a <c>WWW-Authenticate</c>
    /// challenge into an <c>Authorization</c> credential.
    ///
    /// The schemes in <c>HTTP2/Auth</c> are the server half — they *validate* what
    /// arrives. This is their mirror image, and the reason it lives in Core beside
    /// them: Digest in particular is one algorithm whose two ends must agree
    /// exactly, and having the challenge/response computation written down twice in
    /// two projects is how they drift apart.
    ///
    /// Preference order among offered schemes is Digest, then Bearer, then Token,
    /// then Basic — strongest first, with Basic last because it hands the password
    /// to the server in (base64-wrapped) clear.
    ///
    /// One instance per connection: it carries the Digest nonce counter, which RFC
    /// 7616 requires to increase for every request made with the same nonce.
    /// </summary>
    public sealed class HTTPClientAuthenticator
    {

        #region Data

        private readonly HTTPClientCredentials         credentials;
        private readonly Object                        nonceCountLock = new();
        private readonly Dictionary<String, UInt32>    nonceCounts    = new(StringComparer.Ordinal);

        /// <summary>Strongest first — see the class remarks.</summary>
        private static readonly String[] SchemePreference = ["Digest", "Bearer", "Token", "Basic"];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an authenticator for one connection's worth of requests.
        /// </summary>
        /// <param name="Credentials">What we know; a scheme we have nothing for is skipped.</param>
        public HTTPClientAuthenticator(HTTPClientCredentials Credentials)
        {
            credentials = Credentials;
        }

        #endregion


        #region ParseChallenges (WWWAuthenticate)

        /// <summary>
        /// Split <c>WWW-Authenticate</c> field values into (scheme, parameters)
        /// pairs.
        ///
        /// One challenge per field line is both what this stack's server emits (see
        /// <c>HTTPAuthenticator.BuildChallengeHeaders</c>) and by far the common
        /// case in the wild. RFC 9110 does allow several challenges in one line,
        /// but the grammar is genuinely ambiguous there — a comma separates both
        /// challenges *and* auth-params — so this splits only where a comma is
        /// followed by a bare token and a space, which is the unambiguous shape.
        /// </summary>
        public static IReadOnlyList<(String Scheme, String Parameters)> ParseChallenges(IEnumerable<String> WWWAuthenticate)
        {

            var challenges = new List<(String, String)>();

            foreach (var fieldValue in WWWAuthenticate)
                foreach (var challenge in SplitChallenges(fieldValue))
                {

                    var space = challenge.IndexOf(' ');

                    if (space < 0)
                        challenges.Add((challenge.Trim(), ""));
                    else
                        challenges.Add((challenge[..space].Trim(), challenge[(space + 1)..].Trim()));

                }

            return challenges;

        }

        /// <summary>
        /// Cut a field value at every comma that is followed by <c>token SP</c>
        /// outside a quoted string — i.e. the start of another challenge, as
        /// opposed to the comma between two auth-params.
        /// </summary>
        private static IEnumerable<String> SplitChallenges(String FieldValue)
        {

            var start    = 0;
            var inQuotes = false;

            for (var i = 0; i < FieldValue.Length; i++)
            {

                if (FieldValue[i] == '"' && (i == 0 || FieldValue[i - 1] != '\\'))
                    inQuotes = !inQuotes;

                if (inQuotes || FieldValue[i] != ',')
                    continue;

                // Look at what follows the comma: "Scheme param=..." starts a new
                // challenge, "param=..." is just the next parameter of this one.
                var j = i + 1;
                while (j < FieldValue.Length && FieldValue[j] == ' ')
                    j++;

                var tokenStart = j;
                while (j < FieldValue.Length && (Char.IsLetterOrDigit(FieldValue[j]) || FieldValue[j] == '-'))
                    j++;

                if (j > tokenStart && j < FieldValue.Length && FieldValue[j] == ' ')
                {
                    yield return FieldValue[start..i].Trim();
                    start = i + 1;
                }

            }

            var tail = FieldValue[start..].Trim();

            if (tail.Length > 0)
                yield return tail;

        }

        #endregion

        #region Answer (WWWAuthenticate, Method, RequestTarget)

        /// <summary>
        /// Pick the strongest offered scheme we hold credentials for and produce the
        /// matching <c>Authorization</c> field value, or null when nothing offered
        /// can be answered — an unsupported scheme, or one we have no credential
        /// for. Null means "do not retry": re-sending the same request unchanged
        /// would only earn the same 401.
        /// </summary>
        /// <param name="WWWAuthenticate">The challenge field values from the 401.</param>
        /// <param name="Method">The request method, which Digest signs over.</param>
        /// <param name="RequestTarget">The <c>:path</c>, which Digest also signs over.</param>
        public String? Answer(IEnumerable<String> WWWAuthenticate, String Method, String RequestTarget)
        {

            var challenges = ParseChallenges(WWWAuthenticate);

            foreach (var preferred in SchemePreference)
                foreach (var (scheme, parameters) in challenges)
                {

                    if (!String.Equals(scheme, preferred, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var answer = AnswerOne(scheme, parameters, Method, RequestTarget);

                    if (answer is not null)
                        return answer;

                }

            return null;

        }

        private String? AnswerOne(String Scheme, String Parameters, String Method, String RequestTarget)

            => Scheme.ToLowerInvariant() switch {

                   "basic"  => credentials.UserName is not null && credentials.Password is not null
                                   ? "Basic " + Convert.ToBase64String(
                                         // RFC 7617 §2.1: the charset the challenge asks for; UTF-8 is
                                         // the only one the RFC allows to be named, and our own server
                                         // states it explicitly.
                                         Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"))
                                   : null,

                   "bearer" => credentials.Token is not null ? "Bearer " + credentials.Token : null,
                   "token"  => credentials.Token is not null ? "Token "  + credentials.Token : null,

                   "digest" => credentials.UserName is not null && credentials.Password is not null
                                   ? BuildDigest(Parameters, Method, RequestTarget)
                                   : null,

                   _        => null

               };

        #endregion

        #region Digest (RFC 7616)

        /// <summary>
        /// Compute a Digest credential (RFC 7616, Section 3.4). With
        /// <c>qop=auth</c> — which is what any modern server offers, and what this
        /// stack's server sends — the response also covers a client nonce and a
        /// request counter, so a captured credential cannot simply be replayed.
        ///
        /// The counter is per (nonce) and must strictly increase, which is why this
        /// class is per-connection state rather than a static helper: reusing a
        /// server nonce across requests is the normal case, and repeating an nc
        /// value with it would be exactly the replay the mechanism exists to stop.
        /// </summary>
        private String? BuildDigest(String Parameters, String Method, String RequestTarget)
        {

            var p = HTTPAuthParams.Parse(Parameters);

            if (!p.TryGetValue("realm", out var realm) ||
                !p.TryGetValue("nonce", out var nonce))
                return null;   // an unusable challenge — nothing to answer with

            var algorithm = p.GetValueOrDefault("algorithm", "MD5");

            // RFC 7616 registers SHA-256 (and SHA-512/256); RFC 2617's MD5 stays for
            // interop. Anything else we cannot compute, so we decline rather than
            // send a credential the server will reject.
            if (!algorithm.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase) &&
                !algorithm.StartsWith("MD5",     StringComparison.OrdinalIgnoreCase))
                return null;

            var qop = SelectQop(p.GetValueOrDefault("qop", ""));

            // qop was optional in RFC 2617 and is effectively mandatory now; a
            // challenge offering only "auth-int" (which signs the body too) is one
            // we do not implement.
            if (qop is null)
                return null;

            var ha1 = Hash(algorithm, $"{credentials.UserName}:{realm}:{credentials.Password}");
            var ha2 = Hash(algorithm, $"{Method}:{RequestTarget}");

            var credential = new StringBuilder("Digest username=\"").
                                 Append(credentials.UserName).Append("\", realm=\"").
                                 Append(realm).Append("\", nonce=\"").
                                 Append(nonce).Append("\", uri=\"").
                                 Append(RequestTarget).Append("\", algorithm=").
                                 Append(algorithm);

            String response;

            if (qop.Length > 0)
            {

                var cnonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
                var nc     = NextNonceCount(nonce).ToString("x8");

                response = Hash(algorithm, $"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");

                credential.Append(", qop=").Append(qop).
                           Append(", nc=").Append(nc).
                           Append(", cnonce=\"").Append(cnonce).Append('"');

            }
            else
                response = Hash(algorithm, $"{ha1}:{nonce}:{ha2}");

            credential.Append(", response=\"").Append(response).Append('"');

            if (p.TryGetValue("opaque", out var opaque))
                credential.Append(", opaque=\"").Append(opaque).Append('"');

            return credential.ToString();

        }

        /// <summary>
        /// Of the qop values offered, take "auth"; treat an absent qop as the legacy
        /// RFC 2617 no-qop form (empty string). "auth-int" alone is unanswerable
        /// here, signalled as null.
        /// </summary>
        private static String? SelectQop(String Offered)
        {

            if (Offered.Length == 0)
                return "";

            var values = Offered.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return values.Any(value => value.Equals("auth", StringComparison.OrdinalIgnoreCase))
                       ? "auth"
                       : null;

        }

        /// <summary>The next nonce count for this server nonce, starting at 1.</summary>
        private UInt32 NextNonceCount(String Nonce)
        {
            lock (nonceCountLock)
            {
                var next = nonceCounts.GetValueOrDefault(Nonce) + 1;
                nonceCounts[Nonce] = next;
                return next;
            }
        }

        private static String Hash(String Algorithm, String Value)
        {

            var bytes = Encoding.UTF8.GetBytes(Value);

            return Convert.ToHexStringLower(
                       Algorithm.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase)
                           ? SHA256.HashData(bytes)
                           : MD5.HashData(bytes)
                   );

        }

        #endregion

    }

}
