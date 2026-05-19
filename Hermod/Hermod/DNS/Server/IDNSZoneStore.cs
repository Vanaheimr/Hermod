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
        NameError

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

    }


    public interface IDNSZoneStore
    {

        Task<DNSZoneLookupResult> Lookup(DNSQuestion       Question,
                                         CancellationToken  CancellationToken = default);

    }

}
