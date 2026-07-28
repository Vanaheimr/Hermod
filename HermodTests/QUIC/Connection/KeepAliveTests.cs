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
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Integration test for keep-alive via PING (RFC 9000 §10.1.2): regular PINGs keep a connection open
/// past a short idle timeout where it would otherwise be closed silently.
/// </summary>
[TestFixture]
public class KeepAliveTests
{
    [Test]
    public void KeepAlivePings_KeepConnectionAlive_PastTheIdleTimeout()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // The server announces a short idle timeout (250 ms); the client sends a keep-alive PING every 60 ms.
        var serverParams = new TransportParameters { MaxIdleTimeoutMs = 250 };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, serverParams);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");

        client.KeepAliveInterval = TimeSpan.FromMilliseconds(60);

        // Let considerably more than the idle timeout (250 ms) pass, pumping regularly.
        for (int round = 0; round < 18; round++)
        {
            Pump(client, server);
            client.CheckIdleTimeout();
            server.CheckIdleTimeout();
            Thread.Sleep(30); // ~540 ms total ⇒ > 250 ms idle timeout
        }

        Assert.That(server.IsIdleTimedOut, Is.False, "Keep-alive PINGs must prevent the server idle timeout.");
        Assert.That(client.IsIdleTimedOut, Is.False, "The client stays active through the PINGs (and the server's ACKs).");
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
