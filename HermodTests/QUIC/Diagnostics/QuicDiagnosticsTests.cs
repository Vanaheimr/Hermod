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

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Diagnostics;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Diagnostics;

/// <summary>
/// The observability surface of the QUIC layer: the <see cref="QuicMetrics"/> instruments and the
/// <see cref="QuicEventSource"/> events. Both are driven by a real in-process handshake rather than
/// by calling the instruments directly, so the wiring is what is under test, not the API.
/// </summary>
[TestFixture]
public class QuicDiagnosticsTests
{

    #region Helpers

    /// <summary>
    /// Collects every measurement of the QUIC meter until disposed.
    /// </summary>
    private sealed class Measurements : IDisposable
    {

        private readonly MeterListener listener = new();
        private readonly Lock @lock = new();
        private readonly Dictionary<String, Double> totals = [];

        public Measurements()
        {
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == QuicMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<Int64>((instrument, value, tags, _) => Record(instrument.Name, value));
            listener.SetMeasurementEventCallback<Double>((instrument, value, tags, _) => Record(instrument.Name, value));
            listener.Start();
        }

        private void Record(String name, Double value)
        {
            lock (@lock)
                totals[name] = totals.TryGetValue(name, out Double sum) ? sum + value : value;
        }

        /// <summary>
        /// Sum of everything recorded for an instrument; 0 when it never fired.
        /// </summary>
        public Double this[String name]
        {
            get { lock (@lock) return totals.TryGetValue(name, out Double sum) ? sum : 0; }
        }

        public Boolean Saw(String name) { lock (@lock) return totals.ContainsKey(name); }

        public void Dispose() => listener.Dispose();

    }

    /// <summary>
    /// Collects the events of the QUIC EventSource until disposed.
    /// </summary>
    private sealed class Events : EventListener
    {

        private readonly Lock @lock = new();
        private readonly List<String> names = [];

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Vanaheimr-Hermod-Quic")
                EnableEvents(eventSource, EventLevel.Verbose);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            lock (@lock)
                names.Add(eventData.EventName ?? "");
        }

