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


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from a DNS JSON API "data" field
        /// (e.g. Google dns.google/resolve or Cloudflare cloudflare-dns.com/dns-query).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The "data" field value from the JSON response.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static LOC? TryParseFromJSON(DomainName Name, TimeSpan TimeToLive, String Data)
        {
            try
            {

                // LOC presentation format:  "52 22 23.000 N 4 53 32.000 E -2.00m 0.00m 10000.00m 10.00m"
                // Parsing the full presentation format is complex; create a minimal record
                // preserving the version=0 and default precision values.
                var parts = Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return null;

                // Try parsing latitude (d m s.fff N/S)
                UInt32 latitude  = 1u << 31;  // equator default
                UInt32 longitude = 1u << 31;  // prime meridian default
                UInt32 altitude  = 10_000_000; // sea level default (100km base offset in cm)

                var idx = 0;

                // Latitude
                if (idx < parts.Length && Int32.TryParse(parts[idx], out var latDeg))
                {
                    idx++;
                    var latMin = 0; var latSec = 0.0;
                    if (idx < parts.Length && Int32.TryParse(parts[idx], out latMin)) idx++;
                    if (idx < parts.Length && Double.TryParse(parts[idx], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out latSec)) idx++;
                    var latMs = (Int64) latDeg * 3_600_000 + (Int64) latMin * 60_000 + (Int64) (latSec * 1000);
                    if (idx < parts.Length)
                    {
                        if (parts[idx] == "S" || parts[idx] == "s") latMs = -latMs;
                        if (parts[idx] == "N" || parts[idx] == "n" || parts[idx] == "S" || parts[idx] == "s") idx++;
                    }
                    latitude = (UInt32) (latMs + (1L << 31));
                }

                // Longitude
                if (idx < parts.Length && Int32.TryParse(parts[idx], out var lonDeg))
                {
                    idx++;
                    var lonMin = 0; var lonSec = 0.0;
                    if (idx < parts.Length && Int32.TryParse(parts[idx], out lonMin)) idx++;
                    if (idx < parts.Length && Double.TryParse(parts[idx], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lonSec)) idx++;
                    var lonMs = (Int64) lonDeg * 3_600_000 + (Int64) lonMin * 60_000 + (Int64) (lonSec * 1000);
                    if (idx < parts.Length)
                    {
                        if (parts[idx] == "W" || parts[idx] == "w") lonMs = -lonMs;
                        if (parts[idx] == "E" || parts[idx] == "e" || parts[idx] == "W" || parts[idx] == "w") idx++;
                    }
                    longitude = (UInt32) (lonMs + (1L << 31));
                }

                // Altitude (e.g. "10.00m")
                if (idx < parts.Length)
                {
                    var altStr = parts[idx].TrimEnd('m', 'M');
                    if (Double.TryParse(altStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var altM))
                        altitude = (UInt32) ((Int64)(altM * 100) + 10_000_000);
                }

                return new LOC(Name, DNSQueryClasses.IN, TimeToLive,
                               0,     // Version
                               0x12,  // Size (1e2 cm = 1m)
                               0x16,  // HorizPrecision (1e6 cm = 10km)
                               0x13,  // VertPrecision (1e3 cm = 10m)
                               latitude,
                               longitude,
                               altitude);

            }
            catch { return null; }
        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {

            // Latitude:  stored as unsigned 32-bit, 2^31 = equator
            var latMilliseconds  = (Int64) Latitude  - (1L << 31);
            var latNorth         = latMilliseconds >= 0;
            if (!latNorth)   latMilliseconds = -latMilliseconds;
            var latDeg           = (Int32) (latMilliseconds / 3_600_000);
            var latMin           = (Int32) ((latMilliseconds % 3_600_000) / 60_000);
            var latSecWhole      = (Int32) ((latMilliseconds % 60_000) / 1000);
            var latSecFrac       = (Int32) (latMilliseconds % 1000);

            // Longitude: stored as unsigned 32-bit, 2^31 = prime meridian
            var lonMilliseconds  = (Int64) Longitude - (1L << 31);
            var lonEast          = lonMilliseconds >= 0;
            if (!lonEast)    lonMilliseconds = -lonMilliseconds;
            var lonDeg           = (Int32) (lonMilliseconds / 3_600_000);
            var lonMin           = (Int32) ((lonMilliseconds % 3_600_000) / 60_000);
            var lonSecWhole      = (Int32) ((lonMilliseconds % 60_000) / 1000);
            var lonSecFrac       = (Int32) (lonMilliseconds % 1000);

            // Altitude: stored as unsigned 32-bit centimeters from -100000.00m reference
            var altCm            = (Int64) Altitude - 10_000_000;
            var altM             = altCm / 100.0;

            // Size, horizontal precision, vertical precision: encoded as Mantissa*10^Exponent (centimeters)
            static String DecodePrecision(Byte encoded)
            {
                var mantissa = (encoded >> 4) & 0x0F;
                var exponent = encoded & 0x0F;
                var cm       = mantissa * Math.Pow(10, exponent);
                return (cm / 100.0).ToString("0.##") + "m";
            }

            var latStr  = $"{latDeg} {latMin} {latSecWhole}.{latSecFrac:D3} {(latNorth ? "N" : "S")}";
            var lonStr  = $"{lonDeg} {lonMin} {lonSecWhole}.{lonSecFrac:D3} {(lonEast  ? "E" : "W")}";
            var altStr  = $"{altM:0.##}m";

            return $"{latStr} {lonStr} {altStr} {DecodePrecision(Size)} {DecodePrecision(HorizPrecision)} {DecodePrecision(VertPrecision)}";

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
