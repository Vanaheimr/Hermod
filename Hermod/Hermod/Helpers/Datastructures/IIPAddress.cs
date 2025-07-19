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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IP address (IPv4 or IPv6).
    /// </summary>
    public static class IPAddress
    {

        #region Data

        //ToDo: Better do this by hand!
        public static readonly Regex IPv4AddressRegExpr = new (@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

        //ToDo: Better do this by hand!
        public static readonly Regex IPv6AddressRegExpr = new (@"(([a-f0-9:]+:+)+[a-f0-9]+)");

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an IP address.
        /// </summary>
        /// <param name="Text">A text representation of an IP address.</param>
        public static IIPAddress Parse(String Text)
        {

            if (TryParse(Text, out var ipAddress))
                return ipAddress;

            throw new ArgumentException($"Invalid text representation of an IP address: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) Parse   (Bytes)

        /// <summary>
        /// Parse the given byte array as an IP address.
        /// </summary>
        /// <param name="Bytes">A binary representation of an IP address.</param>
        public static IIPAddress Parse(Byte[] Bytes)
        {

            if (TryParse(Bytes, out var ipAddress))
                return ipAddress;

            throw new ArgumentException($"Invalid binary representation of an IP address: '{Bytes.ToHexString()}'!",
                                        nameof(Bytes));

        }

        #endregion

        #region (static) TryParse(Text,  out IPAddress)

        /// <summary>
        /// Try to parse the given text as an IP address.
        /// </summary>
        /// <param name="Text">A text representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(String                               Text,
                                       [NotNullWhen(true)] out IIPAddress?  IPAddress)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (IsIPv4(Text))
                {
                    IPAddress = IPv4Address.Parse(Text);
                    return true;
                }

                if (IsIPv6(Text))
                {
                    IPAddress = IPv6Address.Parse(Text);
                    return true;
                }

            }

            IPAddress = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Bytes, out IPAddress)

        /// <summary>
        /// Try to parse the given byte array as an IP address.
        /// </summary>
        /// <param name="Bytes">A binary representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(Byte[]                               Bytes,
                                       [NotNullWhen(true)] out IIPAddress?  IPAddress)
        {

            if (Bytes.Length == 4)
            {
                IPAddress = new IPv4Address(Bytes);
                return true;
            }

            if (Bytes.Length == 16)
            {
                IPAddress = new IPv6Address(Bytes);
                return true;
            }

            IPAddress = null;
            return false;

        }

        #endregion


        public static Boolean IsIPv4(String IPAddress)
            => IPAddress.IsNotNullOrEmpty() &&
               IPv4AddressRegExpr.IsMatch(IPAddress.Trim());

        public static Boolean IsIPv4(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv4AddressRegExpr.IsMatch(Hostname.ToString());

        public static Boolean IsIPv6(String IPAddress)
            => IPAddress.IsNotNullOrEmpty() &&
               IPv6AddressRegExpr.IsMatch(IPAddress.Trim());

        public static Boolean IsIPv6(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv6AddressRegExpr.IsMatch(Hostname.ToString());


        public static Boolean IsLocalhost(String Text)
            => IsIPv4Localhost(Text) || IsIPv6Localhost(Text);

        public static Boolean IsLocalhost(HTTPHostname Hostname)
            => IsIPv4Localhost(Hostname) || IsIPv6Localhost(Hostname);

        public static Boolean IsIPv4Localhost(String Text)
            => (IsIPv4(Text) && Text.StartsWith("127.")) || Text.ToLower() == "localhost";

        public static Boolean IsIPv4Localhost(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv4Localhost(Hostname.ToString());

        public static Boolean IsIPv6Localhost(String Text)
            => (IsIPv6(Text) && Text == "::1") || Text.ToLower() == "localhost6";

        public static Boolean IsIPv6Localhost(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv6Localhost(Hostname.ToString());



        /// <summary>
        /// Convert this IP address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">An IP address.</param>
        public static IIPAddress Convert(System.Net.IPAddress IPAddress)
        {

            var bytes = IPAddress.GetAddressBytes();

            if (bytes.Length == 4)
                return new IPv4Address(bytes);

            else if (bytes.Length == 16)
                return new IPv6Address(bytes);

            else
                throw new ArgumentException($"Invalid byte array length for an IP address: {bytes.Length}!",
                                            nameof(IPAddress));

        }


        /// <summary>
        /// Convert this IP address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">An IP address.</param>
        public static System.Net.IPAddress Convert(this IIPAddress IPAddress)

            => new (IPAddress.GetBytes());


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
        Byte     Length         { get; }

        /// <summary>
        /// Whether the IP address is an IPv4 multicast address.
        /// </summary>
        Boolean  IsMulticast    { get; }

        Boolean  IsIPv4         { get; }

        Boolean  IsIPv6         { get; }

        Boolean  IsLocalhost    { get; }


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
