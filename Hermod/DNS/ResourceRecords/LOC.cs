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

        #region RFC 1876 §2 — what the octets mean

        /// <summary>
        /// The diameter of the enclosing sphere in centimetres, or null when the
        /// octet is one RFC 1876 §2 leaves undefined.
        /// </summary>
        public UInt64?  SizeInCentimetres            => DecodeScaled(Size);

        /// <summary>The horizontal precision in centimetres, or null when undefined.</summary>
        public UInt64?  HorizPrecisionInCentimetres  => DecodeScaled(HorizPrecision);

        /// <summary>The vertical precision in centimetres, or null when undefined.</summary>
        public UInt64?  VertPrecisionInCentimetres   => DecodeScaled(VertPrecision);


        /// <summary>
        /// The latitude in thousandths of a second of arc, north of the equator
        /// being positive.
        /// </summary>
        /// <remarks>
        /// §2 stores it unsigned with 2^31 as the equator, which keeps the wire
        /// form free of a sign convention and makes every reader subtract the
        /// same constant. Getting the direction of that subtraction wrong puts a
        /// location on the opposite hemisphere, which is the kind of error that
        /// looks plausible.
        /// </remarks>
        public Int64    LatitudeInMilliArcSeconds    => (Int64) Latitude  - (1L << 31);

        /// <summary>The longitude in thousandths of a second of arc, east of the prime meridian being positive.</summary>
        public Int64    LongitudeInMilliArcSeconds   => (Int64) Longitude - (1L << 31);

        /// <summary>
        /// The altitude in centimetres above the WGS 84 reference spheroid.
        /// </summary>
        /// <remarks>
        /// §2 measures it "from a base of 100,000m below" the spheroid, so the
        /// stored value is the real altitude plus 10,000,000 cm. That offset is
        /// what lets the field be unsigned and reach from −100000.00 m to
        /// 42849672.95 m — which is the whole 32-bit range, so no value of the
        /// field is out of bounds and there is nothing here to range-check.
        /// </remarks>
        public Int64    AltitudeInCentimetres        => (Int64) Altitude  - AltitudeReference;

        /// <summary>The 100,000 m the altitude field is measured up from, in centimetres.</summary>
        public const Int64 AltitudeReference = 10_000_000;


        /// <summary>
        /// Whether every field of this record means what RFC 1876 §2 says it
        /// means.
        /// </summary>
        /// <remarks>
        /// False for a version this build does not know — §2: "Implementations
        /// are required to check this field and make no assumptions about the
        /// format of unrecognized versions" — and for the size or precision
        /// octets §2 leaves undefined. In either case the record cannot honestly
        /// be written in the LOC presentation format, because that format is a
        /// statement about what the octets mean.
        /// </remarks>
        public Boolean  IsWellDefined

            => Version == 0                             &&
               SizeInCentimetres.           HasValue    &&
               HorizPrecisionInCentimetres. HasValue    &&
               VertPrecisionInCentimetres.  HasValue;

        #endregion

        #region Defaults (RFC 1876 §3)

        /// <summary>The size RFC 1876 §3 assumes when the master file omits it: 1 m.</summary>
        public const Byte DefaultSize           = 0x12;

        /// <summary>The horizontal precision assumed when omitted: 10000 m.</summary>
        public const Byte DefaultHorizPrecision = 0x16;

        /// <summary>The vertical precision assumed when omitted: 10 m.</summary>
        public const Byte DefaultVertPrecision  = 0x13;

        /// <summary>
        /// The largest length RFC 1876 §2's scaled octet can express: 9e9 cm,
        /// which is 90,000 km — seven times the equatorial diameter of the earth.
        /// </summary>
        public const UInt64 MaxExpressibleCentimetres = 9_000_000_000;

        #endregion

        #region (private static) ReadScaled(Parts, ref Index, Default)

        /// <summary>
        /// Read one optional "&lt;metres&gt;m" field of the presentation format,
        /// falling back to its RFC 1876 §3 default only when it is absent.
        /// </summary>
        private static Byte ReadScaled(String[]  Parts,
                                       ref Int32 Index,
                                       Byte      Default)
        {

            if (Index >= Parts.Length)
                return Default;

            var text = Parts[Index].TrimEnd('m', 'M');

            if (!Double.TryParse(text,
                                 System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out var metres) ||
                metres < 0)
            {
                return Default;
            }

            Index++;

            return EncodeScaled((UInt64) Math.Round(metres * 100));

        }

        #endregion

        #region (static) DecodeScaled(Encoded) / EncodeScaled(Centimetres)

        /// <summary>
        /// Decode one of RFC 1876 §2's scaled octets into centimetres.
        /// </summary>
        /// <param name="Encoded">The octet: base in the high nibble, power of ten in the low one.</param>
        /// <returns>The value in centimetres, or null when §2 leaves this octet undefined.</returns>
        /// <remarks>
        /// §2: "expressed as a pair of four-bit unsigned integers, each ranging
        /// from zero to nine ... Four-bit values greater than 9 are undefined, as
        /// are values with a base of zero and a non-zero exponent."
        /// <para>
        /// Both exclusions matter and neither is decorative. Decoding 0xFF as
        /// 15 × 10¹⁵ cm yields a sphere wider than the solar system, and decoding
        /// 0x05 as zero quietly agrees with a sender who meant something else —
        /// the RFC declines to say what, which is exactly why a reader must not
        /// guess.
        /// </para>
        /// </remarks>
        public static UInt64? DecodeScaled(Byte Encoded)
        {

            var mantissa = (Encoded >> 4) & 0x0F;
            var exponent =  Encoded       & 0x0F;

            if (mantissa > 9 || exponent > 9)
                return null;

            if (mantissa == 0 && exponent != 0)
                return null;

            UInt64 value = (UInt64) mantissa;

            for (var i = 0; i < exponent; i++)
                value *= 10;

            return value;

        }


        /// <summary>
        /// Encode a length in centimetres into RFC 1876 §2's scaled octet.
        /// </summary>
        /// <param name="Centimetres">The length to encode.</param>
        /// <remarks>
        /// The format holds one significant digit, so most values are rounded
        /// rather than represented: 25 m becomes 20 m, and 9e9 cm (90,000 km) is
        /// the largest thing it can say at all.
        /// </remarks>
        public static Byte EncodeScaled(UInt64 Centimetres)
        {

            if (Centimetres == 0)
                return 0x00;

            // Clamp first, and not only for tidiness: the rounding step below
            // adds 5 before dividing, which wraps for a value near the top of a
            // UInt64 — and a wrapped value produces a small octet rather than a
            // large one, so an absurdly big size would come back as an absurdly
            // small one. 9e9 cm is everything this format can say.
            if (Centimetres >= MaxExpressibleCentimetres)
                return 0x99;

            var exponent = 0;
            var value    = Centimetres;

            while (value > 9)
            {
                // Round rather than truncate, so 25 → 3e1 rather than 2e1: one
                // significant digit is lossy either way, and the nearer answer is
                // the better one.
                value = (value + 5) / 10;
                exponent++;
            }

            return (Byte) ((value << 4) | (UInt64) exponent);

        }

        #endregion

        #region Constructor

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
                    {
                        altitude = (UInt32) ((Int64) Math.Round(altM * 100) + AltitudeReference);
                        idx++;
                    }
                }

                // RFC 1876 §3: "size defaults to 1m, horizontal precision
                // defaults to 10000m, and vertical precision defaults to 10m" —
                // *if omitted*. They were being defaulted whether or not they
                // were there, so a zone file saying "30m 40m 50m" loaded as
                // "1m 10000m 10m" and nothing said a word. Defaults are for
                // absent values; substituting them for present ones is a way of
                // discarding data that looks like a specification.
                var size            = ReadScaled(parts, ref idx, DefaultSize);
                var horizPrecision  = ReadScaled(parts, ref idx, DefaultHorizPrecision);
                var vertPrecision   = ReadScaled(parts, ref idx, DefaultVertPrecision);

                return new LOC(Name, DNSQueryClasses.IN, TimeToLive,
                               0,     // Version
                               size,
                               horizPrecision,
                               vertPrecision,
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

            // RFC 1876 §2 requires the version to be checked, and RFC 3597 §5
            // says what to do once it has been: the generic \# form exists partly
            // for "an RR type where the text format varies depending on a
            // version ... e.g., a LOC RR with a VERSION other than 0".
            //
            // The same applies to a size or precision octet §2 leaves undefined.
            // Writing either in the LOC presentation format would be a claim
            // about what the octets mean, and the specification declines to make
            // one — so the honest output is the octets themselves.
            if (!IsWellDefined)
                return UnknownRecord.GenericRData(RDataBytes());

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

        #region (private) RDataBytes()

        /// <summary>The sixteen RDATA octets of this record, in wire order.</summary>
        private Byte[] RDataBytes()
        {

            var rdata = new Byte[16];

            rdata[0] = Version;
            rdata[1] = Size;
            rdata[2] = HorizPrecision;
            rdata[3] = VertPrecision;

            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan( 4, 4), Latitude);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan( 8, 4), Longitude);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(rdata.AsSpan(12, 4), Altitude);

            return rdata;

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
