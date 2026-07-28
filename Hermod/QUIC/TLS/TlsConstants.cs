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
/// TLS 1.3 handshake message types (RFC 8446, §4).
/// </summary>
public enum HandshakeType : byte
{
    ClientHello = 1,
    ServerHello = 2,
    NewSessionTicket = 4,
    EncryptedExtensions = 8,
    Certificate = 11,
    CertificateRequest = 13,
    CertificateVerify = 15,
    Finished = 20,
}

/// <summary>
/// TLS extension types (RFC 8446 §4.2 and RFC 9001 §8.2).
/// </summary>
public enum ExtensionType : ushort
{
    ServerName = 0,
    SupportedGroups = 10,
    SignatureAlgorithms = 13,
    Alpn = 16,
    PreSharedKey = 41,          // RFC 8446 §4.2.11 – must be the LAST extension in the ClientHello
    EarlyData = 42,             // RFC 8446 §4.2.10 – 0-RTT
    SupportedVersions = 43,
    PskKeyExchangeModes = 45,   // RFC 8446 §4.2.9
    KeyShare = 51,
    QuicTransportParameters = 57,
}

/// <summary>
/// PSK key-exchange modes (RFC 8446 §4.2.9). For QUIC we use psk_dhe_ke (PSK with (EC)DHE).
/// </summary>
public enum PskKeyExchangeMode : byte
{
    PskKe = 0,     // pure PSK without (EC)DHE – no forward secrecy
    PskDheKe = 1,  // PSK + (EC)DHE (default)
}

/// <summary>
/// TLS 1.3 cipher suites (RFC 8446 §B.4). Initial packets use TLS_AES_128_GCM_SHA256 by definition.
/// </summary>
public enum CipherSuite : ushort
{
    Aes128GcmSha256 = 0x1301,
    Aes256GcmSha384 = 0x1302,
    ChaCha20Poly1305Sha256 = 0x1303,
}

/// <summary>
/// Named groups / key-exchange methods (RFC 8446 §4.2.7).
/// </summary>
public enum NamedGroup : ushort
{
    Secp256r1 = 0x0017,
    Secp384r1 = 0x0018,
    X25519 = 0x001d,
    X448 = 0x001e,
    X25519MlKem768 = 0x11ec, // post-quantum hybrid (ML-KEM-768 + X25519), draft-ietf-tls-ecdhe-mlkem
}

/// <summary>
/// Signature schemes for signature_algorithms / CertificateVerify (RFC 8446 §4.2.3).
/// </summary>
public enum SignatureScheme : ushort
{
    EcdsaSecp256r1Sha256 = 0x0403,
    EcdsaSecp384r1Sha384 = 0x0503,
    Ed25519 = 0x0807,
    Ed448 = 0x0808,
    RsaPssRsaeSha256 = 0x0804,
    RsaPssRsaeSha384 = 0x0805,
    RsaPssRsaeSha512 = 0x0806,
    RsaPkcs1Sha256 = 0x0401,
    RsaPkcs1Sha384 = 0x0501,

    /// <summary>
    /// ML-DSA-44 (FIPS 204, draft-ietf-tls-mldsa §3): post-quantum signature, pure (no pre-hash),
    /// the FIPS 204 context parameter MUST be empty.
    /// </summary>
    MLDsa44 = 0x0904,

    /// <summary>
    /// ML-DSA-65 (FIPS 204, draft-ietf-tls-mldsa §3).
    /// </summary>
    MLDsa65 = 0x0905,

    /// <summary>
    /// ML-DSA-87 (FIPS 204, draft-ietf-tls-mldsa §3).
    /// </summary>
    MLDsa87 = 0x0906,
}

/// <summary>
/// Shared TLS constants.
/// </summary>
public static class TlsVersions
{
    /// <summary>
    /// Sent in the legacy_version field for compatibility reasons.
    /// </summary>
    public const ushort Tls12 = 0x0303;

    /// <summary>
    /// The actually negotiated version (via supported_versions).
    /// </summary>
    public const ushort Tls13 = 0x0304;
}
