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

#region Usings

using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.NetworkInformation;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// An IP address (IPv4 or IPv6).
    /// </summary>
    public static class IPAddress
    {

        #region Data

        /// <summary>
        /// Something that looks like an IPv4 address, anywhere in a text.
        /// </summary>
        /// <remarks>
        /// Not whether a text IS one - "udp://213.133.98.98:53" and
        /// "10.0.0.1.nip.io" both match - which is why IsIPv4 and TryParse
        /// no longer ask it. They used to, and TryParse then threw on
        /// whatever the pattern had let through.
        /// </remarks>
        public static readonly Regex IPv4AddressRegExpr = new (@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

        /// <summary>
        /// Something that looks like an IPv6 address, anywhere in a text - see
        /// IPv4AddressRegExpr for why nothing here asks it any more.
        /// </summary>
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
        /// <remarks>
        /// The whole text has to be the address. This used to find one
        /// anywhere in it with IPv4AddressRegExpr and then Parse it, which threw
        /// on what the pattern had let through: "udp://213.133.98.98:53" in a
        /// configuration file stopped a program at its start with an exception
        /// out of a TryParse, where the file should have been refused with a
        /// sentence. The typed parsers read the whole text and say no instead.
        /// </remarks>
        /// <param name="Text">A text representation of an IP address.</param>
        /// <param name="IPAddress">The parsed IP address.</param>
        public static Boolean TryParse(String                               Text,
                                       [NotNullWhen(true)] out IIPAddress?  IPAddress)
        {

            if (IPv4Address.TryParse(Text, out var ipv4Address))
            {
                IPAddress = ipv4Address;
                return true;
            }

            if (IPv6Address.TryParse(Text, out var ipv6Address))
            {
                IPAddress = ipv6Address;
                return true;
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


        // Whether the whole of it is an address, by the typed parsers - see
        // TryParse. Asked of a pattern, a host name that merely contained an
        // address was one: "10.0.0.1.nip.io" went on to IPv4Address.Parse in
        // the TCP client and threw, and "127.0.0.1.example.com" was taken for
        // localhost. The host name's own port is not part of the question;
        // the domain name's root dot is not either.

        public static Boolean IsIPv4(String        IPAddress)
            => IPv4Address.TryParse(IPAddress, out _);

        public static Boolean IsIPv4(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv4Address.TryParse(Hostname, out _);

        public static Boolean IsIPv4(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() &&
               IPv4Address.TryParse(DomainName, out _);


        public static Boolean IsIPv6(String        IPAddress)
            => IPv6Address.TryParse(IPAddress, out _);

        public static Boolean IsIPv6(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty &&
               IPv6Address.TryParse(Hostname, out _);

        public static Boolean IsIPv6(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() &&
               IPv6Address.TryParse(DomainName, out _);


        public static Boolean IsLocalhost(String        Text)
            => IsIPv4Localhost(Text)       || IsIPv6Localhost(Text);

        public static Boolean IsLocalhost(HTTPHostname  Hostname)
            => IsIPv4Localhost(Hostname)   || IsIPv6Localhost(Hostname);

        public static Boolean IsLocalhost(DomainName    DomainName)
            => IsIPv4Localhost(DomainName) || IsIPv6Localhost(DomainName);


        public static Boolean IsIPv4Localhost(String        Text)
            => (IsIPv4(Text) && Text.StartsWith("127.")) ||
               Text.Equals("localhost",  StringComparison.CurrentCultureIgnoreCase);

        public static Boolean IsIPv4Localhost(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv4Localhost(Hostname.ToString());

        public static Boolean IsIPv4Localhost(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() && IsIPv4Localhost(DomainName.ToString());


        public static Boolean IsIPv6Localhost(String        Text)
            => (IsIPv6(Text) && Text == "::1") ||
               Text.Equals("localhost",  StringComparison.CurrentCultureIgnoreCase) ||
               Text.Equals("localhost6", StringComparison.CurrentCultureIgnoreCase);

        public static Boolean IsIPv6Localhost(HTTPHostname  Hostname)
            => Hostname.IsNotNullOrEmpty && IsIPv6Localhost(Hostname.ToString());

        public static Boolean IsIPv6Localhost(DomainName    DomainName)
            => DomainName.IsNotNullOrEmpty() && IsIPv6Localhost(DomainName.ToString());



        public static IIPAddress Any
            => IPvXAddress.Any;

        public static IIPAddress Localhost
            => IPvXAddress.Localhost;


        /// <summary>
        /// Convert the given System.Net.IPAddress into a Hermod IP address.
        /// </summary>
        /// <param name="IPAddress">A System.Net.IPAddress.</param>
        public static IIPAddress FromDotNet(System.Net.IPAddress IPAddress)
        {

            var bytes = IPAddress.GetAddressBytes();

            if (bytes.Length == 4)
                return new IPv4Address(bytes);

            else if (bytes.Length == 16)
                return IPv6Address.From(IPAddress);

            else
                throw new ArgumentException($"Invalid byte array length for an IP address: {bytes.Length}!",
                                            nameof(IPAddress));

        }


        /// <summary>
        /// Convert this Hermod IP address into a System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">A Hermod IP address.</param>
        public static System.Net.IPAddress ToDotNet(this IIPAddress IPAddress)
        {

            // IPv4/IPv6 dual mode...
            if (IPAddress.IsIPv4 && IPAddress.IsIPv6)
            {

                if (IPAddress.IsLocalhost)
                    return System.Net.IPAddress.IPv6Loopback;

                return System.Net.IPAddress.IPv6Any;

            }

            if (IPAddress is IPv6Address ipv6Address &&
                ipv6Address.InterfaceId.IsNotNullOrEmpty())
            {

                if (!TryResolveIPv6ScopeId(ipv6Address.InterfaceId, out var scopeId))
                    throw new ArgumentException($"Unknown IPv6 interface identification '{ipv6Address.InterfaceId}'!",
                                                nameof(IPAddress));

                return new System.Net.IPAddress(ipv6Address.GetBytes(), scopeId);

            }

            return new (IPAddress.GetBytes());

        }


        #region (static) Build(ByteArray)

        /// <summary>
        /// Create a new IIPAddress based on the given byte array representation.
        /// </summary>
        /// <param name="ByteArray">A byte representation of an IPAddress.</param>
        public static IIPAddress Build(Byte[] ByteArray)
        {

            switch (ByteArray.Length)
            {

                case  4: return new IPv4Address(ByteArray);
                
                case 16: return new IPv6Address(ByteArray);

                default: throw new FormatException("Not a valid IIPAdress!");

            }

        }

        #endregion

        #region (static) Build(IPAddress)

        /// <summary>
        /// Create a new IIPAddress based on the given System.Net.IPAddress.
        /// </summary>
        /// <param name="IPAddress">A System.Net.IPAddress.</param>
        public static IIPAddress Build(System.Net.IPAddress IPAddress)

            => FromDotNet(IPAddress);

        #endregion


        #region (private static) TryResolveIPv6ScopeId(InterfaceId, out ScopeId)

        private static Boolean TryResolveIPv6ScopeId(String InterfaceId, out Int64 ScopeId)
        {

            if (Int64.TryParse(InterfaceId,
                               NumberStyles.None,
                               CultureInfo.InvariantCulture,
                               out ScopeId))
            {
                return ScopeId >= 0;
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {

                if (!networkInterface.Name.Equals(InterfaceId, StringComparison.Ordinal) &&
                    !networkInterface.Id.  Equals(InterfaceId, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {

                    var ipv6Properties = networkInterface.GetIPProperties().GetIPv6Properties();

                    if (ipv6Properties is not null)
                    {
                        ScopeId = ipv6Properties.Index;
                        return true;
                    }

                }
                catch (NetworkInformationException)
                { }

            }

            ScopeId = 0;
            return false;

        }

        #endregion


    }

}
