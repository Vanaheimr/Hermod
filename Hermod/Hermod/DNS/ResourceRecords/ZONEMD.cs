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
    /// Extensions methods for DNS ZONEMD resource records.
    /// </summary>
    public static class DNS_ZONEMD_Extensions
    {

        #region CacheZONEMD(this DNSClient, DomainName, Serial, Scheme, HashAlgorithm, Digest, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS ZONEMD record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Serial">The SOA serial number of the zone.</param>
        /// <param name="Scheme">The digest scheme.</param>
        /// <param name="HashAlgorithm">The hash algorithm.</param>
        /// <param name="Digest">The digest value.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheZONEMD(this DNSClient   DNSClient,
                                       DomainName       DomainName,
                                       UInt32           Serial,
                                       Byte             Scheme,
                                       Byte             HashAlgorithm,
                                       Byte[]           Digest,
                                       DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                       TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new ZONEMD(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Serial,
                                Scheme,
                                HashAlgorithm,
                                Digest
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Zone Message Digest (ZONEMD) resource record (RFC 8976).
    /// </summary>
    public class ZONEMD : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Zone Message Digest (ZONEMD) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.ZONEMD;

        #endregion

        #region Properties

        /// <summary>
        /// The SOA serial number of the zone.
        /// </summary>
        public UInt32  Serial           { get; }

        /// <summary>
        /// The digest scheme.
        /// </summary>
        public Byte    Scheme           { get; }

        /// <summary>
        /// The hash algorithm.
        /// </summary>
        public Byte    HashAlgorithm    { get; }

        /// <summary>
        /// The digest value.
        /// </summary>
        public Byte[]  Digest           { get; }

        #endregion

        #region Constructor

        #region ZONEMD(Stream)

        /// <summary>
        /// Create a new ZONEMD resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the ZONEMD resource record data.</param>
        public ZONEMD(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Serial         = Stream.ReadUInt32BE();
            this.Scheme         = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.HashAlgorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Digest         = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 6));

        }

        #endregion

        #region ZONEMD(DomainName, Stream)

        /// <summary>
        /// Create a new ZONEMD resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this ZONEMD resource record.</param>
        /// <param name="Stream">A stream containing the ZONEMD resource record data.</param>
        public ZONEMD(DomainName  DomainName,
                      Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Serial         = Stream.ReadUInt32BE();
            this.Scheme         = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.HashAlgorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Digest         = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 6));

        }

        #endregion

        #region ZONEMD(DomainName, Class, TimeToLive, Serial, Scheme, HashAlgorithm, Digest)

        /// <summary>
        /// Create a new DNS ZONEMD resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this ZONEMD resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Serial">The SOA serial number of the zone.</param>
        /// <param name="Scheme">The digest scheme.</param>
        /// <param name="HashAlgorithm">The hash algorithm.</param>
        /// <param name="Digest">The digest value.</param>
        public ZONEMD(DomainName       DomainName,
                      DNSQueryClasses  Class,
                      TimeSpan         TimeToLive,
                      UInt32           Serial,
                      Byte             Scheme,
                      Byte             HashAlgorithm,
                      Byte[]           Digest)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Serial         = Serial;
            this.Scheme         = Scheme;
            this.HashAlgorithm  = HashAlgorithm;
            this.Digest         = Digest;

        }

        #endregion

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

            // RDLENGTH (2 bytes): 4 (Serial) + 1 (Scheme) + 1 (HashAlgorithm) + Digest.Length
            Stream.WriteUInt16BE(6 + Digest.Length);

            Stream.WriteUInt32BE(Serial);
            Stream.WriteByte    (Scheme);
            Stream.WriteByte    (HashAlgorithm);
            Stream.Write        (Digest, 0, Digest.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Serial: {Serial}, Scheme: {Scheme}, HashAlgorithm: {HashAlgorithm}, Digest: {BitConverter.ToString(Digest)}, {base.ToString()}";

        #endregion

    }

}
