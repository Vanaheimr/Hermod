/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Collections.Generic;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class URINode : IEnumerable<HTTPMethodNode>
    {

        #region Data

        /// <summary>
        /// A mapping from HTTPMethods to HTTPMethodNodes.
        /// </summary>
        private readonly Dictionary<HTTPMethod, HTTPMethodNode> _HTTPMethodNodes;

        #endregion

        #region Properties

        /// <summary>
        /// The URL template for this service.
        /// </summary>
        public HTTPPath                                   URITemplate            { get; }

        /// <summary>
        /// The URI regex for this service.
        /// </summary>
        public Regex                                     URIRegex               { get; }

        /// <summary>
        /// The number of parameters within this URLNode for shorting best-matching URLs.
        /// </summary>
        public UInt16                                    ParameterCount         { get; }

        /// <summary>
        /// The lenght of the minimalized URL template for shorting best-matching URLs.
        /// </summary>
        public UInt16                                    SortLength             { get; }

        /// <summary>
        /// A HTTP request logger.
        /// </summary>
        public HTTPRequestLogHandler                   HTTPRequestLogger      { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit URI authentication.
        /// </summary>
        public HTTPAuthentication                        URIAuthentication      { get; }

        /// <summary>
        /// A HTTP delegate.
        /// </summary>
        public HTTPDelegate                              RequestHandler         { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate                              DefaultErrorHandler    { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers          { get; }

        /// <summary>
        /// A HTTP response logger.
        /// </summary>
        public HTTPResponseLogHandler                    HTTPResponseLogger     { get; private set; }


        /// <summary>
        /// Return all defined HTTP methods.
        /// </summary>
        public IEnumerable<HTTPMethod> HTTPMethods
            => _HTTPMethodNodes.Keys;

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerable<HTTPMethodNode> HTTPMethodNodes
            => _HTTPMethodNodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new URLNode.
        /// </summary>
        /// <param name="URITemplate">The URI template for this service.</param>
        /// <param name="URIAuthentication">This and all subordinated nodes demand an explicit URI authentication.</param>
        internal URINode(HTTPPath             URITemplate,
                         HTTPAuthentication  URIAuthentication  = null)

        {

            this.URITemplate            = URITemplate;
            this.URIAuthentication      = URIAuthentication;
            this.RequestHandler         = RequestHandler;
            this.DefaultErrorHandler    = DefaultErrorHandler;
            this.ErrorHandlers          = new Dictionary<HTTPStatusCode, HTTPDelegate>();
            this._HTTPMethodNodes       = new Dictionary<HTTPMethod, HTTPMethodNode>();

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

        public void AddHandler(HTTPDelegate            HTTPDelegate,

                               HTTPMethod              HTTPMethod,
                               HTTPContentType         HTTPContentType             = null,

                               HTTPAuthentication      HTTPMethodAuthentication    = null,
                               HTTPAuthentication      ContentTypeAuthentication   = null,

                               HTTPRequestLogHandler   HTTPRequestLogger           = null,
                               HTTPResponseLogHandler  HTTPResponseLogger          = null,

                               HTTPDelegate            DefaultErrorHandler         = null,
                               URIReplacement          AllowReplacement            = URIReplacement.Fail)

        {

            lock (_HTTPMethodNodes)
            {

                if (!_HTTPMethodNodes.TryGetValue(HTTPMethod, out HTTPMethodNode _HTTPMethodNode))
                {

                    _HTTPMethodNode = _HTTPMethodNodes.AddAndReturnValue(HTTPMethod,
                                                                         new HTTPMethodNode(HTTPMethod,
                                                                                            HTTPMethodAuthentication));

                }

                _HTTPMethodNode.AddHandler(HTTPDelegate,

                                           HTTPContentType,

                                           ContentTypeAuthentication,

                                           HTTPRequestLogger,
                                           HTTPResponseLogger,

                                           DefaultErrorHandler,
                                           AllowReplacement);

            }

        }

        #endregion


        #region Contains(Method)

        /// <summary>
        /// Determines whether the given HTTP method is defined.
        /// </summary>
        /// <param name="Method">A HTTP method.</param>
        public Boolean Contains(HTTPMethod Method)

            => _HTTPMethodNodes.ContainsKey(Method);

        #endregion

        #region Get     (Method)

        /// <summary>
        /// Return the HTTP method node for the given HTTP method.
        /// </summary>
        /// <param name="Method">A HTTP method.</param>
        public HTTPMethodNode Get(HTTPMethod Method)
        {

            if (_HTTPMethodNodes.TryGetValue(Method, out HTTPMethodNode methodNode))
                return methodNode;

            return null;

        }

        #endregion

        #region TryGet  (Method, out MethodNode)

        /// <summary>
        /// Return the HTTP method node for the given HTTP method.
        /// </summary>
        /// <param name="Method">A HTTP method.</param>
        /// <param name="MethodNode">The attached HTTP method node.</param>
        public Boolean TryGet(HTTPMethod Method, out HTTPMethodNode MethodNode)

            => _HTTPMethodNodes.TryGetValue(Method, out MethodNode);

        #endregion


        #region IEnumerable<URINode> members

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerator<HTTPMethodNode> GetEnumerator()
            => _HTTPMethodNodes.Values.GetEnumerator();

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => _HTTPMethodNodes.Values.GetEnumerator();

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
            if (_HTTPMethodNodes.Count > 0)
            {
                var _StringBuilder = _HTTPMethodNodes.Keys.ForEach(new StringBuilder(" ["), (__StringBuilder, __HTTPMethod) => __StringBuilder.Append(__HTTPMethod.MethodName).Append(", "));
                _StringBuilder.Length -= 2;
                _HTTPMethods = _StringBuilder.Append("]").ToString();
            }

            return String.Concat(URITemplate, _URLAuthentication, _URLErrorHandler, _HTTPMethods);

        }

        #endregion

    }

}
