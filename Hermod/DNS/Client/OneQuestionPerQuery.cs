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

using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Several record types asked for as several queries of one question each,
    /// and the answers put back together as one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 9619 §4, which updates RFC 1035: "A DNS message with OPCODE = 0 MUST NOT
    /// include a QDCOUNT parameter whose value is greater than 1." Its reason, in §1:
    /// response fields "such as AA and RCODE have no defined meaning when the message
    /// contains multiple queries as there is no way to signal which question those
    /// parameters relate to." And a server following it answers such a query with
    /// FORMERR, so a caller asking for two types in one message gets neither.
    /// </para>
    /// <para>
    /// Every client takes a list of types in the same <see cref="IDNSClient"/>
    /// signature. <see cref="DNSClient"/> split it per type from the start; the UDP,
    /// TCP, TLS and HTTPS clients made one message of the whole list. The split and
    /// the merge live here so that there is one of each.
    /// </para>
    /// </remarks>
    internal static class OneQuestionPerQuery
    {

        #region AskEachTypeAsync(ResourceRecordTypes, Ask, Timeout)

        /// <summary>
        /// Ask for each of the given types with a query of its own, all of them at
        /// once, and merge the answers.
        /// </summary>
        /// <param name="ResourceRecordTypes">The types asked for, more than one, without repeats.</param>
        /// <param name="Ask">How to ask for one of them.</param>
        /// <param name="Timeout">The timeout the caller gave, if any.</param>
        /// <remarks>
        /// At once rather than one after another, so asking for two types costs what
        /// asking for one costs. A client with one connection to share serialises the
        /// queries on its own lock, which is where that belongs.
        /// </remarks>
        public static async Task<DNSInfo> AskEachTypeAsync(IReadOnlyList<DNSResourceRecordTypes>         ResourceRecordTypes,
                                                           Func<DNSResourceRecordTypes, Task<DNSInfo>>  Ask,
                                                           TimeSpan?                                     Timeout)
        {

            var stopwatch = Stopwatch.StartNew();

            var responses = await Task.WhenAll(ResourceRecordTypes.Select(Ask)).
                                       ConfigureAwait(false);

            stopwatch.Stop();

            return Merge(responses, Timeout, stopwatch.Elapsed);

        }

        #endregion

        #region Merge(Responses, Timeout, Runtime)

        /// <summary>
        /// One answer out of the answers to several one-question queries.
        /// </summary>
        /// <param name="Responses">The answers, at least one.</param>
        /// <param name="Timeout">The timeout the caller gave, if any.</param>
        /// <param name="Runtime">How long all of them took together.</param>
        public static DNSInfo Merge(IReadOnlyList<DNSInfo>  Responses,
                                    TimeSpan?               Timeout,
                                    TimeSpan                Runtime)
        {

            // One of them speaks for the whole, and it is one that answered: its
            // server is the origin, its id the id, and its additional section the
            // additional section. There is no such thing as the union of several
            // additional sections - an OPT record belongs to a single message and
            // must not be carried out of it (RFC 6891 §6.1.1) - so the honest thing
            // is to hand on one message's, not a pile of them.
            var primary = Responses.FirstOrDefault(response => response.ResponseCode == DNSResponseCodes.NoError)
                              ?? Responses[0];

            return new DNSInfo(

                       Origin:                 primary.Origin,
                       QueryId:                primary.QueryId,

                       // Only when every one of them was: half an authoritative
                       // picture is not an authoritative picture.
                       IsAuthoritativeAnswer:  Responses.All(response => response.AuthoritativeAnswer),

                       // Any of them, because a caller that has to retry over TCP
                       // has to retry whatever the reason.
                       IsTruncated:            Responses.Any(response => response.IsTruncated),

                       RecursionDesired:       primary.RecursionRequested,
                       RecursionAvailable:     Responses.Any(response => response.RecursionAvailable),
                       ResponseCode:           primary.ResponseCode,

                       Answers:                WithoutRepeats(Responses.SelectMany(response => response.Answers)),
                       Authorities:            WithoutRepeats(Responses.SelectMany(response => response.Authorities)),
                       AdditionalRecords:      primary.AdditionalRecords,

                       IsValid:                Responses.Any(response => response.IsValid),

                       // Only when nothing came back at all. One type timing out
                       // while another answered is an answer, and saying "timeout"
                       // over it would throw the answer away.
                       IsTimeout:              Responses.All(response => response.IsTimeout),

                       // The one it was given, or else the longest a server was
                       // given - which is what a single type says, too.
                       Timeout:                Timeout ?? Responses.Max(response => response.Timeout),
                       Runtime:                Runtime,

                       AuthenticData:          Responses.All(response => response.AuthenticData),
                       CheckingDisabled:       primary.CheckingDisabled

                   );

        }

        #endregion

        #region (private static) WithoutRepeats(ResourceRecords)

        /// <summary>
        /// The given records with the repeats taken out, in the order they first
        /// appeared.
        /// </summary>
        /// <remarks>
        /// Two queries for the same name come back with the same CNAME chain, and a
        /// caller shown it twice is being shown an artefact of how it was asked
        /// rather than something about the name. What "the same record" means on
        /// paper is its zone-file form, so that is what they are told apart by.
        /// </remarks>
        private static List<IDNSResourceRecord> WithoutRepeats(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {

            var seen  = new HashSet<String>(StringComparer.Ordinal);
            var kept  = new List<IDNSResourceRecord>();

            foreach (var resourceRecord in ResourceRecords)
            {

                String key;

                try
                {
                    key = resourceRecord.ToZoneFileString();
                }
                catch
                {
                    // A record with no presentation form at all - OPT says so by
                    // throwing. Those do not belong in an answer or an authority
                    // section anyway; if one turns up, it is told apart by what can
                    // be read off it instead of being dropped.
                    key = $"{resourceRecord.DomainName}|{resourceRecord.Type}|{resourceRecord.Class}|{resourceRecord.RText}";
                }

                if (seen.Add(key))
                    kept.Add(resourceRecord);

            }

            return kept;

        }

        #endregion

    }

}
