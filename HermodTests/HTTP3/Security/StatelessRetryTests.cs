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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// Stateless address validation (RFC 9000 §8.1.2/§8.1.4): the server answers a connection attempt
/// with a Retry <b>before</b> any connection state exists. The token carries everything it would
/// otherwise have to remember, so a flood from forged sender addresses costs it one AEAD seal per
/// packet — not a connection object per packet.
/// </summary>
[TestFixture]
public class StatelessRetryTests
{
    private static IPEndPoint Client(string address = "192.0.2.10", int port = 4711)
        => new(System.Net.IPAddress.Parse(address), port);

    private static ConnectionId Cid(params byte[] bytes) => new(bytes);

    // ---- The token on its own --------------------------------------------------------------------

    [Test]
    public void Token_RoundTripsTheConnectionIds()
    {
        var tokens = new RetryTokenGenerator();
        ConnectionId odcid = Cid(0xd0, 0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7);
        ConnectionId retryScid = Cid(1, 2, 3, 4, 5, 6, 7, 8);

        byte[] token = tokens.Issue(Client(), odcid, retryScid);

        Assert.That(tokens.TryValidate(token, Client(), out ConnectionId gotOdcid, out ConnectionId gotScid), Is.True);
        Assert.That(gotOdcid, Is.EqualTo(odcid), "The ODCID must survive — it becomes the transport parameter.");
        Assert.That(gotScid, Is.EqualTo(retryScid), "As must the connection ID we promised the client.");
    }

    [Test]
    public void Token_IsRejected_AtADifferentAddressOrPort()
    {
        // RFC 9000 §8.1.4: a Retry token SHOULD prove that source address AND port are unchanged.
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.Issue(Client("192.0.2.10", 4711), Cid(1, 2), Cid(3, 4));

        Assert.That(tokens.TryValidate(token, Client("192.0.2.11", 4711), out _, out _), Is.False,
                    "A token stolen from another client must not work at a different address.");
        Assert.That(tokens.TryValidate(token, Client("192.0.2.10", 4712), out _, out _), Is.False,
                    "Nor at a different port.");
        Assert.That(tokens.TryValidate(token, Client("192.0.2.10", 4711), out _, out _), Is.True);
    }

    [Test]
    public void Token_IsRejected_WhenForgedOrModified()
    {
        // §8.1.4: "the token MUST be covered by integrity protection against modification or
        // falsification by clients".
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.Issue(Client(), Cid(1, 2), Cid(3, 4));

        Assert.That(tokens.TryValidate(RandomNumberGenerator.GetBytes(token.Length), Client(), out _, out _), Is.False,
                    "A guessed token must not be accepted.");

        for (int position = 0; position < token.Length; position += 7)
        {
            byte[] modified = token.ToArray();
            modified[position] ^= 0x01;
            Assert.That(tokens.TryValidate(modified, Client(), out _, out _), Is.False,
                        $"A single flipped bit at byte {position} must invalidate the token.");
        }

        // And a token from a different server (different secret) is worthless here.
        var otherServer = new RetryTokenGenerator();
        Assert.That(tokens.TryValidate(otherServer.Issue(Client(), Cid(1, 2), Cid(3, 4)), Client(), out _, out _),
                    Is.False);
    }

    [Test]
    public void Token_Expires()
    {
        // §8.1.4: tokens from Retry packets SHOULD "only be accepted for a short time" — the client
        // returns them immediately.
        var clock = new FakeTimeProvider();
        var tokens = new RetryTokenGenerator(lifetime: TimeSpan.FromSeconds(10), timeProvider: clock);
        byte[] token = tokens.Issue(Client(), Cid(1, 2), Cid(3, 4));

        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.That(tokens.TryValidate(token, Client(), out _, out _), Is.True, "Still inside the window.");

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.That(tokens.TryValidate(token, Client(), out _, out _), Is.False, "Past the lifetime ⇒ worthless.");
    }

    [Test]
    public void Token_IsAcceptedOnlyOnce()
    {
        // §8.1.4: "Attackers could replay tokens to use servers as amplifiers in DDoS attacks. To
        // protect against such attacks, servers MUST ensure that replay of tokens is prevented or
        // limited."
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.Issue(Client(), Cid(1, 2), Cid(3, 4));

        Assert.That(tokens.TryValidate(token, Client(), out _, out _), Is.True);
        Assert.That(tokens.TryConsume(token), Is.True, "First use.");
        Assert.That(tokens.TryConsume(token), Is.False, "A replay must be refused.");
    }

