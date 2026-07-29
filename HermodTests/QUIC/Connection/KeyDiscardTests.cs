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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// After the handshake no more Initial/Handshake packets may be sent (RFC 9001 §4.9.1/§4.9.2:
/// the client discards the Initial keys as soon as it sends a Handshake packet, and the Handshake keys
/// as soon as the handshake is confirmed). Otherwise a PTO wrongly probes the Initial space and the
/// ClientHello goes out again as an Initial (padded to 1200 bytes).
/// </summary>
[TestFixture]
public class KeyDiscardTests
{
    private static bool IsInitial(byte[] dg) =>
        dg.Length > 0 && PacketFormat.IsLongHeader(dg[0]) && PacketFormat.GetLongPacketType(dg[0]) == LongPacketType.Initial;

    private static bool IsHandshake(byte[] dg) =>
        dg.Length > 0 && PacketFormat.IsLongHeader(dg[0]) && PacketFormat.GetLongPacketType(dg[0]) == LongPacketType.Handshake;

    [Test]
    public void AfterHandshake_ClientSendsNoInitialOrHandshakePackets_EvenUnderPto()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);

        // Create ack-eliciting 1-RTT data that is NOT acknowledged (the server receives nothing more) ⇒ PTO fires.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write("ping"u8.ToArray());

        bool initialSeen = false, handshakeSeen = false;
        for (int round = 0; round < 40; round++)
        {
            client.CheckLossDetectionTimeout(); // drives PTO/loss detection
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                initialSeen |= IsInitial(dg);
                handshakeSeen |= IsHandshake(dg);
            }
            Thread.Sleep(20); // give the real clock time for the PTO deadline
        }

        Assert.That(initialSeen, Is.False, "After the handshake NO Initial packet may be sent anymore (RFC 9001 §4.9.1).");
        Assert.That(handshakeSeen, Is.False, "After the handshake NO Handshake packet may be sent anymore (RFC 9001 §4.9.2).");
    }

    /// <summary>
    /// Counter-check to the "not too late" rule: the Initial keys must not be discarded TOO EARLY either.
    /// The client must still be able to ack the server Initial (ServerHello) (RFC 9001 §4.9), i.e. send a
    /// second Initial packet. Discarding the keys already when installing the Handshake keys would drop that ACK.
    /// </summary>
    [Test]
    public void DuringHandshake_ClientSendsInitialAckForServerHello_KeysNotDiscardedTooEarly()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        int clientInitialPackets = 0;
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                if (IsInitial(dg))
                    clientInitialPackets++;
                server.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
        // ClientHello (≥1 Initial) AND an Initial ACK of the ServerHello ⇒ at least two Initial packets.
        Assert.That(clientInitialPackets >= 2, Is.True, $"The client must still ack the server Initial (≥2 Initial packets), otherwise the keys were discarded too early. Was: {clientInitialPackets}.");
    }

    /// <summary>
    /// RFC 9001 §4.1.2: the client MAY confirm the handshake as soon as one of its 1-RTT packets is
    /// acknowledged — even without HANDSHAKE_DONE. The Handshake keys are thus discarded earlier (or
    /// despite a lost HANDSHAKE_DONE). The server suppresses HANDSHAKE_DONE here so that ONLY the
    /// 1-RTT ACK path triggers the confirmation.
    /// </summary>
    [Test]
    public void ClientConfirmsHandshake_ViaOneRttAck_EvenWithoutHandshakeDone()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        // The confirmation this test is about hangs on a single acknowledgment, and the server may
        // legitimately hold that back for up to max_ack_delay (RFC 9000 §13.2.2) — one 1-RTT packet
        // does not reach the two-packet threshold. A pump loop on a standing clock never gets there,
        // so the clock steps past the deadline between rounds.
        var clock = new FakeTimeProvider();
        using var client = new QuicClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        using var server = new QuicServerConnection(cert, timeProvider: clock) { SuppressHandshakeDoneForTest = true };

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeComplete; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeComplete, Is.True, "The client must have sent its Finished.");
        Assert.That(client.HandshakeConfirmed, Is.False, "Without HANDSHAKE_DONE the handshake is NOT yet confirmed.");

        // The client sends 1-RTT data; its ACK (no HANDSHAKE_DONE!) confirms the handshake (§4.1.2).
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write("hi"u8.ToArray());
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(30));
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "The client must confirm the handshake via the 1-RTT ACK alone (RFC 9001 §4.1.2), without HANDSHAKE_DONE.");
    }

    /// <summary>
    /// RFC 9001 §4.9.2: the Handshake keys are discarded IMMEDIATELY upon confirmation — without a
    /// retention window. Unlike §4.9.3 for 0-RTT (there servers may keep the keys ~3×PTO against
    /// reordering) there is deliberately NO reordering window for Handshake keys: the handshake is
    /// finished on both sides, a late-reordered Handshake packet would only carry what is already known.
    /// The test proves: at the same moment the handshake is confirmed, the Handshake keys are already gone.
    /// </summary>
    [Test]
    public void HandshakeKeys_DiscardedImmediatelyOnConfirmation_NoReorderingWindow()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        bool hadHandshakeKeysBeforeConfirm = false;
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            // Before the confirmation the Handshake keys must have been installed at least once.
            hadHandshakeKeysBeforeConfirm |= client.HasHandshakeKeysForTest;
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
        Assert.That(hadHandshakeKeysBeforeConfirm, Is.True, "The Handshake keys must have been installed during the handshake.");
        Assert.That(client.HasHandshakeKeysForTest, Is.False, "Upon confirmation the Handshake keys must be discarded IMMEDIATELY — no reordering window (RFC 9001 §4.9.2).");
        // The server too discards them immediately upon handshake completion.
        Assert.That(server.HasHandshakeKeysForTest, Is.False, "The server too discards the Handshake keys immediately upon completion (RFC 9001 §4.9.2).");
    }
}
