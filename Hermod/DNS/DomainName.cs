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

using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extension methods for domain names.
    /// </summary>
    public static class DomainNameExtensions
    {

        /// <summary>
        /// Indicates whether this domain name is null or empty.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static Boolean IsNullOrEmpty(this DomainName? DomainName)
            => DomainName?.FullName.IsNullOrEmpty() ?? true;

        /// <summary>
        /// Indicates whether this domain name is null or empty.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static Boolean IsNotNullOrEmpty([NotNullWhen(true)] this DomainName? DomainName)
            => DomainName?.FullName.IsNotNullOrEmpty() ?? false;

    }

    /// <summary>
    /// A domain name (RFC 1035).
    /// </summary>
    public class DomainName : IDomainName,
                              IEquatable<DomainName>,
                              IComparable<DomainName>,
                              IComparable
    {

        #region Data

        public static readonly Regex DomainNameRegExpr = new Regex(
                                                             @"^(?=.{1,254}$)" +                                            // max. 254 Zeichen gesamt inkl. Punkt
                                                             @"(?:[A-Za-z0-9]" +                                            // erstes Label: beginnt mit Buchst./Ziffer
                                                             @"(?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?" +                        // optional mittlere Zeichen, endet mit Buchst./Ziffer
                                                             @")" +
                                                             @"(?:\.(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?))*" +  // 0…n weitere Labels
                                                             @"\.?$",                                                       // optional ein abschließender Punkt
                                                             RegexOptions.IgnoreCase |
                                                             RegexOptions.Compiled   |
                                                             RegexOptions.CultureInvariant
                                                         );

        /// <summary>
        /// Like <see cref="DomainNameRegExpr"/>, but additionally allows the underscore ('_')
        /// within labels. Resource record owner names read from the wire may legitimately
        /// contain underscore labels (e.g. "_dmarc", "_domainkey", "_25._tcp"), so those must
        /// be accepted when parsing a DNS response even though a hostname never has them.
        /// </summary>
        public static readonly Regex DomainNameWithUnderscoresRegExpr = new Regex(
                                                             @"^(?=.{1,254}$)" +
                                                             @"(?:[A-Za-z0-9_]" +
                                                             @"(?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?" +
                                                             @")" +
                                                             @"(?:\.(?:[A-Za-z0-9_](?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?))*" +
                                                             @"\.?$",
                                                             RegexOptions.IgnoreCase |
                                                             RegexOptions.Compiled   |
                                                             RegexOptions.CultureInvariant
                                                         );

        private readonly String[]  labels;

        #endregion

        #region Properties

        /// <summary>
        /// The full name of this domain.
        /// </summary>
        public String                 FullName    { get; }

        /// <summary>
        /// The full name of this domain, without the trailing dot
        /// (e.g. "www.example.com" for "www.example.com.").
        /// </summary>
        public String                 Trimmed
            => FullName.EndsWith('.')
                   ? FullName[..^1]
                   : FullName;

        /// <summary>
        /// The short name of this domain, i.e. the first label
        /// (e.g. "www" for "www.example.com.")
        /// </summary>
        public String                 ShortName
            => FullName.Substring(0, FullName.IndexOf('.'));

        /// <summary>
        /// The labels of this domain.
        /// </summary>
        public IReadOnlyList<String>  Labels
            => labels.AsReadOnly();


        /// <summary>
        /// The parent domain of this domain,
        /// i.e. all labels except the first one.
        /// </summary>
        public DomainName?            ParentDomain

            => labels.Length > 1
                   ? new DomainName(
                         [.. labels.Skip(1)]
                     )
                   : null;

        /// <summary>
        /// The top-level domain of this domain, i.e. the last label.
        /// </summary>
        public String                 TopLevelDomain

            => labels.Last();

        /// <summary>
        /// The top-level domain of this domain, i.e. the last label.
        /// </summary>
        public DomainName             TopLevelDomainName

            => new (labels.Last());

        /// <summary>
        /// The second-level domain of this domain, i.e. the second to last label.
        /// </summary>
        public String                 SecondLevelDomain

            => labels.Length >= 2
                   ? labels[^2]
                   : String.Empty;

        /// <summary>
        /// The second-level domain of this domain, i.e. the second to last label.
        /// </summary>
        public DomainName?            SecondLevelDomainName

            => labels.Length >= 2
                   ? new DomainName(
                         [.. labels.TakeLast(2)]
                     )
                   : null;

        /// <summary>
        /// The localhost domain name.
        /// </summary>
        public static DomainName      Localhost

            => new ("localhost");

        /// <summary>
        /// The loopback domain name.
        /// </summary>
        public static DomainName      Loopback

            => new ("loopback");

        /// <summary>
        /// The empty domain name (root domain).
        /// </summary>
        public static DomainName      Empty

            => new ([]);

        /// <summary>
        /// The wildcard domain name.
        /// </summary>
        public static DomainName      Any

            => new ("*");

        #endregion

        #region Constructor(s)

        protected DomainName(String DomainName)
        {

            this.FullName  = DomainName;
            this.labels    = DomainName.TrimEnd('.').Split('.');

        }

        protected DomainName(params String[] DomainLabels)
        {

            this.FullName  = DomainLabels.AggregateWith('.') + ".";
            this.labels    = DomainLabels;

        }

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given text as domain name.
        /// </summary>
        /// <param name="Text">The text representation of a domain name.</param>
        public static DomainName Parse(String Text)
        {

            if (TryParse(Text, out var domainName, out var errorResponse))
                return domainName;

            throw new ArgumentException($"Invalid text representation of a domain name: '{Text}': {errorResponse}",
                                        nameof(Text));

        }

        #endregion

        #region ParseLenient(Text)

        /// <summary>
        /// Parse the given text as a domain name, additionally tolerating underscore ('_')
        /// labels. Intended for resource record owner names read from a DNS response, which
        /// may legitimately be underscore names (e.g. "_dmarc.example.com").
        /// </summary>
        /// <param name="Text">The text representation of a domain name.</param>
        public static DomainName ParseLenient(String Text)
        {

            if (TryParse(Text, out var domainName, out var errorResponse, AllowUnderscoreLabels: true))
                return domainName;

            throw new ArgumentException($"Invalid text representation of a domain name: '{Text}': {errorResponse}",
                                        nameof(Text));

        }

        #endregion

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as domain name.
        /// </summary>
        /// <param name="Text">The text representation of a domain name.</param>
        public static DomainName? TryParse(String Text)
        {

            if (TryParse(Text, out var domainName, out var errorResponse))
                return domainName;

            return null;

        }

        #endregion

        #region TryParse(Text, out DomainName, out ErrorResponse)

        /// <summary>
        /// Parse the given string as a domain name (RFC 1035).
        /// </summary>
        /// <param name="Text">The text representation of a domain name.</param>
        /// <param name="DomainName">The parsed domain name.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)]  out DomainName?  DomainName,
                                       [NotNullWhen(false)] out String?      ErrorResponse)

            => TryParse(Text,
                        out DomainName,
                        out ErrorResponse,
                        false);

        #endregion

        #region (private) TryParse(Text, out DomainName, out ErrorResponse, AllowUnderscoreLabels)

        /// <summary>
        /// Parse the given string as a domain name (RFC 1035), optionally tolerating underscore
        /// labels for resource record owner names read from the wire (e.g. "_dmarc.example.com").
        /// </summary>
        /// <param name="Text">The text representation of a domain name.</param>
        /// <param name="DomainName">The parsed domain name.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        /// <param name="AllowUnderscoreLabels">Whether to tolerate underscore ('_') labels.</param>
        private static Boolean TryParse(String                                Text,
                                        [NotNullWhen(true)]  out DomainName?  DomainName,
                                        [NotNullWhen(false)] out String?      ErrorResponse,
                                        Boolean                               AllowUnderscoreLabels)
        {

            DomainName     = null;
            ErrorResponse  = null;

            // RFC 1035 §2.3.3: "When you receive a domain name or label, you should
            // preserve its case." The case of a name is therefore carried through
            // unchanged; every comparison below (and in Equals/CompareTo/GetHashCode)
            // is case-insensitive instead, per RFC 4343. Normalizing here would also
            // make dns-0x20 query randomization impossible to build on top of this type.
            Text = Text?.Trim() ?? "";

            if (Text.IsNullOrEmpty())
            {
                ErrorResponse = "The given domain name must not be null or empty!";
                return false;
            }

            if (!Text.EndsWith('.'))
                Text += ".";

            if (Text.Length > 255)
            {
                ErrorResponse = "The given domain name exceeds maximum length of 255 characters!";
                return false;
            }

            if (Text != ".")
            {
                var regExpr = AllowUnderscoreLabels
                                  ? DomainNameWithUnderscoresRegExpr
                                  : DomainNameRegExpr;

                if (!regExpr.IsMatch(Text))
                {
                    ErrorResponse = "The given domain name does not match the required format!";
                    return false;
                }
            }

            var labels = Text.TrimEnd('.').Split('.');
            foreach (var label in labels)
            {

                if (label.Length > 63)
                {
                    ErrorResponse = $"Each label in the domain name must not exceed 63 characters: '{label}'!";
                    return false;
                }

                if (label.StartsWith('-') || label.EndsWith('-'))
                {
                    ErrorResponse = $"Each label in the domain name must not start or end with a hyphen: '{label}'!";
                    return false;
                }

            }

            DomainName = new DomainName(Text);
            return true;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this domain name.
        /// </summary>
        public DomainName Clone()

            => new(
                   FullName.CloneString()
               );

        #endregion



        // Check if this is a subdomain of another domain
        public Boolean IsSubdomainOf(DomainName other)
        {

            if (other is null)
                return false;

            return FullName.EndsWith(other.FullName, StringComparison.OrdinalIgnoreCase);

        }


        public void Serialize(Stream                      Stream,
                              Int32                       CurrentOffset,
                              Boolean                     UseCompression   = true,
                              Dictionary<String, Int32>?  Offsets          = null)
        {

            Offsets ??= [];

            // Compression keys are case-folded, because RFC 4343 makes names that differ
            // only in case the same name: "MiXeD.example." may legitimately be compressed
            // against an earlier "mixed.example.". Folding the key here rather than relying
            // on the dictionary's comparer keeps this correct no matter how the caller
            // constructed it. Only the key is folded — the labels written below keep their
            // original case, which is what preserves it on the wire (RFC 1035 §2.3.3).
            var compressionKey = FullName.ToLowerInvariant();

            // Root domain. A name parsed from "." (or "") has no real labels; depending on the
            // parse path it is represented either as an empty label set or as a single empty
            // label. Both must serialize to just the terminating zero byte — emitting a zero-length
            // label followed by the terminator would put two 0x00 bytes on the wire and corrupt the
            // packet (observed as FORMERR on root DNSKEY/DS queries).
            if (Labels.Count == 0 || Labels.All(label => label.Length == 0))
            {
                Stream.WriteByte(0x00);
                return;
            }

            // Check for compression
            if (UseCompression && Offsets.TryGetValue(compressionKey, out var pointerOffset))
            {
                // Pointer: 0xC0 | (offset >> 8), then low byte
                UInt16 pointer = (UInt16)(0xC000 | pointerOffset);
                Stream.WriteByte((Byte) (pointer >>    8));
                Stream.WriteByte((Byte) (pointer &  0xFF));
                return;
            }

            // Every suffix of this name is itself a name, so record where each one starts:
            // a later "example.com." can then point at the tail of an earlier
            // "www.example.com.". The running offset must advance label by label — measuring
            // every suffix from CurrentOffset only happens to be right for the first one.
            var offset = CurrentOffset;

            for (var i = 0; i < labels.Length; i++)
            {

                var labelBytes = Encoding.ASCII.GetBytes(labels[i]);
                if (labelBytes.Length > 63)
                    throw new ArgumentException("Label too long");

                // RFC 1035 §4.1.4: a pointer carries a 14-bit offset, so anything at or
                // beyond 16384 can never be pointed at. Recording it would emit a pointer
                // with the high bits silently truncated, corrupting the message.
                var suffixKey = String.Join('.', labels.Skip(i)).ToLowerInvariant() + ".";
                if (offset <= 0x3FFF && !Offsets.ContainsKey(suffixKey))
                    Offsets[suffixKey] = offset;

                Stream.WriteByte((Byte) labelBytes.Length);
                Stream.Write    (labelBytes, 0, labelBytes.Length);

                offset += 1 + labelBytes.Length;

            }

            // End of name
            Stream.WriteByte(0x00);

        }


        #region Operator overloading

        #region Operator == (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DomainName DomainName1,
                                           DomainName DomainName2)

            => DomainName1.Equals(DomainName2);

        #endregion

        #region Operator == (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DomainName DomainName1,
                                           String     DomainName2)

            => DomainName1.FullName.Equals(DomainName2, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Operator != (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DomainName DomainName1,
                                           DomainName DomainName2)

            => !DomainName1.Equals(DomainName2);

        #endregion

        #region Operator != (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DomainName DomainName1,
                                           String     DomainName2)

            => !DomainName1.FullName.Equals(DomainName2, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Operator <  (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DomainName DomainName1,
                                          DomainName DomainName2)

            => DomainName1.CompareTo(DomainName2) < 0;

        #endregion

        #region Operator <= (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DomainName DomainName1,
                                           DomainName DomainName2)

            => DomainName1.CompareTo(DomainName2) <= 0;

        #endregion

        #region Operator >  (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DomainName DomainName1,
                                          DomainName DomainName2)

            => DomainName1.CompareTo(DomainName2) > 0;

        #endregion

        #region Operator >= (DomainName1, DomainName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DomainName1">A domain name.</param>
        /// <param name="DomainName2">Another domain name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DomainName DomainName1,
                                           DomainName DomainName2)

            => DomainName1.CompareTo(DomainName2) >= 0;

        #endregion

        #endregion

        #region IComparable<DomainName> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two domain names.
        /// </summary>
        /// <param name="Object">A domain name to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DomainName domainName
                   ? CompareTo(domainName)
                   : throw new ArgumentException("The given object is not a domain name!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DomainName)

        /// <summary>
        /// Compares two domain names.
        /// </summary>
        /// <param name="DomainName">A domain name to compare with.</param>
        public Int32 CompareTo(DomainName? DomainName)
        {

            if (DomainName is null)
                throw new ArgumentNullException(nameof(DomainName), "The given domain name must not be null!");

            // RFC 4343: domain names compare without regard to case.
            return String.Compare(FullName,
                                  DomainName.FullName,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #endregion

        #region IEquatable<DomainName> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two domain names for equality.
        /// </summary>
        /// <param name="Object">A domain name to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DomainName domainName &&
                   Equals(domainName);

        #endregion

        #region Equals(DomainName)

        /// <summary>
        /// Compares two domain names for equality.
        /// </summary>
        /// <param name="DomainName">A domain name to compare with.</param>
        public Boolean Equals(DomainName? DomainName)

            => DomainName is not null &&

               // RFC 4343: "example.com" and "EXAMPLE.COM" are the same name. This must
               // agree with GetHashCode(), which has always hashed case-insensitively.
               String.Equals(FullName,
                             DomainName.FullName,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
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
