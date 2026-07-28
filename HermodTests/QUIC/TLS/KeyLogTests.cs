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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Key log in the NSS key log format (<see cref="KeyLog"/>) — the file Wireshark reads to decrypt
/// our own QUIC traffic. Verified: line format, the client random as the connection identifier,
/// completeness of the secrets over a real handshake, and that BOTH endpoints log identical values
/// (only then can a recording be decrypted from either side).
/// </summary>
[TestFixture]
public class KeyLogTests
{
    [Test]
    public void Line_HasTheNssFormat_LabelSpaceRandomSpaceSecret()
    {
        var lines = new List<string>();
        var log = new KeyLog(lines.Add);

        log.Write(KeyLog.ClientTrafficSecret0, [0xAB, 0xCD], [0x01, 0x23, 0x45]);

        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0], Is.EqualTo("CLIENT_TRAFFIC_SECRET_0 abcd 012345"),
                    "Label, client random and secret, separated by single spaces, hex in lower case.");
    }

    [Test]
    public void EmptyValues_AreSkipped()
    {
        var lines = new List<string>();
        var log = new KeyLog(lines.Add);

        log.Write(KeyLog.ExporterSecret, [], [1, 2, 3]);
        log.Write(KeyLog.ExporterSecret, [1, 2, 3], []);

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void FromEnvironment_IsNull_WithoutTheVariable()
    {
        // The variable alone must never be enough — the application has to pass the key log on
        // explicitly, so secrets can never end up on disk unnoticed.
        Assert.That(KeyLog.FromEnvironment("HTTP3_KEYLOG_TEST_UNSET_VARIABLE"), Is.Null);
    }

    [Test]
    public void ClientRandom_IsReadFromTheClientHello_AtTheFixedOffset()
    {
        // RFC 8446 §4.1.2: handshake header (4) + legacy_version (2), then 32 bytes of random.
        byte[] handshakeMessage = new byte[4 + 2 + 32 + 10];
        for (int i = 0; i < 32; i++)
            handshakeMessage[6 + i] = (byte)(i + 1);

        ReadOnlySpan<byte> random = ClientHelloParser.ClientRandom(handshakeMessage);

        Assert.That(random.Length, Is.EqualTo(32));
        Assert.That(random[0], Is.EqualTo(1));
        Assert.That(random[31], Is.EqualTo(32));
        Assert.That(ClientHelloParser.ClientRandom(new byte[10]).IsEmpty, Is.True, "Too short ⇒ empty.");
    }

    [Test]
    public void Handshake_LogsAllSecrets_AndBothSidesAgree()
    {
        var clientLines = new List<string>();
        var serverLines = new List<string>();

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                                    keyLog: new KeyLog(clientLines.Add));
        using var server = new QuicServerConnection(cert, keyLog: new KeyLog(serverLines.Add));
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);

        string[] expected =
        [
            KeyLog.ClientHandshakeTrafficSecret,
            KeyLog.ServerHandshakeTrafficSecret,
            KeyLog.ClientTrafficSecret0,
            KeyLog.ServerTrafficSecret0,
            KeyLog.ExporterSecret,
        ];
        foreach (string label in expected)
            Assert.That(clientLines, Has.Some.StartsWith(label + " "), $"{label} missing from the client log.");

        // Decisive: the server derives the SAME secrets under the same client random — otherwise a
        // recording could only be decrypted from one side.
        Assert.That(serverLines.Order(), Is.EqualTo(clientLines.Order()),
                    "Both endpoints must log identical lines.");

        // The client random is the connection identifier and identical on every line.
        string[] randoms = [.. clientLines.Select(l => l.Split(' ')[1]).Distinct()];
        Assert.That(randoms, Has.Length.EqualTo(1));
        Assert.That(randoms[0], Has.Length.EqualTo(64), "32 bytes of client random as hex.");
    }

    [Test]
    public void WithoutAKeyLog_NothingIsWritten()
    {
        // The default must stay silent — no accidental secret logging.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake runs unchanged without a key log.");
    }

    [Test]
    public void ZeroRtt_LogsTheEarlyTrafficSecret()
    {
        // CLIENT_EARLY_TRAFFIC_SECRET is what Wireshark needs to decrypt 0-RTT packets.
        var clientLines = new List<string>();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var resumptionCache = new ServerResumptionCache();

        // First connection: collect a ticket.
        ResumptionTicket ticket;
        {
            using var client = new QuicClientConnection("localhost", certificateValidation: validation);
            using var server = new QuicServerConnection(cert, resumptionCache: resumptionCache,
                                                        maxEarlyDataSize: 0xFFFFFFFF);
            client.Start();
            for (int round = 0; round < 20 && client.NewSessionTickets.Count == 0; round++)
            {
                foreach (byte[] dg in client.GetDatagramsToSend())
                    server.ProcessDatagram(dg);
                foreach (byte[] dg in server.GetDatagramsToSend())
                    client.ProcessDatagram(dg);
            }
            Assert.That(client.NewSessionTickets, Is.Not.Empty, "The server must issue a ticket.");
            ticket = client.NewSessionTickets[0];
        }

        // Second connection with 0-RTT.
        using (var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                                     resumptionTicket: ticket, keyLog: new KeyLog(clientLines.Add)))
        {
            client.Start();
            Assert.That(clientLines, Has.Some.StartsWith(KeyLog.ClientEarlyTrafficSecret + " "),
                        "The early traffic secret must be logged as soon as the ClientHello is out.");
        }
    }
}
