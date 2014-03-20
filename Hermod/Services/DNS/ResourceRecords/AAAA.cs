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
    /// AAAA - DNS Resource Record
    /// </summary>
    public class AAAA : ADNSResourceRecord
    {

        #region Properties

        private readonly IPv6Address _IPv6Address;

        public IPv6Address IPv6Address
        {
            get
            {
                return _IPv6Address;
            }
        }

        #endregion

        #region Constructor

        public AAAA(String           Name,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(Name, DNSResourceRecordTypes.AAAA, Class, TimeToLive, IPv6Address.ToString())

        {
            this._IPv6Address = IPv6Address;
        }

        #endregion

    }

}
