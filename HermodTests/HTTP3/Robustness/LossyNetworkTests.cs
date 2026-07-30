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

using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Robustness;

/// <summary>
/// Impaired network: the whole stack under packet loss, reordering and duplication —
/// seeded and therefore deterministic, without an external tool or a real network.
/// <para>
/// Fills the biggest gap in the suite: everything else runs over a perfect in-process link, while
/// the sample's <c>--loss</c> only ever DROPS datagrams. Reordering and duplication were never
/// exercised, even though plenty of logic depends on them — packet-number reconstruction
/// (RFC 9000 §17.1), CRYPTO reassembly, stream reassembly, ACK ranges, the 0-RTT key retention
/// window (RFC 9001 §4.9.3) and RESET_STREAM_AT lowering the reliable size.
/// </para>
/// <para>
/// Time comes from a <see cref="FakeTimeProvider"/> that advances a fixed amount per round: without
/// it, loss recovery would never fire in-process (PTO is time-driven and the rounds run in
/// microseconds), so a lost packet would never be retransmitted.
/// </para>
/// </summary>
[TestFixture]
public class LossyNetworkTests
{
    private static readonly TimeSpan RoundDuration = TimeSpan.FromMilliseconds(5);

    // ---- The link itself ---------------------------------------------------------------------

