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

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class ContentTypeNode
    {

        #region Properties

        /// <summary>
        /// The content type for this service.
        /// </summary>
        public HTTPContentType ContentType { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit http method authentication.
        /// </summary>
        public Boolean ContentTypeAuthentication { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public MethodInfo ContentTypeErrorHandler { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public ConcurrentDictionary<HTTPStatusCode, MethodInfo> ContentTypeErrorHandlers { get; private set; }

        /// <summary>
        /// The method handler.
        /// </summary>
        public MethodInfo MethodHandler { get; private set; }

        #endregion

        #region Constructor(s)

        #region ContentTypeNode()

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ContentType"></param>
        /// <param name="MethodHandler"></param>
        /// <param name="ContentTypeAuthentication"></param>
        /// <param name="ContentTypeError"></param>
        public ContentTypeNode(HTTPContentType ContentType, MethodInfo MethodHandler = null, Boolean ContentTypeAuthentication = false, MethodInfo ContentTypeError = null)
        {
            this.ContentType                = ContentType;
            this.MethodHandler              = MethodHandler;
            this.ContentTypeAuthentication  = ContentTypeAuthentication;
            this.ContentTypeErrorHandler    = ContentTypeErrorHandler;
            this.ContentTypeErrorHandlers   = new ConcurrentDictionary<HTTPStatusCode, MethodInfo>();
        }

        #endregion

        #endregion


    }

}
