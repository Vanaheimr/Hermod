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
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

/// <summary>
/// Tests for stateless reset (RFC 9000 §10.3): the packet construction as well as detection based on the
/// stateless-reset token announced by the server via transport parameter.
/// </summary>
[TestFixture]
public class StatelessResetTests
{
    [Test]
    public void Build_EndsWithToken_AndLooksLikeAShortHeaderPacket()
    {
        byte[] token = RandomNumberGenerator.GetBytes(StatelessReset.TokenLength);
        byte[] packet = StatelessReset.Build(token, totalLength: 41);

        Assert.That(packet.Length, Is.EqualTo(41));
        Assert.That(StatelessReset.EndsWith(packet, token), Is.True);
        Assert.That(packet[0] & 0x80, Is.EqualTo(0));    // header form 0 (short header)
        Assert.That(packet[0] & 0x40, Is.EqualTo(0x40)); // fixed bit set
    }

    [Test]
    public void Build_RejectsWrongTokenLength()
        => Assert.Throws<ArgumentException>(() => StatelessReset.Build(new byte[8]));

    private static (QuicClientConnection client, QuicServerConnection server) Handshaken(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server);
    }

    [Test]
    public void StatelessReset_WithKnownToken_TerminatesConnection()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        // The server announces its stateless-reset token via transport parameter; the client has it.
        byte[]? token = client.PeerTransportParameters?.StatelessResetTokenValue;
        Assert.That(token, Is.Not.Null);

        // A packet that (instead of being decryptable) ends with this token is a stateless reset.
        byte[] reset = StatelessReset.Build(token!);
        client.ProcessDatagram(reset);

        Assert.That(client.StatelessResetReceived, Is.True, "The client must detect the stateless reset.");
        Assert.That(client.IsDraining, Is.True, "After a stateless reset the connection goes into draining.");
    }

    [Test]
    public void TokenGenerator_IsDeterministic_PerCidAndSecret()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        var a = new StatelessResetTokenGenerator(secret);
        var b = new StatelessResetTokenGenerator(secret);
        byte[] cid1 = [1, 2, 3, 4, 5, 6, 7, 8];
        byte[] cid2 = [1, 2, 3, 4, 5, 6, 7, 9];

        Assert.That(b.ComputeToken(cid1), Is.EqualTo(a.ComputeToken(cid1)));    // same secret+CID ⇒ same token
        Assert.That(a.ComputeToken(cid2), Is.Not.EqualTo(a.ComputeToken(cid1))); // different CID ⇒ different token
        Assert.That(a.ComputeToken(cid1).Length, Is.EqualTo(StatelessReset.TokenLength));
    }

    [Test]
    public void BuildResponse_IgnoresLongHeaderAndTinyPackets()
    {
        var gen = new StatelessResetTokenGenerator();
        byte[] longHeader = new byte[30];
        longHeader[0] = 0xC0; // long header (Initial) ⇒ new connection, no reset
        Assert.That(StatelessReset.BuildResponse(longHeader, localCidLength: 8, gen), Is.Null);

        byte[] tiny = new byte[StatelessReset.MinLength];
        tiny[0] = 0x40; // short header, but ≤ 21 bytes ⇒ no (smaller) reset possible
        Assert.That(StatelessReset.BuildResponse(tiny, localCidLength: 8, gen), Is.Null);
    }

    [Test]
    public void BuildResponse_ProducesSmallerResetEndingWithCidToken()
    {
        var gen = new StatelessResetTokenGenerator();
        byte[] cid = [9, 8, 7, 6, 5, 4, 3, 2];
        byte[] incoming = new byte[29];
        incoming[0] = 0x40;
        cid.CopyTo(incoming, 1);

        byte[]? reset = StatelessReset.BuildResponse(incoming, localCidLength: 8, gen);
        Assert.That(reset, Is.Not.Null);
        Assert.That(reset!.Length < incoming.Length, Is.True, "The reset must be smaller than the trigger (loop avoidance).");
        Assert.That(StatelessReset.EndsWith(reset, gen.ComputeToken(cid)), Is.True);
    }

    [Test]
    public void StatelessResponder_WithSharedSecret_ProducesResetTheClientRecognizes()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        var gen = new StatelessResetTokenGenerator(secret);

        // The server derives its token from the CID ⇒ the client stores token = HMAC(secret, serverCID).
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, statelessResetTokens: gen);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);

        // A stateless endpoint with the SAME secret receives a 1-RTT packet for the server CID and
        // recomputes the token from the DCID → stateless reset. (Simulates a server after state loss.)
        ConnectionId serverCid = client.DestinationConnectionId;
        byte[] packetToLostServer = FakeShortHeaderTo(serverCid);
        byte[]? reset = StatelessReset.BuildResponse(packetToLostServer, serverCid.Length, gen);
        Assert.That(reset, Is.Not.Null);

        client.ProcessDatagram(reset!);
        Assert.That(client.StatelessResetReceived, Is.True, "The client must detect the stateless endpoint's stateless reset.");
        Assert.That(client.IsDraining, Is.True);
    }

    private static byte[] FakeShortHeaderTo(ConnectionId dcid)
    {
        // A 1-RTT-shaped packet (> 21 bytes) for the given DCID, otherwise random.
        byte[] packet = RandomNumberGenerator.GetBytes(1 + dcid.Length + 20);
        packet[0] = 0x40; // header form 0 (short), fixed bit 1
        dcid.Span.CopyTo(packet.AsSpan(1));
        return packet;
    }

    [Test]
    public void UnrecognizedToken_IsNotTreatedAsStatelessReset()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        // A "reset" with an unknown (here: all-zero) token must not terminate the connection.
        byte[] reset = StatelessReset.Build(new byte[StatelessReset.TokenLength]);
        client.ProcessDatagram(reset);

        Assert.That(client.StatelessResetReceived, Is.False);
        Assert.That(client.IsDraining, Is.False);
    }
}
