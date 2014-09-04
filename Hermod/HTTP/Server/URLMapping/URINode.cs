/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Text;
using System.Reflection;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using eu.Vanaheimr.Illias.Commons;
using System.Collections.Generic;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class URINode
    {

        #region Properties

        #region URITemplate

        private readonly String _URITemplate;

        /// <summary>
        /// The URL template for this service.
        /// </summary>
        public String URITemplate
        {
            get
            {
                return _URITemplate;
            }
        }

        #endregion

        #region URIRegex

        private readonly Regex _URIRegex;

        /// <summary>
        /// The URI regex for this service.
        /// </summary>
        public Regex URIRegex
        {
            get
            {
                return _URIRegex;
            }
        }

        #endregion

        #region ParameterCount

        private readonly UInt16 _ParameterCount;

        /// <summary>
        /// The number of parameters within this URLNode for shorting best-matching URLs.
        /// </summary>
        public UInt16 ParameterCount
        {
            get
            {
                return _ParameterCount;
            }
        }

        #endregion

        #region SortLength

        private readonly UInt16 _SortLength;

        /// <summary>
        /// The lenght of the minimalized URL template for shorting best-matching URLs.
        /// </summary>
        public UInt16 SortLength
        {
            get
            {
                return _SortLength;
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

        #region URIAuthentication

        private readonly HTTPAuthentication _URIAuthentication;

        /// <summary>
        /// This and all subordinated nodes demand an explicit URI authentication.
        /// </summary>
        public HTTPAuthentication URIAuthentication
        {
            get
            {
                return _URIAuthentication;
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

        private readonly Dictionary<HTTPMethod, HTTPMethodNode> _HTTPMethods;

        /// <summary>
        /// A mapping from HTTPMethods to HTTPMethodNodes.
        /// </summary>
        public Dictionary<HTTPMethod, HTTPMethodNode> HTTPMethods
        {
            get
            {
                return _HTTPMethods;
            }
        }

        #endregion

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new URLNode.
        /// </summary>
        /// <param name="URITemplate">The URI template for this service.</param>
        /// <param name="URIAuthentication">This and all subordinated nodes demand an explicit URI authentication.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        internal URINode(String              URITemplate,
                         HTTPAuthentication  URIAuthentication    = null,
                         HTTPDelegate        RequestHandler       = null,
                         HTTPDelegate        DefaultErrorHandler  = null)

        {

            URITemplate.FailIfNullOrEmpty();

            this._URITemplate           = URITemplate;
            this._URIAuthentication     = URIAuthentication;
            this._RequestHandler        = RequestHandler;
            this._DefaultErrorHandler   = DefaultErrorHandler;
            this._ErrorHandlers         = new Dictionary<HTTPStatusCode, HTTPDelegate>();
            this._HTTPMethods           = new Dictionary<HTTPMethod, HTTPMethodNode>();

            var _ReplaceLastParameter   = new Regex(@"\{[^/]+\}$");
            this._ParameterCount        = (UInt16) _ReplaceLastParameter.Matches(URITemplate).Count;
            var URLTemplate2            = _ReplaceLastParameter.Replace(URITemplate, "([^\n]+)");
            var URLTemplateWithoutVars  = _ReplaceLastParameter.Replace(URITemplate, "");

            var _ReplaceAllParameters   = new Regex(@"\{[^/]+\}");
            this._ParameterCount       += (UInt16) _ReplaceAllParameters.Matches(URLTemplate2).Count;
            this._URIRegex              = new Regex("^" + _ReplaceAllParameters.Replace(URLTemplate2, "([^/]+)"));
            this._SortLength            = (UInt16) _ReplaceAllParameters.Replace(URLTemplateWithoutVars, "").Length;

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate        HTTPDelegate,

                               HTTPMethod          HTTPMethod                  = null,
                               HTTPContentType     HTTPContentType             = null,

                               HTTPAuthentication  HTTPMethodAuthentication    = null,
                               HTTPAuthentication  ContentTypeAuthentication   = null,

                               HTTPDelegate        DefaultErrorHandler         = null)

        {

            HTTPMethodNode _HTTPMethodNode = null;

            if (!_HTTPMethods.TryGetValue(HTTPMethod, out _HTTPMethodNode))
            {
                _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, HTTPMethodAuthentication, HTTPDelegate, DefaultErrorHandler);
                _HTTPMethods.Add(HTTPMethod, _HTTPMethodNode);
            }

            _HTTPMethodNode.AddHandler(HTTPDelegate,

                                       HTTPContentType,

                                       ContentTypeAuthentication,

                                       DefaultErrorHandler);

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {

            var _URLAuthentication = "";
            if (_URIAuthentication != null)
                _URLAuthentication = " (auth)";

            var _URLErrorHandler = "";
            if (_DefaultErrorHandler != null)
                _URLErrorHandler = " (errhdl)";

            var _HTTPMethods = "";
            if (HTTPMethods.Count > 0)
            {
                var _StringBuilder = HTTPMethods.Keys.ForEach(new StringBuilder(" ["), (__StringBuilder, __HTTPMethod) => __StringBuilder.Append(__HTTPMethod.MethodName).Append(", "));
                _StringBuilder.Length = _StringBuilder.Length - 2;
                _HTTPMethods = _StringBuilder.Append("]").ToString();
            }

            return String.Concat(URITemplate, _URLAuthentication, _URLErrorHandler, _HTTPMethods);

        }

        #endregion

    }

}
