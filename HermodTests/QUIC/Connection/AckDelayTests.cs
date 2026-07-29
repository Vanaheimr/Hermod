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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Acknowledgment timing (RFC 9000 §13.2): the two transport parameters that govern it, the delay a
/// receiver reports, and when an ACK has to go out without waiting.
/// </summary>
[TestFixture]
public class AckDelayTests
{

    #region Transport parameters

    [Test]
    public void BothParametersSurviveTheRoundTrip()
    {
        var parameters = new TransportParameters
        {
            InitialSourceConnectionIdValue = ConnectionId.Empty,
            AckDelayExponentValue = 10,
            MaxAckDelayMs = 12,
        };

        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out TransportParameters? decoded), Is.True);
        Assert.That(decoded!.AckDelayExponentValue, Is.EqualTo(10));
        Assert.That(decoded.MaxAckDelayMs, Is.EqualTo(12));
    }

    [Test]
    public void TheDefaultsAreTheOnesTheRfcAssumes()
    {
        // §18.2: absent ack_delay_exponent ⇒ 3, absent max_ack_delay ⇒ 25 ms. Getting these wrong
        // silently misreads every ACK the peer sends.
        var parameters = new TransportParameters();
        Assert.That(parameters.AckDelayExponentValue, Is.EqualTo(3));
        Assert.That(parameters.MaxAckDelayMs, Is.EqualTo(25));
    }

    [Test]
    public void OutOfRangeValuesAreRejected()
    {
        // §18.2: "Values above 20 are invalid" for the exponent, "Values of 2^14 or greater are
        // invalid" for max_ack_delay. Both are TRANSPORT_PARAMETER_ERROR for the receiver.
        static byte[] With(ulong id, ulong value)
        {
            var parameters = new TransportParameters { InitialSourceConnectionIdValue = ConnectionId.Empty };
            if (id == 0x0a) parameters.AckDelayExponentValue = value; else parameters.MaxAckDelayMs = value;
            return parameters.Encode();
        }

        Assert.That(TransportParameters.TryDecode(With(0x0a, 21), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(With(0x0a, 20), out _), Is.True, "20 is still valid.");
        Assert.That(TransportParameters.TryDecode(With(0x0b, 1 << 14), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(With(0x0b, (1 << 14) - 1), out _), Is.True);
    }

    #endregion

    #region Reported delay (§13.2.5)

    [Test]
    public void TheReportedDelayIsScaledByTheExponent()
    {
        // §19.3: the field carries microseconds divided by 2^exponent, so the same measured delay
        // must come out smaller as the exponent grows.
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.NotEct, nowTicks: 0);

        long tenMs = TimeSpan.FromMilliseconds(10).Ticks;
        Assert.That(space.EncodeAckDelay(tenMs, exponent: 0), Is.EqualTo(10_000));
        Assert.That(space.EncodeAckDelay(tenMs, exponent: 3), Is.EqualTo(10_000 / 8));
        Assert.That(space.EncodeAckDelay(tenMs, exponent: 10), Is.EqualTo(10_000 / 1024));
    }

    [Test]
    public void TheDelayIsMeasuredFromTheLargestPacket_NotTheFirst()
    {
        // §13.2.5 measures "between the time the packet with the largest packet number is received
        // and the time an acknowledgment is sent" — an older packet arriving later must not inflate it.
        var space = new PacketNumberSpace();
        long tenMs = TimeSpan.FromMilliseconds(10).Ticks;

        space.RecordReceived(1, EcnCodepoint.NotEct, nowTicks: 0);
        space.RecordReceived(5, EcnCodepoint.NotEct, nowTicks: tenMs);   // the largest, later
        space.RecordReceived(2, EcnCodepoint.NotEct, nowTicks: 2 * tenMs); // older number, later still

        Assert.That(space.EncodeAckDelay(3 * tenMs, exponent: 0), Is.EqualTo(20_000),
                    "Measured from packet 5 at 10 ms, not from packet 1 at 0.");
    }

    [Test]
    public void ADelayIsNeverNegative()
    {
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.NotEct, nowTicks: 1000);
        Assert.That(space.EncodeAckDelay(500, exponent: 3), Is.Zero);
    }

    #endregion

    #region When an ACK cannot wait (§13.2.1)

    [Test]
    public void TwoAckElicitingPacketsAreEnough()
    {
        var space = new PacketNumberSpace();
        var maxAckDelay = TimeSpan.FromMilliseconds(25);

        space.RecordReceived(1, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(1, 0);
        Assert.That(space.IsAckDue(0, maxAckDelay, immediateSpace: false), Is.False, "One is not yet two.");

        space.RecordReceived(2, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(2, 0);
        Assert.That(space.IsAckDue(0, maxAckDelay, immediateSpace: false), Is.True);
    }

    [Test]
    public void MaxAckDelayForcesTheAckOut()
    {
        var space = new PacketNumberSpace();
        var maxAckDelay = TimeSpan.FromMilliseconds(25);

        space.RecordReceived(1, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(1, 0);

        Assert.That(space.IsAckDue(TimeSpan.FromMilliseconds(24).Ticks, maxAckDelay, false), Is.False);
        Assert.That(space.IsAckDue(TimeSpan.FromMilliseconds(25).Ticks, maxAckDelay, false), Is.True,
                    "§13.2.1 calls max_ack_delay an explicit contract.");
    }

    [Test]
    public void AGapForcesAnImmediateAck()
    {
        // §13.2.1: acknowledge without delay when a packet arrives "with a packet number larger than
        // the highest-numbered ack-eliciting packet … and there are missing packets between" — the
        // peer is looking at a hole and would otherwise wait out its loss timer.
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(1, 0);
        space.RecordReceived(5, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(5, 0);

        Assert.That(space.ImmediateAckNeeded, Is.True);
    }

    [Test]
    public void AReorderedPacketForcesAnImmediateAck()
    {
        // The other case of §13.2.1: "the received packet has a packet number less than another
        // ack-eliciting packet that has been received".
        var space = new PacketNumberSpace();
        space.RecordReceived(5, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(5, 0);
        space.RecordReceived(4, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(4, 0);

        Assert.That(space.ImmediateAckNeeded, Is.True);
    }

    [Test]
    public void InOrderPacketsDoNotForceAnything()
    {
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.NotEct, 0);
        space.OnAckElicitingReceived(1, 0);
        Assert.That(space.ImmediateAckNeeded, Is.False);
    }

    [Test]
    public void AnEcnCeMarkForcesAnImmediateAck()
    {
        // §13.2.1: CE-marked packets "SHOULD be acknowledged immediately, to reduce the peer's
        // response time to congestion events".
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.Ce, 0);
        Assert.That(space.ImmediateAckNeeded, Is.True);
    }

    [Test]
    public void ANonAckElicitingPacketNeverStartsTheClock()
    {
        // §13.2.1: "An endpoint MUST NOT send a non-ack-eliciting packet in response to a
        // non-ack-eliciting packet … This avoids an infinite feedback loop of acknowledgments."
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.NotEct, 0);   // recorded, but nothing ack-eliciting in it

        Assert.That(space.AckPending, Is.True, "It still has to be acknowledged eventually.");
        Assert.That(space.IsAckDue(TimeSpan.FromSeconds(10).Ticks, TimeSpan.FromMilliseconds(25), false), Is.False,
                    "But no timer starts for it.");
    }

    [Test]
    public void BuildingTheAckClearsTheState()
    {
        var space = new PacketNumberSpace();
        space.RecordReceived(1, EcnCodepoint.Ce, 0);
        space.OnAckElicitingReceived(1, 0);
        space.OnAckElicitingReceived(2, 0);

        Assert.That(space.BuildAck(42), Is.Not.Null);
        Assert.That(space.AckPending, Is.False);
        Assert.That(space.ImmediateAckNeeded, Is.False);
        Assert.That(space.AckElicitingSinceLastAck, Is.Zero);
    }

    #endregion

}
