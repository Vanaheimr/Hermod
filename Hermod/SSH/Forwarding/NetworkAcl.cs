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

using System.Net.Sockets;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    // Our namespace is nested under Hermod, whose `IPAddress` is a static factory class that shadows the
    // System.Net type — alias it inside the namespace block so the alias wins (a file-scoped alias would lose).
    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// Whether an ACL rule permits or forbids a matching request.
    /// </summary>
    public enum NetworkAclAction
    {
        /// <summary>
        /// The request is permitted.
        /// </summary>
        Allow,
        /// <summary>
        /// The request is forbidden.
        /// </summary>
        Deny
    }


    #region IpCidr

    /// <summary>
    /// An IPv4 or IPv6 address prefix (CIDR), e.g. <c>10.20.0.0/16</c>, <c>127.0.0.0/8</c>, <c>::1</c>,
    /// <c>fc00::/7</c>. A bare address is treated as a host route (/32 or /128).
    /// </summary>
    public readonly struct IpCidr
    {

        private readonly Byte[]         network;
        private readonly Int32          prefixBits;
        private readonly AddressFamily  family;

        private IpCidr(Byte[] Network, Int32 PrefixBits, AddressFamily Family)
        {
            this.network     = Network;
            this.prefixBits  = PrefixBits;
            this.family      = Family;
        }

        /// <summary>
        /// Parse a CIDR or a bare address.
        /// </summary>
        public static IpCidr Parse(String Text)
        {

            var slash   = Text.IndexOf('/');
            var addrStr = slash < 0 ? Text : Text[..slash];
            var addr    = IPAddress.Parse(addrStr.Trim());
            var bytes   = addr.GetAddressBytes();
            var maxBits = bytes.Length * 8;
            var prefix  = slash < 0 ? maxBits : Int32.Parse(Text[(slash + 1)..].Trim());

            if (prefix < 0 || prefix > maxBits)
                throw new FormatException($"Invalid prefix length in CIDR '{Text}'.");

            // Zero the host bits so the stored network is canonical.
            MaskInPlace(bytes, prefix);
            return new IpCidr(bytes, prefix, addr.AddressFamily);

        }

        /// <summary>
        /// Whether the given address falls within this prefix.
        /// </summary>
        public Boolean Contains(IPAddress Address)
        {

            if (Address.AddressFamily != family)
                return false;

            var bytes = Address.GetAddressBytes();
            if (bytes.Length != network.Length)
                return false;

            var fullBytes = prefixBits / 8;
            for (var i = 0; i < fullBytes; i++)
                if (bytes[i] != network[i])
                    return false;

            var remaining = prefixBits % 8;
            if (remaining == 0)
                return true;

            var mask = (Byte) (0xFF << (8 - remaining));
            return (bytes[fullBytes] & mask) == (network[fullBytes] & mask);

        }

        private static void MaskInPlace(Byte[] Bytes, Int32 PrefixBits)
        {
            for (var i = 0; i < Bytes.Length; i++)
            {
                var bitsHere = Math.Clamp(PrefixBits - i * 8, 0, 8);
                var mask     = bitsHere == 0 ? 0 : (Byte) (0xFF << (8 - bitsHere));
                Bytes[i]    &= (Byte) mask;
            }
        }

    }

    #endregion

    #region PortSet

    /// <summary>
    /// A set of TCP ports expressed as individual ports and/or inclusive ranges (e.g. <c>80,443,8000-8999</c>).
    /// </summary>
    public readonly struct PortSet
    {

        private readonly (UInt16 Lo, UInt16 Hi)[] ranges;

        private PortSet((UInt16, UInt16)[] Ranges)
        {
            this.ranges = Ranges;
        }

        /// <summary>
        /// Parse a comma-separated list of ports and <c>lo-hi</c> ranges.
        /// </summary>
        public static PortSet Parse(String Text)
        {
            var ranges = new List<(UInt16, UInt16)>();
            foreach (var part in Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var dash = part.IndexOf('-');
                if (dash < 0)
                {
                    var p = UInt16.Parse(part);
                    ranges.Add((p, p));
                }
                else
                {
                    var lo = UInt16.Parse(part[..dash]);
                    var hi = UInt16.Parse(part[(dash + 1)..]);
                    ranges.Add(lo <= hi ? (lo, hi) : (hi, lo));
                }
            }
            return new PortSet(ranges.ToArray());
        }

        /// <summary>
        /// A port set from explicit ports.
        /// </summary>
        public static PortSet Of(params UInt16[] Ports)
            => new (Ports.Select(p => (p, p)).ToArray());

        /// <summary>
        /// Whether the given port is in the set.
        /// </summary>
        public Boolean Contains(UInt16 Port)
        {
            foreach (var (lo, hi) in ranges)
                if (Port >= lo && Port <= hi)
                    return true;
            return false;
        }

    }

    #endregion


    #region NetworkAclRule

    /// <summary>
    /// One rule in a <see cref="NetworkAcl"/>: an action plus the dimensions it constrains. A null dimension
    /// is a wildcard; a rule matches only when every specified dimension matches.
    /// </summary>
    public sealed class NetworkAclRule
    {

        /// <summary>
        /// Allow or deny when this rule matches.
        /// </summary>
        public NetworkAclAction  Action       { get; }

        /// <summary>
        /// The address prefix this rule applies to (null = any address).
        /// </summary>
        public IpCidr?           Cidr         { get; }

        /// <summary>
        /// The ports this rule applies to (null = any port).
        /// </summary>
        public PortSet?          Ports        { get; }

        /// <summary>
        /// A hostname wildcard pattern this rule applies to (null = any / not host-scoped).
        /// </summary>
        public String?           HostPattern  { get; }

        internal NetworkAclRule(NetworkAclAction Action, IpCidr? Cidr, PortSet? Ports, String? HostPattern)
        {
            this.Action       = Action;
            this.Cidr         = Cidr;
            this.Ports        = Ports;
            this.HostPattern  = HostPattern;
        }

        /// <summary>
        /// Whether this rule matches the given target.
        /// </summary>
        public Boolean Matches(IPAddress? Address, UInt16 Port, String? Host)
        {
            if (Cidr is { } cidr && (Address is null || !cidr.Contains(Address)))
                return false;
            if (Ports is { } ports && !ports.Contains(Port))
                return false;
            if (HostPattern is { } pattern && (Host is null || !WildcardMatch(pattern, Host)))
                return false;
            return true;
        }

        private static Boolean WildcardMatch(String Pattern, String Value)
        {
            var regex = "^" + Regex.Escape(Pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(Value, regex, RegexOptions.IgnoreCase);
        }

    }

    #endregion

    #region NetworkAcl

    /// <summary>
    /// An ordered allow/deny rule engine for network targets (port-forwarding destinations and bind
    /// addresses). Rules are evaluated first-match-wins; the default is <b>deny</b>. The engine is shared by
    /// both roles and evaluated against resolved addresses (DNS-rebinding safe — see
    /// <see cref="AllowsAll"/>).
    /// </summary>
    public sealed class NetworkAcl
    {

        #region Data

        private readonly List<NetworkAclRule> rules = [];

        /// <summary>
        /// The action taken when no rule matches (defaults to <see cref="NetworkAclAction.Deny"/>).
        /// </summary>
        public NetworkAclAction DefaultAction { get; private set; } = NetworkAclAction.Deny;

        /// <summary>
        /// The rules, in evaluation order.
        /// </summary>
        public IReadOnlyList<NetworkAclRule> Rules => rules;

        #endregion

        #region Fluent construction

        /// <summary>
        /// An ACL that denies everything unless a later rule allows it.
        /// </summary>
        public static NetworkAcl DenyByDefault() => new ();

        /// <summary>
        /// An ACL that allows everything unless a later rule denies it.
        /// </summary>
        public static NetworkAcl AllowByDefault() => new () { DefaultAction = NetworkAclAction.Allow };

        /// <summary>
        /// Append an allow rule (any null dimension is a wildcard).
        /// </summary>
        public NetworkAcl Allow(String? Cidr = null, String? Ports = null, String? Host = null)
            => Add(NetworkAclAction.Allow, Cidr, Ports, Host);

        /// <summary>
        /// Append a deny rule (any null dimension is a wildcard).
        /// </summary>
        public NetworkAcl Deny(String? Cidr = null, String? Ports = null, String? Host = null)
            => Add(NetworkAclAction.Deny, Cidr, Ports, Host);

        private NetworkAcl Add(NetworkAclAction Action, String? Cidr, String? Ports, String? Host)
        {
            rules.Add(new NetworkAclRule(Action,
                                         Cidr  is null ? null : IpCidr.Parse(Cidr),
                                         Ports is null ? null : PortSet.Parse(Ports),
                                         Host));
            return this;
        }

        #endregion

        #region Evaluation

        /// <summary>
        /// Whether a single resolved target is permitted.
        /// </summary>
        public Boolean Allows(IIPAddress Address, UInt16 Port, String? Host = null)
            => Allows(Address.ToDotNet(), Port, Host);

        /// <summary>
        /// Whether a single resolved target is permitted.
        /// </summary>
        public Boolean Allows(IPAddress Address, UInt16 Port, String? Host = null)
        {
            foreach (var rule in rules)
                if (rule.Matches(Address, Port, Host))
                    return rule.Action == NetworkAclAction.Allow;
            return DefaultAction == NetworkAclAction.Allow;
        }

        /// <summary>
        /// DNS-rebinding-safe evaluation: a hostname target is permitted only when <b>every</b> resolved
        /// address is permitted (and at least one address resolved). The caller must then dial exactly these
        /// addresses without re-resolving.
        /// </summary>
        public Boolean AllowsAll(IEnumerable<IIPAddress> Addresses, UInt16 Port, String? Host = null)
        {
            var any = false;
            foreach (var address in Addresses)
            {
                any = true;
                if (!Allows(address, Port, Host))
                    return false;
            }
            return any;
        }

        /// <summary>
        /// DNS-rebinding-safe evaluation over already-resolved <see cref="IPAddress"/>es.
        /// </summary>
        public Boolean AllowsAll(IEnumerable<IPAddress> Addresses, UInt16 Port, String? Host = null)
        {
            var any = false;
            foreach (var address in Addresses)
            {
                any = true;
                if (!Allows(address, Port, Host))
                    return false;
            }
            return any;
        }

        #endregion

        #region Presets

        /// <summary>
        /// Allow only loopback destinations (<c>127.0.0.0/8</c> and <c>::1</c>).
        /// </summary>
        public static NetworkAcl LoopbackOnly
            => DenyByDefault().Allow(Cidr: "127.0.0.0/8").Allow(Cidr: "::1/128");

        /// <summary>
        /// Allow only private / link-local networks (RFC 1918, ULA <c>fc00::/7</c>, link-local).
        /// </summary>
        public static NetworkAcl PrivateNetworksOnly
            => DenyByDefault()
                   .Allow(Cidr: "10.0.0.0/8")
                   .Allow(Cidr: "172.16.0.0/12")
                   .Allow(Cidr: "192.168.0.0/16")
                   .Allow(Cidr: "169.254.0.0/16")
                   .Allow(Cidr: "127.0.0.0/8")
                   .Allow(Cidr: "fc00::/7")
                   .Allow(Cidr: "fe80::/10")
                   .Allow(Cidr: "::1/128");

        /// <summary>
        /// Allow only destinations within the given subnet (any port).
        /// </summary>
        public static NetworkAcl Subnet(String Cidr)
            => DenyByDefault().Allow(Cidr: Cidr);

        #endregion

    }

    #endregion

}
