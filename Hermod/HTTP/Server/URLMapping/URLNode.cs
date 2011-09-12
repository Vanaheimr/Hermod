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
using System.Text;
using System.Reflection;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class URLNode
    {

        #region Properties

        /// <summary>
        /// The url for this service.
        /// </summary>
        public String URLTemplate { get; private set; }

        /// <summary>
        /// The url for this service.
        /// </summary>
        public Regex URLRegex { get; private set; }

        /// <summary>
        /// The numner of parameters within this URLNode.
        /// </summary>
        public UInt16 ParameterCount { get; private set; }

        /// <summary>
        /// The method handler.
        /// </summary>
        public MethodInfo MethodHandler { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit url authentication.
        /// </summary>
        public Boolean URLAuthentication { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public MethodInfo URLErrorHandler { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public ConcurrentDictionary<HTTPStatusCode, MethodInfo> URLErrorHandlers { get; private set; }
        
        /// <summary>
        /// A mapping from HTTPMethods to HTTPMethodNodes.
        /// </summary>
        public ConcurrentDictionary<HTTPMethod, HTTPMethodNode> HTTPMethods { get; private set; }

        #endregion

        #region Constructor(s)

        #region URLNode()

        /// <summary>
        /// Creates a new URLNode.
        /// </summary>
        /// <param name="URLTemplate">The url for this service.</param>
        /// <param name="MethodHandler">The method handler.</param>
        /// <param name="URLAuthentication">This and all subordinated nodes demand an explicit url authentication.</param>
        /// <param name="URLErrorHandler">A general error handling method.</param>
        public URLNode(String URLTemplate, MethodInfo MethodHandler = null, Boolean URLAuthentication = false, MethodInfo URLErrorHandler = null)
        {

            this.URLTemplate        = URLTemplate;
            this.MethodHandler      = MethodHandler;
            this.URLAuthentication  = URLAuthentication;
            this.URLErrorHandler    = URLErrorHandler;
            this.URLErrorHandlers   = new ConcurrentDictionary<HTTPStatusCode, MethodInfo>();
            this.HTTPMethods        = new ConcurrentDictionary<HTTPMethod, HTTPMethodNode>();

            var _ReplaceLastParameter = new Regex(@"\{[^/]+\}$");
            this.ParameterCount = (UInt16) _ReplaceLastParameter.Matches(URLTemplate).Count;
            var URLTemplate2 = _ReplaceLastParameter.Replace(URLTemplate, "([^\n]+)");

            var _ReplaceAllParameters  = new Regex(@"\{[^/]+\}");
            this.ParameterCount += (UInt16) _ReplaceAllParameters.Matches(URLTemplate2).Count;
            this.URLRegex = new Regex("^"+_ReplaceAllParameters.Replace(URLTemplate2, "([^/]+)"));

        }

        #endregion

        #endregion

        #region ToString()

        public override String ToString()
        {

            var _URLAuthentication = "";
            if (URLAuthentication)
                _URLAuthentication = " (auth)";

            var _URLErrorHandler = "";
            if (URLErrorHandler != null)
                _URLErrorHandler = " (errhdl)";

            var _HTTPMethods = "";
            if (HTTPMethods.Count > 0)
            {
                var _StringBuilder = HTTPMethods.Keys.ForEach(new StringBuilder(" ["), (__StringBuilder, __HTTPMethod) => __StringBuilder.Append(__HTTPMethod.MethodName).Append(", "));
                _StringBuilder.Length = _StringBuilder.Length - 2;
                _HTTPMethods = _StringBuilder.Append("]").ToString();
            }

            return String.Concat(URLTemplate, _URLAuthentication, _URLErrorHandler, _HTTPMethods);

        }

        #endregion

    }

}
