/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Net;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
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
        public IIPAddress  IPAddress   { get; }

        /// <summary>
        /// Returns the port of this IPSocket.
        /// </summary>
        public IPPort      Port        { get; }

        #endregion

        #region Constructor(s)

        #region IPSocket(IPAddress, Port)

        /// <summary>
        /// Generates a new IPSocket based on the given IPAddress and IPPort.
        /// </summary>
        /// <param name="IPAddress">The IPAdress of the socket.</param>
        /// <param name="Port">The port of the socket.</param>
        public IPSocket(IIPAddress  IPAddress,
                        IPPort      Port)
        {
            this.IPAddress  = IPAddress;
            this.Port       = Port;
        }

        #endregion

        #region IPSocket(IPEndPoint)

        /// <summary>
        /// Generates a new IPSocket based on the given IPEndPoint.
        /// </summary>
        /// <param name="IPEndPoint">An IPEndPoint.</param>
        public IPSocket(IPEndPoint IPEndPoint)
        {
            this.IPAddress  = IPAddressHelper.Build(IPEndPoint.Address.GetAddressBytes());
            this.Port       = new IPPort((UInt16) IPEndPoint.Port);
        }

        #endregion

        #endregion


        #region IPv4Address.Localhost / 127.0.0.1

        /// <summary>
        /// A socket on localhost and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket Localhost(IPPort Port)

            => new IPSocket(IPv4Address.Localhost,
                            Port);

        #endregion

        #region Parse(Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static IPSocket Parse(String Text)
        {

            var Splitter   = Text.LastIndexOf(":");
            var IPAddress  = Text.Substring(0, Splitter);
            var Port       = Text.Substring(Splitter + 1, Text.Length - Splitter - 1);

            return IPAddress.Contains(".")

                       ? new IPSocket(IPv4Address.Parse(IPAddress),
                                      IPPort.Parse(Port))

                       : new IPSocket(IPv6Address.Parse(IPAddress),
                                      IPPort.Parse(Port));

        }

        #endregion


        #region Operator overloading

        #region Operator == (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPSocket IPSocket1, IPSocket IPSocket2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(IPSocket1, IPSocket2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) IPSocket1 == null) || ((Object) IPSocket2 == null))
                return false;

            return IPSocket1.Equals(IPSocket2);

        }

        #endregion

        #region Operator != (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPSocket IPSocket1, IPSocket IPSocket2)
            => !(IPSocket1 == IPSocket2);

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
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var IPSocket = Object as IPSocket;
            if ((Object) IPSocket == null)
                throw new ArgumentException("The given object is not an IP socket!");

            return CompareTo(IPSocket);

        }

        #endregion

        #region CompareTo(IPSocket)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Int32 CompareTo(IPSocket IPSocket)
        {

            if (((Object) IPSocket) == null)
                throw new ArgumentNullException(nameof(IPSocket), "The given IP socket must not be null!");

            var __IPAddress = IPAddress.CompareTo(IPSocket.IPAddress);
            if (__IPAddress != 0)
                return __IPAddress;

            return Port.CompareTo(IPSocket.Port);

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
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var IPSocket = Object as IPSocket;
            if ((Object) IPSocket == null)
                throw new ArgumentException("The given object is not an IP socket!");

            return Equals(IPSocket);

        }

        #endregion

        #region Equals(IPSocket)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket">An object to compare with.</param>
        /// <returns>true|false</returns>
        public Boolean Equals(IPSocket IPSocket)
        {

            if (IPSocket == null)
                throw new ArgumentNullException(nameof(IPSocket), "The given IP socket must not be null!");

            var __IPAddress = IPAddress.Equals(IPSocket.IPAddress);
            if (__IPAddress)
                return Port.Equals(IPSocket.Port);

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

            => IPAddress.GetHashCode() ^
               Port.     GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => IPAddress.ToString() + ":" + Port;

        #endregion

    }

}
