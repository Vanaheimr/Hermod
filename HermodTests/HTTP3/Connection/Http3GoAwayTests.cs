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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// GOAWAY / graceful shutdown (RFC 9114 §5.2): the server announces the first no-longer-accepted
/// request stream ID, serves in-flight work to completion, rejects later work with
/// H3_REQUEST_REJECTED; the client starts no new requests and treats rejected ones as repeatable.
/// </summary>
[TestFixture]
public class Http3GoAwayTests
{
    [Test]
    public void GracefulShutdown_EndToEnd_ClientStopsNewRequests_ServerClosesWithH3NoError()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        Http3Response Handler(Http3Request request) => new()
        {
            Status = 200,
            Body = System.Text.Encoding.UTF8.GetBytes("ok"),
        };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        // The first request runs through normally.
        ulong first = client.SendRequest(Http3Request.Get("localhost", "/one"));
        Http3Response? response = null;
        for (int round = 0; round < 30 && response is null; round++)
        {
            Pump(client, server);
            client.TryGetResponse(first, out response);
        }
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));

        // The server initiates the graceful shutdown: GOAWAY with the next request stream ID (0 + 4).
        server.InitiateGracefulShutdown();
        Assert.That(server.GoAwaySent, Is.EqualTo(4UL));
        for (int round = 0; round < 10 && client.GoAwayStreamId is null; round++)
            Pump(client, server);

        // The client knows the limit and MUST refuse new requests (§5.2).
        Assert.That(client.GoAwayStreamId, Is.EqualTo(4UL));
        Assert.Throws<InvalidOperationException>(() => client.SendRequest(Http3Request.Get("localhost", "/two")));

        // Everything served ⇒ the server closes decently with H3_NO_ERROR (type 0x1d).
        Assert.That(server.HasPendingRequests, Is.False);
        server.CloseGracefully();
        for (int round = 0; round < 10 && client.PeerCloseFrame is null; round++)
            Pump(client, server);

        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.IsApplicationError, Is.True);
        Assert.That(client.PeerCloseFrame.ErrorCode, Is.EqualTo(Http3Error.NoError));
        Assert.That(client.IsDraining, Is.True);
    }

    [Test]
    public void LateRequest_AfterGoAway_IsRejectedWithRequestRejected()
    {
        // A "misbehaved" raw QUIC client ignores the GOAWAY and sends a request anyway —
        // the server MUST reject it (H3_REQUEST_REJECTED) without invoking the handler (§5.2).
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        int handled = 0;
        using var server = new Http3ServerConnection(cert, request => { handled++; return new Http3Response { Status = 200, Body = [] }; });
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        // Proper control stream + first request (stream 0) — still served.
        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        QuicStream firstRequest = client.OpenBidirectionalStream();
        firstRequest.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeGetHeaders("/one")));
        firstRequest.Finish();
        for (int round = 0; round < 10; round++)
            Pump(client, server);
        Assert.That(handled, Is.EqualTo(1));

        server.InitiateGracefulShutdown();
        Assert.That(server.GoAwaySent, Is.EqualTo(4UL));
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        // The raw client sends a second request (stream 4) ANYWAY.
        QuicStream lateRequest = client.OpenBidirectionalStream();
        lateRequest.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeGetHeaders("/two")));
        lateRequest.Finish();
        for (int round = 0; round < 10 && !lateRequest.IsResetByPeer; round++)
            Pump(client, server);

        Assert.That(handled, Is.EqualTo(1)); // the handler was NOT invoked for the late request
        Assert.That(lateRequest.IsResetByPeer, Is.True, "The server must reset the late request.");
        Assert.That(lateRequest.PeerResetErrorCode, Is.EqualTo(Http3Error.RequestRejected));
        Assert.That(server.IsClosing, Is.False, "A late request is NOT a connection error.");
    }

    [Test]
    public void ClientInFlightRequests_AboveGoAwayId_AreMarkedRejected()
    {
        // Raw QUIC server: answers nothing, but sends a GOAWAY with ID 0 —
        // the already-running client request (stream 0) thus counts as NOT processed.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        ulong streamId = client.SendRequest(Http3Request.Get("localhost", "/hangs"));
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        QuicStream control = server.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        control.Write(Http3Frames.Build(Http3FrameType.GoAway, [0x00])); // ID 0: NOTHING was processed
        for (int round = 0; round < 10 && !client.IsRequestRejected(streamId); round++)
            Pump(client, server);

        Assert.That(client.IsRequestRejected(streamId), Is.True, "The in-flight request must count as rejected.");
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);
        // §5.2: the client cleans up the transport state (send-side reset visible at the raw server).
        for (int round = 0; round < 10 && !server.Streams[streamId].IsResetByPeer; round++)
            Pump(client, server);
        Assert.That(server.Streams[streamId].IsResetByPeer, Is.True);
    }

    [Test]
    public void GoAwayIdentifierIncrease_IsIdError()
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
        control.Write(Http3Frames.Build(Http3FrameType.GoAway, [0x00]));
        for (int round = 0; round < 5; round++)
            Pump(client, server);
        Assert.That(client.GoAwayStreamId, Is.EqualTo(0UL));

        // §5.2: the GOAWAY ID must NEVER grow ⇒ H3_ID_ERROR.
        control.Write(Http3Frames.Build(Http3FrameType.GoAway, [0x08]));
        for (int round = 0; round < 10 && !client.IsClosing; round++)
            Pump(client, server);
        Pump(client, server); // still deliver the client's CONNECTION_CLOSE

        Assert.That(client.IsClosing, Is.True);
        Assert.That(server.PeerCloseFrame, Is.Not.Null);
        Assert.That(server.PeerCloseFrame!.ErrorCode, Is.EqualTo(Http3Error.IdError));
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static byte[] EncodeGetHeaders(string path)
        => QpackEncoder.Encode(
        [
            new HeaderField(":method", "GET"),
            new HeaderField(":scheme", "https"),
            new HeaderField(":authority", "localhost"),
            new HeaderField(":path", path),
        ]);

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
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
