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
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Messages;

/// <summary>
/// RFC 9218 (Extensible Prioritization Scheme): `priority` header (u=0..7, i), PRIORITY_UPDATE frame
/// (0xF0700, client control stream only) and the recommended server scheduling (§10): ascending
/// urgency; same urgency non-incremental ⇒ stream-ID order, incremental ⇒ share bandwidth.
/// </summary>
[TestFixture]
public class Http3PriorityTests
{
    // ---- Parser (RFC 9218 §4, Structured Fields) ------------------------------------------

    [Test]
    public void PriorityParse_HandlesDefaultsRangesAndUnknownParameters()
    {
        Assert.That(Http3Priority.Parse(null), Is.EqualTo(new Http3Priority(3, false)));
        Assert.That(Http3Priority.Parse("u=0"), Is.EqualTo(new Http3Priority(0, false)));
        Assert.That(Http3Priority.Parse("u=5, i"), Is.EqualTo(new Http3Priority(5, true)));
        Assert.That(Http3Priority.Parse("i"), Is.EqualTo(new Http3Priority(3, true)));
        Assert.That(Http3Priority.Parse("i=?1"), Is.EqualTo(new Http3Priority(3, true)));
        Assert.That(Http3Priority.Parse("i=?0"), Is.EqualTo(new Http3Priority(3, false)));
        Assert.That(Http3Priority.Parse("u=9"), Is.EqualTo(new Http3Priority(3, false)));        // out of range ⇒ ignore
        Assert.That(Http3Priority.Parse("u=abc"), Is.EqualTo(new Http3Priority(3, false)));      // wrong type ⇒ ignore
        Assert.That(Http3Priority.Parse("u=1, u=6"), Is.EqualTo(new Http3Priority(6, false)));   // the last one wins (SF §3.2)
        Assert.That(Http3Priority.Parse("x=5, u=2, i"), Is.EqualTo(new Http3Priority(2, true))); // unknown ⇒ ignore
        Assert.That(Http3Priority.Parse("u=1;foo=bar"), Is.EqualTo(new Http3Priority(1, false))); // ignore member parameters

        Assert.That(Http3Priority.Default.ToHeaderValue(), Is.EqualTo(""));
        Assert.That(new Http3Priority(0, false).ToHeaderValue(), Is.EqualTo("u=0"));
        Assert.That(new Http3Priority(5, true).ToHeaderValue(), Is.EqualTo("u=5, i"));
        Assert.That(new Http3Priority(3, true).ToHeaderValue(), Is.EqualTo("i"));
    }

    // ---- Scheduling (§10) over our full own stack ------------------------------------------

    [Test]
    public void HigherUrgencyResponse_CompletesFirst_DespiteBeingRequestedSecond()
    {
        (int roundA, int roundB) = RunTwoRequests(
            first: Http3Request.Get("localhost", "/big"),                                       // u=3 (default)
            second: Http3Request.Get("localhost", "/big") with { Priority = new(0, false) });   // u=0

        Assert.That(roundB < roundA, Is.True, $"The more urgent response (u=0) must finish first (B: round {roundB}, A: round {roundA}).");
    }

    [Test]
    public void SameUrgencyNonIncremental_ServedInStreamIdOrder()
    {
        (int roundA, int roundB) = RunTwoRequests(
            first: Http3Request.Get("localhost", "/big"),
            second: Http3Request.Get("localhost", "/big"));

        Assert.That(roundA < roundB, Is.True, $"With the same urgency (non-incremental) request order applies (A: {roundA}, B: {roundB}).");
    }

