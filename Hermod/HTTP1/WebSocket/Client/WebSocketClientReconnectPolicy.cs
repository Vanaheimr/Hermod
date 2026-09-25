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

using System.Globalization;

#endregion

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
    ///
    /// A server that turns an attempt away for now - 503, 429 - and says when
    /// to come back, in a Retry-After, is taken at its word up to
    /// <see cref="MaxRetryAfter"/>: the next attempt is not made before then.
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

        /// <summary>
        /// The longest a Retry-After is waited for. Default: 5 minutes.
        /// </summary>
        /// <remarks>
        /// Beyond the delay the backoff would have waited anyway, and not beyond
        /// this: a server that asks for an hour - a maintenance page saying so,
        /// a proxy misconfigured to - is asked again after this, and every time
        /// after that it still says so. A client of something that has to be
        /// reachable, a charging station for one, should not be told by a
        /// header to stay away for a day. Zero ignores Retry-After altogether.
        /// </remarks>
        public TimeSpan  MaxRetryAfter    { get; }

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
        /// <param name="MaxRetryAfter">The longest a Retry-After is waited for (default: 5 minutes; zero ignores it).</param>
        public WebSocketClientReconnectPolicy(TimeSpan?  InitialDelay    = null,
                                              TimeSpan?  MaxDelay        = null,
                                              Double     BackoffFactor   = 2.0,
                                              Double     JitterRatio     = 0.2,
                                              UInt32?    MaxAttempts     = null,
                                              TimeSpan?  MaxRetryAfter   = null)
        {

            this.InitialDelay   = InitialDelay ?? TimeSpan.FromSeconds(1);
            this.MaxDelay       = MaxDelay     ?? TimeSpan.FromSeconds(30);
            this.BackoffFactor  = BackoffFactor < 1.0 ? 1.0 : BackoffFactor;
            this.JitterRatio    = JitterRatio  < 0.0 ? 0.0 : (JitterRatio > 1.0 ? 1.0 : JitterRatio);
            this.MaxAttempts    = MaxAttempts;
            this.MaxRetryAfter  = MaxRetryAfter is TimeSpan maxRetryAfter && maxRetryAfter > TimeSpan.Zero
                                      ? maxRetryAfter
                                      : MaxRetryAfter.HasValue
                                            ? TimeSpan.Zero
                                            : TimeSpan.FromMinutes(5);

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

        #region DelayForAttempt(Attempt, RetryAfter)

        /// <summary>
        /// Compute the delay to wait before the given reconnect attempt, where the
        /// server may have said when to come back.
        /// </summary>
        /// <remarks>
        /// The later of the two: the backoff, or what the server asked for, up to
        /// <see cref="MaxRetryAfter"/>. Never earlier than it asked - that is what
        /// it asked - and where its wish is what decides, the jitter goes on top of
        /// it rather than either side: every client a restarting server turns away
        /// is told the same moment, and a thousand charging stations arriving at it
        /// together is the very thing the server was trying to spread out.
        /// </remarks>
        /// <param name="Attempt">The 1-based reconnect attempt number.</param>
        /// <param name="RetryAfter">How long the server asked to be left alone, if it said.</param>
        public TimeSpan DelayForAttempt(UInt32     Attempt,
                                        TimeSpan?  RetryAfter)
        {

            var backoff = DelayForAttempt(Attempt);

            if (RetryAfter is not TimeSpan asked || asked <= TimeSpan.Zero || MaxRetryAfter <= TimeSpan.Zero)
                return backoff;

            var honoured = asked > MaxRetryAfter
                               ? MaxRetryAfter
                               : asked;

            if (honoured <= backoff)
                return backoff;

            return JitterRatio > 0.0
                       ? honoured + TimeSpan.FromMilliseconds(honoured.TotalMilliseconds * JitterRatio * Random.Shared.NextDouble())
                       : honoured;

        }

        #endregion

        #region (static) RetryAfter(Value, Now)

        /// <summary>
        /// How long a Retry-After asks to be left alone (RFC 9110 &#167;10.2.3):
        /// a number of seconds, or a date - in any of the three forms an HTTP date
        /// may take (&#167;5.6.7) - counted from the given moment. Null for a value
        /// that is neither, and zero for a date already past.
        /// </summary>
        /// <param name="Value">The value of the header, if there was one.</param>
        /// <param name="Now">What time it is - best the server's own Date, where it sent one, so that a client whose clock is wrong still waits as long as it was asked to.</param>
        public static TimeSpan? RetryAfter(String?         Value,
                                           DateTimeOffset  Now)
        {

            var value = Value?.Trim();

            if (String.IsNullOrEmpty(value))
                return null;

            if (value.All(Char.IsAsciiDigit))
                return Int64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
                       seconds <= TimeSpan.MaxValue.TotalSeconds
                           ? TimeSpan.FromSeconds(seconds)
                           : TimeSpan.MaxValue;

            if (DateTimeOffset.TryParseExact(value,
                                             [
                                                 "ddd, dd MMM yyyy HH:mm:ss 'GMT'",    // IMF-fixdate
                                                 "dddd, dd-MMM-yy HH:mm:ss 'GMT'",     // obsolete RFC 850
                                                 "ddd MMM d HH:mm:ss yyyy"             // obsolete asctime()
                                             ],
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.AllowInnerWhite | DateTimeStyles.AssumeUniversal,
                                             out var date))
            {
                var wait = date - Now;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }

            return null;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"reconnect: {InitialDelay.TotalSeconds:F1}s..{MaxDelay.TotalSeconds:F1}s, x{BackoffFactor}, ±{JitterRatio:P0} jitter, {(MaxAttempts.HasValue ? $"max {MaxAttempts} attempts" : "unlimited")}, Retry-After up to {MaxRetryAfter.TotalSeconds:F0}s";

        #endregion

    }

}
