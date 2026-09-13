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
    /// Comparisons of resource records as Multicast DNS needs them (RFC 6762 §6, §8.2.1, §10.2):
    /// by resource record set (name, type, class), by identity (plus RDATA) and lexicographically.
    /// The RDATA is taken from the uncompressed wire format, so that every record type
    /// compares the same way without a type-specific equality.
    /// </summary>
    public static class MulticastDNSRecordExtensions
    {

        #region ToWireFormat(this Record)

        /// <summary>
        /// Serialize the record without name compression.
        /// </summary>
        /// <param name="Record">A resource record.</param>
        public static Byte[] ToWireFormat(this IDNSResourceRecord Record)
        {

            ArgumentNullException.ThrowIfNull(Record);

            using var stream = new MemoryStream();
            Record.Serialize(stream, UseCompression: false, CompressionOffsets: []);
            return stream.ToArray();

        }

        #endregion

        #region RData(this Record)

        /// <summary>
        /// The RDATA of the record in uncompressed wire format.
        /// </summary>
        /// <param name="Record">A resource record.</param>
        public static ReadOnlyMemory<Byte> RData(this IDNSResourceRecord Record)
        {

            var wire    = Record.ToWireFormat();
            var offset  = 0;

            if (!MulticastDNSWireFormat.TrySkipName(wire, ref offset, out var error))
                throw new InvalidOperationException($"The owner name of '{Record}' could not be walked: {error}");

            // TYPE (2) + CLASS (2) + TTL (4) + RDLENGTH (2)
            offset += 10;

            return offset <= wire.Length
                       ? wire.AsMemory(offset)
                       : ReadOnlyMemory<Byte>.Empty;

        }

        #endregion

        #region IsSameRRSet(this Record, Other)

        /// <summary>
        /// Whether both records belong to the same resource record set: same owner name
        /// (case-insensitive), same type and same class (the cache-flush bit is ignored).
        /// </summary>
        /// <param name="Record">A resource record.</param>
        /// <param name="Other">Another resource record.</param>
        public static Boolean IsSameRRSet(this IDNSResourceRecord  Record,
                                          IDNSResourceRecord       Other)

            => Record.Type == Other.Type &&
               PlainClass(Record.Class) == PlainClass(Other.Class) &&
               Record.DomainName.Equals(Other.DomainName);

        #endregion

        #region IsIdenticalTo(this Record, Other)

        /// <summary>
        /// Whether both records are identical apart from their time-to-live:
        /// same resource record set and the same RDATA (RFC 6762 §6 and §7.1).
        /// </summary>
        /// <param name="Record">A resource record.</param>
        /// <param name="Other">Another resource record.</param>
        public static Boolean IsIdenticalTo(this IDNSResourceRecord  Record,
                                            IDNSResourceRecord       Other)

            => Record.IsSameRRSet(Other) &&
               Record.RData().Span.SequenceEqual(Other.RData().Span);

        #endregion

        #region CompareLexicographically(this Record, Other)

        /// <summary>
        /// Compare two records the way simultaneous probe tie-breaking does (RFC 6762 §8.2.1):
        /// first by class, then by type, then by the raw RDATA as unsigned bytes, where a
        /// record whose RDATA is a prefix of the other's RDATA is the lesser one.
        /// </summary>
        /// <param name="Record">A resource record.</param>
        /// <param name="Other">Another resource record.</param>
        /// <returns>Negative when this record is lexicographically smaller, zero when equal, positive when greater.</returns>
        public static Int32 CompareLexicographically(this IDNSResourceRecord  Record,
                                                     IDNSResourceRecord       Other)
        {

            var classComparison = PlainClass(Record.Class).CompareTo(PlainClass(Other.Class));
            if (classComparison != 0)
                return classComparison;

            var typeComparison = ((UInt16) Record.Type).CompareTo((UInt16) Other.Type);
            if (typeComparison != 0)
                return typeComparison;

            return Record.RData().Span.SequenceCompareTo(Other.RData().Span);

        }

        #endregion

        #region CompareLexicographically(this Records, Others)

        /// <summary>
        /// Compare two sets of records the way simultaneous probe tie-breaking does
        /// (RFC 6762 §8.2.1): both sets are sorted lexicographically and compared record
        /// by record; a set that is a prefix of the other set is the lesser one.
        /// </summary>
        /// <param name="Records">A set of resource records.</param>
        /// <param name="Others">Another set of resource records.</param>
        public static Int32 CompareLexicographically(this IEnumerable<IDNSResourceRecord>  Records,
                                                     IEnumerable<IDNSResourceRecord>       Others)
        {

            var ours    = Records.OrderBy(record => record, LexicographicComparer.Instance).ToArray();
            var theirs  = Others. OrderBy(record => record, LexicographicComparer.Instance).ToArray();

            for (var i = 0; i < Math.Min(ours.Length, theirs.Length); i++)
            {
                var comparison = ours[i].CompareLexicographically(theirs[i]);
                if (comparison != 0)
                    return comparison;
            }

            return ours.Length.CompareTo(theirs.Length);

        }

        #endregion

        #region RecordKey(this Record)

        /// <summary>
        /// A dictionary key identifying the record by owner name (case-insensitive), type,
        /// class and RDATA.
        /// </summary>
        /// <param name="Record">A resource record.</param>
        public static String RecordKey(this IDNSResourceRecord Record)

            => String.Concat(
                   Record.DomainName.FullName.ToLowerInvariant(),
                   "|",
                   ((UInt16) Record.Type).ToString(),
                   "|",
                   PlainClass(Record.Class).ToString(),
                   "|",
                   Convert.ToHexString(Record.RData().Span)
               );

        #endregion

        #region RRSetKey(this Record)

        /// <summary>
        /// A dictionary key identifying the resource record set of the record
        /// (owner name case-insensitive, type and class).
        /// </summary>
        /// <param name="Record">A resource record.</param>
        public static String RRSetKey(this IDNSResourceRecord Record)

            => RRSetKey(Record.DomainName, Record.Type, Record.Class);

        /// <summary>
        /// A dictionary key identifying a resource record set (owner name case-insensitive, type and class).
        /// </summary>
        /// <param name="Name">The owner name.</param>
        /// <param name="Type">The resource record type.</param>
        /// <param name="Class">The class.</param>
        public static String RRSetKey(DNSServiceName          Name,
                                      DNSResourceRecordTypes  Type,
                                      DNSQueryClasses         Class)

            => String.Concat(
                   Name.FullName.ToLowerInvariant(),
                   "|",
                   ((UInt16) Type).ToString(),
                   "|",
                   PlainClass(Class).ToString()
               );

        #endregion


        #region (private static) PlainClass(Class)

        private static UInt16 PlainClass(DNSQueryClasses Class)
            => (UInt16) ((UInt16) Class & MulticastDNS.ClassMask);

        #endregion

        #region (class) LexicographicComparer

        private sealed class LexicographicComparer : IComparer<IDNSResourceRecord>
        {

            public static readonly LexicographicComparer Instance = new();

            public Int32 Compare(IDNSResourceRecord? x, IDNSResourceRecord? y)
            {

                if (ReferenceEquals(x, y))
                    return 0;

                if (x is null)
                    return -1;

                if (y is null)
                    return 1;

                return x.CompareLexicographically(y);

            }

        }

        #endregion

    }

}
