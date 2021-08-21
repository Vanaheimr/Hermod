/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A factory to create a HTTPClient or HTTPSClient based on the given URL protocol.
    /// </summary>
    public static class HTTPClientFactory
    {

        /// <summary>
        /// Create a new HTTP/HTTPS client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public static IHTTPClientCommands Create(URL                                  RemoteURL,
                                                 HTTPHostname?                        VirtualHostname              = null,
                                                 String                               Description                  = null,
                                                 RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                                                 LocalCertificateSelectionCallback    ClientCertificateSelector    = null,
                                                 X509Certificate                      ClientCert                   = null,
                                                 String                               HTTPUserAgent                = HTTPSClient.DefaultHTTPUserAgent,
                                                 TimeSpan?                            RequestTimeout               = null,
                                                 TransmissionRetryDelayDelegate       TransmissionRetryDelay       = null,
                                                 UInt16?                              MaxNumberOfRetries           = HTTPSClient.DefaultMaxNumberOfRetries,
                                                 Boolean                              UseHTTPPipelining            = false,
                                                 HTTPClientLogger                     HTTPLogger                   = null,
                                                 DNSClient                            DNSClient                    = null)

            => RemoteURL.Protocol == HTTPProtocols.http

                                         ? new HTTPClient(RemoteURL,
                                                           VirtualHostname,
                                                           Description,
                                                           HTTPUserAgent,
                                                           RequestTimeout,
                                                           TransmissionRetryDelay,
                                                           MaxNumberOfRetries,
                                                           UseHTTPPipelining,
                                                           HTTPLogger,
                                                           DNSClient) as IHTTPClientCommands

                                         : new HTTPSClient(RemoteURL,
                                                           VirtualHostname,
                                                           Description,
                                                           RemoteCertificateValidator,
                                                           ClientCertificateSelector,
                                                           ClientCert,
                                                           HTTPUserAgent,
                                                           RequestTimeout,
                                                           TransmissionRetryDelay,
                                                           MaxNumberOfRetries,
                                                           UseHTTPPipelining,
                                                           HTTPLogger,
                                                           DNSClient) as IHTTPClientCommands;

    }

}
