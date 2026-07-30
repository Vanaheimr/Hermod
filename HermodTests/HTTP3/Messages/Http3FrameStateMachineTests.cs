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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Messages;

/// <summary>
/// Frame/stream state machine of HTTP/3 (RFC 9114 §4.1, §6.2, §7.2): protocol violations by the
/// peer MUST be answered as connection errors with the matching H3 error code (§8.1) —
/// as a CONNECTION_CLOSE type 0x1d (application error). The "evil" peer is emulated with a raw
/// QUIC connection that writes HTTP/3 bytes by hand.
/// </summary>
[TestFixture]
public class Http3FrameStateMachineTests
{
    // ---- Client violations ⇒ the SERVER must close -----------------------------------------

    [Test]
    public void DataBeforeHeaders_OnRequestStream_IsFrameUnexpected()
        => AssertServerCloses(
            client => client.OpenBidirectionalStream().Write(Http3Frames.Build(Http3FrameType.Data, [1, 2, 3])),
            Http3Error.FrameUnexpected);

    [Test]
    public void ReservedHttp2FrameType_OnRequestStream_IsFrameUnexpected()
        => AssertServerCloses( // 0x06 was HTTP/2 PING — reserved in HTTP/3 (§7.2.8)
            client => client.OpenBidirectionalStream().Write(Http3Frames.Build(0x06, [])),
            Http3Error.FrameUnexpected);

    [Test]
    public void Settings_OnRequestStream_IsFrameUnexpected()
        => AssertServerCloses(
            client => client.OpenBidirectionalStream().Write(Http3Frames.Build(Http3FrameType.Settings, [])),
            Http3Error.FrameUnexpected);

    [Test]
    public void PushPromise_FromClient_IsFrameUnexpected()
        => AssertServerCloses(
            client => client.OpenBidirectionalStream().Write(Http3Frames.Build(Http3FrameType.PushPromise, [0x00])),
            Http3Error.FrameUnexpected);

