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
    /// The DNS SIG resource record (RFC 2535 §4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SIG and RRSIG carry the same nine fields in the same order, and RFC 3755
    /// took SIG's original job — signing an RRset in a zone — away and gave it to
    /// <see cref="RRSIG"/>. What is left is the one use RFC 3755 explicitly kept:
    /// a "type covered" of zero, which makes the record a SIG(0), the per-message
    /// signature of RFC 2931.
    /// </para>
    /// <para>
    /// A SIG(0) is a meta-RR. It is never stored in a zone, never cached, and
    /// exists only for the lifetime of the one message it authenticates — which
    /// is why RFC 2931 §3 says its owner name, class, TTL and original TTL are
    /// meaningless, and asks for the root name, class ANY and TTL 0. Everything
    /// that matters is in the RDATA and in the message it follows.
    /// </para>
    /// </remarks>
    public class SIG : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS SIG resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.SIG;

        /// <summary>
        /// The "type covered" value that makes a SIG a transaction signature
        /// rather than a signature over an RRset (RFC 2931 §3).
        /// </summary>
        public const DNSResourceRecordTypes TransactionSignature = 0;

        #endregion

        #region Properties

        /// <summary>
        /// The type of the RRSet covered by this signature, or zero for a SIG(0).
        /// </summary>
        public DNSResourceRecordTypes  TypeCovered           { get; }

        /// <summary>
        /// The cryptographic algorithm used to create the signature.
        /// </summary>
        public Byte                    Algorithm             { get; }

        /// <summary>
        /// The number of labels in the original owner name; zero for a SIG(0).
        /// </summary>
        public Byte                    Labels                { get; }

        /// <summary>
        /// The original TTL of the covered RRSet; zero for a SIG(0).
        /// </summary>
        public UInt32                  OriginalTTL           { get; }

        /// <summary>
        /// The expiration time of the signature (seconds since epoch).
        /// </summary>
        public UInt32                  SignatureExpiration   { get; }

        /// <summary>
        /// The inception time of the signature (seconds since epoch).
        /// </summary>
        public UInt32                  SignatureInception    { get; }

        /// <summary>
        /// The key tag of the KEY record that made the signature.
        /// </summary>
        public UInt16                  KeyTag                { get; }

        /// <summary>
        /// The owner name of the KEY record that made the signature. RFC 2931 §3
        /// requires a KEY to exist at this name holding the matching public key.
        /// </summary>
        public DomainName              SignerName            { get; }

        /// <summary>
        /// The signature itself.
        /// </summary>
        public Byte[]                  Signature             { get; }


        /// <summary>Whether this is a SIG(0) — a signature over a message rather than over an RRset.</summary>
        public Boolean IsTransactionSignature
            => TypeCovered == TransactionSignature;

        #endregion

        #region Constructor

        #region SIG(DomainName,     Stream)

        /// <summary>
        /// Create a new SIG resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this SIG resource record.</param>
        /// <param name="Stream">A stream containing the SIG resource record data.</param>
        public SIG(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();
            var startPos = Stream.Position;

            this.TypeCovered          = (DNSResourceRecordTypes) Stream.ReadUInt16BE();
            this.Algorithm            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Labels               = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.OriginalTTL          = Stream.ReadUInt32BE();
            this.SignatureExpiration  = Stream.ReadUInt32BE();
            this.SignatureInception   = Stream.ReadUInt32BE();
            this.KeyTag               = Stream.ReadUInt16BE();

            this.SignerName           = DNS.DomainName.ParseLenient(
                                            DNSTools.ExtractName(Stream)
                                        );

            var bytesRead             = (Int32) (Stream.Position - startPos);
            this.Signature            = DNSTools.ExtractByteArray(Stream, (UInt32) (rdLength - bytesRead));

        }

        #endregion

        #region SIG(DNSServiceName, Stream)

        /// <summary>
        /// Create a new SIG resource record from the given name and stream.
        /// </summary>
        /// <param name="DNSServiceName">The DNS service name of this SIG resource record.</param>
        /// <param name="Stream">A stream containing the SIG resource record data.</param>
        public SIG(DNSServiceName  DNSServiceName,
                   Stream          Stream)

            : base(DNSServiceName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();
            var startPos = Stream.Position;

            this.TypeCovered          = (DNSResourceRecordTypes) Stream.ReadUInt16BE();
            this.Algorithm            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Labels               = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.OriginalTTL          = Stream.ReadUInt32BE();
            this.SignatureExpiration  = Stream.ReadUInt32BE();
            this.SignatureInception   = Stream.ReadUInt32BE();
            this.KeyTag               = Stream.ReadUInt16BE();

            this.SignerName           = DNS.DomainName.ParseLenient(
                                            DNSTools.ExtractName(Stream)
                                        );

            var bytesRead             = (Int32) (Stream.Position - startPos);
            this.Signature            = DNSTools.ExtractByteArray(Stream, (UInt32) (rdLength - bytesRead));

        }

        #endregion

        #region SIG(DomainName,     Class, TimeToLive, TypeCovered, Algorithm, Labels, OriginalTTL, SignatureExpiration, SignatureInception, KeyTag, SignerName, Signature)

        /// <summary>
        /// Create a new DNS SIG resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this SIG resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="TypeCovered">The type of the RRSet covered by this signature, or zero for a SIG(0).</param>
        /// <param name="Algorithm">The cryptographic algorithm used to create the signature.</param>
        /// <param name="Labels">The number of labels in the original owner name.</param>
        /// <param name="OriginalTTL">The original TTL of the covered RRSet.</param>
        /// <param name="SignatureExpiration">The expiration time of the signature.</param>
        /// <param name="SignatureInception">The inception time of the signature.</param>
        /// <param name="KeyTag">The key tag of the KEY record that made the signature.</param>
        /// <param name="SignerName">The owner name of that KEY record.</param>
        /// <param name="Signature">The signature.</param>
        public SIG(DomainName              DomainName,
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
            this.SignatureExpiration  = SignatureExpiration;
            this.SignatureInception   = SignatureInception;
            this.KeyTag               = KeyTag;
            this.SignerName           = SignerName;
            this.Signature            = Signature;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field.
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        public static SIG? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {

                var parts = Data.Split(' ', 9);

                if (parts.Length < 9)
                    return null;

                if (!Enum.TryParse<DNSResourceRecordTypes>(parts[0], true, out var typeCovered) &&
                    !(parts[0] == "0" || parts[0].Equals("TYPE0", StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                var signerName = parts[7].EndsWith('.') ? parts[7] : parts[7] + ".";

                return new SIG(
                           Name,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           typeCovered,
                           Byte.  Parse(parts[1]),
                           Byte.  Parse(parts[2]),
                           UInt32.Parse(parts[3]),
                           UInt32.Parse(parts[4]),
                           UInt32.Parse(parts[5]),
                           UInt16.Parse(parts[6]),
                           DNS.DomainName.ParseLenient(signerName),
                           Convert.FromBase64String(parts[8])
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
        {

            var expiration = DateTimeOffset.FromUnixTimeSeconds(SignatureExpiration).UtcDateTime.ToString("yyyyMMddHHmmss");
            var inception  = DateTimeOffset.FromUnixTimeSeconds(SignatureInception). UtcDateTime.ToString("yyyyMMddHHmmss");

            var covered    = IsTransactionSignature
                                 ? "TYPE0"
                                 : TypeCovered.ToString();

            return $"{covered} {Algorithm} {Labels} {OriginalTTL} {expiration} {inception} {KeyTag} {SignerName} {Convert.ToBase64String(Signature)}";

        }

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the SIG resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Whether to use name compression.</param>
        /// <param name="CompressionOffsets">An optional dictionary for name compression offsets.</param>
        /// <remarks>
        /// The signer's name is written uncompressed regardless of what the caller
        /// asks for. RFC 2535 §4.1.7 forbids compressing it, and for good reason:
        /// the name is part of the signed data, and a verifier that re-serializes
        /// the record has no surrounding message to resolve a pointer against.
        /// </remarks>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            using var rdata = new MemoryStream();

            rdata.WriteUInt16BE((UInt16) TypeCovered);
            rdata.WriteByte    (Algorithm);
            rdata.WriteByte    (Labels);
            rdata.WriteUInt32BE(OriginalTTL);
            rdata.WriteUInt32BE(SignatureExpiration);
            rdata.WriteUInt32BE(SignatureInception);
            rdata.WriteUInt16BE(KeyTag);

            var signer = DNSTools.SerializeCanonicalName(SignerName.FullName);
            rdata.Write(signer, 0, signer.Length);

            rdata.Write(Signature, 0, Signature.Length);

            var rdataBytes = rdata.ToArray();

            if (rdataBytes.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            Stream.WriteUInt16BE((UInt16) rdataBytes.Length);
            Stream.Write(rdataBytes, 0, rdataBytes.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"TypeCovered={(IsTransactionSignature ? "SIG(0)" : TypeCovered.ToString())}, Algorithm={Algorithm}, KeyTag={KeyTag}, SignerName={SignerName}, {base.ToString()}";

        #endregion

    }

}
