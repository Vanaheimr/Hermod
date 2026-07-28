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
/// Token-bucket pacer per RFC 9002 §7.7. Spreads the sent bytes over time so the whole congestion
/// window does not go out as one burst (which provokes queues and losses). The rate is
/// <c>N · congestion_window / smoothed_rtt</c> (N = 1.25); the budget is capped at a small burst so
/// that after idle time not arbitrarily much can be "caught up".
/// <para>Timestamps are <see cref="TimeSpan.Ticks"/> (100 ns) of a monotonic clock.</para>
/// </summary>
public sealed class Pacer
{
    /// <summary>
    /// Pacing gain N (RFC 9002 §7.7): allows 25 % above the pure cwnd/RTT rate.
    /// </summary>
    private const double PacingGain = 1.25;

    private const int MaxDatagramSize = 1200;

    private double _budget;              // available send budget in bytes (may be transiently negative)
    private long _lastRefillTicks = -1;

    /// <summary>
    /// Current send budget in bytes (clamped to 0 for the caller).
    /// </summary>
    public long AvailableBytes => (long)Math.Max(0, _budget);

    /// <summary>
    /// Refills the budget based on the time elapsed since the last call and the current rate.
    /// On the first call, a full burst is credited.
    /// </summary>
    public void Refill(long nowTicks, long congestionWindow, TimeSpan smoothedRtt)
    {
        long burstCap = BurstCap(congestionWindow);
        if (_lastRefillTicks < 0)
        {
            _lastRefillTicks = nowTicks;
            _budget = burstCap; // initially a full burst may go out (initial window)
            return;
        }

        long elapsed = nowTicks - _lastRefillTicks;
        if (elapsed <= 0)
            return;
        _lastRefillTicks = nowTicks;

        _budget = Math.Min(burstCap, _budget + BytesPerTick(congestionWindow, smoothedRtt) * elapsed);
    }

    /// <summary>
    /// Debits <paramref name="bytes"/> sent bytes (the budget may go negative in the process).
    /// </summary>
    public void OnBytesSent(int bytes) => _budget -= bytes;

    /// <summary>
    /// Bytes per tick at the current rate; without a valid RTT there is no pacing (unlimited).
    /// </summary>
    private static double BytesPerTick(long congestionWindow, TimeSpan smoothedRtt)
    {
        long rttTicks = smoothedRtt.Ticks;
        if (rttTicks <= 0)
            return double.MaxValue;
        return PacingGain * congestionWindow / rttTicks;
    }

    /// <summary>
    /// Burst cap: allows short bursts (at least 2 datagrams), but at most one initial window
    /// (≈ 10 datagrams) or the current window, whichever is smaller.
    /// </summary>
    private static long BurstCap(long congestionWindow)
        => Math.Max(2 * MaxDatagramSize, Math.Min(congestionWindow, 10 * MaxDatagramSize));
}
