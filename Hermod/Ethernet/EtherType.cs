/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// Extension methods for EtherTypes.
    /// </summary>
    public static class EtherTypeExtensions
    {

        /// <summary>
        /// Indicates whether this EtherType is null or zero.
        /// </summary>
        /// <param name="EtherType">An EtherType.</param>
        public static Boolean IsNullOrZero(this EtherType? EtherType)
            => !EtherType.HasValue || EtherType.Value.Value == 0;

        /// <summary>
        /// Indicates whether this EtherType is NOT null or zero.
        /// </summary>
        /// <param name="EtherType">An EtherType.</param>
        public static Boolean IsNotNullOrZero(this EtherType? EtherType)
            => EtherType.HasValue && EtherType.Value.Value != 0;

    }


    /// <summary>
    /// The 16-bit EtherType/Length field of an Ethernet frame (IEEE 802.3 clause 3.2.6).
    ///
    /// Values of 1500 (0x05DC) and below are an IEEE 802.3 *length* field, values of
    /// 1536 (0x0600) and above are an Ethernet II *type* field. The range in between
    /// is undefined and must not appear on the wire.
    /// </summary>
    public readonly struct EtherType : IEquatable<EtherType>,
                                       IComparable<EtherType>,
                                       IComparable,
                                       IParsable<EtherType>,
                                       ISpanParsable<EtherType>,
                                       IFormattable,
                                       ISpanFormattable
    {

        #region Data

        /// <summary>
        /// The number of bytes of an EtherType/Length field.
        /// </summary>
        public const  Byte    Length             = 2;

        /// <summary>
        /// The largest value that is interpreted as an IEEE 802.3 length field (1500).
        /// </summary>
        public const  UInt16  MaxLengthValue     = 1500;

        /// <summary>
        /// The smallest value that is interpreted as an Ethernet II type field (1536 / 0x0600).
        /// </summary>
        public const  UInt16  MinEtherTypeValue  = 1536;


        /// <summary>
        /// The well-known EtherTypes and their names.
        /// </summary>
        private static readonly Dictionary<UInt16, String> names = new () {

            { 0x0800, "IPv4"                     },
            { 0x0806, "ARP"                      },
            { 0x0842, "WakeOnLAN"                },
            { 0x22EA, "SRP"                      },  // IEEE 802.1Qat Stream Reservation Protocol
            { 0x22F0, "AVTP"                     },  // IEEE 1722 Audio Video Transport Protocol
            { 0x22F3, "TRILL"                    },
            { 0x8035, "RARP"                     },
            { 0x809B, "AppleTalk"                },
            { 0x80F3, "AARP"                     },
            { 0x8100, "VLAN"                     },  // IEEE 802.1Q C-Tag
            { 0x8137, "IPX"                      },
            { 0x86DD, "IPv6"                     },
            { 0x8808, "MACControl"               },  // IEEE 802.3x PAUSE
            { 0x8809, "SlowProtocols"            },  // LACP, Link OAM, ...
            { 0x8847, "MPLSUnicast"              },
            { 0x8848, "MPLSMulticast"            },
            { 0x8863, "PPPoEDiscovery"           },
            { 0x8864, "PPPoESession"             },
            { 0x888E, "EAPOL"                    },  // IEEE 802.1X
            { 0x88A2, "ATAoE"                    },
            { 0x88A4, "EtherCAT"                 },
            { 0x88A8, "ProviderBridging"         },  // IEEE 802.1ad S-Tag (QinQ)
            { 0x88AB, "Powerlink"                },
            { 0x88B8, "GOOSE"                    },  // IEC 61850
            { 0x88B9, "GSE"                      },  // IEC 61850
            { 0x88BA, "SampledValues"            },  // IEC 61850
            { 0x88CC, "LLDP"                     },
            { 0x88CD, "SERCOS3"                  },
            { 0x88E3, "MRP"                      },  // IEC 62439-2 Media Redundancy Protocol
            { 0x88E5, "MACsec"                   },  // IEEE 802.1AE
            { 0x88E7, "ProviderBackboneBridging" },  // IEEE 802.1ah I-Tag
            { 0x88F7, "PTP"                      },  // IEEE 1588
            { 0x88F8, "NCSI"                     },
            { 0x88FB, "PRP"                      },  // Parallel Redundancy Protocol
            { 0x8902, "CFM"                      },  // IEEE 802.1ag / ITU-T Y.1731
            { 0x8906, "FCoE"                     },
            { 0x8914, "FCoEInitialization"       },
            { 0x8915, "RoCE"                     },
            { 0x891D, "TTEthernet"               },
            { 0x892F, "HSR"                      },  // High-availability Seamless Redundancy
            { 0x9000, "ECTP"                     },  // Ethernet Configuration Testing Protocol
            { 0x9100, "LegacyQinQ"               }

        };

        /// <summary>
        /// The reverse lookup of the well-known EtherType names.
        /// </summary>
        private static readonly Dictionary<String, UInt16> byName

            = names.ToDictionary(
                       keyValuePair => keyValuePair.Value,
                       keyValuePair => keyValuePair.Key,
                       StringComparer.OrdinalIgnoreCase
                   );

        #endregion

        #region Properties

        /// <summary>
        /// The numeric value of this EtherType/Length field.
        /// </summary>
        public UInt16   Value               { get; }


        /// <summary>
        /// Whether this field is an IEEE 802.3 length field (&lt;= 1500).
        /// </summary>
        public Boolean  IsLength

            => Value <= MaxLengthValue;


        /// <summary>
        /// Whether this field is an Ethernet II type field (&gt;= 1536).
        /// </summary>
        public Boolean  IsEtherType

            => Value >= MinEtherTypeValue;


        /// <summary>
        /// Whether this field lies within the undefined range between 1501 and 1535,
        /// which must never appear on the wire.
        /// </summary>
        public Boolean  IsUndefined

            => Value > MaxLengthValue &&
               Value < MinEtherTypeValue;


        /// <summary>
        /// Whether this EtherType is a well-known Tag Protocol Identifier introducing
        /// a VLAN tag (IEEE 802.1Q, IEEE 802.1ad or one of the legacy QinQ values).
        /// </summary>
        public Boolean  IsVLANTagProtocolIdentifier

            => Value is 0x8100 or 0x88A8 or 0x9100 or 0x9200 or 0x9300;


        /// <summary>
        /// The well-known name of this EtherType, or null.
        /// </summary>
        public String?  Name

            => names.TryGetValue(Value, out var name)
                   ? name
                   : null;


        /// <summary>
        /// Whether this EtherType is a well-known EtherType.
        /// </summary>
        public Boolean  IsWellKnown

            => names.ContainsKey(Value);

        #endregion

        #region Well-known EtherTypes

        /// <summary>Internet Protocol version 4 (0x0800).</summary>
        public static EtherType  IPv4                      { get; } = new (0x0800);

        /// <summary>Address Resolution Protocol (0x0806).</summary>
        public static EtherType  ARP                       { get; } = new (0x0806);

        /// <summary>Wake-on-LAN (0x0842).</summary>
        public static EtherType  WakeOnLAN                 { get; } = new (0x0842);

        /// <summary>Reverse Address Resolution Protocol (0x8035).</summary>
        public static EtherType  RARP                      { get; } = new (0x8035);

        /// <summary>IEEE 802.1Q customer VLAN tag / C-Tag (0x8100).</summary>
        public static EtherType  VLAN                      { get; } = new (0x8100);

        /// <summary>Internet Protocol version 6 (0x86DD).</summary>
        public static EtherType  IPv6                      { get; } = new (0x86DD);

        /// <summary>IEEE 802.3x MAC Control, e.g. PAUSE frames (0x8808).</summary>
        public static EtherType  MACControl                { get; } = new (0x8808);

        /// <summary>Slow protocols, e.g. LACP and Link OAM (0x8809).</summary>
        public static EtherType  SlowProtocols             { get; } = new (0x8809);

        /// <summary>MPLS unicast (0x8847).</summary>
        public static EtherType  MPLSUnicast               { get; } = new (0x8847);

        /// <summary>MPLS multicast (0x8848).</summary>
        public static EtherType  MPLSMulticast             { get; } = new (0x8848);

        /// <summary>PPPoE discovery stage (0x8863).</summary>
        public static EtherType  PPPoEDiscovery            { get; } = new (0x8863);

        /// <summary>PPPoE session stage (0x8864).</summary>
        public static EtherType  PPPoESession              { get; } = new (0x8864);

        /// <summary>IEEE 802.1X EAP over LAN (0x888E).</summary>
        public static EtherType  EAPOL                     { get; } = new (0x888E);

        /// <summary>IEEE 802.1ad service VLAN tag / S-Tag, "QinQ" (0x88A8).</summary>
        public static EtherType  ProviderBridging          { get; } = new (0x88A8);

        /// <summary>Link Layer Discovery Protocol (0x88CC).</summary>
        public static EtherType  LLDP                      { get; } = new (0x88CC);

        /// <summary>IEEE 802.1AE MAC Security (0x88E5).</summary>
        public static EtherType  MACsec                    { get; } = new (0x88E5);

        /// <summary>IEEE 802.1ah Provider Backbone Bridging / I-Tag (0x88E7).</summary>
        public static EtherType  ProviderBackboneBridging  { get; } = new (0x88E7);

        /// <summary>IEEE 1588 Precision Time Protocol over Ethernet (0x88F7).</summary>
        public static EtherType  PTP                       { get; } = new (0x88F7);

        /// <summary>Legacy vendor QinQ Tag Protocol Identifier (0x9100).</summary>
        public static EtherType  LegacyQinQ                { get; } = new (0x9100);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EtherType/Length field.
        /// </summary>
        /// <param name="Value">The numeric value of the EtherType/Length field.</param>
        private EtherType(UInt16 Value)
        {
            this.Value = Value;
        }

        #endregion


        #region From      (Value)

        /// <summary>
        /// Create a new EtherType from the given numeric value.
        /// </summary>
        /// <param name="Value">The numeric value of the EtherType/Length field.</param>
        public static EtherType From(UInt16 Value)

            => new (Value);

        #endregion

        #region FromLength (Length)

        /// <summary>
        /// Create a new IEEE 802.3 length field.
        /// </summary>
        /// <param name="Length">The length of the MAC client data, 0..1500 bytes.</param>
        public static EtherType FromLength(UInt16 Length)
        {

            if (Length > MaxLengthValue)
                throw new ArgumentOutOfRangeException(nameof(Length),
                                                      $"An IEEE 802.3 length field must not exceed {MaxLengthValue} bytes!");

            return new EtherType(Length);

        }

        #endregion

        #region From      (Bytes)

        /// <summary>
        /// Create a new EtherType from the given 2 bytes in network byte order.
        /// </summary>
        /// <param name="Bytes">A 2-byte span containing the EtherType/Length field.</param>
        public static EtherType From(ReadOnlySpan<Byte> Bytes)
        {

            if (Bytes.Length != Length)
                throw new ArgumentException($"An EtherType must be exactly {Length} bytes long!", nameof(Bytes));

            return new EtherType(BinaryPrimitives.ReadUInt16BigEndian(Bytes));

        }

        #endregion

        #region TryFrom   (Bytes)

        /// <summary>
        /// Try to create a new EtherType from the given 2 bytes in network byte order.
        /// </summary>
        /// <param name="Bytes">A 2-byte span containing the EtherType/Length field.</param>
        public static EtherType? TryFrom(ReadOnlySpan<Byte> Bytes)

            => Bytes.Length == Length
                   ? new EtherType(BinaryPrimitives.ReadUInt16BigEndian(Bytes))
                   : null;

        #endregion


        #region Parse     (Text)

        /// <summary>
        /// Parse the given text as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name (e.g. "IPv4"), a hexadecimal value (e.g. "0x0800") or a decimal value (e.g. "2048").</param>
        public static EtherType Parse(String Text)

            => TryParse(Text, out var etherType)
                   ? etherType
                   : throw new FormatException($"Invalid EtherType: '{Text}'!");

        #endregion

        #region Parse     (Text, Provider)

        /// <summary>
        /// Parse the given text as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="Provider">A format provider (ignored).</param>
        public static EtherType Parse(String Text, IFormatProvider? Provider)

            => Parse(Text);

        #endregion

        #region TryParse  (Text)

        /// <summary>
        /// Try to parse the given text as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        public static EtherType? TryParse(String? Text)

            => TryParse(Text, out var etherType)
                   ? etherType
                   : null;

        #endregion

        #region TryParse  (Text, out EtherType)

        /// <summary>
        /// Try to parse the given text as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="EtherType">The parsed EtherType.</param>
        public static Boolean TryParse(String? Text, out EtherType EtherType)

            => TryParse(Text.AsSpan(), out EtherType);

        #endregion

        #region TryParse  (Text, Provider, out EtherType)

        /// <summary>
        /// Try to parse the given text as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="Provider">EtherTypes are culture-invariant, so we can ignore the provider!</param>
        /// <param name="EtherType">The parsed EtherType.</param>
        public static Boolean TryParse([NotNullWhen(true)] String?  Text,
                                       IFormatProvider?             Provider,
                                       out EtherType                EtherType)

            => TryParse(Text, out EtherType);

        #endregion


        #region Parse     (Text, Provider)     [ISpanParsable]

        /// <summary>
        /// Parse the given span as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="Provider">A format provider (ignored).</param>
        public static EtherType Parse(ReadOnlySpan<Char> Text, IFormatProvider? Provider)

            => TryParse(Text, out var etherType)
                   ? etherType
                   : throw new FormatException($"Invalid EtherType: '{Text}'!");

        #endregion

        #region TryParse  (Text, out EtherType) [ISpanParsable]

        /// <summary>
        /// Try to parse the given span as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="EtherType">The parsed EtherType.</param>
        public static Boolean TryParse(ReadOnlySpan<Char> Text, out EtherType EtherType)
        {

            EtherType = default;

            var text = Text.Trim();

            if (text.IsEmpty)
                return false;

            // "IPv4 (0x0800)" => "0x0800"
            var bracket = text.IndexOf('(');
            if (bracket >= 0 && text[^1] == ')')
                text = text[(bracket + 1)..^1].Trim();

            if (byName.GetAlternateLookup<ReadOnlySpan<Char>>().TryGetValue(text, out var wellKnownValue))
            {
                EtherType = new EtherType(wellKnownValue);
                return true;
            }

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text[2..];

            else if (UInt16.TryParse(text,
                                     NumberStyles.None,
                                     CultureInfo.InvariantCulture,
                                     out var decimalValue))
            {
                EtherType = new EtherType(decimalValue);
                return true;
            }

            if (UInt16.TryParse(text,
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out var hexValue))
            {
                EtherType = new EtherType(hexValue);
                return true;
            }

            return false;

        }


        /// <summary>
        /// Try to parse the given span as an EtherType.
        /// </summary>
        /// <param name="Text">A well-known name, a hexadecimal value or a decimal value.</param>
        /// <param name="Provider">EtherTypes are culture-invariant, so we can ignore the provider!</param>
        /// <param name="EtherType">The parsed EtherType.</param>
        public static Boolean TryParse(ReadOnlySpan<Char>  Text,
                                       IFormatProvider?    Provider,
                                       out EtherType       EtherType)

            => TryParse(Text, out EtherType);

        #endregion

        #region TryFormat (Destination, out CharsWritten, Format, Provider)

        /// <summary>
        /// Try to format this EtherType into the given destination span,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Destination">The destination span to write the formatted EtherType into.</param>
        /// <param name="CharsWritten">The number of characters written into the destination span.</param>
        /// <param name="Format">The format string to use when formatting the EtherType.</param>
        /// <param name="Provider">The format provider to use. This parameter is ignored, since EtherTypes are culture-invariant.</param>
        public Boolean TryFormat(Span<Char>          Destination,
                                 out Int32           CharsWritten,
                                 ReadOnlySpan<Char>  Format,
                                 IFormatProvider?    Provider)
        {

            var text = ToString(
                           Format.IsEmpty
                               ? null
                               : Format.ToString(),
                           Provider
                       );

            if (text.Length > Destination.Length)
            {
                CharsWritten = 0;
                return false;
            }

            text.AsSpan().CopyTo(Destination);
            CharsWritten = text.Length;

            return true;

        }

        #endregion


        #region GetBytes()

        /// <summary>
        /// Return the 2 bytes of this EtherType in network byte order.
        /// </summary>
        public Byte[] GetBytes()

            => [ (Byte) (Value >> 8),
                 (Byte) (Value & 0xFF) ];

        #endregion

        #region WriteTo(Destination)

        /// <summary>
        /// Write the 2 bytes of this EtherType in network byte order
        /// into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes of this EtherType into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            BinaryPrimitives.WriteUInt16BigEndian(Destination, Value);

        }

        #endregion


        #region Operator overloading

        #region Operator == (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator == (EtherType EtherType1,
                                           EtherType EtherType2)

            => EtherType1.Equals(EtherType2);

        #endregion

        #region Operator != (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator != (EtherType EtherType1,
                                           EtherType EtherType2)

            => !EtherType1.Equals(EtherType2);

        #endregion

        #region Operator <  (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator < (EtherType EtherType1,
                                          EtherType EtherType2)

            => EtherType1.CompareTo(EtherType2) < 0;

        #endregion

        #region Operator <= (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator <= (EtherType EtherType1,
                                           EtherType EtherType2)

            => EtherType1.CompareTo(EtherType2) <= 0;

        #endregion

        #region Operator >  (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator > (EtherType EtherType1,
                                          EtherType EtherType2)

            => EtherType1.CompareTo(EtherType2) > 0;

        #endregion

        #region Operator >= (EtherType1, EtherType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EtherType1">An EtherType.</param>
        /// <param name="EtherType2">Another EtherType.</param>
        public static Boolean operator >= (EtherType EtherType1,
                                           EtherType EtherType2)

            => EtherType1.CompareTo(EtherType2) >= 0;

        #endregion

        #endregion

        #region IComparable<EtherType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two EtherTypes.
        /// </summary>
        /// <param name="Object">An EtherType to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is EtherType etherType
                   ? CompareTo(etherType)
                   : throw new ArgumentException("The given object is not an EtherType!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(EtherType)

        /// <summary>
        /// Compares two EtherTypes.
        /// </summary>
        /// <param name="EtherType">An EtherType to compare with.</param>
        public Int32 CompareTo(EtherType EtherType)

            => Value.CompareTo(EtherType.Value);

        #endregion

        #endregion

        #region IEquatable<EtherType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two EtherTypes for equality.
        /// </summary>
        /// <param name="Object">An EtherType to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is EtherType etherType &&
                   Equals(etherType);

        #endregion

        #region Equals(EtherType)

        /// <summary>
        /// Compares two EtherTypes for equality.
        /// </summary>
        /// <param name="EtherType">An EtherType to compare with.</param>
        public Boolean Equals(EtherType EtherType)

            => Value == EtherType.Value;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Value.GetHashCode();

        #endregion

        #region ToString (Format)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the EtherType.</param>
        public String ToString(String? Format)

            => ToString(Format, null);

        #endregion

        #region ToString (Format, FormatProvider)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the EtherType.</param>
        /// <param name="FormatProvider">The format provider to use. This parameter is ignored, since EtherTypes are culture-invariant.</param>
        public String ToString(String?           Format,
                               IFormatProvider?  FormatProvider)

            => Format switch {
                   null or "" or "G" => Name ?? $"0x{Value:X4}",
                   "X"               => $"0x{Value:X4}",
                   "x"               => $"0x{Value:x4}",
                   "D"               => Value.ToString(CultureInfo.InvariantCulture),
                   "F"               => Name is not null
                                            ? $"{Name} (0x{Value:X4})"
                                            : $"0x{Value:X4}",
                   _                 => throw new FormatException($"Invalid EtherType format: '{Format}'!")
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => ToString("G");

        #endregion

    }

}
