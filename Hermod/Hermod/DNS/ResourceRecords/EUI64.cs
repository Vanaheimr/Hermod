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
    /// Extensions methods for DNS EUI64 resource records.
    /// </summary>
    public static class DNS_EUI64_Extensions
    {

        #region CacheEUI64(this DNSClient, DomainName, Address, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS EUI64 record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Address">The 64-bit EUI-64 address (8 bytes).</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheEUI64(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      Byte[]           Address,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new EUI64(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Address
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS EUI-64 address (EUI64) resource record (RFC 7043).
    /// </summary>
    public class EUI64 : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS EUI-64 address (EUI64) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.EUI64;

        #endregion

        #region Properties

        /// <summary>
        /// The 64-bit EUI-64 address (8 bytes).
        /// </summary>
        public Byte[]  Address    { get; }

        #endregion

        #region Constructor

        #region EUI64(Stream)

        /// <summary>
        /// Create a new EUI64 resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the EUI64 resource record data.</param>
        public EUI64(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Address = DNSTools.ExtractByteArray(Stream, 8);

        }

        #endregion

        #region EUI64(DomainName, Stream)

        /// <summary>
        /// Create a new EUI64 resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this EUI64 resource record.</param>
        /// <param name="Stream">A stream containing the EUI64 resource record data.</param>
        public EUI64(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Address = DNSTools.ExtractByteArray(Stream, 8);

        }

        #endregion

        #region EUI64(DomainName, Class, TimeToLive, Address)

        /// <summary>
        /// Create a new DNS EUI64 resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this EUI64 resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Address">The 64-bit EUI-64 address (8 bytes).</param>
        public EUI64(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     Byte[]           Address)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            if (Address.Length != 8)
                throw new ArgumentException("EUI-64 address must be exactly 8 bytes!", nameof(Address));

            this.Address = Address;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field
        /// (e.g. Google dns.google/resolve or Cloudflare cloudflare-dns.com/dns-query).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static EUI64? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try { return new EUI64(Name, DNSQueryClasses.IN, TimeToLive, Convert.FromHexString(Data.Replace("-", "").Replace(":", ""))); }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{Address[0]:x2}-{Address[1]:x2}-{Address[2]:x2}-{Address[3]:x2}-{Address[4]:x2}-{Address[5]:x2}-{Address[6]:x2}-{Address[7]:x2}";

        #endregion

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

            // RDLENGTH (2 bytes): always 8
            Stream.WriteUInt16BE(8);

            Stream.Write(Address, 0, 8);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Address[0]:x2}-{Address[1]:x2}-{Address[2]:x2}-{Address[3]:x2}-{Address[4]:x2}-{Address[5]:x2}-{Address[6]:x2}-{Address[7]:x2}, {base.ToString()}";

        #endregion

    }

}
