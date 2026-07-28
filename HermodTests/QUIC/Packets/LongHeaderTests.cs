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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

/// <summary>
/// Verifies serializing/parsing of long-header packets — coupled to the RFC 9001 vectors:
/// packets built from structured fields must match the Appendix A packets byte for byte.
/// </summary>
[TestFixture]
public class LongHeaderTests
{
    private const uint Version1 = 0x0000_0001;
    private static readonly ConnectionId Dcid = ConnectionId.Parse("8394c8f03e515708");

    private const string ClientCryptoFrameHex = """
        060040f1010000ed0303ebf8fa56f129 39b9584a3896472ec40bb863cfd3e868
        04fe3a47f06a2b69484c000004130113 02010000c000000010000e00000b6578
        616d706c652e636f6dff01000100000a 00080006001d00170018001000070005
        04616c706e0005000501000000000033 00260024001d00209370b2c9caa47fba
        baf4559fedba753de171fa71f50f1ce1 5d43e994ec74d748002b000302030400
        0d0010000e0403050306030203080408 050806002d00020101001c0002400100
        3900320408ffffffffffffffff050480 00ffff07048000ffff08011001048000
        75300901100f088394c8f03e51570806 048000ffff
        """;

    [Test]
    public void Build_ClientInitial_FromFields_MatchesRfcVector()
    {
        var s = InitialSecrets.DeriveV1(Dcid.Span);
        using var prot = new PacketProtection(s.Client);

        byte[] payload = new byte[1162];
        Hex.Parse(ClientCryptoFrameHex).CopyTo(payload, 0);

        byte[] packet = LongHeader.Build(
            prot, LongPacketType.Initial, Version1,
            destinationConnectionId: Dcid,
            sourceConnectionId: ConnectionId.Empty,
            token: default,
            packetNumber: 2, packetNumberLength: 4,
            payload);

        // Only the first 22 bytes are the (protected) header; enough to check the field encoding.
        Assert.That(Hex.ToHex(packet.AsSpan(0, 22)), Is.EqualTo("c000000001088394c8f03e5157080000449e7b9aec34"));
        Assert.That(packet.Length, Is.EqualTo(1200));
    }

    [Test]
    public void Build_ServerInitial_FromFields_MatchesRfcVector()
    {
        var s = InitialSecrets.DeriveV1(Dcid.Span);
        using var prot = new PacketProtection(s.Server);

        byte[] payload = Hex.Parse("""
            02000000000600405a020000560303ee fce7f7b37ba1d1632e96677825ddf739
            88cfc79825df566dc5430b9a045a1200 130100002e00330024001d00209d3c94
            0d89690b84d08a60993c144eca684d10 81287c834d5311bcf32bb9da1a002b00
            020304
            """);

        byte[] packet = LongHeader.Build(
            prot, LongPacketType.Initial, Version1,
            destinationConnectionId: ConnectionId.Empty,
            sourceConnectionId: ConnectionId.Parse("f067a5502a4262b5"),
            token: default,
            packetNumber: 1, packetNumberLength: 2,
            payload);

        Assert.That(Hex.ToHex(packet.AsSpan(0, 20)), Is.EqualTo("cf000000010008f067a5502a4262b500 4075c0d9".Replace(" ", "")));
    }

    [Test]
    public void TryParse_ClientInitial_RecoversFields()
    {
        var s = InitialSecrets.DeriveV1(Dcid.Span);
        using var prot = new PacketProtection(s.Client);

        byte[] payload = new byte[1162];
        Hex.Parse(ClientCryptoFrameHex).CopyTo(payload, 0);
        byte[] packet = LongHeader.Build(
            prot, LongPacketType.Initial, Version1, Dcid, ConnectionId.Empty, default, 2, 4, payload);

        bool ok = LongHeader.TryParse(packet, out LongHeaderPrefix? prefix);

        Assert.That(ok, Is.True);
        Assert.That(prefix, Is.Not.Null);
        Assert.That(prefix!.Type, Is.EqualTo(LongPacketType.Initial));
        Assert.That(prefix.Version, Is.EqualTo(Version1));
        Assert.That(prefix.DestinationConnectionId, Is.EqualTo(Dcid));
        Assert.That(prefix.SourceConnectionId, Is.EqualTo(ConnectionId.Empty));
        Assert.That(prefix.Token, Is.Empty);
        Assert.That(prefix.PacketEndOffset, Is.EqualTo(1200)); // the Initial fills the datagram exactly
    }

