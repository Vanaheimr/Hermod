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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Frames;

/// <summary>
/// Parser fuzzer of the transport-error matrix: the parsers for frames, transport parameters and
/// packet headers must never throw an exception on ARBITRARY bytes — errors must come back as a
/// clean <c>false</c>/<c>EncodingError</c> (the endpoint then turns that into the matching
/// connection error). Deterministic via fixed seeds — every find is reproducible.
/// </summary>
[TestFixture]
public class ParserFuzzTests
{
    private const int RandomIterations = 4000;
    private const int MutationIterations = 2000;

    [Test]
    public void FrameParser_NeverThrows_OnRandomBytes()
    {
        var rng = new Random(20260723);
        byte[] buffer = new byte[96];
        for (int i = 0; i < RandomIterations; i++)
        {
            int length = rng.Next(0, buffer.Length + 1);
            rng.NextBytes(buffer.AsSpan(0, length));
            FrameParser.TryParseAll(buffer.AsSpan(0, length), out _); // result irrelevant — just: no throw
        }
        Assert.Pass($"{RandomIterations} random frame payloads parsed without an exception.");
    }

    [Test]
    public void FrameParser_NeverThrows_OnMutatedValidFrames()
    {
        // A valid frame sequence across all types — then deliberately flip/truncate bytes.
        byte[] valid = FrameParser.Serialize(
        [
            PingFrame.Instance,
            new MaxDataFrame(123456),
            new MaxStreamDataFrame(4, 999),
            new ResetStreamFrame(8, 0x0c, 1000),
            new ResetStreamAtFrame(8, 0x0c, 1000, 40),
            new StopSendingFrame(12, 7),
            new StreamFrame(0, 64, new byte[] { 1, 2, 3, 4, 5 }, Fin: true),
            new DatagramFrame(new byte[] { 9, 9 }),
            new StreamsBlockedFrame(true, 100),
            ConnectionCloseFrame.Transport(TransportError.ProtocolViolation, "test"),
        ]);

        var rng = new Random(9114);
        for (int i = 0; i < MutationIterations; i++)
        {
            byte[] mutated = (byte[])valid.Clone();
            int flips = rng.Next(1, 4);
            for (int f = 0; f < flips; f++)
                mutated[rng.Next(mutated.Length)] ^= (byte)(1 << rng.Next(8));
            int length = rng.Next(4) == 0 ? rng.Next(mutated.Length + 1) : mutated.Length; // occasionally truncate
            FrameParser.TryParseAll(mutated.AsSpan(0, length), out _);
        }
        Assert.Pass($"{MutationIterations} mutated frame sequences parsed without an exception.");
    }

    [Test]
    public void TransportParameters_NeverThrow_OnRandomOrMutatedBytes()
    {
        var rng = new Random(9000);
        byte[] buffer = new byte[80];
        for (int i = 0; i < RandomIterations; i++)
        {
            int length = rng.Next(0, buffer.Length + 1);
            rng.NextBytes(buffer.AsSpan(0, length));
            TransportParameters.TryDecode(buffer.AsSpan(0, length), out _);
        }

        // Mutated VALID encoding — before the length guard a >20-byte CID could throw here.
        byte[] valid = new TransportParameters
        {
            StatelessResetTokenValue = new byte[16],
            MaxDatagramFrameSizeValue = 65535,
        }.Encode();
        for (int i = 0; i < MutationIterations; i++)
        {
            byte[] mutated = (byte[])valid.Clone();
            for (int f = 0; f < 3; f++)
                mutated[rng.Next(mutated.Length)] ^= (byte)(1 << rng.Next(8));
            TransportParameters.TryDecode(mutated, out _);
        }
        Assert.Pass("Random + mutated transport parameters parsed without an exception.");
    }

    [Test]
    public void PacketHeaderParsers_NeverThrow_OnRandomDatagrams()
    {
        var rng = new Random(1200);
        byte[] buffer = new byte[64];
        for (int i = 0; i < RandomIterations; i++)
        {
            int length = rng.Next(0, buffer.Length + 1);
            rng.NextBytes(buffer.AsSpan(0, length));
            ReadOnlySpan<byte> datagram = buffer.AsSpan(0, length);

            if (!datagram.IsEmpty && PacketFormat.IsLongHeader(datagram[0]))
                LongHeader.TryParseInvariant(datagram, out _, out _, out _);
            VersionNegotiationPacket.TryParse(datagram, out _, out _, out _);
        }
        Assert.Pass($"{RandomIterations} random datagram headers parsed without an exception.");
    }
}
