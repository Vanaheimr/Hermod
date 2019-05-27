/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
    /// A HTTPS client.
    /// </summary>
    public class HTTPSClient : HTTPClient
    {

        #region Data

        /// <summary>
        /// The default HTTPS/TCP Port.
        /// </summary>
        public static  IPPort    DefaultHTTPSPort       = IPPort.Parse(443);

        /// <summary>
        /// The default HTTPS user agent.
        /// </summary>
        public const   String    DefaultUserAgent       = "Vanaheimr Hermod HTTPS Client v0.1";

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public static  TimeSpan  DefaultRequestTimeout  = TimeSpan.FromSeconds(60);

        #endregion

        #region HTTPSClient(RemoteIPAddress, RemoteCertificateValidator, ...)

        /// <summary>
        /// Create a new HTTPS client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RemotePort">An optional remote IP port to connect to [default: 443].</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(IIPAddress                           RemoteIPAddress,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           LocalCertificateSelectionCallback    LocalCertificateSelector   = null,
                           X509Certificate                      ClientCert                 = null,
                           String                               UserAgent                  = DefaultUserAgent,
                           IPPort?                              RemotePort                 = null,
                           TimeSpan?                            RequestTimeout             = null,
                           DNSClient                            DNSClient                  = null)

            : base(RemoteIPAddress,
                   RemotePort                 ?? DefaultHTTPSPort,
                   RemoteCertificateValidator ?? throw new ArgumentNullException(nameof(RemoteCertificateValidator), "The given delegate for verifiying the remote SSL/TLS certificate must not be null!"),
                   LocalCertificateSelector,
                   ClientCert,
                   UserAgent                  ?? DefaultUserAgent,
                   RequestTimeout             ?? DefaultRequestTimeout,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(Socket,          RemoteCertificateValidator, ...)

        /// <summary>
        /// Create a new HTTPS client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(IPSocket                             RemoteSocket,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           LocalCertificateSelectionCallback    LocalCertificateSelector   = null,
                           X509Certificate                      ClientCert                 = null,
                           String                               UserAgent                  = DefaultUserAgent,
                           TimeSpan?                            RequestTimeout             = null,
                           DNSClient                            DNSClient                  = null)

            : base(RemoteSocket,
                   RemoteCertificateValidator ?? throw new ArgumentNullException(nameof(RemoteCertificateValidator), "The given delegate for verifiying the remote SSL/TLS certificate must not be null!"),
                   LocalCertificateSelector,
                   ClientCert,
                   UserAgent                  ?? DefaultUserAgent,
                   RequestTimeout             ?? DefaultRequestTimeout,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(RemoteHost,      RemoteCertificateValidator, ...)

        /// <summary>
        /// Create a new HTTPS client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="RemotePort">An optional remote IP port to connect to [default: 443].</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(HTTPHostname                         RemoteHost,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           LocalCertificateSelectionCallback    LocalCertificateSelector   = null,
                           X509Certificate                      ClientCert                 = null,
                           IPPort?                              RemotePort                 = null,
                           String                               UserAgent                  = DefaultUserAgent,
                           TimeSpan?                            RequestTimeout             = null,
                           DNSClient                            DNSClient                  = null)

            : base(RemoteHost,
                   RemotePort                 ?? DefaultHTTPSPort,
                   RemoteCertificateValidator ?? throw new ArgumentNullException(nameof(RemoteCertificateValidator), "The given delegate for verifiying the remote SSL/TLS certificate must not be null!"),
                   LocalCertificateSelector,
                   ClientCert,
                   UserAgent                  ?? DefaultUserAgent,
                   RequestTimeout             ?? DefaultRequestTimeout,
                   DNSClient)

        { }

        #endregion

    }

}
