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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

/// <summary>
/// How firmly a server asks for a client certificate (RFC 8446 §4.3.2/§4.4.2.4). The RFC leaves the
/// decision to the server: when a client answers with an empty Certificate, the server "MAY at its
/// discretion either continue the handshake without client authentication or abort the handshake
/// with a certificate_required alert" — that discretion is exactly what this enum expresses.
/// </summary>
public enum ClientCertificateMode
{
    /// <summary>
    /// No CertificateRequest is sent; the client is never asked. The default.
    /// </summary>
    None,

    /// <summary>
    /// Ask, but accept a client that declines or presents an untrusted certificate. The result is
    /// visible afterwards, so the application can decide for itself (e.g. answer 403).
    /// </summary>
    Request,

    /// <summary>
    /// Ask and insist: a missing or untrusted certificate fails the handshake. This is mutual TLS in
    /// the usual sense.
    /// </summary>
    Require,
}

/// <summary>
/// Server-side configuration for client authentication (mutual TLS).
/// </summary>
public sealed record ClientCertificateOptions
{
    /// <summary>
    /// How firmly to ask.
    /// </summary>
    public ClientCertificateMode Mode { get; init; } = ClientCertificateMode.Require;

    /// <summary>
    /// Trust policy applied to the presented chain. Hostname verification is always off — a client
    /// certificate identifies a client, not a host we dialled.
    /// </summary>
    public CertificateValidationOptions Validation { get; init; } = CertificateValidationOptions.Default;

    /// <summary>
    /// Signature schemes offered in the CertificateRequest (RFC 8446 §4.3.2: signature_algorithms
    /// MUST be present). The default mirrors what this stack can verify.
    /// </summary>
    public IReadOnlyList<SignatureScheme> SignatureSchemes { get; init; } =
    [
        SignatureScheme.EcdsaSecp256r1Sha256,
        SignatureScheme.EcdsaSecp384r1Sha384,
        SignatureScheme.Ed25519,
        SignatureScheme.Ed448,
        SignatureScheme.RsaPssRsaeSha256,
        SignatureScheme.RsaPssRsaeSha384,
        SignatureScheme.RsaPssRsaeSha512,
        SignatureScheme.RsaPkcs1Sha256,
        SignatureScheme.MLDsa44,
        SignatureScheme.MLDsa65,
        SignatureScheme.MLDsa87,
    ];

    /// <summary>
    /// Requires a client certificate that chains to <paramref name="trustRoots"/>.
    /// </summary>
    public static ClientCertificateOptions RequireFrom(params System.Security.Cryptography.X509Certificates.X509Certificate2[] trustRoots)
        => new()
        {
            Mode = ClientCertificateMode.Require,
            Validation = new CertificateValidationOptions
            {
                VerifyHostname = false,
                CustomTrustRoots = [.. trustRoots],
            },
        };
}

/// <summary>
/// What client authentication produced — available on the server connection once the handshake is
/// through.
/// </summary>
/// <param name="Requested">Whether a CertificateRequest was sent at all.</param>
/// <param name="Certificate">
/// The validated leaf certificate, or <c>null</c> when the client declined or failed validation
/// under <see cref="ClientCertificateMode.Request"/>.
/// </param>
/// <param name="FailureReason">Why validation failed, when it did.</param>
public sealed record ClientAuthenticationResult(
    bool Requested,
    System.Security.Cryptography.X509Certificates.X509Certificate2? Certificate,
    string? FailureReason)
{
    /// <summary>
    /// <c>true</c> when the client proved possession of a certificate we accepted.
    /// </summary>
    public bool IsAuthenticated => Certificate is not null;

    /// <summary>
    /// Nothing was asked for.
    /// </summary>
    public static ClientAuthenticationResult NotRequested { get; } = new(false, null, null);
}
