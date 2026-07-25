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
    /// Answers DNS queries from an authoritative zone store.
    /// </summary>
    public sealed class AuthoritativeDNSRequestHandler : IDNSRequestHandler
    {

        private readonly IDNSZoneStore zoneStore;

        /// <summary>
        /// How many CNAMEs will be followed before giving up. RFC 1034 §4.3.2 sets
        /// no limit but warns that a chain can loop; this bounds the work even when
        /// the loop detection below cannot (for instance a chain that grows through
        /// a store which synthesizes names).
        /// </summary>
        private const Int32 MaxCanonicalNameChainLength = 16;


        public Boolean  RecursionAvailable  { get; }

        /// <summary>
        /// The UDP payload size this server advertises in its own OPT record
        /// (RFC 6891 §6.2.4). The default of 1232 bytes follows the DNS Flag Day
        /// 2020 recommendation: it stays below common path-MTU limits, so
        /// responses do not fragment.
        /// </summary>
        public UInt16   UDPPayloadSize      { get; }


        public AuthoritativeDNSRequestHandler(IDNSZoneStore  ZoneStore,
                                              Boolean        RecursionAvailable   = false,
                                              UInt16         UDPPayloadSize       = 1232)
        {

            this.zoneStore           = ZoneStore;
            this.RecursionAvailable  = RecursionAvailable;
            this.UDPPayloadSize      = UDPPayloadSize;

        }


        #region (private) BuildResponseOPT(Request, ResponseCode)

        /// <summary>
        /// Build the OPT record for a response, or null when the request had none.
        /// </summary>
        /// <remarks>
        /// RFC 6891 §6.1.1: a responder that implements EDNS "MUST include an OPT
        /// record in their respective responses" — an EDNS query is answered with
        /// an EDNS response. The OPT advertises this server's own payload size
        /// (§6.2.4) and carries the upper 8 bits of an extended RCODE (§6.1.3).
        /// Options are deliberately not echoed: unknown options MUST be ignored
        /// (§6.1.2), and reflecting them would make the server an amplifier.
        /// </remarks>
        private OPT? BuildResponseOPT(DNSPacket         Request,
                                      DNSResponseCodes  ResponseCode)
        {

            if (!Request.AdditionalRRs.OfType<OPT>().Any())
                return null;

            return new OPT(
                       UDPPayloadSize:  UDPPayloadSize,
                       ExtendedRCODE:   (Byte) ((Int32) ResponseCode >> 4),
                       Version:         0,
                       Flags:           0
                   );

        }

        #endregion


        #region (private) FollowCanonicalNames(Question, Answers, CancellationToken)

        /// <summary>
        /// Apply the CNAME rule: if the queried node holds a CNAME and QTYPE is not
        /// CNAME, copy the CNAME into the answer section and restart the query at
        /// the canonical name.
        /// </summary>
        /// <remarks>
        /// RFC 1034 §4.3.2 step 3a. A CNAME answers a query of *any* type at that
        /// name — the rule is about the node, not about which types it happens to
        /// hold. Without it an alias answers only QTYPE=CNAME and everything else
        /// returns NOERROR with an empty answer section, which a resolver reads as
        /// "this name definitively has no such record" and caches. The alias then
        /// silently stops working for every client that does not already know it is
        /// one.
        ///
        /// The chase stops at the zone edge: when the canonical name is not held
        /// here, the CNAMEs gathered so far are returned and the resolver continues
        /// from them, which is what RFC 1034 expects of an authoritative server.
        /// </remarks>
        /// <param name="Question">The original question.</param>
        /// <param name="Answers">The answer section being assembled.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        private async Task FollowCanonicalNames(DNSQuestion               Question,
                                                List<IDNSResourceRecord>  Answers,
                                                CancellationToken         CancellationToken)
        {

            // A CNAME query is answered directly by the store, and ANY already
            // returns the CNAME alongside everything else at the node, so neither
            // needs a restart.
            if (Question.QueryType == DNSResourceRecordTypes.CNAME ||
                Question.QueryType == DNSResourceRecordTypes.Any)
            {
                return;
            }

            var currentName = Question.DomainName;

            // RFC 1034 §4.3.2 warns that a chain may loop. Track the names already
            // visited rather than relying on the depth limit alone — a two-element
            // cycle would otherwise be walked sixteen times before stopping, and the
            // same CNAME would be added to the answer section on every pass.
            var visited     = new HashSet<DNSServiceName> { currentName };

            for (var depth = 0; depth < MaxCanonicalNameChainLength; depth++)
            {

                var aliasResult = await zoneStore.Lookup(
                                            new DNSQuestion(
                                                currentName,
                                                DNSResourceRecordTypes.CNAME,
                                                Question.QueryClass
                                            ),
                                            CancellationToken
                                        ).ConfigureAwait(false);

                if (aliasResult.Status != DNSZoneLookupStatus.Found)
                    return;

                var alias = aliasResult.AnswerRRs.OfType<CNAME>().FirstOrDefault();

                if (alias is null)
                    return;

                Answers.Add(alias);

                var target = DNSServiceName.Parse(alias.CName.FullName);

                if (!visited.Add(target))
                    return;

                // Restart the original query at the canonical name.
                var targetResult = await zoneStore.Lookup(
                                             new DNSQuestion(
                                                 target,
                                                 Question.QueryType,
                                                 Question.QueryClass
                                             ),
                                             CancellationToken
                                         ).ConfigureAwait(false);

                if (targetResult.Status == DNSZoneLookupStatus.Found)
                {
                    Answers.AddRange(targetResult.AnswerRRs);
                    return;
                }

                // NoData at the target means it may itself be an alias; NameError
                // means the chain leaves this zone and the next pass will stop.
                currentName = target;

            }

        }

        #endregion


        public async Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                          CancellationToken  CancellationToken = default)
        {

            if (Request.QueryOrResponse != DNSQueryResponse.Query)
                return null;

            DNSResponse Error(DNSResponseCodes ResponseCode)
            {

                var opt = BuildResponseOPT(Request, ResponseCode);

                return Request.CreateResponse(
                           Opcode:               Request.Opcode,
                           AuthoritativeAnswer:  false,
                           Truncation:           false,
                           RecursionDesired:     Request.RecursionDesired,
                           RecursionAvailable:   RecursionAvailable,
                           ResponseCode:         ResponseCode,
                           AnswerRRs:            [],
                           AuthorityRRs:         [],
                           AdditionalRRs:        opt is not null ? [ opt ] : []
                       );

            }

            // RFC 6891 §6.1.3: "If a responder does not implement the VERSION
            // level of the request, then it MUST respond with RCODE=BADVERS."
            // Only EDNS version 0 is defined today.
            var requestOPT = Request.AdditionalRRs.OfType<OPT>().FirstOrDefault();

            if (requestOPT is not null && requestOPT.Version > 0)
                return Error(DNSResponseCodes.BadVersion);

            if (Request.Opcode != 0)
                return Error(DNSResponseCodes.NotImplemented);

            var questions = Request.Questions.ToArray();
            if (questions.Length == 0)
                return Error(DNSResponseCodes.FormatError);

            var answers            = new List<IDNSResourceRecord>();
            var authorities        = new List<IDNSResourceRecord>();
            var additionalRecords  = new List<IDNSResourceRecord>();
            var responseCode       = DNSResponseCodes.NoError;
            var foundName          = false;

            foreach (var question in questions)
            {

                var lookupResult = await zoneStore.Lookup(question, CancellationToken).
                                                   ConfigureAwait(false);

                authorities.      AddRange(lookupResult.AuthorityRRs);
                additionalRecords.AddRange(lookupResult.AdditionalRRs);

                if (lookupResult.Status == DNSZoneLookupStatus.Found)
                {
                    foundName = true;
                    answers.AddRange(lookupResult.AnswerRRs);
                }
                else if (lookupResult.Status == DNSZoneLookupStatus.NoData)
                {

                    foundName = true;

                    // The name exists but holds nothing of the requested type. That is
                    // NODATA — unless the node is an alias, in which case RFC 1034
                    // §4.3.2 requires the CNAME to be returned instead.
                    await FollowCanonicalNames(
                              question,
                              answers,
                              CancellationToken
                          ).ConfigureAwait(false);

                }

            }

            if (!foundName)
                responseCode = DNSResponseCodes.NameError;

            // RFC 6891 §6.1.1: mirror EDNS by carrying an OPT in the response.
            var responseOPT = BuildResponseOPT(Request, responseCode);

            if (responseOPT is not null)
                additionalRecords.Add(responseOPT);

            return Request.CreateResponse(
                       Opcode:               Request.Opcode,
                       AuthoritativeAnswer:  true,
                       Truncation:           false,
                       RecursionDesired:     Request.RecursionDesired,
                       RecursionAvailable:   RecursionAvailable,
                       ResponseCode:         responseCode,
                       AnswerRRs:            answers,
                       AuthorityRRs:         authorities,
                       AdditionalRRs:        additionalRecords
                   );

        }

    }

}
