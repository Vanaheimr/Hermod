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

using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

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
    public class DNSHTTPSClient : AHTTPTestClient,
                                  IDNSClient2
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

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

        #endregion

        #region Constructor(s)

        #region DNSHTTPSClient(TCPPort, ...)

        /// <summary>
        /// Create a new DNS HTTPS client for the given DNS server.
        /// </summary>
        /// <param name="IPAddress">The DNS server to query.</param>
        public DNSHTTPSClient(IPPort                                                        TCPPort,
                              I18NString?                                                   Description                          = null,
                              DNSHTTPSMode?                                                 Mode                                 = null,
                              Boolean?                                                      RecursionDesired                     = null,
                              TimeSpan?                                                     QueryTimeout                         = null,

                              String?                                                       HTTPUserAgent                        = null,

                              RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                              SslProtocols?                                                 TLSProtocols                         = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                              CertificateChainPolicy               = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                              Boolean?                                                      AllowRenegotiation                   = null,
                              Boolean?                                                      AllowTLSResume                       = null,

                              Boolean?                                                      PreferIPv4                           = null,
                              TimeSpan?                                                     ConnectTimeout                       = null,
                              TimeSpan?                                                     ReceiveTimeout                       = null,
                              TimeSpan?                                                     SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                              UInt16?                                                       MaxNumberOfRetries                   = null,
                              UInt32?                                                       BufferSize                           = null,
                              TCPEchoLoggingDelegate?                                       LoggingHandler                       = null)

            : base(IPvXAddress.Localhost,
                   TCPPort,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,

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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 512,
                   LoggingHandler)

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
        public DNSHTTPSClient(IIPAddress                                                    IPAddress,
                              IPPort?                                                       TCPPort                              = null,
                              I18NString?                                                   Description                          = null,
                              DNSHTTPSMode?                                                 Mode                                 = null,
                              Boolean?                                                      RecursionDesired                     = null,
                              TimeSpan?                                                     QueryTimeout                         = null,

                              String?                                                       HTTPUserAgent                        = null,

                              RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                              SslProtocols?                                                 TLSProtocols                         = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                              CertificateChainPolicy               = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                              Boolean?                                                      AllowRenegotiation                   = null,
                              Boolean?                                                      AllowTLSResume                       = null,

                              Boolean?                                                      PreferIPv4                           = null,
                              TimeSpan?                                                     ConnectTimeout                       = null,
                              TimeSpan?                                                     ReceiveTimeout                       = null,
                              TimeSpan?                                                     SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                              UInt16?                                                       MaxNumberOfRetries                   = null,
                              UInt32?                                                       BufferSize                           = null,
                              TCPEchoLoggingDelegate?                                       LoggingHandler                       = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTPS,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,

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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 512,
                   LoggingHandler)

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
        public DNSHTTPSClient(URL                                                           URL,
                              I18NString?                                                   Description                          = null,
                              DNSHTTPSMode?                                                 Mode                                 = null,
                              Boolean?                                                      RecursionDesired                     = null,
                              TimeSpan?                                                     QueryTimeout                         = null,

                              String?                                                       HTTPUserAgent                        = null,

                              RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                              SslProtocols?                                                 TLSProtocols                         = null,
                              CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                              X509ChainPolicy?                                              CertificateChainPolicy               = null,
                              X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                              Boolean?                                                      AllowRenegotiation                   = null,
                              Boolean?                                                      AllowTLSResume                       = null,

                              Boolean?                                                      PreferIPv4                           = null,
                              TimeSpan?                                                     ConnectTimeout                       = null,
                              TimeSpan?                                                     ReceiveTimeout                       = null,
                              TimeSpan?                                                     SendTimeout                          = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                              UInt16?                                                       MaxNumberOfRetries                   = null,
                              UInt32?                                                       BufferSize                           = null,
                              TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                              DNSClient?                                                    DNSClient                            = null)

            : base(URL,
                   null,
                   Description,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   null,

                   RemoteCertificateValidationHandler is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          aHTTPTestClient,
                          policyErrors) => RemoteCertificateValidationHandler.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               aHTTPTestClient as DNSHTTPSClient,
                                               policyErrors
                                           )
                       : null,
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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize  ?? 512,
                   LoggingHandler,
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
        public static async Task<DNSHTTPSClient>

            ConnectNew(IIPAddress                                                    IPAddress,
                       IPPort?                                                       TCPPort                              = null,
                       I18NString?                                                   Description                          = null,
                       DNSHTTPSMode?                                                 Mode                                 = null,
                       Boolean?                                                      RecursionDesired                     = null,
                       TimeSpan?                                                     QueryTimeout                         = null,

                       String?                                                       HTTPUserAgent                        = null,

                       RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                       SslProtocols?                                                 TLSProtocols                         = null,
                       CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                       X509ChainPolicy?                                              CertificateChainPolicy               = null,
                       X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                       Boolean?                                                      AllowRenegotiation                   = null,
                       Boolean?                                                      AllowTLSResume                       = null,

                       Boolean?                                                      PreferIPv4                           = null,
                       TimeSpan?                                                     ConnectTimeout                       = null,
                       TimeSpan?                                                     ReceiveTimeout                       = null,
                       TimeSpan?                                                     SendTimeout                          = null,
                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                       UInt16?                                                       MaxNumberOfRetries                   = null,
                       UInt32?                                                       BufferSize                           = null,
                       TCPEchoLoggingDelegate?                                       LoggingHandler                       = null)

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
                             LoggingHandler
                         );

            await client.ConnectAsync();

            return client;

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
        public static async Task<DNSHTTPSClient>

            ConnectNew(URL                                                           URL,
                       SRV_Spec?                                                     DNSService                           = null,
                       I18NString?                                                   Description                          = null,
                       DNSHTTPSMode?                                                 Mode                                 = null,
                       Boolean?                                                      RecursionDesired                     = null,
                       TimeSpan?                                                     QueryTimeout                         = null,

                       String?                                                       HTTPUserAgent                        = null,

                       RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                       SslProtocols?                                                 TLSProtocols                         = null,
                       CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                       X509ChainPolicy?                                              CertificateChainPolicy               = null,
                       X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                       Boolean?                                                      AllowRenegotiation                   = null,
                       Boolean?                                                      AllowTLSResume                       = null,

                       Boolean?                                                      PreferIPv4                           = null,
                       TimeSpan?                                                     ConnectTimeout                       = null,
                       TimeSpan?                                                     ReceiveTimeout                       = null,
                       TimeSpan?                                                     SendTimeout                          = null,
                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                       UInt16?                                                       MaxNumberOfRetries                   = null,
                       UInt32?                                                       BufferSize                           = null,
                       TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                       DNSClient?                                                    DNSClient                            = null)

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

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,
                             LoggingHandler,
                             DNSClient
                         );

            await client.ConnectAsync();

            return client;

        }

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean                              RecursionDesired    = true,
                                   CancellationToken                    CancellationToken   = default)

            => QueryHTTP(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   RecursionDesired,
                   null,
                   null,
                   CancellationToken
               );


        public Task<DNSInfo> QueryHTTP(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       Boolean                              RecursionDesired          = true,
                                       ClientRequestLogHandler?             HTTPRequestLogDelegate    = null,
                                       ClientResponseLogHandler?            HTTPResponseLogDelegate   = null,
                                       CancellationToken                    CancellationToken         = default)

            => QueryHTTP(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   RecursionDesired,
                   HTTPRequestLogDelegate,
                   HTTPResponseLogDelegate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean                              RecursionDesired    = true,
                                   CancellationToken                    CancellationToken   = default)

            => QueryHTTP(
                   DNSServiceName,
                   ResourceRecordTypes,
                   RecursionDesired,
                   null,
                   null,
                   CancellationToken
               );

        public async Task<DNSInfo> QueryHTTP(DNSServiceName                       DNSServiceName,
                                             IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                             Boolean                              RecursionDesired          = true,
                                             ClientRequestLogHandler?             HTTPRequestLogDelegate    = null,
                                             ClientResponseLogHandler?            HTTPResponseLogDelegate   = null,
                                             CancellationToken                    CancellationToken         = default)
        {

            #region Initial checks

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion


            var dnsQuery  = DNSPacket.Query(
                                DNSServiceName,
                                this.RecursionDesired ?? RecursionDesired,
                                [.. resourceRecordTypes]
                            );

            var dnsBytes  = dnsQuery.ToByteArray();

            if (!IsConnected || tcpClient is null)
                await ReconnectAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                var stopwatch = Stopwatch.StartNew();
                clientCancellationTokenSource ??= new CancellationTokenSource();

                var httpRequestBuilder = DefaultRequestBuilder();// {
                                             //Host:   RemoteURL.Value.Hostname
                                         //};

                httpRequestBuilder.SetHost(RemoteURL.Value.Hostname);

                if (Mode == DNSHTTPSMode.GET)
                {
                    // GET https://dns.google/dns-query?dns=fZABAAABAAAAAAAACGNoYXJnaW5nBWNsb3VkAAABAAE
                    httpRequestBuilder.HTTPMethod     = HTTPMethod.GET;
                    httpRequestBuilder.Path           = RemoteURL.Value.Path;
                    httpRequestBuilder.Accept         = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.DNSMessage);
                    httpRequestBuilder.QueryString.Add("dns", dnsBytes.ToBase64URL());
                }
                else if (Mode == DNSHTTPSMode.POST)
                {
                    // POST https://dns.google/dns-query
                    httpRequestBuilder.HTTPMethod     = HTTPMethod.POST;
                    httpRequestBuilder.Path           = RemoteURL.Value.Path;
                    httpRequestBuilder.Accept         = AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.DNSMessage);
                    httpRequestBuilder.ContentType    = HTTPContentType.Application.DNSMessage;
                    httpRequestBuilder.ContentLength  = (UInt64) dnsBytes.Length;
                    httpRequestBuilder.Content        = dnsBytes;
                }
                else
                {
                    throw new ArgumentException($"Unsupported DNS HTTPS mode: {Mode}");
                }

                var httpResponse  = await SendRequest(
                                              httpRequestBuilder.AsImmutable,
                                              HTTPRequestLogDelegate,
                                              HTTPResponseLogDelegate,
                                              CancellationToken
                                          );

                stopwatch.Stop();

                // GET /dns-query?dns=9q8BAAABAAAAAAAACGNoYXJnaW5nBWNsb3VkAAABAAE HTTP/1.1
                // Accept:      application/dns-message; charset=utf-8; q=1
                // Host:        one.one.one.one
                // User-Agent:  Hermod DNS HTTP Test Client
                // Connection:  keep-alive

                // HTTP/1.1 200 OK
                // X-Content-Type-Options:       nosniff
                // Strict-Transport-Security:    max-age=31536000; includeSubDomains; preload
                // Access-Control-Allow-Origin:  *
                // Date:                         Sat, 02 Aug 2025 22:32:12 GMT
                // Expires:                      Sat, 02 Aug 2025 22:32:12 GMT
                // Cache-Control:                private, max-age=3600
                // Content-Type:                 application/dns-message
                // Server:                       HTTP server (unknown)
                // Content-Length:               48
                // X-XSS-Protection:             0
                // X-Frame-Options:              SAMEORIGIN
                // Alt-Svc:                      h3=":443"; ma=2592000,h3-29=":443"; ma=2592000

                var dnsInfo  = DNSInfo.ReadResponse(
                                   new DNSServerConfig(
                                       RemoteIPAddress!,
                                       RemotePort ?? IPPort.DNS,
                                       DNSTransport.HTTPS,
                                       QueryTimeout
                                   ),
                                   dnsQuery.TransactionId,
                                   new MemoryStream(httpResponse.HTTPBody ?? [])
                               );

                return dnsInfo;

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return null;
            }

        }

        #endregion


        #region Google DNS

        public static DNSHTTPSClient Google(DNSHTTPSMode?                                                 Mode                                 = null,
                                            Boolean?                                                      RecursionDesired                     = null,
                                            TimeSpan?                                                     QueryTimeout                         = null,

                                            RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                                            SslProtocols?                                                 TLSProtocols                         = null,
                                            CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                                            X509ChainPolicy?                                              CertificateChainPolicy               = null,
                                            X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                                            Boolean?                                                      AllowRenegotiation                   = null,
                                            Boolean?                                                      AllowTLSResume                       = null,

                                            Boolean?                                                      PreferIPv4                           = null,
                                            TimeSpan?                                                     ConnectTimeout                       = null,
                                            TimeSpan?                                                     ReceiveTimeout                       = null,
                                            TimeSpan?                                                     SendTimeout                          = null,
                                            TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                                            UInt16?                                                       MaxNumberOfRetries                   = null,
                                            UInt32?                                                       BufferSize                           = null,
                                            String?                                                       HTTPUserAgent                        = null,
                                            TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                            DNSClient?                                                    DNSClient                            = null)

            => new (
                   URL.Parse("https://dns.google/dns-query"),
                   I18NString.Create("Google"),
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
                   LoggingHandler,
                   DNSClient
               );

        #endregion

        #region Cloudflare DNS

        public static DNSHTTPSClient Cloudflare_DNSName(DNSHTTPSMode?                                                 Mode                                 = null,
                                                        Boolean?                                                      RecursionDesired                     = null,
                                                        TimeSpan?                                                     QueryTimeout                         = null,

                                                        String?                                                       HTTPUserAgent                        = null,

                                                        RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                                                        SslProtocols?                                                 TLSProtocols                         = null,
                                                        CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                                                        X509ChainPolicy?                                              CertificateChainPolicy               = null,
                                                        X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                                                        Boolean?                                                      AllowRenegotiation                   = null,
                                                        Boolean?                                                      AllowTLSResume                       = null,

                                                        Boolean?                                                      PreferIPv4                           = null,
                                                        TimeSpan?                                                     ConnectTimeout                       = null,
                                                        TimeSpan?                                                     ReceiveTimeout                       = null,
                                                        TimeSpan?                                                     SendTimeout                          = null,
                                                        TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                                                        UInt16?                                                       MaxNumberOfRetries                   = null,
                                                        UInt32?                                                       BufferSize                           = null,
                                                        TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                                        DNSClient?                                                    DNSClient                            = null)

            => new (
                   URL.Parse("https://one.one.one.one/dns-query"),
                   I18NString.Create("Cloudflare (one.one.one.one)"),
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
                   LoggingHandler,
                   DNSClient
               );

        public static DNSHTTPSClient Cloudflare_IPv4_1(DNSHTTPSMode?                                                 Mode                                 = null,
                                                       Boolean?                                                      RecursionDesired                     = null,
                                                       TimeSpan?                                                     QueryTimeout                         = null,

                                                       String?                                                       HTTPUserAgent                        = null,

                                                       RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,
                                                       SslProtocols?                                                 TLSProtocols                         = null,
                                                       CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                                                       X509ChainPolicy?                                              CertificateChainPolicy               = null,
                                                       X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                                                       Boolean?                                                      AllowRenegotiation                   = null,
                                                       Boolean?                                                      AllowTLSResume                       = null,

                                                       Boolean?                                                      PreferIPv4                           = null,
                                                       TimeSpan?                                                     ConnectTimeout                       = null,
                                                       TimeSpan?                                                     ReceiveTimeout                       = null,
                                                       TimeSpan?                                                     SendTimeout                          = null,
                                                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                                                       UInt16?                                                       MaxNumberOfRetries                   = null,
                                                       UInt32?                                                       BufferSize                           = null,
                                                       TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                                       DNSClient?                                                    DNSClient                            = null)

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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   LoggingHandler,
                   DNSClient
               );

        public static DNSHTTPSClient Cloudflare_IPv4_2(DNSHTTPSMode?                                                 Mode                                 = null,
                                                       Boolean?                                                      RecursionDesired                     = null,
                                                       TimeSpan?                                                     QueryTimeout                         = null,
                                                       RemoteTLSServerCertificateValidationHandler<DNSHTTPSClient>?  RemoteCertificateValidationHandler   = null,

                                                       String?                                                       HTTPUserAgent                        = null,

                                                       SslProtocols?                                                 TLSProtocols                         = null,
                                                       CipherSuitesPolicy?                                           CipherSuitesPolicy                   = null,
                                                       X509ChainPolicy?                                              CertificateChainPolicy               = null,
                                                       X509RevocationMode?                                           CertificateRevocationCheckMode       = null,
                                                       Boolean?                                                      AllowRenegotiation                   = null,
                                                       Boolean?                                                      AllowTLSResume                       = null,

                                                       Boolean?                                                      PreferIPv4                           = null,
                                                       TimeSpan?                                                     ConnectTimeout                       = null,
                                                       TimeSpan?                                                     ReceiveTimeout                       = null,
                                                       TimeSpan?                                                     SendTimeout                          = null,
                                                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay               = null,
                                                       UInt16?                                                       MaxNumberOfRetries                   = null,
                                                       UInt32?                                                       BufferSize                           = null,
                                                       TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                                       DNSClient?                                                    DNSClient                            = null)

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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   LoggingHandler,
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


    }

}
