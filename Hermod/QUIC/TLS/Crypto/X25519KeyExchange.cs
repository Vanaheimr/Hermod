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

using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// X25519 key exchange (RFC 7748) via BouncyCastle. The BCL does not offer X25519; this is the
/// only place where an external crypto library is used – encapsulated behind
/// <see cref="IKeyExchange"/>. Public key and secret are 32 bytes each.
/// </summary>
public sealed class X25519KeyExchange : IKeyExchange
{
    private readonly X25519PrivateKeyParameters _privateKey;

    public NamedGroup Group => NamedGroup.X25519;
    public byte[] PublicKey { get; }

    public X25519KeyExchange()
    {
        _privateKey = new X25519PrivateKeyParameters(new SecureRandom());
        PublicKey = _privateKey.GeneratePublicKey().GetEncoded();
    }

    public byte[] DeriveSharedSecret(ReadOnlySpan<byte> peerPublicKey)
    {
        if (peerPublicKey.Length != X25519PublicKeyParameters.KeySize)
            throw new ArgumentException("X25519 public key must be 32 bytes long.", nameof(peerPublicKey));

        var peer = new X25519PublicKeyParameters(peerPublicKey.ToArray(), 0);
        var agreement = new X25519Agreement();
        agreement.Init(_privateKey);
        byte[] secret = new byte[agreement.AgreementSize];
        agreement.CalculateAgreement(peer, secret, 0);
        return secret;
    }

    public void Dispose() { }
}
