/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.IO;

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

        private static readonly  Char[] Splitter = new Char[1] { '.' };

        private readonly         Byte[] IPAddressArray;

        #endregion

        #region Properties

        #region Length

        private const Byte _Length = 4;

        /// <summary>
        /// The length of an IPv4Address.
        /// </summary>
        public Byte Length
            => _Length;

        #endregion

        #region IsMulticast

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// 224.0.0.0 - 239.255.255.255
        /// </summary>
        public Boolean IsMulticast

            => IPAddressArray[0] >= 224 && IPAddressArray[0] <= 239;

        #endregion

        public Boolean IsIPv4
            => true;

        public Boolean IsIPv6
            => false;

        public Boolean IsLocalhost
            => IPAddressArray[0] == 127 &&
               IPAddressArray[1] ==   0 &&
               IPAddressArray[2] ==   0 &&
               IPAddressArray[3] ==   1;

        public Boolean IsLocalNet
            => IPAddressArray[0] == 127;

        #endregion

        #region Constructor(s)

        #region IPv4Address(IPAddress)

        /// <summary>
        /// Generates a new IPv4Address based on the given System.Net.IPAddress.
        /// </summary>
        public IPv4Address(System.Net.IPAddress IPAddress)
            : this(IPAddress.GetAddressBytes())
        { }

        #endregion

        #region IPv4Address(Int32)

        /// <summary>
        /// Generates a new IPv4Address based on the given Int32 representation.
        /// </summary>
        public IPv4Address(Int32 Int32)
        {

            IPAddressArray = new Byte[] {
                                     (Byte) ( Int32        & 0xFF),
                                     (Byte) ((Int32 >>  8) & 0xFF),
                                     (Byte) ((Int32 >> 16) & 0xFF),
                                     (Byte) ( Int32 >> 24)
                                 };

        }

        #endregion

        #region IPv4Address(UInt32)

        /// <summary>
        /// Generates a new IPv4Address based on the given UInt32 representation.
        /// </summary>
        public IPv4Address(UInt32 UInt32)
        {

            IPAddressArray = new Byte[] {
                                     (Byte) ( UInt32        & 0xFF),
                                     (Byte) ((UInt32 >>  8) & 0xFF),
                                     (Byte) ((UInt32 >> 16) & 0xFF),
                                     (Byte) ( UInt32 >> 24)
                                 };

        }

        #endregion

        #region IPv4Address(Byte1, Byte2, Byte3, Byte4)

        /// <summary>
        /// Generates a new IPv4Address based on the given bytes.
        /// </summary>
        public IPv4Address(Byte Byte1, Byte Byte2, Byte Byte3, Byte Byte4)
        {

            IPAddressArray = new Byte[] {
                                 Byte1,
                                 Byte2,
                                 Byte3,
                                 Byte4
                             };

        }

        #endregion

        #region IPv4Address(ByteArray)

        /// <summary>
        /// Generates a new IPv4Address based on the given byte array representation.
        /// </summary>
        public IPv4Address(Byte[] ByteArray)
        {

            if (ByteArray.Length != _Length)
                throw new FormatException("The given byte array length is invalid!");

            IPAddressArray = new Byte[_Length];

            Array.Copy(ByteArray, IPAddressArray, _Length);

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

            IPAddressArray = new Byte[_Length];
            Stream.Read(IPAddressArray, 0, _Length);

        }

        #endregion

        #region IPv4Address(String)

        /// <summary>
        /// Generates a new IPv4Address based on the given string representation.
        /// </summary>
        public IPv4Address(String IPv4AddressString)
        {

            var splitted = IPv4AddressString.Split(Splitter, StringSplitOptions.None);

            if (splitted.Length != _Length)
                throw new ArgumentException("Invalid IPv4 address!");

            IPAddressArray = new Byte[_Length];

            for (var i=0; i<_Length; i++)
            {
                if (Byte.TryParse(splitted[i], out Byte byteValue))
                    IPAddressArray[i] = byteValue;
            }

        }

        #endregion

        #endregion


        #region IPv4Address.Any / 0.0.0.0

        /// <summary>
        /// The IPv4.Any / 0.0.0.0 address.
        /// </summary>
        public static IPv4Address Any

            => new IPv4Address(
                   new Byte[_Length]
               );

        #endregion

        #region IPv4Address.Localhost / 127.0.0.1

        /// <summary>
        /// The IPv4 localhost / 127.0.0.1
        /// </summary>
        public static IPv4Address Localhost

            => new IPv4Address(
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

            => new IPv4Address(
                   new Byte[] {
                       255, 255, 255, 255
                   }
               );

        #endregion


        #region GetBytes()

        public Byte[] GetBytes()
        {

            var result = new Byte[_Length];

            Array.Copy(IPAddressArray,
                       result,
                       _Length);

            return result;

        }

        #endregion

        #region Parse   (IPv4AddressString)

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        public static IPv4Address Parse(String IPv4AddressString)
        {

            if (TryParse(IPv4AddressString, out IPv4Address IPAddress))
                return IPAddress;

            throw new ArgumentException("The given string '" + IPv4AddressString + "' is not a valid IPv4Address!", nameof(IPv4AddressString));

        }

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        public static IPv4Address Parse(HTTPHostname IPv4AddressString)
        {

            if (TryParse(IPv4AddressString, out IPv4Address IPAddress))
                return IPAddress;

            throw new ArgumentException("The given string '" + IPv4AddressString + "' is not a valid IPv4Address!", nameof(IPv4AddressString));

        }

        #endregion

        #region TryParse(IPv4AddressString)

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        public static IPv4Address? TryParse(String IPv4AddressString)
        {

            if (TryParse(IPv4AddressString, out IPv4Address IPAddress))
                return IPAddress;

            return default;

        }

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        public static IPv4Address? TryParse(HTTPHostname IPv4AddressString)
        {

            if (TryParse(IPv4AddressString, out IPv4Address IPAddress))
                return IPAddress;

            return default;

        }

        #endregion

        #region TryParse(IPv4AddressString, out IPv4Address)

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(String IPv4AddressString, out IPv4Address IPv4Address)
        {

            IPv4Address  = default;

            var Elements = IPv4AddressString.Split(Splitter, _Length, StringSplitOptions.None);

            if (Elements.Length != _Length)
                return false;

            var ipv4AddressArray = new Byte[_Length];

            if (!Byte.TryParse(Elements[0], out ipv4AddressArray[0]))
                return false;

            if (!Byte.TryParse(Elements[1], out ipv4AddressArray[1]))
                return false;

            if (!Byte.TryParse(Elements[2], out ipv4AddressArray[2]))
                return false;

            if (!Byte.TryParse(Elements[3], out ipv4AddressArray[3]))
                return false;

            IPv4Address = new IPv4Address(ipv4AddressArray);

            return true;

        }

        /// <summary>
        /// Parsed the given string representation into a new IPv4Address.
        /// </summary>
        /// <param name="IPv4AddressString">An IPv4Address string representation.</param>
        /// <param name="IPv4Address">The parsed IPv4 address.</param>
        public static Boolean TryParse(HTTPHostname IPv4AddressString, out IPv4Address IPv4Address)

            => TryParse(IPv4AddressString.Name, out IPv4Address);

        #endregion


        #region (implicit) operator IPAddress(IPv4Address)

        /// <summary>
        /// Convert this IPv4 address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPv4Address">The IPv4 address.</param>
        public static implicit operator System.Net.IPAddress(IPv4Address IPv4Address)

            => new System.Net.IPAddress(IPv4Address.GetBytes());

        #endregion


        #region Operator overloading

        #region Operator == (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4Address.</param>
        /// <param name="IPv4Address2">Another IPv4Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPv4Address IPv4Address1, IPv4Address IPv4Address2)
            => IPv4Address1.Equals(IPv4Address2);

        #endregion

        #region Operator != (IPv4Address1, IPv4Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address1">A IPv4Address.</param>
        /// <param name="IPv4Address2">Another IPv4Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv4Address IPv4Address1, IPv4Address IPv4Address2)
            => !IPv4Address1.Equals(IPv4Address2);

        #endregion

        #endregion

        #region IComparable Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is IPv4Address ipAddress
                   ? CompareTo(ipAddress)
                   : throw new ArgumentException("The given object is not an IPv4 address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPv4Address)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPv4Address IPv4Address)
        {

            var byteArray = IPv4Address.GetBytes();

            for (var i = 0; i < byteArray.Length; i++)
            {

                var comparision = IPAddressArray[i].CompareTo(byteArray[i]);

                if (comparision != 0)
                    return comparision;

            }

            return 0;

        }

        #endregion

        #region CompareTo(IIPAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IIPAddress">An ip address to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IIPAddress IIPAddress)
        {

            if (IIPAddress is IPv4Address ipv4Address)
                return CompareTo(ipv4Address);

            throw new ArgumentException("The given object is not an IPv4 address!", nameof(IIPAddress));

        }

        #endregion

        #endregion

        #region IEquatable Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is IPv4Address ipAddress &&
                   Equals(ipAddress);

        #endregion

        #region Equals(IPv4Address)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPv4Address IPv4Address)
        {

            var byteArray = IPv4Address.GetBytes();

            for (var i = 0; i < byteArray.Length; i++)
            {
                if (IPAddressArray[i] != byteArray[i])
                    return false;
            }

            return true;

        }

        #endregion

        #region Equals(IIPAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IIPAddress">An IIPAddress.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IIPAddress IIPAddress)
        {

            if (IIPAddress is null)
                throw new ArgumentNullException(nameof(IIPAddress), "The given IIPAddress must not be null!");

            if (_Length != IIPAddress.Length)
                return false;

            if (IIPAddress is IPv4Address ipv4address)
                return Equals(ipv4address);

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
                             IPAddressArray[0],
                             IPAddressArray[1],
                             IPAddressArray[2],
                             IPAddressArray[3]);

        #endregion

    }

}
