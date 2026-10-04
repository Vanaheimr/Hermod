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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        // Keyed by "<name>|<type>". Case-insensitive because names now keep the case they
        // arrived in (RFC 1035 §2.3.3) while still being the same name (RFC 4343) — an
        // ordinal comparer would file "EXAMPLE.com" and "example.com" as separate entries.
        //
        // The response is kept with the entry, because it is the answer: its authority
        // section holds the SOA and, signed, the NSEC or NSEC3 proof of the absence.
        private readonly ConcurrentDictionary<String, (DateTimeOffset EndOfLife, DNSInfo? Response)>  noDataCache  = new(StringComparer.OrdinalIgnoreCase);
        private readonly Timer                                                cleanUpTimer;
        private readonly Object                                               cleanUpLock    = new();

        private readonly ILogger<DNSCache>  logger;

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
        public DNSCache(TimeSpan?            CleanUpEvery       = null,
                        TimeSpan?            NegativeCacheTTL   = null,
                        ILogger<DNSCache>?   Logger             = null,
                        ILoggerFactory?      LoggerFactory      = null)
        {

            this.CleanUpEvery      = CleanUpEvery     ?? DefaultCleanUpEvery;
            this.NegativeCacheTTL  = NegativeCacheTTL ?? DefaultNegativeCacheTTL;
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSCache>();

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
                                      Timeout:                TimeSpan.Zero,

                                      Runtime:                TimeSpan.Zero

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
                                      Timeout:                TimeSpan.Zero,

                                      Runtime:                TimeSpan.Zero

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

            #region Negative caching (NXDOMAIN, NODATA, Refused)

            if (!DNSInformation.Answers.Any())
            {

                // RFC 2308 §2.1 (NXDOMAIN) and §2.2 (NODATA — NOERROR with an empty
                // answer section) are both negative answers and both cacheable.
                //
                // NODATA additionally requires an SOA in the authority section. That
                // is what distinguishes it from a referral, which also has no answers
                // but carries NS records instead: caching a referral here would
                // record "this type does not exist" for a name whose data simply
                // lives elsewhere.
                var isNegative = DNSInformation.ResponseCode is DNSResponseCodes.NameError or
                                                                DNSResponseCodes.Refused ||

                                 (DNSInformation.ResponseCode == DNSResponseCodes.NoError &&
                                  DNSInformation.Authorities.OfType<SOA>().Any());

                if (isNegative)
                    dnsCache[DomainName] = new DNSCacheEntry(
                                               Timestamp.Now + ComputeNegativeCacheTTL(DNSInformation),
                                               DNSInformation
                                           );

                return this;

            }

            #endregion

            var endOfLife      = Timestamp.Now + DNSInformation.Answers.Min(dnsResourceRecord => dnsResourceRecord.TimeToLive);

            var newAnswerTypes = DNSInformation.Answers.
                                     Select(RRsetType).
                                     ToHashSet();

            var newEntry       = new DNSCacheEntry(
                                     endOfLife,
                                     DNSInformation,
                                     newAnswerTypes.ToDictionary(type => type, _ => DNSInformation)
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

                    // A signature is replaced together with the RRset it covers and
                    // with nothing else. Its own type is RRSIG whatever it covers, so
                    // merging by type alone let the RRSIG of a newly arrived RRset
                    // replace the RRSIG of every other RRset at this name: a zone apex
                    // holds both a signed DNSKEY and a signed DS RRset, and whichever
                    // of the two was fetched first lost its signature to the second.
                    // RFC 4035 §4.5 asks for "a single atomic entry containing the
                    // entire answer, including the named RRset and any associated
                    // DNSSEC RRs" — an RRset served without its signature is, to a
                    // validator, an RRset that was never signed.
                    var mergedAnswers   = existingEntry.DNSInfo.Answers.
                                              Where (rr => !newAnswerTypes.Contains(RRsetType(rr))).
                                              Concat(DNSInformation.Answers).
                                              ToArray();

                    var mergedResponses = existingEntry.Responses.
                                              Where (response => !newAnswerTypes.Contains(response.Key)).
                                              Concat(newEntry.Responses).
                                              ToDictionary();

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
                                   DNSInformation.Timeout,
                                   DNSInformation.Runtime
                               ),
                               mergedResponses
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
                           Timeout:                TimeSpan.Zero,
                           Runtime:                TimeSpan.Zero
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
        /// Remove all cached DNS entries, the negative ones included.
        /// </summary>
        /// <remarks>
        /// The two negative caches are emptied along with the positive one: a
        /// remembered "this name does not exist" is as much a cached answer as
        /// a remembered address, and leaving it behind would go on denying a
        /// name for minutes after everything else had been forgotten.
        /// </remarks>
        public void RemoveAll()
        {
            dnsCache.      Clear();
            noDataCache.   Clear();
            nsecRangeCache.Clear();
        }

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

                // A negative entry has no answer records whose TTLs could be
                // filtered, so its lifetime lives on the entry itself. Without this
                // check the entry is returned until the cleanup timer happens to
                // sweep it — however short a life RFC 2308 §4 gave it.
                if (!dnsCacheEntry.DNSInfo.Answers.Any() &&
                     dnsCacheEntry.EndOfLife <= Timestamp.Now)
                {
                    dnsCache.TryRemove(DomainName, out _);
                    DNSInfo = null;
                    return false;
                }

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

        #region TryGetAnswer  (DomainName, RecordType, out DNSInfo)

        /// <summary>
        /// Get a cached positive answer for one record type: the RRset of that type,
        /// with its signatures, and the CNAME and DNAME records that lead to it —
        /// in the response that carried it.
        /// </summary>
        /// <remarks>
        /// The entry for a name holds every RRset cached under it, and handing out
        /// the entry answered a question for one type with all of them: after a
        /// validator had fetched a zone's DNSKEY and DS, the next lookup of the
        /// zone's address came back with the DNSKEY, DS and their RRSIGs in the
        /// answer section. RFC 1034 §3.6.2 and RFC 2181 §5 make an answer the RRset
        /// asked for, reached through any aliases; RFC 4035 §3.1.1 adds the RRSIGs
        /// that cover it, and nothing else.
        ///
        /// An RRset is served whole or not at all: if one of its records or of its
        /// signatures, or one link of the alias chain, has expired, this is a miss.
        /// Handing out what is left would be an RRset without its signature or
        /// shorter than the one that was signed, and to a validator both are Bogus.
        /// </remarks>
        /// <param name="DomainName">The queried domain name.</param>
        /// <param name="RecordType">The queried record type.</param>
        /// <param name="DNSInfo">The cached answer.</param>
        public Boolean TryGetAnswer(DNSServiceName                    DomainName,
                                    DNSResourceRecordTypes            RecordType,
                                    [NotNullWhen(true)] out DNSInfo?  DNSInfo)
        {

            DNSInfo = null;

            // ANY asks for everything at the name, which a cache cannot know it holds.
            if (RecordType == DNSResourceRecordTypes.Any ||
                !dnsCache.TryGetValue(DomainName, out var dnsCacheEntry))
                return false;

            // Entries added record by record (Add(DomainName, params ResourceRecords))
            // came without a response; they are their own response.
            var response = dnsCacheEntry.Responses.Count == 0
                               ? dnsCacheEntry.DNSInfo
                               : dnsCacheEntry.Responses.GetValueOrDefault(RecordType);

            if (response is null ||
                response.ResponseCode != DNSResponseCodes.NoError)
                return false;

            var now      = Timestamp.Now;
            var answers  = new List<IDNSResourceRecord>();
            var visited  = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            var owner    = DomainName;

            while (visited.Add(owner.FullName))
            {

                var atOwner = response.Answers.
                                  Where(rr => rr.DomainName == owner).
                                  ToArray();

                var rrset   = atOwner.
                                  Where(rr => rr.Type == RecordType ||
                                              rr is RRSIG signature && signature.TypeCovered == RecordType).
                                  ToArray();

                if (rrset.Any(rr => rr.Type == RecordType))
                {

                    if (rrset.Any(rr => rr.EndOfLife <= now))
                        return false;

                    answers.AddRange(rrset);

                    DNSInfo = new DNSInfo(
                                  response.Origin,
                                  response.QueryId,
                                  response.AuthoritativeAnswer,
                                  response.IsTruncated,
                                  response.RecursionRequested,
                                  response.RecursionAvailable,
                                  response.ResponseCode,
                                  answers,
                                  response.Authorities.Where(rr => rr.EndOfLife > now),
                                  response.AdditionalRecords,
                                  response.IsValid,
                                  response.IsTimeout,
                                  response.Timeout,
                                  response.Runtime,
                                  response.AuthenticData,
                                  response.CheckingDisabled
                              );

                    return true;

                }

                // A CNAME at the name stands in for every other type there
                // (RFC 1034 §3.6.2) — unless the CNAME is what was asked for.
                var cname   = RecordType != DNSResourceRecordTypes.CNAME
                                  ? atOwner.OfType<CNAME>().FirstOrDefault()
                                  : null;

                if (cname is null ||
                    !DNSServiceName.TryParse(cname.CName.FullName, out var target, out _))
                    return false;

                var cnameRRset = atOwner.
                                     Where(rr => rr.Type == DNSResourceRecordTypes.CNAME ||
                                                 rr is RRSIG signature && signature.TypeCovered == DNSResourceRecordTypes.CNAME).
                                     ToList();

                // A CNAME synthesized from a DNAME (RFC 6672 §5.3.1) is unsigned;
                // the DNAME and its signature are what a validator verifies it by.
                foreach (var dname in response.Answers.OfType<DNAME>())
                {
                    if (DNAME.TrySubstitute(owner, dname.DomainName, dname.Target, out var rewritten) == DNAMESubstitution.Redirected &&
                        rewritten is not null && rewritten == target)
                    {
                        cnameRRset.AddRange(response.Answers.
                                                Where(rr => rr.DomainName == dname.DomainName &&
                                                            (rr.Type == DNSResourceRecordTypes.DNAME ||
                                                             rr is RRSIG signature && signature.TypeCovered == DNSResourceRecordTypes.DNAME)));
                    }
                }

                if (cnameRRset.Any(rr => rr.EndOfLife <= now))
                    return false;

                answers.AddRange(cnameRRset.Where(rr => !answers.Contains(rr)));

                owner = target;

            }

            // An alias loop.
            return false;

        }

        #endregion

        #region (private static) RRsetType(ResourceRecord)

        /// <summary>
        /// The type of the RRset a record belongs to for caching purposes: its own
        /// type, or for an RRSIG the type it covers (RFC 4034 §3.1.1).
        /// </summary>
        private static DNSResourceRecordTypes RRsetType(IDNSResourceRecord ResourceRecord)

            => ResourceRecord is RRSIG signature
                   ? signature.TypeCovered
                   : ResourceRecord.Type;

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
                           Original.Timeout,
                           Original.Runtime
                       );
            }

            return Original;

        }

        #endregion


        #region ComputeNegativeCacheTTL(Response)

        /// <summary>
        /// How long a negative answer may be cached.
        /// </summary>
        /// <remarks>
        /// RFC 2308 §4: the TTL of a negative answer is "the minimum of the MINIMUM
        /// field of the SOA record and the TTL of the SOA itself". Both bounds
        /// matter — MINIMUM is the value the zone operator set aside for exactly
        /// this purpose, and the SOA's own TTL caps how long the record that
        /// carried it may be held. Using only the record TTL, as this did before,
        /// ignores the field RFC 2308 repurposed for the job.
        ///
        /// Falls back to <see cref="DefaultNegativeCacheTTL"/> when the responder
        /// sent no SOA: without one nothing authorizes caching the answer for any
        /// particular length of time.
        /// </remarks>
        /// <param name="Response">The negative response.</param>
        public static TimeSpan ComputeNegativeCacheTTL(DNSInfo Response)
        {

            var soa = Response.Authorities.OfType<SOA>().FirstOrDefault();

            if (soa is null)
                return DefaultNegativeCacheTTL;

            return soa.Minimum < soa.TimeToLive
                       ? soa.Minimum
                       : soa.TimeToLive;

        }

        #endregion

        #region AddNoData    (DomainName, RecordType, TTL, Response = null)

        /// <summary>
        /// Cache a NODATA response (NoError with no matching answers) for
        /// a specific record type. The cache is keyed per (domain, type)
        /// so that a NODATA for AAAA does not suppress valid A records.
        /// </summary>
        /// <param name="DomainName">The queried domain name.</param>
        /// <param name="RecordType">The record type that returned no data.</param>
        /// <param name="TTL">The time to cache this NODATA entry.</param>
        /// <param name="Response">The NODATA response itself, to be handed out again on a cache hit.</param>
        public void AddNoData(DNSServiceName           DomainName,
                              DNSResourceRecordTypes   RecordType,
                              TimeSpan                 TTL,
                              DNSInfo?                 Response   = null)
        {

            var key = $"{DomainName}|{(UInt16) RecordType}";

            noDataCache[key] = (Timestamp.Now + TTL, Response);

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

            => TryGetNoData(DomainName, RecordType, out _);

        #endregion

        #region TryGetNoData (DomainName, RecordType, out Response)

        /// <summary>
        /// Check whether a NODATA response is cached for the given domain and
        /// record type, and get that response if it was cached with the entry.
        /// </summary>
        /// <param name="DomainName">The queried domain name.</param>
        /// <param name="RecordType">The record type to check.</param>
        /// <param name="Response">The NODATA response, or null if the entry was cached without one.</param>
        public Boolean TryGetNoData(DNSServiceName           DomainName,
                                    DNSResourceRecordTypes   RecordType,
                                    out DNSInfo?             Response)
        {

            var key = $"{DomainName}|{(UInt16) RecordType}";

            if (noDataCache.TryGetValue(key, out var entry))
            {

                if (entry.EndOfLife > Timestamp.Now)
                {
                    Response = entry.Response;
                    return true;
                }

                noDataCache.TryRemove(key, out _);

            }

            Response = null;
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
        public Boolean IsNameNegativelyCachedByNSEC(String DomainName)
        {

            // The caller does not know which zone holds the name — that is what
            // it is asking. So try every ancestor as a zone: for a.b.example.
            // that is a.b.example., b.example., example. and the root. Bounded
            // by the label count, and it removes the need to guess a zone from
            // the shape of the name, which cannot be done reliably.
            var labels = DomainName.TrimEnd('.').Split('.');

            for (var skip = 0; skip <= labels.Length; skip++)
                if (IsNameNegativelyCachedByNSEC(DomainName, String.Join('.', labels.Skip(skip)) + "."))
                    return true;

            return false;

        }

        /// <summary>
        /// Check if a domain name falls within a cached NSEC range for a known
        /// zone, proving its non-existence without a network query (RFC 8198).
        /// </summary>
        /// <param name="DomainName">The domain name to check.</param>
        /// <param name="ZoneName">The zone to check NSEC ranges for.</param>
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

                var ownerName = record.DomainName.FullName;
                var nextName  = record.NextDomainName.FullName;

                // RFC 4034 §6.1 canonical order — label by label from the
                // rightmost, which is emphatically not string order. Comparing
                // the dotted strings instead lets "c.z.example." look like it
                // sits between "b.example." and "d.example.", and a resolver
                // that believes that denies a name the NSEC never spoke about.
                var afterOwner = DenialOfExistenceValidator.CompareCanonical(queryName, ownerName) > 0;
                var beforeNext = DenialOfExistenceValidator.CompareCanonical(queryName, nextName)  < 0;

                // The last NSEC of a zone points back at the apex, so its span
                // wraps: everything after the owner, or before the next.
                var wraps      = DenialOfExistenceValidator.CompareCanonical(nextName, ownerName) <= 0;

                if (wraps ? (afterOwner || beforeNext)
                          : (afterOwner && beforeNext))
                    return true;

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
                        logger.LogDebug(
                            "Removed '{DNSServiceName}' from DNS cache because all records expired",
                            expiredEntry.Key
                        );
                        dnsCache.TryRemove(expiredEntry.Key, out _);
                    }

                    // Clean up expired NODATA entries
                    var expiredNoDataEntries = noDataCache.
                                                   Where(entry => entry.Value.EndOfLife < now).
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
                    logger.LogError(e, "Error during DNS cache clean up");
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
