/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using eu.Vanaheimr.Hermod.HTTP;
using System.Threading;

#endregion

namespace eu.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// A REST service serving */*.
    /// </summary>
    public abstract class AHTTPTestService : AHTTPService,
                                             IHTTPTestService
    {

        #region Constructor(s)

        #region AHTTPTestService()

        /// <summary>
        /// Creates a new HTTPTestService_HTML.
        /// </summary>
        public AHTTPTestService()
            : base()
        { }

        #endregion

        #region AHTTPTestService(HTTPContentType)

        /// <summary>
        /// Creates a new abstract http service.
        /// </summary>
        /// <param name="HTTPContentType">A content type.</param>
        public AHTTPTestService(HTTPContentType HTTPContentType)
            : base(HTTPContentType)
        { }

        #endregion

        #region AHTTPTestService(HTTPContentTypes)

        /// <summary>
        /// Creates a new abstract http service.
        /// </summary>
        /// <param name="HTTPContentTypes">A content type.</param>
        public AHTTPTestService(IEnumerable<HTTPContentType> HTTPContentTypes)
            : base(HTTPContentTypes)
        { }

        #endregion

        #region AHTTPTestService(IHTTPConnection, HTTPContentType)

        /// <summary>
        /// Creates a new abstract graph service.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        /// <param name="HTTPContentType">A http content type.</param>
        public AHTTPTestService(IHTTPConnection IHTTPConnection, HTTPContentType HTTPContentType)
            : base(IHTTPConnection, HTTPContentType)
        {
            this.Callback = new ThreadLocal<String>();
            this.Skip     = new ThreadLocal<UInt64>();
            this.Take     = new ThreadLocal<UInt64>();
        }

        #endregion

        #region AHTTPTestService(IHTTPConnection, HTTPContentTypes)

        /// <summary>
        /// Creates a new abstract http service.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        /// <param name="HTTPContentTypes">An enumeration of content types.</param>
        public AHTTPTestService(IHTTPConnection IHTTPConnection, IEnumerable<HTTPContentType> HTTPContentTypes)
            : base(IHTTPConnection, HTTPContentTypes)
        {
            this.Callback = new ThreadLocal<String>();
            this.Skip     = new ThreadLocal<UInt64>();
            this.Take     = new ThreadLocal<UInt64>();
        }

        #endregion

        #endregion


        #region GetRoot()

        public virtual HTTPResponse GET_Root()
        {
            return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region /HelloWorld

        #region HelloWorld_OPTIONS()

        public virtual HTTPResponse HelloWorld_OPTIONS()
        {
            return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region HelloWorld_HEAD()

        public virtual HTTPResponse HelloWorld_HEAD()
        {
            return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region HelloWorld_GET()

        public virtual HTTPResponse HelloWorld_GET()
        {
            return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #endregion

    }

}
