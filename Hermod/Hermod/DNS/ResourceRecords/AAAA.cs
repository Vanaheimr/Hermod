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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS AAAA resource records.
    /// </summary>
    public static class DNS_AAAA_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, AAAARecord)

        /// <summary>
        /// Add a DNS A record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="AAAARecord">A DNS AAAA record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      AAAA            AAAARecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV6(IPPort.DNS),
                AAAARecord
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
        public const UInt16 TypeId = 28;

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
        public AAAA(Stream  Stream)

            : base(Stream,
                   TypeId)

        {
            this.IPv6Address = new IPv6Address(Stream);
        }

        #endregion

        #region AAAA(Name, Stream)

        /// <summary>
        /// Create a new AAAA resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this AAAA resource record.</param>
        /// <param name="Stream">A stream containing the AAAA resource record data.</param>
        public AAAA(String  Name,
                    Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {
            this.IPv6Address = new IPv6Address(Stream);
        }

        #endregion

        #region AAAA(Name, Class, TimeToLive, IPv6Address)

        /// <summary>
        /// Create a new DNS AAAA resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this AAAA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv4Address">The IPv4 address of this resource record.</param>
        public AAAA(String           Name,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(Name,
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
