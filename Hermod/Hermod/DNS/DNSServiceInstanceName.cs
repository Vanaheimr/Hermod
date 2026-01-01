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
    /// Extension methods for DNS service instances.
    /// </summary>
    public static class DNSServiceInstanceExtensions
    {

        /// <summary>
        /// Indicates whether this DNS service instance is null or empty.
        /// </summary>
        /// <param name="DNSServiceInstance">A DNS service instance.</param>
        public static Boolean IsNullOrEmpty(this DNSServiceInstanceName? DNSServiceInstance)
            => DNSServiceInstance?.FullName.IsNullOrEmpty() ?? true;

        /// <summary>
        /// Indicates whether this DNS service instance is null or empty.
        /// </summary>
        /// <param name="DNSServiceInstance">A DNS service instance.</param>
        public static Boolean IsNotNullOrEmpty([NotNullWhen(true)] this DNSServiceInstanceName? DNSServiceInstance)
            => DNSServiceInstance?.FullName.IsNotNullOrEmpty() ?? false;

    }

    /// <summary>
    /// DNS Service Instance Names are used for DNS-Based Service Discovery (DNS-SD).
    /// DNS-SD builds on SRV records to enable zero-configuration networking (e.g., Bonjour/Avahi),
    /// e.g. localController1._ocpp._tls.example.org
    /// https://www.rfc-editor.org/rfc/rfc6763
    /// </summary>
    public class DNSServiceInstanceName : IDomainName,
                                          IEquatable<DNSServiceInstanceName>,
                                          IComparable<DNSServiceInstanceName>,
                                          IComparable
    {

        #region Data

        public static readonly Regex DNSServiceInstanceNameRegExpr  = new Regex(
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

        protected DNSServiceInstanceName(String DNSServiceInstance)
        {

            this.FullName  = DNSServiceInstance;
            this.labels    = DNSServiceInstance.TrimEnd('.').Split('.');

        }

        protected DNSServiceInstanceName(params String[] DomainLabels)
        {

            this.FullName  = DomainLabels.AggregateWith('.') + ".";
            this.labels    = DomainLabels;

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as DNS service instance.
        /// </summary>
        /// <param name="Text">The text representation of a DNS service instance.</param>
        public static DNSServiceInstanceName Parse(String Text)
        {

            if (TryParse(Text, out var dnsServiceName, out var errorResponse))
                return dnsServiceName;

            throw new ArgumentException($"Invalid text representation of a DNS service instance name: '{Text}': {errorResponse}",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text, out DNSServiceInstance, out ErrorResponse)

        /// <summary>
        /// Parse the given string as a DNS service instance (RFC 1035).
        /// </summary>
        /// <param name="Text">The text representation of a DNS service instance.</param>
        /// <param name="DNSServiceInstance">The parsed DNS service instance.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)]  out DNSServiceInstanceName?  DNSServiceInstance,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            DNSServiceInstance     = null;
            ErrorResponse  = null;

            Text = Text?.Trim().ToLowerInvariant() ?? "";

            if (Text.IsNullOrEmpty())
            {
                ErrorResponse = "The given DNS service instance must not be null or empty!";
                return false;
            }

            if (!Text.EndsWith('.'))
                Text += ".";

            if (Text.Length > 255)
            {
                ErrorResponse = "The given DNS service instance exceeds maximum length of 255 characters!";
                return false;
            }

            if (Text != ".")
            {
                if (!DNSServiceInstanceNameRegExpr.IsMatch(Text))
                {
                    ErrorResponse = "The given DNS service instance does not match the required format!";
                    return false;
                }
            }

            var labels = Text.TrimEnd('.').Split('.');
            foreach (var label in labels)
            {

                if (label.Length > 63)
                {
                    ErrorResponse = $"Each label in the DNS service instance must not exceed 63 characters: '{label}'!";
                    return false;
                }

                if (label.StartsWith('-') || label.EndsWith('-'))
                {
                    ErrorResponse = $"Each label in the DNS service instance must not start or end with a hyphen: '{label}'!";
                    return false;
                }

            }

            DNSServiceInstance = new DNSServiceInstanceName(Text);
            return true;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this DNS service instance.
        /// </summary>
        public DNSServiceInstanceName Clone()

            => new(
                   FullName.CloneString()
               );

        #endregion


        public static DNSServiceInstanceName From(DomainName  DomainName,
                                                  SRV_Spec    DNSServiceSpec,
                                                  String      InstanceName)

            => new ($"{InstanceName}.{DNSServiceSpec}.{DomainName.FullName}");



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

        #region Operator == (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSServiceInstanceName DNSServiceInstance1,
                                           DNSServiceInstanceName DNSServiceInstance2)

            => DNSServiceInstance1.Equals(DNSServiceInstance2);

        #endregion

        #region Operator == (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSServiceInstanceName DNSServiceInstance1,
                                           String     DNSServiceInstance2)

            => DNSServiceInstance1.FullName.Equals(DNSServiceInstance2);

        #endregion

        #region Operator != (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSServiceInstanceName DNSServiceInstance1,
                                           DNSServiceInstanceName DNSServiceInstance2)

            => !DNSServiceInstance1.Equals(DNSServiceInstance2);

        #endregion

        #region Operator != (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSServiceInstanceName DNSServiceInstance1,
                                           String     DNSServiceInstance2)

            => !DNSServiceInstance1.FullName.Equals(DNSServiceInstance2);

        #endregion

        #region Operator <  (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DNSServiceInstanceName DNSServiceInstance1,
                                          DNSServiceInstanceName DNSServiceInstance2)

            => DNSServiceInstance1.CompareTo(DNSServiceInstance2) < 0;

        #endregion

        #region Operator <= (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DNSServiceInstanceName DNSServiceInstance1,
                                           DNSServiceInstanceName DNSServiceInstance2)

            => DNSServiceInstance1.CompareTo(DNSServiceInstance2) <= 0;

        #endregion

        #region Operator >  (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DNSServiceInstanceName DNSServiceInstance1,
                                          DNSServiceInstanceName DNSServiceInstance2)

            => DNSServiceInstance1.CompareTo(DNSServiceInstance2) > 0;

        #endregion

        #region Operator >= (DNSServiceInstance1, DNSServiceInstance2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSServiceInstance1">A DNS service instance.</param>
        /// <param name="DNSServiceInstance2">Another DNS service instance.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DNSServiceInstanceName DNSServiceInstance1,
                                           DNSServiceInstanceName DNSServiceInstance2)

            => DNSServiceInstance1.CompareTo(DNSServiceInstance2) >= 0;

        #endregion

        #endregion

        #region IComparable<DNSServiceInstance> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two DNS service instances.
        /// </summary>
        /// <param name="Object">A DNS service instance to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DNSServiceInstanceName domainName
                   ? CompareTo(domainName)
                   : throw new ArgumentException("The given object is not a DNS service instance!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DNSServiceInstance)

        /// <summary>
        /// Compares two DNS service instances.
        /// </summary>
        /// <param name="DNSServiceInstance">A DNS service instance to compare with.</param>
        public Int32 CompareTo(DNSServiceInstanceName? DNSServiceInstance)
        {

            if (DNSServiceInstance is null)
                throw new ArgumentNullException(nameof(DNSServiceInstance), "The given DNS service instance must not be null!");

            return FullName.CompareTo(DNSServiceInstance.FullName);

        }

        #endregion

        #endregion

        #region IEquatable<DNSServiceInstance> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two DNS service instances for equality.
        /// </summary>
        /// <param name="Object">A DNS service instance to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSServiceInstanceName domainName &&
                   Equals(domainName);

        #endregion

        #region Equals(DNSServiceInstance)

        /// <summary>
        /// Compares two DNS service instances for equality.
        /// </summary>
        /// <param name="DNSServiceInstance">A DNS service instance to compare with.</param>
        public Boolean Equals(DNSServiceInstanceName? DNSServiceInstance)

            => DNSServiceInstance is not null &&

               String.Equals(FullName,
                             DNSServiceInstance.FullName,
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
