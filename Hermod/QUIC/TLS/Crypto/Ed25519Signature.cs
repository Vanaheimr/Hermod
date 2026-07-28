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

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// Ed25519 signatures (RFC 8032, PureEdDSA) via BouncyCastle. The BCL does not know Ed25519 as a
/// signature algorithm — as with <see cref="X25519KeyExchange"/>, the primitive comes from
/// BouncyCastle, encapsulated in this class. In TLS 1.3 this is the SignatureScheme <c>ed25519</c>
/// (0x0807, RFC 8446 §4.2.3): the CertificateVerify content is signed directly <b>without</b> a
/// pre-hash. Public key and signature are 32 and 64 bytes respectively.
/// </summary>
public sealed class Ed25519Signature
{
    private readonly Ed25519PrivateKeyParameters _privateKey;

    /// <summary>
    /// The public key (32 bytes, RFC 8032 §5.1.5).
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Creates a fresh key pair.
    /// </summary>
    public Ed25519Signature() : this(new Ed25519PrivateKeyParameters(new SecureRandom())) { }

    /// <summary>
    /// Takes an existing 32-byte seed (private key) — mainly for RFC test vectors.
    /// </summary>
    public Ed25519Signature(ReadOnlySpan<byte> seed)
        : this(new Ed25519PrivateKeyParameters(seed.ToArray(), 0)) { }

    private Ed25519Signature(Ed25519PrivateKeyParameters privateKey)
    {
        _privateKey = privateKey;
        PublicKey = privateKey.GeneratePublicKey().GetEncoded();
    }

    /// <summary>
    /// Signs the content directly (PureEdDSA, no pre-hash). Result: 64 bytes.
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> content)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, _privateKey);
        signer.BlockUpdate(content.ToArray(), 0, content.Length);
        return signer.GenerateSignature();
    }

    /// <summary>
    /// Verifies an Ed25519 signature against a raw 32-byte public key.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != Ed25519PublicKeyParameters.KeySize)
            return false;
        return VerifyWith(new Ed25519PublicKeyParameters(publicKey.ToArray(), 0), content, signature);
    }

    /// <summary>
    /// Verifies against the Ed25519 public key from a SubjectPublicKeyInfo (id-Ed25519, 1.3.101.112) —
    /// as exported from an X.509 leaf certificate.
    /// </summary>
    public static bool VerifyWithSubjectPublicKeyInfo(byte[] spki, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        if (PublicKeyFactory.CreateKey(spki) is not Ed25519PublicKeyParameters pub)
            return false;
        return VerifyWith(pub, content, signature);
    }

    private static bool VerifyWith(Ed25519PublicKeyParameters pub, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, pub);
        verifier.BlockUpdate(content.ToArray(), 0, content.Length);
        return verifier.VerifySignature(signature.ToArray());
    }
}
