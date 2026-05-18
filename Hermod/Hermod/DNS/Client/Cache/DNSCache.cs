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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A cache for DNS entries.
    /// </summary>
    public class DNSCache : IDisposable
    {

        #region Data

        private readonly ConcurrentDictionary<DNSServiceName, DNSCacheEntry>  dnsCache       = [];
        private readonly ConcurrentDictionary<String, DateTimeOffset>        noDataCache    = [];
        private readonly Timer                                                cleanUpTimer;
        private readonly Object                                               cleanUpLock    = new();

        /// <summary>
        /// Cached NSEC records for aggressive negative caching (RFC 8198).
        /// Key: zone name (e.g. "example.com."), Value: list of NSEC records with expiry.
        /// </summary>
        private readonly ConcurrentDictionary<String, List<(NSEC Record, DateTimeOffset Expiry)>> nsecRangeCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The default interval for removing outdated entries from the DNS cache.
        /// </summary>
        public static readonly TimeSpan  DefaultCleanUpEvery       = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default TTL for negative cache entries (NXDOMAIN, etc.).
        /// RFC 2308 recommends caching for the SOA minimum TTL, but since
        /// we may not have that, we use a conservative default.
        /// </summary>
        public static readonly TimeSpan  DefaultNegativeCacheTTL   = TimeSpan.FromMinutes(5);

        #endregion

        #region Properties

        /// <summary>
        /// The interval for removing outdated entries from the DNS cache.
        /// </summary>
        public TimeSpan  CleanUpEvery        { get; }

        /// <summary>
        /// The TTL for negative cache entries (NXDOMAIN, etc.).
        /// </summary>
        public TimeSpan  NegativeCacheTTL    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS cache.
        /// </summary>
        /// <param name="CleanUpEvery">How often to remove outdated entries from DNS cache.</param>
        /// <param name="NegativeCacheTTL">The TTL for negative cache entries (NXDOMAIN, etc.).</param>
        public DNSCache(TimeSpan?  CleanUpEvery       = null,
                        TimeSpan?  NegativeCacheTTL   = null)
        {

            this.CleanUpEvery      = CleanUpEvery     ?? DefaultCleanUpEvery;
            this.NegativeCacheTTL  = NegativeCacheTTL ?? DefaultNegativeCacheTTL;

            this.cleanUpTimer      = new Timer(
                                         RemoveExpiredCacheEntries,
                                         null,
                                         // Delayed start...
                                         CleanUpEvery ?? TimeSpan.FromSeconds(10),
                                         CleanUpEvery ?? TimeSpan.FromSeconds(10)
                                     );

            #region Cache "localhost"

            dnsCache.TryAdd(

                DNSServiceName.Parse(
                    DomainName.Localhost.FullName
                ),

                new DNSCacheEntry(

                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),

                    DNSInfo:      new DNSInfo(
                                      Origin:                 new DNSServerConfig(
                                                                  IPv4Address.Localhost,
                                                                  IPPort.Parse(53)
                                                              ),
                                      QueryId:                0,
                                      IsAuthoritativeAnswer:  true,
                                      IsTruncated:            false,
                                      RecursionDesired:       false,
                                      RecursionAvailable:     false,
                                      ResponseCode:           DNSResponseCodes.NoError,
                                      Answers:                [
                                                                  new    A(DomainName.Localhost, DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv4Address.Localhost),
                                                                  new AAAA(DomainName.Localhost, DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv6Address.Localhost)
                                                              ],
                                      Authorities:            [],
                                      AdditionalRecords:      [],
                                      IsValid:                true,
                                      IsTimeout:              false,
                                      Timeout:                TimeSpan.Zero
                                  )

                )
            );

            #endregion

            #region Cache "loopback"

            dnsCache.TryAdd(

                DNSServiceName.Parse(
                    DomainName.Loopback.FullName
                ),

                new DNSCacheEntry(

                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),

                    DNSInfo:      new DNSInfo(
                                      Origin:                 new DNSServerConfig(
                                                                  IPv4Address.Localhost,
                                                                  IPPort.Parse(53)
                                                              ),
                                      QueryId:                0,
                                      IsAuthoritativeAnswer:  true,
                                      IsTruncated:            false,
                                      RecursionDesired:       false,
                                      RecursionAvailable:     false,
                                      ResponseCode:           DNSResponseCodes.NoError,
                                      Answers:                [
                                                                  new    A(DomainName.Loopback, DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv4Address.Localhost),
                                                                  new AAAA(DomainName.Loopback, DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv6Address.Localhost)
                                                              ],
                                      Authorities:            [],
                                      AdditionalRecords:      [],
                                      IsValid:                true,
                                      IsTimeout:              false,
                                      Timeout:                TimeSpan.Zero
                                 )

                )
            );

            #endregion

        }

        #endregion


        #region Add           (DomainName, DNSInformation)

        /// <summary>
        /// Add the given DNS information to the DNS cache.
        /// Supports negative caching (NXDOMAIN) and merging of answers
        /// when an entry for the same domain already exists.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="DNSInformation">The DNS information to add.</param>
        public DNSCache Add(DNSServiceName  DomainName,
                            DNSInfo         DNSInformation)
        {

            #region Negative caching (NXDOMAIN, Refused, etc.)

            if (!DNSInformation.Answers.Any())
            {

                if (DNSInformation.ResponseCode is DNSResponseCodes.NameError or
                                                   DNSResponseCodes.Refused)
                {

                    var negativeTTL = DNSInformation.Authorities.
                                          OfType<SOA>().
                                          Select(soa => soa.TimeToLive).
                                          FirstOrDefault(DefaultNegativeCacheTTL);

                    dnsCache[DomainName] = new DNSCacheEntry(
                                               Timestamp.Now + negativeTTL,
                                               DNSInformation
                                           );

                }

                return this;

            }

            #endregion

            var endOfLife = Timestamp.Now + DNSInformation.Answers.Min(dnsResourceRecord => dnsResourceRecord.TimeToLive);

            var newEntry  = new DNSCacheEntry(
                                endOfLife,
                                DNSInformation
                            );

            // Use AddOrUpdate for atomic cache insertion with merge.
            // This avoids a race condition where two concurrent queries for
            // different record types of the same domain could overwrite each
            // other's results during the read-merge-write window.
            dnsCache.AddOrUpdate(

                DomainName,

                // Add factory: no existing entry — insert as-is
                newEntry,

                // Update factory: merge new answers into existing entry
                (key, existingEntry) => {

                    var newAnswerTypes = DNSInformation.Answers.
                                             Select(rr => rr.Type).
                                             ToHashSet();

                    var mergedAnswers  = existingEntry.DNSInfo.Answers.
                                             Where (rr => !newAnswerTypes.Contains(rr.Type)).
                                             Concat(DNSInformation.Answers).
                                             ToArray();

                    return new DNSCacheEntry(
                               endOfLife,
                               new DNSInfo(
                                   DNSInformation.Origin,
                                   DNSInformation.QueryId,
                                   DNSInformation.AuthoritativeAnswer,
                                   DNSInformation.IsTruncated,
                                   DNSInformation.RecursionRequested,
                                   DNSInformation.RecursionAvailable,
                                   DNSInformation.ResponseCode,
                                   mergedAnswers,
                                   DNSInformation.Authorities,
                                   DNSInformation.AdditionalRecords,
                                   DNSInformation.IsValid,
                                   DNSInformation.IsTimeout,
                                   DNSInformation.Timeout
                               )
                           );

                }

            );

            return this;

        }

        #endregion

        #region Add           (DomainName,         params ResourceRecords)

        /// <summary>
        /// Add the given DNS resource record to the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="ResourceRecords">The DNS resource records to add.</param>
        public DNSCache Add(DNSServiceName               DomainName,
                            params IDNSResourceRecord[]  ResourceRecords)

            => Add(
                   DomainName,
                   new DNSServerConfig(
                       IPv4Address.Localhost,
                       IPPort.DNS
                    ),
                   ResourceRecords
               );

        #endregion

        #region Add           (DomainName, Origin, params ResourceRecords)

        /// <summary>
        /// Add the given DNS resource record to the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="Origin">The origin of the DNS resource record.</param>
        /// <param name="ResourceRecords">The DNS resource records to add.</param>
        public DNSCache Add(DNSServiceName               DomainName,
                            DNSServerConfig              Origin,
                            params IDNSResourceRecord[]  ResourceRecords)
        {

            if (!dnsCache.TryAdd(
                   DomainName,
                   new DNSCacheEntry(
                       Timestamp.Now + ResourceRecords.Min(dnsResourceRecord => dnsResourceRecord.TimeToLive),
                       new DNSInfo(
                           Origin:                 Origin,
                           QueryId:                Random.Shared.Next(),
                           IsAuthoritativeAnswer:  false,
                           IsTruncated:            false,
                           RecursionDesired:       false,
                           RecursionAvailable:     false,
                           ResponseCode:           DNSResponseCodes.NoError,
                           Answers:                ResourceRecords,
                           Authorities:            [],
                           AdditionalRecords:      [],
                           IsValid:                true,
                           IsTimeout:              false,
                           Timeout:                TimeSpan.Zero
                       )
                   )
               ))
            {
                dnsCache[DomainName].DNSInfo.AddAnswers(ResourceRecords);
            }

            return this;

        }

        #endregion


        #region Remove        (DomainName)

        /// <summary>
        /// Remove a cached DNS entry by its domain name.
        /// </summary>
        /// <param name="DomainName">The domain name to remove.</param>
        public Boolean Remove(DomainName DomainName)

            => dnsCache.TryRemove(
                   DNSServiceName.Parse(DomainName.FullName),
                   out _
               );

        #endregion

        #region Remove        (DNSServiceName)

        /// <summary>
        /// Remove a cached DNS entry by its DNSServiceName.
        /// </summary>
        /// <param name="DNSServiceName">The DNSServiceName to remove.</param>
        public Boolean Remove(DNSServiceName DNSServiceName)

            => dnsCache.TryRemove(
                   DNSServiceName,
                   out _
               );

        #endregion

        #region RemoveAll     ()

        /// <summary>
        /// Remove all cached DNS entries.
        /// </summary>
        public void RemoveAll()

            => dnsCache.Clear();

        #endregion


        #region GetDNSInfo    (DomainName)

        /// <summary>
        /// Get the cached DNS information from the DNS cache.
        /// Expired individual resource records are filtered out.
        /// Returns null if no valid records remain.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        public DNSInfo? GetDNSInfo(DNSServiceName DomainName)
        {

            if (dnsCache.TryGetValue(DomainName, out var dnsCacheEntry))
                return FilterExpiredRecords(dnsCacheEntry.DNSInfo);

            return null;

        }

        #endregion

        #region TryGetDNSInfo (DomainName, out DNSInfo)

        /// <summary>
        /// Get the cached DNS information from the DNS cache.
        /// Expired individual resource records are filtered out.
        /// Returns false if no valid records remain (for positive responses).
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        public Boolean TryGetDNSInfo(DNSServiceName                    DomainName,
                                     [NotNullWhen(true)] out DNSInfo?  DNSInfo)
        {

            if (dnsCache.TryGetValue(DomainName, out var dnsCacheEntry))
            {

                var filtered = FilterExpiredRecords(dnsCacheEntry.DNSInfo);

                if (filtered is not null)
                {
                    DNSInfo = filtered;
                    return true;
                }

            }

            DNSInfo = null;
            return false;

        }

        #endregion

        #region (private) FilterExpiredRecords(DNSInfo)

        /// <summary>
        /// Filter out individual resource records whose EndOfLife has passed.
        /// For negative responses (NXDOMAIN, Refused) the entry is returned as-is
        /// since those have no per-record TTL. Returns null if all positive records
        /// have expired.
        /// </summary>
        private static DNSInfo? FilterExpiredRecords(DNSInfo Original)
        {

            // Negative cache entries have no per-record TTLs to check
            if (Original.ResponseCode is DNSResponseCodes.NameError or
                                          DNSResponseCodes.Refused)
                return Original;

            var now             = Timestamp.Now;

            var liveAnswers     = Original.Answers.
                                      Where(rr => rr.EndOfLife > now).
                                      ToArray();

            var liveAuthorities = Original.Authorities.
                                      Where(rr => rr.EndOfLife > now).
                                      ToArray();

            // If all answer records have expired, treat the entry as stale
            if (liveAnswers.Length == 0 && Original.Answers.Any())
                return null;

            // Return a filtered copy if any records were removed
            if (liveAnswers.Length    != Original.Answers.    Count() ||
                liveAuthorities.Length != Original.Authorities.Count())
            {
                return new DNSInfo(
                           Original.Origin,
                           Original.QueryId,
                           Original.AuthoritativeAnswer,
                           Original.IsTruncated,
                           Original.RecursionRequested,
                           Original.RecursionAvailable,
                           Original.ResponseCode,
                           liveAnswers,
                           liveAuthorities,
                           Original.AdditionalRecords,
                           Original.IsValid,
                           Original.IsTimeout,
                           Original.Timeout
                       );
            }

            return Original;

        }

        #endregion


        #region AddNoData    (DomainName, RecordType, TTL)

        /// <summary>
        /// Cache a NODATA response (NoError with no matching answers) for
        /// a specific record type. The cache is keyed per (domain, type)
        /// so that a NODATA for AAAA does not suppress valid A records.
        /// </summary>
        /// <param name="DomainName">The queried domain name.</param>
        /// <param name="RecordType">The record type that returned no data.</param>
        /// <param name="TTL">The time to cache this NODATA entry.</param>
        public void AddNoData(DNSServiceName           DomainName,
                              DNSResourceRecordTypes   RecordType,
                              TimeSpan                 TTL)
        {

            var key = $"{DomainName}|{(UInt16) RecordType}";

            noDataCache[key] = Timestamp.Now + TTL;

        }

        #endregion

        #region IsNoData     (DomainName, RecordType)

        /// <summary>
        /// Check whether a NODATA response is cached for the given domain and record type.
        /// Returns true if the NODATA entry exists and has not expired.
        /// </summary>
        /// <param name="DomainName">The queried domain name.</param>
        /// <param name="RecordType">The record type to check.</param>
        public Boolean IsNoData(DNSServiceName           DomainName,
                                DNSResourceRecordTypes   RecordType)
        {

            var key = $"{DomainName}|{(UInt16) RecordType}";

            if (noDataCache.TryGetValue(key, out var endOfLife))
            {
                if (endOfLife > Timestamp.Now)
                    return true;

                noDataCache.TryRemove(key, out _);
            }

            return false;

        }

        #endregion

        #region RemoveNoData (DomainName, RecordType)

        /// <summary>
        /// Remove a cached NODATA entry.
        /// </summary>
        public Boolean RemoveNoData(DNSServiceName           DomainName,
                                    DNSResourceRecordTypes   RecordType)

            => noDataCache.TryRemove(
                   $"{DomainName}|{(UInt16) RecordType}",
                   out _
               );

        #endregion


        #region AddNSECRange(ZoneName, NSECRecord, TTL)

        /// <summary>
        /// Cache an NSEC record's range for aggressive negative caching (RFC 8198).
        /// This allows synthesizing NXDOMAIN responses for names that fall
        /// within a proven non-existence range without querying the wire.
        /// </summary>
        /// <param name="ZoneName">The zone this NSEC record belongs to.</param>
        /// <param name="NSECRecord">The NSEC record defining the range.</param>
        /// <param name="TTL">The TTL for this cached range.</param>
        public void AddNSECRange(String    ZoneName,
                                 NSEC      NSECRecord,
                                 TimeSpan  TTL)
        {

            var entry  = (NSECRecord, Expiry: Timestamp.Now + TTL);

            nsecRangeCache.AddOrUpdate(
                ZoneName,
                _ => [entry],
                (_, existing) =>
                {
                    // Remove expired entries and duplicates, then add the new one
                    var now = Timestamp.Now;
                    existing.RemoveAll(e => e.Expiry <= now ||
                                            e.Record.DomainName.FullName.Equals(NSECRecord.DomainName.FullName, StringComparison.OrdinalIgnoreCase));
                    existing.Add(entry);
                    return existing;
                }
            );

        }

        #endregion

        #region IsNameNegativelyCachedByNSEC(DomainName, ZoneName)

        /// <summary>
        /// Check if a domain name falls within a cached NSEC range,
        /// proving its non-existence without a network query (RFC 8198).
        /// Uses canonical DNS name ordering (RFC 4034 Section 6.1).
        /// </summary>
        /// <param name="DomainName">The domain name to check.</param>
        /// <param name="ZoneName">The zone to check NSEC ranges for.</param>
        /// <returns>True if the name is proven non-existent by a cached NSEC range.</returns>
        public Boolean IsNameNegativelyCachedByNSEC(String  DomainName,
                                                    String  ZoneName)
        {

            if (!nsecRangeCache.TryGetValue(ZoneName, out var ranges))
                return false;

            var now       = Timestamp.Now;
            var queryName = DomainName.ToLowerInvariant();

            foreach (var (record, expiry) in ranges)
            {

                if (expiry <= now)
                    continue;

                var ownerName = record.DomainName.FullName.ToLowerInvariant();
                var nextName  = record.NextDomainName.FullName.ToLowerInvariant();

                // RFC 4034 Section 6.1: Canonical DNS Name Order
                // A name is proven non-existent if: owner < name < next
                // Special case: if next <= owner, the range wraps around (last NSEC in zone)
                if (String.Compare(nextName, ownerName, StringComparison.Ordinal) > 0)
                {
                    // Normal range: owner < name < next
                    if (String.Compare(queryName, ownerName, StringComparison.Ordinal) > 0 &&
                        String.Compare(queryName, nextName,  StringComparison.Ordinal) < 0)
                        return true;
                }
                else
                {
                    // Wrap-around range (last NSEC): name > owner OR name < next
                    if (String.Compare(queryName, ownerName, StringComparison.Ordinal) > 0 ||
                        String.Compare(queryName, nextName,  StringComparison.Ordinal) < 0)
                        return true;
                }

            }

            return false;

        }

        #endregion


        #region (private, Timer) RemoveExpiredCacheEntries(State)

        private void RemoveExpiredCacheEntries(Object? State)
        {

            if (Monitor.TryEnter(cleanUpLock))
            {

                try
                {

                    var now             = Timestamp.Now;

                    // Remove entire entries only when ALL resource records have expired.
                    // Per-record filtering on read (FilterExpiredRecords) handles the
                    // case where some records are still valid within a mixed-TTL entry.
                    var expiredEntries  = dnsCache.
                                              Where(entry => entry.Value.EndOfLife < now &&
                                                             entry.Value.DNSInfo.Answers.All(rr => rr.EndOfLife <= now)).
                                              ToArray();

                    foreach (var expiredEntry in expiredEntries)
                    {
                        DebugX.LogT($"Removed '{expiredEntry.Key}' from DNS cache (all records expired)!");
                        dnsCache.TryRemove(expiredEntry.Key, out _);
                    }

                    // Clean up expired NODATA entries
                    var expiredNoDataEntries = noDataCache.
                                                   Where(entry => entry.Value < now).
                                                   ToArray();

                    foreach (var expiredNoDataEntry in expiredNoDataEntries)
                    {
                        noDataCache.TryRemove(expiredNoDataEntry.Key, out _);
                    }

                    // Clean up expired NSEC range entries
                    foreach (var kvp in nsecRangeCache)
                    {
                        kvp.Value.RemoveAll(e => e.Expiry < now);
                        if (kvp.Value.Count == 0)
                            nsecRangeCache.TryRemove(kvp.Key, out _);
                    }

                }

                catch (Exception e)
                {
                    DebugX.LogException(e, "During DNS cache clean up!");
                }

                finally
                {
                    Monitor.Exit(cleanUpLock);
                }

            }

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose the DNS cache and its cleanup timer.
        /// </summary>
        public void Dispose()
        {
            cleanUpTimer.Dispose();
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{dnsCache.Count} cached DNS entries";

        #endregion

    }

}
