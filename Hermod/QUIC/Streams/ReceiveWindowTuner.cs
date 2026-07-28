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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

/// <summary>
/// Auto-tuning of the receive-side flow-control window (phase 9). A fixed window throttles a fast,
/// high-RTT connection to ≈ window/RTT throughput — the bandwidth-delay product (BDP) grows with the
/// RTT. Heuristic (like Chromium/quiche): every time the receiver must send a window update (the
/// credit has fallen below half the window), the time since the LAST update is measured. If it is
/// shorter than a small multiple of the RTT, the sender reached the window edge faster than one
/// RTT — the window is the bottleneck ⇒ double it (up to a limit). The window thus settles onto the
/// connection's BDP by itself, without being large from the start.
/// </summary>
public sealed class ReceiveWindowTuner(ulong initialSize, ulong limit)
{
    /// <summary>
    /// Multiple of the smoothed RTT: if the sender triggers the update faster than this many RTTs,
    /// the window counts as the bottleneck and is enlarged.
    /// </summary>
    private const long RttMultiplier = 2;

    private long _lastUpdateTicks = -1;

    /// <summary>
    /// Current window size (starting value = the configured initial_max_data*; grows up to <see cref="Limit"/>).
    /// </summary>
    public ulong Size { get; private set; } = initialSize;

    /// <summary>
    /// Upper bound up to which the window may grow.
    /// </summary>
    public ulong Limit { get; } = Math.Max(initialSize, limit);

    /// <summary>
    /// Reports that a window update is being triggered now (at <paramref name="nowTicks"/>) and
    /// enlarges the window if less than <see cref="RttMultiplier"/>×RTT has passed since the last
    /// update. Returns <c>true</c> when the window grew.
    /// </summary>
    public bool NoteWindowUpdate(long nowTicks, long smoothedRttTicks)
    {
        bool grew = false;
        if (_lastUpdateTicks >= 0 && smoothedRttTicks > 0 && Size < Limit &&
            nowTicks - _lastUpdateTicks < RttMultiplier * smoothedRttTicks)
        {
            Size = Math.Min(Size * 2, Limit);
            grew = true;
        }
        _lastUpdateTicks = nowTicks;
        return grew;
    }
}
