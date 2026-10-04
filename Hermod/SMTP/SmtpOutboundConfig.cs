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

using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed record SmtpOutboundConfig
    {
        public required String   LocalHostname        { get; init; }
        public          UInt32   ConnectTimeoutMs     { get; init; } = 30_000;
        public          UInt32   ReadTimeoutMs        { get; init; } = 60_000;
        public          UInt32   WriteTimeoutMs       { get; init; } = 60_000;
        public          Boolean  RequireStartTls      { get; init; } = false;
        public          Boolean  PreferStartTls       { get; init; } = true;

        /// <summary>
        /// Require a valid server certificate for EVERY TLS delivery (not just enforced ones).
        /// Default false = opportunistic TLS (RFC 7435): encrypt even with a bad certificate.
        /// Certificates are always validated strictly when TLS is enforced (MTA-STS enforce,
        /// REQUIRETLS, or RequireStartTls), regardless of this flag.
        /// </summary>
        public          Boolean  RequireValidCertificate { get; init; } = false;

        /// <summary>
        /// Decide on a next hop's certificate yourself - for a private CA, or a pinned certificate:
        /// (MX host, certificate, chain, policy errors) -> accept. When set, it replaces the PKIX
        /// check, strict or opportunistic; DANE (RFC 7672) still authenticates against TLSA alone.
        /// </summary>
        public          Func<String, System.Security.Cryptography.X509Certificates.X509Certificate2?, System.Security.Cryptography.X509Certificates.X509Chain?, System.Net.Security.SslPolicyErrors, Boolean>?  RemoteCertificateValidator { get; init; }

        /// <summary>
        /// Enable DANE (RFC 7672): look up DNSSEC-validated TLSA records for the target MX and,
        /// when present, enforce STARTTLS and authenticate the server certificate against them.
        /// Requires a DNSSEC-aware resolver path (the DNS client's DO bit is enabled automatically).
        /// Default false.
        /// </summary>
        public          Boolean  EnableDane           { get; init; } = false;

        /// <summary>
        /// The HTTP handler that fetches MTA-STS policies (RFC 8461 §3.3) - for a proxy, or a policy
        /// host whose certificate the system does not trust. By default one that follows no
        /// redirects; with any handler, a policy reached through a redirect is not taken.
        /// </summary>
        public          HttpMessageHandler?  MtaStsHttpHandler  { get; init; }

        public          String?  SmartHost            { get; init; }  // Optional relay host
        public          UInt16   SmartHostPort        { get; init; } = 25;
        public          String?  SmartHostUsername    { get; init; }
        public          String?  SmartHostPassword    { get; init; }
    }

}
