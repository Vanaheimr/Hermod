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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

/// <summary>
/// Minimal hand-rolled fake clock (BCL-only — no Microsoft.Extensions.Time.Testing dependency):
/// timestamp and wall clock only move via <see cref="Advance"/>. One tick = 100 ns
/// (<see cref="TimeSpan.TicksPerSecond"/> as the frequency), so elapsed-time math is exact.
/// Timers (<see cref="TimeProvider.CreateTimer"/>) are NOT faked — use this only with the
/// deterministic core (CheckTimeouts and friends), not with Task.Delay-based code.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private long _timestamp;
    private DateTimeOffset _utcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _timestamp;
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Moves the fake clock forward by <paramref name="delta"/> (timestamp AND wall clock).
    /// </summary>
    public void Advance(TimeSpan delta)
    {
        _timestamp += delta.Ticks;
        _utcNow += delta;
    }
}
