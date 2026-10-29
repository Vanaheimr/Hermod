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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Recovery;

/// <summary>
/// HyStart++ (RFC 9406): the initial slow start ends on rising delay, not only on loss.
/// </summary>
/// <remarks>
/// Each test drives the controller through rounds: a window's worth of 1200-byte packets sent, then
/// each acknowledged on its own with the round's RTT. That is the shape of slow start on a path
/// whose queue only changes between rounds, which keeps the expected numbers exact.
/// </remarks>
[TestFixture]
public class HyStartTests
{
    private const int Packet = 1200;

    private ulong _nextPacketNumber;

    [SetUp]
    public void Reset() => _nextPacketNumber = 0;

    private void Round(NewRenoCongestionControl cc, double rttMs)
    {
        int packets = cc.CongestionWindow / Packet;
        ulong first = _nextPacketNumber;
        for (int i = 0; i < packets; i++)
            cc.OnPacketSent(Packet);
        _nextPacketNumber += (ulong) packets;
        for (int i = 0; i < packets; i++)
        {
            cc.OnPacketAcked(Packet, sentTimeTicks: 0);
            cc.OnAcknowledgment(TimeSpan.FromMilliseconds(rttMs), first + (ulong) i, _nextPacketNumber);
        }
    }

    [Test]
    public void SteadyDelay_KeepsSlowStartDoubling()
    {
        var cc = new NewRenoCongestionControl();
        int initial = cc.CongestionWindow;
        for (int round = 0; round < 6; round++)
            Round(cc, rttMs: 10);

        Assert.That(cc.InSlowStart, Is.True, "nothing rose, so nothing ends slow start");
        Assert.That(cc.InConservativeSlowStartForTest, Is.False);
        Assert.That(cc.CongestionWindow, Is.EqualTo(initial << 6), "six rounds, six doublings");
    }

    [Test]
    public void RisingDelay_EndsSlowStart_AfterFiveConservativeRounds()
    {
        var cc = new NewRenoCongestionControl();
        Round(cc, rttMs: 10);
        Round(cc, rttMs: 10);

        // 10 ms + max(4 ms, 10/8 ms) = 14 ms: a round whose fastest packet took 15 ms is a queue.
        Round(cc, rttMs: 15);
        Assert.That(cc.InConservativeSlowStartForTest, Is.True, "the delay rose past the threshold");

        int before = cc.CongestionWindow;
        int acked  = before / Packet * Packet;
        Round(cc, rttMs: 15);
        Assert.That(cc.CongestionWindow, Is.EqualTo(before + acked / 4),
                    "CSS grows by a quarter of what is acknowledged, slow start by all of it");

        // A round ends with the first acknowledgment of the next one. CSS began in the third round,
        // so its fifth round ends as the eighth begins.
        for (int round = 5; round <= 7; round++)
        {
            Round(cc, rttMs: 15);
            Assert.That(cc.InSlowStart, Is.True, $"round {round}: still conservative slow start");
        }
        int windowAtEnd = cc.CongestionWindow;
        Round(cc, rttMs: 15);
        Assert.That(cc.InSlowStart, Is.False, "five CSS rounds, the partial first one included, then congestion avoidance");
        Assert.That(cc.SlowStartThreshold, Is.InRange(windowAtEnd, windowAtEnd + Packet),
                    "ssthresh = cwnd where CSS ended (§4.2) - one CSS acknowledgment into the round");
    }

    [Test]
    public void RisesBelowTheThreshold_DoNotEndSlowStart()
    {
        // 3 ms over a 10 ms base is under the 4 ms floor; 9 ms over an 80 ms base is under 80/8.
        var shortPath = new NewRenoCongestionControl();
        Round(shortPath, rttMs: 10);
        Round(shortPath, rttMs: 13);
        Round(shortPath, rttMs: 13);
        Assert.That(shortPath.InConservativeSlowStartForTest, Is.False, "+3 ms on 10 ms");

        _nextPacketNumber = 0;
        var longPath = new NewRenoCongestionControl();
        Round(longPath, rttMs: 80);
        Round(longPath, rttMs: 89);
        Assert.That(longPath.InConservativeSlowStartForTest, Is.False, "+9 ms on 80 ms, threshold 10 ms");
        Round(longPath, rttMs: 101);
        Assert.That(longPath.InConservativeSlowStartForTest, Is.True, "+12 ms on 89 ms crosses it");
    }

    [Test]
    public void DelayFallingBackDuringConservativeSlowStart_ResumesSlowStart()
    {
        var cc = new NewRenoCongestionControl();
        Round(cc, rttMs: 10);
        Round(cc, rttMs: 15);
        Assert.That(cc.InConservativeSlowStartForTest, Is.True, "precondition: in CSS");

        // Below the 15 ms the exit was measured at: the queue was a passing one (§4.2).
        Round(cc, rttMs: 11);
        Assert.That(cc.InConservativeSlowStartForTest, Is.False, "the exit was spurious");
        Assert.That(cc.InSlowStart, Is.True);
    }

