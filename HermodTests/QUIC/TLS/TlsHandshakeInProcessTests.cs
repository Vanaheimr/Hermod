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

using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Runs the client and server TLS 1.3 handshakes against each other in-process (both from scratch):
/// the CRYPTO bytes are exchanged per encryption level between the two engines. Validates the
/// server handshake including the CertificateVerify signature and the certificate chain, without a real network.
/// </summary>
[TestFixture]
public class TlsHandshakeInProcessTests
{
    /// <summary>
    /// Validates the self-signed test certificate against itself as a custom trust root (real chain path).
    /// </summary>
    private static CertificateValidationOptions TrustingOptions(ServerCertificate cert)
        => new() { CustomTrustRoots = [cert.Certificate] };

    [Test]
    public void ClientAndServer_CompleteHandshake_WithMatchingSecrets()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var clientTp = new byte[] { 0x0f, 0x00 };  // minimal transport-parameter block (initial_source_connection_id empty)
        var serverTp = new byte[] { 0x0f, 0x00 };

        var client = new TlsClientHandshake("localhost", clientTp, certificateValidation: TrustingOptions(cert));
        using var server = new TlsServerHandshake(cert, serverTp);

        RunHandshake(client, server);

        Assert.That(server.ClientFinishedValid, Is.True, "The server must accept the client Finished.");
        Assert.That(client.ServerCertificateValid, Is.True, "The client must have validated the server certificate.");
        Assert.That(client.ServerCertificate, Is.Not.Null);
        AssertMatchingSecrets(client, server);

