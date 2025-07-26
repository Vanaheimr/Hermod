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
    public class DomainName : IEquatable<DomainName>,
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

        private readonly String[]  labels;

        #endregion

        #region Properties

        public String                 FullName    { get; }

        public IReadOnlyList<String>  Labels
            => labels.AsReadOnly();



        public DomainName?            ParentDomain

            => labels.Length > 1
                   ? new DomainName(
                         [.. labels.Skip(1)]
                     )
                   : null;

        public String                 TopLevelDomain

            => labels.Last();

        public DomainName             TopLevelDomainName

            => new (labels.Last());

        public String                 SecondLevelDomain

            => labels.Length >= 2
                   ? labels[^2]
                   : String.Empty;

        public DomainName?            SecondLevelDomainName

            => labels.Length >= 2
                   ? new DomainName(
                         [.. labels.TakeLast(2)]
                     )
                   : null;


        public static DomainName      Localhost

            => new ("localhost");

        public static DomainName      Loopback

            => new ("loopback");

        public static DomainName      Empty

            => new ([]);

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


        #region Parse   (Text)

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
        {

            DomainName     = null;
            ErrorResponse  = null;

            Text = Text?.Trim().ToLowerInvariant() ?? "";

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
                if (!DomainNameRegExpr.IsMatch(Text))
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

            => DomainName1.FullName.Equals(DomainName2);

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

            => !DomainName1.FullName.Equals(DomainName2);

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

            return FullName.CompareTo(DomainName.FullName);

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

               String.Equals(FullName,
                             DomainName.FullName,
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
