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
    /// The special-use names a name resolution library answers itself and never sends to a name server:
    /// "localhost." and every name below it, and "invalid." and every name below it (RFC 6761 §6.3, §6.4).
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
    /// "test." is special too, but not to a resolver: §6.2, item 3, asks resolution APIs to send test names to
    /// their servers like any other name.
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

        #region (private) EndsIn(Name, Label)

        // Names compare without case, and only A to Z have one (RFC 4343 §3): a label with a letter above
        // ASCII that folds to one of these is another label.
        private static Boolean EndsIn(DNSServiceName  Name,
                                      String          Label)

            => Name.Labels.Count > 0 &&
               DNSServiceName.ASCIICaseFold(Name.Labels[^1]).Equals(Label, StringComparison.Ordinal);

        #endregion


        #region TryAnswer(Name, Types, RecursionDesired, Timeout, Runtime, out Answer)

        /// <summary>
        /// The answer RFC 6761 gives for a query about a special-use name, which no name server is asked for:
        /// for a localhost name the loopback addresses for A and AAAA, and NODATA for any other type; for an
        /// "invalid" name NXDOMAIN.
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

            if (IsInvalid(Name))
            {
                Answer = Made(DNSResponseCodes.NameError, [], RecursionDesired, Timeout, Runtime);
                return true;
            }

            if (IsLocalhost(Name))
            {

                var types    = Types.Distinct().ToList();
                var answers  = new List<IDNSResourceRecord>();

                if (types.Count == 0)
                    types.Add(DNSResourceRecordTypes.Any);

                // ANY asks for every RRset the name has, and a localhost name has the two.
                if (types.Contains(DNSResourceRecordTypes.A) || types.Contains(DNSResourceRecordTypes.Any))
                    answers.Add(new A   (Name, DNSQueryClasses.IN, TimeToLive, IPv4Address.Localhost));

                if (types.Contains(DNSResourceRecordTypes.AAAA) || types.Contains(DNSResourceRecordTypes.Any))
                    answers.Add(new AAAA(Name, DNSQueryClasses.IN, TimeToLive, IPv6Address.Localhost));

                Answer = Made(DNSResponseCodes.NoError, answers, RecursionDesired, Timeout, Runtime);
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
