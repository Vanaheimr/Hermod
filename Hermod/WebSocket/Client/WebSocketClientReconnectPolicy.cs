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

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// Configures how a <see cref="WebSocketClient"/> reconnects after an
    /// unexpected loss of the WebSocket connection: exponentially increasing
    /// delays with random jitter, an optional cap on the delay, and an optional
    /// cap on the number of attempts.
    ///
    /// Assign an instance to <see cref="WebSocketClient.ReconnectPolicy"/> to
    /// enable automatic reconnects; leaving it null disables them. A clean,
    /// application-initiated close (via <see cref="WebSocketClient.Close"/>) and
    /// a fatal protocol violation never trigger a reconnect.
    /// </summary>
    public sealed class WebSocketClientReconnectPolicy
    {

        #region Properties

        /// <summary>
        /// The delay before the first reconnect attempt, and the base that the
        /// exponential backoff multiplies. Default: 1 second.
        /// </summary>
        public TimeSpan  InitialDelay     { get; }

        /// <summary>
        /// The upper bound for the (pre-jitter) reconnect delay. Default: 30 seconds.
        /// </summary>
        public TimeSpan  MaxDelay         { get; }

        /// <summary>
        /// The factor by which the delay grows after each failed attempt
        /// (delay ≈ InitialDelay · BackoffFactor^(attempt-1), capped at MaxDelay).
        /// A value of 1.0 yields a constant delay. Default: 2.0.
        /// </summary>
        public Double    BackoffFactor    { get; }

        /// <summary>
        /// The relative amount of random jitter applied to each delay, in the
        /// range [0, 1]. 0.2 means the actual delay is uniformly distributed
        /// within ±20 % of the computed backoff. Jitter spreads reconnect
        /// attempts of many clients out in time ("thundering herd"). Default: 0.2.
        /// </summary>
        public Double    JitterRatio      { get; }

        /// <summary>
        /// The maximum number of consecutive reconnect attempts before giving up,
        /// or null for an unlimited number of attempts. The counter resets after
        /// every successfully (re)established connection. Default: null (unlimited).
        /// </summary>
        public UInt32?   MaxAttempts      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new WebSocket client reconnect policy.
        /// </summary>
        /// <param name="InitialDelay">The delay before the first reconnect attempt (default: 1 second).</param>
        /// <param name="MaxDelay">The upper bound for the reconnect delay (default: 30 seconds).</param>
        /// <param name="BackoffFactor">The exponential backoff factor (default: 2.0).</param>
        /// <param name="JitterRatio">The relative random jitter in [0, 1] (default: 0.2).</param>
        /// <param name="MaxAttempts">The maximum number of consecutive attempts, or null for unlimited (default: null).</param>
        public WebSocketClientReconnectPolicy(TimeSpan?  InitialDelay    = null,
                                              TimeSpan?  MaxDelay        = null,
                                              Double     BackoffFactor   = 2.0,
                                              Double     JitterRatio     = 0.2,
                                              UInt32?    MaxAttempts     = null)
        {

            this.InitialDelay   = InitialDelay ?? TimeSpan.FromSeconds(1);
            this.MaxDelay       = MaxDelay     ?? TimeSpan.FromSeconds(30);
            this.BackoffFactor  = BackoffFactor < 1.0 ? 1.0 : BackoffFactor;
            this.JitterRatio    = JitterRatio  < 0.0 ? 0.0 : (JitterRatio > 1.0 ? 1.0 : JitterRatio);
            this.MaxAttempts    = MaxAttempts;

            if (this.MaxDelay < this.InitialDelay)
                this.MaxDelay = this.InitialDelay;

        }

        #endregion


        #region DelayForAttempt(Attempt)

        /// <summary>
        /// Compute the (jittered) delay to wait before the given reconnect attempt.
        /// </summary>
        /// <param name="Attempt">The 1-based reconnect attempt number.</param>
        public TimeSpan DelayForAttempt(UInt32 Attempt)
        {

            // Exponential backoff: InitialDelay * BackoffFactor^(Attempt-1), capped at MaxDelay.
            // Guard the exponent against overflow to +Infinity for large attempt counts.
            var exponent  = Attempt > 0 ? (Double) (Attempt - 1) : 0.0;
            var factor    = Math.Pow(BackoffFactor, Math.Min(exponent, 1024.0));

            var baseMs    = InitialDelay.TotalMilliseconds * factor;
            if (Double.IsNaN(baseMs) || baseMs > MaxDelay.TotalMilliseconds)
                baseMs = MaxDelay.TotalMilliseconds;

            // Symmetric jitter within ±JitterRatio, then clamp to [0, MaxDelay].
            if (JitterRatio > 0.0)
            {
                var delta = baseMs * JitterRatio * (2.0 * Random.Shared.NextDouble() - 1.0);
                baseMs += delta;
            }

            if (baseMs < 0.0)
                baseMs = 0.0;

            if (baseMs > MaxDelay.TotalMilliseconds)
                baseMs = MaxDelay.TotalMilliseconds;

            return TimeSpan.FromMilliseconds(baseMs);

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"reconnect: {InitialDelay.TotalSeconds:F1}s..{MaxDelay.TotalSeconds:F1}s, x{BackoffFactor}, ±{JitterRatio:P0} jitter, {(MaxAttempts.HasValue ? $"max {MaxAttempts} attempts" : "unlimited")}";

        #endregion

    }

}
