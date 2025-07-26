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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS AAAA resource records.
    /// </summary>
    public static class DNS_AAAA_Extensions
    {

        #region CacheAAAA(this DNSClient, DomainName, IPv6Address, Class = IN, TimeToLive = 365days)

        public static void CacheAAAA(this DNSClient   DNSClient,
                                     DomainName       DomainName,
                                     IPv6Address      IPv6Address,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)

            => CacheAAAA(DNSClient,
                         DNSService.Parse(DomainName.FullName),
                         IPv6Address,
                         Class,
                         TimeToLive);


        /// <summary>
        /// Add a DNS AAAA record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="IPv6Address">The IPv6 address of this resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheAAAA(this DNSClient   DNSClient,
                                     DNSService       DomainName,
                                     IPv6Address      IPv6Address,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new AAAA(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                IPv6Address
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS AAAA resource record.
    /// </summary>
    public class AAAA : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS AAAA resource record type identifier.
        /// </summary>
        public const DNSResourceRecords TypeId = DNSResourceRecords.AAAA;

        #endregion

        #region Properties

        /// <summary>
        /// The IPv6 address of this AAAA resource record.
        /// </summary>
        public IPv6Address  IPv6Address    { get; }

        #endregion

        #region Constructor

        #region AAAA(Stream)

        /// <summary>
        /// Create a new AAAA resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the AAAA resource record data.</param>
        public AAAA(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region AAAA(DomainName Stream)

        public AAAA(DomainName  DomainName,
                    Stream      Stream)

            : this(DNSService.Parse(DomainName.FullName),
                   Stream)

        { }


        /// <summary>
        /// Create a new AAAA resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="Stream">A stream containing the AAAA resource record data.</param>
        public AAAA(DNSService  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region AAAA(DomainName, Class, TimeToLive, IPv6Address)

        public AAAA(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : this(DNSService.Parse(DomainName.FullName),
                   Class,
                   TimeToLive,
                   IPv6Address)

        { }


        /// <summary>
        /// Create a new DNS AAAA resource record.
        /// </summary>
        /// <param name="Name">The domain name of this AAAA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv4Address">The IPv4 address of this resource record.</param>
        public AAAA(DNSService       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   IPv6Address.ToString())

        {

            this.IPv6Address = IPv6Address;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{IPv6Address}, {base.ToString()}";

        #endregion

    }

}
