/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Net;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class IPSocketExtensions
    {

        #region ToIPEndPoint(this IPSocket)

        public static IPEndPoint ToIPEndPoint(this IPSocket IPSocket)

            => new IPEndPoint(System.Net.IPAddress.Parse(IPSocket.IPAddress.ToString()),
                              IPSocket.Port.ToUInt16());

        #endregion

    }


    /// <summary>
    /// An IP socket is a combination of an IP address and a layer4 port.
    /// </summary>
    public readonly struct IPSocket : IEquatable <IPSocket>,
                                      IComparable<IPSocket>,
                                      IComparable
    {

        #region Properties

        /// <summary>
        /// The IP address of this IP socket.
        /// </summary>
        public IIPAddress  IPAddress    { get; }

        /// <summary>
        /// The port of this IP socket.
        /// </summary>
        public IPPort      Port         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Generates a new IPSocket based on the given IPAddress and IPPort.
        /// </summary>
        /// <param name="IPAddress">The IPAdress of the socket.</param>
        /// <param name="Port">The port of the socket.</param>
        public IPSocket(IIPAddress  IPAddress,
                        IPPort      Port)
        {

            //if (IPAddress == null)
            //{

            //    StackTrace stackTrace = new StackTrace();           // get call stack
            //    StackFrame[] stackFrames = stackTrace.GetFrames();  // get method calls (frames)

            //    // write call stack method names
            //    foreach (StackFrame stackFrame in stackFrames)
            //    {
            //        Console.WriteLine(stackFrame.GetMethod().Name);   // write method name
            //    }

            //    DebugX.LogT(stackTrace.ToString());

            //}

            this.IPAddress  = IPAddress ?? throw new ArgumentNullException(nameof(IPAddress), "The given IP address must not be null!");
            this.Port       = Port;
        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static IPSocket Parse(String Text)
        {

            if (TryParse(Text, out IPSocket ipSocket))
                return ipSocket;

            throw new ArgumentException("The given text is not a valid IP socket!", nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static IPSocket? TryParse(String Text)
        {

            if (TryParse(Text, out IPSocket ipSocket))
                return ipSocket;

            return default;

        }

        #endregion

        #region TryParse(Text, out IPSocket IPSocket)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static Boolean TryParse(String Text, out IPSocket IPSocket)
        {

            IPSocket  = default;
            Text      = Text?.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var Splitter = Text.LastIndexOf(":");

            if (Splitter < 4) // "1.2.3.4:8" or "::1:8" or "[::]:80"
                return false;

            var ipAddress    = Text.Substring(0, Splitter);
            var ipv4Address  = IPv4Address.TryParse(ipAddress);
            var ipv6Address  = IPv6Address.TryParse(ipAddress);
            var port         = IPPort.     TryParse(Text.Substring(Splitter + 1, Text.Length - Splitter - 1));

            if (ipv4Address.HasValue && port.HasValue)
            {

                IPSocket = new IPSocket(ipv4Address.Value,
                                        port.       Value);

                return true;

            }

            if (ipv6Address.HasValue && port.HasValue)
            {

                IPSocket = new IPSocket(ipv6Address.Value,
                                        port.       Value);

                return true;

            }

            return false;

        }

        #endregion


        #region IPv4Address.LocalhostV4 / 127.0.0.1

        /// <summary>
        /// A socket on IPv4 localhost and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket LocalhostV4(IPPort Port)

            => new IPSocket(IPv4Address.Localhost,
                            Port);

        #endregion

        #region IPv4Address.LocalhostV6 / ::1

        /// <summary>
        /// A socket on IPv6 localhost and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket LocalhostV6(IPPort Port)

            => new IPSocket(IPv6Address.Localhost,
                            Port);

        #endregion


        #region (static) FromIPEndPoint(this IPEndPoint)

        public static IPSocket FromIPEndPoint(IPEndPoint IPEndPoint)

            => new IPSocket(
                   IPAddressHelper.Build(IPEndPoint.Address.GetAddressBytes()),
                   IPPort.Parse(IPEndPoint.Port)
               );

        public static IPSocket FromIPEndPoint(EndPoint IPEndPoint)

            => IPEndPoint is IPEndPoint ipEndPoint

                   ? new IPSocket(
                         IPAddressHelper.Build(ipEndPoint.Address.GetAddressBytes()),
                         IPPort.Parse(ipEndPoint.Port)
                     )

                   : throw new ArgumentException("The given EndPoint is not an IPEndPoint!", nameof(EndPoint));

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

        #region Operator <  (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPSocket IPSocket1, IPSocket IPSocket2)
        {

            if ((Object) IPSocket1 == null)
                throw new ArgumentNullException(nameof(IPSocket1), "The given IPSocket1 must not be null!");

            return IPSocket1.CompareTo(IPSocket2) < 0;

        }

        #endregion

        #region Operator <= (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPSocket IPSocket1, IPSocket IPSocket2)
            => !(IPSocket1 > IPSocket2);

        #endregion

        #region Operator >  (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPSocket IPSocket1, IPSocket IPSocket2)
        {

            if ((Object) IPSocket1 == null)
                throw new ArgumentNullException(nameof(IPSocket1), "The given IPSocket1 must not be null!");

            return IPSocket1.CompareTo(IPSocket2) > 0;

        }

        #endregion

        #region Operator >= (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPSocket IPSocket1, IPSocket IPSocket2)
            => !(IPSocket1 < IPSocket2);

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

            if (!((Object) is IPSocket))
                throw new ArgumentException("The given object is not an IP socket!");

            return CompareTo((IPSocket) Object);

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

            if (!(Object is IPSocket))
                throw new ArgumentException("The given object is not an IP socket!");

            return Equals((IPSocket) Object);

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
        {
            unchecked
            {
                return IPAddress.GetHashCode() * 7 ^
                       Port.     GetHashCode();
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => String.Concat(IPAddress,
                             ":",
                             Port);

        #endregion

    }

}
