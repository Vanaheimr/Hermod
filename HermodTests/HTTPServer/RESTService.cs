/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

using de.ahzf.Vanaheimr.Hermod.HTTP;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// A REST service serving */*.
    /// </summary>
    public class RESTService : AHTTPService, IRESTService
    {

        #region Constructor(s)

        #region RESTService()

        /// <summary>
        /// Creates a new RESTService_HTML.
        /// </summary>
        public RESTService()
            : base(HTTPContentType.ALL)
        { }

        #endregion

        #region RESTService(IHTTPConnection)

        /// <summary>
        /// Creates a new RESTService_HTML.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        public RESTService(IHTTPConnection IHTTPConnection)
            : base(IHTTPConnection, HTTPContentType.ALL, "HermodDemo.resources.")
        {
            this.CallingAssembly = Assembly.GetExecutingAssembly();
        }

        #endregion

        #endregion


        #region GetRoot()

        public HTTPResponse GET_Root()
        {
            return new HTTPResult<Object>(IHTTPConnection.InHTTPRequest, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region /HelloWorld

        #region HelloWorld_OPTIONS()

        public HTTPResponse HelloWorld_OPTIONS()
        {
            return new HTTPResult<Object>(IHTTPConnection.InHTTPRequest, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region HelloWorld_HEAD()

        public HTTPResponse HelloWorld_HEAD()
        {
            return new HTTPResult<Object>(IHTTPConnection.InHTTPRequest, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #region HelloWorld_GET()

        public HTTPResponse HelloWorld_GET()
        {
            return new HTTPResult<Object>(IHTTPConnection.InHTTPRequest, HTTPStatusCode.NotAcceptable).Error;
        }

        #endregion

        #endregion

    }

}
