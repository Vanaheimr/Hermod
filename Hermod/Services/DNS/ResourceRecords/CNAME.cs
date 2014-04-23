/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.IO;

#endregion

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// CNAME - DNS Resource Record
    /// </summary>
    public class CNAME : ADNSResourceRecord
    {

        #region Data

        public const UInt16 TypeId = 5;

        #endregion

        #region Properties

        #region Text

        private readonly String _Text;

        public String Text
        {
            get 
            {
                return _Text;
            }
        }

        #endregion

        #endregion

        #region Constructor

        #region CNAME(Stream)

        public CNAME(Stream  Stream)
            : base(Stream, TypeId)
        {
            this._Text  = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region CNAME(Name, Stream)

        public CNAME(String  Name,
                     Stream  Stream)

            : base(Name, TypeId, Stream)

        {
            this._Text  = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region CNAME(Name, Class, TimeToLive, RText)

        public CNAME(String           Name,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     String           RText)

            : base(Name, TypeId, Class, TimeToLive, RText)

        {
            this._Text  = RText;
        }

        #endregion

        #endregion

    }

}
