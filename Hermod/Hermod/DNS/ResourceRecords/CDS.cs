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
    /// Extensions methods for DNS CDS resource records.
    /// </summary>
    public static class DNS_CDS_Extensions
    {

        #region CacheCDS(this DNSClient, DomainName, KeyTag, Algorithm, DigestType, Digest, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS CDS record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this CDS resource record.</param>
        /// <param name="KeyTag">The key tag of the DNSKEY record.</param>
        /// <param name="Algorithm">The algorithm of the DNSKEY record.</param>
        /// <param name="DigestType">The digest type used to create the digest.</param>
        /// <param name="Digest">The digest of the DNSKEY record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCDS(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    UInt16           KeyTag,
                                    Byte             Algorithm,
                                    Byte             DigestType,
                                    Byte[]           Digest,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CDS(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                KeyTag,
                                Algorithm,
                                DigestType,
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
    /// The DNS Child DS (CDS) resource record (RFC 7344).
    /// Used for automated DNSSEC key rotation between child and parent zone.
    /// </summary>
    public class CDS : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Child DS (CDS) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.CDS;

        #endregion

        #region Properties

        /// <summary>
        /// The key tag of the DNSKEY record referred to by this CDS record.
        /// </summary>
        public UInt16  KeyTag        { get; }

        /// <summary>
        /// The algorithm of the DNSKEY record referred to by this CDS record.
        /// </summary>
        public Byte    Algorithm     { get; }

        /// <summary>
        /// The digest type used to create the digest of the DNSKEY record.
        /// </summary>
        public Byte    DigestType    { get; }

        /// <summary>
        /// The digest of the DNSKEY record referred to by this CDS record.
        /// </summary>
        public Byte[]  Digest        { get; }

        #endregion

        #region Constructor

        #region CDS(Stream)

        /// <summary>
        /// Create a new CDS resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CDS resource record data.</param>
        public CDS(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.KeyTag      = Stream.ReadUInt16BE();
            this.Algorithm   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.DigestType  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Digest      = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 4));

        }

        #endregion

        #region CDS(DomainName, Stream)

        /// <summary>
        /// Create a new CDS resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CDS resource record.</param>
        /// <param name="Stream">A stream containing the CDS resource record data.</param>
        public CDS(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.KeyTag      = Stream.ReadUInt16BE();
            this.Algorithm   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.DigestType  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Digest      = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 4));

        }

        #endregion

        #region CDS(DomainName, Class, TimeToLive, KeyTag, Algorithm, DigestType, Digest)

        /// <summary>
        /// Create a new DNS CDS resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CDS resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="KeyTag">The key tag of the DNSKEY record.</param>
        /// <param name="Algorithm">The algorithm of the DNSKEY record.</param>
        /// <param name="DigestType">The digest type used to create the digest.</param>
        /// <param name="Digest">The digest of the DNSKEY record.</param>
        public CDS(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           KeyTag,
                   Byte             Algorithm,
                   Byte             DigestType,
                   Byte[]           Digest)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.KeyTag      = KeyTag;
            this.Algorithm   = Algorithm;
            this.DigestType  = DigestType;
            this.Digest      = Digest;

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

            // RDLENGTH (2 bytes): 4 (KeyTag + Algorithm + DigestType) + Digest.Length
            Stream.WriteUInt16BE(4 + Digest.Length);

            Stream.WriteUInt16BE(KeyTag);
            Stream.WriteByte    (Algorithm);
            Stream.WriteByte    (DigestType);

            Stream.Write        (Digest, 0, Digest.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"KeyTag={KeyTag}, Algorithm={Algorithm}, DigestType={DigestType}, Digest={Convert.ToHexString(Digest)}, {base.ToString()}";

        #endregion

    }

}
