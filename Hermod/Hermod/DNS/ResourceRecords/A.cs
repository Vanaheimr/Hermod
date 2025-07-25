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
    /// Extensions methods for DNS A resource records.
    /// </summary>
    public static class DNS_A_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, ARecord)

        /// <summary>
        /// Add a DNS A record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="ARecord">A DNS A record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      A               ARecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                ARecord
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
        public const UInt16 TypeId = 1;

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

        #region A(Name, Stream)

        /// <summary>
        /// Create a new A resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this A resource record.</param>
        /// <param name="Stream">A stream containing the A resource record data.</param>
        public A(String  Name,
                 Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {
            this.IPv4Address = new IPv4Address(Stream);
        }

        #endregion

        #region A(Name, Class, TimeToLive, IPv4Address)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv4Address">The IPv4 address of this resource record.</param>
        public A(String           Name,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : base(Name,
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