    [Test]
    public void ControlStream_FirstFrameNotSettings_IsMissingSettings()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.GoAway, [0x00]));
        }, Http3Error.MissingSettings);

    [Test]
    public void SecondControlStream_IsStreamCreationError()
        => AssertServerCloses(client =>
        {
            QuicStream first = client.OpenUnidirectionalStream();
            first.Write([(byte)Http3StreamType.Control]);
            first.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            QuicStream second = client.OpenUnidirectionalStream();
            second.Write([(byte)Http3StreamType.Control]);
        }, Http3Error.StreamCreationError);

    [Test]
    public void ClientInitiatedPushStream_IsStreamCreationError()
        => AssertServerCloses(
            client => client.OpenUnidirectionalStream().Write([(byte)Http3StreamType.Push]),
            Http3Error.StreamCreationError);

    [Test]
    public void ReservedHttp2Setting_IsSettingsError()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, BuildSettingsPayload((0x02, 0)))); // reserved
        }, Http3Error.SettingsError);

    [Test]
    public void DuplicateSettingIdentifier_IsSettingsError()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings,
                BuildSettingsPayload((Http3Setting.QpackMaxTableCapacity, 0), (Http3Setting.QpackMaxTableCapacity, 0))));
        }, Http3Error.SettingsError);

    [Test]
    public void ClosingControlStream_IsClosedCriticalStream()
        => AssertServerCloses(client =>
        {
            QuicStream control = client.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            control.Finish(); // §6.2.1: the control stream must NEVER end
        }, Http3Error.ClosedCriticalStream);

    [Test]
    public void TruncatedFrame_AtCleanStreamEnd_IsFrameError()
        => AssertServerCloses(client =>
        {
            QuicStream stream = client.OpenBidirectionalStream();
            // The frame header promises 10 payload bytes, only 3 follow — then a clean FIN (§7.1).
            stream.Write([(byte)Http3FrameType.Data, 10, 1, 2, 3]);
            stream.Finish();
        }, Http3Error.FrameError);

    // ---- Tolerance: grease MUST be ignored (§7.2.8, §7.2.4.1, §9) -------------------------

    [Test]
    public void GreaseFrameAndGreaseSetting_AreIgnored_RequestSucceeds()
    {
        bool handled = false;
        (QuicClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            RawClientHandshake(_ => { handled = true; });
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using Http3ServerConnection s = server;

        // Control stream with SETTINGS including a grease setting (0x1f·N + 0x21).
        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, BuildSettingsPayload((0x1f * 7 + 0x21, 42))));

        // Request stream: first a grease frame (0x21), then real HEADERS (statically encoded), FIN.
        QuicStream request = client.OpenBidirectionalStream();
        request.Write(Http3Frames.Build(0x21, [0xaa, 0xbb]));
        byte[] headerBlock = QpackEncoder.Encode(
        [
            new HeaderField(":method", "GET"),
            new HeaderField(":scheme", "https"),
            new HeaderField(":authority", "localhost"),
            new HeaderField(":path", "/"),
        ]);
        request.Write(Http3Frames.Build(Http3FrameType.Headers, headerBlock));
        request.Finish();

        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(handled, Is.True, "The request must be answered despite the grease frame/setting.");
        Assert.That(server.IsClosing, Is.False, "Grease must NOT be a connection error (§9).");
    }

    // ---- Server violations ⇒ the CLIENT must close -----------------------------------------

    [Test]
    public void MaxPushId_SentToClient_IsFrameUnexpected()
        => AssertClientCloses(server =>
        {
            QuicStream control = server.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            control.Write(Http3Frames.Build(Http3FrameType.MaxPushId, [0x08])); // §7.2.7: client→server only
        }, Http3Error.FrameUnexpected);

    [Test]
    public void GoAway_WithNonRequestStreamId_IsIdError()
        => AssertClientCloses(server =>
        {
            QuicStream control = server.OpenUnidirectionalStream();
            control.Write([(byte)Http3StreamType.Control]);
            control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
            control.Write(Http3Frames.Build(Http3FrameType.GoAway, [0x03])); // 0b11 = server uni ⇒ illegal (§7.2.6)
        }, Http3Error.IdError);

    // ---- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// Raw QUIC client against our HTTP/3 server: <paramref name="misbehave"/> writes the evil
    /// bytes, after which the server MUST close with the H3 error code <paramref name="expectedError"/>.
    /// </summary>
    private static void AssertServerCloses(Action<QuicClientConnection> misbehave, ulong expectedError)
    {
        (QuicClientConnection client, Http3ServerConnection server, ServerCertificate cert) = RawClientHandshake(null);
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using Http3ServerConnection s = server;

        misbehave(client);
        for (int round = 0; round < 10 && !server.IsClosing; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close the connection.");
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.IsApplicationError, Is.True, "H3 errors are application errors (type 0x1d).");
        Assert.That(client.PeerCloseFrame.ErrorCode, Is.EqualTo(expectedError));
    }

    /// <summary>
    /// Raw QUIC server against our HTTP/3 client: <paramref name="misbehave"/> writes the evil
    /// bytes, after which the client MUST close with the H3 error code <paramref name="expectedError"/>.
    /// </summary>
    private static void AssertClientCloses(Action<QuicServerConnection> misbehave, ulong expectedError)
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

        misbehave(server);
        for (int round = 0; round < 10 && !client.IsClosing; round++)
            Pump(client, server);
        Pump(client, server); // still deliver the client's CONNECTION_CLOSE

        Assert.That(client.IsClosing, Is.True, "The client must close the connection.");
        Assert.That(server.PeerCloseFrame, Is.Not.Null);
        Assert.That(server.PeerCloseFrame!.IsApplicationError, Is.True, "H3 errors are application errors (type 0x1d).");
        Assert.That(server.PeerCloseFrame.ErrorCode, Is.EqualTo(expectedError));
    }

    private static (QuicClientConnection, Http3ServerConnection, ServerCertificate) RawClientHandshake(Action<Http3Request>? onRequest)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, request =>
        {
            onRequest?.Invoke(request);
            return new Http3Response { Status = 200, Body = [] };
        });
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server, cert);
    }

    private static byte[] BuildSettingsPayload(params (ulong Id, ulong Value)[] settings)
    {
        var writer = new BufferWriter(32);
        try
        {
            foreach ((ulong id, ulong value) in settings)
            {
                writer.WriteVarInt(id);
                writer.WriteVarInt(value);
            }
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    private static void Pump(QuicClientConnection client, Http3ServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump(Http3ClientConnection client, QuicServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
