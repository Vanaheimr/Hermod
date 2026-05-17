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
    /// Extensions methods for DNS RP resource records.
    /// </summary>
    public static class DNS_RP_Extensions
    {

        #region CacheRP(this DNSClient, DomainName, Mailbox, TxtDomainName, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS RP record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Mailbox">The mailbox of the responsible person.</param>
        /// <param name="TxtDomainName">The domain name of a TXT record with more information.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheRP(this DNSClient   DNSClient,
                                   DomainName       DomainName,
                                   DomainName       Mailbox,
                                   DomainName       TxtDomainName,
                                   DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                   TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new RP(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Mailbox,
                                TxtDomainName
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Responsible Person (RP) resource record (RFC 1183).
    /// </summary>
    public class RP : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Responsible Person (RP) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.RP;

        #endregion

        #region Properties

        /// <summary>
        /// The mailbox of the responsible person.
        /// </summary>
        public DomainName  Mailbox          { get; }

        /// <summary>
        /// The domain name of a TXT record with more information.
        /// </summary>
        public DomainName  TxtDomainName    { get; }

        #endregion

        #region Constructor

        #region RP(Stream)

        /// <summary>
        /// Create a new RP resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the RP resource record data.</param>
        public RP(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Mailbox        = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

            this.TxtDomainName  = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

        }

        #endregion

        #region RP(DomainName, Stream)

        /// <summary>
        /// Create a new RP resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this RP resource record.</param>
        /// <param name="Stream">A stream containing the RP resource record data.</param>
        public RP(DomainName  DomainName,
                  Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Mailbox        = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

            this.TxtDomainName  = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

        }

        #endregion

        #region RP(DomainName, Class, TimeToLive, Mailbox, TxtDomainName)

        /// <summary>
        /// Create a new DNS RP resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this RP resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Mailbox">The mailbox of the responsible person.</param>
        /// <param name="TxtDomainName">The domain name of a TXT record with more information.</param>
        public RP(DomainName       DomainName,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  DomainName       Mailbox,
                  DomainName       TxtDomainName)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Mailbox        = Mailbox;
            this.TxtDomainName  = TxtDomainName;

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

            // Mailbox domain name (with compression)
            var mailboxOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;
            Mailbox.Serialize(
                tempStream,
                mailboxOffset,
                UseCompression,
                CompressionOffsets
            );

            // TXT domain name (with compression)
            var txtOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;
            TxtDomainName.Serialize(
                tempStream,
                txtOffset,
                UseCompression,
                CompressionOffsets
            );


            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes): Variable, when compression is used!
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

            => $"Mailbox: {Mailbox}, TxtDomainName: {TxtDomainName}, {base.ToString()}";

        #endregion

    }

}
