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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public class HTTPTestClient : AHTTPTestClient,
                                  IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        //public Boolean IsHTTPConnected { get; private set; } = false;

        #endregion

        #region Properties

        /// <summary>
        /// Use DNS URI records to resolve the hostname to IP addresses.
        /// </summary>
        public Boolean  UseDNSURI    { get; }

        #endregion

        #region Constructor(s)

        #region HTTPTestClient(IPAddress, ...)

        public HTTPTestClient(IIPAddress                                                    IPAddress,
                              IPPort?                                                       TCPPort                               = null,
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

                              Boolean?                                                      PreferIPv4                            = null,
                              TimeSpan?                                                     ConnectTimeout                        = null,
                              TimeSpan?                                                     ReceiveTimeout                        = null,
                              TimeSpan?                                                     SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                              UInt16?                                                       MaxNumberOfRetries                    = null,
                              UInt32?                                                       BufferSize                            = null,

                              Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null)

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
                                               tlsClient as HTTPTestClient,
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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize ?? 512,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately)

        { }

        #endregion

        #region HTTPTestClient(URL, ...)

        public HTTPTestClient(URL                                                           URL,
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
                              IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                  = null,
                              Boolean?                                                      AllowRenegotiation                    = null,
                              Boolean?                                                      AllowTLSResume                        = null,

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

            : base(URL,
                   null,
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
                                              (tlsClient as HTTPTestClient)!,
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
                   null,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize  ?? 8192,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DNSClient)

        { }

        #endregion

        #region HTTPTestClient(DomainName, DNSService, ..., DNSClient = null)

        public HTTPTestClient(DomainName                                                    DomainName,
                              SRV_Spec                                                      DNSService,
                         //     Boolean?                                                      UseDNSURI                             = null,
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

                              Boolean?                                                      PreferIPv4                            = null,
                              TimeSpan?                                                     ConnectTimeout                        = null,
                              TimeSpan?                                                     ReceiveTimeout                        = null,
                              TimeSpan?                                                     SendTimeout                           = null,
                              TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                              UInt16?                                                       MaxNumberOfRetries                    = null,
                              UInt32?                                                       BufferSize                            = null,

                              Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                              Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null,

                              TCPEchoLoggingDelegate?                                       LoggingHandler                        = null,
                              IDNSClient?                                                   DNSClient                             = null)

            : base(DomainName,
                   DNSService,
                   //UseDNSURI,
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
                                               tlsClient as HTTPTestClient,
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

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,

                   ConsumeRequestChunkedTEImmediately,
                   ConsumeResponseChunkedTEImmediately,

                   DNSClient)

        { }

        #endregion

        #endregion


        #region ConnectNew (           TCPPort, ...)

        /// <summary>
        /// Create a new HTTPTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(HTTPTestClient?, List<String>)>

            ConnectNew(IPPort                                                        TCPPort,
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

                       Boolean?                                                      PreferIPv4                            = null,
                       TimeSpan?                                                     ConnectTimeout                        = null,
                       TimeSpan?                                                     ReceiveTimeout                        = null,
                       TimeSpan?                                                     SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                       UInt16?                                                       MaxNumberOfRetries                    = null,
                       UInt32?                                                       BufferSize                            = null,

                       Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null,

                       TCPEchoLoggingDelegate?                                       LoggingHandler                        = null)

                => await ConnectNew(
                             IPvXAddress.Localhost,
                             TCPPort,
                             Description,
                             HTTPUserAgent,
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

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,

                             ConsumeRequestChunkedTEImmediately,
                             ConsumeResponseChunkedTEImmediately
                         );

        #endregion

        #region ConnectNew (IPAddress, TCPPort, ...)

        /// <summary>
        /// Create a new HTTPTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<(HTTPTestClient?, List<String>)>

            ConnectNew(IIPAddress                                                    IPAddress,
                       IPPort                                                        TCPPort,
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

            var client = new HTTPTestClient(
                             IPAddress,
                             TCPPort,
                             Description,
                             HTTPUserAgent,
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

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,

                             ConsumeRequestChunkedTEImmediately,
                             ConsumeResponseChunkedTEImmediately
                         );

            var response = await client.ConnectAsync();

            return response.Item1
                       ? (client, [])
                       : (null,   response.Item2);

        }

        #endregion

        #region ConnectNew (URL,       DNSService = null, ..., DNSClient = null)

        /// <summary>
        /// Create a new HTTPTestClient and connect to the given URL.
        /// </summary>
        /// <param name="URL">The URL to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<HTTPTestClient>

            ConnectNew(URL                                                           URL,
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
                       IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols                  = null,
                       Boolean?                                                      AllowRenegotiation                    = null,
                       Boolean?                                                      AllowTLSResume                        = null,

                       Boolean?                                                      PreferIPv4                            = null,
                       TimeSpan?                                                     ConnectTimeout                        = null,
                       TimeSpan?                                                     ReceiveTimeout                        = null,
                       TimeSpan?                                                     SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                       UInt16?                                                       MaxNumberOfRetries                    = null,
                       UInt32?                                                       BufferSize                            = null,
                       DNSClient?                                                    DNSClient                             = null,

                       Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null)

        {

            var client = new HTTPTestClient(
                             URL,
                             Description,
                             HTTPUserAgent,
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

            await client.ConnectAsync();

            return client;

        }

        #endregion

        #region ConnectNew (DNSName,   DNSService,        ..., DNSClient = null)

        /// <summary>
        /// Create a new HTTPTestClient and connect to the given URL.
        /// </summary>
        /// <param name="DNSName">The DNS Name to lookup in order to resolve high available IP addresses and TCP ports.</param>
        /// <param name="DNSService">The DNS service to lookup in order to resolve high available IP addresses and TCP ports.</param>
  //      /// <param name="UseDNSURI">Whether to use DNS URI records to resolve the hostname to high available URIs.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<HTTPTestClient>
            ConnectNew(DomainName                                                    DNSName,
                       SRV_Spec                                                      DNSService,
                     //  Boolean?                                                      UseDNSURI                             = null,
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

                       Boolean?                                                      PreferIPv4                            = null,
                       TimeSpan?                                                     ConnectTimeout                        = null,
                       TimeSpan?                                                     ReceiveTimeout                        = null,
                       TimeSpan?                                                     SendTimeout                           = null,
                       TransmissionRetryDelayDelegate?                               TransmissionRetryDelay                = null,
                       UInt16?                                                       MaxNumberOfRetries                    = null,
                       UInt32?                                                       BufferSize                            = null,

                       Boolean?                                                      ConsumeRequestChunkedTEImmediately    = null,
                       Boolean?                                                      ConsumeResponseChunkedTEImmediately   = null,

                       TCPEchoLoggingDelegate?                                       LoggingHandler                        = null,
                       DNSClient?                                                    DNSClient                             = null)

        {

            var client = new HTTPTestClient(
                             DNSName,
                             DNSService,
                             Description,
                             HTTPUserAgent,
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

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize,

                             ConsumeRequestChunkedTEImmediately,
                             ConsumeResponseChunkedTEImmediately,

                             LoggingHandler,
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

            => $"{nameof(HTTPTestClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion

    }

}
