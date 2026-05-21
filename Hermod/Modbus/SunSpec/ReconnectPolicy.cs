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

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Retry / backoff / timeout policy for Modbus clients. Used by
/// <c>ModbusTlsClient</c> (and reusable by <see cref="ModbusTCPClient"/> if
/// wired up in future).
///
/// Defaults are conservative: 5 attempts, 200ms base delay, 30s max delay,
/// 25% jitter, 10s per-call timeout, NO retry of write operations.
///
/// The retry loop only kicks in for transient failures (network errors,
/// timeouts, malformed responses). Permanent failures (TLS handshake auth
/// errors) and Modbus exception responses propagate immediately.
/// </summary>
public sealed record ReconnectPolicy
{
    /// <summary>
    /// Maximum number of attempts including the initial one. 1 = never retry.
    /// Use <see cref="int.MaxValue"/> for "retry forever (or until cancelled)".
    /// </summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Initial delay before the first retry. Doubles per attempt.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Cap on the per-attempt backoff delay (before jitter).</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Random jitter as a fraction of the computed delay, in [0, 1].
    /// 0.25 means actual delay is in [delay, 1.25 * delay].
    /// </summary>
    public double JitterFraction { get; init; } = 0.25;

    /// <summary>
    /// If true, retry write operations (FC06/FC16) on transient failure.
    /// Most callers want this OFF - blindly replaying a write can cause
    /// double-application. Set to true only when you know the writes are
    /// set-state / idempotent (which is true for most SunSpec writes).
    /// </summary>
    public bool RetryWrites { get; init; } = false;

    /// <summary>
    /// Per-call timeout enforced internally. If exceeded, the in-flight
    /// transaction is cancelled and the connection torn down (the underlying
    /// SslStream may be in an unrecoverable state at that point). Triggers
    /// the retry loop (transient).
    /// </summary>
    public TimeSpan PerCallTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Timeout for the entire connect-then-handshake sequence. Without this,
    /// a black-holed server IP can block the client for the OS default
    /// SYN-retry window (minutes). On expiry, the connect attempt is treated
    /// as a transient failure and the retry loop kicks in.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    // ----- Common presets -----

    /// <summary>No retry. The client behaves like a single-shot connection.</summary>
    public static ReconnectPolicy None { get; } = new() { MaxAttempts = 1 };

    /// <summary>5 attempts, 200ms..30s exponential backoff, reads only.</summary>
    public static ReconnectPolicy Default { get; } = new();

    /// <summary>
    /// Retry forever (until user cancels). Includes writes - only safe if
    /// you know your writes are idempotent.
    /// </summary>
    public static ReconnectPolicy Forever { get; } = new()
    {
        MaxAttempts = int.MaxValue,
        RetryWrites = true,
    };
}
