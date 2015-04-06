/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// A cache for DNS entries.
    /// </summary>
    public class DNSCache
    {

        #region Data

        private readonly Dictionary<String, DNSCacheEntry>  _DNSCache;
        private readonly Timer                              _CleanUpTimer;

        #endregion

        #region Constructor(s)

        #region DNSCache()

        public DNSCache()
            : this(TimeSpan.FromMinutes(1))
        { }

        #endregion

        #region DNSCache(CleanUpEvery)

        public DNSCache(TimeSpan CleanUpEvery)
        {
            this._DNSCache      = new Dictionary<String, DNSCacheEntry>();
            this._CleanUpTimer  = new Timer(RemoveExpiredCacheEntries, null, TimeSpan.FromMinutes(1), CleanUpEvery);
        }

        #endregion

        #endregion


        #region Add(Domainname, DNSInformation)

        public DNSCache Add(String    Domainname,
                            DNSInfo   DNSInformation)
        {

            lock (_DNSCache)
            {

                DNSCacheEntry CacheEntry = null;

                if (!_DNSCache.TryGetValue(Domainname, out CacheEntry))
                    _DNSCache.Add(Domainname, new DNSCacheEntry(
                                                         DateTime.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                                                         DateTime.Now + DNSInformation.Answers.First().TimeToLive,
                                                         DNSInformation));

                else
                {
                    // ToDo: Merge of DNS responses!
                    _DNSCache[Domainname] = new DNSCacheEntry(
                                                       DateTime.Now + TimeSpan.FromSeconds(DNSInformation.Answers.First().TimeToLive.TotalSeconds / 2),
                                                       DateTime.Now + DNSInformation.Answers.First().TimeToLive,
                                                       DNSInformation);
                }

                return this;

            }

        }

        #endregion

        #region Add(Domainname, Origin, ResourceRecord)

        public DNSCache Add(String              Domainname,
                            IPSocket            Origin,
                            ADNSResourceRecord  ResourceRecord)
        {

            lock (_DNSCache)
            {

                DNSCacheEntry CacheEntry = null;

                Debug.WriteLine("[" + DateTime.Now + "] Adding '" + Domainname + "' to the DNS cache!");

                if (!_DNSCache.TryGetValue(Domainname, out CacheEntry))
                    _DNSCache.Add(Domainname, new DNSCacheEntry(
                                                         DateTime.Now + TimeSpan.FromSeconds(ResourceRecord.TimeToLive.TotalSeconds / 2),
                                                         DateTime.Now + ResourceRecord.TimeToLive,
                                                         new DNSInfo(Origin:               Origin,
                                                                     QueryId:              new Random().Next(),
                                                                     IsAuthorativeAnswer:  false,
                                                                     IsTruncated:          false,
                                                                     RecursionDesired:     false,
                                                                     RecursionAvailable:   false,
                                                                     ResponseCode:         DNSResponseCodes.NoError,
                                                                     Answers:              new ADNSResourceRecord[1] { ResourceRecord },
                                                                     Authorities:          new ADNSResourceRecord[0],
                                                                     AdditionalRecords:    new ADNSResourceRecord[0])));

                else
                {
                    // ToDo: Merge of DNS responses!
 //                   InternalDNSCache[Domainname] = Response;
                }

                return this;

            }

        }

        #endregion

        #region GetDNSInfo(DomainName)

        public DNSInfo GetDNSInfo(String DomainName)
        {

            lock (_DNSCache)
            {

                DNSCacheEntry CacheEntry = null;

                if (_DNSCache.TryGetValue(DomainName, out CacheEntry))
                    return CacheEntry.DNSInfo;

                return null;

            }

        }

        #endregion


        #region (private, Timer) RemoveExpiredCacheEntries(State)

        private void RemoveExpiredCacheEntries(Object State)
        {

            if (Monitor.TryEnter(_DNSCache))
            {

                try
                {

                    var Now             = DateTime.Now;

                    var ExpiredEntries  = _DNSCache.
                                              Where(entry => entry.Value.EndOfLife < Now).
                                              ToArray();

                    ExpiredEntries.ForEach(entry => _DNSCache.Remove(entry.Key));

#if DEBUG
                    ExpiredEntries.ForEach(entry => Debug.WriteLine("[" + Now + "] Removed '" + entry.Key + "' from the DNS cache!"));
#endif

                }

                catch (Exception e)
                {
                }

                finally
                {
                    Monitor.Exit(_DNSCache);
                }

            }

        }

        #endregion


    }

}
