/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        #region RFC 2616 - HTTP/1.1

        #region DELETE(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder DELETE(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.DELETE, UrlPath);
        }

        #endregion

        #region GET(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder GET(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.GET, UrlPath);
        }

        #endregion

        #region HEAD(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder HEAD(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.HEAD, UrlPath);
        }

        #endregion

        #region OPTIONS(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder OPTIONS(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.OPTIONS, UrlPath);
        }

        #endregion

        #region POST(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder POST(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.POST, UrlPath);
        }

        #endregion

        #region PUT(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder PUT(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.PUT, UrlPath);
        }

        #endregion

        #region TRACE(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder TRACE(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.TRACE, UrlPath);
        }

        #endregion

        #endregion

        #region Additional methods

        #region PATCH(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder PATCH(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.PATCH, UrlPath);
        }

        #endregion

        #region TRAVERSE(this HTTPClient, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP TRAVERSE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequestBuilder TRAVERSE(this HTTPClient HTTPClient, String UrlPath = "/")
        {
            return HTTPClient.CreateRequest(HTTPMethod.TRAVERSE, UrlPath);
        }

        #endregion

        #endregion

    }

}
