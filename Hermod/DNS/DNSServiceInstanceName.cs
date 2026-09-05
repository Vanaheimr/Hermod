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

        /// <summary>
        /// Checks the lexical presentation syntax. Use <see cref="TryParse(String, out DNSServiceInstanceName, out String)"/>
        /// for authoritative Net-Unicode and wire-length validation.
        /// </summary>
        public static readonly Regex DNSServiceInstanceNameRegExpr  = new(
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

        protected DNSServiceInstanceName(String DNSServiceInstance)
        {

            var serviceName = DNSServiceName.Parse(DNSServiceInstance);

            if (!TryNormalizeLabels(serviceName.Labels, out var normalizedLabels, out var errorResponse))
                throw new ArgumentException(errorResponse, nameof(DNSServiceInstance));

            this.labels    = normalizedLabels;
            this.FullName  = DNSServiceName.ToPresentationName(labels);

        }

        protected DNSServiceInstanceName(params String[] DomainLabels)
        {

            if (!TryNormalizeLabels(DomainLabels, out var normalizedLabels, out var errorResponse))
                throw new ArgumentException(errorResponse, nameof(DomainLabels));

            this.labels    = normalizedLabels;
            this.FullName  = DNSServiceName.ToPresentationName(labels);

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

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as DNS service instance.
        /// </summary>
        /// <param name="Text">The text representation of a DNS service instance.</param>
        public static DNSServiceInstanceName? TryParse(String Text)
        {

            if (TryParse(Text, out var dnsServiceInstanceName, out _))
                return dnsServiceInstanceName;

            return null;

        }

        #endregion

        #region TryParse(Text, out DNSServiceInstance, out ErrorResponse)

        /// <summary>
        /// Parse the given string as a DNS service instance (RFC 1035).
        /// </summary>
        /// <param name="Text">The text representation of a DNS service instance.</param>
        /// <param name="DNSServiceInstance">The parsed DNS service instance.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        public static Boolean TryParse(String                                            Text,
                                       [NotNullWhen(true)]  out DNSServiceInstanceName?  DNSServiceInstance,
                                       [NotNullWhen(false)] out String?                  ErrorResponse)
        {

            DNSServiceInstance = null;

            if (!DNSServiceName.TryParse(Text, out var serviceName, out ErrorResponse) ||
                !TryNormalizeLabels(serviceName.Labels, out var normalizedLabels, out ErrorResponse))
            {
                return false;
            }

            DNSServiceInstance = new DNSServiceInstanceName(normalizedLabels);
            return true;

        }

        #endregion

        #region (private) TryNormalizeLabels(Labels, out NormalizedLabels, out ErrorResponse)

        private static Boolean TryNormalizeLabels(IReadOnlyList<String>              Labels,
                                                  [NotNullWhen(true)]  out String[]? NormalizedLabels,
                                                  [NotNullWhen(false)] out String?   ErrorResponse)
        {

            NormalizedLabels = null;

            if (Labels.Count == 0)
            {
                ErrorResponse = "A DNS service instance name must contain an instance label!";
                return false;
            }

            if (Labels[0].Any(character => character <= '\u001F' || character == '\u007F'))
            {
                ErrorResponse = "The DNS service instance label must not contain ASCII control characters!";
                return false;
            }

            try
            {
                NormalizedLabels = Labels.Select(label => label.Normalize(NormalizationForm.FormC)).ToArray();
            }
            catch (ArgumentException)
            {
                ErrorResponse = "The DNS service instance name contains invalid Unicode!";
                return false;
            }

            return DNSServiceName.TryValidateLabels(NormalizedLabels, out ErrorResponse);

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
        {

            var suffix = DNSServiceName.Parse($"{DNSServiceSpec}.{DomainName.FullName}");

            return new DNSServiceInstanceName([
                       InstanceName,
                       .. suffix.Labels
                   ]);

        }



        public void Serialize(Stream                      Stream,
                              Int32                       CurrentOffset,
                              Boolean                     UseCompression   = true,
                              Dictionary<String, Int32>?  Offsets          = null)

            => DNSServiceName.FromLabels(labels).
                              Serialize(Stream, CurrentOffset, UseCompression, Offsets);



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

            => DNSServiceName.ASCIICaseFold(DNSServiceInstance1.FullName).Equals(
                   DNSServiceName.ASCIICaseFold(DNSServiceInstance2),
                   StringComparison.Ordinal
               );

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

            => !DNSServiceName.ASCIICaseFold(DNSServiceInstance1.FullName).Equals(
                    DNSServiceName.ASCIICaseFold(DNSServiceInstance2),
                    StringComparison.Ordinal
                );

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

            return String.Compare(DNSServiceName.ASCIICaseFold(FullName),
                                  DNSServiceName.ASCIICaseFold(DNSServiceInstance.FullName),
                                  StringComparison.Ordinal);

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

               // RFC 6762 §16: only ASCII A-Z are case-insensitive.
               String.Equals(DNSServiceName.ASCIICaseFold(FullName),
                             DNSServiceName.ASCIICaseFold(DNSServiceInstance.FullName),
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => DNSServiceName.ASCIICaseFold(FullName).GetHashCode(StringComparison.Ordinal);

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
