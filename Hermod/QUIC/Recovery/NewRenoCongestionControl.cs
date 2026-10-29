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
/// recovery over a congestion window (in bytes). Deliberately kept simple (no CUBIC/BBR). The
/// initial slow start ends on rising delay as well as on loss (HyStart++, RFC 9406).
/// </summary>
public sealed class NewRenoCongestionControl
{
    private const int MaxDatagramSize = 1200;
    private const double LossReductionFactor = 0.5;

    // ---- HyStart++ (RFC 9406) --------------------------------------------------------------
    //
    // Loss is the only exit plain slow start has. On a path with a deep buffer and no loss - a
    // loopback, or any bloated router - the window doubles every round trip until the transfer
    // ends, and all of the excess waits in a queue: HTTP3ConformanceTests measured the server's RTT
    // at ~60 ms with 32 responses in flight on a 0.5 ms path. HyStart++ watches each round's
    // minimum RTT instead. When it rises by a threshold, growth drops to a quarter (conservative
    // slow start, CSS) for at most five rounds, and then congestion avoidance takes over. If the
    // delay falls back below where it stood when CSS began, the exit was spurious and slow start
    // resumes. The constants are the RFC's recommendations (§4.3); L is unbounded because the
    // sender paces.
    private static readonly TimeSpan MinRttThresh = TimeSpan.FromMilliseconds(4);
    private static readonly TimeSpan MaxRttThresh = TimeSpan.FromMilliseconds(16);
    private const int MinRttDivisor    = 8;
    private const int NRttSample       = 8;
    private const int CssGrowthDivisor = 4;
    private const int CssRounds        = 5;

    private enum HyStartPhase { SlowStart, ConservativeSlowStart, Off }

    private HyStartPhase _hyStart = HyStartPhase.SlowStart;
    private ulong        _roundEnd;                                 // windowEnd: the round ends once this packet number is acknowledged
    private TimeSpan     _lastRoundMinRtt    = TimeSpan.MaxValue;   // MaxValue = no sample yet ("infinity")
    private TimeSpan     _currentRoundMinRtt = TimeSpan.MaxValue;
    private int          _rttSampleCount;
    private TimeSpan     _cssBaselineMinRtt  = TimeSpan.MaxValue;
    private int          _cssRoundsDone;

    /// <summary>
    /// In conservative slow start (RFC 9406 §4.2): delay has risen, growth runs at a quarter. Tests only.
    /// </summary>
    internal bool InConservativeSlowStartForTest => _hyStart == HyStartPhase.ConservativeSlowStart;

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
    /// The sender left the window unused for want of something to send - no data, or flow control
    /// (RFC 9002 §7.8). While it is, acknowledgments do not grow the window: a window that is never
    /// filled has proved nothing about the path, and growing it anyway lets it reach megabytes on
    /// light traffic, all of which the first burst then sends at once. Set by the endpoint after
    /// every send pass; pacing does not count as application-limited (§7.8 again).
    /// </summary>
    public bool IsApplicationLimited { get; set; }

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

        if (IsApplicationLimited)
            return;

        // Acknowledgments for packets sent before recovery do not increase the window.
        if (_recoveryStartTimeTicks >= 0 && sentTimeTicks <= _recoveryStartTimeTicks)
            return;

        if (InSlowStart)
            CongestionWindow += _hyStart == HyStartPhase.ConservativeSlowStart ? bytes / CssGrowthDivisor : bytes;
        else
            CongestionWindow += (int)((long)MaxDatagramSize * bytes / CongestionWindow);
    }

    /// <summary>
    /// HyStart++ (RFC 9406 §4.2), once per acknowledgment in the application space, after its
    /// packets have been taken: <paramref name="rttSample"/> is the RTT it yielded, if any;
    /// <paramref name="largestAcked"/> its largest acknowledged packet number; and
    /// <paramref name="nextPacketNumber"/> the number the next packet will carry (SND.NXT), where
    /// the next round ends.
    /// </summary>
    public void OnAcknowledgment(TimeSpan? rttSample, ulong largestAcked, ulong nextPacketNumber)
    {
        // Only the initial slow start (§4.2): after a loss, ssthresh holds what the path showed.
        if (_hyStart == HyStartPhase.Off)
            return;

        if (rttSample is { } rtt)
        {
            if (rtt < _currentRoundMinRtt)
                _currentRoundMinRtt = rtt;
            _rttSampleCount++;
        }

        if (_rttSampleCount >= NRttSample &&
            _currentRoundMinRtt != TimeSpan.MaxValue && _lastRoundMinRtt != TimeSpan.MaxValue)
        {
            if (_hyStart == HyStartPhase.SlowStart)
            {
                TimeSpan rttThresh = TimeSpan.FromTicks(Math.Clamp(_lastRoundMinRtt.Ticks / MinRttDivisor,
                                                                   MinRttThresh.Ticks, MaxRttThresh.Ticks));
                if (_currentRoundMinRtt >= _lastRoundMinRtt + rttThresh)
                {
                    _cssBaselineMinRtt = _currentRoundMinRtt;
                    _cssRoundsDone     = 0;
                    _hyStart           = HyStartPhase.ConservativeSlowStart;
                }
            }
            else if (_currentRoundMinRtt < _cssBaselineMinRtt)
            {
                // The delay that started CSS is gone again: the exit was spurious.
                _cssBaselineMinRtt = TimeSpan.MaxValue;
                _hyStart           = HyStartPhase.SlowStart;
            }
        }

        if (largestAcked < _roundEnd)
            return;

        // The round is over. CSS counts its rounds, the partial one it began in included.
        if (_hyStart == HyStartPhase.ConservativeSlowStart && ++_cssRoundsDone >= CssRounds)
        {
            SlowStartThreshold = CongestionWindow; // congestion avoidance from here on
            _hyStart           = HyStartPhase.Off;
            return;
        }
        _lastRoundMinRtt    = _currentRoundMinRtt;
        _currentRoundMinRtt = TimeSpan.MaxValue;
        _rttSampleCount     = 0;
        _roundEnd           = nextPacketNumber;
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
        _hyStart = HyStartPhase.Off; // the path has spoken; later slow starts stop at ssthresh
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
        _hyStart = HyStartPhase.Off;
    }
}
