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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Net;
    using System.Security.Cryptography.X509Certificates;


    /// <summary>
    /// Deciding whether this server is authoritative for the origin a request
    /// names — the question behind 421 (Misdirected Request).
    ///
    /// It matters because of connection reuse (RFC 9113, Section 9.1.1): a client
    /// that already holds a connection to us MAY send a request for a *different*
    /// origin over it, as long as our certificate is valid for that origin too
    /// ("connection coalescing"). The origin it names travels in <c>:authority</c>,
    /// which is therefore not guaranteed to be the name it dialed. A server that
    /// never looks at it answers for origins it may know nothing about; a server
    /// that does can decline with 421 and have the client retry elsewhere (RFC
    /// 9110, Section 15.5.20).
    ///
    /// The name matching follows RFC 6125, Section 6.4: exact match, or a wildcard
    /// in the left-most label only. Certificate identity comes from the
    /// subjectAltName extension when present — the common name is consulted only
    /// for certificates that carry no SAN at all, as CN-as-identity is deprecated.
    /// </summary>
    public static class HTTPAuthority
    {

        #region HostOf (Authority)

        /// <summary>
        /// The host component of an <c>:authority</c> (or <c>Host</c>) value:
        /// without any userinfo prefix, without the port, and with an IPv6
        /// literal's brackets removed.
        /// </summary>
        /// <param name="Authority">An authority such as "example.com:8443", "[::1]:8443" or "127.0.0.1".</param>
        public static String HostOf(String Authority)
        {

            var host = Authority;

            // "user@host" — deprecated in HTTP URIs, but it costs nothing to be
            // robust about it.
            var at = host.LastIndexOf('@');
            if (at >= 0)
                host = host[(at + 1)..];

            // An IPv6 literal is bracketed precisely so its colons cannot be
            // confused with the port separator.
            if (host.StartsWith('['))
            {
                var end = host.IndexOf(']');
                return end > 0
                           ? host[1..end]
                           : host;
            }

            var colon = host.LastIndexOf(':');

            return colon >= 0
                       ? host[..colon]
                       : host;

        }

        #endregion

        #region MatchesName (Host, Name)

        /// <summary>
        /// Match a host against one certificate identity, per RFC 6125, Section
        /// 6.4.3: a wildcard is allowed only as the complete left-most label, and
        /// it matches exactly one label — "*.example.com" covers "a.example.com"
        /// but neither "example.com" nor "a.b.example.com".
        /// </summary>
        public static Boolean MatchesName(String Host, String Name)
        {

            if (Host.Length == 0 || Name.Length == 0)
                return false;

            if (Name.StartsWith("*.", StringComparison.Ordinal))
            {

                var dot = Host.IndexOf('.');

                return dot > 0 &&
                       String.Equals(Host[(dot + 1)..], Name[2..], StringComparison.OrdinalIgnoreCase);

            }

            return String.Equals(Host, Name, StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #region ServedByOrigins (Origins)

        /// <summary>
        /// A predicate accepting exactly the authorities named by a set of origins
        /// in RFC 6454 serialization ("https://example.com:8443") — the natural
        /// companion to announcing that same set in an ORIGIN frame (RFC 8336).
        ///
        /// Unlike certificate identities, an origin also pins the port, so matching
        /// is exact rather than wildcarded; an origin on the scheme's default port
        /// additionally matches the port-less authority, since that is how a client
        /// writes it ("https://example.com" is reached as <c>:authority
        /// example.com</c>).
        /// </summary>
        /// <param name="Origins">Origins in RFC 6454 serialization.</param>
        public static Func<String, Boolean> ServedByOrigins(IEnumerable<String> Origins)
        {

            var authorities = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            foreach (var origin in Origins)
            {

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    continue;

                authorities.Add($"{uri.Host}:{uri.Port}");

                // On the scheme's default port the authority is normally written
                // without one — but a client may still spell it out, so accept both.
                if (uri.IsDefaultPort)
                    authorities.Add(uri.Host);

            }

            return authority => authorities.Contains(authority);

        }

        #endregion

        #region ServedByCertificate (Certificate)

        /// <summary>
        /// A predicate accepting exactly those authorities the given server
        /// certificate is valid for — the natural default for an origin server,
        /// since it is precisely the set a client is entitled to coalesce onto
        /// this connection.
        /// </summary>
        /// <param name="Certificate">The server certificate presented on this listener.</param>
        public static Func<String, Boolean> ServedByCertificate(X509Certificate2 Certificate)
        {

            var dnsNames    = new List<String>();
            var ipAddresses = new List<IPAddress>();

            foreach (var extension in Certificate.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.17")   // subjectAltName
                {

                    var san = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);

                    dnsNames.   AddRange(san.EnumerateDnsNames());
                    ipAddresses.AddRange(san.EnumerateIPAddresses());

                }
            }

            // Only for certificates without any SAN — using the common name as an
            // identity has been deprecated since RFC 2818 was replaced.
            if (dnsNames.Count == 0 && ipAddresses.Count == 0)
            {
                var commonName = Certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                if (!String.IsNullOrEmpty(commonName))
                    dnsNames.Add(commonName);
            }

            return authority => {

                var host = HostOf(authority);

                if (host.Length == 0)
                    return false;

                // An IP-literal authority may only be matched by an iPAddress SAN,
                // never by a dNSName — they are different name forms.
                if (IPAddress.TryParse(host, out var ip))
                    return ipAddresses.Any(address => address.Equals(ip));

                return dnsNames.Any(name => MatchesName(host, name));

            };

        }

        #endregion

    }

}
