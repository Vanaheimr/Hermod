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

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv4 address.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct IPv4Address : IComparable<IPv4Address>,
                                         IEquatable<IPv4Address>,
                                         IIPAddress
    {

        #region Data

        private const            Byte    length    = 4;
        private static readonly  Char[]  splitter  = ['.'];

        private readonly         Byte    byte0;
        private readonly         Byte    byte1;
        private readonly         Byte    byte2;
        private readonly         Byte    byte3;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the IPv4 address as ReadOnlySpan&lt;byte&gt;.
        /// </summary>
        public ReadOnlySpan<Byte>  AsSpan
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in byte0), length);

        /// <summary>
        /// The length of an IPv4 address.
        /// </summary>
        public Byte     Length
            => length;

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// 224.0.0.0 - 239.255.255.255
        /// </summary>
        public Boolean  IsMulticast

            => byte0 >= 224 &&
               byte0 <= 239;

        public Boolean  IsIPv4
            => true;

        Boolean  IIPAddress.IsMappedIPv4
            => false;

        public Boolean  IsIPv6
            => false;

        public Boolean  IsLocalhost

            => byte0 == 127 &&
               byte1 ==   0 &&
               byte2 ==   0 &&
               byte3 ==   1;

        public Boolean  IsAny

            => byte0 == 0 &&
               byte1 == 0 &&
               byte2 == 0 &&
               byte3 == 0;

        public Boolean  IsLoopback

            => byte0 == 127;


        IPv4Address? IIPAddress.AsIPv4
            => this;

        #endregion

        #region Constructor(s)

        #region IPv4Address (Span)

        /// <summary>
        /// Create a new IPv4 address based on the given span of bytes representation.
        /// </summary>
        /// <param name="Span">A span of bytes containing the IPv4 address.</param>
        public IPv4Address(ReadOnlySpan<Byte> Span)
        {

            if (Span.Length != length)
                throw new FormatException($"The given span of bytes must have a length of {length}!");

            this.byte0 = Span[0];
            this.byte1 = Span[1];
            this.byte2 = Span[2];
            this.byte3 = Span[3];

        }

        #endregion

        #region IPv4Address (Stream)

        /// <summary>
        /// Reads a new IPv4Address from the given stream of bytes.
        /// </summary>
        /// <param name="Stream">A stream of bytes containing the IPv4 address.</param>
        public IPv4Address(Stream Stream)
        {

            if (!Stream.CanRead)
                throw new FormatException($"The given stream must be readable!");

            Span<Byte> buffer = stackalloc Byte[length];
            Stream.ReadExactly(buffer);

            this.byte0 = buffer[0];
            this.byte1 = buffer[1];
            this.byte2 = buffer[2];
            this.byte3 = buffer[3];

        }

        #endregion

        #region IPv4Address (Byte1, Byte2, Byte3, Byte4)

        /// <summary>
        /// Create a new IPv4 address based on the given bytes.
        /// </summary>
        /// <param name="Byte1">The first byte of the IPv4 address.</param>
        /// <param name="Byte2">The second byte of the IPv4 address.</param>
        /// <param name="Byte3">The third byte of the IPv4 address.</param>
        /// <param name="Byte4">The fourth byte of the IPv4 address.</param>
        public IPv4Address(Byte Byte1,
                           Byte Byte2,
                           Byte Byte3,
                           Byte Byte4)
        {

            this.byte0 = Byte1;
            this.byte1 = Byte2;
            this.byte2 = Byte3;
            this.byte3 = Byte4;

        }

        #endregion

        #endregion


        #region IPv4Address.Any       / 0.0.0.0

        /// <summary>
        /// The IPv4.Any / 0.0.0.0 address.
        /// </summary>
        public static IPv4Address Any

            => new();

        #endregion

        #region IPv4Address.Localhost / 127.0.0.1

        /// <summary>
        /// The IPv4 localhost / 127.0.0.1
        /// </summary>
        public static IPv4Address Localhost

            => new ([ 127, 0, 0, 1 ]);

        #endregion

        #region IPv4Address.Broadcast / 255.255.255.255

        /// <summary>
        /// The IPv4 broadcast / 255.255.255.255
        /// </summary>
        public static IPv4Address Broadcast

            => new ([ 255, 255, 255, 255 ]);

        #endregion


        #region GetBytes ()

        public Byte[] GetBytes()

            => [byte0, byte1, byte2, byte3];

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given string as an IPv4 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        public static IPv4Address Parse(String Text)
        {

            if (TryParse(Text, out var ipv4Address))
                return ipv4Address;

            throw new ArgumentException($"Invalid text representation of an IPv4 address: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region Parse    (Hostname)

        /// <summary>
        /// Parsed the given HTTP hostname as an IPv4 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        public static IPv4Address Parse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv4Address))
                return ipv4Address;

            throw new ArgumentException($"Invalid text representation of an IPv4 address: '{Hostname}'!",
                                        nameof(Hostname));

        }

        #endregion

        #region Parse    (DomainName)

        /// <summary>
        /// Parsed the given domain name as an IPv4 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static IPv4Address Parse(DomainName DomainName)
        {

            if (TryParse(DomainName, out var ipv4Address))
                return ipv4Address;

            throw new ArgumentException($"Invalid text representation of an IPv4 address: '{DomainName}'!",
                                        nameof(DomainName));

        }

        #endregion


        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as an IPv4 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        public static IPv4Address? TryParse(String Text)
        {

            if (TryParse(Text, out var ipv4Address))
                return ipv4Address;

            return default;

        }

        #endregion

        #region TryParse (Hostname)

        /// <summary>
        /// Try to parse the given text as an IPv4 address.
        /// </summary>
        /// <param name="Hostname">A text representation of an IPv4 address.</param>
        public static IPv4Address? TryParse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv4Address))
                return ipv4Address;

            return default;

        }

        #endregion

        #region TryParse (DomainName)

        /// <summary>
        /// Try to parse the given domain name as an IPv4 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        public static IPv4Address? TryParse(DomainName DomainName)
        {

            if (TryParse(DomainName.FullName, out var ipv4Address))
                return ipv4Address;

            return default;

        }

        #endregion


        #region TryParse (Text,       out IPv4Address)

        /// <summary>
        /// Try to parse the given text as an IPv4 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(String           Text,
                                       out IPv4Address  IPv4Address)
        {

            IPv4Address  = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            var elements = Text.Split(splitter, length, StringSplitOptions.None);

            if (elements.Length != length)
                return false;

            Span<Byte> bytes = stackalloc Byte[4];

            if (!Byte.TryParse(elements[0], out bytes[0]) ||
                !Byte.TryParse(elements[1], out bytes[1]) ||
                !Byte.TryParse(elements[2], out bytes[2]) ||
                !Byte.TryParse(elements[3], out bytes[3]))
            {
                return false;
            }

            IPv4Address = new IPv4Address(bytes);

            return true;

        }

        #endregion

        #region TryParse (Hostname,   out IPv4Address)

        /// <summary>
        /// Try to parse the given HTTP hostname as an IPv4 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(HTTPHostname     Hostname,
                                       out IPv4Address  IPv4Address)

            => TryParse(
                   Hostname.Name,
                   out IPv4Address
               );

        #endregion

        #region TryParse (DomainName, out IPv4Address)

        /// <summary>
        /// Try to parse the given domain name as an IPv4 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(DomainName       DomainName,
                                       out IPv4Address  IPv4Address)

            => TryParse(
                   DomainName.FullName,
                   out IPv4Address
               );

        #endregion


        #region (implicit) operator IPAddress(IPv4Address)

        /// <summary>
        /// Convert this IPv4 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPv4Address">The IPv4 address.</param>
        public static implicit operator System.Net.IPAddress(IPv4Address IPv4Address)

            => new (IPv4Address.GetBytes());

        #endregion


        #region From     (Int32)

        /// <summary>
        /// Create a new IPv4 address based on the given Int32 representation.
        /// </summary>
        public static IPv4Address From(Int32 Int32)

            => new (
                       (Byte) ( Int32        & 0xFF),
                       (Byte) ((Int32 >>  8) & 0xFF),
                       (Byte) ((Int32 >> 16) & 0xFF),
                       (Byte) ( Int32 >> 24)
                   );

        #endregion

        #region From     (UInt32)

        /// <summary>
        /// Create a new IPv4 address based on the given UInt32 representation.
        /// </summary>
        public static IPv4Address From(UInt32 UInt32)

            => new (
                       (Byte) ( UInt32        & 0xFF),
                       (Byte) ((UInt32 >>  8) & 0xFF),
                       (Byte) ((UInt32 >> 16) & 0xFF),
                       (Byte) ( UInt32 >> 24)
                   );

        #endregion

        #region From     (IPAddress)

        /// <summary>
        /// Create a new IPv4 address from the given System.Net.IPAddress.
        /// </summary>
        public static IPv4Address From(System.Net.IPAddress IPAddress)

            => new (IPAddress.GetAddressBytes());

        #endregion


        #region Operator overloading

        #region Operator == (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => IPv4Address1.Equals(IPv4Address2);

        #endregion

        #region Operator != (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => !IPv4Address1.Equals(IPv4Address2);

        #endregion

        #region Operator <  (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPv4Address IPv4Address1,
                                          IPv4Address IPv4Address2)

            => IPv4Address1.CompareTo(IPv4Address2) < 0;

        #endregion

        #region Operator <= (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => IPv4Address1.CompareTo(IPv4Address2) <= 0;

        #endregion

        #region Operator >  (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPv4Address IPv4Address1,
                                          IPv4Address IPv4Address2)

            => IPv4Address1.CompareTo(IPv4Address2) > 0;

        #endregion

        #region Operator >= (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4 address.</param>
        /// <param name="IPv4Address2">Another IPv4 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => IPv4Address1.CompareTo(IPv4Address2) >= 0;

        #endregion

        #endregion

        #region IComparable<IPv4Address> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two IPv4 addresses.
        /// </summary>
        /// <param name="Object">An IPv4 address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPv4Address ipv4Address
                   ? CompareTo(ipv4Address)
                   : throw new ArgumentException("The given object is not an IPv4 address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPv4Address)

        /// <summary>
        /// Compares two IPv4 addresses.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address to compare with.</param>
        public Int32 CompareTo(IPv4Address IPv4Address)

            => AsSpan.SequenceCompareTo(IPv4Address.AsSpan);

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

            if (IIPAddress is IPv4Address ipv4Address)
                return CompareTo(ipv4Address);

            throw new ArgumentException("The given object is not an IPv4 address!", nameof(IIPAddress));

        }

        #endregion

        #endregion

        #region IEquatable<IPv4Address> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two IPv4 addresses.
        /// </summary>
        /// <param name="Object">An IPv4 address to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is IPv4Address ipv4Address &&
                   Equals(ipv4Address);

        #endregion

        #region Equals(IPv4Address)

        /// <summary>
        /// Compares two IPv4 addresses.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address to compare with.</param>
        public Boolean Equals(IPv4Address IPv4Address)

            => byte0 == IPv4Address.byte0 &&
               byte1 == IPv4Address.byte1 &&
               byte2 == IPv4Address.byte2 &&
               byte3 == IPv4Address.byte3;

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

            if (Length != IIPAddress.Length)
                return false;

            if (IIPAddress is IPv4Address ipv4Address)
                return Equals(ipv4Address);

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

            => (byte0 << 24) |
               (byte1 << 16) |
               (byte2 <<  8) |
                byte3;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{byte0}.{byte1}.{byte2}.{byte3}";

        #endregion

    }

}
