/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.IO;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// MailExchange Resource Record
    /// </summary>
    public class MX : ADNSResourceRecord
    {

        #region Data

        public const UInt16 TypeId = 15;

        #endregion

        #region Properties

        #region Preference

        private readonly Int32 _Preference;

        public Int32 Preference
        {
            get
            {
                return _Preference;
            }
        }

        #endregion

        #region Exchange

        private readonly String _Exchange;

        public String Exchange
        {
            get
            {
                return _Exchange;
            }
        }

        #endregion

        #endregion

        #region Constructor

        #region MX(Stream)

        public MX(Stream  Stream)

            : base(Stream, TypeId)

        {

            this._Preference  = (Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue);
            this._Exchange    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region MX(Name, Stream)

        public MX(String  Name,
                  Stream  Stream)

            : base(Name, TypeId, Stream)

        {

            this._Preference  = (Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue);
            this._Exchange    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region MX(Name, Class, TimeToLive, Preference, Exchange)

        public MX(String           Name,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  Int32            Preference,
                  String           Exchange)

            : base(Name, TypeId, Class, TimeToLive)

        {

            this._Preference  = Preference;
            this._Exchange    = Exchange;

        }

        #endregion

        #endregion

    }

}
