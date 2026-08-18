/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// Extension methods for Internet protocol addresses.
    /// </summary>
    public static class IIPAddressExtensions
    {

        #region ToIPLiteral(this IPAddress)

        /// <summary>
        /// Return this IP address in the shape the authority of an URL may carry it:
        /// an IPv6 address enclosed in brackets, an IPv4 address as it is.
        ///
        /// Note: RFC 3986 section 3.2.2 calls the bracketed form an "IP-literal". The
        ///       brackets are what keeps the colon in front of a port apart from the
        ///       colons inside the address.
        ///
        ///       IPv6Address.ToString() brackets "::" and "::1" by itself and spells
        ///       every other address out bare, so this adds brackets only where they
        ///       are missing. Bracketing unconditionally - which is what the callers
        ///       of this used to do, each on its own - rendered the loopback as
        ///       "[[::1]]".
        /// </summary>
        /// <param name="IPAddress">An IP address.</param>
        public static String ToIPLiteral(this IIPAddress IPAddress)
        {

            var text = IPAddress.ToString();

            return IPAddress.IsIPv6 && !text.StartsWith('[')
                       ? $"[{text}]"
                       :   text;

        }

        #endregion

    }

    /// <summary> 
    /// A common interface for all kinds of Internet protocol addresses.
    /// </summary>
    public interface IIPAddress : IEquatable<IIPAddress>,
                                  IComparable<IIPAddress>,
                                  IComparable
    {

        /// <summary>
        /// The length of the IP Address.
        /// </summary>
        Byte     Length         { get; }

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// </summary>
        Boolean  IsMulticast    { get; }

        Boolean  IsIPv4         { get; }

        Boolean  IsMappedIPv4   { get; }

        Boolean  IsIPv6         { get; }

        Boolean  IsLocalhost    { get; }

        Boolean  IsAny          { get; }



        IPv4Address?  AsIPv4         { get; }


        /// <summary>
        /// Return a byte array representation of this object.
        /// </summary>
        Byte[]   GetBytes();


        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        Int32    GetHashCode();

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        String   ToString();

    }

}
