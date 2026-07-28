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

using System.Threading;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests of path validation (RFC 9000 §8.2) — the core primitive of connection migration (§9):
/// send PATH_CHALLENGE, expect a matching PATH_RESPONSE.
/// </summary>
[TestFixture]
public class ConnectionMigrationTests
{
    private static (QuicClientConnection client, QuicServerConnection server) Handshaken(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        Pump(client, server);
        return (client, server);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    [Test]
    public void PathValidation_ClientInitiates_ServerResponds_IsValidated()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        Assert.That(client.PathValidated, Is.False);

        client.InitiatePathValidation(); // sends PATH_CHALLENGE
        Assert.That(client.PathValidationPending, Is.True);

        for (int round = 0; round < 5; round++)
            Pump(client, server); // the server mirrors it back via PATH_RESPONSE

        Assert.That(client.PathValidated, Is.True, "A matching PATH_RESPONSE must validate the path.");
        Assert.That(client.PathValidationPending, Is.False);
    }

    [Test]
    public void PathValidation_ServerInitiates_ClientResponds_IsValidated()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        server.InitiatePathValidation();
        Assert.That(server.PathValidationPending, Is.True);

        for (int round = 0; round < 5; round++)
            Pump(client, server);

        Assert.That(server.PathValidated, Is.True);
    }

    [Test]
    public void PathValidation_WithoutResponse_Expires_AndDoesNotValidate()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        client.InitiatePathValidation();
        Assert.That(client.PathValidationPending, Is.True);

        // Without an exchange the validation deadline passes (3·PTO, tiny in-process).
        Thread.Sleep(300);
        client.CheckIdleTimeout(); // drives the expiry

        Assert.That(client.PathValidationPending, Is.False, "Without a PATH_RESPONSE the validation must expire.");
        Assert.That(client.PathValidated, Is.False);
    }
}
