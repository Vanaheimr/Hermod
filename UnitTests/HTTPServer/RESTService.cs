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
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using de.ahzf.Hermod.HTTP;

#endregion

namespace de.ahzf.Hermod.UnitTests
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

        public HTTPResponseHeader GetRoot()
        {
            return Error406_NotAcceptable();
        }

        #endregion

        #region /HelloWorld

        #region HelloWorld_OPTIONS()

        public HTTPResponseHeader HelloWorld_OPTIONS()
        {
            return Error406_NotAcceptable();
        }

        #endregion

        #region HelloWorld_HEAD()

        public HTTPResponseHeader HelloWorld_HEAD()
        {
            return Error406_NotAcceptable();
        }

        #endregion

        #region HelloWorld_GET()

        public HTTPResponseHeader HelloWorld_GET()
        {
            return Error406_NotAcceptable();
        }

        #endregion

        #endregion

    }

}
