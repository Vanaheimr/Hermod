/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Net;
using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HTTPImplementationAttribute : Attribute
    {

        #region Properties

        #region HTTPContentType

        public HTTPContentType ContentType { get; private set; }

        #endregion

        #endregion

        #region Constructor(s)

        // Optional parameters on attributes seem to lead
        // to compilation errors on Microsoft .NET 4.0!

        #region HTTPServiceAttribute()

        public HTTPImplementationAttribute(String ContentType)
        {

            HTTPContentType _HTTPContentType;

            if (HTTPContentType.TryParseString(ContentType, out _HTTPContentType))
                this.ContentType = _HTTPContentType;

        }

        #endregion

        #endregion

    }

}
