/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Text;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS cache entry.
    /// </summary>
    public class DNSCacheEntry
    {

        #region Properties

        #region RefreshTime

        private DateTime _RefreshTime;

        /// <summary>
        /// The timestamp of the last refresh.
        /// </summary>
        public DateTime RefreshTime
        {
            get
            {
                return _RefreshTime;
            }
        }

        #endregion

        #region EndOfLife

        private DateTime _EndOfLife;

        /// <summary>
        /// The timestamp when this entry gets invalidated.
        /// </summary>
        public DateTime EndOfLife
        {
            get
            {
                return _EndOfLife;
            }
        }

        #endregion

        #region DNSInfo

        private DNSInfo _DNSInfo;

        /// <summary>
        /// The cached DNS information.
        /// </summary>
        public DNSInfo DNSInfo
        {
            get
            {
                return _DNSInfo;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS cache entry.
        /// </summary>
        /// <param name="RefreshTime">The timestamp of the last refresh.</param>
        /// <param name="EndOfLife">The timestamp when this entry gets invalidated.</param>
        /// <param name="DNSInfo">The cached DNS information.</param>
        public DNSCacheEntry(DateTime  RefreshTime,
                             DateTime  EndOfLife,
                             DNSInfo   DNSInfo)
        {

            this._RefreshTime  = RefreshTime;
            this._EndOfLife    = EndOfLife;
            this._DNSInfo      = DNSInfo;

        }

        #endregion

    }

}