    [Test]
    public void AfterALoss_RisingDelayNoLongerCounts()
    {
        var cc = new NewRenoCongestionControl();
        Round(cc, rttMs: 10);
        Round(cc, rttMs: 10);
        cc.OnPacketSent(Packet);
        cc.OnPacketsLost(Packet, largestLostSentTimeTicks: 1, nowTicks: 2);
        cc.OnPersistentCongestion(); // back to the minimum window, below ssthresh: slow start again

        Assert.That(cc.InSlowStart, Is.True, "precondition: slow start after persistent congestion");
        Round(cc, rttMs: 10);
        Round(cc, rttMs: 30);
        Round(cc, rttMs: 30);
        Assert.That(cc.InConservativeSlowStartForTest, Is.False,
                    "HyStart++ is for the initial slow start; this one ends at the ssthresh the loss set");
    }

    [Test]
    public void ADeepBuffer_IsNotFilledWithTheWholeTransfer()
    {
        // Real connections on a simulated clock, over a 12 MB/s bottleneck with 5 ms each way and a
        // buffer that never drops: 120 KB in flight fill the path, and everything beyond waits.
        // Without a loss, plain slow start never ends, so the window - and the queue - grows until the
        // transfer is through. Measured on this setup:
        //
        //                       longest queue   4 MiB took   window at the end
        //   plain slow start       173 ms         395 ms         2.2 MB
        //   HyStart++               86 ms         400 ms         1.2 MB
        //
        // Not a few milliseconds: after the exit, five conservative rounds grow the window by a quarter
        // each (§4.2), about threefold, before congestion avoidance takes over. That is the RFC's
        // trade against exiting on noise. The bottleneck stays as busy either way.
        const int payloadSize = 4 * 1024 * 1024;
        var step = TimeSpan.FromTicks(500); // 50 µs; one datagram takes 100 µs through the bottleneck
        var time = new FakeTimeProvider();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: time,
                                                    maxDatagramSizeCeiling: PathMtuDiscovery.BasePlpmtu);
        using var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxDataValue                 = 2 * payloadSize,
            InitialMaxStreamDataBidiRemoteValue = 2 * payloadSize,
        }, timeProvider: time);
        var up   = new SimulatedLink(bytesPerSecond: 12_000_000, delay: TimeSpan.FromMilliseconds(5));
        var down = new SimulatedLink(bytesPerSecond: 0,          delay: TimeSpan.FromMilliseconds(5));

        client.Start();
        QuicStream? stream = null;
        int received = 0;
        long now = 0, startedAt = 0;
        for (int i = 0; i < 400_000 && received < payloadSize; i++)
        {
            time.Advance(step);
            now += step.Ticks;
            foreach (byte[] dg in up.Deliver(now))   server.ProcessDatagram(dg);
            foreach (byte[] dg in down.Deliver(now)) client.ProcessDatagram(dg);
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();
            if (stream is null && client.HandshakeConfirmed)
            {
                stream = client.OpenBidirectionalStream();
                stream.Write(new byte[payloadSize]);
                stream.Finish();
                startedAt = now;
            }
            foreach (byte[] dg in client.GetDatagramsToSend()) up.Send(now, dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) down.Send(now, dg);
            if (stream is not null && server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received += peer.Read().Length;
        }

        var transfer = TimeSpan.FromTicks(now - startedAt);
        var lineRate = TimeSpan.FromSeconds(payloadSize / 12_000_000.0);
        Assert.That(received, Is.EqualTo(payloadSize), "the whole payload must arrive");
        Assert.That(up.MaxQueueingDelay, Is.LessThan(TimeSpan.FromMilliseconds(120)),
                    "slow start must end on the delay it causes, not run until the transfer does");
        Assert.That(transfer, Is.LessThan(1.3 * lineRate),
                    $"{transfer.TotalMilliseconds:F0} ms against {lineRate.TotalMilliseconds:F0} ms at line rate: the exit must not starve the bottleneck");
    }

    /// <summary>
    /// One direction of a path: a bottleneck of the given rate (0 = none) whose buffer never drops,
    /// then a fixed delay. Times are in ticks of the simulated clock.
    /// </summary>
    private sealed class SimulatedLink(double bytesPerSecond, TimeSpan delay)
    {
        private readonly Queue<(long Due, byte[] Datagram)> _inTransit = new();
        private long _busyUntil;

        public TimeSpan MaxQueueingDelay { get; private set; }

        public void Send(long now, byte[] datagram)
        {
            long start = Math.Max(now, _busyUntil);
            if (TimeSpan.FromTicks(start - now) > MaxQueueingDelay)
                MaxQueueingDelay = TimeSpan.FromTicks(start - now);
            _busyUntil = start + (bytesPerSecond > 0 ? (long) (datagram.Length / bytesPerSecond * TimeSpan.TicksPerSecond) : 0);
            _inTransit.Enqueue((_busyUntil + delay.Ticks, datagram));
        }

        public List<byte[]> Deliver(long now)
        {
            var due = new List<byte[]>();
            while (_inTransit.TryPeek(out var next) && next.Due <= now)
                due.Add(_inTransit.Dequeue().Datagram);
            return due;
        }
    }
}
