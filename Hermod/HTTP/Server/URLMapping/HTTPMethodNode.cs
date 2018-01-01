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
using System.Linq;
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

        /// <summary>
        /// The http method for this service.
        /// </summary>
        public HTTPMethod                                    HTTPMethod                  { get; }

        public HTTPDelegate                                  RequestHandler              { get; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP method authentication.
        /// </summary>
        public HTTPAuthentication                            HTTPMethodAuthentication    { get; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate                                  DefaultErrorHandler         { get; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode,  HTTPDelegate>     ErrorHandlers               { get; }

        /// <summary>
        /// A mapping from HTTPContentTypes to HTTPContentTypeNodes.
        /// </summary>
        public Dictionary<HTTPContentType, ContentTypeNode>  HTTPContentTypes            { get; }

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
                                HTTPAuthentication  HTTPMethodAuthentication   = null,
                                HTTPDelegate        RequestHandler             = null,
                                HTTPDelegate        DefaultErrorHandler        = null)

        {

            this.HTTPMethod                = HTTPMethod ?? throw new ArgumentException("HTTPMethod == null!");
            this.HTTPMethodAuthentication  = HTTPMethodAuthentication;
            this.RequestHandler            = RequestHandler;
            this.DefaultErrorHandler       = DefaultErrorHandler;

            this.ErrorHandlers             = new Dictionary<HTTPStatusCode,  HTTPDelegate>();
            this.HTTPContentTypes          = new Dictionary<HTTPContentType, ContentTypeNode>();

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate        HTTPDelegate,
                               HTTPContentType     HTTPContentType            = null,
                               HTTPAuthentication  ContentTypeAuthentication  = null,
                               HTTPDelegate        DefaultErrorHandler        = null,
                               URIReplacement      AllowReplacement           = URIReplacement.Fail)

        {

            if (HTTPContentType == null)
            {
                //RequestHandler       = HTTPDelegate;
                //DefaultErrorHandler  = DefaultErrorHandler;
            }

            else if (!HTTPContentTypes.TryGetValue(HTTPContentType, out ContentTypeNode _ContentTypeNode))
            {
                _ContentTypeNode = new ContentTypeNode(HTTPContentType, ContentTypeAuthentication, HTTPDelegate, DefaultErrorHandler, AllowReplacement);
                HTTPContentTypes.Add(HTTPContentType, _ContentTypeNode);
            }

            else
            {

                if (_ContentTypeNode.AllowReplacement == URIReplacement.Allow)
                {
                    _ContentTypeNode = new ContentTypeNode(HTTPContentType, ContentTypeAuthentication, HTTPDelegate, DefaultErrorHandler, AllowReplacement);
                    HTTPContentTypes[HTTPContentType] = _ContentTypeNode;
                }

                else if (_ContentTypeNode.AllowReplacement == URIReplacement.Ignore)
                {
                }

                else
                    throw new ArgumentException("Duplicate HTTP API definition!");

            }

        }

        #endregion


        public override String ToString()

            => String.Concat(HTTPMethod,
                             " (",
                             HTTPContentTypes.Select(contenttype => contenttype.Value.ToString()).AggregateWith(", "),
                             ")");

    }

}
