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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Packet size limit (RFC 9000 §14): no datagram may exceed the path MTU — anything above the
/// 1200 bytes guaranteed by §14.1 is dropped or fragmented by the path without notice. The control
/// frames of one send pass (retransmits, per-stream flow control, cancellations, a wide ACK) are
/// unbounded in number and therefore have to be spread across several packets.
/// </summary>
[TestFixture]
public class PacketSizeTests
{
    // ---- Building blocks ---------------------------------------------------------------------

    [Test]
    public void BufferWriter_Truncate_UndoesTheLastWrite()
    {
        var writer = new BufferWriter();
        try
        {
            writer.WriteBytes([1, 2, 3]);
            int mark = writer.Length;
            writer.WriteBytes([4, 5, 6, 7]);
            Assert.That(writer.Length, Is.EqualTo(7));

            writer.Truncate(mark);

            Assert.That(writer.Length, Is.EqualTo(3));
            Assert.That(writer.WrittenSpan.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            writer.WriteBytes([9]); // still usable afterwards
            Assert.That(writer.WrittenSpan.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 9 }));
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public void WriteUpTo_StopsAtTheBudget_AndReportsTheCursor()
    {
        // Each STOP_SENDING is only a few bytes; with a tight budget only some of them fit.
        List<Frame> frames = [.. Enumerable.Range(0, 20).Select(n => (Frame)new StopSendingFrame((ulong)(n * 4), 0x1234))];

        var writer = new BufferWriter();
        try
        {
            int cursor = FrameParser.WriteUpTo(ref writer, frames, 0, maxLength: 20);

            Assert.That(cursor, Is.GreaterThan(0), "At least one frame must fit.");
            Assert.That(cursor, Is.LessThan(frames.Count), "Not everything may fit into 20 bytes.");
            Assert.That(writer.Length, Is.LessThanOrEqualTo(20));

            // The remainder follows in the next packet — nothing is lost.
            int before = writer.Length;
            writer.Truncate(0);
            int cursor2 = FrameParser.WriteUpTo(ref writer, frames, cursor, maxLength: 1000);
            Assert.That(cursor2, Is.EqualTo(frames.Count));
            Assert.That(before, Is.GreaterThan(0));
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public void WriteUpTo_EmitsAnOversizedFrame_WhenItWouldBeTheFirstOne()
    {
        // Otherwise such a frame could never be sent at all.
        List<Frame> frames = [new CryptoFrame(0, new byte[500])];

        var writer = new BufferWriter();
        try
        {
            int cursor = FrameParser.WriteUpTo(ref writer, frames, 0, maxLength: 10);

            Assert.That(cursor, Is.EqualTo(1));
            Assert.That(writer.Length, Is.GreaterThan(10));
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Test]
    public void BuildAck_CapsTheNumberOfRanges()
    {
        // A pathological loss pattern (every second packet missing) would otherwise produce an ACK
        // frame that no longer fits into a packet.
        var space = new PacketNumberSpace();
        for (ulong pn = 0; pn < 400; pn += 2)
            space.RecordReceived(pn);

        AckFrame? ack = space.BuildAck();

        Assert.That(ack!.Ranges, Has.Count.LessThanOrEqualTo(32));
        // The newest are kept: the largest acknowledged stays the highest received packet number.
        Assert.That(ack.LargestAcknowledged, Is.EqualTo(398UL));
    }

    // ---- End to end over real QUIC connections -----------------------------------------------

    [Test]
    public void ManyStreamCancellationsInOnePass_StayWithinTheMtu()
    {
        // 300 streams, each aborted in both directions => ~600 control frames that WITHOUT the
        // splitting would all be packed into a single datagram of several kilobytes.
        const int streamCount = 300;

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxStreamsBidiValue = 1000,
        });
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        Pump(client, server); // drain the remainder of the handshake

        var streams = new List<QuicStream>();
        for (int n = 0; n < streamCount; n++)
        {
            QuicStream stream = client.OpenBidirectionalStream();
            stream.Write([1, 2, 3, 4]);
            streams.Add(stream);
        }
        foreach (QuicStream stream in streams)
        {
            stream.Reset(0x1234);   // RESET_STREAM
            stream.AbortRead(0x5678); // STOP_SENDING
        }

        // ONE send pass — everything queued above wants to go out here.
        IReadOnlyList<byte[]> datagrams = client.GetDatagramsToSend();

        Assert.That(datagrams, Is.Not.Empty);
        foreach (byte[] datagram in datagrams)
            Assert.That(datagram.Length, Is.LessThanOrEqualTo(QuicEndpoint.MaxDatagramSize),
                        $"Datagram of {datagram.Length} bytes exceeds the MTU " +
                        $"({QuicEndpoint.MaxDatagramSize}) — control frames not split?");
        Assert.That(datagrams, Has.Count.GreaterThan(1),
                    "The control frames must be spread across several packets.");

        // And nothing is lost on the way: the peer sees every cancellation.
        foreach (byte[] datagram in datagrams)
            server.ProcessDatagram(datagram);
        for (int round = 0; round < 20; round++)
            Pump(client, server);

        int resetsSeen = streams.Count(s => server.Streams.TryGetValue(s.Id.Value, out QuicStream? peer) && peer.IsResetByPeer);
        Assert.That(resetsSeen, Is.EqualTo(streamCount),
                    "Every RESET_STREAM must arrive despite being spread across packets.");
    }

    [Test]
    public void LargeHandshakeFlight_StaysWithinTheMtu()
    {
        // The handshake carries the biggest CRYPTO payloads (PQ-hybrid ClientHello, certificate
        // chain) — plus ACKs and retransmits on top of them.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                Assert.That(dg.Length, Is.LessThanOrEqualTo(QuicEndpoint.MaxDatagramSize),
                            $"Client datagram of {dg.Length} bytes exceeds the MTU.");
                server.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server.GetDatagramsToSend())
            {
                Assert.That(dg.Length, Is.LessThanOrEqualTo(QuicEndpoint.MaxDatagramSize),
                            $"Server datagram of {dg.Length} bytes exceeds the MTU.");
                client.ProcessDatagram(dg);
            }
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
