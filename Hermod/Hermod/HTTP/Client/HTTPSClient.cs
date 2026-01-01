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
    /// An HTTPS client.
    /// </summary>
    public class HTTPSClient : AHTTPClient,
                               IHTTPClientCommands
    {

        #region Data

        /// <summary>
        /// The default HTTPS user agent.
        /// </summary>
        public new const String  DefaultHTTPUserAgent  = "GraphDefined HTTPS Client";

        #endregion


        #region HTTPSClient(RemoteURL, ...)

        /// <summary>
        /// Create a new HTTPS client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="Authentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPSClient(URL                                                        RemoteURL,
                           HTTPHostname?                                              VirtualHostname              = null,
                           I18NString?                                                Description                  = null,
                           Boolean?                                                   PreferIPv4                   = null,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                           LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                           SslStreamCertificateContext?                               ClientCertificateContext     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                           SslProtocols?                                              TLSProtocol                  = null,
                           HTTPContentType?                                           ContentType                  = null,
                           AcceptTypes?                                               Accept                       = null,
                           IHTTPAuthentication?                                       Authentication               = null,
                           TOTPConfig?                                                TOTPConfig                   = null,
                           String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                           ConnectionType?                                            Connection                   = null,
                           TimeSpan?                                                  RequestTimeout               = null,
                           TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                           UInt16?                                                    MaxNumberOfRetries           = null,
                           UInt32?                                                    InternalBufferSize           = null,
                           Boolean                                                    UseHTTPPipelining            = false,
                           Boolean?                                                   DisableLogging               = false,
                           HTTPClientLogger?                                          HTTPLogger                   = null,
                           IDNSClient?                                                DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator ?? ((sender,
                                                   certificate,
                                                   certificateChain,
                                                   tlsClient,
                                                   policyErrors) => DefaultCertificateValidator(
                                                                        sender,
                                                                        certificate,
                                                                        certificateChain,
                                                                        tlsClient,
                                                                        policyErrors
                                                                    )),
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   Authentication,
                   TOTPConfig,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(RemoteIPAddress, RemotePort = null, ...)

        /// <summary>
        /// Create a new HTTPS client.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPSClient(IIPAddress                                                 RemoteIPAddress,
                           IPPort?                                                    RemotePort                   = null,
                           HTTPHostname?                                              VirtualHostname              = null,
                           I18NString?                                                Description                  = null,
                           Boolean?                                                   PreferIPv4                   = null,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                           LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                           SslStreamCertificateContext?                               ClientCertificateContext     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                           SslProtocols?                                              TLSProtocol                  = null,
                           HTTPContentType?                                           ContentType                  = null,
                           AcceptTypes?                                               Accept                       = null,
                           IHTTPAuthentication?                                       Authentication               = null,
                           TOTPConfig?                                                TOTPConfig                   = null,
                           String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                           ConnectionType?                                            Connection                   = null,
                           TimeSpan?                                                  RequestTimeout               = null,
                           TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                           UInt16?                                                    MaxNumberOfRetries           = null,
                           UInt32?                                                    InternalBufferSize           = null,
                           Boolean                                                    UseHTTPPipelining            = false,
                           Boolean?                                                   DisableLogging               = false,
                           HTTPClientLogger?                                          HTTPLogger                   = null,
                           IDNSClient?                                                DNSClient                    = null)

            : this(URL.Parse($"https://{RemoteIPAddress}{(RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : String.Empty)}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   Authentication,
                   TOTPConfig,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(RemoteSocket, ...)

        /// <summary>
        /// Create a new HTTPS client.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="Authentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPSClient(IPSocket                                                   RemoteSocket,
                           HTTPHostname?                                              VirtualHostname              = null,
                           I18NString?                                                Description                  = null,
                           Boolean?                                                   PreferIPv4                   = null,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                           LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                           SslStreamCertificateContext?                               ClientCertificateContext     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                           SslProtocols?                                              TLSProtocol                  = null,
                           HTTPContentType?                                           ContentType                  = null,
                           AcceptTypes?                                               Accept                       = null,
                           IHTTPAuthentication?                                       Authentication               = null,
                           TOTPConfig?                                                TOTPConfig                   = null,
                           String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                           ConnectionType?                                            Connection                   = null,
                           TimeSpan?                                                  RequestTimeout               = null,
                           TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                           UInt16?                                                    MaxNumberOfRetries           = null,
                           UInt32?                                                    InternalBufferSize           = null,
                           Boolean                                                    UseHTTPPipelining            = false,
                           Boolean?                                                   DisableLogging               = false,
                           HTTPClientLogger?                                          HTTPLogger                   = null,
                           IDNSClient?                                                DNSClient                    = null)

            : this(URL.Parse($"https://{RemoteSocket.IPAddress}:{RemoteSocket.Port}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   Authentication,
                   TOTPConfig,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(RemoteHost, ...)

        /// <summary>
        /// Create a new HTTPS client.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="Authentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public HTTPSClient(HTTPHostname                                               RemoteHost,
                           IPPort?                                                    RemotePort                   = null,
                           HTTPHostname?                                              VirtualHostname              = null,
                           I18NString?                                                Description                  = null,
                           Boolean?                                                   PreferIPv4                   = null,
                           RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                           LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificates           = null,
                           SslStreamCertificateContext?                               ClientCertificateContext     = null,
                           IEnumerable<X509Certificate2>?                             ClientCertificateChain       = null,
                           SslProtocols?                                              TLSProtocol                  = null,
                           HTTPContentType?                                           ContentType                  = null,
                           AcceptTypes?                                               Accept                       = null,
                           IHTTPAuthentication?                                       Authentication               = null,
                           TOTPConfig?                                                TOTPConfig                   = null,
                           String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                           ConnectionType?                                            Connection                   = null,
                           TimeSpan?                                                  RequestTimeout               = null,
                           TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                           UInt16?                                                    MaxNumberOfRetries           = null,
                           UInt32?                                                    InternalBufferSize           = null,
                           Boolean                                                    UseHTTPPipelining            = false,
                           Boolean?                                                   DisableLogging               = false,
                           HTTPClientLogger?                                          HTTPLogger                   = null,
                           IDNSClient?                                                DNSClient                    = null)

            : this(URL.Parse($"https://{RemoteHost}{(RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : String.Empty)}"),
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   Authentication,
                   TOTPConfig,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   UseHTTPPipelining,
                   DisableLogging,
                   HTTPLogger,
                   DNSClient)

        { }

        #endregion


        #region (private) DefaultCertificateValidator<T>(Sender, Certificate, CertificateChain, TLSClient, PolicyErrors)


        private static (Boolean, IEnumerable<String>)

            DefaultCertificateValidator<T>(Object             Sender,
                                           X509Certificate2?  Certificate,
                                           X509Chain?         CertificateChain,
                                           T                  TLSClient,
                                           SslPolicyErrors    PolicyErrors)

        {

            if (Certificate is null)
                return (false, []);

            if (PolicyErrors != SslPolicyErrors.None)
                return (false, []);

            using var chain = CertificateChain ?? new X509Chain();

            chain.ChainPolicy = new X509ChainPolicy {
                                    RevocationMode       = X509RevocationMode.Online,
                                    RevocationFlag       = X509RevocationFlag.ExcludeRoot,
                                    VerificationFlags    = X509VerificationFlags.NoFlag,
                                    VerificationTime     = Timestamp.Now.DateTime,
                                    UrlRetrievalTimeout  = TimeSpan.FromSeconds(15),
                                };

            return chain.Build(Certificate)
                       ? (true,  [])
                       : (false, chain.ChainStatus.Select(status => $"{status.Status}: {status.StatusInformation.Trim()}"));

        }

        #endregion


    }

}
