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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// An (EC)DHE key exchange for TLS 1.3 (RFC 8446 §4.2.8). Abstracts over the concrete group so
/// that transport/handshake stay unchanged, whether P-256 (BCL) or X25519 (BouncyCastle).
/// </summary>
public interface IKeyExchange : IDisposable
{
    NamedGroup Group { get; }

    /// <summary>
    /// The public key in the group's wire format (P-256: 0x04‖X‖Y; X25519: 32 bytes).
    /// </summary>
    byte[] PublicKey { get; }

    /// <summary>
    /// Derives the shared secret from the peer's public key (client side).
    /// </summary>
    byte[] DeriveSharedSecret(ReadOnlySpan<byte> peerPublicKey);

    /// <summary>
    /// Server side: produces from the client's key share our own response (key share) and the shared
    /// secret. For classic (EC)DHE the response is simply our own public key (and the secret the
    /// DH product); for a KEM hybrid (X25519MLKEM768) the response is the ML-KEM ciphertext
    /// (‖ X25519) from the encapsulation against the client key. This asymmetry is the reason for
    /// the separate client/server methods — with a KEM, the server response depends on the client share.
    /// </summary>
    (byte[] ResponseShare, byte[] SharedSecret) Encapsulate(ReadOnlySpan<byte> peerShare)
        => (PublicKey, DeriveSharedSecret(peerShare));
}

/// <summary>
/// Creates the matching key exchange for a named group.
/// </summary>
public static class KeyExchange
{
    /// <summary>
    /// Default order of the offered groups: X25519 first (the field standard), then P-256.
    /// </summary>
    public static IReadOnlyList<NamedGroup> DefaultGroups { get; } = [NamedGroup.X25519, NamedGroup.Secp256r1];

    public static IKeyExchange Create(NamedGroup group) => group switch
    {
        NamedGroup.X25519 => new X25519KeyExchange(),
        NamedGroup.X448 => new X448KeyExchange(),
        NamedGroup.X25519MlKem768 => new X25519MlKem768KeyExchange(),
        NamedGroup.Secp256r1 or NamedGroup.Secp384r1 => EcdheKeyExchange.Create(group),
        _ => throw new NotSupportedException($"Named group {group} is not supported."),
    };

    public static bool IsSupported(NamedGroup group)
        => group is NamedGroup.X25519 or NamedGroup.X448 or NamedGroup.X25519MlKem768
            or NamedGroup.Secp256r1 or NamedGroup.Secp384r1;
}
