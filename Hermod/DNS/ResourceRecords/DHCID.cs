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

#region Usings

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS DHCID resource records.
    /// </summary>
    public static class DNS_DHCID_Extensions
    {

        #region CacheDHCID(this DNSClient, DomainName, Data, Class = IN, TimeToLive = 1day)

        /// <summary>
        /// Add a DNS DHCID record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this DHCID resource record.</param>
        /// <param name="Data">The DHCID RDATA: identifier type code, digest type code and digest.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheDHCID(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      Byte[]           Data,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new DHCID(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(1),
                                Data
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS DHCID resource record (RFC 4701), which ties a DHCP client's
    /// identity to a domain name so that two clients cannot silently claim the
    /// same one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 4701 §3.1 gives the RDATA three fields — a 2-octet identifier type
    /// code, a 1-octet digest type code, and the digest — but §3.5 gives the
    /// presentation format as one thing: "In DNS master files, the RDATA is
    /// represented as a single block in base-64 encoding".
    /// </para>
    /// <para>
    /// So the block is what this record stores, and the three fields are a view
    /// onto it. That way round matters: a DHCID whose RDATA is shorter than the
    /// three fields — which §3.5's encoding permits to exist on the wire, since
    /// nothing in the base-64 block declares a minimum — still round-trips
    /// byte for byte instead of being rejected or silently repaired. The fields
    /// simply report that they are not there.
    /// </para>
    /// </remarks>
    public class DHCID : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS DHCID resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.DHCID;

        /// <summary>
        /// The identifier type code and digest type code together (RFC 4701 §3.1).
        /// </summary>
        private const Int32 HeaderLength = 3;

        #endregion

        #region Properties

        /// <summary>
        /// The DHCID RDATA as it appears on the wire and, base-64 encoded, in a
        /// zone file (RFC 4701 §3.5).
        /// </summary>
        public Byte[]   Data              { get; }

        /// <summary>
        /// The identifier type code (RFC 4701 §3.3), or null when the RDATA is
        /// too short to carry one.
        /// </summary>
        public UInt16?  IdentifierType    { get; }

        /// <summary>
        /// The digest type code (RFC 4701 §3.4) — 1 is SHA-256 — or null when the
        /// RDATA is too short to carry one.
        /// </summary>
        public Byte?    DigestType        { get; }

        /// <summary>
        /// The digest itself, or null when the RDATA is too short to carry the
        /// two type codes that precede it.
        /// </summary>
        public Byte[]?  Digest            { get; }

        #endregion

        #region Constructor(s)

        #region DHCID(DomainName, Stream)

        /// <summary>
        /// Create a new DHCID resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this DHCID resource record.</param>
        /// <param name="Stream">A stream containing the DHCID resource record data.</param>
        public DHCID(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Data            = DNSTools.ExtractByteArray(Stream, rdLength);

            if (Data.Length >= HeaderLength)
            {
                this.IdentifierType  = (UInt16) ((Data[0] << 8) | Data[1]);
                this.DigestType      = Data[2];
                this.Digest          = Data[HeaderLength..];
            }

        }

        #endregion

        #region DHCID(DomainName, Class, TimeToLive, Data)

        /// <summary>
        /// Create a new DNS DHCID resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this DHCID resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Data">The DHCID RDATA: identifier type code, digest type code and digest.</param>
        public DHCID(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     Byte[]           Data)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Data            = Data;

            if (Data.Length >= HeaderLength)
            {
                this.IdentifierType  = (UInt16) ((Data[0] << 8) | Data[1]);
                this.DigestType      = Data[2];
                this.Digest          = Data[HeaderLength..];
            }

        }

        #endregion

        #region DHCID(DomainName, Class, TimeToLive, IdentifierType, DigestType, Digest)

        /// <summary>
        /// Create a new DNS DHCID resource record from its three fields.
        /// </summary>
        /// <param name="DomainName">The domain name of this DHCID resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="IdentifierType">The identifier type code (RFC 4701 §3.3).</param>
        /// <param name="DigestType">The digest type code (RFC 4701 §3.4); 1 is SHA-256.</param>
        /// <param name="Digest">The digest.</param>
        public DHCID(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     UInt16           IdentifierType,
                     Byte             DigestType,
                     Byte[]           Digest)

            : this(DomainName,
                   Class,
                   TimeToLive,
                   [ (Byte) (IdentifierType >> 8), (Byte) (IdentifierType & 0xFF), DigestType, .. Digest ])

        { }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field, which
        /// carries the same base-64 block RFC 4701 §3.5 puts in a zone file.
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static DHCID? TryParseFromJSON(DomainName  Name,
                                              TimeSpan    TimeToLive,
                                              String      Data)
        {
            try
            {

                // RFC 4701 §3.5 permits the block to be split over several lines
                // inside parentheses, and the zone-file tokenizer hands those over
                // as separate words; base-64 does not mind being reassembled.
                var base64 = Data.Replace(" ", "").Replace("\t", "");

                return new DHCID(
                           Name,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           Convert.FromBase64String(base64)
                       );

            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => Convert.ToBase64String(Data);

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

            Stream.WriteUInt16BE(Data.Length);
            Stream.Write        (Data, 0, Data.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{(IdentifierType.HasValue ? $"IdentifierType={IdentifierType}, DigestType={DigestType}, " : "")}{Convert.ToBase64String(Data)}, {base.ToString()}";

        #endregion

    }

}
