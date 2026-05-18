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

    #region (enum) TLSA_CertificateUsage

    /// <summary>
    /// The TLSA certificate usage field.
    /// https://www.rfc-editor.org/rfc/rfc6698
    /// </summary>
    public enum TLSA_CertificateUsage : Byte
    {
        PKIX_TA  = 0,
        PKIX_EE  = 1,
        DANE_TA  = 2,
        DANE_EE  = 3
    }

    #endregion

    #region (enum) TLSA_Selector

    /// <summary>
    /// The TLSA selector field.
    /// https://www.rfc-editor.org/rfc/rfc6698
    /// </summary>
    public enum TLSA_Selector : Byte
    {
        FullCertificate      = 0,
        SubjectPublicKeyInfo = 1
    }

    #endregion

    #region (enum) TLSA_MatchingType

    /// <summary>
    /// The TLSA matching type field.
    /// https://www.rfc-editor.org/rfc/rfc6698
    /// </summary>
    public enum TLSA_MatchingType : Byte
    {
        Full   = 0,
        SHA256 = 1,
        SHA512 = 2
    }

    #endregion


    /// <summary>
    /// Extensions methods for DNS TLSA resource records.
    /// </summary>
    public static class DNS_TLSA_Extensions
    {

        #region CacheTLSA(this DNSClient, DomainName, CertificateUsage, Selector, MatchingType, CertificateAssociationData, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS TLSA record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this TLSA resource record.</param>
        /// <param name="CertificateUsage">The TLSA certificate usage.</param>
        /// <param name="Selector">The TLSA selector.</param>
        /// <param name="MatchingType">The TLSA matching type.</param>
        /// <param name="CertificateAssociationData">The certificate association data.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheTLSA(this DNSClient   DNSClient,
                                     DomainName       DomainName,
                                     Byte             CertificateUsage,
                                     Byte             Selector,
                                     Byte             MatchingType,
                                     Byte[]           CertificateAssociationData,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new TLSA(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                CertificateUsage,
                                Selector,
                                MatchingType,
                                CertificateAssociationData
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS TLSA resource record (RFC 6698).
    /// Associates a TLS certificate with a domain name (DANE).
    /// </summary>
    public class TLSA : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS TLSA resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.TLSA;

        #endregion

        #region Properties

        /// <summary>
        /// The TLSA certificate usage.
        /// </summary>
        public Byte    CertificateUsage             { get; }

        /// <summary>
        /// The TLSA selector.
        /// </summary>
        public Byte    Selector                     { get; }

        /// <summary>
        /// The TLSA matching type.
        /// </summary>
        public Byte    MatchingType                 { get; }

        /// <summary>
        /// The certificate association data.
        /// </summary>
        public Byte[]  CertificateAssociationData   { get; }

        #endregion

        #region Constructor

        #region TLSA(Stream)

        /// <summary>
        /// Create a new TLSA resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the TLSA resource record data.</param>
        public TLSA(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.CertificateUsage            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Selector                    = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.MatchingType                = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.CertificateAssociationData  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 3));

        }

        #endregion

        #region TLSA(DomainName, Stream)

        /// <summary>
        /// Create a new TLSA resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this TLSA resource record.</param>
        /// <param name="Stream">A stream containing the TLSA resource record data.</param>
        public TLSA(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.CertificateUsage            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Selector                    = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.MatchingType                = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.CertificateAssociationData  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 3));

        }

        #endregion

        #region TLSA(DomainName, Class, TimeToLive, CertificateUsage, Selector, MatchingType, CertificateAssociationData)

        /// <summary>
        /// Create a new DNS TLSA resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this TLSA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="CertificateUsage">The TLSA certificate usage.</param>
        /// <param name="Selector">The TLSA selector.</param>
        /// <param name="MatchingType">The TLSA matching type.</param>
        /// <param name="CertificateAssociationData">The certificate association data.</param>
        public TLSA(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    Byte             CertificateUsage,
                    Byte             Selector,
                    Byte             MatchingType,
                    Byte[]           CertificateAssociationData)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.CertificateUsage            = CertificateUsage;
            this.Selector                    = Selector;
            this.MatchingType                = MatchingType;
            this.CertificateAssociationData  = CertificateAssociationData;

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
        public static TLSA? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new TLSA(Name, DNSQueryClasses.IN, TimeToLive,
                                Byte.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{CertificateUsage} {Selector} {MatchingType} {Convert.ToHexString(CertificateAssociationData).ToLowerInvariant()}";

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

            // RDLENGTH (2 bytes): 3 (CertificateUsage + Selector + MatchingType) + CertificateAssociationData.Length
            Stream.WriteUInt16BE(3 + CertificateAssociationData.Length);

            Stream.WriteByte(CertificateUsage);
            Stream.WriteByte(Selector);
            Stream.WriteByte(MatchingType);

            Stream.Write    (CertificateAssociationData, 0, CertificateAssociationData.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Usage={CertificateUsage}, Selector={Selector}, MatchingType={MatchingType}, Data={BitConverter.ToString(CertificateAssociationData)}, {base.ToString()}";

        #endregion

    }

}
