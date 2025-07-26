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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS A resource records.
    /// </summary>
    public static class DNS_A_Extensions
    {

        #region CacheA(this DNSClient, Name, IPv4Address, Class = IN, TimeToLive = 365days)

        public static void CacheA(this DNSClient   DNSClient,
                                  DomainName       DomainName,
                                  IPv4Address      IPv4Address,
                                  DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                  TimeSpan?        TimeToLive   = null)

            => CacheA(DNSClient,
                      DNSService.Parse(DomainName.FullName),
                      IPv4Address,
                      Class,
                      TimeToLive);


        /// <summary>
        /// Add a DNS A record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv4Address">The IPv4 address of this resource record.</param>
        public static void CacheA(this DNSClient   DNSClient,
                                  DNSService       DomainName,
                                  IPv4Address      IPv4Address,
                                  DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                  TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new A(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                IPv4Address
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS A resource record.
    /// </summary>
    public class A : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS A resource record type identifier.
        /// </summary>
        public const DNSResourceRecords TypeId = DNSResourceRecords.A;

        #endregion

        #region Properties

        /// <summary>
        /// The IPv4 address.
        /// </summary>
        public IPv4Address  IPv4Address    { get; }

        #endregion

        #region Constructor

        #region A(Stream)

        /// <summary>
        /// Create a new A resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the A resource record data.</param>
        public A(Stream Stream)

            : base(Stream,
                   TypeId)

        {
            this.IPv4Address = new IPv4Address(Stream);
        }

        #endregion

        #region A(DomainName, Stream)

        public A(DomainName  DomainName,
                 Stream      Stream)

            : this(DNSService.Parse(DomainName.FullName),
                   Stream)

        { }


        /// <summary>
        /// Create a new A resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Stream">A stream containing the A resource record data.</param>
        public A(DNSService  DomainName,
                 Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.IPv4Address = new IPv4Address(Stream);

        }

        #endregion

        #region A(Name, Class, TimeToLive, IPv4Address)

        public A(DomainName       DomainName,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : this(DNSService.Parse(DomainName.FullName),
                   Class,
                   TimeToLive,
                   IPv4Address)

        { }


        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv4Address">The IPv4 address of this resource record.</param>
        public A(DNSService       DomainName,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive,
                   IPv4Address.ToString())

        {

            this.IPv4Address = IPv4Address;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{IPv4Address}, {base.ToString()}";

        #endregion

    }

}
