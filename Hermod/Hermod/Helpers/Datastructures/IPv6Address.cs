/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv6 address.
    /// </summary>
    public readonly struct IPv6Address : IIPAddress,
                                         IComparable<IPv6Address>,
                                         IEquatable<IPv6Address>
    {

        #region Data

        private const            Byte    length = 16;

        private static readonly  Char[]  splitter = [':'];

        private readonly         Byte[]  ipAddressArray;

        #endregion

        #region Properties

        /// <summary>
        /// The length of an IPv6 address.
        /// </summary>
        public Byte     Length
            => length;

        /// <summary>
        /// Whether the IP address is an IPv6 multicast address.
        /// </summary>
        public Boolean  IsMulticast
            => new System.Net.IPAddress(ipAddressArray).IsIPv6Multicast;

        /// <summary>
        /// The interface identification for local IPv6 addresses, e.g. .
        /// </summary>
        public String   InterfaceId { get; }

        public Boolean  IsIPv4
            => false;

        public Boolean  IsIPv6
            => true;

        public Boolean  IsLocalhost
            => ipAddressArray[ 0] == 0 &&
               ipAddressArray[ 1] == 0 &&
               ipAddressArray[ 2] == 0 &&
               ipAddressArray[ 3] == 0 &&
               ipAddressArray[ 4] == 0 &&
               ipAddressArray[ 5] == 0 &&
               ipAddressArray[ 6] == 0 &&
               ipAddressArray[ 7] == 0 &&
               ipAddressArray[ 8] == 0 &&
               ipAddressArray[ 9] == 0 &&
               ipAddressArray[10] == 0 &&
               ipAddressArray[11] == 0 &&
               ipAddressArray[12] == 0 &&
               ipAddressArray[13] == 0 &&
               ipAddressArray[14] == 0 &&
               ipAddressArray[15] == 1;

        public Boolean  IsAny
            => ipAddressArray.All(b => b == 0);

        #endregion

        #region Constructor(s)

        #region IPv6Address(IPv6Address)

        /// <summary>
        /// Create a new IPv6 address.
        /// </summary>
        public IPv6Address(System.Net.IPAddress IPv6Address)
            : this(IPv6Address.GetAddressBytes())
        { }

        #endregion

        #region IPv6Address(ByteArray, InterfaceId = null)

        /// <summary>
        /// Create a new IPv6 address.
        /// </summary>
        /// <param name="ByteArray">The IPv6 as byte array.</param>
        /// <param name="InterfaceId">An optional interface identification for the scope of the IPv6 address.</param>
        public IPv6Address(Byte[]   ByteArray,
                           String?  InterfaceId   = null)
        {

            this.InterfaceId     = InterfaceId ?? "";
            this.ipAddressArray  = new Byte[length];

            Array.Copy(ByteArray,
                       ipAddressArray,
                       Math.Max(ByteArray.Length, length));

        }

        #endregion

        #region IPv6Address(Stream)

        /// <summary>
        /// Reads a new IPv6Address from the given stream of bytes.
        /// </summary>
        public IPv6Address(Stream Stream)
        {

            if (!Stream.CanRead)
                throw new FormatException("The given stream is invalid!");

            ipAddressArray = new Byte[length];
            Stream.Read(ipAddressArray, 0, length);
            this.InterfaceId = "";

        }

        #endregion

        #endregion


        #region IPv6Address.Any       / ::0

        /// <summary>
        /// The IPv6.Any / ::0 address.
        /// </summary>
        public static IPv6Address Any

            => new (
                   new Byte[length]
               );

        #endregion

        #region IPv6Address.Localhost / ::1

        /// <summary>
        /// The IPv6 localhost / ::1
        /// </summary>
        public static IPv6Address Localhost
        {
            get
            {

                var byteArray = new Byte[length];
                byteArray[^1] = 1;

                return new IPv6Address(byteArray);

            }
        }

        #endregion


        #region GetBytes()

        public Byte[] GetBytes()
        {

            var result = new Byte[length];

            Array.Copy(ipAddressArray,
                       result,
                       length);

            return result;

        }

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an IPv6 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv6 address.</param>
        public static IPv6Address Parse(String Text)
        {

            if (TryParse(Text, out var ipv6Address))
                return ipv6Address;

            throw new ArgumentException($"Invalid text representation of an IPv6 address: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region Parse   (Hostname)

        /// <summary>
        /// Parsed the given HTTP hostname as an IPv6 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        public static IPv6Address Parse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv6Address))
                return ipv6Address;

            throw new ArgumentException($"Invalid text representation of an IPv6 address: '" + Hostname + "'!",
                                        nameof(Hostname));

        }

        #endregion

        #region TryParse(Text)

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

        #region TryParse(Hostname)

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

        #region TryParse(Text,       out IPv4Address)

        /// <summary>
        /// Try to parse the given text as an IPv6 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(String Text, out IPv6Address IPv6Address)
        {

            // 2001:0db8:85a3:08d3:1319:8a2e:0370:7344
            // fd00::9ec7:a6ff:feb7:c6 => fd00:0000:0000:0000:9ec7:a6ff:feb7:00c6
            IPv6Address = default;

            Text = Text.Trim().TrimStart('[').TrimEnd(']').Trim();

            if (Text.IndexOf(':') < 0)
                return false;

            var positionOfInterfaceId  = Text.IndexOf('%');
            var interfaceId            = "";

            if (positionOfInterfaceId > -1)
            {
                interfaceId  = Text[(positionOfInterfaceId + 1)..];
                Text         = Text[..positionOfInterfaceId];
            }

            var elements = Text.Replace("::", Enumerable.Repeat(":0000", 8 - Text.
                                                         Where(c => c == ':').
                                                         Count()).
                                              Aggregate((a, b) => a + b) + ":").
                                Split  (splitter, 7+1, StringSplitOptions.None).
                                Select (el => new String(Enumerable.Repeat('0', 4 - el.Length).ToArray()) + el).
                                ToArray();

            if (elements.Length != 8)
                return false;

            var ipv6AddressArray = new Byte[length];

            if (!Byte.TryParse(elements[0].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[0]))
                return false;

            if (!Byte.TryParse(elements[0].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[1]))
                return false;

            if (!Byte.TryParse(elements[1].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[2]))
                return false;

            if (!Byte.TryParse(elements[1].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[3]))
                return false;

            if (!Byte.TryParse(elements[2].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[4]))
                return false;

            if (!Byte.TryParse(elements[2].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[5]))
                return false;

            if (!Byte.TryParse(elements[3].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[6]))
                return false;

            if (!Byte.TryParse(elements[3].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[7]))
                return false;


            if (!Byte.TryParse(elements[4].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[8]))
                return false;

            if (!Byte.TryParse(elements[4].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[9]))
                return false;

            if (!Byte.TryParse(elements[5].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[10]))
                return false;

            if (!Byte.TryParse(elements[5].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[11]))
                return false;

            if (!Byte.TryParse(elements[6].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[12]))
                return false;

            if (!Byte.TryParse(elements[6].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[13]))
                return false;

            if (!Byte.TryParse(elements[7].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[14]))
                return false;

            if (!Byte.TryParse(elements[7].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[15]))
                return false;

            IPv6Address = new IPv6Address(ipv6AddressArray, interfaceId);

            return true;

        }

        #endregion

        #region TryParse(Hostname,   out IPv6Address)

        /// <summary>
        /// Try to parse the given HTTP hostname as an IPv6 address.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(HTTPHostname Hostname, out IPv6Address IPv6Address)

            => TryParse(Hostname.Name.FullName, out IPv6Address);

        #endregion

        #region TryParse(DomainName, out IPv6Address)

        /// <summary>
        /// Try to parse the given domain name as an IPv6 address.
        /// </summary>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(DomainName DomainName, out IPv6Address IPv6Address)

            => TryParse(DomainName.FullName, out IPv6Address);

        #endregion


        #region (implicit) operator IPAddress(IPv6Address)

        /// <summary>
        /// Convert this IPv6 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPv6Address">The IPv6 address.</param>
        public static implicit operator System.Net.IPAddress(IPv6Address IPv6Address)

            => new (IPv6Address.GetBytes());

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
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPv6Address IPv6Address)
        {

            var byteArray = IPv6Address.GetBytes();

            for (var i = 0; i < byteArray.Length; i++)
            {

                var comparision = ipAddressArray[i].CompareTo(byteArray[i]);

                if (comparision != 0)
                    return comparision;

            }

            return 0;

        }

        #endregion

        #region CompareTo(IIPAddress)

        /// <summary>
        /// Compares two IP addresses.
        /// </summary>
        /// <param name="IIPAddress">An IP address to compare with.</param>
        public Int32 CompareTo(IIPAddress? IIPAddress)
        {

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
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPv6Address IPv6Address)
        {

            var byteArray = IPv6Address.GetBytes();

            for (var i = 0; i < byteArray.Length; i++)
            {
                if (ipAddressArray[i] != byteArray[i])
                    return false;
            }

            return true;

        }

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
        public override Int32 GetHashCode()

            => ToString().GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => IsAny
                   ? "[::]"
                   : IsLocalhost
                         ? "[::1]"
                         : String.Format(
                               "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}{8}",
                               ipAddressArray[ 0].ToString("x2") + ipAddressArray[ 1].ToString("x2"),
                               ipAddressArray[ 2].ToString("x2") + ipAddressArray[ 3].ToString("x2"),
                               ipAddressArray[ 4].ToString("x2") + ipAddressArray[ 5].ToString("x2"),
                               ipAddressArray[ 6].ToString("x2") + ipAddressArray[ 7].ToString("x2"),
                               ipAddressArray[ 8].ToString("x2") + ipAddressArray[ 9].ToString("x2"),
                               ipAddressArray[10].ToString("x2") + ipAddressArray[11].ToString("x2"),
                               ipAddressArray[12].ToString("x2") + ipAddressArray[13].ToString("x2"),
                               ipAddressArray[14].ToString("x2") + ipAddressArray[15].ToString("x2"),
                               InterfaceId.IsNotNullOrEmpty() ? "%" + InterfaceId : String.Empty
                           );

        #endregion

    }

}
