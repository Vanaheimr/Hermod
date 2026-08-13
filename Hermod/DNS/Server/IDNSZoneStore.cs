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

    public enum DNSZoneLookupStatus
    {

        Found,
        NoData,
        NameError,

        /// <summary>
        /// The name lives below a delegation point: this server is not
        /// authoritative for it, and RFC 1034 §4.3.2 step 3b says to answer with
        /// the child zone's NS records rather than with data. The response
        /// carries no answer, and AA is clear.
        /// </summary>
        Referral

    }


    public sealed class DNSZoneLookupResult
    {

        public DNSZoneLookupStatus             Status             { get; }

        public IEnumerable<IDNSResourceRecord> AnswerRRs          { get; }

        public IEnumerable<IDNSResourceRecord> AuthorityRRs       { get; }

        public IEnumerable<IDNSResourceRecord> AdditionalRRs      { get; }


        private DNSZoneLookupResult(DNSZoneLookupStatus             Status,
                                    IEnumerable<IDNSResourceRecord> AnswerRRs,
                                    IEnumerable<IDNSResourceRecord> AuthorityRRs,
                                    IEnumerable<IDNSResourceRecord> AdditionalRRs)
        {

            this.Status         = Status;
            this.AnswerRRs      = AnswerRRs;
            this.AuthorityRRs   = AuthorityRRs;
            this.AdditionalRRs  = AdditionalRRs;

        }


        public static DNSZoneLookupResult Found(IEnumerable<IDNSResourceRecord>  AnswerRRs,
                                                IEnumerable<IDNSResourceRecord>? AuthorityRRs    = null,
                                                IEnumerable<IDNSResourceRecord>? AdditionalRRs   = null)

            => new (
                   DNSZoneLookupStatus.Found,
                   AnswerRRs,
                   AuthorityRRs  ?? [],
                   AdditionalRRs ?? []
               );


        public static DNSZoneLookupResult NoData(IEnumerable<IDNSResourceRecord>? AuthorityRRs    = null,
                                                 IEnumerable<IDNSResourceRecord>? AdditionalRRs   = null)

            => new (
                   DNSZoneLookupStatus.NoData,
                   [],
                   AuthorityRRs  ?? [],
                   AdditionalRRs ?? []
               );


        public static DNSZoneLookupResult NameError(IEnumerable<IDNSResourceRecord>? AuthorityRRs    = null,
                                                    IEnumerable<IDNSResourceRecord>? AdditionalRRs   = null)

            => new (
                   DNSZoneLookupStatus.NameError,
                   [],
                   AuthorityRRs  ?? [],
                   AdditionalRRs ?? []
               );


        /// <summary>
        /// The name is below a zone cut. The NS records of the child zone travel
        /// in the authority section, any glue in the additional section, and the
        /// answer section stays empty (RFC 1034 §4.3.2 step 3b).
        /// </summary>
        public static DNSZoneLookupResult Referral(IEnumerable<IDNSResourceRecord>  AuthorityRRs,
                                                   IEnumerable<IDNSResourceRecord>? AdditionalRRs   = null)

            => new (
                   DNSZoneLookupStatus.Referral,
                   [],
                   AuthorityRRs,
                   AdditionalRRs ?? []
               );

    }


    public interface IDNSZoneStore
    {

        /// <summary>
        /// Answer one question from this zone.
        /// </summary>
        /// <param name="Question">The question to answer.</param>
        /// <param name="DNSSECOK">Whether the querier set the EDNS DO bit (RFC 4035 §3.2.1) and therefore wants the RRSIGs and the NSEC/NSEC3 records that prove a negative answer. A store holding no signatures may ignore this.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task<DNSZoneLookupResult> Lookup(DNSQuestion        Question,
                                         Boolean            DNSSECOK            = false,
                                         CancellationToken  CancellationToken   = default);

    }

}
