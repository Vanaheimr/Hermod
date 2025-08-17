/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// A URL node which stores some child nodes and a callback
    /// </summary>
    /// <param name="HTTPContentType">The http content type for this service.</param>
    /// <param name="HTTPContentTypeAuthentication">This and all subordinated nodes demand an explicit HTTP content type authentication.</param>
    /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
    /// <param name="RequestHandler">The default delegate to call for any request to this URI template.</param>
    /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
    /// <param name="DefaultErrorHandler">The default error handling delegate.</param>
    /// <param name="AllowReplacement">How to handle duplicate URI handlers.</param>
    public class ContentTypeNodeX(HTTPAPIX                    HTTPAPI,
                                  HTTPContentType             HTTPContentType,
                                  HTTPAuthentication?         HTTPContentTypeAuthentication   = null,
                                  OnHTTPRequestLogDelegate?   HTTPRequestLogger               = null,
                                  HTTPDelegate?               RequestHandler                  = null,
                                  OnHTTPResponseLogDelegate?  HTTPResponseLogger              = null,
                                  HTTPDelegate?               DefaultErrorHandler             = null,
                                  URLReplacement              AllowReplacement                = URLReplacement.Fail)
    {

        #region Properties

        /// <summary>
        /// The hosting HTTP API.
        /// </summary>
        public HTTPAPIX                                  HTTPAPI                          { get; } = HTTPAPI;

        /// <summary>
        /// The HTTP content type for this service.
        /// </summary>
        public HTTPContentType                           HTTPContentType                  { get; } = HTTPContentType
            ?? throw new ArgumentNullException(nameof(HTTPContentType), "The given HTTP content type must not be null!");

        public HTTPDelegate?                   RequestHandler                   { get; } = RequestHandler;

        /// <summary>
        /// This and all subordinated nodes demand an explicit HTTP content type authentication.
        /// </summary>
        public HTTPAuthentication?                       HTTPContentTypeAuthentication    { get; } = HTTPContentTypeAuthentication;

        /// <summary>
        /// An HTTP request logger.
        /// </summary>
        public OnHTTPRequestLogDelegate?                 HTTPRequestLogger                { get; } = HTTPRequestLogger;

        /// <summary>
        /// An HTTP response logger.
        /// </summary>
        public OnHTTPResponseLogDelegate?                HTTPResponseLogger               { get; } = HTTPResponseLogger;

        /// <summary>
        /// A general error handling method.
        /// </summary>
        public HTTPDelegate?                             DefaultErrorHandler              { get; } = DefaultErrorHandler;

        public URLReplacement                            AllowReplacement                 { get; } = AllowReplacement;


        /// <summary>
        /// Error handling methods for specific http status codes.
        /// </summary>
        public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers                    { get; } = [];

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
