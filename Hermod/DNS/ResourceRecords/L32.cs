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

using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The DNS 32-bit Locator (L32) resource record (RFC 6742 §2.2): an ILNPv4
    /// locator, "identical on-the-wire to the ADDRESS field of the existing DNS A
    /// record", with a preference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six octets: a 16-bit preference and four of locator (§2.2.1). Any other
    /// length is not an L32 and is kept as the octets that arrived.
    /// </para>
    /// <para>
    /// The presentation form is the A record's dotted quad, and the RFC's own
    /// examples got it wrong: §2.2.3 wrote "10.1.02.0", Erratum 3413 corrected it
    /// to "10.1.2.0", and BIND refuses the uncorrected form. A leading zero is
    /// refused here too, see <see cref="DNSTools.TryParseDottedQuad"/>.
    /// </para>
    /// </remarks>
    public class L32 : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS L32 resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.L32;

        /// <summary>
        /// The length of an L32's RDATA: a 16-bit preference and a 32-bit locator.
        /// </summary>
        public const Int32 RDataLength = 6;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this L32 among the L32 records of the same owner;
        /// "Lower Preference values are preferred" (RFC 6742 §2.2.1.1).
        /// </summary>
        public UInt16       Preference       { get; }

        /// <summary>
        /// The 32-bit locator (RFC 6742 §2.2.1.2).
        /// </summary>
        public IPv4Address  Locator32        { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it was not the six octets an L32
        /// is. Null for every readable record. While it is set,
        /// <see cref="Preference"/> and <see cref="Locator32"/> carry nothing.
        /// </summary>
        public Byte[]?      UnparsedRData    { get; }

        #endregion

        #region Constructor(s)

        #region L32(DomainName, Stream)

        /// <summary>
        /// Create a new L32 resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this L32 resource record.</param>
        /// <param name="Stream">A stream containing the L32 resource record data.</param>
        public L32(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"An L32 record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            if (rdata.Length == RDataLength)
            {
                this.Preference     = (UInt16) ((rdata[0] << 8) | rdata[1]);
                this.Locator32      = new IPv4Address(rdata[2..6]);
            }

            else
            {
                this.Locator32      = new IPv4Address(new Byte[4]);
                this.UnparsedRData  = rdata;
            }

        }

        #endregion

        #region L32(DomainName, Class, TimeToLive, Preference, Locator32)

        /// <summary>
        /// Create a new DNS L32 resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this L32 resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this L32 record.</param>
        /// <param name="Locator32">The 32-bit locator.</param>
        public L32(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Preference,
                   IPv4Address      Locator32)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.Locator32   = Locator32;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from its presentation form,
        /// "&lt;Preference&gt; &lt;Locator32&gt;" (RFC 6742 §2.2.2, §2.2.3).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        public static L32? TryParseFromJSON(DomainName  Name,
                                            TimeSpan    TimeToLive,
                                            String      Data)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return null;

            // §2.2.2: "a 16-bit unsigned decimal integer".
            if (!UInt16.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var preference))
                return null;

            if (!DNSTools.TryParseDottedQuad(parts[1], out var octets))
                return null;

            return new L32(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       preference,
                       new IPv4Address(octets)
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
        {

            if (UnparsedRData is not null)
                return UnknownRecord.GenericRData(UnparsedRData);

            var octets = Locator32.GetBytes();

            return $"{Preference} {octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";

        }

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: an L32 holds no names.</param>
        /// <param name="CompressionOffsets">Ignored: an L32 holds no names.</param>
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

            var locator = Locator32.GetBytes();

            Stream.WriteUInt16BE(RDataLength);
            Stream.WriteUInt16BE(Preference);
            Stream.Write(locator, 0, 4);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, Locator32: {Locator32}, {base.ToString()}";

        #endregion

    }

}
