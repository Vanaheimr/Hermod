/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Tests for the WebSocket client auto-reconnect backoff/jitter policy.
    /// </summary>
    [TestFixture]
    public class WebSocketClientReconnectPolicyTests
    {

        #region ExponentialBackoff_NoJitter_Test()

        [Test]
        public void ExponentialBackoff_NoJitter_Test()
        {

            var policy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(1),
                             MaxDelay:       TimeSpan.FromSeconds(30),
                             BackoffFactor:  2.0,
                             JitterRatio:    0.0
                         );

            // 1 * 2^0, 2^1, 2^2, 2^3 = 1s, 2s, 4s, 8s, 16s, then capped at 30s.
            Assert.Multiple(() => {
                Assert.That(policy.DelayForAttempt(1).TotalSeconds, Is.EqualTo( 1.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(2).TotalSeconds, Is.EqualTo( 2.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(3).TotalSeconds, Is.EqualTo( 4.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(4).TotalSeconds, Is.EqualTo( 8.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(5).TotalSeconds, Is.EqualTo(16.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(6).TotalSeconds, Is.EqualTo(30.0).Within(0.001)); // 32 -> capped
                Assert.That(policy.DelayForAttempt(7).TotalSeconds, Is.EqualTo(30.0).Within(0.001));
            });

        }

        #endregion

        #region Jitter_StaysWithinBounds_Test()

        [Test]
        public void Jitter_StaysWithinBounds_Test()
        {

            var policy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(1),
                             MaxDelay:       TimeSpan.FromSeconds(30),
                             BackoffFactor:  2.0,
                             JitterRatio:    0.2
                         );

            // Attempt 3 (pre-jitter 4s) must stay within +-20% => [3.2s, 4.8s].
            for (var i = 0; i < 1000; i++)
            {
                var delay = policy.DelayForAttempt(3).TotalSeconds;
                Assert.That(delay, Is.GreaterThanOrEqualTo(3.2 - 0.001));
                Assert.That(delay, Is.LessThanOrEqualTo(   4.8 + 0.001));
            }

        }

        #endregion

        #region Jitter_NeverExceedsMaxDelay_Test()

        [Test]
        public void Jitter_NeverExceedsMaxDelay_Test()
        {

            var policy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(1),
                             MaxDelay:       TimeSpan.FromSeconds(10),
                             BackoffFactor:  2.0,
                             JitterRatio:    0.5
                         );

            // Well beyond the cap: the jittered delay must never exceed MaxDelay and never go negative.
            for (var i = 0; i < 1000; i++)
            {
                var delay = policy.DelayForAttempt(20).TotalSeconds;
                Assert.That(delay, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(delay, Is.LessThanOrEqualTo(10.0 + 0.001));
            }

        }

        #endregion

        #region ConstantBackoff_Factor1_Test()

        [Test]
        public void ConstantBackoff_Factor1_Test()
        {

            var policy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(5),
                             MaxDelay:       TimeSpan.FromSeconds(30),
                             BackoffFactor:  1.0,
                             JitterRatio:    0.0
                         );

            Assert.Multiple(() => {
                Assert.That(policy.DelayForAttempt(1).TotalSeconds, Is.EqualTo(5.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(5).TotalSeconds, Is.EqualTo(5.0).Within(0.001));
                Assert.That(policy.DelayForAttempt(50).TotalSeconds, Is.EqualTo(5.0).Within(0.001));
            });

        }

        #endregion

        #region Defaults_And_Clamping_Test()

        [Test]
        public void Defaults_And_Clamping_Test()
        {

            var defaults = new WebSocketClientReconnectPolicy();
            Assert.Multiple(() => {
                Assert.That(defaults.InitialDelay,  Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(defaults.MaxDelay,      Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(defaults.BackoffFactor, Is.EqualTo(2.0));
                Assert.That(defaults.JitterRatio,   Is.EqualTo(0.2));
                Assert.That(defaults.MaxAttempts,   Is.Null);
            });

            // Out-of-range inputs are clamped: BackoffFactor >= 1, JitterRatio in [0,1],
            // and MaxDelay is never below InitialDelay.
            var clamped = new WebSocketClientReconnectPolicy(
                              InitialDelay:   TimeSpan.FromSeconds(10),
                              MaxDelay:       TimeSpan.FromSeconds(1),
                              BackoffFactor:  0.1,
                              JitterRatio:    5.0
                          );
            Assert.Multiple(() => {
                Assert.That(clamped.BackoffFactor, Is.EqualTo(1.0));
                Assert.That(clamped.JitterRatio,   Is.EqualTo(1.0));
                Assert.That(clamped.MaxDelay,      Is.EqualTo(TimeSpan.FromSeconds(10)));
            });

        }

        #endregion

    }

}
