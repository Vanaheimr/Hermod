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

        #region GET    (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPClientCommand">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static Task<HTTPResponse> GET(this IHTTPClientCommands      HTTPClientCommand,
                                             HTTPPath                      Path,
                                             Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                             IHTTPAuthentication?          Authentication   = null)

            => HTTPClientCommand.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.GET,
                                 Path,
                                 BuilderAction,
                                 Authentication
                             )
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
        public static Task<HTTPResponse> HEAD(this AHTTPClient              HTTPClient,
                                              HTTPPath                      Path,
                                              Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                              IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.HEAD,
                                 Path,
                                 BuilderAction,
                                 Authentication
                             )
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
        public static Task<HTTPResponse> POST(this AHTTPClient              HTTPClient,
                                              HTTPPath                      Path,
                                              Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                              IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.POST,
                                 Path,
                                 BuilderAction,
                                 Authentication
                               // Always send a Content-Length header, even when it's value is zero!
                             ).SetContentLength(0)
               );

        #endregion

        #region PUT    (this AHTTPClient, Path = "/", BuilderAction = null, Authentication = null)

        /// <summary>
        /// Create a new HTTP PUT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        public static Task<HTTPResponse> PUT(this AHTTPClient              HTTPClient,
                                             HTTPPath                      Path,
                                             Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                             IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.PUT,
                                 Path,
                                 BuilderAction,
                                 Authentication
                             )
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
        public static Task<HTTPResponse> PATCH(this AHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                               IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.PATCH,
                                 Path,
                                 BuilderAction,
                                 Authentication
                             )
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
        public static Task<HTTPResponse> DELETE(this AHTTPClient              HTTPClient,
                                                HTTPPath                      Path,
                                                Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                IHTTPAuthentication?          Authentication   = null)

            =>  HTTPClient.Execute(
                    client => client.CreateRequest(
                                  HTTPMethod.DELETE,
                                  Path,
                                  BuilderAction,
                                  Authentication
                              )
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
        public static Task<HTTPResponse> OPTIONS(this AHTTPClient              HTTPClient,
                                                 HTTPPath                      Path,
                                                 Action<HTTPRequest.Builder>?  BuilderAction    = null,
                                                 IHTTPAuthentication?          Authentication   = null)

            => HTTPClient.Execute(
                   client => client.CreateRequest(
                                 HTTPMethod.OPTIONS,
                                 Path,
                                 BuilderAction,
                                 Authentication
                             )
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
        public static Task<HTTPResponse> CHECK(this AHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.CHECK,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region COUNT  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP COUNT request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> COUNT(this AHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.COUNT,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region CLEAR  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CLEAR request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> CLEAR(this AHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.CLEAR,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region CREATE (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP CREATE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> CREATE(this AHTTPClient              HTTPClient,
                                                HTTPPath                      Path,
                                                Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.CREATE,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region ADD    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP ADD request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> ADD(this AHTTPClient              HTTPClient,
                                             HTTPPath                      Path,
                                             Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.ADD,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region SET    (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP SET request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> SET(this AHTTPClient              HTTPClient,
                                             HTTPPath                      Path,
                                             Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.SET,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region TRACE  (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP TRACE request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A HTTP request object.</returns>
        public static Task<HTTPResponse> TRACE(this AHTTPClient              HTTPClient,
                                               HTTPPath                      Path,
                                               Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.TRACE,
                                                                 Path,
                                                                 BuilderAction));

        #endregion

        #region MIRROR (this AHTTPClient, Path = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP MIRROR request.
        /// </summary>
        /// <param name="HTTPClient">A HTTP client.</param>
        /// <param name="Path">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        public static Task<HTTPResponse> MIRROR(this AHTTPClient              HTTPClient,
                                                HTTPPath                      Path,
                                                Action<HTTPRequest.Builder>?  BuilderAction = null)

            => HTTPClient.Execute(client => client.CreateRequest(HTTPMethod.MIRROR,
                                                                 Path,
                                                                 BuilderAction));

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
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
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
                                                 String?                                                    HTTPUserAgent                = null,
                                                 IHTTPAuthentication?                                       HTTPAuthentication           = null,
                                                 TimeSpan?                                                  RequestTimeout               = null,
                                                 TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                                                 UInt16?                                                    MaxNumberOfRetries           = null,
                                                 UInt32?                                                    InternalBufferSize           = null,
                                                 Boolean                                                    UseHTTPPipelining            = false,
                                                 Boolean?                                                   DisableLogging               = false,
                                                 HTTPClientLogger?                                          HTTPLogger                   = null,
                                                 DNSClient?                                                 DNSClient                    = null)

            => RemoteURL.Protocol == URLProtocols.http

                   ? new HTTPClient(
                         RemoteURL,
                         VirtualHostname,
                         Description,
                         PreferIPv4,
                         HTTPUserAgent,
                         HTTPAuthentication,
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
                         HTTPUserAgent,
                         HTTPAuthentication,
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
