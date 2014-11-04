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
using System.Text;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.DNS
{

    public class DNSCacheEntry
    {

        #region Properties

        #region RefreshTime

        private DateTime _RefreshTime;

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
