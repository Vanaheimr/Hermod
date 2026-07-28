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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

/// <summary>
/// Tests for version negotiation (RFC 9000 §6) and Retry / address validation (RFC 9000 §8.1, §17.2.5;
/// RFC 9001 §5.8): packet round trips as well as in-process scenarios client ↔ our own server.
/// </summary>
[TestFixture]
public class VersionAndRetryTests
{
    private const uint V1 = 0x0000_0001;

    private static ConnectionId Cid(params byte[] bytes) => new(bytes);

    [Test]
    public void VersionNegotiation_RoundTrips()
    {
        ConnectionId dcid = Cid(1, 2, 3, 4);
        ConnectionId scid = Cid(9, 8, 7);
        byte[] packet = VersionNegotiationPacket.Build(dcid, scid, [V1, 0xff00_001d]);

        Assert.That(VersionNegotiationPacket.TryParse(packet, out ConnectionId d, out ConnectionId s, out List<uint> versions), Is.True);
        Assert.That(d.Span.ToArray(), Is.EqualTo(dcid.Span.ToArray()));
        Assert.That(s.Span.ToArray(), Is.EqualTo(scid.Span.ToArray()));
        Assert.That(versions, Is.EqualTo([V1, 0xff00_001d]));
    }

    [Test]
    public void RetryPacket_RoundTrips_AndIntegrityTagVerifies()
    {
        ConnectionId dcid = Cid(0xc0, 0xc1);       // = client SCID
        ConnectionId scid = Cid(0x51, 0x52, 0x53); // = new server SCID
        ConnectionId originalDcid = Cid(0xd0, 0xd1, 0xd2, 0xd3);
        byte[] token = [0xaa, 0xbb, 0xcc, 0xdd, 0xee];

        byte[] packet = RetryPacket.Build(V1, dcid, scid, token, originalDcid);

        Assert.That(RetryPacket.TryParse(packet, out uint version, out ConnectionId d, out ConnectionId s, out byte[] t, out byte[] tag), Is.True);
        Assert.That(version, Is.EqualTo(V1));
        Assert.That(d.Span.ToArray(), Is.EqualTo(dcid.Span.ToArray()));
        Assert.That(s.Span.ToArray(), Is.EqualTo(scid.Span.ToArray()));
        Assert.That(t, Is.EqualTo(token));
        Assert.That(tag.Length, Is.EqualTo(16));

        Assert.That(RetryPacket.Verify(packet, originalDcid), Is.True, "The integrity tag must verify against the correct ODCID.");
        Assert.That(RetryPacket.Verify(packet, Cid(0xff, 0xff)), Is.False, "A wrong ODCID must make the tag fail.");
    }

    [Test]
    public void VersionNegotiation_ClientWithUnsupportedVersion_ReceivesOfferedVersions()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        // The client announces a (made-up) version that the server does not support.
        using var client = new QuicClientConnection("localhost", version: 0x1a2a_3a4a);
        using var server = new QuicServerConnection(cert); // supports only v1
        client.Start();

        // One round trip: client Initial (bogus version) → server answers with VN → client processes it.
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);

        Assert.That(client.VersionNegotiationReceived, Is.True, "The client must have received a version-negotiation packet.");
        Assert.That(client.OfferedVersions, Does.Contain(V1));
        Assert.That(client.HandshakeConfirmed, Is.False);
    }

    [Test]
    public void VersionNegotiation_IncludesReservedGreaseVersion()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        using var client = new QuicClientConnection("localhost", version: 0x1a2a_3a4a);
        using var server = new QuicServerConnection(cert);
        client.Start();

        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);

        byte[]? vn = server.GetDatagramsToSend().FirstOrDefault();
        Assert.That(vn, Is.Not.Null);
        Assert.That(VersionNegotiationPacket.TryParse(vn!, out _, out _, out List<uint> versions), Is.True);
        Assert.That(versions, Does.Contain(V1));
        // A reserved version in the pattern 0x?a?a?a?a (RFC 9000 §6.3) guards against ossification.
        Assert.That(versions, Has.Some.Matches<uint>(v => (v & 0x0F0F_0F0Fu) == 0x0A0A_0A0Au));
    }

    [Test]
    public void VersionNegotiation_NotSent_ForDatagramSmallerThan1200()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        using var server = new QuicServerConnection(cert);

        // A tiny long-header packet with an unsupported version (0x1a2a3a4a), 4-byte CIDs → 15 bytes.
        byte[] tiny =
        [
            0xC0, 0x1a, 0x2a, 0x3a, 0x4a,   // long header (Initial) + version
            4, 0xD0, 0xD1, 0xD2, 0xD3,      // DCID length + DCID
            4, 0x50, 0x51, 0x52, 0x53,      // SCID length + SCID
        ];
        server.ProcessDatagram(tiny);

        // RFC 9000 §6.1/§14.1: NO VN may be sent for a datagram < 1200 bytes (anti-amplification).
        Assert.That(server.GetDatagramsToSend(), Is.Empty);
    }

    [Test]
    public void Retry_ClientReattemptsWithToken_AndCompletesHandshake()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, requireRetry: true);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(server.SentRetry, Is.True, "The server must have sent a Retry for address validation.");
        Assert.That(client.RetryHandled, Is.True, "The client must have processed the Retry and re-sent the ClientHello.");
        Assert.That(client.HandshakeConfirmed, Is.True, "The handshake must complete despite the Retry.");
        Assert.That(server.HandshakeComplete, Is.True);

        // The server must have echoed the original DCID back as a transport parameter (RFC 9000 §7.3).
        Assert.That(client.PeerTransportParameters, Is.Not.Null);
        Assert.That(client.PeerTransportParameters!.RetrySourceConnectionIdValue, Is.Not.Null);
    }
}
