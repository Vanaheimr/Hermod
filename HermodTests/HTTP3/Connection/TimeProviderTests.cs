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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// TimeProvider support: all connection timers (idle timeout, closing deadline, keep-alive) run on
/// an injectable <see cref="TimeProvider"/> (default: the system clock). With a
/// <see cref="FakeTimeProvider"/> the previously sleep-based scenarios become fully deterministic —
/// no <c>Thread.Sleep</c>, time only moves via <see cref="FakeTimeProvider.Advance"/>.
/// </summary>
[TestFixture]
public class TimeProviderTests
{
    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static (QuicClientConnection Client, QuicServerConnection Server) Handshaken(
        ServerCertificate cert, FakeTimeProvider clock, TransportParameters? serverParams = null)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        // PMTU discovery off for the same reason the ACK flush below exists: its probes are
        // ack-eliciting, so under a fake clock that jumps in 100-ms steps each one contributes a
        // 100-ms RTT sample, and the 3×PTO idle floor then swallows the margins these tests measure.
        // These tests are about timers, not about path MTU.
        var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock,
                                              maxDatagramSizeCeiling: QuicEndpoint.MaxDatagramSize);
        var server = new QuicServerConnection(cert, serverParams, timeProvider: clock,
                                              maxDatagramSizeCeiling: QuicEndpoint.MaxDatagramSize);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        // Flush trailing ACKs at fake time 0: otherwise the first Advance would deliver them "late"
        // and inflate the RTT samples — which (correctly, RFC 9000 §10.1) raises the 3×PTO idle floor.
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        return (client, server);
    }

    [Test]
    public void IdleTimeout_Expires_WhenOnlyTheFakeClockAdvances()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var clock = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server) =
            Handshaken(cert, clock, new TransportParameters { MaxIdleTimeoutMs = 300 });
        using var _ = client;
        using var __ = server;

        // Real time does not matter anymore: without Advance the connection stays open …
        server.CheckIdleTimeout();
        Assert.That(server.IsIdleTimedOut, Is.False);

        // … and 400 fake milliseconds later (> 300 ms idle timeout) it is silently closed.
        clock.Advance(TimeSpan.FromMilliseconds(400));
        server.CheckIdleTimeout();
        Assert.That(server.IsIdleTimedOut, Is.True, "The idle timeout must fire on the fake clock alone.");
        Assert.That(server.GetDatagramsToSend(), Is.Empty); // closed silently ⇒ no more datagrams
    }

    [Test]
    public void ClosingConnection_ReachesClosed_AfterFakeCloseTimeout()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var clock = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert, clock);
        using var _ = client;
        using var __ = server;

        client.Close();
        Assert.That(client.IsClosing, Is.True);
        Assert.That(client.IsClosed, Is.False);

        // After more than 3×PTO of fake time the final closed state follows (RFC 9000 §10.2).
        clock.Advance(TimeSpan.FromSeconds(2));
        client.CheckIdleTimeout(); // drives the transition

        Assert.That(client.IsClosed, Is.True, "The closing period must expire on the fake clock.");
        Assert.That(client.IsClosing, Is.False);
    }

    [Test]
    public void KeepAlivePings_PreventTheIdleTimeout_UnderTheFakeClock()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var clock = new FakeTimeProvider();
        (QuicClientConnection client, QuicServerConnection server) =
            Handshaken(cert, clock, new TransportParameters { MaxIdleTimeoutMs = 300 });
        using var _ = client;
        using var __ = server;

        client.KeepAliveInterval = TimeSpan.FromMilliseconds(100);

        // Let 1 s of fake time pass in 100-ms steps (far beyond the 300-ms idle timeout) —
        // the keep-alive PINGs sent while pumping keep both sides alive.
        for (int step = 0; step < 10; step++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Pump(client, server);
            client.CheckIdleTimeout();
            server.CheckIdleTimeout();
        }

        Assert.That(server.IsIdleTimedOut, Is.False, "Keep-alive PINGs must prevent the server idle timeout.");
        Assert.That(client.IsIdleTimedOut, Is.False);

        // Counter-check: stop pumping and the very same clock does time the connection out.
        clock.Advance(TimeSpan.FromMilliseconds(400));
        server.CheckIdleTimeout();
        Assert.That(server.IsIdleTimedOut, Is.True);
    }

    [Test]
    public void Http3Connections_PassTheTimeProviderThrough_ToTheQuicTimers()
    {
        // The same fake-clocked idle-timeout scenario through the HTTP/3 layer proves that the
        // ctor parameter reaches the underlying QUIC endpoints.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var clock = new FakeTimeProvider();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
            new TransportParameters { MaxIdleTimeoutMs = 300 }, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);
        // Flush trailing ACKs at fake time 0 (see Handshaken): keeps the RTT samples at zero so the
        // 3×PTO floor stays below the negotiated 300 ms.
        for (int round = 0; round < 3; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(server.IsIdleTimedOut, Is.False);

        clock.Advance(TimeSpan.FromMilliseconds(400));
        server.CheckTimeouts();
        Assert.That(server.IsIdleTimedOut, Is.True, "The fake clock must reach the QUIC timers through the HTTP/3 layer.");
    }

    [Test]
    public void ObfuscatedTicketAge_UsesTheInjectedWallClock()
    {
        // RFC 8446 §4.2.11: obfuscated_ticket_age = age in ms + age_add (mod 2^32) — with an
        // injected reception time and "now" the value is exactly predictable.
        var receivedAt = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var ticket = new ResumptionTicket(
            psk: new byte[32], identity: [1, 2, 3], ageAdd: 1000, cipherSuite: CipherSuite.Aes128GcmSha256,
            serverName: "localhost", lifetimeSeconds: 7200, maxEarlyDataSize: 0, peerTransportParameters: [],
            receivedAt: receivedAt);

        Assert.That(ticket.ObfuscatedTicketAge(receivedAt.AddMilliseconds(5000)), Is.EqualTo(5000u + 1000u));
    }
}
