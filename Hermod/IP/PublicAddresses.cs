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

using System.Net;
using System.Net.Sockets;

using NetAddress = System.Net.IPAddress;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Which addresses are the internet's - for whatever connects where somebody
    /// else's name or record says, and must not be led to this host, its network,
    /// or what is behind it.
    /// </summary>
    public static class PublicAddresses
    {

        #region IsPublic(Address)

        /// <summary>
        /// Whether an address is one of the internet's: not loopback, private,
        /// shared (100.64/10), link-local, multicast, unspecified, benchmarking or
        /// otherwise reserved - also not when it is an IPv4 address of that kind
        /// inside an IPv6 one: mapped (::ffff:0:0/96), compatible (::/96), NAT64
        /// (64:ff9b::/96, 64:ff9b:1::/48), 6to4 (2002::/16) or Teredo (2001::/32),
        /// which a host with such a way out would reach as that IPv4 address.
        /// </summary>
        public static Boolean IsPublic(NetAddress Address)
        {

            if (Address.IsIPv4MappedToIPv6)
                Address = Address.MapToIPv4();

            if (NetAddress.IsLoopback(Address) || Address.Equals(NetAddress.Any) || Address.Equals(NetAddress.IPv6Any) ||
                Address.Equals(NetAddress.Broadcast))
                return false;

            if (Address.AddressFamily == AddressFamily.InterNetworkV6)
            {

                if (Address.IsIPv6LinkLocal || Address.IsIPv6SiteLocal || Address.IsIPv6UniqueLocal || Address.IsIPv6Multicast)
                    return false;

                var v6 = Address.GetAddressBytes();

                // ::/96 (IPv4-compatible, deprecated) - and :: and ::1, taken above.
                if (v6[..12].All(octet => octet == 0))
                    return IsPublic(new NetAddress(v6[12..16]));

                // NAT64: 64:ff9b::/96 and the local-use 64:ff9b:1::/48.
                if (v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xff && v6[3] == 0x9b)
                    return v6[4] == 0x00 && v6[5] == 0x00 && v6[6..12].All(octet => octet == 0)
                               ? IsPublic(new NetAddress(v6[12..16]))
                               : !(v6[4] == 0x00 && v6[5] == 0x01);

                // 6to4: 2002:AABB:CCDD::/48 carries the IPv4 address AA.BB.CC.DD.
                if (v6[0] == 0x20 && v6[1] == 0x02)
                    return IsPublic(new NetAddress(v6[2..6]));

                // Teredo: 2001:0000::/32, the client's IPv4 address in the last 32 bits, inverted.
                if (v6[0] == 0x20 && v6[1] == 0x01 && v6[2] == 0x00 && v6[3] == 0x00)
                    return IsPublic(new NetAddress(v6[12..16].Select(octet => (Byte) ~octet).ToArray()));

                // Documentation (2001:db8::/32) and discard-only (100::/64).
                return !(v6[0] == 0x20 && v6[1] == 0x01 && v6[2] == 0x0d && v6[3] == 0xb8 ||
                         v6[0] == 0x01 && v6[1] == 0x00 && v6[2..8].All(octet => octet == 0));

            }

            var octets = Address.GetAddressBytes();

            return !(octets[0] == 0                                          ||
                     octets[0] == 10                                         ||
                     octets[0] == 100 && octets[1] >= 64 && octets[1] <= 127 ||
                     octets[0] == 127                                        ||
                     octets[0] == 169 && octets[1] == 254                    ||
                     octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31  ||
                     octets[0] == 192 && octets[1] == 0   && octets[2] == 0  ||
                     octets[0] == 192 && octets[1] == 168                    ||
                     octets[0] == 198 && octets[1] >= 18  && octets[1] <= 19 ||
                     octets[0] >= 224);

        }

        #endregion

        #region TryParseLiteral(Host, out Address)

        /// <summary>
        /// The address a host name is when it is none but an address: 192.0.2.1, ::1, or an
        /// SMTP address literal (RFC 5321 §4.1.3) - [192.0.2.1], [IPv6:2001:db8::1].
        /// </summary>
        public static Boolean TryParseLiteral(String Host, out NetAddress Address)
        {

            var text = Host.Trim();

            if (text.StartsWith('[') && text.EndsWith(']'))
                text = text[1..^1];

            if (text.StartsWith("IPv6:", StringComparison.OrdinalIgnoreCase))
                text = text[5..];

            return NetAddress.TryParse(text, out Address!);

        }

        #endregion

        #region ResolveAsync(Host, CancellationToken)

        /// <summary>
        /// The addresses of a host name - the address itself when it is one - from the
        /// resolver of the system, as a connection to the name would have them.
        /// </summary>
        public static async Task<NetAddress[]> ResolveAsync(String             Host,
                                                           CancellationToken  CancellationToken   = default)

            => TryParseLiteral(Host, out var literal)
                   ? [ literal ]
                   : await Dns.GetHostAddressesAsync(Host, CancellationToken);

        #endregion

    }

}
