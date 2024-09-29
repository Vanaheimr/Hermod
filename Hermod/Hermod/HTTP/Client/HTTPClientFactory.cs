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

#region Usings

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class IHTTPClientCommandsExtensions
    {

        // RFC 2616 - HTTP/1.1

        #region GET     (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> GET(this IHTTPClientCommands      HTTPClientCommand,
                                             HTTPPath                      Path,
                                             QueryString?                  QueryString           = null,
                                             AcceptTypes?                  Accept                = null,
                                             IHTTPAuthentication?          Authentication        = null,
                                             String?                       UserAgent             = null,
                                             ConnectionType?               Connection            = null,
                                             TimeSpan?                     RequestTimeout        = null,
                                             EventTracking_Id?             EventTrackingId       = null,
                                             Byte                          NumberOfRetry         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                             ClientRequestLogHandler?      RequestLogDelegate    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                             CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.GET,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region HEAD    (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP HEAD request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> HEAD(this IHTTPClientCommands      HTTPClientCommand,
                                              HTTPPath                      Path,
                                              QueryString?                  QueryString           = null,
                                              AcceptTypes?                  Accept                = null,
                                              IHTTPAuthentication?          Authentication        = null,
                                              String?                       UserAgent             = null,
                                              ConnectionType?               Connection            = null,
                                              TimeSpan?                     RequestTimeout        = null,
                                              EventTracking_Id?             EventTrackingId       = null,
                                              Byte                          NumberOfRetry         = 0,
                                              Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                              ClientRequestLogHandler?      RequestLogDelegate    = null,
                                              ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                              CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.HEAD,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region POST    (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> POST(this IHTTPClientCommands      HTTPClientCommand,
                                              HTTPPath                      Path,
                                              Byte[]                        Content,
                                              HTTPContentType?              ContentType           = null,
                                              QueryString?                  QueryString           = null,
                                              AcceptTypes?                  Accept                = null,
                                              IHTTPAuthentication?          Authentication        = null,
                                              String?                       UserAgent             = null,
                                              ConnectionType?               Connection            = null,
                                              TimeSpan?                     RequestTimeout        = null,
                                              EventTracking_Id?             EventTrackingId       = null,
                                              Byte                          NumberOfRetry         = 0,
                                              Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                              ClientRequestLogHandler?      RequestLogDelegate    = null,
                                              ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                              CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.POST,
                                 Path,
                                 Content,
                                 ContentType    ?? HTTPClientCommand.ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ).SetContentLength(0), // Always send a Content-Length header, even when it's value is zero!
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region PUT     (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> PUT(this IHTTPClientCommands      HTTPClientCommand,
                                             HTTPPath                      Path,
                                             Byte[]                        Content,
                                             HTTPContentType               ContentType,
                                             QueryString?                  QueryString           = null,
                                             AcceptTypes?                  Accept                = null,
                                             IHTTPAuthentication?          Authentication        = null,
                                             String?                       UserAgent             = null,
                                             ConnectionType?               Connection            = null,
                                             TimeSpan?                     RequestTimeout        = null,
                                             EventTracking_Id?             EventTrackingId       = null,
                                             Byte                          NumberOfRetry         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                             ClientRequestLogHandler?      RequestLogDelegate    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                             CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.PUT,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region PATCH   (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP PATCH request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> PATCH(this IHTTPClientCommands      HTTPClientCommand,
                                               HTTPPath                      Path,
                                               Byte[]                        Content,
                                               HTTPContentType               ContentType,
                                               QueryString?                  QueryString           = null,
                                               AcceptTypes?                  Accept                = null,
                                               IHTTPAuthentication?          Authentication        = null,
                                               String?                       UserAgent             = null,
                                               ConnectionType?               Connection            = null,
                                               TimeSpan?                     RequestTimeout        = null,
                                               EventTracking_Id?             EventTrackingId       = null,
                                               Byte                          NumberOfRetry         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                               ClientRequestLogHandler?      RequestLogDelegate    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                               CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.PATCH,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region DELETE  (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP DELETE request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> DELETE(this IHTTPClientCommands      HTTPClientCommand,
                                                HTTPPath                      Path,
                                                QueryString?                  QueryString           = null,
                                                AcceptTypes?                  Accept                = null,
                                                IHTTPAuthentication?          Authentication        = null,
                                                String?                       UserAgent             = null,
                                                ConnectionType?               Connection            = null,
                                                TimeSpan?                     RequestTimeout        = null,
                                                EventTracking_Id?             EventTrackingId       = null,
                                                Byte                          NumberOfRetry         = 0,
                                                Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                                ClientRequestLogHandler?      RequestLogDelegate    = null,
                                                ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                                CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                    client => client.CreateRequest(
                                  HTTPMethod.DELETE,
                                  Path,
                                  QueryString,
                                  Accept         ?? HTTPClientCommand.Accept,
                                  Authentication ?? HTTPClientCommand.Authentication,
                                  UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                  Connection     ?? HTTPClientCommand.Connection,
                                  RequestBuilder,
                                  CancellationToken
                              ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region OPTIONS (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP OPTIONS request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> OPTIONS(this IHTTPClientCommands      HTTPClientCommand,
                                                 HTTPPath                      Path,
                                                 QueryString?                  QueryString           = null,
                                                 AcceptTypes?                  Accept                = null,
                                                 IHTTPAuthentication?          Authentication        = null,
                                                 String?                       UserAgent             = null,
                                                 ConnectionType?               Connection            = null,
                                                 TimeSpan?                     RequestTimeout        = null,
                                                 EventTracking_Id?             EventTrackingId       = null,
                                                 Byte                          NumberOfRetry         = 0,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                                 ClientRequestLogHandler?      RequestLogDelegate    = null,
                                                 ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                                 CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.OPTIONS,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion


        // Inofficial HTTP methods

        #region CHECK  (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CHECK request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> CHECK(this IHTTPClientCommands      HTTPClientCommand,
                                               HTTPPath                      Path,
                                               QueryString?                  QueryString           = null,
                                               AcceptTypes?                  Accept                = null,
                                               IHTTPAuthentication?          Authentication        = null,
                                               String?                       UserAgent             = null,
                                               ConnectionType?               Connection            = null,
                                               TimeSpan?                     RequestTimeout        = null,
                                               EventTracking_Id?             EventTrackingId       = null,
                                               Byte                          NumberOfRetry         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                               ClientRequestLogHandler?      RequestLogDelegate    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                               CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.CHECK,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region COUNT  (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> COUNT(this IHTTPClientCommands      HTTPClientCommand,
                                               HTTPPath                      Path,
                                               QueryString?                  QueryString           = null,
                                               AcceptTypes?                  Accept                = null,
                                               IHTTPAuthentication?          Authentication        = null,
                                               String?                       UserAgent             = null,
                                               ConnectionType?               Connection            = null,
                                               TimeSpan?                     RequestTimeout        = null,
                                               EventTracking_Id?             EventTrackingId       = null,
                                               Byte                          NumberOfRetry         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                               ClientRequestLogHandler?      RequestLogDelegate    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                               CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.COUNT,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region CLEAR  (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CLEAR request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> CLEAR(this IHTTPClientCommands      HTTPClientCommand,
                                               HTTPPath                      Path,
                                               QueryString?                  QueryString           = null,
                                               AcceptTypes?                  Accept                = null,
                                               IHTTPAuthentication?          Authentication        = null,
                                               String?                       UserAgent             = null,
                                               ConnectionType?               Connection            = null,
                                               TimeSpan?                     RequestTimeout        = null,
                                               EventTracking_Id?             EventTrackingId       = null,
                                               Byte                          NumberOfRetry         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                               ClientRequestLogHandler?      RequestLogDelegate    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                               CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.CLEAR,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region CREATE (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> CREATE(this IHTTPClientCommands      HTTPClientCommand,
                                                HTTPPath                      Path,
                                                Byte[]                        Content,
                                                HTTPContentType               ContentType,
                                                QueryString?                  QueryString           = null,
                                                AcceptTypes?                  Accept                = null,
                                                IHTTPAuthentication?          Authentication        = null,
                                                String?                       UserAgent             = null,
                                                ConnectionType?               Connection            = null,
                                                TimeSpan?                     RequestTimeout        = null,
                                                EventTracking_Id?             EventTrackingId       = null,
                                                Byte                          NumberOfRetry         = 0,
                                                Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                                ClientRequestLogHandler?      RequestLogDelegate    = null,
                                                ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                                CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.CREATE,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region ADD    (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> ADD(this IHTTPClientCommands      HTTPClientCommand,
                                             HTTPPath                      Path,
                                             Byte[]                        Content,
                                             HTTPContentType               ContentType,
                                             QueryString?                  QueryString           = null,
                                             AcceptTypes?                  Accept                = null,
                                             IHTTPAuthentication?          Authentication        = null,
                                             String?                       UserAgent             = null,
                                             ConnectionType?               Connection            = null,
                                             TimeSpan?                     RequestTimeout        = null,
                                             EventTracking_Id?             EventTrackingId       = null,
                                             Byte                          NumberOfRetry         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                             ClientRequestLogHandler?      RequestLogDelegate    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                             CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.ADD,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region SET    (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> SET(this IHTTPClientCommands      HTTPClientCommand,
                                             HTTPPath                      Path,
                                             Byte[]                        Content,
                                             HTTPContentType               ContentType,
                                             QueryString?                  QueryString           = null,
                                             AcceptTypes?                  Accept                = null,
                                             IHTTPAuthentication?          Authentication        = null,
                                             String?                       UserAgent             = null,
                                             ConnectionType?               Connection            = null,
                                             TimeSpan?                     RequestTimeout        = null,
                                             EventTracking_Id?             EventTrackingId       = null,
                                             Byte                          NumberOfRetry         = 0,
                                             Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                             ClientRequestLogHandler?      RequestLogDelegate    = null,
                                             ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                             CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.SET,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region TRACE  (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> TRACE(this IHTTPClientCommands      HTTPClientCommand,
                                               HTTPPath                      Path,
                                               QueryString?                  QueryString           = null,
                                               AcceptTypes?                  Accept                = null,
                                               IHTTPAuthentication?          Authentication        = null,
                                               String?                       UserAgent             = null,
                                               ConnectionType?               Connection            = null,
                                               TimeSpan?                     RequestTimeout        = null,
                                               EventTracking_Id?             EventTrackingId       = null,
                                               Byte                          NumberOfRetry         = 0,
                                               Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                               ClientRequestLogHandler?      RequestLogDelegate    = null,
                                               ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                               CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.TRACE,
                                 Path,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region MIRROR (this HTTPClientCommand, Path = "/", ...)

        /// <summary>
        /// Create a new HTTP MIRROR request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="Content">A HTTP content.</param>
        /// <param name="ContentType">A HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="NumberOfRetry">The optional number of retries.</param>
        /// <param name="RequestBuilder">An optional delegate to configure the new HTTP request builder.</param>
        /// <param name="RequestLogDelegate">An optional delegate to log HTTP requests.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log HTTP responses.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse> MIRROR(this IHTTPClientCommands      HTTPClientCommand,
                                                HTTPPath                      Path,
                                                Byte[]                        Content,
                                                HTTPContentType               ContentType,
                                                QueryString?                  QueryString           = null,
                                                AcceptTypes?                  Accept                = null,
                                                IHTTPAuthentication?          Authentication        = null,
                                                String?                       UserAgent             = null,
                                                ConnectionType?               Connection            = null,
                                                TimeSpan?                     RequestTimeout        = null,
                                                EventTracking_Id?             EventTrackingId       = null,
                                                Byte                          NumberOfRetry         = 0,
                                                Action<HTTPRequest.Builder>?  RequestBuilder        = null,
                                                ClientRequestLogHandler?      RequestLogDelegate    = null,
                                                ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                                CancellationToken             CancellationToken     = default)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.MIRROR,
                                 Path,
                                 Content,
                                 ContentType,
                                 QueryString,
                                 Accept         ?? HTTPClientCommand.Accept,
                                 Authentication ?? HTTPClientCommand.Authentication,
                                 UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
                                 Connection     ?? HTTPClientCommand.Connection,
                                 RequestBuilder,
                                 CancellationToken
                             ),
                   RequestLogDelegate,
                   ResponseLogDelegate,
                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion


    }

    public interface IHTTPClientCommands : IHTTPClient
    {

        Task<HTTPResponse> Execute(Func<AHTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                   ClientRequestLogHandler?        RequestLogDelegate    = null,
                                   ClientResponseLogHandler?       ResponseLogDelegate   = null,

                                   EventTracking_Id?               EventTrackingId       = null,
                                   TimeSpan?                       RequestTimeout        = null,
                                   Byte                            NumberOfRetry         = 0,
                                   CancellationToken               CancellationToken     = default);

        Task<HTTPResponse> Execute(HTTPRequest                     Request,
                                   ClientRequestLogHandler?        RequestLogDelegate    = null,
                                   ClientResponseLogHandler?       ResponseLogDelegate   = null,

                                   EventTracking_Id?               EventTrackingId       = null,
                                   TimeSpan?                       RequestTimeout        = null,
                                   Byte                            NumberOfRetry         = 0,
                                   CancellationToken               CancellationToken     = default);

    }


        /// <summary>
    /// A factory to create a HTTPClient or HTTPSClient based on the given URL protocol.
    /// </summary>
    public static class HTTPClientFactory
    {

        /// <summary>
        /// Create a new HTTP/HTTPS client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">The internal buffer size.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public static IHTTPClientCommands Create(URL                                                        RemoteURL,
                                                 HTTPHostname?                                              VirtualHostname              = null,
                                                 I18NString?                                                Description                  = null,
                                                 Boolean?                                                   PreferIPv4                   = null,
                                                 RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                                                 LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                                                 X509Certificate?                                           ClientCert                   = null,
                                                 SslProtocols?                                              TLSProtocol                  = null,
                                                 HTTPContentType?                                           ContentType                  = null,
                                                 AcceptTypes?                                               Accept                       = null,
                                                 IHTTPAuthentication?                                       HTTPAuthentication           = null,
                                                 String?                                                    HTTPUserAgent                = null,
                                                 ConnectionType?                                            Connection                   = null,
                                                 TimeSpan?                                                  RequestTimeout               = null,
                                                 TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                                                 UInt16?                                                    MaxNumberOfRetries           = null,
                                                 UInt32?                                                    InternalBufferSize           = null,
                                                 Boolean                                                    UseHTTPPipelining            = false,
                                                 Boolean?                                                   DisableLogging               = false,
                                                 HTTPClientLogger?                                          HTTPLogger                   = null,
                                                 DNSClient?                                                 DNSClient                    = null)

            => RemoteURL.Protocol == URLProtocols.http ||
               RemoteURL.Protocol == URLProtocols.ws

                   ? new HTTPClient(
                         RemoteURL,
                         VirtualHostname,
                         Description,
                         PreferIPv4,
                         ContentType,
                         Accept,
                         HTTPAuthentication,
                         HTTPUserAgent,
                         Connection,
                         RequestTimeout,
                         TransmissionRetryDelay,
                         MaxNumberOfRetries,
                         InternalBufferSize,
                         UseHTTPPipelining,
                         DisableLogging,
                         HTTPLogger,
                         DNSClient
                     )

                   : new HTTPSClient(
                         RemoteURL,
                         VirtualHostname,
                         Description,
                         PreferIPv4,
                         RemoteCertificateValidator,
                         LocalCertificateSelector,
                         ClientCert,
                         TLSProtocol,
                         ContentType,
                         Accept,
                         HTTPAuthentication,
                         HTTPUserAgent,
                         Connection,
                         RequestTimeout,
                         TransmissionRetryDelay,
                         MaxNumberOfRetries,
                         InternalBufferSize,
                         UseHTTPPipelining,
                         DisableLogging,
                         HTTPLogger,
                         DNSClient
                     );

    }

}
