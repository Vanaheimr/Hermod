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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Streams;

/// <summary>
/// A connection lets go of a stream once its owner has released it and nothing is left on it
/// (<see cref="QuicEndpoint.ReleaseStream"/>), and what the stream counted for stays counted.
/// </summary>
/// <remarks>
/// A connection used to keep every stream it ever had, and every send pass walked all of them.
/// Letting go is the easy part; these tests are about what the stream still stood for once it is
/// gone: its ID, which a late frame may still name, its bytes in the connection window, and its
/// data still in flight.
/// </remarks>
[TestFixture]
public class StreamReleaseTests
{

    #region When a stream goes

    [Test]
    public void AReleasedStream_IsLetGo_OnceNothingIsLeftOnIt()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = Exchange(client, server, out QuicStream serverStream);
        ulong id = stream.Id.Value;

        server.ReleaseStream(id);
        Pump(client, server);
        Assert.That(server.Streams.ContainsKey(id), Is.False, "the server released a finished stream");
        Assert.That(server.IsStreamClosed(id), Is.True);
        Assert.That(client.Streams.ContainsKey(id), Is.True, "the client has not released its side");

        client.ReleaseStream(id);
        Pump(client, server);
        Assert.That(client.Streams.ContainsKey(id), Is.False);
        Assert.That(client.IsStreamClosed(id), Is.True);

