/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A cache for DNS entries.
    /// </summary>
    public class DNSCache
    {

        #region Data

        private readonly Dictionary<String, DNSCacheEntry>  dnsCache;
        private readonly Timer                              cleanUpTimer;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS cache.
        /// </summary>
        /// <param name="CleanUpEvery">How often to remove outdated entries from DNS cache.</param>
        public DNSCache(TimeSpan? CleanUpEvery = null)
        {

            dnsCache = new Dictionary<String, DNSCacheEntry> {
                {
                    "localhost",  new DNSCacheEntry(RefreshTime:  Timestamp.Now,
                                                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),
                                                    DNSInfo:      new DNSInfo(Origin:               new IPSocket(IPv4Address.Parse("127.0.0.1"), IPPort.Parse(53)),
                                                                              QueryId:              0,
                                                                              IsAuthorativeAnswer:  true,
                                                                              IsTruncated:          false,
                                                                              RecursionDesired:     false,
                                                                              RecursionAvailable:   false,
                                                                              ResponseCode:         DNSResponseCodes.NoError,
                                                                              Answers:              new ADNSResourceRecord[] {
                                                                                                        new    A("localhost", DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv4Address.Parse("127.0.0.1")),
                                                                                                        new AAAA("localhost", DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv6Address.Parse("::1"))
                                                                                                    },
                                                                              Authorities:          Array.Empty<ADNSResourceRecord>(),
                                                                              AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                                                              IsValid:              true,
                                                                              IsTimeout:            false,
                                                                              Timeout:              TimeSpan.Zero)
                                                                 )
                },
                {
                    "loopback",   new DNSCacheEntry(RefreshTime:  Timestamp.Now,
                                                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),
                                                    DNSInfo:      new DNSInfo(Origin:               new IPSocket(IPv4Address.Parse("127.0.0.1"), IPPort.Parse(53)),
                                                                              QueryId:              0,
                                                                              IsAuthorativeAnswer:  true,
                                                                              IsTruncated:          false,
                                                                              RecursionDesired:     false,
                                                                              RecursionAvailable:   false,
                                                                              ResponseCode:         DNSResponseCodes.NoError,
                                                                              Answers:              new ADNSResourceRecord[] {
                                                                                                        new    A("loopback", DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv4Address.Parse("127.0.0.1")),
                                                                                                        new AAAA("loopback", DNSQueryClasses.IN, TimeSpan.FromDays(3650), IPv6Address.Parse("::1"))
                                                                                                    },
                                                                              Authorities:          Array.Empty<ADNSResourceRecord>(),
                                                                              AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                                                              IsValid:              true,
                                                                              IsTimeout:            false,
                                                                              Timeout:              TimeSpan.Zero)
                                                                 )
                }
            };

            cleanUpTimer = new Timer(RemoveExpiredCacheEntries,
                                     null,
                                     CleanUpEvery ?? TimeSpan.FromMinutes(1), // delay one round!
                                     CleanUpEvery ?? TimeSpan.FromMinutes(1));

        }

        #endregion


        #region Add(Domainname, DNSInformation)

        /// <summary>
        /// Add the given DNS information to the DNS cache.
        /// </summary>
        /// <param name="Domainname">The domain name.</param>
        /// <param name="DNSInformation">The DNS information to add.</param>
        public DNSCache Add(String   Domainname,
                            DNSInfo  DNSInformation)
        {

            lock (dnsCache)
            {

                if (!dnsCache.ContainsKey(Domainname))
                    dnsCache.Add(Domainname,
                                 new DNSCacheEntry(
                                     Timestamp.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                                     Timestamp.Now + DNSInformation.Answers.First().TimeToLive,
                                     DNSInformation
                                 ));

                else
                {

                    if (DNSInformation.Answers.Any())
                    {

                        // ToDo: Merge of DNS responses!
                        dnsCache[Domainname] = new DNSCacheEntry(
                                                   Timestamp.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                                                   Timestamp.Now + DNSInformation.Answers.First().TimeToLive,
                                                   DNSInformation
                                               );

                    }

                    // ToDo: Add negative answers to avoid asking again and again...

                }

                return this;

            }

        }

        #endregion

        #region Add(Domainname, Origin, params ResourceRecords)

        /// <summary>
        /// Add the given DNS resource record to the DNS cache.
        /// </summary>
        /// <param name="Domainname">The domain name.</param>
        /// <param name="Origin">The origin of the DNS resource record.</param>
        /// <param name="ResourceRecords">The DNS resource records to add.</param>
        public DNSCache Add(String                       Domainname,
                            IPSocket                     Origin,
                            params ADNSResourceRecord[]  ResourceRecords)
        {

            lock (dnsCache)
            {

                if (!dnsCache.ContainsKey(Domainname))
                    dnsCache.Add(Domainname,
                                 new DNSCacheEntry(
                                     Timestamp.Now + TimeSpan.FromSeconds(ResourceRecords.Min(rr => rr.TimeToLive.TotalSeconds) / 2),
                                     Timestamp.Now + ResourceRecords.Min(rr => rr.TimeToLive),
                                     new DNSInfo(Origin:               Origin,
                                                 QueryId:              Random.Shared.Next(),
                                                 IsAuthorativeAnswer:  false,
                                                 IsTruncated:          false,
                                                 RecursionDesired:     false,
                                                 RecursionAvailable:   false,
                                                 ResponseCode:         DNSResponseCodes.NoError,
                                                 Answers:              ResourceRecords,
                                                 Authorities:          Array.Empty<ADNSResourceRecord>(),
                                                 AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                                 IsValid:              true,
                                                 IsTimeout:            false,
                                                 Timeout:              TimeSpan.Zero)
                                 ));

                return this;

            }

        }

        #endregion

        #region GetDNSInfo(DomainName)

        /// <summary>
        /// Get the cached DNS information from the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        public DNSInfo? GetDNSInfo(String DomainName)
        {

            lock (dnsCache)
            {

                if (dnsCache.TryGetValue(DomainName, out DNSCacheEntry? cacheEntry))
                    return cacheEntry.DNSInfo;

                return null;

            }

        }

        #endregion


        #region (private, Timer) RemoveExpiredCacheEntries(State)

        private void RemoveExpiredCacheEntries(Object? State)
        {

            if (Monitor.TryEnter(dnsCache))
            {

                try
                {

                    var now             = Timestamp.Now;

                    // Info: Will remove all resource records even when only a single one is expired!
                    var expiredEntries  = dnsCache.
                                              Where(entry => entry.Value.EndOfLife < now).
                                              ToArray();

                    expiredEntries.ForEach(entry => dnsCache.Remove(entry.Key));

                }

                catch (Exception e)
                {
                    Debug.WriteLine("[" + Timestamp.Now + "] An exception occured during DNS cache cleanup: " + e.Message);
                }

                finally
                {
                    Monitor.Exit(dnsCache);
                }

            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => dnsCache.Count + " cached DNS entries";

        #endregion

    }

}
