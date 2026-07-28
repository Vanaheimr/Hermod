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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

[TestFixture]
public class PacketNumberSpaceTests
{
    [Test]
    public void NextPacketNumber_IncrementsFromZero()
    {
        var space = new PacketNumberSpace();
        Assert.That(space.NextPacketNumber(), Is.EqualTo(0UL));
        Assert.That(space.NextPacketNumber(), Is.EqualTo(1UL));
        Assert.That(space.NextPacketNumber(), Is.EqualTo(2UL));
    }

    [Test]
    public void ReceivedPackets_DriveAckPendingAndBuildAck()
    {
        var space = new PacketNumberSpace();
        Assert.That(space.AckPending, Is.False);
        Assert.That(space.BuildAck(), Is.Null);

        space.RecordReceived(0);
        space.RecordReceived(1);
        Assert.That(space.AckPending, Is.True);

        AckFrame? ack = space.BuildAck();
        Assert.That(ack, Is.Not.Null);
        Assert.That(ack!.LargestAcknowledged, Is.EqualTo(1UL));
        Assert.That(space.AckPending, Is.False); // acknowledged after building
        Assert.That(space.LargestReceived, Is.EqualTo(1));
    }

    [Test]
    public void OnAckReceived_TracksLargestAckedByPeer()
    {
        var space = new PacketNumberSpace();
        Assert.That(space.LargestAckedByPeer, Is.EqualTo(-1));

        space.OnAckReceived(5);
        Assert.That(space.LargestAckedByPeer, Is.EqualTo(5));
        space.OnAckReceived(3); // a smaller value changes nothing
        Assert.That(space.LargestAckedByPeer, Is.EqualTo(5));
    }
}

public class TlsClientHandshakeTests
{
    [Test]
    public void Start_ProducesClientHelloAtInitialLevel()
    {
        using var tls = new TlsClientHandshake("example.com", new byte[] { 0x01, 0x01, 0x00 });
        tls.Start();

        Assert.That(tls.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data), Is.True);
        Assert.That(level, Is.EqualTo(EncryptionLevel.Initial));
        Assert.That(data[0], Is.EqualTo(0x01)); // handshake type ClientHello
        Assert.That(tls.TryGetOutgoingCrypto(out _, out _), Is.False); // only one message
    }

    [Test]
    public void ProvideServerHello_DerivesHandshakeSecrets()
    {
        // Server key share from a real P-256 key pair.
        using var serverKex = EcdheKeyExchange.Create(NamedGroup.Secp256r1);
        byte[] serverHello = BuildServerHello((ushort)CipherSuite.Aes128GcmSha256,
            (ushort)NamedGroup.Secp256r1, serverKex.PublicKey);

        using var tls = new TlsClientHandshake("example.com", new byte[] { 0x01, 0x01, 0x00 });
        tls.Start();
        tls.TryGetOutgoingCrypto(out _, out _); // fetch the ClientHello

        tls.ProvideCrypto(EncryptionLevel.Initial, serverHello);

        Assert.That(tls.NegotiatedCipherSuite, Is.EqualTo(CipherSuite.Aes128GcmSha256));
        Assert.That(tls.HandshakeSecrets, Is.Not.Null);
        Assert.That(tls.HandshakeSecrets!.ServerHandshakeTrafficSecret.Length, Is.EqualTo(32));
    }

    private static byte[] BuildServerHello(ushort cipher, ushort group, byte[] keyShare)
    {
        string keyShareExt = "0033" + (4 + keyShare.Length).ToString("x4") + group.ToString("x4")
                             + keyShare.Length.ToString("x4") + Convert.ToHexString(keyShare);
        string extensions = "002b00020304" + keyShareExt;
        string body = "0303" + new string('0', 64) + "00" + cipher.ToString("x4") + "00"
                      + (extensions.Length / 2).ToString("x4") + extensions;
        return Hex.Parse("02" + (body.Length / 2).ToString("x6") + body);
    }
}
