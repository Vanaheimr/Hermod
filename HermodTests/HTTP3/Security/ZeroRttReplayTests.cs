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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// Anti-replay for 0-RTT (RFC 8446 §8, made a MUST for QUIC by RFC 9001 §9.2). Implemented is the
/// single-use ticket of §8.1 plus the freshness check of §8.3 — this fixture proves that a captured
/// 0-RTT flight really cannot be replayed a second time.
/// </summary>
[TestFixture]
public class ZeroRttReplayTests
{
    private const uint MaxEarly = 0xffffffff;

    private static Http3Response Handler(Http3Request request) => new()
    {
        Status = 200,
        Headers = [new HeaderField("content-type", "text/plain")],
        Body = System.Text.Encoding.UTF8.GetBytes($"response to {request.Path}"),
    };

    // ---- The store on its own ------------------------------------------------------------------

    [Test]
    public void Ticket_CanBeConsumedExactlyOnce()
    {
        var cache = new ServerResumptionCache();
        byte[] identity = cache.Issue([1, 2, 3], CipherSuite.Aes128GcmSha256, MaxEarly, [], ageAdd: 0, lifetimeSeconds: 7200);

        Assert.That(cache.TryConsume(identity), Is.True, "First use wins.");
        Assert.That(cache.TryConsume(identity), Is.False, "Every replay loses (RFC 8446 §8.1).");
        Assert.That(cache.Count, Is.Zero);
    }

    [Test]
    public void Lookup_DoesNotConsume_SoABogusBinderCannotBurnAForeignTicket()
    {
        // TryResolve runs BEFORE the binder is verified. If it consumed, anyone who merely observed
        // a ticket identity could destroy the legitimate client's ticket with garbage.
        var cache = new ServerResumptionCache();
        byte[] identity = cache.Issue([1, 2, 3], CipherSuite.Aes128GcmSha256, MaxEarly, [], ageAdd: 0, lifetimeSeconds: 7200);

        Assert.That(cache.TryResolve(identity, 0, out ResumptionLookup? first), Is.True);
        Assert.That(cache.TryResolve(identity, 0, out ResumptionLookup? second), Is.True, "Still there.");
        Assert.That(first!.Psk, Is.EqualTo(second!.Psk));
        Assert.That(cache.TryConsume(identity), Is.True, "And the legitimate client can still use it.");
    }

    [Test]
    public void ExpiredTicket_IsRejectedAndDropped()
    {
        var clock = new FakeTimeProvider();
        var cache = new ServerResumptionCache(clock);
        byte[] identity = cache.Issue([1, 2, 3], CipherSuite.Aes128GcmSha256, MaxEarly, [], ageAdd: 0, lifetimeSeconds: 60);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.That(cache.TryResolve(identity, 59_000, out _), Is.True, "Still valid.");

        clock.Advance(TimeSpan.FromSeconds(2)); // 61 s > lifetime
        Assert.That(cache.TryResolve(identity, 61_000, out _), Is.False, "Expired (RFC 8446 §4.6.1).");
        Assert.That(cache.Count, Is.Zero, "And removed, so it cannot pile up.");
    }

    [Test]
    public void Cache_StaysBounded_AndEvictsTheOldest()
    {
        // Without a cap an attacker could exhaust memory: every handshake issues a ticket.
        var cache = new ServerResumptionCache(maxEntries: 8);
        var identities = new List<byte[]>();
        for (int i = 0; i < 20; i++)
            identities.Add(cache.Issue([(byte)i], CipherSuite.Aes128GcmSha256, MaxEarly, [], 0, 7200));

        Assert.That(cache.Count, Is.EqualTo(8));
        Assert.That(cache.TryResolve(identities[0], 0, out _), Is.False, "The oldest was evicted.");
        Assert.That(cache.TryResolve(identities[^1], 0, out _), Is.True, "The newest survives.");
    }

    [Test]
    public void FreshnessCheck_RejectsEarlyDataForAnImplausibleTicketAge()
    {
        // RFC 8446 §8.3: the client reports how old it thinks the ticket is
        // (obfuscated_ticket_age − ticket_age_add). If that clashes with the age the server measured,
        // the ClientHello is not plausibly recent ⇒ no 0-RTT (but resumption may still proceed).
        var clock = new FakeTimeProvider();
        var cache = new ServerResumptionCache(clock, freshnessWindow: TimeSpan.FromSeconds(10));
        const uint ageAdd = 1000;
        byte[] identity = cache.Issue([1, 2, 3], CipherSuite.Aes128GcmSha256, MaxEarly, [], ageAdd, 7200);

        clock.Advance(TimeSpan.FromSeconds(5));

        // Client claims 5000 ms — matches what the server measured.
        Assert.That(cache.TryResolve(identity, 5000 + ageAdd, out ResumptionLookup? honest), Is.True);
        Assert.That(honest!.EarlyDataFresh, Is.True);

        // Replay hours later with the SAME recorded value ⇒ claim and measurement diverge.
        Assert.That(cache.TryResolve(identity, 5000 + ageAdd, out ResumptionLookup? stale), Is.True);
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.That(cache.TryResolve(identity, 5000 + ageAdd, out ResumptionLookup? old), Is.True);
        Assert.That(old!.EarlyDataFresh, Is.False, "Too old ⇒ no 0-RTT.");
        Assert.That(stale, Is.Not.Null);
    }

