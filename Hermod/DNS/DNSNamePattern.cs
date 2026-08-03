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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extension methods for DNS name patterns.
    /// </summary>
    public static class DNSNamePatternExtensions
    {

        /// <summary>
        /// Indicates whether this DNS name pattern is null or empty.
        /// </summary>
        /// <param name="DNSNamePattern">A DNS name pattern.</param>
        public static Boolean IsNullOrEmpty(this DNSNamePattern? DNSNamePattern)
            => DNSNamePattern?.FullName.IsNullOrEmpty() ?? true;

        /// <summary>
        /// Indicates whether this DNS name pattern is null or empty.
        /// </summary>
        /// <param name="DNSNamePattern">A DNS name pattern.</param>
        public static Boolean IsNotNullOrEmpty([NotNullWhen(true)] this DNSNamePattern? DNSNamePattern)
            => DNSNamePattern?.FullName.IsNotNullOrEmpty() ?? false;


        /// <summary>
        /// Whether any of these patterns matches the given host name.
        /// </summary>
        /// <param name="DNSNamePatterns">An enumeration of DNS name patterns.</param>
        /// <param name="HostName">The host name to match.</param>
        public static Boolean AnyMatches(this IEnumerable<DNSNamePattern>  DNSNamePatterns,
                                         DomainName                        HostName)

            => DNSNamePatterns.Any(pattern => pattern.Matches(HostName));

    }


    /// <summary>
    /// A DNS name as it may appear in a server certificate: either an exact name, or a name
    /// whose left-most label is the wildcard "*" — RFC 9525's DNS-ID, its "presented identifier".
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because <see cref="DomainName"/> is the wrong type for the job in two
    /// separate ways, and using it anyway is a defect that only shows up against a wildcard
    /// certificate.
    /// </para>
    /// <para>
    /// The first is simply that <c>DomainName.Parse("*.example.com")</c> throws: a wildcard is
    /// not a host name, and the strict parse is right to say so. Reading a certificate's names
    /// through it therefore fails on the certificates most likely to be in front of a real
    /// server.
    /// </para>
    /// <para>
    /// The second is subtler and matters more. <see cref="DomainName.ParseLenient"/> does accept
    /// a leading "*", because a DNS <em>owner name</em> read off the wire can be a wildcard
    /// (RFC 4592) — but a DNS wildcard and a certificate wildcard are different things with
    /// different matching rules. RFC 9525 § 6.3 is explicit about not confusing them: a
    /// certificate wildcard "can only match one label", where the DNS one "always matches at
    /// least one whole label and sometimes more". A type that carried both meanings would have
    /// to guess which was intended, and guessing wrong in the permissive direction means
    /// accepting a certificate for a host it was not issued for.
    /// </para>
    /// <para>
    /// So the rules here are RFC 9525's, and only those:
    /// </para>
    /// <list type="bullet">
    /// <item>There is at most one wildcard character.</item>
    /// <item>It appears only as the complete content of the left-most label.</item>
    /// <item>It matches exactly one label — never zero, never two.</item>
    /// <item>Comparison is case-insensitive ASCII, label by label (RFC 4343).</item>
    /// </list>
    /// <para>
    /// Anything else is what § 6.3 calls an invalid presented identifier, which "MUST be
    /// ignored" — hence <see cref="TryParse(String, out DNSNamePattern, out String)"/> as the
    /// intended entry point when reading a certificate. One bad entry disqualifies that entry,
    /// not the certificate.
    /// </para>
    /// <para>
    /// Two things are deliberately not done here. Internationalized names are compared as
    /// A-labels, per § 6.3 and § 7.3, so a caller holding a U-label must convert it before
    /// asking — this type does no IDNA processing and would silently compare the wrong strings
    /// if it pretended to. And nothing here knows about public suffixes: refusing "*.com" needs
    /// a suffix list, and § 7.1 puts that out of scope explicitly. <see cref="BaseName"/> is
    /// exposed so a caller who has such a list can apply it.
    /// </para>
    /// </remarks>
    public class DNSNamePattern : IEquatable<DNSNamePattern>,
                                  IComparable<DNSNamePattern>,
                                  IComparable
    {

        #region Properties

        /// <summary>
        /// The pattern as it appears in the certificate, without a trailing dot
        /// (e.g. "*.example.com" or "www.example.com").
        /// </summary>
        public String      FullName    { get; }

        /// <summary>
        /// Whether the left-most label is the wildcard "*".
        /// </summary>
        public Boolean     IsWildcard  { get; }

        /// <summary>
        /// The name this pattern is anchored to: everything below the wildcard label
        /// ("example.com" for "*.example.com"), or the whole name when there is no wildcard.
        /// </summary>
        /// <remarks>
        /// A real <see cref="DomainName"/>, because it is one — the wildcard label is the only
        /// part that was never a host name. This is also what a caller needs in order to apply a
        /// public suffix list, which RFC 9525 § 7.1 leaves to them.
        /// </remarks>
        public DomainName  BaseName    { get; }

        #endregion

        #region Constructor(s)

        private DNSNamePattern(String      FullName,
                               Boolean     IsWildcard,
                               DomainName  BaseName)
        {

            this.FullName    = FullName;
            this.IsWildcard  = IsWildcard;
            this.BaseName    = BaseName;

        }

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given text as a certificate DNS name.
        /// </summary>
        /// <param name="Text">The text representation of a DNS name pattern.</param>
        public static DNSNamePattern Parse(String Text)
        {

            if (TryParse(Text, out var pattern, out var errorResponse))
                return pattern;

            throw new ArgumentException($"Invalid text representation of a DNS name pattern: '{Text}': {errorResponse}",
                                        nameof(Text));

        }

        #endregion

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a certificate DNS name.
        /// </summary>
        /// <param name="Text">The text representation of a DNS name pattern.</param>
        public static DNSNamePattern? TryParse(String Text)
        {

            if (TryParse(Text, out var pattern, out _))
                return pattern;

            return null;

        }

        #endregion

        #region TryParse (Text, out DNSNamePattern, out ErrorResponse)

        /// <summary>
        /// Try to parse the given text as a certificate DNS name (RFC 9525 § 6.3).
        /// </summary>
        /// <param name="Text">The text representation of a DNS name pattern.</param>
        /// <param name="DNSNamePattern">The parsed DNS name pattern.</param>
        /// <param name="ErrorResponse">An error response in case the parsing fails.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)]  out DNSNamePattern?  DNSNamePattern,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            DNSNamePattern  = null;
            ErrorResponse   = null;

            var text = Text?.Trim() ?? "";

            if (text.IsNullOrEmpty())
            {
                ErrorResponse = "The given DNS name pattern must not be null or empty!";
                return false;
            }

            // A bare "*" meets the letter of both § 6.3 requirements — it is one wildcard
            // character and it is the complete content of the left-most label — and is refused
            // anyway: it would vouch for every single-label name there is, "localhost" included,
            // and no certificate authority issues one. RFC 9525 § 3 permits an application to be
            // stricter about wildcards than the document is, and there is nothing here worth
            // being less strict for.
            //
            // Checked before the wildcard label is recognized, because "*" has no "*." prefix
            // and would otherwise fall through to the rule below and be reported as a badly
            // placed wildcard, which it is not.
            if (text == "*")
            {
                ErrorResponse = "A wildcard needs a domain to be a wildcard of.";
                return false;
            }

            var isWildcard  = text.StartsWith("*.", StringComparison.Ordinal);
            var baseText    = isWildcard
                                  ? text[2..]
                                  : text;

            // § 6.3, requirements 1 and 2: one wildcard character, and only as the complete
            // content of the left-most label. Any asterisk left after a well-formed wildcard
            // label has been taken off the front breaks one or the other — "*.*.example.com",
            // "www.*.example.com" and "w*.example.com" alike. The partial forms are the ones
            // worth naming: RFC 6125 tolerated "f*.example.com" as a SHOULD NOT, implementations
            // disagreed about what it covered, and RFC 9525 settled it by making such an
            // identifier invalid outright.
            //
            // These two checks are about the error message and nothing else. DomainName's own
            // parse below rejects every one of these cases already, since an asterisk is not a
            // legal character in a label and an empty name is not a name — but it rejects them
            // as "does not match the required format", which tells a reader looking at a
            // certificate nothing about which rule they fell foul of.
            if (baseText.Contains('*'))
            {
                ErrorResponse = "A wildcard may appear only once, and only as the complete " +
                                "content of the left-most label (RFC 9525 § 6.3).";
                return false;
            }

            // What remains below the wildcard is an ordinary host name, so DomainName decides
            // whether it is one — the label lengths, the total length, the hyphen rules and the
            // character set all live there and are not worth a second, divergent copy.
            if (!DomainName.TryParse(baseText, out var baseName, out var domainNameError))
            {
                ErrorResponse = domainNameError;
                return false;
            }

            DNSNamePattern = new DNSNamePattern(
                                 isWildcard
                                     ? $"*.{baseName.Trimmed}"
                                     : baseName.Trimmed,
                                 isWildcard,
                                 baseName
                             );

            return true;

        }

        #endregion

        #region ParseAll (Texts)

        /// <summary>
        /// Parse those of the given names that are valid presented identifiers, and silently
        /// drop the rest.
        /// </summary>
        /// <remarks>
        /// The shape RFC 9525 § 6.3 asks for when reading a certificate: an invalid presented
        /// identifier "MUST be ignored", and a certificate carrying one alongside good names is
        /// still usable through the good ones.
        /// </remarks>
        /// <param name="Texts">An enumeration of text representations of DNS name patterns.</param>
        public static IEnumerable<DNSNamePattern> ParseAll(IEnumerable<String> Texts)
        {

            foreach (var text in Texts)
            {
                if (TryParse(text, out var pattern, out _))
                    yield return pattern;
            }

        }

        #endregion


        #region Matches(HostName)

        /// <summary>
        /// Whether this pattern matches the given host name — RFC 9525 § 6.3's comparison of a
        /// presented identifier against a reference identifier.
        /// </summary>
        /// <remarks>
        /// The wildcard matches exactly one label. "*.example.com" therefore covers
        /// "www.example.com" and neither "example.com" — there is no label for the wildcard to
        /// stand for — nor "a.b.example.com", where there are two. § 7.1 puts it plainly:
        /// wildcard certificates "automatically vouch for any single-label hostnames within
        /// their domain, but not multiple levels of domains".
        /// </remarks>
        /// <param name="HostName">The host name this client set out to reach.</param>
        public Boolean Matches(DomainName? HostName)
        {

            if (HostName is null)
                return false;

            // RFC 4343 and § 6.3: names compare without regard to case. DomainName's own
            // equality already does, which is why the exact case defers to it rather than
            // repeating the rule.
            if (!IsWildcard)
                return BaseName.Equals(HostName);

            var hostLabels  = HostName.Labels;
            var baseLabels  = BaseName.Labels;

            // Exactly one label above the anchor: the one the wildcard stands for.
            if (hostLabels.Count != baseLabels.Count + 1)
                return false;

            // And that label must be something. A host name cannot have an empty left-most
            // label, but the check costs nothing and this is the wrong place to find out.
            if (hostLabels[0].IsNullOrEmpty())
                return false;

            for (var i = 0; i < baseLabels.Count; i++)
            {
                if (!String.Equals(hostLabels[i + 1],
                                   baseLabels[i],
                                   StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;

        }

        #endregion

        #region Matches(HostName)

        /// <summary>
        /// Whether this pattern matches the given host name.
        /// </summary>
        /// <remarks>
        /// A host name that is not a host name matches nothing. The reference identifier a
        /// client checks against is the name it set out to reach, so text that cannot be a
        /// domain name at all is a caller's mistake rather than a match to be attempted.
        /// </remarks>
        /// <param name="HostName">The host name this client set out to reach.</param>
        public Boolean Matches(String? HostName)

            => HostName is not null &&
               DomainName.TryParse(HostName, out var hostName, out _) &&
               Matches(hostName);

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this DNS name pattern.
        /// </summary>
        public DNSNamePattern Clone()

            => new (FullName.CloneString(),
                    IsWildcard,
                    BaseName.Clone());

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two DNS name patterns for equality.
        /// </summary>
        public static Boolean operator == (DNSNamePattern DNSNamePattern1,
                                           DNSNamePattern DNSNamePattern2)

            => DNSNamePattern1.Equals(DNSNamePattern2);

        /// <summary>
        /// Compares two DNS name patterns for inequality.
        /// </summary>
        public static Boolean operator != (DNSNamePattern DNSNamePattern1,
                                           DNSNamePattern DNSNamePattern2)

            => !DNSNamePattern1.Equals(DNSNamePattern2);

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        public static Boolean operator < (DNSNamePattern DNSNamePattern1,
                                          DNSNamePattern DNSNamePattern2)

            => DNSNamePattern1.CompareTo(DNSNamePattern2) < 0;

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        public static Boolean operator <= (DNSNamePattern DNSNamePattern1,
                                           DNSNamePattern DNSNamePattern2)

            => DNSNamePattern1.CompareTo(DNSNamePattern2) <= 0;

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        public static Boolean operator > (DNSNamePattern DNSNamePattern1,
                                          DNSNamePattern DNSNamePattern2)

            => DNSNamePattern1.CompareTo(DNSNamePattern2) > 0;

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        public static Boolean operator >= (DNSNamePattern DNSNamePattern1,
                                           DNSNamePattern DNSNamePattern2)

            => DNSNamePattern1.CompareTo(DNSNamePattern2) >= 0;

        #endregion

        #region IComparable<DNSNamePattern> Members

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        /// <param name="Object">A DNS name pattern to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DNSNamePattern pattern
                   ? CompareTo(pattern)
                   : throw new ArgumentException("The given object is not a DNS name pattern!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two DNS name patterns.
        /// </summary>
        /// <param name="DNSNamePattern">A DNS name pattern to compare with.</param>
        public Int32 CompareTo(DNSNamePattern? DNSNamePattern)
        {

            if (DNSNamePattern is null)
                throw new ArgumentNullException(nameof(DNSNamePattern), "The given DNS name pattern must not be null!");

            return String.Compare(FullName,
                                  DNSNamePattern.FullName,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #region IEquatable<DNSNamePattern> Members

        /// <summary>
        /// Compares two DNS name patterns for equality.
        /// </summary>
        /// <param name="Object">A DNS name pattern to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSNamePattern pattern &&
                   Equals(pattern);

        /// <summary>
        /// Compares two DNS name patterns for equality.
        /// </summary>
        /// <remarks>
        /// Two patterns are the same pattern, not two patterns that cover the same names —
        /// "*.example.com" and "www.example.com" are different however much they overlap. Use
        /// <see cref="Matches(DomainName)"/> to ask the other question.
        /// </remarks>
        /// <param name="DNSNamePattern">A DNS name pattern to compare with.</param>
        public Boolean Equals(DNSNamePattern? DNSNamePattern)

            => DNSNamePattern is not null &&

               String.Equals(FullName,
                             DNSNamePattern.FullName,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => FullName.GetHashCode(StringComparison.OrdinalIgnoreCase);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => FullName;

        #endregion

    }

}
