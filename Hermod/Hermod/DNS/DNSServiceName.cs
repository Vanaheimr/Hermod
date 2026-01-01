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

        public static readonly Regex DNSServiceNameRegExpr  = new Regex(
                                                                  @"^(?=.{1,254}$)" +                                               // max. 254 Zeichen gesamt inkl. Punkt
                                                                  @"(?:[A-Za-z0-9_]" +                                              // erstes Label: beginnt mit Buchst./Ziffer/_
                                                                  @"(?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?" +                         // optional mittlere Zeichen, endet mit Buchst./Ziffer/_
                                                                  @")" +
                                                                  @"(?:\.(?:[A-Za-z0-9_](?:[A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?))*" +  // 0…n weitere Labels
                                                                  @"\.?$",                                                          // optional ein abschließender Punkt
                                                                  RegexOptions.IgnoreCase |
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

            this.FullName  = DNSService;
            this.labels    = DNSService.TrimEnd('.').Split('.');

        }

        protected DNSServiceName(params String[] DomainLabels)
        {

            this.FullName  = DomainLabels.AggregateWith('.') + ".";
            this.labels    = DomainLabels;

        }

        #endregion


        #region Parse   (Text)

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

        #region TryParse(Text, out DNSService, out ErrorResponse)

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

            DNSService     = null;
            ErrorResponse  = null;

            Text = Text?.Trim().ToLowerInvariant() ?? "";

            if (Text.IsNullOrEmpty())
            {
                ErrorResponse = "The given DNS service must not be null or empty!";
                return false;
            }

            if (!Text.EndsWith('.'))
                Text += ".";

            if (Text.Length > 255)
            {
                ErrorResponse = "The given DNS service exceeds maximum length of 255 characters!";
                return false;
            }

            if (Text != ".")
            {
                if (!DNSServiceNameRegExpr.IsMatch(Text))
                {
                    ErrorResponse = "The given DNS service does not match the required format!";
                    return false;
                }
            }

            var labels = Text.TrimEnd('.').Split('.');
            foreach (var label in labels)
            {

                if (label.Length > 63)
                {
                    ErrorResponse = $"Each label in the DNS service must not exceed 63 characters: '{label}'!";
                    return false;
                }

                if (label.StartsWith('-') || label.EndsWith('-'))
                {
                    ErrorResponse = $"Each label in the DNS service must not start or end with a hyphen: '{label}'!";
                    return false;
                }

            }

            DNSService = new DNSServiceName(Text);
            return true;

        }

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

            // Root domain
            if (Labels.Count == 0)
            {
                Stream.WriteByte(0x00);
                return;
            }

            // Check for compression
            if (UseCompression && Offsets.TryGetValue(FullName, out var pointerOffset))
            {
                // Pointer: 0xC0 | (offset >> 8), then low byte
                var pointer = (UInt16) (0xC000 | pointerOffset);
                Stream.WriteByte((Byte) (pointer >>    8));
                Stream.WriteByte((Byte) (pointer &  0xFF));
                return;
            }

            // Add offset for this name
            Offsets[FullName] = CurrentOffset;

            foreach (var label in Labels)
            {

                var labelBytes = Encoding.ASCII.GetBytes(label);
                if (labelBytes.Length > 63)
                    throw new ArgumentException("Label too long");

                Stream.WriteByte((Byte) labelBytes.Length);
                Stream.Write    (labelBytes, 0, labelBytes.Length);

                // Update offset for suffixes
                var suffix = String.Join(".", labels.AsEnumerable().Skip(Array.IndexOf(labels, label) + 1));
                if (!String.IsNullOrEmpty(suffix) && !Offsets.ContainsKey(suffix))
                    Offsets[suffix] = CurrentOffset + 1 + labelBytes.Length;

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

            => DNSService1.FullName.Equals(DNSService2);

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

            => !DNSService1.FullName.Equals(DNSService2);

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

            return FullName.CompareTo(DNSService.FullName);

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

               String.Equals(FullName,
                             DNSService.FullName,
                             StringComparison.Ordinal);

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
