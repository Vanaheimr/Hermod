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

using System.Text;
using System.Buffers;
using System.Diagnostics;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public delegate HTTPRequest.Builder DefaultRequestBuilderDelegate();
    //public delegate HTTPRequest.Builder DefaultRequestBuilder2Delegate(AHTTPTestClient HTTPClient);


    public static class HTTPTestClientExtensions
    {

        #region OPTIONS (Path, ...)

        public static Task<HTTPResponse> OPTIONS(this AHTTPTestClient          HTTPTestClient,
                                                 HTTPPath                      Path,
                                                 IHTTPAuthentication?          Authentication        = null,
                                                 ConnectionType?               Connection            = null,
                                                 TimeSpan?                     RequestTimeout        = null,
                                                 EventTracking_Id?             EventTrackingId       = null,
                                                 Byte                          NumberOfRetry         = 0,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder        = null,

                                                 ClientRequestLogHandler?      RequestLogDelegate    = null,
                                                 ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                                 CancellationToken             CancellationToken     = default)

            => HTTPTestClient.RunRequest(

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

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion


        #region GET     (Path, ...)

        public static Task<HTTPResponse> GET(this AHTTPTestClient          HTTPTestClient,
                                             HTTPPath                      Path,
                                             //Byte[]?                       Content               = null,
                                             //HTTPContentType?              ContentType           = null,
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

            => HTTPTestClient.RunRequest(

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

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

        #endregion


        #region POST    (Path, Content, ...)

        public static Task<HTTPResponse> POST(this AHTTPTestClient          HTTPTestClient,
                                              HTTPPath                      Path,
                                              Byte[]                        Content,
                                              HTTPContentType?              ContentType           = null,
                                              QueryString?                  QueryString           = null,
                                              AcceptTypes?                  Accept                = null,
                                              IHTTPAuthentication?          Authentication        = null,
                                              ConnectionType?               Connection            = null,
                                              TimeSpan?                     RequestTimeout        = null,
                                              EventTracking_Id?             EventTrackingId       = null,
                                              Byte                          NumberOfRetry         = 0,
                                              Action<HTTPRequest.Builder>?  RequestBuilder        = null,

                                              ClientRequestLogHandler?      RequestLogDelegate    = null,
                                              ClientResponseLogHandler?     ResponseLogDelegate   = null,
                                              CancellationToken             CancellationToken     = default)

            => HTTPTestClient.RunRequest(

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

                   RequestLogDelegate,
                   ResponseLogDelegate,
                   CancellationToken

               );

          //  => HTTPClientCommand.Execute(
          //         client => client.CreateRequest(
          //                       HTTPMethod.POST,
          //                       Path,
          //                       Content,
          //                       ContentType    ?? HTTPClientCommand.ContentType,
          //                       QueryString,
          //                       Accept         ?? HTTPClientCommand.Accept,
          //                       Authentication ?? HTTPClientCommand.Authentication,
          //                       UserAgent      ?? HTTPClientCommand.HTTPUserAgent,
          //                       Connection     ?? HTTPClientCommand.Connection,
          //                       RequestBuilder,
          //                       CancellationToken
          //                   ).SetContentLength(0), // Always send a Content-Length header, even when it's value is zero!
          //         RequestLogDelegate,
          //         ResponseLogDelegate,
          //         EventTrackingId,
          //         RequestTimeout,
          //         NumberOfRetry,
          //         CancellationToken
          //     );
        //{

        //    var requestBuilder = HTTPTestClient.DefaultRequestBuilder();

        //    requestBuilder.HTTPMethod         = HTTPMethod.POST;
        //    requestBuilder.Path               = Path;
        //    requestBuilder.Content            = Content;
        //    requestBuilder.CancellationToken  = CancellationToken;

        //    if (ContentType is not null)
        //        requestBuilder.ContentType      = ContentType;

        //    if (QueryString is not null)
        //        requestBuilder.QueryString      = QueryString;

        //    if (Accept is not null)
        //        requestBuilder.Accept           = Accept;

        //    if (Authentication is not null)
        //        requestBuilder.Authorization    = Authentication;

        //    if (Connection is not null)
        //        requestBuilder.Connection       = Connection;

        //    if (RequestTimeout is not null)
        //        requestBuilder.Timeout          = RequestTimeout.Value;

        //    if (EventTrackingId is not null)
        //        requestBuilder.EventTrackingId  = EventTrackingId;

        //    RequestBuilder?.Invoke(requestBuilder);


        //    var xx = await HTTPTestClient.SendRequest(
        //                       requestBuilder.AsImmutable,
        //                       RequestLogDelegate,
        //                       ResponseLogDelegate,
        //                       CancellationToken
        //                   );

        //    return xx.Item2;

        //}

        #endregion

    }


    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class AHTTPTestClient : ATLSTestClient,
                                            IHTTPClient,
                                            IAsyncDisposable
    {

        #region Data

        private readonly  SemaphoreSlim  sendRequestSemaphore = new (1, 1);

        /// <summary>
        /// The internal HTTP stream.
        /// </summary>
        protected         Stream?        httpStream;

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public  const     String         DefaultHTTPUserAgent  = "Hermod HTTP Test Client";

        #endregion

        #region Properties

        public Boolean                        IsHTTPConnected          { get; private set; } = false;

        public String?                        HTTPUserAgent            { get; }

        public DefaultRequestBuilderDelegate  DefaultRequestBuilder    { get;}


        public UInt64                         KeepAliveMessageCount    { get; private set; } = 0;

        public Boolean                        IsBusy;

        public TimeSpan                       MaxSemaphoreWaitTime     { get; set; }         = TimeSpan.FromSeconds(30);



        URL IHTTPClient.RemoteURL => throw new NotImplementedException();

        public HTTPHostname? VirtualHostname => throw new NotImplementedException();

        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>? RemoteCertificateValidator => throw new NotImplementedException();

        public X509Certificate2? ClientCertificate => throw new NotImplementedException();

        public HTTPContentType? ContentType => throw new NotImplementedException();

        public AcceptTypes? Accept => throw new NotImplementedException();

        public ConnectionType? Connection => throw new NotImplementedException();

        public TimeSpan RequestTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Boolean UseHTTPPipelining => throw new NotImplementedException();

        public HTTPClientLogger? HTTPLogger => throw new NotImplementedException();

        public Boolean Connected => throw new NotImplementedException();

        public IHTTPAuthentication? HTTPAuthentication { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TOTPConfig? TOTPConfig { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion

        #region Events

        public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region AHTTPTestClient(IPAddress, ...)

        protected AHTTPTestClient(IIPAddress                                                     IPAddress,
                                  IPPort?                                                        TCPPort                              = null,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,
                                  DefaultRequestBuilderDelegate?                                 DefaultRequestBuilder                = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  X509RevocationMode?                                            CertificateRevocationCheckMode       = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  Boolean?                                                       PreferIPv4                           = null,
                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  TransmissionRetryDelayDelegate?                                TransmissionRetryDelay               = null,
                                  UInt16?                                                        MaxNumberOfRetries                   = null,
                                  UInt32?                                                        BufferSize                           = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTP,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
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
                   BufferSize)

        {

            this.HTTPUserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.DefaultRequestBuilder  = DefaultRequestBuilder
                                              ?? (() => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                            Host        = HTTPHostname.Parse(IPAddress.ToString()),
                                                            Accept      = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                            UserAgent   = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                            Connection  = ConnectionType.KeepAlive
                                                        });

        }

        #endregion

        #region AHTTPTestClient(URL, DNSService = null, ...)

        protected AHTTPTestClient(URL                                                            URL,
                                  SRV_Spec?                                                      DNSService                           = null,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,
                                  DefaultRequestBuilderDelegate?                                 DefaultRequestBuilder                = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  X509RevocationMode?                                            CertificateRevocationCheckMode       = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  Boolean?                                                       PreferIPv4                           = null,
                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  TransmissionRetryDelayDelegate?                                TransmissionRetryDelay               = null,
                                  UInt16?                                                        MaxNumberOfRetries                   = null,
                                  UInt32?                                                        BufferSize                           = null,
                                  IDNSClient?                                                    DNSClient                            = null)

            : base(URL,
                   DNSService,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
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
                   DNSClient)

        {

            this.HTTPUserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.DefaultRequestBuilder  = DefaultRequestBuilder
                                              ?? (() => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                            Host        = URL.Hostname,
                                                            Accept      = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                            UserAgent   = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                            Connection  = ConnectionType.KeepAlive
                                                        });

        }

        #endregion

        #region AHTTPTestClient(DomainName, DNSService, ...)

        protected AHTTPTestClient(DomainName                                                     DomainName,
                                  SRV_Spec                                                       DNSService,
                                  I18NString?                                                    Description                          = null,
                                  String?                                                        HTTPUserAgent                        = null,
                                  DefaultRequestBuilderDelegate?                                 DefaultRequestBuilder                = null,

                                  RemoteTLSServerCertificateValidationHandler<AHTTPTestClient>?  RemoteCertificateValidationHandler   = null,
                                  LocalCertificateSelectionHandler?                              LocalCertificateSelector             = null,
                                  IEnumerable<X509Certificate>?                                  ClientCertificateChain               = null,
                                  SslProtocols?                                                  TLSProtocols                         = null,
                                  CipherSuitesPolicy?                                            CipherSuitesPolicy                   = null,
                                  X509ChainPolicy?                                               CertificateChainPolicy               = null,
                                  X509RevocationMode?                                            CertificateRevocationCheckMode       = null,
                                  Boolean?                                                       EnforceTLS                           = null,
                                  IEnumerable<SslApplicationProtocol>?                           ApplicationProtocols                 = null,
                                  Boolean?                                                       AllowRenegotiation                   = null,
                                  Boolean?                                                       AllowTLSResume                       = null,

                                  Boolean?                                                       PreferIPv4                           = null,
                                  TimeSpan?                                                      ConnectTimeout                       = null,
                                  TimeSpan?                                                      ReceiveTimeout                       = null,
                                  TimeSpan?                                                      SendTimeout                          = null,
                                  TransmissionRetryDelayDelegate?                                TransmissionRetryDelay               = null,
                                  UInt16?                                                        MaxNumberOfRetries                   = null,
                                  UInt32?                                                        BufferSize                           = null,
                                  IDNSClient?                                                    DNSClient                            = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
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
                   DNSClient)

        {

            this.HTTPUserAgent          = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.DefaultRequestBuilder  = DefaultRequestBuilder
                                              ?? (() => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                            Host        = HTTPHostname.Parse(DomainName.FullName.TrimEnd('.')),
                                                            Accept      = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                            UserAgent   = HTTPUserAgent ?? DefaultHTTPUserAgent,
                                                            Connection  = ConnectionType.KeepAlive
                                                        });

        }

        #endregion

        #endregion


        #region ReconnectAsync()

        public async Task<(Boolean, List<String>)> ReconnectAsync()
        {

            return await base.ReconnectAsync();

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task<(Boolean, List<String>)>

            ConnectAsync(CancellationToken CancellationToken = default)

        {

            var response = await base.ConnectAsync(CancellationToken);

            if (!response.Item1)
                return response;

            httpStream = tcpClient?.GetStream();

            if (EnforceTLS ||
                RemoteURL?.Protocol.EnforcesTLS() == true)
            {

                if (tlsStream is null || tlsStream.IsAuthenticated == false)
                    return (false, new List<string>() { "TLS Authentication failed!" });

                httpStream = tlsStream;

            }

            IsHTTPConnected        = true;
            KeepAliveMessageCount  = 0;

            return response;

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
        public Task<HTTPResponse>

            RunRequest(HTTPMethod                    HTTPMethod,
                       HTTPPath                      HTTPPath,
                       QueryString?                  QueryString           = null,
                       AcceptTypes?                  Accept                = null,
                       IHTTPAuthentication?          Authentication        = null,
                       Byte[]?                       Content               = null,
                       HTTPContentType?              ContentType           = null,
                       String?                       UserAgent             = null,
                       ConnectionType?               Connection            = null,
                       Action<HTTPRequest.Builder>?  RequestBuilder        = null,

                       ClientRequestLogHandler?      RequestLogDelegate    = null,
                       ClientResponseLogHandler?     ResponseLogDelegate   = null,
                       CancellationToken             CancellationToken     = default)

                => SendRequest(
                       CreateRequest(
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
                           CancellationToken
                       ).AsImmutable,
                       RequestLogDelegate,
                       ResponseLogDelegate,
                       null, // MaxSemaphoreWaitTime
                       CancellationToken
                   );

        #endregion

        #region SendRequest   (Request)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<HTTPResponse>

            SendRequest(HTTPRequest                Request,
                        ClientRequestLogHandler?   RequestLogDelegate     = null,
                        ClientResponseLogHandler?  ResponseLogDelegate    = null,
                        TimeSpan?                  MaxSemaphoreWaitTime   = null,
                        CancellationToken          CancellationToken      = default)

        {

            var success = await sendRequestSemaphore.WaitAsync(
                                    MaxSemaphoreWaitTime ?? this.MaxSemaphoreWaitTime,
                                    CancellationToken
                                );

            if (success)
            {
                try
                {

                    var retry = 1;

                    while (retry <= MaxNumberOfRetries)
                    {

                        if (retry > 1)
                            DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)} {RemoteURL?.ToString() ?? RemoteSocket?.ToString() ?? "?"}, retry #{retry} of {MaxNumberOfRetries}...");

                        if (!IsConnected || !IsHTTPConnected || IsConnectionClosed)
                        {
                            try
                            {

                                var connectionResult = await ReconnectAsync();

                                if (!connectionResult.Item1)
                                {
                                    await Log($"Error in SendRequest: {connectionResult.Item2.AggregateWith(", ")}");
                                    DebugX.LogT($"{nameof(AHTTPTestClient)}.{nameof(SendRequest)}: {connectionResult.Item2.AggregateWith(", ")}");
                                    IsHTTPConnected = false;
                                    retry++;
                                    continue;
                                }

                            }
                            catch (Exception ex)
                            {
                                await Log($"Error in SendRequest: {ex.Message}");
                                DebugX.LogException(ex, nameof(AHTTPTestClient) + "." + nameof(SendRequest));
                                IsHTTPConnected = false;
                                retry++;
                                continue;
                            }
                        }

                        if (httpStream is null)
                        {
                            await Log("HTTP stream is not available!");
                            DebugX.Log($"{nameof(AHTTPTestClient)}.{nameof(SendRequest)} HTTP stream is not available!");
                            IsHTTPConnected = false;
                            retry++;
                            continue;
                        }

                        var stopwatch = Stopwatch.StartNew();

                        try
                        {

                            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                                                                    clientCancellationTokenSource.Token,
                                                                    CancellationToken
                                                                );

                            #region Log  HTTP Request

                            if (LocalSocket. HasValue) {
                                Request.LocalSocket   = LocalSocket.Value;
                                Request.HTTPSource    = new HTTPSource(LocalSocket.Value);
                            }

                            if (RemoteSocket.HasValue)
                                Request.RemoteSocket  = RemoteSocket.Value;

                            Request.HTTPClient      ??= this;
                            KeepAliveMessageCount++;

                            await LogEvent(
                                      ClientRequestLogDelegate,
                                      async loggingDelegate => await loggingDelegate.Invoke(
                                          Timestamp.Now,
                                          this,
                                          Request
                                      ),
                                      nameof(SendRequest)
                                  );

                            await LogEvent(
                                      RequestLogDelegate,
                                      async loggingDelegate => await loggingDelegate.Invoke(
                                          Timestamp.Now,
                                          this,
                                          Request
                                      ),
                                      nameof(SendRequest)
                                  );

                            #endregion

                            #region Send HTTP Request

                            await httpStream.WriteAsync(
                                      Encoding.UTF8.GetBytes(Request.EntireRequestHeader + "\r\n\r\n"),
                                      linkedCancellationToken.Token
                                  ).ConfigureAwait(false);

                            if (Request.HTTPBody is not null && Request.ContentLength > 0)
                                await httpStream.WriteAsync(
                                          Request.HTTPBody,
                                          linkedCancellationToken.Token
                                      ).ConfigureAwait(false);

                            await httpStream.FlushAsync(
                                      linkedCancellationToken.Token
                                  ).ConfigureAwait(false);

                            #endregion

                            IMemoryOwner<Byte>? bufferOwner = MemoryPool<Byte>.Shared.Rent((Int32) BufferSize * 2);
                            var buffer = bufferOwner.Memory;
                            var dataLength = 0;

                            while (IsHTTPConnected)
                            {

                                #region Read data if no delimiter found yet

                                if (dataLength < endOfHTTPHeaderDelimiterLength ||
                                    buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                                {

                                    if (dataLength > buffer.Length - BufferSize)
                                        throw new Exception("Header too large.");

                                    var bytesRead = await httpStream.ReadAsync(
                                                              buffer.Slice(dataLength, (Int32) BufferSize),
                                                              linkedCancellationToken.Token
                                                          );

                                    if (bytesRead == 0)
                                    {

                                        bufferOwner?.Dispose();

                                        await Log("Could not read HTTP response from the HTTP stream!");
                                        DebugX.Log($"{nameof(AHTTPTestClient)}.{nameof(SendRequest)} Could not read HTTP response from the HTTP stream!");
                                        IsHTTPConnected = false;
                                        retry++;
                                        continue;

                                    }

                                    dataLength += bytesRead;
                                    continue;

                                }

                                #endregion

                                #region Search for End-of-HTTPHeader

                                var endOfHTTPHeaderIndex = buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan());
                                if (endOfHTTPHeaderIndex < 0)
                                    continue;  // Should not reach here due to the if-condition above.

                                #endregion

                                #region Parse HTTP Response

                                var response = HTTPResponse.Parse(
                                                   Timestamp.Now,
                                                   stopwatch.Elapsed,
                                                   Request,
                                                   this,
                                                   LocalSocket!. Value,
                                                   RemoteSocket!.Value,
                                                   new HTTPSource(LocalSocket!.Value),
                                                   Encoding.UTF8.GetString(buffer[..endOfHTTPHeaderIndex].Span),
                                                   CancellationToken: Request.CancellationToken
                                               );

                                response.HTTPClient ??= this;

                                #endregion

                                #region Shift remaining data

                                var remainingStart  = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                                var remainingLength = dataLength - remainingStart;
                                buffer.Slice(remainingStart, remainingLength).CopyTo(buffer[..]);
                                dataLength = remainingLength;

                                #endregion

                                #region Setup HTTP body stream

                                Stream? bodyDataStream = null;
                                Stream? bodyStream = null;

                                var prefix = buffer[..dataLength];
                                if (response.IsChunkedTransferEncoding || response.ContentLength.HasValue)
                                {

                                    bodyDataStream = new PrefixStream(
                                                         prefix,
                                                         httpStream,
                                                         LeaveInnerStreamOpen: true
                                                     );

                                    if (response.IsChunkedTransferEncoding)
                                        bodyStream = new ChunkedTransferEncodingStream(
                                                         bodyDataStream,
                                                         LeaveInnerStreamOpen: true
                                                     );
                                    else if (response.ContentLength.HasValue && response.ContentLength.Value > 0)
                                        bodyStream = new LengthLimitedStream(
                                                         bodyDataStream,
                                                         response.ContentLength.Value,
                                                         LeaveInnerStreamOpen: true
                                                     );

                                }

                                response.HTTPBodyStream = bodyStream;
                             //   response.BufferOwner    = bufferOwner;  // Transfer ownership to response for disposal after body is consumed.

                                #endregion

                                if (response.IsConnectionClose)
                                {

                                    // An optional close action after the HTTP body stream has been read!
                                    response.CloseActionAfterBodyWasRead = () => {
                                        try
                                        {
                                            httpStream.Close();
                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogException(e, $"while closing {RemoteSocket}!");
                                        }
                                    };

                                    // Mark connection for closure after response handling!
                                    IsHTTPConnected = false;

                                }

                                #region Log HTTP Response

                                await LogEvent(
                                          ClientResponseLogDelegate,
                                          async loggingDelegate => await loggingDelegate.Invoke(
                                              Timestamp.Now,
                                              this,
                                              Request,
                                              response
                                          ),
                                          nameof(SendRequest)
                                      );

                                await LogEvent(
                                          ResponseLogDelegate,
                                          async loggingDelegate => await loggingDelegate.Invoke(
                                              Timestamp.Now,
                                              this,
                                              Request,
                                              response
                                          ),
                                          nameof(SendRequest)
                                      );

                                #endregion

                                response.Runtime = stopwatch.Elapsed;

                                return response;

                            }

                        }
                        catch (Exception ex)
                        {
                            await Log($"Error in SendRequest: {ex.Message}");
                            DebugX.LogException(ex, nameof(AHTTPTestClient) + "." + nameof(SendRequest));
                            IsHTTPConnected = false;
                            retry++;
                        }
                        finally
                        {
                            stopwatch.Stop();
                        }

                    }

                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               Content         = "Maximum HTTP retries reached!".ToUTF8Bytes(),
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Runtime         = TimeSpan.Zero
                           };

                }
                catch (Exception e)
                {

                    DebugX.LogT(e.Message);

                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               Content         = JSONObject.Create(
                                                     new JProperty("message",     $"Exception in {nameof(AHTTPTestClient)}.{nameof(SendRequest)}: {e.Message}"),
                                                     new JProperty("exception",   e.Message),
                                                     new JProperty("stackTrace",  e.StackTrace)
                                                 ).ToUTF8Bytes(),
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Runtime         = TimeSpan.Zero
                           };

                }
                finally
                {

                    // Was "booked" by a connection pool...
                    if (IsBusy)
                    {
                        while (Interlocked.CompareExchange(ref IsBusy, false, true) == false)
                        {
                            DebugX.LogT($"{nameof(AHTTPTestClient)}.{nameof(SendRequest)}: Waiting for IsBusy to be released...");
                            Thread.Sleep(1);
                        }
                    }

                    sendRequestSemaphore.Release();

                }
            }

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                       Content         = $"Could not acquire semaphore for {nameof(AHTTPTestClient)}.{nameof(SendRequest)}.".ToUTF8Bytes(),
                       ContentType     = HTTPContentType.Text.PLAIN,
                       Runtime         = TimeSpan.Zero
                   };

        }

        #endregion

        #region SendText      (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            if (!IsConnected || tcpClient is null)
                return (false, "", "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch = Stopwatch.StartNew();
                var stream = tcpClient.GetStream();
                clientCancellationTokenSource ??= new CancellationTokenSource();

                // Send the data
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Text), clientCancellationTokenSource.Token).ConfigureAwait(false);
                await stream.FlushAsync(clientCancellationTokenSource.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer = new Byte[8192];
                var bytesRead = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, clientCancellationTokenSource.Token).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), clientCancellationTokenSource.Token).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return (true, Encoding.UTF8.GetString(responseStream.ToArray()), null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, "", ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(AHTTPTestClient),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(HTTPTestClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion


    }

}
