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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Recovery;

[TestFixture]
public class RttEstimatorTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Test]
    public void FirstSample_InitializesSmoothedAndVar()
    {
        var rtt = new RttEstimator();
        rtt.AddSample(Ms(100), ackDelay: Ms(0), maxAckDelay: Ms(25));

        Assert.That(rtt.MinRtt, Is.EqualTo(Ms(100)));
        Assert.That(rtt.SmoothedRtt, Is.EqualTo(Ms(100)));
        Assert.That(rtt.RttVar, Is.EqualTo(Ms(50)));
    }

    [Test]
    public void SubsequentSample_SmoothsToward_AndAppliesAckDelay()
    {
        var rtt = new RttEstimator();
        rtt.AddSample(Ms(100), Ms(0), Ms(25));
        rtt.AddSample(Ms(140), ackDelay: Ms(20), maxAckDelay: Ms(25)); // adjusted = 120

        // smoothed = 7/8*100 + 1/8*120 = 102.5 ms
        Assert.That(rtt.SmoothedRtt, Is.EqualTo(Ms(102.5)));
        Assert.That(rtt.MinRtt, Is.EqualTo(Ms(100)));
    }

    [Test]
    public void Pto_UsesInitialRtt_BeforeAnySample()
    {
        var rtt = new RttEstimator();
        Assert.That(rtt.GetProbeTimeout(Ms(25)), Is.EqualTo(2 * RttEstimator.InitialRtt));
    }
}

public class NewRenoTests
{
    [Test]
    public void SlowStart_GrowsWindowByAckedBytes()
    {
        var cc = new NewRenoCongestionControl();
        int start = cc.CongestionWindow;
        cc.OnPacketSent(1200);
        cc.OnPacketAcked(1200, sentTimeTicks: 100);

        Assert.That(cc.InSlowStart, Is.True);
        Assert.That(cc.CongestionWindow, Is.EqualTo(start + 1200));
        Assert.That(cc.BytesInFlight, Is.EqualTo(0));
    }

    [Test]
    public void Loss_HalvesWindow_AndEntersRecovery()
    {
        var cc = new NewRenoCongestionControl();
        int start = cc.CongestionWindow;
        cc.OnPacketSent(1200);
        cc.OnPacketsLost(1200, largestLostSentTimeTicks: 100, nowTicks: 200);

        Assert.That(cc.SlowStartThreshold, Is.EqualTo(start / 2));
        Assert.That(cc.CongestionWindow, Is.EqualTo(Math.Max(start / 2, NewRenoCongestionControl.MinimumWindow)));
        Assert.That(cc.InSlowStart, Is.False);
    }

    [Test]
    public void CanSend_BlocksWhenBytesInFlightReachWindow()
    {
        var cc = new NewRenoCongestionControl();
        int window = cc.CongestionWindow;

        cc.OnPacketSent(window); // window exactly filled
        Assert.That(cc.Available, Is.EqualTo(0));
        Assert.That(cc.CanSend(1), Is.False);   // nothing new allowed anymore
        Assert.That(cc.CanSend(0), Is.True);

        cc.OnPacketAcked(window, sentTimeTicks: 1); // released again
        Assert.That(cc.Available > 0, Is.True);
        Assert.That(cc.CanSend(1), Is.True);
    }

    [Test]
    public void PersistentCongestion_CollapsesWindowToMinimum_AndRestartsSlowStart()
    {
        var cc = new NewRenoCongestionControl();
        cc.OnPacketSent(20_000);
        cc.OnPacketsLost(20_000, largestLostSentTimeTicks: 100, nowTicks: 200); // ssthresh set, not slow start

        cc.OnPersistentCongestion();

        Assert.That(cc.CongestionWindow, Is.EqualTo(NewRenoCongestionControl.MinimumWindow));
        Assert.That(cc.InSlowStart, Is.True); // cwnd < ssthresh ⇒ slow start again
    }
}

public class PacerTests
{
    private static long Ticks(double ms) => TimeSpan.FromMilliseconds(ms).Ticks;

    [Test]
    public void FirstRefill_GrantsFullBurst()
    {
        var pacer = new Pacer();
        pacer.Refill(nowTicks: 0, congestionWindow: 12000, smoothedRtt: TimeSpan.FromMilliseconds(100));
        Assert.That(pacer.AvailableBytes, Is.EqualTo(12000)); // burst cap = min(cwnd, 10·MDS) = 12000
    }

