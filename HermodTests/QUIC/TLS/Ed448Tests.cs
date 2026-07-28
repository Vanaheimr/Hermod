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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Ed448 (RFC 8032, PureEdDSA over Edwards448/SHAKE256, empty context) — verifies the
/// BouncyCastle primitive byte for byte against the test vectors from RFC 8032 §7.4 (public key
/// <b>and</b> signature) and checks that verification rejects tampered signatures.
/// </summary>
[TestFixture]
public class Ed448Tests
{
    // RFC 8032 §7.4 — test "Blank" (empty message, empty context).
    private const string BlankSeed =
        "6c82a562cb808d10d632be89c8513ebf6c929f34ddfa8c9f63c9960ef6e348a3528c8a3fcc2f044e39a3fc5b94492f8f032e7549a20098f95b";
    private const string BlankPublicKey =
        "5fd7449b59b461fd2ce787ec616ad46a1da1342485a70e1f8a0ea75d80e96778edf124769b46c7061bd6783df1e50f6cd1fa1abeafe8256180";
    private const string BlankSignature =
        "533a37f6bbe457251f023c0d88f976ae2dfb504a843e34d2074fd823d41a591f2b233f034f628281f2fd7a22ddd47d7828c59bd0a21bfd39" +
        "80ff0d2028d4b18a9df63e006c5d1c2d345b925d8dc00b4104852db99ac5c7cdda8530a113a0f4dbb61149f05a7363268c71d95808ff2e652600";

    // RFC 8032 §7.4 — test "1 octet" (message = 0x03, empty context).
    private const string OneOctetSeed =
        "c4eab05d357007c632f3dbb48489924d552b08fe0c353a0d4a1f00acda2c463afbea67c5e8d2877c5e3bc397a659949ef8021e954e0a12274e";
    private const string OneOctetPublicKey =
        "43ba28f430cdff456ae531545f7ecd0ac834a55d9358c0372bfa0c6c6798c0866aea01eb00742802b8438ea4cb82169c235160627b4c3a9480";
    private const string OneOctetMessage = "03";
    private const string OneOctetSignature =
        "26b8f91727bd62897af15e41eb43c377efb9c610d48f2335cb0bd0087810f4352541b143c4b981b7e18f62de8ccdf633fc1bf037ab7cd779" +
        "805e0dbcc0aae1cbcee1afb2e027df36bc04dcecbf154336c19f0af7e0a6472905e799f1953d2a0ff3348ab21aa4adafd1d234441cf807c03a00";

    [Test]
    public void PublicKeyAndSignature_MatchRfc8032BlankTest_EmptyMessage()
    {
        var ed = new Ed448Signature(Convert.FromHexString(BlankSeed));

        Assert.That(Convert.ToHexString(ed.PublicKey).ToLowerInvariant(), Is.EqualTo(BlankPublicKey));
        byte[] signature = ed.Sign(ReadOnlySpan<byte>.Empty);
        Assert.That(Convert.ToHexString(signature).ToLowerInvariant(), Is.EqualTo(BlankSignature));
    }

    [Test]
    public void PublicKeyAndSignature_MatchRfc8032OneOctetTest()
    {
        var ed = new Ed448Signature(Convert.FromHexString(OneOctetSeed));

        Assert.That(Convert.ToHexString(ed.PublicKey).ToLowerInvariant(), Is.EqualTo(OneOctetPublicKey));
        byte[] signature = ed.Sign(Convert.FromHexString(OneOctetMessage));
        Assert.That(Convert.ToHexString(signature).ToLowerInvariant(), Is.EqualTo(OneOctetSignature));
    }

    [Test]
    public void Verify_AcceptsValidRfcSignature()
    {
        byte[] publicKey = Convert.FromHexString(OneOctetPublicKey);
        byte[] message = Convert.FromHexString(OneOctetMessage);
        byte[] signature = Convert.FromHexString(OneOctetSignature);

        Assert.That(Ed448Signature.Verify(publicKey, message, signature), Is.True);
    }

    [Test]
    public void Verify_RejectsTamperedSignatureAndMessage()
    {
        byte[] publicKey = Convert.FromHexString(OneOctetPublicKey);
        byte[] message = Convert.FromHexString(OneOctetMessage);
        byte[] signature = Convert.FromHexString(OneOctetSignature);

        byte[] tamperedSig = (byte[])signature.Clone();
        tamperedSig[0] ^= 0x01;
        Assert.That(Ed448Signature.Verify(publicKey, message, tamperedSig), Is.False);

        Assert.That(Ed448Signature.Verify(publicKey, [0x04], signature), Is.False);
    }

    [Test]
    public void SignAndVerify_RoundTripsWithFreshKeyPair()
    {
        var ed = new Ed448Signature();
        byte[] content = "TLS 1.3, server CertificateVerify"u8.ToArray();

        byte[] signature = ed.Sign(content);
        Assert.That(ed.PublicKey.Length, Is.EqualTo(57));
        Assert.That(signature.Length, Is.EqualTo(114));
        Assert.That(Ed448Signature.Verify(ed.PublicKey, content, signature), Is.True);
    }
}
