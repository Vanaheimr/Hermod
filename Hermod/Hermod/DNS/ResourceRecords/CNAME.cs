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
    /// Extensions methods for DNS CNAME resource records.
    /// </summary>
    public static class DNS_CNAME_Extensions
    {

        #region CacheCNAME(this DNSClient, DomainName, CNAMERecord)

        /// <summary>
        /// Add a DNS CNAME record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="CName">The target of this CNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCNAME(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      DomainName       CName,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CNAME(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                CName
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Canonical Name (CNAME) resource record.
    /// </summary>
    public class CNAME : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Canonical Name (CNAME) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.CNAME;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS Canonical Name (CNAME).
        /// </summary>
        public DomainName  CName    { get; }

        #endregion

        #region Constructor

        #region CNAME(Stream)

        /// <summary>
        /// Create a new CNAME resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CNAME resource record data.</param>
        public CNAME(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            this.CName = DNS.DomainName.Parse(
                             DNSTools.ExtractName(Stream)
                         );

        }

        #endregion

        #region CNAME(DomainName, Stream)

        /// <summary>
        /// Create a new CNAME resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CNAME resource record.</param>
        /// <param name="Stream">A stream containing the CNAME resource record data.</param>
        public CNAME(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.CName = DomainName.Parse(
                             DNSTools.ExtractName(Stream)
                         );

        }

        #endregion

        #region CNAME(DomainName, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS CNAME resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="CName">The target of this CNAME resource record.</param>
        public CNAME(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     DomainName       CName)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.CName = CName;

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

            // RDATA: Canonical Name
            CName.Serialize(
                tempStream,
                (Int32) Stream.Position + 2,
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

            => $"{CName}, {base.ToString()}";

        #endregion

    }

}
