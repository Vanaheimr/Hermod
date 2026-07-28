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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests of the anti-amplification limit (RFC 9000 §8.1): before address validation the server must not
/// send more than three times the received bytes; the client is unlimited by construction.
/// </summary>
[TestFixture]
public class AntiAmplificationTests
{
    private static (QuicClientConnection client, QuicServerConnection server) NewPair(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        return (new QuicClientConnection("localhost", certificateValidation: validation), new QuicServerConnection(cert));
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    [Test]
    public void Client_IsValidatedByConstruction_ServerIsNot()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = NewPair(cert);
        using var _ = client;
        using var __ = server;

        Assert.That(client.AddressValidated, Is.True);   // the client does not limit itself
        Assert.That(server.AddressValidated, Is.False);   // the server only after address validation
    }

    [Test]
    public void Server_NeverSendsMoreThanThreeTimesReceived_BeforeAddressValidation()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = NewPair(cert);
        using var _ = client;
        using var __ = server;
        client.Start();

        // Give the server exactly ONE client Initial; after that receive NOTHING more.
        long received = 0;
        foreach (byte[] dg in client.GetDatagramsToSend())
        {
            received += dg.Length;
            server.ProcessDatagram(dg);
        }

        // Across several send opportunities (without further reception) the server must never exceed the 3× limit.
        long sent = 0;
        for (int i = 0; i < 10; i++)
            foreach (byte[] dg in server.GetDatagramsToSend())
                sent += dg.Length;

        Assert.That(server.AddressValidated, Is.False); // still unvalidated without a Handshake packet from the client
        Assert.That(sent > 0, Is.True, "The server sends its (limited) flight.");
        Assert.That(sent <= 3 * received, Is.True, $"Anti-amplification violated: {sent} > 3×{received}.");
    }

    [Test]
    public void Server_BecomesValidated_AfterCompletingHandshake()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = NewPair(cert);
        using var _ = client;
        using var __ = server;
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);

        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(server.AddressValidated, Is.True, "A received Handshake packet validates the client address.");
    }

    [Test]
    public void RetryToken_ValidatesAddress_Immediately()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, requireRetry: true);
        client.Start();

        // Until the handshake completes: the valid Retry token lifts the limit early.
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);

        Assert.That(server.SentRetry, Is.True);
        Assert.That(server.AddressValidated, Is.True);
        Assert.That(client.HandshakeConfirmed, Is.True);
    }
}
