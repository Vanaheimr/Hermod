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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

[TestFixture]
public class PacketNumberTests
{
    // RFC 9000 Appendix A.3: largest_pn = 0xa82f30ea, truncated = 0x9b32, length = 2
    //   => full packet number 0xa82f9b32.
    [Test]
    public void Decode_RfcAppendixA3_Example()
    {
        ulong pn = PacketNumber.Decode(truncated: 0x9b32, length: 2, largestAcked: 0xa82f30ea);
        Assert.That(pn, Is.EqualTo(0xa82f9b32UL));
    }

        [TestCase(0UL, -1L, 1)]        // first packets, no ACK yet -> full range, but small
    [TestCase(127UL, -1L, 1)]
    [TestCase(128UL, -1L, 2)]
    [TestCase(2UL, -1L, 1)]
    public void EncodeLength_PicksEnoughBytes(ulong pn, long largestAcked, int expected)
    {
        Assert.That(PacketNumber.EncodeLength(pn, largestAcked), Is.EqualTo(expected));
    }

        [TestCase(0UL, 1)]
    [TestCase(2UL, 4)]
    [TestCase(0xa82f9b32UL, 4)]
    [TestCase(0x1234UL, 2)]
    public void EncodeThenDecode_RoundTrips(ulong pn, int length)
    {
        Span<byte> buf = stackalloc byte[4];
        PacketNumber.Encode(buf, pn, length);

        uint truncated = 0;
        for (int i = 0; i < length; i++)
            truncated = (truncated << 8) | buf[i];

        // largestAcked = pn - 1: the receiver expects exactly this number.
        ulong decoded = PacketNumber.Decode(truncated, length, (long)pn - 1);
        Assert.That(decoded, Is.EqualTo(pn));
    }
}
