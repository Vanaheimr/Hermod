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

#region Usings

using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class IHTTPClientExtensions
    {

        #region GET             (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP GET request.
        /// </summary>u
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

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken
               );

        #endregion

        #region GET_Text        (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>u
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
        /// </summary>u
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

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;

                if (text.IsNotNullOrEmpty())
                {

                    var json = JObject.Parse(text);

                    return new HTTPResponse<JObject>(
                               response,
                               json
                           );

                }

                return text.IsNotNullOrEmpty()
                           ? new HTTPResponse<JObject>(response, JObject.Parse(text))
                           : new HTTPResponse<JObject>(response, IsFault: true);

            }
            catch (Exception e)
            {
                return new HTTPResponse<JObject>(
                           response,
                           e
                       );
            }

        }

        #endregion

        #region GET_JSONArray   (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>u
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

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;

                if (text.IsNotNullOrEmpty())
                {

                    var json = JArray.Parse(text);

                    return new HTTPResponse<JArray>(
                               response,
                               json
                           );

                }

                return text.IsNotNullOrEmpty()
                           ? new HTTPResponse<JArray>(response, JArray.Parse(text))
                           : new HTTPResponse<JArray>(response, IsFault: true);

            }
            catch (Exception e)
            {
                return new HTTPResponse<JArray>(
                           response,
                           e
                       );
            }

        }

        #endregion


        #region POST            (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP POST request.
        /// </summary>u
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

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken
               );

        #endregion

        #region POST_Text       (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>u
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
        /// </summary>u
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

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;

                if (text.IsNotNullOrEmpty())
                {

                    var json = JObject.Parse(text);

                    return new HTTPResponse<JObject>(
                               response,
                               json
                           );

                }

                return text.IsNotNullOrEmpty()
                           ? new HTTPResponse<JObject>(response, JObject.Parse(text))
                           : new HTTPResponse<JObject>(response, IsFault: true);

            }
            catch (Exception e)
            {
                return new HTTPResponse<JObject>(
                           response,
                           e
                       );
            }

        }

        #endregion

        #region POST_JSONArray  (this HTTPClientPool, Path, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>u
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

                                      RequestLogDelegate,
                                      ResponseLogDelegate,
                                      CancellationToken
                                  );

            try
            {

                var text = response.HTTPBodyAsUTF8String;

                if (text.IsNotNullOrEmpty())
                {

                    var json = JArray.Parse(text);

                    return new HTTPResponse<JArray>(
                               response,
                               json
                           );

                }

                return text.IsNotNullOrEmpty()
                           ? new HTTPResponse<JArray>(response, JArray.Parse(text))
                           : new HTTPResponse<JArray>(response, IsFault: true);

            }
            catch (Exception e)
            {
                return new HTTPResponse<JArray>(
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

        private static UInt64 clientCounter = 0;

        private readonly  ConcurrentDictionary<UInt64, HTTPTestClient>  httpClientPool         = [];

        private readonly  SemaphoreSlim                                 MaxNumberOfClientsSemaphore;

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public  const     String                                        DefaultHTTPUserAgent   = "Hermod HTTP Client Pool";

        #endregion

        #region Properties

        public String Id {  get; }

        public Boolean                        IsHTTPConnected          { get; private set; } = false;

        public String?                        HTTPUserAgent            { get; }


        private Boolean isBusy;
        public Boolean                        IsBusy
            => isBusy;

        public TimeSpan                       MaxSemaphoreWaitTime     { get; set; }         = TimeSpan.FromSeconds(30);


        public const UInt16 DefaultMaxNumberOfClients = 5;

        public UInt16 MaxNumberOfClients { get; set; }


        private readonly Func<String, HTTPTestClient> httpClientBuilder;




        public DefaultRequestBuilderDelegate     DefaultRequestBuilder     { get;}
        //public DefaultRequestBuilder2Delegate    DefaultRequestBuilder     { get;}




        /// <summary>
        /// The description of this TCP client.
        /// </summary>
        public I18NString                        Description               { get; }



        public  URL?                             RemoteURL                 { get; }
        public  IIPAddress?                      RemoteIPAddress           { get; private   set; }
        public  IPPort?                          RemotePort                { get; protected set; }

        /// <summary>
        /// The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public  DomainName?                      DomainName                { get; }

        /// <summary>
        /// The DNS Service to lookup in order to resolve high available IP addresses and TCP ports.
        /// </summary>
        public  SRV_Spec?                        DNSService                { get; }






        URL IHTTPClient.RemoteURL => throw new NotImplementedException();

        public HTTPHostname? VirtualHostname => throw new NotImplementedException();

        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>? RemoteCertificateValidator => throw new NotImplementedException();

        public X509Certificate2? ClientCertificate => throw new NotImplementedException();

        public HTTPContentType? ContentType => throw new NotImplementedException();

        public AcceptTypes? Accept => throw new NotImplementedException();

        public IHTTPAuthentication? Authentication => throw new NotImplementedException();

        public ConnectionType? Connection => throw new NotImplementedException();

        public TimeSpan RequestTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TransmissionRetryDelayDelegate TransmissionRetryDelay => throw new NotImplementedException();

        public Boolean UseHTTPPipelining => throw new NotImplementedException();

        public HTTPClientLogger? HTTPLogger => throw new NotImplementedException();

        public Boolean Connected => throw new NotImplementedException();

        public SslProtocols TLSProtocols => throw new NotImplementedException();

        public Boolean PreferIPv4 => throw new NotImplementedException();

        public UInt16 MaxNumberOfRetries => throw new NotImplementedException();

        public IDNSClient? DNSClient => throw new NotImplementedException();

        public UInt64 KeepAliveMessageCount => throw new NotImplementedException();

        public IHTTPAuthentication? HTTPAuthentication { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TOTPConfig? TOTPConfig { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion

        #region Events

        //public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        //public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region HTTPClientPool(IPAddress, ...)

        public HTTPClientPool(IIPAddress                                                    IPAddress,
                              IPPort?                                                       TCPPort                               = null,
                              I18NString?                                                   Description                           = null,
                              String?                                                       HTTPUserAgent                         = null,
                              AcceptTypes?                                                  Accept                                = null,
                              HTTPContentType?                                              ContentType                           = null,
                              ConnectionType?                                               Connection                            = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidationHandler    = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificates                    = null,
                              SslStreamCertificateContext?                                  ClientCertificateContext              = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificateChain                = null,
                              SslProtocols?                                                 TLSProtocols                          = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                              CertificateChainPolicy                = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode        = null,
                              Boolean?                                                      EnforceTLS                            = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                  = null,
                              Boolean?                                                      AllowRenegotiation                    = null,
                              Boolean?                                                      AllowTLSResume                        = null,

                              UInt16?                                                       MaxNumberOfClients                    = null,

                              Boolean?                                                      PreferIPv4                            = null,
                              TimeSpan?                                                     ConnectTimeout                        = null,
                              TimeSpan?                                                     ReceiveTimeout                        = null,
                              TimeSpan?                                                     SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                              UInt16?                                                       MaxNumberOfRetries                    = null,
                              UInt32?                                                       BufferSize                            = null,

                              Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null)
        {

            this.RemotePort       = TCPPort;
            this.RemoteIPAddress  = IPAddress;


            this.Id                           = $"{IPAddress}:{TCPPort ?? IPPort.HTTPS}";

            this.Description                  = Description        ?? I18NString.Empty;

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.DefaultRequestBuilder        = DefaultRequestBuilder
                                                    ?? (() => new HTTPRequest.Builder() {
                                                                  Host               = HTTPHostname.Parse(IPAddress.ToString()),
                                                                  Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                  UserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                                  Connection         = ConnectionType.KeepAlive,
                                                                  CancellationToken  = CancellationToken.None
                                                              });

            this.httpClientBuilder            = (description) => {

                                                          return new HTTPTestClient(

                                                                     IPAddress,
                                                                     TCPPort,
                                                                     Description,
                                                                     HTTPUserAgent + description,
                                                                     Accept,
                                                                     ContentType,
                                                                     Connection,
                                                                     null, //DefaultRequestBuilder,

                                                                     RemoteCertificateValidationHandler,
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

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize,

                                                                     ConsumeRequestChunkedTEImmediately,
                                                                     ConsumeResponseChunkedTEImmediately

                                                                 ); };

        }

        #endregion

        #region HTTPClientPool(URL, DNSService = null, ...)

        public HTTPClientPool(URL                                                           URL,
                              I18NString?                                                   Description                           = null,
                              String?                                                       HTTPUserAgent                         = null,
                              AcceptTypes?                                                  Accept                                = null,
                              HTTPContentType?                                              ContentType                           = null,
                              ConnectionType?                                               Connection                            = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificates                    = null,
                              SslStreamCertificateContext?                                  ClientCertificateContext              = null,
                              SslProtocols?                                                 TLSProtocols                          = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                              CertificateChainPolicy                = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode        = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificateChain                = null,
                              //Boolean?                                                      EnforceTLS                            = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                  = null,
                              Boolean?                                                      AllowRenegotiation                    = null,
                              Boolean?                                                      AllowTLSResume                        = null,

                              UInt16?                                                       MaxNumberOfClients                    = null,

                              Boolean?                                                      PreferIPv4                            = null,
                              TimeSpan?                                                     ConnectTimeout                        = null,
                              TimeSpan?                                                     ReceiveTimeout                        = null,
                              TimeSpan?                                                     SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                              UInt16?                                                       MaxNumberOfRetries                    = null,
                              UInt32?                                                       BufferSize                            = null,

                              Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null,

                              IDNSClient?                                                   DNSClient                             = null)

        {

            this.RemoteURL   = URL;
            this.DNSService  = DNSService;


            this.Id                           = URL.ToString();

            this.Description                  = Description        ?? I18NString.Empty;

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.DefaultRequestBuilder        = DefaultRequestBuilder
                                                    ?? (() => new HTTPRequest.Builder() {
                                                                  Host               = URL.Hostname,
                                                                  Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                  UserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                                  Connection         = ConnectionType.KeepAlive,
                                                                  CancellationToken  = CancellationToken.None
                                                              });

            this.httpClientBuilder            = (description) => new HTTPTestClient(

                                                                     URL,
                                                                     Description,
                                                                     HTTPUserAgent + description,
                                                                     Accept,
                                                                     ContentType,
                                                                     Connection,
                                                                     this.DefaultRequestBuilder,

                                                                     RemoteCertificateValidator,
                                                                     LocalCertificateSelector,
                                                                     ClientCertificates,
                                                                     ClientCertificateContext,
                                                                     ClientCertificateChain,
                                                                     TLSProtocols,
                                                                     CipherSuitesPolicy,
                                                                     CertificateChainPolicy,
                                                                     CertificateRevocationCheckMode,
                                                                     //EnforceTLS,
                                                                     ApplicationProtocols,
                                                                     AllowRenegotiation,
                                                                     AllowTLSResume,

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize,

                                                                     ConsumeRequestChunkedTEImmediately,
                                                                     ConsumeResponseChunkedTEImmediately,

                                                                     DNSClient

                                                                 );

        }

        #endregion

        #region HTTPClientPool(DomainName, DNSService, ...)

        public HTTPClientPool(DomainName                                                    DomainName,
                              SRV_Spec                                                      DNSService,
                              I18NString?                                                   Description                           = null,
                              String?                                                       HTTPUserAgent                         = null,
                              AcceptTypes?                                                  Accept                                = null,
                              HTTPContentType?                                              ContentType                           = null,
                              ConnectionType?                                               Connection                            = null,
                              DefaultRequestBuilderDelegate?                                DefaultRequestBuilder                 = null,

                              RemoteTLSServerCertificateValidationHandler<HTTPTestClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                             LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificates                    = null,
                              SslStreamCertificateContext?                                  ClientCertificateContext              = null,
                              IEnumerable<X509Certificate2>?                                ClientCertificateChain                = null,
                              SslProtocols?                                                 TLSProtocols                          = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                              CertificateChainPolicy                = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode        = null,
                              Boolean?                                                      EnforceTLS                            = null,
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                  = null,
                              Boolean?                                                      AllowRenegotiation                    = null,
                              Boolean?                                                      AllowTLSResume                        = null,

                              UInt16?                                                       MaxNumberOfClients                    = null,

                              Boolean?                                                      PreferIPv4                            = null,
                              TimeSpan?                                                     ConnectTimeout                        = null,
                              TimeSpan?                                                     ReceiveTimeout                        = null,
                              TimeSpan?                                                     SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                              UInt16?                                                       MaxNumberOfRetries                    = null,
                              UInt32?                                                       BufferSize                            = null,

                              Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null,

                              IDNSClient?                                                   DNSClient                             = null)

        {

            this.DomainName  = DomainName;
            this.DNSService  = DNSService;


            this.Id                           = $"{DomainName} / {DNSService}";

            this.Description                  = Description        ?? I18NString.Empty;

            this.MaxNumberOfClients           = MaxNumberOfClients ?? DefaultMaxNumberOfClients;

            this.MaxNumberOfClientsSemaphore  = new SemaphoreSlim(
                                                    this.MaxNumberOfClients,
                                                    this.MaxNumberOfClients
                                                );

            this.DefaultRequestBuilder        = DefaultRequestBuilder
                                                    ?? (() => new HTTPRequest.Builder() {
                                                                  Host               = HTTPHostname.Parse(DomainName.FullName.TrimEnd('.')),
                                                                  Accept             = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                  UserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                                  Connection         = ConnectionType.KeepAlive,
                                                                  CancellationToken  = CancellationToken.None
                                                              });

            this.httpClientBuilder            = (description) => new HTTPTestClient(

                                                                     DomainName,
                                                                     DNSService,
                                                                     Description,
                                                                     HTTPUserAgent + description,
                                                                     Accept,
                                                                     ContentType,
                                                                     Connection,
                                                                     null, //DefaultRequestBuilder,

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

                                                                     PreferIPv4,
                                                                     ConnectTimeout,
                                                                     ReceiveTimeout,
                                                                     SendTimeout,
                                                                     TransmissionRetryDelay,
                                                                     MaxNumberOfRetries,
                                                                     BufferSize,

                                                                     ConsumeRequestChunkedTEImmediately,
                                                                     ConsumeResponseChunkedTEImmediately,

                                                                     null,
                                                                     DNSClient

                                                                 );

        }

        #endregion

        #endregion


        private HTTPTestClient? GetFreeConnection()
        {

            foreach (var httpClient in httpClientPool)
            {

                if (httpClient.Value.IsConnected     &&
                    httpClient.Value.IsHTTPConnected &&
                   !httpClient.Value.IsBusy)
                {
                    if (Interlocked.CompareExchange(ref httpClient.Value.IsBusy, true, false) == false)
                    {
                        return httpClient.Value;
                    }
                }

                if (!httpClient.Value.IsConnected)
                    httpClientPool.Remove(httpClient.Key, out _);

            }

            return null;

        }


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
                                                 QueryString?                  QueryString         = null,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 Byte[]?                       Content             = null,
                                                 HTTPContentType?              ContentType         = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
        {

            var requestBuilder = DefaultRequestBuilder();

            //requestBuilder.Host        = HTTPHostname.Localhost; // HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
            requestBuilder.Host        = HTTPHostname.Parse((RemoteURL?.Hostname.ToString() ?? DomainName?.ToString() ?? RemoteIPAddress?.ToString()) +
                                                     (RemoteURL?.Port.HasValue == true && RemoteURL.Value.Port != IPPort.HTTP && RemoteURL.Value.Port != IPPort.HTTPS
                                                          ? ":" + RemoteURL.Value.Port.ToString()
                                                          : String.Empty));
            requestBuilder.HTTPMethod  = HTTPMethod;
            requestBuilder.Path        = HTTPPath;

            if (QueryString    is not null)
                requestBuilder.QueryString    = QueryString;

            if (Accept         is not null)
                requestBuilder.Accept         = Accept;

            if (Authentication is not null)
                requestBuilder.Authorization  = Authentication;

            if (UserAgent.IsNotNullOrEmpty())
                requestBuilder.UserAgent      = UserAgent;

            if (Content        is not null)
                requestBuilder.Content        = Content;

            if (ContentType    is not null)
                requestBuilder.ContentType    = ContentType;

            if (Content is not null && requestBuilder.ContentType is null)
                requestBuilder.ContentType    = HTTPContentType.Application.OCTETSTREAM;

            if (Connection     is not null)
                requestBuilder.Connection     = Connection;

            requestBuilder.CancellationToken  = CancellationToken;

            RequestBuilder?.Invoke(requestBuilder);

            return requestBuilder;

        }

        #endregion

        #region RunRequest    (HTTPMethod, HTTPPath, ...)

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
        public async Task<HTTPResponse>

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

                       ClientRequestLogHandler?      RequestLogDelegate                    = null,
                       ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                       CancellationToken             CancellationToken                     = default)

        {

            // Only MaxNumberOfClients concurrent requests!
            await MaxNumberOfClientsSemaphore.WaitAsync(CancellationToken);

            try
            {

                var httpClient = GetFreeConnection();

                if (httpClient is null)
                {
                    if (httpClientPool.Count < MaxNumberOfClients)
                    {
                        Interlocked.Increment      (ref clientCounter);
                        httpClient = httpClientBuilder($"#{clientCounter}");
                        httpClientPool.TryAdd(clientCounter, httpClient);
                        Interlocked.CompareExchange(ref httpClient.IsBusy, true, false);
                    }
                    else
                    {
                        while ((httpClient = GetFreeConnection()) is null)
                        {
                            await Task.Delay(10, CancellationToken);
                        }
                    }
                }

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

                                 RequestLogDelegate,
                                 ResponseLogDelegate,
                                 CancellationToken

                             );

            }
            finally
            {
                MaxNumberOfClientsSemaphore.Release();
            }

            //return new HTTPResponse.Builder(Request) {
            //           HTTPStatusCode  = HTTPStatusCode.BadRequest,
            //           Content         = $"Could not acquire semaphore for {nameof(AHTTPTestClient)}.{nameof(SendRequest)}.".ToUTF8Bytes(),
            //           ContentType     = HTTPContentType.Text.PLAIN,
            //           Runtime         = TimeSpan.Zero
            //       };

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

            => $"{nameof(HTTPClientPool)} ({MaxNumberOfClients}): {Id}";

        #endregion

        #region Dispose()

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        #endregion

    }

}
