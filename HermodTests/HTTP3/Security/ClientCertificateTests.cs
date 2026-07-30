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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// Client certificates / mutual TLS (RFC 8446 §4.3.2, §4.4.2.4, §4.4.3). The server sends a
/// CertificateRequest after EncryptedExtensions; the client answers at the END of its flight with
/// Certificate + CertificateVerify, both of which then feed the transcript its Finished is computed
/// over. That transcript ordering is the whole difficulty — get it wrong and the failure looks like
/// a crypto bug, so most of what is asserted here is really "the two sides agree on the transcript".
/// </summary>
[TestFixture]
public class ClientCertificateTests
{
    /// <summary>
    /// Runs a handshake to completion (or to the point where it fails) and returns both endpoints.
    /// </summary>
    private static (QuicClientConnection Client, QuicServerConnection Server) Handshake(
        ServerCertificate serverCert,
        ClientCertificateOptions? serverPolicy = null,
        ServerCertificate? clientCert = null,
        int rounds = 20)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [serverCert.Certificate] };
        var server = new QuicServerConnection(serverCert, clientCertificate: serverPolicy);
        var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                              clientCertificate: clientCert);
        client.Start();
        for (int round = 0; round < rounds; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        return (client, server);
    }

    /// <summary>Trusts exactly the given certificate, and nothing else.</summary>
    private static ClientCertificateOptions Policy(ClientCertificateMode mode, ServerCertificate trusted)
        => new()
        {
            Mode = mode,
            Validation = new CertificateValidationOptions
            {
                VerifyHostname = false,
                CustomTrustRoots = [trusted.Certificate],
            },
        };

    // ---- The CertificateRequest message ---------------------------------------------------------

    [Test]
    public void CertificateRequest_RoundTrips()
    {
        List<SignatureScheme> offered =
            [SignatureScheme.EcdsaSecp256r1Sha256, SignatureScheme.Ed25519, SignatureScheme.RsaPssRsaeSha256];

        byte[] message = CertificateRequestMessage.Build(offered);

        Assert.That(message[0], Is.EqualTo((byte)HandshakeType.CertificateRequest));
        Assert.That(CertificateRequestMessage.TryParse(message.AsSpan(4), out byte[] context,
                                                       out List<SignatureScheme> parsed), Is.True);
        Assert.That(context, Is.Empty, "§4.3.2: the context is zero-length outside post-handshake auth.");
        Assert.That(parsed, Is.EqualTo(offered));
    }

    [Test]
    public void CertificateRequest_WithoutSignatureAlgorithms_IsInvalid()
    {
        // §4.3.2: "The signature_algorithms extension MUST be specified".
        byte[] body = [0x00, 0x00, 0x00]; // empty context, empty extensions vector
        Assert.That(CertificateRequestMessage.TryParse(body, out _, out _), Is.False);
    }

    [Test]
    public void CertificateMessage_CarriesAnEmptyList()
    {
        // §4.4.2.4: an empty Certificate is how a client declines — it must parse as valid-but-empty,
        // not as malformed, or the server cannot tell the two apart.
        byte[] message = CertificateMessage.Build(ReadOnlySpan<byte>.Empty);

        Assert.That(CertificateMessage.TryParseWithContext(message.AsSpan(4), out List<byte[]> certs,
                                                           out byte[] context), Is.True);
        Assert.That(certs, Is.Empty);
        Assert.That(context, Is.Empty);
    }

    // ---- The handshake ---------------------------------------------------------------------------

    [Test]
    public void Require_WithATrustedClientCertificate_Succeeds()
    {
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("test-client");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Require, clientCert), clientCert);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True, "The handshake must complete with mutual TLS.");
        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(client.ClientCertificateRequested, Is.True, "The client must have seen the CertificateRequest.");
        Assert.That(client.ClientCertificateSent, Is.True);

        ClientAuthenticationResult auth = server.ClientAuthentication;
        Assert.That(auth.Requested, Is.True);
        Assert.That(auth.IsAuthenticated, Is.True, auth.FailureReason ?? "no reason given");
        Assert.That(auth.Certificate!.Subject, Does.Contain("test-client"),
                    "The server must end up with the certificate the client actually presented.");
    }

    [Test]
    public void Require_WithoutAClientCertificate_FailsTheHandshake()
    {
        // §4.4.2.4: the server "MAY at its discretion either continue the handshake without client
        // authentication or abort the handshake with a certificate_required alert" — Require aborts.
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("test-client");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Require, clientCert), clientCert: null);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.False, "A client without a certificate must not get in.");
        Assert.That(client.ClientCertificateRequested, Is.True, "…but it WAS asked.");
        Assert.That(client.ClientCertificateSent, Is.False);
        Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
        Assert.That(server.LocalCloseErrorCode,
                    Is.EqualTo((ulong)TransportError.CryptoErrorBase + (ulong)TlsAlert.CertificateRequired),
                    "RFC 9001 §4.8: the alert travels as CRYPTO_ERROR + certificate_required (116).");
    }

    [Test]
    public void Require_WithAnUntrustedClientCertificate_FailsTheHandshake()
    {
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var trusted = ServerCertificate.CreateSelfSigned("test-client");
        using var stranger = ServerCertificate.CreateSelfSigned("someone-else");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Require, trusted), clientCert: stranger);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.False, "A certificate from an unknown CA must not be accepted.");
        Assert.That(server.LocalCloseErrorCode,
                    Is.EqualTo((ulong)TransportError.CryptoErrorBase + (ulong)TlsAlert.BadCertificate),
                    "…and the peer is told why: bad_certificate (42).");
    }

    [Test]
    public void Request_WithoutAClientCertificate_ConnectsUnauthenticated()
    {
        // The other half of the §4.4.2.4 discretion: ask, but let the application decide.
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("test-client");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Request, clientCert), clientCert: null);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True, "Request must let an anonymous client through.");
        Assert.That(server.ClientAuthentication.Requested, Is.True);
        Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False,
                    "…but it is not authenticated, and the application can see that.");
    }

    [Test]
    public void Request_WithAnUntrustedCertificate_ConnectsAndReportsWhy()
    {
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var trusted = ServerCertificate.CreateSelfSigned("test-client");
        using var stranger = ServerCertificate.CreateSelfSigned("someone-else");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Request, trusted), clientCert: stranger);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True);
        Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
        Assert.That(server.ClientAuthentication.FailureReason, Is.Not.Null.And.Not.Empty,
                    "The application should be able to log WHY it is treating this client as anonymous.");
    }

    [Test]
    public void WithoutAPolicy_NothingChanges()
    {
        // Regression guard: the ordinary handshake must not grow a CertificateRequest.
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");

        (QuicClientConnection client, QuicServerConnection server) = Handshake(serverCert);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True);
        Assert.That(client.ClientCertificateRequested, Is.False, "No policy ⇒ no CertificateRequest.");
        Assert.That(server.ClientAuthentication.Requested, Is.False);
    }

    [Test]
    public void ClientCertificate_WorksWithEd25519()
    {
        // The client CertificateVerify runs through the same signer abstraction as the server's, so a
        // non-ECDSA client key is worth one shot — Ed25519 signs the content directly (PureEdDSA).
        //
        // Chain verification is off here for the same reason the server-side Ed25519 test turns it
        // off: the Windows X509Chain cannot build a chain for an Ed25519 key at all. What is under
        // test is our signature path, which is the part that is ours.
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSignedEd25519("ed25519-client");
        var policy = new ClientCertificateOptions
        {
            Mode = ClientCertificateMode.Require,
            Validation = new CertificateValidationOptions { VerifyHostname = false, VerifyCertificateChain = false },
        };

        (QuicClientConnection client, QuicServerConnection server) = Handshake(serverCert, policy, clientCert);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True);
        Assert.That(server.ClientAuthentication.Certificate!.Subject, Does.Contain("ed25519-client"));
    }

    [Test]
    public void AChainTheePlatformCannotBuild_IsARejection_NotAnException()
    {
        // Ed25519 makes the Windows chain builder throw rather than report a status. A peer must
        // never be able to throw an arbitrary exception type through the validator and out into the
        // I/O loop, so that has to come back as an ordinary rejection.
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSignedEd25519("ed25519-client");

        (QuicClientConnection client, QuicServerConnection server) =
            Handshake(serverCert, Policy(ClientCertificateMode.Request, clientCert), clientCert);
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.HandshakeComplete, Is.True, "Request ⇒ the connection survives the rejection.");
        Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
        Assert.That(server.ClientAuthentication.FailureReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Resumption_DoesNotAskForACertificate()
    {
        // §4.3.2: "Servers which are authenticating with a PSK MUST NOT send the CertificateRequest
        // message in the main handshake."
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("test-client");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [serverCert.Certificate] };
        ClientCertificateOptions policy = Policy(ClientCertificateMode.Request, clientCert);

        // First connection: full handshake, collects a ticket.
        ResumptionTicket ticket;
        {
            using var server = new QuicServerConnection(serverCert, resumptionCache: cache, clientCertificate: policy);
            using var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                                        clientCertificate: clientCert);
            client.Start();
            for (int round = 0; round < 30; round++)
            {
                foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
                foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
            }
            Assert.That(client.NewSessionTickets, Is.Not.Empty, "Need a ticket to resume with.");
            ticket = client.NewSessionTickets[0];
        }

        // Second connection: resumed — and therefore NOT asked for a certificate.
        using var server2 = new QuicServerConnection(serverCert, resumptionCache: cache, clientCertificate: policy);
        using var client2 = new QuicClientConnection("localhost", certificateValidation: validation,
                                                     resumptionTicket: ticket, clientCertificate: clientCert);
        client2.Start();
        for (int round = 0; round < 30; round++)
        {
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
        }

        Assert.That(server2.ResumptionAccepted, Is.True, "The second handshake must actually resume.");
        Assert.That(server2.HandshakeComplete, Is.True);
        Assert.That(client2.ClientCertificateRequested, Is.False,
                    "A PSK handshake MUST NOT carry a CertificateRequest (§4.3.2).");
    }

    // ---- End to end over real sockets -------------------------------------------------------------

    [Test]
    public async Task MutualTls_OverTheAsyncFacades()
    {
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("api-client");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [serverCert.Certificate] };

        await using var server = new Http3Server(serverCert,
            _ => new Http3Response { Status = 200, Body = "mutually authenticated"u8.ToArray() },
            port: 0, clientCertificate: Policy(ClientCertificateMode.Require, clientCert));
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation, clientCertificate: clientCert);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        Http3Response response = await client.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("mutually authenticated"));
    }

    [Test]
    public async Task MutualTls_RefusesAClientWithoutACertificate()
    {
        using var serverCert = ServerCertificate.CreateSelfSigned("localhost");
        using var clientCert = ServerCertificate.CreateSelfSigned("api-client");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [serverCert.Certificate] };

        await using var server = new Http3Server(serverCert,
            _ => new Http3Response { Status = 200 },
            port: 0, clientCertificate: Policy(ClientCertificateMode.Require, clientCert));
        server.Start();

        await using var anonymous = new Http3Client("localhost", server.Port, validation);
        Assert.ThrowsAsync<TimeoutException>(async () => await anonymous.ConnectAsync(TimeSpan.FromSeconds(3)),
                                             "A client without a certificate must not complete the handshake.");

        // Decisive: the server survived the rejection and still serves the next client. Before the
        // handshake failure was turned into a connection close, the exception escaped into the
        // accept loop and took the whole server with it.
        await using var authorised = new Http3Client("localhost", server.Port, validation, clientCertificate: clientCert);
        await authorised.ConnectAsync(TimeSpan.FromSeconds(10));
        Http3Response response = await authorised.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(response.Status, Is.EqualTo(200));
    }
}
