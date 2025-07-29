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

#region Usings

using System.Net;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for IP sockets.
    /// </summary>
    public static class IPSocketExtensions
    {

        /// <summary>
        /// Convert this IP socket into an .NET IPEndPoint.
        /// </summary>
        public static IPEndPoint ToIPEndPoint(this IPSocket IPSocket)
        {

            if (IPSocket.IPAddress.IsAny)
                return new IPEndPoint(
                           System.Net.IPAddress.Any,
                           IPSocket.Port.ToUInt16()
                       );

            return new (
                       System.Net.IPAddress.Parse(
                           IPSocket.IPAddress.ToString()
                       ),
                       IPSocket.Port.ToUInt16()
                   );


        }

    }


    /// <summary>
    /// An IP socket: A combination of an IP address and an IP port.
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
        /// Create a new IP socket based on the given IP address and IP port.
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


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static IPSocket Parse(String Text)
        {

            if (TryParse(Text, out var ipSocket))
                return ipSocket;

            throw new ArgumentException("The given text is not a valid IP socket!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Parse the given text representation of an IP socket.
        /// </summary>
        /// <param name="Text">A text representation of an IP socket.</param>
        public static IPSocket? TryParse(String Text)
        {

            if (TryParse(Text, out var ipSocket))
                return ipSocket;

            return null;

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
            Text      = Text.Trim();

            if (Text.IsNullOrEmpty())
                return false;

            var Splitter = Text.LastIndexOf(":");

            if (Splitter < 4) // "1.2.3.4:8" or "::1:8" or "[::]:80"
                return false;

            var ipAddress    = Text[..Splitter];
            var ipv4Address  = IPv4Address.TryParse(ipAddress);
            var ipv6Address  = IPv6Address.TryParse(ipAddress);
            var port         = IPPort.     TryParse(Text.Substring(Splitter + 1, Text.Length - Splitter - 1));

            if (ipv4Address.HasValue && port.HasValue)
            {

                IPSocket = new IPSocket(
                               ipv4Address.Value,
                               port.       Value
                           );

                return true;

            }

            if (ipv6Address.HasValue && port.HasValue)
            {

                IPSocket = new IPSocket(
                               ipv6Address.Value,
                               port.       Value
                           );

                return true;

            }

            return false;

        }

        #endregion


        #region Zero

        /// <summary>
        /// A socket on IPv6 ::0 and port 0.
        /// </summary>
        public static IPSocket Zero { get; }

            = new (
                  IPv6Address.Any,
                  IPPort.Zero
              );

        #endregion

        #region IPv4Address.AnyV4 / 0.0.0.0

        /// <summary>
        /// A socket on IPv4 0.0.0.0 and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket AnyV4(IPPort Port)

            => new (
                   IPv4Address.Any,
                   Port
               );

        #endregion

        #region IPv4Address.AnyV6 / ::0

        /// <summary>
        /// A socket on IPv6 ::0 and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket AnyV6(IPPort Port)

            => new (
                   IPv6Address.Any,
                   Port
               );

        #endregion

        #region IPv4Address.LocalhostV4 / 127.0.0.1

        /// <summary>
        /// A socket on IPv4 localhost and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket LocalhostV4(IPPort Port)

            => new (
                   IPv4Address.Localhost,
                   Port
               );

        #endregion

        #region IPv4Address.LocalhostV6 / ::1

        /// <summary>
        /// A socket on IPv6 localhost and the given port.
        /// </summary>
        /// <param name="Port">The IP port.</param>
        public static IPSocket LocalhostV6(IPPort Port)

            => new (
                   IPv6Address.Localhost,
                   Port
               );

        #endregion


        #region (static) FromIPEndPoint(this IPEndPoint)

        /// <summary>
        /// Convert the given .NET IPEndPoint into an IP Socket.
        /// </summary>
        /// <param name="IPEndPoint">A .NET IPEndPoint.</param>
        public static IPSocket FromIPEndPoint(IPEndPoint IPEndPoint)

            => new (
                   IPAddressHelper.Build(IPEndPoint.Address.GetAddressBytes()),
                   IPPort.         Parse(IPEndPoint.Port)
               );


        /// <summary>
        /// Convert the given .NET EndPoint into an IP Socket.
        /// </summary>
        /// <param name="EndPoint">A .NET EndPoint.</param>
        public static IPSocket? FromIPEndPoint(EndPoint? EndPoint)

            => EndPoint is IPEndPoint ipEndPoint
                   ? FromIPEndPoint(ipEndPoint)
                   : null;

        #endregion


        #region Operator overloading

        #region Operator == (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (IPSocket IPSocket1,
                                           IPSocket IPSocket2)

            => IPSocket1.Equals(IPSocket2);

        #endregion

        #region Operator != (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (IPSocket IPSocket1,
                                           IPSocket IPSocket2)

            => !IPSocket1.Equals(IPSocket2);

        #endregion

        #region Operator <  (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (IPSocket IPSocket1,
                                          IPSocket IPSocket2)

            => IPSocket1.CompareTo(IPSocket2) < 0;

        #endregion

        #region Operator <= (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (IPSocket IPSocket1,
                                           IPSocket IPSocket2)

            => IPSocket1.CompareTo(IPSocket2) <= 0;

        #endregion

        #region Operator >  (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (IPSocket IPSocket1,
                                          IPSocket IPSocket2)

            => IPSocket1.CompareTo(IPSocket2) > 0;

        #endregion

        #region Operator >= (IPSocket1, IPSocket2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="IPSocket1">An IP socket.</param>
        /// <param name="IPSocket2">Another IP socket.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (IPSocket IPSocket1,
                                           IPSocket IPSocket2)

            => IPSocket1.CompareTo(IPSocket2) >= 0;

        #endregion

        #endregion

        #region IComparable<IPSocket> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two IP sockets.
        /// </summary>
        /// <param name="Object">An IP socket to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is IPSocket ipSocket
                   ? CompareTo(ipSocket)
                   : throw new ArgumentException("The given object is not an IP socket!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(IPSocket)

        /// <summary>
        /// Compares two IP sockets.
        /// </summary>
        /// <param name="IPSocket">An IP socket to compare with.</param>
        public Int32 CompareTo(IPSocket IPSocket)
        {

            var c = IPAddress.CompareTo(IPSocket.IPAddress);

            if (c == 0)
                c = Port.     CompareTo(IPSocket.Port);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<IPSocket> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two IP sockets for equality.
        /// </summary>
        /// <param name="Object">An IP socket to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is IPSocket ipSocket &&
                   Equals(ipSocket);

        #endregion

        #region Equals(IPSocket)

        /// <summary>
        /// Compares two IP sockets for equality.
        /// </summary>
        /// <param name="IPSocket">An IP socket to compare with.</param>
        public Boolean Equals(IPSocket IPSocket)

            => IPAddress.Equals(IPSocket.IPAddress) &&
               Port.     Equals(IPSocket.Port);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return IPAddress.GetHashCode() * 3 ^
                       Port.     GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{IPAddress}:{Port}";

        #endregion


    }

}
