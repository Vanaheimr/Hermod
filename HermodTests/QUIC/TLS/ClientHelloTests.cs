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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

[TestFixture]
public class ClientHelloTests
{
    private static ClientHelloOptions BuildOptions(out byte[] transportParams)
    {
        using var kex = EcdheKeyExchange.Create(NamedGroup.Secp256r1);

        var tp = new TransportParameters
        {
            InitialSourceConnectionIdValue = ConnectionId.Parse("0102030405"),
        };
        transportParams = tp.Encode();

        return new ClientHelloOptions
        {
            ServerName = "cloudflare-quic.com",
            SupportedGroups = [NamedGroup.Secp256r1],
            KeyShares = [new KeyShareEntry(NamedGroup.Secp256r1, kex.PublicKey)],
            QuicTransportParameters = transportParams,
        };
    }

    [Test]
    public void Build_ClientHello_HasWellFormedStructureAndRequiredExtensions()
    {
        ClientHelloOptions options = BuildOptions(out _);
        byte[] ch = ClientHello.Build(options);

        var reader = new BufferReader(ch);
        Assert.That(reader.ReadByte(), Is.EqualTo((byte)HandshakeType.ClientHello));

        int length = (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
        Assert.That(reader.Remaining, Is.EqualTo(length)); // the 3-byte handshake length covers exactly the rest

        Assert.That(reader.ReadUInt16(), Is.EqualTo(TlsVersions.Tls12)); // legacy_version
        reader.ReadBytes(32);                                 // random
        Assert.That(reader.ReadByte(), Is.EqualTo(0));                   // empty legacy_session_id

        ushort csLen = reader.ReadUInt16();
        List<ushort> suites = ReadUInt16List(ref reader, csLen);
        Assert.That(suites, Does.Contain((ushort)CipherSuite.Aes128GcmSha256));

        Assert.That(reader.ReadByte(), Is.EqualTo(1)); // compression methods length
        Assert.That(reader.ReadByte(), Is.EqualTo(0)); // null compression

        ushort extsLen = reader.ReadUInt16();
        Assert.That(reader.Remaining, Is.EqualTo(extsLen));
        Dictionary<ushort, byte[]> exts = ReadExtensions(ref reader);

        // supported_versions contains TLS 1.3
        Assert.That(exts.ContainsKey((ushort)ExtensionType.SupportedVersions), Is.True);
        byte[] sv = exts[(ushort)ExtensionType.SupportedVersions];
        Assert.That((sv[1] << 8) | sv[2], Is.EqualTo(0x0304)); // [listLen(1)][0x03,0x04]

        // ALPN contains "h3"
        byte[] alpn = exts[(ushort)ExtensionType.Alpn];
        Assert.That(DecodeAlpn(alpn), Does.Contain("h3"));

        // key_share: secp256r1 with a 65-byte point
        byte[] ks = exts[(ushort)ExtensionType.KeyShare];
        int group = (ks[2] << 8) | ks[3];
        int keyLen = (ks[4] << 8) | ks[5];
        Assert.That(group, Is.EqualTo((int)NamedGroup.Secp256r1));
        Assert.That(keyLen, Is.EqualTo(65));
        Assert.That(ks[6], Is.EqualTo(0x04)); // uncompressed point

        // SNI contains the hostname
        byte[] sni = exts[(ushort)ExtensionType.ServerName];
        Assert.That(System.Text.Encoding.ASCII.GetString(sni), Does.Contain("cloudflare-quic.com"));

        // quic_transport_parameters present and non-empty
        Assert.That(exts.TryGetValue((ushort)ExtensionType.QuicTransportParameters, out byte[]? qtp), Is.True);
        Assert.That(qtp!, Is.Not.Empty);
    }

    [Test]
    public void Ecdhe_BothParties_DeriveSameSecret()
    {
        using var client = EcdheKeyExchange.Create(NamedGroup.Secp256r1);
        using var server = EcdheKeyExchange.Create(NamedGroup.Secp256r1);

        byte[] clientView = client.DeriveSharedSecret(server.PublicKey);
        byte[] serverView = server.DeriveSharedSecret(client.PublicKey);

        Assert.That(serverView, Is.EqualTo(clientView));
        Assert.That(clientView.Length, Is.EqualTo(32)); // P-256: X coordinate = 32 bytes
    }

    [Test]
    public void TransportParameters_RoundTrip_PreservesValues()
    {
        var tp = new TransportParameters
        {
            MaxIdleTimeoutMs = 15_000,
            InitialMaxDataValue = 500_000,
            InitialMaxStreamsBidiValue = 42,
            InitialSourceConnectionIdValue = ConnectionId.Parse("cafebabe"),
        };

        Assert.That(TransportParameters.TryDecode(tp.Encode(), out TransportParameters? decoded), Is.True);
        Assert.That(decoded!.MaxIdleTimeoutMs, Is.EqualTo(15_000UL));
        Assert.That(decoded.InitialMaxDataValue, Is.EqualTo(500_000UL));
        Assert.That(decoded.InitialMaxStreamsBidiValue, Is.EqualTo(42UL));
        Assert.That(decoded.InitialSourceConnectionIdValue, Is.EqualTo(ConnectionId.Parse("cafebabe")));
    }

    [Test]
    public void TransportParameters_Decode_IgnoresUnknownParameters()
    {
        // Known parameter (max_idle_timeout id=01, len=01, value=05)
        // + unknown ID 16383 (2-byte VarInt 7fff), len=02, value aabb -> must be ignored.
        byte[] wire = Hex.Parse("010105" + "7fff02aabb");
        Assert.That(TransportParameters.TryDecode(wire, out TransportParameters? tp), Is.True);
        Assert.That(tp!.MaxIdleTimeoutMs, Is.EqualTo(5UL));
    }

    // --- Helper functions ---------------------------------------------------------------------

    private static List<ushort> ReadUInt16List(ref BufferReader reader, int byteLength)
    {
        var list = new List<ushort>();
        for (int i = 0; i < byteLength; i += 2)
            list.Add(reader.ReadUInt16());
        return list;
    }

    private static Dictionary<ushort, byte[]> ReadExtensions(ref BufferReader reader)
    {
        var exts = new Dictionary<ushort, byte[]>();
        while (!reader.IsEmpty)
        {
            ushort type = reader.ReadUInt16();
            ushort len = reader.ReadUInt16();
            exts[type] = reader.ReadBytes(len).ToArray();
        }
        return exts;
    }

    private static List<string> DecodeAlpn(byte[] alpn)
    {
        var protocols = new List<string>();
        var reader = new BufferReader(alpn);
        reader.ReadUInt16(); // ProtocolNameList length
        while (!reader.IsEmpty)
        {
            byte len = reader.ReadByte();
            protocols.Add(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(len)));
        }
        return protocols;
    }
}
