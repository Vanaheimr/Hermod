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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Streams;

/// <summary>
/// RESET_STREAM_AT (draft-ietf-quic-reliable-stream-reset): the RESET_STREAM with guaranteed
/// partial delivery up to a reliable size, plus the associated reset_stream_at transport parameter.
/// </summary>
[TestFixture]
public class ResetStreamAtTests
{
    // ---- Unit: frame encoding --------------------------------------------------------------

    [Test]
    public void ResetStreamAtFrame_RoundTrips()
    {
        var original = new ResetStreamAtFrame(StreamId: 8, ApplicationErrorCode: 0x0c, FinalSize: 1000, ReliableSize: 40);
        byte[] bytes = FrameParser.Serialize([original]);

        Assert.That(FrameParser.TryParseAll(bytes, out var frames), Is.EqualTo(FrameParseResult.Ok));
        var frame = Expect.Type<ResetStreamAtFrame>(Expect.Single(frames));
        Assert.That(frame.StreamId, Is.EqualTo(8UL));
        Assert.That(frame.ApplicationErrorCode, Is.EqualTo(0x0cUL));
        Assert.That(frame.FinalSize, Is.EqualTo(1000UL));
        Assert.That(frame.ReliableSize, Is.EqualTo(40UL));
    }

    [Test]
    public void ResetStreamAtFrame_TruncatedBody_IsEncodingError()
    {
        // Type 0x24 followed by only three of the four mandatory VarInts ⇒ FRAME_ENCODING_ERROR.
        var writer = new BufferWriter();
        try
        {
            writer.WriteVarInt(FrameType.ResetStreamAt);
            writer.WriteVarInt(8);
            writer.WriteVarInt(0x0c);
            writer.WriteVarInt(1000);
            Assert.That(FrameParser.TryParseAll(writer.WrittenSpan.ToArray(), out _), Is.EqualTo(FrameParseResult.EncodingError));
        }
        finally { writer.Dispose(); }
    }

    // ---- Unit: transport parameter --------------------------------------------------------

    [Test]
    public void TransportParameter_ResetStreamAt_IsAdvertisedAndParsed()
    {
        var tp = new TransportParameters();
        Assert.That(tp.ResetStreamAtSupported, Is.True); // default: active

        Assert.That(TransportParameters.TryDecode(tp.Encode(), out var decoded), Is.True);
        Assert.That(decoded!.PeerSupportsResetStreamAt, Is.True);

        // Turned off ⇒ parameter absent ⇒ the peer sees no support.
        var off = new TransportParameters { ResetStreamAtSupported = false };
        Assert.That(TransportParameters.TryDecode(off.Encode(), out var decodedOff), Is.True);
        Assert.That(decodedOff!.PeerSupportsResetStreamAt, Is.False);
    }

    [Test]
    public void TransportParameter_ResetStreamAt_NonEmptyValue_IsRejected()
    {
        // draft §3: a non-empty value is a TRANSPORT_PARAMETER_ERROR ⇒ decode fails.
        var writer = new BufferWriter();
        try
        {
            writer.WriteVarInt(0x1d);          // reset_stream_at
            writer.WriteVarInt(1);             // length 1 (not permitted)
            writer.WriteBytes(new byte[] { 0 });
            Assert.That(TransportParameters.TryDecode(writer.WrittenSpan.ToArray(), out _), Is.False);
        }
        finally { writer.Dispose(); }
    }

    // ---- Unit: receive buffer (reliable partial delivery) ---------------------------------

