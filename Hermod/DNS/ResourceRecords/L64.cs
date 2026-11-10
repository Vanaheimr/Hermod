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

using System.Buffers.Binary;
using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The DNS 64-bit Locator (L64) resource record (RFC 6742 §2.3): an ILNPv6
    /// locator, "the same syntax and semantics as a 64-bit IPv6 routing prefix",
    /// with a preference.
    /// </summary>
    /// <remarks>
    /// NID's twin on the wire and on the page — ten octets, and four groups that
    /// must not be written with "::" (§2.3.2) — and kept as the octets that arrived
    /// when it is any other length.
    /// </remarks>
    public class L64 : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS L64 resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.L64;

        /// <summary>
        /// The length of an L64's RDATA: a 16-bit preference and a 64-bit locator.
        /// </summary>
        public const Int32 RDataLength = 10;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this L64 among the L64 records of the same owner;
        /// "Lower Preference values are preferred" (RFC 6742 §2.3.1.1).
        /// </summary>
        public UInt16   Preference       { get; }

        /// <summary>
        /// The 64-bit locator (RFC 6742 §2.3.1.2).
        /// </summary>
        public UInt64   Locator64        { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it was not the ten octets an L64
        /// is. Null for every readable record. While it is set,
        /// <see cref="Preference"/> and <see cref="Locator64"/> carry nothing.
        /// </summary>
        public Byte[]?  UnparsedRData    { get; }

        #endregion

        #region Constructor(s)

        #region L64(DomainName, Stream)

        /// <summary>
        /// Create a new L64 resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this L64 resource record.</param>
        /// <param name="Stream">A stream containing the L64 resource record data.</param>
        public L64(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"An L64 record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            if (rdata.Length == RDataLength)
            {
                this.Preference     = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
                this.Locator64      = BinaryPrimitives.ReadUInt64BigEndian(rdata.AsSpan(2, 8));
            }

            else
                this.UnparsedRData  = rdata;

        }

        #endregion

        #region L64(DomainName, Class, TimeToLive, Preference, Locator64)

        /// <summary>
        /// Create a new DNS L64 resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this L64 resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this L64 record.</param>
        /// <param name="Locator64">The 64-bit locator.</param>
        public L64(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Preference,
                   UInt64           Locator64)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.Locator64   = Locator64;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from its presentation form,
        /// "&lt;Preference&gt; &lt;Locator64&gt;" (RFC 6742 §2.3.2, §2.3.3).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        public static L64? TryParseFromJSON(DomainName  Name,
                                            TimeSpan    TimeToLive,
                                            String      Data)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return null;

            // §2.3.2: "a 16-bit unsigned decimal integer".
            if (!UInt16.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var preference))
                return null;

            if (!DNSTools.TryParseLocator64(parts[1], out var locator64))
                return null;

            return new L64(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       preference,
                       locator64
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => UnparsedRData is not null
                   ? UnknownRecord.GenericRData(UnparsedRData)
                   : $"{Preference} {DNSTools.ToLocator64Text(Locator64)}";

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: an L64 holds no names.</param>
        /// <param name="CompressionOffsets">Ignored: an L64 holds no names.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            if (UnparsedRData is not null)
            {
                Stream.WriteUInt16BE(UnparsedRData.Length);
                Stream.Write(UnparsedRData, 0, UnparsedRData.Length);
                return;
            }

            var rdata = new Byte[RDataLength];

            BinaryPrimitives.WriteUInt16BigEndian(rdata.AsSpan(0, 2), Preference);
            BinaryPrimitives.WriteUInt64BigEndian(rdata.AsSpan(2, 8), Locator64);

            Stream.WriteUInt16BE(rdata.Length);
            Stream.Write(rdata, 0, rdata.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, Locator64: {DNSTools.ToLocator64Text(Locator64)}, {base.ToString()}";

        #endregion

    }

}
