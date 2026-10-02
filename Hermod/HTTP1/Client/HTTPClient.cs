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

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client.
    /// </summary>
    public class HTTPClient : AHTTPClient
    {

        #region Constructor(s)

        #region HTTPClient(IPAddress, ...)

        public HTTPClient(IIPAddress                                                 IPAddress,
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
                          UInt32?                                                    BufferSize                            = null,

                          Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                          Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                          Boolean?                                                   DisableLogging                        = null,
                          ILogger<AHTTPClient>?                                      Logger                                = null,
                          ILoggerFactory?                                            LoggerFactory                         = null,
                          TimeSpan?                                                  MaxConnectionLifetime                 = null)

            : base(IPAddress,
                   TCPPort ?? IPPort.HTTPS,
                   Description,

                   HTTPUserAgent,
                   HTTPAuthentication,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

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
                                               tlsClient as HTTPClient,
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

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 512,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                    DisableLogging,
                    Logger,
                    LoggerFactory,
                    MaxConnectionLifetime)

        { }

        #endregion

        #region HTTPClient(URL, ...)

        public HTTPClient(URL                                                        URL,
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

                   HTTPUserAgent,
                   HTTPAuthentication,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

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
                                              (tlsClient as HTTPClient)!,
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

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize  ?? 8192,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DisableLogging,
                    DNSClient,
                    Logger,
                    LoggerFactory,
                    MaxConnectionLifetime)

        { }

        #endregion

        #region HTTPClient(DomainName, DNSService, ..., DNSClient = null)

        public HTTPClient(DomainName                                                 DomainName,
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
                          UInt32?                                                    BufferSize                            = null,

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

                   HTTPUserAgent,
                   HTTPAuthentication,
                   Accept,
                   ContentType,
                   Connection,
                   DefaultRequestBuilder,

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
                                               tlsClient as HTTPClient,
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

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DisableLogging,
                    DNSClient,
                    Logger,
                    LoggerFactory,
                    MaxConnectionLifetime)

        { }

        #endregion

        #endregion


        #region ConnectNew (           TCPPort, ...)

        /// <summary>
        /// Create a new HTTP client and connect it to the given TCP port on the loopback address:
        /// [::1] by default and under PreferIPv6 or IPv6Only, 127.0.0.1 under PreferIPv4 or IPv4Only.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="HTTPUserAgent">An optional HTTP user agent.</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="ContentType">An optional HTTP content type header.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="DefaultRequestBuilder">An optional delegate to create the default HTTP request builder.</param>
        /// <param name="TLSHostname">An optional hostname for TLS SNI (Server Name Indication) and remote certificate validation.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificates">Optional TLS client certificates to use for authentication. Ignored if ClientCertificateContext is provided.</param>
        /// <param name="ClientCertificateContext">An optional TLS client certificate context, including the client certificate and any intermediate CAs. If provided, this takes precedence over ClientCertificates.</param>
        /// <param name="ClientCertificateChain">An optional TLS client certificate chain, including the client certificate and any intermediate CAs.</param>
        /// <param name="TLSProtocols">The TLS protocols to use. Defaults to TLS 1.3 if not specified.</param>
        /// <param name="CipherSuitesPolicy">The TLS cipher suites policy to use. If null, the system defaults will be used.</param>
        /// <param name="CertificateChainPolicy">An optional TLS certificate chain policy to use for validating the server's certificate chain.</param>
        /// <param name="CertificateRevocationCheckMode">An optional TLS certificate revocation check mode to use for validating the server's certificate.</param>
        /// <param name="EnforceTLS">Whether to enforce TLS. If true, the client will attempt to establish a TLS connection immediately after connecting.</param>
        /// <param name="ApplicationProtocols">The TLS application protocols to use for ALPN (Application-Layer Protocol Negotiation). If empty, ALPN will be disabled.</param>
        /// <param name="AllowRenegotiation">Whether to allow TLS renegotiation. Defaults to true if not specified.</param>
        /// <param name="AllowTLSResume">Whether to allow TLS session resumption. Defaults to false if not specified.</param>
        /// <param name="TOTPConfig">An optional Time-Based One-Time Password (TOTP) configuration.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="ConsumeResponseChunkedTEImmediately">Whether to consume the response chunked transfer encoding immediately.</param>
        /// <param name="DisableLogging">Disable logging of connection events and errors.</param>
        /// <returns>The new HTTP client, also when the connect failed, and the result of the connect.</returns>
        public static async Task<(HTTPClient?, TCPConnectionResult)>

            ConnectNew(IPPort                                                     TCPPort,
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

                => await ConnectNew(

                             IPvXAddress.Localhost,
                             TCPPort,
                             Description,
                             HTTPUserAgent,
                             HTTPAuthentication,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

                             TLSHostname,
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
        /// Create a new HTTP client and connect it to the given IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="HTTPUserAgent">An optional HTTP user agent.</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="ContentType">An optional HTTP content type header.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="DefaultRequestBuilder">An optional delegate to create the default HTTP request builder.</param>
        /// <param name="TLSHostname">An optional hostname for TLS SNI (Server Name Indication) and remote certificate validation.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificates">Optional TLS client certificates to use for authentication. Ignored if ClientCertificateContext is provided.</param>
        /// <param name="ClientCertificateContext">An optional TLS client certificate context, including the client certificate and any intermediate CAs. If provided, this takes precedence over ClientCertificates.</param>
        /// <param name="ClientCertificateChain">An optional TLS client certificate chain, including the client certificate and any intermediate CAs.</param>
        /// <param name="TLSProtocols">The TLS protocols to use. Defaults to TLS 1.3 if not specified.</param>
        /// <param name="CipherSuitesPolicy">The TLS cipher suites policy to use. If null, the system defaults will be used.</param>
        /// <param name="CertificateChainPolicy">An optional TLS certificate chain policy to use for validating the server's certificate chain.</param>
        /// <param name="CertificateRevocationCheckMode">An optional TLS certificate revocation check mode to use for validating the server's certificate.</param>
        /// <param name="EnforceTLS">Whether to enforce TLS. If true, the client will attempt to establish a TLS connection immediately after connecting.</param>
        /// <param name="ApplicationProtocols">The TLS application protocols to use for ALPN (Application-Layer Protocol Negotiation). If empty, ALPN will be disabled.</param>
        /// <param name="AllowRenegotiation">Whether to allow TLS renegotiation. Defaults to true if not specified.</param>
        /// <param name="AllowTLSResume">Whether to allow TLS session resumption. Defaults to false if not specified.</param>
        /// <param name="TOTPConfig">An optional Time-Based One-Time Password (TOTP) configuration.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="ConsumeResponseChunkedTEImmediately">Whether to consume the response chunked transfer encoding immediately.</param>
        /// <param name="DisableLogging">Disable logging of connection events and errors.</param>
        /// <returns>The new HTTP client, also when the connect failed, and the result of the connect.</returns>
        public static async Task<(HTTPClient?, TCPConnectionResult)>

            ConnectNew(IIPAddress                                                 IPAddress,
                       IPPort                                                     TCPPort,
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

            var client = new HTTPClient(

                             IPAddress,
                             TCPPort,
                             Description,
                             HTTPUserAgent,
                             HTTPAuthentication,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

                             TLSHostname,
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

            return (client, response);

        }

        #endregion

        #region ConnectNew (URL, ...)

        /// <summary>
        /// Create a new HTTP client and connect it to the given URL.
        /// </summary>
        /// <param name="URL">The URL to connect to.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="HTTPUserAgent">An optional HTTP user agent.</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="ContentType">An optional HTTP content type header.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="DefaultRequestBuilder">An optional delegate to create the default HTTP request builder.</param>
        /// <param name="TLSHostname">An optional hostname for TLS SNI (Server Name Indication) and remote certificate validation.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificates">Optional TLS client certificates to use for authentication. Ignored if ClientCertificateContext is provided.</param>
        /// <param name="ClientCertificateContext">An optional TLS client certificate context, including the client certificate and any intermediate CAs. If provided, this takes precedence over ClientCertificates.</param>
        /// <param name="ClientCertificateChain">An optional TLS client certificate chain, including the client certificate and any intermediate CAs.</param>
        /// <param name="TLSProtocols">The TLS protocols to use. Defaults to TLS 1.3 if not specified.</param>
        /// <param name="CipherSuitesPolicy">The TLS cipher suites policy to use. If null, the system defaults will be used.</param>
        /// <param name="CertificateChainPolicy">An optional TLS certificate chain policy to use for validating the server's certificate chain.</param>
        /// <param name="CertificateRevocationCheckMode">An optional TLS certificate revocation check mode to use for validating the server's certificate.</param>
        /// <param name="ApplicationProtocols">The TLS application protocols to use for ALPN (Application-Layer Protocol Negotiation). If empty, ALPN will be disabled.</param>
        /// <param name="AllowRenegotiation">Whether to allow TLS renegotiation. Defaults to true if not specified.</param>
        /// <param name="AllowTLSResume">Whether to allow TLS session resumption. Defaults to false if not specified.</param>
        /// <param name="TOTPConfig">An optional Time-Based One-Time Password (TOTP) configuration.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="ConsumeResponseChunkedTEImmediately">Whether to consume the response chunked transfer encoding immediately.</param>
        /// <param name="DisableLogging">Disable logging of connection events and errors.</param>
        /// <returns>The new HTTP client, also when the connect failed: see <see cref="AHTTPClient.IsHTTPConnected"/>.</returns>
        public static async Task<HTTPClient>

            ConnectNew(URL                                                        URL,
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

                       IPVersionPreference?                                       PreferIPv4                            = null,
                       TimeSpan?                                                  ConnectTimeout                        = null,
                       TimeSpan?                                                  ReceiveTimeout                        = null,
                       TimeSpan?                                                  SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                            TransmissionRetryDelay                = null,
                       UInt16?                                                    MaxNumberOfRetries                    = null,
                       UInt32?                                                    BufferSize                            = null,
                       IDNSClient?                                                 DNSClient                             = null,

                       Boolean?                                                   ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                   ConsumeResponseChunkedTEImmediately   = null,

                       Boolean?                                                   DisableLogging                        = null)

        {

            var client = new HTTPClient(

                             URL,
                             Description,
                             HTTPUserAgent,
                             HTTPAuthentication,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

                             TLSHostname,
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
        /// Create a new HTTP client and connect it to an IP address and TCP port resolved from the given DNS name and DNS service.
        /// </summary>
        /// <param name="DNSName">The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.</param>
        /// <param name="DNSService">The DNS service to lookup in order to resolve high available IP addresses and TCP ports.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="HTTPUserAgent">An optional HTTP user agent.</param>
        /// <param name="HTTPAuthentication">An optional HTTP authentication.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="ContentType">An optional HTTP content type header.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="DefaultRequestBuilder">An optional delegate to create the default HTTP request builder.</param>
        /// <param name="TLSHostname">An optional hostname for TLS SNI (Server Name Indication) and remote certificate validation.</param>
        /// <param name="RemoteCertificateValidator">An optional remote TLS server certificate validator.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificates">Optional TLS client certificates to use for authentication. Ignored if ClientCertificateContext is provided.</param>
        /// <param name="ClientCertificateContext">An optional TLS client certificate context, including the client certificate and any intermediate CAs. If provided, this takes precedence over ClientCertificates.</param>
        /// <param name="ClientCertificateChain">An optional TLS client certificate chain, including the client certificate and any intermediate CAs.</param>
        /// <param name="TLSProtocols">The TLS protocols to use. Defaults to TLS 1.3 if not specified.</param>
        /// <param name="CipherSuitesPolicy">The TLS cipher suites policy to use. If null, the system defaults will be used.</param>
        /// <param name="CertificateChainPolicy">An optional TLS certificate chain policy to use for validating the server's certificate chain.</param>
        /// <param name="CertificateRevocationCheckMode">An optional TLS certificate revocation check mode to use for validating the server's certificate.</param>
        /// <param name="EnforceTLS">Whether to enforce TLS. If true, the client will attempt to establish a TLS connection immediately after connecting.</param>
        /// <param name="ApplicationProtocols">The TLS application protocols to use for ALPN (Application-Layer Protocol Negotiation). If empty, ALPN will be disabled.</param>
        /// <param name="AllowRenegotiation">Whether to allow TLS renegotiation. Defaults to true if not specified.</param>
        /// <param name="AllowTLSResume">Whether to allow TLS session resumption. Defaults to false if not specified.</param>
        /// <param name="TOTPConfig">An optional Time-Based One-Time Password (TOTP) configuration.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="ConsumeRequestChunkedTEImmediately">Whether to consume the request chunked transfer encoding immediately.</param>
        /// <param name="ConsumeResponseChunkedTEImmediately">Whether to consume the response chunked transfer encoding immediately.</param>
        /// <param name="DisableLogging">Disable logging of connection events and errors.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <returns>The new HTTP client, also when the connect failed: see <see cref="AHTTPClient.IsHTTPConnected"/>.</returns>
        public static async Task<HTTPClient>

            ConnectNew(DomainName                                                 DNSName,
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
                       IDNSClient?                                                 DNSClient                             = null)

        {

            var client = new HTTPClient(

                             DNSName,
                             DNSService,
                             Description,
                             HTTPUserAgent,
                             HTTPAuthentication,
                             Accept,
                             ContentType,
                             Connection,
                             DefaultRequestBuilder,

                             TLSHostname,
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

            => $"{nameof(HTTPClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion

    }

}
