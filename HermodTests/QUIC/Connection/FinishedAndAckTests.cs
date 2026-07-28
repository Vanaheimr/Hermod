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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

[TestFixture]
public class FinishedAndAckTests
{
    [Test]
    public void FinishedKey_FromServerHsSecret_MatchesRfc8448()
    {
        // RFC 8448 §3: finished_key from the server handshake traffic secret.
        byte[] sHsSecret = Hex.Parse("b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38");
        byte[] finishedKey = new KeySchedule(CipherSuite.Aes128GcmSha256).FinishedKey(sHsSecret);

        Assert.That(Hex.ToHex(finishedKey), Is.EqualTo("008d3b66f816ea559f96b537e885c31fc068bf492c652f01f288a1d8cdc19fc8"));
    }

    [Test]
    public void Finished_BuildMessage_HasCorrectHeader()
    {
        byte[] verifyData = new byte[32];
        Array.Fill(verifyData, (byte)0xAB);

        byte[] msg = Finished.BuildMessage(verifyData);

        Assert.That(msg[0], Is.EqualTo((byte)HandshakeType.Finished));
        Assert.That(msg[1..4], Is.EqualTo(new byte[] { 0x00, 0x00, 0x20 })); // 3-byte length = 32
        Assert.That(msg[4..], Is.EqualTo(verifyData));
    }

    [Test]
    public void AckFrame_FromPacketNumbers_CoalescesConsecutive()
    {
        // {0,1,2, 5, 7,8} -> ranges [8..7], [5..5], [2..0]
        var ack = AckFrame.FromPacketNumbers([2, 0, 1, 8, 5, 7]);

        Assert.That(ack.LargestAcknowledged, Is.EqualTo(8UL));
        Assert.That(ack.Ranges, Is.EqualTo(new[] { new PacketNumberRange(8, 7), new PacketNumberRange(5, 5), new PacketNumberRange(2, 0) }));
    }

    [Test]
    public void AckFrame_FromPacketNumbers_SingleRange_RoundTripsThroughWire()
    {
        var ack = AckFrame.FromPacketNumbers([0, 1, 2, 3]);
        byte[] bytes = FrameParser.Serialize([ack]);

        Assert.That(FrameParser.TryParseAll(bytes, out var frames), Is.EqualTo(FrameParseResult.Ok));
        var parsed = Expect.Type<AckFrame>(Expect.Single(frames));
        Assert.That(parsed.Ranges, Is.EqualTo(new[] { new PacketNumberRange(3, 0) }));
    }
}
