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

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Runtime configuration for a DNS server instance.
    /// </summary>
    public sealed class DNSServerOptions
    {

        public Boolean   EnableUDPUnicast       { get; init; } = true;

        public Boolean   EnableUDPMulticast     { get; init; } = true;

        public Boolean   EnableTCPUnicast       { get; init; } = true;

        public Boolean   EnableTLSUnicast       { get; init; } = false;

        public IPSocket  UDPUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.Parse(63));

        public IPSocket  TCPUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.Parse(63));

        public IPSocket  TLSUnicastSocket       { get; init; } = new(IPvXAddress.Any, IPPort.DNS_TLS);

        public IPSocket  UDPMulticastSocket     { get; init; } = new(IPvXAddress.Any, IPPort.Parse(6363));

        public String    MulticastGroupAddress  { get; init; } = "224.0.0.251";

        public Int32     TCPBacklog             { get; init; } = 100;

        public TimeSpan  TCPReadTimeout         { get; init; } = TimeSpan.FromSeconds(30);

        public X509Certificate2?  TLSServerCertificate  { get; init; }

        public SslProtocols      TLSProtocols           { get; init; } = SslProtocols.Tls13 | SslProtocols.Tls12;

        public Boolean           TLSClientCertificateRequired { get; init; } = false;

        public RemoteCertificateValidationCallback? TLSClientCertificateValidator { get; init; }

        public X509RevocationMode TLSCertificateRevocationCheckMode { get; init; } = X509RevocationMode.NoCheck;

        /// <summary>
        /// The TSIG keys this server accepts (RFC 8945).
        /// </summary>
        /// <remarks>
        /// Empty by default, which leaves TSIG entirely inactive: a request
        /// carrying a TSIG record is answered as though it did not, exactly as
        /// before. Configuring keys turns verification on, and from then on a
        /// signed request that fails to verify is answered NOTAUTH rather than
        /// served — an unsigned request is still served, because refusing those
        /// is a policy decision and not something RFC 8945 requires.
        /// </remarks>
        public IEnumerable<TSIGKey>  TSIGKeys      { get; init; } = [];

        /// <summary>
        /// The KEY records whose SIG(0) signatures this server accepts (RFC 2931).
        /// </summary>
        /// <remarks>
        /// Empty by default, and the default is conformant rather than lazy:
        /// §3.2 says a server that does not implement request SIGs "MUST ignore
        /// them without error where they are optional", and §3.1 adds that
        /// "servers are not required to check a request SIG(0)" outside the
        /// privileged operations — an update, a TKEY — that Hermod does not have.
        /// So an unconfigured server answers a signed query exactly as it answers
        /// an unsigned one.
        ///
        /// Configuring keys turns verification on, and from then on a request
        /// signed by a key that does not verify is refused. An *unsigned* request
        /// is still served: refusing those is a policy decision, and RFC 2931
        /// does not make it here.
        /// </remarks>
        public IEnumerable<KEY>      SIG0Keys      { get; init; } = [];

        /// <summary>
        /// The key this server signs its replies with when the request carried a
        /// SIG(0). Null leaves replies unsigned.
        /// </summary>
        /// <remarks>
        /// Off by default because RFC 2931 §3.1 makes response signing optional —
        /// "a DNS reply may be optionally signed" — where RFC 8945 §5.2 makes
        /// TSIG's mandatory. The asymmetry is not an oversight in either
        /// specification: a public-key signature per reply is the expensive
        /// operation §2.4 warns about spending freely, and a client that cannot
        /// check it gains nothing from it.
        /// </remarks>
        public SIG0Key?              SIG0ResponseKey  { get; init; }

        public Boolean   UseCompression         { get; init; } = false;

        /// <summary>
        /// The largest UDP response this server will emit, even when a requestor
        /// advertises a bigger EDNS0 buffer. The default of 1232 bytes follows the
        /// DNS Flag Day 2020 recommendation and keeps responses below common path
        /// MTUs, so they do not fragment. Larger answers are truncated with TC=1,
        /// which tells the client to retry over TCP (RFC 1035 §4.2.1).
        /// </summary>
        public UInt16    MaxUDPResponseSize     { get; init; } = 1232;

    }

}
