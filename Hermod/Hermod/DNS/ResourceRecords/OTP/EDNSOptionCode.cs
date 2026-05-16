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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// EDNS0 option codes as defined by IANA.
    /// https://www.iana.org/assignments/dns-parameters/dns-parameters.xhtml#dns-parameters-11
    /// </summary>
    public enum EDNSOptionCode : UInt16
    {

        /// <summary>
        /// Name Server Identifier (RFC 5001).
        /// </summary>
        NSID              =  3,

        /// <summary>
        /// EDNS Client Subnet (RFC 7871).
        /// Allows a recursive resolver to forward a truncated client IP address
        /// to the authoritative server for geo-aware / CDN-optimized responses.
        /// </summary>
        ClientSubnet      =  8,

        /// <summary>
        /// DNS COOKIE (RFC 7873).
        /// Lightweight transaction authentication to protect against
        /// off-path spoofing and DNS amplification attacks.
        /// </summary>
        Cookie            = 10,

        /// <summary>
        /// TCP Keepalive (RFC 7828).
        /// Allows a server to signal the idle timeout for persistent
        /// TCP/TLS connections (EDNS TCP Keepalive).
        /// </summary>
        Keepalive         = 11,

        /// <summary>
        /// Padding (RFC 7830).
        /// Adds padding bytes to DNS-over-TLS / DNS-over-HTTPS messages
        /// to prevent traffic analysis based on message length.
        /// </summary>
        Padding           = 12,

        /// <summary>
        /// Extended DNS Error (RFC 8914).
        /// Provides additional error information beyond the basic RCODE,
        /// e.g. DNSSEC validation failure details, stale data, policy blocks.
        /// </summary>
        ExtendedDNSError  = 15

    }

}
