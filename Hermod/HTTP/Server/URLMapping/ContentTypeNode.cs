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
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class ContentTypeNode
    {

        #region Properties

        #region HTTPContentType

        private readonly HTTPContentType _HTTPContentType;

        /// <summary>
        /// The HTTP content type for this service.
        /// </summary>
        public HTTPContentType HTTPContentType
        {
            get
            {
                return _HTTPContentType;
            }
        }

        #endregion

        #region RequestHandler

        private readonly HTTPDelegate _RequestHandler;

        public HTTPDelegate RequestHandler
        {
            get
            {
                return _RequestHandler;
            }
        }

        #endregion

        #region HTTPContentTypeAuthentication

        private readonly HTTPAuthentication _HTTPContentTypeAuthentication;

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP content type authentication.
        /// </summary>
        public HTTPAuthentication HTTPContentTypeAuthentication
        {
            get
            {
                return _HTTPContentTypeAuthentication;
            }
        }

        #endregion

        #region DefaultErrorHandler

        private readonly HTTPDelegate _DefaultErrorHandler;

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate DefaultErrorHandler
        {
            get
            {
                return _DefaultErrorHandler;
            }
        }

        #endregion

        #region ErrorHandlers

        private readonly Dictionary<HTTPStatusCode, HTTPDelegate> _ErrorHandlers;

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate> ErrorHandlers
        {
            get
            {
                return _ErrorHandlers;
            }
        }

        #endregion

        #region URIReplacement

        private readonly URIReplacement _AllowReplacement;

        public URIReplacement AllowReplacement
        {
            get
            {
                return _AllowReplacement;
            }
        }

        #endregion

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new HTTP ContentTypeNode.
        /// </summary>
        /// <param name="HTTPContentType">The http content type for this service.</param>
        /// <param name="HTTPContentTypeAuthentication">This and all subordinated nodes demand an explicit HTTP content type authentication.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        /// <param name="AllowReplacement">How to handle duplicate URI handlers.</param>
        internal ContentTypeNode(HTTPContentType     HTTPContentType,
                                 HTTPAuthentication  HTTPContentTypeAuthentication   = null,
                                 HTTPDelegate        RequestHandler                  = null,
                                 HTTPDelegate        DefaultErrorHandler             = null,
                                 URIReplacement      AllowReplacement                = URIReplacement.Fail)
        {

            if (HTTPContentType == null)
                throw new ArgumentException("HTTPContentType == null!");

            this._HTTPContentType                = HTTPContentType;
            this._HTTPContentTypeAuthentication  = HTTPContentTypeAuthentication;
            this._RequestHandler                 = RequestHandler;
            this._DefaultErrorHandler            = DefaultErrorHandler;
            this._AllowReplacement               = AllowReplacement;

            this._ErrorHandlers                  = new Dictionary<HTTPStatusCode, HTTPDelegate>();

        }

        #endregion


    }

}
