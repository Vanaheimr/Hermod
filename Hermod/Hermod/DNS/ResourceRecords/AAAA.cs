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
                         DNSServiceName.Parse(DomainName.FullName),
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
                                     DNSServiceName       DomainName,
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
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.AAAA;

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
        /// <param name="Stream">AAAA stream containing the AAAA resource record data.</param>
        public AAAA(Stream Stream)

            : base(Stream,
                   TypeId)

        {
            this.IPv6Address = new IPv6Address(Stream);
        }

        #endregion

        #region AAAA(DomainName,     Stream)

        /// <summary>
        /// Create a new AAAA resource record from the given domain name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="Stream">AAAA stream containing the AAAA resource record data.</param>
        public AAAA(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength     = Stream.ReadUInt16BE();

            this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region AAAA(DNSServiceName, Stream)

        /// <summary>
        /// Create a new AAAA resource record from the given domain name and stream.
        /// </summary>
        /// <param name="DNSServiceName">The DNS Service Name of this AAAA resource record.</param>
        /// <param name="Stream">AAAA stream containing the AAAA resource record data.</param>
        public AAAA(DNSServiceName  DNSServiceName,
                    Stream          Stream)

            : base(DNSServiceName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.IPv6Address = new IPv6Address(Stream);

        }

        #endregion

        #region AAAA(DomainName,     Class, TimeToLive, IPv6Address)

        /// <summary>
        /// Create a new DNS AAAA resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv6Address">The IPv6 address of this resource record.</param>
        public AAAA(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {
            this.IPv6Address = IPv6Address;
        }

        #endregion

        #region AAAA(DNSServiceName, Class, TimeToLive, IPv6Address)

        /// <summary>
        /// Create a new DNS AAAA resource record.
        /// </summary>
        /// <param name="DNSServiceName">The DNS Service Name of this AAAA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IPv6Address">The IPv6 address of this resource record.</param>
        public AAAA(DNSServiceName   DNSServiceName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(DNSServiceName,
                   TypeId,
                   Class,
                   TimeToLive)

        {
            this.IPv6Address = IPv6Address;
        }

        #endregion

        #endregion

        public static AAAA Parse(DomainName       DomainName,
                                 DNSQueryClasses  Class,
                                 TimeSpan         TimeToLive,
                                 Byte[]           RData)
        {

            if (RData.Length != 16)
                throw new InvalidDataException("Invalid AAAA RData length");

            return new AAAA(
                       DomainName,
                       Class,
                       TimeToLive,
                       new IPv6Address(RData)
                   );

        }

        public static async ValueTask<AAAA> Parse(DomainName         DomainName,
                                                  DNSQueryClasses    Class,
                                                  TimeSpan           TimeToLive,
                                                  Stream             RDataStream,
                                                  CancellationToken  CancellationToken   = default)
        {

            var memory  = new Byte[16];
            var read    = await RDataStream.ReadAsync(
                                    memory,
                                    CancellationToken
                                );

            if (read != 16)
                throw new InvalidDataException("Invalid AAAA RData length!");

            return new AAAA(
                       DomainName,
                       Class,
                       TimeToLive,
                       new IPv6Address(memory)
                   );

        }


        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression (true by default).</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            // RDLENGTH (2 bytes): 16 for AAAA
            Stream.WriteUInt16BE(16);

            // RDATA: IPv4 address (16 bytes)
            Stream.Write(IPv6Address.GetBytes(), 0, 16);

        }

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
