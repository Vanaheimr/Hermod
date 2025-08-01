/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// Extensions methods for DNS PTR resource records.
    /// </summary>
    public static class DNS_PTR_Extensions
    {

        #region CachePTR(this DNSClient, DomainName, Target, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS PTR record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this PTR resource record.</param>
        /// <param name="Target">The text of this DNS Pointer (PTR) resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CachePTR(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    DNSServiceName   Target,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new PTR(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Target
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Pointer (PTR) resource record type, e.g. used for reverse DNS lookups.
    /// </summary>
    public class PTR : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Pointer (PTR) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.PTR;

        #endregion

        #region Properties

        /// <summary>
        /// The text of this DNS Pointer (PTR) resource record.
        /// Might also be a DNS Service Name within the context of DNS-Based Service Discovery (DNS-SD, RFC 6763).
        /// </summary>
        public DNSServiceName  Target    { get; }

        #endregion

        #region Constructor

        #region PTR(Stream)

        /// <summary>
        /// Create a new PTR resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the PTR resource record data.</param>
        public PTR(Stream  Stream)

            : base(Stream,
                   TypeId)

        {
            this.Target = DNSTools.ExtractDNSServiceName(Stream);
        }

        #endregion

        #region PTR(DomainName, Stream)

        /// <summary>
        /// Create a new PTR resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this PTR resource record.</param>
        /// <param name="Stream">A stream containing the PTR resource record data.</param>
        public PTR(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Target = DNSTools.ExtractDNSServiceName(Stream);

        }

        #endregion

        #region PTR(DomainName, Class, TimeToLive, Target)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Target">The text of this DNS Pointer (PTR) resource record.</param>
        public PTR(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   DNSServiceName   Target)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {
            this.Target = Target;
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
            Target.Serialize(
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

            => $"{Target}, {base.ToString()}";

        #endregion

    }

}
