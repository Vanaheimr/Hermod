/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for the HTTP client interface.
    /// </summary>
    public static class IHTTPClientExtensions
    {

        #region OPTIONS (Path, ...)

        public static Task<HTTPResponse> OPTIONS(this IHTTPClient              HTTPClient,
                                                 HTTPPath                      Path,
                                                 IHTTPAuthentication?          Authentication                        = null,
                                                 ConnectionType?               Connection                            = null,
                                                 TimeSpan?                     RequestTimeout                        = null,
                                                 EventTracking_Id?             EventTrackingId                       = null,
                                                 Byte                          NumberOfRetry                         = 0,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                                 Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                                 Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                                 ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                                 ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                                 CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.OPTIONS,
                   Path,
                   null,
                   null,
                   Authentication,
                   null, //UserAgent
                   null, //Content,
                   null, //ContentType,
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion

        #region GET     (Path, ...)

        public static Task<HTTPResponse> GET(this IHTTPClient              HTTPClient,
                                             HTTPPath                      Path,
                                             QueryString?                  QueryString                           = null,
                                             AcceptTypes?                  Accept                                = null,
                                             IHTTPAuthentication?          Authentication                        = null,
                                             String?                       UserAgent                             = null,
                                             ConnectionType?               Connection                            = null,
                                             Boolean?                      Consume = null,

                                             TimeSpan?                     RequestTimeout                        = null,
                                             EventTracking_Id?             EventTrackingId                       = null,
                                             Byte                          NumberOfRetry                         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                             Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                             Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                             ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                             CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.GET,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   null, //Content,
                   null, //ContentType,
                   UserAgent,
                   Connection ?? ConnectionType.KeepAlive,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion

        #region POST    (Path, Content, ...)

        public static Task<HTTPResponse> POST(this IHTTPClient              HTTPClient,
                                              HTTPPath                      Path,
                                              Byte[]                        Content,
                                              HTTPContentType?              ContentType                           = null,
                                              QueryString?                  QueryString                           = null,
                                              AcceptTypes?                  Accept                                = null,
                                              IHTTPAuthentication?          Authentication                        = null,
                                              ConnectionType?               Connection                            = null,
                                              TimeSpan?                     RequestTimeout                        = null,
                                              EventTracking_Id?             EventTrackingId                       = null,
                                              Byte                          NumberOfRetry                         = 0,
                                              Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                              Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                              Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                              ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                              ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                              CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.POST,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   null, //UserAgent
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion

        #region PUT     (Path, Content, ...)

        public static Task<HTTPResponse> PUT(this IHTTPClient              HTTPClient,
                                             HTTPPath                      Path,
                                             Byte[]                        Content,
                                             HTTPContentType?              ContentType                           = null,
                                             QueryString?                  QueryString                           = null,
                                             AcceptTypes?                  Accept                                = null,
                                             IHTTPAuthentication?          Authentication                        = null,
                                             ConnectionType?               Connection                            = null,
                                             TimeSpan?                     RequestTimeout                        = null,
                                             EventTracking_Id?             EventTrackingId                       = null,
                                             Byte                          NumberOfRetry                         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                             Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                             Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                             ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                             CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.PUT,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   null, //UserAgent
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion

        #region PATCH   (Path, Content, ...)

        public static Task<HTTPResponse> PATCH(this IHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Byte[]                        Content,
                                               HTTPContentType?              ContentType                           = null,
                                               QueryString?                  QueryString                           = null,
                                               AcceptTypes?                  Accept                                = null,
                                               IHTTPAuthentication?          Authentication                        = null,
                                               ConnectionType?               Connection                            = null,
                                               TimeSpan?                     RequestTimeout                        = null,
                                               EventTracking_Id?             EventTrackingId                       = null,
                                               Byte                          NumberOfRetry                         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                               Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                               Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                               ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                               CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.PATCH,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   null, //UserAgent
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion

        #region DELETE  (Path, ...)

        public static Task<HTTPResponse> DELETE(this IHTTPClient              HTTPClient,
                                                HTTPPath                      Path,
                                                Byte[]?                       Content                               = null,
                                                HTTPContentType?              ContentType                           = null,
                                                QueryString?                  QueryString                           = null,
                                                AcceptTypes?                  Accept                                = null,
                                                IHTTPAuthentication?          Authentication                        = null,
                                                ConnectionType?               Connection                            = null,
                                                TimeSpan?                     RequestTimeout                        = null,
                                                EventTracking_Id?             EventTrackingId                       = null,
                                                Byte                          NumberOfRetry                         = 0,
                                                Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                                Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                                Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                                ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                                ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                                CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.DELETE,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   null, //UserAgent
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion


        #region MIRROR  (Path, Content, ...)

        public static Task<HTTPResponse> MIRROR(this IHTTPClient              HTTPClient,
                                                HTTPPath                      Path,
                                                Byte[]                        Content,
                                                HTTPContentType?              ContentType                           = null,
                                                QueryString?                  QueryString                           = null,
                                                AcceptTypes?                  Accept                                = null,
                                                IHTTPAuthentication?          Authentication                        = null,
                                                ConnectionType?               Connection                            = null,
                                                TimeSpan?                     RequestTimeout                        = null,
                                                EventTracking_Id?             EventTrackingId                       = null,
                                                Byte                          NumberOfRetry                         = 0,
                                                Action<HTTPRequest.Builder>?  RequestBuilder                        = null,

                                                Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                                                Boolean?                      ConsumeResponseChunkedTEImmediately   = null,

                                                ClientRequestLogHandler?      RequestLogDelegate                    = null,
                                                ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                                                CancellationToken             CancellationToken                     = default)

            => HTTPClient.RunRequest(

                   HTTPMethod.MIRROR,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   null, //UserAgent
                   Connection,
                   RequestBuilder,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   EventTrackingId,
                   RequestTimeout,

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion



        // RFC 2616 - HTTP/1.1

        #region GETRequest    (Path = "/", ...)

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
        public static HTTPRequest.Builder GETRequest(this IHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     TOTPConfig?                   TOTPConfig          = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     EventTracking_Id?             EventTrackingId     = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.GET,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
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
        //public static HTTPRequest.Builder GETRequest(this AHTTPTestClient              HTTPClient,
        //                                             URL                           RequestURL,
        //                                             AcceptTypes?                  Accept              = null,
        //                                             IHTTPAuthentication?          Authentication      = null,
        //                                             TOTPConfig?                   TOTPConfig          = null,
        //                                             String?                       UserAgent           = null,
        //                                             ConnectionType?               Connection          = null,
        //                                             Action<HTTPRequest.Builder>?  RequestBuilder      = null,
        //                                             CancellationToken             CancellationToken   = default)

        //    => HTTPClient.CreateRequest(
        //           HTTPMethod.GET,
        //           RequestURL,
        //           Accept,
        //           Authentication,
        //           null,  // Content
        //           null,  // ContentType
        //                  // TOTPConfig
        //           UserAgent,
        //           Connection,
        //           RequestBuilder,
        //           CancellationToken
        //       );

        #endregion

        #region HEADRequest   (Path = "/", ...)

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
        public static HTTPRequest.Builder HEADRequest(this IHTTPClient              HTTPClient,
                                                      HTTPPath                      HTTPPath,
                                                      QueryString?                  QueryString         = null,
                                                      AcceptTypes?                  Accept              = null,
                                                      IHTTPAuthentication?          Authentication      = null,
                                                      TOTPConfig?                   TOTPConfig          = null,
                                                      String?                       UserAgent           = null,
                                                      ConnectionType?               Connection          = null,
                                                      Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                      EventTracking_Id?             EventTrackingId     = null,
                                                      CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.HEAD,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region POSTRequest   (Path = "/", ...)

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
        public static HTTPRequest.Builder POSTRequest(this IHTTPClient              HTTPClient,
                                                      HTTPPath                      HTTPPath,
                                                      QueryString?                  QueryString         = null,
                                                      AcceptTypes?                  Accept              = null,
                                                      IHTTPAuthentication?          Authentication      = null,
                                                      TOTPConfig?                   TOTPConfig          = null,
                                                      String?                       UserAgent           = null,
                                                      ConnectionType?               Connection          = null,
                                                      Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                      EventTracking_Id?             EventTrackingId     = null,
                                                      CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.POST,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
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
        //public static HTTPRequest.Builder POSTRequest(this AHTTPTestClient              HTTPClient,
        //                                              URL                           RequestURL,
        //                                              AcceptTypes?                  Accept              = null,
        //                                              IHTTPAuthentication?          Authentication      = null,
        //                                              TOTPConfig?                   TOTPConfig          = null,
        //                                              String?                       UserAgent           = null,
        //                                              ConnectionType?               Connection          = null,
        //                                              Action<HTTPRequest.Builder>?  RequestBuilder      = null,
        //                                              CancellationToken             CancellationToken   = default)

        //    => HTTPClient.CreateRequest(
        //           HTTPMethod.POST,
        //           RequestURL,
        //           Accept,
        //           Authentication,
        //           null,  // Content
        //           null,  // ContentType
        //                  // TOTPConfig
        //           UserAgent,
        //           Connection,
        //           RequestBuilder,
        //           null,  // ConsumeRequestChunkedTEImmediately
        //           CancellationToken
        //       ).
        //       // Always send a Content-Length header, even when it's value is zero!
        //       SetContentLength(0);

        #endregion

        #region PUTRequest    (Path = "/", ...)

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
        public static HTTPRequest.Builder PUTRequest(this IHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     TOTPConfig?                   TOTPConfig          = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     EventTracking_Id?             EventTrackingId     = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PUT,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region PATCHRequest  (Path = "/", ...)

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
        public static HTTPRequest.Builder PATCHRequest(this IHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       TOTPConfig?                   TOTPConfig          = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       EventTracking_Id?             EventTrackingId     = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.PATCH,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region DELETERequest (Path = "/", ...)

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
        public static HTTPRequest.Builder DELETERequest(this IHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        TOTPConfig?                   TOTPConfig          = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        EventTracking_Id?             EventTrackingId     = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.DELETE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region OPTIONSRequest(Path = "/", ...)

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
        public static HTTPRequest.Builder OPTIONSRequest(this IHTTPClient              HTTPClient,
                                                         HTTPPath                      HTTPPath,
                                                         QueryString?                  QueryString         = null,
                                                         AcceptTypes?                  Accept              = null,
                                                         IHTTPAuthentication?          Authentication      = null,
                                                         TOTPConfig?                   TOTPConfig          = null,
                                                         String?                       UserAgent           = null,
                                                         ConnectionType?               Connection          = null,
                                                         Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                         EventTracking_Id?             EventTrackingId     = null,
                                                         CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.OPTIONS,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion


        // Inofficial HTTP methods

        #region CHECKRequest  (Path = "/", ...)

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
        public static HTTPRequest.Builder CHECKRequest(this IHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       TOTPConfig?                   TOTPConfig          = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       EventTracking_Id?             EventTrackingId     = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CHECK,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region COUNTRequest  (Path = "/", ...)

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
        public static HTTPRequest.Builder COUNTRequest(this IHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       TOTPConfig?                   TOTPConfig          = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       EventTracking_Id?             EventTrackingId     = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.COUNT,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region CLEARRequest  (Path = "/", ...)

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
        public static HTTPRequest.Builder CLEARRequest(this IHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       TOTPConfig?                   TOTPConfig          = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       EventTracking_Id?             EventTrackingId     = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CLEAR,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region CREATERequest (Path = "/", ...)

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
        public static HTTPRequest.Builder CREATERequest(this IHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        TOTPConfig?                   TOTPConfig          = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        EventTracking_Id?             EventTrackingId     = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.CREATE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region ADDRequest    (Path = "/", ...)

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
        public static HTTPRequest.Builder ADDRequest(this IHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     TOTPConfig?                   TOTPConfig          = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     EventTracking_Id?             EventTrackingId     = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.ADD,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region SETRequest    (Path = "/", ...)

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
        public static HTTPRequest.Builder SETRequest(this IHTTPClient              HTTPClient,
                                                     HTTPPath                      HTTPPath,
                                                     QueryString?                  QueryString         = null,
                                                     AcceptTypes?                  Accept              = null,
                                                     IHTTPAuthentication?          Authentication      = null,
                                                     TOTPConfig?                   TOTPConfig          = null,
                                                     String?                       UserAgent           = null,
                                                     ConnectionType?               Connection          = null,
                                                     Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                     EventTracking_Id?             EventTrackingId     = null,
                                                     CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.SET,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region TRACERequest  (Path = "/", ...)

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
        public static HTTPRequest.Builder TRACERequest(this IHTTPClient              HTTPClient,
                                                       HTTPPath                      HTTPPath,
                                                       QueryString?                  QueryString         = null,
                                                       AcceptTypes?                  Accept              = null,
                                                       IHTTPAuthentication?          Authentication      = null,
                                                       TOTPConfig?                   TOTPConfig          = null,
                                                       String?                       UserAgent           = null,
                                                       ConnectionType?               Connection          = null,
                                                       Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                       EventTracking_Id?             EventTrackingId     = null,
                                                       CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.TRACE,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region MIRRORRequest (Path = "/", ...)

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
        public static HTTPRequest.Builder MIRRORRequest(this IHTTPClient              HTTPClient,
                                                        HTTPPath                      HTTPPath,
                                                        QueryString?                  QueryString         = null,
                                                        AcceptTypes?                  Accept              = null,
                                                        IHTTPAuthentication?          Authentication      = null,
                                                        TOTPConfig?                   TOTPConfig          = null,
                                                        String?                       UserAgent           = null,
                                                        ConnectionType?               Connection          = null,
                                                        Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                        EventTracking_Id?             EventTrackingId     = null,
                                                        CancellationToken             CancellationToken   = default)

            => HTTPClient.CreateRequest(
                   HTTPMethod.MIRROR,
                   HTTPPath,
                   QueryString,
                   Accept,
                   Authentication,
                   null,  // Content
                   null,  // ContentType
                          // TOTPConfig
                   UserAgent,
                   Connection,
                   RequestBuilder,
                   null,  // ConsumeRequestChunkedTEImmediately
                   EventTrackingId,
                   CancellationToken
               );

        #endregion


    }


    /// <summary>
    /// The HTTP client interface.
    /// </summary>
    public interface IHTTPClient
    {

        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        URL                                                        RemoteURL                     { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        IIPAddress?                                                RemoteIPAddress               { get; }

        /// <summary>
        /// An optional HTTP virtual hostname.
        /// </summary>
        HTTPHostname?                                              VirtualHostname               { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        I18NString                                                 Description                   { get; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// Multiple optional TLS client certificates to use for HTTP authentication (not a chain of certificates!).
        /// </summary>
        IEnumerable<X509Certificate2>                              ClientCertificates            { get; }

        /// <summary>
        /// The optionalTLS client certificate context to use for HTTP authentication.
        /// </summary>
        SslStreamCertificateContext?                               ClientCertificateContext      { get; }

        /// <summary>
        /// The optional TLS client certificate chain to use for HTTP authentication.
        /// </summary>
        IEnumerable<X509Certificate2>                              ClientCertificateChain        { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        SslProtocols                                               TLSProtocols                  { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        IPVersionPreference                                        PreferIPv4                    { get; }

        /// <summary>
        /// An optional HTTP content type.
        /// </summary>
        HTTPContentType?                                           ContentType                   { get; }

        /// <summary>
        /// Optional HTTP accept types.
        /// </summary>
        AcceptTypes?                                               Accept                        { get; }

        /// <summary>
        /// The optional HTTP authentication to use.
        /// </summary>
        IHTTPAuthentication?                                       HTTPAuthentication            { get; set; }

        /// <summary>
        /// The optional Time-Based One-Time Password (TOTP) generator.
        /// </summary>
        TOTPConfig?                                                TOTPConfig                    { get; set; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        String?                                                    HTTPUserAgent                 { get; }

        /// <summary>
        /// The optional HTTP connection type.
        /// </summary>
        ConnectionType?                                            Connection                    { get; }

        /// <summary>
        /// The timeout for HTTP requests.
        /// </summary>
        TimeSpan                                                   RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        TransmissionRetryDelayDelegate                             TransmissionRetryDelay        { get; }

        /// <summary>
        /// The maximum number of transmission retries for HTTP request.
        /// </summary>
        UInt16                                                     MaxNumberOfRetries            { get; }

        /// <summary>
        /// Make use of HTTP pipelining.
        /// </summary>
        Boolean                                                    UseHTTPPipelining             { get; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        HTTPClientLogger?                                          HTTPLogger                    { get; }

        /// <summary>
        /// The DNS client to use.
        /// </summary>
        IDNSClient?                                                DNSClient                     { get; }



        UInt64                                                     KeepAliveMessageCount         { get; }



        //int Available { get; }
        //X509Certificate ClientCert { get; }
        Boolean Connected { get; }

        //LingerOption LingerState { get; set; }
        //LocalCertificateSelectionHandler LocalCertificateSelector { get; }
        //bool NoDelay { get; set; }
        //byte TTL { get; set; }

        //event HTTPClient.OnDataReadDelegate OnDataRead;

        //void Close();




        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                          HTTPPath                      HTTPPath,
                                          QueryString?                  QueryString                          = null,
                                          AcceptTypes?                  Accept                               = null,
                                          IHTTPAuthentication?          Authentication                       = null,
                                          Byte[]?                       Content                              = null,
                                          HTTPContentType?              ContentType                          = null,
                                          String?                       UserAgent                            = null,
                                          ConnectionType?               Connection                           = null,
                                          Action<HTTPRequest.Builder>?  RequestBuilder                       = null,
                                          Boolean?                      ConsumeRequestChunkedTEImmediately   = null,
                                          EventTracking_Id?             EventTrackingId                      = null,
                                          CancellationToken             CancellationToken                    = default);

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>u
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task<HTTPResponse>

            RunRequest(HTTPMethod                    HTTPMethod,
                       HTTPPath                      HTTPPath,
                       QueryString?                  QueryString                           = null,
                       AcceptTypes?                  Accept                                = null,
                       IHTTPAuthentication?          Authentication                        = null,
                       Byte[]?                       Content                               = null,
                       HTTPContentType?              ContentType                           = null,
                       String?                       UserAgent                             = null,
                       ConnectionType?               Connection                            = null,
                       Action<HTTPRequest.Builder>?  RequestBuilder                        = null,
                       Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                      ConsumeResponseChunkedTEImmediately   = null,
                       EventTracking_Id?             EventTrackingId                       = null,
                       TimeSpan?                     RequestTimeout                        = null,

                       ClientRequestLogHandler?      RequestLogDelegate                    = null,
                       ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                       CancellationToken             CancellationToken                     = default);


    }

}
