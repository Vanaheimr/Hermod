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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Recovery;

/// <summary>
/// ECN (RFC 9000 §13.4 / RFC 9002 §7.3): the receiver counts the ECN codepoints per packet-number space
/// and reports them in the ACK frame (type 0x03); the sender treats an increased CE counter like a loss
/// and shrinks the congestion window. Unit tests for counting/reporting and the CE reaction plus an
/// end-to-end test.
/// </summary>
[TestFixture]
public class EcnTests
{
    [Test]
    public void PacketNumberSpace_CountsEcnCodepoints_AndReportsThemInAck()
    {
        var space = new PacketNumberSpace();
        space.RecordReceived(0, EcnCodepoint.Ect0);
        space.RecordReceived(1, EcnCodepoint.Ect0);
        space.RecordReceived(2, EcnCodepoint.Ce);

        AckFrame? ack = space.BuildAck();
        Assert.That(ack, Is.Not.Null);
        Assert.That(ack!.Ecn, Is.Not.Null);
        Assert.That(ack.Ecn!.Value.Ect0, Is.EqualTo(2ul));
        Assert.That(ack.Ecn.Value.Ect1, Is.EqualTo(0ul));
        Assert.That(ack.Ecn.Value.CongestionExperienced, Is.EqualTo(1ul));
    }

    [Test]
    public void PacketNumberSpace_WithoutEcnMarks_BuildsPlainAck()
    {
        var space = new PacketNumberSpace();
        space.RecordReceived(0); // Not-ECT
        space.RecordReceived(1);

        AckFrame? ack = space.BuildAck();
        Assert.That(ack, Is.Not.Null);
        Assert.That(ack!.Ecn, Is.Null); // type 0x02, no ECN counters
    }

    [Test]
    public void LossRecovery_OnIncreasedCeCount_ReducesCongestionWindow()
    {
        var recovery = new LossRecovery();
        recovery.OnPacketSent(space: 0, new SentPacket
        {
            PacketNumber = 0, TimeSentTicks = 0, AckEliciting = true, Size = 1200, RetransmittableFrames = [],
        });
        long before = recovery.Congestion.CongestionWindow;

        // The ACK confirms packet 0 AND reports a CE counter of 1 ⇒ congestion signal.
        var ack = new AckFrame([new PacketNumberRange(0, 0)], 0, new EcnCounts(0, 0, 1));
        recovery.OnAckReceived(space: 0, ack, System.TimeSpan.Zero, nowTicks: 1000);

        Assert.That(recovery.Congestion.CongestionWindow < before, Is.True, $"An increased CE counter must shrink the window (was {before}, is {recovery.Congestion.CongestionWindow}).");
    }

    [Test]
    public void LossRecovery_SameCeCount_DoesNotReduceTwice()
    {
        var recovery = new LossRecovery();
        recovery.OnPacketSent(0, new SentPacket { PacketNumber = 0, TimeSentTicks = 0, AckEliciting = true, Size = 1200, RetransmittableFrames = [] });
        recovery.OnPacketSent(0, new SentPacket { PacketNumber = 1, TimeSentTicks = 0, AckEliciting = true, Size = 1200, RetransmittableFrames = [] });

        recovery.OnAckReceived(0, new AckFrame([new PacketNumberRange(0, 0)], 0, new EcnCounts(0, 0, 1)), System.TimeSpan.Zero, 1000);
        long afterFirst = recovery.Congestion.CongestionWindow;
        // A second ACK with an UNCHANGED CE counter ⇒ no renewed shrinking.
        recovery.OnAckReceived(0, new AckFrame([new PacketNumberRange(1, 1)], 0, new EcnCounts(0, 0, 1)), System.TimeSpan.Zero, 2000);

        Assert.That(recovery.Congestion.CongestionWindow >= afterFirst, Is.True, "The same CE counter must not shrink again.");
    }

    [Test]
    public void CeMarkedPackets_ReportedByPeer_ReduceSendersCongestionWindow()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();

        long prevCwnd = client.CongestionWindow;
        bool cwndDropped = false;

        // Mark all client→server datagrams as CE ⇒ the server reports CE back in its ACKs,
        // whereupon the client (RFC 9002 §7.3) shrinks the window. Since no real loss occurs
        // in-process, a window decrease can only come from the CE reaction.
        for (int round = 0; round < 30; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg, EcnCodepoint.Ce);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
            if (client.CongestionWindow < prevCwnd)
                cwndDropped = true;
            prevCwnd = client.CongestionWindow;
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        Assert.That(server.ApplicationReceivedCeCount > 0, Is.True, "The server must have counted CE-marked 1-RTT packets.");
        Assert.That(cwndDropped, Is.True, "The server's CE report must shrink the client's congestion window.");
    }
}
