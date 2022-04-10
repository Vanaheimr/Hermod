/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
    /// Start of Authority Resource Record
    /// </summary>
    public class SOA : ADNSResourceRecord
    {

        #region Data

        public const UInt16 TypeId = 6;

        #endregion

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

        #region Constructor

        #region SOA(Stream)

        public SOA(Stream  Stream)

            : base(Stream, TypeId)

        {

            this._Server   = DNSTools.ExtractName(Stream);
            this._Email    = DNSTools.ExtractName(Stream);
            this._Serial   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Refresh  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Retry    = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Expire   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Minimum  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region SOA(Name, Stream)

        public SOA(String  Name,
                   Stream  Stream)

            : base(Name, TypeId, Stream)

        {

            this._Server   = DNSTools.ExtractName(Stream);
            this._Email    = DNSTools.ExtractName(Stream);
            this._Serial   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Refresh  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Retry    = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Expire   = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;
            this._Minimum  = (Stream.ReadByte() & Byte.MaxValue) << 24 | (Stream.ReadByte() & Byte.MaxValue) << 16 | (Stream.ReadByte() & Byte.MaxValue) << 8 | Stream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region SOA(Name, Class, TimeToLive, ...)

        public SOA(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           Server,
                   String           Email,
                   Int64            Serial,
                   Int64            Refresh,
                   Int64            Retry,
                   Int64            Expire,
                   Int64            Minimum)

            : base(Name, TypeId, Class, TimeToLive)

        {

            this._Server   = Server;
            this._Email    = Email;
            this._Serial   = Serial;
            this._Refresh  = Refresh;
            this._Retry    = Retry;
            this._Expire   = Expire;
            this._Minimum  = Minimum;

        }

        #endregion

        #endregion

    }

}