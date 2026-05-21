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

using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public enum DNSHTTPSMode
    {
        GET,
        POST,
        JSON
    }


    /// <summary>
    /// A DNS HTTPS client for a single DNS server.
    /// 
    /// Can be A GET request with a base64url-encoded DNS query in the URL,
    /// or a POST request with the DNS query in the body.
    /// 
    /// DNS JSON requests/responses are also supported by Google and Cloudflare.
    /// </summary>
    public class DNSHTTPSClient : AHTTPClient,
                                  IDNSClientWithTransport
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

        private readonly SemaphoreSlim httpStreamLock = new(1, 1);
        private readonly ILogger<DNSHTTPSClient> logger;

        public new const  String DefaultHTTPUserAgent    = "Hermod DNS HTTP Test Client";

        #endregion

        #region Properties

        /// <summary>
        /// The DNS request mode.
        /// </summary>
        public DNSHTTPSMode  Mode                { get; set; }

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?      RecursionDesired    { get; set; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan      QueryTimeout        { get; set; }

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions      { get; } = [];

        #endregion

        #region Constructor(s)

        #region DNSHTTPSClient(TCPPort, ...)

        /// <summary>
        /// Create a new DNS HTTPS client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSHTTPSClient(IPPort                                                     TCPPort,
                              I18NString?                                                Description                          = null,
                              DNSHTTPSMode?                                              Mode                                 = null,
                              Boolean?                                                   RecursionDesired                     = null,
                              TimeSpan?                                                  QueryTimeout                         = null,

                              String?                                                    HTTPUserAgent                        = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                              String?                                                    TLSHostname                          = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                              SslProtocols?                                              TLSProtocols                         = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                           CertificateChainPolicy               = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                              Boolean?                                                   AllowRenegotiation                   = null,
                              Boolean?                                                   AllowTLSResume                       = null,

                              IPVersionPreference?                                       PreferIPv4                           = null,
                              TimeSpan?                                                  ConnectTimeout                       = null,
                              TimeSpan?                                                  ReceiveTimeout                       = null,
                              TimeSpan?                                                  SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                              UInt16?                                                    MaxNumberOfRetries                   = null,
                              UInt32?                                                    BufferSize                           = null,

                              Boolean?                                                   DisableLogging                       = null,
                              ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                              ILoggerFactory?                                            LoggerFactory                        = null)

            : base(IPvXAddress.Localhost,
                   TCPPort,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   HTTPAuthentication,
                   null,
                   null,
                   null,
                   null,

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
                   null,
                   null,
                   null,
                   null,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   true,
                   null,
                   AllowRenegotiation,
                   AllowTLSResume,
                   null,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096,

                   true,
                   true,

                   DisableLogging,
                   LoggerFactory: LoggerFactory)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSHTTPSClient>();

        }

        #endregion

        #region DNSHTTPSClient(IPAddress, ...)

        /// <summary>
        /// Create a new DNS HTTPS client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSHTTPSClient(IIPAddress                                                 IPAddress,
                              IPPort?                                                    TCPPort                              = null,
                              I18NString?                                                Description                          = null,
                              DNSHTTPSMode?                                              Mode                                 = null,
                              Boolean?                                                   RecursionDesired                     = null,
                              TimeSpan?                                                  QueryTimeout                         = null,

                              String?                                                    HTTPUserAgent                        = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                              String?                                                    TLSHostname                          = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                              SslProtocols?                                              TLSProtocols                         = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                           CertificateChainPolicy               = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                              Boolean?                                                   AllowRenegotiation                   = null,
                              Boolean?                                                   AllowTLSResume                       = null,

                              IPVersionPreference?                                       PreferIPv4                           = null,
                              TimeSpan?                                                  ConnectTimeout                       = null,
                              TimeSpan?                                                  ReceiveTimeout                       = null,
                              TimeSpan?                                                  SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                              UInt16?                                                    MaxNumberOfRetries                   = null,
                              UInt32?                                                    BufferSize                           = null,

                              Boolean?                                                   DisableLogging                       = null,
                              ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                              ILoggerFactory?                                            LoggerFactory                        = null)

            : base(IPAddress,
                   TCPPort       ?? IPPort.HTTPS,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   HTTPAuthentication,
                   null,
                   null,
                   null,
                   null,

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
                   null,
                   null,
                   null,
                   null,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   true,
                   null,
                   AllowRenegotiation,
                   AllowTLSResume,
                   null,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 4096,

                   true,
                   true,

                   DisableLogging,
                   LoggerFactory: LoggerFactory)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSHTTPSClient>();

        }

        #endregion

        #region DNSHTTPSClient(URL, ...)

        /// <summary>
        /// Create a new DNS HTTPS client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSHTTPSClient(URL                                                        URL,
                              I18NString?                                                Description                          = null,
                              DNSHTTPSMode?                                              Mode                                 = null,
                              Boolean?                                                   RecursionDesired                     = null,
                              TimeSpan?                                                  QueryTimeout                         = null,

                              String?                                                    HTTPUserAgent                        = null,
                              IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                              String?                                                    TLSHostname                          = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                              SslProtocols?                                              TLSProtocols                         = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                           CertificateChainPolicy               = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                              Boolean?                                                   AllowRenegotiation                   = null,
                              Boolean?                                                   AllowTLSResume                       = null,

                              IPVersionPreference?                                       PreferIPv4                           = null,
                              TimeSpan?                                                  ConnectTimeout                       = null,
                              TimeSpan?                                                  ReceiveTimeout                       = null,
                              TimeSpan?                                                  SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                              UInt16?                                                    MaxNumberOfRetries                   = null,
                              UInt32?                                                    BufferSize                           = null,

                              Boolean?                                                   DisableLogging                       = null,
                              DNSClient?                                                 DNSClient                            = null,
                              ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                              ILoggerFactory?                                            LoggerFactory                        = null)

            : base(URL,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   HTTPAuthentication,
                   null,  // Accept
                   null,  // ContentType
                   null,  // Connection
                   null,  // DefaultRequestBuilder

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          aHTTPTestClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               aHTTPTestClient as IHTTPClient,
                                               policyErrors
                                           )
                       : null,
                   null,  // LocalCertificateSelector
                   null,  // ClientCertificates
                   null,  // ClientCertificateContext
                   null,  // ClientCertificateChain
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   null,  // ApplicationProtocols
                   AllowRenegotiation,
                   AllowTLSResume,
                   null,  // TOTPConfig

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize  ?? 4096,

                   true,  // ConsumeRequestChunkedTEImmediately
                   true,  // ConsumeResponseChunkedTEImmediately

                   DisableLogging,
                   DNSClient,
                   LoggerFactory: LoggerFactory)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);
            this.logger            = Logger           ?? (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSHTTPSClient>();

            RemotePort ??= URL.Port ?? IPPort.HTTPS;

        }

        #endregion

        #endregion


        #region ConnectNew (IPAddress, ...)

        /// <summary>
        /// Create a new DNSHTTPSClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(DNSHTTPSClient?, TCPConnectionResult)>

            ConnectNew(IIPAddress                                                 IPAddress,
                       IPPort?                                                    TCPPort                              = null,
                       I18NString?                                                Description                          = null,
                       DNSHTTPSMode?                                              Mode                                 = null,
                       Boolean?                                                   RecursionDesired                     = null,
                       TimeSpan?                                                  QueryTimeout                         = null,

                       String?                                                    HTTPUserAgent                        = null,
                       IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                       String?                                                    TLSHostname                          = null,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidationHandler   = null,
                       SslProtocols?                                              TLSProtocols                         = null,
                       CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                       X509ChainPolicy?                                           CertificateChainPolicy               = null,
                       X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                       Boolean?                                                   AllowRenegotiation                   = null,
                       Boolean?                                                   AllowTLSResume                       = null,

                       IPVersionPreference?                                       PreferIPv4                           = null,
                       TimeSpan?                                                  ConnectTimeout                       = null,
                       TimeSpan?                                                  ReceiveTimeout                       = null,
                       TimeSpan?                                                  SendTimeout                          = null,
                       TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                       UInt16?                                                    MaxNumberOfRetries                   = null,
                       UInt32?                                                    BufferSize                           = null,

                       Boolean?                                                   DisableLogging                       = null,
                       ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                       ILoggerFactory?                                            LoggerFactory                        = null)

        {

            var client = new DNSHTTPSClient(

                             IPAddress,
                             TCPPort,
                             Description,
                             Mode,
                             RecursionDesired,
                             QueryTimeout,

                             HTTPUserAgent,
                             HTTPAuthentication,

                             TLSHostname,
                             RemoteCertificateValidationHandler,
                             TLSProtocols,
                             CipherSuitesPolicy,
                             CertificateChainPolicy,
                             CertificateRevocationCheckMode,
                             AllowRenegotiation,
                             AllowTLSResume,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,

                             DisableLogging,
                             Logger,
                             LoggerFactory

                         );

            var response = await client.ConnectAsync();

            return (client, response);

        }

        #endregion

        #region ConnectNew (URL, ..., DNSClient = null)

        /// <summary>
        /// Create a new DNSHTTPSClient and connect to the given URL.
        /// </summary>
        /// <param name="URL">The URL to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(DNSHTTPSClient?, TCPConnectionResult)>

            ConnectNew(URL                                                        URL,
                       I18NString?                                                Description                          = null,
                       DNSHTTPSMode?                                              Mode                                 = null,
                       Boolean?                                                   RecursionDesired                     = null,
                       TimeSpan?                                                  QueryTimeout                         = null,

                       String?                                                    HTTPUserAgent                        = null,
                       IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                       String?                                                    TLSHostname                          = null,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidationHandler   = null,
                       SslProtocols?                                              TLSProtocols                         = null,
                       CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                       X509ChainPolicy?                                           CertificateChainPolicy               = null,
                       X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                       Boolean?                                                   AllowRenegotiation                   = null,
                       Boolean?                                                   AllowTLSResume                       = null,

                       IPVersionPreference?                                       PreferIPv4                           = null,
                       TimeSpan?                                                  ConnectTimeout                       = null,
                       TimeSpan?                                                  ReceiveTimeout                       = null,
                       TimeSpan?                                                  SendTimeout                          = null,
                       TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                       UInt16?                                                    MaxNumberOfRetries                   = null,
                       UInt32?                                                    BufferSize                           = null,

                       Boolean?                                                   DisableLogging                       = null,
                       DNSClient?                                                 DNSClient                            = null,
                       ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                       ILoggerFactory?                                            LoggerFactory                        = null)

        {

            var client = new DNSHTTPSClient(

                             URL,
                             Description,
                             Mode,
                             RecursionDesired,
                             QueryTimeout,

                             HTTPUserAgent,
                             HTTPAuthentication,

                             TLSHostname,
                             RemoteCertificateValidationHandler,
                             TLSProtocols,
                             CipherSuitesPolicy,
                             CertificateChainPolicy,
                             CertificateRevocationCheckMode,
                             AllowRenegotiation,
                             AllowTLSResume,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,

                             DisableLogging,
                             DNSClient,
                             Logger,
                             LoggerFactory

                         );

            var response = await client.ConnectAsync();

            return (client, response);

        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default)

            => QueryHTTP(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   BypassCache,
                   null,
                   null,
                   CancellationToken
               );


        public Task<DNSInfo> QueryHTTP(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout                   = null,
                                       Boolean?                             RecursionDesired          = true,
                                       Boolean?                             BypassCache               = false,
                                       ClientRequestLogHandler?             HTTPRequestLogDelegate    = null,
                                       ClientResponseLogHandler?            HTTPResponseLogDelegate   = null,
                                       CancellationToken                    CancellationToken         = default)

            => QueryHTTP(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   BypassCache,
                   HTTPRequestLogDelegate,
                   HTTPResponseLogDelegate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, RecursionDesired = true, BypassCache = false, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default)

            => QueryHTTP(
                   DNSServiceName,
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   BypassCache,
                   null,
                   null,
                   CancellationToken
               );

        public async Task<DNSInfo> QueryHTTP(DNSServiceName                       DNSServiceName,
                                             IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                             TimeSpan?                            Timeout                   = null,
                                             Boolean?                             RecursionDesired          = true,
                                             Boolean?                             BypassCache               = false,
                                             ClientRequestLogHandler?             HTTPRequestLogDelegate    = null,
                                             ClientResponseLogHandler?            HTTPResponseLogDelegate   = null,
                                             CancellationToken                    CancellationToken         = default)
        {

            #region Initial checks

            var stopwatch = Stopwatch.StartNew();

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            // The DNS JSON API supports only one record type per query parameter.
            // If multiple types are requested, fan out to sequential queries and merge results.
            if (Mode == DNSHTTPSMode.JSON && resourceRecordTypes.Count > 1)
                return await QueryHTTPMultiTypeJSONAsync(
                                 DNSServiceName,
                                 resourceRecordTypes,
                                 Timeout,
                                 RecursionDesired,
                                 BypassCache,
                                 HTTPRequestLogDelegate,
                                 HTTPResponseLogDelegate,
                                 CancellationToken
                             ).ConfigureAwait(false);


            var dnsQuery  = DNSPacket.Query(
                                DNSServiceName,
                                0,
                                this.RecursionDesired ?? RecursionDesired ?? true,
                                EDNSOptions.Count > 0 ? EDNSOptions : null,
                                [.. resourceRecordTypes]
                            );

            var dnsBytes  = dnsQuery.ToByteArray();

            var effectiveTimeout = Timeout ?? QueryTimeout;

            await httpStreamLock.WaitAsync(CancellationToken).
                                 ConfigureAwait(false);

            try
            {

                if (!IsConnected || tcpClient is null)
                    await ReconnectAsync(CancellationToken).ConfigureAwait(false);

                stopwatch.Restart();
                clientCancellationTokenSource ??= new CancellationTokenSource();

                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(
                                           clientCancellationTokenSource.Token,
                                           CancellationToken
                                       );
                timeoutCTS.CancelAfter(effectiveTimeout);

                var httpRequestBuilder = DefaultRequestBuilder(this);

                httpRequestBuilder.SetHost(RemoteURL.Hostname);

                if (Mode == DNSHTTPSMode.GET)
                {
                    httpRequestBuilder.HTTPMethod     = HTTPMethod.GET;
                    httpRequestBuilder.Path           = RemoteURL.Path;
                    httpRequestBuilder.Accept         = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.DNSMessage);
                    httpRequestBuilder.QueryString.Add("dns", dnsBytes.ToBase64URL());
                }
                else if (Mode == DNSHTTPSMode.POST)
                {
                    httpRequestBuilder.HTTPMethod     = HTTPMethod.POST;
                    httpRequestBuilder.Path           = RemoteURL.Path;
                    httpRequestBuilder.Accept         = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.DNSMessage);
                    httpRequestBuilder.ContentType    = HTTPContentType.Application.DNSMessage;
                    httpRequestBuilder.ContentLength  = (UInt64) dnsBytes.Length;
                    httpRequestBuilder.Content        = dnsBytes;
                }
                else if (Mode == DNSHTTPSMode.JSON)
                {
                    httpRequestBuilder.HTTPMethod     = HTTPMethod.GET;
                    httpRequestBuilder.Path           = RemoteURL.Path;
                    httpRequestBuilder.Accept         = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.DNSJson);
                    httpRequestBuilder.QueryString.Add("name", DNSServiceName.ToString().TrimEnd('.'));
                    httpRequestBuilder.QueryString.Add("type", ((UInt16) resourceRecordTypes.First()).ToString());
                }

                var httpRequest = httpRequestBuilder.AsImmutable;

                HTTPResponse httpResponse;

                try
                {
                    httpResponse = await SendRequest(
                                             httpRequest,
                                             true,
                                             HTTPRequestLogDelegate,
                                             HTTPResponseLogDelegate,
                                             CancellationToken: timeoutCTS.Token
                                         );
                }
                catch (IOException)
                {
                    await ReconnectAsync(CancellationToken).ConfigureAwait(false);

                    httpResponse = await SendRequest(
                                             httpRequest,
                                             true,
                                             HTTPRequestLogDelegate,
                                             HTTPResponseLogDelegate,
                                             CancellationToken: timeoutCTS.Token
                                         );
                }

                stopwatch.Stop();

                var serverConfig = new DNSServerConfig(
                                       RemoteIPAddress!,
                                       RemotePort ?? IPPort.HTTPS,
                                       DNSTransport.HTTPS,
                                       effectiveTimeout
                                   );

                // Check HTTP status code before attempting to parse the response body.
                // Non-2xx responses (429 Rate Limit, 403 Forbidden, 503 Unavailable, etc.)
                // must not be fed to the DNS wire-format parser.
                if (httpResponse.HTTPStatusCode.Code < 200 ||
                    httpResponse.HTTPStatusCode.Code >= 300)
                {

                    logger.LogWarning(
                        "DNS HTTPS query to {RemoteIPAddress}:{RemotePort} returned HTTP {HTTPStatusCode}",
                        RemoteIPAddress,
                        RemotePort,
                        httpResponse.HTTPStatusCode.Code
                    );

                    return DNSInfo.Failed(
                               serverConfig,
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                // JSON mode: parse the JSON response
                if (Mode == DNSHTTPSMode.JSON &&
                    TryParseJSONResponse(
                        serverConfig,
                        httpResponse,
                        out var dnsInfo,
                        effectiveTimeout,
                        stopwatch.Elapsed
                    ))
                {
                    return dnsInfo;
                }

                // Binary mode: parse the wire-format response
                var body = httpResponse.HTTPBody ?? [];

                if (body.Length < 12)
                {

                    logger.LogWarning(
                        "DNS HTTPS response from {RemoteIPAddress}:{RemotePort} too short: {Length} bytes, minimum 12",
                        RemoteIPAddress,
                        RemotePort,
                        body.Length
                    );

                    return DNSInfo.Failed(
                               serverConfig,
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                return DNSInfo.ReadResponse(
                           serverConfig,
                           dnsQuery.TransactionId,
                           new MemoryStream(body),
                           effectiveTimeout,
                           stopwatch.Elapsed
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.HTTPS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (OperationCanceledException)
            {

                // External cancellation (race-cancel or caller-initiated).
                // Silent return — not a real failure.
                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.HTTPS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (Exception ex)
            {

                logger.LogError(
                    ex,
                    "DNS HTTPS query to {RemoteIPAddress}:{RemotePort} failed",
                    RemoteIPAddress,
                    RemotePort
                );

                await Log($"DNS HTTPS query to {RemoteIPAddress}:{RemotePort} failed: [{ex.GetType().Name}] {ex.Message}");

                return DNSInfo.Failed(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.HTTPS
                           ),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            finally
            {
                httpStreamLock.Release();
            }

        }

        #endregion

        #region (private) QueryHTTPMultiTypeJSONAsync(...)

        /// <summary>
        /// The DNS JSON API supports only one record type per query parameter.
        /// When multiple types are requested, this method fans out to sequential
        /// single-type queries and merges the results into a single DNSInfo.
        /// Sequential because we share one HTTP/1.1 connection with a semaphore.
        /// </summary>
        private async Task<DNSInfo> QueryHTTPMultiTypeJSONAsync(DNSServiceName                       DNSServiceName,
                                                                List<DNSResourceRecordTypes>          ResourceRecordTypes,
                                                                TimeSpan?                            Timeout,
                                                                Boolean?                             RecursionDesired,
                                                                Boolean?                             BypassCache,
                                                                ClientRequestLogHandler?             HTTPRequestLogDelegate,
                                                                ClientResponseLogHandler?            HTTPResponseLogDelegate,
                                                                CancellationToken                    CancellationToken)
        {

            // Fan out: one query per record type (sequentially, since we share one HTTP/1.1 connection)
            var allAnswers     = new List<IDNSResourceRecord>();
            var allAuthorities = new List<IDNSResourceRecord>();
            DNSInfo? lastResponse = null;

            foreach (var recordType in ResourceRecordTypes)
            {

                var response = await QueryHTTP(
                                         DNSServiceName,
                                         [ recordType ],
                                         Timeout,
                                         RecursionDesired,
                                         BypassCache,
                                         HTTPRequestLogDelegate,
                                         HTTPResponseLogDelegate,
                                         CancellationToken
                                     ).ConfigureAwait(false);

                lastResponse = response;

                if (response.ResponseCode == DNSResponseCodes.NoError)
                {
                    allAnswers.    AddRange(response.Answers);
                    allAuthorities.AddRange(response.Authorities);
                }

                // On NXDOMAIN, the domain itself doesn't exist — no point querying further types
                if (response.ResponseCode == DNSResponseCodes.NameError)
                    return response;

            }

            if (lastResponse is null)
                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               RemoteIPAddress!,
                               RemotePort ?? IPPort.HTTPS
                           ),
                           0,
                           Timeout ?? QueryTimeout
                       );

            return new DNSInfo(
                       Origin:                 lastResponse.Origin,
                       QueryId:                lastResponse.QueryId,
                       IsAuthoritativeAnswer:  lastResponse.AuthoritativeAnswer,
                       IsTruncated:            lastResponse.IsTruncated,
                       RecursionDesired:       lastResponse.RecursionRequested,
                       RecursionAvailable:     lastResponse.RecursionAvailable,
                       ResponseCode:           lastResponse.ResponseCode,
                       Answers:                allAnswers,
                       Authorities:            allAuthorities,
                       AdditionalRecords:      [],
                       IsValid:                lastResponse.IsValid,
                       IsTimeout:              lastResponse.IsTimeout,
                       Timeout:                lastResponse.Timeout,
                       Runtime:                lastResponse.Runtime
                   );

        }

        #endregion


        #region (private static) TryParseJSONResponse(ServerConfig, HTTPResponse, out DNSInfo)

        /// <summary>
        /// Parse a DNS JSON response (application/dns-json) as returned by
        /// Google (https://dns.google/resolve) and Cloudflare (https://cloudflare-dns.com/dns-query).
        /// </summary>
        private static Boolean TryParseJSONResponse(DNSServerConfig                   ServerConfig,
                                                    HTTPResponse                      HTTPResponse,
                                                    [NotNullWhen(true)] out DNSInfo?  DNSInfo,
                                                    TimeSpan                          Timeout,
                                                    TimeSpan                          Runtime)
        {

            var body = HTTPResponse.HTTPBodyAsUTF8String?.Trim();

            if (String.IsNullOrEmpty(body))
            {

                DNSInfo = new DNSInfo(
                              Origin:                 ServerConfig,
                              QueryId:                0,
                              IsAuthoritativeAnswer:  false,
                              IsTruncated:            false,
                              RecursionDesired:       true,
                              RecursionAvailable:     false,
                              ResponseCode:           DNSResponseCodes.ServerFailure,
                              Answers:                [],
                              Authorities:            [],
                              AdditionalRecords:      [],
                              IsValid:                false,
                              IsTimeout:              false,
                              Timeout:                Timeout,
                              Runtime:                TimeSpan.Zero
                          );

                return false;

            }

            var json = JObject.Parse(body);

            var status = json["Status"]?.Value<Int32>() ?? 2;
            var tc     = json["TC"]?.    Value<Boolean>() ?? false;
            var rd     = json["RD"]?.    Value<Boolean>() ?? true;
            var ra     = json["RA"]?.    Value<Boolean>() ?? false;

            var answers     = new List<IDNSResourceRecord>();
            var authorities = new List<IDNSResourceRecord>();

            if (json["Answer"] is JArray answerArray)
                foreach (var record in answerArray)
                {
                    if (TryParseJSONResourceRecord(record, out var dnsResourceRecord))
                        answers.Add(dnsResourceRecord);
                }

            if (json["Authority"] is JArray authorityArray)
                foreach (var record in authorityArray)
                {
                    if (TryParseJSONResourceRecord(record, out var dnsResourceRecord))
                        authorities.Add(dnsResourceRecord);
                }

            DNSInfo = new DNSInfo(
                          Origin:                 ServerConfig,
                          QueryId:                0,
                          IsAuthoritativeAnswer:  false,
                          IsTruncated:            tc,
                          RecursionDesired:       rd,
                          RecursionAvailable:     ra,
                          ResponseCode:           (DNSResponseCodes) status,
                          Answers:                answers,
                          Authorities:            authorities,
                          AdditionalRecords:      [],
                          IsValid:                true,
                          IsTimeout:              false,
                          Timeout:                Timeout,
                          Runtime:                Runtime
                      );

            return true;

        }

        #endregion

        #region (private static) TryParseJSONResourceRecord(Record, out ResourceRecord)

        /// <summary>
        /// Parse a single resource record from the DNS JSON response.
        /// Delegates to the static TryParseFromJSON factory method on each
        /// concrete resource record class, keeping parsing logic co-located
        /// with the record type definition.
        /// </summary>
        private static Boolean TryParseJSONResourceRecord(JToken                                       Record,
                                                          [NotNullWhen(true)] out IDNSResourceRecord?  ResourceRecord)
        {

            ResourceRecord      = null;

            var name            = Record["name"]?.Value<String>() ?? "";
            var type            = Record["type"]?.Value<UInt16>() ?? 0;
            var ttl             = Record["TTL"]?. Value<UInt32>() ?? 0;
            var data            = Record["data"]?.Value<String>() ?? "";

            // Ensure the domain name ends with a dot (FQDN)
            if (!name.EndsWith('.'))
                name           += ".";

            // DNSServiceName is more permissive than DomainName (allows underscores in labels,
            // e.g. _ocpp._tcp.api.charging.cloud. for SRV records). Since ADNSResourceRecord
            // stores the name as DNSServiceName internally, we use it as the common parser.
            var dnsServiceName  = DNSServiceName.TryParse   (name);
            var domainName      = DomainName.    TryParse   (name);
            var timeToLive      = TimeSpan.      FromSeconds(ttl);

            if (domainName is not null)
                ResourceRecord  = (DNSResourceRecordTypes) type switch {

                    // Standard record types
                    DNSResourceRecordTypes.A           => A.         TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.AAAA        => AAAA.      TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CNAME       => CNAME.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.MX          => MX.        TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.TXT         => TXT.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.NS          => NS.        TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.SOA         => SOA.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.PTR         => PTR.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.DNAME       => DNAME.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.NAPTR       => NAPTR.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.SPF         => SPF.       TryParseFromJSON(domainName,  timeToLive, data),

                    // Service binding record types (RFC 9460)
                    DNSResourceRecordTypes.HTTPS       => HTTPS.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.SVCB        => SVCB.      TryParseFromJSON(domainName,  timeToLive, data),

                    // DNSSEC record types (RFC 4033/4034/4035)
                    DNSResourceRecordTypes.DS          => DS.        TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.RRSIG       => RRSIG.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.NSEC        => NSEC.      TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.DNSKEY      => DNSKEY.    TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.NSEC3       => NSEC3.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.NSEC3PARAM  => NSEC3PARAM.TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CDS         => CDS.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CDNSKEY     => CDNSKEY.   TryParseFromJSON(domainName,  timeToLive, data),

                    // Security / certificate record types
                    DNSResourceRecordTypes.TLSA        => TLSA.      TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.SMIMEA      => SMIMEA.    TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CERT        => CERT.      TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.OPENPGPKEY  => OPENPGPKEY.TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CAA         => CAA.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.SSHFP       => SSHFP.     TryParseFromJSON(domainName,  timeToLive, data),

                    // Other standard record types
                    DNSResourceRecordTypes.HINFO       => HINFO.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.RP          => RP.        TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.AFSDB       => AFSDB.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.LOC         => LOC.       TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.CSYNC       => CSYNC.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.ZONEMD      => ZONEMD.    TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.EUI48       => EUI48.     TryParseFromJSON(domainName,  timeToLive, data),
                    DNSResourceRecordTypes.EUI64       => EUI64.     TryParseFromJSON(domainName,  timeToLive, data),

                    _                                  => null

                };

            if (ResourceRecord is null && dnsServiceName is not null)
                ResourceRecord  = (DNSResourceRecordTypes) type switch {

                    // Standard record types
                    DNSResourceRecordTypes.SRV         => SRV.       TryParseFromJSON(dnsServiceName, timeToLive, data),

                    // Service binding record types (RFC 9460)
                    DNSResourceRecordTypes.URI         => URI.       TryParseFromJSON(dnsServiceName, timeToLive, data),

                    _                                  => null

                };

            return ResourceRecord is not null;

        }

        #endregion


        #region Google DNS

        public static DNSHTTPSClient Google(DNSHTTPSMode?                                              Mode                                 = null,
                                            Boolean?                                                   RecursionDesired                     = null,
                                            TimeSpan?                                                  QueryTimeout                         = null,

                                            String?                                                    TLSHostname                          = null,
                                            RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                                            SslProtocols?                                              TLSProtocols                         = null,
                                            CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                                            X509ChainPolicy?                                           CertificateChainPolicy               = null,
                                            X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                                            Boolean?                                                   AllowRenegotiation                   = null,
                                            Boolean?                                                   AllowTLSResume                       = null,

                                            IPVersionPreference?                                       PreferIPv4                           = null,
                                            TimeSpan?                                                  ConnectTimeout                       = null,
                                            TimeSpan?                                                  ReceiveTimeout                       = null,
                                            TimeSpan?                                                  SendTimeout                          = null,
                                            TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                                            UInt16?                                                    MaxNumberOfRetries                   = null,
                                            UInt32?                                                    BufferSize                           = null,
                                            String?                                                    HTTPUserAgent                        = null,

                                            Boolean?                                                   DisableLogging                       = null,
                                            DNSClient?                                                 DNSClient                            = null,
                                            ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                                            ILoggerFactory?                                            LoggerFactory                        = null)

            => new (
                   URL.Parse(Mode == DNSHTTPSMode.JSON
                                 ? "https://dns.google/resolve"
                                 : "https://dns.google/dns-query"),
                   I18NString.Create("Google"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,
                   null,

                   TLSHostname,
                   RemoteCertificateValidator,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory
               );

        #endregion

        #region Cloudflare DNS

        public static DNSHTTPSClient Cloudflare_DNSName(DNSHTTPSMode?                                              Mode                                 = null,
                                                        Boolean?                                                   RecursionDesired                     = null,
                                                        TimeSpan?                                                  QueryTimeout                         = null,

                                                        String?                                                    HTTPUserAgent                        = null,

                                                        String?                                                    TLSHostname                          = null,
                                                        RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                                                        SslProtocols?                                              TLSProtocols                         = null,
                                                        CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                                                        X509ChainPolicy?                                           CertificateChainPolicy               = null,
                                                        X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                                                        Boolean?                                                   AllowRenegotiation                   = null,
                                                        Boolean?                                                   AllowTLSResume                       = null,

                                                        IPVersionPreference?                                       PreferIPv4                           = null,
                                                        TimeSpan?                                                  ConnectTimeout                       = null,
                                                        TimeSpan?                                                  ReceiveTimeout                       = null,
                                                        TimeSpan?                                                  SendTimeout                          = null,
                                                        TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                                                        UInt16?                                                    MaxNumberOfRetries                   = null,
                                                        UInt32?                                                    BufferSize                           = null,

                                                        Boolean?                                                   DisableLogging                       = null,
                                                        DNSClient?                                                 DNSClient                            = null,
                                                        ILogger<DNSHTTPSClient>?                                   Logger                               = null,
                                                        ILoggerFactory?                                            LoggerFactory                        = null)

            => new (
                   URL.Parse("https://one.one.one.one/dns-query"),
                   I18NString.Create("Cloudflare (one.one.one.one)"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,
                   null,

                   TLSHostname,
                   RemoteCertificateValidator,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory
               );

        public static DNSHTTPSClient Cloudflare_IPv4_1(DNSHTTPSMode?                                              Mode                                 = null,
                                                       Boolean?                                                   RecursionDesired                     = null,
                                                       TimeSpan?                                                  QueryTimeout                         = null,

                                                       String?                                                    HTTPUserAgent                        = null,

                                                       String?                                                    TLSHostname                          = null,
                                                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidationHandler   = null,
                                                       SslProtocols?                                              TLSProtocols                         = null,
                                                       CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                                                       X509ChainPolicy?                                           CertificateChainPolicy               = null,
                                                       X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                                                       Boolean?                                                   AllowRenegotiation                   = null,
                                                       Boolean?                                                   AllowTLSResume                       = null,

                                                       IPVersionPreference?                                       PreferIPv4                           = null,
                                                       TimeSpan?                                                  ConnectTimeout                       = null,
                                                       TimeSpan?                                                  ReceiveTimeout                       = null,
                                                       TimeSpan?                                                  SendTimeout                          = null,
                                                       TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                                                       UInt16?                                                    MaxNumberOfRetries                   = null,
                                                       UInt32?                                                    BufferSize                           = null,

                                                       Boolean?                                                   DisableLogging                       = null,
                                                       DNSClient?                                                 DNSClient                            = null)

            => new (
                   URL.Parse("https://1.1.1.1/dns-query"),
                   I18NString.Create("Cloudflare (1.1.1.1)"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,
                   null,

                   TLSHostname,
                   RemoteCertificateValidationHandler,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient
               );

        public static DNSHTTPSClient Cloudflare_IPv4_2(DNSHTTPSMode?                                              Mode                                 = null,
                                                       Boolean?                                                   RecursionDesired                     = null,
                                                       TimeSpan?                                                  QueryTimeout                         = null,

                                                       String?                                                    HTTPUserAgent                        = null,

                                                       String?                                                    TLSHostname                          = null,
                                                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidationHandler   = null,
                                                       SslProtocols?                                              TLSProtocols                         = null,
                                                       CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                                                       X509ChainPolicy?                                           CertificateChainPolicy               = null,
                                                       X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                                                       Boolean?                                                   AllowRenegotiation                   = null,
                                                       Boolean?                                                   AllowTLSResume                       = null,

                                                       IPVersionPreference?                                       PreferIPv4                           = null,
                                                       TimeSpan?                                                  ConnectTimeout                       = null,
                                                       TimeSpan?                                                  ReceiveTimeout                       = null,
                                                       TimeSpan?                                                  SendTimeout                          = null,
                                                       TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                                                       UInt16?                                                    MaxNumberOfRetries                   = null,
                                                       UInt32?                                                    BufferSize                           = null,

                                                       Boolean?                                                   DisableLogging                       = null,
                                                       DNSClient?                                                 DNSClient                            = null)

            => new (
                   URL.Parse("https://1.0.0.1/dns-query"),
                   I18NString.Create("Cloudflare (1.0.0.1)"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,
                   null,

                   TLSHostname,
                   RemoteCertificateValidationHandler,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   DisableLogging,
                   DNSClient
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Using DNS server: {RemoteIPAddress}:{RemotePort}";

        #endregion


        public override async ValueTask DisposeAsync()
        {
            httpStreamLock.Dispose();
            await base.DisposeAsync();
        }


    }

}
