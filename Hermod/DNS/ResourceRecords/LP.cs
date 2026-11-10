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
    /// The DNS Locator Pointer (LP) resource record (RFC 6742 §2.4): the name of a
    /// subnetwork whose L32 and L64 records hold the locators, "effectively, a
    /// Locator Pointer to L32 and/or L64 records".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same shape as KX and MX, a preference and a name, and two rules of its
    /// own. §2.4.1.2: "A sender MUST NOT use DNS name compression on the FQDN
    /// field", which RFC 3597 §4 already says of every type after RFC 1035. And
    /// the FQDN "MUST NOT have the same value as the owner name of the LP RR
    /// instance" — a pointer to itself, which a resolver following it would
    /// follow for ever.
    /// </para>
    /// <para>
    /// The second rule is enforced where a record is written down, in the
    /// presentation reader, and not where one arrives: on the wire RFC 3597 §2
    /// wants every record kept, and BIND accepts such a line without a word. And
    /// unlike KX's, the FQDN keeps its case in the DNSSEC canonical form: LP came
    /// after RFC 3597, whose §7 closed the list of types whose names are
    /// lowercased there.
    /// </para>
    /// </remarks>
    public class LP : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS LP resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.LP;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this LP record among the LP records of the same
        /// owner; "Lower Preference values are preferred" (RFC 6742 §2.4.1.1).
        /// </summary>
        public UInt16      Preference       { get; }

        /// <summary>
        /// The name whose L32 and L64 records hold the locators.
        /// </summary>
        public DomainName  FQDN             { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it could not be read as an LP —
        /// shorter than a preference and a name, a name that does not end where
        /// the RDATA does, or a compressed one. Null for every readable record.
        /// While it is set, <see cref="Preference"/> and <see cref="FQDN"/> carry
        /// nothing.
        /// </summary>
        public Byte[]?     UnparsedRData    { get; }

        #endregion

        #region Constructor(s)

        #region LP(DomainName, Stream)

        /// <summary>
        /// Create a new LP resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this LP resource record.</param>
        /// <param name="Stream">A stream containing the LP resource record data.</param>
        public LP(DomainName  DomainName,
                  Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"An LP record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            var offset    = 2;

            if (rdata.Length >= 3 &&
                DNSTools.TryReadUncompressedName(rdata, ref offset, out var fqdn) &&
                offset == rdata.Length)
            {
                this.Preference     = (UInt16) ((rdata[0] << 8) | rdata[1]);
                this.FQDN           = fqdn;
            }

            else
            {
                this.FQDN           = DNS.DomainName.Parse(".");
                this.UnparsedRData  = rdata;
            }

        }

        #endregion

        #region LP(DomainName, Class, TimeToLive, Preference, FQDN)

        /// <summary>
        /// Create a new DNS LP resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this LP resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this LP record.</param>
        /// <param name="FQDN">The name whose L32 and L64 records hold the locators.</param>
        public LP(DomainName       DomainName,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  UInt16           Preference,
                  DomainName       FQDN)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.FQDN        = FQDN;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data, Origin = null)

        /// <summary>
        /// Try to parse this resource record from its presentation form,
        /// "&lt;Preference&gt; &lt;FQDN&gt;" (RFC 6742 §2.4.2, §2.4.3).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        /// <param name="Origin">An optional origin for a relative FQDN.</param>
        public static LP? TryParseFromJSON(DomainName   Name,
                                           TimeSpan     TimeToLive,
                                           String       Data,
                                           DomainName?  Origin   = null)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return null;

            // §2.4.2: "a 16-bit unsigned decimal integer".
            if (!UInt16.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var preference))
                return null;

            DomainName fqdn;

            try
            {
                fqdn = DNS.DomainName.ParseLenient(parts[1], Origin);
            }
            catch
            {
                return null;
            }

            // §2.4.1.2: "This field MUST NOT have the same value as the owner name
            // of the LP RR instance." The same value in the sense of RFC 4343,
            // which makes two names that differ only in case one name.
            if (String.Equals(fqdn.FullName.TrimEnd('.'),
                              Name.FullName.TrimEnd('.'),
                              StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new LP(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       preference,
                       fqdn
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => UnparsedRData is not null
                   ? UnknownRecord.GenericRData(UnparsedRData)
                   : $"{Preference} {FQDN.FullName}";

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: RFC 6742 §2.4.1.2 forbids compressing the FQDN.</param>
        /// <param name="CompressionOffsets">Ignored, for the same reason.</param>
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

            var tempStream = new MemoryStream();

            tempStream.WriteUInt16BE(Preference);

            // §2.4.1.2: "A sender MUST NOT use DNS name compression on the FQDN
            // field when transmitting an LP RR."
            FQDN.Serialize(
                tempStream,
                0,
                false,
                null
            );

            // Two octets and a name of at most 255: RDLENGTH cannot overflow here.
            Stream.WriteUInt16BE(tempStream.Length);

            tempStream.Position = 0;
            tempStream.CopyTo(Stream);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, FQDN: {FQDN}, {base.ToString()}";

        #endregion

    }

}
