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

using System.Threading;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// 0-RTT / early data (RFC 8446 §2.3, RFC 9001 §4), phase B: connection 1 collects a ticket with
/// early_data allowed; connection 2 queues the HTTP/3 request BEFORE the handshake so it goes out
/// as a 0-RTT packet (long header 0x01, application PN space). The server accepts the early data,
/// processes the request and responds — over the full QUIC/HTTP-3 datagram path, both ends from scratch.
/// </summary>
[TestFixture]
public class ZeroRttTests
{
    private const uint MaxEarly = 0xffffffff; // QUIC uses 0xFFFFFFFF (RFC 9001 §4.6.1)

    private static Http3Response Handler(Http3Request request) => new()
    {
        Status = 200,
        Headers = [new HeaderField("content-type", "text/plain")],
        Body = System.Text.Encoding.UTF8.GetBytes($"0-RTT response to {request.Path}"),
    };

    [Test]
    public void ClientSendsEarlyGet_ServerAcceptsEarlyData_AndResponds()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // --- Connection 1: collect a ticket (with early_data allowed) ---
        ResumptionTicket ticket;
        using (var server1 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly))
        using (var client1 = new Http3ClientConnection("localhost", certificateValidation: validation))
        {
            client1.Start();
            bool sent = false;
            ulong stream1 = 0;
            Http3Response? r = null;
            for (int round = 0; round < 40 && (r is null || client1.NewSessionTickets.Count == 0); round++)
            {
                client1.CheckTimeouts();
                foreach (byte[] dg in client1.GetDatagramsToSend()) server1.ProcessDatagram(dg);
                foreach (byte[] dg in server1.GetDatagramsToSend()) client1.ProcessDatagram(dg);
                if (client1.HandshakeConfirmed && !sent)
                {
                    client1.InitializeHttp3();
                    stream1 = client1.SendRequest(Http3Request.Get("localhost", "/one"));
                    sent = true;
                }
                if (sent) client1.TryGetResponse(stream1, out r);
            }
            Assert.That(client1.NewSessionTickets, Is.Not.Empty);
            ticket = client1.NewSessionTickets[0];
            Assert.That(ticket.AllowsEarlyData, Is.True, "The ticket must allow 0-RTT (max_early_data_size > 0).");
        }

        // --- Connection 2: 0-RTT — queue the request BEFORE the handshake ---
        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();                                              // control/QPACK streams
        ulong stream = client2.SendRequest(Http3Request.Get("localhost", "/zero")); // request — everything as early data

        Http3Response? response = null;
        for (int round = 0; round < 40 && response is null; round++)
        {
            client2.CheckTimeouts();
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
            client2.TryGetResponse(stream, out response);
        }

        Assert.That(client2.EarlyDataAccepted, Is.True, "The server must have accepted 0-RTT (EncryptedExtensions).");
        Assert.That(server2.EarlyDataAccepted, Is.True, "early_data must be accepted on the server side.");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Does.Contain("/zero"));
    }

    /// <summary>
    /// RFC 9001 §4.9.3: the client discards its 0-RTT keys as soon as the 1-RTT keys are installed
    /// (after that it sends no more 0-RTT packets, §5.6, and never receives any itself ⇒ the keys are useless).
    /// The test queues 0-RTT data, checks that the keys exist during the early sending, and that they are
    /// discarded after the 1-RTT install (handshake confirmed). The IMMEDIATE, unconditional discard is also
    /// the proof that on the client — unlike on the server — there is NO reordering window (it has no
    /// 0-RTT read path, reordered packets never use 0-RTT keys).
    /// </summary>
    [Test]
    public void Client_DiscardsZeroRttKeys_OnInstallingOneRttKeys()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // Connection 1: collect a ticket with early_data allowed.
        ResumptionTicket ticket;
        using (var server1 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly))
        using (var client1 = new Http3ClientConnection("localhost", certificateValidation: validation))
        {
            client1.Start();
            bool sent = false;
            ulong s1 = 0;
            Http3Response? r = null;
            for (int round = 0; round < 40 && (r is null || client1.NewSessionTickets.Count == 0); round++)
            {
                client1.CheckTimeouts();
                foreach (byte[] dg in client1.GetDatagramsToSend()) server1.ProcessDatagram(dg);
                foreach (byte[] dg in server1.GetDatagramsToSend()) client1.ProcessDatagram(dg);
                if (client1.HandshakeConfirmed && !sent) { client1.InitializeHttp3(); s1 = client1.SendRequest(Http3Request.Get("localhost", "/one")); sent = true; }
                if (sent) client1.TryGetResponse(s1, out r);
            }
            ticket = client1.NewSessionTickets[0];
        }

        // Connection 2: 0-RTT — queue the request before the handshake so that 0-RTT keys are installed.
        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();
        client2.SendRequest(Http3Request.Get("localhost", "/zero"));

        // First send burst: 0-RTT goes out, the 0-RTT keys must now be installed (1-RTT not yet).
        byte[][] firstFlight = client2.GetDatagramsToSend().ToArray();
        Assert.That(client2.Quic.HasZeroRttKeysForTest, Is.True, "Before the 1-RTT install the 0-RTT keys must be installed.");
        foreach (byte[] dg in firstFlight) server2.ProcessDatagram(dg);

        // Pump the handshake to completion; with the 1-RTT install the 0-RTT keys must disappear.
        for (int round = 0; round < 40 && !client2.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
        }

        Assert.That(client2.HandshakeConfirmed, Is.True);
        Assert.That(client2.Quic.HasZeroRttKeysForTest, Is.False, "Upon installing the 1-RTT keys the client must have discarded its 0-RTT keys (RFC 9001 §4.9.3).");
    }

    [Test]
    public void RejectedEarlyData_IsRetriedOver1Rtt_AndStillSucceeds()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // Connection 1: the server allows 0-RTT ⇒ the ticket allows early data.
        ResumptionTicket ticket;
        using (var server1 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly))
        using (var client1 = new Http3ClientConnection("localhost", certificateValidation: validation))
        {
            client1.Start();
            bool sent = false;
            ulong s1 = 0;
            Http3Response? r = null;
            for (int round = 0; round < 40 && (r is null || client1.NewSessionTickets.Count == 0); round++)
            {
                client1.CheckTimeouts();
                foreach (byte[] dg in client1.GetDatagramsToSend()) server1.ProcessDatagram(dg);
                foreach (byte[] dg in server1.GetDatagramsToSend()) client1.ProcessDatagram(dg);
                if (client1.HandshakeConfirmed && !sent) { client1.InitializeHttp3(); s1 = client1.SendRequest(Http3Request.Get("localhost", "/one")); sent = true; }
                if (sent) client1.TryGetResponse(s1, out r);
            }
            ticket = client1.NewSessionTickets[0];
        }

        // Connection 2: the client offers 0-RTT (the ticket allows it), but the server REJECTS
        // (maxEarlyDataSize = 0). The early-sent 0-RTT data must be repeated over 1-RTT.
        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: 0);
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();
        ulong stream = client2.SendRequest(Http3Request.Get("localhost", "/rejected"));

        Http3Response? response = null;
        for (int round = 0; round < 80 && response is null; round++)
        {
            client2.CheckTimeouts();
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
            client2.TryGetResponse(stream, out response);
        }

        Assert.That(client2.EarlyDataAccepted, Is.False, "The server rejected 0-RTT.");
        Assert.That(response, Is.Not.Null); // still answered — the request ran over 1-RTT
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Does.Contain("/rejected"));
    }

    /// <summary>
    /// Handshake keys after 0-RTT rejection (RFC 9001 §4.9.2 + §4.1.2): even when the first application
    /// packets were rejected 0-RTT packets, the handshake-key discard must take effect correctly. Critical:
    /// 0-RTT and 1-RTT share the application PN space, but the §4.1.2 confirmation may ONLY count the
    /// acknowledgment of a genuine 1-RTT packet. HANDSHAKE_DONE is suppressed here so the confirmation runs
    /// exclusively via the 1-RTT ACK of the request repeated over 1-RTT — and the client then discards its
    /// handshake keys.
    /// </summary>
    [Test]
    public void RejectedEarlyData_HandshakeStillConfirmsAndDiscardsHandshakeKeys_ViaOneRttAck()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // Connection 1: collect a ticket with early_data allowed.
        ResumptionTicket ticket;
        using (var server1 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly))
        using (var client1 = new Http3ClientConnection("localhost", certificateValidation: validation))
        {
            client1.Start();
            bool sent = false;
            ulong s1 = 0;
            Http3Response? r = null;
            for (int round = 0; round < 40 && (r is null || client1.NewSessionTickets.Count == 0); round++)
            {
                client1.CheckTimeouts();
                foreach (byte[] dg in client1.GetDatagramsToSend()) server1.ProcessDatagram(dg);
                foreach (byte[] dg in server1.GetDatagramsToSend()) client1.ProcessDatagram(dg);
                if (client1.HandshakeConfirmed && !sent) { client1.InitializeHttp3(); s1 = client1.SendRequest(Http3Request.Get("localhost", "/one")); sent = true; }
                if (sent) client1.TryGetResponse(s1, out r);
            }
            ticket = client1.NewSessionTickets[0];
        }

        // Connection 2: the client offers 0-RTT, the server REJECTS (maxEarlyDataSize = 0) AND suppresses HANDSHAKE_DONE.
        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: 0);
        server2.Quic.SuppressHandshakeDoneForTest = true; // force confirmation only via the 1-RTT ACK (§4.1.2)
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();
        ulong stream = client2.SendRequest(Http3Request.Get("localhost", "/rejected")); // goes out as (rejected) 0-RTT

        Http3Response? response = null;
        for (int round = 0; round < 80 && (response is null || !client2.HandshakeConfirmed); round++)
        {
            client2.CheckTimeouts();
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
            client2.TryGetResponse(stream, out response);
        }

        Assert.That(client2.EarlyDataAccepted, Is.False, "The server rejected 0-RTT.");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200)); // repeated over 1-RTT and answered
        Assert.That(client2.HandshakeConfirmed, Is.True, "Despite 0-RTT rejection AND a missing HANDSHAKE_DONE the client must confirm via the 1-RTT ACK (§4.1.2) — not through a 0-RTT packet.");
        Assert.That(client2.Quic.HasHandshakeKeysForTest, Is.False, "Upon confirmation the handshake keys must be discarded (RFC 9001 §4.9.2) — on the 0-RTT rejection path too.");
    }

    private static ResumptionTicket AcquireTicket(ServerCertificate cert, ServerResumptionCache cache, CertificateValidationOptions validation)
    {
        using var server1 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        using var client1 = new Http3ClientConnection("localhost", certificateValidation: validation);
        client1.Start();
        bool sent = false;
        ulong s1 = 0;
        Http3Response? r = null;
        for (int round = 0; round < 40 && (r is null || client1.NewSessionTickets.Count == 0); round++)
        {
            client1.CheckTimeouts();
            foreach (byte[] dg in client1.GetDatagramsToSend()) server1.ProcessDatagram(dg);
            foreach (byte[] dg in server1.GetDatagramsToSend()) client1.ProcessDatagram(dg);
            if (client1.HandshakeConfirmed && !sent) { client1.InitializeHttp3(); s1 = client1.SendRequest(Http3Request.Get("localhost", "/one")); sent = true; }
            if (sent) client1.TryGetResponse(s1, out r);
        }
        return client1.NewSessionTickets[0];
    }

    /// <summary>
    /// RFC 9001 §4.9.3 (last sentence): the server MAY discard the 0-RTT read keys EARLIER than after
    /// 3×PTO once it has received all 0-RTT packets (gapless packet numbers). In the loss-free
    /// in-process run the application space is gapless from 0 as soon as the first 1-RTT packet arrives
    /// ⇒ the keys must be gone even though the 3×PTO deadline (deliberately set to 5 min here) is far
    /// from expiring.
    /// </summary>
    [Test]
    public void Server_DiscardsZeroRttKeysEarly_WhenAllPacketsReceived_NoGap()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        ResumptionTicket ticket = AcquireTicket(cert, cache, validation);

        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        server2.Quic.ServerZeroRttDiscardDelayForTest = TimeSpan.FromMinutes(5); // practically rule out the timer path
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();
        ulong stream = client2.SendRequest(Http3Request.Get("localhost", "/zero"));

        Http3Response? response = null;
        for (int round = 0; round < 40 && (response is null || !client2.HandshakeConfirmed); round++)
        {
            client2.CheckTimeouts();
            foreach (byte[] dg in client2.GetDatagramsToSend()) server2.ProcessDatagram(dg);
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
            client2.TryGetResponse(stream, out response);
        }

        Assert.That(server2.EarlyDataAccepted, Is.True, "The server must have accepted 0-RTT.");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        // No loss ⇒ gapless ⇒ the server discards the 0-RTT read keys immediately with the first 1-RTT packet,
        // without waiting for the (5-minute) deadline.
        Assert.That(server2.Quic.HasZeroRttKeysForTest, Is.False, "With gaplessly received 0-RTT packets the server MUST discard the 0-RTT read keys early (RFC 9001 §4.9.3).");
    }

    /// <summary>
    /// Counter-check to the early discard: if the server is missing a packet number in the application
    /// space (here the first 1-RTT packet is "dropped" so a permanent gap remains below later packets),
    /// it can NOT be sure it received all 0-RTT packets — it keeps the 0-RTT read keys until the short
    /// deadline (RECOMMENDED 3×PTO) expires and then discards them timer-driven. (0-RTT packets themselves
    /// are coalesced with the Initial here and thus not droppable individually; for the gap logic any
    /// missing PN counts the same.)
    /// </summary>
    [Test]
    public void Server_RetainsZeroRttKeys_UntilTimeout_WhenPacketNumberGapPersists()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        ResumptionTicket ticket = AcquireTicket(cert, cache, validation);

        using var server2 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        server2.Quic.ServerZeroRttDiscardDelayForTest = TimeSpan.FromMilliseconds(300);
        using var client2 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client2.Start();
        client2.InitializeHttp3();
        client2.SendRequest(Http3Request.Get("localhost", "/zero"));

        // Do NOT deliver the FIRST 1-RTT packet (short header) ⇒ permanent PN gap below the following
        // 1-RTT packets. Fixed round count (no break) so the server receives further 1-RTT packets and
        // arms the deadline, but the gap remains.
        bool droppedOneRtt = false;
        for (int round = 0; round < 40; round++)
        {
            client2.CheckTimeouts();
            foreach (byte[] dg in client2.GetDatagramsToSend())
            {
                if (!droppedOneRtt && !PacketFormat.IsLongHeader(dg[0])) { droppedOneRtt = true; continue; } // simulated loss
                server2.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server2.GetDatagramsToSend()) client2.ProcessDatagram(dg);
        }

        Assert.That(droppedOneRtt, Is.True, "There must have been a 1-RTT packet to drop.");
        Assert.That(server2.EarlyDataAccepted, Is.True, "The server accepts early_data based on the ClientHello (independent of packet loss).");
        Assert.That(client2.HandshakeConfirmed, Is.True);
        // Persistent gap ⇒ no early discard; within the 300-ms deadline the keys are still there.
        Assert.That(server2.Quic.HasZeroRttKeysForTest, Is.True, "With a PN gap the server keeps the 0-RTT read keys until the deadline expires (RFC 9001 §4.9.3).");

        // Let the deadline expire in time ⇒ timer-driven discard.
        Thread.Sleep(360);
        server2.Quic.CheckLossDetectionTimeout();
        Assert.That(server2.Quic.HasZeroRttKeysForTest, Is.False, "After the deadline expires the server MUST discard the 0-RTT read keys (RFC 9001 §4.9.3).");
    }

    /// <summary>
    /// Connection end (Dispose): if the connection ends before the 0-RTT read keys were discarded
    /// regularly (forced here via a PN gap + a deadline that effectively never expires), they must be
    /// released at Dispose at the latest — otherwise key material would sit undisposed until the GC.
    /// Test without <c>using</c> for the server to check the state right before and after the manual Dispose.
    /// </summary>
    [Test]
    public void Server_DiscardsZeroRttKeys_OnDispose()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var cache = new ServerResumptionCache();
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        ResumptionTicket ticket = AcquireTicket(cert, cache, validation);

        var server3 = new Http3ServerConnection(cert, Handler, resumptionCache: cache, maxEarlyDataSize: MaxEarly);
        server3.Quic.ServerZeroRttDiscardDelayForTest = TimeSpan.FromMinutes(5); // the timer never fires in the test
        using var client3 = new Http3ClientConnection("localhost", certificateValidation: validation, resumptionTicket: ticket);
        client3.Start();
        client3.InitializeHttp3();
        client3.SendRequest(Http3Request.Get("localhost", "/zero"));

        // Drop the first 1-RTT packet ⇒ persistent PN gap ⇒ no early discard; long deadline ⇒ no timer either.
        bool droppedOneRtt = false;
        for (int round = 0; round < 40; round++)
        {
            client3.CheckTimeouts();
            foreach (byte[] dg in client3.GetDatagramsToSend())
            {
                if (!droppedOneRtt && !PacketFormat.IsLongHeader(dg[0])) { droppedOneRtt = true; continue; }
                server3.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server3.GetDatagramsToSend()) client3.ProcessDatagram(dg);
        }

        Assert.That(server3.EarlyDataAccepted, Is.True, "The server must have accepted 0-RTT and installed the read keys.");
        Assert.That(server3.Quic.HasZeroRttKeysForTest, Is.True, "Precondition: the 0-RTT read keys are still there (gap ⇒ no early discard, long deadline ⇒ no timer).");

        server3.Dispose(); // connection end

        Assert.That(server3.Quic.HasZeroRttKeysForTest, Is.False, "At connection end (Dispose) the 0-RTT read keys must be released.");
    }
}
