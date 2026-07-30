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
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// RESET_STREAM/STOP_SENDING (RFC 9000 §2.4, §3.5, §19.4/§19.5) and the HTTP/3 request
/// cancellation built on top of them (RFC 9114 §4.1.1).
/// </summary>
[TestFixture]
public class StreamCancellationTests
{
    // ---- Unit: send/receive buffers --------------------------------------------------------

    [Test]
    public void SendBuffer_Reset_DropsPendingData_AndEmitsSingleResetFrame()
    {
        var send = new StreamSendBuffer(4) { MaxData = 100 };
        send.Write([1, 2, 3, 4, 5]);
        Assert.That(send.NextFrame(3), Is.Not.Null); // 3 bytes sent ⇒ final size = 3 (RFC 9000 §4.5)

        send.Reset(0x0c);
        Assert.That(send.IsReset, Is.True);
        Assert.That(send.HasPending, Is.False);                    // unsent data discarded
        Assert.That(send.NextFrame(100), Is.Null);                 // §19.4: no more STREAM frames
        var frame = Expect.Type<ResetStreamFrame>(send.TakeResetFrame(peerSupportsResetAt: false));
        Assert.That(frame.FinalSize, Is.EqualTo(3UL));
        Assert.That(frame.ApplicationErrorCode, Is.EqualTo(0x0cUL));
        Assert.That(send.TakeResetFrame(peerSupportsResetAt: false), Is.Null);   // retrievable only once
        send.Write([9]);                                  // ignored after the reset
        Assert.That(send.HasPending, Is.False);
    }

    [Test]
    public void ReceiveBuffer_Reset_ValidatesFinalSize_AndNeverCompletes()
    {
        // Final size smaller than what was already seen ⇒ FINAL_SIZE_ERROR (RFC 9000 §4.5).
        var recv1 = new StreamReceiveBuffer();
        Assert.That(recv1.Receive(0, new byte[10], fin: false), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv1.Reset(0x0c, finalSize: 5), Is.EqualTo(StreamReceiveResult.FinalSizeError));

        // Contradiction with a known final size (FIN) ⇒ FINAL_SIZE_ERROR.
        var recv2 = new StreamReceiveBuffer();
        Assert.That(recv2.Receive(0, new byte[4], fin: true), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv2.Reset(0x0c, finalSize: 8), Is.EqualTo(StreamReceiveResult.FinalSizeError));

        // Final size above the flow-control window ⇒ FLOW_CONTROL_ERROR (RFC 9000 §4.1).
        var recv3 = new StreamReceiveBuffer { MaxData = 4 };
        Assert.That(recv3.Reset(0x0c, finalSize: 5), Is.EqualTo(StreamReceiveResult.FlowControlError));

        // Valid reset: buffered data discarded, error code remembered, NEVER "complete".
        var recv4 = new StreamReceiveBuffer();
        Assert.That(recv4.Receive(0, new byte[10], fin: false), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv4.Reset(0x0c, finalSize: 20), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv4.ResetReceived, Is.True);
        Assert.That(recv4.ResetErrorCode, Is.EqualTo(0x0cUL));
        Assert.That(recv4.ReadAvailable(), Is.Empty);
        Assert.That(recv4.IsComplete, Is.False);
        Assert.That(recv4.BytesConsumed, Is.EqualTo(20UL)); // §4.5: the final size counts as consumed credit
        Assert.That(recv4.Reset(0x0c, finalSize: 20), Is.EqualTo(StreamReceiveResult.Ok)); // idempotent
    }

    // ---- Integration (QUIC): STOP_SENDING solicits RESET_STREAM with a copied code ---------

