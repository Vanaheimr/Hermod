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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

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

                              Boolean?                                                   DisableLogging                       = null)

            : base(IPvXAddress.Localhost,
                   TCPPort,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,
                   null,
                   null,
                   null,

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

                   DisableLogging)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

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

                              Boolean?                                                   DisableLogging                       = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTPS,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,
                   null,
                   null,
                   null,

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

                   DisableLogging)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

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

                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator           = null,
                              SslProtocols?                                              TLSProtocols                         = null,
                              CipherSuitesPolicy?                                        CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                           CertificateChainPolicy               = null,
                              X509RevocationMode?                                        CertificateRevocationCheckMode       = null,
                              Boolean?                                                   AllowRenegotiation                   = null,
                              Boolean?                                                   AllowTLSResume                       = null,

                              IHTTPAuthentication?                                       HTTPAuthentication                   = null,

                              IPVersionPreference?                                       PreferIPv4                           = null,
                              TimeSpan?                                                  ConnectTimeout                       = null,
                              TimeSpan?                                                  ReceiveTimeout                       = null,
                              TimeSpan?                                                  SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay               = null,
                              UInt16?                                                    MaxNumberOfRetries                   = null,
                              UInt32?                                                    BufferSize                           = null,

                              Boolean?                                                   DisableLogging                       = null,
                              DNSClient?                                                 DNSClient                            = null)

            : base(URL,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,  // Accept
                   null,  // ContentType
                   null,  // Connection
                   null,  // DefaultRequestBuilder

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

                   HTTPAuthentication,

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
                   DNSClient)

        {

            this.Mode              = Mode             ?? DNSHTTPSMode.GET;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? TimeSpan.FromSeconds(23.5);

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

                       Boolean?                                                   DisableLogging                       = null)

        {

            var client = new DNSHTTPSClient(

                             IPAddress,
                             TCPPort,
                             Description,
                             Mode,
                             RecursionDesired,
                             QueryTimeout,

                             HTTPUserAgent,

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

                             DisableLogging

                         );

            var response = await client.ConnectAsync();

            return (client, response);

        }

        #endregion

        #region ConnectNew (URL, DNSService = null, ..., DNSClient = null)

        /// <summary>
        /// Create a new DNSHTTPSClient and connect to the given URL.
        /// </summary>
        /// <param name="URL">The URL to connect to.</param>
        /// <param name="DNSService">The DNS service to lookup in order to resolve high available IP addresses and TCP ports for the given URL hostname.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(DNSHTTPSClient?, TCPConnectionResult)>

            ConnectNew(URL                                                        URL,
                       SRV_Spec?                                                  DNSService                           = null,
                       I18NString?                                                Description                          = null,
                       DNSHTTPSMode?                                              Mode                                 = null,
                       Boolean?                                                   RecursionDesired                     = null,
                       TimeSpan?                                                  QueryTimeout                         = null,

                       String?                                                    HTTPUserAgent                        = null,

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
                       DNSClient?                                                 DNSClient                            = null,

                       Boolean?                                                   DisableLogging                       = null)

        {

            var client = new DNSHTTPSClient(

                             URL,
                             Description,
                             Mode,
                             RecursionDesired,
                             QueryTimeout,

                             HTTPUserAgent,

                             RemoteCertificateValidationHandler,
                             TLSProtocols,
                             CipherSuitesPolicy,
                             CertificateChainPolicy,
                             CertificateRevocationCheckMode,
                             AllowRenegotiation,
                             AllowTLSResume,

                             null,

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

                var stopwatch = Stopwatch.StartNew();
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

                    DebugX.LogT($"DNS HTTPS query to {RemoteIPAddress}:{RemotePort} returned HTTP {httpResponse.HTTPStatusCode.Code}!");

                    return DNSInfo.Failed(
                               serverConfig,
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                // JSON mode: parse the JSON response
                if (Mode == DNSHTTPSMode.JSON)
                    return ParseJSONResponse(serverConfig, httpResponse);

                // Binary mode: parse the wire-format response
                var body = httpResponse.HTTPBody ?? [];

                if (body.Length < 12)
                {

                    DebugX.LogT($"DNS HTTPS response from {RemoteIPAddress}:{RemotePort} too short ({body.Length} bytes, minimum 12)!");

                    return DNSInfo.Failed(
                               serverConfig,
                               dnsQuery.TransactionId,
                               effectiveTimeout
                           );

                }

                return DNSInfo.ReadResponse(
                           serverConfig,
                           dnsQuery.TransactionId,
                           new MemoryStream(body)
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
            catch (Exception ex)
            {

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
                       Timeout:                lastResponse.Timeout
                   );

        }

        #endregion


        #region (private static) ParseJSONResponse(ServerConfig, HTTPResponse)

        /// <summary>
        /// Parse a DNS JSON response (application/dns-json) as returned by
        /// Google (https://dns.google/resolve) and Cloudflare (https://cloudflare-dns.com/dns-query).
        /// </summary>
        private static DNSInfo ParseJSONResponse(DNSServerConfig  ServerConfig,
                                                 HTTPResponse     HTTPResponse)
        {

            var body = HTTPResponse.HTTPBodyAsUTF8String?.Trim();

            if (String.IsNullOrEmpty(body))
                return new DNSInfo(
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
                           Timeout:                ServerConfig.QueryTimeout ?? TimeSpan.Zero
                       );

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
                    var parsed = ParseJSONResourceRecord(record);
                    if (parsed is not null)
                        answers.Add(parsed);
                }

            if (json["Authority"] is JArray authorityArray)
                foreach (var record in authorityArray)
                {
                    var parsed = ParseJSONResourceRecord(record);
                    if (parsed is not null)
                        authorities.Add(parsed);
                }

            return new DNSInfo(
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
                       Timeout:                ServerConfig.QueryTimeout ?? TimeSpan.Zero
                   );

        }

        #endregion

        #region (private static) ParseJSONResourceRecord(JToken)

        /// <summary>
        /// Parse a single resource record from the DNS JSON response.
        /// </summary>
        private static IDNSResourceRecord? ParseJSONResourceRecord(JToken Record)
        {

            var name = Record["name"]?.Value<String>() ?? "";
            var type = Record["type"]?.Value<UInt16>() ?? 0;
            var ttl  = Record["TTL"]?. Value<UInt32>() ?? 0;
            var data = Record["data"]?.Value<String>() ?? "";

            // Ensure the domain name ends with a dot (FQDN)
            if (!name.EndsWith('.'))
                name += ".";

            // DNSServiceName is more permissive than DomainName (allows underscores in labels,
            // e.g. _ocpp._tcp.api.charging.cloud. for SRV records). Since ADNSResourceRecord
            // stores the name as DNSServiceName internally, we use it as the common parser.
            var serviceName = DNSServiceName.Parse(name);
            var timeToLive  = TimeSpan.FromSeconds(ttl);

            return (DNSResourceRecordTypes) type switch {

                DNSResourceRecordTypes.A
                    => new A(
                           serviceName,
                           DNSQueryClasses.IN,
                           timeToLive,
                           IPv4Address.Parse(data)
                       ),

                DNSResourceRecordTypes.AAAA
                    => new AAAA(
                           serviceName,
                           DNSQueryClasses.IN,
                           timeToLive,
                           IPv6Address.Parse(data)
                       ),

                DNSResourceRecordTypes.CNAME
                    => new CNAME(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           DomainName.Parse(data.EndsWith('.') ? data : data + ".")
                       ),

                DNSResourceRecordTypes.MX
                    => ParseMXFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.TXT
                    => new TXT(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           data.Trim('"')
                       ),

                DNSResourceRecordTypes.NS
                    => new NS(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           DomainName.Parse(data.EndsWith('.') ? data : data + ".")
                       ),

                DNSResourceRecordTypes.SOA
                    => ParseSOAFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.SRV
                    => ParseSRVFromJSON(name, timeToLive, data),

                DNSResourceRecordTypes.PTR
                    => new PTR(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           DNSServiceName.Parse(data.EndsWith('.') ? data : data + ".")
                       ),

                DNSResourceRecordTypes.DNAME
                    => new DNAME(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           DomainName.Parse(data.EndsWith('.') ? data : data + ".")
                       ),

                DNSResourceRecordTypes.NAPTR
                    => ParseNAPTRFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.SSHFP
                    => ParseSSHFPFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.SPF
                    => new SPF(
                           DomainName.Parse(name),
                           DNSQueryClasses.IN,
                           timeToLive,
                           data.Trim('"')
                       ),

                DNSResourceRecordTypes.HTTPS
                    => ParseHTTPSFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.SVCB
                    => ParseSVCBFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.URI
                    => ParseURIFromJSON(serviceName, timeToLive, data),

                // DNSSEC record types
                DNSResourceRecordTypes.DS
                    => ParseDSFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.RRSIG
                    => ParseRRSIGFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.NSEC
                    => ParseNSECFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.DNSKEY
                    => ParseDNSKEYFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.NSEC3
                    => ParseNSEC3FromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.NSEC3PARAM
                    => ParseNSEC3PARAMFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.CDS
                    => ParseCDSFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.CDNSKEY
                    => ParseCDNSKEYFromJSON(DomainName.Parse(name), timeToLive, data),

                // Security / certificate record types
                DNSResourceRecordTypes.TLSA
                    => ParseTLSAFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.SMIMEA
                    => ParseSMIMEAFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.CERT
                    => ParseCERTFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.OPENPGPKEY
                    => ParseOPENPGPKEYFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.CAA
                    => ParseCAAFromJSON(DomainName.Parse(name), timeToLive, data),

                // Other standard record types
                DNSResourceRecordTypes.HINFO
                    => ParseHINFOFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.RP
                    => ParseRPFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.AFSDB
                    => ParseAFSDBFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.LOC
                    => ParseLOCFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.CSYNC
                    => ParseCSYNCFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.ZONEMD
                    => ParseZONEMDFromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.EUI48
                    => ParseEUI48FromJSON(DomainName.Parse(name), timeToLive, data),

                DNSResourceRecordTypes.EUI64
                    => ParseEUI64FromJSON(DomainName.Parse(name), timeToLive, data),

                _ => null

            };

        }

        #endregion

        #region (private static) ParseMXFromJSON (...)

        /// <summary>
        /// Parse an MX record from the JSON "data" field (e.g. "10 mail.example.com.").
        /// </summary>
        private static MX ParseMXFromJSON(DomainName  DomainName,
                                          TimeSpan    TimeToLive,
                                          String      Data)
        {

            var parts      = Data.Split(' ', 2);
            var preference = UInt16.Parse(parts[0]);
            var exchange   = parts[1];

            if (!exchange.EndsWith('.'))
                exchange += ".";

            return new MX(
                       DomainName,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       preference,
                       DNS.DomainName.Parse(exchange)
                   );

        }

        #endregion

        #region (private static) ParseSOAFromJSON(...)

        /// <summary>
        /// Parse a SOA record from the JSON "data" field
        /// (e.g. "ns1.example.com. admin.example.com. 2024010101 3600 900 604800 86400").
        /// </summary>
        private static SOA ParseSOAFromJSON(DomainName  DomainName,
                                            TimeSpan    TimeToLive,
                                            String      Data)
        {

            var parts = Data.Split(' ');

            return new SOA(
                       DomainName,
                       DNSQueryClasses.IN,
                       TimeToLive,
                       DNS.DomainName.Parse(parts[0].EndsWith('.') ? parts[0] : parts[0] + "."),
                       SimpleEMailAddress.Parse(DNSTools.ReplaceFirstDotWithAt(parts[1].TrimEnd('.'))),
                       UInt32.Parse(parts[2]),
                       TimeSpan.FromSeconds(UInt32.Parse(parts[3])),
                       TimeSpan.FromSeconds(UInt32.Parse(parts[4])),
                       TimeSpan.FromSeconds(UInt32.Parse(parts[5])),
                       TimeSpan.FromSeconds(UInt32.Parse(parts[6]))
                   );

        }

        #endregion

        #region (private static) ParseSRVFromJSON(...)

        /// <summary>
        /// Parse a SRV record from the JSON "data" field
        /// (e.g. "10 5 8080 target.example.com.").
        /// The name is passed as a string because SRV names contain underscores
        /// (e.g. _ocpp._tcp.api.charging.cloud.) which DomainName rejects.
        /// </summary>
        private static SRV ParseSRVFromJSON(String    Name,
                                            TimeSpan  TimeToLive,
                                            String    Data)
        {

            var parts = Data.Split(' ');

            return new SRV(
                       DNSServiceName.Parse(Name),
                       DNSQueryClasses.IN,
                       TimeToLive,
                       UInt16.Parse(parts[0]),
                       UInt16.Parse(parts[1]),
                       IPPort.Parse(parts[2]),
                       DNS.DomainName.Parse(parts[3].EndsWith('.') ? parts[3] : parts[3] + ".")
                   );

        }

        #endregion

        #region (private static) TryParseSVCParamKey(Name, out Key)

        /// <summary>
        /// Try to map a well-known SVC parameter name to its numeric key
        /// (RFC 9460 §14.3.2).
        /// </summary>
        private static Boolean TryParseSVCParamKey(String Name, out UInt16 Key)
        {
            Key = Name.ToLowerInvariant() switch {
                "mandatory"       => 0,
                "alpn"            => 1,
                "no-default-alpn" => 2,
                "port"            => 3,
                "ipv4hint"        => 4,
                "ech"             => 5,
                "ipv6hint"        => 6,
                _                 => UInt16.MaxValue
            };
            return Key != UInt16.MaxValue;
        }

        #endregion

        #region (private static) ParseNAPTRFromJSON(...)

        /// <summary>
        /// Parse a NAPTR record from the JSON "data" field
        /// (e.g. "100 10 \"u\" \"E2U+sip\" \"!^.*$!sip:info@example.com!\" .").
        /// </summary>
        private static NAPTR? ParseNAPTRFromJSON(DomainName  DomainName,
                                                 TimeSpan    TimeToLive,
                                                 String      Data)
        {

            try
            {

                // NAPTR data format: Order Preference Flags Services Regexp Replacement
                var parts = Data.Split(' ', 6);
                if (parts.Length < 6)
                    return null;

                var order       = UInt16.Parse(parts[0]);
                var preference  = UInt16.Parse(parts[1]);
                var flags       = parts[2].Trim('"');
                var services    = parts[3].Trim('"');
                var regExpr     = parts[4].Trim('"');
                var replacement = parts[5].TrimEnd('.');

                if (!replacement.EndsWith('.'))
                    replacement += ".";

                return new NAPTR(
                           DomainName,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           order,
                           preference,
                           flags,
                           services,
                           regExpr,
                           DNS.DomainName.Parse(replacement)
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) ParseSSHFPFromJSON(...)

        /// <summary>
        /// Parse an SSHFP record from the JSON "data" field
        /// (e.g. "1 1 AABBCCDD...").
        /// </summary>
        private static SSHFP? ParseSSHFPFromJSON(DomainName  DomainName,
                                                  TimeSpan    TimeToLive,
                                                  String      Data)
        {

            try
            {

                var parts = Data.Split(' ', 3);
                if (parts.Length < 3)
                    return null;

                var algorithm   = (SSHFP_Algorithm)       Byte.Parse(parts[0]);
                var fpType      = (SSHFP_FingerprintType) Byte.Parse(parts[1]);
                var fingerprint = Convert.FromHexString(parts[2].Replace(" ", ""));

                return new SSHFP(
                           DomainName,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           algorithm,
                           fpType,
                           fingerprint
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) ParseHTTPSFromJSON(...)

        /// <summary>
        /// Parse an HTTPS record from the JSON "data" field
        /// (e.g. "1 . alpn=h2,h3").
        /// Since SVC parameters have complex wire-format encoding, we store
        /// the raw presentation-format string for now.
        /// </summary>
        private static HTTPS? ParseHTTPSFromJSON(DomainName  DomainName,
                                                  TimeSpan    TimeToLive,
                                                  String      Data)
        {

            try
            {

                var parts      = Data.Split(' ', 3);
                if (parts.Length < 2)
                    return null;

                var priority   = UInt16.Parse(parts[0]);
                var targetName = parts[1].TrimEnd('.');

                if (targetName == ".")
                    targetName = DomainName.ToString().TrimEnd('.');

                if (!targetName.EndsWith('.'))
                    targetName += ".";

                // Parse SVC parameters from the remaining data (if present)
                var svcParams = new List<SVCParameter>();
                if (parts.Length >= 3 && !String.IsNullOrWhiteSpace(parts[2]))
                {
                    foreach (var param in parts[2].Split(' '))
                    {
                        var kv = param.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            if (UInt16.TryParse(kv[0], out var key) ||
                                TryParseSVCParamKey(kv[0], out key))
                            {
                                svcParams.Add(new SVCParameter(key, System.Text.Encoding.UTF8.GetBytes(kv[1])));
                            }
                        }
                    }
                }

                return new HTTPS(
                           DomainName,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           priority,
                           DNS.DomainName.Parse(targetName),
                           svcParams
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) ParseSVCBFromJSON(...)

        /// <summary>
        /// Parse a SVCB record from the JSON "data" field
        /// (e.g. "1 target.example.com. alpn=h2").
        /// </summary>
        private static SVCB? ParseSVCBFromJSON(DomainName  DomainName,
                                                TimeSpan    TimeToLive,
                                                String      Data)
        {

            try
            {

                var parts      = Data.Split(' ', 3);
                if (parts.Length < 2)
                    return null;

                var priority   = UInt16.Parse(parts[0]);
                var targetName = parts[1].TrimEnd('.');

                if (targetName == ".")
                    targetName = DomainName.ToString().TrimEnd('.');

                if (!targetName.EndsWith('.'))
                    targetName += ".";

                var svcParams = new List<SVCParameter>();
                if (parts.Length >= 3 && !String.IsNullOrWhiteSpace(parts[2]))
                {
                    foreach (var param in parts[2].Split(' '))
                    {
                        var kv = param.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            if (UInt16.TryParse(kv[0], out var key) ||
                                TryParseSVCParamKey(kv[0], out key))
                            {
                                svcParams.Add(new SVCParameter(key, System.Text.Encoding.UTF8.GetBytes(kv[1])));
                            }
                        }
                    }
                }

                return new SVCB(
                           DomainName,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           priority,
                           DNS.DomainName.Parse(targetName),
                           svcParams
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region (private static) ParseURIFromJSON(...)

        /// <summary>
        /// Parse a URI record from the JSON "data" field
        /// (e.g. "10 1 \"https://example.com/path\"").
        /// </summary>
        private static URI? ParseURIFromJSON(DNSServiceName  ServiceName,
                                              TimeSpan        TimeToLive,
                                              String          Data)
        {

            try
            {

                var parts = Data.Split(' ', 3);
                if (parts.Length < 3)
                    return null;

                var priority = UInt16.Parse(parts[0]);
                var weight   = UInt16.Parse(parts[1]);
                var target   = parts[2].Trim('"');

                return new URI(
                           ServiceName,
                           DNSQueryClasses.IN,
                           TimeToLive,
                           priority,
                           weight,
                           HTTP.URL.Parse(target)
                       );

            }
            catch
            {
                return null;
            }

        }

        #endregion


        // ──────────────────────────────────────────────────────────
        //  DNSSEC JSON parsers
        // ──────────────────────────────────────────────────────────

        #region (private static) ParseDSFromJSON(...)

        /// <summary>
        /// Parse a DS record from the JSON "data" field (e.g. "60485 5 1 2BB183AF5F22588179A53B0A98631FAD1A292118").
        /// </summary>
        private static DS? ParseDSFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new DS(DomainName, DNSQueryClasses.IN, TimeToLive,
                              UInt16.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                              Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseRRSIGFromJSON(...)

        /// <summary>
        /// Parse an RRSIG record from the JSON "data" field.
        /// Format: "TypeCovered Algorithm Labels OrigTTL SigExpiration SigInception KeyTag SignerName Signature"
        /// </summary>
        private static RRSIG? ParseRRSIGFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 9);
                if (parts.Length < 9) return null;
                var signerName = parts[7].EndsWith('.') ? parts[7] : parts[7] + ".";
                return new RRSIG(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 (DNSResourceRecordTypes) UInt16.Parse(parts[0]),
                                 Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                 UInt32.Parse(parts[3]), UInt32.Parse(parts[4]), UInt32.Parse(parts[5]),
                                 UInt16.Parse(parts[6]),
                                 DNS.DomainName.Parse(signerName),
                                 Convert.FromBase64String(parts[8]));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseNSECFromJSON(...)

        /// <summary>
        /// Parse an NSEC record from the JSON "data" field (e.g. "b.example.com. A AAAA RRSIG NSEC").
        /// </summary>
        private static NSEC? ParseNSECFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 2);
                if (parts.Length < 1) return null;
                var nextName = parts[0].EndsWith('.') ? parts[0] : parts[0] + ".";
                // Store type list as raw bytes — for JSON we keep an empty bitmap since
                // precise bitmap encoding from text presentation is complex.
                return new NSEC(DomainName, DNSQueryClasses.IN, TimeToLive,
                                DNS.DomainName.Parse(nextName), []);
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseDNSKEYFromJSON(...)

        /// <summary>
        /// Parse a DNSKEY record from the JSON "data" field (e.g. "256 3 8 AwEAA...base64...").
        /// </summary>
        private static DNSKEY? ParseDNSKEYFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new DNSKEY(DomainName, DNSQueryClasses.IN, TimeToLive,
                                  UInt16.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                  Convert.FromBase64String(parts[3]));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseNSEC3FromJSON(...)

        /// <summary>
        /// Parse an NSEC3 record from the JSON "data" field.
        /// Format: "HashAlg Flags Iterations Salt NextHashedOwner Types..."
        /// </summary>
        private static NSEC3? ParseNSEC3FromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 6);
                if (parts.Length < 5) return null;
                var salt = parts[3] == "-" ? [] : Convert.FromHexString(parts[3]);
                var nextHash = Convert.FromHexString(parts[4]);
                return new NSEC3(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 Byte.Parse(parts[0]), Byte.Parse(parts[1]), UInt16.Parse(parts[2]),
                                 salt, nextHash, []);
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseNSEC3PARAMFromJSON(...)

        /// <summary>
        /// Parse an NSEC3PARAM record from the JSON "data" field (e.g. "1 0 10 AABB").
        /// </summary>
        private static NSEC3PARAM? ParseNSEC3PARAMFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                var salt = parts[3] == "-" ? [] : Convert.FromHexString(parts[3]);
                return new NSEC3PARAM(DomainName, DNSQueryClasses.IN, TimeToLive,
                                      Byte.Parse(parts[0]), Byte.Parse(parts[1]), UInt16.Parse(parts[2]), salt);
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseCDSFromJSON(...)

        private static CDS? ParseCDSFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new CDS(DomainName, DNSQueryClasses.IN, TimeToLive,
                               UInt16.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                               Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseCDNSKEYFromJSON(...)

        private static CDNSKEY? ParseCDNSKEYFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new CDNSKEY(DomainName, DNSQueryClasses.IN, TimeToLive,
                                   UInt16.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                   Convert.FromBase64String(parts[3]));
            }
            catch { return null; }
        }

        #endregion


        // ──────────────────────────────────────────────────────────
        //  Security / certificate JSON parsers
        // ──────────────────────────────────────────────────────────

        #region (private static) ParseTLSAFromJSON(...)

        /// <summary>
        /// Parse a TLSA record from the JSON "data" field (e.g. "3 1 1 AABB...hex...").
        /// </summary>
        private static TLSA? ParseTLSAFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new TLSA(DomainName, DNSQueryClasses.IN, TimeToLive,
                                Byte.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseSMIMEAFromJSON(...)

        private static SMIMEA? ParseSMIMEAFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new SMIMEA(DomainName, DNSQueryClasses.IN, TimeToLive,
                                  Byte.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                  Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseCERTFromJSON(...)

        /// <summary>
        /// Parse a CERT record from the JSON "data" field (e.g. "1 12345 3 base64data...").
        /// </summary>
        private static CERT? ParseCERTFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new CERT(DomainName, DNSQueryClasses.IN, TimeToLive,
                                UInt16.Parse(parts[0]), UInt16.Parse(parts[1]), Byte.Parse(parts[2]),
                                Convert.FromBase64String(parts[3]));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseOPENPGPKEYFromJSON(...)

        /// <summary>
        /// Parse an OPENPGPKEY record from the JSON "data" field (base64 key data).
        /// </summary>
        private static OPENPGPKEY? ParseOPENPGPKEYFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                return new OPENPGPKEY(DomainName, DNSQueryClasses.IN, TimeToLive,
                                      Convert.FromBase64String(Data));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseCAAFromJSON(...)

        /// <summary>
        /// Parse a CAA record from the JSON "data" field (e.g. "0 issue \"letsencrypt.org\"").
        /// </summary>
        private static CAA? ParseCAAFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 3);
                if (parts.Length < 3) return null;
                return new CAA(DomainName, DNSQueryClasses.IN, TimeToLive,
                               Byte.Parse(parts[0]), parts[1], parts[2].Trim('"'));
            }
            catch { return null; }
        }

        #endregion


        // ──────────────────────────────────────────────────────────
        //  Other standard type JSON parsers
        // ──────────────────────────────────────────────────────────

        #region (private static) ParseHINFOFromJSON(...)

        /// <summary>
        /// Parse a HINFO record from the JSON "data" field (e.g. "\"PC\" \"Linux\"").
        /// </summary>
        private static HINFO? ParseHINFOFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 2);
                if (parts.Length < 2) return null;
                return new HINFO(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 parts[0].Trim('"'), parts[1].Trim('"'));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseRPFromJSON(...)

        /// <summary>
        /// Parse an RP record from the JSON "data" field (e.g. "admin.example.com. txt.example.com.").
        /// </summary>
        private static RP? ParseRPFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 2);
                if (parts.Length < 2) return null;
                var mailbox  = parts[0].EndsWith('.') ? parts[0] : parts[0] + ".";
                var txtDname = parts[1].EndsWith('.') ? parts[1] : parts[1] + ".";
                return new RP(DomainName, DNSQueryClasses.IN, TimeToLive,
                              DNS.DomainName.Parse(mailbox), DNS.DomainName.Parse(txtDname));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseAFSDBFromJSON(...)

        /// <summary>
        /// Parse an AFSDB record from the JSON "data" field (e.g. "1 afsdb.example.com.").
        /// </summary>
        private static AFSDB? ParseAFSDBFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 2);
                if (parts.Length < 2) return null;
                var hostname = parts[1].EndsWith('.') ? parts[1] : parts[1] + ".";
                return new AFSDB(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 UInt16.Parse(parts[0]), DNS.DomainName.Parse(hostname));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseLOCFromJSON(...)

        /// <summary>
        /// Parse a LOC record from the JSON "data" field.
        /// LOC presentation format is complex (degrees/minutes/seconds).
        /// We store a minimal representation; full parsing is best-effort.
        /// </summary>
        private static LOC? ParseLOCFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                // LOC has a very complex text presentation format.
                // For JSON DNS (e.g. Google DNS), the data is in presentation format.
                // We create a minimal LOC with default precision values.
                return new LOC(DomainName, DNSQueryClasses.IN, TimeToLive,
                               0, 0x12, 0x16, 0x13, 0x80000000, 0x80000000, 10000000);
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseCSYNCFromJSON(...)

        /// <summary>
        /// Parse a CSYNC record from the JSON "data" field (e.g. "12345 3 A AAAA NS").
        /// </summary>
        private static CSYNC? ParseCSYNCFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 3);
                if (parts.Length < 2) return null;
                return new CSYNC(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 UInt32.Parse(parts[0]), UInt16.Parse(parts[1]), []);
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseZONEMDFromJSON(...)

        /// <summary>
        /// Parse a ZONEMD record from the JSON "data" field (e.g. "12345 1 1 AABB...hex...").
        /// </summary>
        private static ZONEMD? ParseZONEMDFromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                var parts = Data.Split(' ', 4);
                if (parts.Length < 4) return null;
                return new ZONEMD(DomainName, DNSQueryClasses.IN, TimeToLive,
                                  UInt32.Parse(parts[0]), Byte.Parse(parts[1]), Byte.Parse(parts[2]),
                                  Convert.FromHexString(parts[3].Replace(" ", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseEUI48FromJSON(...)

        /// <summary>
        /// Parse an EUI48 record from the JSON "data" field (e.g. "00-00-5e-00-53-2a").
        /// </summary>
        private static EUI48? ParseEUI48FromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                return new EUI48(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 Convert.FromHexString(Data.Replace("-", "").Replace(":", "")));
            }
            catch { return null; }
        }

        #endregion

        #region (private static) ParseEUI64FromJSON(...)

        /// <summary>
        /// Parse an EUI64 record from the JSON "data" field (e.g. "00-00-5e-ef-10-00-00-2a").
        /// </summary>
        private static EUI64? ParseEUI64FromJSON(DomainName DomainName, TimeSpan TimeToLive, String Data)
        {
            try
            {
                return new EUI64(DomainName, DNSQueryClasses.IN, TimeToLive,
                                 Convert.FromHexString(Data.Replace("-", "").Replace(":", "")));
            }
            catch { return null; }
        }

        #endregion


        #region Google DNS

        public static DNSHTTPSClient Google(DNSHTTPSMode?                                              Mode                                 = null,
                                            Boolean?                                                   RecursionDesired                     = null,
                                            TimeSpan?                                                  QueryTimeout                         = null,

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
                                            DNSClient?                                                 DNSClient                            = null)

            => new (
                   URL.Parse(Mode == DNSHTTPSMode.JSON
                                 ? "https://dns.google/resolve"
                                 : "https://dns.google/dns-query"),
                   I18NString.Create("Google"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,

                   RemoteCertificateValidator,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   null,

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

        #region Cloudflare DNS

        public static DNSHTTPSClient Cloudflare_DNSName(DNSHTTPSMode?                                              Mode                                 = null,
                                                        Boolean?                                                   RecursionDesired                     = null,
                                                        TimeSpan?                                                  QueryTimeout                         = null,

                                                        String?                                                    HTTPUserAgent                        = null,

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
                                                        DNSClient?                                                 DNSClient                            = null)

            => new (
                   URL.Parse("https://one.one.one.one/dns-query"),
                   I18NString.Create("Cloudflare (one.one.one.one)"),
                   Mode,
                   RecursionDesired,
                   QueryTimeout,

                   HTTPUserAgent,

                   RemoteCertificateValidator,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   null,

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

        public static DNSHTTPSClient Cloudflare_IPv4_1(DNSHTTPSMode?                                              Mode                                 = null,
                                                       Boolean?                                                   RecursionDesired                     = null,
                                                       TimeSpan?                                                  QueryTimeout                         = null,

                                                       String?                                                    HTTPUserAgent                        = null,

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

                   RemoteCertificateValidationHandler,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   null,

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
                                                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidationHandler   = null,

                                                       String?                                                    HTTPUserAgent                        = null,

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

                   RemoteCertificateValidationHandler,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   AllowRenegotiation,
                   AllowTLSResume,

                   null,

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
