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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// Idle timeout per RFC 9000 §10.1. The negotiated value is the minimum of the mutually announced
/// <c>max_idle_timeout</c> values (0 = disabled at that peer). Effectively it is raised to at least
/// <c>3·PTO</c> so the timeout never falls below a plausible delivery time. The timer restarts when
/// a packet is received successfully – and on sending an ack-eliciting packet, provided none has
/// been sent since the last receive.
/// <para>Timestamps are <see cref="TimeSpan.Ticks"/> (100 ns) of a monotonic clock.</para>
/// </summary>
public sealed class IdleTimeout
{
    private TimeSpan _negotiated;               // 0 = disabled
    private long _lastActivityTicks;
    private bool _ackElicitingSinceReceive;

    /// <summary>
    /// <c>true</c> once at least one side has announced an idle timeout.
    /// </summary>
    public bool Enabled => _negotiated > TimeSpan.Zero;

    /// <summary>
    /// The negotiated idle timeout (not yet combined with 3·PTO).
    /// </summary>
    public TimeSpan Negotiated => _negotiated;

    /// <summary>
    /// Negotiates the idle timeout from the local and peer values (in ms): the minimum of the
    /// non-zero values; if both are 0, the timeout stays disabled.
    /// </summary>
    public void Negotiate(ulong localMs, ulong peerMs)
    {
        ulong negotiated = (localMs, peerMs) switch
        {
            (0, 0) => 0,
            (0, _) => peerMs,
            (_, 0) => localMs,
            _ => Math.Min(localMs, peerMs),
        };
        _negotiated = TimeSpan.FromMilliseconds(negotiated);
    }

    /// <summary>
    /// Starts the timer (connection start).
    /// </summary>
    public void Start(long nowTicks)
    {
        _lastActivityTicks = nowTicks;
        _ackElicitingSinceReceive = false;
    }

    /// <summary>
    /// A packet was received and processed successfully: restart the timer (RFC 9000 §10.1).
    /// </summary>
    public void OnPacketReceived(long nowTicks)
    {
        _lastActivityTicks = nowTicks;
        _ackElicitingSinceReceive = false;
    }

    /// <summary>
    /// An ack-eliciting packet was sent: restart the timer provided none has been sent since the
    /// last receive (RFC 9000 §10.1) – so pure send bursts do not extend the timeout endlessly.
    /// </summary>
    public void OnAckElicitingPacketSent(long nowTicks)
    {
        if (_ackElicitingSinceReceive)
            return;
        _lastActivityTicks = nowTicks;
        _ackElicitingSinceReceive = true;
    }

    /// <summary>
    /// Whether (keep-alive, RFC 9000 §10.1.2) an ack-eliciting packet is due because more than
    /// <paramref name="interval"/> has passed since the last activity. Only relevant while an idle
    /// timeout is negotiated; <paramref name="interval"/> should be smaller than it.
    /// </summary>
    public bool ShouldSendKeepAlive(long nowTicks, TimeSpan interval)
        => Enabled && nowTicks - _lastActivityTicks >= interval.Ticks;

    /// <summary>
    /// Whether the timeout has expired. The effective bound is <c>max(negotiated, 3·pto)</c>
    /// (RFC 9000 §10.1: "at least three times the current Probe Timeout").
    /// </summary>
    public bool IsExpired(long nowTicks, TimeSpan pto)
    {
        if (!Enabled)
            return false;
        TimeSpan threePto = 3 * pto;
        long limitTicks = (_negotiated > threePto ? _negotiated : threePto).Ticks;
        return nowTicks - _lastActivityTicks > limitTicks;
    }
}
