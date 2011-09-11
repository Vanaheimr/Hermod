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

using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        #region RFC 2616 - HTTP/1.1

        #region DELETE(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest DELETE(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.DELETE, URLPattern);
        }

        #endregion

        #region GET(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest GET(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.GET, URLPattern);
        }

        #endregion

        #region HEAD(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest HEAD(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.HEAD, URLPattern);
        }

        #endregion

        #region OPTIONS(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest OPTIONS(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.OPTIONS, URLPattern);
        }

        #endregion

        #region POST(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest POST(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.POST, URLPattern);
        }

        #endregion

        #region PUT(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest PUT(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.PUT, URLPattern);
        }

        #endregion

        #region TRACE(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest TRACE(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.TRACE, URLPattern);
        }

        #endregion

        #endregion

        #region Additional methods

        #region PATCH(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest PATCH(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.PATCH, URLPattern);
        }

        #endregion

        #region TRAVERSE(this HTTPClient, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP TRAVERSE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="URLPattern">An URL pattern.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest TRAVERSE(this HTTPClient HTTPClient, String URLPattern = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.TRAVERSE, URLPattern);
        }

        #endregion

        #endregion

    }

}
