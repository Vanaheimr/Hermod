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

using System.Text;
using System.Buffers;
using System.Net.Sockets;
using System.Net.Security;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate HTTPRequest.Builder DefaultRequestBuilderDelegate(IHTTPClient HTTPClient);


    /// <summary>
    /// An abstract HTTP client.
    /// </summary>
    public abstract class AHTTPClient : ATLSClient,
                                        IHTTPClient,
                                        IAsyncDisposable
    {

        #region Data

        protected static readonly Byte[]                   endOfHTTPHeaderDelimiter         = Encoding.UTF8.GetBytes("\r\n\r\n");
        protected const           Byte                     endOfHTTPHeaderDelimiterLength   = 4;


        private readonly  SemaphoreSlim  sendRequestSemaphore = new (1, 1);
        private           CancellationTokenSource?  backgroundConnectionRenewalCTS;
        private           Task?                     backgroundConnectionRenewalTask;
        private           Int32                     backgroundConnectionRenewalGeneration;

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

        public Boolean                        IsHTTPConnected                        { get; private set; } = false;
        public DefaultRequestBuilderDelegate  DefaultRequestBuilder                  { get;}
        public UInt64                         KeepAliveMessageCount                  { get; private set; } = 0;

        public Boolean                        IsBusy;
        public TimeSpan                       MaxSemaphoreWaitTime                   { get; set; }         = TimeSpan.FromSeconds(30);

        public String?                        HTTPUserAgent                          { get; }
        public AcceptTypes?                   Accept                                 { get; }
        public HTTPContentType?               ContentType                            { get; }
        public IHTTPAuthentication?           HTTPAuthentication                     { get; set; }
        public TOTPConfig?                    TOTPConfig                             { get; set; }
        public ConnectionType?                Connection                             { get;}
        public TimeSpan                       RequestTimeout                         { get; set; }         = TimeSpan.FromSeconds(120);

        /// <summary>
        /// When set, the background renewal task tries to refresh the connection slightly before it reaches MaxConnectionLifetime.
        /// </summary>
        public TimeSpan                       BackgroundConnectionRenewalLeadTime    { get; set; }         = TimeSpan.FromSeconds(5);

        public Boolean?                       ConsumeRequestChunkedTEImmediately     { get;}
        public Boolean?                       ConsumeResponseChunkedTEImmediately    { get;}

        public HTTPClientLogger?              HTTPLogger                             { get; set; }






        public new RemoteTLSServerCertificateValidationHandler<IHTTPClient>? RemoteCertificateValidator
        {
            get
            {
                return base.RemoteCertificateValidator is not null
                           ? (a, b, c, d, e) => base.RemoteCertificateValidator.Invoke(a, b, c, (ATLSClient) d, e)
                           : null;
            }
        }

        public HTTPHostname? VirtualHostname { get; set; }

        public Boolean UseHTTPPipelining { get; set; }

        public Boolean Connected
            => IsHTTPConnected;

        #endregion

        #region Events

        public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region AHTTPClient(IPAddress, ...)

        protected AHTTPClient(IIPAddress                                                 IPAddress,
                              IPPort?                                                    TCPPort                               = null,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              String?                                                    TLSHostname                           = null,
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

                              IPVersionPreference?                                       IPVersionPreference                   = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    InternalBufferSize                    = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null,
                              ILogger<AHTTPClient>?                                      Logger                                = null,
                              ILoggerFactory?                                            LoggerFactory                         = null,
                              TimeSpan?                                                  MaxConnectionLifetime                 = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTP,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as IHTTPClient,
                                               policyErrors
                                           )
                       : null,
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

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   Logger,
                   LoggerFactory)

        {

            this.HTTPUserAgent                        = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.Accept                               = Accept;
            this.ContentType                          = ContentType;
            this.Connection                           = Connection;

            this.HTTPAuthentication                   = HTTPAuthentication;

            this.ConsumeRequestChunkedTEImmediately   = ConsumeRequestChunkedTEImmediately;
            this.ConsumeResponseChunkedTEImmediately  = ConsumeResponseChunkedTEImmediately;
            this.MaxConnectionLifetime                = MaxConnectionLifetime;

            this.DefaultRequestBuilder                = DefaultRequestBuilder
                                                            ?? ((httpClient) => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                                          Host                                       = TCPPort.HasValue
                                                                                                                           ? HTTPHostname.Parse(IPAddress.ToString(), TCPPort.Value)
                                                                                                                           : HTTPHostname.Parse(IPAddress.ToString()),
                                                                          Accept                                     = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                          UserAgent                                  = httpClient.HTTPUserAgent,
                                                                          ConsumeChunkedTransferEncodingImmediately  = ConsumeRequestChunkedTEImmediately,
                                                                          Connection                                 = ConnectionType.KeepAlive
                                                                      });

            this.TOTPConfig                           = TOTPConfig;

        }

        #endregion

        #region AHTTPClient(URL, ...)

        protected AHTTPClient(URL                                                        URL,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              String?                                                    TLSHostname                           = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator            = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificates                    = null,
                              SslStreamCertificateContext?                               ClientCertificateContext              = null,
                              IEnumerable<X509Certificate2>?                             ClientCertificateChain                = null,
                              SslProtocols?                                              TLSProtocols                          = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                    = null,
                              X509ChainPolicy?                                           CertificateChainPolicy                = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode        = null,
                              IEnumerable<SslApplicationProtocol>?                       ApplicationProtocols                  = null,
                              Boolean?                                                   AllowRenegotiation                    = null,
                              Boolean?                                                   AllowTLSResume                        = null,
                              TOTPConfig?                                                TOTPConfig                            = null,

                              IPVersionPreference?                                       IPVersionPreference                   = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    InternalBufferSize                    = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null,
                              IDNSClient?                                                DNSClient                             = null,
                              ILogger<AHTTPClient>?                                      Logger                                = null,
                              ILoggerFactory?                                            LoggerFactory                         = null,
                              TimeSpan?                                                  MaxConnectionLifetime                 = null)

            : base(URL,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as IHTTPClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   URL.Protocol == URLProtocols.https || URL.Protocol == URLProtocols.wss,//  EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.HTTPUserAgent                        = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.Accept                               = Accept;
            this.ContentType                          = ContentType;
            this.Connection                           = Connection;

            this.HTTPAuthentication                   = HTTPAuthentication;

            this.ConsumeRequestChunkedTEImmediately   = ConsumeRequestChunkedTEImmediately;
            this.ConsumeResponseChunkedTEImmediately  = ConsumeResponseChunkedTEImmediately;
            this.MaxConnectionLifetime                = MaxConnectionLifetime;

            this.DefaultRequestBuilder                = DefaultRequestBuilder
                                                            ?? ((httpClient) => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                                                    Host                                       = URL.Hostname,
                                                                                    Accept                                     = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                                    UserAgent                                  = httpClient.HTTPUserAgent,
                                                                                    ConsumeChunkedTransferEncodingImmediately  = ConsumeRequestChunkedTEImmediately,
                                                                                    Connection                                 = ConnectionType.KeepAlive
                                                                                });

            this.TOTPConfig                           = TOTPConfig;

        }

        #endregion

        #region AHTTPClient(DomainName, DNSService, ...)

        protected AHTTPClient(DomainName                                                 DomainName,
                              SRV_Spec                                                   DNSService,
                              I18NString?                                                Description                           = null,
                              String?                                                    HTTPUserAgent                         = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                    = null,
                              AcceptTypes?                                               Accept                                = null,
                              HTTPContentType?                                           ContentType                           = null,
                              ConnectionType?                                            Connection                            = null,
                              DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

                              String?                                                    TLSHostname                           = null,
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

                              IPVersionPreference?                                       IPVersionPreference                   = null,
                              TimeSpan?                                                  ConnectTimeout                        = null,
                              TimeSpan?                                                  ReceiveTimeout                        = null,
                              TimeSpan?                                                  SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                              UInt16?                                                    MaxNumberOfRetries                    = null,
                              UInt32?                                                    InternalBufferSize                    = null,

                              Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                              Boolean?                                                   DisableLogging                        = null,
                              IDNSClient?                                                DNSClient                             = null,
                              ILogger<AHTTPClient>?                                      Logger                                = null,
                              ILoggerFactory?                                            LoggerFactory                         = null,
                              TimeSpan?                                                  MaxConnectionLifetime                 = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as IHTTPClient,
                                               policyErrors
                                           )
                       : null,
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

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.HTTPUserAgent                        = HTTPUserAgent ?? DefaultHTTPUserAgent;

            this.Accept                               = Accept;
            this.ContentType                          = ContentType;
            this.Connection                           = Connection;

            this.HTTPAuthentication                   = HTTPAuthentication;

            this.ConsumeRequestChunkedTEImmediately   = ConsumeRequestChunkedTEImmediately;
            this.ConsumeResponseChunkedTEImmediately  = ConsumeResponseChunkedTEImmediately;
            this.MaxConnectionLifetime                = MaxConnectionLifetime;

            this.DefaultRequestBuilder                = DefaultRequestBuilder
                                                            ?? ((httpClient) => new HTTPRequest.Builder(this, CancellationToken.None) {
                                                                          Host                                       = HTTPHostname.Parse(DomainName.FullName.TrimEnd('.')),
                                                                          Accept                                     = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8),
                                                                          UserAgent                                  = httpClient.HTTPUserAgent,
                                                                          ConsumeChunkedTransferEncodingImmediately  = ConsumeRequestChunkedTEImmediately,
                                                                          Connection                                 = ConnectionType.KeepAlive
                                                                      });

            this.TOTPConfig                           = TOTPConfig;

        }

        #endregion

        #endregion


        #region ReconnectAsync()

        public override async Task<TCPConnectionResult>

            ReconnectAsync(CancellationToken CancellationToken = default)

        {

            try
            {

                clientCancellationTokenSource?.Cancel();

                StopBackgroundConnectionRenewal();

                try { httpStream?.Dispose(); } catch { }
                httpStream = null;

                IsHTTPConnected = false;

                var tcpConnectionResult = await base.ReconnectAsync(CancellationToken);

                if (tcpConnectionResult.IsFailure)
                    return tcpConnectionResult;

                httpStream = tcpClient?.GetStream();

                if (EnforceTLS ||
                    RemoteURL.Protocol.EnforcesTLS() == true)
                {

                    if (tlsStream is null || tlsStream.IsAuthenticated == false)
                        return TCPConnectionResult.Failed("TLS Authentication failed!");

                    httpStream = tlsStream;

                }

                IsHTTPConnected        = true;
                KeepAliveMessageCount  = 0;
                ScheduleBackgroundConnectionRenewal();

                return tcpConnectionResult;

            }
            catch (Exception e)
            {

                await Log(e.Message);

                if (e.StackTrace is not null)
                    await Log(e.StackTrace);

                return TCPConnectionResult.Failed("Reconnection failed!");

            }

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task<TCPConnectionResult>

            ConnectAsync(CancellationToken CancellationToken = default)

        {

            var response = await base.ConnectAsync(CancellationToken);

            if (!response.IsSuccess)
                return response;

            httpStream = tcpClient?.GetStream();

            if (EnforceTLS ||
                RemoteURL.Protocol.EnforcesTLS() == true)
            {

                if (tlsStream is null || tlsStream.IsAuthenticated == false)
                    return TCPConnectionResult.Failed("TLS Authentication failed!");

                httpStream = tlsStream;

            }

            IsHTTPConnected        = true;
            KeepAliveMessageCount  = 0;
            ScheduleBackgroundConnectionRenewal();

            return response;

        }

        #endregion

        #region Background connection renewal

        private void StopBackgroundConnectionRenewal()
        {

            Interlocked.Increment(ref backgroundConnectionRenewalGeneration);

            var cts = backgroundConnectionRenewalCTS;
            backgroundConnectionRenewalCTS = null;

            try { cts?.Cancel(); } catch { }

        }

        private void ScheduleBackgroundConnectionRenewal()
        {

            StopBackgroundConnectionRenewal();

            if (!MaxConnectionLifetime.HasValue ||
                !NextConnectionRenewalAt.HasValue ||
                !IsConnected ||
                !IsHTTPConnected)
            {
                return;
            }

            var cts         = new CancellationTokenSource();
            var generation  = Volatile.Read(ref backgroundConnectionRenewalGeneration);

            backgroundConnectionRenewalCTS  = cts;
            backgroundConnectionRenewalTask = Task.Run(
                async () => await RunBackgroundConnectionRenewal(generation, cts.Token).ConfigureAwait(false),
                CancellationToken.None
            );

        }

        private async Task RunBackgroundConnectionRenewal(Int32              Generation,
                                                          CancellationToken  CancellationToken)
        {

            while (!CancellationToken.IsCancellationRequested &&
                   Generation == Volatile.Read(ref backgroundConnectionRenewalGeneration))
            {

                var renewalAt = NextConnectionRenewalAt;
                if (!renewalAt.HasValue ||
                    !IsConnected ||
                    !IsHTTPConnected)
                {
                    return;
                }

                var dueAt = renewalAt.Value - BackgroundConnectionRenewalLeadTime;
                if (dueAt < Timestamp.Now)
                    dueAt = Timestamp.Now;

                var delay = dueAt - Timestamp.Now;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, CancellationToken).ConfigureAwait(false);

                if (CancellationToken.IsCancellationRequested ||
                    Generation != Volatile.Read(ref backgroundConnectionRenewalGeneration) ||
                    !IsConnected ||
                    !IsHTTPConnected)
                {
                    return;
                }

                if (!await sendRequestSemaphore.WaitAsync(0, CancellationToken).ConfigureAwait(false))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {

                    if (NextConnectionRenewalAt.HasValue &&
                        Timestamp.Now >= NextConnectionRenewalAt.Value - BackgroundConnectionRenewalLeadTime &&
                        IsConnected &&
                        IsHTTPConnected)
                    {

                        await ReconnectAsync(CancellationToken.None).ConfigureAwait(false);
                        return;

                    }

                }
                finally
                {
                    sendRequestSemaphore.Release();
                }

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
        /// <param name="Content">An optional HTTP body content.</param>
        /// <param name="ContentType">An optional HTTP content type header.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="EventTrackingId">An optional event tracking identifier to correlate this request with other events in the system.</param>
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

            var requestBuilder  = DefaultRequestBuilder(this);
            var requestBuilder2 = DefaultRequestBuilder(this);

            //requestBuilder.Host                                       = HTTPHostname.Localhost; // HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
            //requestBuilder.Host                                       = HTTPHostname.Parse((RemoteURL.Hostname.ToString() ?? DomainName?.ToString() ?? RemoteIPAddress?.ToString()) +
            //                                                                        (RemoteURL.Port.HasValue == true && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS
            //                                                                             ? ":" + RemoteURL.Port.ToString()
            //                                                                             : String.Empty));
            requestBuilder.HTTPMethod                                 = HTTPMethod;
            requestBuilder.Path                                       = HTTPPath;

            requestBuilder.ConsumeChunkedTransferEncodingImmediately  = ConsumeRequestChunkedTEImmediately;
            requestBuilder.CancellationToken                          = CancellationToken;

            requestBuilder.QueryString                                = QueryString    ??                            requestBuilder2.QueryString   ?? QueryString.Empty;
            requestBuilder.Accept                                     = Accept         ?? this.Accept             ?? requestBuilder2.Accept        ?? [];

            requestBuilder.Authorization                              = Authentication ?? this.HTTPAuthentication ?? requestBuilder2.Authorization;
            requestBuilder.UserAgent                                  = UserAgent      ?? this.HTTPUserAgent      ?? requestBuilder2.UserAgent;
            requestBuilder.Content                                    = Content;
            requestBuilder.ContentType                                = ContentType    ?? this.ContentType        ?? requestBuilder2.ContentType;

            if (Content is not null && requestBuilder.ContentType is null)
                requestBuilder.ContentType                            = HTTPContentType.Application.OCTETSTREAM;

            requestBuilder.Connection                                 = Connection     ?? this.Connection         ?? requestBuilder2.Connection;
            requestBuilder.TOTPConfig                                 = TOTPConfig     ?? this.TOTPConfig         ?? requestBuilder2.TOTPConfig;
            requestBuilder.EventTrackingId                            = EventTrackingId;

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
                       QueryString?                  QueryString                           = null,
                       AcceptTypes?                  Accept                                = null,
                       IHTTPAuthentication?          Authentication                        = null,
                       Byte[]?                       Content                               = null,
                       HTTPContentType?              ContentType                           = null,
                       String?                       UserAgent                             = null,
                       ConnectionType?               Connection                            = null,
                       UInt16?                       MaxNumberOfRetries                    = null,
                       Action<HTTPRequest.Builder>?  RequestBuilder                        = null,
                       Boolean?                      ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                      ConsumeResponseChunkedTEImmediately   = null,
                       EventTracking_Id?             EventTrackingId                       = null,
                       TimeSpan?                     RequestTimeout                        = null,

                       ClientRequestLogHandler?      RequestLogDelegate                    = null,
                       ClientResponseLogHandler?     ResponseLogDelegate                   = null,
                       CancellationToken             CancellationToken                     = default)

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
                           ConsumeRequestChunkedTEImmediately,
                           EventTrackingId,
                           CancellationToken
                       ).AsImmutable,
                       ConsumeResponseChunkedTEImmediately ?? this.ConsumeResponseChunkedTEImmediately,
                       RequestLogDelegate,
                       ResponseLogDelegate,
                       null, // MaxSemaphoreWaitTime
                       RequestTimeout,
                       MaxNumberOfRetries,
                       CancellationToken
                    );

        #endregion

        #region SendRequest   (Request)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        public async Task<HTTPResponse>

            SendRequest(HTTPRequest                Request,
                        Boolean?                   ConsumeResponseChunkedTEImmediately   = null,
                        ClientRequestLogHandler?   RequestLogDelegate                    = null,
                        ClientResponseLogHandler?  ResponseLogDelegate                   = null,
                        TimeSpan?                  MaxSemaphoreWaitTime                  = null,
                        //EventTracking_Id?          EventTrackingId                       = null,
                        TimeSpan?                  RequestTimeout                        = null,
                        UInt16?                    MaxNumberOfRetries                    = null,
                        CancellationToken          CancellationToken                     = default)

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

                    while (retry <= (MaxNumberOfRetries ?? this.MaxNumberOfRetries))
                    {

                        if (retry > 1)
                            DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)} {RemoteURL}, retry #{retry} of {MaxNumberOfRetries}...");

                        if (!IsConnected || !IsHTTPConnected || IsConnectionClosed || IsConnectionLifetimeExceeded)
                        {
                            try
                            {

                                var connectionResult = await ReconnectAsync(CancellationToken);

                                if (!connectionResult.IsSuccess)
                                {
                                    await Log($"Error in SendRequest: {connectionResult.Errors.AggregateWith(", ")}");
                                    DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)}: {connectionResult.Errors.AggregateWith(", ")}");
                                    IsHTTPConnected = false;
                                    retry++;
                                    continue;
                                }

                            }
                            catch (Exception ex)
                            {
                                await Log($"Error in SendRequest: {ex.Message}");
                                DebugX.LogException(ex, nameof(AHTTPClient) + "." + nameof(SendRequest));
                                IsHTTPConnected = false;
                                retry++;
                                continue;
                            }
                        }

                        if (httpStream is null)
                        {
                            await Log("HTTP stream is not available!");
                            DebugX.Log($"{nameof(AHTTPClient)}.{nameof(SendRequest)} HTTP stream is not available!");
                            IsHTTPConnected = false;
                            retry++;
                            continue;
                        }

                        var stopwatch = Stopwatch.StartNew();

                        try
                        {

                            using var requestTimeoutCancellationToken = new CancellationTokenSource(
                                                                          RequestTimeout ?? this.RequestTimeout
                                                                      );

                            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                                                                    clientCancellationTokenSource.Token,
                                                                    CancellationToken,
                                                                    requestTimeoutCancellationToken.Token
                                                                );

                            #region Set optional Time-Based One-Time Password (TOTP)

                            if (TOTPConfig is not null)
                                Request.TOTP = GenerateCurrentTOTP();

                            #endregion

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
                                  ).ConfigureAwait(false);

                            await LogEvent(
                                      RequestLogDelegate,
                                      async loggingDelegate => await loggingDelegate.Invoke(
                                          Timestamp.Now,
                                          this,
                                          Request
                                      ),
                                      nameof(SendRequest)
                                  ).ConfigureAwait(false);

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


                            IMemoryOwner<Byte>? bufferOwner = MemoryPool<Byte>.Shared.Rent((Int32) InternalBufferSize * 2);
                            var buffer = bufferOwner.Memory;
                            var dataLength = 0;

                            while (IsHTTPConnected)
                            {

                                #region Read data if no delimiter found yet

                                if (dataLength < endOfHTTPHeaderDelimiterLength ||
                                    buffer.Span[0..dataLength].IndexOf(endOfHTTPHeaderDelimiter.AsSpan()) < 0)
                                {

                                    if (dataLength > buffer.Length - InternalBufferSize)
                                        throw new Exception("Header too large.");

                                    var bytesRead = await httpStream.ReadAsync(
                                                              buffer.Slice(dataLength, (Int32) InternalBufferSize),
                                                              linkedCancellationToken.Token
                                                          );

                                    if (bytesRead == 0)
                                    {

                                        bufferOwner?.Dispose();

                                        await Log("Could not read HTTP response from the HTTP stream!");
                                        DebugX.Log($"{nameof(AHTTPClient)}.{nameof(SendRequest)} Could not read HTTP response from the HTTP stream!");
                                        await CloseHTTPConnectionAfterFailure().ConfigureAwait(false);
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
                                                   ConsumeResponseChunkedTEImmediately,
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

                                Stream? bodyDataStream  = null;
                                Stream? bodyStream      = null;
                                var bodyAlreadyConsumed  = false;

                                var prefix = buffer[..dataLength];
                                if (response.IsChunkedTransferEncoding || response.ContentLength.HasValue)
                                {

                                    bodyDataStream = new PrefixStream(
                                                         prefix,
                                                         httpStream,
                                                         LeaveInnerStreamOpen: true
                                                     );

                                    if (response.IsChunkedTransferEncoding)
                                    {

                                        var chunkedStream = new ChunkedTransferEncodingStream(
                                                                bodyDataStream,
                                                                LeaveInnerStreamOpen: true
                                                            );

                                        bodyStream = chunkedStream;

                                        if (response.ConsumeChunkedTransferEncodingImmediately == true)
                                        {

                                            var chunks         = new MemoryStream();

                                            var trailers       = await chunkedStream.ReadAllChunks(
                                                                           async (timestamp, elapsed, counter, data) => await chunks.WriteAsync(data),
                                                                           CancellationToken
                                             );

                                             response.HTTPBody  = chunks.ToArray();
                                             bodyAlreadyConsumed = true;

                                         }

                                    }

                                    else if (response.ContentLength.HasValue && response.ContentLength.Value > 0)
                                        bodyStream = new LengthLimitedStream(
                                                         bodyDataStream,
                                                         response.ContentLength.Value,
                                                         LeaveInnerStreamOpen: true
                                                     );

                                }

                                response.HTTPBodyStream = bodyStream;

                                #endregion

                                if (response.ContentType == HTTPContentType.Text.EVENTSTREAM)
                                {

                                    bodyStream?.Dispose();

                                    if (!ReferenceEquals(bodyStream, bodyDataStream))
                                        bodyDataStream?.Dispose();

                                    response.HTTPBodyStream = dataLength > 0
                                                                  ? new PrefixStream(
                                                                        prefix.ToArray(),
                                                                        httpStream,
                                                                        LeaveInnerStreamOpen: true
                                                                    )
                                                                  : httpStream;

                                    IsHTTPConnected = false;
                                    response.CloseActionAfterBodyWasRead = () => {
                                        try
                                        {
                                            Dispose();
                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogException(e, $"while disposing event stream client {RemoteSocket}!");
                                        }
                                    };

                                    bufferOwner?.Dispose();
                                    bufferOwner = null;

                                }
                                else
                                {

                                    try
                                    {

                                        if (bodyStream is not null)
                                        {

                                            if (!bodyAlreadyConsumed)
                                                response.HTTPBody = await ReadHTTPBodyStream(
                                                                        bodyStream,
                                                                        linkedCancellationToken.Token
                                                                    ).ConfigureAwait(false);

                                            response.HTTPBodyStream = null;

                                        }
                                        else if (!response.ContentLength.HasValue || response.ContentLength.Value == 0)
                                            response.HTTPBody = [];

                                    }
                                    finally
                                    {

                                        bodyStream?.Dispose();

                                        if (!ReferenceEquals(bodyStream, bodyDataStream))
                                            bodyDataStream?.Dispose();

                                        bufferOwner?.Dispose();
                                        bufferOwner = null;

                                    }

                                }

                                if (response.IsConnectionClose)
                                {

                                    IsHTTPConnected = false;

                                    if (response.ContentType == HTTPContentType.Text.EVENTSTREAM)
                                    {

                                        response.CloseActionAfterBodyWasRead ??= () => {
                                            try
                                            {
                                                Dispose();
                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.LogException(e, $"while disposing event stream client {RemoteSocket}!");
                                            }
                                        };

                                    }
                                    else
                                        await CloseHTTPConnectionAfterFailure().ConfigureAwait(false);

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
                                      ).ConfigureAwait(false);

                                await LogEvent(
                                          ResponseLogDelegate,
                                          async loggingDelegate => await loggingDelegate.Invoke(
                                              Timestamp.Now,
                                              this,
                                              Request,
                                              response
                                          ),
                                          nameof(SendRequest)
                                      ).ConfigureAwait(false);

                                #endregion

                                response.Runtime = stopwatch.Elapsed;

                                return response;

                            }

                        }
                        catch (Exception ex)
                        {

                            // Persistend HTTP connection was probably just closed...
                            if (retry > 1 || ex.InnerException is not SocketException)
                            {
                                await Log($"Error in SendRequest: {ex.Message}");
                                DebugX.LogException(ex, nameof(AHTTPClient) + "." + nameof(SendRequest));
                            }

                            await CloseHTTPConnectionAfterFailure().ConfigureAwait(false);
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
                                                     new JProperty("message",     $"Exception in {nameof(AHTTPClient)}.{nameof(SendRequest)}: {e.Message}"),
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

                        //while (Interlocked.CompareExchange(ref IsBusy, false, true) == false)
                        //{
                        //    DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)}: Waiting for IsBusy to be released...");
                        //    Thread.Sleep(1);
                        //}

                        // As not other thread should be writing to IsBusy at the same time!
                        Interlocked.Exchange(ref IsBusy, false);

                    }

                    sendRequestSemaphore.Release();

                }
            }

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                       Content         = $"Could not acquire semaphore for {nameof(AHTTPClient)}.{nameof(SendRequest)}.".ToUTF8Bytes(),
                       ContentType     = HTTPContentType.Text.PLAIN,
                       Runtime         = TimeSpan.Zero
                   };

        }

        #endregion


        #region (private)   ReadHTTPBodyStream(BodyStream, CancellationToken)

        private static async Task<Byte[]> ReadHTTPBodyStream(Stream             BodyStream,
                                                             CancellationToken  CancellationToken)
        {

            using var memoryStream = new MemoryStream();

            await BodyStream.CopyToAsync(
                      memoryStream,
                      CancellationToken
                  ).ConfigureAwait(false);

            return memoryStream.ToArray();

        }

        #endregion

        #region (private)   CloseHTTPConnectionAfterFailure()

        private async Task CloseHTTPConnectionAfterFailure()
        {

            IsHTTPConnected = false;
            StopBackgroundConnectionRenewal();

            // The next reconnect should fetch DNS upstream and refresh the DNS cache
            // instead of reusing a possibly stale cached load-balancer address.
            ForceDNSCacheUpdateOnNextConnect();

            try { httpStream?.Dispose(); } catch { }

            if (!ReferenceEquals(httpStream, tlsStream))
            {
                try { tlsStream?.Dispose(); } catch { }
            }

            httpStream = null;
            tlsStream  = null;

            await Close().ConfigureAwait(false);

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


        #region GenerateCurrentTOTP(TLSExporterMaterial = null)

        /// <summary>
        /// Generate the current Time-Based One-Time Password (TOTP).
        /// </summary>
        /// <param name="TLSExporterMaterial">Optional TLS Exporter Material.</param>
        public TOTPHTTPHeader? GenerateCurrentTOTP(Byte[]? TLSExporterMaterial = null)
        {

            if (TOTPConfig is null)
                return null;

            var (current, _, _)  = TOTPGenerator.GenerateTOTP(
                                       TOTPConfig.SharedSecret,
                                       TOTPConfig.ValidityTime,
                                       TOTPConfig.Length,
                                       TOTPConfig.Alphabet,
                                       null,
                                       TOTPConfig.UseTLSExporterMaterial == true
                                           ? TLSExporterMaterial
                                           : null
                                   );

            return new TOTPHTTPHeader(
                       TOTPConfig.UseTLSExporterMaterial == true
                           ? TOTPHTTPHeaderType.TLSChannelBinding
                           : TOTPHTTPHeaderType.RAW,
                       current
                   );

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(AHTTPClient),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region Dispose / IAsyncDisposable

        public override async ValueTask DisposeAsync()
        {

            StopBackgroundConnectionRenewal();

            try { httpStream?.Dispose(); } catch { }
            httpStream = null;

            try { tlsStream?.Dispose(); } catch { }
            tlsStream = null;

            await base.DisposeAsync().ConfigureAwait(false);

        }

        public override void Dispose()
        {

            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(HTTPClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion


    }

}
