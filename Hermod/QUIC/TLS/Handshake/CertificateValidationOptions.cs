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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

/// <summary>
/// Controls how the client validates the server certificate. The <b>CertificateVerify signature</b>
/// (the cryptographic binding of the certificate to the handshake, RFC 8446 §4.4.3) is <b>always</b>
/// verified — it is the actual MITM defence. These options only concern the separate
/// <b>trust policy</b>: chain building up to a trusted root, hostname and validity period.
/// </summary>
public sealed record CertificateValidationOptions
{
    /// <summary>
    /// Builds the certificate chain up to a trusted root and validates it (default: on).
    /// </summary>
    public bool VerifyCertificateChain { get; init; } = true;

    /// <summary>
    /// Checks whether the hostname matches the certificate (SAN/CN, RFC 6125). Default: on.
    /// </summary>
    public bool VerifyHostname { get; init; } = true;

    /// <summary>
    /// Additional trust anchors. When set, validation runs exclusively against these
    /// (custom root trust) instead of the system trust store — useful for deliberately
    /// trusting a self-signed test certificate.
    /// </summary>
    public X509Certificate2Collection? CustomTrustRoots { get; init; }

    /// <summary>
    /// Default: full validation against the system roots incl. hostname.
    /// </summary>
    public static CertificateValidationOptions Default { get; } = new();

    /// <summary>
    /// Like <c>curl -k</c>: chain and hostname are NOT checked, but the CertificateVerify signature
    /// still is. Meant for self-signed test servers (e.g. localhost).
    /// </summary>
    public static CertificateValidationOptions Insecure { get; } = new()
    {
        VerifyCertificateChain = false,
        VerifyHostname = false,
    };
}
