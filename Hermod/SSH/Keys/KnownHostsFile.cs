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

using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>The trust marker on a <c>known_hosts</c> line.</summary>
    public enum KnownHostMarker
    {
        /// <summary>An ordinary host-key line.</summary>
        None,
        /// <summary><c>@cert-authority</c> — the key is a CA that signs host certificates.</summary>
        CertAuthority,
        /// <summary><c>@revoked</c> — the key is explicitly distrusted.</summary>
        Revoked
    }


    /// <summary>One parsed <c>known_hosts</c> entry: an optional marker, the host patterns and the key.</summary>
    public sealed class KnownHostEntry
    {

        /// <summary>The trust marker (<see cref="KnownHostMarker.None"/> for a plain entry).</summary>
        public KnownHostMarker  Marker        { get; init; }

        /// <summary>The raw host-pattern field (comma-separated patterns or a hashed <c>|1|…</c> token).</summary>
        public String           HostPatterns  { get; init; } = "";

        /// <summary>The host key.</summary>
        public required SshPublicKey  PublicKey  { get; init; }


        /// <summary>Whether this entry applies to the given host and port.</summary>
        public Boolean Matches(String Host, IPPort Port)
        {

            var lookupName = Port.ToUInt16() == 22 ? Host : $"[{Host}]:{Port.ToUInt16()}";

            // Hashed entry: |1|<base64 salt>|<base64 HMAC-SHA1(salt, name)>.
            if (HostPatterns.StartsWith("|1|", StringComparison.Ordinal))
            {

                var parts = HostPatterns.Split('|');
                if (parts.Length != 4)
                    return false;

                var salt      = Convert.FromBase64String(parts[2]);
                var expected  = Convert.FromBase64String(parts[3]);
                var actual    = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(lookupName));

                return CryptographicOperations.FixedTimeEquals(actual, expected);

            }

            // Plain / wildcard patterns: a negated match excludes the line; a positive match includes it.
            var matched = false;
            foreach (var pattern in HostPatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var negated = pattern.StartsWith('!');
                var glob    = negated ? pattern[1..] : pattern;

                if (WildcardMatches(glob, lookupName))
                {
                    if (negated)
                        return false;
                    matched = true;
                }
            }

            return matched;

        }

        private static Boolean WildcardMatches(String Pattern, String Value)
        {
            var regex = "^" + Regex.Escape(Pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(Value, regex, RegexOptions.IgnoreCase);
        }

    }


    /// <summary>
    /// Parses a <c>known_hosts</c> file and looks host keys up by host and port, understanding hashed
    /// (<c>|1|…</c>) entries, the <c>@cert-authority</c> / <c>@revoked</c> markers and wildcard patterns.
    /// </summary>
    public sealed class KnownHostsFile
    {

        #region Data

        private readonly List<KnownHostEntry> entries;

        #endregion

        #region Properties

        /// <summary>All parsed entries.</summary>
        public IReadOnlyList<KnownHostEntry> Entries => entries;

        #endregion

        #region Constructor(s)

        private KnownHostsFile(List<KnownHostEntry> Entries)
        {
            this.entries = Entries;
        }

        #endregion


        #region (static) Parse(Text)

        /// <summary>Parse the full text of a <c>known_hosts</c> file.</summary>
        public static KnownHostsFile Parse(String Text)
        {

            var entries = new List<KnownHostEntry>();

            foreach (var rawLine in Text.Replace("\r\n", "\n").Split('\n'))
            {

                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var fields  = line.Split((Char[]?) null, StringSplitOptions.RemoveEmptyEntries);
                var index   = 0;
                var marker  = KnownHostMarker.None;

                if (fields.Length > 0 && fields[0].StartsWith('@'))
                {
                    marker = fields[0] switch {
                                 "@cert-authority" => KnownHostMarker.CertAuthority,
                                 "@revoked"        => KnownHostMarker.Revoked,
                                 _                 => KnownHostMarker.None
                             };
                    index = 1;
                }

                // Remaining: <hostpatterns> <keytype> <base64> [comment]
                if (fields.Length < index + 3)
                    continue;

                var hostPatterns = fields[index];
                var keyLine      = String.Join(' ', fields[(index + 1)..]);

                if (!SshPublicKey.TryParse(keyLine, out var key))
                    continue;

                entries.Add(new KnownHostEntry {
                    Marker        = marker,
                    HostPatterns  = hostPatterns,
                    PublicKey     = key!
                });

            }

            return new KnownHostsFile(entries);

        }

        #endregion

        #region Lookup(Host, Port)

        /// <summary>All entries whose host patterns match the given host and port.</summary>
        public IReadOnlyList<KnownHostEntry> Lookup(String Host, IPPort Port)
            => entries.Where(entry => entry.Matches(Host, Port)).ToList();

        #endregion

    }

}
