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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Streams;

[TestFixture]
public class StreamIdTests
{
        [TestCase(0UL, true, true)]   // client, bidi
    [TestCase(1UL, false, true)]  // server, bidi
    [TestCase(2UL, true, false)]  // client, uni
    [TestCase(3UL, false, false)] // server, uni
    public void Bits_EncodeInitiatorAndDirection(ulong value, bool clientInitiated, bool bidirectional)
    {
        var id = new StreamId(value);
        Assert.That(id.IsClientInitiated, Is.EqualTo(clientInitiated));
        Assert.That(id.IsServerInitiated, Is.EqualTo(!clientInitiated));
        Assert.That(id.IsBidirectional, Is.EqualTo(bidirectional));
        Assert.That(id.IsUnidirectional, Is.EqualTo(!bidirectional));
    }

    [Test]
    public void Create_ProducesExpectedStreamIds()
    {
        Assert.That(StreamId.Create(true, true, 0).Value, Is.EqualTo(0UL));   // first client bidi
        Assert.That(StreamId.Create(true, true, 1).Value, Is.EqualTo(4UL));   // second client bidi
        Assert.That(StreamId.Create(true, false, 0).Value, Is.EqualTo(2UL));  // first client uni (control)
        Assert.That(StreamId.Create(false, false, 0).Value, Is.EqualTo(3UL)); // first server uni
    }
}

public class StreamReceiveBufferTests
{
    [Test]
    public void ReassemblesOutOfOrder_AndTracksFin()
    {
        var buf = new StreamReceiveBuffer();
        Assert.That(buf.Receive(3, [0x33, 0x44], fin: true), Is.EqualTo(StreamReceiveResult.Ok));  // end first
        Assert.That(buf.ReadAvailable(), Is.Empty);                                              // gap -> nothing readable
        Assert.That(buf.Receive(0, [0x00, 0x11, 0x22], false), Is.EqualTo(StreamReceiveResult.Ok)); // close the gap

        Assert.That(buf.ReadAvailable(), Is.EqualTo(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 }));
        Assert.That(buf.FinReceived, Is.True);
        Assert.That(buf.IsComplete, Is.True);
    }

    [Test]
    public void OverlappingAndDuplicateData_HandledIdempotently()
    {
        var buf = new StreamReceiveBuffer();
        buf.Receive(0, [1, 2, 3], false);
        buf.Receive(2, [3, 4, 5], false); // overlaps at offset 2
        Assert.That(buf.ReadAvailable(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void ExceedingFlowControlLimit_ReportsError()
    {
        var buf = new StreamReceiveBuffer { MaxData = 4 };
        Assert.That(buf.Receive(2, [1, 2, 3], false), Is.EqualTo(StreamReceiveResult.FlowControlError)); // end at 5 > 4
    }

    [Test]
    public void ConflictingFinalSize_ReportsError()
    {
        var buf = new StreamReceiveBuffer();
        buf.Receive(0, [1, 2, 3], fin: true);          // final size = 3
        Assert.That(buf.Receive(3, [4, 5], fin: true), Is.EqualTo(StreamReceiveResult.FinalSizeError)); // final size = 5
    }
}

public class StreamSendBufferTests
{
    [Test]
    public void EmitsFramesWithinFlowControlWindow()
    {
        var send = new StreamSendBuffer(0) { MaxData = 3 };
        send.Write([1, 2, 3, 4, 5]);

        StreamFrame? f1 = send.NextFrame(maxPayload: 10);
        Assert.That(f1, Is.Not.Null);
        Assert.That(f1!.Data.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 })); // limited by MaxData=3
        Assert.That(f1.Offset, Is.EqualTo(0UL));
        Assert.That(send.IsBlocked, Is.True);                              // the rest waits for credit

        send.MaxData = 5; // MAX_STREAM_DATA raises the window
        StreamFrame? f2 = send.NextFrame(10);
        Assert.That(f2!.Data.ToArray(), Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(f2.Offset, Is.EqualTo(3UL));
    }

    [Test]
    public void RespectsMaxPayload_AndSendsFin()
    {
        var send = new StreamSendBuffer(4) { MaxData = 1000 };
        send.Write([1, 2, 3, 4]);
        send.Finish();

        StreamFrame? f1 = send.NextFrame(maxPayload: 2);
        Assert.That(f1!.Data.ToArray(), Is.EqualTo(new byte[] { 1, 2 }));
        Assert.That(f1.Fin, Is.False);

        StreamFrame? f2 = send.NextFrame(maxPayload: 100);
        Assert.That(f2!.Data.ToArray(), Is.EqualTo(new byte[] { 3, 4 }));
        Assert.That(f2.Fin, Is.True); // the last frame carries the FIN
        Assert.That(send.NextFrame(100), Is.Null);
    }
}

public class FlowControlFrameTests
{
    private static T RoundTrip<T>(T frame) where T : Frame
    {
        byte[] bytes = FrameParser.Serialize([frame]);
        Assert.That(FrameParser.TryParseAll(bytes, out var frames), Is.EqualTo(FrameParseResult.Ok));
        return Expect.Type<T>(Expect.Single(frames));
    }

    [Test]
    public void MaxData_RoundTrips() => Assert.That(RoundTrip(new MaxDataFrame(1_000_000)).MaximumData, Is.EqualTo(1_000_000UL));

    [Test]
    public void MaxStreamData_RoundTrips()
    {
        var f = RoundTrip(new MaxStreamDataFrame(4, 65536));
        Assert.That(f.StreamId, Is.EqualTo(4UL));
        Assert.That(f.MaximumStreamData, Is.EqualTo(65536UL));
    }

    [Test]
    public void MaxStreams_RoundTrips_BothDirections()
    {
        Assert.That(RoundTrip(new MaxStreamsFrame(true, 100)).Bidirectional, Is.True);
        Assert.That(RoundTrip(new MaxStreamsFrame(false, 3)).Bidirectional, Is.False);
    }

    [Test]
    public void ResetStreamAndStopSending_RoundTrip()
    {
        var reset = RoundTrip(new ResetStreamFrame(4, 0x10c, 999));
        Assert.That(reset.FinalSize, Is.EqualTo(999UL));
        var stop = RoundTrip(new StopSendingFrame(8, 0x10c));
        Assert.That(stop.StreamId, Is.EqualTo(8UL));
    }
}
