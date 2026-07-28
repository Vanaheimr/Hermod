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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Auto-tuning of the receive-side flow-control window (phase 9): the <see cref="ReceiveWindowTuner"/>
/// grows the window when the sender drains it faster than ~2×RTT, and caps at the limit.
/// </summary>
[TestFixture]
public class WindowAutoTuningTests
{
    private const long Ms = TimeSpan.TicksPerMillisecond;

    // ---- Unit: tuner heuristic -------------------------------------------------------------

    [Test]
    public void Tuner_DoublesWindow_WhenUpdatesArriveFasterThanTwoRtt()
    {
        var tuner = new ReceiveWindowTuner(initialSize: 64 * 1024, limit: 1024 * 1024);
        long rtt = 100 * Ms;

        // The first update only establishes the timestamp (no growth without a reference point).
        Assert.That(tuner.NoteWindowUpdate(nowTicks: 0, smoothedRttTicks: rtt), Is.False);
        Assert.That(tuner.Size, Is.EqualTo(64UL * 1024));

        // Next update after 50 ms (< 2×RTT = 200 ms) ⇒ double the window.
        Assert.That(tuner.NoteWindowUpdate(nowTicks: 50 * Ms, smoothedRttTicks: rtt), Is.True);
        Assert.That(tuner.Size, Is.EqualTo(128UL * 1024));

        // Fast again ⇒ double again.
        Assert.That(tuner.NoteWindowUpdate(nowTicks: 90 * Ms, smoothedRttTicks: rtt), Is.True);
        Assert.That(tuner.Size, Is.EqualTo(256UL * 1024));
    }

    [Test]
    public void Tuner_DoesNotGrow_WhenUpdatesAreSlow()
    {
        var tuner = new ReceiveWindowTuner(initialSize: 64 * 1024, limit: 1024 * 1024);
        long rtt = 100 * Ms;

        tuner.NoteWindowUpdate(0, rtt);
        // 500 ms later (> 2×RTT) ⇒ the window is NOT the bottleneck, no growth.
        Assert.That(tuner.NoteWindowUpdate(500 * Ms, rtt), Is.False);
        Assert.That(tuner.Size, Is.EqualTo(64UL * 1024));
    }

    [Test]
    public void Tuner_CapsAtLimit()
    {
        var tuner = new ReceiveWindowTuner(initialSize: 400 * 1024, limit: 1024 * 1024);
        long rtt = 100 * Ms;
        long t = 0;
        tuner.NoteWindowUpdate(t, rtt);
        for (int i = 0; i < 10; i++)
            tuner.NoteWindowUpdate(t += 10 * Ms, rtt); // always fast ⇒ grow up to the cap
        Assert.That(tuner.Size, Is.EqualTo(1024UL * 1024)); // exactly at the limit, not above
    }

    [Test]
    public void Tuner_LimitNeverBelowInitialSize()
    {
        // An absurdly small limit is raised to the initial size (the window must never shrink).
        var tuner = new ReceiveWindowTuner(initialSize: 256 * 1024, limit: 1024);
        Assert.That(tuner.Limit, Is.EqualTo(256UL * 1024));
    }

    // ---- Integration (QUIC): the connection window grows under load ------------------------

    [Test]
    public void ConnectionReceiveWindow_GrowsUnderSustainedTransfer()
    {
        // Small initial value (64 KiB) — a sustained transfer must tune the window up.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            HandshakeInProcess(serverInitialMaxData: 64 * 1024);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        ulong windowBefore = server.ConnectionReceiveWindowSize;
        Assert.That(windowBefore, Is.EqualTo(64UL * 1024));

        // The client pushes lots of data; the server reads it (⇒ consumed credit ⇒ window updates),
        // and because in-process every pump is "immediate" (RTT ≪ 2×SmoothedRtt), the window tunes up.
        QuicStream stream = client.OpenBidirectionalStream();
        QuicStream? serverStream = null;
        for (int round = 0; round < 400 && server.ConnectionReceiveWindowSize <= windowBefore; round++)
        {
            stream.Write(new byte[16 * 1024]);
            Pump(client, server);
            serverStream ??= server.Streams.TryGetValue(stream.Id.Value, out QuicStream? ss) ? ss : null;
            serverStream?.Read(); // consume so that new credit is granted
        }

        Assert.That(server.ConnectionReceiveWindowSize, Is.GreaterThan(windowBefore),
            "The connection receive window must auto-tune under sustained load.");
        Assert.That(server.IsClosing, Is.False);
        Assert.That(client.IsClosing, Is.False);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) HandshakeInProcess(ulong serverInitialMaxData)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var serverParams = new TransportParameters
        {
            InitialMaxDataValue = serverInitialMaxData,
            InitialMaxStreamDataBidiRemoteValue = serverInitialMaxData,
        };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert, serverParams);
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
}