    [Test]
    public void BuildThenParseThenUnprotect_ClientInitial_FullRoundTrip()
    {
        var s = InitialSecrets.DeriveV1(Dcid.Span);
        using var prot = new PacketProtection(s.Client);

        byte[] payload = new byte[1162];
        Hex.Parse(ClientCryptoFrameHex).CopyTo(payload, 0);
        byte[] packet = LongHeader.Build(
            prot, LongPacketType.Initial, Version1, Dcid, ConnectionId.Empty, default, 2, 4, payload);

        // Receiver path: parse -> pnOffset -> unprotect.
        Assert.That(LongHeader.TryParse(packet, out LongHeaderPrefix? prefix), Is.True);
        byte[] plaintext = new byte[packet.Length];
        bool ok = prot.UnprotectPacket(
            packet, prefix!.PacketNumberOffset, largestAckedPacketNumber: -1, longHeader: true,
            plaintext, out ulong pn, out int len);

        Assert.That(ok, Is.True);
        Assert.That(pn, Is.EqualTo(2UL));
        Assert.That(Hex.ToHex(plaintext.AsSpan(0, len)), Is.EqualTo(Hex.ToHex(payload)));
    }

    [Test]
    public void Build_Handshake_HasNoToken_AndParsesBack()
    {
        // For test purposes Handshake uses the same Initial keys here (real HS keys come from TLS).
        var s = InitialSecrets.DeriveV1(Dcid.Span);
        using var prot = new PacketProtection(s.Client);

        byte[] payload = new byte[100];
        new Random(1).NextBytes(payload);

        byte[] packet = LongHeader.Build(
            prot, LongPacketType.Handshake, Version1,
            ConnectionId.Parse("0102030405"), ConnectionId.Parse("aabb"),
            token: default, packetNumber: 7, packetNumberLength: 1, payload);

        Assert.That(LongHeader.TryParse(packet, out LongHeaderPrefix? prefix), Is.True);
        Assert.That(prefix!.Type, Is.EqualTo(LongPacketType.Handshake));
        Assert.That(prefix.Token, Is.Empty); // Handshake packets carry no token field
        Assert.That(prefix.DestinationConnectionId, Is.EqualTo(ConnectionId.Parse("0102030405")));
        Assert.That(prefix.SourceConnectionId, Is.EqualTo(ConnectionId.Parse("aabb")));
    }

    [Test]
    public void TryParse_RejectsOversizedConnectionId()
    {
        // A DCID length of 21 (> 20) must be rejected.
        byte[] bad = Hex.Parse("c000000001" + "15" + new string('a', 42) + "00");
        Assert.That(LongHeader.TryParse(bad, out _), Is.False);
    }

    [Test]
    public void TryParse_RejectsShortHeader()
    {
        byte[] shortHeader = [0x40, 0x01, 0x02, 0x03];
        Assert.That(LongHeader.TryParse(shortHeader, out _), Is.False);
    }

    [Test]
    public void ShortHeader_LocatesPacketNumber_WithKnownCidLength()
    {
        // Short header: first byte 0x40 (form=0, fixed=1), then a 4-byte DCID, then the PN.
        byte[] packet = Hex.Parse("41" + "deadbeef" + "aabbccdd");
        bool ok = ShortHeader.TryLocatePacketNumber(packet, localConnectionIdLength: 4, out ConnectionId dcid, out int pnOffset);

        Assert.That(ok, Is.True);
        Assert.That(dcid, Is.EqualTo(ConnectionId.Parse("deadbeef")));
        Assert.That(pnOffset, Is.EqualTo(5));
    }
}