    [Test]
    public void Refill_AccumulatesAtRate_ProportionalToElapsedTime()
    {
        var pacer = new Pacer();
        pacer.Refill(0, congestionWindow: 12000, smoothedRtt: TimeSpan.FromMilliseconds(120));
        pacer.OnBytesSent(12000); // budget down to 0

        // Rate = 1.25 · 12000 / 1_200_000 ticks = 0.0125 bytes/tick; over 10 ms (100_000 ticks) → 1250 bytes.
        pacer.Refill(Ticks(10), congestionWindow: 12000, smoothedRtt: TimeSpan.FromMilliseconds(120));
        Assert.That(pacer.AvailableBytes, Is.EqualTo(1250));
    }

    [Test]
    public void Refill_CapsAtBurst_AfterLongIdle()
    {
        var pacer = new Pacer();
        pacer.Refill(0, 12000, TimeSpan.FromMilliseconds(100));
        pacer.OnBytesSent(12000);

        // Very long pause: would accumulate far beyond the burst — gets capped.
        pacer.Refill(Ticks(100_000), 12000, TimeSpan.FromMilliseconds(100));
        Assert.That(pacer.AvailableBytes, Is.EqualTo(12000));
    }

    [Test]
    public void BurstCap_NeverExceeds_TenDatagrams_EvenForLargeWindow()
    {
        var pacer = new Pacer();
        pacer.Refill(0, congestionWindow: 1_000_000, smoothedRtt: TimeSpan.FromMilliseconds(50));
        Assert.That(pacer.AvailableBytes, Is.EqualTo(12000)); // 10 · 1200, regardless of the large cwnd
    }

    [Test]
    public void OverSpending_DrivesBudgetNegative_ReportedAsZero()
    {
        var pacer = new Pacer();
        pacer.Refill(0, congestionWindow: 2000, smoothedRtt: TimeSpan.FromMilliseconds(100));
        Assert.That(pacer.AvailableBytes, Is.EqualTo(2400)); // burst cap = max(2·MDS, min(cwnd, 10·MDS)) = 2400

        pacer.OnBytesSent(3000); // sent more than the budget
        Assert.That(pacer.AvailableBytes, Is.EqualTo(0)); // negative, but reported as 0
    }
}

public class LossRecoveryTests
{
    private static SentPacket Packet(ulong pn, long tick, params Frame[] frames) => new()
    {
        PacketNumber = pn,
        TimeSentTicks = tick,
        AckEliciting = true,
        Size = 1200,
        RetransmittableFrames = frames,
    };

    [Test]
    public void PacketThreshold_MarksOlderPacketsLost_AndReturnsTheirFrames()
    {
        var lr = new LossRecovery();
        var crypto = new CryptoFrame(0, new byte[] { 1, 2, 3 });

        // Send packets 0..3; 0 carries a CRYPTO frame.
        lr.OnPacketSent(0, Packet(0, 1000, crypto));
        for (ulong pn = 1; pn <= 3; pn++)
            lr.OnPacketSent(0, Packet(pn, 1000 + (long)pn));

        // ACK for packet 3 → packet 0 is 3 behind the largest acknowledged ⇒ lost.
        var ack = AckFrame.FromPacketNumbers([3]);
        List<Frame> lost = lr.OnAckReceived(0, ack, TimeSpan.Zero, nowTicks: 2000);

        Assert.That(lost, Does.Contain(crypto));
    }

    [Test]
    public void Ack_UpdatesRtt_FromLargestAcked()
    {
        var lr = new LossRecovery();
        lr.OnPacketSent(0, Packet(0, tick: 0));

        // "now" = 100 ms later (in ticks).
        long now = TimeSpan.FromMilliseconds(100).Ticks;
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([0]), TimeSpan.Zero, now);

        Assert.That(lr.Rtt.SmoothedRtt, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
    }

    [Test]
    public void ProbeTimeout_ReturnsOldestUnackedFrames()
    {
        var lr = new LossRecovery();
        var crypto = new CryptoFrame(0, new byte[] { 9 });
        lr.OnPacketSent(1, Packet(0, tick: 0, crypto));

        Assert.That(lr.GetProbeTimeoutDeadline() > 0, Is.True);
        lr.OnProbeTimeoutFired();
        List<Frame> probe = lr.GetProbeFrames(1);
        Assert.That(probe, Does.Contain(crypto));
        Assert.That(lr.PtoCount, Is.EqualTo(1));
    }

    [Test]
    public void PtoStaysArmed_WithNothingInFlight_UntilThePeerHasValidatedTheAddress()
    {
        // RFC 9002 §6.2.2.1: normally the PTO timer is cancelled once nothing ack-eliciting is in
        // flight. NOT so at a client whose address the server has not yet validated — it has to keep
        // probing to unblock the server, otherwise a lost server flight deadlocks both sides.
        var lr = new LossRecovery { PeerCompletedAddressValidation = false };
        lr.OnPacketSent(0, Packet(0, tick: Ms(0)));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([0]), TimeSpan.Zero, Ms(50));

