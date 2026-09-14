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
    /// One entry of an Address Prefix List (RFC 3123 §4): an address family, a
    /// prefix length, the address bits that survive the prefix, and whether the
    /// entry is negated.
    /// </summary>
    /// <param name="AddressFamily">The IANA address family; 1 is IPv4 and 2 is IPv6.</param>
    /// <param name="Prefix">The prefix length in bits.</param>
    /// <param name="Negated">Whether the item carries the '!' of RFC 3123 §5.</param>
    /// <param name="AFDPart">
    /// The address family dependent part, with trailing zero octets removed as
    /// RFC 3123 §4 requires of a sender.
    /// </param>
    public class APLItem(UInt16  AddressFamily,
                         Byte    Prefix,
                         Boolean Negated,
                         Byte[]  AFDPart)
    {

        #region Data

        /// <summary>The IANA address family number for IPv4.</summary>
        public const UInt16 IPv4 = 1;

        /// <summary>The IANA address family number for IPv6.</summary>
        public const UInt16 IPv6 = 2;

        #endregion

        #region Properties

        /// <summary>
        /// The IANA address family; 1 is IPv4 and 2 is IPv6.
        /// </summary>
        public UInt16   AddressFamily    { get; } = AddressFamily;

        /// <summary>
        /// The prefix length in bits.
        /// </summary>
        public Byte     Prefix           { get; } = Prefix;

        /// <summary>
        /// Whether this item is negated — the leading '!' of RFC 3123 §5.
        /// </summary>
        public Boolean  Negated          { get; } = Negated;

        /// <summary>
        /// The address family dependent part, trailing zero octets removed.
        /// </summary>
        public Byte[]   AFDPart          { get; } = AFDPart;

        /// <summary>
        /// Whether RFC 3123 gives this address family a text form. It names only
        /// two, so an item in any other family has to be written in the RFC 3597
        /// §5 generic form instead of being given invented syntax.
        /// </summary>
        public Boolean  HasTextForm

            => AddressFamily == IPv4 ||
               AddressFamily == IPv6;

        #endregion


        #region (static) Create(AddressFamily, Prefix, Negated, FullAddress)

        /// <summary>
        /// Create an item from a complete address, removing the trailing zero
        /// octets that RFC 3123 §4 forbids a sender to include.
        /// </summary>
        /// <param name="AddressFamily">The IANA address family.</param>
        /// <param name="Prefix">The prefix length in bits.</param>
        /// <param name="Negated">Whether the item is negated.</param>
        /// <param name="FullAddress">The address in full, 4 octets for IPv4 or 16 for IPv6.</param>
        public static APLItem Create(UInt16   AddressFamily,
                                     Byte     Prefix,
                                     Boolean  Negated,
                                     Byte[]   FullAddress)
        {

            // "Trailing zero octets do not bear any information (e.g., there is
            // no semantic difference between 10.0.0.0/16 and 10/16)" and "the
            // sender MUST NOT include trailing zero octets in the AFDPART
            // regardless of the value of PREFIX" (RFC 3123 §4).
            var length = FullAddress.Length;

            while (length > 0 && FullAddress[length - 1] == 0)
                length--;

            return new APLItem(
                       AddressFamily,
                       Prefix,
                       Negated,
                       FullAddress[..length]
                   );

        }

        #endregion

        #region FullAddress()

        /// <summary>
        /// The address with the trailing zero octets put back, so that it can be
        /// handed to an address type: 4 octets for IPv4, 16 for IPv6, and the
        /// AFDPART unchanged for any other family.
        /// </summary>
        public Byte[] FullAddress()
        {

            var width = AddressFamily switch {
                            IPv4  =>  4,
                            IPv6  => 16,
                            _     => AFDPart.Length
                        };

            if (AFDPart.Length >= width)
                return AFDPart;

            var full = new Byte[width];
            Array.Copy(AFDPart, full, AFDPart.Length);

            return full;

        }

        #endregion

        #region (static) TryParse(Text, out Item)

        /// <summary>
        /// Try to read one item of the RFC 3123 §5 presentation format:
        /// an optional '!', the address family, ':', the address, '/', and the
        /// prefix length.
        /// </summary>
        /// <param name="Text">One whitespace-delimited item of an APL RDATA.</param>
        /// <param name="Item">The parsed item.</param>
        public static Boolean TryParse(String Text, out APLItem? Item)
        {

            Item = null;

            var text     = Text.Trim();
            var negated  = text.StartsWith('!');

            if (negated)
                text = text[1..];

            var colon = text.IndexOf(':');
            if (colon < 1)
                return false;

            if (!UInt16.TryParse(text[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var family))
                return false;

            var rest  = text[(colon + 1)..];

            // The address of an IPv6 item contains colons of its own, so the
            // prefix is split off from the right.
            var slash = rest.LastIndexOf('/');
            if (slash < 0)
                return false;

            if (!Byte.TryParse(rest[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
                return false;

            var address = rest[..slash];

            switch (family)
            {

                case IPv4:
                    {

                        if (prefix > 32)
                            return false;

                        if (!IPv4Address.TryParse(address, out var ipv4))
                            return false;

                        Item = Create(IPv4, prefix, negated, ipv4.GetBytes());
                        return true;

                    }

                case IPv6:
                    {

                        if (prefix > 128)
                            return false;

                        if (!IPv6Address.TryParse(address, out var ipv6))
                            return false;

                        Item = Create(IPv6, prefix, negated, ipv6.GetBytes());
                        return true;

                    }

                // RFC 3123 §5 gives a text form to two address families and no
                // others. Refusing here rather than guessing is what leaves the
                // RFC 3597 §5 generic form as the way to write the rest.
                default:
                    return false;

            }

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The RFC 3123 §5 presentation form of this item.
        /// </summary>
        public override String ToString()
        {

            var address = AddressFamily switch {
                              IPv4  => new IPv4Address(FullAddress()).ToString(),
                              IPv6  => DNSTools.ToZoneFileText(new IPv6Address(FullAddress())),
                              _     => AFDPart.ToHexString()
                          };

            return $"{(Negated ? "!" : "")}{AddressFamily}:{address}/{Prefix}";

        }

        #endregion

    }


    /// <summary>
    /// The DNS Address Prefix List (APL) resource record (RFC 3123), which
    /// carries a list of address prefixes, each of which may be negated.
    /// </summary>
    public class APL : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS APL resource record type identifier.
        /// </summary>
        public const DNSResourceRecordTypes TypeId = DNSResourceRecordTypes.APL;

        #endregion

        #region Properties

        /// <summary>
        /// The prefixes of this list, in the order they appear in the RDATA.
        /// RFC 3123 §4 gives them no ordering rule, so the order is preserved
        /// rather than normalised.
        /// </summary>
        public IEnumerable<APLItem>  Items    { get; }

        #endregion

        #region Constructor(s)

        #region APL(DomainName, Stream)

        /// <summary>
        /// Create a new APL resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this APL resource record.</param>
        /// <param name="Stream">A stream containing the APL resource record data.</param>
        public APL(DomainName  DomainName,
                   Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            var rdLength  = Stream.ReadUInt16BE();
            var rdata     = DNSTools.ExtractByteArray(Stream, rdLength);
            var items     = new List<APLItem>();
            var offset    = 0;

            // Every item is at least four octets: family, prefix, and the octet
            // carrying the negation flag beside the length. A remainder shorter
            // than that is a truncated item, and stopping is the only thing left
            // to do — the stream is already past the RDATA either way, so no
            // record behind this one is lost.
            while (offset + 4 <= rdata.Length)
            {

                var family     = (UInt16) ((rdata[offset] << 8) | rdata[offset + 1]);
                var prefix     = rdata[offset + 2];
                var negated    = (rdata[offset + 3] & 0x80) == 0x80;
                var afdLength  = rdata[offset + 3] & 0x7F;

                offset += 4;

                if (offset + afdLength > rdata.Length)
                    break;

                items.Add(
                    new APLItem(
                        family,
                        prefix,
                        negated,
                        rdata[offset..(offset + afdLength)]
                    )
                );

                offset += afdLength;

            }

            this.Items = items;

        }

        #endregion

        #region APL(DomainName, Class, TimeToLive, Items)

        /// <summary>
        /// Create a new DNS APL resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this APL resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Items">The address prefixes of this list.</param>
        public APL(DomainName            DomainName,
                   DNSQueryClasses       Class,
                   TimeSpan              TimeToLive,
                   IEnumerable<APLItem>  Items)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Items = Items;

        }

        #endregion

        #endregion


        #region (static) TryParseFromJSON(Name, TimeToLive, Data)

        /// <summary>
        /// Try to parse this resource record from the RFC 3123 §5 presentation
        /// format, which is also what a DNS JSON API returns in its "data" field.
        /// </summary>
        /// <param name="Name">The owner name of this resource record.</param>
        /// <param name="TimeToLive">The TTL of this resource record.</param>
        /// <param name="Data">The presentation form of the RDATA.</param>
        /// <returns>The parsed resource record, or null if parsing fails.</returns>
        public static APL? TryParseFromJSON(DomainName  Name,
                                            TimeSpan    TimeToLive,
                                            String      Data)
        {

            var items = new List<APLItem>();

            // An empty prefix list is a legal APL: RFC 3123 §4 puts no lower
            // bound on the number of items, and an empty one denies everything.
            foreach (var word in Data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {

                if (!APLItem.TryParse(word, out var item) || item is null)
                    return null;

                items.Add(item);

            }

            return new APL(
                       Name,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       items
                   );

        }

        #endregion

        #region (protected override) ZoneFileRData()

        /// <inheritdoc/>
        /// <remarks>
        /// RFC 3123 §5 gives a text form to address families 1 and 2 only. An
        /// item in any other family is written with the whole record in the
        /// RFC 3597 §5 generic form, which is valid zone-file syntax for any
        /// type and reads back through the same wire constructor — rather than
        /// inventing a syntax for it, or throwing and leaving the record
        /// unwritable.
        /// </remarks>
        protected override String ZoneFileRData()
        {

            if (!Items.All(item => item.HasTextForm))
            {

                var rdata = RData();

                return $"\\# {rdata.Length} {rdata.ToHexString()}";

            }

            return Items.Select(item => item.ToString()).AggregateWith(" ");

        }

        #endregion

        #region (private) RData()

        /// <summary>
        /// The RDATA octets of this record, without the length that precedes them.
        /// </summary>
        private Byte[] RData()
        {

            using var stream = new MemoryStream();

            foreach (var item in Items)
            {

                stream.WriteUInt16BE(item.AddressFamily);
                stream.WriteByte    (item.Prefix);
                stream.WriteByte    ((Byte) ((item.Negated ? 0x80 : 0x00) | (item.AFDPart.Length & 0x7F)));
                stream.Write        (item.AFDPart, 0, item.AFDPart.Length);

            }

            return stream.ToArray();

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

            var rdata = RData();

            Stream.WriteUInt16BE(rdata.Length);
            Stream.Write        (rdata, 0, rdata.Length);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Items.Select(item => item.ToString()).AggregateWith(" ")}, {base.ToString()}";

        #endregion

    }

}
