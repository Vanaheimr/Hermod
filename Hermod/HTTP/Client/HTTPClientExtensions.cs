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
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        // RFC 2616 - HTTP/1.1

        #region DELETE (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder DELETE(this HTTPClient              HTTPClient,
                                                 HTTPURI                      URI,
                                                 Action<HTTPRequest.Builder>  BuilderAction = null)
        {
            return HTTPClient.CreateRequest(HTTPMethod.DELETE, URI, BuilderAction);
        }

        #endregion

        #region GET    (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder GET(this HTTPClient              HTTPClient,
                                              HTTPURI                      URI,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.GET, URI, BuilderAction);

        #endregion

        #region COUNT  (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder COUNT(this HTTPClient              HTTPClient,
                                                HTTPURI                      URI,
                                                Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.COUNT, URI, BuilderAction);

        #endregion

        #region CREATE (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CREATE(this HTTPClient              HTTPClient,
                                                 HTTPURI                      URI,
                                                 Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.CREATE, URI, BuilderAction);

        #endregion

        #region ADD    (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder ADD(this HTTPClient              HTTPClient,
                                              HTTPURI                      URI,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.ADD, URI, BuilderAction);

        #endregion

        #region SET    (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder SET(this HTTPClient              HTTPClient,
                                              HTTPURI                      URI,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.SET, URI, BuilderAction);

        #endregion

        #region CREATE (this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CREATE(this HTTPClient  HTTPClient,
                                                 HTTPURI          URI)

            => HTTPClient.CreateRequest(HTTPMethod.CREATE, URI);

        #endregion

        #region HEAD   (this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder HEAD(this HTTPClient  HTTPClient,
                                               HTTPURI          URI)

            => HTTPClient.CreateRequest(HTTPMethod.HEAD, URI);

        #endregion

        #region OPTIONS(this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder OPTIONS(this HTTPClient  HTTPClient,
                                                  HTTPURI          URI)

            => HTTPClient.CreateRequest(HTTPMethod.OPTIONS, URI);

        #endregion

        #region POST   (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder POST(this HTTPClient              HTTPClient,
                                               HTTPURI                      URI,
                                               Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.
                   CreateRequest(HTTPMethod.POST, URI, BuilderAction).
                   // Always send a Content-Length header, even when it's value is zero
                   SetContentLength(0);

        #endregion

        #region PUT    (this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder PUT(this HTTPClient              HTTPClient,
                                              HTTPURI                      URI,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.PUT, URI, BuilderAction);

        #endregion

        #region TRACE  (this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder TRACE(this HTTPClient  HTTPClient,
                                                HTTPURI          URI)

            => HTTPClient.CreateRequest(HTTPMethod.TRACE, URI);

        #endregion


        // Additional methods

        #region PATCH   (this HTTPClient, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder PATCH(this HTTPClient              HTTPClient,
                                                HTTPURI                      URI,
                                                Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.PATCH, URI, BuilderAction);

        #endregion

        #region TRAVERSE(this HTTPClient, URI = "/")

        /// <summary>
        /// Create a new HTTP TRAVERSE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URI">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder TRAVERSE(this HTTPClient  HTTPClient,
                                                   HTTPURI          URI)

            => HTTPClient.CreateRequest(HTTPMethod.TRAVERSE, URI);

        #endregion


    }

}
