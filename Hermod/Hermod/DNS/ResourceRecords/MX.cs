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
    /// Extensions methods for DNS MX resource records.
    /// </summary>
    public static class DNS_MX_Extensions
    {

        #region CacheMXRecord(this DNSClient, DomainName, Preference, Exchange, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS MX record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Preference">The preference of this mail exchange.</param>
        /// <param name="Exchange">The domain name of the mail exchange server.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheMXRecord(this DNSClient   DNSClient,
                                         DomainName       DomainName,
                                         UInt16           Preference,
                                         DomainName       Exchange,
                                         DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                         TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new MX(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Preference,
                                Exchange
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Mail Exchange (MX) resource record.
    /// </summary>
    public class MX : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Mail Exchange (MX) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordType TypeId = DNSResourceRecordType.MX;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this mail exchange.
        /// </summary>
        public UInt16      Preference    { get; }

        /// <summary>
        /// The domain name of the mail exchange server.
        /// </summary>
        public DomainName  Exchange      { get; }

        #endregion

        #region Constructor

        #region MX(Stream)

        /// <summary>
        /// Create a new MX resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the MX resource record data.</param>
        public MX(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Preference  = (UInt16) ((Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue));

            this.Exchange    = DNSTools.ExtractDomainName(Stream);

        }

        #endregion

        #region MX(DomainName, Stream)

        /// <summary>
        /// Create a new MX resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this MX resource record.</param>
        /// <param name="Stream">A stream containing the MX resource record data.</param>
        public MX(DomainName  DomainName,
                  Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.Preference  = (UInt16) ((Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue));

            this.Exchange    = DNSTools.ExtractDomainName(Stream);

        }

        #endregion

        #region MX(DomainName, Class, TimeToLive, Preference, Exchange)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this mail exchange.</param>
        /// <param name="Exchange">The domain name of the mail exchange server.</param>
        public MX(DomainName       DomainName,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  UInt16           Preference,
                  DomainName       Exchange)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.Exchange    = Exchange;

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

            tempStream.WriteUInt16BE(Preference);

            // Mail Exchange domain name (variable, with compression)
            var exchangeOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;  // +2 for RDLength
            Exchange.Serialize(
                tempStream,
                exchangeOffset,
                UseCompression,
                CompressionOffsets
            );

            if (tempStream.Length > UInt16.MaxValue)
                throw new InvalidOperationException("RDATA exceeds maximum UInt16 length (65535 bytes)!");

            // RDLENGTH (2 bytes)
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

            => $"Preference: {Preference}, Exchange: {Exchange}, {base.ToString()}";

        #endregion

    }

}
