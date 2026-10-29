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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Recovery;

/// <summary>
/// An unused congestion window does not grow (RFC 9002 §7.8), over real QUIC connections.
/// </summary>
/// <remarks>
/// Every acknowledged byte used to grow the window, whether the sender had filled it or not. Light
/// traffic - request after small request - then grew it to megabytes: HTTP3ConformanceTests measured
/// ~4.7 MB on average on a loopback path that needs tens of kilobytes, and once a burst came along
/// it was all sent at once and sat in the receiver's queue, the server's RTT reaching 110 ms.
/// </remarks>
[TestFixture]
public class ApplicationLimitedTests
{
    [Test]
    public void ASenderThatNeverFillsItsWindow_DoesNotGrowIt()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var (client, server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        long afterHandshake = client.CongestionWindow;

        // 300 small messages, each acknowledged before the next: the window is never close to full.
        QuicStream stream = client.OpenBidirectionalStream();
        int received = 0;
        for (int i = 0; i < 300; i++)
        {
            stream.Write(new byte[100]);
            Pump(client, server);
            Pump(client, server);
            if (server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received += peer.Read().Length;
        }

        Assert.That(received, Is.EqualTo(300 * 100), "every message must arrive");
        Assert.That(client.CongestionWindow, Is.EqualTo(afterHandshake),
                    $"30 000 bytes acknowledged into a window of {afterHandshake} that was never filled");
    }

    [Test]
    public void ASenderThatFillsItsWindow_StillGrowsIt()
    {
        // The other half of §7.8, and the reason the rule needs the send path's view: a paced sender
        // keeps less than its window in flight on purpose, and that must not count as unused. A rule
        // that only compared bytes in flight with the window would stop slow start here.
        const int payloadSize = 1024 * 1024;

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var (client, server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        long afterHandshake = client.CongestionWindow;

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[payloadSize]);
        stream.Finish();

        int received = 0;
        for (int round = 0; round < 100_000 && received < payloadSize; round++)
        {
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();
            Pump(client, server);
            if (server.Streams.TryGetValue(stream.Id.Value, out QuicStream? peer))
                received += peer.Read().Length;
        }

        Assert.That(received, Is.EqualTo(payloadSize), "the whole payload must arrive");
        Assert.That(client.CongestionWindow, Is.GreaterThanOrEqualTo(8 * afterHandshake),
                    $"a megabyte through a window that started at {afterHandshake}");
    }

    private static (QuicClientConnection Client, QuicServerConnection Server) Handshaken(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        // Flow control well above the payloads: a flow-control-limited sender is application-limited
        // too (§7.8), and these tests are about the window, not the peer's credit.
        var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxDataValue = 8 * 1024 * 1024,
            InitialMaxStreamDataBidiRemoteValue = 8 * 1024 * 1024,
        });
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "the handshake must complete");
        // The handshake's own tail - HANDSHAKE_DONE, the acknowledgments of the last flights - must
        // be over before the window is read, or its last growth would land inside the measurement.
        for (int round = 0; round < 5; round++)
            Pump(client, server);
        return (client, server);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
