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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// DNS Resource Record types.
    /// </summary>
    public enum DNSResourceRecordTypes : UInt16
    {

        // Pseudo Types

        /// <summary>
        /// OPT record, which is used to carry additional information in DNS messages, such as EDNS0 options.
        /// </summary>
        OPT     = 41,

        /// <summary>
        /// Incremental zone transfer record, which is used to transfer only the changes made to a zone file since the last transfer.
        /// </summary>
        IXFR    = 251,

        /// <summary>
        /// Authoritative transfer record, which is used to transfer a complete zone file from a primary DNS server to a secondary DNS server.
        /// </summary>
        AXFR    = 252,

        /// <summary>
        /// Any record type, which matches any DNS record within DNS queries.
        /// </summary>
        Any     = 255,


        // Standard Types

        /// <summary>
        /// IPv4 address record.
        /// </summary>
        A       = 1,

        /// <summary>
        /// Name server record.
        /// </summary>
        NS      = 2,

        /// <summary>
        /// Canonical Name record, which is an alias for another domain name.
        /// </summary>
        CNAME   = 5,

        /// <summary>
        /// Start of Authority record, which provides information about the DNS zone.
        /// </summary>
        SOA     = 6,

        /// <summary>
        /// Pointer record, which points to a canonical name, e.g. used for reverse DNS lookups.
        /// </summary>
        PTR     = 12,

        /// <summary>
        /// Mail Exchange record, which specifies the mail server responsible for receiving email for a domain.
        /// </summary>
        MX      = 15,

        /// <summary>
        /// Text record, which can hold arbitrary text information, often used for domain verification or SPF records.
        /// </summary>
        TXT     = 16,

        /// <summary>
        /// IPv6 address record.
        /// </summary>
        AAAA    = 28,

        /// <summary>
        /// DNS Service record, which specifies the hostname and port of highly available services.
        /// </summary>
        SRV     = 33,

        /// <summary>
        /// NAPTR record maps domain names to URIs and other resources.
        /// </summary>
        NAPTR   = 35,

        /// <summary>
        /// SSH Fingerprint record, which stores the public key fingerprints for SSH servers.
        /// </summary>
        SSHFP   = 44,

        /// <summary>
        /// Service Binding record, which specifies the hostname and port of a service.
        /// </summary>
        SVCB    = 64,

        /// <summary>
        /// HTTPS record, which specifies the hostname and port of a secure web service.
        /// </summary>
        HTTPS   = 65,

        /// <summary>
        /// Sender Policy Framework record, which specifies the mail servers allowed to send email for a domain.
        /// </summary>
        SPF     = 99,

        /// <summary>
        /// DNS URI record, which specifies a URI of highly available services.
        /// </summary>
        URI     = 256


        // SFP => use TXT!


    }

}
