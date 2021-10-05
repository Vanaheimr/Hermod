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
using System.Linq;
using System.Globalization;

using org.GraphDefined.Vanaheimr.Illias;

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

        private static readonly  Char[] Splitter        = new Char[1] { ':' };

        private readonly         Byte[] IPAddressArray;

        #endregion

        #region Properties

        #region Length

        private const Byte _Length = 16;

        /// <summary>
        /// The length of an IPv6Address.
        /// </summary>
        public Byte Length
        {
            get
            {
                return _Length;
            }
        }

        #endregion

        #region IsMulticast

        /// <summary>
        /// Whether the IP address is an IPv6 multicast address.
        /// </summary>
        public Boolean IsMulticast
        {
            get
            {
                return new System.Net.IPAddress(IPAddressArray).IsIPv6Multicast;
            }
        }

        #endregion

        #region InterfaceId

        /// <summary>
        /// The interface identification for local IPv6 addresses, e.g. .
        /// </summary>
        public String InterfaceId { get; }

        #endregion

        public Boolean IsIPv4
            => false;

        public Boolean IsIPv6
            => true;

        public Boolean IsLocalhost
            => ToString() == "::1";

        #endregion

        #region Constructor(s)

        #region IPv6Address(IPv6Address)

        /// <summary>
        /// Generates a new IPv6Address.
        /// </summary>
        public IPv6Address(System.Net.IPAddress IPv6Address)
            : this(IPv6Address.GetAddressBytes())
        { }

        #endregion

        #region IPv6Address(ByteArray, InterfaceId = null)

        /// <summary>
        /// Generates a new IPv6Address.
        /// </summary>
        /// <param name="ByteArray">The IPv6 as byte array.</param>
        /// <param name="InterfaceId">An optional interface identification for the scope of the IPv6 address.</param>
        public IPv6Address(Byte[]  ByteArray,
                           String  InterfaceId = null)
        {

            IPAddressArray = new Byte[_Length];

            Array.Copy(ByteArray, IPAddressArray, Math.Max(ByteArray.Length, _Length));

            this.InterfaceId = (InterfaceId != null) ? InterfaceId : "";

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

            IPAddressArray = new Byte[_Length];
            Stream.Read(IPAddressArray, 0, _Length);
            this.InterfaceId = "";

        }

        #endregion

        #endregion


        #region IPv6Address.Any / ::0

        /// <summary>
        /// The IPv6.Any / ::0 address.
        /// </summary>
        public static IPv6Address Any
        {
            get
            {
                return new IPv6Address(new Byte[_Length]);
            }
        }

        #endregion

        #region IPv6Address.Localhost / ::1

        /// <summary>
        /// The IPv6 localhost / ::1
        /// </summary>
        public static IPv6Address Localhost
        {
            get
            {

                var _ByteArray = new Byte[_Length];
                _ByteArray[_ByteArray.Length - 1] = 1;

                return new IPv6Address(_ByteArray);

            }
        }

        #endregion


        #region GetBytes()

        public Byte[] GetBytes()
        {
            return new Byte[_Length];
        }

        #endregion

        #region Parse   (IPv6AddressString)

        /// <summary>
        /// Parsed the given string representation into a new IPv6Address.
        /// </summary>
        /// <param name="IPv6AddressString">An IPv6Address string representation.</param>
        public static IPv6Address Parse(String IPv6AddressString)
        {

            if (TryParse(IPv6AddressString, out IPv6Address IPAddress))
                return IPAddress;

            throw new ArgumentException("The given string '" + IPv6AddressString + "' is not a valid IPv6Address!", nameof(IPv6AddressString));

        }

        #endregion

        #region TryParse(IPv6AddressString)

        /// <summary>
        /// Parsed the given string representation into a new IPv6Address.
        /// </summary>
        /// <param name="IPv6AddressString">An IPv6Address string representation.</param>
        public static IPv6Address? TryParse(String IPv6AddressString)
        {

            if (TryParse(IPv6AddressString, out IPv6Address IPAddress))
                return IPAddress;

            return default;

        }

        #endregion

        #region TryParse(IPv6AddressString, out IPv6Address)

        /// <summary>
        /// Parsed the given string representation into a new IPv6Address.
        /// </summary>
        /// <param name="IPv6AddressString">An IPv6Address string representation.</param>
        /// <param name="IPv6Address">The parsed IPv6 address.</param>
        public static Boolean TryParse(String IPv6AddressString, out IPv6Address IPv6Address)
        {

            // 2001:0db8:85a3:08d3:1319:8a2e:0370:7344
            // fd00::9ec7:a6ff:feb7:c6 => fd00:0000:0000:0000:9ec7:a6ff:feb7:00c6
            IPv6Address = default(IPv6Address);

            if (IPv6AddressString.IndexOf(':') < 0)
                return false;

            var PositionOfInterfaceId  = IPv6AddressString.IndexOf('%');
            var InterfaceId            = "";

            if (PositionOfInterfaceId > -1)
            {
                InterfaceId        = IPv6AddressString.Substring(PositionOfInterfaceId + 1);
                IPv6AddressString  = IPv6AddressString.Substring(0, PositionOfInterfaceId);
            }

            var Elements = IPv6AddressString.Replace("::", Enumerable.Repeat(":0000", 8 - IPv6AddressString.
                                                                      Where(c => c == ':').
                                                                      Count()).
                                                           Aggregate((a, b) => a + b) + ":").
                                             Split(Splitter, 7+1, StringSplitOptions.None).
                                             Select(el => new String(Enumerable.Repeat('0', 4 - el.Length).ToArray()) + el).
                                             ToArray();

            if (Elements.Length != 8)
                return false;

            var ipv6AddressArray = new Byte[_Length];

            if (!Byte.TryParse(Elements[0].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[0]))
                return false;

            if (!Byte.TryParse(Elements[0].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[1]))
                return false;

            if (!Byte.TryParse(Elements[1].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[2]))
                return false;

            if (!Byte.TryParse(Elements[1].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[3]))
                return false;

            if (!Byte.TryParse(Elements[2].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[4]))
                return false;

            if (!Byte.TryParse(Elements[2].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[5]))
                return false;

            if (!Byte.TryParse(Elements[3].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[6]))
                return false;

            if (!Byte.TryParse(Elements[3].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[7]))
                return false;


            if (!Byte.TryParse(Elements[4].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[8]))
                return false;

            if (!Byte.TryParse(Elements[4].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[9]))
                return false;

            if (!Byte.TryParse(Elements[5].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[10]))
                return false;

            if (!Byte.TryParse(Elements[5].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[11]))
                return false;

            if (!Byte.TryParse(Elements[6].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[12]))
                return false;

            if (!Byte.TryParse(Elements[6].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[13]))
                return false;

            if (!Byte.TryParse(Elements[7].Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[14]))
                return false;

            if (!Byte.TryParse(Elements[7].Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out ipv6AddressArray[15]))
                return false;

            IPv6Address = new IPv6Address(ipv6AddressArray, InterfaceId);

            return true;

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
        public static Boolean operator == (IPv6Address IPv6Address1, IPv6Address IPv6Address2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(IPv6Address1, IPv6Address2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) IPv6Address1 == null) || ((Object) IPv6Address2 == null))
                return false;

            return IPv6Address1.Equals(IPv6Address2);

        }

        #endregion

        #region Operator != (IPv6Address1, IPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address1">A IPv6 address.</param>
        /// <param name="IPv6Address2">Another IPv6 address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv6Address IPv6Address1, IPv6Address IPv6Address2)
            => !(IPv6Address1 == IPv6Address2);

        #endregion

        #endregion

        #region IComparable<IPAddress> Members

        #region IComparable Members

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is IPv6Address))
                throw new ArgumentException("The given object is not an IPv6Address!");

            return CompareTo((IPv6Address) Object);

        }

        #endregion

        #region CompareTo(IPv6Address)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPv6Address IPv6Address)
        {

            if ((Object)IPv6Address == null)
                throw new ArgumentNullException("The given IPv6 address must not be null!");

            var _ByteArray = IPv6Address.GetBytes();
            var _Comparision = 0;

            for (var _BytePosition = 0; _BytePosition < 4; _BytePosition++)
            {

                _Comparision = IPAddressArray[0].CompareTo(_ByteArray[0]);

                if (_Comparision != 0)
                    return _Comparision;

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

            if (IIPAddress == null)
                throw new ArgumentNullException("The given IIPAddress must not be null!");

            if (!(IIPAddress is IPv6Address))
                throw new ArgumentException("The given object is not an IPv6 address!");

            return CompareTo((IPv6Address) IIPAddress);

        }

        #endregion

        #endregion

        #region IEquatable<IPAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            if (!(Object is IPv6Address))
                return false;

            return Equals((IPv6Address) Object);

        }

        #endregion

        #region Equals(IPv6Address)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPv6Address IPv6Address)
        {

            if ((Object) IPv6Address == null)
                return false;

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(this, IPv6Address))
                return true;

            return ToString().Equals(IPv6Address.ToString());

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

            if ((Object) IIPAddress == null)
                throw new ArgumentNullException("The given IIPAddress must not be null!");

            if (_Length != IIPAddress.Length)
                return false;

            if (!(IIPAddress is IPv6Address))
                return false;

            return Equals((IPv6Address) IIPAddress);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => ToString().GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => String.Format("{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}{8}",
                             IPAddressArray[ 0].ToString("x2") + IPAddressArray[ 1].ToString("x2"),
                             IPAddressArray[ 2].ToString("x2") + IPAddressArray[ 3].ToString("x2"),
                             IPAddressArray[ 4].ToString("x2") + IPAddressArray[ 5].ToString("x2"),
                             IPAddressArray[ 6].ToString("x2") + IPAddressArray[ 7].ToString("x2"),
                             IPAddressArray[ 8].ToString("x2") + IPAddressArray[ 9].ToString("x2"),
                             IPAddressArray[10].ToString("x2") + IPAddressArray[11].ToString("x2"),
                             IPAddressArray[12].ToString("x2") + IPAddressArray[13].ToString("x2"),
                             IPAddressArray[14].ToString("x2") + IPAddressArray[15].ToString("x2"),
                             InterfaceId.IsNotNullOrEmpty() ? "%" + InterfaceId : "");

        #endregion

    }

}
