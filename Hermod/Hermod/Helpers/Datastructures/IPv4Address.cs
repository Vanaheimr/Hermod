/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv4 address.
    /// </summary>
    public readonly struct IPv4Address : IIPAddress,
                                         IComparable<IPv4Address>,
                                         IEquatable<IPv4Address>
    {

        #region Data

        private const            Byte    length = 4;

        private static readonly  Char[]  splitter = new Char[1] { '.' };

        private readonly         Byte[]  ipAddressArray;

        #endregion

        #region Properties

        /// <summary>
        /// The length of an IPv4 address.
        /// </summary>
        public Byte Length
            => length;

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// 224.0.0.0 - 239.255.255.255
        /// </summary>
        public Boolean IsMulticast

            => ipAddressArray[0] >= 224 && ipAddressArray[0] <= 239;

        public Boolean IsIPv4
            => true;

        public Boolean IsIPv6
            => false;

        public Boolean IsLocalhost

            => ipAddressArray[0] == 127 &&
               ipAddressArray[1] ==   0 &&
               ipAddressArray[2] ==   0 &&
               ipAddressArray[3] ==   1;

        public Boolean IsLocalNet

            => ipAddressArray[0] == 127;

        #endregion

        #region Constructor(s)

        #region IPv4Address(IPAddress)

        /// <summary>
        /// Create a new IPv4 address based on the given System.Net.IPAddress.
        /// </summary>
        public IPv4Address(System.Net.IPAddress IPAddress)
            : this(IPAddress.GetAddressBytes())
        { }

        #endregion

        #region IPv4Address(Int32)

        /// <summary>
        /// Create a new IPv4 address based on the given Int32 representation.
        /// </summary>
        public IPv4Address(Int32 Int32)
        {

            ipAddressArray = new Byte[] {
                                     (Byte) ( Int32        & 0xFF),
                                     (Byte) ((Int32 >>  8) & 0xFF),
                                     (Byte) ((Int32 >> 16) & 0xFF),
                                     (Byte) ( Int32 >> 24)
                                 };

        }

        #endregion

        #region IPv4Address(UInt32)

        /// <summary>
        /// Create a new IPv4 address based on the given UInt32 representation.
        /// </summary>
        public IPv4Address(UInt32 UInt32)
        {

            ipAddressArray = new Byte[] {
                                     (Byte) ( UInt32        & 0xFF),
                                     (Byte) ((UInt32 >>  8) & 0xFF),
                                     (Byte) ((UInt32 >> 16) & 0xFF),
                                     (Byte) ( UInt32 >> 24)
                                 };

        }

        #endregion

        #region IPv4Address(Byte1, Byte2, Byte3, Byte4)

        /// <summary>
        /// Create a new IPv4 address based on the given bytes.
        /// </summary>
        public IPv4Address(Byte Byte1, Byte Byte2, Byte Byte3, Byte Byte4)
        {

            ipAddressArray = new Byte[] {
                                 Byte1,
                                 Byte2,
                                 Byte3,
                                 Byte4
                             };

        }

        #endregion

        #region IPv4Address(ByteArray)

        /// <summary>
        /// Create a new IPv4 address based on the given byte array representation.
        /// </summary>
        public IPv4Address(Byte[] ByteArray)
        {

            if (ByteArray.Length != length)
                throw new FormatException("The given byte array length is invalid!");

            ipAddressArray = new Byte[length];

            Array.Copy(ByteArray, ipAddressArray, length);

        }

        #endregion

        #region IPv4Address(Stream)

        /// <summary>
        /// Reads a new IPv4Address from the given stream of bytes.
        /// </summary>
        public IPv4Address(Stream Stream)
        {

            if (!Stream.CanRead)
                throw new FormatException("The given stream is invalid!");

            ipAddressArray = new Byte[length];
            Stream.Read(ipAddressArray, 0, length);

        }

        #endregion

        #region IPv4Address(String)

        /// <summary>
        /// Create a new IPv4 address based on the given string representation.
        /// </summary>
        public IPv4Address(String IPv4AddressString)
        {

            var splitted = IPv4AddressString.Split(splitter, StringSplitOptions.None);

            if (splitted.Length != length)
                throw new ArgumentException("Invalid IPv4 address!");

            ipAddressArray = new Byte[length];

            for (var i=0; i<length; i++)
            {
                if (Byte.TryParse(splitted[i], out Byte byteValue))
                    ipAddressArray[i] = byteValue;
            }

        }

        #endregion

        #endregion


        #region IPv4Address.Any       / 0.0.0.0

        /// <summary>
        /// The IPv4.Any / 0.0.0.0 address.
        /// </summary>
        public static IPv4Address Any

            => new (
                   new Byte[length]
               );

        #endregion

        #region IPv4Address.Localhost / 127.0.0.1

        /// <summary>
        /// The IPv4 localhost / 127.0.0.1
        /// </summary>
        public static IPv4Address Localhost

            => new (
                   new Byte[] {
                       127, 0, 0, 1
                   }
               );

        #endregion

        #region IPv4Address.Broadcast / 255.255.255.255

        /// <summary>
        /// The IPv4 broadcast / 255.255.255.255
        /// </summary>
        public static IPv4Address Broadcast

            => new (
                   new Byte[] {
                       255, 255, 255, 255
                   }
               );

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
        /// Parse the given string as an IPv4 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        public static IPv4Address Parse(String Text)
        {

            if (TryParse(Text, out var ipv4Address))
                return ipv4Address;

            throw new ArgumentException("Invalid text representation of an IPv4 address: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region Parse   (Hostname)

        /// <summary>
        /// Parsed the given HTTP hostname as an IPv4 address.
        /// </summary>
        /// <param name="Hostname">A HTTP hostname.</param>
        public static IPv4Address Parse(HTTPHostname Hostname)
        {

            if (TryParse(Hostname, out var ipv4Address))
                return ipv4Address;

            throw new ArgumentException("Invalid text representation of an IPv4 address: '" + Hostname + "'!",
                                        nameof(Hostname));

        }

        #endregion

        #region TryParse(Text)

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

        #region TryParse(Hostname)

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

        #region TryParse(Text,     out IPv4Address)

        /// <summary>
        /// Try to parse the given text as an IPv4 address.
        /// </summary>
        /// <param name="Text">A text representation of an IPv4 address.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(String Text, out IPv4Address IPv4Address)
        {

            IPv4Address  = default;

            var elements = Text.Split(splitter, length, StringSplitOptions.None);

            if (elements.Length != length)
                return false;

            var ipv4AddressArray = new Byte[length];

            if (!Byte.TryParse(elements[0], out ipv4AddressArray[0]))
                return false;

            if (!Byte.TryParse(elements[1], out ipv4AddressArray[1]))
                return false;

            if (!Byte.TryParse(elements[2], out ipv4AddressArray[2]))
                return false;

            if (!Byte.TryParse(elements[3], out ipv4AddressArray[3]))
                return false;

            IPv4Address = new IPv4Address(ipv4AddressArray);

            return true;

        }

        #endregion

        #region TryParse(Hostname, out IPv4Address)

        /// <summary>
        /// Try to parse the given HTTP hostname as an IPv4 address.
        /// </summary>
        /// <param name="Hostname">A HTTP hostname.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(HTTPHostname Hostname, out IPv4Address IPv4Address)

            => TryParse(Hostname.Name, out IPv4Address);

        #endregion


        #region (implicit) operator IPAddress(IPv4Address)

        /// <summary>
        /// Convert this IPv4 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPv4Address">The IPv4 address.</param>
        public static implicit operator System.Net.IPAddress(IPv4Address IPv4Address)

            => new (IPv4Address.GetBytes());

        #endregion


        #region Operator overloading

        #region Operator == (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4Address.</param>
        /// <param name="IPv4Address2">Another IPv4Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => IPv4Address1.Equals(IPv4Address2);

        #endregion

        #region Operator != (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4Address.</param>
        /// <param name="IPv4Address2">Another IPv4Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv4Address IPv4Address1,
                                           IPv4Address IPv4Address2)

            => !IPv4Address1.Equals(IPv4Address2);

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
        {

            var byteArray = IPv4Address.GetBytes();

            for (var i = 0; i < byteArray.Length; i++)
            {

                var c = ipAddressArray[i].CompareTo(byteArray[i]);

                if (c != 0)
                    return c;

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
        /// <param name="IIPAddress">An IPv4 address to compare with.</param>
        public Boolean Equals(IPv4Address IPv4Address)
        {

            var byteArray = IPv4Address.GetBytes();

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

            if (IIPAddress is IPv4Address ipv4Address)
                return Equals(ipv4Address);

            return false;

        }

        #endregion

        #endregion

        #region GetHashCode()

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
        public override String ToString()

            => String.Format("{0}.{1}.{2}.{3}",
                             ipAddressArray[0],
                             ipAddressArray[1],
                             ipAddressArray[2],
                             ipAddressArray[3]);

        #endregion

    }

}
