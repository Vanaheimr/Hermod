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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// The ACK-frequency extension (draft-ietf-quic-ack-frequency): the min_ack_delay transport
/// parameter, the ACK_FREQUENCY and IMMEDIATE_ACK frames, and the acknowledgment-scheduling
/// behaviour they drive — including the reordering-threshold tables of §6.2.1.
/// </summary>
[TestFixture]
public class AckFrequencyTests
{

    #region Transport parameter (draft §3)

    [Test]
    public void MinAckDelay_SurvivesTheRoundTrip()
    {
        var parameters = new TransportParameters
        {
            InitialSourceConnectionIdValue = ConnectionId.Empty,
            MinAckDelayUs = 2000,
        };

        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out TransportParameters? decoded), Is.True);
        Assert.That(decoded!.PeerMinAckDelayUs, Is.EqualTo(2000UL));
    }

    [Test]
    public void MinAckDelay_IsAdvertisedByDefault()
    {
        // Sending it is the unilateral opt-in for the extension; we do so out of the box (§3).
        Assert.That(new TransportParameters().MinAckDelayUs, Is.EqualTo(1000UL));
    }

    [Test]
    public void MinAckDelay_CanBeSuppressed()
    {
        var parameters = new TransportParameters { InitialSourceConnectionIdValue = ConnectionId.Empty, MinAckDelayUs = null };
        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out TransportParameters? decoded), Is.True);
        Assert.That(decoded!.PeerMinAckDelayUs, Is.Null);
    }

    [Test]
    public void MinAckDelay_GreaterThanMaxAckDelay_IsRejected()
    {
        // §3: min_ack_delay (µs) MUST NOT exceed max_ack_delay (ms). 30 000 µs = 30 ms > 25 ms default.
        var parameters = new TransportParameters
        {
            InitialSourceConnectionIdValue = ConnectionId.Empty,
            MaxAckDelayMs = 25,
            MinAckDelayUs = 30_000,
        };
        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out _), Is.False,
                    "⇒ TRANSPORT_PARAMETER_ERROR.");
    }

    [Test]
    public void MinAckDelay_EqualToMaxAckDelay_IsAccepted()
    {
        // The boundary: 25 000 µs = 25 ms == max_ack_delay is allowed ("MUST NOT be greater").
        var parameters = new TransportParameters
        {
            InitialSourceConnectionIdValue = ConnectionId.Empty,
            MaxAckDelayMs = 25,
            MinAckDelayUs = 25_000,
        };
        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out _), Is.True);
    }

    #endregion

    #region Frames (draft §4/§5)

    [Test]
    public void AckFrequencyFrame_RoundTrips()
    {
        var frame = new AckFrequencyFrame(SequenceNumber: 7, AckElicitingThreshold: 10,
                                          RequestedMaxAckDelayUs: 5000, ReorderingThreshold: 3);
        byte[] bytes = FrameParser.Serialize([frame]);

        Assert.That(FrameParser.TryParseAll(bytes, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var decoded = Expect.Type<AckFrequencyFrame>(frames[0]);
        Assert.That(decoded, Is.EqualTo(frame));
    }

    [Test]
    public void ImmediateAckFrame_RoundTrips()
    {
        byte[] bytes = FrameParser.Serialize([ImmediateAckFrame.Instance]);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x1f }), "IMMEDIATE_ACK is a single-byte frame type.");
        Assert.That(FrameParser.TryParseAll(bytes, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        Expect.Type<ImmediateAckFrame>(frames[0]);
    }

    [Test]
    public void AckFrequencyFrame_TruncatedBody_IsEncodingError()
    {
        // Type 0xaf plus only a sequence number, missing the other three fields.
        byte[] bytes = [0x40, 0xaf, 0x01];
        Assert.That(FrameParser.TryParseAll(bytes, out _), Is.EqualTo(FrameParseResult.EncodingError));
    }

    #endregion

    #region Ack-eliciting threshold (draft §6)

    [Test]
    public void RaisedThreshold_DelaysTheAck_UntilItIsExceeded()
    {
        // Threshold 3: an ACK is due only after MORE than 3 ack-eliciting packets (§6).
        var space = new PacketNumberSpace { AckElicitingThreshold = 3 };
        var never = TimeSpan.FromDays(1);

        for (ulong pn = 0; pn < 3; pn++)
        {
            space.RecordReceived(pn, EcnCodepoint.NotEct, 0);
            space.OnAckElicitingReceived(pn, 0);
        }
        Assert.That(space.IsAckDue(0, never, false), Is.False, "Three packets do not exceed the threshold of 3.");

        space.RecordReceived(3, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(3, 0);
        Assert.That(space.IsAckDue(0, never, false), Is.True, "The fourth packet does.");
    }

    [Test]
    public void ThresholdZero_AcknowledgesEveryPacket()
    {
        var space = new PacketNumberSpace { AckElicitingThreshold = 0 };
        space.RecordReceived(0, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(0, 0);
        Assert.That(space.IsAckDue(0, TimeSpan.FromDays(1), false), Is.True, "A single packet already exceeds 0.");
    }

    [Test]
    public void DefaultThreshold_MatchesTheRfc9000TwoPacketRule()
    {
        // The default of 1 must reproduce RFC 9000 §13.2.2 exactly: ACK after two ack-eliciting packets.
        var space = new PacketNumberSpace();
        var never = TimeSpan.FromDays(1);
        space.RecordReceived(0, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(0, 0);
        Assert.That(space.IsAckDue(0, never, false), Is.False);
        space.RecordReceived(1, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(1, 0);
        Assert.That(space.IsAckDue(0, never, false), Is.True);
    }

    #endregion

    #region Reordering threshold (draft §6.2)

    [Test]
    public void ReorderingThresholdZero_NeverForcesAnImmediateAck()
    {
        // §6.2: "A value of 0 indicates out-of-order packets do not elicit an immediate ACK."
        var space = new PacketNumberSpace { ReorderingThreshold = 0, AckElicitingThreshold = 100 };
        space.RecordReceived(0, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(0, 0);
        space.RecordReceived(5, EcnCodepoint.NotEct, 0); // a gap that threshold 1 would acknowledge at once
        space.OnAckElicitingReceived(5, 0);
        Assert.That(space.ImmediateAckNeeded, Is.False);
    }

    [Test]
    public void ReorderingThreshold3_FollowsTheSpecExample()
    {
        // draft §6.2.1, Table 1: reordering threshold 3, acknowledgments only from reordering.
        RunReorderingTable(reorderingThreshold: 3,
            steps:
            [
                (0, false),
                (1, false),
                (3, false),
                (4, false),
                (5, true),   // 5 - 2 >= 3
                (8, false),
                (9, true),   // 9 - 6 >= 3
                (10, true),  // 10 - 7 >= 3
            ]);
    }

    [Test]
    public void ReorderingThreshold5_FollowsTheSpecExample()
    {
        // draft §6.2.1, Table 2: reordering threshold 5.
        RunReorderingTable(reorderingThreshold: 5,
            steps:
            [
                (0, false),
                (1, false),
                (3, false),
                (5, false),
                (6, false),
                (7, true),   // 7 - 2 >= 5
                (8, false),
                (9, true),   // 9 - 4 >= 5
            ]);
    }

    /// <summary>
    /// Feeds the packet numbers of a draft §6.2.1 table into a space one by one and checks whether each
    /// one triggers an immediate ACK. On a triggering packet the ACK is "sent" (BuildAck resets the
    /// state, OnAckFrameSent advances "Largest Acked"), matching the table's "acknowledgments are only
    /// sent due to reordering" assumption. A high ack-eliciting threshold keeps that rule from
    /// interfering.
    /// </summary>
    private static void RunReorderingTable(ulong reorderingThreshold, (ulong Packet, bool ExpectAck)[] steps)
    {
        var space = new PacketNumberSpace { ReorderingThreshold = reorderingThreshold, AckElicitingThreshold = 1000 };
        ulong dummyPn = 0;
        foreach ((ulong packet, bool expectAck) in steps)
        {
            space.RecordReceived(packet, EcnCodepoint.NotEct, 0);
            space.OnAckElicitingReceived(packet, 0);
            Assert.That(space.ImmediateAckNeeded, Is.EqualTo(expectAck),
                        $"Reordering threshold {reorderingThreshold}, received packet {packet}.");
            if (expectAck)
            {
                AckFrame ack = space.BuildAck()!;
                space.OnAckFrameSent(dummyPn++, ack.LargestAcknowledged); // advances "Largest Acked"
            }
        }
    }

    #endregion

    #region ECN CE transition (draft §6.3)

    [Test]
    public void RaisedThreshold_AcknowledgesOnlyTheFirstCeInARun()
    {
        // §6.3: with the threshold above 1, only the non-CE → CE transition forces an immediate ACK,
        // not every CE packet.
        var space = new PacketNumberSpace { AckElicitingThreshold = 10 };

        space.RecordReceived(0, EcnCodepoint.NotEct, 0); // non-CE
        Assert.That(space.ImmediateAckNeeded, Is.False);

        space.RecordReceived(1, EcnCodepoint.Ce, 0);     // transition ⇒ immediate ACK
        Assert.That(space.ImmediateAckNeeded, Is.True);

        AckFrame ack = space.BuildAck()!;                // ACK sent, ImmediateAckNeeded cleared
        space.OnAckFrameSent(0, ack.LargestAcknowledged);
        Assert.That(space.ImmediateAckNeeded, Is.False);

        space.RecordReceived(2, EcnCodepoint.Ce, 0);     // still CE ⇒ no new immediate ACK
        Assert.That(space.ImmediateAckNeeded, Is.False);
    }

    [Test]
    public void DefaultThreshold_AcknowledgesEveryCe()
    {
        // §6.3: with the default threshold (1), RFC 9000 stands — acknowledge every CE-marked packet.
        var space = new PacketNumberSpace();
        space.RecordReceived(0, EcnCodepoint.Ce, 0);
        Assert.That(space.ImmediateAckNeeded, Is.True);
    }

    #endregion

    #region End to end

    private static (QuicClientConnection Client, QuicServerConnection Server, ServerCertificate Cert) Handshake()
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        return (client, server, cert);
    }

    [Test]
    public void SentAckFrequency_IsAdoptedByThePeer()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Handshake();
        using (cert) using (client) using (server)
        {
            // The server asks the client to acknowledge less often and to tolerate more reordering.
            Assert.That(server.TrySendAckFrequency(ackElicitingThreshold: 9,
                                                   requestedMaxAckDelay: TimeSpan.FromMilliseconds(50),
                                                   reorderingThreshold: 3), Is.True,
                        "Both sides advertise min_ack_delay by default, so the extension is available.");

            for (int round = 0; round < 4; round++)
            {
                foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
                foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            }

            Assert.That(client.ApplicationAckElicitingThresholdForTest, Is.EqualTo(9UL));
            Assert.That(client.ApplicationReorderingThresholdForTest, Is.EqualTo(3UL));
            Assert.That(client.LocalMaxAckDelayForTest, Is.EqualTo(TimeSpan.FromMilliseconds(50)));
        }
    }

    [Test]
    public void AckFrequency_IsRefused_WhenThePeerDidNotAdvertiseSupport()
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        // The client switches the extension off, so the server must not send ACK_FREQUENCY to it.
        var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                              transportParameters: new TransportParameters { MinAckDelayUs = null });
        var server = new QuicServerConnection(cert);
        using (cert) using (client) using (server)
        {
            client.Start();
            for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            {
                foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
                foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
            }
            Assert.That(client.HandshakeConfirmed, Is.True);

            Assert.That(server.TrySendAckFrequency(1, TimeSpan.FromMilliseconds(20)), Is.False,
                        "The client advertised no min_ack_delay ⇒ the extension is unavailable.");
        }
    }

    [Test]
    public void AckFrequency_IsRefused_WhenTheRequestedDelayIsOutOfRange()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Handshake();
        using (cert) using (client) using (server)
        {
            // Below the peer's advertised min_ack_delay (1000 µs default) — the peer would reject it.
            Assert.That(server.TrySendAckFrequency(1, TimeSpan.FromMicroseconds(500)), Is.False);
            // 2^14 ms or greater is invalid for max_ack_delay.
            Assert.That(server.TrySendAckFrequency(1, TimeSpan.FromMilliseconds(1 << 14)), Is.False);
        }
    }

    #endregion

}
