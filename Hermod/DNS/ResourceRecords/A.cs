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
using System.IO;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// A - DNS Resource Record
    /// </summary>
    public class A : ADNSResourceRecord
    {

        #region Data

        public const UInt16 TypeId = 1;

        #endregion

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

        public A(Stream  Stream)
            : base(Stream, TypeId)
        {
            this._IPv4Address  = new IPv4Address(Stream);
        }

        #endregion

        #region A(Name, Stream)

        public A(String  Name,
                 Stream  Stream)

            : base(Name, TypeId, Stream)

        {
            this._IPv4Address  = new IPv4Address(Stream);
        }

        #endregion

        #region A(Name, Class, TimeToLive, IPv4Address)

        public A(String           Name,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : base(Name, TypeId, Class, TimeToLive, IPv4Address.ToString())

        {
            this._IPv4Address  = IPv4Address;
        }

        #endregion

        #endregion

    }

}
