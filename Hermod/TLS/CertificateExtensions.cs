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

        #region GetDNSNamePatterns            (this Certificate)

        /// <summary>
        /// The dNSName entries of the certificate's Subject Alternative Name extension, as
        /// RFC 9525 § 6.3 presented identifiers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <see cref="DNSNamePattern"/> rather than a <see cref="DomainName"/> because a
        /// certificate name need not be a host name: "*.example.com" is the commonest thing a
        /// commercial certificate authority issues, and <c>DomainName.Parse</c> throws on it.
        /// This used to return domain names and did exactly that — one wildcard entry and the
        /// whole enumeration threw, on the certificates most likely to be in front of a real
        /// server.
        /// </para>
        /// <para>
        /// Entries that are not valid presented identifiers are dropped rather than reported.
        /// § 6.3: an invalid one "MUST be ignored", and a certificate carrying one alongside
        /// good names is still usable through the good ones.
        /// </para>
        /// </remarks>
        /// <param name="Certificate">A certificate.</param>
        public static IEnumerable<DNSNamePattern> GetDNSNamePatterns(this X509Certificate2 Certificate)

            => DNSNamePattern.ParseAll(
                   Certificate.SubjectAlternativeNameExtension()?.EnumerateDnsNames() ?? []
               );

        #endregion

        #region MatchesHostName               (this Certificate, HostName)

        /// <summary>
        /// Whether any dNSName entry of this certificate matches the given host name, by
        /// RFC 9525 § 6.3.
        /// </summary>
        /// <remarks>
        /// Only the subject alternative names are consulted. The Common Name is not a fallback:
        /// RFC 9525 § 6.1 dropped it, browsers stopped accepting it years earlier, and a client
        /// that still falls back to it accepts certificates that no certificate authority may
        /// issue and no other client will honour.
        /// </remarks>
        /// <param name="Certificate">A certificate.</param>
        /// <param name="HostName">The host name the client set out to reach.</param>
        public static Boolean MatchesHostName(this X509Certificate2  Certificate,
                                              DomainName             HostName)

            => Certificate.GetDNSNamePatterns().
                   Any(pattern => pattern.Matches(HostName));

        #endregion

        #region GetIIPAddresses               (this Certificate)

        /// <summary>
        /// The IPv4/v6 Address entries of the certificate's Subject Alternative Name extension.
        /// </summary>
        /// <remarks>
        /// Named as the region above it always said, and not as the method itself did. Styx has
        /// a <c>GetIPAddresses</c> returning <see cref="System.Net.IPAddress"/>, and with both
        /// namespaces imported — which is the normal case — neither could be called. The extra I
        /// is for the interface this one returns.
        /// </remarks>
        /// <param name="Certificate">A certificate.</param>
        public static IEnumerable<IIPAddress> GetIIPAddresses(this X509Certificate2 Certificate)

            => Certificate.SubjectAlternativeNameExtension()?.EnumerateIPAddresses().Select(IPAddress.FromDotNet) ?? [];

        #endregion

        #region DecodeSANs                    (this Certificate)

        /// <summary>
        /// The certificate's subject alternative names, as "DNS-Name=..." and "IP-Address=..."
        /// strings.
        ///
        /// Prefer <see cref="GetDNSNamePatterns"/> or <see cref="GetIIPAddresses"/>: they return
        /// the values themselves, and cannot be misread.
        ///
        /// Named for the abbreviation rather than spelled out because Styx offers the same
        /// method under the spelled-out name, and the two namespaces are almost always imported
        /// together — which made every call to either of them ambiguous.
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
        public static IEnumerable<String> DecodeSANs(this X509Certificate2 Certificate)
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
