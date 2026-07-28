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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// TLS alert descriptions (RFC 8446 §6). QUIC carries no alert records: RFC 9001 §4.8 turns an alert
/// into the QUIC transport error <c>CRYPTO_ERROR</c>, whose code is <c>0x0100 + AlertDescription</c>.
/// Only the values this stack actually raises are listed.
/// </summary>
public enum TlsAlert : byte
{
    /// <summary>An unexpected message arrived for the current state (§6.2).</summary>
    UnexpectedMessage = 10,

    /// <summary>A certificate was unacceptable — e.g. an untrusted chain (§6.2).</summary>
    BadCertificate = 42,

    /// <summary>Negotiation failed; the generic handshake error (§6.2).</summary>
    HandshakeFailure = 40,

    /// <summary>A message could not be decoded (§6.2).</summary>
    DecodeError = 50,

    /// <summary>
    /// The server requires a client certificate and did not get an acceptable one
    /// (RFC 8446 §4.4.2.4).
    /// </summary>
    CertificateRequired = 116,
}

/// <summary>
/// A handshake failure that names the TLS alert it corresponds to, so the QUIC layer can report the
/// precise <c>CRYPTO_ERROR</c> code (RFC 9001 §4.8) instead of a catch-all.
/// </summary>
public sealed class TlsHandshakeException(TlsAlert alert, string message) : Exception(message)
{
    /// <summary>
    /// The alert a TLS-over-TCP endpoint would have sent here.
    /// </summary>
    public TlsAlert Alert { get; } = alert;
}
