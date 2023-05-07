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
        public HTTPSource(IPSocket                  Socket,
                          IEnumerable<IIPAddress>?  ForwardedFor = null)
        {

            this.Socket            = Socket;
            this.ForwardedForList  = ForwardedFor?.ToArray() ?? Array.Empty<IIPAddress>();

            unchecked
            {
                hashCode = Socket.          GetHashCode() * 3 ^
                           ForwardedForList.CalcHashCode();
            }

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
        public static Boolean operator == (HTTPSource HTTPSource1,
                                           HTTPSource HTTPSource2)

            => HTTPSource1.Equals(HTTPSource2);

        #endregion

        #region Operator != (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPSource HTTPSource1,
                                           HTTPSource HTTPSource2)

            => !HTTPSource1.Equals(HTTPSource2);

        #endregion

        #region Operator <  (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPSource HTTPSource1,
                                          HTTPSource HTTPSource2)

            => HTTPSource1.CompareTo(HTTPSource2) < 0;

        #endregion

        #region Operator <= (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPSource HTTPSource1,
                                           HTTPSource HTTPSource2)

            => HTTPSource1.CompareTo(HTTPSource2) <= 0;

        #endregion

        #region Operator >  (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPSource HTTPSource1,
                                          HTTPSource HTTPSource2)

            => HTTPSource1.CompareTo(HTTPSource2) > 0;

        #endregion

        #region Operator >= (HTTPSource1, HTTPSource2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPSource1">A HTTP source.</param>
        /// <param name="HTTPSource2">Another HTTP source.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPSource HTTPSource1,
                                           HTTPSource HTTPSource2)

            => HTTPSource1.CompareTo(HTTPSource2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPSource> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP sources.
        /// </summary>
        /// <param name="Object">A HTTP source to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPSource httpSource
                   ? CompareTo(httpSource)
                   : throw new ArgumentException("The given object is not a HTTP source!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPSource)

        /// <summary>
        /// Compares two HTTP sources.
        /// </summary>
        /// <param name="HTTPSource">A HTTP source to compare with.</param>
        public Int32 CompareTo(HTTPSource HTTPSource)
        {

            var c = Socket.CompareTo(HTTPSource.Socket);

            if (c == 0)
                c = ForwardedForList.Length.CompareTo(HTTPSource.ForwardedForList.Length);

            foreach (var forwardedForElement in ForwardedForList)
            {

                c = forwardedForElement.CompareTo(HTTPSource.ForwardedForList.FirstOrDefault());

                if (c != 0)
                    return c;

            }

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<HTTPSource> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP sources for equality.
        /// </summary>
        /// <param name="Object">A HTTP source to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPSource httpSource &&
                   Equals(httpSource);

        #endregion

        #region Equals(HTTPSource)

        /// <summary>
        /// Compares two HTTP sources for equality.
        /// </summary>
        /// <param name="HTTPSource">A HTTP source to compare with.</param>
        public Boolean Equals(HTTPSource HTTPSource)

            => Socket.Equals(HTTPSource.Socket) &&

               ForwardedForList.Length.Equals(HTTPSource.ForwardedForList.Length) &&
               ForwardedForList.All(forwardedForElement => HTTPSource.ForwardedForList.Contains(forwardedForElement));

        #endregion

        #endregion

        #region GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => ForwardedForList.Any()
                   ? $"{Socket} ({ForwardedForList.AggregateWith(" <- ")})"
                   : Socket.ToString();

        #endregion

    }

}
