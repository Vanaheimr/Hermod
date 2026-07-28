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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Buffers;

[TestFixture]
public class BufferTests
{
    [Test]
    public void WriteThenRead_RoundTripsAllPrimitives()
    {
        byte[] snapshot;
        using (var writer = new BufferWriter(16))
        {
            writer.WriteByte(0xAB);
            writer.WriteUInt16(0x1234);
            writer.WriteUInt32(0xDEADBEEF);
            writer.WriteUInt64(0x0102030405060708);
            writer.WriteVarInt(151288809941952652UL);
            writer.WriteBytes([0x11, 0x22, 0x33]);
            snapshot = writer.WrittenSpan.ToArray();
        }

        var reader = new BufferReader(snapshot);
        Assert.That(reader.ReadByte(), Is.EqualTo(0xAB));
        Assert.That(reader.ReadUInt16(), Is.EqualTo(0x1234));
        Assert.That(reader.ReadUInt32(), Is.EqualTo(0xDEADBEEF));
        Assert.That(reader.ReadUInt64(), Is.EqualTo(0x0102030405060708UL));
        Assert.That(reader.ReadVarInt(), Is.EqualTo(151288809941952652UL));
        Assert.That(reader.ReadBytes(3).ToArray(), Is.EqualTo(new byte[] { 0x11, 0x22, 0x33 }));
        Assert.That(reader.IsEmpty, Is.True);
    }

    [Test]
    public void Writer_GrowsBeyondInitialCapacity()
    {
        using var writer = new BufferWriter(4);
        for (int i = 0; i < 1000; i++)
            writer.WriteByte((byte)i);

        Assert.That(writer.Length, Is.EqualTo(1000));
        Assert.That(writer.WrittenSpan[999], Is.EqualTo(unchecked((byte)999)));
    }

    [Test]
    public void WriteRepeated_FillsPadding()
    {
        using var writer = new BufferWriter();
        writer.WriteRepeated(0x00, 1200);

        Assert.That(writer.Length, Is.EqualTo(1200));
        Assert.That(writer.WrittenSpan.ToArray().All(b => b == 0x00), Is.True);
    }

    [Test]
    public void GetSpan_AllowsBackpatching()
    {
        using var writer = new BufferWriter();
        Span<byte> lengthSlot = writer.GetSpan(2);
        writer.WriteBytes([0xAA, 0xBB, 0xCC]);

        // Backpatch the length afterwards.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(lengthSlot, 3);

        Assert.That(writer.WrittenSpan.ToArray(), Is.EqualTo([0x00, 0x03, 0xAA, 0xBB, 0xCC]));
    }

    [Test]
    public void Reader_TrackksPositionAndRemaining()
    {
        var reader = new BufferReader([0x01, 0x02, 0x03, 0x04]);

        Assert.That(reader.Remaining, Is.EqualTo(4));
        reader.ReadUInt16();
        Assert.That(reader.Position, Is.EqualTo(2));
        Assert.That(reader.Remaining, Is.EqualTo(2));
        Assert.That(reader.RemainingSpan.ToArray(), Is.EqualTo(new byte[] { 0x03, 0x04 }));
    }

    [Test]
    public void Reader_TryMethods_ReturnFalseWhenExhausted()
    {
        var reader = new BufferReader([0x01]);

        Assert.That(reader.TryReadByte(out _), Is.True);
        Assert.That(reader.TryReadByte(out _), Is.False);
        Assert.That(reader.TryReadUInt16(out _), Is.False);
    }

    [Test]
    public void Reader_ThrowsOnOverread()
    {
        // BufferReader is a ref struct and cannot be captured in a lambda (Assert.Throws).
        var reader = new BufferReader([0x01, 0x02]);
        bool threw = false;
        try
        {
            reader.ReadUInt32();
        }
        catch (EndOfBufferException)
        {
            threw = true;
        }
        Assert.That(threw, Is.True);
    }
}
