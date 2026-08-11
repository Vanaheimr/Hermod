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

#region Usings

using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// One entry from an <c>authorized_keys</c> file (or an equivalent constructed in code): the public
    /// key plus its options, including certificate-style <c>NotBefore</c>/<c>NotAfter</c> validity
    /// windows (a HermodSSH extension, with the OpenSSH-compatible <c>expiry-time=</c> alias for the
    /// upper bound). Validity is evaluated at authentication time via a <see cref="TimeProvider"/>.
    /// </summary>
    public sealed class AuthorizedKey
    {

        #region Properties

        /// <summary>The public key this entry authorizes.</summary>
        public SshPublicKey       PublicKey    { get; }

        /// <summary>Whether the key is a certificate authority (<c>cert-authority</c> option).</summary>
        public Boolean            IsCertAuthority  { get; init; }

        /// <summary>The certificate principals this entry allows (<c>principals="…"</c>), if any.</summary>
        public IReadOnlyList<String>  Principals  { get; init; } = [];

        /// <summary>Not valid before this instant, if set (<c>not-before="…"</c>).</summary>
        public DateTimeOffset?    NotBefore    { get; init; }

        /// <summary>Not valid after this instant, if set (<c>not-after="…"</c> / <c>expiry-time="…"</c>).</summary>
        public DateTimeOffset?    NotAfter     { get; init; }

        /// <summary>A forced command (<c>command="…"</c>), if set.</summary>
        public String?            ForcedCommand  { get; init; }

        /// <summary>The raw option tokens as parsed, preserved for options we do not model explicitly.</summary>
        public IReadOnlyList<String>  Options  { get; init; } = [];

        /// <summary>
        /// What this entry's options confine the session to — the enforced form of <c>command=</c>,
        /// <c>from=</c>, <c>restrict</c>, <c>no-pty</c> and <c>no-port-forwarding</c>, applied by the
        /// server once the key authenticates. A line whose options cannot all be enforced is refused at
        /// parse time rather than accepted with parts of it silently dropped.
        /// </summary>
        public SshSessionRestrictions  Restrictions  { get; init; } = SshSessionRestrictions.None;

        #endregion

        #region Constructor(s)

        /// <summary>Create an authorized-key entry for the given public key.</summary>
        public AuthorizedKey(SshPublicKey PublicKey)
        {
            this.PublicKey = PublicKey;
        }

        #endregion


        #region IsValidAt(Now)

        /// <summary>
        /// Whether the validity window contains <paramref name="Now"/> (<c>NotBefore ≤ now &lt; NotAfter</c>);
        /// keys without a window are always in range. Established sessions survive later expiry — this is a
        /// gate for the authentication moment only.
        /// </summary>
        public Boolean IsValidAt(DateTimeOffset Now)
        {

            if (NotBefore.HasValue && Now < NotBefore.Value)
                return false;

            if (NotAfter.HasValue && Now >= NotAfter.Value)
                return false;

            return true;

        }

        #endregion

        #region Matches(PublicKeyBlob)

        /// <summary>Whether this entry's public key equals the given wire blob.</summary>
        public Boolean Matches(ReadOnlySpan<Byte> PublicKeyBlob)
            => PublicKey.Blob.AsSpan().SequenceEqual(PublicKeyBlob);

        #endregion


        #region (static) ParseTimestamp(Value)

        /// <summary>
        /// Parse a validity timestamp: the OpenSSH numeric form <c>YYYYMMDD[HHMM[SS]]</c> (optionally
        /// suffixed with <c>Z</c>, interpreted as UTC) or an ISO 8601 date/time.
        /// </summary>
        public static DateTimeOffset ParseTimestamp(String Value)
        {

            var text  = Value.Trim().Trim('"');
            var utc   = text.EndsWith('Z');
            var digits = utc ? text[..^1] : text;

            if (digits.Length is 8 or 12 or 14 && digits.All(Char.IsDigit))
            {

                var year   = Int32.Parse(digits.AsSpan(0, 4), CultureInfo.InvariantCulture);
                var month  = Int32.Parse(digits.AsSpan(4, 2), CultureInfo.InvariantCulture);
                var day    = Int32.Parse(digits.AsSpan(6, 2), CultureInfo.InvariantCulture);
                var hour   = digits.Length >= 12 ? Int32.Parse(digits.AsSpan(8,  2), CultureInfo.InvariantCulture) : 0;
                var minute = digits.Length >= 12 ? Int32.Parse(digits.AsSpan(10, 2), CultureInfo.InvariantCulture) : 0;
                var second = digits.Length == 14 ? Int32.Parse(digits.AsSpan(12, 2), CultureInfo.InvariantCulture) : 0;

                return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);

            }

            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        }

        #endregion

    }

}
