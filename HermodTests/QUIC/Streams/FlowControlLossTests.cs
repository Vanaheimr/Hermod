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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Streams;

/// <summary>
/// Credit that is lost on the way is sent again (RFC 9000 §13.3): MAX_STREAM_DATA, MAX_DATA and
/// MAX_STREAMS.
/// </summary>
/// <remarks>
/// A credit frame goes out once, when the window has drained far enough. Each of these frames was
/// left out of loss recovery, so a lost packet took the frame with it. The peer then sends up to
/// the old limit and stops, and the limit is never re-sent: the drain that would trigger the next
/// update needs the very bytes the peer is holding back. With the default 256 KiB stream window a
/// 300 KB upload needs exactly one MAX_STREAM_DATA, so one lost packet is enough to stall it.
///
/// Each test drops the server's datagrams from the one send pass that raised the limit, so the
/// first copy of the frame is lost for certain, and then delivers everything else.
/// </remarks>
[TestFixture]
public class FlowControlLossTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(1);

    [Test]
    public void ALostMaxStreamData_IsSentAgain()
    {
        const int payloadSize = 300_000; // more than the 256 KiB stream window, less than the 1 MiB connection window
        var time = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Handshake(time, new TransportParameters());
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[payloadSize]);
        stream.Finish();

        int received = Transfer(time, client, server, stream, payloadSize,
                                creditOf: srv => srv.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer) ? peer.Receive.MaxData : 0,
                                out bool dropped);

        Assert.That(dropped, Is.True, "precondition: the send pass that raised the stream limit was dropped");
        Assert.That(received, Is.EqualTo(payloadSize), "the upload must not stop at the stream limit it had before");
    }

    [Test]
    public void ALostMaxData_IsSentAgain()
    {
        const int payloadSize = 300_000;
        var time = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Handshake(time, new TransportParameters
        {
            InitialMaxDataValue                 = 256 * 1024, // the connection window is the one that runs out
            InitialMaxStreamDataBidiRemoteValue = 1024 * 1024,
        });
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[payloadSize]);
        stream.Finish();

        int received = Transfer(time, client, server, stream, payloadSize,
                                creditOf: srv => srv.LocalConnectionMaxDataForTest, out bool dropped);

        Assert.That(dropped, Is.True, "precondition: the send pass that raised the connection limit was dropped");
        Assert.That(received, Is.EqualTo(payloadSize), "the upload must not stop at the connection limit it had before");
    }

    [Test]
    public void ALostMaxStreams_IsSentAgain()
    {
        // A grant of one: the first finished stream buys the second, in a single MAX_STREAMS.
        var time = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Handshake(time, new TransportParameters
        {
            InitialMaxStreamsBidiValue = 1,
        });
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // A request and its response, both finished and read: the stream is done in both directions.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1]);
        stream.Finish();
        bool answered = false;
        bool dropped  = false;
        for (int i = 0; i < 5_000 && client.PeerStreamLimitsForTest.Bidi < 2; i++)
        {
            time.Advance(Step);
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            if (!answered && server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer) && peer.Receive.FinReceived)
            {
                peer.Read();
                peer.Write([1]);
                peer.Finish();
                answered = true;
            }
            ulong before = server.GrantedStreamLimitsForTest.Bidi;
            List<byte[]> fromServer = [.. server.GetDatagramsToSend()];
            if (!dropped && server.GrantedStreamLimitsForTest.Bidi > before)
                dropped = true;
            else
                foreach (byte[] dg in fromServer)
                    client.ProcessDatagram(dg);
            stream.Read();
        }

        Assert.That(dropped, Is.True, "precondition: the send pass that raised the stream limit was dropped");
        Assert.That(client.PeerStreamLimitsForTest.Bidi, Is.EqualTo(2UL), "the client must learn of its second stream");
    }

    // ---- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// Runs the upload on the simulated clock and drops the server's datagrams from the first send
    /// pass that raised <paramref name="creditOf"/>. Returns the bytes the server read.
    /// </summary>
    private static int Transfer(FakeTimeProvider time, QuicClientConnection client, QuicServerConnection server,
                                QuicStream stream, int payloadSize, Func<QuicServerConnection, ulong> creditOf,
                                out bool dropped)
    {
        dropped = false;
        int received = 0;
        // Five simulated seconds: the stall this guards against is permanent, and a lost frame is
        // declared lost within a few PTOs of the in-process RTT.
        for (int i = 0; i < 5_000 && received < payloadSize; i++)
        {
            time.Advance(Step);
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            if (server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received += peer.Read().Length;
            ulong before = creditOf(server);
            List<byte[]> fromServer = [.. server.GetDatagramsToSend()];
            if (!dropped && before > 0 && creditOf(server) > before)
                dropped = true;
            else
                foreach (byte[] dg in fromServer)
                    client.ProcessDatagram(dg);
        }
        return received;
    }

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) Handshake(FakeTimeProvider time,
                                                                                             TransportParameters serverParams)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: time);
        var server = new QuicServerConnection(cert, serverParams, timeProvider: time);
        client.Start();
        for (int round = 0; round < 50 && !client.HandshakeConfirmed; round++)
        {
            time.Advance(Step);
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server, cert);
    }
}
