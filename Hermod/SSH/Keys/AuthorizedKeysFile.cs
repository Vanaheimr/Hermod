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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Parses an <c>authorized_keys</c> file into <see cref="AuthorizedKey"/> entries.
    ///
    /// <para>
    /// Understood and <b>enforced</b>: <c>cert-authority</c>, <c>principals="…"</c>,
    /// <c>command="…"</c> (forced command), <c>from="…"</c> (address/CIDR list),
    /// <c>restrict</c>, <c>no-pty</c>/<c>pty</c>, <c>no-port-forwarding</c>/<c>port-forwarding</c>,
    /// and the validity options <c>not-before="…"</c> / <c>not-after="…"</c> (with the
    /// OpenSSH-compatible <c>expiry-time="…"</c> alias).
    /// </para>
    ///
    /// <para>
    /// Options that only restrict a feature this implementation does not offer (X11, agent forwarding,
    /// user-rc) are accepted as already satisfied. <b>Anything else causes the line to be rejected</b>,
    /// including <c>permitopen="…"</c> and a <c>from=</c> entry that is not an address or CIDR (a
    /// hostname pattern or negation). An option is written in order to take access away, so a line
    /// cannot be honoured while quietly dropping part of it — that would grant the key more than the
    /// administrator wrote down. Same rule as a certificate's critical options.
    /// </para>
    /// </summary>
    public static class AuthorizedKeysFile
    {

        // Options that restrict a capability this implementation never offers, so there is nothing to
        // enforce and accepting them cannot widen access.
        private static readonly HashSet<String> VacuousOptions =
            new (StringComparer.OrdinalIgnoreCase) {
                "no-agent-forwarding", "no-x11-forwarding", "no-user-rc", "no-touch-required",
                "agent-forwarding",    "x11-forwarding",    "user-rc",    "touch-required"
            };

        // from= entries we can actually evaluate: a bare address or a CIDR block.
        private static Boolean TryParseCidr(String Entry, out IpCidr Cidr)
        {
            try
            {
                // A bare address means "exactly this host" — full-length prefix, per family.
                var text = Entry.Contains('/')
                               ? Entry
                               : Entry + (Entry.Contains(':') ? "/128" : "/32");
                Cidr = IpCidr.Parse(text);
                return true;
            }
            catch
            {
                Cidr = default;
                return false;
            }
        }

        #region Parse(Text)

        /// <summary>Parse the full text of an <c>authorized_keys</c> file.</summary>
        public static IReadOnlyList<AuthorizedKey> Parse(String Text)
        {

            var entries = new List<AuthorizedKey>();

            foreach (var rawLine in Text.Replace("\r\n", "\n").Split('\n'))
            {

                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (TryParseLine(line, out var entry))
                    entries.Add(entry!);

            }

            return entries;

        }

        #endregion

        #region TryParseLine(Line, out Entry)

        /// <summary>Parse a single <c>authorized_keys</c> line (options optional).</summary>
        public static Boolean TryParseLine(String Line, out AuthorizedKey? Entry)
        {

            Entry = null;

            var (first, rest) = SplitFirstToken(Line);

            // No options: the line starts directly with a key type.
            if (IsKeyType(first))
                return SshPublicKey.TryParse(Line, out var bareKey) && Build(bareKey!, [], out Entry);

            // Otherwise the first token is the option list and the rest is the key.
            if (!SshPublicKey.TryParse(rest, out var key))
                return false;

            return Build(key!, SplitOptions(first), out Entry);

        }

        #endregion


        #region (private) Build(Key, Options, out Entry)

        private static Boolean Build(SshPublicKey Key, IReadOnlyList<String> Options, out AuthorizedKey? Entry)
        {

            Entry = null;

            var isCa         = false;
            var principals   = new List<String>();
            DateTimeOffset?  notBefore  = null;
            DateTimeOffset?  notAfter   = null;
            String?          command    = null;
            List<IpCidr>?    sources    = null;
            var allowPty             = true;
            var allowPortForwarding  = true;

            foreach (var option in Options)
            {

                if (String.Equals(option, "cert-authority", StringComparison.OrdinalIgnoreCase))
                    isCa = true;

                else if (TryOptionValue(option, "principals", out var p))
                    principals.AddRange(p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                else if (TryOptionValue(option, "command", out var c))
                    command = c;

                else if (TryOptionValue(option, "from", out var f))
                {
                    // Only address/CIDR forms can be evaluated here. A hostname pattern or a negation
                    // would need matching we do not implement, and quietly ignoring it would grant the
                    // key more reach than the administrator wrote down — so the line is refused instead.
                    sources = [];
                    foreach (var entry in f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!TryParseCidr(entry, out var cidr))
                            return false;
                        sources.Add(cidr);
                    }
                }

                else if (TryOptionValue(option, "not-before", out var nb))
                    notBefore = AuthorizedKey.ParseTimestamp(nb);

                else if (TryOptionValue(option, "not-after", out var na) || TryOptionValue(option, "expiry-time", out na))
                    notAfter = AuthorizedKey.ParseTimestamp(na);

                else if (String.Equals(option, "restrict", StringComparison.OrdinalIgnoreCase))
                {
                    allowPty            = false;
                    allowPortForwarding = false;
                }

                else if (String.Equals(option, "no-pty", StringComparison.OrdinalIgnoreCase))
                    allowPty = false;

                else if (String.Equals(option, "no-port-forwarding", StringComparison.OrdinalIgnoreCase))
                    allowPortForwarding = false;

                else if (String.Equals(option, "pty", StringComparison.OrdinalIgnoreCase))
                    allowPty = true;                       // re-enables after `restrict`

                else if (String.Equals(option, "port-forwarding", StringComparison.OrdinalIgnoreCase))
                    allowPortForwarding = true;            // re-enables after `restrict`

                else if (VacuousOptions.Contains(option))
                    { /* restricts a feature this implementation does not offer at all */ }

                else
                    // An option we cannot enforce must not be silently dropped: it was written to take
                    // access away, so honouring the line without it would grant more than intended.
                    return false;

            }

            Entry = new AuthorizedKey(Key)
            {
                IsCertAuthority  = isCa,
                Principals       = principals,
                NotBefore        = notBefore,
                NotAfter         = notAfter,
                ForcedCommand    = command,
                Options          = Options,
                Restrictions     = new SshSessionRestrictions(command, sources, allowPty, allowPortForwarding)
            };

            return true;

        }

        #endregion

        #region (private) option / token parsing

        // Split "key=value" (value optionally double-quoted); returns false if the option isn't "name=…".
        private static Boolean TryOptionValue(String Option, String Name, out String Value)
        {

            Value = "";

            if (!Option.StartsWith(Name + "=", StringComparison.OrdinalIgnoreCase))
                return false;

            var raw = Option[(Name.Length + 1)..];
            Value   = raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"' ? raw[1..^1] : raw;
            return true;

        }

        // A known SSH key-type token (so the line has no options).
        private static Boolean IsKeyType(String Token)
            => Token.StartsWith("ssh-",       StringComparison.Ordinal) ||
               Token.StartsWith("ecdsa-",     StringComparison.Ordinal) ||
               Token.StartsWith("sk-",        StringComparison.Ordinal) ||
               Token.StartsWith("rsa-sha2-",  StringComparison.Ordinal);

        // Split off the first whitespace-delimited token, honouring double quotes; returns (first, rest).
        private static (String First, String Remainder) SplitFirstToken(String Line)
        {

            var inQuotes = false;
            for (var i = 0; i < Line.Length; i++)
            {
                var c = Line[i];
                if (c == '"')
                    inQuotes = !inQuotes;
                else if (!inQuotes && (c == ' ' || c == '\t'))
                    return (Line[..i], Line[(i + 1)..].TrimStart());
            }

            return (Line, "");

        }

        // Split a comma-separated option list, honouring double-quoted values (which may contain commas).
        private static List<String> SplitOptions(String OptionList)
        {

            var options   = new List<String>();
            var current   = new StringBuilder();
            var inQuotes  = false;

            foreach (var c in OptionList)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    if (current.Length > 0) { options.Add(current.ToString()); current.Clear(); }
                }
                else
                    current.Append(c);
            }

            if (current.Length > 0)
                options.Add(current.ToString());

            return options;

        }

        #endregion

    }

}
