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

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// The server's preferred address (RFC 9000 §9.6): the wire format of transport parameter 0x0d
/// (§18.2 Figure 22), and what a real connection does with it.
/// </summary>
[TestFixture]
public class PreferredAddressTests
{

    private static readonly ConnectionId Cid = new(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    private static readonly byte[] Token = RandomNumberGenerator.GetBytes(16);

    #region Wire format

    [Test]
    public void ARoundTripPreservesEverything()
    {
        var original = new PreferredAddress(System.Net.IPAddress.Parse("198.51.100.7"), 4433,
                                            System.Net.IPAddress.Parse("2001:db8::1"), 8443,
                                            Cid, Token);

        Assert.That(PreferredAddress.TryParse(original.Encode(), out PreferredAddress? parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(original));
    }

    [Test]
    public void TheEncodingHasTheLayoutOfFigure22()
    {
        // 4 + 2 + 16 + 2 + 1 + cid + 16 — asserted on the bytes, not on a round trip, so a swapped
        // field order could not pass by cancelling itself out.
        byte[] value = PreferredAddress.ForIPv4(new IPEndPoint(System.Net.IPAddress.Parse("192.0.2.1"), 0x1234),
                                                Cid, Token).Encode();

        Assert.That(value, Has.Length.EqualTo(4 + 2 + 16 + 2 + 1 + 8 + 16));
        Assert.That(value[..4], Is.EqualTo(new byte[] { 192, 0, 2, 1 }), "IPv4 in network byte order.");
        Assert.That(value[4], Is.EqualTo(0x12));
        Assert.That(value[5], Is.EqualTo(0x34));
        Assert.That(value[6..22], Is.EqualTo(new byte[16]), "No IPv6 offered ⇒ all zeros (§18.2).");
        Assert.That(value[22], Is.Zero);
        Assert.That(value[23], Is.Zero);
        Assert.That(value[24], Is.EqualTo(8), "Connection ID length.");
        Assert.That(value[25..33], Is.EqualTo(Cid.ToArray()));
        Assert.That(value[33..49], Is.EqualTo(Token));
    }

    [Test]
    public void AFamilyTheServerDoesNotOfferComesBackAsNull()
    {
        // §18.2: "Servers MAY choose to only send a preferred address of one address family by
        // sending an all-zero address and port … for the other family." Reporting that as
        // 0.0.0.0:0 would invite a caller to migrate to nowhere.
        byte[] value = PreferredAddress.ForIPv6(new IPEndPoint(System.Net.IPAddress.Parse("2001:db8::2"), 443),
                                                Cid, Token).Encode();

        Assert.That(PreferredAddress.TryParse(value, out PreferredAddress? parsed), Is.True);
        Assert.That(parsed!.IPv4, Is.Null);
        Assert.That(parsed.EndPointFor(AddressFamily.InterNetwork), Is.Null);
        Assert.That(parsed.EndPointFor(AddressFamily.InterNetworkV6)!.Port, Is.EqualTo(443));
    }

    [Test]
    public void AZeroLengthConnectionIdIsRejected()
    {
        // §18.2: "a server MUST NOT include a zero-length connection ID in this transport parameter.
        // A client MUST treat a violation of these requirements as a connection error of type
        // TRANSPORT_PARAMETER_ERROR."
        byte[] value = new byte[4 + 2 + 16 + 2 + 1 + 16]; // length byte stays 0
        Assert.That(PreferredAddress.TryParse(value, out _), Is.False);
    }

    [Test]
    public void AnOverlongConnectionIdIsRejected()
    {
        byte[] value = new byte[4 + 2 + 16 + 2 + 1 + 21 + 16];
        value[24] = 21; // one more than the 20 bytes §17.2 permits
        Assert.That(PreferredAddress.TryParse(value, out _), Is.False);
    }

    [Test]
    public void ATruncatedParameterIsRejected()
    {
        byte[] value = PreferredAddress.ForIPv4(new IPEndPoint(System.Net.IPAddress.Loopback, 443), Cid, Token).Encode();
        Assert.That(PreferredAddress.TryParse(value.AsSpan(0, value.Length - 1), out _), Is.False);
        Assert.That(PreferredAddress.TryParse([], out _), Is.False);
    }

    [Test]
    public void ItSurvivesTheTransportParameterEncoding()
    {
        var parameters = new TransportParameters
        {
            InitialSourceConnectionIdValue = Cid,
            PreferredAddressValue = PreferredAddress.ForIPv4(
                new IPEndPoint(System.Net.IPAddress.Parse("203.0.113.5"), 4433), Cid, Token),
        };

        Assert.That(TransportParameters.TryDecode(parameters.Encode(), out TransportParameters? decoded), Is.True);
        Assert.That(decoded!.SawPreferredAddress, Is.True);
        Assert.That(decoded.PreferredAddressValue, Is.EqualTo(parameters.PreferredAddressValue));
    }

    [Test]
    public void AMalformedParameterFailsTheWholeDecode()
    {
        // A broken preferred_address must not be skipped as if it were an unknown parameter — the
        // caller turns a false here into TRANSPORT_PARAMETER_ERROR.
        var writer = new List<byte> { 0x40, 0x0d, 0x02, 0x00, 0x00 }; // id 0x0d, length 2, garbage
        Assert.That(TransportParameters.TryDecode(writer.ToArray(), out _), Is.False);
    }

    #endregion

    #region Over a real connection

    [Test]
    public void TheClientLearnsTheAddress_AndMigratesAfterTheHandshake()
    {
        var preferred = PreferredAddress.ForIPv4(new IPEndPoint(System.Net.IPAddress.Parse("198.51.100.9"), 4433),
                                                 Cid, Token);

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, preferredAddress: preferred);

        client.Start();

        // §9.6.1: "Once the handshake is confirmed" — not before.
        Assert.That(client.MigrateToPreferredAddress(), Is.Null);

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] datagram in client.GetDatagramsToSend())
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in server.GetDatagramsToSend())
                client.ProcessDatagram(datagram);
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(client.ServerPreferredAddress, Is.EqualTo(preferred));

        IPEndPoint? target = client.MigrateToPreferredAddress();
        Assert.That(target, Is.Not.Null);
        Assert.That(target!.Address.ToString(), Is.EqualTo("198.51.100.9"));
        Assert.That(target.Port, Is.EqualTo(4433));

        // §9.6.1: the client uses the connection ID that came with the address, not the handshake
        // one — reusing the old CID would let an observer link the two paths.
        Assert.That(client.DestinationConnectionId, Is.EqualTo(Cid));
        Assert.That(client.PathValidationPending, Is.True, "A migration starts with path validation (§9.6.2).");
    }

    [Test]
    public void WithoutAPreferredAddress_ThereIsNothingToMigrateTo()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] datagram in client.GetDatagramsToSend())
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in server.GetDatagramsToSend())
                client.ProcessDatagram(datagram);
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(client.ServerPreferredAddress, Is.Null);
        Assert.That(client.MigrateToPreferredAddress(), Is.Null);
        Assert.That(client.PathValidationPending, Is.False);
    }

    [Test]
    public void AskingForAFamilyTheServerDidNotOffer_MigratesNowhere()
    {
        var preferred = PreferredAddress.ForIPv4(new IPEndPoint(System.Net.IPAddress.Parse("198.51.100.9"), 4433),
                                                 Cid, Token);

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, preferredAddress: preferred);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] datagram in client.GetDatagramsToSend())
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in server.GetDatagramsToSend())
                client.ProcessDatagram(datagram);
        }

        Assert.That(client.MigrateToPreferredAddress(AddressFamily.InterNetworkV6), Is.Null);
        Assert.That(client.PathValidationPending, Is.False, "Nothing may be started for an address that is not there.");
    }

    [Test]
    public void AServerRefusesToAnnounceAZeroLengthConnectionId()
    {
        // §18.2 forbids it, and the client would have to kill the connection over it — so it never
        // reaches the wire.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var broken = new PreferredAddress(System.Net.IPAddress.Loopback, 4433, null, 0, ConnectionId.Empty, Token);

        Assert.Throws<ArgumentException>(() => _ = new QuicServerConnection(cert, preferredAddress: broken));
    }

    #endregion

}
