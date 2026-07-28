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

using System.Security.Cryptography;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests for 1-RTT key updates (RFC 9001 §6): the "quic ku" derivation of the next generation as well
/// as a complete update round trip between our client and server via the key-phase bit.
/// </summary>
[TestFixture]
public class KeyUpdateTests
{
    [Test]
    public void TrafficKeys_Next_AdvancesSecretKeyAndIv_ButKeepsHeaderProtectionKey()
    {
        byte[] secret = new byte[32];
        for (int i = 0; i < secret.Length; i++)
            secret[i] = (byte)(i * 7 + 1);

        var tk = TrafficKeys.FromSecret(HashAlgorithmName.SHA256, secret, keyLength: 16);
        TrafficKeys next = tk.Next(HashAlgorithmName.SHA256, hashLength: 32);

        Assert.That(next.Secret, Is.Not.EqualTo(tk.Secret)); // secret_<n+1> = HKDF-Expand-Label(secret_<n>, "quic ku", …)
        Assert.That(next.Key, Is.Not.EqualTo(tk.Key));
        Assert.That(next.Iv, Is.Not.EqualTo(tk.Iv));
        Assert.That(next.HeaderProtectionKey, Is.EqualTo(tk.HeaderProtectionKey)); // HP key stays unchanged (RFC 9001 §6.1)
    }

    private static (QuicClientConnection client, QuicServerConnection server) Handshaken(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        Pump(client, server); // let the remaining data (HANDSHAKE_DONE ACK etc.) drain
        return (client, server);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    [Test]
    public void KeyUpdate_ClientInitiated_RotatesBothDirections_AndStreamDataArrives()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        Assert.That(client.CurrentKeyPhase, Is.False);
        Assert.That(server.CurrentKeyPhase, Is.False);

        // The client initiates the key update: the send phase flips to 1 immediately.
        Assert.That(client.InitiateKeyUpdate(), Is.True);
        Assert.That(client.CurrentKeyPhase, Is.True);

        // Send stream data under the new keys.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([10, 20, 30, 40, 50]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        // The server detects the flipped key-phase bit, rotates read and send keys.
        Assert.That(server.CurrentKeyPhase, Is.True, "The server must have switched to the new key phase.");
        Assert.That(server.KeyUpdateCount >= 1, Is.True);
        // The client rotates its read keys as soon as the server answers with the new phase.
        Assert.That(client.CurrentKeyPhase, Is.True);

        // Crucially: the application data arrived correctly under the rotated keys.
        Assert.That(server.Streams.ContainsKey(stream.Id.Value), Is.True);
        Assert.That(server.Streams[stream.Id.Value].Read(), Is.EqualTo([10, 20, 30, 40, 50]));
    }

    [Test]
    public void KeyUpdate_SecondUpdate_TogglesKeyPhaseBack_AndDataStillFlows()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        // First update (phase → 1).
        client.InitiateKeyUpdate();
        QuicStream s1 = client.OpenBidirectionalStream();
        s1.Write([1, 1, 1]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);
        Assert.That(client.CurrentKeyPhase, Is.True);
        Assert.That(server.CurrentKeyPhase, Is.True);

        // Second update (phase → 0, secret generation 2).
        Assert.That(client.InitiateKeyUpdate(), Is.True);
        Assert.That(client.CurrentKeyPhase, Is.False);
        QuicStream s2 = client.OpenBidirectionalStream();
        s2.Write([2, 2, 2, 2]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(server.CurrentKeyPhase, Is.False, "After the second update the phase must be 0 again.");
        Assert.That(server.Streams[s2.Id.Value].Read(), Is.EqualTo([2, 2, 2, 2]));
    }

    [Test]
    public void KeyUpdate_ServerInitiated_IsDetectedByClient()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        // The server initiates the update.
        Assert.That(server.InitiateKeyUpdate(), Is.True);
        Assert.That(server.CurrentKeyPhase, Is.True);

        // The server sends data with the new phase; the client must detect the update.
        QuicStream stream = server.OpenUnidirectionalStream();
        stream.Write([7, 7, 7]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(client.CurrentKeyPhase, Is.True, "The client must have detected the server's key update.");
        Assert.That(server.CurrentKeyPhase, Is.True);
    }
}
