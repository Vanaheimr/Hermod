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
    /// Extensions methods for DNS CDNSKEY resource records.
    /// </summary>
    public static class DNS_CDNSKEY_Extensions
    {

        #region CacheCDNSKEY(this DNSClient, DomainName, Flags, Protocol, Algorithm, PublicKey, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS CDNSKEY record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this CDNSKEY resource record.</param>
        /// <param name="Flags">The CDNSKEY flags field.</param>
        /// <param name="Protocol">The protocol field (must be 3).</param>
        /// <param name="Algorithm">The algorithm used for the public key.</param>
        /// <param name="PublicKey">The public key material.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCDNSKEY(this DNSClient   DNSClient,
                                        DomainName       DomainName,
                                        UInt16           Flags,
                                        Byte             Protocol,
                                        Byte             Algorithm,
                                        Byte[]           PublicKey,
                                        DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                        TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CDNSKEY(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Flags,
                                Protocol,
                                Algorithm,
                                PublicKey
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Child DNSKEY (CDNSKEY) resource record (RFC 7344).
    /// Used for automated DNSSEC key rotation between child and parent zone.
    /// </summary>
    public class CDNSKEY : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Child DNSKEY (CDNSKEY) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.CDNSKEY;

        #endregion

        #region Properties

        /// <summary>
        /// The CDNSKEY flags field.
        /// </summary>
        public UInt16  Flags        { get; }

        /// <summary>
        /// The protocol field (must be 3 for DNSSEC).
        /// </summary>
        public Byte    Protocol     { get; }

        /// <summary>
        /// The algorithm used for the public key.
        /// </summary>
        public Byte    Algorithm    { get; }

        /// <summary>
        /// The public key material.
        /// </summary>
        public Byte[]  PublicKey    { get; }

        #endregion

        #region Constructor

        #region CDNSKEY(Stream)

        /// <summary>
        /// Create a new CDNSKEY resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CDNSKEY resource record data.</param>
        public CDNSKEY(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Flags      = Stream.ReadUInt16BE();
            this.Protocol   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Algorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.PublicKey  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 4));

        }

        #endregion

        #region CDNSKEY(DomainName, Stream)

        /// <summary>
        /// Create a new CDNSKEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CDNSKEY resource record.</param>
        /// <param name="Stream">A stream containing the CDNSKEY resource record data.</param>
        public CDNSKEY(DomainName  DomainName,
                       Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Flags      = Stream.ReadUInt16BE();
            this.Protocol   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Algorithm  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.PublicKey  = DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 4));

        }

        #endregion

        #region CDNSKEY(DomainName, Class, TimeToLive, Flags, Protocol, Algorithm, PublicKey)

        /// <summary>
        /// Create a new DNS CDNSKEY resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CDNSKEY resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Flags">The CDNSKEY flags field.</param>
        /// <param name="Protocol">The protocol field (must be 3).</param>
        /// <param name="Algorithm">The algorithm used for the public key.</param>
        /// <param name="PublicKey">The public key material.</param>
        public CDNSKEY(DomainName       DomainName,
                       DNSQueryClasses  Class,
                       TimeSpan         TimeToLive,
                       UInt16           Flags,
                       Byte             Protocol,
                       Byte             Algorithm,
                       Byte[]           PublicKey)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Flags      = Flags;
            this.Protocol   = Protocol;
            this.Algorithm  = Algorithm;
            this.PublicKey  = PublicKey;

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
        public static CDNSKEY? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new CDNSKEY(Name, DNSQueryClasses.IN, TimeToLive,
                                   UInt16.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                   Convert.FromBase64String(parts[3]));
            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => $"{Flags} {Protocol} {Algorithm} {Convert.ToBase64String(PublicKey)}";

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

            // RDLENGTH (2 bytes): 4 (Flags + Protocol + Algorithm) + PublicKey.Length
            Stream.WriteUInt16BE(4 + PublicKey.Length);

            Stream.WriteUInt16BE(Flags);
            Stream.WriteByte    (Protocol);
            Stream.WriteByte    (Algorithm);

            Stream.Write        (PublicKey, 0, PublicKey.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Flags={Flags}, Protocol={Protocol}, Algorithm={Algorithm}, PublicKey=[{PublicKey.Length} bytes], {base.ToString()}";

        #endregion

    }

}
