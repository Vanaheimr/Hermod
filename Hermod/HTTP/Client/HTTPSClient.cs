/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using System.Net.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTPS client.
    /// </summary>
    public class HTTPSClient : HTTPClient
    {

        #region HTTPSClient(RemoteIPAddress, RemoteCertificateValidator, RemotePort = null, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="RemotePort">An optional remote IP port to connect to [default: 443].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(IIPAddress                           RemoteIPAddress,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           IPPort                               RemotePort  = null,
                           DNSClient                            DNSClient   = null)

            : base(RemoteIPAddress,
                   RemotePort != null ? RemotePort : IPPort.Parse(443),
                   RemoteCertificateValidator,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(Socket, RemoteCertificateValidator, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(IPSocket                             RemoteSocket,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           DNSClient                            DNSClient  = null)

            : base(RemoteSocket,
                   RemoteCertificateValidator,
                   DNSClient)

        { }

        #endregion

        #region HTTPSClient(RemoteHost, RemoteCertificateValidator, RemotePort = null, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="RemotePort">An optional remote IP port to connect to [default: 443].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPSClient(String                               RemoteHost,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator,
                           IPPort                               RemotePort  = null,
                           DNSClient                            DNSClient   = null)

            : base(RemoteHost,
                   RemotePort != null ? RemotePort : IPPort.Parse(443),
                   RemoteCertificateValidator,
                   DNSClient)

        { }

        #endregion

    }

}
