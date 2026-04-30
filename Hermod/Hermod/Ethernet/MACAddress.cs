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
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// Extension methods for MAC addresses.
    /// </summary>
    public static class MACAddressExtensions
    {

        /// <summary>
        /// Indicates whether this MAC address is null or zero.
        /// </summary>
        /// <param name="MACAddress">A MAC address.</param>
        public static Boolean IsNullOrZero(this MACAddress? MACAddress)
            => !MACAddress.HasValue || MACAddress.Value.IsZero;

        /// <summary>
        /// Indicates whether this MAC address is NOT null or zero.
        /// </summary>
        /// <param name="MACAddress">A MAC address.</param>
        public static Boolean IsNotNullOrZero(this MACAddress? MACAddress)
            => MACAddress.HasValue && !MACAddress.Value.IsZero;

    }


    /// <summary>
    /// A 48-bit IEEE MAC address.
    /// </summary>
    public readonly struct MACAddress : IEquatable<MACAddress>,
                                        IComparable<MACAddress>,
                                        IComparable,
                                        IParsable<MACAddress>,
                                        ISpanParsable<MACAddress>,
                                        IFormattable,
                                        ISpanFormattable
    {

        #region Data

        private readonly Byte byte0, byte1, byte2, byte3, byte4, byte5;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this MAC address is either zero (00:00:00:00:00:00) or broadcast (FF:FF:FF:FF:FF:FF).
        /// </summary>
        public Boolean IsZeroOrBroadcast
            => IsZero || IsBroadcast;

        /// <summary>
        /// The zero MAC address (00:00:00:00:00:00).
        /// </summary>
        public static MACAddress Zero { get; }
            = new ([0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        /// <summary>
        /// Whether this MAC address is zero (00:00:00:00:00:00).
        /// </summary>
        public Boolean IsZero
            => byte0 == 0 && byte1 == 0 && byte2 == 0 &&
               byte3 == 0 && byte4 == 0 && byte5 == 0;


        /// <summary>
        /// Whether this MAC address is a unicast address
        /// (least significant bit of first byte is 0).
        /// </summary>
        public Boolean IsUnicast
            => !IsMulticast;

        /// <summary>
        /// Whether this MAC address is a multicast address
        /// (least significant bit of first byte is 1).
        /// </summary>
        public Boolean IsMulticast
            => (byte0 & 0x01) == 1;

        /// <summary>
        /// Whether this MAC address is an IPv4 multicast MAC mapping
        /// (01:00:5E:00:00:00 through 01:00:5E:7F:FF:FF).
        /// </summary>
        public Boolean IsIPv4Multicast
            => byte0 == 0x01 &&
               byte1 == 0x00 &&
               byte2 == 0x5E &&
              (byte3 & 0x80) == 0x00;

        /// <summary>
        /// Whether this MAC address is an IPv6 multicast address (33:33:XX:XX:XX:XX).
        /// </summary>
        public Boolean IsIPv6Multicast
            => byte0 == 0x33 &&
               byte1 == 0x33;


        /// <summary>
        /// Whether this MAC address is the VRRP (Virtual Router Redundancy Protocol) address (00:00:5E:00:01:XX or 00:00:5E:00:02:XX).
        /// </summary>
        public Boolean IsVRRP
            => byte0 == 0x00 &&
               byte1 == 0x00 &&
               byte2 == 0x5E &&
               byte3 == 0x00 &&
              (byte4 == 0x01 || byte4 == 0x02);

        /// <summary>
        /// Whether this MAC address is the STP bridge group address (01:80:C2:00:00:00).
        /// </summary>
        public Boolean IsSTP
            => byte0 == 0x01 &&
               byte1 == 0x80 &&
               byte2 == 0xC2 &&
               byte3 == 0x00 &&
               byte4 == 0x00 &&
               byte5 == 0x00;

        /// <summary>
        /// Whether this MAC address is the LLDP nearest bridge group address (01:80:C2:00:00:0E).
        /// </summary>
        public Boolean IsLLDP
            => byte0 == 0x01 &&
               byte1 == 0x80 &&
               byte2 == 0xC2 &&
               byte3 == 0x00 &&
               byte4 == 0x00 &&
               byte5 == 0x0E;


        /// <summary>
        /// Whether this MAC address is the Ethernet MAC Control pause address (01:80:C2:00:00:01).
        /// </summary>
        public Boolean IsPauseFrameAddress
            => byte0 == 0x01 &&
               byte1 == 0x80 &&
               byte2 == 0xC2 &&
               byte3 == 0x00 &&
               byte4 == 0x00 &&
               byte5 == 0x01;

        /// <summary>
        /// Whether this MAC address is the slow protocols multicast address
        /// commonly used for LACP (01:80:C2:00:00:02).
        /// </summary>
        public Boolean IsLACP
            => byte0 == 0x01 &&
               byte1 == 0x80 &&
               byte2 == 0xC2 &&
               byte3 == 0x00 &&
               byte4 == 0x00 &&
               byte5 == 0x02;

        /// <summary>
        /// Whether this MAC address is the IEEE 802.1X PAE (Port Access Entity) address (01:80:C2:00:00:03).
        /// </summary>
        public Boolean IsIEEE8021X
            => byte0 == 0x01 &&
               byte1 == 0x80 &&
               byte2 == 0xC2 &&
               byte3 == 0x00 &&
               byte4 == 0x00 &&
               byte5 == 0x03;


        /// <summary>
        /// The broadcast MAC address (FF:FF:FF:FF:FF:FF).
        /// </summary>
        public static MACAddress Broadcast { get; }
            = new ([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        /// <summary>
        /// Whether this MAC address is a broadcast address (FF:FF:FF:FF:FF:FF).
        /// </summary>
        public Boolean IsBroadcast
            => byte0 == 0xFF && byte1 == 0xFF && byte2 == 0xFF &&
               byte3 == 0xFF && byte4 == 0xFF && byte5 == 0xFF;








        /// <summary>
        /// Whether this MAC address is globally administered.
        /// Usually assigned from an IEEE OUI/MA block.
        /// </summary>
        public Boolean IsGloballyAdministered
            => (byte0 & 0x02) == 0;

        /// <summary>
        /// Whether this MAC address is locally administered.
        /// </summary>
        public Boolean IsLocallyAdministered
            => (byte0 & 0x02) != 0;

        /// <summary>
        /// Return the OUI (Organizationally Unique Identifier) part of this MAC address.
        /// </summary>
        public String OUI
            => $"{byte0:X2}:{byte1:X2}:{byte2:X2}";



        /// <summary>
        /// Return a .NET PhysicalAddress representation of this MAC address.
        /// </summary>
        public PhysicalAddress ToPhysicalAddress()
            => new ([byte0, byte1, byte2, byte3, byte4, byte5]);

        /// <summary>
        /// Convert the given .NET PhysicalAddress to a MACAddress.
        /// </summary>
        /// <param name="PhysicalAddressa">A .NET PhysicalAddress.</param>
        public static MACAddress FromPhysicalAddress(PhysicalAddress PhysicalAddress)
            => MACAddress.From(PhysicalAddress.GetAddressBytes());


        /// <summary>
        /// The EUI-64 representation (IPv6) of this MAC address.
        /// </summary>
        public Byte[] AsEUI64()
            => [ byte0, byte1, byte2, 0xFF, 0xFE, byte3, byte4, byte5 ];

        /// <summary>
        /// The modified EUI-64 representation (IPv6) of this MAC address, with the universal/local bit inverted.
        /// </summary>
        public Byte[] AsIPv6ModifiedEUI64()
            => [ (Byte) (byte0 ^ 0x02), byte1, byte2, 0xFF, 0xFE, byte3, byte4, byte5 ];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new MAC address from the given bytes.
        /// </summary>
        /// <param name="Bytes">A 6-byte span containing the MAC address.</param>
        private MACAddress(ReadOnlySpan<Byte> Bytes)
        {

            this.byte0 = Bytes[0];
            this.byte1 = Bytes[1];
            this.byte2 = Bytes[2];
            this.byte3 = Bytes[3];
            this.byte4 = Bytes[4];
            this.byte5 = Bytes[5];

        }

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        public static MACAddress Parse(String Text)

            => TryParse(Text, out var macAddress)
                   ? macAddress
                   : throw new FormatException($"Invalid MAC address: '{Text}'!");

        #endregion

        #region Parse    (Text, Provider)

        /// <summary>
        /// Parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="Provider">A format provider (ignored).</param>
        public static MACAddress Parse(String Text, IFormatProvider? Provider)

            => TryParse(Text, Provider, out var macAddress)
                   ? macAddress
                   : throw new FormatException($"Invalid MAC address: '{Text}'!");

        #endregion

        #region Parse    (Text, Provider)

        /// <summary>
        /// Parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="Provider">A format provider (ignored).</param>
        public static MACAddress Parse(ReadOnlySpan<Char> Text, IFormatProvider? Provider)

            => TryParse(Text, Provider, out var macAddress)
                   ? macAddress
                   : throw new FormatException($"Invalid MAC address: '{Text}'!");

        #endregion


        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        public static MACAddress? TryParse(String? Text)

            => TryParse(Text, out var macAddress)
                   ? macAddress
                   : null;


        /// <summary>
        /// Try to parse the given span as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        public static MACAddress? TryParse(ReadOnlySpan<Char> Text)

            => TryParse(Text, out var macAddress)
                   ? macAddress
                   : null;

        #endregion

        #region TryParse (Text, Provider, out MACAddress)

        /// <summary>
        /// Try to parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="Provider">MAC addresses are culture-invariant, so we can ignore the provider!</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParse([NotNullWhen(true)] String?  Text,
                                       IFormatProvider?             Provider,
                                       out MACAddress               MACAddress)

            => TryParse(
                   Text,
                   out MACAddress
               );


        /// <summary>
        /// Try to parse the given span as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="Provider">MAC addresses are culture-invariant, so we can ignore the provider!</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParse(ReadOnlySpan<Char>  Text,
                                       IFormatProvider?    Provider,
                                       out MACAddress      MACAddress)

            => TryParse(
                   Text,
                   out MACAddress
               );

        #endregion


        #region (private) TryParseByte (Text, Offset, out Byte)

        private static Boolean TryParseByte(ReadOnlySpan<Char>  Text,
                                            Int32               Offset,
                                            out Byte            Value)
        {

            Value = default;

            if (Offset < 0 || Offset + 2 > Text.Length)
                return false;

            return Byte.TryParse(
                       Text.Slice(Offset, 2),
                       NumberStyles.AllowHexSpecifier,
                       CultureInfo.InvariantCulture,
                       out Value
                   );

        }

        #endregion

        #region TryParsePlain     (Text,            out MACAddress)

        /// <summary>
        /// Try to parse the given text as a plain MAC address (e.g. AABBCCDDEEFF).
        /// </summary>
        /// <param name="Text">A text representation of a plain MAC address (e.g. AABBCCDDEEFF).</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParsePlain(String? Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            return TryParsePlain(
                       Text.AsSpan(),
                       out MACAddress
                   );

        }


        /// <summary>
        /// Try to parse the given span as a plain MAC address (e.g. AABBCCDDEEFF).
        /// </summary>
        /// <param name="Text">A text representation of a plain MAC address (e.g. AABBCCDDEEFF).</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParsePlain(ReadOnlySpan<Char> Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            Text = Text.Trim();

            if (Text.Length != 12)
                return false;

            Span<Byte> bytes = stackalloc Byte[6];

            for (var i = 0; i < 6; i++)
            {
                if (!TryParseByte(Text, i * 2, out bytes[i]))
                    return false;
            }

            MACAddress = new MACAddress(bytes);
            return true;

        }

        #endregion

        #region TryParseSeparated (Text, Separator, out MACAddress)

        /// <summary>
        /// Try to parse the given text as a MAC address with the given separator (e.g. AA:BB:CC:DD:EE:FF or AA-BB-CC-DD-EE-FF).
        /// </summary>
        /// <param name="Text">A text representation of a MAC address with the specified separator.</param>
        /// <param name="Separator">A character used as a separator between the bytes of the MAC address (e.g. ':' or '-').</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParseSeparated(String? Text, Char Separator, out MACAddress MACAddress)
        {

            MACAddress = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            return TryParseSeparated(
                       Text.AsSpan(),
                       Separator,
                       out MACAddress
                   );

        }


        /// <summary>
        /// Try to parse the given span as a MAC address with the given separator (e.g. AA:BB:CC:DD:EE:FF or AA-BB-CC-DD-EE-FF).
        /// </summary>
        /// <param name="Text">A text representation of a MAC address with the specified separator.</param>
        /// <param name="Separator">A character used as a separator between the bytes of the MAC address (e.g. ':' or '-').</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParseSeparated(ReadOnlySpan<Char> Text, Char Separator, out MACAddress MACAddress)
        {

            MACAddress = default;

            Text = Text.Trim();

            if (Text.Length != 17)
                return false;

            if (Text[ 2] != Separator ||
                Text[ 5] != Separator ||
                Text[ 8] != Separator ||
                Text[11] != Separator ||
                Text[14] != Separator)
            {
                return false;
            }

            Span<Byte> bytes = stackalloc Byte[6];

            for (var i = 0; i < 6; i++)
            {
                if (!TryParseByte(Text, i * 3, out bytes[i]))
                    return false;
            }

            MACAddress = new MACAddress(bytes);
            return true;

        }

        #endregion

        #region TryParseCisco     (Text,            out MACAddress)

        /// <summary>
        /// Try to parse the given text as a Cisco-style MAC address (aabb.ccdd.eeff).
        /// </summary>
        /// <param name="Text">A text representation of a Cisco-style MAC address.</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParseCisco(String? Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            return TryParseCisco(
                       Text.AsSpan(),
                       out MACAddress
                   );

        }


        /// <summary>
        /// Try to parse the given span as a Cisco-style MAC address (aabb.ccdd.eeff).
        /// </summary>
        /// <param name="Text">A text representation of a Cisco-style MAC address.</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParseCisco(ReadOnlySpan<Char> Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            Text = Text.Trim();

            if (Text.Length != 14)
                return false;

            if (Text[4] != '.' ||
                Text[9] != '.')
            {
                return false;
            }

            Span<Byte> bytes = stackalloc Byte[6];

            if (TryParseByte(Text,  0, out bytes[0]) &&
                TryParseByte(Text,  2, out bytes[1]) &&
                TryParseByte(Text,  5, out bytes[2]) &&
                TryParseByte(Text,  7, out bytes[3]) &&
                TryParseByte(Text, 10, out bytes[4]) &&
                TryParseByte(Text, 12, out bytes[5]))
            {
                MACAddress = new MACAddress(bytes);
                return true;
            }

            return false;

        }

        #endregion

        #region TryParse          (Text,            out MACAddress)

        /// <summary>
        /// Try to parse the given text as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParse(String? Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            return TryParse(
                       Text.AsSpan(),
                       out MACAddress
                   );

        }


        /// <summary>
        /// Try to parse the given span as a MAC address.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        public static Boolean TryParse(ReadOnlySpan<Char> Text, out MACAddress MACAddress)
        {

            MACAddress = default;

            if (Text.IsEmpty)
                return false;

            Text = Text.Trim();

            if (TryParsePlain     (Text,      out MACAddress) ||
                TryParseSeparated (Text, ':', out MACAddress) ||
                TryParseSeparated (Text, '-', out MACAddress) ||
                TryParseCisco     (Text,      out MACAddress))
            {
                return true;
            }

            return false;

        }

        #endregion


        #region TryRead           (Text, out MACAddress, out CharsConsumed)

        /// <summary>
        /// Try to read a MAC address from the beginning of the given text,
        /// and return how many characters were consumed.
        /// </summary>
        /// <param name="Text">A text representation of a MAC address.</param>
        /// <param name="MACAddress">The parsed MAC address.</param>
        /// <param name="CharsConsumed">The number of characters consumed from the input text.</param>
        public static Boolean TryRead(ReadOnlySpan<Char>  Text,
                                      out MACAddress      MACAddress,
                                      out Int32           CharsConsumed)
        {

            MACAddress    = default;
            CharsConsumed = 0;

            var originalLength = Text.Length;

            Text = Text.TrimStart();

            var skipped = originalLength - Text.Length;

            if (Text.Length >= 17)
            {
                if (TryParseSeparated(Text[..17], ':', out MACAddress) ||
                    TryParseSeparated(Text[..17], '-', out MACAddress))
                {
                    CharsConsumed = skipped + 17;
                    return true;
                }
            }

            if (Text.Length >= 14 &&
                TryParseCisco(Text[..14], out MACAddress))
            {
                CharsConsumed = skipped + 14;
                return true;
            }

            if (Text.Length >= 12 &&
                TryParsePlain(Text[..12], out MACAddress))
            {
                CharsConsumed = skipped + 12;
                return true;
            }

            return false;

        }

        #endregion



        #region From              (Bytes)


        /// <summary>
        /// Create a new MAC address from the given span of bytes.
        /// </summary>
        /// <param name="Bytes">The bytes representing the MAC address.</param>
        public static MACAddress From(ReadOnlySpan<Byte> Bytes)
        {

            if (Bytes.Length != 6)
                throw new ArgumentException("A MAC address must be exactly 6 bytes long!", nameof(Bytes));

            return new MACAddress(Bytes);

        }

        #endregion

        #region TryFrom           (Bytes)

        /// <summary>
        /// Try to create a new MAC address from the given span of bytes.
        /// </summary>
        /// <param name="Bytes">The bytes representing the MAC address.</param>
        public static MACAddress? TryFrom(ReadOnlySpan<Byte> Bytes)
        {

            if (Bytes.Length != 6)
                return null;

            return new MACAddress(Bytes);

        }

        #endregion

        #region FromIPv4Multicast (IPv4Address)

        /// <summary>
        /// Create a new MAC address from the given IPv4 address.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address.</param>
        public static MACAddress FromIPv4Multicast(IPv4Address IPv4Address)
        {

            var ipv4Address = IPv4Address.GetBytes();

            if (ipv4Address.Length != 4)
                throw new ArgumentException("An IPv4 address must be exactly 4 bytes long!", nameof(IPv4Address));

            if (!IPv4Address.IsMulticast)
                throw new ArgumentException("The IPv4 address must be a multicast address!", nameof(IPv4Address));


            // 01:00:5E:0xxxxxxx:xxxxxxxx:xxxxxxxx
            return From([
                            0x01,
                            0x00,
                            0x5E,
                            (Byte) (ipv4Address[1] & 0x7F),
                            ipv4Address[2],
                            ipv4Address[3]
                        ]);

        }

        #endregion

        #region FromIPv6Multicast (IPv4Address)

        /// <summary>
        /// Create a new MAC address from the given IPv6 address.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address.</param>
        public static MACAddress FromIPv6Multicast(IPv6Address IPv6Address)
        {

            var ipv6Address = IPv6Address.GetBytes();

            if (ipv6Address.Length != 16)
                throw new ArgumentException("An IPv6 address must be exactly 16 bytes long!", nameof(IPv6Address));

            if (!IPv6Address.IsMulticast)
                throw new ArgumentException("The IPv6 address must be a multicast address!", nameof(IPv6Address));


            return From([
                            0x33,
                            0x33,
                            ipv6Address[12],
                            ipv6Address[13],
                            ipv6Address[14],
                            ipv6Address[15]
                        ]);
        }

        #endregion


        #region Random (SetLocalAdministeredBit = true, SetMulticastBit = false)

        /// <summary>
        /// Return a random MAC address.
        /// </summary>
        /// <param name="SetLocalAdministeredBit"> Whether to set the local administered bit. Default is true.</param>
        /// <param name="SetMulticastBit">Whether to set the multicast bit. Default is false.</param>
        public static MACAddress Random(Boolean  SetLocalAdministeredBit   = true,
                                        Boolean  SetMulticastBit           = false)
        {

            var bytes = new Byte[6];

            RandomNumberGenerator.Fill(bytes);

            bytes[0] = (Byte) (
                                  (bytes[0] & 0xFC)                       |
                                  (SetLocalAdministeredBit ? 0x02 : 0x00) |
                                  (SetMulticastBit         ? 0x01 : 0x00)
                              );

            return new MACAddress(bytes);

        }

        #endregion

        #region RandomLocalUnicast()

        /// <summary>
        /// Return a random locally administered unicast MAC address.
        /// </summary>
        /// <returns></returns>
        public static MACAddress RandomLocalUnicast()

            => Random(SetLocalAdministeredBit:  true,
                      SetMulticastBit:          false);

        #endregion

        #region RandomLocalMulticast()

        /// <summary>
        /// Return a random locally administered multicast MAC address.
        /// </summary>
        public static MACAddress RandomLocalMulticast()

            => Random(SetLocalAdministeredBit:  true,
                      SetMulticastBit:          true);

        #endregion


        #region CopyTo(Destination)

        /// <summary>
        /// Copy the bytes of this MAC address into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to copy the bytes of this MAC address into.</param>
        public void CopyTo(Span<Byte> Destination)
        {

            if (Destination.Length < 6)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            Destination[0] = byte0;
            Destination[1] = byte1;
            Destination[2] = byte2;
            Destination[3] = byte3;
            Destination[4] = byte4;
            Destination[5] = byte5;

        }

        #endregion

        #region ToArray()

        /// <summary>
        /// Return the byte array representation of this MAC address.
        /// </summary>
        public Byte[] ToArray()

            => [byte0, byte1, byte2, byte3, byte4, byte5];

        #endregion

        #region Deconstruct(out Byte Byte0, out Byte Byte1, out Byte Byte2, out Byte Byte3, out Byte Byte4, out Byte Byte5)

        /// <summary>
        /// Deconstruct this MAC address into its individual bytes.
        /// </summary>
        /// <param name="Byte0">The first byte of the MAC address.</param>
        /// <param name="Byte1">The second byte of the MAC address.</param>
        /// <param name="Byte2">The third byte of the MAC address.</param>
        /// <param name="Byte3">The fourth byte of the MAC address.</param>
        /// <param name="Byte4">The fifth byte of the MAC address.</param>
        /// <param name="Byte5">The sixth byte of the MAC address.</param>
        public void Deconstruct(out Byte Byte0,
                                out Byte Byte1,
                                out Byte Byte2,
                                out Byte Byte3,
                                out Byte Byte4,
                                out Byte Byte5)

            => (Byte0,
                Byte1,
                Byte2,
                Byte3,
                Byte4,
                Byte5) = (byte0,
                          byte1,
                          byte2,
                          byte3,
                          byte4,
                          byte5);

        #endregion


        #region TryFormat

        /// <summary>
        /// Try to format this MAC address into the given destination span,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Destination">The destination span to write the formatted MAC address into.</param>
        /// <param name="CharsWritten">The number of characters written into the destination span.</param>
        /// <param name="Format">The format string to use when formatting the MAC address. Supported formats are:
        /// <param name="Provider">The format provider to use when formatting the MAC address. This parameter is ignored since MAC addresses are culture-invariant.</param>
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


        #region Operator overloading

        #region Operator == (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (MACAddress MACAddress1,
                                           MACAddress MACAddress2)

            => MACAddress1.Equals(MACAddress2);

        #endregion

        #region Operator != (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (MACAddress MACAddress1,
                                           MACAddress MACAddress2)

            => !MACAddress1.Equals(MACAddress2);

        #endregion

        #region Operator <  (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (MACAddress MACAddress1,
                                          MACAddress MACAddress2)

            => MACAddress1.CompareTo(MACAddress2) < 0;

        #endregion

        #region Operator <= (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (MACAddress MACAddress1,
                                           MACAddress MACAddress2)

            => MACAddress1.CompareTo(MACAddress2) <= 0;

        #endregion

        #region Operator >  (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (MACAddress MACAddress1,
                                          MACAddress MACAddress2)

            => MACAddress1.CompareTo(MACAddress2) > 0;

        #endregion

        #region Operator >= (MACAddress1, MACAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MACAddress1">A MAC address.</param>
        /// <param name="MACAddress2">Another MAC address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (MACAddress MACAddress1,
                                           MACAddress MACAddress2)

            => MACAddress1.CompareTo(MACAddress2) >= 0;

        #endregion

        #endregion

        #region IComparable<MACAddress> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two MAC addresses.
        /// </summary>
        /// <param name="Object">A MAC address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is MACAddress macAddress
                   ? CompareTo(macAddress)
                   : throw new ArgumentException("The given object is not a MAC address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(MACAddress)

        /// <summary>
        /// Compares two MAC addresses.
        /// </summary>
        /// <param name="MACAddress">A MAC address to compare with.</param>
        public Int32 CompareTo(MACAddress MACAddress)

            => byte0 != MACAddress.byte0 ? byte0.CompareTo(MACAddress.byte0) :
               byte1 != MACAddress.byte1 ? byte1.CompareTo(MACAddress.byte1) :
               byte2 != MACAddress.byte2 ? byte2.CompareTo(MACAddress.byte2) :
               byte3 != MACAddress.byte3 ? byte3.CompareTo(MACAddress.byte3) :
               byte4 != MACAddress.byte4 ? byte4.CompareTo(MACAddress.byte4) :
                                           byte5.CompareTo(MACAddress.byte5);

        #endregion

        #endregion

        #region IEquatable<MACAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two MAC addresses for equality.
        /// </summary>
        /// <param name="Object">A MAC address to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is MACAddress macAddress &&
                   Equals(macAddress);

        #endregion

        #region Equals(MACAddress)

        /// <summary>
        /// Compares two MAC addresses for equality.
        /// </summary>
        /// <param name="MACAddress">A MAC address to compare with.</param>
        public Boolean Equals(MACAddress MACAddress)

            => byte0 == MACAddress.byte0 &&
               byte1 == MACAddress.byte1 &&
               byte2 == MACAddress.byte2 &&
               byte3 == MACAddress.byte3 &&
               byte4 == MACAddress.byte4 &&
               byte5 == MACAddress.byte5;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => HashCode.Combine(byte0, byte1, byte2, byte3, byte4, byte5);

        #endregion

        #region ToString (Format)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the MAC address.</param>
        public String ToString(String? Format)

            => ToString(
                   Format,
                   null
               );

        #endregion

        #region ToString (Format, FormatProvider)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the MAC address.</param>
        /// <param name="FormatProvider">The format provider to use when formatting the MAC address. This parameter is ignored since MAC addresses are culture-invariant.</param>
        public String ToString(String?           Format,
                               IFormatProvider?  FormatProvider)

            => Format switch {
                null or "" or "G" or "C" => $"{byte0:X2}:{byte1:X2}:{byte2:X2}:{byte3:X2}:{byte4:X2}:{byte5:X2}",
                "D" => $"{byte0:X2}-{byte1:X2}-{byte2:X2}-{byte3:X2}-{byte4:X2}-{byte5:X2}",
                "N" => $"{byte0:X2}{byte1:X2}{byte2:X2}{byte3:X2}{byte4:X2}{byte5:X2}",
                "c" => $"{byte0:x2}:{byte1:x2}:{byte2:x2}:{byte3:x2}:{byte4:x2}:{byte5:x2}",
                "d" => $"{byte0:x2}-{byte1:x2}-{byte2:x2}-{byte3:x2}-{byte4:x2}-{byte5:x2}",
                "n" => $"{byte0:x2}{byte1:x2}{byte2:x2}{byte3:x2}{byte4:x2}{byte5:x2}",
                "Cisco" => $"{byte0:x2}{byte1:x2}.{byte2:x2}{byte3:x2}.{byte4:x2}{byte5:x2}",
                _ => throw new FormatException($"Invalid MAC address format: '{Format}'!")

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
