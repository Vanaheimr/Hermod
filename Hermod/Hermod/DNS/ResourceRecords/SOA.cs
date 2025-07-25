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
    /// Extensions methods for DNS SOA resource records.
    /// </summary>
    public static class DNS_SOA_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, SOARecord)

        /// <summary>
        /// Add a DNS SOA record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="SOARecord">A DNS SOA record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      SOA             SOARecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                SOARecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Start of Authority (SOA) resource record.
    /// </summary>
    public class SOA : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Start of Authority (SOA) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 6;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the DNS server that is authoritative for the domain.
        /// </summary>
        public String  Server     { get; }

        /// <summary>
        /// The email address of the person responsible for the domain.
        /// </summary>
        public String  Email      { get; }

        /// <summary>
        /// The serial number of the zone file, which is incremented each time the zone file is updated.
        /// </summary>
        public Int64   Serial     { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should wait before refreshing its zone file from the primary DNS server.
        /// </summary>
        public Int64   Refresh    { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should wait before retrying a failed refresh attempt.
        /// </summary>
        public Int64   Retry      { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should consider the zone file valid before it expires.
        /// </summary>
        public Int64   Expire     { get; }

        /// <summary>
        /// The minimum time interval (in seconds) that a DNS resolver should cache the SOA record.
        /// </summary>
        public Int64   Minimum    { get; }

        #endregion

        #region Constructor

        #region SOA(Stream)

        /// <summary>
        /// Create a new SOA resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the SOA resource record data.</param>
        public SOA(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Server   = DNSTools.ExtractName(Stream);
            this.Email    = DNSTools.ExtractName(Stream);
            this.Serial   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Refresh  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Retry    = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Expire   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Minimum  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region SOA(Name, Stream)

        /// <summary>
        /// Create a new SOA resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this SOA resource record.</param>
        /// <param name="Stream">A stream containing the SOA resource record data.</param>
        public SOA(String  Name,
                   Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {

            this.Server   = DNSTools.ExtractName(Stream);
            this.Email    = DNSTools.ExtractName(Stream);
            this.Serial   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Refresh  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Retry    = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Expire   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this.Minimum  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region SOA(Name, Class, TimeToLive, ...)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Server">The name of the DNS server that is authoritative for the domain.</param>
        /// <param name="Email">The email address of the person responsible for the domain.</param>
        /// <param name="Serial">The serial number of the zone file, which is incremented each time the zone file is updated.</param>
        /// <param name="Refresh">The time interval (in seconds) that a secondary DNS server should wait before refreshing its zone file from the primary DNS server.</param>
        /// <param name="Retry">The time interval (in seconds) that a secondary DNS server should wait before retrying a failed refresh attempt.</param>
        /// <param name="Expire">The time interval (in seconds) that a secondary DNS server should consider the zone file valid before it expires.</param>
        /// <param name="Minimum">The minimum time interval (in seconds) that a DNS resolver should cache the SOA record.</param>
        public SOA(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           Server,
                   String           Email,
                   Int64            Serial,
                   Int64            Refresh,
                   Int64            Retry,
                   Int64            Expire,
                   Int64            Minimum)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Server   = Server;
            this.Email    = Email;
            this.Serial   = Serial;
            this.Refresh  = Refresh;
            this.Retry    = Retry;
            this.Expire   = Expire;
            this.Minimum  = Minimum;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   $"SOA(Name: {Name}, ",
                   $"Server: {Server}, ",
                   $"Email: {Email}, ",
                   $"Serial: {Serial}, ",
                   $"Refresh: {Refresh}, ",
                   $"Retry: {Retry}, ",
                   $"Expire: {Expire}, ",
                   $"Minimum: {Minimum}, ",
                   base.ToString()
               );

        #endregion

    }

}
