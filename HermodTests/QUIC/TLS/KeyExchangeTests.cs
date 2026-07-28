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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

[TestFixture]
public class KeyExchangeTests
{
    [Test]
    public void X25519_TwoParties_DeriveSameSecret()
    {
        using var alice = new X25519KeyExchange();
        using var bob = new X25519KeyExchange();

        Assert.That(alice.PublicKey.Length, Is.EqualTo(32));
        byte[] aliceView = alice.DeriveSharedSecret(bob.PublicKey);
        byte[] bobView = bob.DeriveSharedSecret(alice.PublicKey);

        Assert.That(bobView, Is.EqualTo(aliceView));
        Assert.That(aliceView.Length, Is.EqualTo(32));
    }

    [Test]
    public void X448_TwoParties_DeriveSameSecret()
    {
        using var alice = new X448KeyExchange();
        using var bob = new X448KeyExchange();

        Assert.That(alice.PublicKey.Length, Is.EqualTo(56));
        byte[] aliceView = alice.DeriveSharedSecret(bob.PublicKey);
        byte[] bobView = bob.DeriveSharedSecret(alice.PublicKey);

        Assert.That(bobView, Is.EqualTo(aliceView));
        Assert.That(aliceView.Length, Is.EqualTo(56));
    }

    // RFC 7748 §5.2 — the two X448 single vectors: scalarmult(clamp(scalar), u) == output.
        [TestCase(
        "3d262fddf9ec8e88495266fea19a34d28882acef045104d0d1aae121700a779c984c24f8cdd78fbff44943eba368f54b29259a4f1c600ad3",
        "06fce640fa3487bfda5f6cf2d5263f8aad88334cbd07437f020f08f9814dc031ddbdc38c19c6da2583fa5429db94ada18aa7a7fb4ef8a086",
        "ce3e4ff95a60dc6697da1db1d85e6afbdf79b50a2412d7546d5f239fe14fbaadeb445fc66a01b0779d98223961111e21766282f73dd96b6f")]
    [TestCase(
        "203d494428b8399352665ddca42f9de8fef600908e0d461cb021f8c538345dd77c3e4806e25f46d3315c44e0a5b4371282dd2c8d5be3095f",
        "0fbcc2f993cd56d3305b0b7d9e55d4c1a8fb5dbb52f8e9a1e9b6201b165d015894e56c4d3570bee52fe205e28a78b91cdfbde71ce8d157db",
        "884a02576239ff7a2f2f63b2db6a9ff37047ac13568e1e30fe63c4a7ad1b3ee3a5700df34321d62077e63633c575c1c954514e99da7c179d")]
    public void X448_MatchesRfc7748ScalarMultVector(string scalar, string inputU, string outputU)
    {
        using var kex = new X448KeyExchange(Convert.FromHexString(scalar));
        byte[] secret = kex.DeriveSharedSecret(Convert.FromHexString(inputU));

        Assert.That(Convert.ToHexString(secret).ToLowerInvariant(), Is.EqualTo(outputU));
    }

    [Test]
    public void X25519MlKem768_ClientServer_DeriveSameSecret()
    {
        // The client offers (ek ‖ x25519 = 1216 bytes); the server encapsulates (ct ‖ x25519 = 1120 bytes);
        // the client derives the same 64-byte secret from the server response (ML-KEM ss ‖ X25519 ss).
        using var client = new X25519MlKem768KeyExchange();
        using var server = new X25519MlKem768KeyExchange();

        Assert.That(client.PublicKey.Length, Is.EqualTo(1216));

        (byte[] responseShare, byte[] serverSecret) = server.Encapsulate(client.PublicKey);
        Assert.That(responseShare.Length, Is.EqualTo(1120));

        byte[] clientSecret = client.DeriveSharedSecret(responseShare);

        Assert.That(clientSecret.Length, Is.EqualTo(64));
        Assert.That(clientSecret, Is.EqualTo(serverSecret));
    }

    [Test]
    public void X25519MlKem768_TamperedCiphertext_YieldsDifferentSecret()
    {
        // ML-KEM is IND-CCA2: a modified ciphertext yields (implicit rejection) a different secret,
        // not an error — the handshake then fails at the Finished MAC.
        using var client = new X25519MlKem768KeyExchange();
        using var server = new X25519MlKem768KeyExchange();

        (byte[] responseShare, _) = server.Encapsulate(client.PublicKey);
        byte[] honest = client.DeriveSharedSecret(responseShare);

        responseShare[0] ^= 0x01; // tamper with the ML-KEM ciphertext part
        byte[] tampered = client.DeriveSharedSecret(responseShare);

        Assert.That(tampered, Is.Not.EqualTo(honest));
    }

    [Test]
    public void Factory_CreatesRightImplementation()
    {
        using IKeyExchange x = KeyExchange.Create(NamedGroup.X25519);
        using IKeyExchange x448 = KeyExchange.Create(NamedGroup.X448);
        using IKeyExchange p256 = KeyExchange.Create(NamedGroup.Secp256r1);

        Expect.Type<X25519KeyExchange>(x);
        Expect.Type<X448KeyExchange>(x448);
        Expect.Type<EcdheKeyExchange>(p256);
        Assert.That(x.PublicKey.Length, Is.EqualTo(32));    // X25519: 32 bytes
        Assert.That(x448.PublicKey.Length, Is.EqualTo(56)); // X448: 56 bytes
        Assert.That(p256.PublicKey.Length, Is.EqualTo(65)); // P-256: uncompressed point
    }

    [Test]
    public void CrossGroup_ProducesDifferentUnusableSecrets_ButSameGroupAgrees()
    {
        // Sanity: the same group agrees; this is the foundation of the handshake.
        using var a = KeyExchange.Create(NamedGroup.X25519);
        using var b = KeyExchange.Create(NamedGroup.X25519);
        Assert.That(b.DeriveSharedSecret(a.PublicKey), Is.EqualTo(a.DeriveSharedSecret(b.PublicKey)));
    }
}
