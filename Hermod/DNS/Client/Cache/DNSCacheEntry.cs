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
    /// A DNS cache entry.
    /// </summary>
    public sealed class DNSCacheEntry
    {

        #region Properties

        /// <summary>
        /// The timestamp when this entry gets invalidated.
        /// </summary>
        public DateTimeOffset  EndOfLife      { get; }

        /// <summary>
        /// The cached DNS information.
        /// </summary>
        public DNSInfo         DNSInfo        { get; }

        /// <summary>
        /// The response each RRset at this name came with, by the type of the RRset
        /// (for an RRSIG, the type it covers). A cache hit for one type is served
        /// from the response that carried it, and not from the merged entry above,
        /// which holds every RRset cached under the name and the header and
        /// authority section of whichever response came last.
        /// </summary>
        public IReadOnlyDictionary<DNSResourceRecordTypes, DNSInfo>  Responses  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS cache entry.
        /// </summary>
        /// <param name="EndOfLife">The timestamp when this entry gets invalidated.</param>
        /// <param name="DNSInfo">The cached DNS information.</param>
        /// <param name="Responses">The response each RRset at this name came with, by RRset type.</param>
        public DNSCacheEntry(DateTimeOffset                                        EndOfLife,
                             DNSInfo                                               DNSInfo,
                             IReadOnlyDictionary<DNSResourceRecordTypes, DNSInfo>?  Responses   = null)
        {

            this.EndOfLife    = EndOfLife;
            this.DNSInfo      = DNSInfo;
            this.Responses    = Responses ?? new Dictionary<DNSResourceRecordTypes, DNSInfo>();

        }

        #endregion

    }

}
