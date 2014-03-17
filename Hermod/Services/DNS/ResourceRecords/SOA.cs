/*
 * Copyright (c) 2013 Achim Friedland <achim.friedland@belectric.com>
 * This file is part of eMI3 OICP <http://www.github.com/BelectricDrive/eMI3_OICP>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System;

#endregion

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// Start of Authority Resource Record
    /// </summary>
    public class SOA : ADNSResourceRecord
    {

        #region Properties

        #region Server

        private readonly String _Server;

        public String Server
        {
            get
            {
                return _Server;
            }
        }

        #endregion

        #region Email

        private readonly String _Email;

        public String Email
        {
            get
            {
                return _Email;
            }
        }

        #endregion

        #region Serial

        private readonly Int64 _Serial;

        public Int64 Serial
        {
            get
            {
                return _Serial;
            }
        }

        #endregion

        #region Refresh

        private readonly Int64 _Refresh;

        public Int64 Refresh
        {
            get
            {
                return _Refresh;
            }
        }

        #endregion

        #region Retry

        private readonly Int64 _Retry;

        public Int64 Retry
        {
            get
            {
                return _Retry;
            }
        }

        #endregion

        #region Expire

        private readonly Int64 _Expire;

        public Int64 Expire
        {
            get
            {
                return _Expire;
            }
        }

        #endregion

        #region Minimum

        private readonly Int64 _Minimum;

        public Int64 Minimum
        {
            get
            {
                return _Minimum;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        public SOA(String   _Name,
                   DNSResourceRecordTypes    _Type,
                   DNSQueryClasses  _Class,
                   TimeSpan         TimeToLive,
                   String   _Server,
                   String   _Email,
                   Int64    _Serial,
                   Int64    _Refresh,
                   Int64    _Retry,
                   Int64    _Expire,
                   Int64    _Minimum)

            : base(_Name, _Type, _Class, TimeToLive)

        {

            this._Server      = Server;
            this._Email       = Email;
            this._Serial      = Serial;
            this._Refresh     = Refresh;
            this._Retry       = Retry;
            this._Expire      = Expire;
            this._Minimum     = Minimum;

        }

        #endregion

    }

}