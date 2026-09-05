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
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extension methods for DNS services.
    /// </summary>
    public static class DNSServiceExtensions
    {

        /// <summary>
        /// Indicates whether this DNS service is null or empty.
        /// </summary>
        /// <param name="DNSService">A DNS service.</param>
        public static Boolean IsNullOrEmpty(this DNSServiceName? DNSService)
            => DNSService?.FullName.IsNullOrEmpty() ?? true;

        /// <summary>
        /// Indicates whether this DNS service is null or empty.
        /// </summary>
        /// <param name="DNSService">A DNS service.</param>
        public static Boolean IsNotNullOrEmpty([NotNullWhen(true)] this DNSServiceName? DNSService)
            => DNSService?.FullName.IsNotNullOrEmpty() ?? false;

    }

    /// <summary>
    /// DNS service DNS services are used to identify services in the Domain Name System (DNS).
    /// It is a domain name that is used to specify a service provided by a server.
    /// In contrast to domain names it allows "_" as the first character of a label,
    /// </summary>
    public class DNSServiceName : IDomainName,
                                  IEquatable<DNSServiceName>,
                                  IComparable<DNSServiceName>,
                                  IComparable
    {

        #region Data

        /// <summary>
        /// Checks the lexical presentation syntax. Use <see cref="TryParse(String, out DNSServiceName, out String)"/>
        /// for authoritative UTF-8 label and wire-length validation.
        /// </summary>
        public static readonly Regex DNSServiceNameRegExpr  = new(
                                                                   @"^(?:\.|(?:\\.|[^.\\])+(?:\.(?:\\.|[^.\\])+)*\.?)$",
                                                                   RegexOptions.Compiled |
                                                                   RegexOptions.CultureInvariant
                                                               );

        #endregion

        #region Properties

        public String                 FullName    { get; }


        private readonly String[] labels;
        public IReadOnlyList<String>  Labels
            => labels.AsReadOnly();

        #endregion

        #region Constructor(s)

        protected DNSServiceName(String DNSService)
        {

            if (!TryParseLabels(DNSService, out var parsedLabels, out var errorResponse))
                throw new ArgumentException(errorResponse, nameof(DNSService));

            this.labels    = parsedLabels;
            this.FullName  = ToPresentationName(parsedLabels);

        }

        protected DNSServiceName(params String[] DomainLabels)
        {

            if (!TryValidateLabels(DomainLabels, out var errorResponse))
                throw new ArgumentException(errorResponse, nameof(DomainLabels));

            this.labels    = [.. DomainLabels];
            this.FullName  = ToPresentationName(labels);

        }

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given text as DNS service.
        /// </summary>
        /// <param name="Text">The text representation of a DNS service.</param>
        public static DNSServiceName Parse(String Text)
        {

            if (TryParse(Text, out var dnsServiceName, out var errorResponse))
                return dnsServiceName;

            throw new ArgumentException($"Invalid text representation of a DNS service name: '{Text}': {errorResponse}",
                                        nameof(Text));

        }

        #endregion

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as DNS service.
        /// </summary>
        /// <param name="Text">The text representation of a DNS service.</param>
        public static DNSServiceName? TryParse(String Text)
        {

            if (TryParse(Text, out var dnsServiceName, out _))
                return dnsServiceName;

            return null;

        }

        #endregion

        #region TryParse (Text, out DNSService, out ErrorResponse)

        /// <summary>
        /// Parse the given string as a DNS service (RFC 1035).
        /// </summary>
        /// <param name="Text">The text representation of a DNS service.</param>
        /// <param name="DNSService">The parsed DNS service.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)]  out DNSServiceName?  DNSService,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            DNSService = null;

            if (!TryParseLabels(Text, out var parsedLabels, out ErrorResponse))
                return false;

            DNSService = new DNSServiceName(parsedLabels);
            return true;

        }

        #endregion

        #region DNS label presentation helpers

        private static readonly UTF8Encoding strictUTF8 = new(false, true);

        internal static Boolean TryParseLabels(String?                              Text,
                                               [NotNullWhen(true)]  out String[]?   Labels,
                                               [NotNullWhen(false)] out String?      ErrorResponse)
        {

            Labels         = null;
            ErrorResponse  = null;

            if (String.IsNullOrEmpty(Text))
            {
                ErrorResponse = "The given DNS service must not be null or empty!";
                return false;
            }

            if (Text == ".")
            {
                Labels = [];
                return true;
            }

            var hasRootTerminator = Text[^1] == '.' && !IsEscaped(Text, Text.Length - 1);
            var presentation      = hasRootTerminator ? Text[..^1] : Text;
            var parsedLabels      = new List<String>();
            var label             = new StringBuilder();

            for (var i = 0; i < presentation.Length; i++)
            {

                var character = presentation[i];

                if (character == '\\')
                {
                    if (++i >= presentation.Length)
                    {
                        ErrorResponse = "The DNS service name ends with an incomplete escape sequence!";
                        return false;
                    }

                    label.Append(presentation[i]);
                    continue;
                }

                if (character == '.')
                {
                    if (label.Length == 0)
                    {
                        ErrorResponse = "The DNS service name contains an empty label!";
                        return false;
                    }

                    parsedLabels.Add(label.ToString());
                    label.Clear();
                    continue;
                }

                label.Append(character);

            }

            if (label.Length == 0)
            {
                ErrorResponse = "The DNS service name contains an empty label!";
                return false;
            }

            parsedLabels.Add(label.ToString());

            if (!TryValidateLabels(parsedLabels, out ErrorResponse))
                return false;

            Labels = [.. parsedLabels];
            return true;

        }

        internal static Boolean TryValidateLabels(IEnumerable<String>               Labels,
                                                  [NotNullWhen(false)] out String?  ErrorResponse)
        {

            ErrorResponse = null;
            var wireLength = 1;

            foreach (var label in Labels)
            {

                if (String.IsNullOrEmpty(label))
                {
                    ErrorResponse = "A DNS service name must not contain an empty label!";
                    return false;
                }

                Int32 labelLength;

                try
                {
                    labelLength = strictUTF8.GetByteCount(label);
                }
                catch (EncoderFallbackException)
                {
                    ErrorResponse = $"The DNS label contains invalid Unicode: '{label}'!";
                    return false;
                }

                if (labelLength > 63)
                {
                    ErrorResponse = $"Each label in the DNS service must not exceed 63 UTF-8 octets: '{label}'!";
                    return false;
                }

                wireLength += 1 + labelLength;

            }

            if (wireLength > 255)
            {
                ErrorResponse = "The given DNS service exceeds the maximum wire length of 255 octets!";
                return false;
            }

            return true;

        }

        private static Boolean IsEscaped(String Text, Int32 Index)
        {

            var slashCount = 0;

            for (var i = Index - 1; i >= 0 && Text[i] == '\\'; i--)
                slashCount++;

            return slashCount % 2 == 1;

        }

        internal static String EscapeLabel(String Label)

            => Label.Replace("\\", "\\\\", StringComparison.Ordinal).
                     Replace(".",  "\\.",   StringComparison.Ordinal);

        internal static String ToPresentationName(IEnumerable<String> Labels)
        {

            var escapedLabels = Labels.Select(EscapeLabel).ToArray();

            return escapedLabels.Length == 0
                       ? "."
                       : $"{String.Join('.', escapedLabels)}.";

        }

        /// <summary>
        /// Apply the DNS case-folding rule: only ASCII A-Z are equivalent to a-z.
        /// UTF-8 multibyte characters remain byte-for-byte distinct (RFC 6762 §16).
        /// </summary>
        internal static String ASCIICaseFold(String Text)

            => String.Create(
                   Text.Length,
                   Text,
                   static (characters, source) => {
                       for (var i = 0; i < source.Length; i++)
                           characters[i] = source[i] is >= 'A' and <= 'Z'
                                               ? (Char) (source[i] + ('a' - 'A'))
                                               : source[i];
                   }
               );

        /// <summary>
        /// Create a DNS service name from already separated labels. This is the unambiguous
        /// form for labels containing dots, backslashes, spaces or Unicode characters.
        /// </summary>
        public static DNSServiceName FromLabels(params String[] Labels)

            => new(Labels);

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this DNS service.
        /// </summary>
        public DNSServiceName Clone()

            => new(
                   FullName.CloneString()
               );

        #endregion


        public static DNSServiceName From(DomainName  DomainName,
                                          SRV_Spec    DNSServiceSpec)

            => new ($"{DNSServiceSpec}.{DomainName.FullName}");



        public void Serialize(Stream                      Stream,
                              Int32                       CurrentOffset,
                              Boolean                     UseCompression   = true,
                              Dictionary<String, Int32>?  Offsets          = null)
        {

            Offsets ??= [];

            // Case-folded compression key — see DomainName.Serialize (RFC 4343).
            var compressionKey = ASCIICaseFold(FullName);

            // Root domain. A name parsed from "." (or "") is represented as a single empty
            // label; it must serialize to just the terminating zero byte. Emitting a zero-length
            // label followed by the terminator would put two 0x00 bytes on the wire and corrupt
            // the packet (observed as FORMERR on root DNSKEY/DS queries).
            if (Labels.Count == 0 || Labels.All(label => label.Length == 0))
            {
                Stream.WriteByte(0x00);
                return;
            }

            // Check for compression
            if (UseCompression && Offsets.TryGetValue(compressionKey, out var pointerOffset))
            {
                // Pointer: 0xC0 | (offset >> 8), then low byte
                var pointer = (UInt16) (0xC000 | pointerOffset);
                Stream.WriteByte((Byte) (pointer >>    8));
                Stream.WriteByte((Byte) (pointer &  0xFF));
                return;
            }

            // Record where each suffix of this name starts — see DomainName.Serialize.
            var offset = CurrentOffset;

            for (var i = 0; i < labels.Length; i++)
            {

                var labelBytes = strictUTF8.GetBytes(labels[i]);
                if (labelBytes.Length > 63)
                    throw new ArgumentException("Label too long");

                // RFC 1035 §4.1.4: pointers carry a 14-bit offset; never record one that
                // cannot be represented.
                var suffixKey = ASCIICaseFold(ToPresentationName(labels.Skip(i)));
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

        #region Operator == (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSServiceName DNSService1,
                                           DNSServiceName DNSService2)

            => DNSService1.Equals(DNSService2);

        #endregion

        #region Operator == (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSServiceName DNSService1,
                                           String     DNSService2)

            => ASCIICaseFold(DNSService1.FullName).Equals(ASCIICaseFold(DNSService2), StringComparison.Ordinal);

        #endregion

        #region Operator != (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSServiceName DNSService1,
                                           DNSServiceName DNSService2)

            => !DNSService1.Equals(DNSService2);

        #endregion

        #region Operator != (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSServiceName DNSService1,
                                           String     DNSService2)

            => !ASCIICaseFold(DNSService1.FullName).Equals(ASCIICaseFold(DNSService2), StringComparison.Ordinal);

        #endregion

        #region Operator <  (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DNSServiceName DNSService1,
                                          DNSServiceName DNSService2)

            => DNSService1.CompareTo(DNSService2) < 0;

        #endregion

        #region Operator <= (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DNSServiceName DNSService1,
                                           DNSServiceName DNSService2)

            => DNSService1.CompareTo(DNSService2) <= 0;

        #endregion

        #region Operator >  (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DNSServiceName DNSService1,
                                          DNSServiceName DNSService2)

            => DNSService1.CompareTo(DNSService2) > 0;

        #endregion

        #region Operator >= (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DNSServiceName DNSService1,
                                           DNSServiceName DNSService2)

            => DNSService1.CompareTo(DNSService2) >= 0;

        #endregion

        #endregion

        #region IComparable<DNSService> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two DNS services.
        /// </summary>
        /// <param name="Object">A DNS service to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DNSServiceName domainName
                   ? CompareTo(domainName)
                   : throw new ArgumentException("The given object is not a DNS service!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DNSService)

        /// <summary>
        /// Compares two DNS services.
        /// </summary>
        /// <param name="DNSService">A DNS service to compare with.</param>
        public Int32 CompareTo(DNSServiceName? DNSService)
        {

            if (DNSService is null)
                throw new ArgumentNullException(nameof(DNSService), "The given DNS service must not be null!");

            return String.Compare(ASCIICaseFold(FullName),
                                  ASCIICaseFold(DNSService.FullName),
                                  StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<DNSService> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two DNS services for equality.
        /// </summary>
        /// <param name="Object">A DNS service to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSServiceName domainName &&
                   Equals(domainName);

        #endregion

        #region Equals(DNSService)

        /// <summary>
        /// Compares two DNS services for equality.
        /// </summary>
        /// <param name="DNSService">A DNS service to compare with.</param>
        public Boolean Equals(DNSServiceName? DNSService)

            => DNSService is not null &&

               // RFC 6762 §16: ASCII A-Z are case-insensitive; UTF-8 multibyte
               // characters remain distinct. This must agree with GetHashCode().
               String.Equals(ASCIICaseFold(FullName),
                             ASCIICaseFold(DNSService.FullName),
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => ASCIICaseFold(FullName).GetHashCode(StringComparison.Ordinal);

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
