/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class IPAddress
    {

        //ToDo: Better do this by hand!
        public static Regex IPv4AddressRegExpr = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

        //ToDo: Better do this by hand!
        public static Regex IPv6AddressRegExpr = new Regex(@"(([a-f0-9:]+:+)+[a-f0-9]+)");


        public static IIPAddress Parse(String Text)
        {

            if (TryParse(Text, out IIPAddress ipAddress))
                return ipAddress;

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given IP address must not be null or empty!");

            return null;

        }

        public static Boolean TryParse(String Text, out IIPAddress IPAddress)
        {

            Text = Text?.Trim();

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

        public static Boolean IsIPv4(String IPAddress)
            => IPAddress.IsNotNullOrEmpty() &&
               IPv4AddressRegExpr.IsMatch(IPAddress?.Trim());

        public static Boolean IsIPv4(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv4AddressRegExpr.IsMatch(Hostname.ToString());

        public static Boolean IsIPv6(String IPAddress)
            => IPAddress.IsNotNullOrEmpty() &&
               IPv6AddressRegExpr.IsMatch(IPAddress?.Trim());

        public static Boolean IsIPv6(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv6AddressRegExpr.IsMatch(Hostname.ToString());

        public static Boolean IsLocalhost(String Text)
            => IsIPv4Localhost(Text) || IsIPv6Localhost(Text);

        public static Boolean IsLocalhost(HTTPHostname Hostname)
            => IsIPv4Localhost(Hostname) || IsIPv6Localhost(Hostname);

        public static Boolean IsIPv4Localhost(String Text)
            => IsIPv4(Text) && (Text.StartsWith("127.") || Text.ToLower() == "localhost");

        public static Boolean IsIPv4Localhost(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv4Localhost(Hostname.ToString());

        public static Boolean IsIPv6Localhost(String Text)
            => IsIPv6(Text) && (Text == "::1" || Text.ToLower() == "localhost6");

        public static Boolean IsIPv6Localhost(HTTPHostname Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv6Localhost(Hostname.ToString());

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
