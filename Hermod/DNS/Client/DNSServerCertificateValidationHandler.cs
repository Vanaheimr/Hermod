/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Whether a name server reached over TLS or HTTPS may be asked, judged by
    /// the certificate it showed at the handshake.
    /// </summary>
    /// <remarks>
    /// One delegate for every encrypted transport of a <see cref="DNSClient"/>,
    /// with the server it is about: the transport clients each have a handler of
    /// their own type, and whoever holds the resolver wants to judge the server,
    /// not the client object that happened to be talking to it.
    /// </remarks>
    /// <param name="Server">The name server, as the resolver knows it.</param>
    /// <param name="Certificate">The certificate it showed, or null where it showed none.</param>
    /// <param name="CertificateChain">The chain this machine built for it, with what the server sent beside it.</param>
    /// <param name="PolicyErrors">What building that chain, and comparing the name, found.</param>
    public delegate TLSValidationResult DNSServerCertificateValidationHandler(DNSServerConfig    Server,
                                                                              X509Certificate2?  Certificate,
                                                                              X509Chain?         CertificateChain,
                                                                              SslPolicyErrors    PolicyErrors);

}
