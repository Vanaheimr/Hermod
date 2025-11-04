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

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.JSON
{

    /// <summary>
    /// A specialized HTTP client for JSON transport.
    /// </summary>
    public class JSONClient : AJSONClient
    {

        #region Constructor

        /// <summary>
        /// Create a new specialized HTTP client for the JavaScript Object Notation (JSON).
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">An optional connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public JSONClient(URL                                                        RemoteURL,
                          HTTPHostname?                                              VirtualHostname              = null,
                          I18NString?                                                Description                  = null,
                          Boolean?                                                   PreferIPv4                   = null,
                          RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                          X509Certificate2?                                          ClientCertificate            = null,
                          SslProtocols?                                              TLSProtocol                  = null,
                          HTTPContentType?                                           ContentType                  = null,
                          AcceptTypes?                                               Accept                       = null,
                          IHTTPAuthentication?                                       HTTPAuthentication           = null,
                          TOTPConfig?                                                TOTPConfig                   = null,
                          String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                          ConnectionType?                                            Connection                   = null,
                          TimeSpan?                                                  RequestTimeout               = null,
                          TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                          UInt16?                                                    MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                          UInt32?                                                    InternalBufferSize           = null,
                          Boolean?                                                   DisableLogging               = false,
                          DNSClient?                                                 DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCertificate,
                   TLSProtocol,
                   ContentType,
                   Accept,
                   HTTPAuthentication,
                   TOTPConfig,
                   HTTPUserAgent,
                   Connection,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   DisableLogging,
                   DNSClient)

        { }

        #endregion

    }

}
