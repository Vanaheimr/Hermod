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

using Org.BouncyCastle.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// No TLS Client Authentication
    /// </summary>
    public class NoTLSClientAuthentication : TlsAuthentication
    {

        /// <summary>
        /// Called by the protocol handler to report the server certificate.
        /// </summary>
        /// <param name="ServerCertificate">The received server certificate.</param>
        public void NotifyServerCertificate(TlsServerCertificate ServerCertificate)
        {
            // Check server certificate for validity/pinning
        }

        /// <summary>
        /// Return the client credentials in response to server's certificate request.
        /// </summary>
        /// <param name="CertificateRequest">Details of the certificate request.</param>
        public TlsCredentials GetClientCredentials(CertificateRequest CertificateRequest)
        {
            // If no client cert needed, return null
            return null;
        }

    }

}
