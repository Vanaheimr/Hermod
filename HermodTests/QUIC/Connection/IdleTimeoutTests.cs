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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Deterministic tests of the idle timeout (RFC 9000 §10.1) with injected points in time.
/// </summary>
[TestFixture]
public class IdleTimeoutTests
{
    private static long Ms(double ms) => TimeSpan.FromMilliseconds(ms).Ticks;
    private static readonly TimeSpan TinyPto = TimeSpan.FromMilliseconds(1); // 3·PTO = 3 ms, below all limits here

    [Test]
    public void Negotiate_TakesMinimumOfNonZeroValues()
    {
        var idle = new IdleTimeout();

        idle.Negotiate(30_000, 10_000);
        Assert.That(idle.Negotiated, Is.EqualTo(TimeSpan.FromMilliseconds(10_000)));

        idle.Negotiate(0, 5_000);
        Assert.That(idle.Negotiated, Is.EqualTo(TimeSpan.FromMilliseconds(5_000))); // 0 = disabled at this peer

        idle.Negotiate(7_000, 0);
        Assert.That(idle.Negotiated, Is.EqualTo(TimeSpan.FromMilliseconds(7_000)));
    }

    [Test]
    public void Negotiate_BothZero_DisablesTimeout()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(0, 0);

        Assert.That(idle.Enabled, Is.False);
        idle.Start(0);
        Assert.That(idle.IsExpired(Ms(1_000_000), TinyPto), Is.False); // disabled ⇒ never expired
    }

    [Test]
    public void IsExpired_OnlyAfterNegotiatedDurationElapses()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(100, 0); // 100 ms
        idle.Start(0);

        Assert.That(idle.IsExpired(Ms(50), TinyPto), Is.False);
        Assert.That(idle.IsExpired(Ms(100), TinyPto), Is.False); // exactly equal is not yet "beyond"
        Assert.That(idle.IsExpired(Ms(150), TinyPto), Is.True);
    }

    [Test]
    public void IsExpired_UsesThreePtoFloor_WhenLargerThanNegotiated()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(10, 0);                       // only 10 ms negotiated
        idle.Start(0);
        var pto = TimeSpan.FromMilliseconds(20);      // 3·PTO = 60 ms > 10 ms ⇒ limit 60 ms

        Assert.That(idle.IsExpired(Ms(50), pto), Is.False);
        Assert.That(idle.IsExpired(Ms(70), pto), Is.True);
    }

    [Test]
    public void OnPacketReceived_RestartsTimer()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(100, 0);
        idle.Start(0);

        idle.OnPacketReceived(Ms(90)); // restart the timer at t=90 ms
        Assert.That(idle.IsExpired(Ms(150), TinyPto), Is.False); // 150 − 90 = 60 ms < 100 ms
        Assert.That(idle.IsExpired(Ms(200), TinyPto), Is.True);  // 200 − 90 = 110 ms > 100 ms
    }

    [Test]
    public void ShouldSendKeepAlive_OnlyAfterIntervalOfInactivity_AndResetsOnActivity()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(1_000, 0); // idle timeout active
        idle.Start(0);
        var interval = TimeSpan.FromMilliseconds(100);

        Assert.That(idle.ShouldSendKeepAlive(Ms(50), interval), Is.False);
        Assert.That(idle.ShouldSendKeepAlive(Ms(120), interval), Is.True);

        idle.OnPacketReceived(Ms(120)); // activity resets the counter
        Assert.That(idle.ShouldSendKeepAlive(Ms(150), interval), Is.False);
        Assert.That(idle.ShouldSendKeepAlive(Ms(230), interval), Is.True);
    }

    [Test]
    public void ShouldSendKeepAlive_False_WhenIdleTimeoutDisabled()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(0, 0); // disabled
        idle.Start(0);
        Assert.That(idle.ShouldSendKeepAlive(Ms(1_000_000), TimeSpan.FromMilliseconds(1)), Is.False);
    }

    [Test]
    public void OnAckElicitingSent_RestartsTimer_OnlyOnceUntilNextReceive()
    {
        var idle = new IdleTimeout();
        idle.Negotiate(100, 0);
        idle.Start(0);

        idle.OnAckElicitingPacketSent(Ms(50)); // first ack-eliciting since reception ⇒ reset to 50 ms
        idle.OnAckElicitingPacketSent(Ms(90)); // another one without an intervening reception ⇒ NO reset

        Assert.That(idle.IsExpired(Ms(160), TinyPto), Is.True); // 160 − 50 = 110 ms > 100 ms (base stayed at 50 ms)

        idle.OnPacketReceived(Ms(160));         // reception allows a send reset again
        idle.OnAckElicitingPacketSent(Ms(200)); // now the reset takes effect again ⇒ base 200 ms
        Assert.That(idle.IsExpired(Ms(260), TinyPto), Is.False); // 260 − 200 = 60 ms < 100 ms
    }
}
