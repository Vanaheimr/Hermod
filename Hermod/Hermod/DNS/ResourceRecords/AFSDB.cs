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
    /// Extensions methods for DNS AFSDB resource records.
    /// </summary>
    public static class DNS_AFSDB_Extensions
    {

        #region CacheAFSDB(this DNSClient, DomainName, Subtype, Hostname, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS AFSDB record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Subtype">The AFSDB subtype.</param>
        /// <param name="Hostname">The hostname of the AFS database server.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheAFSDB(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      UInt16           Subtype,
                                      DomainName       Hostname,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new AFSDB(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Subtype,
                                Hostname
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS AFS Database (AFSDB) resource record (RFC 1183).
    /// </summary>
    public class AFSDB : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS AFS Database (AFSDB) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.AFSDB;

        #endregion

        #region Properties

        /// <summary>
        /// The AFSDB subtype.
        /// </summary>
        public UInt16      Subtype     { get; }

        /// <summary>
        /// The hostname of the AFS database server.
        /// </summary>
        public DomainName  Hostname    { get; }

        #endregion

        #region Constructor

        #region AFSDB(Stream)

        /// <summary>
        /// Create a new AFSDB resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the AFSDB resource record data.</param>
        public AFSDB(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Subtype   = Stream.ReadUInt16BE();

            this.Hostname  = DNS.DomainName.Parse(
                                 DNSTools.ExtractName(Stream)
                             );

        }

        #endregion

        #region AFSDB(DomainName, Stream)

        /// <summary>
        /// Create a new AFSDB resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this AFSDB resource record.</param>
        /// <param name="Stream">A stream containing the AFSDB resource record data.</param>
        public AFSDB(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Subtype   = Stream.ReadUInt16BE();

            this.Hostname  = DNS.DomainName.Parse(
                                 DNSTools.ExtractName(Stream)
                             );

        }

        #endregion

        #region AFSDB(DomainName, Class, TimeToLive, Subtype, Hostname)

        /// <summary>
        /// Create a new DNS AFSDB resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this AFSDB resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Subtype">The AFSDB subtype.</param>
        /// <param name="Hostname">The hostname of the AFS database server.</param>
        public AFSDB(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     UInt16           Subtype,
                     DomainName       Hostname)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Subtype   = Subtype;
            this.Hostname  = Hostname;

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

            // Subtype (2 bytes)
            tempStream.WriteUInt16BE(Subtype);

            // Hostname domain name (with compression)
            var hostnameOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;
            Hostname.Serialize(
                tempStream,
                hostnameOffset,
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

            => $"Subtype: {Subtype}, Hostname: {Hostname}, {base.ToString()}";

        #endregion

    }

}