    [Test]
    public void ReceiveBuffer_ResetAt_DeliversReliablePrefix_ThenSurfacesReset()
    {
        var recv = new StreamReceiveBuffer { MaxData = 1000 };
        Assert.That(recv.Receive(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, fin: false), Is.EqualTo(StreamReceiveResult.Ok));

        // RESET_STREAM_AT: final size 100, but the first 4 bytes remain deliverable.
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 4), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ResetReceived, Is.True);
        Assert.That(recv.ReliableSize, Is.EqualTo(4UL));

        // Only the reliable 4 bytes are delivered, the rest is discarded.
        Assert.That(recv.ReadAvailable(), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        // §4.5: the full final size counts as consumed flow-control credit.
        Assert.That(recv.BytesConsumed, Is.EqualTo(100UL));
        Assert.That(recv.IsComplete, Is.False);
    }

    [Test]
    public void ReceiveBuffer_ResetAt_ReliableSizeGreaterThanFinalSize_IsFrameEncodingError()
    {
        var recv = new StreamReceiveBuffer { MaxData = 1000 };
        Assert.That(recv.ResetAt(0x0c, finalSize: 10, reliableSize: 20), Is.EqualTo(StreamReceiveResult.FrameEncodingError));
    }

    [Test]
    public void ReceiveBuffer_ResetAt_LaterFrameMayOnlyLowerReliableSize()
    {
        var recv = new StreamReceiveBuffer { MaxData = 1000 };
        Assert.That(recv.Receive(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, fin: false), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 6), Is.EqualTo(StreamReceiveResult.Ok));

        // §5.2: an increase (reordering) is ignored …
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 8), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ReliableSize, Is.EqualTo(6UL));

        // … a decrease is adopted and trims what is already buffered.
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 3), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ReliableSize, Is.EqualTo(3UL));
        Assert.That(recv.ReadAvailable(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void ReceiveBuffer_ResetAt_ChangedErrorCode_IsStreamStateError()
    {
        var recv = new StreamReceiveBuffer { MaxData = 1000 };
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 4), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ResetAt(0x0d, finalSize: 100, reliableSize: 4), Is.EqualTo(StreamReceiveResult.StreamStateError));
    }

    [Test]
    public void ReceiveBuffer_ResetAt_LateStreamFrameBeyondReliableSize_IsDropped()
    {
        var recv = new StreamReceiveBuffer { MaxData = 1000 };
        Assert.That(recv.ResetAt(0x0c, finalSize: 100, reliableSize: 4), Is.EqualTo(StreamReceiveResult.Ok));

        // A STREAM frame arriving afterwards straddles the reliable boundary: only up to 4 is delivered.
        Assert.That(recv.Receive(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, fin: false), Is.EqualTo(StreamReceiveResult.Ok));
        Assert.That(recv.ReadAvailable(), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    // ---- Unit: send buffer ----------------------------------------------------------------

    [Test]
    public void SendBuffer_ResetAt_EmitsResetStreamAt_WhenPeerSupports()
    {
        var send = new StreamSendBuffer(8) { MaxData = 1000 };
        send.Write(new byte[50]);
        Assert.That(send.NextFrame(50), Is.Not.Null); // 50 bytes sent ⇒ final size = 50

        send.ResetAt(0x0c, reliableSize: 20);
        Assert.That(send.IsResetAt, Is.True);
        Assert.That(send.ReliableSize, Is.EqualTo(20UL));

        var atFrame = Expect.Type<ResetStreamAtFrame>(send.TakeResetFrame(peerSupportsResetAt: true));
        Assert.That(atFrame.FinalSize, Is.EqualTo(50UL));
        Assert.That(atFrame.ReliableSize, Is.EqualTo(20UL));
    }

    [Test]
    public void SendBuffer_ResetAt_DegradesToResetStream_WhenPeerLacksSupport()
    {
        var send = new StreamSendBuffer(8) { MaxData = 1000 };
        send.Write(new byte[50]);
        Assert.That(send.NextFrame(50), Is.Not.Null);

        send.ResetAt(0x0c, reliableSize: 20);
        // Without peer support ⇒ an ordinary RESET_STREAM (without a delivery guarantee).
        var frame = Expect.Type<ResetStreamFrame>(send.TakeResetFrame(peerSupportsResetAt: false));
        Assert.That(frame.FinalSize, Is.EqualTo(50UL));
    }

    [Test]
    public void SendBuffer_ResetAt_ClampsReliableSizeToSentOffset()
    {
        var send = new StreamSendBuffer(8) { MaxData = 1000 };
        send.Write(new byte[10]);
        Assert.That(send.NextFrame(10), Is.Not.Null); // only 10 bytes sent

        // Only already-sent bytes can be guaranteed ⇒ reliable size clamped to 10.
        send.ResetAt(0x0c, reliableSize: 999);
        Assert.That(send.ReliableSize, Is.EqualTo(10UL));
    }

    // ---- Integration (QUIC): end to end via real frame processing -------------------------

    [Test]
    public void ResetStreamAt_EndToEnd_PeerReceivesReliablePrefix()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = HandshakeInProcess();
        using ServerCertificate _ = cert;
        using QuicClientConnection c = client;
        using QuicServerConnection s = server;

        // The peer (server) has announced reset_stream_at.
        Assert.That(client.PeerTransportParameters!.PeerSupportsResetStreamAt, Is.True);

        // The client sends 12 bytes on a bidi stream and then aborts with reliable size 4.
        QuicStream clientStream = client.OpenBidirectionalStream();
        byte[] payload = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };
        clientStream.Write(payload);
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        clientStream.ResetAt(0x0c, reliableSize: 4);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.False);
        Assert.That(client.IsClosing, Is.False);

        QuicStream serverStream = server.Streams[clientStream.Id.Value];
        Assert.That(serverStream.IsResetByPeer, Is.True);
        Assert.That(serverStream.PeerResetErrorCode, Is.EqualTo(0x0cUL));
        Assert.That(serverStream.PeerReliableSize, Is.EqualTo(4UL));
        // The reliably promised first 4 bytes are readable despite the reset.
        Assert.That(serverStream.Read(), Is.EqualTo(new byte[] { 10, 11, 12, 13 }));
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
}
