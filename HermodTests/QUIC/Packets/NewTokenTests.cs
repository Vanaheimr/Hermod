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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Packets;

/// <summary>
/// Address validation for FUTURE connections (RFC 9000 §8.1.3): the server issues a token in a
/// NEW_TOKEN frame, the client keeps it and presents it in the Initial of its next connection, which
/// lets the server skip the Retry round trip.
/// </summary>
[TestFixture]
public class NewTokenTests
{

    private static readonly System.Net.IPAddress ClientAddress = System.Net.IPAddress.Parse("198.51.100.7");

    #region Token construction (RetryTokenGenerator)

    [Test]
    public void ANewTokenValidates_ForTheAddressItWasIssuedTo()
    {
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.IssueNewToken(ClientAddress);

        Assert.That(tokens.TryValidateNewToken(token, ClientAddress), Is.True);
        Assert.That(tokens.TryValidateNewToken(token, System.Net.IPAddress.Parse("203.0.113.9")), Is.False,
                    "§8.1.4 requires the client IP address to be bound.");
    }

    [Test]
    public void ANewTokenIgnoresThePort_UnlikeARetryToken()
    {
        // §8.1.3: "It is unlikely that the client port number is the same on two different
        // connections; validating the port is therefore unlikely to be successful." A NEW_TOKEN
        // token that bound the port would almost never validate — the client picks a fresh source
        // port for the next connection.
        var tokens = new RetryTokenGenerator();
        byte[] newToken = tokens.IssueNewToken(ClientAddress);

        Assert.That(tokens.TryValidateNewToken(newToken, ClientAddress), Is.True,
                    "Validation does not involve a port at all.");

        // The Retry token, in contrast, SHOULD bind address AND port (§8.1.4) — and does.
        var cidA = new ConnectionId(new byte[] { 1, 2, 3, 4 });
        var cidB = new ConnectionId(new byte[] { 5, 6, 7, 8 });
        byte[] retryToken = tokens.Issue(new IPEndPoint(ClientAddress, 4433), cidA, cidB);
        Assert.That(tokens.TryValidate(retryToken, new IPEndPoint(ClientAddress, 4433), out _, out _), Is.True);
        Assert.That(tokens.TryValidate(retryToken, new IPEndPoint(ClientAddress, 4434), out _, out _), Is.False,
                    "A different port must break a RETRY token.");
    }

    [Test]
    public void TheTwoKindsAreNotInterchangeable()
    {
        // §8.1.1: both travel in the same header field but "require different handling from servers",
        // so neither may pass as the other.
        var tokens = new RetryTokenGenerator();
        var cid = new ConnectionId(new byte[] { 9, 9, 9, 9 });
        var endpoint = new IPEndPoint(ClientAddress, 4433);

        byte[] newToken = tokens.IssueNewToken(ClientAddress);
        byte[] retryToken = tokens.Issue(endpoint, cid, cid);

        Assert.That(tokens.TryValidate(newToken, endpoint, out _, out _), Is.False,
                    "A NEW_TOKEN token must not validate as a Retry token.");
        Assert.That(tokens.TryValidateNewToken(retryToken, ClientAddress), Is.False,
                    "A Retry token must not validate as a NEW_TOKEN token.");
    }

    [Test]
    public void TheKindIsReadable_ForTokensWeIssued_AndNotForAnythingElse()
    {
        var tokens = new RetryTokenGenerator();
        var endpoint = new IPEndPoint(ClientAddress, 4433);
        var cid = new ConnectionId(new byte[] { 4, 3, 2, 1 });

        Assert.That(tokens.TryReadKind(tokens.IssueNewToken(ClientAddress), endpoint, out RetryTokenGenerator.TokenKind newKind), Is.True);
        Assert.That(newKind, Is.EqualTo(RetryTokenGenerator.TokenKind.NewToken));

        Assert.That(tokens.TryReadKind(tokens.Issue(endpoint, cid, cid), endpoint, out RetryTokenGenerator.TokenKind retryKind), Is.True);
        Assert.That(retryKind, Is.EqualTo(RetryTokenGenerator.TokenKind.Retry));

        Assert.That(tokens.TryReadKind(new byte[48], endpoint, out _), Is.False,
                    "A token that is not ours cannot be attributed to either mechanism.");
    }

    [Test]
    public void ANewTokenIsAcceptedOnlyOnce()
    {
        // §8.1.4: tokens from NEW_TOKEN frames "SHOULD NOT be accepted multiple times".
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.IssueNewToken(ClientAddress);

        Assert.That(tokens.TryConsumeNewToken(token), Is.True);
        Assert.That(tokens.TryConsumeNewToken(token), Is.False, "A replay must not be accepted.");
    }

    [Test]
    public void ANewTokenExpires_ButOutlivesARetryToken()
    {
        // §8.1.4: Retry tokens are returned immediately and should be accepted "only … for a short
        // time"; NEW_TOKEN tokens "need to be valid for longer".
        var clock = new FakeTimeProvider();
        var tokens = new RetryTokenGenerator(timeProvider: clock,
                                             lifetime: TimeSpan.FromSeconds(10),
                                             newTokenLifetime: TimeSpan.FromMinutes(30));
        byte[] token = tokens.IssueNewToken(ClientAddress);

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.That(tokens.TryValidateNewToken(token, ClientAddress), Is.True,
                    "Still inside the NEW_TOKEN lifetime — far beyond a Retry token's.");

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.That(tokens.TryValidateNewToken(token, ClientAddress), Is.False);
    }

