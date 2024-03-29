﻿/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A URL node which stores some childnodes and a callback
    /// </summary>
    public class HTTPMethodNode : IEnumerable<ContentTypeNode>
    {

        #region Data

        /// <summary>
        /// A mapping from HTTPContentTypes to HTTPContentTypeNodes.
        /// </summary>
        private readonly Dictionary<HTTPContentType, ContentTypeNode> _ContentTypeNodes;

        #endregion

        #region Properties

        /// <summary>
        /// The http method for this service.
        /// </summary>
        public HTTPMethod                                    HTTPMethod                  { get; }

        /// <summary>
        /// Whether this HTTP method node HTTP handler can be replaced/overwritten.
        /// </summary>
        public URLReplacement                                AllowReplacement            { get; }

        /// <summary>
        /// A HTTP request logger.
        /// </summary>
        public HTTPRequestLogHandler                       HTTPRequestLogger           { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP method authentication.
        /// </summary>
        public HTTPAuthentication                            HTTPMethodAuthentication    { get; }

        /// <summary>
        /// A HTTP delegate.
        /// </summary>
        public HTTPDelegate                                  RequestHandler              { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate                                  DefaultErrorHandler         { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode,  HTTPDelegate>     ErrorHandlers               { get; }

        /// <summary>
        /// A HTTP response logger.
        /// </summary>
        public HTTPResponseLogHandler                        HTTPResponseLogger          { get; private set; }



        /// <summary>
        /// Return all defined HTTP content types.
        /// </summary>
        public IEnumerable<HTTPContentType> ContentTypes
            => _ContentTypeNodes.Keys;

        /// <summary>
        /// Return all HTTP content type nodes.
        /// </summary>
        public IEnumerable<ContentTypeNode> ContentTypeNodes
            => _ContentTypeNodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new HTTPMethodNode.
        /// </summary>
        /// <param name="HTTPMethod">The http method for this service.</param>
        /// <param name="HTTPMethodAuthentication">This and all subordinated nodes demand an explicit HTTP method authentication.</param>
        internal HTTPMethodNode(HTTPMethod          HTTPMethod,
                                HTTPAuthentication  HTTPMethodAuthentication  = null)

        {

            this.HTTPMethod                = HTTPMethod;
            this.HTTPMethodAuthentication  = HTTPMethodAuthentication;
            this.ErrorHandlers             = new Dictionary<HTTPStatusCode,  HTTPDelegate>();
            this._ContentTypeNodes         = new Dictionary<HTTPContentType, ContentTypeNode>();

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPDelegate              RequestHandler,
                               HTTPContentType           HTTPContentType             = null,
                               HTTPAuthentication        ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler     HTTPRequestLogger           = null,
                               HTTPResponseLogHandler    HTTPResponseLogger          = null,
                               HTTPDelegate              DefaultErrorHandler         = null,
                               URLReplacement            AllowReplacement            = URLReplacement.Fail)

        {

            lock (_ContentTypeNodes)
            {

                // For ANY content type...
                if (HTTPContentType == null)
                {

                    if (this.RequestHandler == null || AllowReplacement == URLReplacement.Allow)
                    {

                        this.HTTPRequestLogger    = HTTPRequestLogger;
                        this.RequestHandler       = RequestHandler;
                        this.DefaultErrorHandler  = DefaultErrorHandler;
                        this.HTTPResponseLogger   = HTTPResponseLogger;

                    }

                    else
                        throw new ArgumentException("An URI without a content type? Does this make sense here!");

                }

                // For a specific content type...
                else
                {

                    // The content type already exists!
                    if (_ContentTypeNodes.TryGetValue(HTTPContentType, out ContentTypeNode _ContentTypeNode))
                    {

                        if (_ContentTypeNode.AllowReplacement == URLReplacement.Allow)
                        {

                            _ContentTypeNodes[HTTPContentType] = new ContentTypeNode(HTTPContentType,
                                                                                     ContentTypeAuthentication,
                                                                                     HTTPRequestLogger,
                                                                                     RequestHandler,
                                                                                     HTTPResponseLogger,
                                                                                     DefaultErrorHandler,
                                                                                     AllowReplacement);

                        }

                        else if (_ContentTypeNode.AllowReplacement == URLReplacement.Ignore)
                        {
                        }

                        else
                            throw new ArgumentException("Duplicate HTTP API definition!");

                    }


                    // A new content type to add...
                    else
                    {

                        _ContentTypeNodes.Add(HTTPContentType,
                                              new ContentTypeNode(HTTPContentType,
                                                                  ContentTypeAuthentication,
                                                                  HTTPRequestLogger,
                                                                  RequestHandler,
                                                                  HTTPResponseLogger,
                                                                  DefaultErrorHandler,
                                                                  AllowReplacement));

                    }

                }

            }

        }

        #endregion


        #region Contains(ContentType)

        /// <summary>
        /// Determines whether the given HTTP content type is defined.
        /// </summary>
        /// <param name="ContentType">A HTTP content type.</param>
        public Boolean Contains(HTTPContentType ContentType)

            => _ContentTypeNodes.ContainsKey(ContentType);

        #endregion

        #region Get     (ContentType)

        /// <summary>
        /// Return the HTTP content type node for the given HTTP content type.
        /// </summary>
        /// <param name="ContentType">A HTTP content type.</param>
        public ContentTypeNode Get(HTTPContentType ContentType)
        {

            if (_ContentTypeNodes.TryGetValue(ContentType, out ContentTypeNode contentTypeNode))
                return contentTypeNode;

            return null;

        }

        #endregion

        #region TryGet  (ContentType, out ContentTypeNode)

        /// <summary>
        /// Return the HTTP content type node for the given HTTP content type.
        /// </summary>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="ContentTypeNode">The attached HTTP content type node.</param>
        public Boolean TryGet(HTTPContentType ContentType, out ContentTypeNode ContentTypeNode)

            => _ContentTypeNodes.TryGetValue(ContentType, out ContentTypeNode);

        #endregion


        #region IEnumerable<URINode> members

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerator<ContentTypeNode> GetEnumerator()
            => _ContentTypeNodes.Values.GetEnumerator();

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => _ContentTypeNodes.Values.GetEnumerator();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(HTTPMethod,
                             " (",
                             _ContentTypeNodes.Select(contenttype => contenttype.Value.ToString()).AggregateWith(", "),
                             ")");

        #endregion

    }

}
