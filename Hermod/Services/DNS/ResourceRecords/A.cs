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
using System.IO;

#endregion

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// A - DNS Resource Record
    /// </summary>
    public class A : ADNSResourceRecord
    {

        #region Properties

        private readonly IPv4Address _IPv4Address;

        public IPv4Address IPv4Address
        {
            get
            {
                return _IPv4Address;
            }
        }

        #endregion

        #region Constructor

        #region A(Stream)

        public A(Stream Stream)
            : base(Stream, DNSResourceRecordTypes.A)
        {
            this._IPv4Address    = new IPv4Address(Stream);
        }

        #endregion

        #region A(Name, Stream)

        public A(String           Name,
                 Stream           Stream)

            : base(Name, DNSResourceRecordTypes.A, Stream)

        {
            this._IPv4Address = new IPv4Address(Stream);
        }

        #endregion

        #region A(Name, Class, TimeToLive, IPv4Address)

        public A(String           Name,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : base(Name, DNSResourceRecordTypes.A, Class, TimeToLive, IPv4Address.ToString())

        {
            this._IPv4Address = IPv4Address;
        }

        #endregion

        #endregion

    }

}
