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


        public DNSSRVEndpoint? SelectEndpoint(String DNSServiceName)
        {

            var endpoints = GetEndpoints(DNSServiceName);

            if (endpoints is null || endpoints.Count == 0)
                return null;

            // Group by Priority and filter healthy endpoints
            var minPriority = endpoints.Min  (e => e.Priority);
            var candidates  = endpoints.Where(e => e.Priority == minPriority && e.IsHealthy).ToList();

            if (candidates.Count == 0)
                return null;

            // Weighted Random Selection
            var totalWeight = candidates.Sum(e => e.Weight);
            var randomValue = Random.Shared.Next(0, totalWeight);

            foreach (var candidate in candidates)
            {
                if (randomValue < candidate.Weight)
                    return candidate;
                randomValue -= candidate.Weight;
            }

            return candidates.Last(); // Fallback

        }


        public void MarkUnhealthy(String  DNSServiceName,
                                  String  Target)
        {
            if (internalCache.TryGetValue(DNSServiceName, out var entry))
            {

                //var updatedEndpoints = entry.Endpoints.Select(e =>
                //    e.Target.Equals(Target, StringComparison.OrdinalIgnoreCase)
                //        ? e with { IsHealthy = false }
                //        : e
                //).ToImmutableList();

                //entry.Update(updatedEndpoints);

            }
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
