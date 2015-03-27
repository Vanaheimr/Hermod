/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class HTTPMethodNode
    {

        #region Properties

        #region HTTPMethod

        private readonly HTTPMethod _HTTPMethod;

        /// <summary>
        /// The http method for this service.
        /// </summary>
        public HTTPMethod HTTPMethod
        {
            get
            {
                return _HTTPMethod;
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

        #region HTTPMethodAuthentication

        private readonly HTTPAuthentication _HTTPMethodAuthentication;

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP method authentication.
        /// </summary>
        public HTTPAuthentication HTTPMethodAuthentication
        {
            get
            {
                return _HTTPMethodAuthentication;
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

        #region HTTPMethods

        private readonly Dictionary<HTTPContentType, ContentTypeNode> _HTTPContentTypes;

        /// <summary>
        /// A mapping from HTTPContentTypes to HTTPContentTypeNodes.
        /// </summary>
        public Dictionary<HTTPContentType, ContentTypeNode> HTTPContentTypes
        {
            get
            {
                return _HTTPContentTypes;
            }
        }

        #endregion

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new HTTPMethodNode.
        /// </summary>
        /// <param name="HTTPMethod">The http method for this service.</param>
        /// <param name="HTTPMethodAuthentication">This and all subordinated nodes demand an explicit HTTP method authentication.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        internal HTTPMethodNode(HTTPMethod          HTTPMethod,
                                HTTPAuthentication  HTTPMethodAuthentication    = null,
                                HTTPDelegate        RequestHandler              = null,
                                HTTPDelegate        DefaultErrorHandler         = null)

        {

            if (HTTPMethod == null)
                throw new ArgumentException("HTTPMethod == null!");

            this._HTTPMethod                = HTTPMethod;
            this._HTTPMethodAuthentication  = HTTPMethodAuthentication;
            this._RequestHandler            = RequestHandler;
            this._DefaultErrorHandler       = DefaultErrorHandler;

            this._ErrorHandlers             = new Dictionary<HTTPStatusCode,  HTTPDelegate>();
            this._HTTPContentTypes          = new Dictionary<HTTPContentType, ContentTypeNode>();

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate        HTTPDelegate,
                               HTTPContentType     HTTPContentType            = null,
                               HTTPAuthentication  ContentTypeAuthentication  = null,
                               HTTPDelegate        DefaultErrorHandler        = null)

        {

            ContentTypeNode _ContentTypeNode = null;

            if (HTTPContentType == null)
            {
                //RequestHandler       = HTTPDelegate;
                //DefaultErrorHandler  = DefaultErrorHandler;
            }

            else if (!_HTTPContentTypes.TryGetValue(HTTPContentType, out _ContentTypeNode))
            {
                _ContentTypeNode = new ContentTypeNode(HTTPContentType, ContentTypeAuthentication, HTTPDelegate, DefaultErrorHandler);
                _HTTPContentTypes.Add(HTTPContentType, _ContentTypeNode);
            }

            else
                throw new ArgumentException("Duplicate HTTP API definition!");

        }

        #endregion

    }

}
