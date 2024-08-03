/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        // RFC 2616 - HTTP/1.1

        #region GET    (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder GETRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      Path,
                                                     Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                     IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.GET,
                   Path,
                   BuilderAction,
                   Authentication
               );


        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="RequestURL">The request URL.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder GETRequest(this AHTTPClient              HTTPClient,
                                                     URL                           RequestURL,
                                                     Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                     IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.GET,
                   RequestURL,
                   BuilderAction,
                   Authentication
               );

        #endregion

        #region HEAD   (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder HEADRequest(this AHTTPClient              HTTPClient,
                                                      HTTPPath                      Path,
                                                      Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                      IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.HEAD,
                   Path,
                   BuilderAction,
                   Authentication
               );

        #endregion

        #region POST   (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder POSTRequest(this AHTTPClient              HTTPClient,
                                                      HTTPPath                      Path,
                                                      Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                      IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.POST,
                   Path,
                   BuilderAction,
                   Authentication
               ).
               // Always send a Content-Length header, even when it's value is zero!
               SetContentLength(0);


        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="RequestURL">The request URL.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder POSTRequest(this AHTTPClient              HTTPClient,
                                                      URL                           RequestURL,
                                                      Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                      IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.POST,
                   RequestURL,
                   BuilderAction,
                   Authentication
               ).
               // Always send a Content-Length header, even when it's value is zero!
               SetContentLength(0);

        #endregion

        #region PUT    (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder PUTRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      Path,
                                                     Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                     IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PUT,
                   Path,
                   BuilderAction,
                   Authentication
               );

        #endregion

        #region PATCH  (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder PATCHRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      Path,
                                                       Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                       IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PATCH,
                   Path,
                   BuilderAction,
                   Authentication
               );

        #endregion

        #region DELETE (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder DELETERequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      Path,
                                                        Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                        IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.DELETE,
                   Path,
                   BuilderAction,
                   Authentication
               );

        #endregion

        #region OPTIONS(this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static HTTPRequest.Builder OPTIONSRequest(this AHTTPClient              HTTPClient,
                                                         HTTPPath                      Path,
                                                         Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                         IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.CreateRequest(
                   HTTPMethod.OPTIONS,
                   Path,
                   BuilderAction,
                   Authentication
               );

        #endregion


        #region CHECK  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CHECK request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CHECKRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      Path,
                                                       Action<HTTPRequest.Builder>?  BuilderAction = null)

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
        public static HTTPRequest.Builder COUNTRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      Path,
                                                       Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.COUNT,
                                        Path,
                                        BuilderAction);

        #endregion

        #region CLEAR  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CLEAR request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static HTTPRequest.Builder CLEARRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      Path,
                                                       Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.CLEAR,
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
        public static HTTPRequest.Builder CREATERequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      Path,
                                                        Action<HTTPRequest.Builder>?  BuilderAction = null)

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
        public static HTTPRequest.Builder ADDRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      Path,
                                                     Action<HTTPRequest.Builder>?  BuilderAction = null)

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
        public static HTTPRequest.Builder SETRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      Path,
                                                     Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.SET,
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
        public static HTTPRequest.Builder TRACERequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      Path,
                                                       Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.TRACE,
                                        Path,
                                        BuilderAction);

        #endregion

        #region MIRROR (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP MIRROR request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        public static HTTPRequest.Builder MIRRORRequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      Path,
                                                        Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.CreateRequest(HTTPMethod.MIRROR,
                                        Path,
                                        BuilderAction);

        #endregion


    }

}
