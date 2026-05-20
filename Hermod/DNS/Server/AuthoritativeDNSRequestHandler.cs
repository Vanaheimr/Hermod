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


        public AuthoritativeDNSRequestHandler(IDNSZoneStore  ZoneStore,
                                              Boolean        RecursionAvailable = false)
        {

            this.zoneStore           = ZoneStore;
            this.RecursionAvailable  = RecursionAvailable;

        }


        public async Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                          CancellationToken  CancellationToken = default)
        {

            if (Request.QueryOrResponse != DNSQueryResponse.Query)
                return null;

            if (Request.Opcode != 0)
                return Request.CreateResponse(
                           Opcode:               Request.Opcode,
                           AuthoritativeAnswer:  false,
                           Truncation:           false,
                           RecursionDesired:     Request.RecursionDesired,
                           RecursionAvailable:   RecursionAvailable,
                           ResponseCode:         DNSResponseCodes.NotImplemented,
                           AnswerRRs:            [],
                           AuthorityRRs:         [],
                           AdditionalRRs:        []
                       );

            var questions = Request.Questions.ToArray();
            if (questions.Length == 0)
                return Request.CreateResponse(
                           Opcode:               Request.Opcode,
                           AuthoritativeAnswer:  false,
                           Truncation:           false,
                           RecursionDesired:     Request.RecursionDesired,
                           RecursionAvailable:   RecursionAvailable,
                           ResponseCode:         DNSResponseCodes.FormatError,
                           AnswerRRs:            [],
                           AuthorityRRs:         [],
                           AdditionalRRs:        []
                       );

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
