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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS CAA resource records.
    /// </summary>
    public static class DNS_CAA_Extensions
    {

        #region CacheCAA(this DNSClient, DomainName, Flags, Tag, Value, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS CAA record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this CAA resource record.</param>
        /// <param name="Flags">The CAA flags.</param>
        /// <param name="Tag">The CAA property tag.</param>
        /// <param name="Value">The CAA property value.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCAA(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    Byte             Flags,
                                    String           Tag,
                                    String           Value,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CAA(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Flags,
                                Tag,
                                Value
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Certification Authority Authorization (CAA) resource record (RFC 8659).
    /// Specifies which CAs may issue certificates for a domain.
    /// </summary>
    public class CAA : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS CAA resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.CAA;

        #endregion

        #region Properties

        /// <summary>
        /// The CAA flags.
        /// </summary>
        public Byte    Flags    { get; }

        /// <summary>
        /// The CAA property tag (e.g. "issue", "issuewild", "iodef").
        /// </summary>
        public String  Tag      { get; }

        /// <summary>
        /// The CAA property value.
        /// </summary>
        public String  Value    { get; }

        #endregion

        #region Constructor

        #region CAA(Stream)

        /// <summary>
        /// Create a new CAA resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CAA resource record data.</param>
        public CAA(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength  = Stream.ReadUInt16BE();

            this.Flags    = (Byte) (Stream.ReadByte() & Byte.MaxValue);

            var tagLength = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Tag      = Encoding.ASCII.GetString(DNSTools.ExtractByteArray(Stream, tagLength));
            this.Value    = Encoding.ASCII.GetString(DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 2 - tagLength)));

        }

        #endregion

        #region CAA(DomainName, Stream)

        /// <summary>
        /// Create a new CAA resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CAA resource record.</param>
        /// <param name="Stream">A stream containing the CAA resource record data.</param>
        public CAA(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();

            this.Flags    = (Byte) (Stream.ReadByte() & Byte.MaxValue);

            var tagLength = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Tag      = Encoding.ASCII.GetString(DNSTools.ExtractByteArray(Stream, tagLength));
            this.Value    = Encoding.ASCII.GetString(DNSTools.ExtractByteArray(Stream, (UInt32)(rdLength - 2 - tagLength)));

        }

        #endregion

        #region CAA(DomainName, Class, TimeToLive, Flags, Tag, Value)

        /// <summary>
        /// Create a new DNS CAA resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CAA resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Flags">The CAA flags.</param>
        /// <param name="Tag">The CAA property tag.</param>
        /// <param name="Value">The CAA property value.</param>
        public CAA(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   Byte             Flags,
                   String           Tag,
                   String           Value)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Flags  = Flags;
            this.Tag    = Tag;
            this.Value  = Value;

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

            var tagBytes   = Encoding.ASCII.GetBytes(Tag);
            var valueBytes = Encoding.ASCII.GetBytes(Value);

            // RDLENGTH (2 bytes): 1 (Flags) + 1 (TagLength) + tagBytes.Length + valueBytes.Length
            Stream.WriteUInt16BE(2 + tagBytes.Length + valueBytes.Length);

            Stream.WriteByte((Byte) Flags);
            Stream.WriteByte((Byte) tagBytes.Length);

            Stream.Write    (tagBytes,   0, tagBytes.Length);
            Stream.Write    (valueBytes, 0, valueBytes.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Flags={Flags}, Tag={Tag}, Value={Value}, {base.ToString()}";

        #endregion

    }

}
