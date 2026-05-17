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
    /// Extensions methods for DNS OPENPGPKEY resource records.
    /// </summary>
    public static class DNS_OPENPGPKEY_Extensions
    {

        #region CacheOPENPGPKEY(this DNSClient, DomainName, PublicKey, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS OPENPGPKEY record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this OPENPGPKEY resource record.</param>
        /// <param name="PublicKey">The OpenPGP public key data.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheOPENPGPKEY(this DNSClient   DNSClient,
                                           DomainName       DomainName,
                                           Byte[]           PublicKey,
                                           DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                           TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new OPENPGPKEY(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
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
    /// The DNS OpenPGP Public Key (OPENPGPKEY) resource record (RFC 7929).
    /// Stores OpenPGP public keys in DNS.
    /// </summary>
    public class OPENPGPKEY : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS OPENPGPKEY resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.OPENPGPKEY;

        #endregion

        #region Properties

        /// <summary>
        /// The OpenPGP public key data.
        /// </summary>
        public Byte[]  PublicKey    { get; }

        #endregion

        #region Constructor

        #region OPENPGPKEY(Stream)

        /// <summary>
        /// Create a new OPENPGPKEY resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the OPENPGPKEY resource record data.</param>
        public OPENPGPKEY(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.PublicKey = DNSTools.ExtractByteArray(Stream, rdLength);

        }

        #endregion

        #region OPENPGPKEY(DomainName, Stream)

        /// <summary>
        /// Create a new OPENPGPKEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this OPENPGPKEY resource record.</param>
        /// <param name="Stream">A stream containing the OPENPGPKEY resource record data.</param>
        public OPENPGPKEY(DomainName  DomainName,
                          Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.PublicKey = DNSTools.ExtractByteArray(Stream, rdLength);

        }

        #endregion

        #region OPENPGPKEY(DomainName, Class, TimeToLive, PublicKey)

        /// <summary>
        /// Create a new DNS OPENPGPKEY resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this OPENPGPKEY resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="PublicKey">The OpenPGP public key data.</param>
        public OPENPGPKEY(DomainName       DomainName,
                          DNSQueryClasses  Class,
                          TimeSpan         TimeToLive,
                          Byte[]           PublicKey)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.PublicKey = PublicKey;

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

            // RDLENGTH (2 bytes): PublicKey.Length
            Stream.WriteUInt16BE(PublicKey.Length);

            Stream.Write        (PublicKey, 0, PublicKey.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"PublicKey={BitConverter.ToString(PublicKey)}, {base.ToString()}";

        #endregion

    }

}
