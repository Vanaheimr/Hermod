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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;

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

        private readonly ConcurrentDictionary<DNSServiceName, DNSCacheEntry>  dnsCache = [];
        private readonly Timer                                            cleanUpTimer;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS cache.
        /// </summary>
        /// <param name="CleanUpEvery">How often to remove outdated entries from DNS cache.</param>
        public DNSCache(TimeSpan? CleanUpEvery = null)
        {

            #region Cache "localhost"

            dnsCache.TryAdd(
                DNSServiceName.Parse(DomainName.Localhost.FullName),
                new DNSCacheEntry(
                    RefreshTime:  Timestamp.Now,
                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),
                    DNSInfo:      new DNSInfo(
                                      Origin:                 new IPSocket(IPv4Address.Localhost, IPPort.Parse(53)),
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
                DNSServiceName.Parse(DomainName.Localhost.FullName),
                new DNSCacheEntry(
                    RefreshTime:  Timestamp.Now,
                    EndOfLife:    Timestamp.Now + TimeSpan.FromDays(3650),
                    DNSInfo:      new DNSInfo(
                                      Origin:                 new IPSocket(IPv4Address.Localhost, IPPort.Parse(53)),
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

            cleanUpTimer = new Timer(
                               RemoveExpiredCacheEntries,
                               null,
                               // Delayed start...
                               CleanUpEvery ?? TimeSpan.FromMinutes(1),
                               CleanUpEvery ?? TimeSpan.FromMinutes(1)
                           );

        }

        #endregion


        #region Add(DomainName, DNSInformation)

        /// <summary>
        /// Add the given DNS information to the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="DNSInformation">The DNS information to add.</param>
        public DNSCache Add(DNSServiceName  DomainName,
                            DNSInfo     DNSInformation)
        {

            if (!dnsCache.TryAdd(
                DomainName,
                new DNSCacheEntry(
                    Timestamp.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                    Timestamp.Now + DNSInformation.Answers.First().TimeToLive,
                    DNSInformation
                )))
            {

                if (DNSInformation.Answers.Any())
                {

                    // ToDo: Merge of DNS responses!
                    dnsCache[DomainName] = new DNSCacheEntry(
                                               Timestamp.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                                               Timestamp.Now + DNSInformation.Answers.First().TimeToLive,
                                               DNSInformation
                                           );

                }

                // ToDo: Add negative answers to avoid asking again and again...

            }

            return this;

        }

        #endregion


        #region Add(DomainName,         params ResourceRecords)

        /// <summary>
        /// Add the given DNS resource record to the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="ResourceRecords">The DNS resource records to add.</param>
        public DNSCache Add(DNSServiceName                   DomainName,
                            params IDNSResourceRecord[]  ResourceRecords)

            => Add(DomainName,
                   IPSocket.LocalhostV4(IPPort.DNS),
                   ResourceRecords);

        #endregion

        #region Add(DomainName, Origin, params ResourceRecords)

        /// <summary>
        /// Add the given DNS resource record to the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="Origin">The origin of the DNS resource record.</param>
        /// <param name="ResourceRecords">The DNS resource records to add.</param>
        public DNSCache Add(DNSServiceName                   DomainName,
                            IPSocket                     Origin,
                            params IDNSResourceRecord[]  ResourceRecords)
        {

            if (!dnsCache.TryAdd(
                              DomainName,
                              new DNSCacheEntry(
                                  Timestamp.Now + TimeSpan.FromSeconds(ResourceRecords.Min(rr => rr.TimeToLive.TotalSeconds) / 2),
                                  Timestamp.Now + ResourceRecords.Min(rr => rr.TimeToLive),
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


        #region GetDNSInfo    (DomainName)

        /// <summary>
        /// Get the cached DNS information from the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        public DNSInfo? GetDNSInfo(DNSServiceName DomainName)
        {

            if (dnsCache.TryGetValue(DomainName, out var cacheEntry))
                return cacheEntry.DNSInfo;

            return null;

        }

        #endregion

        #region TryGetDNSInfo (DomainName, out DNSInfo)

        /// <summary>
        /// Get the cached DNS information from the DNS cache.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        public Boolean TryGetDNSInfo(DNSServiceName                        DomainName,
                                     [NotNullWhen(true)] out DNSInfo?  DNSInfo)
        {

            if (dnsCache.TryGetValue(DomainName, out var cacheEntry))
            {
                DNSInfo = cacheEntry.DNSInfo;
                return true;
            }

            DNSInfo = null;
            return false;

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

                    expiredEntries.ForEach(entry => dnsCache.TryRemove(entry.Key, out _));

                }

                catch (Exception e)
                {
                    Debug.WriteLine($"[{Timestamp.Now}] An exception occured during DNS cache clean up: " + e.Message);
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

            => $"{dnsCache.Count} cached DNS entries";

        #endregion

    }

}
