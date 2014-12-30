/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.DNS
{

    public class DNSCache
    {

        #region Data

        private readonly Dictionary<String, DNSCacheEntry>  InternalDNSCache;
        private readonly Timer                              CleanUpTimer;

        #endregion

        #region Constructor(s)

        #region DNSCache()

        public DNSCache()
            : this(TimeSpan.FromMinutes(1))
        { }

        #endregion

        #region DNSCache(CleanUpTime)

        public DNSCache(TimeSpan CleanUpTime)
        {
            this.InternalDNSCache  = new Dictionary<String, DNSCacheEntry>();
            this.CleanUpTimer      = new Timer(CleanUp, null, TimeSpan.FromMinutes(1), CleanUpTime);
        }

        #endregion

        #endregion


        #region Add(Domainname, Response)

        public DNSCache Add(String   Domainname,
                            DNSInfo  Response)
        {

            lock (InternalDNSCache)
            {

                DNSCacheEntry CacheEntry = null;

                if (!InternalDNSCache.TryGetValue(Domainname, out CacheEntry))
                    InternalDNSCache.Add(Domainname, new DNSCacheEntry(
                                                         DateTime.Now + TimeSpan.FromSeconds(Response.Answers.First().TimeToLive.TotalSeconds / 2),
                                                         DateTime.Now + Response.Answers.First().TimeToLive,
                                                         Response));

                else
                {
                    // ToDo: Merge of DNS responses!
                    InternalDNSCache[Domainname] = new DNSCacheEntry(
                                                       DateTime.Now + TimeSpan.FromSeconds(Response.Answers.First().TimeToLive.TotalSeconds / 2),
                                                       DateTime.Now + Response.Answers.First().TimeToLive,
                                                       Response);
                }

                return this;

            }

        }

        #endregion

        #region Add(Domainname, ResourceRecord)

        public DNSCache Add(String              Domainname,
                            ADNSResourceRecord  ResourceRecord)
        {

            lock (InternalDNSCache)
            {

                DNSCacheEntry CacheEntry = null;

                if (!InternalDNSCache.TryGetValue(Domainname, out CacheEntry))
                    InternalDNSCache.Add(Domainname, new DNSCacheEntry(
                                                         DateTime.Now + TimeSpan.FromSeconds(ResourceRecord.TimeToLive.TotalSeconds / 2),
                                                         DateTime.Now + ResourceRecord.TimeToLive,
                                                         new DNSInfo(QueryId:              new Random().Next(),
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


        #region (private) CleanUp(State)

        private void CleanUp(Object State)
        {

            if (Monitor.TryEnter(InternalDNSCache))
            {

                try
                {

                }

                catch (Exception e)
                {
                }

                finally
                {
                    Monitor.Exit(InternalDNSCache);
                }

            }

        }

        #endregion

    }

}
