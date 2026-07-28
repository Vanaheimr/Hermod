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

using System.Security.Cryptography;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Crypto;

/// <summary>
/// Byte-exact verification of the QUIC Initial crypto against the test vectors from
/// RFC 9001, Appendix A. This is the foundation of the entire stack — if a single bit is off here,
/// every real handshake fails later.
/// </summary>
[TestFixture]
public class Rfc9001VectorTests
{
    // Destination connection ID used by both sides (RFC 9001 A).
    private static readonly byte[] Dcid = Hex.Parse("8394c8f03e515708");

    // --- A.1: key schedule ------------------------------------------------------------------

    [Test]
    public void A1_InitialSecret_Matches()
    {
        var secrets = InitialSecrets.DeriveV1(Dcid);
        Assert.That(Hex.ToHex(secrets.InitialSecret), Is.EqualTo("7db5df06e7a69e432496adedb00851923595221596ae2ae9fb8115c1e9ed0a44"));
    }

    [Test]
    public void A1_ClientKeys_Match()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        Assert.That(Hex.ToHex(s.Client.Secret), Is.EqualTo("c00cf151ca5be075ed0ebfb5c80323c42d6b7db67881289af4008f1f6c357aea"));
        Assert.That(Hex.ToHex(s.Client.Key), Is.EqualTo("1f369613dd76d5467730efcbe3b1a22d"));
        Assert.That(Hex.ToHex(s.Client.Iv), Is.EqualTo("fa044b2f42a3fd3b46fb255c"));
        Assert.That(Hex.ToHex(s.Client.HeaderProtectionKey), Is.EqualTo("9f50449e04a0e810283a1e9933adedd2"));
    }

    [Test]
    public void A1_ServerKeys_Match()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        Assert.That(Hex.ToHex(s.Server.Secret), Is.EqualTo("3c199828fd139efd216c155ad844cc81fb82fa8d7446fa7d78be803acdda951b"));
        Assert.That(Hex.ToHex(s.Server.Key), Is.EqualTo("cf3a5331653c364c88f0f379b6067e37"));
        Assert.That(Hex.ToHex(s.Server.Iv), Is.EqualTo("0ac1493ca1905853b0bba03e"));
        Assert.That(Hex.ToHex(s.Server.HeaderProtectionKey), Is.EqualTo("c206b8d9b9f0f37644430b490eeaa314"));
    }

    [Test]
    public void A1_HkdfLabel_Encoding_Matches()
    {
        // RFC 9001 A.1 shows the complete HkdfLabel encoding.
        Assert.That(Hex.ToHex(TlsHkdf.BuildHkdfLabel("client in", default, 32)), Is.EqualTo("00200f746c73313320636c69656e7420696e00"));
        Assert.That(Hex.ToHex(TlsHkdf.BuildHkdfLabel("server in", default, 32)), Is.EqualTo("00200f746c7331332073657276657220696e00"));
        Assert.That(Hex.ToHex(TlsHkdf.BuildHkdfLabel("quic key", default, 16)), Is.EqualTo("00100e746c7331332071756963206b657900"));
        Assert.That(Hex.ToHex(TlsHkdf.BuildHkdfLabel("quic iv", default, 12)), Is.EqualTo("000c0d746c733133207175696320697600"));
        Assert.That(Hex.ToHex(TlsHkdf.BuildHkdfLabel("quic hp", default, 16)), Is.EqualTo("00100d746c733133207175696320687000"));
    }

    // --- A.2: Client Initial ----------------------------------------------------------------

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
    public void A2_ClientInitial_ProtectedPacket_Matches()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        using var prot = new PacketProtection(s.Client);

        byte[] header = Hex.Parse("c300000001088394c8f03e5157080000449e00000002");
        byte[] payload = new byte[1162];
        Hex.Parse(ClientCryptoFrameHex).CopyTo(payload, 0); // the rest stays 0 => PADDING frames

        byte[] packet = prot.ProtectPacket(header, packetNumberLength: 4, packetNumber: 2, payload, longHeader: true);

        Assert.That(Hex.ToHex(packet), Is.EqualTo(ExpectedClientInitial));
    }

    [Test]
    public void A2_ClientHeaderProtectionMask_Matches()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        using var prot = new PacketProtection(s.Client);

        Span<byte> mask = stackalloc byte[5];
        prot.HeaderProtectionMask(Hex.Parse("d1b1c98dd7689fb8ec11d242b123dc9b"), mask);
        Assert.That(Hex.ToHex(mask), Is.EqualTo("437b9aec36"));
    }

    // --- A.3: Server Initial ----------------------------------------------------------------

    private const string ServerPayloadHex = """
        02000000000600405a020000560303ee fce7f7b37ba1d1632e96677825ddf739
        88cfc79825df566dc5430b9a045a1200 130100002e00330024001d00209d3c94
        0d89690b84d08a60993c144eca684d10 81287c834d5311bcf32bb9da1a002b00
        020304
        """;

    private const string ServerHeaderHex = "c1000000010008f067a5502a4262b50040750001";

    private const string ExpectedServerInitial = """
        cf000000010008f067a5502a4262b500 4075c0d95a482cd0991cd25b0aac406a
        5816b6394100f37a1c69797554780bb3 8cc5a99f5ede4cf73c3ec2493a1839b3
        dbcba3f6ea46c5b7684df3548e7ddeb9 c3bf9c73cc3f3bded74b562bfb19fb84
        022f8ef4cdd93795d77d06edbb7aaf2f 58891850abbdca3d20398c276456cbc4
        2158407dd074ee
        """;

    [Test]
    public void A3_ServerInitial_ProtectedPacket_Matches()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        using var prot = new PacketProtection(s.Server);

        byte[] header = Hex.Parse(ServerHeaderHex);
        byte[] payload = Hex.Parse(ServerPayloadHex);

        byte[] packet = prot.ProtectPacket(header, packetNumberLength: 2, packetNumber: 1, payload, longHeader: true);

        Assert.That(Hex.ToHex(packet), Is.EqualTo(Hex.ToHex(Hex.Parse(ExpectedServerInitial))));
    }

    [Test]
    public void A3_ServerHeaderProtectionMask_Matches()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        using var prot = new PacketProtection(s.Server);

        Span<byte> mask = stackalloc byte[5];
        prot.HeaderProtectionMask(Hex.Parse("2cd0991cd25b0aac406a5816b6394100"), mask);
        Assert.That(Hex.ToHex(mask), Is.EqualTo("2ec0d8356a"));
    }

    [Test]
    public void A3_ServerInitial_RoundTrip_Unprotect()
    {
        var s = InitialSecrets.DeriveV1(Dcid);
        using var prot = new PacketProtection(s.Server);

        byte[] packet = Hex.Parse(ExpectedServerInitial);
        byte[] plaintext = new byte[packet.Length];

        // pnOffset = 18 (the header is 20 bytes, 2-byte packet number).
        bool ok = prot.UnprotectPacket(
            packet, packetNumberOffset: 18, largestAckedPacketNumber: -1, longHeader: true,
            plaintext, out ulong pn, out int len);

        Assert.That(ok, Is.True);
        Assert.That(pn, Is.EqualTo(1UL));
        Assert.That(Hex.ToHex(plaintext.AsSpan(0, len)), Is.EqualTo(Hex.ToHex(Hex.Parse(ServerPayloadHex))));
    }

    // --- A.4: Retry -------------------------------------------------------------------------

    [Test]
    public void A4_RetryIntegrityTag_Matches()
    {
        // Retry packet incl. tag; the last 16 bytes are the tag.
        byte[] retry = Hex.Parse("ff000000010008f067a5502a4262b5746f6b656e04a265ba2eff4d829058fb3f0f2496ba");
        ReadOnlySpan<byte> retryWithoutTag = retry.AsSpan(0, retry.Length - 16);
        ReadOnlySpan<byte> expectedTag = retry.AsSpan(retry.Length - 16);

        byte[] tag = RetryIntegrity.ComputeTag(Dcid, retryWithoutTag);

        Assert.That(Hex.ToHex(tag), Is.EqualTo("04a265ba2eff4d829058fb3f0f2496ba"));
        Assert.That(RetryIntegrity.Verify(Dcid, retryWithoutTag, expectedTag), Is.True);
    }

    // --- Expected client Initial (1200 bytes) -----------------------------------------------

    private const string ExpectedClientInitial = "c000000001088394c8f03e5157080000" +
        "449e7b9aec34d1b1c98dd7689fb8ec11" + "d242b123dc9bd8bab936b47d92ec356c" +
        "0bab7df5976d27cd449f63300099f399" + "1c260ec4c60d17b31f8429157bb35a12" +
        "82a643a8d2262cad67500cadb8e7378c" + "8eb7539ec4d4905fed1bee1fc8aafba1" +
        "7c750e2c7ace01e6005f80fcb7df6212" + "30c83711b39343fa028cea7f7fb5ff89" +
        "eac2308249a02252155e2347b63d58c5" + "457afd84d05dfffdb20392844ae81215" +
        "4682e9cf012f9021a6f0be17ddd0c208" + "4dce25ff9b06cde535d0f920a2db1bf3" +
        "62c23e596d11a4f5a6cf3948838a3aec" + "4e15daf8500a6ef69ec4e3feb6b1d98e" +
        "610ac8b7ec3faf6ad760b7bad1db4ba3" + "485e8a94dc250ae3fdb41ed15fb6a8e5" +
        "eba0fc3dd60bc8e30c5c4287e53805db" + "059ae0648db2f64264ed5e39be2e20d8" +
        "2df566da8dd5998ccabdae053060ae6c" + "7b4378e846d29f37ed7b4ea9ec5d82e7" +
        "961b7f25a9323851f681d582363aa5f8" + "9937f5a67258bf63ad6f1a0b1d96dbd4" +
        "faddfcefc5266ba6611722395c906556" + "be52afe3f565636ad1b17d508b73d874" +
        "3eeb524be22b3dcbc2c7468d54119c74" + "68449a13d8e3b95811a198f3491de3e7" +
        "fe942b330407abf82a4ed7c1b311663a" + "c69890f4157015853d91e923037c227a" +
        "33cdd5ec281ca3f79c44546b9d90ca00" + "f064c99e3dd97911d39fe9c5d0b23a22" +
        "9a234cb36186c4819e8b9c5927726632" + "291d6a418211cc2962e20fe47feb3edf" +
        "330f2c603a9d48c0fcb5699dbfe58964" + "25c5bac4aee82e57a85aaf4e2513e4f0" +
        "5796b07ba2ee47d80506f8d2c25e50fd" + "14de71e6c418559302f939b0e1abd576" +
        "f279c4b2e0feb85c1f28ff18f58891ff" + "ef132eef2fa09346aee33c28eb130ff2" +
        "8f5b766953334113211996d20011a198" + "e3fc433f9f2541010ae17c1bf202580f" +
        "6047472fb36857fe843b19f5984009dd" + "c324044e847a4f4a0ab34f719595de37" +
        "252d6235365e9b84392b061085349d73" + "203a4a13e96f5432ec0fd4a1ee65accd" +
        "d5e3904df54c1da510b0ff20dcc0c77f" + "cb2c0e0eb605cb0504db87632cf3d8b4" +
        "dae6e705769d1de354270123cb11450e" + "fc60ac47683d7b8d0f811365565fd98c" +
        "4c8eb936bcab8d069fc33bd801b03ade" + "a2e1fbc5aa463d08ca19896d2bf59a07" +
        "1b851e6c239052172f296bfb5e724047" + "90a2181014f3b94a4e97d117b4381303" +
        "68cc39dbb2d198065ae3986547926cd2" + "162f40a29f0c3c8745c0f50fba3852e5" +
        "66d44575c29d39a03f0cda721984b6f4" + "40591f355e12d439ff150aab7613499d" +
        "bd49adabc8676eef023b15b65bfc5ca0" + "6948109f23f350db82123535eb8a7433" +
        "bdabcb909271a6ecbcb58b936a88cd4e" + "8f2e6ff5800175f113253d8fa9ca8885" +
        "c2f552e657dc603f252e1a8e308f76f0" + "be79e2fb8f5d5fbbe2e30ecadd220723" +
        "c8c0aea8078cdfcb3868263ff8f09400" + "54da48781893a7e49ad5aff4af300cd8" +
        "04a6b6279ab3ff3afb64491c85194aab" + "760d58a606654f9f4400e8b38591356f" +
        "bf6425aca26dc85244259ff2b19c41b9" + "f96f3ca9ec1dde434da7d2d392b905dd" +
        "f3d1f9af93d1af5950bd493f5aa731b4" + "056df31bd267b6b90a079831aaf579be" +
        "0a39013137aac6d404f518cfd4684064" + "7e78bfe706ca4cf5e9c5453e9f7cfd2b" +
        "8b4c8d169a44e55c88d4a9a7f9474241" + "e221af44860018ab0856972e194cd934";
}
