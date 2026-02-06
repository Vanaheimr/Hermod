/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Collections;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some child nodes and a callback
    /// </summary>
    public class URL_Node : IEnumerable<HTTPMethodNode>
    {

        #region Data

        private static readonly Regex replaceLastParameter = new Regex(@"\{[^/]+\}$");
        private static readonly Regex replaceAllParameters = new Regex(@"\{[^/]+\}");

        /// <summary>
        /// A mapping from HTTPMethods to HTTPMethodNodes.
        /// </summary>
        private readonly ConcurrentDictionary<HTTPMethod, HTTPMethodNode> httpMethodNodes = new();

        #endregion

        #region Properties

        /// <summary>
        /// The hosting HTTP API.
        /// </summary>
        public HTTPAPI                                   HTTPAPI                { get; }

        /// <summary>
        /// The URL template for this service.
        /// </summary>
        public HTTPPath                                  URLTemplate            { get; }

        /// <summary>
        /// Whether the URL template matches subdirectories at the end of the template.
        /// </summary>
        public Boolean                                   OpenEnd                { get; }

        /// <summary>
        /// The URI regex for this service.
        /// </summary>
        public Regex                                     URLRegex               { get; }

        /// <summary>
        /// The number of parameters within this URLNode for shorting best-matching URLs.
        /// </summary>
        public UInt16                                    ParameterCount         { get; }

        /// <summary>
        /// The length of the minimalized URL template for shorting best-matching URLs.
        /// </summary>
        public UInt16                                    SortLength             { get; }

        /// <summary>
        /// An HTTP request logger.
        /// </summary>
        public HTTPRequestLogHandler?                    HTTPRequestLogger      { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit URI authentication.
        /// </summary>
        public HTTPAuthentication?                       URLAuthentication      { get; }

        /// <summary>
        /// An HTTP delegate.
        /// </summary>
        public HTTPDelegate?                             RequestHandler         { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate?                             DefaultErrorHandler    { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers          { get; } = [];

        /// <summary>
        /// An HTTP response logger.
        /// </summary>
        public HTTPResponseLogHandler?                   HTTPResponseLogger     { get; private set; }


        /// <summary>
        /// Return all defined HTTP methods.
        /// </summary>
        public IEnumerable<HTTPMethod>                   HTTPMethods
            => httpMethodNodes.Keys;

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerable<HTTPMethodNode>               HTTPMethodNodes
            => httpMethodNodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new URLNode.
        /// </summary>
        /// <param name="URLTemplate">The URI template for this service.</param>
        /// <param name="OpenEnd">Whether the URL template matches subdirectories at the end of the template.</param>
        /// <param name="URLAuthentication">This and all subordinated nodes demand an explicit URI authentication.</param>
        internal URL_Node(HTTPAPI              HTTPAPI,
                          HTTPPath             URLTemplate,
                          Boolean              OpenEnd             = false,
                          HTTPAuthentication?  URLAuthentication   = null)

        {

            this.HTTPAPI                = HTTPAPI;
            this.URLTemplate            = URLTemplate;
            this.OpenEnd                = OpenEnd;
            this.URLAuthentication      = URLAuthentication;

            this.ParameterCount         = (UInt16) replaceLastParameter.Matches(URLTemplate.ToString()).Count;
            var URLTemplate2            = OpenEnd
                                             ? replaceLastParameter.Replace(URLTemplate.ToString(), "([^\n]+)")
                                             : replaceLastParameter.Replace(URLTemplate.ToString(), "([^/]+)");
            var URLTemplateWithoutVars  = replaceLastParameter.Replace(URLTemplate.ToString(), "");

            this.ParameterCount        += (UInt16) replaceAllParameters.Matches(URLTemplate2).Count;
            this.URLRegex               = new Regex("^" + replaceAllParameters.Replace(URLTemplate2, "([^/]+)") + "$");
            this.SortLength             = (UInt16) replaceAllParameters.Replace(URLTemplateWithoutVars, "").Length;

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPAPI                  HTTPAPI,
                               HTTPDelegate             HTTPDelegate,

                               HTTPMethod               HTTPMethod,
                               HTTPContentType?         HTTPContentType             = null,

                               HTTPAuthentication?      HTTPMethodAuthentication    = null,
                               HTTPAuthentication?      ContentTypeAuthentication   = null,

                               HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?  HTTPResponseLogger          = null,

                               HTTPDelegate?            DefaultErrorHandler         = null,
                               URLReplacement           AllowReplacement            = URLReplacement.Fail)

        {

            if (!httpMethodNodes.TryGetValue(HTTPMethod, out var httpMethodNode))
                httpMethodNode = httpMethodNodes.AddAndReturnValue(
                                     HTTPMethod,
                                     new HTTPMethodNode(
                                         HTTPAPI,
                                         HTTPMethod,
                                         HTTPMethodAuthentication
                                     )
                                 );

            httpMethodNode.AddHandler(
                               HTTPAPI,
                               HTTPDelegate,

                               HTTPContentType,

                               ContentTypeAuthentication,

                               HTTPRequestLogger,
                               HTTPResponseLogger,

                               DefaultErrorHandler,
                               AllowReplacement
                           );

        }

        #endregion


        #region Contains(Method)

        /// <summary>
        /// Determines whether the given HTTP method is defined.
        /// </summary>
        /// <param name="Method">An HTTP method.</param>
        public Boolean Contains(HTTPMethod Method)

            => httpMethodNodes.ContainsKey(Method);

        #endregion

        #region Get     (Method)

        /// <summary>
        /// Return the HTTP method node for the given HTTP method.
        /// </summary>
        /// <param name="Method">An HTTP method.</param>
        public HTTPMethodNode? Get(HTTPMethod Method)
        {

            if (httpMethodNodes.TryGetValue(Method, out var methodNode))
                return methodNode;

            return null;

        }

        #endregion

        #region TryGet  (Method, out MethodNode)

        /// <summary>
        /// Return the HTTP method node for the given HTTP method.
        /// </summary>
        /// <param name="Method">An HTTP method.</param>
        /// <param name="MethodNode">The attached HTTP method node.</param>
        public Boolean TryGet(HTTPMethod Method, out HTTPMethodNode? MethodNode)

            => httpMethodNodes.TryGetValue(Method, out MethodNode);

        #endregion


        #region IEnumerable<URINode> Members

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerator<HTTPMethodNode> GetEnumerator()
            => httpMethodNodes.Values.GetEnumerator();

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => httpMethodNodes.Values.GetEnumerator();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   !httpMethodNodes.IsEmpty
                       ? $"[{httpMethodNodes.Keys.AggregateCSV()}] "
                       : String.Empty,

                   URLTemplate.ToString(),

                   URLAuthentication is not null
                       ? " (auth)"
                       : String.Empty,

                   DefaultErrorHandler is not null
                       ? " (error)"
                       : String.Empty

               );

        #endregion

    }

}
