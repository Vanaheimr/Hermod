/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// AAAA - DNS Resource Record
    /// </summary>
    public class AAAA : ADNSResourceRecord
    {

        #region Data

        public const UInt16 TypeId = 28;

        #endregion

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

        #region AAAA(Stream)

        public AAAA(Stream  Stream)
            : base(Stream, TypeId)
        {
            this._IPv6Address  = new IPv6Address(Stream);
        }

        #endregion

        #region AAAA(Name, Stream)

        public AAAA(String  Name,
                    Stream  Stream)

            : base(Name, TypeId, Stream)

        {
            this._IPv6Address  = new IPv6Address(Stream);
        }

        #endregion

        #region AAAA(Name, Class, TimeToLive, IPv6Address)

        public AAAA(String           Name,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(Name, TypeId, Class, TimeToLive, IPv6Address.ToString())

        {
            this._IPv6Address  = IPv6Address;
        }

        #endregion

        #endregion

    }

}
