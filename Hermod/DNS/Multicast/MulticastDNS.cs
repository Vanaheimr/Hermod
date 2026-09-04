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
    /// Constants and helpers of Multicast DNS (RFC 6762) and DNS-Based Service Discovery (RFC 6763).
    /// </summary>
    public static class MulticastDNS
    {

        #region Addresses and names

        /// <summary>
        /// The UDP port of Multicast DNS (RFC 6762 §2).
        /// </summary>
        public static readonly IPPort       Port                            = IPPort.Parse(5353);

        /// <summary>
        /// The IPv4 multicast group of Multicast DNS (RFC 6762 §3).
        /// </summary>
        public static readonly IPv4Address  IPv4Group                       = IPv4Address.Parse("224.0.0.251");

        /// <summary>
        /// The IPv6 link-local multicast group of Multicast DNS (RFC 6762 §3).
        /// </summary>
        public static readonly IPv6Address  IPv6Group                       = IPv6Address.Parse("ff02::fb");

        /// <summary>
        /// The link-local domain of Multicast DNS (RFC 6762 §3).
        /// </summary>
        public static readonly DomainName   LocalDomain                     = DomainName.Parse("local.");

        /// <summary>
        /// The IP time-to-live / hop limit of multicast DNS packets (RFC 6762 §11).
        /// </summary>
        public const           Int32        MulticastTimeToLive             = 255;

        /// <summary>
        /// The maximum size of a Multicast DNS packet (RFC 6762 §17).
        /// </summary>
        public const           Int32        MaxPacketSize                   = 9000;

        /// <summary>
        /// The recommended maximum size of a Multicast DNS packet on Ethernet
        /// (an IPv4 datagram fitting into the interface MTU, RFC 6762 §17).
        /// </summary>
        public const           Int32        PreferredMaxPacketSize          = 1472;

        #endregion

        #region Wire format bits

        /// <summary>
        /// The "unicast-response" bit in the class field of a question (RFC 6762 §5.4).
        /// </summary>
        public const           UInt16       UnicastResponseBit              = 0x8000;

        /// <summary>
        /// The "cache-flush" bit in the class field of a resource record (RFC 6762 §10.2).
        /// </summary>
        public const           UInt16       CacheFlushBit                   = 0x8000;

        /// <summary>
        /// The mask of the class field without the unicast-response or cache-flush bit.
        /// </summary>
        public const           UInt16       ClassMask                       = 0x7FFF;

        #endregion

        #region Time-to-live recommendations (RFC 6762 §10)

        /// <summary>
        /// The recommended time-to-live of records containing a host name (A, AAAA, SRV, HINFO): 120 seconds.
        /// </summary>
        public static readonly TimeSpan     HostRecordTimeToLive            = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The recommended time-to-live of other records (PTR, TXT): 75 minutes.
        /// </summary>
        public static readonly TimeSpan     SharedRecordTimeToLive          = TimeSpan.FromMinutes(75);

        /// <summary>
        /// The maximum time-to-live of a record within a legacy unicast response (RFC 6762 §6.7): 10 seconds.
        /// </summary>
        public static readonly TimeSpan     MaxLegacyUnicastTimeToLive      = TimeSpan.FromSeconds(10);

        #endregion

        #region Timing (RFC 6762 §6, §8)

        /// <summary>
        /// The number of probes before a unique record is announced (RFC 6762 §8.1).
        /// </summary>
        public const           Int32        DefaultProbeCount               = 3;

        /// <summary>
        /// The interval between two probes (RFC 6762 §8.1): 250 ms.
        /// </summary>
        public static readonly TimeSpan     DefaultProbeInterval            = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// The number of announcements after probing (RFC 6762 §8.3): at least two.
        /// </summary>
        public const           Int32        DefaultAnnouncementCount        = 2;

        /// <summary>
        /// The interval between two announcements (RFC 6762 §8.3): one second.
        /// </summary>
        public static readonly TimeSpan     DefaultAnnouncementInterval     = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The minimum delay of a response containing shared records (RFC 6762 §6): 20 ms.
        /// </summary>
        public static readonly TimeSpan     MinSharedResponseDelay          = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// The maximum delay of a response containing shared records (RFC 6762 §6): 120 ms.
        /// </summary>
        public static readonly TimeSpan     MaxSharedResponseDelay          = TimeSpan.FromMilliseconds(120);

        /// <summary>
        /// The minimum delay before answering a query whose known-answer list was truncated (RFC 6762 §7.2): 400 ms.
        /// </summary>
        public static readonly TimeSpan     MinTruncatedQueryDelay          = TimeSpan.FromMilliseconds(400);

        /// <summary>
        /// The maximum delay before answering a query whose known-answer list was truncated (RFC 6762 §7.2): 500 ms.
        /// </summary>
        public static readonly TimeSpan     MaxTruncatedQueryDelay          = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The minimum interval between two multicasts of the same record (RFC 6762 §6): one second.
        /// </summary>
        public static readonly TimeSpan     MinRecordMulticastInterval      = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The initial interval between two repeated queries of a continuous querier (RFC 6762 §5.2): one second.
        /// </summary>
        public static readonly TimeSpan     InitialContinuousQueryInterval  = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The maximum interval between two repeated queries of a continuous querier (RFC 6762 §5.2): 60 minutes.
        /// </summary>
        public static readonly TimeSpan     MaxContinuousQueryInterval      = TimeSpan.FromMinutes(60);

        /// <summary>
        /// The delay after which a record announced with a time-to-live of zero
        /// (a "goodbye" packet) is removed from a cache (RFC 6762 §10.1): one second.
        /// </summary>
        public static readonly TimeSpan     GoodbyeDelay                    = TimeSpan.FromSeconds(1);

        #endregion


        #region IsLocalName(Name)

        /// <summary>
        /// Whether the given name belongs to the link-local Multicast DNS domains
        /// (".local", "254.169.in-addr.arpa" and "8.e.f.ip6.arpa" … "b.e.f.ip6.arpa", RFC 6762 §3 and §12).
        /// </summary>
        /// <param name="Name">A domain name.</param>
        public static Boolean IsLocalName(IDomainName Name)
        {

            ArgumentNullException.ThrowIfNull(Name);

            return IsLocalName(Name.FullName);

        }

        /// <summary>
        /// Whether the given name belongs to the link-local Multicast DNS domains
        /// (".local", "254.169.in-addr.arpa" and "8.e.f.ip6.arpa" … "b.e.f.ip6.arpa", RFC 6762 §3 and §12).
        /// </summary>
        /// <param name="Name">A domain name.</param>
        public static Boolean IsLocalName(String Name)
        {

            if (String.IsNullOrWhiteSpace(Name))
                return false;

            var name = Name.Trim().TrimEnd('.').ToLowerInvariant();

            return name == "local"                             ||
                   name.EndsWith(".local",                     StringComparison.Ordinal) ||
                   name.EndsWith("254.169.in-addr.arpa",       StringComparison.Ordinal) ||
                   name.EndsWith("8.e.f.ip6.arpa",             StringComparison.Ordinal) ||
                   name.EndsWith("9.e.f.ip6.arpa",             StringComparison.Ordinal) ||
                   name.EndsWith("a.e.f.ip6.arpa",             StringComparison.Ordinal) ||
                   name.EndsWith("b.e.f.ip6.arpa",             StringComparison.Ordinal);

        }

        #endregion

        #region IsUniqueRecordType(Type)

        /// <summary>
        /// Whether records of the given type are normally unique to one host (RFC 6762 §2,
        /// e.g. A, AAAA, SRV, TXT, HINFO) rather than shared between hosts (PTR).
        /// Unique records are probed before they are announced and carry the
        /// cache-flush bit when they are multicast.
        /// </summary>
        /// <param name="Type">A resource record type.</param>
        public static Boolean IsUniqueRecordType(DNSResourceRecordTypes Type)

            => Type != DNSResourceRecordTypes.PTR &&
               Type != DNSResourceRecordTypes.NSEC;

        #endregion

        #region IsHostRecordType(Type)

        /// <summary>
        /// Whether records of the given type contain a host name and therefore use the
        /// shorter recommended time-to-live of 120 seconds (RFC 6762 §10).
        /// </summary>
        /// <param name="Type">A resource record type.</param>
        public static Boolean IsHostRecordType(DNSResourceRecordTypes Type)

            => Type is DNSResourceRecordTypes.A     or
                       DNSResourceRecordTypes.AAAA  or
                       DNSResourceRecordTypes.SRV   or
                       DNSResourceRecordTypes.HINFO;

        #endregion

        #region DefaultTimeToLive(Type)

        /// <summary>
        /// The recommended time-to-live of records of the given type (RFC 6762 §10).
        /// </summary>
        /// <param name="Type">A resource record type.</param>
        public static TimeSpan DefaultTimeToLive(DNSResourceRecordTypes Type)

            => IsHostRecordType(Type)
                   ? HostRecordTimeToLive
                   : SharedRecordTimeToLive;

        #endregion

    }

}
