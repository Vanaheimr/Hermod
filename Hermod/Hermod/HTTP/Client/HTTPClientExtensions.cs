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

using System;
using System.Threading.Tasks;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        // RFC 2616 - HTTP/1.1

        #region DELETE (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> DELETE(this AHTTPClient             HTTPClient,
                                                HTTPPath                     Path,
                                                Action<HTTPRequest.Builder>  BuilderAction = null)

            =>  HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.DELETE,
                                                                  Path,
                                                                  BuilderAction));


        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder DELETERequest(this AHTTPClient             HTTPClient,
                                                        HTTPPath                     Path,
                                                        Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.DELETE,
                                        Path,
                                        BuilderAction);

        #endregion

        #region GET    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> GET(this AHTTPClient             HTTPClient,
                                             HTTPPath                     Path,
                                             Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.GET,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder GETRequest(this AHTTPClient             HTTPClient,
                                                     HTTPPath                     Path,
                                                     Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.GET,
                                        Path,
                                        BuilderAction);

        #endregion

        #region CHECK  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CHECK request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> CHECK(this AHTTPClient             HTTPClient,
                                               HTTPPath                     Path,
                                               Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.CHECK,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP CHECK request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CHECKRequest(this AHTTPClient             HTTPClient,
                                                       HTTPPath                     Path,
                                                       Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.CHECK,
                                        Path,
                                        BuilderAction);

        #endregion

        #region COUNT  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> COUNT(this AHTTPClient             HTTPClient,
                                               HTTPPath                     Path,
                                               Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.COUNT,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder COUNTRequest(this AHTTPClient             HTTPClient,
                                                       HTTPPath                     Path,
                                                       Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.COUNT,
                                        Path,
                                        BuilderAction);

        #endregion

        #region CREATE (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> CREATE(this AHTTPClient             HTTPClient,
                                                HTTPPath                     Path,
                                                Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.CREATE,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CREATERequest(this AHTTPClient             HTTPClient,
                                                        HTTPPath                     Path,
                                                        Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.CREATE,
                                        Path,
                                        BuilderAction);

        #endregion

        #region ADD    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> ADD(this AHTTPClient             HTTPClient,
                                             HTTPPath                     Path,
                                             Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.ADD,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder ADDRequest(this AHTTPClient             HTTPClient,
                                                     HTTPPath                     Path,
                                                     Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.ADD,
                                        Path,
                                        BuilderAction);

        #endregion

        #region SET    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> SET(this AHTTPClient             HTTPClient,
                                             HTTPPath                     Path,
                                             Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.SET,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder SETRequest(this AHTTPClient             HTTPClient,
                                                     HTTPPath                     Path,
                                                     Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.SET,
                                        Path,
                                        BuilderAction);

        #endregion

        #region HEAD   (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> HEAD(this AHTTPClient             HTTPClient,
                                              HTTPPath                     Path,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.HEAD,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder HEADRequest(this AHTTPClient             HTTPClient,
                                                      HTTPPath                     Path,
                                                      Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.HEAD,
                                        Path,
                                        BuilderAction);

        #endregion

        #region OPTIONS(this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> OPTIONS(this AHTTPClient             HTTPClient,
                                                 HTTPPath                     Path,
                                                 Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.OPTIONS,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder OPTIONSRequest(this AHTTPClient             HTTPClient,
                                                         HTTPPath                     Path,
                                                         Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.OPTIONS,
                                        Path,
                                        BuilderAction);

        #endregion

        #region POST   (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> POST(this AHTTPClient             HTTPClient,
                                              HTTPPath                     Path,
                                              Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.POST,
                                                                 Path,
                                                                 BuilderAction).
                                                   // Always send a Content-Length header, even when it's value is zero!
                                                   SetContentLength(0));


        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder POSTRequest(this AHTTPClient             HTTPClient,
                                                      HTTPPath                     Path,
                                                      Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.POST,
                                        Path,
                                        BuilderAction).
                          // Always send a Content-Length header, even when it's value is zero!
                          SetContentLength(0);

        #endregion

        #region PUT    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> PUT(this AHTTPClient             HTTPClient,
                                             HTTPPath                     Path,
                                             Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.PUT,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder PUTRequest(this AHTTPClient             HTTPClient,
                                                     HTTPPath                     Path,
                                                     Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.PUT,
                                        Path,
                                        BuilderAction);

        #endregion

        #region TRACE  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> TRACE(this AHTTPClient             HTTPClient,
                                               HTTPPath                     Path,
                                               Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.TRACE,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder TRACERequest(this AHTTPClient             HTTPClient,
                                                       HTTPPath                     Path,
                                                       Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.TRACE,
                                        Path,
                                        BuilderAction);

        #endregion


        // Additional methods

        #region PATCH   (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> PATCH(this AHTTPClient             HTTPClient,
                                               HTTPPath                     Path,
                                               Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.PATCH,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder PATCHRequest(this AHTTPClient             HTTPClient,
                                                       HTTPPath                     Path,
                                                       Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.PATCH,
                                        Path,
                                        BuilderAction);

        #endregion

        #region TRAVERSE(this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP TRAVERSE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> TRAVERSE(this AHTTPClient             HTTPClient,
                                                  HTTPPath                     Path,
                                                  Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.TRAVERSE,
                                                                 Path,
                                                                 BuilderAction));


        /// <summary>
        /// Create a new HTTP TRAVERSE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder TRAVERSERequest(this AHTTPClient             HTTPClient,
                                                          HTTPPath                     Path,
                                                          Action<HTTPRequest.Builder>  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.TRAVERSE,
                                        Path,
                                        BuilderAction);

        #endregion


    }

}
