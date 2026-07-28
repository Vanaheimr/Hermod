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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Buffers;

[TestFixture]
public class VarIntTests
{
    // Test vectors from RFC 9000, Appendix A.1 ("Sample Variable-Length Integer Decoding").
    // Format: hex wire bytes -> expected decimal value.
        [TestCase("c2197c5eff14e88c", 151288809941952652UL)] // 8-byte
    [TestCase("9d7f3e7d", 494878333UL)]                    // 4-byte
    [TestCase("7bbd", 15293UL)]                             // 2-byte
    [TestCase("25", 37UL)]                                  // 1-byte
    [TestCase("4025", 37UL)]                                // 2-byte, but value 37 (non-minimal encoding)
    public void Decode_RfcSampleVectors(string hex, ulong expected)
    {
        byte[] bytes = Convert.FromHexString(hex);

        bool ok = VarInt.TryRead(bytes, out ulong value, out int read);

        Assert.That(ok, Is.True);
        Assert.That(value, Is.EqualTo(expected));
        Assert.That(read, Is.EqualTo(bytes.Length));
    }

        [TestCase(0UL, 1)]
    [TestCase(63UL, 1)]
    [TestCase(64UL, 2)]
    [TestCase(16383UL, 2)]
    [TestCase(16384UL, 4)]
    [TestCase(1073741823UL, 4)]
    [TestCase(1073741824UL, 8)]
    [TestCase(VarInt.MaxValue, 8)]
    public void GetLength_PicksSmallestEncoding(ulong value, int expectedLength)
    {
        Assert.That(VarInt.GetLength(value), Is.EqualTo(expectedLength));
    }

        [TestCase(0UL)]
    [TestCase(63UL)]
    [TestCase(64UL)]
    [TestCase(16383UL)]
    [TestCase(16384UL)]
    [TestCase(1073741823UL)]
    [TestCase(1073741824UL)]
    [TestCase(151288809941952652UL)]
    [TestCase(VarInt.MaxValue)]
    public void RoundTrip_WriteThenRead(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];

        int written = VarInt.Write(buffer, value);
        bool ok = VarInt.TryRead(buffer[..written], out ulong readBack, out int read);

        Assert.That(ok, Is.True);
        Assert.That(readBack, Is.EqualTo(value));
        Assert.That(read, Is.EqualTo(written));
        Assert.That(written, Is.EqualTo(VarInt.GetLength(value)));
    }

    [Test]
    public void Write_ProducesMinimalEncoding_ForValue37()
    {
        Span<byte> buffer = stackalloc byte[8];
        int written = VarInt.Write(buffer, 37);

        Assert.That(written, Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(0x25));
    }

    [Test]
    public void TryRead_ReturnsFalse_WhenTruncated()
    {
        // The first byte announces 4 bytes, but only one is present.
        byte[] truncated = [0x9d];

        Assert.That(VarInt.TryRead(truncated, out _, out _), Is.False);
    }

    [Test]
    public void TryRead_ReturnsFalse_OnEmptyInput()
    {
        Assert.That(VarInt.TryRead(ReadOnlySpan<byte>.Empty, out _, out _), Is.False);
    }

    [Test]
    public void Write_Throws_WhenValueExceedsMax()
    {
        byte[] buffer = new byte[8];
        Assert.Throws<ArgumentOutOfRangeException>(() => VarInt.Write(buffer, VarInt.MaxValue + 1));
    }

    [Test]
    public void Write_Throws_WhenDestinationTooSmall()
    {
        byte[] tooSmall = new byte[1];
        Assert.Throws<ArgumentException>(() => VarInt.Write(tooSmall, 16384));
    }

        [TestCase(0x00, 1)]
    [TestCase(0x40, 2)]
    [TestCase(0x80, 4)]
    [TestCase(0xC0, 8)]
    public void GetLengthFromFirstByte_ReadsPrefixBits(byte first, int expected)
    {
        Assert.That(VarInt.GetLengthFromFirstByte(first), Is.EqualTo(expected));
    }
}
