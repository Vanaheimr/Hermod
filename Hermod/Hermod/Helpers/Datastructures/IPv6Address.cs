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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv6 address.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct IPv6Address : IComparable<IPv6Address>,
                                         IEquatable<IPv6Address>,
                                         IIPAddress
    {

        #region Data

        private const            Byte    length    = 16;

        private static readonly  Char[]  splitter  = [':'];

        private readonly         Byte    byte0;
        private readonly         Byte    byte1;
        private readonly         Byte    byte2;
        private readonly         Byte    byte3;
        private readonly         Byte    byte4;
        private readonly         Byte    byte5;
        private readonly         Byte    byte6;
        private readonly         Byte    byte7;
        private readonly         Byte    byte8;
        private readonly         Byte    byte9;
        private readonly         Byte    byte10;
        private readonly         Byte    byte11;
        private readonly         Byte    byte12;
        private readonly         Byte    byte13;
        private readonly         Byte    byte14;
        private readonly         Byte    byte15;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the IPv6 address as ReadOnlySpan&lt;byte&gt;.
        /// </summary>
        public ReadOnlySpan<Byte>  AsSpan
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in byte0), length);

        /// <summary>
        /// The length of an IPv6 address.
        /// </summary>
        public Byte     Length
            => length;

        /// <summary>
        /// Whether the IP address is an IPv6 multicast address.
        /// </summary>
        public Boolean  IsMulticast
            => byte0 == 0xFF;

        public Boolean  IsIPv4
            => false;

        public Boolean  IsIPv6
            => true;

        public Boolean  IsLocalhost

            => byte0  == 0 && byte1  == 0 && byte2  == 0 && byte3  == 0 &&
               byte4  == 0 && byte5  == 0 && byte6  == 0 && byte7  == 0 &&
               byte8  == 0 && byte9  == 0 && byte10 == 0 && byte11 == 0 &&
               byte12 == 0 && byte13 == 0 && byte14 == 0 && byte15 == 1;

        public Boolean  IsAny

            => byte0  == 0 && byte1  == 0 && byte2  == 0 && byte3  == 0 &&
               byte4  == 0 && byte5  == 0 && byte6  == 0 && byte7  == 0 &&
               byte8  == 0 && byte9  == 0 && byte10 == 0 && byte11 == 0 &&
               byte12 == 0 && byte13 == 0 && byte14 == 0 && byte15 == 0;

        /// <summary>
        /// If this is an IPv4-mapped IPv6 (::ffff:w.x.y.z) address, returns "w.x.y.z".
        /// </summary>
        public IPv4Address?  MappedIPv4
        {
            get
            {

                // ...:ffff:...
                if (byte10 != 0xFF || byte11 != 0xFF)
                    return null;

                // 0000:0000:0000:0000:0000:...
                if (byte0 != 0 || byte1 != 0 || byte2 != 0 || byte3 != 0 ||
                    byte4 != 0 || byte5 != 0 || byte6 != 0 || byte7 != 0 ||
                    byte8 != 0 || byte9 != 0)
                    return null;

                return new IPv4Address(byte12, byte13, byte14, byte15);

            }
        }

        IPv4Address? IIPAddress.AsIPv4
            => MappedIPv4;

        /// <summary>
        /// True, when this is an IPv4-mapped IPv6 (::ffff:w.x.y.z) address.
        /// </summary>
        public Boolean IsMappedIPv4

            => MappedIPv4.HasValue;

        /// <summary>
        /// The interface identification for local IPv6 addresses, e.g. .
        /// </summary>
        public String   InterfaceId    { get; }

        #endregion

        #region Constructor(s)

        #region IPv6Address (Span,   InterfaceId = null)

        /// <summary>
        /// Create a new IPv6 address from a span of bytes.
        /// </summary>
        /// <param name="Span">The IPv6 address as a span of 16 bytes.</param>
        /// <param name="InterfaceId">An optional interface identification for the scope of the IPv6 address.</param>
        public IPv6Address(ReadOnlySpan<Byte>  Span,
                           String?             InterfaceId   = null)
        {

            this.InterfaceId = InterfaceId ?? "";

            if (Span.Length != length)
                throw new FormatException($"The given span of bytes must have a length of {length}!");

            this.byte0   = Span[0];
            this.byte1   = Span[1];
            this.byte2   = Span[2];
            this.byte3   = Span[3];
            this.byte4   = Span[4];
            this.byte5   = Span[5];
            this.byte6   = Span[6];
            this.byte7   = Span[7];
            this.byte8   = Span[8];
            this.byte9   = Span[9];
            this.byte10  = Span[10];
            this.byte11  = Span[11];
            this.byte12  = Span[12];
            this.byte13  = Span[13];
            this.byte14  = Span[14];
            this.byte15  = Span[15];

        }

        #endregion

        #region IPv6Address (Stream, InterfaceId = null)

        /// <summary>
        /// Reads a new IPv6Address from the given stream of bytes.
        /// </summary>
        /// <param name="Stream">The stream to read the IPv6 address from.</param>
        /// <param name="InterfaceId">An optional interface identification for the scope of the IPv6 address.</param>
        public IPv6Address(Stream   Stream,
                           String?  InterfaceId   = null)
        {

            this.InterfaceId = InterfaceId ?? "";

            if (!Stream.CanRead)
                throw new FormatException($"The given stream must be readable!");

            Span<Byte> buffer = stackalloc Byte[length];
            Stream.ReadExactly(buffer);

            this.byte0   = buffer[0];
            this.byte1   = buffer[1];
            this.byte2   = buffer[2];
            this.byte3   = buffer[3];
            this.byte4   = buffer[4];
            this.byte5   = buffer[5];
            this.byte6   = buffer[6];
            this.byte7   = buffer[7];
            this.byte8   = buffer[8];
            this.byte9   = buffer[9];
            this.byte10  = buffer[10];
            this.byte11  = buffer[11];
            this.byte12  = buffer[12];
            this.byte13  = buffer[13];
            this.byte14  = buffer[14];
            this.byte15  = buffer[15];

        }

        #endregion

        #endregion


        #region IPv6Address.Any       / ::0

        /// <summary>
        /// The IPv6.Any / ::0 address.
        /// </summary>
        public static IPv6Address Any

            => new ();

        #endregion

        #region IPv6Address.Localhost / ::1

        /// <summary>
        /// The IPv6 localhost / ::1
        /// </summary>
        public static IPv6Address Localhost

            => new ([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]);

        #endregion


        #region GetBytes ()

        public Byte[] GetBytes()

            => [ byte0,
                 byte1,
                 byte2,
                 byte3,
                 byte4,
                 byte5,
                 byte6,
                 byte7,
                 byte8,
                 byte9,
                 byte10,
                 byte11,
                 byte12,
                 byte13,
                 byte14,
                 byte15 ];

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given string as an IPv6 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv6 address.</param>
        public static IPv6Address Parse(String Text)
        {

            if (TryParse(Text, out var ipv6Address))
                return ipv6Address;

            throw new ArgumentException($"Invalid text representation of an IPv6 address: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region Parse    (Hostname)

        /// <summary>
        /// Parsed the given HTTP hostname as an IPv6 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        public static IPv6Address Parse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv6Address))
                return ipv6Address;

            throw new ArgumentException($"Invalid text representation of an IPv6 address: '{Hostname}'!",
                                        nameof(Hostname));

        }

        #endregion

        #region Parse    (DomainName)

        /// <summary>
        /// Parsed the given domain name as an IPv6 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static IPv6Address Parse(DomainName DomainName)
        {

            if (TryParse(DomainName, out var ipv6Address))
                return ipv6Address;

            throw new ArgumentException($"Invalid text representation of an IPv6 address: '{DomainName}'!",
                                        nameof(DomainName));

        }

        #endregion


        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as an IPv6 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv6 address.</param>
        public static IPv6Address? TryParse(String Text)
        {

            if (TryParse(Text, out var ipv6Address))
                return ipv6Address;

            return default;

        }

        #endregion

        #region TryParse (Hostname)

        /// <summary>
        /// Try to parse the given text as an IPv6 address.
        /// </summary>
        /// <param name="Hostname">A text representation of an IPv6 address.</param>
        public static IPv6Address? TryParse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv6Address))
                return ipv6Address;

            return default;

        }

        #endregion

        #region TryParse (DomainName)

        /// <summary>
        /// Try to parse the given domain name as an IPv6 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static IPv6Address? TryParse(DomainName DomainName)
        {

            if (TryParse(DomainName, out var ipv6Address))
                return ipv6Address;

            return default;

        }

        #endregion


        #region TryParse (Text,       out IPv4Address)

        /// <summary>
        /// Try to parse the given text as an IPv6 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv6 address.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(String Text, out IPv6Address IPv6Address)
        {
            IPv6Address = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            Text = Text.Trim().TrimStart('[').TrimEnd(']').Trim();

            var positionOfInterfaceId = Text.IndexOf('%');
            var interfaceId = "";

            if (positionOfInterfaceId > -1)
            {
                interfaceId = Text[(positionOfInterfaceId + 1)..];
                Text         = Text[..positionOfInterfaceId];
            }

            if (Text.IndexOf(':') < 0)
                return false;

            // ::-Kompression auflösen
            int colonCount   = Text.Count(static c => c == ':');
            int zeroGroups   = 8 - colonCount;

            if (zeroGroups < 0)
                return false;

            if (Text.Contains("::"))
            {
                if (zeroGroups == 0)
                    return false;

                var replacement = string.Concat(Enumerable.Repeat(":0000", zeroGroups)) + ":";
                Text = Text.Replace("::", replacement);
            }

            var elements = Text.Split(splitter, 8, StringSplitOptions.None);
            if (elements.Length != 8)
                return false;

            Span<Byte> ipv6AddressArray = stackalloc Byte[length];

            for (var i = 0; i < 8; i++)
            {
                var group = elements[i].PadLeft(4, '0');   // immer 4 Hex-Ziffern

                if (group.Length > 4 ||
                    !UInt16.TryParse(group, NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out var value))
                    return false;

                ipv6AddressArray[i * 2]     = (Byte)(value >> 8);
                ipv6AddressArray[i * 2 + 1] = (Byte)value;
            }

            IPv6Address = new IPv6Address(
                              ipv6AddressArray,
                              interfaceId
                          );

            return true;

        }

        #endregion

        #region TryParse (Hostname,   out IPv6Address)

        /// <summary>
        /// Try to parse the given HTTP hostname as an IPv6 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(HTTPHostname     Hostname,
                                       out IPv6Address  IPv6Address)

            => TryParse(
                   Hostname.Name,
                   out IPv6Address
               );

        #endregion

        #region TryParse (DomainName, out IPv6Address)

        /// <summary>
        /// Try to parse the given domain name as an IPv6 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(DomainName       DomainName,
                                       out IPv6Address  IPv6Address)

            => TryParse(
                   DomainName.FullName,
                   out IPv6Address
               );

        #endregion


        #region (implicit) operator IPAddress(IPv6Address)

        /// <summary>
        /// Convert this IPv6 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPv6Address">The IPv6 address.</param>
        public static implicit operator System.Net.IPAddress(IPv6Address IPv6Address)

            => new (IPv6Address.GetBytes());

        #endregion


        #region From     (IPAddress)

        /// <summary>
        /// Create a new IPv6 address from the given System.Net.IPAddress.
        /// </summary>
        public static IPv6Address From(System.Net.IPAddress IPAddress)

            => new (IPAddress.GetAddressBytes());

        #endregion

        #region FromIPv4 (IPv4Address)

        /// <summary>
        /// Create a new IPv6 address by mapping the given IPv4 address to an IPv6 address.
        /// </summary>
        public static IPv6Address FromIPv4(IPv4Address IPv4Address)
        {

            Span<Byte> bytes  = stackalloc Byte[length];
            var         ipv4  = IPv4Address.GetBytes();

            bytes[10] = 0xFF;
            bytes[11] = 0xFF;
            bytes[12] = ipv4[0];
            bytes[13] = ipv4[1];
            bytes[14] = ipv4[2];
            bytes[15] = ipv4[3];

            return new IPv6Address(bytes);

        }

        #endregion


        #region Operator overloading

        #region Operator == (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPv6Address IPv6Address1,
                                           IPv6Address IPv6Address2)

            => IPv6Address1.Equals(IPv6Address2);

        #endregion

        #region Operator != (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv6Address IPv6Address1,
                                           IPv6Address IPv6Address2)

            => !IPv6Address1.Equals(IPv6Address2);

        #endregion

        #region Operator <  (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPv6Address IPv6Address1,
                                          IPv6Address IPv6Address2)

            => IPv6Address1.CompareTo(IPv6Address2) < 0;

        #endregion

        #region Operator <= (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPv6Address IPv6Address1,
                                           IPv6Address IPv6Address2)

            => IPv6Address1.CompareTo(IPv6Address2) <= 0;

        #endregion

        #region Operator >  (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPv6Address IPv6Address1,
                                          IPv6Address IPv6Address2)

            => IPv6Address1.CompareTo(IPv6Address2) > 0;

        #endregion

        #region Operator >= (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPv6Address IPv6Address1,
                                           IPv6Address IPv6Address2)

            => IPv6Address1.CompareTo(IPv6Address2) >= 0;

        #endregion

        #endregion

        #region IComparable<IPAddress> Members

        #region IComparable Members

        /// <summary>
        /// Compares two IPv6 addresses.
        /// </summary>
        /// <param name="Object">An IPv6 address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPv6Address ipv6Address
                   ? CompareTo(ipv6Address)
                   : throw new ArgumentException("The given object is not an IPv6 address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPv6Address)

        /// <summary>
        /// Compares two IPv6 addresses.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        public Int32 CompareTo(IPv6Address IPv6Address)

            => AsSpan.SequenceCompareTo(IPv6Address.AsSpan);

        #endregion

        #region CompareTo(IIPAddress)

        /// <summary>
        /// Compares two IP addresses.
        /// </summary>
        /// <param name="IIPAddress">An IP address to compare with.</param>
        public Int32 CompareTo(IIPAddress? IIPAddress)
        {

            if (IIPAddress is null)
                return 1;

            if (Length != IIPAddress.Length)
                return Length.CompareTo(IIPAddress.Length);

            if (IIPAddress is IPv6Address ipv6Address)
                return CompareTo(ipv6Address);

            throw new ArgumentException("The given object is not an IPv6 address!", nameof(IIPAddress));

        }

        #endregion

        #endregion

        #region IEquatable<IPAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two IPv6 addresses.
        /// </summary>
        /// <param name="Object">An IPv6 address to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is IPv6Address ipv6Address &&
                   Equals(ipv6Address);

        #endregion

        #region Equals(IPv6Address)

        /// <summary>
        /// Compares two IPv6 addresses.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        public Boolean Equals(IPv6Address IPv6Address)

            => AsSpan.SequenceEqual(IPv6Address.AsSpan);

        #endregion

        #region Equals(IIPAddress)

        /// <summary>
        /// Compares two IP addresses.
        /// </summary>
        /// <param name="IIPAddress">An IP address to compare with.</param>
        public Boolean Equals(IIPAddress? IIPAddress)
        {

            if (IIPAddress is null)
                return false;

            if (length != IIPAddress.Length)
                return false;

            if (IIPAddress is IPv6Address ipv6Address)
                return Equals(ipv6Address);

            return false;

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Int32 GetHashCode()

            => HashCode.Combine(
                   MemoryMarshal.Read<UInt64>(AsSpan),
                   MemoryMarshal.Read<UInt64>(AsSpan[8..])
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {

            if (IsAny)
                return InterfaceId.IsNotNullOrEmpty() ? $"[::%{InterfaceId}]" : "[::]";

            if (IsLocalhost)
                return InterfaceId.IsNotNullOrEmpty() ? $"[::1%{InterfaceId}]" : "[::1]";

            return String.Format(
                       "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}{8}",
                       byte0. ToString("x2")  + byte1. ToString("x2"),
                       byte2. ToString("x2")  + byte3. ToString("x2"),
                       byte4. ToString("x2")  + byte5. ToString("x2"),
                       byte6. ToString("x2")  + byte7. ToString("x2"),
                       byte8. ToString("x2")  + byte9. ToString("x2"),
                       byte10.ToString("x2") + byte11.ToString("x2"),
                       byte12.ToString("x2") + byte13.ToString("x2"),
                       byte14.ToString("x2") + byte15.ToString("x2"),
                       InterfaceId.IsNotNullOrEmpty() ? "%" + InterfaceId : ""
                   );

        }

        #endregion

    }

}
