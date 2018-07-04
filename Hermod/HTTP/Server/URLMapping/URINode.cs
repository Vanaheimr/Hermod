/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Text;
using System.Reflection;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class URINode
    {

        #region Properties

        /// <summary>
        /// The URL template for this service.
        /// </summary>
        public HTTPURI             URITemplate            { get; }

        /// <summary>
        /// The URI regex for this service.
        /// </summary>
        public Regex               URIRegex               { get; }

        /// <summary>
        /// The number of parameters within this URLNode for shorting best-matching URLs.
        /// </summary>
        public UInt16              ParameterCount         { get; }

        /// <summary>
        /// The lenght of the minimalized URL template for shorting best-matching URLs.
        /// </summary>
        public UInt16              SortLength             { get; }

        public HTTPDelegate        RequestHandler         { get; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit URI authentication.
        /// </summary>
        public HTTPAuthentication  URIAuthentication      { get; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate        DefaultErrorHandler    { get; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers    { get; }

        /// <summary>
        /// A mapping from HTTPMethods to HTTPMethodNodes.
        /// </summary>
        public Dictionary<HTTPMethod, HTTPMethodNode>    HTTPMethods      { get; }

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new URLNode.
        /// </summary>
        /// <param name="URITemplate">The URI template for this service.</param>
        /// <param name="URIAuthentication">This and all subordinated nodes demand an explicit URI authentication.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        internal URINode(HTTPURI             URITemplate,
                         HTTPAuthentication  URIAuthentication    = null,
                         HTTPDelegate        RequestHandler       = null,
                         HTTPDelegate        DefaultErrorHandler  = null)

        {

            this.URITemplate            = URITemplate;
            this.URIAuthentication      = URIAuthentication;
            this.RequestHandler         = RequestHandler;
            this.DefaultErrorHandler    = DefaultErrorHandler;
            this.ErrorHandlers          = new Dictionary<HTTPStatusCode, HTTPDelegate>();
            this.HTTPMethods            = new Dictionary<HTTPMethod, HTTPMethodNode>();

            var _ReplaceLastParameter   = new Regex(@"\{[^/]+\}$");
            this.ParameterCount         = (UInt16) _ReplaceLastParameter.Matches(URITemplate.ToString()).Count;
            var URLTemplate2            = _ReplaceLastParameter.Replace(URITemplate.ToString(), "([^\n]+)");
            var URLTemplateWithoutVars  = _ReplaceLastParameter.Replace(URITemplate.ToString(), "");

            var _ReplaceAllParameters   = new Regex(@"\{[^/]+\}");
            this.ParameterCount        += (UInt16) _ReplaceAllParameters.Matches(URLTemplate2).Count;
            this.URIRegex               = new Regex("^" + _ReplaceAllParameters.Replace(URLTemplate2, "([^/]+)") + "$");
            this.SortLength             = (UInt16) _ReplaceAllParameters.Replace(URLTemplateWithoutVars, "").Length;

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate        HTTPDelegate,

                               HTTPMethod          HTTPMethod,
                               HTTPContentType     HTTPContentType             = null,

                               HTTPAuthentication  HTTPMethodAuthentication    = null,
                               HTTPAuthentication  ContentTypeAuthentication   = null,

                               HTTPDelegate        DefaultErrorHandler         = null,
                               URIReplacement      AllowReplacement            = URIReplacement.Fail)

        {

            if (!HTTPMethods.TryGetValue(HTTPMethod, out HTTPMethodNode _HTTPMethodNode))
            {
                _HTTPMethodNode = new HTTPMethodNode(HTTPMethod, HTTPMethodAuthentication, HTTPDelegate, DefaultErrorHandler);
                HTTPMethods.Add(HTTPMethod, _HTTPMethodNode);
            }

            _HTTPMethodNode.AddHandler(HTTPDelegate,

                                       HTTPContentType,

                                       ContentTypeAuthentication,

                                       DefaultErrorHandler,
                                       AllowReplacement);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            var _URLAuthentication = "";
            if (URIAuthentication != null)
                _URLAuthentication = " (auth)";

            var _URLErrorHandler = "";
            if (DefaultErrorHandler != null)
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
