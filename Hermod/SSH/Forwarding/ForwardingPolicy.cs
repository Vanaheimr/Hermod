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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A server-side port-forwarding policy, gating the two forwarding directions with a
    /// <see cref="NetworkAcl"/> each: <see cref="DirectTcpIp"/> caps where a client may tunnel <i>to</i>
    /// (<c>ssh -L</c> / <c>direct-tcpip</c>, the <c>PermitOpen</c> equivalent) and <see cref="TcpIpForward"/>
    /// caps where the server may <i>listen</i> on the client's behalf (<c>ssh -R</c> / <c>tcpip-forward</c>,
    /// the <c>PermitListen</c> equivalent). A null ACL means that direction is entirely forbidden — the
    /// default is <see cref="None"/> (forwarding off).
    /// </summary>
    public sealed record ForwardingPolicy
    {

        /// <summary>
        /// The ACL for outbound <c>direct-tcpip</c> destinations, or null to forbid it.
        /// </summary>
        public NetworkAcl?  DirectTcpIp   { get; init; }

        /// <summary>
        /// The ACL for <c>tcpip-forward</c> listen (bind) addresses, or null to forbid it.
        /// </summary>
        public NetworkAcl?  TcpIpForward  { get; init; }


        #region Presets

        /// <summary>
        /// No forwarding at all (the default, and hard-off for SFTP-only profiles).
        /// </summary>
        public static ForwardingPolicy None
            => new ();

        /// <summary>
        /// Allow forwarding only to/from loopback (services on the SSH host itself).
        /// </summary>
        public static ForwardingPolicy LoopbackOnly
            => new () { DirectTcpIp = NetworkAcl.LoopbackOnly, TcpIpForward = NetworkAcl.LoopbackOnly };

        /// <summary>
        /// Allow <c>direct-tcpip</c> only to private / link-local networks.
        /// </summary>
        public static ForwardingPolicy PrivateNetworks
            => new () { DirectTcpIp = NetworkAcl.PrivateNetworksOnly };

        /// <summary>
        /// Allow <c>direct-tcpip</c> only within the given subnet.
        /// </summary>
        public static ForwardingPolicy Subnet(String Cidr)
            => new () { DirectTcpIp = NetworkAcl.Subnet(Cidr) };

        /// <summary>
        /// A custom policy from explicit ACLs.
        /// </summary>
        public static ForwardingPolicy Custom(NetworkAcl? DirectTcpIp = null, NetworkAcl? TcpIpForward = null)
            => new () { DirectTcpIp = DirectTcpIp, TcpIpForward = TcpIpForward };

        #endregion

        #region Decisions

        /// <summary>
        /// Whether a <c>direct-tcpip</c> to the given resolved addresses / port / host is permitted
        /// (DNS-rebinding safe — every resolved address must pass).
        /// </summary>
        public Boolean AllowsDirectTcpIp(IEnumerable<IIPAddress> ResolvedAddresses, UInt16 Port, String? Host = null)
            => DirectTcpIp?.AllowsAll(ResolvedAddresses, Port, Host) ?? false;

        /// <summary>
        /// Whether a single resolved <c>direct-tcpip</c> destination is permitted.
        /// </summary>
        public Boolean AllowsDirectTcpIp(IIPAddress Address, UInt16 Port, String? Host = null)
            => DirectTcpIp?.Allows(Address, Port, Host) ?? false;

        /// <summary>
        /// Whether a <c>tcpip-forward</c> listen on the given bind address / port is permitted.
        /// </summary>
        public Boolean AllowsListen(IIPAddress BindAddress, UInt16 Port)
            => TcpIpForward?.Allows(BindAddress, Port) ?? false;

        #endregion

    }

}
