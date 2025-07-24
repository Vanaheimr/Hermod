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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP client extension methods.
    /// </summary>
    public static class HTTPClientExtensions
    {

        // RFC 2616 - HTTP/1.1

        #region GETRequest    (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder GETRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.GET,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );


        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="RequestURL">The request URL.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder GETRequest(this AHTTPClient              HTTPClient,
                                                     URL                           RequestURL,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.GET,
                   RequestURL,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region HEADRequest   (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder HEADRequest(this AHTTPClient              HTTPClient,
                                                      HTTPPath                      HTTPPath,
                                                      QueryString?                  QueryString         = null,
                                                      AcceptTypes?                  Accept              = null,
                                                      IHTTPAuthentication?          Authentication      = null,
                                                      String?                       UserAgent           = null,
                                                      ConnectionType?               Connection          = null,
                                                      Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                      CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.HEAD,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region POSTRequest   (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder POSTRequest(this AHTTPClient              HTTPClient,
                                                      HTTPPath                      HTTPPath,
                                                      QueryString?                  QueryString         = null,
                                                      AcceptTypes?                  Accept              = null,
                                                      IHTTPAuthentication?          Authentication      = null,
                                                      String?                       UserAgent           = null,
                                                      ConnectionType?               Connection          = null,
                                                      Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                      CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.POST,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               ).
               // Always send a Content-Length header, even when it's value is zero!
               SetContentLength(0);


        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="RequestURL">The request URL.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder POSTRequest(this AHTTPClient              HTTPClient,
                                                      URL                           RequestURL,
                                                      AcceptTypes?                  Accept              = null,
                                                      IHTTPAuthentication?          Authentication      = null,
                                                      String?                       UserAgent           = null,
                                                      ConnectionType?               Connection          = null,
                                                      Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                      CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.POST,
                   RequestURL,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               ).
               // Always send a Content-Length header, even when it's value is zero!
               SetContentLength(0);

        #endregion

        #region PUTRequest    (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder PUTRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PUT,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region PATCHRequest  (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder PATCHRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PATCH,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region DELETERequest (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder DELETERequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.DELETE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region OPTIONSRequest(this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder OPTIONSRequest(this AHTTPClient              HTTPClient,
                                                         HTTPPath                      HTTPPath,
                                                         QueryString?                  QueryString         = null,
                                                         AcceptTypes?                  Accept              = null,
                                                         IHTTPAuthentication?          Authentication      = null,
                                                         String?                       UserAgent           = null,
                                                         ConnectionType?               Connection          = null,
                                                         Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                         CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.OPTIONS,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion


        // Inofficial HTTP methods

        #region CHECKRequest  (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CHECK request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder CHECKRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CHECK,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region COUNTRequest  (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder COUNTRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.COUNT,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region CLEARRequest  (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CLEAR request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder CLEARRequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CLEAR,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region CREATERequest (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder CREATERequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CREATE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region ADDRequest    (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder ADDRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.ADD,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region SETRequest    (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder SETRequest(this AHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.SET,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region TRACERequest  (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder TRACERequest(this AHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.TRACE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion

        #region MIRRORRequest (this AHTTPClient, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP MIRROR request.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static HTTPRequest.Builder MIRRORRequest(this AHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.MIRROR,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   CancellationToken
               );

        #endregion


    }

}
