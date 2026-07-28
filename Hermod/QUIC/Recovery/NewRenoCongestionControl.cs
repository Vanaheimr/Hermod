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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;

/// <summary>
/// NewReno congestion control per RFC 9002 §7 / appendix B: slow start, congestion avoidance and
/// recovery over a congestion window (in bytes). Deliberately kept simple (no CUBIC/BBR).
/// </summary>
public sealed class NewRenoCongestionControl
{
    private const int MaxDatagramSize = 1200;
    private const double LossReductionFactor = 0.5;

    /// <summary>
    /// Minimum window (RFC 9002: kMinimumWindow = 2 · max_datagram_size).
    /// </summary>
    public static int MinimumWindow => 2 * MaxDatagramSize;

    /// <summary>
    /// Initial window (RFC 9002: kInitialWindow = min(10·MDS, max(2·MDS, 14720))).
    /// </summary>
    public static int InitialWindow => Math.Min(10 * MaxDatagramSize, Math.Max(2 * MaxDatagramSize, 14720));

    private long _recoveryStartTimeTicks = -1;

    public int CongestionWindow { get; private set; } = InitialWindow;
    public long BytesInFlight { get; private set; }
    public long SlowStartThreshold { get; private set; } = long.MaxValue;

    public bool InSlowStart => CongestionWindow < SlowStartThreshold;

    /// <summary>
    /// Sending is allowed as long as the bytes in flight stay below the window.
    /// </summary>
    public bool CanSend(int bytes) => BytesInFlight + bytes <= CongestionWindow;

    /// <summary>
    /// Available send volume (window minus bytes in flight).
    /// </summary>
    public long Available => Math.Max(0, CongestionWindow - BytesInFlight);

    public void OnPacketSent(int bytes) => BytesInFlight += bytes;

    /// <summary>
    /// When discarding a packet-number space (RFC 9002 §6.4): subtract bytes in flight without a congestion event.
    /// </summary>
    public void OnPacketDiscarded(int bytes) => BytesInFlight = Math.Max(0, BytesInFlight - bytes);

    /// <summary>
    /// A packet was acknowledged (RFC 9002 §7.3.1/§B.5).
    /// </summary>
    public void OnPacketAcked(int bytes, long sentTimeTicks)
    {
        BytesInFlight = Math.Max(0, BytesInFlight - bytes);

        // Acknowledgments for packets sent before recovery do not increase the window.
        if (_recoveryStartTimeTicks >= 0 && sentTimeTicks <= _recoveryStartTimeTicks)
            return;

        if (InSlowStart)
            CongestionWindow += bytes;
        else
            CongestionWindow += (int)((long)MaxDatagramSize * bytes / CongestionWindow);
    }

    /// <summary>
    /// Packet loss detected (RFC 9002 §7.3.2/§B.6): halve the window, begin recovery.
    /// </summary>
    public void OnPacketsLost(int bytes, long largestLostSentTimeTicks, long nowTicks)
    {
        BytesInFlight = Math.Max(0, BytesInFlight - bytes);
        OnCongestionEvent(largestLostSentTimeTicks, nowTicks);
    }

    /// <summary>
    /// Reaction to an ECN-CE report (RFC 9002 §7.3): the sender treats an increased CE counter like a
    /// loss – halve the window, begin recovery. <paramref name="largestAckedSentTimeTicks"/> is the
    /// send time of the largest acknowledged packet, so the window shrinks only once per recovery period.
    /// </summary>
    public void OnEcnCongestionEvent(long largestAckedSentTimeTicks, long nowTicks)
        => OnCongestionEvent(largestAckedSentTimeTicks, nowTicks);

    private void OnCongestionEvent(long sentTimeTicks, long nowTicks)
    {
        // No renewed shrinking within an ongoing recovery period.
        if (_recoveryStartTimeTicks >= 0 && sentTimeTicks <= _recoveryStartTimeTicks)
            return;

        _recoveryStartTimeTicks = nowTicks;
        SlowStartThreshold = (long)(CongestionWindow * LossReductionFactor);
        CongestionWindow = (int)Math.Max(SlowStartThreshold, MinimumWindow);
    }

    /// <summary>
    /// Persistent congestion detected (RFC 9002 §7.6/§B.8): the window collapses to the minimum and
    /// the recovery period is reset (<c>congestion_recovery_start_time = 0</c>), so the next
    /// acknowledgments ramp up again in slow start.
    /// </summary>
    public void OnPersistentCongestion()
    {
        CongestionWindow = MinimumWindow;
        _recoveryStartTimeTicks = -1;
    }
}
