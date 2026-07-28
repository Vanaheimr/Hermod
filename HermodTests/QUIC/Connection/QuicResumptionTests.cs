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
/// Session resumption over the full QUIC datagram path: connection 1 completes the handshake and
/// receives the server's NewSessionTicket (application-level CRYPTO in the 1-RTT packet); connection 2
/// resumes with the ticket. This also checks sending/receiving post-handshake CRYPTO.
/// </summary>
[TestFixture]
public class QuicResumptionTests
{
    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
    }

    [Test]
    public void ClientResumesAgainstOwnServer_OverDatagrams_NoCertificate()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // --- Connection 1: full handshake + ticket reception ---
        ResumptionTicket ticket;
        using (var client = new QuicClientConnection("localhost", certificateValidation: validation))
        using (var server = new QuicServerConnection(cert, resumptionCache: cache))
        {
            client.Start();
            for (int r = 0; r < 40 && !client.HandshakeConfirmed; r++)
                Pump(client, server);
            Assert.That(client.HandshakeConfirmed, Is.True, "Handshake (connection 1) must complete.");

            // The NewSessionTicket arrives post-handshake at application level — pump a few more rounds.
            for (int r = 0; r < 6 && client.NewSessionTickets.Count == 0; r++)
                Pump(client, server);
            Assert.That(client.NewSessionTickets, Is.Not.Empty);
            ticket = client.NewSessionTickets[0];
        }

        // --- Connection 2: resumption with the ticket ---
        using var client2 = new QuicClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        using var server2 = new QuicServerConnection(cert, resumptionCache: cache);
        client2.Start();
        for (int r = 0; r < 40 && !client2.HandshakeConfirmed; r++)
            Pump(client2, server2);

        Assert.That(client2.HandshakeConfirmed, Is.True, "Handshake (connection 2) must complete.");
        Assert.That(client2.ResumptionAccepted, Is.True, "The client must detect the PSK acceptance.");
        Assert.That(server2.ResumptionAccepted, Is.True, "The server must have accepted the binder.");
    }
}
