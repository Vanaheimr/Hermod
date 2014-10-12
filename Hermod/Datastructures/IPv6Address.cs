/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IPv6 address.
    /// </summary>    
    public class IPv6Address : IIPAddress, IComparable, IComparable<IPv6Address>, IEquatable<IPv6Address>
    {

        #region Data

        private readonly Byte[] IPAddressArray;

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


        #endregion

        #region Constructor(s)

        #region IPv6Address(myIPAddress)

        /// <summary>
        /// Generates a new IPv6Address.
        /// </summary>
        public IPv6Address(System.Net.IPAddress myIPAddress)
            : this(myIPAddress.GetAddressBytes())
        { }

        #endregion

        #region IPv6Address(ByteArray)

        /// <summary>
        /// Generates a new IPv6Address.
        /// </summary>
        public IPv6Address(Byte[] ByteArray)
        {

            IPAddressArray = new Byte[_Length];

            Array.Copy(ByteArray, IPAddressArray, Math.Max(ByteArray.Length, _Length));

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

        }

        #endregion

        #endregion


        public static IPv6Address Any
        {
            get
            {
                return new IPv6Address(new Byte[_Length]);
            }
        }


        #region GetBytes()

        public Byte[] GetBytes()
        {
            return new Byte[_Length];
        }

        #endregion

        #region Parse(IPv6AddressString)

        /// <summary>
        /// Parsed the given string representation into a new IPv6Address.
        /// </summary>
        /// <param name="IPv6AddressString">An IPv6Address string representation.</param>
        public static IPv6Address Parse(String IPv6AddressString)
        {
            throw new ArgumentException("The given string '" + IPv6AddressString + "' is not a valid IPv6Address!");
        }

        #endregion

        #region TryParse(IPv6AddressString, out IPv6Address)

        /// <summary>
        /// Parsed the given string representation into a new IPv6Address.
        /// </summary>
        /// <param name="IPv6AddressString">An IPv6Address string representation.</param>
        public static Boolean TryParse(String IPv6AddressString, out IPv6Address IPv6Address)
        {
            throw new ArgumentException("The given string '" + IPv6AddressString + "' is not a valid IPv6Address!");
        }

        #endregion


        #region Operator overloading

        #region Operator == (myIPv6Address1, myIPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPv6Address1">A IPv6Address.</param>
        /// <param name="myIPv6Address2">Another IPv6Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPv6Address myIPv6Address1, IPv6Address myIPv6Address2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myIPv6Address1, myIPv6Address2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myIPv6Address1 == null) || ((Object) myIPv6Address2 == null))
                return false;

            return myIPv6Address1.Equals(myIPv6Address2);

        }

        #endregion

        #region Operator != (myIPv6Address1, myIPv6Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPv6Address1">A IPv6Address.</param>
        /// <param name="myIPv6Address2">Another IPv6Address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPv6Address myIPv6Address1, IPv6Address myIPv6Address2)
        {
            return !(myIPv6Address1 == myIPv6Address2);
        }

        #endregion

        #endregion


        #region IComparable<IPAddress> Members

        #region IComparable Members

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myObject">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("myObject must not be null!");

            // Check if myObject can be casted to an ElementId object
            var myIPAddress = myObject as IPv6Address;
            if ((Object) myIPAddress == null)
                throw new ArgumentException("myObject is not of type IPAddress!");

            return CompareTo(myIPAddress);

        }

        #endregion

        #region CompareTo(myIPAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myElementId">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPv6Address myIPAddress)
        {

            // Check if myIPAddress is null
            if (myIPAddress == null)
                throw new ArgumentNullException("myElementId must not be null!");

            //return _IPAddress.GetAddressBytes() .CompareTo(myIPAddress._IPAddress);
            return 0;

        }

        #endregion

        public int CompareTo(IIPAddress other)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IEquatable<IPAddress> Members

        #region Equals(myObject)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myObject">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("Parameter myObject must not be null!");

            // Check if myObject can be cast to IPAddress
            var myIPAddress = myObject as IPv6Address;
            if ((Object) myIPAddress == null)
                throw new ArgumentException("Parameter myObject could not be casted to type IPAddress!");

            return this.Equals(myIPAddress);

        }

        #endregion

        #region Equals(myIPAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myElementId">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPv6Address myIPAddress)
        {

            // Check if myIPAddress is null
            if (myIPAddress == null)
                throw new ArgumentNullException("Parameter myIPAddress must not be null!");

            var __IPAddress = IPAddressArray.Equals(myIPAddress.IPAddressArray);

            return false;

        }

        #endregion

        public bool Equals(IIPAddress other)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return IPAddressArray.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {

            return String.Format("{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}",
                                 IPAddressArray[0].ToString("x2")  + IPAddressArray[1].ToString("x2"),
                                 IPAddressArray[2].ToString("x2")  + IPAddressArray[3].ToString("x2"),
                                 IPAddressArray[4].ToString("x2")  + IPAddressArray[5].ToString("x2"),
                                 IPAddressArray[6].ToString("x2")  + IPAddressArray[7].ToString("x2"),
                                 IPAddressArray[8].ToString("x2")  + IPAddressArray[9].ToString("x2"),
                                 IPAddressArray[10].ToString("x2") + IPAddressArray[11].ToString("x2"),
                                 IPAddressArray[12].ToString("x2") + IPAddressArray[13].ToString("x2"),
                                 IPAddressArray[14].ToString("x2") + IPAddressArray[15].ToString("x2")
                                 
                                 
                                 );

        }

        #endregion

    }

}
