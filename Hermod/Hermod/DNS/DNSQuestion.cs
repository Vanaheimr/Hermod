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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS question represents a query made to a DNS server.
    /// </summary>
    public class DNSQuestion : IEquatable<DNSQuestion>,
                               IComparable<DNSQuestion>,
                               IComparable
    {

        #region Properties

        /// <summary>
        /// The domain name for which the DNS query is made.
        /// </summary>
        public DNSServiceName          DomainName    { get; }

        /// <summary>
        /// The type of DNS resource record being queried.
        /// </summary>
        public DNSResourceRecordTypes  QueryType     { get; }

        /// <summary>
        /// The class of the DNS query, typically IN for Internet.
        /// </summary>
        public DNSQueryClasses         QueryClass    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS question.
        /// </summary>
        /// <param name="DomainName">The domain name for which the DNS query is made.</param>
        /// <param name="QueryType">The type of DNS resource record being queried.</param>
        /// <param name="QueryClass">The class of the DNS query, typically IN for Internet.</param>
        public DNSQuestion(DNSServiceName          DomainName,
                           DNSResourceRecordTypes  QueryType,
                           DNSQueryClasses         QueryClass)
        {

            this.DomainName  = DomainName;
            this.QueryType   = QueryType;
            this.QueryClass  = QueryClass;

            unchecked
            {

                hashCode = this.DomainName.GetHashCode() * 5 ^
                           this.QueryType. GetHashCode() * 3 ^
                           this.QueryClass.GetHashCode();

            }

        }

        #endregion


        #region Parse(Stream)

        /// <summary>
        /// Parse a DNS question from the given stream.
        /// </summary>
        /// <param name="Stream">A DNS stream.</param>
        public static DNSQuestion Parse(Stream Stream)

            => new (
                   DNSTools.ExtractDNSServiceName(Stream),
                   (DNSResourceRecordTypes) Stream.ReadUInt16BE(),
                   (DNSQueryClasses)        Stream.ReadUInt16BE()
               );

        #endregion

        #region Serialize(Stream, CurrentOffset, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize this DNS question to the given stream.
        /// </summary>
        /// <param name="Stream">A DNS stream.</param>
        /// <param name="CurrentOffset">The current offset in the stream.</param>
        /// <param name="UseCompression">Whether to use compression for domain names.</param>
        /// <param name="CompressionOffsets">Optional dictionary of compression offsets for domain names.</param>
        public void Serialize(Stream                      Stream,
                              Int32                       CurrentOffset,
                              Boolean                     UseCompression       = true,
                              Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            DomainName.Serialize(
                Stream,
                CurrentOffset,
                UseCompression,
                CompressionOffsets
            );

            Stream.WriteUInt16BE((UInt16) QueryType);
            Stream.WriteUInt16BE((UInt16) QueryClass);

        }

        #endregion


        #region Operator overloading

        #region Operator == (DNSQuestion1, DNSQuestion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSQuestion1">A DNS Question.</param>
        /// <param name="DNSQuestion2">Another DNS Question.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSQuestion DNSQuestion1,
                                           DNSQuestion DNSQuestion2)

            => DNSQuestion1.Equals(DNSQuestion2);

        #endregion

        #region Operator != (DNSQuestion1, DNSQuestion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSQuestion1">A DNS Question.</param>
        /// <param name="DNSQuestion2">Another DNS Question.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSQuestion DNSQuestion1,
                                           DNSQuestion DNSQuestion2)

            => !DNSQuestion1.Equals(DNSQuestion2);

        #endregion

        #endregion

        #region IComparable<DNSQuestion> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two DNS Questions.
        /// </summary>
        /// <param name="Object">A DNS Question to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DNSQuestion dnsQuestion
                   ? CompareTo(dnsQuestion)
                   : throw new ArgumentException("The given object is not a DNS Question!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DNSQuestion)

        /// <summary>
        /// Compares two DNS Questions.
        /// </summary>
        /// <param name="DNSQuestion">A DNS Question to compare with.</param>
        public Int32 CompareTo(DNSQuestion DNSQuestion)
        {

            var c = DomainName.CompareTo(DNSQuestion.DomainName);

            if (c == 0)
                c = QueryType. CompareTo(DNSQuestion.QueryType);

            if (c == 0)
                c = QueryClass.CompareTo(DNSQuestion.QueryClass);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<DNSQuestion> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two DNS Questions.
        /// </summary>
        /// <param name="Object">A DNS Question to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSQuestion dnsQuestion &&
                   Equals(dnsQuestion);

        #endregion

        #region Equals(DNSQuestion)

        /// <summary>
        /// Compares two DNS Questions.
        /// </summary>
        /// <param name="DNSQuestion">A DNS Question to compare with.</param>
        public Boolean Equals(DNSQuestion? DNSQuestion)

            => DNSQuestion is not null &&

               DomainName.Equals(DNSQuestion.DomainName) &&
               QueryType. Equals(DNSQuestion.QueryType)  &&
               QueryClass.Equals(DNSQuestion.QueryClass);

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{DomainName}: {QueryClass} {QueryType}";

        #endregion

    }

}