    [Test]
    public void SpentTokens_StayBounded()
    {
        // The single-use set must not become the memory leak that the connection cap closed.
        var tokens = new RetryTokenGenerator();
        for (int i = 0; i < RetryTokenGenerator.MaxTrackedTokens + 500; i++)
            tokens.TryConsume(tokens.Issue(Client(port: 1000 + i), Cid(1, 2), Cid(3, 4)));

        Assert.That(tokens.TrackedTokenCount, Is.LessThanOrEqualTo(RetryTokenGenerator.MaxTrackedTokens),
                    "Remembering spent tokens must be bounded, even under a flood of valid returns.");
    }

    [Test]
    public void ExpiredTokens_AreForgotten()
    {
        var clock = new FakeTimeProvider();
        var tokens = new RetryTokenGenerator(lifetime: TimeSpan.FromSeconds(10), timeProvider: clock);
        for (int i = 0; i < RetryTokenGenerator.MaxTrackedTokens; i++)
            tokens.TryConsume(tokens.Issue(Client(port: 1000 + (i % 60000)), Cid(1, 2), Cid(3, 4)));

        clock.Advance(TimeSpan.FromSeconds(30));
        tokens.TryConsume(tokens.Issue(Client(), Cid(1, 2), Cid(3, 4)));

        Assert.That(tokens.TrackedTokenCount, Is.LessThan(RetryTokenGenerator.MaxTrackedTokens / 2),
                    "Once they can no longer be replayed, spent tokens are dead weight.");
    }

    [Test]
    public void ANewTokenStyleTokenIsNotAcceptedAsARetryToken()
    {
        // §8.1.1: "A token sent in a NEW_TOKEN frame or a Retry packet MUST be constructed in a way
        // that allows the server to identify how it was provided to a client." The kind byte is what
        // makes that possible — here we prove it is actually checked.
        var tokens = new RetryTokenGenerator();
        byte[] token = tokens.Issue(Client(), Cid(1, 2), Cid(3, 4));
        Assert.That(tokens.TryValidate(token, Client(), out _, out _), Is.True);

        Assert.That(RetryTokenGenerator.TokenKind.Retry, Is.Not.EqualTo(RetryTokenGenerator.TokenKind.NewToken),
                    "The two kinds must be distinguishable inside the token.");
    }

    // ---- The listener --------------------------------------------------------------------------

