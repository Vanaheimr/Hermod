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
    /// Extensions methods for DNS CSYNC resource records.
    /// </summary>
    public static class DNS_CSYNC_Extensions
    {

        #region CacheCSYNC(this DNSClient, DomainName, SOASerial, Flags, TypeBitMaps, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS CSYNC record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="SOASerial">The SOA serial number.</param>
        /// <param name="Flags">The CSYNC flags.</param>
        /// <param name="TypeBitMaps">The type bit maps.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCSYNC(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      UInt32           SOASerial,
                                      UInt16           Flags,
                                      Byte[]           TypeBitMaps,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CSYNC(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                SOASerial,
                                Flags,
                                TypeBitMaps
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Child-to-Parent Synchronization (CSYNC) resource record (RFC 7477).
    /// </summary>
    public class CSYNC : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Child-to-Parent Synchronization (CSYNC) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.CSYNC;

        #endregion

        #region Properties

        /// <summary>
        /// The SOA serial number.
        /// </summary>
        public UInt32  SOASerial      { get; }

        /// <summary>
        /// The CSYNC flags.
        /// </summary>
        public UInt16  Flags          { get; }

        /// <summary>
        /// The type bit maps.
        /// </summary>
        public Byte[]  TypeBitMaps    { get; }

        #endregion

        #region Constructor

        #region CSYNC(Stream)

        /// <summary>
        /// Create a new CSYNC resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CSYNC resource record data.</param>
        public CSYNC(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.SOASerial    = Stream.ReadUInt32BE();
            this.Flags        = Stream.ReadUInt16BE();
            this.TypeBitMaps  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 6));

        }

        #endregion

        #region CSYNC(DomainName, Stream)

        /// <summary>
        /// Create a new CSYNC resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CSYNC resource record.</param>
        /// <param name="Stream">A stream containing the CSYNC resource record data.</param>
        public CSYNC(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.SOASerial    = Stream.ReadUInt32BE();
            this.Flags        = Stream.ReadUInt16BE();
            this.TypeBitMaps  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 6));

        }

        #endregion

        #region CSYNC(DomainName, Class, TimeToLive, SOASerial, Flags, TypeBitMaps)

        /// <summary>
        /// Create a new DNS CSYNC resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CSYNC resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="SOASerial">The SOA serial number.</param>
        /// <param name="Flags">The CSYNC flags.</param>
        /// <param name="TypeBitMaps">The type bit maps.</param>
        public CSYNC(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     UInt32           SOASerial,
                     UInt16           Flags,
                     Byte[]           TypeBitMaps)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.SOASerial    = SOASerial;
            this.Flags        = Flags;
            this.TypeBitMaps  = TypeBitMaps;

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
        public static CSYNC? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return null;
                return new CSYNC(Name, DNSQueryClasses.IN, TimeToLive,
                                 UInt32.Parse(parts[0]),
                                 UInt16.Parse(parts[1]),
                                 EncodeTypeBitMaps(parts.Skip(2)));
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{SOASerial} {Flags} {DecodeTypeBitMaps(TypeBitMaps)}";

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

            // RDLENGTH (2 bytes): 4 (SOASerial) + 2 (Flags) + TypeBitMaps.Length
            Stream.WriteUInt16BE(6 + TypeBitMaps.Length);

            Stream.WriteUInt32BE(SOASerial);
            Stream.WriteUInt16BE(Flags);
            Stream.Write        (TypeBitMaps, 0, TypeBitMaps.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"SOASerial: {SOASerial}, Flags: {Flags}, TypeBitMaps: {BitConverter.ToString(TypeBitMaps)}, {base.ToString()}";

        #endregion

    }

}