    [Test]
    public void StopSending_SolicitsResetStream_WithCopiedErrorCode()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // The client sends on a bidi stream; the server aborts reading (§3.5).
        QuicStream clientStream = client.OpenBidirectionalStream();
        clientStream.Write([1, 2, 3]);
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        QuicStream serverStream = server.Streams[clientStream.Id.Value];
        serverStream.AbortRead(0x77);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        // The client received the STOP_SENDING and MUST answer with RESET_STREAM (§3.5),
        // SHOULD copy the error code; the server sees the reset.
        Assert.That(clientStream.PeerStopSendingErrorCode, Is.EqualTo(0x77UL));
        Assert.That(clientStream.Send.IsReset, Is.True);
        Assert.That(serverStream.IsResetByPeer, Is.True);
        Assert.That(serverStream.PeerResetErrorCode, Is.EqualTo(0x77UL));
        Assert.That(server.IsClosing, Is.False);
        Assert.That(client.IsClosing, Is.False);
    }

    [Test]
    public void StopSending_OnReceiveOnlyStream_IsStreamStateError()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // The client "abuses" the API: STOP_SENDING on its OWN uni stream (the peer never sends
        // there). The server MUST terminate the connection with STREAM_STATE_ERROR (RFC 9000 §19.5).
        QuicStream uni = client.OpenUnidirectionalStream();
        uni.Write([1]);
        for (int round = 0; round < 5; round++)
            Pump(client, server);
        uni.AbortRead(0x01);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close due to STREAM_STATE_ERROR.");
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.StreamStateError));
    }

    [Test]
    public void ResetStream_OnSendOnlyStream_IsStreamStateError()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // Mirror image: the server "resets" the client-initiated uni stream on which it never sends.
        // The client MUST close with STREAM_STATE_ERROR (RFC 9000 §19.4).
        QuicStream uni = client.OpenUnidirectionalStream();
        uni.Write([1]);
        for (int round = 0; round < 5; round++)
            Pump(client, server);
        server.Streams[uni.Id.Value].Reset(0x01);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(client.IsClosing, Is.True, "The client must close due to STREAM_STATE_ERROR.");
        Assert.That(server.PeerCloseFrame, Is.Not.Null);
        Assert.That(server.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.StreamStateError));
    }

    // ---- Integration (HTTP/3): request cancellation (RFC 9114 §4.1.1) ---------------------

    [Test]
    public void CancelRequest_MidResponse_StopsServer_AndConnectionRemainsUsable()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        byte[] bigBody = new byte[300_000];
        Http3Response Handler(Http3Request request) => new()
        {
            Status = 200,
            Body = request.Path == "/big" ? bigBody : System.Text.Encoding.UTF8.GetBytes("small"),
        };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        // The cancelled response leaves a burst of unacknowledged packets in flight, and the second
        // request only moves once the congestion window frees up again. Acknowledgments may be held
        // back for up to max_ack_delay (RFC 9000 §13.2.2), a deadline a pump loop on a standing clock
        // never reaches — so the clock steps past it between rounds.
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation,
                                                     timeProvider: clock);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        // Request a large response, let it run only briefly (slow start ⇒ only a fraction has arrived) …
        ulong streamId = client.SendRequest(Http3Request.Get("localhost", "/big"));
        for (int round = 0; round < 3; round++)
            Pump(client, server);
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);

        // … and cancel (§4.1.1: RESET_STREAM + STOP_SENDING with H3_REQUEST_CANCELLED).
        client.CancelRequest(streamId);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(client.IsRequestCancelled(streamId), Is.True);
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);
        // The server reset its response side and sends nothing more on the stream.
        QuicStream serverStream = server.Quic.Streams[streamId];
        Assert.That(serverStream.Send.IsReset, Is.True);
        Assert.That(serverStream.Send.HasPending, Is.False, "Unsent response data must be discarded.");
        // The client sees the server reset (H3_REQUEST_CANCELLED, via the copied STOP_SENDING code).
        Assert.That(client.RequestResetErrorCode(streamId), Is.EqualTo(Http3Error.RequestCancelled));

        // The CONNECTION lives on: a second request runs through normally.
        ulong second = client.SendRequest(Http3Request.Get("localhost", "/small"));
        Http3Response? response = null;
        for (int round = 0; round < 50 && response is null; round++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(30));
            Pump(client, server);
            client.TryGetResponse(second, out response);
        }
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("small"));
    }

    [Test]
    public void CancelRequest_SurvivesPacketLoss_ViaRetransmission()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        byte[] bigBody = new byte[300_000];
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = bigBody };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        ulong streamId = client.SendRequest(Http3Request.Get("localhost", "/big"));
        for (int round = 0; round < 3; round++)
            Pump(client, server);

        client.CancelRequest(streamId);

        // Drop the FIRST client flight after the cancellation (carries RESET_STREAM + STOP_SENDING) —
        // loss recovery (PTO) must reliably redeliver both frames (RFC 9000 §19.4/§3.5).
        client.CheckTimeouts();
        foreach (byte[] _ in client.GetDatagramsToSend()) { /* dropped */ }

        QuicStream serverStream = server.Quic.Streams[streamId];
        for (int round = 0; round < 40 && !serverStream.IsResetByPeer; round++)
        {
            Thread.Sleep(30); // let the PTO elapse
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(serverStream.IsResetByPeer, Is.True, "The RESET_STREAM must arrive via retransmission.");
        Assert.That(serverStream.Send.IsReset, Is.True, "The STOP_SENDING must arrive via retransmission.");
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) HandshakeInProcess()
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        return (client, server, cert);
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
