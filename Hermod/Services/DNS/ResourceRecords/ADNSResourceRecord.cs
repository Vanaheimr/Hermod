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
    /// Base Resource Record class for objects returned in 
    /// answers, authorities and additional record DNS responses. 
    /// </summary>
    public abstract class ADNSResourceRecord
    {

        #region Properties

        #region Name

        private readonly String _Name;

        public String Name
        {
            get
            {
                return _Name;
            }
        }

        #endregion

        #region Type

        private readonly DNSResourceRecordTypes _Type;

        public DNSResourceRecordTypes Type
        {
            get
            {
                return _Type;
            }
        }

        #endregion

        #region Class

        private readonly DNSQueryClasses _Class;

        public DNSQueryClasses Class
        {
            get
            {
                return _Class;
            }
        }

        #endregion

        #region TimeToLive

        private readonly TimeSpan _TimeToLive;

        public TimeSpan TimeToLive
        {
            get
            {
                return _TimeToLive;
            }
        }

        #endregion

        #region RText

        private readonly String _RText;

        public String RText
        {
            get
            {
                return _RText;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region DNSResourceRecord(Name, Type, Class, TimeToLive)

        public ADNSResourceRecord(String                  Name,
                                  DNSResourceRecordTypes  Type,
                                  DNSQueryClasses         Class,
                                  TimeSpan                TimeToLive)
        {

            this._Name        = Name;
            this._Type        = Type;
            this._Class       = Class;
            this._TimeToLive  = TimeToLive;

        }

        #endregion

        #region DNSResourceRecord(Name, Type, Class, TimeToLive, RText)

        public ADNSResourceRecord(String                  Name,
                                  DNSResourceRecordTypes  Type,
                                  DNSQueryClasses         Class,
                                  TimeSpan                TimeToLive,
                                  String                  RText)
        {

            this._Name        = Name;
            this._Type        = Type;
            this._Class       = Class;
            this._TimeToLive  = TimeToLive;
            this._RText       = RText;

        }

        #endregion

        #endregion


        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Concat("Name=", _Name, ", Type=", _Type, ", Class=", _Class, ", TTL=", _TimeToLive);
        }

        #endregion

    }

}