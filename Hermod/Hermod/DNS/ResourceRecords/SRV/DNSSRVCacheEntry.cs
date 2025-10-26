/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class DNSSRVCacheEntry
    {

        public ImmutableList<DNSSRVEndpoint>  Endpoints      { get; private set; }
        public DateTimeOffset                 LastRefresh    { get; private set; }
        public TimeSpan                       MinTTL         { get; private set; }

        public DNSSRVCacheEntry(IEnumerable<DNSSRVEndpoint> Endpoints)
        {

            this.Endpoints    = Endpoints.OrderBy(e => e.Priority).
                                     ThenByDescending(e => e.Weight).
                                     ToImmutableList();

            this.LastRefresh  = DateTimeOffset.UtcNow;

            this.MinTTL       = TimeSpan.FromSeconds(this.Endpoints.Min(e => (Int32) e.TTL.TotalSeconds));

        }


        public Boolean IsExpired
            => DateTimeOffset.UtcNow > LastRefresh.Add(MinTTL);


        public void Update(IEnumerable<DNSSRVEndpoint> NewEndpoints)
        {

            this.Endpoints    = NewEndpoints.OrderBy(e => e.Priority)
                                       .ThenByDescending(e => e.Weight)
                                       .ToImmutableList();

            this.LastRefresh  = DateTimeOffset.UtcNow;

            this.MinTTL       = TimeSpan.FromSeconds(Endpoints.Min(e => (Int32) e.TTL.TotalSeconds));

        }

    }

}
