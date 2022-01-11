/*
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

        /// <summary>
        /// The HTTP content type for this service.
        /// </summary>
        public HTTPContentType                           HTTPContentType                  { get; }

        public HTTPDelegate                              RequestHandler                   { get; }

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP content type authentication.
        /// </summary>
        public HTTPAuthentication                        HTTPContentTypeAuthentication    { get; }

        /// <summary>
        /// A HTTP request logger.
        /// </summary>
        public HTTPRequestLogHandler                   HTTPRequestLogger                { get; }

        /// <summary>
        /// A HTTP response logger.
        /// </summary>
        public HTTPResponseLogHandler                    HTTPResponseLogger               { get; }

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate                              DefaultErrorHandler              { get; }

        public URLReplacement                            AllowReplacement                 { get; }


        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers                    { get; }

        #endregion

        #region (internal) Constructor(s)

        /// <summary>
        /// Creates a new HTTP ContentTypeNode.
        /// </summary>
        /// <param name="HTTPContentType">The http content type for this service.</param>
        /// <param name="HTTPContentTypeAuthentication">This and all subordinated nodes demand an explicit HTTP content type authentication.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
        /// <param name="AllowReplacement">How to handle duplicate URI handlers.</param>
        internal ContentTypeNode(HTTPContentType           HTTPContentType,
                                 HTTPAuthentication        HTTPContentTypeAuthentication   = null,
                                 HTTPRequestLogHandler   HTTPRequestLogger               = null,
                                 HTTPDelegate              RequestHandler                  = null,
                                 HTTPResponseLogHandler    HTTPResponseLogger              = null,
                                 HTTPDelegate              DefaultErrorHandler             = null,
                                 URLReplacement            AllowReplacement                = URLReplacement.Fail)
        {

            this.HTTPContentType                 = HTTPContentType ?? throw new ArgumentNullException(nameof(HTTPContentType),  "The given HTTP content type must not be null!");
            this.HTTPContentTypeAuthentication   = HTTPContentTypeAuthentication;
            this.HTTPRequestLogger               = HTTPRequestLogger;
            this.RequestHandler                  = RequestHandler;
            this.HTTPResponseLogger              = HTTPResponseLogger;
            this.DefaultErrorHandler             = DefaultErrorHandler;
            this.AllowReplacement                = AllowReplacement;

            this.ErrorHandlers                   = new Dictionary<HTTPStatusCode, HTTPDelegate>();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => HTTPContentType.ToString();

        #endregion

    }

}