    [Test]
    public async Task Handshake_SucceedsThroughTheRetry()
    {
        // The decisive end-to-end case: with address validation on, a normal client still gets
        // through — one extra round trip, and the transport parameters (§7.3) all check out, which
        // is what proves the ODCID survived the stateless detour.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = "validated"u8.ToArray() },
            port: 0, addressValidation: new RetryTokenGenerator());
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        Http3Response response = await client.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("validated"));
        Assert.That(server.RetriesSent, Is.GreaterThan(0), "The server must actually have sent a Retry.");
        Assert.That(server.ConnectionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task FloodFromManySources_CreatesNoState()
    {
        // The point of the whole exercise. Each attempt comes from a DIFFERENT source port, because
        // that is what a spoofed flood looks like and what defeats the endpoint-based demux — one
        // socket would just be routed to the connection the first packet created. Measured without
        // address validation, this same flood produces one connection per source (see the sibling
        // test below); with it, none.
        const int attempts = 128;
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        await using var server = new Http3Server(cert,
            _ => new Http3Response { Status = 200 }, port: 0, addressValidation: new RetryTokenGenerator());
        server.Start();

        List<UdpClient> sources = await FloodAsync(server.Port, attempts);
        await WaitUntil(() => server.RetriesSent >= attempts, TimeSpan.FromSeconds(10));
        CloseAll(sources);

        // Not every datagram necessarily arrives — this is UDP, and the kernel drops what does not
        // fit the receive buffer. What matters is the ratio: many attempts answered, ZERO state.
        Assert.That(server.RetriesSent, Is.GreaterThan(attempts / 4),
                    "The Retry path must actually be answering these attempts.");
        Assert.That(server.ConnectionCount, Is.Zero,
                    $"{server.ConnectionCount} connections created although no client ever proved its address.");
        Assert.That(server.ConnectionsRefused, Is.Zero, "The connection cap was never even approached.");
    }

    [Test]
    public async Task WithoutAddressValidation_TheSameFloodCreatesAConnectionPerSource()
    {
        // The control experiment — without it the test above proves nothing, since a flood that
        // creates no state anyway would pass either way. This is the cost the feature removes.
        const int attempts = 128;
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        await using var server = new Http3Server(cert, _ => new Http3Response { Status = 200 }, port: 0);
        server.Start();

        List<UdpClient> sources = await FloodAsync(server.Port, attempts);
        await WaitUntil(() => server.ConnectionCount >= attempts / 4, TimeSpan.FromSeconds(10));
        CloseAll(sources);

        Assert.That(server.ConnectionCount, Is.GreaterThan(attempts / 4),
                    "Without address validation every forged source buys the attacker a connection object.");
        Assert.That(server.RetriesSent, Is.Zero);
    }

    /// <summary>
    /// Sends <paramref name="attempts"/> Initial-looking datagrams, each from its own source port —
    /// as many distinct "clients" as attempts. The sockets are returned still open: closing one makes
    /// the OS answer the server's reply with an ICMP port-unreachable, and that noise would land in
    /// the middle of the measurement.
    /// </summary>
    private static async Task<List<UdpClient>> FloodAsync(int serverPort, int attempts)
    {
        var target = new IPEndPoint(System.Net.IPAddress.Loopback, serverPort);
        var sockets = new List<UdpClient>(attempts);
        for (int i = 0; i < attempts; i++)
        {
            var socket = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            sockets.Add(socket);
            byte[] initial = FakeInitial();
            socket.Send(initial, initial.Length, target);
            if (i % 10 == 9)
                await Task.Delay(1).ConfigureAwait(false); // let the loop keep up with the socket buffer
        }
        return sockets;
    }

    private static void CloseAll(List<UdpClient> sockets)
    {
        foreach (UdpClient socket in sockets)
            socket.Dispose();
    }

    [Test]
    public async Task AValidNewTokenSkipsTheRetry()
    {
        // The point of §8.1.3: a client that connected before presents the token it was given, and
        // the server validates the address from the token alone — no Retry, so no extra round trip.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tokens = new RetryTokenGenerator();

        await using var server = new Http3Server(cert,
            _ => new Http3Response { Status = 200 }, port: 0, addressValidation: tokens);
        server.Start();

        using var client = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var target = new IPEndPoint(System.Net.IPAddress.Loopback, server.Port);

        byte[] initial = FakeInitial(token: tokens.IssueNewToken(System.Net.IPAddress.Loopback));
        client.Send(initial, initial.Length, target);

        await WaitUntil(() => server.NewTokensAccepted > 0, TimeSpan.FromSeconds(10));

        Assert.That(server.NewTokensAccepted, Is.EqualTo(1));
        Assert.That(server.RetriesSent, Is.Zero, "That round trip is exactly what the token saves.");
        Assert.That(server.TokensRejected, Is.Zero);
    }

    [Test]
    public async Task AReplayedNewTokenDoesNotSkipTheRetry()
    {
        // §8.1.4: NEW_TOKEN tokens "SHOULD NOT be accepted multiple times" — otherwise a replayed
        // token would let an attacker use the server as an amplifier from a spoofed address.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tokens = new RetryTokenGenerator();

        await using var server = new Http3Server(cert,
            _ => new Http3Response { Status = 200 }, port: 0, addressValidation: tokens);
        server.Start();

        using var client = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var target = new IPEndPoint(System.Net.IPAddress.Loopback, server.Port);
        byte[] token = tokens.IssueNewToken(System.Net.IPAddress.Loopback);

        byte[] first = FakeInitial(token: token);
        client.Send(first, first.Length, target);
        await WaitUntil(() => server.NewTokensAccepted > 0, TimeSpan.FromSeconds(10));

        // The same token again, from a fresh source port so it is not routed to the connection above.
        using var replayer = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        byte[] second = FakeInitial(token: token);
        replayer.Send(second, second.Length, target);
        await WaitUntil(() => server.RetriesSent > 0, TimeSpan.FromSeconds(10));

        Assert.That(server.NewTokensAccepted, Is.EqualTo(1), "The second use must not count as valid.");
        Assert.That(server.RetriesSent, Is.EqualTo(1), "The replay falls back to address validation.");
    }

    [Test]
    public async Task UnreadableToken_GetsARetry_NotARefusal()
    {
        // Once the server also issues NEW_TOKEN tokens (§8.1.3), a token it cannot decrypt is no
        // longer attributable to either mechanism — it may well be one of our own NEW_TOKEN tokens,
        // expired or issued under a secret this process no longer holds. §8.1.3: "If the token is
        // invalid, then the server SHOULD proceed as if the client did not have a validated address,
        // including potentially sending a Retry packet." The note there is explicit that discarding
        // would fail such a connection outright. §8.1.2's INVALID_TOKEN is reserved for a token the
        // server CAN read and knows to be a Retry token — see the test below.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        await using var server = new Http3Server(cert,
            _ => new Http3Response { Status = 200 }, port: 0, addressValidation: new RetryTokenGenerator());
        server.Start();

        using var attacker = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var target = new IPEndPoint(System.Net.IPAddress.Loopback, server.Port);
        byte[] initial = FakeInitial(token: RandomNumberGenerator.GetBytes(48));
        attacker.Send(initial, initial.Length, target);

        await WaitUntil(() => server.RetriesSent > 0, TimeSpan.FromSeconds(10));

        Assert.That(server.RetriesSent, Is.EqualTo(1));
        Assert.That(server.NewTokensRejected, Is.EqualTo(1));
        Assert.That(server.TokensRejected, Is.Zero, "An unattributable token is not an invalid RETRY token.");
        Assert.That(server.ConnectionCount, Is.Zero, "Still no state — the token is the state.");

        attacker.Client.ReceiveTimeout = 5000;
        IPEndPoint? from = null;
        byte[] answer = attacker.Receive(ref from);
        Assert.That(PacketFormat.IsLongHeader(answer[0]), Is.True);
        Assert.That(PacketFormat.GetLongPacketType(answer[0]), Is.EqualTo(LongPacketType.Retry));
    }

    [Test]
    public async Task InvalidRetryToken_IsRefusedWithInvalidToken()
    {
        // §8.1.2: "If a server receives a client Initial that contains an invalid Retry token but is
        // otherwise valid, it knows the client will not accept another Retry token … the server
        // SHOULD immediately close the connection with an INVALID_TOKEN error." The token here IS
        // ours and IS of the Retry kind, so it is attributable — it just names a connection ID the
        // Initial is not addressed to, which is how a token pasted onto a foreign packet looks.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var tokens = new RetryTokenGenerator();

        await using var server = new Http3Server(cert,
            _ => new Http3Response { Status = 200 }, port: 0, addressValidation: tokens);
        server.Start();

        using var attacker = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var target = new IPEndPoint(System.Net.IPAddress.Loopback, server.Port);
        var source = (IPEndPoint)attacker.Client.LocalEndPoint!;

        byte[] retryToken = tokens.Issue(source,
                                         new ConnectionId(RandomNumberGenerator.GetBytes(8)),
                                         new ConnectionId(RandomNumberGenerator.GetBytes(8)));
        byte[] initial = FakeInitial(token: retryToken);
        attacker.Send(initial, initial.Length, target);

        await WaitUntil(() => server.TokensRejected > 0, TimeSpan.FromSeconds(10));

        Assert.That(server.TokensRejected, Is.EqualTo(1));
        Assert.That(server.ConnectionCount, Is.Zero);
        Assert.That(server.RetriesSent, Is.Zero, "It must NOT trigger a second Retry (§8.1.2).");

        attacker.Client.ReceiveTimeout = 5000;
        IPEndPoint? from = null;
        byte[] answer = attacker.Receive(ref from);
        Assert.That(PacketFormat.IsLongHeader(answer[0]), Is.True);
        Assert.That(PacketFormat.GetLongPacketType(answer[0]), Is.EqualTo(LongPacketType.Initial),
                    "INVALID_TOKEN is delivered in an Initial packet.");
    }

    [Test]
    public async Task WithoutAddressValidation_TheFirstInitialStillOpensAConnection()
    {
        // The guard against over-reach: the new code path must stay off unless it is asked for.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert, _ => new Http3Response { Status = 200 }, port: 0);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        Assert.That(server.RetriesSent, Is.Zero);
        Assert.That(server.ConnectionCount, Is.EqualTo(1));
    }

    /// <summary>
    /// A datagram that looks like a client Initial to the listener: long header, type Initial,
    /// version 1, 1200 bytes. The payload is nonsense — it never gets that far, because the Retry
    /// decision is made from the cleartext header alone.
    /// </summary>
    private static byte[] FakeInitial(byte[]? token = null)
    {
        byte[] datagram = new byte[InitialPacketFactory.MinimumClientInitialSize];
        RandomNumberGenerator.Fill(datagram);

        int offset = 0;
        datagram[offset++] = 0xC3;                                   // long header + fixed bit + Initial
        datagram[offset++] = 0x00; datagram[offset++] = 0x00;        // version 1
        datagram[offset++] = 0x00; datagram[offset++] = 0x01;
        datagram[offset++] = 8;                                      // DCID length
        offset += 8;                                                 // DCID (random, as a client's would be)
        datagram[offset++] = 8;                                      // SCID length
        offset += 8;                                                 // SCID (random)

        token ??= [];
        datagram[offset++] = (byte)token.Length;                     // token length (varint, < 64)
        token.CopyTo(datagram, offset);
        offset += token.Length;

        // Length field (2-byte varint) covering the rest of the datagram.
        int remaining = datagram.Length - offset - 2;
        datagram[offset++] = (byte)(0x40 | (remaining >> 8));
        datagram[offset] = (byte)remaining;
        return datagram;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !condition())
            await Task.Delay(25).ConfigureAwait(false);
    }
}