    [Test]
    public void EveryIssuedTokenIsUnique()
    {
        // §8.1.3: "A server MUST ensure that every NEW_TOKEN frame it sends is unique across all
        // clients" — otherwise two clients could be correlated by an observer.
        var tokens = new RetryTokenGenerator();
        var seen = new HashSet<string>();
        for (int i = 0; i < 500; i++)
            Assert.That(seen.Add(Convert.ToHexString(tokens.IssueNewToken(ClientAddress))), Is.True);
    }

    #endregion

    #region Over a real connection

    private static (QuicClientConnection Client, QuicServerConnection Server, ServerCertificate Cert) Pair(
        byte[]? clientToken = null)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                              addressValidationToken: clientToken);
        var server = new QuicServerConnection(cert);
        client.Start();
        return (client, server, cert);
    }

    private static void Exchange(QuicClientConnection client, QuicServerConnection server, int rounds = 10)
    {
        for (int round = 0; round < rounds; round++)
        {
            foreach (byte[] datagram in client.GetDatagramsToSend())
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in server.GetDatagramsToSend())
                client.ProcessDatagram(datagram);
        }
    }

    [Test]
    public void TheServerIssuesItsTokenAfterTheHandshake_AndTheClientKeepsIt()
    {
        var tokens = new RetryTokenGenerator();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair();
        using (cert) using (client) using (server)
        {
            server.NewTokenToSend = tokens.IssueNewToken(ClientAddress);

            Exchange(client, server);

            Assert.That(client.HandshakeConfirmed, Is.True);
            Assert.That(client.NewTokens, Has.Count.EqualTo(1), "The client must have kept the token.");
            Assert.That(tokens.TryValidateNewToken(client.NewTokens[0], ClientAddress), Is.True,
                        "And it must be the token the server issued.");
        }
    }

    [Test]
    public void WithoutAConfiguredToken_NoNewTokenFrameIsSent()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair();
        using (cert) using (client) using (server)
        {
            Exchange(client, server);

            Assert.That(client.HandshakeConfirmed, Is.True);
            Assert.That(client.NewTokens, Is.Empty, "Issuing tokens is a MAY, not a must (§8.1.3).");
        }
    }

    [Test]
    public void AClientPresentsItsTokenInTheInitial()
    {
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.IssueNewToken(ClientAddress);

        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair(clientToken: token);
        using (cert) using (client) using (server)
        {
            // §8.1.3: "The client MUST include the token in all Initial packets it sends."
            byte[] firstDatagram = client.GetDatagramsToSend()[0];
            Assert.That(LongHeader.TryParse(firstDatagram, out LongHeaderPrefix? prefix), Is.True);
            Assert.That(prefix!.Token.ToArray(), Is.EqualTo(token));
        }
    }

    [Test]
    public void ADuplicateTokenIsNotStoredTwice()
    {
        // §19.7: a repaired loss can deliver the same NEW_TOKEN twice, and "Clients are responsible
        // for discarding duplicate values, which might be used to link connection attempts".
        var tokens = new RetryTokenGenerator();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair();
        using (cert) using (client) using (server)
        {
            byte[] token = tokens.IssueNewToken(ClientAddress);
            server.NewTokenToSend = token;
            Exchange(client, server);
            Assert.That(client.NewTokens, Has.Count.EqualTo(1));

            // The same token again, as a retransmission would deliver it.
            server.NewTokenToSend = token;
            Exchange(client, server);
            Assert.That(client.NewTokens, Has.Count.EqualTo(1), "The duplicate must be discarded.");
        }
    }

    [Test]
    public void AnEmptyNewToken_IsAFrameEncodingError()
    {
        // §19.7: "A client MUST treat receipt of a NEW_TOKEN frame with an empty Token field as a
        // connection error of type FRAME_ENCODING_ERROR."
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair();
        using (cert) using (client) using (server)
        {
            Exchange(client, server);
            Assert.That(client.HandshakeConfirmed, Is.True);

            server.SendApplicationFrameForTest(new NewTokenFrame(ReadOnlyMemory<byte>.Empty));
            Exchange(client, server, rounds: 2);

            Assert.That(client.LocalCloseErrorCode, Is.EqualTo((ulong)TransportError.FrameEncodingError));
        }
    }

    [Test]
    public void AServerReceivingNewToken_TreatsItAsAProtocolViolation()
    {
        // §19.7: "Clients MUST NOT send NEW_TOKEN frames. A server MUST treat receipt of a NEW_TOKEN
        // frame as a connection error of type PROTOCOL_VIOLATION."
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair();
        using (cert) using (client) using (server)
        {
            Exchange(client, server);
            Assert.That(client.HandshakeConfirmed, Is.True);

            client.SendApplicationFrameForTest(new NewTokenFrame(new byte[] { 1, 2, 3 }));
            Exchange(client, server, rounds: 2);

            Assert.That(server.LocalCloseErrorCode, Is.EqualTo((ulong)TransportError.ProtocolViolation));
        }
    }

    #endregion

}
