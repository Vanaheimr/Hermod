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

using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for certificates.
    /// </summary>
    public static class CertificateExtensions
    {

        #region GetDNSDomainNames             (this Certificate)

        /// <summary>
        /// The DNS DomainName entries of the certificate's Subject Alternative Name extension.
        /// </summary>
        /// <param name="Certificate">A certificate.</param>
        public static IEnumerable<DomainName> GetDNSDomainNames(this X509Certificate2 Certificate)

            => Certificate.SubjectAlternativeNameExtension()?.EnumerateDnsNames().Select(DomainName.Parse) ?? [];

        #endregion

        #region GetIIPAddresses               (this Certificate)

        /// <summary>
        /// The IPv4/v6 Address entries of the certificate's Subject Alternative Name extension.
        /// </summary>
        /// <param name="Certificate">A certificate.</param>
        public static IEnumerable<IIPAddress> GetIPAddresses(this X509Certificate2 Certificate)

            => Certificate.SubjectAlternativeNameExtension()?.EnumerateIPAddresses().Select(IPAddress.FromDotNet) ?? [];

        #endregion

        #region DecodeSubjectAlternativeNames (this Certificate)

        /// <summary>
        /// The certificate's subject alternative names, as "DNS-Name=..." and "IP-Address=..."
        /// strings.
        ///
        /// Prefer <see cref="GetDNSDomainNames"/> or <see cref="GetIIPAddresses"/>: they return the
        /// values themselves, and cannot be misread.
        ///
        /// This used to render the extension with AsnEncodedData.Format(), which delegates to
        /// the operating system — CryptFormatObject on Windows — and is localized. The same
        /// certificate produced "DNS-Name=" on a German installation and "DNS Name=" on an
        /// English one, so any caller matching on the prefix worked only on the machine it was
        /// written on. Norn's NTS-KE hostname verification did exactly that and silently found
        /// no names anywhere else. The prefixes below are now written here, in one language,
        /// whatever the host is set to.
        /// </summary>
        /// <param name="Certificate">A certificate.</param>
        public static IEnumerable<String> DecodeSubjectAlternativeNames(this X509Certificate2 Certificate)
        {

            var extension = Certificate.SubjectAlternativeNameExtension();

            if (extension is null)
                return [];

            return [
                       .. extension.EnumerateDnsNames().   Select(dnsName   => $"DNS-Name={dnsName}"),
                       .. extension.EnumerateIPAddresses().Select(ipAddress => $"IP-Address={ipAddress}")
                   ];

        }

        #endregion

    }

}
