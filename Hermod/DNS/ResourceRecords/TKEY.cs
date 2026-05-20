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
    /// Extensions methods for DNS TKEY resource records.
    /// </summary>
    public static class DNS_TKEY_Extensions
    {

        #region CacheTKEY(this DNSClient, DomainName, Algorithm, Inception, Expiration, Mode, Error, KeyData, OtherData, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS TKEY record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="Algorithm">The algorithm domain name.</param>
        /// <param name="Inception">The inception time.</param>
        /// <param name="Expiration">The expiration time.</param>
        /// <param name="Mode">The key agreement mode.</param>
        /// <param name="Error">The error code.</param>
        /// <param name="KeyData">The key data.</param>
        /// <param name="OtherData">The other data.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheTKEY(this DNSClient   DNSClient,
                                     DomainName       DomainName,
                                     DomainName       Algorithm,
                                     UInt32           Inception,
                                     UInt32           Expiration,
                                     UInt16           Mode,
                                     UInt16           Error,
                                     Byte[]           KeyData,
                                     Byte[]           OtherData,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new TKEY(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Algorithm,
                                Inception,
                                Expiration,
                                Mode,
                                Error,
                                KeyData,
                                OtherData
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Transaction Key (TKEY) resource record (RFC 2930).
    /// </summary>
    public class TKEY : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Transaction Key (TKEY) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.TKEY;

        #endregion

        #region Properties

        /// <summary>
        /// The algorithm domain name.
        /// </summary>
        public DomainName  Algorithm      { get; }

        /// <summary>
        /// The inception time.
        /// </summary>
        public UInt32      Inception      { get; }

        /// <summary>
        /// The expiration time.
        /// </summary>
        public UInt32      Expiration     { get; }

        /// <summary>
        /// The key agreement mode.
        /// </summary>
        public UInt16      Mode           { get; }

        /// <summary>
        /// The error code.
        /// </summary>
        public UInt16      Error          { get; }

        /// <summary>
        /// The key data.
        /// </summary>
        public Byte[]      KeyData        { get; }

        /// <summary>
        /// The other data.
        /// </summary>
        public Byte[]      OtherData      { get; }

        #endregion

        #region Constructor

        #region TKEY(Stream)

        /// <summary>
        /// Create a new TKEY resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the TKEY resource record data.</param>
        public TKEY(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Algorithm   = DNS.DomainName.Parse(
                                   DNSTools.ExtractName(Stream)
                               );

            this.Inception   = Stream.ReadUInt32BE();
            this.Expiration  = Stream.ReadUInt32BE();
            this.Mode        = Stream.ReadUInt16BE();
            this.Error       = Stream.ReadUInt16BE();

            var keySize      = Stream.ReadUInt16BE();
            this.KeyData     = DNSTools.ExtractByteArray(Stream, keySize);

            var otherSize    = Stream.ReadUInt16BE();
            this.OtherData   = DNSTools.ExtractByteArray(Stream, otherSize);

        }

        #endregion

        #region TKEY(DomainName, Stream)

        /// <summary>
        /// Create a new TKEY resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this TKEY resource record.</param>
        /// <param name="Stream">A stream containing the TKEY resource record data.</param>
        public TKEY(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Algorithm   = DNS.DomainName.Parse(
                                   DNSTools.ExtractName(Stream)
                               );

            this.Inception   = Stream.ReadUInt32BE();
            this.Expiration  = Stream.ReadUInt32BE();
            this.Mode        = Stream.ReadUInt16BE();
            this.Error       = Stream.ReadUInt16BE();

            var keySize      = Stream.ReadUInt16BE();
            this.KeyData     = DNSTools.ExtractByteArray(Stream, keySize);

            var otherSize    = Stream.ReadUInt16BE();
            this.OtherData   = DNSTools.ExtractByteArray(Stream, otherSize);

        }

        #endregion

        #region TKEY(DomainName, Class, TimeToLive, Algorithm, Inception, Expiration, Mode, Error, KeyData, OtherData)

        /// <summary>
        /// Create a new DNS TKEY resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this TKEY resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Algorithm">The algorithm domain name.</param>
        /// <param name="Inception">The inception time.</param>
        /// <param name="Expiration">The expiration time.</param>
        /// <param name="Mode">The key agreement mode.</param>
        /// <param name="Error">The error code.</param>
        /// <param name="KeyData">The key data.</param>
        /// <param name="OtherData">The other data.</param>
        public TKEY(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    DomainName       Algorithm,
                    UInt32           Inception,
                    UInt32           Expiration,
                    UInt16           Mode,
                    UInt16           Error,
                    Byte[]           KeyData,
                    Byte[]           OtherData)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Algorithm   = Algorithm;
            this.Inception   = Inception;
            this.Expiration  = Expiration;
            this.Mode        = Mode;
            this.Error       = Error;
            this.KeyData     = KeyData;
            this.OtherData   = OtherData;

        }

        #endregion

        #endregion


        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {
            var inceptionStr  = DateTimeOffset.FromUnixTimeSeconds(Inception).UtcDateTime.ToString("yyyyMMddHHmmss");
            var expirationStr = DateTimeOffset.FromUnixTimeSeconds(Expiration).UtcDateTime.ToString("yyyyMMddHHmmss");
            return $"{Algorithm} {inceptionStr} {expirationStr} {Mode} {Error} {Convert.ToBase64String(KeyData)}";
        }

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

            // Algorithm domain name (with compression)
            var algorithmOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;
            Algorithm.Serialize(
                tempStream,
                algorithmOffset,
                UseCompression,
                CompressionOffsets
            );

            tempStream.WriteUInt32BE(Inception);
            tempStream.WriteUInt32BE(Expiration);
            tempStream.WriteUInt16BE(Mode);
            tempStream.WriteUInt16BE(Error);

            // Key Size + Key Data
            tempStream.WriteUInt16BE((UInt16) KeyData.Length);
            tempStream.Write        (KeyData, 0, KeyData.Length);

            // Other Size + Other Data
            tempStream.WriteUInt16BE((UInt16) OtherData.Length);
            tempStream.Write        (OtherData, 0, OtherData.Length);


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

            => $"Algorithm: {Algorithm}, Mode: {Mode}, Error: {Error}, KeyData: {BitConverter.ToString(KeyData)}, {base.ToString()}";

        #endregion

    }

}