        public IReadOnlyList<String> Names { get { lock (@lock) return [.. names]; } }

    }

    /// <summary>
    /// Runs a complete handshake in process, opens a stream and exchanges some data.
    /// </summary>
    private static void RunConnection()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Exchange(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "The handshake must complete.");

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1, 2, 3, 4]);
        for (int round = 0; round < 5; round++)
            Exchange(client, server);

        client.Close();
        Exchange(client, server);
    }

    private static void Exchange(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] datagram in client.GetDatagramsToSend())
            server.ProcessDatagram(datagram);
        foreach (byte[] datagram in server.GetDatagramsToSend())
            client.ProcessDatagram(datagram);
    }

    #endregion

    #region Metrics

    [Test]
    public void ARealConnection_FeedsTheInstruments()
    {
        using var measurements = new Measurements();

        RunConnection();

        Assert.Multiple(() =>
        {
            Assert.That(measurements["quic.handshakes"], Is.EqualTo(2),
                        "One handshake per side — client and server each install their own 1-RTT keys.");
            Assert.That(measurements["quic.bytes.received"], Is.GreaterThan(0));
            Assert.That(measurements["quic.bytes.sent"], Is.GreaterThan(0));
            Assert.That(measurements["quic.packets.sent"], Is.GreaterThan(0));
            Assert.That(measurements["quic.streams.opened"], Is.GreaterThan(0));
            Assert.That(measurements.Saw("quic.handshake.duration"), Is.True);
            Assert.That(measurements.Saw("quic.rtt.smoothed"), Is.True, "An ACK must produce an RTT sample.");
            Assert.That(measurements.Saw("quic.congestion_window"), Is.True);
        });
    }

    [Test]
    public void ActiveConnections_ReturnsToItsStartingValueAfterDisposal()
    {
        using var measurements = new Measurements();

        RunConnection(); // both connections are disposed by the using blocks inside

        // Two connections up, two down. The net is what matters: a leaked +1 is exactly the bug an
        // active-connection gauge is prone to.
        Assert.That(measurements["quic.connections.active"], Is.Zero);
    }

    [Test]
    public void BytesSent_CountsEveryDatagramHandedOut()
    {
        using var measurements = new Measurements();

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);

        client.Start();
        long handedOut = 0;
        foreach (byte[] datagram in client.GetDatagramsToSend())
            handedOut += datagram.Length;

        Assert.That(handedOut, Is.GreaterThan(0), "The Initial flight must produce something.");
        Assert.That(measurements["quic.bytes.sent"], Is.EqualTo(handedOut),
                    "The counter must match the bytes the caller actually received.");
    }

    /// <summary>
    /// The loss path is the one whose wiring changed: the recovery hook used to be attached only
    /// when a qlog was configured, so without one nothing counted losses at all. A seeded lossy link
    /// makes that reproducible instead of hoping for a bad network.
    /// </summary>
    [Test]
    public void OverALossyLink_LossesAndRetransmissionsAreCounted()
    {
        using var measurements = new Measurements();

        var network = new LossyNetwork(seed: 20260729, dropRate: 0.3);
        var clock = new FakeTimeProvider();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        using var server = new QuicServerConnection(cert, timeProvider: clock);

        client.Start();
        for (int round = 0; round < 400 && !client.HandshakeConfirmed; round++)
        {
            // Time has to advance, otherwise the PTO (RFC 9002 §6.2) never fires and a dropped
            // packet is never retransmitted — the rounds themselves take microseconds.
            clock.Advance(TimeSpan.FromMilliseconds(10));
            client.CheckLossDetectionTimeout();
            server.CheckLossDetectionTimeout();

            foreach (byte[] datagram in network.ClientToServer.Transfer(client.GetDatagramsToSend()))
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in network.ServerToClient.Transfer(server.GetDatagramsToSend()))
                client.ProcessDatagram(datagram);
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "The handshake must survive 30 % loss.");
        Assert.That(network.ClientToServer.Dropped + network.ServerToClient.Dropped, Is.GreaterThan(0),
                    "The link must actually have dropped something.");
        Assert.That(measurements["quic.packets.lost"], Is.GreaterThan(0), "Losses must reach the counter.");
        Assert.That(measurements["quic.frames.retransmitted"], Is.GreaterThan(0));
    }

    #endregion

    #region Events

    [Test]
    public void ARealConnection_WritesTheLifecycleEvents()
    {
        using var events = new Events();

        RunConnection();

        IReadOnlyList<String> names = events.Names;
        Assert.Multiple(() =>
        {
            Assert.That(names, Has.Some.EqualTo("ConnectionStarted"));
            Assert.That(names, Has.Some.EqualTo("HandshakeCompleted"));
            Assert.That(names, Has.Some.EqualTo("ConnectionClosed"));
        });
    }

    #endregion

    #region Cost when nobody is listening

    [Test]
    public void WithoutAListener_TheInstrumentsReportThemselvesDisabled()
    {
        // This is the whole zero-cost claim: the guards in the hot paths read exactly this flag, so
        // an unobserved connection never builds a tag or records a measurement.
        Assert.Multiple(() =>
        {
            Assert.That(QuicMetrics.PacketsSent.Enabled,       Is.False);
            Assert.That(QuicMetrics.BytesReceived.Enabled,     Is.False);
            Assert.That(QuicMetrics.SmoothedRtt.Enabled,       Is.False);
            Assert.That(QuicMetrics.CongestionWindow.Enabled,  Is.False);
        });
    }

    [Test]
    public void AListenerFlipsTheInstrumentsOn_AndDisposingFlipsThemBackOff()
    {
        Assert.That(QuicMetrics.PacketsSent.Enabled, Is.False);

        using (var measurements = new Measurements())
            Assert.That(QuicMetrics.PacketsSent.Enabled, Is.True, "A subscribed listener must enable the instrument.");

        Assert.That(QuicMetrics.PacketsSent.Enabled, Is.False, "Disposal must restore the free path.");
    }

    #endregion

}