        Assert.That(lr.LastAckElicitingSentTicks, Is.EqualTo(-1), "Nothing is in flight any more.");
        Assert.That(lr.GetProbeTimeoutDeadline(), Is.GreaterThan(0),
                    "Before address validation the client must keep a PTO armed.");

        // Once a Handshake ACK arrives (or the handshake is complete), the normal rule applies again.
        lr.PeerCompletedAddressValidation = true;
        Assert.That(lr.GetProbeTimeoutDeadline(), Is.EqualTo(-1));
    }

    [Test]
    public void PtoTimer_StaysCancelled_ForTheServer()
    {
        // The server validates the client implicitly (RFC 9002 §A.6) – the default must not change
        // its behaviour.
        var lr = new LossRecovery();
        lr.OnPacketSent(0, Packet(0, tick: Ms(0)));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([0]), TimeSpan.Zero, Ms(50));

        Assert.That(lr.GetProbeTimeoutDeadline(), Is.EqualTo(-1));
    }

    private static long Ms(double ms) => TimeSpan.FromMilliseconds(ms).Ticks;

    /// <summary>
    /// Non-ack-eliciting packet (triggers no RTT sample) to trigger losses deliberately.
    /// </summary>
    private static SentPacket Trigger(ulong pn, long tick) => new()
    {
        PacketNumber = pn,
        TimeSentTicks = tick,
        AckEliciting = false,
        Size = 40,
        RetransmittableFrames = [],
    };

    [Test]
    public void PersistentCongestion_CollapsesWindow_AfterLongLossRun()
    {
        var lr = new LossRecovery { MaxAckDelay = TimeSpan.FromMilliseconds(25) };

        // First RTT sample: send packet 0, acknowledge 100 ms later ⇒ smoothed_rtt = 100 ms,
        // rttvar = 50 ms ⇒ PC duration = (100 + 200 + 25) · 3 = 975 ms.
        lr.OnPacketSent(0, Packet(0, tick: 0));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([0]), TimeSpan.Zero, Ms(100));
        long cwndBefore = lr.Congestion.CongestionWindow;

        // Ack-eliciting packets 1..8 spread over 1050 ms (all sent after the first sample).
        for (ulong pn = 1; pn <= 8; pn++)
            lr.OnPacketSent(0, Packet(pn, Ms(200 + (pn - 1) * 150)));

        // Acknowledge a later, non-ack-eliciting packet 20 ⇒ 1..8 count as lost per the packet threshold.
        lr.OnPacketSent(0, Trigger(20, Ms(1400)));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([20]), TimeSpan.Zero, Ms(1400));

        // Span 1..8 = 1050 ms > 975 ms ⇒ persistent congestion ⇒ the window collapses to the minimum.
        Assert.That(lr.Congestion.CongestionWindow, Is.EqualTo(NewRenoCongestionControl.MinimumWindow));
        Assert.That(lr.Congestion.CongestionWindow < cwndBefore, Is.True);
    }

    [Test]
    public void PersistentCongestion_NotEstablished_WhenLossSpanTooShort()
    {
        var lr = new LossRecovery { MaxAckDelay = TimeSpan.FromMilliseconds(25) };
        lr.OnPacketSent(0, Packet(0, tick: 0));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([0]), TimeSpan.Zero, Ms(100)); // PC duration = 975 ms

        // Packets 1..5 spread over only 200 ms ⇒ span < PC duration.
        for (ulong pn = 1; pn <= 5; pn++)
            lr.OnPacketSent(0, Packet(pn, Ms(200 + (pn - 1) * 50)));
        lr.OnPacketSent(0, Trigger(20, Ms(500)));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([20]), TimeSpan.Zero, Ms(500));

        // No PC: the window is only halved by the congestion event, not collapsed to the minimum.
        Assert.That(lr.Congestion.CongestionWindow > NewRenoCongestionControl.MinimumWindow, Is.True);
    }

    [Test]
    public void PersistentCongestion_NotEstablished_BeforeFirstRttSample()
    {
        var lr = new LossRecovery { MaxAckDelay = TimeSpan.FromMilliseconds(25) };

        // Without ever an RTT sample: long loss run 1..8, triggered by non-ack-eliciting packet 20.
        for (ulong pn = 1; pn <= 8; pn++)
            lr.OnPacketSent(0, Packet(pn, Ms(200 + (pn - 1) * 150)));
        lr.OnPacketSent(0, Trigger(20, Ms(1400)));
        lr.OnAckReceived(0, AckFrame.FromPacketNumbers([20]), TimeSpan.Zero, Ms(1400));

        // PC must not take effect without a prior RTT sample (RFC 9002 §7.6.2).
        Assert.That(lr.Congestion.CongestionWindow > NewRenoCongestionControl.MinimumWindow, Is.True);
    }
}
