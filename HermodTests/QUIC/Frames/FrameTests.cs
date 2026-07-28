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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Frames;

[TestFixture]
public class FrameTests
{
    private static byte[] Serialize(params Frame[] frames) => FrameParser.Serialize(frames);

    // --- RFC 9001 A.3 payload: the real server-Initial content (ACK + CRYPTO) -----------------

    private const string ServerPayloadHex = """
        02000000000600405a020000560303ee fce7f7b37ba1d1632e96677825ddf739
        88cfc79825df566dc5430b9a045a1200 130100002e00330024001d00209d3c94
        0d89690b84d08a60993c144eca684d10 81287c834d5311bcf32bb9da1a002b00
        020304
        """;

    [Test]
    public void Parse_ServerInitialPayload_YieldsAckAndCrypto()
    {
        byte[] payload = Hex.Parse(ServerPayloadHex);

        FrameParseResult result = FrameParser.TryParseAll(payload, out List<Frame> frames);

        Assert.That(result, Is.EqualTo(FrameParseResult.Ok));
        Assert.That(frames.Count, Is.EqualTo(2));

        var ack = Expect.Type<AckFrame>(frames[0]);
        Assert.That(ack.LargestAcknowledged, Is.EqualTo(0UL));
        Assert.That(ack.AckDelay, Is.EqualTo(0UL));
        Expect.Single(ack.Ranges);
        Assert.That(ack.Ranges[0], Is.EqualTo(new PacketNumberRange(0, 0)));
        Assert.That(ack.Ecn, Is.Null);

        var crypto = Expect.Type<CryptoFrame>(frames[1]);
        Assert.That(crypto.Offset, Is.EqualTo(0UL));
        Assert.That(crypto.Data.Length, Is.EqualTo(90)); // 0x5a
    }

