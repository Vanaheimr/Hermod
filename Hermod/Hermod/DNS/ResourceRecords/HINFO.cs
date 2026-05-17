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
    /// Extensions methods for DNS HINFO resource records.
    /// </summary>
    public static class DNS_HINFO_Extensions
    {

        #region CacheHINFO(this DNSClient, DomainName, CPU, OS, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS HINFO record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this HINFO resource record.</param>
        /// <param name="CPU">The CPU type.</param>
        /// <param name="OS">The operating system type.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheHINFO(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      String           CPU,
                                      String           OS,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new HINFO(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                CPU,
                                OS
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Host Information (HINFO) resource record (RFC 1035).
    /// Stores CPU and OS information for the host.
    /// </summary>
    public class HINFO : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS HINFO resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.HINFO;

        #endregion

        #region Properties

        /// <summary>
        /// The CPU type.
        /// </summary>
        public String  CPU    { get; }

        /// <summary>
        /// The operating system type.
        /// </summary>
        public String  OS     { get; }

        #endregion

        #region Constructor

        #region HINFO(Stream)

        /// <summary>
        /// Create a new HINFO resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the HINFO resource record data.</param>
        public HINFO(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.CPU = DNSTools.ExtractCharacterString(Stream);
            this.OS  = DNSTools.ExtractCharacterString(Stream);

        }

        #endregion

        #region HINFO(DomainName, Stream)

        /// <summary>
        /// Create a new HINFO resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this HINFO resource record.</param>
        /// <param name="Stream">A stream containing the HINFO resource record data.</param>
        public HINFO(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.CPU = DNSTools.ExtractCharacterString(Stream);
            this.OS  = DNSTools.ExtractCharacterString(Stream);

        }

        #endregion

        #region HINFO(DomainName, Class, TimeToLive, CPU, OS)

        /// <summary>
        /// Create a new DNS HINFO resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this HINFO resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="CPU">The CPU type.</param>
        /// <param name="OS">The operating system type.</param>
        public HINFO(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     String           CPU,
                     String           OS)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.CPU = CPU;
            this.OS  = OS;

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

            var cpuBytes = Encoding.ASCII.GetBytes(CPU);
            var osBytes  = Encoding.ASCII.GetBytes(OS);

            // RDLENGTH (2 bytes): 1 (CPU length byte) + cpuBytes.Length + 1 (OS length byte) + osBytes.Length
            Stream.WriteUInt16BE(2 + cpuBytes.Length + osBytes.Length);

            // CPU character-string: length byte + data
            Stream.WriteByte((Byte) cpuBytes.Length);
            Stream.Write    (cpuBytes, 0, cpuBytes.Length);

            // OS character-string: length byte + data
            Stream.WriteByte((Byte) osBytes.Length);
            Stream.Write    (osBytes, 0, osBytes.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"CPU={CPU}, OS={OS}, {base.ToString()}";

        #endregion

    }

}
