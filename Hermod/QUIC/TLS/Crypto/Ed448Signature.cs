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
/// Ed448 signatures (RFC 8032, PureEdDSA over Edwards448/SHAKE256) via BouncyCastle. Like
/// <see cref="Ed25519Signature"/> a BCL gap that BouncyCastle fills. In TLS 1.3 this is the
/// SignatureScheme <c>ed448</c> (0x0808, RFC 8446 §4.2.3): the CertificateVerify content is signed
/// <b>without</b> a pre-hash and with an <b>empty context</b>. Public key and signature are 57 and
/// 114 bytes respectively.
/// </summary>
public sealed class Ed448Signature
{
    // Ed448 always signs over a context; TLS 1.3 requires the empty context (RFC 8446 §4.2.3).
    private static readonly byte[] EmptyContext = [];

    private readonly Ed448PrivateKeyParameters _privateKey;

    /// <summary>
    /// The public key (57 bytes, RFC 8032 §5.2.5).
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Creates a fresh key pair.
    /// </summary>
    public Ed448Signature() : this(new Ed448PrivateKeyParameters(new SecureRandom())) { }

    /// <summary>
    /// Takes an existing 57-byte seed (private key) — mainly for RFC test vectors.
    /// </summary>
    public Ed448Signature(ReadOnlySpan<byte> seed)
        : this(new Ed448PrivateKeyParameters(seed.ToArray(), 0)) { }

    private Ed448Signature(Ed448PrivateKeyParameters privateKey)
    {
        _privateKey = privateKey;
        PublicKey = privateKey.GeneratePublicKey().GetEncoded();
    }

    /// <summary>
    /// Signs the content directly (PureEdDSA, no pre-hash, empty context). Result: 114 bytes.
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> content)
    {
        var signer = new Ed448Signer(EmptyContext);
        signer.Init(forSigning: true, _privateKey);
        signer.BlockUpdate(content.ToArray(), 0, content.Length);
        return signer.GenerateSignature();
    }

    /// <summary>
    /// Verifies an Ed448 signature against a raw 57-byte public key.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != Ed448PublicKeyParameters.KeySize)
            return false;
        return VerifyWith(new Ed448PublicKeyParameters(publicKey.ToArray(), 0), content, signature);
    }

    /// <summary>
    /// Verifies against the Ed448 public key from a SubjectPublicKeyInfo (id-Ed448, 1.3.101.113) —
    /// as exported from an X.509 leaf certificate.
    /// </summary>
    public static bool VerifyWithSubjectPublicKeyInfo(byte[] spki, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        if (PublicKeyFactory.CreateKey(spki) is not Ed448PublicKeyParameters pub)
            return false;
        return VerifyWith(pub, content, signature);
    }

    private static bool VerifyWith(Ed448PublicKeyParameters pub, ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        var verifier = new Ed448Signer(EmptyContext);
        verifier.Init(forSigning: false, pub);
        verifier.BlockUpdate(content.ToArray(), 0, content.Length);
        return verifier.VerifySignature(signature.ToArray());
    }
}
