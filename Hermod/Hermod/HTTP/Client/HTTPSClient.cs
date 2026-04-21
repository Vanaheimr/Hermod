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
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTPS client.
    /// </summary>
    public class HTTPSClient : AHTTPClient
    {

        #region Constructor(s)

        #region HTTPSClient(IPAddress, ...)

        public HTTPSClient(IIPAddress                                                IPAddress,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,

                           IPPort?                                                   TCPPort                               = null,
                           I18NString?                                               Description                           = null,
                           String?                                                   HTTPUserAgent                         = null,
                           AcceptTypes?                                              Accept                                = null,
                           HTTPContentType?                                          ContentType                           = null,
                           ConnectionType?                                           Connection                            = null,
                           DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                           LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                           IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                           SslStreamCertificateContext?                              ClientCertificateContext              = null,
                           IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                           SslProtocols?                                             TLSProtocols                          = null,
                           CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                           X509ChainPolicy?                                          CertificateChainPolicy                = null,
                           X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                           Boolean?                                                  EnforceTLS                            = null,
                           IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                           Boolean?                                                  AllowRenegotiation                    = null,
                           Boolean?                                                  AllowTLSResume                        = null,
                           TOTPConfig?                                               TOTPConfig                            = null,

                           IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                           IPVersionPreference?                                      PreferIPv4                            = null,
                           TimeSpan?                                                 ConnectTimeout                        = null,
                           TimeSpan?                                                 ReceiveTimeout                        = null,
                           TimeSpan?                                                 SendTimeout                           = null,
                           TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                           UInt16?                                                   MaxNumberOfRetries                    = null,
                           UInt32?                                                   BufferSize                            = null,

                           Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                           Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                           Boolean?                                                  DisableLogging                        = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTPS,
                   Description,

                   HTTPUserAgent,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as HTTPSClient,
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
                   TOTPConfig,

                   HTTPAuthentication,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 512,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DisableLogging)

        { }

        #endregion

        #region HTTPSClient(URL, ...)

        public HTTPSClient(URL                                                        URL,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>   RemoteCertificateValidator,

                           I18NString?                                                Description                           = null,
                           String?                                                    HTTPUserAgent                         = null,
                           AcceptTypes?                                               Accept                                = null,
                           HTTPContentType?                                           ContentType                           = null,
                           ConnectionType?                                            Connection                            = null,
                           DefaultRequestBuilderDelegate?                             DefaultRequestBuilder                 = null,

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

                           IHTTPAuthentication?                                       HTTPAuthentication                    = null,

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

            : base(URL,
                   Description,

                   HTTPUserAgent,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                              (tlsClient as HTTPSClient)!,
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
                   BufferSize  ?? 8192,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DisableLogging,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(DomainName, DNSService, ..., DNSClient = null)

        public HTTPSClient(DomainName                                                DomainName,
                           SRV_Spec                                                  DNSService,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,

                           I18NString?                                               Description                           = null,
                           String?                                                   HTTPUserAgent                         = null,
                           AcceptTypes?                                              Accept                                = null,
                           HTTPContentType?                                          ContentType                           = null,
                           ConnectionType?                                           Connection                            = null,
                           DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                           LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                           IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                           SslStreamCertificateContext?                              ClientCertificateContext              = null,
                           IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                           SslProtocols?                                             TLSProtocols                          = null,
                           CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                           X509ChainPolicy?                                          CertificateChainPolicy                = null,
                           X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                           Boolean?                                                  EnforceTLS                            = null,
                           IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                           Boolean?                                                  AllowRenegotiation                    = null,
                           Boolean?                                                  AllowTLSResume                        = null,
                           TOTPConfig?                                               TOTPConfig                            = null,

                           IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                           IPVersionPreference?                                      PreferIPv4                            = null,
                           TimeSpan?                                                 ConnectTimeout                        = null,
                           TimeSpan?                                                 ReceiveTimeout                        = null,
                           TimeSpan?                                                 SendTimeout                           = null,
                           TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                           UInt16?                                                   MaxNumberOfRetries                    = null,
                           UInt32?                                                   BufferSize                            = null,

                           Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                           Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                           Boolean?                                                  DisableLogging                        = null,
                           IDNSClient?                                               DNSClient                             = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   HTTPUserAgent,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsClient as HTTPSClient,
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
                   DNSClient)

        { }

        #endregion

        #endregion


        #region ConnectNew (           TCPPort, ...)

        /// <summary>
        /// Create a new HTTPSClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        public static async Task<(HTTPSClient?, IReadOnlyList<String>)>

            ConnectNew(IPPort                                                    TCPPort,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,

                       I18NString?                                               Description                           = null,
                       String?                                                   HTTPUserAgent                         = null,
                       AcceptTypes?                                              Accept                                = null,
                       HTTPContentType?                                          ContentType                           = null,
                       ConnectionType?                                           Connection                            = null,
                       DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                       LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                       SslStreamCertificateContext?                              ClientCertificateContext              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                       SslProtocols?                                             TLSProtocols                          = null,
                       CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                       X509ChainPolicy?                                          CertificateChainPolicy                = null,
                       X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                       Boolean?                                                  EnforceTLS                            = null,
                       IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                       Boolean?                                                  AllowRenegotiation                    = null,
                       Boolean?                                                  AllowTLSResume                        = null,
                       TOTPConfig?                                               TOTPConfig                            = null,

                       IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                       IPVersionPreference?                                      PreferIPv4                            = null,
                       TimeSpan?                                                 ConnectTimeout                        = null,
                       TimeSpan?                                                 ReceiveTimeout                        = null,
                       TimeSpan?                                                 SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                       UInt16?                                                   MaxNumberOfRetries                    = null,
                       UInt32?                                                   BufferSize                            = null,

                       Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                       Boolean?                                                  DisableLogging                        = null)

                => await ConnectNew(

                             IPvXAddress.Localhost,
                             TCPPort,
                             RemoteCertificateValidator,

                             Description,
                             HTTPUserAgent,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

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

        #endregion

        #region ConnectNew (IPAddress, TCPPort, ...)

        /// <summary>
        /// Create a new HTTPSClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(HTTPSClient?, IReadOnlyList<String>)>

            ConnectNew(IIPAddress                                                IPAddress,
                       IPPort                                                    TCPPort,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,

                       I18NString?                                               Description                           = null,
                       String?                                                   HTTPUserAgent                         = null,
                       AcceptTypes?                                              Accept                                = null,
                       HTTPContentType?                                          ContentType                           = null,
                       ConnectionType?                                           Connection                            = null,
                       DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                       LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                       SslStreamCertificateContext?                              ClientCertificateContext              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                       SslProtocols?                                             TLSProtocols                          = null,
                       CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                       X509ChainPolicy?                                          CertificateChainPolicy                = null,
                       X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                       Boolean?                                                  EnforceTLS                            = null,
                       IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                       Boolean?                                                  AllowRenegotiation                    = null,
                       Boolean?                                                  AllowTLSResume                        = null,
                       TOTPConfig?                                               TOTPConfig                            = null,

                       IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                       IPVersionPreference?                                      PreferIPv4                            = null,
                       TimeSpan?                                                 ConnectTimeout                        = null,
                       TimeSpan?                                                 ReceiveTimeout                        = null,
                       TimeSpan?                                                 SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                       UInt16?                                                   MaxNumberOfRetries                    = null,
                       UInt32?                                                   BufferSize                            = null,

                       Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                       Boolean?                                                  DisableLogging                        = null)

        {

            var client = new HTTPSClient(

                             IPAddress,
                             RemoteCertificateValidator,
                             TCPPort,
                             Description,
                             HTTPUserAgent,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

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

            var response = await client.ConnectAsync();

            return response.Success
                       ? (client, [])
                       : (null,   response.Errors);

        }

        #endregion

        #region ConnectNew (URL, ...)

        /// <summary>
        /// Create a new HTTPSClient and connect to the given URL.
        /// </summary>
        /// <param name="URL">The URL to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<HTTPSClient>

            ConnectNew(URL                                                       URL,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,

                       I18NString?                                               Description                           = null,
                       String?                                                   HTTPUserAgent                         = null,
                       AcceptTypes?                                              Accept                                = null,
                       HTTPContentType?                                          ContentType                           = null,
                       ConnectionType?                                           Connection                            = null,
                       DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                       LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                       SslStreamCertificateContext?                              ClientCertificateContext              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                       SslProtocols?                                             TLSProtocols                          = null,
                       CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                       X509ChainPolicy?                                          CertificateChainPolicy                = null,
                       X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                       IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                       Boolean?                                                  AllowRenegotiation                    = null,
                       Boolean?                                                  AllowTLSResume                        = null,
                       TOTPConfig?                                               TOTPConfig                            = null,

                       IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                       IPVersionPreference?                                      PreferIPv4                            = null,
                       TimeSpan?                                                 ConnectTimeout                        = null,
                       TimeSpan?                                                 ReceiveTimeout                        = null,
                       TimeSpan?                                                 SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                       UInt16?                                                   MaxNumberOfRetries                    = null,
                       UInt32?                                                   BufferSize                            = null,
                       DNSClient?                                                DNSClient                             = null,

                       Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                       Boolean?                                                  DisableLogging                        = null)

        {

            var client = new HTTPSClient(

                             URL,
                             RemoteCertificateValidator,

                             Description,
                             HTTPUserAgent,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

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

            await client.ConnectAsync();

            return client;

        }

        #endregion

        #region ConnectNew (DNSName,   DNSService, ...)

        /// <summary>
        /// Create a new HTTPSClient and connect to the given URL.
        /// </summary>
        /// <param name="DNSName">The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.</param>
        /// <param name="DNSService">The DNS service to lookup in order to resolve high available IP addresses and TCP ports.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        public static async Task<HTTPSClient>

            ConnectNew(DomainName                                                DNSName,
                       SRV_Spec                                                  DNSService,
                       RemoteTLSServerCertificateValidationHandler<IHTTPClient>  RemoteCertificateValidator,


                       I18NString?                                               Description                           = null,
                       String?                                                   HTTPUserAgent                         = null,
                       AcceptTypes?                                              Accept                                = null,
                       HTTPContentType?                                          ContentType                           = null,
                       ConnectionType?                                           Connection                            = null,
                       DefaultRequestBuilderDelegate?                            DefaultRequestBuilder                 = null,

                       LocalCertificateSelectionHandler?                         LocalCertificateSelector              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificates                    = null,
                       SslStreamCertificateContext?                              ClientCertificateContext              = null,
                       IEnumerable<X509Certificate2>?                            ClientCertificateChain                = null,
                       SslProtocols?                                             TLSProtocols                          = null,
                       CipherSuitesPolicy?                                       CipherSuitesPolicy                    = null,
                       X509ChainPolicy?                                          CertificateChainPolicy                = null,
                       X509RevocationMode?                                       CertificateRevocationCheckMode        = null,
                       Boolean?                                                  EnforceTLS                            = null,
                       IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols                  = null,
                       Boolean?                                                  AllowRenegotiation                    = null,
                       Boolean?                                                  AllowTLSResume                        = null,
                       TOTPConfig?                                               TOTPConfig                            = null,

                       IHTTPAuthentication?                                      HTTPAuthentication                    = null,

                       IPVersionPreference?                                      PreferIPv4                            = null,
                       TimeSpan?                                                 ConnectTimeout                        = null,
                       TimeSpan?                                                 ReceiveTimeout                        = null,
                       TimeSpan?                                                 SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                           TransmissionRetryDelay                = null,
                       UInt16?                                                   MaxNumberOfRetries                    = null,
                       UInt32?                                                   BufferSize                            = null,

                       Boolean?                                                  ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                  ConsumeResponseChunkedTEImmediately   = null,

                       Boolean?                                                  DisableLogging                        = null,
                       DNSClient?                                                DNSClient                             = null)

        {

            var client = new HTTPSClient(

                             DNSName,
                             DNSService,
                             RemoteCertificateValidator,

                             Description,
                             HTTPUserAgent,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

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

            await client.ConnectAsync();

            return client;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(HTTPSClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion

    }

}