    [Test]
    public void AgeCalculation_WrapsModulo2Pow32()
    {
        // RFC 8446 §4.2.11.1: the client adds ticket_age_add modulo 2^32 — the subtraction has to wrap too.
        var clock = new FakeTimeProvider();
        var cache = new ServerResumptionCache(clock, freshnessWindow: TimeSpan.FromSeconds(10));
        uint ageAdd = uint.MaxValue - 100; // forces the wrap
        byte[] identity = cache.Issue([1], CipherSuite.Aes128GcmSha256, MaxEarly, [], ageAdd, 7200);

        clock.Advance(TimeSpan.FromSeconds(1));
        uint obfuscated = unchecked(1000u + ageAdd); // wraps around

        Assert.That(cache.TryResolve(identity, obfuscated, out ResumptionLookup? lookup), Is.True);
        Assert.That(lookup!.EarlyDataFresh, Is.True, "The wrap must be computed correctly.");
    }

    // ---- End to end: a captured 0-RTT flight -------------------------------------------------

    [Test]
    public void CapturedZeroRttFlight_IsAcceptedOnce_AndRejectedOnReplay()
    {
        // The realistic attack: an on-path attacker records the client's first flight (ClientHello +
        // 0-RTT packets carrying the request) and sends it a second time.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        ResumptionTicket ticket = ObtainTicket(cert, cache, validation);

        // --- The victim's genuine 0-RTT connection; we record its datagrams on the way ---
        var capturedFlight = new List<byte[]>();
        int handlerCalls = 0;
        Http3Response CountingHandler(Http3Request request)
        {
            handlerCalls++;
            return Handler(request);
        }

        using var server = new Http3ServerConnection(cert, CountingHandler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client.Start();
        client.InitializeHttp3();
        ulong stream = client.SendRequest(Http3Request.Get("localhost", "/transfer-money"));

        Http3Response? response = null;
        for (int round = 0; round < 40 && response is null; round++)
        {
            client.CheckTimeouts();
            bool beforeHandshake = !client.HandshakeConfirmed; // the 0-RTT window
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                // The attacker records everything the client sends before the handshake completes —
                // Initial (ClientHello) plus the 0-RTT packets carrying the request. Later 1-RTT
                // packets are useless to a replay: they are protected with keys of THIS connection.
                if (beforeHandshake)
                    capturedFlight.Add([.. dg]);
                server.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
            client.TryGetResponse(stream, out response);
        }

        Assert.That(server.EarlyDataAccepted, Is.True, "The genuine connection must get 0-RTT.");
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(handlerCalls, Is.EqualTo(1));
        Assert.That(capturedFlight, Is.Not.Empty, "The attacker has something to replay.");

        // --- The attacker replays the recorded flight against a fresh connection of the same server ---
        using var replayTarget = new Http3ServerConnection(cert, CountingHandler, resumptionCache: cache,
                                                           maxEarlyDataSize: MaxEarly);
        foreach (byte[] dg in capturedFlight)
            replayTarget.ProcessDatagram(dg);
        for (int round = 0; round < 10; round++)
        {
            replayTarget.CheckTimeouts();
            replayTarget.GetDatagramsToSend();
        }

        // The decisive assertion: WITHOUT the single-use ticket the server accepts the replayed
        // flight here (verified — this assertion fails when TryConsume is disabled), installs the
        // early traffic keys and decrypts the attacker's copy of the request. That is precisely the
        // replay RFC 8446 §8.1 exists to prevent.
        Assert.That(replayTarget.EarlyDataAccepted, Is.False,
                    "The replayed 0-RTT flight MUST NOT be accepted a second time (RFC 8446 §8.1).");
        Assert.That(handlerCalls, Is.EqualTo(1), "And the request must not run a second time.");
    }

    [Test]
    public void TicketIsSingleUse_SoASecondResumptionFallsBackToAFullHandshake()
    {
        // Consequence of §8.1: even a legitimate second use of the SAME ticket gets a full
        // handshake. Clients therefore need a fresh ticket per resumption — which the server issues.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        ResumptionTicket ticket = ObtainTicket(cert, cache, validation);

        Assert.That(Resume(cert, cache, validation, ticket), Is.True, "First resumption works.");
        Assert.That(Resume(cert, cache, validation, ticket), Is.False,
                    "The same ticket a second time ⇒ full handshake instead of resumption.");
    }

    // ---- Helpers -----------------------------------------------------------------------------

    private static ResumptionTicket ObtainTicket(ServerCertificate cert, ServerResumptionCache cache,
                                                 CertificateValidationOptions validation)
    {
        using var server = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        bool sent = false;
        ulong stream = 0;
        Http3Response? response = null;
        for (int round = 0; round < 40 && (response is null || client.NewSessionTickets.Count == 0); round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
            if (client.HandshakeConfirmed && !sent)
            {
                client.InitializeHttp3();
                stream = client.SendRequest(Http3Request.Get("localhost", "/first"));
                sent = true;
            }
            if (sent) client.TryGetResponse(stream, out response);
        }
        Assert.That(client.NewSessionTickets, Is.Not.Empty, "The server must issue a ticket.");
        return client.NewSessionTickets[0];
    }

    /// <summary>
    /// Runs a resumption handshake and reports whether the server accepted the PSK.
    /// </summary>
    private static bool Resume(ServerCertificate cert, ServerResumptionCache cache,
                               CertificateValidationOptions validation, ResumptionTicket ticket)
    {
        using var server = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation,
                                                     resumptionTicket: ticket);
        client.Start();
        for (int round = 0; round < 40 && !client.HandshakeConfirmed; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True, "The handshake must succeed either way.");
        return server.ResumptionAccepted;
    }
}
