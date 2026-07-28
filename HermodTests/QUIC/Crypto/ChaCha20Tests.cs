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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Crypto;

/// <summary>
/// Tests of the ChaCha20-Poly1305 suite: the ChaCha20 block (RFC 8439 §2.3.2), the header-protection
/// mask (RFC 9001 §5.4.4/§A.5) and a packet round trip via <see cref="PacketProtection"/>.
/// </summary>
[TestFixture]
public class ChaCha20Tests
{
    [Test]
    public void ChaCha20Block_MatchesRfc8439Vector()
    {
        byte[] key = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
        byte[] nonce = Convert.FromHexString("000000090000004a00000000");
        Span<byte> keystream = stackalloc byte[64];

        ChaCha20.Block(key, counter: 1, nonce, keystream);

        // RFC 8439 §2.3.2: serialized keystream block.
        Assert.That(Convert.ToHexString(keystream).ToLowerInvariant(), Is.EqualTo("10f1e7e4d13b5915500fdd1fa32071c4c7d1f4c733c068030422aa9ac3d46c4e" +
            "d2826446079faa0914c2d705d98b02a2b5129cd1de164eb9cbd083e8a2503c4e"));
    }

    [Test]
    public void HeaderProtectionMask_MatchesRfc9001AppendixA5()
    {
        // RFC 9001 §A.5 (ChaCha20-Poly1305 short header): hp key + sample ⇒ mask aefefe7d03.
        byte[] hp = Convert.FromHexString("25a282b9e82f06f21f488917a4fc8f1b73573685608597d0efcb076b0ab7a7a4");
        byte[] sample = Convert.FromHexString("5e5cd55c41f69080575d7999c25a5bfb");
        Span<byte> mask = stackalloc byte[5];

        ChaCha20.HeaderProtectionMask(hp, sample, mask);

        Assert.That(Convert.ToHexString(mask).ToLowerInvariant(), Is.EqualTo("aefefe7d03"));
    }

    [Test]
    public void PacketProtection_ChaCha20_RoundTripsAShortHeaderPacket()
    {
        byte[] secret = new byte[32];
        for (int i = 0; i < secret.Length; i++)
            secret[i] = (byte)(i * 13 + 5);
        TrafficKeys keys = TrafficKeys.FromSecret(HashAlgorithmName.SHA256, secret, keyLength: 32);
        using var protection = new PacketProtection(keys, AeadAlgorithm.ChaCha20Poly1305);

        var dcid = new ConnectionId(Convert.FromHexString("0102030405060708"));
        byte[] payload = [0xaa, 0xbb, 0xcc, 0xdd, 0xee];
        byte[] packet = ShortHeader.Build(protection, dcid, packetNumber: 42, packetNumberLength: 2, payload);

        byte[] plaintext = new byte[packet.Length];
        Assert.That(protection.UnprotectPacket(packet, packetNumberOffset: 1 + dcid.Length,
            largestAckedPacketNumber: 0, longHeader: false, plaintext, out ulong pn, out int len), Is.True);
        Assert.That(pn, Is.EqualTo(42ul));
        Assert.That(plaintext[..len], Is.EqualTo(payload));
    }

    [Test]
    public void ChaCha20Poly1305_IsNegotiated_AndCompletesTheQuicHandshake()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // The client offers ONLY ChaCha20-Poly1305 ⇒ the server must pick it.
        using var client = new QuicClientConnection("localhost", certificateValidation: validation,
            cipherSuites: [CipherSuite.ChaCha20Poly1305Sha256]);
        using var server = new QuicServerConnection(cert);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        // The handshake completes ⇒ the Handshake/1-RTT packets are protected with ChaCha20-Poly1305.
        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(client.NegotiatedCipherSuite, Is.EqualTo(CipherSuite.ChaCha20Poly1305Sha256));
    }
}
