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
    /// A DNS resource record whose type this build has no parser for, carried as
    /// opaque data (RFC 3597).
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 3597 §2 draws the line this class exists to hold: a type code with no
    /// parser behind it is <i>data</i>, not an error. The record has a well-known
    /// outer shape — owner name, TYPE, CLASS, TTL, RDLENGTH — and only the RDATA
    /// is unreadable, so a server can store it, an authoritative server can serve
    /// it, and a resolver can pass it on, none of them knowing what it means.
    /// </para>
    /// <para>
    /// The alternative is not "ignore it". A parser that gives up on the type
    /// still has to consume RDLENGTH octets to find the next record, and one that
    /// returns early instead leaves the stream inside the record it refused —
    /// after which every following record is read out of the middle of this one.
    /// One unknown type in the answer section therefore does not cost one record,
    /// it costs the rest of the message.
    /// </para>
    /// <para>
    /// The RDATA is never interpreted, and that includes not interpreting it as
    /// names: RFC 3597 §4 forbids compression pointers inside the RDATA of types
    /// that are not well-known precisely because a receiver in this position
    /// cannot expand them, and §6 requires the octets to be compared as they
    /// stand rather than case-insensitively as embedded names would be.
    /// </para>
    /// </remarks>
    public class UnknownRecord : ADNSResourceRecord
    {

        #region Properties

        /// <summary>
        /// The uninterpreted RDATA of this resource record.
        /// </summary>
        public Byte[] RData { get; }

        #endregion

        #region Constructor(s)

        #region UnknownRecord(DNSServiceName, Type, Stream)

        /// <summary>
        /// Read an unknown resource record from the given stream, positioned
        /// directly behind the owner name and TYPE.
        /// </summary>
        /// <param name="DNSServiceName">The owner name of this resource record.</param>
        /// <param name="Type">The resource record type code read from the wire.</param>
        /// <param name="Stream">A stream containing CLASS, TTL, RDLENGTH and RDATA.</param>
        public UnknownRecord(DNSServiceName          DNSServiceName,
                             DNSResourceRecordTypes  Type,
                             Stream                  Stream)

            : base(DNSServiceName,
                   Type,
                   Stream)

        {

            var rdLength = Stream.ReadUInt16BE();
            var rdata    = new Byte[rdLength];

            // ReadAtLeast rather than Read, and the count is checked: a single
            // Read may legitimately return less than was asked for, so a short
            // buffer and a short stream look alike unless the length is compared.
            // Saying which one it was is the whole benefit of RDLENGTH — the
            // record is unreadable either way, but a message that ends inside a
            // record is a different fault from a type with no parser, and only
            // one of the two is the sender's doing.
            var read     = Stream.ReadAtLeast(rdata, rdLength, throwOnEndOfStream: false);

            if (read != rdLength)
                throw new InvalidDataException($"A TYPE{(UInt16) Type} record claims {rdLength} RDATA octets but only {read} were left in the message!");

            this.RData   = rdata;

        }

        #endregion

        #region UnknownRecord(DNSServiceName, Type, Class, TimeToLive, RData)

        /// <summary>
        /// Create an unknown resource record with the given opaque RDATA.
        /// </summary>
        /// <param name="DNSServiceName">The owner name of this resource record.</param>
        /// <param name="Type">The resource record type code.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RData">The uninterpreted RDATA.</param>
        public UnknownRecord(DNSServiceName          DNSServiceName,
                             DNSResourceRecordTypes  Type,
                             DNSQueryClasses         Class,
                             TimeSpan                TimeToLive,
                             Byte[]                  RData)

            : base(DNSServiceName,
                   Type,
                   Class,
                   TimeToLive,
                   GenericRData(RData))

        {

            if (RData.Length > UInt16.MaxValue)
                throw new ArgumentException($"RDATA is limited to {UInt16.MaxValue} octets by the 16-bit RDLENGTH field, but {RData.Length} were given!", nameof(RData));

            this.RData = RData;

        }

        #endregion

        #region UnknownRecord(DomainName,     Type, Class, TimeToLive, RData)

        /// <summary>
        /// Create an unknown resource record with the given opaque RDATA.
        /// </summary>
        /// <param name="DomainName">The owner name of this resource record.</param>
        /// <param name="Type">The resource record type code.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RData">The uninterpreted RDATA.</param>
        public UnknownRecord(DomainName              DomainName,
                             DNSResourceRecordTypes  Type,
                             DNSQueryClasses         Class,
                             TimeSpan                TimeToLive,
                             Byte[]                  RData)

            : this(DNSServiceName.Parse(DomainName.FullName),
                   Type,
                   Class,
                   TimeToLive,
                   RData)

        { }

        #endregion

        #endregion


        #region (static) GenericRData(RData)

        /// <summary>
        /// The RFC 3597 §5 generic RDATA presentation format: the token <c>\#</c>,
        /// the length in octets, and the octets in hexadecimal.
        /// </summary>
        /// <param name="RData">The RDATA to render.</param>
        public static String GenericRData(Byte[] RData)

            // §5: "If the RDATA is of zero length, the text representation
            // contains only the \# token and the single zero representing the
            // length." — no trailing separator, which a naive Join would leave.
            => RData.Length == 0
                   ? @"\# 0"
                   : $@"\# {RData.Length} {Convert.ToHexString(RData).ToLowerInvariant()}";

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        protected override String ZoneFileRData()
            => GenericRData(RData);

        #endregion

        #region (protected override) SerializeRRData(Stream, UseCompression = true, CompressionOffsets = null)

        /// <summary>
        /// Serialize the opaque RDATA to the given stream.
        /// </summary>
        /// <param name="Stream">The stream to write to.</param>
        /// <param name="UseCompression">Ignored — RFC 3597 §4 forbids compression inside the RDATA of a type that is not well-known.</param>
        /// <param name="CompressionOffsets">Ignored, for the same reason.</param>
        protected override void SerializeRRData(Stream                      Stream,
                                                Boolean                     UseCompression       = true,
                                                Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            // Verbatim, both octets and length. RFC 3597 §3 asks for the record
            // to come out the far side unchanged, and there is nothing here to
            // change it into: whatever the RDATA happens to look like — a name, a
            // pointer, an address — this build does not know that it is one.
            Stream.WriteUInt16BE((UInt16) RData.Length);
            Stream.Write        (RData, 0, RData.Length);

        }

        #endregion


        #region (override) Equals(Object)

        /// <summary>
        /// Compare two resource records for equality.
        /// </summary>
        /// <param name="Object">Another object.</param>
        /// <remarks>
        /// RFC 3597 §6: the RDATA of an unknown type is compared as octets, case
        /// sensitively. The case-insensitive rule for embedded domain names
        /// cannot apply here, because nothing in this record says which octets
        /// were a name — and guessing would make two records that differ compare
        /// equal.
        /// </remarks>
        public override Boolean Equals(Object? Object)

            => Object is UnknownRecord other &&
               Type       == other.Type      &&
               Class      == other.Class     &&
               DomainName.Equals(other.DomainName) &&
               RData.SequenceEqual(other.RData);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {

            var hashCode = new HashCode();

            hashCode.Add(DomainName);
            hashCode.Add(Type);
            hashCode.Add(Class);
            hashCode.AddBytes(RData);

            return hashCode.ToHashCode();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"TYPE{(UInt16) Type} {GenericRData(RData)}, {base.ToString()}";

        #endregion

    }

}
