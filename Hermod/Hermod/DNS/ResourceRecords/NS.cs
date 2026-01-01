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
    /// Extensions methods for DNS NS resource records.
    /// </summary>
    public static class DNS_NS_Extensions
    {

        #region CacheAAAA(this DNSClient, DomainName, NameServer, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS NS record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this AAAA resource record.</param>
        /// <param name="NameServer">The text of the NS DNS Resource Record, which is the domain name of the authoritative name server.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheNS(this DNSClient   DNSClient,
                                   DomainName       DomainName,
                                   DomainName       NameServer,
                                   DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                   TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new NS(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                NameServer
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Name Server (NS) resource record.
    /// </summary>
    public class NS : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Name Server (NS) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NS;

        #endregion

        #region Properties

        /// <summary>
        /// The text of the NS DNS Resource Record, which is the domain name of the authoritative name server.
        /// </summary>
        public DomainName  NameServer    { get; }

        #endregion

        #region Constructor

        #region NS(Stream)

        /// <summary>
        /// Create a new NS resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NS resource record data.</param>
        public NS(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.NameServer = DNS.DomainName.Parse(
                                  DNSTools.ExtractName(Stream)
                              );

        }

        #endregion

        #region NS(DomainName, Stream)

        /// <summary>
        /// Create a new NS resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NS resource record.</param>
        /// <param name="Stream">A stream containing the NS resource record data.</param>
        public NS(DomainName  DomainName,
                  Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.NameServer = DNSTools.ExtractDomainName(Stream);

        }

        #endregion

        #region NS(DomainName, Class, TimeToLive, NameServer)

        /// <summary>
        /// Create a new DNS NS resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NS resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="NameServer">The text of the NS DNS Resource Record, which is the domain name of the authoritative name server.</param>
        public NS(DomainName       DomainName,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  DomainName       NameServer)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.NameServer = NameServer;

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

            // RDATA: Target domain name
            NameServer.Serialize(
                tempStream,
                (Int32) Stream.Position + 2,
                UseCompression,
                CompressionOffsets
            );


            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH: Variable, when compression is used!
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

            => $"{NameServer}, {base.ToString()}";

        #endregion

    }

}
