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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Session resumption / PSK (RFC 8446 §2.2/§4.6.1) at the TLS level, in-process: connection 1 performs
/// a full handshake and the server issues a NewSessionTicket; connection 2 plays the ticket back in
/// pre_shared_key, the server checks the binder and resumes (without a certificate). Also checks the
/// graceful fallback paths when the server does not know the ticket.
/// </summary>
[TestFixture]
public class ResumptionTests
{
    private static readonly byte[] Tp = [0x0f, 0x00];

    [Test]
    public void Client_ResumesWithPsk_ServerSkipsCertificate_AndSecretsMatch()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var trusting = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // Connection 1: full handshake; the server then issues a ticket.
        var client1 = new TlsClientHandshake("localhost", Tp, certificateValidation: trusting);
        using var server1 = new TlsServerHandshake(cert, Tp, resumptionCache: cache);
        RunHandshake(client1, server1);
        Assert.That(client1.ServerCertificateValid, Is.True);
        Assert.That(client1.ResumptionAccepted, Is.False);

        // The NewSessionTicket arrives post-handshake at application level — pump once more.
        Pump(server1, client1);
        Assert.That(client1.NewSessionTickets, Is.Not.Empty);
        ResumptionTicket ticket = client1.NewSessionTickets[0];
        client1.Dispose();

        // Connection 2: resumption with the ticket.
        var client2 = new TlsClientHandshake("localhost", Tp, certificateValidation: trusting, resumptionTicket: ticket);
        using var server2 = new TlsServerHandshake(cert, Tp, resumptionCache: cache);
        RunHandshake(client2, server2);

        Assert.That(client2.ResumptionAccepted, Is.True, "The client must detect the PSK acceptance (selected_identity).");
        Assert.That(server2.ResumptionAccepted, Is.True, "The server must have accepted the PSK binder.");
        Assert.That(client2.ServerCertificateValid, Is.False); // resumption ⇒ no certificate
        AssertMatchingSecrets(client2, server2);
        client2.Dispose();
    }

    [Test]
    public void UnknownTicket_FallsBackToFullHandshake_WithCertificate()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var trusting = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        var client1 = new TlsClientHandshake("localhost", Tp, certificateValidation: trusting);
        using var server1 = new TlsServerHandshake(cert, Tp, resumptionCache: cache);
        RunHandshake(client1, server1);
        Pump(server1, client1);
        ResumptionTicket ticket = client1.NewSessionTickets[0];
        client1.Dispose();

        // Second server with an EMPTY store ⇒ does not know the ticket ⇒ full handshake with certificate.
        var client2 = new TlsClientHandshake("localhost", Tp, certificateValidation: trusting, resumptionTicket: ticket);
        using var server2 = new TlsServerHandshake(cert, Tp, resumptionCache: new ServerResumptionCache());
        RunHandshake(client2, server2);

        Assert.That(client2.ResumptionAccepted, Is.False);
        Assert.That(server2.ResumptionAccepted, Is.False);
        Assert.That(client2.ServerCertificateValid, Is.True, "Without resumption the certificate must be validated.");
        AssertMatchingSecrets(client2, server2);
        client2.Dispose();
    }

    private static void RunHandshake(TlsClientHandshake client, TlsServerHandshake server)
    {
        client.Start();
        for (int round = 0; round < 10 && !(client.IsComplete && server.IsComplete); round++)
        {
            Pump(client, server);
            Pump(server, client);
        }
        Assert.That(client.IsComplete, Is.True, "Client handshake incomplete.");
        Assert.That(server.IsComplete, Is.True, "Server handshake incomplete.");
    }

    private static void AssertMatchingSecrets(ITlsHandshake client, ITlsHandshake server)
    {
        Assert.That(server.ApplicationSecrets!.ClientApplicationTrafficSecret, Is.EqualTo(client.ApplicationSecrets!.ClientApplicationTrafficSecret));
        Assert.That(server.ApplicationSecrets.ServerApplicationTrafficSecret, Is.EqualTo(client.ApplicationSecrets.ServerApplicationTrafficSecret));
    }

    private static void Pump(ITlsHandshake from, ITlsHandshake to)
    {
        while (from.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data))
            to.ProvideCrypto(level, data);
    }
}
