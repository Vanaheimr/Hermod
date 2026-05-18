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
    /// Extensions methods for DNS NSEC resource records.
    /// </summary>
    public static class DNS_NSEC_Extensions
    {

        #region CacheNSEC(this DNSClient, DomainName, NextDomainName, TypeBitMaps, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS NSEC record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this NSEC resource record.</param>
        /// <param name="NextDomainName">The next domain name in the canonical ordering of the zone.</param>
        /// <param name="TypeBitMaps">The type bit maps indicating which RR types exist at the NSEC owner name.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheNSEC(this DNSClient   DNSClient,
                                     DomainName       DomainName,
                                     DomainName       NextDomainName,
                                     Byte[]           TypeBitMaps,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new NSEC(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                NextDomainName,
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
    /// The DNS Next Secure (NSEC) resource record (RFC 4034).
    /// </summary>
    public class NSEC : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Next Secure (NSEC) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NSEC;

        #endregion

        #region Properties

        /// <summary>
        /// The next domain name in the canonical ordering of the zone.
        /// </summary>
        public DomainName  NextDomainName    { get; }

        /// <summary>
        /// The type bit maps indicating which RR types exist at the NSEC owner name.
        /// </summary>
        public Byte[]      TypeBitMaps       { get; }

        #endregion

        #region Constructor

        #region NSEC(Stream)

        /// <summary>
        /// Create a new NSEC resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NSEC resource record data.</param>
        public NSEC(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var startPos  = Stream.Position;

            this.NextDomainName = DNS.DomainName.Parse(
                                     DNSTools.ExtractName(Stream)
                                 );

            var bytesRead       = (Int32) (Stream.Position - startPos);
            this.TypeBitMaps    = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - bytesRead));

        }

        #endregion

        #region NSEC(DomainName, Stream)

        /// <summary>
        /// Create a new NSEC resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC resource record.</param>
        /// <param name="Stream">A stream containing the NSEC resource record data.</param>
        public NSEC(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var startPos  = Stream.Position;

            this.NextDomainName = DNS.DomainName.Parse(
                                     DNSTools.ExtractName(Stream)
                                 );

            var bytesRead       = (Int32) (Stream.Position - startPos);
            this.TypeBitMaps    = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - bytesRead));

        }

        #endregion

        #region NSEC(DomainName, Class, TimeToLive, NextDomainName, TypeBitMaps)

        /// <summary>
        /// Create a new DNS NSEC resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NSEC resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="NextDomainName">The next domain name in the canonical ordering of the zone.</param>
        /// <param name="TypeBitMaps">The type bit maps indicating which RR types exist at the NSEC owner name.</param>
        public NSEC(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    DomainName       NextDomainName,
                    Byte[]           TypeBitMaps)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.NextDomainName = NextDomainName;
            this.TypeBitMaps    = TypeBitMaps;

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
        public static NSEC? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 2);
                if (parts.Length < 1) return null;
                var nextName = parts[0].EndsWith('.') ? parts[0] : parts[0] + ".";
                return new NSEC(Name, DNSQueryClasses.IN, TimeToLive, DNS.DomainName.Parse(nextName), []);
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{NextDomainName} {DecodeTypeBitMaps(TypeBitMaps)}";

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

            var tempStream = new MemoryStream();

            // RDATA: Next Domain Name
            NextDomainName.Serialize(
                tempStream,
                (Int32) Stream.Position + 2,
                UseCompression,
                CompressionOffsets
            );

            // Type Bit Maps
            tempStream.Write(TypeBitMaps, 0, TypeBitMaps.Length);


            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes)
            Stream.WriteUInt16BE(tempStream.Length);

            // Copy RDATA (tempStream) to main stream
            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"NextDomainName={NextDomainName}, TypeBitMaps=[{TypeBitMaps.Length} bytes], {base.ToString()}";

        #endregion

    }

}
