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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Limiting the ACK state (RFC 9000 §13.2.4): as soon as the peer acknowledges one of our packets
/// that carried an ACK frame, we may stop acknowledging everything up to that frame's Largest
/// Acknowledged. Without it, the set of received packet numbers — and with it every ACK frame we
/// build — would grow for the entire lifetime of the connection.
/// </summary>
[TestFixture]
public class AckPruningTests
{
    // ---- Range construction ------------------------------------------------------------------

    [Test]
    public void FromAscendingPacketNumbers_MatchesTheGeneralVariant()
    {
        ulong[] received = [0, 1, 2, 5, 6, 9, 20, 21, 22];

        AckFrame ascending = AckFrame.FromAscendingPacketNumbers(received);
        AckFrame general = AckFrame.FromPacketNumbers(received.Reverse().ToArray());

        Assert.That(ascending.Ranges, Is.EqualTo(general.Ranges));
        // Descending, non-overlapping ranges as the wire format requires (RFC 9000 §19.3).
        Assert.That(ascending.Ranges, Is.EqualTo(new[]
        {
            new PacketNumberRange(22, 20),
            new PacketNumberRange(9, 9),
            new PacketNumberRange(6, 5),
            new PacketNumberRange(2, 0),
        }));
    }

    [Test]
    public void Covers_RecognisesNumbersInsideAndOutsideTheRanges()
    {
        AckFrame ack = AckFrame.FromAscendingPacketNumbers([0, 1, 2, 5, 6]);

        Assert.That(ack.Covers(0), Is.True);
        Assert.That(ack.Covers(2), Is.True);
        Assert.That(ack.Covers(6), Is.True);
        Assert.That(ack.Covers(3), Is.False);
        Assert.That(ack.Covers(7), Is.False);
    }

    // ---- Pruning of the packet-number space --------------------------------------------------

    [Test]
    public void AcknowledgedAckFrame_ReleasesTheAcknowledgedPacketNumbers()
    {
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn <= 9; pn++)
            space.RecordReceived(pn);

        // We send our ACK (largest = 9) in our own packet 3.
        AckFrame? sent = space.BuildAck();
        Assert.That(sent!.LargestAcknowledged, Is.EqualTo(9UL));
        space.OnAckFrameSent(packetNumber: 3, sent.LargestAcknowledged);
        Assert.That(space.TrackedReceivedCount, Is.EqualTo(10));

        // More packets arrive …
        space.RecordReceived(10);
        space.RecordReceived(11);
        Assert.That(space.TrackedReceivedCount, Is.EqualTo(12));