        // By default the parties agree on X25519 (first offered group, no HRR).
        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X25519));
        Assert.That(server.SentHelloRetryRequest, Is.False);
        client.Dispose();
    }

    [Test]
    public void ClientAndServer_CompleteHandshake_WithX25519MlKem768()
    {
        // ML-KEM comes from the OS: Windows 11 24H2+ or OpenSSL 3.5+ (Debian 13). Where the
        // platform lacks it, that is a platform gap, not a Hermod bug — skip, don't fail.
        Assume.That(System.Security.Cryptography.MLKem.IsSupported, "ML-KEM is not supported by this platform's crypto library.");

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // The client offers only the PQ hybrid, the server prefers it → agreement without HRR. Also checks
        // that the large key shares (1216/1120 bytes) are serialized correctly through ClientHello/ServerHello.
        var client = new TlsClientHandshake("localhost", tp,
            keyShareGroups: [NamedGroup.X25519MlKem768],
            supportedGroups: [NamedGroup.X25519MlKem768],
            certificateValidation: TrustingOptions(cert));
        using var server = new TlsServerHandshake(cert, tp, preferredGroups: [NamedGroup.X25519MlKem768]);

        RunHandshake(client, server);

        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X25519MlKem768));
        Assert.That(server.SentHelloRetryRequest, Is.False);
        Assert.That(server.ClientFinishedValid, Is.True);
        Assert.That(client.ServerCertificateValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void ClientAndServer_CompleteHandshake_WithX448()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // The client offers only X448, the server prefers X448 → agreement on X448 without HRR.
        var client = new TlsClientHandshake("localhost", tp,
            keyShareGroups: [NamedGroup.X448],
            supportedGroups: [NamedGroup.X448],
            certificateValidation: TrustingOptions(cert));
        using var server = new TlsServerHandshake(cert, tp, preferredGroups: [NamedGroup.X448]);

        RunHandshake(client, server);

        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X448));
        Assert.That(server.SentHelloRetryRequest, Is.False);
        Assert.That(server.ClientFinishedValid, Is.True);
        Assert.That(client.ServerCertificateValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void ClientAndServer_CompleteHandshake_WithEd25519ServerCertificate()
    {
        using var cert = ServerCertificate.CreateSelfSignedEd25519("localhost");
        Assert.That(cert.SignatureScheme, Is.EqualTo(SignatureScheme.Ed25519));
        var tp = new byte[] { 0x0f, 0x00 };

        // Insecure validates the CertificateVerify signature — i.e. our Ed25519 verification path —
        // but not the X.509 chain (Ed25519 chain support in the OS is not guaranteed).
        var client = new TlsClientHandshake("localhost", tp,
            certificateValidation: CertificateValidationOptions.Insecure);
        using var server = new TlsServerHandshake(cert, tp);

        RunHandshake(client, server);

        Assert.That(client.ServerCertificateValid, Is.True, "The client must accept the Ed25519 CertificateVerify signature.");
        Assert.That(server.ClientFinishedValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void ClientAndServer_CompleteHandshake_WithEd448ServerCertificate()
    {
        using var cert = ServerCertificate.CreateSelfSignedEd448("localhost");
        Assert.That(cert.SignatureScheme, Is.EqualTo(SignatureScheme.Ed448));
        var tp = new byte[] { 0x0f, 0x00 };

        // Insecure validates the CertificateVerify signature — here our Ed448 verification path —
        // but not the X.509 chain (Ed448 chain support in the OS is not guaranteed).
        var client = new TlsClientHandshake("localhost", tp,
            certificateValidation: CertificateValidationOptions.Insecure);
        using var server = new TlsServerHandshake(cert, tp);

        RunHandshake(client, server);

        Assert.That(client.ServerCertificateValid, Is.True, "The client must accept the Ed448 CertificateVerify signature.");
        Assert.That(server.ClientFinishedValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void ClientAndServer_CompleteHandshake_WithMLDsaServerCertificate()
    {
        if (!System.Security.Cryptography.MLDsa.IsSupported)
            Assert.Ignore("ML-DSA is not supported on this platform (BCL/OS).");

        using var cert = ServerCertificate.CreateSelfSignedMLDsa("localhost");
        Assert.That(cert.SignatureScheme, Is.EqualTo(SignatureScheme.MLDsa65));
        var tp = new byte[] { 0x0f, 0x00 };

        // Insecure validates the CertificateVerify signature — here the ML-DSA verification path
        // (draft-ietf-tls-mldsa §4: pure, FIPS 204 context empty) — but not the X.509 chain.
        var client = new TlsClientHandshake("localhost", tp,
            certificateValidation: CertificateValidationOptions.Insecure);
        using var server = new TlsServerHandshake(cert, tp);

        RunHandshake(client, server);

        Assert.That(client.ServerCertificateValid, Is.True, "The client must accept the ML-DSA CertificateVerify signature.");
        Assert.That(server.ClientFinishedValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void MLDsaCertificates_AllThreeParameterSets_CarryMatchingKeys()
    {
        if (!System.Security.Cryptography.MLDsa.IsSupported)
            Assert.Ignore("ML-DSA is not supported on this platform (BCL/OS).");

        // NIST CSOR OIDs: id-ML-DSA-44/65/87 = 2.16.840.1.101.3.4.3.17/.18/.19.
        foreach ((SignatureScheme scheme, string oid) in new[]
        {
            (SignatureScheme.MLDsa44, "2.16.840.1.101.3.4.3.17"),
            (SignatureScheme.MLDsa65, "2.16.840.1.101.3.4.3.18"),
            (SignatureScheme.MLDsa87, "2.16.840.1.101.3.4.3.19"),
        })
        {
            using var cert = ServerCertificate.CreateSelfSignedMLDsa("localhost", scheme);
            Assert.That(cert.SignatureScheme, Is.EqualTo(scheme));
            Assert.That(cert.Certificate.PublicKey.Oid.Value, Is.EqualTo(oid));
            Assert.That(cert.SignCertificateVerify([1, 2, 3]), Is.Not.Empty);
        }

        Assert.Throws<ArgumentException>(() => ServerCertificate.CreateSelfSignedMLDsa("localhost", SignatureScheme.Ed25519));
    }

    [Test]
    public void HelloRetryRequest_WhenClientOffersOnlyP256ButServerPrefersX25519()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // The client sends only a P-256 key share but lists X25519 in supported_groups.
        var client = new TlsClientHandshake("localhost", tp,
            keyShareGroups: [NamedGroup.Secp256r1],
            supportedGroups: [NamedGroup.X25519, NamedGroup.Secp256r1],
            certificateValidation: TrustingOptions(cert));
        // The server accepts ONLY X25519 → no matching key share present → HelloRetryRequest.
        using var server = new TlsServerHandshake(cert, tp, preferredGroups: [NamedGroup.X25519]);

        RunHandshake(client, server);

        Assert.That(server.SentHelloRetryRequest, Is.True, "The server must have sent an HRR.");
        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X25519)); // agreed on X25519 after the HRR
        Assert.That(server.ClientFinishedValid, Is.True);
        Assert.That(client.ServerCertificateValid, Is.True);
        AssertMatchingSecrets(client, server);
        client.Dispose();
    }

    [Test]
    public void HelloRetryRequest_SecondClientHello_ReusesTheFirstRandom()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // Same constellation as above: only a P-256 key share offered, server insists on X25519,
        // so a HelloRetryRequest is unavoidable and the client rebuilds its ClientHello.
        var client = new TlsClientHandshake("localhost", tp,
            keyShareGroups: [NamedGroup.Secp256r1],
            supportedGroups: [NamedGroup.X25519, NamedGroup.Secp256r1],
            certificateValidation: TrustingOptions(cert));
        using var server = new TlsServerHandshake(cert, tp, preferredGroups: [NamedGroup.X25519]);

        // Collect every ClientHello the client puts on the wire. Initial-level CRYPTO carrying
        // handshake type 0x01 is a ClientHello; after an HRR there are exactly two.
        var clientHellos = new List<byte[]>();
        client.Start();
        for (int round = 0; round < 10 && !(client.IsComplete && server.IsComplete); round++)
        {
            while (client.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data))
            {
                if (level == EncryptionLevel.Initial && data.Length > 0 && data[0] == 0x01)
                    clientHellos.Add(data);
                server.ProvideCrypto(level, data);
            }
            Pump(server, client);
        }

        Assert.That(server.SentHelloRetryRequest, Is.True, "The server must have sent an HRR.");
        Assert.That(clientHellos, Has.Count.EqualTo(2), "Expected ClientHello1 and ClientHello2.");

        // RFC 8446 §4.1.2: the second ClientHello may differ only in an enumerated list of ways,
        // and the random is not among them. A fresh random here is a MUST violation.
        byte[] random1 = ClientHelloParser.ClientRandom(clientHellos[0]).ToArray();
        byte[] random2 = ClientHelloParser.ClientRandom(clientHellos[1]).ToArray();
        Assert.That(random1, Has.Length.EqualTo(32));
        Assert.That(random2, Is.EqualTo(random1), "ClientHello2 must carry the random of ClientHello1.");

        // The two hellos must still be genuinely different messages — otherwise this test would
        // also pass if the client simply resent ClientHello1 and never switched groups.
        Assert.That(clientHellos[1], Is.Not.EqualTo(clientHellos[0]));
        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X25519));
        client.Dispose();
    }

    [Test]
    public void ClientRejects_WhenHostnameDoesNotMatchCertificate()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // The expected hostname does NOT match the certificate (SAN: localhost) → validation must fail.
        var client = new TlsClientHandshake("wrong.example", tp,
            certificateValidation: new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] });
        using var server = new TlsServerHandshake(cert, tp);

        var ex = Assert.Throws<CertificateValidationException>(() => RunHandshake(client, server));
        Assert.That(ex!.Message, Does.Contain("hostname"));
        client.Dispose();
    }

    [Test]
    public void ClientRejects_SelfSignedCertificate_UnderDefaultPolicy()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // The default policy validates the chain against the system roots → self-signed is not trusted.
        var client = new TlsClientHandshake("localhost", tp); // CertificateValidationOptions.Default
        using var server = new TlsServerHandshake(cert, tp);

        Assert.Throws<CertificateValidationException>(() => RunHandshake(client, server));
        client.Dispose();
    }

    [Test]
    public void ClientAccepts_SelfSignedCertificate_UnderInsecurePolicy()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tp = new byte[] { 0x0f, 0x00 };

        // curl -k: the signature is validated, chain/hostname are not → the handshake succeeds.
        var client = new TlsClientHandshake("whatever.example", tp,
            certificateValidation: CertificateValidationOptions.Insecure);
        using var server = new TlsServerHandshake(cert, tp);

        RunHandshake(client, server);

        Assert.That(client.ServerCertificateValid, Is.True);
        Assert.That(server.ClientFinishedValid, Is.True);
        client.Dispose();
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
        Assert.That(client.HandshakeSecrets, Is.Not.Null);
        Assert.That(server.HandshakeSecrets, Is.Not.Null);
        Assert.That(server.HandshakeSecrets!.ServerHandshakeTrafficSecret, Is.EqualTo(client.HandshakeSecrets!.ServerHandshakeTrafficSecret));
        Assert.That(server.ApplicationSecrets!.ClientApplicationTrafficSecret, Is.EqualTo(client.ApplicationSecrets!.ClientApplicationTrafficSecret));
        Assert.That(server.ApplicationSecrets.ServerApplicationTrafficSecret, Is.EqualTo(client.ApplicationSecrets.ServerApplicationTrafficSecret));
    }

    private static void Pump(ITlsHandshake from, ITlsHandshake to)
    {
        while (from.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data))
            to.ProvideCrypto(level, data);
    }
}
