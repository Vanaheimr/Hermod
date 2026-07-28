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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests for connection ID rotation (RFC 9000 §5.1, §19.15/§19.16): the <see cref="ConnectionIdManager"/>
/// in isolation as well as a full round trip in which the server issues a CID and the client switches
/// its DCID to it.
/// </summary>
[TestFixture]
public class ConnectionIdRotationTests
{
    private static ConnectionId Cid(params byte[] bytes) => new(bytes);
    private static byte[] Token() => new byte[16];

    [Test]
    public void Issue_RespectsActiveConnectionIdLimit()
    {
        var m = new ConnectionIdManager(Cid(1, 1));
        Assert.That(m.Issue(Cid(2, 2), Token(), activeLimit: 2), Is.Not.Null); // sequence 0 + one more
        Assert.That(m.Issue(Cid(3, 3), Token(), activeLimit: 2), Is.Null);    // limit reached
        Assert.That(m.LocalCount, Is.EqualTo(2));
    }

    [Test]
    public void RetireLocal_RemovesIssuedConnectionId()
    {
        var m = new ConnectionIdManager(Cid(1, 1));
        m.Issue(Cid(2, 2), Token(), activeLimit: 5);
        Assert.That(m.LocalCount, Is.EqualTo(2));
        m.RetireLocal(1);
        Assert.That(m.LocalCount, Is.EqualTo(1));
    }

    [Test]
    public void OnNewConnectionId_AddsRemote_AndRetirePriorTo_SwitchesDcid()
    {
        var m = new ConnectionIdManager(Cid(1, 1));
        m.InitializeRemote(Cid(0x10, 0x10)); // remote sequence 0

        var frame = new NewConnectionIdFrame(SequenceNumber: 1, RetirePriorTo: 1, Cid(0x11, 0x11).ToArray(), Token());
        List<RetireConnectionIdFrame> retires = m.OnNewConnectionId(frame, out bool changed, out ConnectionId newDcid);

        Expect.Single(retires);
        Assert.That(retires[0].SequenceNumber, Is.EqualTo(0ul)); // seq 0 retired
        Assert.That(changed, Is.True);
        Assert.That(newDcid, Is.EqualTo(Cid(0x11, 0x11)));        // DCID switched to seq 1
        Assert.That(m.CurrentRemoteSequence, Is.EqualTo(1ul));
    }

    [Test]
    public void Rotate_SwitchesToHigherRemote_AndRetiresOld()
    {
        var m = new ConnectionIdManager(Cid(1, 1));
        m.InitializeRemote(Cid(0x10, 0x10));
        m.OnNewConnectionId(new NewConnectionIdFrame(1, 0, Cid(0x11, 0x11).ToArray(), Token()), out _, out _);

        (RetireConnectionIdFrame Retire, ConnectionId NewDcid)? rotation = m.Rotate();
        Assert.That(rotation, Is.Not.Null);
        Assert.That(rotation!.Value.NewDcid, Is.EqualTo(Cid(0x11, 0x11)));
        Assert.That(rotation.Value.Retire.SequenceNumber, Is.EqualTo(0ul));
        Assert.That(m.CurrentRemoteSequence, Is.EqualTo(1ul));
        Assert.That(m.Rotate(), Is.Null); // no further ID available
    }

    // ---- Integration ----------------------------------------------------------------------

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    [Test]
    public void ServerIssuesConnectionId_ClientRotatesDcid_AndDataStillFlows()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        Pump(client, server); // let the remaining data drain

        // The server issues an additional connection ID (NEW_CONNECTION_ID).
        ConnectionId? issued = server.IssueConnectionId();
        Assert.That(issued, Is.Not.Null);
        Assert.That(server.LocalConnectionIdCount, Is.EqualTo(2)); // seq 0 + the new one

        // Bring the frame to the client; the client learns the remote CID.
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        Assert.That(client.RemoteConnectionIdCount, Is.EqualTo(2));

        // The client switches its DCID to the new ID (and retires the old one via RETIRE_CONNECTION_ID).
        Assert.That(client.RotateDestinationConnectionId(), Is.True);
        Assert.That(client.DestinationConnectionId, Is.EqualTo(issued!.Value));

        // From now on the client's packets carry the new DCID — the server must keep accepting them.
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([9, 8, 7, 6]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        // The data arrived under the new connection ID.
        Assert.That(server.Streams.ContainsKey(stream.Id.Value), Is.True);
        Assert.That(server.Streams[stream.Id.Value].Read(), Is.EqualTo([9, 8, 7, 6]));

        // The server retired its original (seq 0) connection ID upon RETIRE_CONNECTION_ID.
        Assert.That(server.LocalConnectionIdCount, Is.EqualTo(1));
    }
}
