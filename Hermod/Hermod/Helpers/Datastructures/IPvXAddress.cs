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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv4/IPv6 address.
    /// </summary>
    public readonly struct IPvXAddress : IIPAddress,
                                         IComparable<IPvXAddress>,
                                         IEquatable<IPvXAddress>
    {

        #region Data

        private readonly Byte ipAddressArray;

        #endregion

        #region Properties

        /// <summary>
        /// The length of an IPvX address.
        /// </summary>
        public Byte     Length
            => 1;

        public Boolean  IsIPv4
            => true;

        public Boolean  IsIPv6
            => true;

        public Boolean  IsLocalhost
            => ipAddressArray == 1;

        public Boolean  IsAny
            => ipAddressArray == 0;

        public Boolean  IsMulticast
            => false;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IPvX address based on the given bytes.
        /// </summary>
        public IPvXAddress(Byte Byte1)
        {
            ipAddressArray = Byte1;
        }

        #endregion


        #region IPvXAddress.Any

        /// <summary>
        /// The IPv4.Any / 0.0.0.0 and IPv6.Any / [::] address.
        /// </summary>
        public static IPvXAddress Any

            => new (0);

        #endregion

        #region IPvXAddress.Localhost

        /// <summary>
        /// The IPv4 localhost / 127.0.0.1 and IPv6 localhost / [::1]
        /// </summary>
        public static IPvXAddress Localhost

            => new(1);

        #endregion


        #region GetBytes()

        public Byte[] GetBytes()

            => [ ipAddressArray ];

        #endregion



        #region Operator overloading

        #region Operator == (IPvXAddress1, IPvXAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPvXAddress1">A IPvXAddress.</param>
        /// <param name="IPvXAddress2">Another IPvXAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPvXAddress IPvXAddress1,
                                           IPvXAddress IPvXAddress2)

            => IPvXAddress1.Equals(IPvXAddress2);

        #endregion

        #region Operator != (IPvXAddress1, IPvXAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPvXAddress1">A IPvXAddress.</param>
        /// <param name="IPvXAddress2">Another IPvXAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPvXAddress IPvXAddress1,
                                           IPvXAddress IPvXAddress2)

            => !IPvXAddress1.Equals(IPvXAddress2);

        #endregion

        #endregion

        #region IComparable<IPvXAddress> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two IPvX addresses.
        /// </summary>
        /// <param name="Object">An IPvX address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPvXAddress ipvXAddress
                   ? CompareTo(ipvXAddress)
                   : throw new ArgumentException("The given object is not an IPvX address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPvXAddress)

        /// <summary>
        /// Compares two IPvX addresses.
        /// </summary>
        /// <param name="IPvXAddress">An IPvX address to compare with.</param>
        public Int32 CompareTo(IPvXAddress IPvXAddress)

            => ipAddressArray.CompareTo(IPvXAddress.ipAddressArray);

        #endregion

        #region CompareTo(IIPAddress)

        /// <summary>
        /// Compares two IP addresses.
        /// </summary>
        /// <param name="IIPAddress">An IP address to compare with.</param>
        public Int32 CompareTo(IIPAddress? IIPAddress)
        {

            if (IIPAddress is IPvXAddress ipvXAddress)
                return CompareTo(ipvXAddress);

            throw new ArgumentException("The given object is not an IPvX address!", nameof(IIPAddress));

        }

        #endregion

        #endregion

        #region IEquatable<IPvXAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two IPvX addresses.
        /// </summary>
        /// <param name="Object">An IPvX address to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is IPvXAddress ipvXAddress &&
                   Equals(ipvXAddress);

        #endregion

        #region Equals(IPvXAddress)

        /// <summary>
        /// Compares two IPvX addresses.
        /// </summary>
        /// <param name="IIPAddress">An IPvX address to compare with.</param>
        public Boolean Equals(IPvXAddress IPvXAddress)

            => ipAddressArray == IPvXAddress.ipAddressArray;

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

            if (IIPAddress is IPvXAddress ipvXAddress)
                return Equals(ipvXAddress);

            return false;

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => ipAddressArray;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => ipAddressArray == 0
                   ? "any"
                   : "localhost";

        #endregion

    }

}