    [Test]
    public void ParseThenSerialize_ServerInitialPayload_RoundTripsExactly()
    {
        byte[] payload = Hex.Parse(ServerPayloadHex);

        Assert.That(FrameParser.TryParseAll(payload, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        byte[] reencoded = Serialize([.. frames]);

        Assert.That(Hex.ToHex(reencoded), Is.EqualTo(Hex.ToHex(payload)));
    }

    // --- Client-Initial payload: CRYPTO + PADDING ---------------------------------------------

    [Test]
    public void Parse_ClientInitialPayload_CoalescesPadding()
    {
        // CRYPTO(245 bytes) + PADDING up to 1162.
        byte[] cryptoFrame = Hex.Parse("""
            060040f1010000ed0303ebf8fa56f129 39b9584a3896472ec40bb863cfd3e868
            04fe3a47f06a2b69484c000004130113 02010000c000000010000e00000b6578
            616d706c652e636f6dff01000100000a 00080006001d00170018001000070005
            04616c706e0005000501000000000033 00260024001d00209370b2c9caa47fba
            baf4559fedba753de171fa71f50f1ce1 5d43e994ec74d748002b000302030400
            0d0010000e0403050306030203080408 050806002d00020101001c0002400100
            3900320408ffffffffffffffff050480 00ffff07048000ffff08011001048000
            75300901100f088394c8f03e51570806 048000ffff
            """);
        byte[] payload = new byte[1162];
        cryptoFrame.CopyTo(payload, 0);

        Assert.That(FrameParser.TryParseAll(payload, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        Assert.That(frames.Count, Is.EqualTo(2));
        var crypto = Expect.Type<CryptoFrame>(frames[0]);
        Assert.That(crypto.Data.Length, Is.EqualTo(241));
        var padding = Expect.Type<PaddingFrame>(frames[1]);
        Assert.That(padding.Length, Is.EqualTo(1162 - cryptoFrame.Length));

        // Round trip: exactly the same bytes.
        Assert.That(Hex.ToHex(Serialize([.. frames])), Is.EqualTo(Hex.ToHex(payload)));
    }

    // --- ACK with multiple ranges -------------------------------------------------------------

    [Test]
    public void AckFrame_MultipleRanges_RoundTrips()
    {
        var ack = new AckFrame(
            [new PacketNumberRange(100, 100), new PacketNumberRange(90, 80), new PacketNumberRange(70, 70)],
            AckDelay: 1234);

        byte[] bytes = Serialize(ack);

        Assert.That(FrameParser.TryParseAll(bytes, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var parsed = Expect.Type<AckFrame>(Expect.Single(frames));
        Assert.That(parsed.Ranges, Is.EqualTo(ack.Ranges));
        Assert.That(parsed.AckDelay, Is.EqualTo(1234UL));
    }

    [Test]
    public void AckFrame_WithEcn_RoundTrips()
    {
        var ack = new AckFrame([new PacketNumberRange(5, 0)], AckDelay: 0, Ecn: new EcnCounts(10, 0, 2));

        Assert.That(FrameParser.TryParseAll(Serialize(ack), out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var parsed = Expect.Type<AckFrame>(Expect.Single(frames));
        Assert.That(parsed.Ecn, Is.EqualTo(new EcnCounts(10, 0, 2)));
    }

    // --- CONNECTION_CLOSE / STREAM / PING ------------------------------------------------------

    [Test]
    public void ConnectionClose_Transport_RoundTrips()
    {
        var close = ConnectionCloseFrame.Transport(TransportError.ProtocolViolation, "evil frames", triggeringFrameType: 0x06);

        Assert.That(FrameParser.TryParseAll(Serialize(close), out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var parsed = Expect.Type<ConnectionCloseFrame>(Expect.Single(frames));
        Assert.That(parsed.ErrorCode, Is.EqualTo((ulong)TransportError.ProtocolViolation));
        Assert.That(parsed.IsApplicationError, Is.False);
        Assert.That(parsed.TriggeringFrameType, Is.EqualTo(0x06UL));
        Assert.That(parsed.ReasonPhrase, Is.EqualTo("evil frames"));
    }

        [TestCase(0UL, false)]
    [TestCase(1000UL, true)]
    public void StreamFrame_RoundTrips(ulong offset, bool fin)
    {
        byte[] data = [1, 2, 3, 4, 5];
        var stream = new StreamFrame(StreamId: 4, offset, data, fin);

        Assert.That(FrameParser.TryParseAll(Serialize(stream), out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var parsed = Expect.Type<StreamFrame>(Expect.Single(frames));
        Assert.That(parsed.StreamId, Is.EqualTo(4UL));
        Assert.That(parsed.Offset, Is.EqualTo(offset));
        Assert.That(parsed.Fin, Is.EqualTo(fin));
        Assert.That(parsed.Data.ToArray(), Is.EqualTo(data));
    }

    [Test]
    public void PingAndPadding_Mix_Parses()
    {
        byte[] bytes = Serialize(PingFrame.Instance, new PaddingFrame(3), PingFrame.Instance);

        Assert.That(FrameParser.TryParseAll(bytes, out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        Assert.That(frames, Has.Count.EqualTo(3));
        Assert.That(frames[0], Is.TypeOf<PingFrame>());
        Assert.That(Expect.Type<PaddingFrame>(frames[1]).Length, Is.EqualTo(3));
        Assert.That(frames[2], Is.TypeOf<PingFrame>());
    }

    // --- Error paths --------------------------------------------------------------------------

    [Test]
    public void Parse_UnknownFrameType_ReportsError()
    {
        // 0x40 as a 1-byte VarInt is type 0 (padding); use 0x1f (reserved/unknown here).
        byte[] bytes = [0x1f];
        Assert.That(FrameParser.TryParseAll(bytes, out _), Is.EqualTo(FrameParseResult.UnknownFrameType));
    }

    [Test]
    public void Parse_TruncatedCryptoFrame_ReportsEncodingError()
    {
        // CRYPTO, offset 0, length 10, but only 2 data bytes present.
        byte[] bytes = [0x06, 0x00, 0x0a, 0xaa, 0xbb];
        Assert.That(FrameParser.TryParseAll(bytes, out _), Is.EqualTo(FrameParseResult.EncodingError));
    }
}
