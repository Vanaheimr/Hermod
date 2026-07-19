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
using System.Reflection;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The Mozilla Public Suffix List (https://publicsuffix.org/), used to determine the
    /// "organizational domain" (registrable domain) of a hostname. This is what DMARC
    /// relaxed identifier alignment (RFC 7489 §3.2) compares, so that e.g.
    /// "mail.example.co.uk" and "example.co.uk" are recognised as the same organization.
    ///
    /// The list snapshot is embedded (DNSVerifications/public_suffix_list.dat) and parsed
    /// once. The matching algorithm follows https://publicsuffix.org/list/ :
    ///   * the prevailing rule is the matching rule with the most labels;
    ///   * an exception rule (!) always wins and its public suffix is the rule minus its
    ///     left-most label;
    ///   * a wildcard label (*) matches exactly one label;
    ///   * if no rule matches, the prevailing rule is "*" (the public suffix is the TLD).
    /// The registrable domain is the public suffix plus one additional label to its left.
    ///
    /// Both the list entries and the queried names are normalised to A-labels (punycode,
    /// lower-case) so that the list's mixed U-label/A-label IDN entries match consistently.
    /// </summary>
    public static class PublicSuffixList
    {

        #region Data

        // Exact rules (e.g. "com", "co.uk", "uk.com"), stored as normalised A-label strings.
        private static readonly HashSet<String> _rules      = new (StringComparer.Ordinal);

        // Wildcard rules "*.x.y" stored by their parent "x.y" (the labels after the '*').
        private static readonly HashSet<String> _wildcards  = new (StringComparer.Ordinal);

        // Exception rules "!x.y.z" stored as the body "x.y.z".
        private static readonly HashSet<String> _exceptions = new (StringComparer.Ordinal);

        static PublicSuffixList()
        {
            Load();
        }

        private static void Load()
        {

            var asm          = typeof(PublicSuffixList).Assembly;
            var resourceName = Array.Find(asm.GetManifestResourceNames(),
                                          n => n.EndsWith("public_suffix_list.dat", StringComparison.Ordinal));

            if (resourceName is null)
                return;   // no list embedded => callers fall back to the naive heuristic

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
                return;

            using var reader = new StreamReader(stream);

            String? line;
            while ((line = reader.ReadLine()) is not null)
            {

                line = line.Trim();

                // Skip blank lines and comments (section markers "// ===..." are comments too).
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line[0] == '!')
                {
                    var body = NormalizeName(line[1..]);
                    if (body.Length > 0)
                        _exceptions.Add(body);
                }
                else if (line.StartsWith("*.", StringComparison.Ordinal))
                {
                    var parent = NormalizeName(line[2..]);
                    if (parent.Length > 0)
                        _wildcards.Add(parent);
                }
                else
                {
                    var rule = NormalizeName(line);
                    if (rule.Length > 0)
                        _rules.Add(rule);
                }

            }

        }

        #endregion

        #region GetRegistrableDomain(hostname)

        /// <summary>
        /// Return the registrable / organizational domain of <paramref name="hostname"/>
        /// (public suffix + one label), or null if the name is itself a public suffix or
        /// cannot be parsed.
        /// </summary>
        public static String? GetRegistrableDomain(String? hostname)
        {

            if (String.IsNullOrEmpty(hostname))
                return null;

            var name = NormalizeName(hostname);
            if (name.Length == 0)
                return null;

            var labels = name.Split('.');
            var n      = labels.Length;

            // A leading dot or a doubled dot yields an empty label => not a valid name.
            if (Array.Exists(labels, l => l.Length == 0))
                return null;

            // --- exception rules win outright; public suffix = rule minus its left-most label ---
            var bestExceptionLabels = 0;
            for (var k = 1; k <= n; k++)
            {
                var suffix = Join(labels, n - k, k);
                if (_exceptions.Contains(suffix) && k > bestExceptionLabels)
                    bestExceptionLabels = k;
            }

            int publicSuffixLabels;
            if (bestExceptionLabels > 0)
            {
                publicSuffixLabels = bestExceptionLabels - 1;
            }
            else
            {
                // default rule "*" => the public suffix is the right-most label (TLD).
                var best = 1;
                for (var k = 1; k <= n; k++)
                {
                    var suffix = Join(labels, n - k, k);
                    if (_rules.Contains(suffix) && k > best)
                        best = k;

                    // wildcard "*.parent" matches when the parent (right-most k-1 labels) is listed.
                    if (k >= 2)
                    {
                        var parent = Join(labels, n - k + 1, k - 1);
                        if (_wildcards.Contains(parent) && k > best)
                            best = k;
                    }
                }
                publicSuffixLabels = best;
            }

            // The name is (or is shorter than) a public suffix => no registrable domain.
            if (n <= publicSuffixLabels)
                return null;

            return Join(labels, n - publicSuffixLabels - 1, publicSuffixLabels + 1);

        }

        #endregion

        #region GetOrganizationalDomain(hostname)

        /// <summary>
        /// The organizational domain as used by DMARC. Same as the registrable domain, but
        /// falls back to the input (lower-cased, trailing dot stripped) when the name is
        /// itself a public suffix or has no registrable part, so callers always get a value.
        /// </summary>
        public static String GetOrganizationalDomain(String hostname)
            => GetRegistrableDomain(hostname)
                   ?? (hostname ?? "").Trim().TrimEnd('.').ToLowerInvariant();

        #endregion

        #region (private) helpers

        // Lower-case, strip a trailing dot, and convert to A-labels (punycode) so that the
        // list's U-label IDN entries and punycode input compare equal. Invalid IDN input
        // falls back to the lower-cased raw form.
        private static String NormalizeName(String name)
        {

            name = name.Trim().TrimEnd('.').ToLowerInvariant();
            if (name.Length == 0)
                return name;

            if (!IsAscii(name))
            {
                try
                {
                    name = _idn.GetAscii(name);
                }
                catch
                {
                    // keep the lower-cased raw form
                }
            }

            return name;

        }

        private static readonly IdnMapping _idn = new () { AllowUnassigned = true, UseStd3AsciiRules = false };

        private static Boolean IsAscii(String s)
        {
            foreach (var c in s)
                if (c > 0x7F)
                    return false;
            return true;
        }

        private static String Join(String[] labels, Int32 start, Int32 count)
            => String.Join('.', labels, start, count);

        #endregion

    }

}
