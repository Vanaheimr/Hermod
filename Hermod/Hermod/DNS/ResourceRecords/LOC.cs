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
    /// Extensions methods for DNS LOC resource records.
    /// </summary>
    public static class DNS_LOC_Extensions
    {

        #region CacheLOC(this DNSClient, DomainName, Version, Size, HorizPrecision, VertPrecision, Latitude, Longitude, Altitude, Class = IN, TimeToLive = 365days)

        /// <summary>
        /// Add a DNS LOC record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">The domain name of this LOC resource record.</param>
        /// <param name="Version">The LOC version (must be 0).</param>
        /// <param name="Size">The diameter of a sphere enclosing the entity.</param>
        /// <param name="HorizPrecision">The horizontal precision of the data.</param>
        /// <param name="VertPrecision">The vertical precision of the data.</param>
        /// <param name="Latitude">The latitude of the center of the sphere.</param>
        /// <param name="Longitude">The longitude of the center of the sphere.</param>
        /// <param name="Altitude">The altitude of the center of the sphere.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheLOC(this DNSClient   DNSClient,
                                    DomainName       DomainName,
                                    Byte             Version,
                                    Byte             Size,
                                    Byte             HorizPrecision,
                                    Byte             VertPrecision,
                                    UInt32           Latitude,
                                    UInt32           Longitude,
                                    UInt32           Altitude,
                                    DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                    TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new LOC(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                Version,
                                Size,
                                HorizPrecision,
                                VertPrecision,
                                Latitude,
                                Longitude,
                                Altitude
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Location (LOC) resource record (RFC 1876).
    /// Stores geographic location information.
    /// </summary>
    public class LOC : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS LOC resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.LOC;

        #endregion

        #region Properties

        /// <summary>
        /// The LOC version (must be 0).
        /// </summary>
        public Byte    Version           { get; }

        /// <summary>
        /// The diameter of a sphere enclosing the entity.
        /// </summary>
        public Byte    Size              { get; }

        /// <summary>
        /// The horizontal precision of the data.
        /// </summary>
        public Byte    HorizPrecision    { get; }

        /// <summary>
        /// The vertical precision of the data.
        /// </summary>
        public Byte    VertPrecision     { get; }

        /// <summary>
        /// The latitude of the center of the sphere.
        /// </summary>
        public UInt32  Latitude          { get; }

        /// <summary>
        /// The longitude of the center of the sphere.
        /// </summary>
        public UInt32  Longitude         { get; }

        /// <summary>
        /// The altitude of the center of the sphere.
        /// </summary>
        public UInt32  Altitude          { get; }

        #endregion

        #region Constructor

        #region LOC(Stream)

        /// <summary>
        /// Create a new LOC resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the LOC resource record data.</param>
        public LOC(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Version         = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Size            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.HorizPrecision  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.VertPrecision   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Latitude        = Stream.ReadUInt32BE();
            this.Longitude       = Stream.ReadUInt32BE();
            this.Altitude        = Stream.ReadUInt32BE();

        }

        #endregion

        #region LOC(DomainName, Stream)

        /// <summary>
        /// Create a new LOC resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this LOC resource record.</param>
        /// <param name="Stream">A stream containing the LOC resource record data.</param>
        public LOC(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();

            this.Version         = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Size            = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.HorizPrecision  = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.VertPrecision   = (Byte) (Stream.ReadByte() & Byte.MaxValue);
            this.Latitude        = Stream.ReadUInt32BE();
            this.Longitude       = Stream.ReadUInt32BE();
            this.Altitude        = Stream.ReadUInt32BE();

        }

        #endregion

        #region LOC(DomainName, Class, TimeToLive, Version, Size, HorizPrecision, VertPrecision, Latitude, Longitude, Altitude)

        /// <summary>
        /// Create a new DNS LOC resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this LOC resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Version">The LOC version (must be 0).</param>
        /// <param name="Size">The diameter of a sphere enclosing the entity.</param>
        /// <param name="HorizPrecision">The horizontal precision of the data.</param>
        /// <param name="VertPrecision">The vertical precision of the data.</param>
        /// <param name="Latitude">The latitude of the center of the sphere.</param>
        /// <param name="Longitude">The longitude of the center of the sphere.</param>
        /// <param name="Altitude">The altitude of the center of the sphere.</param>
        public LOC(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   Byte             Version,
                   Byte             Size,
                   Byte             HorizPrecision,
                   Byte             VertPrecision,
                   UInt32           Latitude,
                   UInt32           Longitude,
                   UInt32           Altitude)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Version         = Version;
            this.Size            = Size;
            this.HorizPrecision  = HorizPrecision;
            this.VertPrecision   = VertPrecision;
            this.Latitude        = Latitude;
            this.Longitude       = Longitude;
            this.Altitude        = Altitude;

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

            // RDLENGTH (2 bytes): 16 (Version + Size + HorizPrecision + VertPrecision + Latitude + Longitude + Altitude)
            Stream.WriteUInt16BE(16);

            Stream.WriteByte    (Version);
            Stream.WriteByte    (Size);
            Stream.WriteByte    (HorizPrecision);
            Stream.WriteByte    (VertPrecision);
            Stream.WriteUInt32BE(Latitude);
            Stream.WriteUInt32BE(Longitude);
            Stream.WriteUInt32BE(Altitude);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Version={Version}, Latitude={Latitude}, Longitude={Longitude}, Altitude={Altitude}, {base.ToString()}";

        #endregion

    }

}
