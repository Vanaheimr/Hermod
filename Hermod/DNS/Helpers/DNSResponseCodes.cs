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
    /// Query Result/Response Codes
    /// </summary>
    public enum DNSResponseCodes : Int32
    {

        NoError         = 0,
        FormatError     = 1,
        ServerFailure   = 2,
        NameError       = 3, // NXDOMAIN
        NotImplemented  = 4,
        Refused         = 5,

        /// <summary>
        /// The name exists when it should not (RFC 2136 §2.2) — and, for a server
        /// that has no dynamic updates at all, the answer RFC 6672 §2.2 requires
        /// when a DNAME substitution would produce a name longer than the 255
        /// octets a domain name has room for. The DNAME that could not be applied
        /// travels in the answer section as the proof.
        /// </summary>
        YXDomain        = 6,

        /// <summary>
        /// An RRset exists when it should not (RFC 2136 §2.2).
        /// </summary>
        YXRRSet         = 7,

        /// <summary>
        /// An RRset that should exist does not (RFC 2136 §2.2).
        /// </summary>
        NXRRSet         = 8,

        /// <summary>
        /// Not authoritative / not authorized (RFC 8945 §5.2). A TSIG-signed
        /// request that fails verification is answered with this, and the reason
        /// travels in the TSIG record's own Error field rather than here.
        /// </summary>
        NotAuthorized   = 9,

        /// <summary>
        /// The name is not contained in the zone (RFC 2136 §2.2).
        /// </summary>
        NotZone         = 10,

        /// <summary>
        /// Bad EDNS version (RFC 6891 §9). An extended RCODE: the low 4 bits
        /// travel in the message header, the upper 8 in the OPT record's TTL
        /// field, so this value only reaches the wire when an OPT is present.
        /// </summary>
        BadVersion      = 16

    }

}
