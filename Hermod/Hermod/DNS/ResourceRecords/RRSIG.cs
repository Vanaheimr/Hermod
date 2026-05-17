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
    /// Extensions methods for DNS RRSIG resource records.
    /// </summary>
    public static class DNS_RRSIG_Extensions
    {

        #region CacheRRSIG(this DNSClient, DomainName, TypeCovered, Algorithm, Labels, OriginalTTL, SignatureExpiration, SignatureInception, KeyTag, SignerName, Signature, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS RRSIG record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this RRSIG resource record.</param>
        /// <param name="TypeCovered">The type of the RRSet covered by this signature.</param>
        /// <param name="Algorithm">The cryptographic algorithm used to create the signature.</param>
        /// <param name="Labels">The number of labels in the original RRSIG RR owner name.</param>
        /// <param name="OriginalTTL">The original TTL of the covered RRSet.</param>
        /// <param name="SignatureExpiration">The expiration time of the signature.</param>
        /// <param name="SignatureInception">The inception time of the signature.</param>
        /// <param name="KeyTag">The key tag of the DNSKEY record that created the signature.</param>
        /// <param name="SignerName">The domain name of the signer.</param>
        /// <param name="Signature">The cryptographic signature.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheRRSIG(this DNSClient          DNSClient,
                                      DomainName              DomainName,
                                      DNSResourceRecordTypes  TypeCovered,
                                      Byte                    Algorithm,
                                      Byte                    Labels,
                                      UInt32                  OriginalTTL,
                                      UInt32                  SignatureExpiration,
                                      UInt32                  SignatureInception,
                                      UInt16                  KeyTag,
                                      DomainName              SignerName,
                                      Byte[]                  Signature,
                                      DNSQueryClasses         Class        = DNSQueryClasses.IN,
                                      TimeSpan?               TimeToLive   = null)
        {

            var dnsRecord = new RRSIG(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                TypeCovered,
                                Algorithm,
                                Labels,
                                OriginalTTL,
                                SignatureExpiration,
                                SignatureInception,
                                KeyTag,
                                SignerName,
                                Signature
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Resource Record Signature (RRSIG) resource record (RFC 4034).
    /// </summary>
    public class RRSIG : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Resource Record Signature (RRSIG) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.RRSIG;

        #endregion

        #region Properties

        /// <summary>
        /// The type of the RRSet covered by this signature.
        /// </summary>
        public DNSResourceRecordTypes  TypeCovered             { get; }

        /// <summary>
        /// The cryptographic algorithm used to create the signature.
        /// </summary>
        public Byte                    Algorithm               { get; }

        /// <summary>
        /// The number of labels in the original RRSIG RR owner name.
        /// </summary>
        public Byte                    Labels                  { get; }

        /// <summary>
        /// The original TTL of the covered RRSet.
        /// </summary>
        public UInt32                  OriginalTTL             { get; }

        /// <summary>
        /// The expiration time of the signature (seconds since epoch).
        /// </summary>
        public UInt32                  SignatureExpiration      { get; }

        /// <summary>
        /// The inception time of the signature (seconds since epoch).
        /// </summary>
        public UInt32                  SignatureInception       { get; }

        /// <summary>
        /// The key tag of the DNSKEY record that created the signature.
        /// </summary>
        public UInt16                  KeyTag                  { get; }

        /// <summary>
        /// The domain name of the signer.
        /// </summary>
        public DomainName              SignerName              { get; }

        /// <summary>
        /// The cryptographic signature.
        /// </summary>
        public Byte[]                  Signature               { get; }

        #endregion

        #region Constructor

        #region RRSIG(Stream)

        /// <summary>
        /// Create a new RRSIG resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the RRSIG resource record data.</param>
        public RRSIG(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var startPos  = Stream.Position;

            this.TypeCovered          = (DNSResourceRecordTypes) Stream.ReadUInt16BE();
            this.Algorithm            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Labels               = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.OriginalTTL          = Stream.ReadUInt32BE();
            this.SignatureExpiration   = Stream.ReadUInt32BE();
            this.SignatureInception    = Stream.ReadUInt32BE();
            this.KeyTag               = Stream.ReadUInt16BE();

            this.SignerName           = DNS.DomainName.Parse(
                                           DNSTools.ExtractName(Stream)
                                       );

            var bytesRead             = (Int32) (Stream.Position - startPos);
            this.Signature            = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - bytesRead));

        }

        #endregion

        #region RRSIG(DomainName, Stream)

        /// <summary>
        /// Create a new RRSIG resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this RRSIG resource record.</param>
        /// <param name="Stream">A stream containing the RRSIG resource record data.</param>
        public RRSIG(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var startPos  = Stream.Position;

            this.TypeCovered          = (DNSResourceRecordTypes) Stream.ReadUInt16BE();
            this.Algorithm            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Labels               = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.OriginalTTL          = Stream.ReadUInt32BE();
            this.SignatureExpiration   = Stream.ReadUInt32BE();
            this.SignatureInception    = Stream.ReadUInt32BE();
            this.KeyTag               = Stream.ReadUInt16BE();

            this.SignerName           = DNS.DomainName.Parse(
                                           DNSTools.ExtractName(Stream)
                                       );

            var bytesRead             = (Int32) (Stream.Position - startPos);
            this.Signature            = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - bytesRead));

        }

        #endregion

        #region RRSIG(DomainName, Class, TimeToLive, TypeCovered, Algorithm, Labels, OriginalTTL, SignatureExpiration, SignatureInception, KeyTag, SignerName, Signature)

        /// <summary>
        /// Create a new DNS RRSIG resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this RRSIG resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="TypeCovered">The type of the RRSet covered by this signature.</param>
        /// <param name="Algorithm">The cryptographic algorithm used to create the signature.</param>
        /// <param name="Labels">The number of labels in the original RRSIG RR owner name.</param>
        /// <param name="OriginalTTL">The original TTL of the covered RRSet.</param>
        /// <param name="SignatureExpiration">The expiration time of the signature.</param>
        /// <param name="SignatureInception">The inception time of the signature.</param>
        /// <param name="KeyTag">The key tag of the DNSKEY record that created the signature.</param>
        /// <param name="SignerName">The domain name of the signer.</param>
        /// <param name="Signature">The cryptographic signature.</param>
        public RRSIG(DomainName              DomainName,
                     DNSQueryClasses         Class,
                     TimeSpan                TimeToLive,
                     DNSResourceRecordTypes  TypeCovered,
                     Byte                    Algorithm,
                     Byte                    Labels,
                     UInt32                  OriginalTTL,
                     UInt32                  SignatureExpiration,
                     UInt32                  SignatureInception,
                     UInt16                  KeyTag,
                     DomainName              SignerName,
                     Byte[]                  Signature)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.TypeCovered          = TypeCovered;
            this.Algorithm            = Algorithm;
            this.Labels               = Labels;
            this.OriginalTTL          = OriginalTTL;
            this.SignatureExpiration   = SignatureExpiration;
            this.SignatureInception    = SignatureInception;
            this.KeyTag               = KeyTag;
            this.SignerName           = SignerName;
            this.Signature            = Signature;

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

            var tempStream = new MemoryStream();

            // RDATA fixed fields (18 bytes)
            tempStream.WriteUInt16BE((UInt16) TypeCovered);
            tempStream.WriteByte    (Algorithm);
            tempStream.WriteByte    (Labels);
            tempStream.WriteUInt32BE(OriginalTTL);
            tempStream.WriteUInt32BE(SignatureExpiration);
            tempStream.WriteUInt32BE(SignatureInception);
            tempStream.WriteUInt16BE(KeyTag);

            // Signer Name (compressed domain name)
            SignerName.Serialize(
                tempStream,
                (Int32) Stream.Position + 2 + (Int32) tempStream.Position,
                UseCompression,
                CompressionOffsets
            );

            // Signature
            tempStream.Write(Signature, 0, Signature.Length);


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

            => $"TypeCovered={TypeCovered}, Algorithm={Algorithm}, Labels={Labels}, OriginalTTL={OriginalTTL}, KeyTag={KeyTag}, SignerName={SignerName}, {base.ToString()}";

        #endregion

    }

}
