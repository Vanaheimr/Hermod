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
    /// The DNS Node Identifier (NID) resource record (RFC 6742 §2.1): the 64-bit
    /// node identifier of an ILNP-capable host, with a preference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ten octets on the wire, a 16-bit preference and the NodeID, and nothing
    /// else (§2.1.1). A record of any other length is not a NID — BIND answers
    /// FORMERR for nine octets and for eleven — and is kept as the octets that
    /// arrived rather than completed or cut to fit.
    /// </para>
    /// <para>
    /// The NodeID is written like four groups of an IPv6 address and must not be
    /// mistaken for one: §2.1.2 forbids the "::" form "to avoid confusion with
    /// 128-bit IPv6 addresses, because the NID is a 64-bit field". See
    /// <see cref="DNSTools.TryParseLocator64"/>.
    /// </para>
    /// </remarks>
    public class NID : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS NID resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.NID;

        /// <summary>
        /// The length of a NID's RDATA: a 16-bit preference and a 64-bit NodeID.
        /// </summary>
        public const Int32 RDataLength = 10;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this NID among the NID records of the same owner;
        /// "Lower Preference values are preferred" (RFC 6742 §2.1.1.1).
        /// </summary>
        public UInt16   Preference       { get; }

        /// <summary>
        /// The 64-bit node identifier (RFC 6742 §2.1.1.2).
        /// </summary>
        public UInt64   NodeID           { get; }

        /// <summary>
        /// The RDATA exactly as it arrived, when it was not the ten octets a NID
        /// is. Null for every readable record. While it is set,
        /// <see cref="Preference"/> and <see cref="NodeID"/> carry nothing.
        /// </summary>
        public Byte[]?  UnparsedRData    { get; }

        #endregion

        #region Constructor(s)

        #region NID(DomainName, Stream)

        /// <summary>
        /// Create a new NID resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this NID resource record.</param>
        /// <param name="Stream">A stream containing the NID resource record data.</param>
        public NID(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);

            if (rdata.Length != rdLength)
                throw new InvalidDataException($"A NID record claims {rdLength} RDATA octets but only {rdata.Length} were left in the message!");

            if (rdata.Length == RDataLength)
            {
                this.Preference     = BinaryPrimitives.ReadUInt16BigEndian(rdata.AsSpan(0, 2));
                this.NodeID         = BinaryPrimitives.ReadUInt64BigEndian(rdata.AsSpan(2, 8));
            }

            else
                this.UnparsedRData  = rdata;

        }

        #endregion

        #region NID(DomainName, Class, TimeToLive, Preference, NodeID)

        /// <summary>
        /// Create a new DNS NID resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this NID resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this NID record.</param>
        /// <param name="NodeID">The 64-bit node identifier.</param>
        public NID(DomainName       DomainName,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Preference,
                   UInt64           NodeID)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.NodeID      = NodeID;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from its presentation form,
        /// "&lt;Preference&gt; &lt;NodeID&gt;" (RFC 6742 §2.1.2, §2.1.3).
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        public static NID? TryParseFromJSON(DomainName  Name,
                                            TimeSpan    TimeToLive,
                                            String      Data)
        {

            var parts = Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return null;

            // §2.1.2: "a 16-bit unsigned decimal integer".
            if (!UInt16.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var preference))
                return null;

            if (!DNSTools.TryParseLocator64(parts[1], out var nodeID))
                return null;

            return new NID(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       preference,
                       nodeID
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()

            => UnparsedRData is not null
                   ? UnknownRecord.GenericRData(UnparsedRData)
                   : $"{Preference} {DNSTools.ToLocator64Text(NodeID)}";

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the concrete DNS resource record to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored: a NID holds no names.</param>
        /// <param name="CompressionOffsets">Ignored: a NID holds no names.</param>
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
            BinaryPrimitives.WriteUInt64BigEndian(rdata.AsSpan(2, 8), NodeID);

            Stream.WriteUInt16BE(rdata.Length);
            Stream.Write(rdata, 0, rdata.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, NodeID: {DNSTools.ToLocator64Text(NodeID)}, {base.ToString()}";

        #endregion

    }

}
