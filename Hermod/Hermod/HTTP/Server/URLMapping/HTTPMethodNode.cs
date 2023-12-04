/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using org.GraphDefined.Vanaheimr.Illias;

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
        private readonly ConcurrentDictionary<HTTPContentType, ContentTypeNode> contentTypeNodes = [];

        #endregion

        #region Properties

        /// <summary>
        /// The hosting HTTP API.
        /// </summary>
        public HTTPAPI                                   HTTPAPI                     { get; }

        /// <summary>
        /// The http method for this service.
        /// </summary>
        public HTTPMethod                                HTTPMethod                  { get; }

        /// <summary>
        /// Whether this HTTP method node HTTP handler can be replaced/overwritten.
        /// </summary>
        public URLReplacement                            AllowReplacement            { get; }

        /// <summary>
        /// A HTTP request logger.
        /// </summary>
        public HTTPRequestLogHandler?                    HTTPRequestLogger           { get; private set; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP method authentication.
        /// </summary>
        public HTTPAuthentication?                       HTTPMethodAuthentication    { get; }

        /// <summary>
        /// A HTTP delegate.
        /// </summary>
        public HTTPDelegate?                             RequestHandler              { get; private set; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate?                             DefaultErrorHandler         { get; private set; }

        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers               { get; } = [];

        /// <summary>
        /// A HTTP response logger.
        /// </summary>
        public HTTPResponseLogHandler?                   HTTPResponseLogger          { get; private set; }


        /// <summary>
        /// Return all defined HTTP content types.
        /// </summary>
        public IEnumerable<HTTPContentType>              ContentTypes
            => contentTypeNodes.Keys;

        /// <summary>
        /// Return all HTTP content type nodes.
        /// </summary>
        public IEnumerable<ContentTypeNode>              ContentTypeNodes
            => contentTypeNodes.Values;

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Create a new HTTP method node.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="HTTPMethodAuthentication">This and all subordinated nodes demand an optional explicit HTTP method authentication.</param>
        internal HTTPMethodNode(HTTPAPI              HTTPAPI,
                                HTTPMethod           HTTPMethod,
                                HTTPAuthentication?  HTTPMethodAuthentication   = null)
        {

            this.HTTPAPI                   = HTTPAPI;
            this.HTTPMethod                = HTTPMethod;
            this.HTTPMethodAuthentication  = HTTPMethodAuthentication;

        }

        #endregion


        #region AddHandler(...)

        public void AddHandler(HTTPAPI                  HTTPAPI,
                               HTTPDelegate?            RequestHandler,
                               HTTPContentType?         HTTPContentType             = null,
                               HTTPAuthentication?      ContentTypeAuthentication   = null,
                               HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                               HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                               HTTPDelegate?            DefaultErrorHandler         = null,
                               URLReplacement           AllowReplacement            = URLReplacement.Fail)

        {

            #region For ANY content type...

            if (HTTPContentType is null)
            {

                if (this.RequestHandler is null || AllowReplacement == URLReplacement.Allow)
                {
                    this.HTTPRequestLogger    = HTTPRequestLogger;
                    this.RequestHandler       = RequestHandler;
                    this.DefaultErrorHandler  = DefaultErrorHandler;
                    this.HTTPResponseLogger   = HTTPResponseLogger;
                }

                else if (AllowReplacement == URLReplacement.Fail)
                    throw new ArgumentException("Replacing this URL template is not allowed!");

                else
                    throw new ArgumentException("An URL template without a content type? Does this make sense here!");

            }

            #endregion

            #region ...or for a specific content type

            else
            {

                #region The content type already exists...

                if (contentTypeNodes.TryGetValue(HTTPContentType, out var contentTypeNode))
                {

                    if (contentTypeNode.AllowReplacement == URLReplacement.Allow)
                        contentTypeNodes[HTTPContentType] = new ContentTypeNode(
                                                                HTTPAPI,
                                                                HTTPContentType,
                                                                ContentTypeAuthentication,
                                                                HTTPRequestLogger,
                                                                RequestHandler,
                                                                HTTPResponseLogger,
                                                                DefaultErrorHandler,
                                                                AllowReplacement
                                                            );

                    else if (contentTypeNode.AllowReplacement == URLReplacement.Ignore)
                    {
                        DebugX.Log("HTTP API definition replaced!");
                    }

                    else
                        throw new ArgumentException("Duplicate HTTP API definition!");

                }

                #endregion

                #region ...or a new content type to add

                    else
                    {

                        if (!contentTypeNodes.TryAdd(
                                                  HTTPContentType,
                                                  new ContentTypeNode(
                                                      HTTPAPI,
                                                      HTTPContentType,
                                                      ContentTypeAuthentication,
                                                      HTTPRequestLogger,
                                                      RequestHandler,
                                                      HTTPResponseLogger,
                                                      DefaultErrorHandler,
                                                      AllowReplacement
                                                  )
                                              ))
                        {
                            throw new ArgumentException("Could not add the given HTTP API definition!");
                        }

                    }

                    #endregion

            }

            #endregion

        }

        #endregion


        #region Contains(ContentType)

        /// <summary>
        /// Determines whether the given HTTP content type is defined.
        /// </summary>
        /// <param name="ContentType">A HTTP content type.</param>
        public Boolean Contains(HTTPContentType ContentType)

            => contentTypeNodes.ContainsKey(ContentType);

        #endregion

        #region Get     (ContentType)

        /// <summary>
        /// Return the HTTP content type node for the given HTTP content type.
        /// </summary>
        /// <param name="ContentType">A HTTP content type.</param>
        public ContentTypeNode? Get(HTTPContentType ContentType)
        {

            if (contentTypeNodes.TryGetValue(ContentType, out var contentTypeNode))
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
        public Boolean TryGet(HTTPContentType ContentType, out ContentTypeNode? ContentTypeNode)

            => contentTypeNodes.TryGetValue(ContentType, out ContentTypeNode);

        #endregion


        #region IEnumerable<URINode> members

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        public IEnumerator<ContentTypeNode> GetEnumerator()
            => contentTypeNodes.Values.GetEnumerator();

        /// <summary>
        /// Return all HTTP method nodes.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
            => contentTypeNodes.Values.GetEnumerator();

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(HTTPMethod,
                             " (",
                             contentTypeNodes.Select(contenttype => contenttype.Value.ToString()).AggregateWith(", "),
                             ")");

        #endregion

    }

}
