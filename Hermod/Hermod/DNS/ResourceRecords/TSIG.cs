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
    /// Extensions methods for DNS TSIG resource records.
    /// </summary>
    public static class DNS_TSIG_Extensions
    {

        #region CacheTSIG(this DNSClient, DomainName, AlgorithmName, TimeSigned, Fudge, MAC, OriginalID, Error, OtherData, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS TSIG record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="AlgorithmName">The algorithm name.</param>
        /// <param name="TimeSigned">The time signed (48-bit value stored as UInt64).</param>
        /// <param name="Fudge">The fudge (allowed time difference).</param>
        /// <param name="MAC">The message authentication code.</param>
        /// <param name="OriginalID">The original message ID.</param>
        /// <param name="Error">The error code.</param>
        /// <param name="OtherData">The other data.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheTSIG(this DNSClient   DNSClient,
                                     DomainName       DomainName,
                                     DomainName       AlgorithmName,
                                     UInt64           TimeSigned,
                                     UInt16           Fudge,
                                     Byte[]           MAC,
                                     UInt16           OriginalID,
                                     UInt16           Error,
                                     Byte[]           OtherData,
                                     DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                     TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new TSIG(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                AlgorithmName,
                                TimeSigned,
                                Fudge,
                                MAC,
                                OriginalID,
                                Error,
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
    /// The DNS Transaction Signature (TSIG) resource record (RFC 8945).
    /// </summary>
    public class TSIG : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Transaction Signature (TSIG) resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.TSIG;

        #endregion

        #region Properties

        /// <summary>
        /// The algorithm name.
        /// </summary>
        public DomainName  AlgorithmName    { get; }

        /// <summary>
        /// The time signed (48-bit value stored as UInt64).
        /// </summary>
        public UInt64      TimeSigned       { get; }

        /// <summary>
        /// The fudge (allowed time difference).
        /// </summary>
        public UInt16      Fudge            { get; }

        /// <summary>
        /// The message authentication code.
        /// </summary>
        public Byte[]      MAC              { get; }

        /// <summary>
        /// The original message ID.
        /// </summary>
        public UInt16      OriginalID       { get; }

        /// <summary>
        /// The error code.
        /// </summary>
        public UInt16      Error            { get; }

        /// <summary>
        /// The other data.
        /// </summary>
        public Byte[]      OtherData        { get; }

        #endregion

        #region Constructor

        #region TSIG(Stream)

        /// <summary>
        /// Create a new TSIG resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the TSIG resource record data.</param>
        public TSIG(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.AlgorithmName  = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

            // TimeSigned: 48-bit value (high 16 bits + low 32 bits)
            var timeHigh        = (UInt64) Stream.ReadUInt16BE();
            var timeLow         = (UInt64) Stream.ReadUInt32BE();
            this.TimeSigned     = (timeHigh << 32) | timeLow;

            this.Fudge          = Stream.ReadUInt16BE();

            var macSize         = Stream.ReadUInt16BE();
            this.MAC            = DNSTools.ExtractByteArray(Stream, macSize);

            this.OriginalID     = Stream.ReadUInt16BE();
            this.Error          = Stream.ReadUInt16BE();

            var otherLen        = Stream.ReadUInt16BE();
            this.OtherData      = DNSTools.ExtractByteArray(Stream, otherLen);

        }

        #endregion

        #region TSIG(DomainName, Stream)

        /// <summary>
        /// Create a new TSIG resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this TSIG resource record.</param>
        /// <param name="Stream">A stream containing the TSIG resource record data.</param>
        public TSIG(DomainName  DomainName,
                    Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.AlgorithmName  = DNS.DomainName.Parse(
                                      DNSTools.ExtractName(Stream)
                                  );

            // TimeSigned: 48-bit value (high 16 bits + low 32 bits)
            var timeHigh        = (UInt64) Stream.ReadUInt16BE();
            var timeLow         = (UInt64) Stream.ReadUInt32BE();
            this.TimeSigned     = (timeHigh << 32) | timeLow;

            this.Fudge          = Stream.ReadUInt16BE();

            var macSize         = Stream.ReadUInt16BE();
            this.MAC            = DNSTools.ExtractByteArray(Stream, macSize);

            this.OriginalID     = Stream.ReadUInt16BE();
            this.Error          = Stream.ReadUInt16BE();

            var otherLen        = Stream.ReadUInt16BE();
            this.OtherData      = DNSTools.ExtractByteArray(Stream, otherLen);

        }

        #endregion

        #region TSIG(DomainName, Class, TimeToLive, AlgorithmName, TimeSigned, Fudge, MAC, OriginalID, Error, OtherData)

        /// <summary>
        /// Create a new DNS TSIG resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this TSIG resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="AlgorithmName">The algorithm name.</param>
        /// <param name="TimeSigned">The time signed (48-bit value stored as UInt64).</param>
        /// <param name="Fudge">The fudge (allowed time difference).</param>
        /// <param name="MAC">The message authentication code.</param>
        /// <param name="OriginalID">The original message ID.</param>
        /// <param name="Error">The error code.</param>
        /// <param name="OtherData">The other data.</param>
        public TSIG(DomainName       DomainName,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    DomainName       AlgorithmName,
                    UInt64           TimeSigned,
                    UInt16           Fudge,
                    Byte[]           MAC,
                    UInt16           OriginalID,
                    UInt16           Error,
                    Byte[]           OtherData)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.AlgorithmName  = AlgorithmName;
            this.TimeSigned     = TimeSigned;
            this.Fudge          = Fudge;
            this.MAC            = MAC;
            this.OriginalID     = OriginalID;
            this.Error          = Error;
            this.OtherData      = OtherData;

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

            // Algorithm Name domain name (with compression)
            var algorithmOffset = (Int32) Stream.Position + 2 + (Int32) tempStream.Position;
            AlgorithmName.Serialize(
                tempStream,
                algorithmOffset,
                UseCompression,
                CompressionOffsets
            );

            // TimeSigned: 48-bit value (high 16 bits + low 32 bits)
            tempStream.WriteUInt16BE((UInt16) (TimeSigned >> 32));
            tempStream.WriteUInt32BE((UInt32) (TimeSigned & 0xFFFFFFFF));

            tempStream.WriteUInt16BE(Fudge);

            // MAC Size + MAC
            tempStream.WriteUInt16BE((UInt16) MAC.Length);
            tempStream.Write        (MAC, 0, MAC.Length);

            tempStream.WriteUInt16BE(OriginalID);
            tempStream.WriteUInt16BE(Error);

            // Other Len + Other Data
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

            => $"AlgorithmName: {AlgorithmName}, TimeSigned: {TimeSigned}, Fudge: {Fudge}, OriginalID: {OriginalID}, Error: {Error}, MAC: {BitConverter.ToString(MAC)}, {base.ToString()}";

        #endregion

    }

}