    [Test]
    public void SameUrgencyIncremental_SharesBandwidth()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        byte[] big = new byte[150_000];
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = big };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);
        client.InitializeHttp3();

        var incremental = new Http3Priority(3, true);
        ulong a = client.SendRequest(Http3Request.Get("localhost", "/big") with { Priority = incremental });
        ulong b = client.SendRequest(Http3Request.Get("localhost", "/big") with { Priority = incremental });

        // Both responses must make progress in parallel BEFORE their respective completion (§10).
        bool sharedProgress = false;
        Http3Response? responseA = null, responseB = null;
        for (int round = 0; round < 2000 && (responseA is null || responseB is null); round++)
        {
            Pump(client, server, clock);
            client.TryGetResponse(a, out responseA);
            client.TryGetResponse(b, out responseB);
            if (responseA is null && responseB is null &&
                server.Quic.Streams[a].Send.SentOffset > 0 && server.Quic.Streams[b].Send.SentOffset > 0)
                sharedProgress = true;
        }

        Assert.That(responseA, Is.Not.Null);
        Assert.That(responseB, Is.Not.Null);
        Assert.That(sharedProgress, Is.True, "Incremental responses of the same urgency must share the bandwidth.");
    }

    [Test]
    public void PriorityUpdate_OverridesHeader_Reprioritization()
    {
        // A is requested with u=0, then demoted via PRIORITY_UPDATE to u=7 (background) —
        // the update trumps the header (§7), so B (default u=3) wins.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        byte[] big = new byte[150_000];
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = big };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);
        client.InitializeHttp3();

        ulong a = client.SendRequest(Http3Request.Get("localhost", "/big") with { Priority = new(0, false) });
        ulong b = client.SendRequest(Http3Request.Get("localhost", "/big"));
        client.SendPriorityUpdate(a, new Http3Priority(7, false)); // degrade the prefetch (§6)

        (int roundA, int roundB) = AwaitBoth(client, server, a, b, clock);
        Assert.That(roundB < roundA, Is.True, $"The PRIORITY_UPDATE (u=7) must override the header (u=0) (A: {roundA}, B: {roundB}).");
    }

    [Test]
    public void PriorityUpdate_BeforeStreamOpens_IsBufferedAndApplied()
    {
        // Raw client: PRIORITY_UPDATE for stream 4 (u=0) BEFORE it opens — the server MUST buffer
        // the latest update and apply it when the stream opens (§7) ⇒ stream 4 is served before stream 0.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        byte[] big = new byte[150_000];
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = big }, timeProvider: clock);
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);

        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        control.Write(Http3Frames.Build(Http3FrameType.PriorityUpdateRequest, [0x04, .. "u=0"u8])); // stream 4

        QuicStream requestA = client.OpenBidirectionalStream(); // stream 0 (default u=3)
        requestA.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeGet("/a")));
        requestA.Finish();
        QuicStream requestB = client.OpenBidirectionalStream(); // stream 4 (u=0 via the update)
        requestB.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeGet("/b")));
        requestB.Finish();

        int completeA = -1, completeB = -1;
        for (int round = 0; round < 2000 && (completeA < 0 || completeB < 0); round++)
        {
            Pump(client, server, clock);
            requestA.Read();
            requestB.Read();
            if (completeA < 0 && requestA.IsReceiveComplete) completeA = round;
            if (completeB < 0 && requestB.IsReceiveComplete) completeB = round;
        }

        Assert.That(completeB >= 0 && completeA >= 0, Is.True, "Both responses must arrive.");
        Assert.That(completeB < completeA, Is.True, $"The buffered PRIORITY_UPDATE (u=0) must favor stream 4 (A: {completeA}, B: {completeB}).");
    }

    // ---- State machine (RFC 9218 §7.2 MUSTs) ----------------------------------------------

    [Test]
    public void PriorityUpdate_OnRequestStream_IsFrameUnexpected()
        => AssertServerCloses(client =>
        {
            QuicStream request = client.OpenBidirectionalStream();
            request.Write(Http3Frames.Build(Http3FrameType.PriorityUpdateRequest, [0x00, .. "u=0"u8]));
        }, Http3Error.FrameUnexpected);

    [Test]
    public void PriorityUpdate_ForNonRequestStreamId_IsIdError()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            control.Write(Http3Frames.Build(Http3FrameType.PriorityUpdateRequest, [0x03, .. "u=0"u8])); // 0b11 ≠ request stream
        }, Http3Error.IdError);

    [Test]
    public void PriorityUpdate_PushVariant_IsIdError()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            control.Write(Http3Frames.Build(Http3FrameType.PriorityUpdatePush, [0x00, .. "u=0"u8])); // never promised
        }, Http3Error.IdError);

    [Test]
    public void PriorityUpdate_SentToClient_IsFrameUnexpected()
    {
        // RFC 9218 §7.2: servers MUST NEVER send PRIORITY_UPDATE — the client must close.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump2(client, server);
        client.InitializeHttp3();

        QuicStream control = server.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        control.Write(Http3Frames.Build(Http3FrameType.PriorityUpdateRequest, [0x00, .. "u=0"u8]));

        for (int round = 0; round < 10 && !client.IsClosing; round++)
            Pump2(client, server);
        Pump2(client, server); // still deliver the CONNECTION_CLOSE

        Assert.That(client.IsClosing, Is.True);
        Assert.That(server.PeerCloseFrame, Is.Not.Null);
        Assert.That(server.PeerCloseFrame!.ErrorCode, Is.EqualTo(Http3Error.FrameUnexpected));
    }

    // ---- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// Two large requests over our own stack; returns: the round of each one's completion.
    /// </summary>
    private static (int RoundA, int RoundB) RunTwoRequests(Http3Request first, Http3Request second)
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        byte[] big = new byte[150_000];
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = big };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        ulong a = client.SendRequest(first);
        ulong b = client.SendRequest(second);
        return AwaitBoth(client, server, a, b, clock);
    }

    private static (int RoundA, int RoundB) AwaitBoth(Http3ClientConnection client, Http3ServerConnection server, ulong a, ulong b, FakeTimeProvider clock)
    {
        int roundA = -1, roundB = -1;
        for (int round = 0; round < 2000 && (roundA < 0 || roundB < 0); round++)
        {
            Pump(client, server, clock);
            if (roundA < 0 && client.TryGetResponse(a, out _)) roundA = round;
            if (roundB < 0 && client.TryGetResponse(b, out _)) roundB = round;
        }
        Assert.That(roundA >= 0 && roundB >= 0, Is.True, "Both responses must arrive.");
        return (roundA, roundB);
    }

    private static byte[] EncodeGet(string path)
        => QpackEncoder.Encode(
        [
            new HeaderField(":method", "GET"),
            new HeaderField(":scheme", "https"),
            new HeaderField(":authority", "localhost"),
            new HeaderField(":path", path),
        ]);

    private static void AssertServerCloses(Action<QuicClientConnection> misbehave, ulong expectedError)
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] });
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        misbehave(client);
        for (int round = 0; round < 10 && !server.IsClosing; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close the connection.");
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.IsApplicationError, Is.True);
        Assert.That(client.PeerCloseFrame.ErrorCode, Is.EqualTo(expectedError));
    }

    // The big-transfer tests pump on a FakeTimeProvider advanced 1 ms per round: the pacer refills
    // its budget from ELAPSED TIME (RFC 9002 §7.7), and with the real clock a fixed round budget
    // races the machine — on a JIT-warm process 2000 rounds burn down in ~3 ms, less real time than
    // the pacer needs to recover its post-handshake deficit at the handshake's compute-time-derived
    // sRTT (~40 ms), so the transfer stalls forever. One fake millisecond per round makes the budget
    // a function of the ROUND COUNT instead of the wall clock, on every machine.
    private static void Pump(Http3ClientConnection client, Http3ServerConnection server, FakeTimeProvider? clock = null)
    {
        clock?.Advance(TimeSpan.FromMilliseconds(1));
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump(QuicClientConnection client, Http3ServerConnection server, FakeTimeProvider? clock = null)
    {
        clock?.Advance(TimeSpan.FromMilliseconds(1));
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump2(Http3ClientConnection client, QuicServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
