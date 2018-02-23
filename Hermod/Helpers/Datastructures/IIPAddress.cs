/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class IPAddress
    {

        public static IIPAddress Parse(String Text)
        {

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given IP address must not be null or empty!");

            return Text.Contains(".")
                       ? (IIPAddress) IPv4Address.Parse(Text)
                       : (IIPAddress) IPv6Address.Parse(Text);

        }

    }


    /// <summary>
    /// A common interface for all kinds of Internet protocol addresses.
    /// </summary>
    public interface IIPAddress : IComparable,
                                  IComparable<IIPAddress>,
                                  IEquatable<IIPAddress>
    {

        /// <summary>
        /// The length of the IP Address.
        /// </summary>
        Byte   Length { get; }

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// </summary>
        Boolean IsMulticast { get; }

        Boolean IsIPv4 { get; }

        Boolean IsIPv6 { get; }

        Boolean IsLocalhost { get; }


        /// <summary>
        /// Return a byte array representation of this object.
        /// </summary>
        Byte[] GetBytes();


        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        Int32  GetHashCode();

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        String ToString();

    }

}
