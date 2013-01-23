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
using System.Reflection;
using System.Collections.Concurrent;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class HTTPMethodNode
    {

        #region Properties

        /// <summary>
        /// The http method for this service.
        /// </summary>
        public HTTPMethod HTTPMethod { get; private set; }

        /// <summary>
        /// The method handler.
        /// </summary>
        public MethodInfo MethodHandler { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit http method authentication.
        /// </summary>
        public Boolean HTTPMethodAuthentication { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public MethodInfo HTTPMethodErrorHandler { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public ConcurrentDictionary<HTTPStatusCode, MethodInfo> HTTPMethodErrorHandlers { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public ConcurrentDictionary<HTTPContentType, ContentTypeNode> HTTPContentTypes { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPMethodNode()

        /// <summary>
        /// Creates a new HTTPMethodNode.
        /// </summary>
        /// <param name="HTTPMethod">The http method for this service.</param>
        /// <param name="MethodHandler">The method handler.</param>
        /// <param name="HTTPMethodAuthentication">This and all subordinated nodes demand an explicit http method authentication.</param>
        /// <param name="HTTPMethodErrorHandler">A general error handling method.</param>
        public HTTPMethodNode(HTTPMethod HTTPMethod, MethodInfo MethodHandler = null, Boolean HTTPMethodAuthentication = false, MethodInfo HTTPMethodErrorHandler = null, Boolean HandleContentTypes = false)
        {

            this.HTTPMethod                = HTTPMethod;
            this.MethodHandler             = MethodHandler;
            this.HTTPMethodAuthentication  = HTTPMethodAuthentication;
            this.HTTPMethodErrorHandler    = HTTPMethodErrorHandler;
            this.HTTPMethodErrorHandlers   = new ConcurrentDictionary<HTTPStatusCode, MethodInfo>();
            this.MethodHandler             = MethodHandler;

            if (HandleContentTypes)
                HTTPContentTypes = new ConcurrentDictionary<HTTPContentType, ContentTypeNode>(); 

        }

        #endregion

        #endregion

    }

}
