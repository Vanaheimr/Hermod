/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Net;

#endregion

namespace de.ahzf.Hermod.Datastructures
{

    /// <summary>
    /// An IPSocket is a combination of an IPAddress and a IPPort.
    /// </summary>    
    public class IPSocket : IComparable, IComparable<IPSocket>, IEquatable<IPSocket>
    {

        #region Properties

        /// <summary>
        /// The IPAddress of this IPSocket.
        /// </summary>
        public IIPAddress IPAddress { get; private set; }

        /// <summary>
        /// Returns the port of this IPSocket.
        /// </summary>
        public IPPort     Port      { get; private set; }

        #endregion

        #region Constructor(s)

        #region IPSocket(IPAddress, Port)

        /// <summary>
        /// Generates a new IPSocket based on the given IPAddress and IPPort.
        /// </summary>
        /// <param name="IPAddress">The IPAdress of the socket.</param>
        /// <param name="Port">The port of the socket.</param>
        public IPSocket(IIPAddress IPAddress, IPPort Port)
        {
            this.IPAddress = IPAddress;
            this.Port      = Port;
        }

        #endregion

        #region IPSocket(IPEndPoint)

        /// <summary>
        /// Generates a new IPSocket based on the given IPEndPoint.
        /// </summary>
        /// <param name="IPEndPoint">An IPEndPoint.</param>
        public IPSocket(IPEndPoint IPEndPoint)
        {
            this.IPAddress = IPAddressHelper.Build(IPEndPoint.Address.GetAddressBytes());
            this.Port      = new IPPort((UInt16) IPEndPoint.Port);
        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (myIPSocket1, myIPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPSocket1">A IPSocket.</param>
        /// <param name="myIPSocket2">Another IPSocket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPSocket myIPSocket1, IPSocket myIPSocket2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myIPSocket1, myIPSocket2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myIPSocket1 == null) || ((Object) myIPSocket2 == null))
                return false;

            return myIPSocket1.Equals(myIPSocket2);

        }

        #endregion

        #region Operator != (myIPSocket1, myIPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myIPSocket1">A IPSocket.</param>
        /// <param name="myIPSocket2">Another IPSocket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPSocket myIPSocket1, IPSocket myIPSocket2)
        {
            return !(myIPSocket1 == myIPSocket2);
        }

        #endregion

        #endregion

        #region IComparable Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given Object must not be null!");

            // Check if myObject can be casted to an IPSocket object
            var myIPSocket = Object as IPSocket;
            if ((Object) myIPSocket == null)
                throw new ArgumentException("The given Object is not an IPSocket!");

            return CompareTo(myIPSocket);

        }

        #endregion

        #region CompareTo(IPSocket)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myElementId">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPSocket IPSocket)
        {

            if (((Object) IPSocket) == null)
                throw new ArgumentNullException("The given IPSocket object must not be null!");

            var __IPAddress = this.IPAddress.CompareTo(IPSocket.IPAddress);
            if (__IPAddress != 0)
                return __IPAddress;

            return this.Port.CompareTo(IPSocket.Port);

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
        {

            if (Object == null)
                throw new ArgumentNullException("The given Object must not be null!");

            // Check if myObject can be cast to IPSocket
            var myIPSocket = Object as IPSocket;
            if ((Object) myIPSocket == null)
                throw new ArgumentException("The given Object is not an IPSocket!");

            return this.Equals(myIPSocket);

        }

        #endregion

        #region Equals(IPSocket)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="myElementId">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPSocket IPSocket)
        {

            if (IPSocket == null)
                throw new ArgumentNullException("Teh given IPSocket must not be null!");

            var __IPAddress = this.IPAddress.Equals(IPSocket.IPAddress);
            if (__IPAddress)
                return this.Port.Equals(IPSocket.Port);

            return false;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return IPAddress.GetHashCode() ^ Port.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return IPAddress.ToString() + ":" + Port.ToString();
        }

        #endregion

    }

}
