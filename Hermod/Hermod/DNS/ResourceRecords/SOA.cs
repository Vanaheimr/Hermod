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

#region Usings

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS SOA resource records.
    /// </summary>
    public static class DNS_SOA_Extensions
    {

        #region CacheSOA(this DNSClient, DomainName, Server, Email, Serial, Refresh, Retry, Expire, Minimum, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS SOA record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this SOA resource record.</param>
        /// <param name="Server">The name of the DNS server that is authoritative for the domain.</param>
        /// <param name="Email">The email address of the person responsible for the domain.</param>
        /// <param name="Serial">The serial number of the zone file, which is incremented each time the zone file is updated.</param>
        /// <param name="Refresh">The time interval (in seconds) that a secondary DNS server should wait before refreshing its zone file from the primary DNS server.</param>
        /// <param name="Retry">The time interval (in seconds) that a secondary DNS server should wait before retrying a failed refresh attempt.</param>
        /// <param name="Expire">The time interval (in seconds) that a secondary DNS server should consider the zone file valid before it expires.</param>
        /// <param name="Minimum">The minimum time interval (in seconds) that a DNS resolver should cache the SOA record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheSOA(this DNSClient      DNSClient,
                                    DomainName          DomainName,
                                    DomainName          Server,
                                    SimpleEMailAddress  Email,
                                    UInt32              Serial,
                                    TimeSpan            Refresh,
                                    TimeSpan            Retry,
                                    TimeSpan            Expire,
                                    TimeSpan            Minimum,
                                    DNSQueryClasses     Class        = DNSQueryClasses.IN,
                                    TimeSpan?           TimeToLive   = null)
        {

            var dnsRecord = new SOA(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Server,
                                Email,
                                Serial,
                                Refresh,
                                Retry,
                                Expire,
                                Minimum
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Start of Authority (SOA) resource record.
    /// </summary>
    public class SOA : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Start of Authority (SOA) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.SOA;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the DNS server that is authoritative for the domain.
        /// </summary>
        public DomainName          Server     { get; }

        /// <summary>
        /// The email address of the person responsible for the domain.
        /// </summary>
        public SimpleEMailAddress  EMail      { get; }

        /// <summary>
        /// The serial number of the zone file, which is incremented each time the zone file is updated.
        /// </summary>
        public UInt32              Serial     { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should wait before refreshing its zone file from the primary DNS server.
        /// </summary>
        public TimeSpan            Refresh    { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should wait before retrying a failed refresh attempt.
        /// </summary>
        public TimeSpan            Retry      { get; }

        /// <summary>
        /// The time interval (in seconds) that a secondary DNS server should consider the zone file valid before it expires.
        /// </summary>
        public TimeSpan            Expire     { get; }

        /// <summary>
        /// The minimum time interval (in seconds) that a DNS resolver should cache the SOA record.
        /// </summary>
        public TimeSpan            Minimum    { get; }

        #endregion

        #region Constructor

        #region SOA(Stream)

        /// <summary>
        /// Create a new SOA resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the SOA resource record data.</param>
        public SOA(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Server   = DNS.DomainName.    Parse(DNSTools.ExtractName(Stream));
            this.EMail    = SimpleEMailAddress.Parse(DNSTools.ExtractName(Stream));
            this.Serial   = (UInt32) ((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Refresh  = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Retry    = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Expire   = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Minimum  = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);

        }

        #endregion

        #region SOA(DomainName, Stream)

        /// <summary>
        /// Create a new SOA resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this SOA resource record.</param>
        /// <param name="Stream">A stream containing the SOA resource record data.</param>
        public SOA(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            var ss = (Stream as MemoryStream).ToArray().ToHexString();

            //var aa = DNSTools.ReadDomainNameFromBytes(Stream);
            //var bb = DNSTools.ReadDomainNameFromBytes(Stream);

            //var server = DNSTools.ExtractName(Stream);

            //if (server == "")
            //{
            //    var data = Stream.ToByteArray().ToHexString();
            //    server = ".";
            //}

            this.Server   = DomainName.        Parse(DNSTools.ExtractName(Stream));
            this.EMail    = SimpleEMailAddress.Parse(DNSTools.ReplaceFirstDotWithAt(DNSTools.ExtractName(Stream)));
            this.Serial   = (UInt32) ((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Refresh  = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Retry    = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Expire   = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);
            this.Minimum  = TimeSpan.FromSeconds((Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue);

        }

        #endregion

        #region SOA(DomainName, Class, TimeToLive, ...)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Server">The name of the DNS server that is authoritative for the domain.</param>
        /// <param name="EMail">The email address of the person responsible for the domain.</param>
        /// <param name="Serial">The serial number of the zone file, which is incremented each time the zone file is updated.</param>
        /// <param name="Refresh">The time interval (in seconds) that a secondary DNS server should wait before refreshing its zone file from the primary DNS server.</param>
        /// <param name="Retry">The time interval (in seconds) that a secondary DNS server should wait before retrying a failed refresh attempt.</param>
        /// <param name="Expire">The time interval (in seconds) that a secondary DNS server should consider the zone file valid before it expires.</param>
        /// <param name="Minimum">The minimum time interval (in seconds) that a DNS resolver should cache the SOA record.</param>
        public SOA(DomainName          DomainName,
                   DNSQueryClasses     Class,
                   TimeSpan            TimeToLive,
                   DomainName          Server,
                   SimpleEMailAddress  EMail,
                   UInt32              Serial,
                   TimeSpan            Refresh,
                   TimeSpan            Retry,
                   TimeSpan            Expire,
                   TimeSpan            Minimum)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Server   = Server;
            this.EMail    = EMail;
            this.Serial   = Serial;
            this.Refresh  = Refresh;
            this.Retry    = Retry;
            this.Expire   = Expire;
            this.Minimum  = Minimum;

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

            // MNAME (primary name server, domain-name with compression)
            var mnameOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            Server.Serialize(
                tempStream,
                mnameOffset,
                UseCompression,
                CompressionOffsets
            );

            // RNAME (responsible person's mailbox, domain-name with compression)
            var rnameOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // Update offset
            EMail.ToString().Replace("@", ".").Serialize(
                tempStream,
                rnameOffset,
                UseCompression,
                CompressionOffsets
            );

            tempStream.WriteUInt32BE  (Serial);
            tempStream.WriteTimeSpanBE(Refresh);
            tempStream.WriteTimeSpanBE(Retry);
            tempStream.WriteTimeSpanBE(Expire);
            tempStream.WriteTimeSpanBE(Minimum);

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH: Variable, when compression is used!
            Stream.WriteUInt16BE(tempStream.Length);

            // Copy RDATA to main stream
            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   $"SOA({DomainName}, ",
                   $"Server: {Server}, ",
                   $"Email: {EMail}, ",
                   $"Serial: {Serial}, ",
                   $"Refresh: {Refresh}, ",
                   $"Retry: {Retry}, ",
                   $"Expire: {Expire}, ",
                   $"Minimum: {Minimum}, ",
                   base.ToString()
               );

        #endregion

    }

}
