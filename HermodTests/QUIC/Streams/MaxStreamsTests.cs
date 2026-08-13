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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Streams;

/// <summary>
/// Stream credit renewal: MAX_STREAMS and STREAMS_BLOCKED (RFC 9000 §4.6, §19.11, §19.14).
/// </summary>
/// <remarks>
/// These exist because of a defect nothing in this suite could have caught. Stream IDs are consumed
/// and never recycled, so a limit set only by the transport parameter is a budget for the whole life
/// of a connection: MAX_STREAMS was parsed and logged but never sent, and a connection therefore
/// stopped dead after initial_max_streams_bidi requests — 100 by default.
///
/// It stayed invisible because every test here worked in single digits of streams and both ends were
/// ours. It was found from outside, by driving a running server with msquic until it stalled. Hence
/// the first test below: it opens more streams than the initial grant, which no other test does.
/// </remarks>
[TestFixture]
public class MaxStreamsTests
{

    #region A connection outlives its initial stream grant

    [Test]
    public void ManyStreams_PastTheInitialGrant_KeepFlowing()
    {
        // A small grant so the boundary is crossed in a test-sized number of round trips; the
        // mechanism is the same one that fails at 100 with the default.
        var parameters = new TransportParameters { InitialMaxStreamsBidiValue = 4 };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // Four times the grant. Each stream is opened, written, finished and drained by the server,
        // which is what should hand the credit back.
        for (int i = 0; i < 16; i++)
        {
            QuicStream stream = client.OpenBidirectionalStream();
            stream.Write([(byte) i]);
            stream.Finish();
            for (int round = 0; round < 6; round++)
                Pump(client, server);

            Assert.That(server.IsClosing, Is.False,
                        $"the server closed the connection while handling stream {i}");
            Assert.That(server.Streams.ContainsKey(stream.Id.Value), Is.True,
                        $"stream {i} never reached the server");
            Assert.That(server.Streams[stream.Id.Value].Read(), Is.EqualTo(new[] { (byte) i }));

            // Answer and finish, so the bidirectional stream is done in both directions.
            QuicStream serverStream = server.Streams[stream.Id.Value];
            serverStream.Write([(byte) i]);
            serverStream.Finish();
            for (int round = 0; round < 6; round++)
                Pump(client, server);
        }

        // The 17th stream would be index 16 — far beyond the 4 the transport parameter granted.
        Assert.That(client.IsClosing, Is.False);
        Assert.That(server.IsClosing, Is.False);
    }

    #endregion

    #region Credit is granted, once per stream, and only when the stream is really done

    [Test]
    public void FinishedStreams_GrantFreshCredit()
    {
        var parameters = new TransportParameters { InitialMaxStreamsBidiValue = 4 };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        Assert.That(server.GrantedStreamLimitsForTest.Bidi, Is.EqualTo(4UL),
                    "the transport parameter is the first grant");

        for (int i = 0; i < 4; i++)
        {
            QuicStream stream = client.OpenBidirectionalStream();
            stream.Write([1]);
            stream.Finish();
            for (int round = 0; round < 4; round++)
                Pump(client, server);
            QuicStream serverStream = server.Streams[stream.Id.Value];
            serverStream.Read();
            serverStream.Finish();
            for (int round = 0; round < 4; round++)
                Pump(client, server);
        }

        Assert.That(server.GrantedStreamLimitsForTest.Bidi, Is.GreaterThan(4UL),
                    "four finished streams should have bought four more");
        // And the client learned about it, which is the half that actually matters.
        Assert.That(client.PeerStreamLimitsForTest.Bidi, Is.EqualTo(server.GrantedStreamLimitsForTest.Bidi));
    }

    [Test]
    public void UnfinishedStream_GrantsNothing()
    {
        var parameters = new TransportParameters { InitialMaxStreamsBidiValue = 4 };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // Data arrives, but the client never sends FIN and the server never answers: the stream is
        // live in both directions. Crediting it here would let the peer open a stream against
        // capacity that is still in use.
        for (int i = 0; i < 4; i++)
        {
            QuicStream stream = client.OpenBidirectionalStream();
            stream.Write([1]);
            for (int round = 0; round < 6; round++)
                Pump(client, server);
        }

        Assert.That(server.GrantedStreamLimitsForTest.Bidi, Is.EqualTo(4UL),
                    "no stream has finished, so there is nothing to hand back");
    }

    #endregion

    #region Frame semantics

    [Test]
    public void MaxStreams_IsATotal_AndReorderingCannotLowerIt()
    {
        // §19.11: the value is cumulative, and frames may arrive out of order — a stale smaller
        // total must not undo a larger one that already landed.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // Sent through the real send/receive path rather than injected, so the frame is encoded,
        // packed, protected and parsed exactly as one off the wire would be.
        server.SendApplicationFrameForTest(new MaxStreamsFrame(Bidirectional: true, 500));
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        Assert.That(client.PeerStreamLimitsForTest.Bidi, Is.EqualTo(500UL));

        server.SendApplicationFrameForTest(new MaxStreamsFrame(Bidirectional: true, 300));
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        Assert.That(client.PeerStreamLimitsForTest.Bidi, Is.EqualTo(500UL),
                    "the larger total wins, not the later frame");

        Assert.That(client.IsClosing, Is.False);
    }

    [Test]
    public void MaxStreams_AboveTwoToThe60_IsAFrameEncodingError()
    {
        // §4.6: a count that large cannot be turned into a stream ID that fits a varint.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        server.SendApplicationFrameForTest(new MaxStreamsFrame(Bidirectional: true, (1UL << 60) + 1));
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        Assert.That(client.IsClosing, Is.True);
    }

    [Test]
    public void StreamsBlocked_ReleasesCreditThatWasWaitingForTheBatchingThreshold()
    {
        // Credit is normally held back until it is worth a frame. A peer saying it is blocked is
        // exactly the case where holding back stops being an optimisation.
        var parameters = new TransportParameters { InitialMaxStreamsBidiValue = 64 };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // One finished stream: one credit, far below the threshold of 64/8 = 8.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1]);
        stream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        QuicStream serverStream = server.Streams[stream.Id.Value];
        serverStream.Read();
        serverStream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(server.GrantedStreamLimitsForTest.Bidi, Is.EqualTo(64UL),
                    "one stream is not worth a frame yet");

        client.SendApplicationFrameForTest(new StreamsBlockedFrame(Bidirectional: true, 64));
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(server.GrantedStreamLimitsForTest.Bidi, Is.GreaterThan(64UL),
                    "being told the peer is stuck should release the held credit at once");
    }

    #endregion

    #region Helpers

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) HandshakeInProcess(
        TransportParameters? serverParameters = null)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert, transportParameters: serverParameters);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server, cert);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    #endregion

}
