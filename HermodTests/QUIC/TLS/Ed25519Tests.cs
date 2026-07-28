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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Ed25519 (RFC 8032, PureEdDSA) — verifies the BouncyCastle primitive byte for byte against the
/// test vectors from RFC 8032 §7.1 (public key <b>and</b> signature) and checks that verification
/// rejects tampered signatures.
/// </summary>
[TestFixture]
public class Ed25519Tests
{
    // RFC 8032 §7.1 — TEST 1 (empty message).
    private const string Test1Seed = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string Test1PublicKey = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Test1Signature =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    // RFC 8032 §7.1 — TEST 2 (message = 0x72).
    private const string Test2Seed = "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb";
    private const string Test2PublicKey = "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c";
    private const string Test2Message = "72";
    private const string Test2Signature =
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00";

    [Test]
    public void PublicKeyAndSignature_MatchRfc8032Test1_EmptyMessage()
    {
        var ed = new Ed25519Signature(Convert.FromHexString(Test1Seed));

        Assert.That(Convert.ToHexString(ed.PublicKey).ToLowerInvariant(), Is.EqualTo(Test1PublicKey));
        byte[] signature = ed.Sign(ReadOnlySpan<byte>.Empty);
        Assert.That(Convert.ToHexString(signature).ToLowerInvariant(), Is.EqualTo(Test1Signature));
    }

    [Test]
    public void PublicKeyAndSignature_MatchRfc8032Test2_OneByteMessage()
    {
        var ed = new Ed25519Signature(Convert.FromHexString(Test2Seed));

        Assert.That(Convert.ToHexString(ed.PublicKey).ToLowerInvariant(), Is.EqualTo(Test2PublicKey));
        byte[] signature = ed.Sign(Convert.FromHexString(Test2Message));
        Assert.That(Convert.ToHexString(signature).ToLowerInvariant(), Is.EqualTo(Test2Signature));
    }

    [Test]
    public void Verify_AcceptsValidRfcSignature()
    {
        byte[] publicKey = Convert.FromHexString(Test2PublicKey);
        byte[] message = Convert.FromHexString(Test2Message);
        byte[] signature = Convert.FromHexString(Test2Signature);

        Assert.That(Ed25519Signature.Verify(publicKey, message, signature), Is.True);
    }

    [Test]
    public void Verify_RejectsTamperedSignatureAndMessage()
    {
        byte[] publicKey = Convert.FromHexString(Test2PublicKey);
        byte[] message = Convert.FromHexString(Test2Message);
        byte[] signature = Convert.FromHexString(Test2Signature);

        byte[] tamperedSig = (byte[])signature.Clone();
        tamperedSig[0] ^= 0x01;
        Assert.That(Ed25519Signature.Verify(publicKey, message, tamperedSig), Is.False);

        Assert.That(Ed25519Signature.Verify(publicKey, [0x73], signature), Is.False);
    }

    [Test]
    public void SignAndVerify_RoundTripsWithFreshKeyPair()
    {
        var ed = new Ed25519Signature();
        byte[] content = "TLS 1.3, server CertificateVerify"u8.ToArray();

        byte[] signature = ed.Sign(content);
        Assert.That(Ed25519Signature.Verify(ed.PublicKey, content, signature), Is.True);
    }
}
