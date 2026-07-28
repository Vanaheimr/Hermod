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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

[TestFixture]
public class HandshakeReassemblyTests
{
    [Test]
    public void CryptoAssembler_OutOfOrder_ProducesContiguousPrefix()
    {
        var a = new CryptoStreamAssembler();
        a.Add(4, [0x44, 0x55]);      // arrives first, but at offset 4
        a.Add(0, [0x00, 0x11, 0x22, 0x33]); // closes the gap from 0

        Assert.That(a.Contiguous(), Is.EqualTo(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }));
    }

    [Test]
    public void CryptoAssembler_StopsAtGap()
    {
        var a = new CryptoStreamAssembler();
        a.Add(0, [1, 2]);
        a.Add(5, [9, 9]); // gap at 2..5 -> prefix ends after 2 bytes

        Assert.That(a.Contiguous(), Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test]
    public void CryptoAssembler_Duplicates_AreIdempotent()
    {
        var a = new CryptoStreamAssembler();
        a.Add(0, [1, 2, 3]);
        a.Add(0, [1, 2, 3]); // retransmit of the same fragment

        Assert.That(a.Contiguous(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void CryptoAssembler_OverlappingFragment_AppendsOnlyNewBytes()
    {
        var a = new CryptoStreamAssembler();
        a.Add(0, [1, 2, 3]);
        a.Add(2, [3, 4, 5]); // overlaps at offset 2

        Assert.That(a.Contiguous(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void HandshakeMessages_ParsesMultiple_AndReportsPartialTail()
    {
        // EncryptedExtensions (type 08, length 2, body aabb) + Finished (type 14, length 1, body cc)
        // + a started third message (type 0b, length 10, but only 1 byte present).
        byte[] buffer = Hex.Parse("08000002aabb" + "14000001cc" + "0b0000 0a ff".Replace(" ", ""));

        Assert.That(HandshakeMessages.TryReadAll(buffer, out var messages, out int consumed), Is.True);
        Assert.That(messages.Count, Is.EqualTo(2));
        Assert.That(messages[0].Type, Is.EqualTo(HandshakeType.EncryptedExtensions));
        Assert.That(messages[1].Type, Is.EqualTo(HandshakeType.Finished));
        Assert.That(messages[0].Body.ToArray(), Is.EqualTo(new byte[] { 0xaa, 0xbb }));
        Assert.That(consumed, Is.EqualTo(6 + 5)); // the third, incomplete message remains unread
    }
}
