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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M7: the token-bucket rate limiter, paced deterministically by a manual clock.</summary>
    [TestFixture]
    public class TokenBucketRateLimiterTests
    {

        #region (helper) ManualClock

        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;
            public ManualClock(DateTimeOffset Start) { now = Start; }
            public override DateTimeOffset GetUtcNow() => now;
            public void Advance(TimeSpan By) => now += By;
        }

        private static ManualClock NewClock()
            => new (new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));

        #endregion


        #region Unlimited_NeverWaits

        [Test]
        public void Unlimited_NeverWaits()
        {
            var rl = new TokenBucketRateLimiter(0, NewClock());
            Assert.That(rl.Reserve(1_000_000), Is.EqualTo(TimeSpan.Zero));
        }

        #endregion

        #region Burst_ThenPacesAtRate

        [Test]
        public void Burst_ThenPacesAtRate()
        {

            var clock = NewClock();
            var rl    = new TokenBucketRateLimiter(BytesPerSecond: 1000, TimeProvider: clock, BurstBytes: 1000);

            // The full burst is immediately available …
            Assert.That(rl.Reserve(1000), Is.EqualTo(TimeSpan.Zero));

            // … then a further 1000 bytes must wait exactly one second at 1000 B/s.
            Assert.That(rl.Reserve(1000).TotalSeconds, Is.EqualTo(1.0).Within(0.0001));

            // After a second the bucket has refilled to zero, so the next 1000 waits another second:
            // steady-state throughput is exactly the configured rate.
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(rl.Reserve(1000).TotalSeconds, Is.EqualTo(1.0).Within(0.0001));

        }

        #endregion

        #region PartialBurst_ConsumesThenDelaysTheRemainder

        [Test]
        public void PartialBurst_ConsumesThenDelaysTheRemainder()
        {

            var clock = NewClock();
            var rl    = new TokenBucketRateLimiter(1000, clock, BurstBytes: 1000);

            Assert.Multiple(() => {
                Assert.That(rl.Reserve(400), Is.EqualTo(TimeSpan.Zero));          // 600 left
                Assert.That(rl.Reserve(400), Is.EqualTo(TimeSpan.Zero));          // 200 left
                Assert.That(rl.Reserve(400).TotalSeconds, Is.EqualTo(0.2).Within(0.0001)); // 200 short → 0.2 s
            });

        }

        #endregion

        #region HalfSecondRefill_HalfTheTokens

        [Test]
        public void HalfSecondRefill_HalfTheTokens()
        {

            var clock = NewClock();
            var rl    = new TokenBucketRateLimiter(1000, clock, BurstBytes: 1000);

            rl.Reserve(1000);                        // drain the burst to zero
            clock.Advance(TimeSpan.FromSeconds(0.5)); // refill 500 bytes

            Assert.Multiple(() => {
                Assert.That(rl.Reserve(500), Is.EqualTo(TimeSpan.Zero));          // exactly the refilled amount
                Assert.That(rl.Reserve(250).TotalSeconds, Is.EqualTo(0.25).Within(0.0001));
            });

        }

        #endregion

    }

}
