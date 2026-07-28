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

using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// Post-quantum hybrid <c>X25519MLKEM768</c> (named group 0x11EC, draft-ietf-tls-ecdhe-mlkem): combines
/// the classic X25519 key exchange (BouncyCastle) with the ML-KEM-768 KEM (FIPS 203, native in the BCL
/// of .NET 10 as <see cref="MLKem"/>). Motivation: "harvest now, decrypt later" — even if X25519
/// later falls to a quantum computer, ML-KEM protects the secret; and even if ML-KEM had a
/// classical weakness, X25519 protects it. Chrome/Firefox/Cloudflare have run the hybrid since 2024/25.
/// <para>
/// Unlike pure (EC)DHE, a KEM is asymmetric: the client sends an ML-KEM encapsulation key and later
/// <b>decapsulates</b> the server's ciphertext; the server <b>encapsulates</b> against the client key
/// (<see cref="Encapsulate"/>). Order per the draft for <c>X25519MLKEM768</c>: ML-KEM part first, then
/// X25519 — both in the key shares and in the shared secret.
/// </para>
/// </summary>
public sealed class X25519MlKem768KeyExchange : IKeyExchange
{
    // Fixed sizes for ML-KEM-768 (FIPS 203) and X25519.
    private const int MlKemEncapsulationKeyLength = 1184;
    private const int MlKemCiphertextLength = 1088;
    private const int MlKemSharedSecretLength = 32;
    private const int X25519KeyLength = 32;

    private readonly MLKem _mlKem;                 // client role: holds the decapsulation key
    private readonly X25519KeyExchange _x25519;

    public NamedGroup Group => NamedGroup.X25519MlKem768;

    /// <summary>
    /// The client's offered key share: ML-KEM encapsulation key (1184) ‖ X25519 public key (32) = 1216 bytes.
    /// </summary>
    public byte[] PublicKey { get; }

    public X25519MlKem768KeyExchange()
    {
        _mlKem = MLKem.GenerateKey(MLKemAlgorithm.MLKem768);
        _x25519 = new X25519KeyExchange();

        PublicKey = new byte[MlKemEncapsulationKeyLength + X25519KeyLength];
        _mlKem.ExportEncapsulationKey(PublicKey.AsSpan(0, MlKemEncapsulationKeyLength));
        _x25519.PublicKey.CopyTo(PublicKey.AsSpan(MlKemEncapsulationKeyLength));
    }

    /// <summary>
    /// Client side: derive the secret from the server share (ML-KEM ciphertext ‖ X25519 public key) —
    /// decapsulate ML-KEM and compute X25519. Result: ML-KEM secret (32) ‖ X25519 secret (32) = 64 bytes.
    /// </summary>
    public byte[] DeriveSharedSecret(ReadOnlySpan<byte> peerShare)
    {
        if (peerShare.Length != MlKemCiphertextLength + X25519KeyLength)
            throw new ArgumentException(
                $"X25519MLKEM768 server share must be {MlKemCiphertextLength + X25519KeyLength} bytes long.", nameof(peerShare));

        byte[] mlKemSecret = _mlKem.Decapsulate(peerShare[..MlKemCiphertextLength].ToArray());
        byte[] x25519Secret = _x25519.DeriveSharedSecret(peerShare[MlKemCiphertextLength..]);
        return Concat(mlKemSecret, x25519Secret);
    }

    /// <summary>
    /// Server side: encapsulate against the client share (ML-KEM encapsulation key ‖ X25519 public key).
    /// Returns the response (ML-KEM ciphertext (1088) ‖ own X25519 public key (32) = 1120 bytes) and the
    /// secret (ML-KEM secret (32) ‖ X25519 secret (32) = 64 bytes).
    /// </summary>
    public (byte[] ResponseShare, byte[] SharedSecret) Encapsulate(ReadOnlySpan<byte> peerShare)
    {
        if (peerShare.Length != MlKemEncapsulationKeyLength + X25519KeyLength)
            throw new ArgumentException(
                $"X25519MLKEM768 client share must be {MlKemEncapsulationKeyLength + X25519KeyLength} bytes long.", nameof(peerShare));

        using MLKem peerKem = MLKem.ImportEncapsulationKey(
            MLKemAlgorithm.MLKem768, peerShare[..MlKemEncapsulationKeyLength]);
        peerKem.Encapsulate(out byte[] ciphertext, out byte[] mlKemSecret);
        byte[] x25519Secret = _x25519.DeriveSharedSecret(peerShare[MlKemEncapsulationKeyLength..]);

        byte[] responseShare = Concat(ciphertext, _x25519.PublicKey);
        byte[] sharedSecret = Concat(mlKemSecret, x25519Secret);
        return (responseShare, sharedSecret);
    }

    private static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        byte[] result = new byte[a.Length + b.Length];
        a.CopyTo(result);
        b.CopyTo(result.AsSpan(a.Length));
        return result;
    }

    public void Dispose()
    {
        _mlKem.Dispose();
        _x25519.Dispose();
    }
}
