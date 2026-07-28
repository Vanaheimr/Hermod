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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

[TestFixture]
public class InitialPacketFactoryTests
{
    [Test]
    public void BuildClientInitial_PadsToMinimumSize_AndRoundTrips()
    {
        var dcid = ConnectionId.Parse("0102030405060708");
        var scid = ConnectionId.Parse("aabbccdd");
        var secrets = InitialSecrets.DeriveV1(dcid.Span);
        using var clientProt = new PacketProtection(secrets.Client);

        // Small "ClientHello" placeholder -> must be padded to >= 1200.
        byte[] cryptoData = new byte[80];
        new Random(7).NextBytes(cryptoData);

        byte[] packet = InitialPacketFactory.BuildClientInitial(
            clientProt, 0x0000_0001, dcid, scid, token: default,
            packetNumber: 0, packetNumberLength: 4, cryptoData);

        Assert.That(packet.Length >= InitialPacketFactory.MinimumClientInitialSize, Is.True, $"Packet was only {packet.Length} bytes.");

        // Receiver side: parse -> unprotect -> frames -> recover the CRYPTO data.
        Assert.That(LongHeader.TryParse(packet, out LongHeaderPrefix? prefix), Is.True);
        Assert.That(prefix!.DestinationConnectionId, Is.EqualTo(dcid));
        Assert.That(prefix.SourceConnectionId, Is.EqualTo(scid));

        using var serverView = new PacketProtection(secrets.Client); // same direction (self-test)
        byte[] plaintext = new byte[packet.Length];
        Assert.That(serverView.UnprotectPacket(packet, prefix.PacketNumberOffset, -1, longHeader: true,
            plaintext, out ulong pn, out int len), Is.True);
        Assert.That(pn, Is.EqualTo(0UL));

        Assert.That(FrameParser.TryParseAll(plaintext.AsSpan(0, len), out List<Frame> frames), Is.EqualTo(FrameParseResult.Ok));
        var crypto = Expect.Type<CryptoFrame>(frames[0]);
        Assert.That(crypto.Data.ToArray(), Is.EqualTo(cryptoData));
        Expect.Type<PaddingFrame>(frames[1]); // the rest is PADDING
    }
}