        // … and the peer acknowledges our packet 3 ⇒ 0..9 are no longer needed.
        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([2, 3, 4]));

        Assert.That(space.TrackedReceivedCount, Is.EqualTo(2));
        AckFrame? after = space.BuildAck();
        Assert.That(after!.Ranges, Is.EqualTo(new[] { new PacketNumberRange(11, 10) }));
    }

    [Test]
    public void UnacknowledgedAckFrame_ReleasesNothing()
    {
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn <= 9; pn++)
            space.RecordReceived(pn);
        space.OnAckFrameSent(packetNumber: 3, space.BuildAck()!.LargestAcknowledged);

        // The peer acknowledges only a different packet of ours — our packet 3 remains open.
        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([7]));

        Assert.That(space.TrackedReceivedCount, Is.EqualTo(10));
    }

    [Test]
    public void PermanentGapFromAnEarlyLoss_DisappearsWithPruning()
    {
        // Without pruning, packet 3 never arriving leaves a gap that every future ACK frame keeps
        // carrying — the frame grows for the whole connection.
        var space = new PacketNumberSpace();
        foreach (ulong pn in (ulong[])[0, 1, 2, 4, 5, 6])
            space.RecordReceived(pn);

        AckFrame? withGap = space.BuildAck();
        Assert.That(withGap!.Ranges, Has.Count.EqualTo(2));
        space.OnAckFrameSent(packetNumber: 1, withGap.LargestAcknowledged);

        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([1]));
        space.RecordReceived(7);

        AckFrame? afterPruning = space.BuildAck();
        Assert.That(afterPruning!.Ranges, Is.EqualTo(new[] { new PacketNumberRange(7, 7) }),
                    "After the peer confirms our ACK, the gap must vanish from the ACK frame.");
    }

    [Test]
    public void LargestReceived_SurvivesCompletePruning()
    {
        // Packet-number reconstruction on receive (RFC 9000 §17.1) depends on this value — it must
        // not fall back to -1 just because the ACK state was released.
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn <= 4; pn++)
            space.RecordReceived(pn);
        space.OnAckFrameSent(packetNumber: 0, space.BuildAck()!.LargestAcknowledged);
        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([0]));

        Assert.That(space.TrackedReceivedCount, Is.EqualTo(0));
        Assert.That(space.LargestReceived, Is.EqualTo(4));
        Assert.That(space.BuildAck(), Is.Null); // nothing left to acknowledge
    }

    [Test]
    public void IsContiguousFromZero_StaysCorrectAcrossPruning()
    {
        // The server's early 0-RTT key discard (RFC 9001 §4.9.3) hangs off this property.
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn <= 4; pn++)
            space.RecordReceived(pn);
        space.OnAckFrameSent(packetNumber: 0, space.BuildAck()!.LargestAcknowledged);
        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([0]));

        Assert.That(space.IsContiguousFromZero, Is.True, "Pruned numbers count as received.");

        space.RecordReceived(6); // 5 is missing ⇒ gap
        Assert.That(space.IsContiguousFromZero, Is.False);
        space.RecordReceived(5);
        Assert.That(space.IsContiguousFromZero, Is.True);
    }

    [Test]
    public void LatePacketBelowThePruningBound_TriggersNoNewAck()
    {
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn <= 4; pn++)
            space.RecordReceived(pn);
        space.OnAckFrameSent(packetNumber: 0, space.BuildAck()!.LargestAcknowledged);
        space.OnAckReceived(AckFrame.FromAscendingPacketNumbers([0]));
        Assert.That(space.AckPending, Is.False);

        space.RecordReceived(2); // duplicate/very late reordering — already acknowledged long ago

        Assert.That(space.AckPending, Is.False);
        Assert.That(space.TrackedReceivedCount, Is.EqualTo(0));
    }

    // ---- End to end over real QUIC connections -----------------------------------------------

    [Test]
    public void SustainedTransfer_KeepsTheAckStateBounded()
    {
        const int payloadSize = 4 * 1024 * 1024;

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxDataValue = 8 * 1024 * 1024,
            InitialMaxStreamDataBidiRemoteValue = 8 * 1024 * 1024,
        });
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[payloadSize]);
        stream.Finish();

        int datagrams = 0;
        int received = 0;
        for (int round = 0; round < 100_000 && received < payloadSize; round++)
        {
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                datagrams++;
                server.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server.GetDatagramsToSend())
            {
                datagrams++;
                client.ProcessDatagram(dg);
            }
            if (server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received += peer.Read().Length;
        }

        Assert.That(received, Is.EqualTo(payloadSize), "The complete payload must arrive.");
        Assert.That(datagrams, Is.GreaterThan(2000), "The transfer must span plenty of packets.");

        // The core of the matter: the receiver's ACK state does NOT grow with the number of packets.
        // Without §13.2.4 pruning it would hold one entry per received packet (thousands here).
        Assert.That(server.ApplicationTrackedReceivedCount, Is.LessThan(500),
                    $"The server holds {server.ApplicationTrackedReceivedCount} packet numbers after " +
                    $"{datagrams} datagrams — ACK state grows without bound?");
        Assert.That(client.ApplicationTrackedReceivedCount, Is.LessThan(500),
                    $"The client holds {client.ApplicationTrackedReceivedCount} packet numbers.");
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
