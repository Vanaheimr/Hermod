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

using System.Collections.Immutable;
using System.Collections.Concurrent;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class DNSSRVManager
    {

        private readonly ConcurrentDictionary<String, DNSSRVCacheEntry> internalCache = new (StringComparer.OrdinalIgnoreCase);

        public void AddOrUpdate(String                       DNSServiceName,
                                IEnumerable<DNSSRVEndpoint>  DNSSRVEndpoints)
        {

            internalCache.AddOrUpdate(DNSServiceName,
                key => new DNSSRVCacheEntry(DNSSRVEndpoints),
                (key, existing) =>
                {
                    existing.Update(DNSSRVEndpoints);
                    return existing;
                });

        }

        public ImmutableList<DNSSRVEndpoint>? GetEndpoints(String DNSServiceName)
        {

            if (internalCache.TryGetValue(DNSServiceName, out var entry))
            {

                if (entry.IsExpired)
                {

                    //ToDo: Maybe trigger an asynchronous DNS query to refresh the entry
                    internalCache.TryRemove(DNSServiceName, out _);

                    // Signal, that a refresh is needed
                    return null;

                }

                return entry.Endpoints;

            }

            return null;

        }


        /// <summary>
        /// Choose a target, following the selection RFC 2782 specifies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// "A client MUST attempt to contact the target host with the
        /// lowest-numbered priority it can reach" — <i>it can reach</i>. A
        /// priority whose targets are all unreachable is a dead end for that
        /// priority and not for the query, so the search moves up rather than
        /// giving up: reporting nothing while a usable target sits at the next
        /// priority is the one failure a caller cannot work around, because it
        /// never learns the others exist.
        /// </para>
        /// </remarks>
        public DNSSRVEndpoint? SelectEndpoint(String DNSServiceName)
        {

            var endpoints = GetEndpoints(DNSServiceName);

            if (endpoints is null || endpoints.Count == 0)
                return null;

            foreach (var priority in endpoints.Select(endpoint => endpoint.Priority).
                                               Distinct().
                                               Order())
            {

                var candidates = endpoints.Where(endpoint => endpoint.Priority == priority &&
                                                             endpoint.IsHealthy).
                                           ToList();

                if (candidates.Count > 0)
                    return SelectByWeight(candidates);

            }

            return null;

        }

        /// <summary>
        /// RFC 2782's weighted selection among targets of one priority.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three details carry it, and the previous implementation had none of
        /// them. "All those with weight 0 are placed at the beginning of the
        /// list"; the random number is "between 0 and the sum computed
        /// (inclusive)"; and the record chosen is the first whose running sum is
        /// "greater than or equal to" it.
        /// </para>
        /// <para>
        /// Together those three are what give a weight-0 target the "very small
        /// chance of being selected" the RFC asks for: only a draw of exactly
        /// zero reaches one, which is one outcome in sum + 1. Subtracting
        /// weights and comparing with a strict less-than instead — the obvious
        /// way to write it — gives a weight-0 target no chance at all, because
        /// nothing is ever less than zero. Measured over 20,000 draws against
        /// weights 0, 10 and 40, the zero was chosen 0 times.
        /// </para>
        /// <para>
        /// The rest of the order is a shuffle. The RFC says "in any order",
        /// which permits leaving it alone — but with every weight 0, as the RFC
        /// itself recommends when "there isn't any server selection to do", an
        /// unshuffled list sends every client to whichever target the dictionary
        /// happened to yield last. Three equal targets, no spreading at all.
        /// </para>
        /// </remarks>
        private static DNSSRVEndpoint SelectByWeight(List<DNSSRVEndpoint> Candidates)
        {

            var ordered = Candidates.OrderBy(endpoint => endpoint.Weight == 0 ? 0 : 1).
                                     ThenBy  (_        => Random.Shared.Next()).
                                     ToList();

            var sum     = ordered.Sum(endpoint => (Int32) endpoint.Weight);
            var random  = Random.Shared.Next(0, sum + 1);
            var running = 0;

            foreach (var candidate in ordered)
            {

                running += candidate.Weight;

                if (running >= random)
                    return candidate;

            }

            return ordered[^1];

        }


        public async Task ResolveAddressesAsync(String DNSServiceName)
        {
            if (internalCache.TryGetValue(DNSServiceName, out var entry))
            {

                var updatedEndpoints = new List<DNSSRVEndpoint>();

                foreach (var ep in entry.Endpoints)
                {
                    try
                    {
                        //var addresses = await Dns.GetHostAddressesAsync(ep.Target);
                        //updatedEndpoints.Add(ep with { ResolvedAddresses = addresses.ToImmutableList() });
                    }
                    catch
                    {
                        //updatedEndpoints.Add(ep with { IsHealthy = false });
                    }
                }

                entry.Update(updatedEndpoints);

            }
        }

    }

}
