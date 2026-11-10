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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The special-use names of RFC 6761 §6.1 to §6.4 and RFC 7686: those a name resolution library answers
    /// itself and never sends to a name server — "localhost.", "invalid." and "onion.", each with every name
    /// below it — and those an authoritative server answers negatively when it serves no zone for them —
    /// "test." and the private-address reverse-mapping domains, with every name below them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 6761 §6.3, item 3: "Name resolution APIs and libraries SHOULD recognize localhost names as special
    /// and SHOULD always return the IP loopback address for address queries and negative responses for all
    /// other query types. Name resolution APIs SHOULD NOT send queries for localhost names to their configured
    /// caching DNS server(s)." §6.4, item 3, asks the same of "invalid" names, with "immediate negative
    /// responses", and item 1 lets users assume that these are NXDOMAIN.
    /// </para>
    /// <para>
    /// RFC 7686 §2, item 3, asks more of the .onion names of the Tor network: "Resolvers MUST either respond to
    /// requests for .onion names by resolving them according to [tor-rendezvous] or by responding with
    /// NXDOMAIN". Hermod speaks no Tor, so they are NXDOMAIN, and no name server is asked.
    /// </para>
    /// <para>
    /// "test." and the private-address reverse names are special too, but not to a resolver: item 3 of §6.1
    /// and §6.2 asks resolution APIs to send them to their servers like any other name. Item 5 asks
    /// authoritative servers to answer them negatively "unless explicitly configured by the administrator to
    /// give positive answers" (see <see cref="AuthoritativeDNSRequestHandler"/>).
    /// </para>
    /// </remarks>
    public static class SpecialUseNames
    {

        #region Data

        /// <summary>
        /// How long an answer RFC 6761 makes is good for: as long as the cache keeps its entry for "localhost.".
        /// </summary>
        public static readonly TimeSpan TimeToLive = TimeSpan.FromDays(3650);

        #endregion


        #region IsLocalhost(Name)

        /// <summary>
        /// Whether the name is "localhost." or a name below it (RFC 6761 §6.3).
        /// </summary>
        /// <param name="Name">A name.</param>
        public static Boolean IsLocalhost(DNSServiceName Name)
            => EndsIn(Name, "localhost");

        #endregion

        #region IsInvalid  (Name)

        /// <summary>
        /// Whether the name is "invalid." or a name below it (RFC 6761 §6.4).
        /// </summary>
        /// <param name="Name">A name.</param>
        public static Boolean IsInvalid(DNSServiceName Name)
            => EndsIn(Name, "invalid");

        #endregion

        #region IsOnion    (Name)

        /// <summary>
        /// Whether the name is "onion." or a name below it, a name of the Tor network (RFC 7686).
        /// </summary>
        /// <param name="Name">A name.</param>
        public static Boolean IsOnion(DNSServiceName Name)
            => EndsIn(Name, "onion");

        #endregion

        #region IsTest     (Name)

        /// <summary>
        /// Whether the name is "test." or a name below it (RFC 6761 §6.2).
        /// </summary>
        /// <param name="Name">A name.</param>
        public static Boolean IsTest(DNSServiceName Name)
            => EndsIn(Name, "test");

        #endregion

        #region IsPrivateAddressReverse(Name)

        /// <summary>
        /// Whether the name is one of the reverse-mapping domains of the private address ranges of RFC 1918 —
        /// "10.in-addr.arpa.", "16.172.in-addr.arpa." to "31.172.in-addr.arpa." and "168.192.in-addr.arpa." —
        /// or a name below one (RFC 6761 §6.1).
        /// </summary>
        /// <param name="Name">A name.</param>
        public static Boolean IsPrivateAddressReverse(DNSServiceName Name)
            => PrivateAddressReverseDomain(Name) is not null;

        #endregion

        #region DomainOf(Name)

        /// <summary>
        /// The special-use domain of RFC 6761 §6.1 to §6.4 or RFC 7686 the name is in, in lower case and with the
        /// root's dot — "localhost.", "invalid.", "onion.", "test.", "10.in-addr.arpa." and so on — or null when
        /// it is in none.
        /// </summary>
        /// <param name="Name">A name.</param>
        public static String? DomainOf(DNSServiceName Name)
        {

            if (IsLocalhost(Name))
                return "localhost.";

            if (IsInvalid(Name))
                return "invalid.";

            if (IsOnion(Name))
                return "onion.";

            if (IsTest(Name))
                return "test.";

            return PrivateAddressReverseDomain(Name);

        }

        #endregion

        #region (private) EndsIn(Name, Label)

        // Names compare without case, and only A to Z have one (RFC 4343 §3): a label with a letter above
        // ASCII that folds to one of these is another label.
        private static Boolean EndsIn(DNSServiceName  Name,
                                      String          Label)

            => Name.Labels.Count > 0 &&
               DNSServiceName.ASCIICaseFold(Name.Labels[^1]).Equals(Label, StringComparison.Ordinal);

        #endregion

        #region (private) PrivateAddressReverseDomain(Name)

        private static readonly HashSet<String> privateSecondOctetsOf172 = [
            "16", "17", "18", "19", "20", "21", "22", "23",
            "24", "25", "26", "27", "28", "29", "30", "31"
        ];

        // A label is the octet only as the digits RFC 1035 §3.5 writes it: "016" is another label, and so
        // another name.
        private static String? PrivateAddressReverseDomain(DNSServiceName Name)
        {

            var labels = Name.Labels;

            if (labels.Count < 3                                                                          ||
                !DNSServiceName.ASCIICaseFold(labels[^1]).Equals("arpa",    StringComparison.Ordinal) ||
                !DNSServiceName.ASCIICaseFold(labels[^2]).Equals("in-addr", StringComparison.Ordinal))
            {
                return null;
            }

            var first = labels[^3];

            if (first == "10")
                return "10.in-addr.arpa.";

            if (labels.Count < 4)
                return null;

            var second = labels[^4];

            if (first == "172" && privateSecondOctetsOf172.Contains(second))
                return $"{second}.172.in-addr.arpa.";

            if (first == "192" && second == "168")
                return "168.192.in-addr.arpa.";

            return null;

        }

        #endregion


        #region LoopbackAddresses(Name, Types, TimeToLive)

        /// <summary>
        /// The records a localhost name has for the types asked: its A for A, its AAAA for AAAA, and both for ANY,
        /// which asks for every RRset the name has. Any other type has none (RFC 6761 §6.3).
        /// </summary>
        /// <param name="Name">A localhost name, which every record is owned by.</param>
        /// <param name="Types">The types asked for.</param>
        /// <param name="TimeToLive">The time to live of the records.</param>
        public static IReadOnlyList<IDNSResourceRecord> LoopbackAddresses(DNSServiceName                       Name,
                                                                          IEnumerable<DNSResourceRecordTypes>  Types,
                                                                          TimeSpan                             TimeToLive)
        {

            var types    = Types.ToHashSet();
            var answers  = new List<IDNSResourceRecord>();

            if (types.Contains(DNSResourceRecordTypes.A)    || types.Contains(DNSResourceRecordTypes.Any))
                answers.Add(new A   (Name, DNSQueryClasses.IN, TimeToLive, IPv4Address.Localhost));

            if (types.Contains(DNSResourceRecordTypes.AAAA) || types.Contains(DNSResourceRecordTypes.Any))
                answers.Add(new AAAA(Name, DNSQueryClasses.IN, TimeToLive, IPv6Address.Localhost));

            return answers;

        }

        #endregion


        #region TryAnswer(Name, Types, RecursionDesired, Timeout, Runtime, out Answer)

        /// <summary>
        /// The answer RFC 6761 and RFC 7686 give for a query about a special-use name, which no name server is
        /// asked for: for a localhost name the loopback addresses for A and AAAA, and NODATA for any other type;
        /// for an "invalid" name and a .onion name NXDOMAIN.
        /// </summary>
        /// <param name="Name">The name asked about.</param>
        /// <param name="Types">The types asked for; none is ANY.</param>
        /// <param name="RecursionDesired">Whether the query asked for recursion, which the answer copies back (RFC 1035 §4.1.1).</param>
        /// <param name="Timeout">The timeout of the query.</param>
        /// <param name="Runtime">How long the query ran.</param>
        /// <param name="Answer">The answer, when the name is a special-use name.</param>
        public static Boolean TryAnswer(DNSServiceName                         Name,
                                        IEnumerable<DNSResourceRecordTypes>    Types,
                                        Boolean                                RecursionDesired,
                                        TimeSpan                               Timeout,
                                        TimeSpan                               Runtime,
                                        [NotNullWhen(true)] out DNSInfo?       Answer)
        {

            // RFC 7686 §2, item 3: a resolver that does not resolve a .onion name over Tor responds "with
            // NXDOMAIN", as RFC 6761 §6.4 has it respond for an "invalid" name.
            if (IsInvalid(Name) || IsOnion(Name))
            {
                Answer = Made(DNSResponseCodes.NameError, [], RecursionDesired, Timeout, Runtime);
                return true;
            }

            if (IsLocalhost(Name))
            {

                var types = Types.ToList();

                if (types.Count == 0)
                    types.Add(DNSResourceRecordTypes.Any);

                Answer = Made(DNSResponseCodes.NoError, LoopbackAddresses(Name, types, TimeToLive), RecursionDesired, Timeout, Runtime);
                return true;

            }

            Answer = null;
            return false;

        }

        #endregion

        #region (private) Made(ResponseCode, Answers, RecursionDesired, Timeout, Runtime)

        // Authoritative, as the cache's entry for "localhost." is: the answer comes from the specification,
        // and no zone anybody serves could contradict it. Nothing was sent, so nothing was truncated, no
        // server offered recursion, and no clock ran out.
        private static DNSInfo Made(DNSResponseCodes                 ResponseCode,
                                    IEnumerable<IDNSResourceRecord>  Answers,
                                    Boolean                          RecursionDesired,
                                    TimeSpan                         Timeout,
                                    TimeSpan                         Runtime)

            => new (Origin:                 new DNSServerConfig(
                                                IPv4Address.Localhost,
                                                IPPort.DNS
                                            ),
                    QueryId:                0,
                    IsAuthoritativeAnswer:  true,
                    IsTruncated:            false,
                    RecursionDesired:       RecursionDesired,
                    RecursionAvailable:     false,
                    ResponseCode:           ResponseCode,
                    Answers:                Answers,
                    Authorities:            [],
                    AdditionalRecords:      [],
                    IsValid:                true,
                    IsTimeout:              false,
                    Timeout:                Timeout,
                    Runtime:                Runtime);

        #endregion

    }

}