        // The stream objects stay usable for whoever still holds them.
        Assert.That(serverStream.IsReceiveComplete, Is.True);
        Assert.That(stream.Read(), Is.Empty);
    }

    [Test]
    public void AReleasedStream_Stays_WhileSomethingIsLeftOnIt()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // The client has not finished: its final size is unknown.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1, 2, 3]);
        Pump(client, server);
        QuicStream serverStream = server.Streams[stream.Id.Value];
        server.ReleaseStream(stream.Id.Value);
        serverStream.Write([4]);
        serverStream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.Streams.ContainsKey(stream.Id.Value), Is.True, "the peer's final size is not known yet");

        stream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.IsStreamClosed(stream.Id.Value), Is.True);

        // And the other way round: released before its own FIN has gone out.
        QuicStream second = client.OpenBidirectionalStream();
        second.Write([5]);
        second.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        server.ReleaseStream(second.Id.Value);
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.Streams.ContainsKey(second.Id.Value), Is.True, "the server has not finished its side");

        server.Streams[second.Id.Value].Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.IsStreamClosed(second.Id.Value), Is.True);
    }

    [Test]
    public void AStreamNobodyReleases_Stays()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = Exchange(client, server);
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(server.Streams.ContainsKey(stream.Id.Value), Is.True);
        Assert.That(client.Streams.ContainsKey(stream.Id.Value), Is.True);
    }

    #endregion

    #region A closed stream's ID

    [Test]
    public void LateFrames_DoNotReopenAClosedPeerStream()
    {
        // A retransmission that crossed our acknowledgment names a stream we are done with. It
        // must not come back as a new one: HTTP/3 would take it for a new request.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = Exchange(client, server);
        ulong id = stream.Id.Value;
        server.ReleaseStream(id);
        Pump(client, server);
        Assert.That(server.IsStreamClosed(id), Is.True);
        Assert.That(server.TakeNewRequestStreams(), Is.EqualTo(new[] { id }));

        client.SendApplicationFrameForTest(new StreamFrame(id, 0, new byte[] { 1 }, true));
        client.SendApplicationFrameForTest(new ResetStreamFrame(id, 0x0c, 1));
        client.SendApplicationFrameForTest(new StopSendingFrame(id, 0x0c));
        client.SendApplicationFrameForTest(new MaxStreamDataFrame(id, 1_000_000));
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.False, "frames for a closed stream are no error");
        Assert.That(server.Streams.ContainsKey(id), Is.False, "the closed stream came back");
        Assert.That(server.TakeNewRequestStreams(), Is.Empty, "the closed stream was reported as new");
    }

    [Test]
    public void LateFrames_DoNotReopenAClosedLocalStream()
    {
        // The same from the opener's side, where RESET_STREAM and STOP_SENDING for a stream that is
        // not there used to mean "never opened", a STREAM_STATE_ERROR (RFC 9000 §19.4, §19.5).
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = Exchange(client, server);
        ulong id = stream.Id.Value;
        client.ReleaseStream(id);
        Pump(client, server);
        Assert.That(client.IsStreamClosed(id), Is.True);

        server.SendApplicationFrameForTest(new StreamFrame(id, 0, new byte[] { 2 }, true));
        server.SendApplicationFrameForTest(new ResetStreamFrame(id, 0x0c, 1));
        server.SendApplicationFrameForTest(new StopSendingFrame(id, 0x0c));
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(client.IsClosing, Is.False, "frames for a closed stream are no error");
        Assert.That(client.Streams.ContainsKey(id), Is.False, "the closed stream came back");
    }

    [Test]
    public void AStreamNotSeenYet_IsNotTakenForAClosedOne()
    {
        // Stream 8 arrives first, which opens 0 and 4 as well (RFC 9000 §3.2), and is gone before
        // their first frames arrive. They are new streams then, not late frames of closed ones.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream first  = client.OpenBidirectionalStream();
        QuicStream second = client.OpenBidirectionalStream();
        QuicStream third  = client.OpenBidirectionalStream();
        third.Write([3]);
        third.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.TakeNewRequestStreams(), Is.EqualTo(new[] { third.Id.Value }));

        server.Streams[third.Id.Value].Read();
        server.Streams[third.Id.Value].Finish();
        server.ReleaseStream(third.Id.Value);
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.IsStreamClosed(third.Id.Value), Is.True);
        Assert.That(server.IsStreamClosed(first.Id.Value), Is.False);

        first.Write([1]);
        first.Finish();
        second.Write([2]);
        second.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);

        Assert.That(server.TakeNewRequestStreams(), Is.EquivalentTo(new[] { first.Id.Value, second.Id.Value }));
        Assert.That(server.Streams[first.Id.Value].Read(),  Is.EqualTo(new byte[] { 1 }));
        Assert.That(server.Streams[second.Id.Value].Read(), Is.EqualTo(new byte[] { 2 }));
    }

    #endregion

    #region A closed stream's bytes

    [TestCase(true,  TestName = "ClosedStreams_KeepTheConnectionWindowOpen(read first)")]
    [TestCase(false, TestName = "ClosedStreams_KeepTheConnectionWindowOpen(released unread)")]
    public void ClosedStreams_KeepTheConnectionWindowOpen(bool readFirst)
    {
        // Ten times the connection window, one stream after another, each released once the server
        // has it. The window opens by what the peer's streams have consumed, and a closed stream is
        // no longer there to say how much that was. Released unread, its data counts as consumed,
        // as after a reset (RFC 9000 §4.5).
        var parameters = new TransportParameters
        {
            InitialMaxDataValue                 = 16_384,
            InitialMaxStreamDataBidiRemoteValue = 16_384,
        };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        for (int i = 0; i < 40; i++)
        {
            QuicStream stream = client.OpenBidirectionalStream();
            stream.Write(new byte[4096]);
            stream.Finish();
            for (int round = 0; round < 20 && !(server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer) && peer.Receive.FinReceived); round++)
                Pump(client, server);

            Assert.That(server.Streams.TryGetValue(stream.Id.Value, out QuicStream? serverStream) && serverStream.Receive.FinReceived,
                        Is.True, $"stream {i} did not get through: {i * 4} KiB sent through a 16 KiB window");
            if (readFirst)
                Assert.That(serverStream!.Read(), Has.Length.EqualTo(4096));
            serverStream!.Finish();
            server.ReleaseStream(stream.Id.Value);
            Pump(client, server);
        }
        Pump(client, server); // the last FIN out, then the stream gone

        Assert.That(server.Streams.Keys.Where(id => new StreamId(id).IsBidirectional), Is.Empty);
    }

    [Test]
    public void ClosedStreams_StillCountTowardsTheConnectionLimit()
    {
        // A peer that overruns the connection window is caught counting what it sent on every
        // stream (RFC 9000 §4.1), closed ones included.
        var parameters = new TransportParameters
        {
            InitialMaxDataValue                 = 16_384,
            InitialMaxStreamDataBidiRemoteValue = 1_048_576,
        };
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverParameters: parameters);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;
        client.OverrideConnSendLimitForTest(ulong.MaxValue);

        QuicStream first = client.OpenBidirectionalStream();
        first.Write(new byte[8192]);
        first.Finish();
        for (int round = 0; round < 6; round++)
            Pump(client, server);
        QuicStream serverFirst = server.Streams[first.Id.Value];
        Assert.That(serverFirst.Read(), Has.Length.EqualTo(8192));
        serverFirst.Finish();
        server.ReleaseStream(first.Id.Value);
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(server.IsStreamClosed(first.Id.Value), Is.True);

        // 1,000 bytes more than the window still has room for.
        ulong limit = server.LocalConnectionMaxDataForTest;
        QuicStream second = client.OpenBidirectionalStream();
        second.Write(new byte[(int)(limit - 8192) + 1000]);
        for (int round = 0; round < 20 && !server.IsClosing; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "the overrun went unnoticed");
        Assert.That(client.PeerCloseFrame?.ErrorCode, Is.EqualTo((ulong)TransportError.FlowControlError));
    }

    #endregion

    #region A closed stream's data in flight

    [Test]
    public void AResetStream_SendsNoneOfItsDataAgain_OnceGone()
    {
        // The data of a reset stream is not sent again (RFC 9000 §19.4), which the retransmission
        // path checks against the stream. Lost after the stream is gone, it would go out anyway.
        var clock = new FakeTimeProvider();
        var records = new List<string>();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(clock: clock, serverQlog: new QlogWriter(records.Add, isServer: true));
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1]);
        stream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server, clock);
        QuicStream serverStream = server.Streams[stream.Id.Value];
        serverStream.Read();

        // A response's first flight, lost.
        serverStream.Write(new byte[8_000]);
        Assert.That(server.GetDatagramsToSend(), Is.Not.Empty);
        int mark = records.Count;

        // The client stops it; the server resets and lets go. The acknowledgment of the reset
        // declares the flight lost, in the very pass that closes the stream.
        stream.AbortRead(0x10c);
        server.ReleaseStream(stream.Id.Value);
        for (int round = 0; round < 4; round++)
            Pump(client, server, clock);
        Assert.That(serverStream.Send.IsReset, Is.True);
        Assert.That(server.IsStreamClosed(stream.Id.Value), Is.True);

        // Nor does whatever loss recovery finds later include the stream's data.
        for (int round = 0; round < 10; round++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(250));
            Pump(client, server, clock);
        }

        string streamData = $"\"frame_type\":\"stream\",\"stream_id\":{stream.Id.Value},";
        Assert.That(records.Skip(mark).Where(r => r.Contains("quic:packet_sent") && r.Contains(streamData)), Is.Empty,
                    "data of the reset stream went out again");
    }

    #endregion

    #region Helpers

    private static QuicStream Exchange(QuicClientConnection client, QuicServerConnection server)
        => Exchange(client, server, out QuicStream _);

    // Client asks, server answers, both FINs out and read.
    private static QuicStream Exchange(QuicClientConnection client, QuicServerConnection server, out QuicStream serverStream)
    {
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1]);
        stream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        serverStream = server.Streams[stream.Id.Value];
        Assert.That(serverStream.Read(), Is.EqualTo(new byte[] { 1 }));
        serverStream.Write([2]);
        serverStream.Finish();
        for (int round = 0; round < 4; round++)
            Pump(client, server);
        Assert.That(stream.Read(), Is.EqualTo(new byte[] { 2 }));
        Assert.That(stream.IsReceiveComplete && serverStream.IsReceiveComplete, Is.True);
        return stream;
    }

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) HandshakeInProcess(
        TransportParameters? serverParameters = null,
        FakeTimeProvider?    clock            = null,
        QlogWriter?          serverQlog       = null)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        var server = new QuicServerConnection(cert, transportParameters: serverParameters, timeProvider: clock, qlog: serverQlog);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server, cert);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server, FakeTimeProvider? clock = null)
    {
        clock?.Advance(TimeSpan.FromMilliseconds(1));
        client.CheckLossDetectionTimeout();
        server.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    #endregion

}
