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
    /// DNS Resource Record types.
    /// </summary>
    public enum DNSResourceRecordTypes : UInt16
    {

        // Pseudo Types

        /// <summary>
        /// OPT record, which is used to carry additional information in DNS messages, such as EDNS0 options.
        /// </summary>
        OPT          = 41,

        /// <summary>
        /// Transaction Key record (RFC 2930), used for key exchange for TSIG.
        /// </summary>
        TKEY         = 249,

        /// <summary>
        /// Transaction Signature record (RFC 8945), used for authenticating dynamic updates and zone transfers.
        /// </summary>
        TSIG         = 250,

        /// <summary>
        /// Incremental zone transfer record, which is used to transfer only the changes made to a zone file since the last transfer.
        /// </summary>
        IXFR         = 251,

        /// <summary>
        /// Authoritative transfer record, which is used to transfer a complete zone file from a primary DNS server to a secondary DNS server.
        /// </summary>
        AXFR         = 252,

        /// <summary>
        /// Any record type, which matches any DNS record within DNS queries.
        /// </summary>
        Any          = 255,


        // Standard Types

        /// <summary>
        /// IPv4 address record (RFC 1035).
        /// </summary>
        A            = 1,

        /// <summary>
        /// Name server record (RFC 1035).
        /// </summary>
        NS           = 2,

        /// <summary>
        /// Canonical Name record (RFC 1035), which is an alias for another domain name.
        /// </summary>
        CNAME        = 5,

        /// <summary>
        /// Start of Authority record (RFC 1035), which provides information about the DNS zone.
        /// </summary>
        SOA          = 6,

        /// <summary>
        /// Pointer record (RFC 1035), which points to a canonical name, e.g. used for reverse DNS lookups.
        /// </summary>
        PTR          = 12,

        /// <summary>
        /// Host Information record (RFC 1035), which stores CPU and OS information for the host.
        /// </summary>
        HINFO        = 13,

        /// <summary>
        /// Mail Exchange record (RFC 1035), which specifies the mail server responsible for receiving email for a domain.
        /// </summary>
        MX           = 15,

        /// <summary>
        /// Text record (RFC 1035), which can hold arbitrary text information, often used for domain verification or SPF records.
        /// </summary>
        TXT          = 16,

        /// <summary>
        /// Responsible Person record (RFC 1183), which specifies a mailbox and a TXT domain name for contact information.
        /// </summary>
        RP           = 17,

        /// <summary>
        /// AFS Database record (RFC 1183), which maps a domain name to an AFS cell database server.
        /// </summary>
        AFSDB        = 18,

        /// <summary>
        /// IPv6 address record (RFC 3596).
        /// </summary>
        AAAA         = 28,

        /// <summary>
        /// Location record (RFC 1876), which stores geographic location information.
        /// </summary>
        LOC          = 29,

        /// <summary>
        /// DNS Service record (RFC 2782), which specifies the hostname and port of highly available services.
        /// </summary>
        SRV          = 33,

        /// <summary>
        /// NAPTR record (RFC 3403), which maps domain names to URIs and other resources.
        /// </summary>
        NAPTR        = 35,

        /// <summary>
        /// Certificate record (RFC 4398), which stores certificates (PKIX, SPKI, PGP, etc.) in DNS.
        /// </summary>
        CERT         = 37,

        /// <summary>
        /// DNAME record (RFC 6672), which provides redirection for an entire subtree of the domain name tree.
        /// </summary>
        DNAME        = 39,


        // DNSSEC Types (RFC 4033/4034/4035)

        /// <summary>
        /// Delegation Signer record (RFC 4034), the bridge between parent and child zone in DNSSEC.
        /// </summary>
        DS           = 43,

        /// <summary>
        /// SSH Fingerprint record (RFC 4255), which stores the public key fingerprints for SSH servers.
        /// </summary>
        SSHFP        = 44,

        /// <summary>
        /// Resource Record Signature (RFC 4034), the core DNSSEC signature over an RRSet.
        /// </summary>
        RRSIG        = 46,

        /// <summary>
        /// Next Secure record (RFC 4034), used for authenticated denial of existence in DNSSEC.
        /// </summary>
        NSEC         = 47,

        /// <summary>
        /// DNS Public Key record (RFC 4034), stores the public key used for DNSSEC zone signing.
        /// </summary>
        DNSKEY       = 48,

        /// <summary>
        /// NSEC3 record (RFC 5155), hashed authenticated denial of existence (prevents zone walking).
        /// </summary>
        NSEC3        = 50,

        /// <summary>
        /// NSEC3 Parameters record (RFC 5155), stores the NSEC3 hashing parameters for a zone.
        /// </summary>
        NSEC3PARAM   = 51,

        /// <summary>
        /// TLSA record (RFC 6698), associates a TLS certificate with a domain name (DANE).
        /// </summary>
        TLSA         = 52,

        /// <summary>
        /// S/MIME Certificate Association record (RFC 8162), associates S/MIME certificates with email addresses.
        /// </summary>
        SMIMEA       = 53,

        /// <summary>
        /// Child DS record (RFC 7344), used for automated DNSSEC key rotation between child and parent zone.
        /// </summary>
        CDS          = 59,

        /// <summary>
        /// Child DNSKEY record (RFC 7344), used for automated DNSSEC key rotation between child and parent zone.
        /// </summary>
        CDNSKEY      = 60,

        /// <summary>
        /// OpenPGP Public Key record (RFC 7929), stores OpenPGP public keys in DNS.
        /// </summary>
        OPENPGPKEY   = 61,

        /// <summary>
        /// Child-to-Parent Synchronization record (RFC 7477), for synchronizing NS and DS records.
        /// </summary>
        CSYNC        = 62,

        /// <summary>
        /// Zone Message Digest record (RFC 8976), provides integrity verification for DNS zones.
        /// </summary>
        ZONEMD       = 63,

        /// <summary>
        /// Service Binding record (RFC 9460), which specifies the hostname and port of a service.
        /// </summary>
        SVCB         = 64,

        /// <summary>
        /// HTTPS record (RFC 9460), which specifies the hostname and port of a secure web service.
        /// </summary>
        HTTPS        = 65,

        /// <summary>
        /// Sender Policy Framework record (RFC 7208), which specifies the mail servers allowed to send email for a domain.
        /// </summary>
        SPF          = 99,

        /// <summary>
        /// EUI-48 address record (RFC 7043), stores a 48-bit MAC address.
        /// </summary>
        EUI48        = 108,

        /// <summary>
        /// EUI-64 address record (RFC 7043), stores a 64-bit MAC address.
        /// </summary>
        EUI64        = 109,

        /// <summary>
        /// DNS URI record (RFC 7553), which specifies a URI of highly available services.
        /// </summary>
        URI          = 256,

        /// <summary>
        /// Certification Authority Authorization record (RFC 8659), specifies which CAs may issue certificates for a domain.
        /// </summary>
        CAA          = 257


        // SFP => use TXT!


    }

}
