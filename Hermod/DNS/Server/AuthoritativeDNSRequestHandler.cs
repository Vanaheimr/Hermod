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
