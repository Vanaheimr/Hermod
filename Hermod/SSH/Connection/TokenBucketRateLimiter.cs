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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A token-bucket rate limiter for pacing byte throughput (e.g. SFTP upload/download bandwidth caps).
    /// Tokens accrue at a fixed bytes-per-second rate up to a burst capacity; reserving more than is
    /// available returns the time the caller must wait. All timing goes through a <see cref="TimeProvider"/>,
    /// so the pacing is fully deterministic under a fake clock.
    /// </summary>
    public sealed class TokenBucketRateLimiter
    {

        #region Data

        private readonly TimeProvider    timeProvider;
        private readonly Double          ratePerSecond;
        private readonly Double          capacity;
        private readonly Lock            gate = new ();

        private Double                   tokens;
        private DateTimeOffset           last;

        #endregion

        #region Properties

        /// <summary>The sustained rate in bytes per second (0 means unlimited).</summary>
        public Double BytesPerSecond => ratePerSecond;

        /// <summary>The burst capacity in bytes.</summary>
        public Double BurstBytes     => capacity;

        #endregion

        #region Constructor(s)

        /// <summary>Create a rate limiter.</summary>
        /// <param name="BytesPerSecond">The sustained rate; 0 or negative means unlimited (never waits).</param>
        /// <param name="TimeProvider">The clock; defaults to <see cref="TimeProvider.System"/>.</param>
        /// <param name="BurstBytes">The burst capacity; defaults to one second's worth of the rate.</param>
        public TokenBucketRateLimiter(Double BytesPerSecond, TimeProvider? TimeProvider = null, Double? BurstBytes = null)
        {
            this.timeProvider   = TimeProvider ?? System.TimeProvider.System;
            this.ratePerSecond  = BytesPerSecond;
            this.capacity       = BurstBytes ?? Math.Max(1, BytesPerSecond);
            this.tokens         = this.capacity;
            this.last           = this.timeProvider.GetUtcNow();
        }

        #endregion


        #region Reserve(Bytes)

        /// <summary>
        /// Account for <paramref name="Bytes"/> against the bucket and return how long the caller must wait
        /// before proceeding (<see cref="TimeSpan.Zero"/> when tokens were available). The reservation is
        /// applied even when it drives the bucket negative, so subsequent reservations are correctly paced.
        /// </summary>
        public TimeSpan Reserve(Int64 Bytes)
        {

            if (ratePerSecond <= 0)
                return TimeSpan.Zero;   // unlimited

            lock (gate)
            {

                var now      = timeProvider.GetUtcNow();
                var elapsed  = (now - last).TotalSeconds;
                if (elapsed > 0)
                {
                    tokens = Math.Min(capacity, tokens + elapsed * ratePerSecond);
                    last   = now;
                }

                tokens -= Bytes;

                return tokens >= 0
                           ? TimeSpan.Zero
                           : TimeSpan.FromSeconds(-tokens / ratePerSecond);

            }

        }

        #endregion

        #region ThrottleAsync(Bytes, CancellationToken)

        /// <summary>Reserve capacity for <paramref name="Bytes"/> and asynchronously wait out any required delay.</summary>
        public async ValueTask ThrottleAsync(Int64 Bytes, CancellationToken CancellationToken = default)
        {
            var wait = Reserve(Bytes);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, timeProvider, CancellationToken).ConfigureAwait(false);
        }

        #endregion

    }

}