    [Test]
    public void Link_WithoutImpairments_DeliversEverythingImmediatelyAndInOrder()
    {
        var link = new LossyLink(new Random(1));

        List<byte[]> delivered = link.Transfer([[1], [2], [3]]);

        Assert.That(delivered.Select(d => d[0]), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(link.Dropped, Is.Zero);
        Assert.That(link.PendingCount, Is.Zero);
    }

    [Test]
    public void Link_DeliversCopies_SoInPlaceUnprotectingCannotCorruptADuplicate()
    {
        var link = new LossyLink(new Random(1)) { DuplicateRate = 1.0, MaxDelayRounds = 0 };
        byte[] original = [1, 2, 3];

        List<byte[]> delivered = link.Transfer([original]);

        Assert.That(delivered, Has.Count.EqualTo(2));
        delivered[0][0] = 0xFF; // as ProcessDatagram does when removing header protection
        Assert.That(delivered[1][0], Is.EqualTo(1), "The duplicate must be its own buffer.");
        Assert.That(original[0], Is.EqualTo(1), "The sender's buffer must stay untouched.");
    }

    [Test]
    public void Link_Reordering_LetsLaterDatagramsOvertakeEarlierOnes()
    {
        // Reordering arises where it does in reality too: within ONE send pass, when part of the
        // batch is held back and the rest goes straight through.
        var link = new LossyLink(new Random(7)) { ReorderRate = 0.5, MaxDelayRounds = 3 };

        var delivered = new List<byte>();
        byte[][] batch = [[0], [1], [2], [3], [4], [5], [6], [7]];
        for (int round = 0; round < 8; round++)
            foreach (byte[] datagram in link.Transfer(round == 0 ? batch : []))
                delivered.Add(datagram[0]);

        Assert.That(delivered, Has.Count.EqualTo(batch.Length), "Nothing may be lost without a drop rate.");
        Assert.That(delivered.Order(), Is.EqualTo(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }), "… and nothing duplicated.");
        Assert.That(link.Delayed, Is.GreaterThan(0));
        Assert.That(delivered, Is.Not.EqualTo(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }), "Reordering must be visible.");
        Assert.That(link.DeliveredOutOfOrder, Is.GreaterThan(0));
    }

    [Test]
    public void SameSeed_ReproducesTheSameImpairments()
    {
        static (int Dropped, int Delayed, int Duplicated) Run(int seed)
        {
            var net = new LossyNetwork(seed, dropRate: 0.2, duplicateRate: 0.1, reorderRate: 0.3);
            for (int round = 0; round < 50; round++)
                net.ClientToServer.Transfer([[(byte)round], [(byte)(round + 1)]]);
            return (net.ClientToServer.Dropped, net.ClientToServer.Delayed, net.ClientToServer.Duplicated);
        }

        Assert.That(Run(4711), Is.EqualTo(Run(4711)));
        Assert.That(Run(4711), Is.Not.EqualTo(Run(1234)), "A different seed must impair differently.");
    }

    // ---- The stack under impairment ----------------------------------------------------------

    [Test]
    public void Handshake_Completes_UnderTwentyPercentPacketLoss()
    {
        var net = new LossyNetwork(seed: 20260724, dropRate: 0.2);
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        for (int round = 0; round < 400 && !client.HandshakeConfirmed; round++)
            Pump(client, server, net, clock);

        Assert.That(client.HandshakeConfirmed, Is.True,
                    $"The handshake must survive the loss. {net}");
        Assert.That(net.ClientToServer.Dropped + net.ServerToClient.Dropped, Is.GreaterThan(0),
                    "The test only means something if datagrams were actually dropped.");
    }

    [Test]
    public void PureReordering_BreaksNothing()
    {
        // No loss at all — only the ORDER is scrambled. Exercises packet-number reconstruction,
        // CRYPTO reassembly and stream reassembly on their own, without retransmissions masking a bug.
        var net = new LossyNetwork(seed: 99, reorderRate: 0.6, maxDelayRounds: 4);
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        for (int round = 0; round < 200 && !client.HandshakeConfirmed; round++)
            Pump(client, server, net, clock);
        Assert.That(client.HandshakeConfirmed, Is.True, $"Handshake under reordering. {net}");

        byte[] payload = RandomNumberGenerator.GetBytes(60_000);
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(payload);
        stream.Finish();

        byte[] received = Drain(client, server, stream, payload.Length, net, clock);

        Assert.That(received, Is.EqualTo(payload), "Reordered stream data must reassemble byte-exactly.");
        Assert.That(net.ClientToServer.DeliveredOutOfOrder + net.ServerToClient.DeliveredOutOfOrder,
                    Is.GreaterThan(0), "Datagrams must actually have arrived out of order.");
        Assert.That(net.ClientToServer.Dropped + net.ServerToClient.Dropped, Is.Zero);
    }

    [Test]
    public void Duplicates_AreProcessedIdempotently()
    {
        // Every datagram arrives twice. Duplicate packet numbers must neither corrupt state nor
        // provoke a connection error.
        var net = new LossyNetwork(seed: 5, duplicateRate: 1.0, maxDelayRounds: 2);
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        for (int round = 0; round < 200 && !client.HandshakeConfirmed; round++)
            Pump(client, server, net, clock);
        Assert.That(client.HandshakeConfirmed, Is.True, $"Handshake despite duplicates. {net}");

        byte[] payload = RandomNumberGenerator.GetBytes(30_000);
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(payload);
        stream.Finish();

        byte[] received = Drain(client, server, stream, payload.Length, net, clock);

        Assert.That(received, Is.EqualTo(payload));
        Assert.That(client.IsClosing || client.IsDraining, Is.False, "Duplicates must not close the connection.");
        Assert.That(server.IsClosing || server.IsDraining, Is.False);
        Assert.That(net.ClientToServer.Duplicated, Is.GreaterThan(0));
    }

    [Test]
    public void LargeTransfer_ArrivesByteExact_UnderLossReorderingAndDuplication()
    {
        // Everything at once — the realistic worst case.
        var net = new LossyNetwork(seed: 2026, dropRate: 0.05, duplicateRate: 0.05, reorderRate: 0.2, maxDelayRounds: 3);
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        for (int round = 0; round < 400 && !client.HandshakeConfirmed; round++)
            Pump(client, server, net, clock);
        Assert.That(client.HandshakeConfirmed, Is.True, $"Handshake. {net}");

        byte[] payload = RandomNumberGenerator.GetBytes(120_000);
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(payload);
        stream.Finish();

        byte[] received = Drain(client, server, stream, payload.Length, net, clock);

        Assert.That(received, Is.EqualTo(payload), $"Payload must arrive byte-exactly. {net}");
        Assert.Pass(net.ToString());
    }

    [Test]
    public void Http3Request_Succeeds_UnderLossAndReordering()
    {
        // The complete stack including QPACK and the HTTP/3 framing.
        var net = new LossyNetwork(seed: 31337, dropRate: 0.1, reorderRate: 0.25);
        var clock = new FakeTimeProvider();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        byte[] body = RandomNumberGenerator.GetBytes(40_000);
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = body };

        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;

        for (int round = 0; round < 1500 && response is null; round++)
        {
            clock.Advance(RoundDuration);
            client.CheckTimeouts();
            server.CheckTimeouts();

            foreach (byte[] dg in net.ClientToServer.Transfer(client.GetDatagramsToSend()))
                server.ProcessDatagram(dg);
            foreach (byte[] dg in net.ServerToClient.Transfer(server.GetDatagramsToSend()))
                client.ProcessDatagram(dg);

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(Http3Request.Get("localhost", "/lossy"));
                requestSent = true;
            }
            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }

        Assert.That(response, Is.Not.Null, $"The response must arrive despite the impairment. {net}");
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.Body, Is.EqualTo(body), "The body must be byte-exact.");
        Assert.Pass(net.ToString());
    }

    // ---- Regressions the impaired link uncovered ---------------------------------------------

    [Test]
    public void LostHandshakeDone_IsRetransmitted_AndDoesNotDeadlockTheHandshake()
    {
        // RFC 9000 §13.3: HANDSHAKE_DONE is delivered reliably, i.e. repeated on loss. It used not to
        // be tracked as retransmittable at all, so a lost one was gone for good — and a
        // Handshake-level probe cannot reach the server either, since it discards those keys on
        // completion. (The client can additionally confirm via a 1-RTT ACK per RFC 9001 §4.1.2, which
        // is why the retransmission is checked DIRECTLY here rather than only via its effect.)
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        int swallowed = 0;
        for (int round = 0; round < 400 && !client.HandshakeConfirmed; round++)
        {
            clock.Advance(RoundDuration);
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();

            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);

            foreach (byte[] dg in server.GetDatagramsToSend())
            {
                // Swallow the server's first datagrams after completion — that is where the
                // HANDSHAKE_DONE sits.
                if (server.HandshakeComplete && swallowed < 3)
                {
                    swallowed++;
                    continue;
                }
                client.ProcessDatagram(dg);
            }
        }

        Assert.That(swallowed, Is.GreaterThan(0), "The test only means something if something was swallowed.");
        Assert.That(server.HandshakeDoneSentCountForTest, Is.GreaterThan(1),
                    "The server must put HANDSHAKE_DONE on the wire again after the loss (RFC 9000 §13.3).");
        Assert.That(client.HandshakeConfirmed, Is.True, "And the handshake must get confirmed.");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]   // used to deadlock: HANDSHAKE_DONE lost
    [TestCase(8)]
    [TestCase(14)]  // used to deadlock: client stopped probing
    [TestCase(25)]
    [TestCase(47)]
    [TestCase(50)]
    [TestCase(72)]
    public void Seed_SurvivesLossReorderingAndDuplication(int seed)
    {
        // Seed sweep as a permanent regression: each of these once produced a stalled handshake.
        var net = new LossyNetwork(seed, dropRate: 0.18, duplicateRate: 0.08, reorderRate: 0.4, maxDelayRounds: 6);
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert, FakeTimeProvider clock) = Pair();
        using ServerCertificate certGuard = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // Generous budget: the PTO backs off exponentially, so a bad run legitimately needs seconds.
        for (int round = 0; round < 4000 && !client.HandshakeConfirmed; round++)
            Pump(client, server, net, clock);
        Assert.That(client.HandshakeConfirmed, Is.True, $"Handshake (seed {seed}). {net}");

        byte[] payload = new byte[30_000];
        new Random(seed).NextBytes(payload);
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(payload);
        stream.Finish();

        byte[] received = Drain(client, server, stream, payload.Length, net, clock);

        Assert.That(received, Is.EqualTo(payload), $"Payload (seed {seed}). {net}");
        Assert.That(client.IsClosing || client.IsDraining, Is.False, $"Connection stayed alive? {net}");
    }

    // ---- Helpers -----------------------------------------------------------------------------

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate, FakeTimeProvider) Pair()
    {
        var clock = new FakeTimeProvider();
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxDataValue = 4 * 1024 * 1024,
            InitialMaxStreamDataBidiRemoteValue = 4 * 1024 * 1024,
        }, timeProvider: clock);
        client.Start();
        return (client, server, cert, clock);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server, LossyNetwork net, FakeTimeProvider clock)
    {
        // Time must advance, otherwise loss recovery (PTO, RFC 9002 §6.2) never fires.
        clock.Advance(RoundDuration);
        client.CheckLossDetectionTimeout();
        server.CheckLossDetectionTimeout();

        foreach (byte[] dg in net.ClientToServer.Transfer(client.GetDatagramsToSend()))
            server.ProcessDatagram(dg);
        foreach (byte[] dg in net.ServerToClient.Transfer(server.GetDatagramsToSend()))
            client.ProcessDatagram(dg);
    }

    private static byte[] Drain(QuicClientConnection client, QuicServerConnection server, QuicStream stream,
                                int expectedLength, LossyNetwork net, FakeTimeProvider clock)
    {
        var received = new List<byte>(expectedLength);
        for (int round = 0; round < 20_000 && received.Count < expectedLength; round++)
        {
            Pump(client, server, net, clock);
            if (server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received.AddRange(peer.Read());
        }
        return [.. received];
    }
}
