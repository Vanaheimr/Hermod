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


        #region RetryAfter_InSeconds_Test()

        [Test]
        public void RetryAfter_InSeconds_Test()
        {

            var now = DateTimeOffset.UtcNow;

            Assert.Multiple(() => {
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("120",  now), Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter(" 7 ",  now), Is.EqualTo(TimeSpan.FromSeconds(7)));
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("0",    now), Is.EqualTo(TimeSpan.Zero));
            });

        }

        #endregion

        #region RetryAfter_AsADate_InAllThreeForms_Test()

        /// <summary>
        /// RFC 9110, section 5.6.7: a recipient takes all three forms of an HTTP date -
        /// the one that is sent now, and the two that older servers still send.
        /// </summary>
        [Test]
        public void RetryAfter_AsADate_InAllThreeForms_Test()
        {

            var now = new DateTimeOffset(1994, 11, 6, 8, 49, 0, TimeSpan.Zero);

            Assert.Multiple(() => {
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("Sun, 06 Nov 1994 08:49:37 GMT",   now), Is.EqualTo(TimeSpan.FromSeconds(37)), "IMF-fixdate");
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("Sunday, 06-Nov-94 08:49:37 GMT",  now), Is.EqualTo(TimeSpan.FromSeconds(37)), "RFC 850");
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("Sun Nov  6 08:49:37 1994",        now), Is.EqualTo(TimeSpan.FromSeconds(37)), "asctime");
            });

        }

        #endregion

        #region RetryAfter_InThePast_IsNothingToWaitFor_Test()

        [Test]
        public void RetryAfter_InThePast_IsNothingToWaitFor_Test()
        {

            Assert.That(WebSocketClientReconnectPolicy.RetryAfter("Sun, 06 Nov 1994 08:49:37 GMT",
                                                                  new DateTimeOffset(1994, 11, 6, 9, 0, 0, TimeSpan.Zero)),
                        Is.EqualTo(TimeSpan.Zero));

        }

        #endregion

        #region RetryAfter_Nonsense_IsNoRetryAfter_Test()

        [Test]
        public void RetryAfter_Nonsense_IsNoRetryAfter_Test()
        {

            var now = DateTimeOffset.UtcNow;

            Assert.Multiple(() => {
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter(null,                now), Is.Null);
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("",                  now), Is.Null);
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("soon",              now), Is.Null);
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("-5",                now), Is.Null);
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("1.5",               now), Is.Null);
                Assert.That(WebSocketClientReconnectPolicy.RetryAfter("Sun, 06 Nov 1994",  now), Is.Null);
            });

        }

        #endregion

        #region DelayForAttempt_WaitsForRetryAfter_Test()

        /// <summary>
        /// The later of the backoff and the Retry-After, and nothing asked for
        /// when nothing was said.
        /// </summary>
        [Test]
        public void DelayForAttempt_WaitsForRetryAfter_Test()
        {

            var policy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(1),
                             JitterRatio:    0.0
                         );

            Assert.Multiple(() => {
                Assert.That(policy.DelayForAttempt(1, TimeSpan.FromSeconds(10)),          Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(policy.DelayForAttempt(1, TimeSpan.FromMilliseconds(500)),    Is.EqualTo(TimeSpan.FromSeconds(1)),
                            "A Retry-After shorter than the backoff hurried the client.");
                Assert.That(policy.DelayForAttempt(1, null),                              Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(policy.DelayForAttempt(3, TimeSpan.Zero),                     Is.EqualTo(TimeSpan.FromSeconds(4)));
            });

        }

        #endregion

        #region DelayForAttempt_RetryAfter_IsCapped_Test()

        [Test]
        public void DelayForAttempt_RetryAfter_IsCapped_Test()
        {

            var capped   = new WebSocketClientReconnectPolicy(JitterRatio: 0.0, MaxRetryAfter: TimeSpan.FromSeconds(60));
            var ignoring = new WebSocketClientReconnectPolicy(JitterRatio: 0.0, MaxRetryAfter: TimeSpan.Zero);

            Assert.Multiple(() => {
                Assert.That(new WebSocketClientReconnectPolicy().MaxRetryAfter,  Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(capped.  DelayForAttempt(1, TimeSpan.FromHours(1)),  Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(ignoring.DelayForAttempt(1, TimeSpan.FromHours(1)),  Is.EqualTo(TimeSpan.FromSeconds(1)),
                            "A policy that allows no Retry-After waited for one.");
            });

        }

        #endregion

        #region DelayForAttempt_RetryAfter_JitterOnlyAfterIt_Test()

        /// <summary>
        /// Never earlier than the server asked, and spread out after it: every
        /// client it turned away was told the same moment.
        /// </summary>
        [Test]
        public void DelayForAttempt_RetryAfter_JitterOnlyAfterIt_Test()
        {

            var policy  = new WebSocketClientReconnectPolicy(JitterRatio: 0.2);
            var delays  = Enumerable.Range(0, 1000).Select(_ => policy.DelayForAttempt(1, TimeSpan.FromSeconds(10)).TotalSeconds).ToArray();

            Assert.Multiple(() => {
                Assert.That(delays.Min(), Is.GreaterThanOrEqualTo(10.0));
                Assert.That(delays.Max(), Is.LessThanOrEqualTo(  12.0 + 0.001));
                Assert.That(delays.Max() - delays.Min(), Is.GreaterThan(1.0),
                            "Every client told the same moment would come back at the same moment.");
            });

        }

        #endregion

    }

}
