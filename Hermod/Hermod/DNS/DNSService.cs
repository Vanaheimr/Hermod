/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

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
        public static Boolean IsNullOrEmpty(this DNSService? DNSService)
            => DNSService?.FullName.IsNullOrEmpty() ?? true;

        /// <summary>
        /// Indicates whether this DNS service is null or empty.
        /// </summary>
        /// <param name="DNSService">A DNS service.</param>
        public static Boolean IsNotNullOrEmpty(this DNSService? DNSService)
            => DNSService?.FullName.IsNotNullOrEmpty() ?? false;

    }

    /// <summary>
    /// DNS service DNS services are used to identify services in the Domain Name System (DNS).
    /// </summary>
    public class DNSService : IEquatable<DNSService>,
                              IComparable<DNSService>,
                              IComparable
    {

        #region Data

        public static readonly Regex DNSServiceRegExpr = new Regex(
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

        private readonly String[]  labels;

        #endregion

        #region Properties

        public String                 FullName    { get; }

        public IReadOnlyList<String>  Labels
            => labels.AsReadOnly();

        #endregion

        #region Constructor(s)

        protected DNSService(String DNSService)
        {

            this.FullName  = DNSService;
            this.labels    = DNSService.TrimEnd('.').Split('.');

        }

        protected DNSService(params String[] DomainLabels)
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
        public static DNSService Parse(String Text)
        {

            if (TryParse(Text, out var domainName, out var errorResponse))
                return domainName;

            throw new ArgumentException($"Invalid text representation of a DNS service: '{Text}': {errorResponse}",
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
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)]  out DNSService?  DNSService,
                                       [NotNullWhen(false)] out String?      ErrorResponse)
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

            if (!DNSServiceRegExpr.IsMatch(Text))
            {
                ErrorResponse = "The given DNS service does not match the required format!";
                return false;
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

            DNSService = new DNSService(Text);
            return true;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this DNS service.
        /// </summary>
        public DNSService Clone()

            => new(
                   FullName.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSService DNSService1,
                                           DNSService DNSService2)

            => DNSService1.Equals(DNSService2);

        #endregion

        #region Operator == (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSService DNSService1,
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
        public static Boolean operator != (DNSService DNSService1,
                                           DNSService DNSService2)

            => !DNSService1.Equals(DNSService2);

        #endregion

        #region Operator != (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSService DNSService1,
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
        public static Boolean operator < (DNSService DNSService1,
                                          DNSService DNSService2)

            => DNSService1.CompareTo(DNSService2) < 0;

        #endregion

        #region Operator <= (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DNSService DNSService1,
                                           DNSService DNSService2)

            => DNSService1.CompareTo(DNSService2) <= 0;

        #endregion

        #region Operator >  (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DNSService DNSService1,
                                          DNSService DNSService2)

            => DNSService1.CompareTo(DNSService2) > 0;

        #endregion

        #region Operator >= (DNSService1, DNSService2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSService1">A DNS service.</param>
        /// <param name="DNSService2">Another DNS service.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DNSService DNSService1,
                                           DNSService DNSService2)

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

            => Object is DNSService domainName
                   ? CompareTo(domainName)
                   : throw new ArgumentException("The given object is not a DNS service!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DNSService)

        /// <summary>
        /// Compares two DNS services.
        /// </summary>
        /// <param name="DNSService">A DNS service to compare with.</param>
        public Int32 CompareTo(DNSService? DNSService)
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

            => Object is DNSService domainName &&
                   Equals(domainName);

        #endregion

        #region Equals(DNSService)

        /// <summary>
        /// Compares two DNS services for equality.
        /// </summary>
        /// <param name="DNSService">A DNS service to compare with.</param>
        public Boolean Equals(DNSService? DNSService)

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
