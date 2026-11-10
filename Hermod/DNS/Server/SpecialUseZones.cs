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

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// What an authoritative server answers for the special-use names of RFC 6761 §6.1 to §6.4, RFC 6762 §22.1
    /// and RFC 7686: the localhost, "invalid" and .onion names whatever it serves, the link-local names unless
    /// told to serve them, and the test names and private-address reverse names when it serves no zone for
    /// them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §6.3, item 5: "Authoritative DNS servers SHOULD recognize localhost names as special and handle them as
    /// described above for caching DNS servers" — the loopback address for an address query, and "an immediate
    /// negative response" for any other type. §6.4, item 5, says the same of "invalid" names, which are
    /// NXDOMAIN. Item 6 of both: the answers are "defined by protocol specification" and "cannot be modified by
    /// local configuration", so they are given before the zone is asked.
    /// </para>
    /// <para>
    /// RFC 7686 §2, item 5: "Authoritative servers MUST respond to queries for .onion with NXDOMAIN." Item 6
    /// forbids operators to configure an answer, so a .onion name is NXDOMAIN before the zone is asked, too.
    /// </para>
    /// <para>
    /// RFC 6762 §22.1, item 5, has an authoritative server answer the link-local names of Multicast DNS with
    /// NXDOMAIN and not be configurable to answer them by default, and lets server software "provide a
    /// configuration option to override this default". That option is
    /// <see cref="AuthoritativeDNSRequestHandler.ServeLinkLocalNames"/>; a query that arrived by multicast is
    /// answered from the zone either way.
    /// </para>
    /// <para>
    /// §6.1 and §6.2, item 5, ask for "immediate negative responses" for the private-address reverse names and
    /// the test names, "unless explicitly configured by the administrator to give positive answers". A name the
    /// zone serves is that configuration, so these answers are given only where the zone is not authoritative,
    /// and in place of the REFUSED that is the answer for every other such name.
    /// </para>
    /// <para>
    /// A negative answer is NXDOMAIN, unless the zone holds a name below the one asked: NXDOMAIN would deny that
    /// one too (RFC 8020 §2), and the answer is NODATA. Each negative answer cites the SOA of the special-use
    /// domain, as RFC 2308 §3 asks of every negative answer, so that it may be cached. The SOA is the one RFC
    /// 6303 §3 gives the empty zones a resolver serves for such domains: the domain as its own primary server,
    /// "nobody.invalid." as the mailbox, and three hours for the record, for the negative answers and for the
    /// addresses of a localhost name.
    /// </para>
    /// </remarks>
    internal static class SpecialUseZones
    {

        #region Data

        /// <summary>
        /// The time to live of every record given here, and the negative TTL of the SOA: RFC 6303 §3's 10800
        /// seconds, which "SHOULD match".
        /// </summary>
        internal static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(10800);

        #endregion


        #region FixedBySpecification(Question)

        /// <summary>
        /// The answer for a localhost, an "invalid" or a .onion name, which no zone changes, and for a link-local
        /// name when those are not served; null for any other name.
        /// </summary>
        /// <param name="Question">The question.</param>
        /// <param name="AnswerLinkLocalNames">Whether a link-local name is answered NXDOMAIN rather than from the zone (RFC 6762 §22.1, item 5).</param>
        internal static DNSZoneLookupResult? FixedBySpecification(DNSQuestion  Question,
                                                                  Boolean      AnswerLinkLocalNames   = true)
        {

            var name = Question.DomainName;

            if (!IsInternetClass(Question))
                return null;

            if (SpecialUseNames.IsInvalid(name) ||
                SpecialUseNames.IsOnion  (name) ||
                (AnswerLinkLocalNames && SpecialUseNames.IsLinkLocal(name)))
            {
                return DNSZoneLookupResult.NameError([ StartOfAuthority(SpecialUseNames.DomainOf(name)!) ]);
            }

            if (SpecialUseNames.IsLocalhost(name))
            {

                var addresses = SpecialUseNames.LoopbackAddresses(name, [ Question.QueryType ], TimeToLive);

                return addresses.Count > 0
                           ? DNSZoneLookupResult.Found (addresses)
                           : DNSZoneLookupResult.NoData([ StartOfAuthority("localhost.") ]);

            }

            return null;

        }

        #endregion

        #region NegativeUnlessServed(Question, HoldsNamesBelow)

        /// <summary>
        /// The negative answer for a test name or a private-address reverse name the zone is not authoritative
        /// for; null for any other name.
        /// </summary>
        /// <param name="Question">The question.</param>
        /// <param name="HoldsNamesBelow">Whether the zone holds names below the one asked.</param>
        internal static DNSZoneLookupResult? NegativeUnlessServed(DNSQuestion  Question,
                                                                  Boolean      HoldsNamesBelow)
        {

            var name = Question.DomainName;

            if (!IsInternetClass(Question))
                return null;

            if (!SpecialUseNames.IsTest(name) && !SpecialUseNames.IsPrivateAddressReverse(name))
                return null;

            var startOfAuthority = StartOfAuthority(SpecialUseNames.DomainOf(name)!);

            return HoldsNamesBelow
                       ? DNSZoneLookupResult.NoData   ([ startOfAuthority ])
                       : DNSZoneLookupResult.NameError([ startOfAuthority ]);

        }

        #endregion


        #region (private) IsInternetClass(Question)

        // The special-use names are names of the Internet class; a question in another one is the zone's.
        private static Boolean IsInternetClass(DNSQuestion Question)

            => Question.QueryClass is DNSQueryClasses.IN
                                   or DNSQueryClasses.ANY;

        #endregion

        #region (private) StartOfAuthority(Domain)

        // RFC 6303 §3: "@ 10800 IN SOA @ nobody.invalid. 1 3600 1200 604800 10800".
        private static SOA StartOfAuthority(String Domain)

            => new (DomainName.Parse(Domain),
                    DNSQueryClasses.IN,
                    TimeToLive,
                    DomainName.Parse(Domain),
                    SimpleEMailAddress.Parse("nobody@invalid"),
                    1,
                    TimeSpan.FromSeconds(3600),
                    TimeSpan.FromSeconds(1200),
                    TimeSpan.FromSeconds(604800),
                    TimeToLive);

        #endregion

    }

}
