/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP source.
    /// </summary>
    public readonly struct HTTPSource : IEquatable<HTTPSource>,
                                        IComparable<HTTPSource>,
                                        IComparable
    {

        #region Properties

        /// <summary>
        /// The IP socket of the HTTP source.
        /// </summary>
        public IPSocket    Socket      { get; }

        /// <summary>
        /// The IP address of the HTTP source.
        /// </summary>
        public IIPAddress  IPAddress
                   => Socket.IPAddress;

        /// <summary>
        /// The port of the HTTP source.
        /// </summary>
        public IPPort      Port
                   => Socket.Port;


        private readonly IIPAddress[] ForwardedForList;

        /// <summary>
        /// An additional enumeration of IP addresses, when the message had been forwarded between HTTP servers.
        /// </summary>
        public IEnumerable<IIPAddress>  ForwardedFor
                   => ForwardedForList;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP source.
        /// </summary>
        /// <param name="Socket">The IP socket of the HTTP source.</param>
        /// <param name="ForwardedFor">An additional enumeration of IP addresses, when the message had been forwarded between HTTP servers.</param>
        public HTTPSource(IPSocket                 Socket,
                          IEnumerable<IIPAddress>  ForwardedFor = null)
        {

            this.Socket            = Socket;
            this.ForwardedForList  = ForwardedFor != null ? ForwardedFor.ToArray() : new IIPAddress[0];

        }

        #endregion


        #region Parse(Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static HTTPSource Parse(String Text)
        {

            if (!Text.IsNullOrEmpty())
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text must not be null or empty!");

            var Socket = IPSocket.Parse(Text);

            return new HTTPSource(Socket);

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPSource HTTPSource1, HTTPSource HTTPSource2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPSource1, HTTPSource2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPSource1 == null) || ((Object) HTTPSource2 == null))
                return false;

            return HTTPSource1.Equals(HTTPSource2);

        }

        #endregion

        #region Operator != (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPSource HTTPSource1, HTTPSource HTTPSource2)
            => !(HTTPSource1 == HTTPSource2);

        #endregion

        #endregion

        #region IComparable<HTTPSource> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (Object is HTTPSource)
                return CompareTo((HTTPSource) Object);

            throw new ArgumentException("The given object is neither a HTTP source, nor its text representation!");

        }

        #endregion

        #region CompareTo(HTTPSource)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource">An object to compare with.</param>
        public Int32 CompareTo(HTTPSource HTTPSource)
        {

            if ((Object) HTTPSource == null)
                throw new ArgumentNullException("The given HTTP source must not be null!");

            return Socket.CompareTo(HTTPSource.Socket);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPSource> Members

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

            if (Object is HTTPSource)
                return Equals((HTTPSource) Object);

            return false;

        }

        #endregion

        #region Equals(HTTPSource)

        /// <summary>
        /// Compares two HTTPSources for equality.
        /// </summary>
        /// <param name="HTTPSource">A HTTPSource to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPSource HTTPSource)
        {

            if ((Object) HTTPSource == null)
                return false;

            return Socket.Equals(HTTPSource.Socket);

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
            unchecked
            {
                return Socket.       GetHashCode() * 3 ^
                       ForwardedForList.GetHashCode();
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => ForwardedForList.Length > 0
                   ? String.Concat(Socket, " (", ForwardedForList.AggregateWith(" <- "), ")")
                   : Socket.ToString();

        #endregion

    }

}
