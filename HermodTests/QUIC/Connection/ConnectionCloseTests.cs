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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using System.Threading;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests for immediate close and draining (RFC 9000 §10.2): a CONNECTION_CLOSE puts the sender into
/// the closing state, the receiver into the draining state; after 3·PTO the final closed state follows.
/// </summary>
[TestFixture]
public class ConnectionCloseTests
{
    private static (QuicClientConnection client, QuicServerConnection server) Handshaken(ServerCertificate cert)
    {
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        return (client, server);
    }

    [Test]
    public void Close_PutsSenderInClosing_AndReceiverInDraining_PropagatingErrorAndReason()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        client.Close(TransportError.ApplicationError, "goodbye");
        Assert.That(client.IsClosing, Is.True);

        // One round trip brings the CONNECTION_CLOSE to the server.
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);

        Assert.That(server.IsDraining, Is.True, "Receiving a CONNECTION_CLOSE must lead into the draining state.");
        Assert.That(server.PeerCloseFrame, Is.Not.Null);
        Assert.That(server.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.ApplicationError));
        Assert.That(server.PeerCloseFrame.ReasonPhrase, Is.EqualTo("goodbye"));
        Assert.That(server.PeerCloseFrame.IsApplicationError, Is.False); // transport CONNECTION_CLOSE (type 0x1c)

        // Draining: the server sends no more datagrams whatsoever (RFC 9000 §10.2.2).
        Assert.That(server.GetDatagramsToSend(), Is.Empty);
    }

    [Test]
    public void ClosingEndpoint_ResendsConnectionClose_OnIncomingPacket_ButNotOtherwise()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        client.Close();
        IReadOnlyList<byte[]> first = client.GetDatagramsToSend();
        Expect.Single(first);                       // exactly one close packet
        Assert.That(client.GetDatagramsToSend(), Is.Empty);  // not again without a trigger

        // An incoming packet in the closing state triggers a renewed CONNECTION_CLOSE (RFC 9000 §10.2.1).
        client.ProcessDatagram(first[0]);
        Expect.Single(client.GetDatagramsToSend());
    }

    [Test]
    public void ClosingConnection_TransitionsToClosed_AfterCloseTimeout()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        (QuicClientConnection client, QuicServerConnection server) = Handshaken(cert);
        using var _ = client;
        using var __ = server;

        client.Close();
        Assert.That(client.IsClosing, Is.True);
        Assert.That(client.IsClosed, Is.False);

        // After more than 3·PTO (tiny in-process) the final closed state follows.
        Thread.Sleep(400);
        client.CheckIdleTimeout(); // drives the transition

        Assert.That(client.IsClosed, Is.True);
        Assert.That(client.IsClosing, Is.False);
        Assert.That(client.GetDatagramsToSend(), Is.Empty);
    }
}
