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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// A unidirectional stream of a type we do not know, grease among them, is no connection error
/// (RFC 9114 §6.2), and it is let go: we stop reading it, unless the peer has finished it, and
/// QUIC closes it once its end is known. So is a stream that ends before its type has arrived.
/// Both connections used to read such a stream and drop its data for as long as they lived. The
/// peer is a raw QUIC connection that writes the HTTP/3 bytes by hand.
/// </summary>
[TestFixture]
public class UnknownStreamTypeTests
{

    /// <summary>
    /// A reserved stream type (0x1f·N + 0x21, RFC 9114 §6.2.3), in a two-byte varint.
    /// </summary>
    private const ulong Grease = 0x1f * 3 + 0x21;

    #region Server

    [Test]
    public void AnUnknownStream_IsNoLongerRead_AndLetGo()
    {

        (QuicClientConnection client, Http3ServerConnection server, ServerCertificate cert) = RawClientHandshake();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using Http3ServerConnection s = server;
        int before = server.RememberedPeerUniStreamsForTest;

        QuicStream unknown = client.OpenUnidirectionalStream();
        unknown.Write([.. VarInt(Grease), .. "never finished"u8]);
        Pump(client, server, 6);

        Assert.That(server.IsClosing, Is.False, "an unknown stream type is no connection error");
        Assert.That(unknown.Send.IsReset, Is.True, "the server's STOP_SENDING, answered with a reset");
        Assert.That(unknown.Send.ResetErrorCode, Is.EqualTo(Http3Error.StreamCreationError));
        Assert.That(server.Quic.IsStreamClosed(unknown.Id.Value), Is.True, "the server's QUIC stream");
        Assert.That(server.RememberedPeerUniStreamsForTest, Is.EqualTo(before), "the streams the server remembers");

    }

    [Test]
    public void AFinishedUnknownStream_IsLetGo_WithoutAStopSending()
    {

        (QuicClientConnection client, Http3ServerConnection server, ServerCertificate cert) = RawClientHandshake();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using Http3ServerConnection s = server;
        int before = server.RememberedPeerUniStreamsForTest;

        QuicStream unknown = client.OpenUnidirectionalStream();
        unknown.Write([.. VarInt(Grease), .. "all there is"u8]);
        unknown.Finish();
        Pump(client, server, 6);

        Assert.That(server.IsClosing, Is.False);
        Assert.That(unknown.Send.IsReset, Is.False, "nothing to stop: the stream is finished");
        Assert.That(server.Quic.IsStreamClosed(unknown.Id.Value), Is.True, "the server's QUIC stream");
        Assert.That(server.RememberedPeerUniStreamsForTest, Is.EqualTo(before), "the streams the server remembers");

    }

    [TestCase("finished empty")]
    [TestCase("finished within its type")]
    [TestCase("reset")]
    public void AStreamThatEndsBeforeItsType_IsLetGo(string how)
    {

        (QuicClientConnection client, Http3ServerConnection server, ServerCertificate cert) = RawClientHandshake();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using Http3ServerConnection s = server;
        int before = server.RememberedPeerUniStreamsForTest;

        QuicStream untyped = client.OpenUnidirectionalStream();
        switch (how)
        {
            case "finished empty":
                untyped.Finish();
                break;
            case "finished within its type":
                untyped.Write(VarInt(Grease)[..1]); // the first byte of two
                untyped.Finish();
                break;
            case "reset":
                untyped.Reset(Http3Error.RequestCancelled);
                break;
        }
        Pump(client, server, 6);

        Assert.That(server.IsClosing, Is.False, "RFC 9114 §6.2: to be tolerated");
        Assert.That(server.Quic.IsStreamClosed(untyped.Id.Value), Is.True, "the server's QUIC stream");
        Assert.That(server.RememberedPeerUniStreamsForTest, Is.EqualTo(before), "the streams the server remembers");

    }

    #endregion

    #region Client

    [Test]
    public void AnUnknownStreamFromTheServer_IsNoLongerRead_AndLetGo()
    {

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        QuicStream control = server.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        Pump(client, server, 3);
        int before = client.RememberedPeerUniStreamsForTest;

        QuicStream unknown = server.OpenUnidirectionalStream();
        unknown.Write([.. VarInt(Grease), .. "never finished"u8]);
        Pump(client, server, 6);

        Assert.That(client.IsClosing, Is.False, "an unknown stream type is no connection error");
        Assert.That(unknown.Send.IsReset, Is.True, "the client's STOP_SENDING, answered with a reset");
        Assert.That(unknown.Send.ResetErrorCode, Is.EqualTo(Http3Error.StreamCreationError));
        Assert.That(client.Quic.IsStreamClosed(unknown.Id.Value), Is.True, "the client's QUIC stream");
        Assert.That(client.RememberedPeerUniStreamsForTest, Is.EqualTo(before), "the streams the client remembers");

    }

    #endregion

    #region (helpers)

    /// <summary>
    /// Our server against a raw QUIC client, which has opened its control stream.
    /// </summary>
    private static (QuicClientConnection, Http3ServerConnection, ServerCertificate) RawClientHandshake()
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] });
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        Pump(client, server, 3);
        return (client, server, cert);
    }

    private static byte[] VarInt(ulong value)
    {
        var writer = new BufferWriter(8);
        try
        {
            writer.WriteVarInt(value);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    private static void Pump(QuicClientConnection client, Http3ServerConnection server, int rounds = 1)
    {
        for (int round = 0; round < rounds; round++)
        {
            client.CheckLossDetectionTimeout();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
    }

    private static void Pump(Http3ClientConnection client, QuicServerConnection server, int rounds = 1)
    {
        for (int round = 0; round < rounds; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
    }

    #endregion

}
