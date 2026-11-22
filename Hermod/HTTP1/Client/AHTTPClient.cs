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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
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

        /// <summary>
        /// How many interim responses one response keeps. Section 15.2 of
        /// RFC 9110 sets no limit, and a server could send them without end
        /// until the request times out; the ones after these are still read and
        /// skipped, but not kept.
        /// </summary>
        public    const           Byte                     MaxInformationalResponses        = 16;


        private readonly  SemaphoreSlim  sendRequestSemaphore = new (1, 1);

        private readonly  Object                    authenticatorLock = new ();
        private           HTTPClientAuthenticator?  authenticator;
        private           HTTPClientCredentials?    authenticatorCredentials;
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

        /// <summary>
        /// Connecting to an https:// URL means TCP and TLS, so the handshake gets the
        /// connect timeout too, as with .NET's own SocketsHttpHandler.ConnectTimeout.
        /// RequestTimeout starts only after the connection is up, and nothing else ended
        /// a handshake that the server never answered: SendRequest waited for as long
        /// as its caller's token allowed, and the background renewal, which passes none,
        /// for good.
        /// </summary>
        protected override TimeSpan?          TLSHandshakeTimeout
            => ConnectTimeout;

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

        public HTTPHostname?  VirtualHostname      { get; set; }

        public Boolean        UseHTTPPipelining    { get; set; }

        /// <summary>
        /// Advertise "Accept-Encoding: br, gzip, deflate" on requests that do not
        /// already carry one, and undo the content coding of the responses that
        /// come back (RFC 9110, Section 8.4), so that the caller reads the
        /// representation rather than the octets that carried it.
        ///
        /// Off by default. It changes what goes out on the wire and what the caller
        /// gets back, which makes it the caller's decision and not ours — and it is
        /// off for the same reason in the HTTP/2 client, where it is called the same
        /// thing.
        /// </summary>
        public Boolean        AutomaticDecompression    { get; set; }

        /// <summary>
        /// The ceiling on a decoded response body, enforced *during* decompression
        /// and at every step of a multi-coding chain. A few kilobytes of gzip can
        /// expand to gigabytes, so a client that decodes automatically needs a bound
        /// exactly as much as a server that buffers request bodies does.
        /// </summary>
        public UInt64         MaxDecodedBodySize        { get; set; } = AHTTPPDU.DefaultMaxDecodedBodySize;

        /// <summary>
        /// Ask for an RFC 9530 Content-Digest on requests that do not already
        /// carry a Want-Content-Digest, and check the one a response brings, in
        /// its header or its trailer section, against the octets as they arrived:
        /// before <see cref="AutomaticDecompression"/>, which reads such a body
        /// first and decodes it afterwards. The outcome is on
        /// HTTPResponse.DigestVerification, where "there was none" stays apart
        /// from "it matched". Content that does not match is not handed on: the
        /// caller gets a ClientError with DigestVerification Mismatch instead,
        /// as the HTTP/2 client throws an HTTPDigestMismatchException.
        ///
        /// Off by default, as on the HTTP/2 client: it puts a field on the wire,
        /// and can turn a delivered response into an error.
        /// </summary>
        public Boolean        VerifyDigests             { get; set; }

        /// <summary>
        /// Credentials to answer a 401 (Unauthorized) with (RFC 9110, Section 11):
        /// the request is sent once more, carrying an Authorization field built
        /// from the server's WWW-Authenticate challenge, Digest preferred, then
        /// Bearer, then Token, then Basic. Null, the default, leaves the 401 to the
        /// caller, as the HTTP/2 client does with its option of the same name.
        ///
        /// Nothing is sent preemptively: credentials go out in answer to a
        /// challenge, once, and to the origin that issued it. A request that
        /// already carries an Authorization field is not answered for, since the
        /// caller has chosen its credentials, and neither is one whose body a
        /// chunk worker writes, since that body cannot be sent a second time.
        /// </summary>
        public HTTPClientCredentials?  Credentials      { get; set; }

        /// <summary>
        /// How many redirections to follow (RFC 9110, Section 15.4). Zero, the
        /// default, hands the 3xx to the caller.
        ///
        /// 301, 302, 303, 307 and 308 are followed, with the method and the body
        /// rewritten as HTTPRedirect says. Only while the target stays on this
        /// client's origin: an AHTTPClient is one connection to one origin, and
        /// a Location elsewhere is handed back unfollowed, with the 3xx intact,
        /// as the HTTP/2 client does. That boundary is also what keeps
        /// <see cref="Credentials"/> safe, since no hop can carry an
        /// Authorization field to an origin that did not ask for it. The targets
        /// followed are on HTTPResponse.RedirectChain.
        /// </summary>
        public Byte                    MaxRedirects     { get; set; }

        /// <summary>
        /// An RFC 9111 cache for this client's requests, or null, the default,
        /// for none, as before. Each single request goes through it, the ones a
        /// challenge or a redirection adds included: a GET is answered from it
        /// where its rules allow, and a successful unsafe request invalidates
        /// what it holds for that target. One cache may serve several clients.
        /// See <see cref="HTTPClientCache"/> for what it leaves out.
        ///
        /// Off by default, as on the HTTP/2 client, where the cache is a class
        /// of its own: a response from a cache is not the origin's answer of
        /// this moment, and a caller who did not ask for one would not expect it.
        /// </summary>
        public HTTPClientCache?        Cache            { get; set; }

        public Boolean        Connected
            => IsHTTPConnected;

        #endregion

        #region Events

        public event ClientRequestLogHandler?   ClientRequestLogDelegate;
        public event ClientResponseLogHandler?  ClientResponseLogDelegate;

        #endregion

        #region Constructor(s)

        #region AHTTPClient(IPAddress, ...)

        protected AHTTPClient(IPAddress                                                 IPAddress,
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

            // The validator as it was given: the base was handed a delegate around it.
            TakeKeyPinsOf(RemoteCertificateValidator);

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
                                                                          Host                                       = HTTPHostname.From(URLHost.From(IPAddress), TCPPort),
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
                   URL.Scheme == URIScheme.https || URL.Scheme == URIScheme.wss,//  EnforceTLS,
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

            // The validator as it was given: the base was handed a delegate around it.
            TakeKeyPinsOf(RemoteCertificateValidator);

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
                                                                                    Host                                       = URL.HostHeader,
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

            // The validator as it was given: the base was handed a delegate around it.
            TakeKeyPinsOf(RemoteCertificateValidator);

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

                // The connection's token, not the client's - see
                // ATCPClient.CycleConnectionToken. SendRequest's retry loop
                // calls this from inside a request whose token derives from the
                // client's, so cancelling that one here ended the request that
                // asked for the reconnect, left the next two retries starting
                // from an already-cancelled token, and ended in the response the
                // loop fabricates when it runs out - back then an "HTTP 400 -
                // Maximum HTTP retries reached!".
                CycleConnectionToken();

                StopBackgroundConnectionRenewal();

                try { httpStream?.Dispose(); } catch { }
                httpStream = null;

                IsHTTPConnected = false;

                var tcpConnectionResult = await base.ReconnectAsync(CancellationToken);

                if (tcpConnectionResult.IsFailure)
                    return tcpConnectionResult;

                httpStream = tcpClient?.GetStream();

                if (EnforceTLS ||
                    RemoteURL.Scheme?.EnforcesTLS == true)
                {

                    if (TLSStream is null || TLSSession is null)
                        return TCPConnectionResult.Failed("TLS Authentication failed!");

                    httpStream = TLSStream;

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
                RemoteURL.Scheme?.EnforcesTLS == true)
            {

                if (TLSStream is null || TLSSession is null)
                    return TCPConnectionResult.Failed("TLS Authentication failed!");

                httpStream = TLSStream;

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

            // RFC 9110, Section 12.5.3: ask for the codings we can undo — but never
            // over a caller's own Accept-Encoding, which may be deliberately
            // narrower, or an explicit "identity" switching compression off for one
            // request.
            if (AutomaticDecompression && requestBuilder.AcceptEncoding is null)
                requestBuilder.AcceptEncoding                         = HTTPContentCoding.AcceptEncoding;

            // RFC 9530, Section 4: likewise never over a caller's own preference.
            if (VerifyDigests && requestBuilder.WantContentDigest is null)
                requestBuilder.WantContentDigest                      = HTTPDigest.Want;

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
        ///
        /// With <see cref="Credentials"/> a 401 is answered once, and with
        /// <see cref="MaxRedirects"/> a redirection on this origin is followed;
        /// each hop is answered by the same 401 logic, so a redirect into an
        /// authenticated area still works. Without either this is one request.
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

            Task<HTTPResponse> SendOne(HTTPRequest OneRequest)

                => SendOneRequest(
                       OneRequest,
                       ConsumeResponseChunkedTEImmediately,
                       RequestLogDelegate,
                       ResponseLogDelegate,
                       MaxSemaphoreWaitTime,
                       RequestTimeout,
                       MaxNumberOfRetries,
                       CancellationToken
                   );

            var cache = Cache;

            Task<HTTPResponse> Send(HTTPRequest OneRequest)

                => cache is null
                       ? SendOne(OneRequest)
                       : cache.SendAsync(OneRequest, CacheOrigin(OneRequest), SendOne);

            return await SendAnsweringAndFollowing(Request, Send, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region DownloadAsync (Path, Destination, ...)

        /// <summary>
        /// GET a representation into the destination, and when its body breaks
        /// off, ask for the rest with Range (RFC 9110, Section 14) instead of
        /// starting over.
        ///
        /// The body goes to the destination as it arrives rather than into
        /// HTTPBody: SendRequest keeps a body only once it is whole, and a body
        /// thrown away when it breaks off is nothing to resume from.
        ///
        /// A resume needs a validator to guard it, a strong entity-tag or else a
        /// Last-Modified date, and every resume carries it in If-Range (Section
        /// 13.1.5). The server then decides whether the two parts are one
        /// representation: a 206 that starts where the destination stops is
        /// appended, and a 200, or a 206 that starts anywhere else, means the
        /// representation changed and the destination starts over. A weak
        /// entity-tag does not qualify: equivalent is not the same octets.
        ///
        /// Accept-Encoding is identity unless RequestBuilder says otherwise,
        /// since ranges of a compressed representation would splice compressed
        /// parts. With VerifyDigests the request asks for a Repr-Digest, and the
        /// octets of every part are checked against it together, since no single
        /// range's content digest says anything about the whole.
        ///
        /// Credentials and MaxRedirects apply as they do to SendRequest. Nothing
        /// but the body of a 200 or 206 is written to the destination, so an error
        /// page cannot pass for the representation.
        /// </summary>
        /// <param name="Path">The path of the representation.</param>
        /// <param name="Destination">Where its octets go, from its current position on. It must seek for a restart.</param>
        /// <param name="QueryString">An optional query string.</param>
        /// <param name="RequestBuilder">An optional delegate to change each request, applied before Range and If-Range are set.</param>
        /// <param name="MaxAttempts">How many requests in all, the first one included.</param>
        /// <param name="RequestTimeout">An optional timeout for each request.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<HTTPDownloadResult> DownloadAsync(HTTPPath                      Path,
                                                            Stream                        Destination,
                                                            QueryString?                  QueryString         = null,
                                                            Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                            Byte                          MaxAttempts         = 3,
                                                            TimeSpan?                     RequestTimeout      = null,
                                                            CancellationToken             CancellationToken   = default)
        {

            var startPosition   = Destination.CanSeek ? Destination.Position : 0L;

            UInt64  written     = 0;
            Byte    attempts    = 0,
                    resumes     = 0,
                    restarts    = 0;
            String? validator   = null;
            String? notWritten  = null;

            HTTPResponse? representation = null;

            // RFC 9530, Section 3: the digest of the whole representation, and the
            // running hash of everything written to check it against at the end.
            IncrementalHash?        hash          = null;
            String?                 algorithm     = null;
            Byte[]?                 expected      = null;
            HTTPDigestVerification  verification  = HTTPDigestVerification.NotPresent;

            HTTPDownloadResult Result(HTTPStatusCode Status, HTTPResponse? Response, String? Error = null)

                => new (Status,
                        Response,
                        written,
                        attempts,
                        resumes,
                        restarts,
                        validator,
                        verification,
                        Error);

            try
            {

                while (true)
                {

                    if (CancellationToken.IsCancellationRequested)
                        return Result(HTTPStatusCode.ClientError, representation, "the download was cancelled");

                    attempts++;
                    notWritten = null;

                    var resuming = written > 0 && validator is not null;

                    if (resuming)
                        resumes++;

                    var request = CreateRequest(
                                      HTTPMethod.GET,
                                      Path,
                                      QueryString,
                                      RequestBuilder: builder => {

                                          builder.SetHeaderField("Accept-Encoding", "identity");

                                          // The digest of one range's content says
                                          // nothing about the file the ranges add up
                                          // to; the representation's digest does.
                                          builder.RemoveHeaderField("Want-Content-Digest");

                                          if (VerifyDigests)
                                              builder.SetHeaderField("Want-Repr-Digest", HTTPDigest.Want);

                                          RequestBuilder?.Invoke(builder);

                                          if (resuming)
                                          {
                                              builder.SetHeaderField("Range",    HTTPContentRange.RequestFrom((Int64) written));
                                              builder.SetHeaderField("If-Range", validator!);
                                          }

                                      },
                                      CancellationToken: CancellationToken
                                  ).AsImmutable;

                    // Asked once each response head is in: whether its body goes to
                    // the destination, which only that of a 200 or a 206 does.
                    Func<ReadOnlyMemory<Byte>, CancellationToken, ValueTask>? Sink(HTTPResponse Head)
                    {

                        if (Head.HTTPStatusCode.Code is not (200 or 206))
                            return null;

                        // Where does this body start? A resume answered 200, or with
                        // a range that does not begin where the destination stops,
                        // means the octets there belong to a representation that is
                        // gone.
                        if (resuming &&
                            (Head.HTTPStatusCode.Code == 200 ||
                             ContentRangeOf(Head)?.Start != (Int64) written))
                        {

                            if (!Destination.CanSeek)
                            {
                                notWritten = "the representation changed and the download must start over, but the destination cannot seek";
                                return null;
                            }

                            Destination.Position = startPosition;
                            Destination.SetLength(startPosition);

                            written         = 0;
                            restarts++;
                            representation  = null;

                            // A different representation has a different digest.
                            hash?.Dispose();
                            hash            = null;
                            algorithm       = null;
                            expected        = null;
                            verification    = HTTPDigestVerification.NotPresent;

                        }

                        representation ??= Head;

                        // The digest is taken from the first response that brings
                        // one, and only while nothing has been written: a hash begun
                        // in the middle of the file could never match.
                        if (VerifyDigests && algorithm is null && written == 0 &&
                            FieldLines(Head, "Repr-Digest") is [ var offered ])
                        {

                            var digests = HTTPDigest.Parse(offered);

                            algorithm = Array.Find(HTTPDigest.Supported, digests.ContainsKey);

                            if (algorithm is not null)
                            {
                                expected  = digests[algorithm];
                                hash      = HTTPDigest.CreateIncremental(algorithm);
                            }
                            else
                                verification = HTTPDigestVerification.Unsupported;

                        }

                        validator = ResumeValidatorOf(Head) ?? validator;

                        return async (chunk, cancellationToken) => {
                            await Destination.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                            hash?.AppendData(chunk.Span);
                            written += (UInt64) chunk.Length;
                        };

                    }

                    var response = await SendAnsweringAndFollowing(
                                             request,
                                             oneRequest => SendOneRequest(
                                                               oneRequest,
                                                               ConsumeResponseChunkedTEImmediately: false,
                                                               RequestTimeout:     RequestTimeout,
                                                               CancellationToken:  CancellationToken,
                                                               BodySink:           Sink
                                                           ),
                                             CancellationToken
                                         ).ConfigureAwait(false);

                    if (notWritten is not null)
                        return Result(HTTPStatusCode.ClientError, response, notWritten);

                    if (response.HTTPStatusCode.Code is not (200 or 206))
                    {

                        // A 416 to a resume is not a failure when the representation
                        // is exactly as long as what the destination holds: the body
                        // broke off at its very end (RFC 9110, Section 15.5.17).
                        if (response.HTTPStatusCode.Code == 416 && resuming &&
                            ContentRangeOf(response) is { IsUnsatisfied: true } unsatisfied &&
                            unsatisfied.CompleteLength == (Int64) written)
                        {
                            return Finish(HTTPStatusCode.OK, representation ?? response);
                        }

                        return Result(response.HTTPStatusCode, representation ?? response);

                    }

                    if (response.BodyInterruption is Exception interruption)
                    {

                        if (attempts < MaxAttempts && written > 0 && validator is not null)
                            continue;

                        return Result(
                                   HTTPStatusCode.ClientError,
                                   representation ?? response,
                                   $"the body broke off after {written} octets, " +
                                   (validator is null
                                        ? "and the server sent no validator to resume with"
                                        : $"after {attempts} {(attempts == 1 ? "attempt" : "attempts")}") +
                                   $": {interruption.Message}"
                               );

                    }

                    return Finish(response.HTTPStatusCode, representation ?? response);

                }

            }
            finally
            {
                hash?.Dispose();
            }

            // The last octet is in: compare the hash of all of them with the
            // Repr-Digest the server promised. A destination that holds other
            // octets than those is not a download to report as complete.
            HTTPDownloadResult Finish(HTTPStatusCode Status, HTTPResponse Response)
            {

                if (hash is not null && expected is not null)
                {

                    verification = expected.AsSpan().SequenceEqual(hash.GetCurrentHash())
                                       ? HTTPDigestVerification.Match
                                       : HTTPDigestVerification.Mismatch;

                    if (verification == HTTPDigestVerification.Mismatch)
                        return Result(HTTPStatusCode.ClientError, Response, $"the {algorithm} digest of the {written} octets written does not match the Repr-Digest the server sent (RFC 9530)");

                }

                return Result(Status, Response);

            }

        }

        /// <summary>
        /// The response's Content-Range, when it has exactly one that parses.
        /// </summary>
        private static HTTPContentRange? ContentRangeOf(HTTPResponse Response)

            => FieldLines(Response, "Content-Range") is [ var value ] &&
               HTTPContentRange.TryParse(value, out var contentRange)
                   ? contentRange
                   : null;

        /// <summary>
        /// The validator a resume may be guarded by (RFC 9110, Section 13.1.5): a
        /// strong entity-tag, or else the Last-Modified date. A weak entity-tag
        /// promises equivalence, and splicing octets needs them to be the same.
        /// </summary>
        private static String? ResumeValidatorOf(HTTPResponse Response)
        {

            if (FieldLines(Response, "ETag") is [ var etag ] &&
                !HTTPValidators.SplitETag(etag).Weak)
            {
                return etag.Trim();
            }

            return FieldLines(Response, "Last-Modified") is [ var lastModified ]
                       ? lastModified.Trim()
                       : null;

        }

        #endregion

        #region (private) CacheOrigin               (Request)

        /// <summary>
        /// The scheme and authority a cache keys this request's responses by:
        /// the scheme this client connects with and the request's Host.
        /// </summary>
        private String CacheOrigin(HTTPRequest Request)

            => (EnforceTLS || RemoteURL.Scheme?.EnforcesTLS == true ? "https" : "http") +
               "://" + Request.Host.ToString().ToLowerInvariant();

        #endregion

        #region (private) SendAnsweringAndFollowing (Request, Send, CancellationToken)

        /// <summary>
        /// The request, its 401 answered with <see cref="Credentials"/> and its
        /// redirections followed as far as <see cref="MaxRedirects"/> allows, each
        /// single request going out through Send: SendRequest's way, or
        /// DownloadAsync's, which hands a body to its destination as it arrives.
        /// </summary>
        private async Task<HTTPResponse> SendAnsweringAndFollowing(HTTPRequest                            Request,
                                                                   Func<HTTPRequest, Task<HTTPResponse>>  Send,
                                                                   CancellationToken                      CancellationToken)
        {

            var maxRedirects  = MaxRedirects;
            var request       = Request;
            var response      = await SendAnsweringChallenge(request, Send, CancellationToken).ConfigureAwait(false);

            List<String>? chain = null;

            for (var hop = 0; hop < maxRedirects; hop++)
            {

                // The request that was redirected, not the one that answered a
                // challenge on the way: Digest signs the request-target, so an
                // Authorization field computed for this hop is wrong for the next,
                // and a request carrying one is not answered for again.
                if (!TryFollowRedirect(request, response, out var next, out var target))
                {
                    break;
                }

                (chain ??= []).Add(target.ToString());

                request  = next;
                response = await SendAnsweringChallenge(request, Send, CancellationToken).ConfigureAwait(false);

                response.RedirectChain = [.. chain];

            }

            return response;

        }

        #endregion

        #region (private) SendAnsweringChallenge (Request, Send, CancellationToken)

        /// <summary>
        /// One request, sent once more with an Authorization field when it is
        /// answered with a 401 whose challenge <see cref="Credentials"/> can meet
        /// (RFC 9110, Section 11.6.1). Once: a second 401 says the credentials are
        /// wrong, and sending them again would only earn a third. The request sent
        /// again is the same one, so the authority cannot change between the
        /// challenge and the answer.
        /// </summary>
        private async Task<HTTPResponse> SendAnsweringChallenge(HTTPRequest                            Request,
                                                                Func<HTTPRequest, Task<HTTPResponse>>  Send,
                                                                CancellationToken                      CancellationToken)
        {

            var response     = await Send(Request).ConfigureAwait(false);
            var credentials  = Credentials;

            if (response.HTTPStatusCode.Code != 401 ||
                credentials is null                 ||
                HasBodyOnTheConnection(response)    ||
                FieldLines(Request, HTTPRequestHeaderField.Authorization.Name).Length > 0 ||
                !HasReplayableBody(Request))
            {
                return response;
            }

            var challenges = FieldLines(response, HTTPResponseHeaderField.WWWAuthenticate.Name);

            if (challenges.Length == 0)
                return response;

            var authorization = AuthenticatorFor(credentials).Answer(
                                    challenges,
                                    Request.HTTPMethod,
                                    Request.RequestTarget
                                );

            if (authorization is null)
                return response;

            var answered = new HTTPRequest.Builder(Request);
            answered.SetHeaderField(HTTPRequestHeaderField.Authorization.Name, authorization);

            return await Send(answered.AsImmutable).ConfigureAwait(false);

        }

        /// <summary>
        /// The authenticator for these credentials. It keeps the Digest nonce
        /// count, which RFC 7616 requires to increase with every request made
        /// with one nonce, so it lives as long as the credentials it was built
        /// for rather than as long as one request.
        /// </summary>
        private HTTPClientAuthenticator AuthenticatorFor(HTTPClientCredentials Credentials)
        {
            lock (authenticatorLock)
            {

                if (authenticator is null || !ReferenceEquals(authenticatorCredentials, Credentials))
                {
                    authenticator             = new HTTPClientAuthenticator(Credentials);
                    authenticatorCredentials  = Credentials;
                }

                return authenticator;

            }
        }

        #endregion

        #region (private) TryFollowRedirect      (Request, Response, out Next, out Target)

        /// <summary>
        /// The request that follows a redirection (RFC 9110, Section 15.4), or
        /// false when there is none to follow: a status that is not a redirect,
        /// a body still on the connection, no Location or more than one, a target
        /// on another origin, or a 307 or 308 asking to send again a body that a
        /// chunk worker wrote.
        /// </summary>
        private Boolean TryFollowRedirect(HTTPRequest                                   Request,
                                          HTTPResponse                                  Response,
                                          [NotNullWhen(true)] out HTTPRequest?          Next,
                                          [NotNullWhen(true)] out HTTPRedirectTarget?   Target)
        {

            Next    = null;
            Target  = null;

            var status = (Int32) Response.HTTPStatusCode.Code;

            if (!HTTPRedirect.IsRedirect(status) ||
                HasBodyOnTheConnection(Response))
            {
                return false;
            }

            // Two Location lines name two targets, and following either would be a
            // guess (RFC 9110, Section 10.2.2 defines the field as one URI).
            var locations  = FieldLines(Response, HTTPResponseHeaderField.Location.Name);
            var hosts      = FieldLines(Request,  HTTPRequestHeaderField.Host.Name);

            if (locations.Length != 1 || hosts.Length != 1)
                return false;

            // The base a relative Location resolves against is the request as it
            // was addressed: this client's scheme and the Host it sent.
            if (!HTTPRedirect.TryResolve(
                     status,
                     locations[0],
                     RemoteURL.Scheme?.EnforcesTLS == true ? URIScheme.https : URIScheme.http,
                     hosts[0],
                     Request.RequestTarget,
                     Request.HTTPMethod,
                     out var target
                 ) ||
                !target.SameOrigin ||
                (target.KeepBody && !HasReplayableBody(Request)))
            {
                return false;
            }

            var query    = target.Path.IndexOf('?');
            var builder  = new HTTPRequest.Builder(Request) {
                               HTTPMethod   = target.Method,
                               Path         = HTTPPath.Parse(query < 0 ? target.Path : target.Path[..query]),
                               QueryString  = query < 0 ? QueryString.Empty : QueryString.Parse(target.Path[(query + 1)..])
                           };

            // A follow-up without the body must not go on describing one: a GET
            // still carrying the POST's Content-Length would be malformed, and
            // RFC 9110, Section 15.4 has the fields about the content go with it.
            if (!target.KeepBody)
            {

                builder.Content        = null;
                builder.ContentStream  = null;

                foreach (var fieldName in ContentFieldNames)
                    builder.RemoveHeaderField(fieldName);

            }

            Next    = builder.AsImmutable;
            Target  = target;

            return true;

        }

        /// <summary>
        /// The request fields that describe the content, which a follow-up that
        /// drops it drops with it.
        /// </summary>
        private static readonly String[] ContentFieldNames = [
                                                                 "Content-Length",
                                                                 "Content-Type",
                                                                 "Content-Encoding",
                                                                 "Content-Language",
                                                                 "Content-Location",
                                                                 "Content-Digest",
                                                                 "Repr-Digest",
                                                                 "Transfer-Encoding",
                                                                 "Expect"
                                                             ];

        #endregion

        #region (private) Helpers

        /// <summary>
        /// Whether the request's body, if it has one, can be sent a second time:
        /// an array can, what a chunk worker writes cannot.
        /// </summary>
        private static Boolean HasReplayableBody(HTTPRequest Request)

            => !(Request.IsChunkedTransferEncoding && Request.UseChunkWorker);

        /// <summary>
        /// The field lines of one field, as they arrived. The header section keeps
        /// a repeated field as an array.
        /// </summary>
        private static String[] FieldLines(AHTTPPDU PDU, String FieldName)

            => PDU.TryGetHeaderField(FieldName, out var value)
                   ? value switch {
                         String    line   => [ line ],
                         String[]  lines  => lines,
                         _                => [ value.ToString() ?? "" ]
                     }
                   : [];

        /// <summary>
        /// Whether the response's body is still on the connection rather than
        /// read. This client reads every body but an event stream's, and an event
        /// stream need never end: the connection is the stream's now, and the
        /// response is handed back as it is, neither followed nor answered.
        /// </summary>
        private static Boolean HasBodyOnTheConnection(HTTPResponse Response)

            => Response.HTTPBodyStream is not null;

        #endregion

        #region (private) SendOneRequest         (Request, ...)

        /// <summary>
        /// Send the given HTTP Request to the server and receive the HTTP Response,
        /// once, as it is: no challenge answered and no redirect followed.
        /// </summary>
        /// <param name="Request">The HTTP Request to send.</param>
        /// <param name="BodySink">Asked once the response head is in, for where its body should go instead of HTTPBody, or null to keep it as usual.</param>
        private async Task<HTTPResponse>

            SendOneRequest(HTTPRequest                Request,
                           Boolean?                   ConsumeResponseChunkedTEImmediately   = null,
                           ClientRequestLogHandler?   RequestLogDelegate                    = null,
                           ClientResponseLogHandler?  ResponseLogDelegate                   = null,
                           TimeSpan?                  MaxSemaphoreWaitTime                  = null,
                           TimeSpan?                  RequestTimeout                        = null,
                           UInt16?                    MaxNumberOfRetries                    = null,
                           CancellationToken          CancellationToken                     = default,
                           Func<HTTPResponse, Func<ReadOnlyMemory<Byte>, CancellationToken, ValueTask>?>?  BodySink  = null)

        {

            // Refused before anything is opened or sent: a one-time password that
            // was asked to be bound to the TLS session cannot be, see TOTPConfig.
            if (TOTPConfig?.TLSChannelBinding == true)
                return new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.ClientError,
                           Content         = $"Not sent to {RemoteURL}: {TOTPConfig.TLSChannelBindingNotAvailable}".ToUTF8Bytes(),
                           ContentType     = HTTPContentType.Text.PLAIN,
                           Runtime         = TimeSpan.Zero
                       };

            ConsumeResponseChunkedTEImmediately ??= this.ConsumeResponseChunkedTEImmediately ?? true;

            var success = await sendRequestSemaphore.WaitAsync(
                                    MaxSemaphoreWaitTime ?? this.MaxSemaphoreWaitTime,
                                    CancellationToken
                                );

            if (success)
            {
                try
                {

                    var maxNumberOfRetries  = MaxNumberOfRetries ?? this.MaxNumberOfRetries;
                    var retry               = 1;

                    // Why the loop gave up, in the words of whatever actually
                    // went wrong. Without it the caller was told nothing beyond
                    // "maximum retries reached", which names the symptom.
                    var lastError           = (String?) null;

                    while (retry <= maxNumberOfRetries)
                    {

                        // A request whose token is cancelled is not tried again.
                        // The loop used to go on regardless: each attempt hands
                        // the token to a reconnect that does not watch it, so a
                        // caller's deadline ran out while the retries went on.
                        // Against a refused port that is about two seconds an
                        // attempt on Windows, and DNSHTTPSClient's 3 s query
                        // came back after 8.2 s.
                        if (CancellationToken.IsCancellationRequested)
                        {
                            lastError ??= "the request was cancelled";
                            break;
                        }

                        if (retry > 1)
                            DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)} {RemoteURL}, retry #{retry} of {maxNumberOfRetries}...");

                        if (!IsConnected || !IsHTTPConnected || IsConnectionClosed || IsConnectionLifetimeExceeded)
                        {
                            try
                            {

                                var connectionResult = await ReconnectAsync(CancellationToken);

                                if (!connectionResult.IsSuccess)
                                {
                                    lastError = connectionResult.Errors.AggregateWith(", ");
                                    await Log($"Error in SendRequest: {lastError}");
                                    DebugX.LogT($"{nameof(AHTTPClient)}.{nameof(SendRequest)}: {lastError}");
                                    IsHTTPConnected = false;
                                    retry++;
                                    continue;
                                }

                            }
                            catch (Exception ex)
                            {
                                lastError = $"[{ex.GetType().Name}] {ex.Message}";
                                await Log($"Error in SendRequest: {ex.Message}");
                                DebugX.LogException(ex, nameof(AHTTPClient) + "." + nameof(SendRequest));
                                IsHTTPConnected = false;
                                retry++;
                                continue;
                            }
                        }

                        if (httpStream is null)
                        {
                            lastError = "HTTP stream is not available!";
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

                            // LiveClient..., not the field: after a reconnect the
                            // field can hold a source which has been cancelled,
                            // and linking to it hands this request a token that
                            // is dead before it is used.
                            using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                                                                    LiveClientCancellationTokenSource.Token,
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

                            var useChunkWorker      = Request.IsChunkedTransferEncoding &&
                                                      Request.UseChunkWorker;
                            var hasRequestBody      = useChunkWorker ||
                                                      Request.HTTPBody is not null &&
                                                      (Request.ContentLength > 0 ||
                                                       Request.IsChunkedTransferEncoding);
                            var waitFor100Continue = hasRequestBody &&
                                                     Request.Expect?.Split(',').Any(expectation =>
                                                         expectation.Trim().Equals("100-continue", StringComparison.OrdinalIgnoreCase)
                                                     ) == true;

                            // The 1xx responses ahead of the final one (RFC 9110,
                            // Section 15.2), for HTTPResponse.InformationalResponses.
                            var informationalResponses = new List<HTTPResponse>();

                            async Task SendRequestBody()
                            {

                                if (useChunkWorker)
                                {

                                    using var chunkedStream = new ChunkedTransferEncodingStream(
                                                                      httpStream,
                                                                      LeaveInnerStreamOpen: true
                                                                  );

                                    await Request.ChunkWorker(
                                              Request,
                                              chunkedStream
                                          ).ConfigureAwait(false);

                                    // RFC 9112, Section 7.1: the zero-length chunk
                                    // is what ends a chunked body, and a request
                                    // that stops short of it leaves the server
                                    // waiting for a message it has already been
                                    // sent. Same hazard as on the response side,
                                    // one direction over. Finish is idempotent, so
                                    // a worker that ended its own body - which is
                                    // what every worker in this repository does -
                                    // is unaffected.
                                    await chunkedStream.Finish(
                                              Request.TrailingHeaders.ToDictionary(trailer => trailer.Key, trailer => trailer.Value),
                                              linkedCancellationToken.Token
                                          ).ConfigureAwait(false);

                                }

                                else if (Request.HTTPBody is not null &&
                                         (Request.ContentLength > 0 ||
                                          Request.IsChunkedTransferEncoding))
                                {
                                    await httpStream.WriteAsync(
                                              Request.HTTPBody,
                                              linkedCancellationToken.Token
                                          ).ConfigureAwait(false);
                                }

                            }

                            await httpStream.WriteAsync(
                                      Encoding.UTF8.GetBytes(Request.EntireRequestHeader + "\r\n\r\n"),
                                      linkedCancellationToken.Token
                                  ).ConfigureAwait(false);

                            if (!waitFor100Continue && hasRequestBody)
                                await SendRequestBody().ConfigureAwait(false);

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

                                 var responseHeader = Encoding.UTF8.GetString(buffer[..endOfHTTPHeaderIndex].Span);

                                if (!TryValidateResponseFraming(responseHeader, out var responseFramingError))
                                {

                                    bufferOwner?.Dispose();

                                    // The connection cannot be reused after this,
                                    // and saying so is the whole point of the line.
                                    //
                                    // We have refused the response BECAUSE we could
                                    // not work out where its body ends - so we did
                                    // not consume it, and we cannot. Whatever of it
                                    // is still in the socket becomes the first
                                    // octets of the next response read here: a
                                    // second request on this connection came back
                                    // as "Invalid HTTP response status line",
                                    // because what it read was "5\r\nhello".
                                    //
                                    // It only shows when the body arrives in a
                                    // later TCP segment than the head. Send both at
                                    // once and the leftovers land in this client's
                                    // own buffer and are dropped with it, which is
                                    // why the connection looked reusable and why
                                    // nothing here had caught it. Found by A6 while
                                    // checking the client half of H-29.
                                    IsHTTPConnected = false;

                                    // This client refusing the server's framing,
                                    // reported as the server refusing the
                                    // client's request. The server said no such
                                    // thing - it said whatever is in
                                    // responseHeader, and this code would not
                                    // parse it.
                                    return new HTTPResponse.Builder(Request) {
                                               HTTPStatusCode  = HTTPStatusCode.ClientError,
                                               Content         = responseFramingError.ToUTF8Bytes(),
                                               ContentType     = HTTPContentType.Text.PLAIN,
                                               Runtime         = stopwatch.Elapsed
                                           };

                                }

                                 if (IsInterimResponse(responseHeader))
                                {

                                    if (waitFor100Continue &&
                                        Is100ContinueResponse(responseHeader))
                                    {
                                        await SendRequestBody().ConfigureAwait(false);

                                        await httpStream.FlushAsync(
                                                  linkedCancellationToken.Token
                                              ).ConfigureAwait(false);

                                        waitFor100Continue = false;
                                    }

                                    // Kept rather than dropped, so that a caller can
                                    // read a 103's Link fields (RFC 8297). One that
                                    // does not parse is skipped as before: it is
                                    // advisory, and the final response is what was
                                    // asked for.
                                    if (informationalResponses.Count < MaxInformationalResponses)
                                    {
                                        try
                                        {
                                            informationalResponses.Add(
                                                HTTPResponse.Parse(
                                                    Timestamp.Now,
                                                    stopwatch.Elapsed,
                                                    Request,
                                                    this,
                                                    LocalSocket!. Value,
                                                    RemoteSocket!.Value,
                                                    new HTTPSource(LocalSocket!.Value),
                                                    responseHeader,
                                                    CancellationToken: Request.CancellationToken
                                                )
                                            );
                                        }
                                        catch (Exception)
                                        { }
                                    }

                                    var interimRemainingStart  = endOfHTTPHeaderIndex + endOfHTTPHeaderDelimiterLength;
                                    var interimRemainingLength = dataLength - interimRemainingStart;
                                    buffer.Slice(interimRemainingStart, interimRemainingLength).CopyTo(buffer[..]);
                                    dataLength = interimRemainingLength;

                                     continue;

                                 }

                                 #region Parse HTTP Response

                                var response = HTTPResponse.Parse(
                                                Timestamp.Now,
                                                stopwatch.Elapsed,
                                                Request,
                                                this,
                                                LocalSocket!. Value,
                                                RemoteSocket!.Value,
                                                new HTTPSource(LocalSocket!.Value),
                                                responseHeader,
                                                ConsumeResponseChunkedTEImmediately,
                                                CancellationToken: Request.CancellationToken
                                            );

                                response.HTTPClient           ??= this;
                                response.InformationalResponses   = informationalResponses;

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
                                 var responseHasNoBody    = Request.HTTPMethod == HTTPMethod.HEAD ||
                                                             response.HTTPStatusCode.Code is >= 100 and < 200 or 204 or 205 or 304;
                                  var responseMustNotDeclareABody = response.HTTPStatusCode.Code is >= 100 and < 200 or 204 or 205;
                                  var hasUnexpectedBodyBytes      = responseHasNoBody &&
                                                                       (dataLength > 0 ||
                                                                        (responseMustNotDeclareABody &&
                                                                         (response.IsChunkedTransferEncoding ||
                                                                          response.ContentLength.GetValueOrDefault() > 0)));

                                var prefix = buffer[..dataLength];
                                var isCloseDelimitedBody = !responseHasNoBody &&
                                                           !response.IsChunkedTransferEncoding &&
                                                           !response.ContentLength.HasValue &&
                                                           response.IsConnectionClose;

                                if (!responseHasNoBody &&
                                    (response.IsChunkedTransferEncoding ||
                                     response.ContentLength.HasValue ||
                                     isCloseDelimitedBody))
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

                                            response.SetTrailingHeaders(trailers);
                                            response.ChunkExtensions = chunkedStream.ChunkExtensions;
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

                                    else if (isCloseDelimitedBody)
                                        bodyStream = bodyDataStream;

                                }

                                response.HTTPBodyStream = bodyStream;

                                #endregion

                                if (!responseHasNoBody &&
                                    response.ContentType == HTTPContentType.Text.EVENTSTREAM)
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

                                    // An event stream can be gzipped like anything
                                    // else — nginx does it by default — and this is
                                    // the path where decoding has to happen *in* the
                                    // stream: the body is not supposed to end, so
                                    // there is never a moment at which it could be
                                    // buffered and decoded afterwards. Ownership of
                                    // the connection stream stays where it is; that
                                    // is what CloseActionAfterBodyWasRead below is.
                                    if (AutomaticDecompression &&
                                        !response.TryDecodeBodyStream(out var sseDecodeError, MaxDecodedBodySize))
                                    {
                                        DebugX.LogT($"{nameof(AHTTPClient)} left the event stream encoded: {sseDecodeError}");
                                    }

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

                                        // A body handed to a sink as it arrives rather
                                        // than kept: what DownloadAsync writes to its
                                        // destination. The octets go on as they came,
                                        // undecoded and unchecked, since a range of a
                                        // representation has no content coding or
                                        // digest of its own to undo or check. A body
                                        // that breaks off is recorded on the response
                                        // instead of sent for again: what reached the
                                        // sink is kept, and the request is not
                                        // repeated from its first octet by the retry
                                        // loop below, which would hand the same octets
                                        // to the sink a second time.
                                        var bodySink = bodyStream is not null && !bodyAlreadyConsumed
                                                           ? BodySink?.Invoke(response)
                                                           : null;

                                        if (bodySink is not null)
                                        {

                                            var chunk = new Byte[64 * 1024];

                                            try
                                            {

                                                Int32 read;

                                                while ((read = await bodyStream!.ReadAsync(chunk, linkedCancellationToken.Token).ConfigureAwait(false)) > 0)
                                                    await bodySink(chunk.AsMemory(0, read), linkedCancellationToken.Token).ConfigureAwait(false);

                                            }
                                            catch (Exception e)
                                            {
                                                response.BodyInterruption = e;
                                                await CloseHTTPConnectionAfterFailure().ConfigureAwait(false);
                                            }

                                            response.HTTPBody        = [];
                                            response.HTTPBodyStream  = null;

                                        }

                                        else if (bodyStream is not null)
                                        {

                                            // RFC 9110, Section 8.4: undo what we asked
                                            // for. Before the body is read rather than
                                            // after, so that the buffered and the
                                            // streamed case are one implementation and
                                            // cannot drift apart. The decoder takes the
                                            // pipeline over — hence the reassignment,
                                            // which is also what puts it in reach of
                                            // the finally below.
                                            //
                                            // Except when a digest is to be checked:
                                            // RFC 9530 has it describe the octets as
                                            // they arrived, which a decoder in the
                                            // pipeline would never let us see. Then
                                            // the body is read as it came and decoded
                                            // in place afterwards — the order the
                                            // chunked case below has anyway.
                                            var decodeInPlace = bodyAlreadyConsumed || VerifyDigests;

                                            if (AutomaticDecompression && !decodeInPlace)
                                            {

                                                if (response.TryDecodeBodyStream(out var decodeError,
                                                                                 MaxDecodedBodySize,
                                                                                 LeaveBodyStreamOpen: false))
                                                    bodyStream = response.HTTPBodyStream ?? bodyStream;

                                                else
                                                    // A body we cannot decode is still a
                                                    // body: it is handed on as it came,
                                                    // Content-Encoding intact, rather
                                                    // than passed off as identity.
                                                    DebugX.LogT($"{nameof(AHTTPClient)} left the response body encoded: {decodeError}");

                                            }

                                            if (!bodyAlreadyConsumed)
                                                response.HTTPBody = await ReadHTTPBodyStream(
                                                                        bodyStream,
                                                                        linkedCancellationToken.Token
                                                                    ).ConfigureAwait(false);

                                            response.HTTPBodyStream = null;

                                            if (VerifyDigests)
                                                response.DigestVerification = response.VerifyContentDigest();

                                            // A chunked body consumed the moment it
                                            // arrived was already an array before the
                                            // stream could be wrapped, so that case
                                            // decodes the other way round.
                                            if (AutomaticDecompression && decodeInPlace &&
                                                !response.TryDecodeBodyInPlace(out var inPlaceError, MaxDecodedBodySize))
                                            {
                                                DebugX.LogT($"{nameof(AHTTPClient)} left the response body encoded: {inPlaceError}");
                                            }

                                        }
                                        else if (responseHasNoBody ||
                                                 !response.ContentLength.HasValue || response.ContentLength.Value == 0)
                                        {

                                            response.HTTPBody = [];

                                            // An empty content has a digest too. A
                                            // response to HEAD carries none, so
                                            // whatever its field says is not about
                                            // these zero octets.
                                            if (VerifyDigests && !responseHasNoBody)
                                                response.DigestVerification = response.VerifyContentDigest();

                                        }

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

                                if (response.IsConnectionClose || hasUnexpectedBodyBytes)
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

                                // RFC 9530: content that disagrees with its digest
                                // is not handed to a caller who asked for it to be
                                // checked. It is logged above as it arrived, and the
                                // connection is no worse for it: the framing was
                                // sound, so the request is not tried again either.
                                if (response.DigestVerification == HTTPDigestVerification.Mismatch)
                                {

                                    var mismatch = new HTTPResponse.Builder(Request) {
                                                       HTTPStatusCode  = HTTPStatusCode.ClientError,
                                                       Content         = $"The content of the {response.HTTPStatusCode.Code} response does not match its Content-Digest (RFC 9530)".ToUTF8Bytes(),
                                                       ContentType     = HTTPContentType.Text.PLAIN,
                                                       Runtime         = response.Runtime
                                                   }.AsImmutable;

                                    mismatch.DigestVerification = HTTPDigestVerification.Mismatch;

                                    return mismatch;

                                }

                                return response;

                            }

                        }
                        catch (Exception ex)
                        {

                            // Persistend HTTP connection was probably just closed...

                            // The whole chain, because the outer message alone can point
                            // the wrong way: a rejected TLS 1.3 client certificate, for
                            // example, surfaces here as "The decryption operation failed"
                            // and only the inner Win32Exception names the actual alert.
                            lastError = $"[{ex.GetType().Name}/0x{ex.HResult:X8}] {ex.Message}";
                            for (var innerException = ex.InnerException; innerException is not null; innerException = innerException.InnerException)
                                lastError += $" <= [{innerException.GetType().Name}/0x{innerException.HResult:X8}" +
                                             (innerException is System.ComponentModel.Win32Exception win32Exception ? $"/native:{win32Exception.NativeErrorCode}" : "") +
                                             $"] {innerException.Message}";

                            if (retry > 1 || ex.InnerException is not SocketException)
                            {
                                await Log($"Error in SendRequest: {lastError}");
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

                    // This one is written here, not received: the request was
                    // never sent, or the exchange never finished.
                    // HTTPStatusCode.ClientError is code 0 and exists for exactly
                    // that - no server can return it, so no caller can mistake it
                    // for a verdict. BadRequest was such a mistake waiting to
                    // happen, and DNSHTTPSClient duly made it, passing the
                    // client's own 400 on as "the resolver returned HTTP 400".
                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.ClientError,
                               Content         = $"Giving up on this HTTP request to {RemoteURL} after {retry - 1} {(retry - 1 == 1 ? "attempt" : "attempts")}: {lastError ?? "no reason was recorded"}".ToUTF8Bytes(),
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Runtime         = TimeSpan.Zero
                           };

                }
                catch (Exception e)
                {

                    DebugX.LogT(e.Message);

                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.ClientError,
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

            // Also nothing from a server: this request never even reached the
            // sending code.
            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.ClientError,
                       Content         = $"Could not acquire semaphore for {nameof(AHTTPClient)}.{nameof(SendRequest)}.".ToUTF8Bytes(),
                       ContentType     = HTTPContentType.Text.PLAIN,
                       Runtime         = TimeSpan.Zero
                   };

        }

        #endregion


        #region (private)   IsInterimResponse(HTTPHeader)

        /// <summary>
        /// Whether the given HTTP response header belongs to an informational
        /// response that precedes the final response.
        /// </summary>
        private static Boolean IsInterimResponse(String HTTPHeader)
        {

            var statusLineEnd = HTTPHeader.IndexOf("\r\n", StringComparison.Ordinal);
            var statusLine    = statusLineEnd >= 0
                                    ? HTTPHeader[..statusLineEnd]
                                    : HTTPHeader;

            if (!statusLine.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                return false;

            var statusCodeStart = statusLine.IndexOf(' ');
            if (statusCodeStart < 0)
                return false;

            var statusCodeEnd = statusLine.IndexOf(' ', statusCodeStart + 1);
            var statusCode    = statusLine[(statusCodeStart + 1)..(statusCodeEnd >= 0 ? statusCodeEnd : statusLine.Length)];

            return UInt16.TryParse(statusCode, out var statusCodeNumber) &&
                   statusCodeNumber is >= 100 and < 200 and not 101;

        }

        #endregion


        #region (private)   TryValidateResponseFraming(HTTPHeader, out ErrorDescription)

        private static Boolean TryValidateResponseFraming(String   HTTPHeader,
                                                           out String? ErrorDescription)
        {

            ErrorDescription = null;

            var statusLine = HTTPHeader.Split("\r\n", 2, StringSplitOptions.None)[0];
            var statusParts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            if (statusParts.Length < 2 ||
                statusParts[0] is not "HTTP/1.0" and not "HTTP/1.1" ||
                statusParts[1].Length != 3 ||
                !statusParts[1].All(character => character >= '0' && character <= '9') ||
                !UInt16.TryParse(statusParts[1], out var statusCode) ||
                statusCode is < 100 or > 599)
            {
                ErrorDescription = "Invalid HTTP response status line.";
                return false;
            }

            var contentLengths    = new List<String>();
            var transferEncodings = new List<String>();

            foreach (var headerLine in HTTPHeader.Split("\r\n", StringSplitOptions.None).Skip(1))
            {

                var separatorIndex = headerLine.IndexOf(':');
                if (separatorIndex <= 0 ||
                    !headerLine[..separatorIndex].All(character => character is >= 'a' and <= 'z' or
                                                                    >= 'A' and <= 'Z' or
                                                                    >= '0' and <= '9' or
                                                                    '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or
                                                                    '^' or '_' or '`' or '|' or '~'))
                {
                    ErrorDescription = "Invalid HTTP response header field line.";
                    return false;
                }

                 var name  = headerLine[..separatorIndex];
                 var value = headerLine[(separatorIndex + 1)..].Trim();

                if (value.Any(character => character is < ' ' and not '\t' or '\x7F'))
                {
                    ErrorDescription = "Invalid HTTP response header field value.";
                    return false;
                }

                 if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    contentLengths.Add(value);

                else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    transferEncodings.Add(value);

            }

            if (contentLengths.Count > 1 ||
                contentLengths.Any(value => value.Contains(',') ||
                                            value.IsNullOrEmpty() ||
                                           !value.All(character => character >= '0' && character <= '9') ||
                                           !UInt64.TryParse(value, out _)))
            {
                ErrorDescription = "Invalid or duplicate Content-Length in HTTP response.";
                return false;
            }

            if (contentLengths.Count > 0 && transferEncodings.Count > 0)
            {
                ErrorDescription = "Content-Length and Transfer-Encoding must not be used together in an HTTP response.";
                return false;
            }

            if (transferEncodings.Count > 0)
            {

                var transferCodings = transferEncodings.SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries))
                                                       .ToArray();

                if (transferCodings.Length != 1 ||
                    !transferCodings[0].Equals("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorDescription = "Only a single final chunked Transfer-Encoding is supported in an HTTP response.";
                    return false;
                }

            }

            return true;

        }

        #endregion


        #region (private)   Is100ContinueResponse(HTTPHeader)

        /// <summary>
        /// Whether the given HTTP response header belongs to a 100 Continue response.
        /// </summary>
        private static Boolean Is100ContinueResponse(String HTTPHeader)
        {

            var statusLineEnd = HTTPHeader.IndexOf("\r\n", StringComparison.Ordinal);
            var statusLine    = statusLineEnd >= 0
                                    ? HTTPHeader[..statusLineEnd]
                                    : HTTPHeader;

            return statusLine.StartsWith("HTTP/1.1 100", StringComparison.OrdinalIgnoreCase);

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

            if (!ReferenceEquals(httpStream, TLSStream))
            {
                try { TLSStream?.Dispose(); } catch { }
            }

            httpStream             = null;
            tlsStream              = null;
            rawPublicKeyTLSStream  = null;
            TLSSession             = null;

            try
            {
                // CloseConnection, not Close. This runs inside SendRequest's
                // retry loop, and Close cancels the client's token - the one a
                // caller's request is linked to. So closing "the connection"
                // after a failed attempt ended the request being retried, and
                // the attempts after it began already cancelled.
                await CloseConnection().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                DebugX.LogException(e, "while closing a failed HTTP client connection");
            }

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

                // Taken once, and from LiveClient... rather than with ??=: the
                // latter fills in a token source which is missing, never one
                // which has already been cancelled.
                var clientToken = LiveClientCancellationTokenSource.Token;

                // Send the data
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Text), clientToken).ConfigureAwait(false);
                await stream.FlushAsync(clientToken).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer = new Byte[8192];
                var bytesRead = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, clientToken).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), clientToken).ConfigureAwait(false);
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

            // A token computed without the keying material and labelled as bound
            // is refused by every verifier that binds - and was what this sent.
            if (TOTPConfig.TLSChannelBinding == true && TLSExporterMaterial is null)
                throw new InvalidOperationException(TOTPConfig.TLSChannelBindingNotAvailable);

            var (current, _, _)  = TOTPGenerator.GenerateTOTP(
                                       TOTPConfig.SharedSecret,
                                       TOTPConfig.ValidityTime,
                                       TOTPConfig.Length,
                                       TOTPConfig.Alphabet,
                                       null,
                                       TOTPConfig.TLSChannelBinding == true
                                           ? TLSExporterMaterial
                                           : null,
                                       TOTPConfig.HashAlgorithm
                                   );

            return new TOTPHTTPHeader(
                       TOTPConfig.TLSChannelBinding == true
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

            try { TLSStream?.Dispose(); } catch { }
            tlsStream              = null;
            rawPublicKeyTLSStream  = null;
            TLSSession             = null;

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
