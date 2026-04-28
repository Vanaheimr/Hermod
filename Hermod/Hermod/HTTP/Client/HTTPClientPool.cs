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
using System.Threading.Channels;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPClientPoolExtensions
    {

        #region GET             (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse>

            GET(this HTTPClientPool           HTTPClientPool,
                HTTPPath                      Path,
                QueryString?                  QueryString                           = null,
                AcceptTypes?                  Accept                                = null,
                IHTTPAuthentication?          Authentication                        = null,
                String?                       UserAgent                             = null,
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

            => HTTPClientPool.RunRequest(
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

        #region GET_Text        (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<String>>

            GET_Text(this HTTPClientPool           HTTPClientPool,
                     HTTPPath                      Path,
                     QueryString?                  QueryString                           = null,
                     AcceptTypes?                  Accept                                = null,
                     IHTTPAuthentication?          Authentication                        = null,
                     String?                       UserAgent                             = null,
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

        {

            var response = await HTTPClientPool.RunRequest(
                                     HTTPMethod.GET,
                                     Path,
                                     QueryString,
                                     Accept     ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.PLAIN),
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

            return new HTTPResponse<String>(
                       response,
                       response.HTTPBodyAsUTF8String ?? String.Empty
                   );

        }

        #endregion

        #region GET_JSONObject  (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<JObject>>

            GET_JSONObject(this HTTPClientPool           HTTPClientPool,
                           HTTPPath                      Path,
                           QueryString?                  QueryString                           = null,
                           AcceptTypes?                  Accept                                = null,
                           IHTTPAuthentication?          Authentication                        = null,
                           String?                       UserAgent                             = null,
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

        {

            var response  = await HTTPClientPool.RunRequest(
                                      HTTPMethod.GET,
                                      Path,
                                      QueryString,
                                      Accept     ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSONLD_UTF8),
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

            try
            {

                var text = response.HTTPBodyAsUTF8String;
                if (text.IsNotNullOrEmpty())
                    return new HTTPResponse<JObject>(
                               response,
                               JObject.Parse(text)
                           );

                return HTTPResponse<JObject>.FromError(
                           response
                       );

            }
            catch (Exception e)
            {
                return HTTPResponse<JObject>.FromException(
                           response,
                           e
                       );
            }

        }

        #endregion

        #region GET_JSONArray   (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<JArray>>

            GET_JSONArray(this HTTPClientPool           HTTPClientPool,
                          HTTPPath                      Path,
                          QueryString?                  QueryString                           = null,
                          AcceptTypes?                  Accept                                = null,
                          IHTTPAuthentication?          Authentication                        = null,
                          String?                       UserAgent                             = null,
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

        {

            var response  = await HTTPClientPool.RunRequest(
                                      HTTPMethod.GET,
                                      Path,
                                      QueryString,
                                      Accept     ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSONLD_UTF8),
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

            try
            {

                var text = response.HTTPBodyAsUTF8String;
                if (text.IsNotNullOrEmpty())
                    return new HTTPResponse<JArray>(
                               response,
                               JArray.Parse(text)
                           );

                return HTTPResponse<JArray>.FromError(
                           response
                       );

            }
            catch (Exception e)
            {
                return HTTPResponse<JArray>.FromException(
                           response,
                           e
                       );
            }

        }

        #endregion


        #region POST            (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static Task<HTTPResponse>

            POST(this HTTPClientPool           HTTPClientPool,
                 HTTPPath                      Path,
                 Byte[]                        Content,
                 HTTPContentType?              ContentType                           = null,
                 QueryString?                  QueryString                           = null,
                 AcceptTypes?                  Accept                                = null,
                 IHTTPAuthentication?          Authentication                        = null,
                 String?                       UserAgent                             = null,
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

            => HTTPClientPool.RunRequest(
                   HTTPMethod.POST,
                   Path,
                   QueryString,
                   Accept,
                   Authentication,
                   Content,
                   ContentType,
                   UserAgent,
                   Connection  ?? ConnectionType.KeepAlive,
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

        #region POST_Text       (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<String>>

            POST_Text(this HTTPClientPool           HTTPClientPool,
                      HTTPPath                      Path,
                      Byte[]                        Content,
                      HTTPContentType?              ContentType                           = null,
                      QueryString?                  QueryString                           = null,
                      AcceptTypes?                  Accept                                = null,
                      IHTTPAuthentication?          Authentication                        = null,
                      String?                       UserAgent                             = null,
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

        {

            var response = await HTTPClientPool.RunRequest(
                                     HTTPMethod.POST,
                                     Path,
                                     QueryString,
                                     Accept      ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.PLAIN),
                                     Authentication,
                                     Content,
                                     ContentType ?? HTTPContentType.Text.PLAIN,
                                     UserAgent,
                                     Connection  ?? ConnectionType.KeepAlive,
                                     RequestBuilder,

                                     ConsumeRequestChunkedTEImmediately,
                                     ConsumeResponseChunkedTEImmediately,

                                     EventTrackingId,
                                     RequestTimeout,

                                     RequestLogDelegate,
                                     ResponseLogDelegate,
                                     CancellationToken
                                 );

            return new HTTPResponse<String>(
                       response,
                       response.HTTPBodyAsUTF8String ?? String.Empty
                   );

        }

        #endregion

        #region POST_JSONObject (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<JObject>>

            POST_JSONObject(this HTTPClientPool           HTTPClientPool,
                            HTTPPath                      Path,
                            Byte[]                        Content,
                            HTTPContentType?              ContentType                           = null,
                            QueryString?                  QueryString                           = null,
                            AcceptTypes?                  Accept                                = null,
                            IHTTPAuthentication?          Authentication                        = null,
                            String?                       UserAgent                             = null,
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

        {

            var response  = await HTTPClientPool.RunRequest(
                                      HTTPMethod.POST,
                                      Path,
                                      QueryString,
                                      Accept      ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSONLD_UTF8),
                                      Authentication,
                                      Content,
                                      ContentType ?? HTTPContentType.Application.JSONLD_UTF8,
                                      UserAgent,
                                      Connection  ?? ConnectionType.KeepAlive,
                                      RequestBuilder,

                                      ConsumeRequestChunkedTEImmediately,
                                      ConsumeResponseChunkedTEImmediately,

                                      EventTrackingId,
                                      RequestTimeout,

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;
                if (text.IsNotNullOrEmpty())
                    return new HTTPResponse<JObject>(
                               response,
                               JObject.Parse(text)
                           );

                return HTTPResponse<JObject>.FromError(
                           response
                       );

            }
            catch (Exception e)
            {
                return HTTPResponse<JObject>.FromException(
                           response,
                           e
                       );
            }

        }

        #endregion

        #region POST_JSONArray  (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async Task<HTTPResponse<JArray>>

            POST_JSONArray(this HTTPClientPool           HTTPClientPool,
                           HTTPPath                      Path,
                           Byte[]                        Content,
                           HTTPContentType?              ContentType                           = null,
                           QueryString?                  QueryString                           = null,
                           AcceptTypes?                  Accept                                = null,
                           IHTTPAuthentication?          Authentication                        = null,
                           String?                       UserAgent                             = null,
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

        {

            var response  = await HTTPClientPool.RunRequest(
                                      HTTPMethod.POST,
                                      Path,
                                      QueryString,
                                      Accept      ?? AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSONLD_UTF8),
                                      Authentication,
                                      Content,
                                      ContentType ?? HTTPContentType.Application.JSONLD_UTF8,
                                      UserAgent,
                                      Connection  ?? ConnectionType.KeepAlive,
                                      RequestBuilder,

                                      ConsumeRequestChunkedTEImmediately,
                                      ConsumeResponseChunkedTEImmediately,

                                      EventTrackingId,
                                      RequestTimeout,

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;
                if (text.IsNotNullOrEmpty())
                    return new HTTPResponse<JArray>(
                               response,
                               JArray.Parse(text)
                           );

                return HTTPResponse<JArray>.FromError(
                           response
                       );

            }
            catch (Exception e)
            {
                return HTTPResponse<JArray>.FromException(
                           response,
                           e
                       );
            }

        }

        #endregion

    }



    /// <summary>
    /// A pool of HTTP clients, that use HTTP Keep-Alive and HTTP Pipelining.
    /// </summary>
    public class HTTPClientPool : IHTTPClient,
                                  IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        private readonly Channel<HTTPClient>       idleHTTPClients;
        private readonly SemaphoreSlim                 maxNumberOfHTTPClientsSemaphore;
        private readonly Func<String, HTTPClient>  httpClientFactory;
        private          Int32                         clientCounter;
        private          Int32                         isDisposed;

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public  const    String                        DefaultHTTPUserAgent        = "Hermod HTTP Client Pool";

        public  const    UInt16                        DefaultMaxNumberOfClients   = 5;

        #endregion

        #region Properties

        public String                                                     Id                            { get; }

        public Boolean                                                    IsHTTPConnected               { get; private set; } = false;

        public TimeSpan                                                   MaxSemaphoreWaitTime          { get; set; } = TimeSpan.FromSeconds(30);

        public UInt16                                                     MaxNumberOfClients            { get; private set; }

        public DefaultRequestBuilderDelegate                              DefaultRequestBuilder         { get; }

        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        public URL                                                        RemoteURL                     { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                                                RemoteIPAddress               { get; private set; }

        /// <summary>
        /// The HTTP/TCP port to connect to.
        /// </summary>
        public IPPort?                                                    RemotePort                    { get; }

        /// <summary>
        /// The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public DomainName?                                                DomainName                    { get; }

        /// <summary>
        /// The DNS Service to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public SRV_Spec?                                                  DNSService                    { get; }

        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                                              VirtualHostname               { get; }

        /// <summary>
        /// The Remote X.509 certificate.
        /// </summary>
        public X509Certificate2?                                          RemoteCertificate             { get; private set; }

        /// <summary>
        /// The Remote X.509 certificate chain.
        /// </summary>
        public X509Chain?                                                 RemoteCertificateChain        { get; private set; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator    { get; protected internal set; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?                          LocalCertificateSelector      { get; }

        /// <summary>
        /// Multiple optional TLS client certificates to use for HTTP authentication (not a chain of certificates!).
        /// </summary>
        public IEnumerable<X509Certificate2>                              ClientCertificates            { get; }

        /// <summary>
        /// The optionalTLS client certificate context to use for HTTP authentication.
        /// </summary>
        public SslStreamCertificateContext?                               ClientCertificateContext      { get; }

        /// <summary>
        /// The optional TLS client certificate chain to use for HTTP authentication.
        /// </summary>
        public IEnumerable<X509Certificate2>                              ClientCertificateChain        { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                                               TLSProtocols                  { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public IPVersionPreference                                        PreferIPv4                    { get; }

        /// <summary>
        /// An optional HTTP content type.
        /// </summary>
        public HTTPContentType?                                           ContentType                   { get; }

        /// <summary>
        /// The optional HTTP accept header.
        /// </summary>
        public AcceptTypes?                                               Accept                        { get; }

        /// <summary>
        /// The optional HTTP authentication.
        /// </summary>
        public IHTTPAuthentication?                                       HTTPAuthentication            { get; set; }

        /// <summary>
        /// The optional Time-Based One-Time Password (TOTP) generator configuration.
        /// </summary>
        public TOTPConfig?                                                TOTPConfig                    { get; set; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                                     HTTPUserAgent                 { get; }

        /// <summary>
        /// The optional HTTP connection type.
        /// </summary>
        public ConnectionType?                                            Connection                    { get; }

        public Boolean Connected
            => throw new NotImplementedException();

        /// <summary>
        /// The default delay between transmission retries.
        /// </summary>
        public static readonly TransmissionRetryDelayDelegate             DefaultTransmissionRetryDelay =
             (retryCounter) => TimeSpan.FromSeconds(retryCounter * retryCounter * TimeSpan.FromSeconds(2).TotalSeconds);


        //public DefaultRequestBuilderDelegate     DefaultRequestBuilder     { get;}
        //public DefaultRequestBuilder2Delegate    DefaultRequestBuilder     { get;}



        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                                                   RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate                             TransmissionRetryDelay        { get; }

        /// <summary>
        /// The size of the internal HTTP client buffers.
        /// </summary>
        public UInt32                                                     InternalBufferSize            { get; }

        /// <summary>
        /// The maximum number of retries when communicating with the remote HTTP service.
        /// </summary>
        public UInt16                                                     MaxNumberOfRetries            { get; }

        /// <summary>
        /// Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.
        /// </summary>
        public Boolean                                                    UseHTTPPipelining             { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        public I18NString                                                 Description                   { get; set; }

        /// <summary>
        /// Disable any logging.
        /// </summary>
        public Boolean                                                    DisableLogging                { get; }

        /// <summary>
        /// The HTTP client logger.
        /// </summary>
        public HTTPClientLogger?                                          HTTPLogger                    { get; set; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public IDNSClient?                                                DNSClient                     { get; }

        public UInt64                                                     KeepAliveMessageCount         { get; private set; } = 0;


        public IHTTPAuthentication? Authentication
            => throw new NotImplementedException();

        #endregion

        #region Events

        //public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        //public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region HTTPClientPool(IPAddress, ...)

        public HTTPClientPool(IIPAddress                                                 IPAddress,
                              IPPort?                                                    TCPPort                               = null,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificates                    = null,
                              SslStreamCertificateContext?                               ClientCertificateContext              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificateChain                = null,
                              SslProtocols?                                              TLSProtocols                          = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                           CertificateChainPolicy                = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode        = null,
                              Boolean?                                                   EnforceTLS                            = null,
                              IEnumerable<SslApplicationProtocol>?                       ApplicationProtocols                  = null,
                              Boolean?                                                   AllowRenegotiation                    = null,
                              Boolean?                                                   AllowTLSResume                        = null,
                              TOTPConfig?                                                TOTPConfig                            = null,

                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              UInt16?                                                    MaxNumberOfClients                    = null,

                              IPVersionPreference?                                       PreferIPv4                            = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    BufferSize                            = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null)
        {

            this.RemoteIPAddress                  = IPAddress;
            this.RemotePort                       = TCPPort ?? IPPort.HTTPS;
            this.Id                               = $"{IPAddress}:{RemotePort}";

            this.Description                      = Description            ?? I18NString.Empty;
            this.HTTPUserAgent                    = HTTPUserAgent          ?? DefaultHTTPUserAgent;
            this.MaxNumberOfClients               = MaxNumberOfClients     ?? DefaultMaxNumberOfClients;
            this.ClientCertificates               = ClientCertificates     ?? [];
            this.ClientCertificateChain           = ClientCertificateChain ?? [];
            this.TransmissionRetryDelay           = TransmissionRetryDelay ?? DefaultTransmissionRetryDelay;

            this.DefaultRequestBuilder            = DefaultRequestBuilder  ?? ((httpClient) => new HTTPRequest.Builder(httpClient) {
                                                                                                   Host               = HTTPHostname.Parse(IPAddress.ToString()),
                                                                                                   Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                                                   UserAgent          = httpClient.HTTPUserAgent,
                                                                                                   Connection         = ConnectionType.KeepAlive,
                                                                                                   CancellationToken  = CancellationToken.None
                                                                                               });

            this.httpClientFactory                = (description) => new HTTPClient(

                                                                         IPAddress,
                                                                         TCPPort,
                                                                         Description,
                                                                         this.HTTPUserAgent + description,
                                                                         Accept,
                                                                         ContentType,
                                                                         Connection,
                                                                         DefaultRequestBuilder,

                                                                         RemoteCertificateValidator,
                                                                         LocalCertificateSelector,
                                                                         ClientCertificates,
                                                                         ClientCertificateContext,
                                                                         ClientCertificateChain,
                                                                         TLSProtocols,
                                                                         CipherSuitesPolicy,
                                                                         CertificateChainPolicy,
                                                                         CertificateRevocationCheckMode,
                                                                         EnforceTLS,
                                                                         ApplicationProtocols,
                                                                         AllowRenegotiation,
                                                                         AllowTLSResume,
                                                                         TOTPConfig,

                                                                         HTTPAuthentication,

                                                                         PreferIPv4,
                                                                         ConnectTimeout,
                                                                         ReceiveTimeout,
                                                                         SendTimeout,
                                                                         TransmissionRetryDelay,
                                                                         MaxNumberOfRetries,
                                                                         BufferSize,

                                                                         ConsumeRequestChunkedTEImmediately,
                                                                         ConsumeResponseChunkedTEImmediately,

                                                                         DisableLogging

                                                                     );

            this.idleHTTPClients                  = Channel.CreateBounded<HTTPClient>(
                                                        new BoundedChannelOptions(this.MaxNumberOfClients) {
                                                            FullMode     = BoundedChannelFullMode.Wait,
                                                            SingleReader = false,
                                                            SingleWriter = false
                                                        }
                                                    );

            this.maxNumberOfHTTPClientsSemaphore  = new SemaphoreSlim(
                                                        this.MaxNumberOfClients,
                                                        this.MaxNumberOfClients
                                                    );

        }

        #endregion

        #region HTTPClientPool(URL, ...)

        public HTTPClientPool(URL                                                        URL,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificates                    = null,
                              SslStreamCertificateContext?                               ClientCertificateContext              = null,
                              SslProtocols?                                              TLSProtocols                          = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                           CertificateChainPolicy                = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode        = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificateChain                = null,
                              IEnumerable<SslApplicationProtocol>?                       ApplicationProtocols                  = null,
                              Boolean?                                                   AllowRenegotiation                    = null,
                              Boolean?                                                   AllowTLSResume                        = null,
                              TOTPConfig?                                                TOTPConfig                            = null,

                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              UInt16?                                                    MaxNumberOfClients                    = null,

                              IPVersionPreference?                                       PreferIPv4                            = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    BufferSize                            = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null,
                              IDNSClient?                                                DNSClient                             = null)
        {

            this.RemoteURL                        = URL;
            this.Id                               = URL.ToString();

            this.Description                      = Description            ?? I18NString.Empty;
            this.HTTPUserAgent                    = HTTPUserAgent          ?? DefaultHTTPUserAgent;
            this.MaxNumberOfClients               = MaxNumberOfClients     ?? DefaultMaxNumberOfClients;
            this.ClientCertificates               = ClientCertificates     ?? [];
            this.ClientCertificateChain           = ClientCertificateChain ?? [];
            this.TransmissionRetryDelay           = TransmissionRetryDelay ?? DefaultTransmissionRetryDelay;

            this.DefaultRequestBuilder            = DefaultRequestBuilder  ?? ((httpClient) => new HTTPRequest.Builder(httpClient) {
                                                                                                   Host               = URL.Hostname,
                                                                                                   Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                                                   UserAgent          = httpClient.HTTPUserAgent,
                                                                                                   Connection         = ConnectionType.KeepAlive,
                                                                                                   CancellationToken  = CancellationToken.None
                                                                                               });

            this.httpClientFactory                = (description) => new HTTPClient(

                                                                         URL,
                                                                         Description,
                                                                         this.HTTPUserAgent + description,
                                                                         Accept,
                                                                         ContentType,
                                                                         Connection,
                                                                         DefaultRequestBuilder,

                                                                         RemoteCertificateValidator,
                                                                         LocalCertificateSelector,
                                                                         ClientCertificates,
                                                                         ClientCertificateContext,
                                                                         ClientCertificateChain,
                                                                         TLSProtocols,
                                                                         CipherSuitesPolicy,
                                                                         CertificateChainPolicy,
                                                                         CertificateRevocationCheckMode,
                                                                         ApplicationProtocols,
                                                                         AllowRenegotiation,
                                                                         AllowTLSResume,
                                                                         TOTPConfig,

                                                                         HTTPAuthentication,

                                                                         PreferIPv4,
                                                                         ConnectTimeout,
                                                                         ReceiveTimeout,
                                                                         SendTimeout,
                                                                         TransmissionRetryDelay,
                                                                         MaxNumberOfRetries,
                                                                         BufferSize,

                                                                         ConsumeRequestChunkedTEImmediately,
                                                                         ConsumeResponseChunkedTEImmediately,

                                                                         DisableLogging,
                                                                         DNSClient

                                                                     );

            this.idleHTTPClients                  = Channel.CreateBounded<HTTPClient>(
                                                        new BoundedChannelOptions(this.MaxNumberOfClients) {
                                                            FullMode     = BoundedChannelFullMode.Wait,
                                                            SingleReader = false,
                                                            SingleWriter = false
                                                        }
                                                    );

            this.maxNumberOfHTTPClientsSemaphore  = new SemaphoreSlim(
                                                        this.MaxNumberOfClients,
                                                        this.MaxNumberOfClients
                                                    );

        }

        #endregion

        #region HTTPClientPool(DomainName, DNSService, ...)

        public HTTPClientPool(DomainName                                                 DomainName,
                              SRV_Spec                                                   DNSService,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificates                    = null,
                              SslStreamCertificateContext?                               ClientCertificateContext              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificateChain                = null,
                              SslProtocols?                                              TLSProtocols                          = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                           CertificateChainPolicy                = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode        = null,
                              Boolean?                                                   EnforceTLS                            = null,
                              IEnumerable<SslApplicationProtocol>?                       ApplicationProtocols                  = null,
                              Boolean?                                                   AllowRenegotiation                    = null,
                              Boolean?                                                   AllowTLSResume                        = null,
                              TOTPConfig?                                                TOTPConfig                            = null,

                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              UInt16?                                                    MaxNumberOfClients                    = null,

                              IPVersionPreference?                                       PreferIPv4                            = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    BufferSize                            = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null,
                              IDNSClient?                                                DNSClient                             = null)

        {

            this.DomainName                       = DomainName;
            this.DNSService                       = DNSService;
            this.Id                               = $"{DomainName} / {DNSService}";
            this.RemoteURL                        = URL.Parse(DomainName.ToString());

            this.Description                      = Description            ?? I18NString.Empty;
            this.HTTPUserAgent                    = HTTPUserAgent          ?? DefaultHTTPUserAgent;
            this.MaxNumberOfClients               = MaxNumberOfClients     ?? DefaultMaxNumberOfClients;
            this.ClientCertificates               = ClientCertificates     ?? [];
            this.ClientCertificateChain           = ClientCertificateChain ?? [];
            this.TransmissionRetryDelay           = TransmissionRetryDelay ?? DefaultTransmissionRetryDelay;

            this.DefaultRequestBuilder            = DefaultRequestBuilder  ?? ((httpClient) => new HTTPRequest.Builder(httpClient) {
                                                                                                   Host               = HTTPHostname.Parse(DomainName.FullName.TrimEnd('.')),
                                                                                                   Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                                                   UserAgent          = httpClient.HTTPUserAgent,
                                                                                                   Connection         = ConnectionType.KeepAlive,
                                                                                                   CancellationToken  = CancellationToken.None
                                                                                               });

            this.httpClientFactory                = (description) => new HTTPClient(

                                                                         DomainName,
                                                                         DNSService,
                                                                         Description,
                                                                         this.HTTPUserAgent + description,
                                                                         Accept,
                                                                         ContentType,
                                                                         Connection,
                                                                         DefaultRequestBuilder,

                                                                         RemoteCertificateValidator,
                                                                         LocalCertificateSelector,
                                                                         ClientCertificates,
                                                                         ClientCertificateContext,
                                                                         ClientCertificateChain,
                                                                         TLSProtocols,
                                                                         CipherSuitesPolicy,
                                                                         CertificateChainPolicy,
                                                                         CertificateRevocationCheckMode,
                                                                         EnforceTLS,
                                                                         ApplicationProtocols,
                                                                         AllowRenegotiation,
                                                                         AllowTLSResume,
                                                                         TOTPConfig,

                                                                         HTTPAuthentication,

                                                                         PreferIPv4,
                                                                         ConnectTimeout,
                                                                         ReceiveTimeout,
                                                                         SendTimeout,
                                                                         TransmissionRetryDelay,
                                                                         MaxNumberOfRetries,
                                                                         BufferSize,

                                                                         ConsumeRequestChunkedTEImmediately,
                                                                         ConsumeResponseChunkedTEImmediately,

                                                                         DisableLogging,
                                                                         DNSClient

                                                                     );

            this.idleHTTPClients                  = Channel.CreateBounded<HTTPClient>(
                                                        new BoundedChannelOptions(this.MaxNumberOfClients) {
                                                            FullMode     = BoundedChannelFullMode.Wait,
                                                            SingleReader = false,
                                                            SingleWriter = false
                                                        }
                                                    );

            this.maxNumberOfHTTPClientsSemaphore  = new SemaphoreSlim(
                                                        this.MaxNumberOfClients,
                                                        this.MaxNumberOfClients
                                                    );

        }

        #endregion

        #endregion


        #region (private) GetClientAsync(...)

        private async Task<HTTPClient> GetClientAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);

            await maxNumberOfHTTPClientsSemaphore.WaitAsync(CancellationToken).
                      ConfigureAwait(false);

            try
            {

                ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);

                if (idleHTTPClients.Reader.TryRead(out var httpClient))
                    return httpClient;

                var clientId = Interlocked.Increment(ref clientCounter);
                return httpClientFactory($" #{clientId}");

            }
            catch
            {
                maxNumberOfHTTPClientsSemaphore.Release();
                throw;
            }

        }

        #endregion

        #region (private) ReturnClientAsync(Client)

        private ValueTask ReturnClientAsync(HTTPClient Client)
        {

            try
            {

                if (Volatile.Read(ref isDisposed) != 0 /* || !client.IsReusable */)
                {
                    Client.Dispose();
                    return ValueTask.CompletedTask;
                }

                if (!idleHTTPClients.Writer.TryWrite(Client))
                    Client.Dispose();
            }
            finally
            {
                maxNumberOfHTTPClientsSemaphore.Release();
            }

            return ValueTask.CompletedTask;

        }

        #endregion


        #region RunRequest    (HTTPMethod, HTTPPath, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="Content">An optional HTTP content.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// 
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="ConsumeResponseChunkedTEImmediately">Whether to consume the response chunked transfer encoding immediately.</param>
        /// 
        /// <param name="RequestLogDelegate">An optional delegate to log the HTTP request.</param>
        /// <param name="ResponseLogDelegate">An optional delegate to log the HTTP response.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<HTTPResponse> RunRequest(HTTPMethod                    HTTPMethod,
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
                                                   CancellationToken             CancellationToken                     = default)
        {

            var httpClient = await GetClientAsync(CancellationToken);

            try
            {

                return await httpClient.RunRequest(
                                 HTTPMethod,
                                 HTTPPath,
                                 QueryString,
                                 Accept,
                                 Authentication,
                                 Content,
                                 ContentType,
                                 UserAgent,
                                 Connection,
                                 RequestBuilder,
                                 ConsumeRequestChunkedTEImmediately,
                                 ConsumeResponseChunkedTEImmediately,
                                 EventTrackingId,
                                 RequestTimeout,
                                 RequestLogDelegate,
                                 ResponseLogDelegate,
                                 CancellationToken
                             ).ConfigureAwait(false);

            }
            finally
            {
                await ReturnClientAsync(httpClient).
                          ConfigureAwait(false);
            }

        }

        #endregion

        #region CreateRequest (HTTPMethod, HTTPPath, ...)

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
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
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
                                                 CancellationToken             CancellationToken                    = default)
        {

            var host  = VirtualHostname?.  ToString() ??
                        RemoteURL.Hostname.ToString();

            if (RemoteURL.Port.HasValue &&
                RemoteURL.Port != IPPort.HTTP &&
                RemoteURL.Port != IPPort.HTTPS)
            {
                host += ":" + RemoteURL.Port.Value;
            }

            var requestBuilder = DefaultRequestBuilder(this);

            requestBuilder.Host                                       = HTTPHostname.Parse(host);
            requestBuilder.HTTPMethod                                 = HTTPMethod;
            requestBuilder.Path                                       = HTTPPath;
            requestBuilder.ConsumeChunkedTransferEncodingImmediately  = ConsumeRequestChunkedTEImmediately;
            requestBuilder.CancellationToken                          = CancellationToken;

            requestBuilder.QueryString                                = QueryString     ?? QueryString.Empty;
            requestBuilder.Accept                                     = Accept          ?? this.Accept ?? [];

            requestBuilder.Authorization                              = Authentication  ?? this.HTTPAuthentication;
            requestBuilder.UserAgent                                  = UserAgent       ?? this.HTTPUserAgent;
            requestBuilder.Content                                    = Content;
            requestBuilder.ContentType                                = ContentType     ?? this.ContentType;

            if (Content is not null && requestBuilder.ContentType is null)
                requestBuilder.ContentType                            = HTTPContentType.Application.OCTETSTREAM;

            requestBuilder.Connection                                 = Connection      ?? this.Connection;
            requestBuilder.TOTPConfig                                 = TOTPConfig      ?? this.TOTPConfig;
            requestBuilder.EventTrackingId                            = EventTrackingId;

            RequestBuilder?.Invoke(requestBuilder);

            return requestBuilder;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        //private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
        //                                 Func<TDelegate, Task>                              LogHandler,
        //                                 [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
        //                                 [CallerMemberName()]                       String  OICPCommand   = "")

        //    where TDelegate : Delegate

        //    => LogEvent(
        //           nameof(AHTTPTestClient),
        //           Logger,
        //           LogHandler,
        //           EventName,
        //           OICPCommand
        //       );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(HTTPClientPool)} ({MaxNumberOfClients} clients): {Id}";

        #endregion

        #region Dispose

        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref isDisposed, 1) != 0)
                return;

            idleHTTPClients.Writer.TryComplete();

            for (var i = 0; i < MaxNumberOfClients; i++)
                await maxNumberOfHTTPClientsSemaphore.WaitAsync().ConfigureAwait(false);

            while (idleHTTPClients.Reader.TryRead(out var client))
                await client.DisposeAsync().ConfigureAwait(false);

            maxNumberOfHTTPClientsSemaphore.Dispose();

        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        #endregion

    }

}
